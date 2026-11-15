using System.Data;
using System.Security.Cryptography;
using System.Text;
using Crecisol.Application.Identity;
using Crecisol.Application.Tenancy;
using Crecisol.Domain.Identity;
using Crecisol.Domain.Tenancy;
using Crecisol.Infrastructure.Tenancy;
using MySqlConnector;

namespace Crecisol.Infrastructure.Identity;

/// <summary>UTC source. It never reads browser time.</summary>
public sealed class SystemHostUtcClock : ITrustedHostUtcClock
{
    public DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow;
}

/// <summary>
/// Exact, server-owned read adapters for the shared C04 tenant/host catalog.
/// All query parameters are backend-derived; duplicate rows fail closed.
/// </summary>
public sealed class MySqlOperationalIdentityCatalog(string? trustedDsn) :
    ITrustedTenantCatalogReader, ITrustedVerifiedHostCatalog
{
    public async Task<TenantCatalogSnapshot?> FindByIdAsync(
        TenantId id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        await using var db = await OpenAsync(cancellationToken);
        await using var cmd = new MySqlCommand("""
            SELECT internal_name, display_name, lifecycle_state, address_mode,
                   catalog_revision, updated_at_utc
              FROM c04_tenant_catalog WHERE tenant_id=@tenant LIMIT 2
            """, db);
        cmd.Parameters.Add("@tenant", MySqlDbType.Binary).Value = MySqlRfc4122Guid.Encode(id.Value);
        await using var rows = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await rows.ReadAsync(cancellationToken)) return null;
        var snapshot = TenantCatalogSnapshot.RestoreFromTrustedCatalog(
            id, rows.GetString(0), rows.GetString(1),
            (TenantLifecycleState)rows.GetByte(2), (TenantAddressMode)rows.GetByte(3),
            checked((long)rows.GetUInt64(4)), Utc(rows.GetDateTime(5)));
        if (await rows.ReadAsync(cancellationToken))
            throw new InvalidOperationException("Duplicate tenant catalog key.");
        return snapshot;
    }

    public async Task<IReadOnlyList<VerifiedTenantHostBinding>?> FindExactBindingsAsync(
        CanonicalTenantHost host, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(host);
        await using var db = await OpenAsync(cancellationToken);
        await using var cmd = new MySqlCommand("""
            SELECT tenant_id, address_mode, verification_state, host_revision,
                   changed_at_utc, verified_at_utc, valid_until_utc
              FROM c04_verified_tenant_host
             WHERE canonical_host=@host LIMIT 2
            """, db);
        cmd.Parameters.Add("@host", MySqlDbType.VarChar, 253).Value = host.Value;
        var matches = new List<VerifiedTenantHostBinding>(2);
        await using var rows = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await rows.ReadAsync(cancellationToken))
        {
            var tenant = TenantId.From(MySqlRfc4122Guid.Decode((byte[])rows.GetValue(0)));
            DateTimeOffset? verified = rows.IsDBNull(5) ? null : Utc(rows.GetDateTime(5));
            DateTimeOffset? expires = rows.IsDBNull(6) ? null : Utc(rows.GetDateTime(6));
            matches.Add(VerifiedTenantHostBinding.RestoreFromTrustedCatalog(
                tenant, host, (TenantAddressMode)rows.GetByte(1),
                (HostVerificationState)rows.GetByte(2), checked((long)rows.GetUInt64(3)),
                Utc(rows.GetDateTime(4)), verified, expires));
        }
        return matches;
    }

    private async Task<MySqlConnection> OpenAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(trustedDsn))
            throw new InvalidOperationException("Identity catalog storage is not configured.");
        var builder = new MySqlConnectionStringBuilder(trustedDsn)
        {
            ConnectionTimeout = 3, DefaultCommandTimeout = 3,
            Pooling = true, MaximumPoolSize = 20
        };
        var db = new MySqlConnection(builder.ConnectionString);
        try { await db.OpenAsync(ct); return db; }
        catch { await db.DisposeAsync(); throw; }
    }

    private static DateTimeOffset Utc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}

