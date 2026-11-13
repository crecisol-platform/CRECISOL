using System.Buffers.Binary;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using Crecisol.Application.Identity;
using Crecisol.Domain.Identity;
using Crecisol.Domain.Tenancy;
using Crecisol.Infrastructure.Tenancy;
using MySqlConnector;

namespace Crecisol.Infrastructure.Identity;

/// <summary>
/// An authenticated LOCAL TEST mailbox sink, never SMTP/production delivery.
/// The channel destination comes from a decrypted protected intent, never from
/// an HTTP request. The sink MUST NOT persist tokens or report them to clients.
/// </summary>
public interface ITrustedLocalProofMailboxSink
{
    Task DeliverInTestOnlyAsync(
        string storedCanonicalMailbox, ReadOnlyMemory<byte> capability,
        CancellationToken cancellationToken);
}

/// <summary>
/// C05-02 disposable integration of independent proof ledger and encrypted
/// destination lookup. Only local, isolated fixture DB is permitted.
/// There is NO production API/DI or real SMTP binding.
/// </summary>
public sealed class MySqlDisposableEmailPossessionAdapter :
    ITrustedEmailPossessionLedger, ITrustedEmailPossessionDelivery, IDisposable
{
    private readonly string dsn;
    private readonly byte[] encryptionKey;
    private readonly int keyVersion;
    private readonly ITrustedLocalProofMailboxSink sink;
    private bool disposed;

    public MySqlDisposableEmailPossessionAdapter(
        string? trustedServerTestDsn, byte[] serverOwnedMasterKey,
        int activeKeyVersion, ITrustedLocalProofMailboxSink localMailboxSink)
    {
        ArgumentNullException.ThrowIfNull(serverOwnedMasterKey);
        sink = localMailboxSink ?? throw new ArgumentNullException(nameof(localMailboxSink));
        if (serverOwnedMasterKey.Length != 32 || activeKeyVersion <= 0 ||
            string.IsNullOrWhiteSpace(trustedServerTestDsn))
            throw new InvalidOperationException("Protected mailbox proof configuration unavailable.");

        var options = new MySqlConnectionStringBuilder(trustedServerTestDsn)
        {
            Pooling = false,
            ConnectionTimeout = 5,
            DefaultCommandTimeout = 8
        };
        if (options.Database != "c05_02_disposable" ||
            options.Server is not ("127.0.0.1" or "localhost" or "::1") ||
            options.AllowLoadLocalInfile)
            throw new InvalidOperationException("Mailbox proof must use local disposable MySQL only.");
        dsn = options.ConnectionString;
        keyVersion = activeKeyVersion;
        encryptionKey = HMACSHA256.HashData(
            serverOwnedMasterKey, "CRECISOL/C05-02/MAILBOX-ENCRYPTION/V1"u8);
    }

    public async Task<bool> TryIssueOnceAsync(
        TenantId tenant, Guid intentId, EmailPossessionDigest digest,
        DateTimeOffset nowUtc, DateTimeOffset expiresUtc,
        CancellationToken cancellationToken)
    {
        CheckInput(tenant, intentId, digest, cancellationToken);
        InvitationSnapshot.RequireUtc(nowUtc);
        InvitationSnapshot.RequireUtc(expiresUtc);
        if (expiresUtc <= nowUtc || expiresUtc - nowUtc > TimeSpan.FromMinutes(10))
            throw new InvalidOperationException("Email proof lifetime out of bounds.");
        var result = await CallProofProcedureAsync(
            "c05_02_mailbox_proof_issue", tenant, intentId, digest,
            nowUtc, expiresUtc, cancellationToken);
        return result switch
        {
            "ISSUED" => true,
            "DENIED" => false,
            _ => throw new InvalidOperationException("Unexpected issue result.")
        };
    }

    public async Task<bool> TryVerifyOnceAsync(
        TenantId tenant, Guid intentId, EmailPossessionDigest suppliedDigest,
        DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        CheckInput(tenant, intentId, suppliedDigest, cancellationToken);
        InvitationSnapshot.RequireUtc(nowUtc);
        var result = await CallProofProcedureAsync(
            "c05_02_mailbox_proof_verify", tenant, intentId, suppliedDigest,
            nowUtc, null, cancellationToken);
        return result switch
        {
            "VERIFIED" => true,
            "DENIED" => false,
            _ => throw new InvalidOperationException("Unexpected verification result.")
        };
    }

    public async Task<bool> TryRevokeUndeliveredAsync(
        TenantId tenant, Guid intentId, EmailPossessionDigest expectedDigest,
        CancellationToken cancellationToken)
    {
        CheckInput(tenant, intentId, expectedDigest, cancellationToken);
        var result = await CallProofProcedureAsync(
            "c05_02_mailbox_proof_revoke", tenant, intentId, expectedDigest,
            null, null, cancellationToken);
        return result switch
        {
            "REVOKED" => true,
            "DENIED" => false,
            _ => throw new InvalidOperationException("Unexpected revocation result.")
        };
    }

    private async Task<string> CallProofProcedureAsync(
        string procedure, TenantId tenant, Guid intentId, EmailPossessionDigest digest,
        DateTimeOffset? nowUtc, DateTimeOffset? expiresUtc, CancellationToken ct)
    {
        var storedDigest = digest.CopyForProtectedDatabase();
        try
        {
            await using var db = new MySqlConnection(dsn);
            await db.OpenAsync(ct);
            await using var command = new MySqlCommand(procedure, db)
            {
                CommandType = CommandType.StoredProcedure
            };
            AddBinary(command, "p_tenant", MySqlRfc4122Guid.Encode(tenant.Value));
            AddBinary(command, "p_intent", MySqlRfc4122Guid.Encode(intentId));
            AddBinary(command, "p_digest", storedDigest);
            if (procedure == "c05_02_mailbox_proof_issue")
            {
                command.Parameters.Add("p_issued", MySqlDbType.DateTime).Value =
                    nowUtc!.Value.UtcDateTime;
                command.Parameters.Add("p_expires", MySqlDbType.DateTime).Value =
                    expiresUtc!.Value.UtcDateTime;
            }
            else if (procedure == "c05_02_mailbox_proof_verify")
                command.Parameters.Add("p_now", MySqlDbType.DateTime).Value =
                    nowUtc!.Value.UtcDateTime;
            var result = await command.ExecuteScalarAsync(ct);
            return result as string ??
                throw new InvalidOperationException("Protected proof procedure returned no result.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(storedDigest);
        }
    }

    public async Task DeliverToRecordedMailboxAsync(
        TenantId tenant, Guid intentId, ReadOnlyMemory<byte> secret,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(tenant);
        cancellationToken.ThrowIfCancellationRequested();
        if (intentId == Guid.Empty || secret.Length != 32)
            throw new InvalidOperationException("Bound ephemeral email proof required.");

        await using var db = new MySqlConnection(dsn);
        await db.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand("""
            SELECT nonce,tag,ciphertext,key_version,mailbox_digest,expires_at_utc
              FROM c05_02_neutral_intents
             WHERE tenant_id=@tenant AND intent_id=@intent LIMIT 2
            """, db);
        AddBinary(command, "@tenant", MySqlRfc4122Guid.Encode(tenant.Value));
        AddBinary(command, "@intent", MySqlRfc4122Guid.Encode(intentId));
        await using var rows = await command.ExecuteReaderAsync(cancellationToken);
        if (!await rows.ReadAsync(cancellationToken))
            throw new InvalidOperationException("Protected mailbox destination missing.");
        var nonce = (byte[])rows.GetValue(0);
        var tag = (byte[])rows.GetValue(1);
        var cipher = (byte[])rows.GetValue(2);
        var version = rows.GetInt32(3);
        var digest = (byte[])rows.GetValue(4);
        var expiry = rows.GetDateTime(5);
        var aad = new byte[52];
        var plaintext = new byte[cipher.Length];
        try
        {
            if (nonce.Length != 12 || tag.Length != 16 || digest.Length != 32 ||
                version != keyVersion || expiry <= DateTime.UtcNow ||
                cipher.Length is < 6 or > 254)
                throw new InvalidOperationException("Protected mailbox envelope invalid.");
            if (await rows.ReadAsync(cancellationToken))
                throw new InvalidOperationException("Ambiguous protected destination.");

            MySqlRfc4122Guid.Encode(tenant.Value).CopyTo(aad, 0);
            BinaryPrimitives.WriteInt32BigEndian(aad.AsSpan(16, 4), version);
            digest.CopyTo(aad, 20);
            using (var aes = new AesGcm(encryptionKey, 16))
                aes.Decrypt(nonce, cipher, tag, plaintext, aad);
            var storedMailbox = Encoding.UTF8.GetString(plaintext);
            if (storedMailbox != RequestNeutralRegistrationIntent.CanonicalMailboxOrNull(
                    storedMailbox))
                throw new InvalidOperationException("Invalid trusted delivery mailbox.");
            await sink.DeliverInTestOnlyAsync(
                storedMailbox, secret, cancellationToken);
        }
        finally
        {
            foreach (var buffer in new[] { nonce, tag, cipher, digest, aad, plaintext })
                CryptographicOperations.ZeroMemory(buffer);
        }
    }

    private void CheckInput(
        TenantId tenant, Guid intent, EmailPossessionDigest digest, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(tenant);
        ArgumentNullException.ThrowIfNull(digest);
        if (intent == Guid.Empty)
            throw new InvalidOperationException("Opaque intent ID required.");
        ct.ThrowIfCancellationRequested();
    }

    private static void AddBinary(MySqlCommand command, string key, byte[] raw) =>
        command.Parameters.Add(key, MySqlDbType.Binary).Value = raw;

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        CryptographicOperations.ZeroMemory(encryptionKey);
    }
}
