using System.Buffers.Binary;
using System.Data;
using System.Security.Cryptography;
using Crecisol.Application.Identity;
using Crecisol.Domain.Identity;
using Crecisol.Domain.Tenancy;
using Crecisol.Infrastructure.Tenancy;
using MySqlConnector;

namespace Crecisol.Infrastructure.Identity;

/// <summary>
/// C05-02 TEST-ONLY durable encrypted mailbox-proof delivery outbox.
/// There is no DI registration or route in the production API. This class
/// accepts ONLY a server-verified tenant and opaque intent, and it can only
/// connect to an isolated local MySQL fixture. It never creates grants.
/// </summary>
public sealed class MySqlDisposableProofDeliveryOutbox : IDisposable
{
    private readonly string dsn;
    private readonly byte[] encryptionKey;
    private readonly int keyVersion;
    private readonly ITrustedEmailPossessionDelivery trustedDelivery;
    private bool disposed;

    public MySqlDisposableProofDeliveryOutbox(
        string? trustedLocalTestDsn, byte[] serverOwnedMasterKey,
        int activeKeyVersion, ITrustedEmailPossessionDelivery delivery)
    {
        ArgumentNullException.ThrowIfNull(serverOwnedMasterKey);
        trustedDelivery = delivery ?? throw new ArgumentNullException(nameof(delivery));
        if (string.IsNullOrWhiteSpace(trustedLocalTestDsn) ||
            serverOwnedMasterKey.Length != 32 || activeKeyVersion <= 0)
            throw new InvalidOperationException("Protected local delivery configuration unavailable.");
        var options = new MySqlConnectionStringBuilder(trustedLocalTestDsn)
        {
            Pooling = false,
            ConnectionTimeout = 5,
            DefaultCommandTimeout = 8
        };
        if (options.Database != "c05_02_disposable" ||
            options.Server is not ("127.0.0.1" or "localhost" or "::1") ||
            options.AllowLoadLocalInfile)
            throw new InvalidOperationException("Outbox requires isolated loopback MySQL fixture.");
        dsn = options.ConnectionString;
        keyVersion = activeKeyVersion;
        encryptionKey = HMACSHA256.HashData(
            serverOwnedMasterKey, "CRECISOL/C05-02/PROOF-OUTBOX-ENCRYPTION/V1"u8);
    }

    /// <summary>
    /// Atomically persist a fresh one-purpose proof digest and its encrypted
    /// delivery code. No bearer is persisted in plaintext or returned to HTTP.
    /// An existing challenge (including revoked) is NEVER overwritten.
    /// </summary>
    public async Task<bool> StageOnceAsync(
        TenantId verifiedTenant, Guid storedIntentId,
        DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        Check(verifiedTenant, storedIntentId, cancellationToken);
        InvitationSnapshot.RequireUtc(nowUtc);
        using var challenge = EphemeralEmailPossessionCode.Generate();
        var digest = challenge.CreateDigest(verifiedTenant, storedIntentId);
        var raw = challenge.TransferToTrustedDeliveryOnce();
        var storedDigest = digest.CopyForProtectedDatabase();
        var nonce = RandomNumberGenerator.GetBytes(12);
        var tag = new byte[16];
        var ciphertext = new byte[32];
        var aad = CreateAad(verifiedTenant, storedIntentId, storedDigest, keyVersion);
        try
        {
            using (var aes = new AesGcm(encryptionKey, 16))
                aes.Encrypt(nonce, raw, ciphertext, tag, aad);
            await using var db = new MySqlConnection(dsn);
            await db.OpenAsync(cancellationToken);
            await using var command = new MySqlCommand("c05_02_outbox_issue", db)
            {
                CommandType = CommandType.StoredProcedure
            };
            AddBinary(command, "p_tenant", MySqlRfc4122Guid.Encode(verifiedTenant.Value));
            AddBinary(command, "p_intent", MySqlRfc4122Guid.Encode(storedIntentId));
            AddBinary(command, "p_digest", storedDigest);
            AddBinary(command, "p_nonce", nonce);
            AddBinary(command, "p_tag", tag);
            AddBinary(command, "p_ciphertext", ciphertext);
            command.Parameters.Add("p_key_version", MySqlDbType.Int32).Value = keyVersion;
            command.Parameters.Add("p_issued", MySqlDbType.DateTime).Value = nowUtc.UtcDateTime;
            command.Parameters.Add("p_expires", MySqlDbType.DateTime).Value =
                nowUtc.AddMinutes(10).UtcDateTime;
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result switch
            {
                "ISSUED" => true,
                "DENIED" => false,
                _ => throw new InvalidOperationException("Unexpected protected outbox issue result.")
            };
        }
        finally
        {
            foreach (var buffer in new[] { raw, storedDigest, nonce, tag, ciphertext, aad })
                CryptographicOperations.ZeroMemory(buffer);
        }
    }