/// <summary>
/// Persistent neutral registration writer. Mailbox and peer are separately
/// HMAC-scoped; the mailbox is AEAD encrypted. Quota reservation and intent
/// insert share one transaction, with an eight-per-minute atomic peer budget.
/// </summary>
public sealed class MySqlOperationalNeutralRegistrationRecorder :
    ITrustedNeutralRegistrationIntentRecorder, IDisposable
{
    private readonly string? dsn;
    private readonly int keyVersion;
    private readonly byte[] mailboxHmacKey;
    private readonly byte[] peerHmacKey;
    private readonly byte[] mailboxEncryptionKey;
    private readonly byte[] proofOutboxEncryptionKey;
    private bool disposed;

    public MySqlOperationalNeutralRegistrationRecorder(
        string? dsn, int keyVersion, ReadOnlySpan<byte> mailboxHmacKey,
        ReadOnlySpan<byte> peerHmacKey, ReadOnlySpan<byte> mailboxEncryptionKey,
        ReadOnlySpan<byte> proofOutboxEncryptionKey)
    {
        if (keyVersion < 1 || mailboxHmacKey.Length != 32 ||
            peerHmacKey.Length != 32 || mailboxEncryptionKey.Length != 32 ||
            proofOutboxEncryptionKey.Length != 32)
            throw new InvalidOperationException("Independent 256-bit identity keys are required.");
        this.dsn = dsn;
        this.keyVersion = keyVersion;
        this.mailboxHmacKey = mailboxHmacKey.ToArray();
        this.peerHmacKey = peerHmacKey.ToArray();
        this.mailboxEncryptionKey = mailboxEncryptionKey.ToArray();
        this.proofOutboxEncryptionKey = proofOutboxEncryptionKey.ToArray();
    }

    public async Task RecordWithoutEnumerationAsync(
        TenantId verifiedTenant, string canonicalMailbox,
        ReadOnlyMemory<byte> verifiedPeerAddress, DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(verifiedTenant);
        cancellationToken.ThrowIfCancellationRequested();
        if (nowUtc.Offset != TimeSpan.Zero || verifiedPeerAddress.Length != 16 ||
            RequestNeutralRegistrationIntent.CanonicalMailboxOrNull(canonicalMailbox) != canonicalMailbox)
            throw new InvalidOperationException("Trusted canonical registration context required.");

        var intentId = Guid.NewGuid();
        byte[]? tenant = null, mailbox = null, peer = null, aad = null;
        byte[]? mailboxDigest = null, peerDigest = null, nonce = null, tag = null, cipher = null;
        byte[]? proofToken = null, proofDigest = null, proofAad = null;
        byte[]? outboxNonce = null, outboxTag = null, outboxCipher = null;
        try
        {
            tenant = MySqlRfc4122Guid.Encode(verifiedTenant.Value);
            mailbox = Encoding.UTF8.GetBytes(canonicalMailbox);
            peer = new byte[32];
            tenant.CopyTo(peer, 0);
            verifiedPeerAddress.Span.CopyTo(peer.AsSpan(16));
            peerDigest = HMACSHA256.HashData(peerHmacKey, peer);
            var mailboxMaterial = new byte[tenant.Length + mailbox.Length];
            tenant.CopyTo(mailboxMaterial, 0);
            mailbox.CopyTo(mailboxMaterial, tenant.Length);
            mailboxDigest = HMACSHA256.HashData(mailboxHmacKey, mailboxMaterial);
            CryptographicOperations.ZeroMemory(mailboxMaterial);

            aad = new byte[52];
            tenant.CopyTo(aad, 0);
            System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(aad.AsSpan(16, 4), keyVersion);
            mailboxDigest.CopyTo(aad, 20);
            nonce = RandomNumberGenerator.GetBytes(12);
            tag = new byte[16];
            cipher = new byte[mailbox.Length];
            using (var aes = new AesGcm(mailboxEncryptionKey, 16))
                aes.Encrypt(nonce, mailbox, cipher, tag, aad);

            proofToken = RandomNumberGenerator.GetBytes(32);
            proofDigest = EmailPossessionDigest.FromToken(verifiedTenant, intentId, proofToken)
                .CopyForProtectedDatabase();
            proofAad = CreateProofOutboxAad(tenant, intentId, proofDigest, keyVersion);
            outboxNonce = RandomNumberGenerator.GetBytes(12);
            outboxTag = new byte[16];
            outboxCipher = new byte[proofToken.Length];
            using (var aes = new AesGcm(proofOutboxEncryptionKey, 16))
                aes.Encrypt(outboxNonce, proofToken, outboxCipher, outboxTag, proofAad);

            if (string.IsNullOrWhiteSpace(dsn))
                throw new InvalidOperationException("Registration storage is not configured.");
            var options = new MySqlConnectionStringBuilder(dsn)
            {
                ConnectionTimeout = 3, DefaultCommandTimeout = 3, Pooling = true
            };
            await using var db = new MySqlConnection(options.ConnectionString);
            await db.OpenAsync(cancellationToken);
            await using var tx = await db.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
            var window = new DateTime(nowUtc.Year, nowUtc.Month, nowUtc.Day,
                nowUtc.Hour, nowUtc.Minute, 0, DateTimeKind.Utc);
            await using (var reserve = new MySqlCommand("""
                INSERT INTO c05_identity_registration_quota
                  (tenant_id, peer_hmac, window_start_utc, admitted_count)
                VALUES (@tenant,@peer,@window,0)
                ON DUPLICATE KEY UPDATE admitted_count=admitted_count
                """, db, (MySqlTransaction)tx))
            {
                reserve.Parameters.Add("@tenant", MySqlDbType.Binary).Value = tenant;
                reserve.Parameters.Add("@peer", MySqlDbType.Binary).Value = peerDigest;
                reserve.Parameters.Add("@window", MySqlDbType.DateTime).Value = window;
                await reserve.ExecuteNonQueryAsync(cancellationToken);
            }
            await using (var check = new MySqlCommand("""
                SELECT admitted_count FROM c05_identity_registration_quota
                 WHERE tenant_id=@tenant AND peer_hmac=@peer AND window_start_utc=@window FOR UPDATE
                """, db, (MySqlTransaction)tx))
            {
                check.Parameters.Add("@tenant", MySqlDbType.Binary).Value = tenant;
                check.Parameters.Add("@peer", MySqlDbType.Binary).Value = peerDigest;
                check.Parameters.Add("@window", MySqlDbType.DateTime).Value = window;
                var count = Convert.ToUInt16(await check.ExecuteScalarAsync(cancellationToken));
                if (count < 8)
                {
                    await using (var increment = new MySqlCommand("""
                        UPDATE c05_identity_registration_quota SET admitted_count=admitted_count+1
                         WHERE tenant_id=@tenant AND peer_hmac=@peer AND window_start_utc=@window
                           AND admitted_count<8
                        """, db, (MySqlTransaction)tx))
                    {
                        increment.Parameters.Add("@tenant", MySqlDbType.Binary).Value = tenant;
                        increment.Parameters.Add("@peer", MySqlDbType.Binary).Value = peerDigest;
                        increment.Parameters.Add("@window", MySqlDbType.DateTime).Value = window;
                        if (await increment.ExecuteNonQueryAsync(cancellationToken) != 1)
                            throw new InvalidOperationException("Registration quota reservation was lost.");
                    }
                    await using var insert = new MySqlCommand("""
                        INSERT INTO c05_identity_registration_intent
                          (tenant_id,intent_id,mailbox_hmac,peer_hmac,mailbox_ciphertext,
                           mailbox_nonce,mailbox_tag,key_version,state,created_at_utc,expires_at_utc)
                        VALUES (@tenant,@intent,@mailbox,@peer,@cipher,@nonce,@tag,@version,0,@created,@expires)
                        """, db, (MySqlTransaction)tx);
                    insert.Parameters.Add("@tenant", MySqlDbType.Binary).Value = tenant;
                    insert.Parameters.Add("@intent", MySqlDbType.Binary).Value = MySqlRfc4122Guid.Encode(intentId);
                    insert.Parameters.Add("@mailbox", MySqlDbType.Binary).Value = mailboxDigest;
                    insert.Parameters.Add("@peer", MySqlDbType.Binary).Value = peerDigest;
                    insert.Parameters.Add("@cipher", MySqlDbType.VarBinary).Value = cipher;
                    insert.Parameters.Add("@nonce", MySqlDbType.Binary).Value = nonce;
                    insert.Parameters.Add("@tag", MySqlDbType.Binary).Value = tag;
                    insert.Parameters.Add("@version", MySqlDbType.Int32).Value = keyVersion;
                    insert.Parameters.Add("@created", MySqlDbType.DateTime).Value = nowUtc.UtcDateTime;
                    insert.Parameters.Add("@expires", MySqlDbType.DateTime).Value = nowUtc.AddMinutes(30).UtcDateTime;
                    await insert.ExecuteNonQueryAsync(cancellationToken);

                    await using (var proof = new MySqlCommand(
                        "c05_operational_stage_mailbox_proof", db, (MySqlTransaction)tx)
                    {
                        CommandType = CommandType.StoredProcedure
                    })
                    {
                        proof.Parameters.Add("p_tenant", MySqlDbType.Binary).Value = tenant;
                        proof.Parameters.Add("p_proof", MySqlDbType.Binary).Value = MySqlRfc4122Guid.Encode(intentId);
                        proof.Parameters.Add("p_mailbox", MySqlDbType.Binary).Value = mailboxDigest;
                        proof.Parameters.Add("p_proof_digest", MySqlDbType.Binary).Value = proofDigest;
                        proof.Parameters.Add("p_outbox_cipher", MySqlDbType.Binary).Value = outboxCipher;
                        proof.Parameters.Add("p_outbox_nonce", MySqlDbType.Binary).Value = outboxNonce;
                        proof.Parameters.Add("p_outbox_tag", MySqlDbType.Binary).Value = outboxTag;
                        proof.Parameters.Add("p_version", MySqlDbType.Int32).Value = keyVersion;
                        proof.Parameters.Add("p_created", MySqlDbType.DateTime).Value = nowUtc.UtcDateTime;
                        proof.Parameters.Add("p_proof_expires", MySqlDbType.DateTime).Value = nowUtc.AddMinutes(10).UtcDateTime;
                        proof.Parameters.Add("p_outbox_expires", MySqlDbType.DateTime).Value = nowUtc.AddMinutes(10).UtcDateTime;
                        await proof.ExecuteNonQueryAsync(cancellationToken);
                    }
                }
            }
            await tx.CommitAsync(cancellationToken);
        }
        finally
        {
            foreach (var bytes in new[] { tenant, mailbox, peer, aad, mailboxDigest, peerDigest,
                         nonce, tag, cipher, proofToken, proofDigest, proofAad,
                         outboxNonce, outboxTag, outboxCipher })
                if (bytes is not null) CryptographicOperations.ZeroMemory(bytes);
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        CryptographicOperations.ZeroMemory(mailboxHmacKey);
        CryptographicOperations.ZeroMemory(peerHmacKey);
        CryptographicOperations.ZeroMemory(mailboxEncryptionKey);
        CryptographicOperations.ZeroMemory(proofOutboxEncryptionKey);
    }

    private static byte[] CreateProofOutboxAad(
        ReadOnlySpan<byte> tenant, Guid proofId, ReadOnlySpan<byte> digest, int version)
    {
        var purpose = Encoding.ASCII.GetBytes("CRECISOL/C05/registration-proof-outbox/v1\0");
        var aad = new byte[purpose.Length + 16 + 16 + 4 + 32];
        purpose.CopyTo(aad, 0);
        tenant.CopyTo(aad.AsSpan(purpose.Length, 16));
        MySqlRfc4122Guid.Encode(proofId).CopyTo(aad, purpose.Length + 16);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(
            aad.AsSpan(purpose.Length + 32, 4), version);
        digest.CopyTo(aad.AsSpan(purpose.Length + 36));
        return aad;
    }
}