    /// <summary>
    /// Claims an exclusive time-limited lease from the DB, decrypts the stored
    /// bearer only in process memory, verifies purpose/tenant/intent digest,
    /// then uses the trusted local delivery port. Provider delivery is not
    /// exactly-once: after a crash it may repeat the SAME code, max 3 leases.
    /// Any observed failure revokes pending proof and outbox atomically.
    /// </summary>
    public async Task<bool> ProcessOnceAsync(
        TenantId trustedTenant, Guid storedIntentId,
        CancellationToken cancellationToken)
    {
        Check(trustedTenant, storedIntentId, cancellationToken);
        var lease = RandomNumberGenerator.GetBytes(16);
        EncryptedLease? envelope = null;
        byte[]? raw = null;
        byte[]? aad = null;
        byte[]? computedDigest = null;
        var claimed = false;
        try
        {
            envelope = await ClaimAsync(trustedTenant, storedIntentId, lease, cancellationToken);
            if (envelope is null) return false;
            claimed = true;
            if (envelope.KeyVersion != keyVersion ||
                envelope.Nonce.Length != 12 || envelope.Tag.Length != 16 ||
                envelope.Ciphertext.Length != 32 || envelope.Digest.Length != 32)
                throw new InvalidOperationException("Protected outbox envelope invalid.");
            raw = new byte[32];
            aad = CreateAad(trustedTenant, storedIntentId, envelope.Digest, keyVersion);
            using (var aes = new AesGcm(encryptionKey, 16))
                aes.Decrypt(envelope.Nonce, envelope.Ciphertext, envelope.Tag, raw, aad);
            computedDigest = EmailPossessionDigest
                .FromToken(trustedTenant, storedIntentId, raw)
                .CopyForProtectedDatabase();
            if (!CryptographicOperations.FixedTimeEquals(computedDigest, envelope.Digest))
                throw new InvalidOperationException("Protected delivery digest mismatch.");

            // This port resolves and authenticates the recipient from the
            // stored AES-GCM intent. It NEVER accepts a client mailbox.
            if (await FinishAsync("c05_02_outbox_can_dispatch",
                    trustedTenant, storedIntentId, lease, cancellationToken) != "ALLOW")
            {
                await BestEffortRevokeAsync(trustedTenant, storedIntentId, lease);
                return false;
            }
            await trustedDelivery.DeliverToRecordedMailboxAsync(
                trustedTenant, storedIntentId, raw, cancellationToken);
            if (await FinishAsync("c05_02_outbox_mark_sent",
                    trustedTenant, storedIntentId, lease, cancellationToken) != "SENT")
            {
                await BestEffortRevokeAsync(trustedTenant, storedIntentId, lease);
                return false;
            }
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (claimed)
                await BestEffortRevokeAsync(trustedTenant, storedIntentId, lease);
            throw;
        }
        catch (Exception)
        {
            if (claimed)
                await BestEffortRevokeAsync(trustedTenant, storedIntentId, lease);
            return false;
        }
        finally
        {
            foreach (var buffer in new[] { lease, raw, aad, computedDigest,
                         envelope?.Digest, envelope?.Nonce, envelope?.Tag, envelope?.Ciphertext })
                if (buffer is not null) CryptographicOperations.ZeroMemory(buffer);
        }
    }

    private async Task<EncryptedLease?> ClaimAsync(
        TenantId tenant, Guid intentId, byte[] lease, CancellationToken ct)
    {
        await using var db = new MySqlConnection(dsn);
        await db.OpenAsync(ct);
        await using var command = new MySqlCommand("c05_02_outbox_claim", db)
        {
            CommandType = CommandType.StoredProcedure
        };
        AddBinary(command, "p_tenant", MySqlRfc4122Guid.Encode(tenant.Value));
        AddBinary(command, "p_intent", MySqlRfc4122Guid.Encode(intentId));
        AddBinary(command, "p_lease", lease);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        var result = new EncryptedLease(
            (byte[])reader.GetValue(0), (byte[])reader.GetValue(1),
            (byte[])reader.GetValue(2), (byte[])reader.GetValue(3),
            reader.GetInt32(4));
        if (await reader.ReadAsync(ct))
        {
            result.Clear();
            throw new InvalidOperationException("Ambiguous protected delivery lease.");
        }
        return result;
    }

    private async Task<string> FinishAsync(
        string procedure, TenantId tenant, Guid intentId, byte[] lease, CancellationToken ct)
    {
        await using var db = new MySqlConnection(dsn);
        await db.OpenAsync(ct);
        await using var command = new MySqlCommand(procedure, db)
        {
            CommandType = CommandType.StoredProcedure
        };
        AddBinary(command, "p_tenant", MySqlRfc4122Guid.Encode(tenant.Value));
        AddBinary(command, "p_intent", MySqlRfc4122Guid.Encode(intentId));
        AddBinary(command, "p_lease", lease);
        return await command.ExecuteScalarAsync(ct) as string ??
            throw new InvalidOperationException("Protected delivery state unavailable.");
    }

    private async Task BestEffortRevokeAsync(TenantId tenant, Guid intentId, byte[] lease)
    {
        try
        {
            await FinishAsync("c05_02_outbox_mark_failed",
                tenant, intentId, lease, CancellationToken.None);
        }
        catch (Exception)
        {
            // Fail closed: the challenge still expires and no authentication,
            // membership or access token can be created by this component.
        }
    }

    private static byte[] CreateAad(
        TenantId tenant, Guid intentId, byte[] protectedDigest, int version)
    {
        var aad = new byte[68];
        MySqlRfc4122Guid.Encode(tenant.Value).CopyTo(aad, 0);
        MySqlRfc4122Guid.Encode(intentId).CopyTo(aad, 16);
        protectedDigest.CopyTo(aad, 32);
        BinaryPrimitives.WriteInt32BigEndian(aad.AsSpan(64, 4), version);
        return aad;
    }

    private void Check(TenantId tenant, Guid intentId, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(tenant);
        if (intentId == Guid.Empty)
            throw new InvalidOperationException("Trusted opaque intent required.");
        ct.ThrowIfCancellationRequested();
    }

    private static void AddBinary(MySqlCommand command, string name, byte[] bytes) =>
        command.Parameters.Add(name, MySqlDbType.Binary).Value = bytes;

    private sealed record EncryptedLease(
        byte[] Digest, byte[] Nonce, byte[] Tag, byte[] Ciphertext, int KeyVersion)
    {
        public void Clear()
        {
            foreach (var bytes in new[] { Digest, Nonce, Tag, Ciphertext })
                CryptographicOperations.ZeroMemory(bytes);
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        CryptographicOperations.ZeroMemory(encryptionKey);
    }
}
