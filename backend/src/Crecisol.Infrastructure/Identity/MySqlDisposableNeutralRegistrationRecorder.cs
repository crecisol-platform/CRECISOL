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
/// Local-only, disposable C05-02 test implementation. The API is NOT wired to
/// this adapter, nor is it authorized for any shared/dedicated production DB.
/// Server-owned key protects both identity-blind fingerprints and AES-GCM
/// ciphertext; never store plain mailboxes, network addresses, or passwords.
/// </summary>
public sealed class MySqlDisposableNeutralRegistrationRecorder :
    ITrustedNeutralRegistrationIntentRecorder, IDisposable
{
    private readonly string dsn;
    private readonly byte[] mailboxKey;
    private readonly byte[] peerKey;
    private readonly byte[] encryptionKey;
    private readonly int keyVersion;
    private bool disposed;

    public MySqlDisposableNeutralRegistrationRecorder(
        string? trustedLocalTestDsn, byte[] serverOwnedMasterKey,
        int serverOwnedKeyVersion)
    {
        if (string.IsNullOrWhiteSpace(trustedLocalTestDsn))
            throw new InvalidOperationException("Local protected registration storage required.");
        ArgumentNullException.ThrowIfNull(serverOwnedMasterKey);
        if (serverOwnedMasterKey.Length != 32 || serverOwnedKeyVersion < 1)
            throw new InvalidOperationException("Registration key/version unavailable.");
        var options = new MySqlConnectionStringBuilder(trustedLocalTestDsn)
        {
            Pooling = false,
            ConnectionTimeout = 5,
            DefaultCommandTimeout = 5
        };
        if (options.Database != "c05_02_disposable" ||
            options.Server is not ("127.0.0.1" or "localhost" or "::1") ||
            options.AllowLoadLocalInfile)
            throw new InvalidOperationException("Only isolated loopback MySQL8.4 fixture permitted.");
        dsn = options.ConnectionString;
        keyVersion = serverOwnedKeyVersion;

        // Domain-separated derived keys; the caller retains ownership of the
        // raw master. No hardcoded secret and no key material in logs or SQL.
        mailboxKey = HMACSHA256.HashData(serverOwnedMasterKey,
            "CRECISOL/C05-02/MAILBOX-FINGERPRINT/V1"u8);
        peerKey = HMACSHA256.HashData(serverOwnedMasterKey,
            "CRECISOL/C05-02/SOCKET-QUOTA/V1"u8);
        encryptionKey = HMACSHA256.HashData(serverOwnedMasterKey,
            "CRECISOL/C05-02/MAILBOX-ENCRYPTION/V1"u8);
    }

    public async Task RecordWithoutEnumerationAsync(
        TenantId verifiedTenant, string canonicalMailbox,
        ReadOnlyMemory<byte> verifiedPeerAddress,
        DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(verifiedTenant);
        ArgumentNullException.ThrowIfNull(canonicalMailbox);
        cancellationToken.ThrowIfCancellationRequested();
        InvitationSnapshot.RequireUtc(nowUtc);
        if (verifiedPeerAddress.Length != 16 ||
            verifiedPeerAddress.Span.IndexOfAnyExcept((byte)0) < 0 ||
            !string.Equals(
                RequestNeutralRegistrationIntent.CanonicalMailboxOrNull(canonicalMailbox),
                canonicalMailbox, StringComparison.Ordinal))
            throw new InvalidOperationException("Only trusted canonical registration inputs accepted.");

        byte[]? email = null;
        byte[]? tenantAndEmail = null;
        byte[]? tenantAndPeer = null;
        byte[]? aad = null;
        byte[]? hash = null;
        byte[]? peerHash = null;
        byte[]? ciphertext = null;
        byte[]? nonce = null;
        byte[]? tag = null;
        try
        {
            var tenantId = MySqlRfc4122Guid.Encode(verifiedTenant.Value);
            email = Encoding.UTF8.GetBytes(canonicalMailbox);
            tenantAndEmail = new byte[tenantId.Length + email.Length];
            tenantId.CopyTo(tenantAndEmail, 0);
            email.CopyTo(tenantAndEmail, tenantId.Length);
            hash = HMACSHA256.HashData(mailboxKey, tenantAndEmail);

            tenantAndPeer = new byte[32];
            tenantId.CopyTo(tenantAndPeer, 0);
            verifiedPeerAddress.Span.CopyTo(tenantAndPeer.AsSpan(16));
            peerHash = HMACSHA256.HashData(peerKey, tenantAndPeer);

            aad = new byte[52]; // tenant(16) + keyVersion(4) + hash(32)
            tenantId.CopyTo(aad, 0);
            BinaryPrimitives.WriteInt32BigEndian(aad.AsSpan(16, 4), keyVersion);
            hash.CopyTo(aad, 20);
            ciphertext = new byte[email.Length];
            nonce = RandomNumberGenerator.GetBytes(12);
            tag = new byte[16];
            using (var aes = new AesGcm(encryptionKey, 16))
                aes.Encrypt(nonce, email, ciphertext, tag, aad);

            await using var db = new MySqlConnection(dsn);
            await db.OpenAsync(cancellationToken);
            await using var command = new MySqlCommand(
                "c05_02_record_registration_intent", db)
            {
                CommandType = CommandType.StoredProcedure
            };
            AddBinary(command, "p_tenant", tenantId);
            AddBinary(command, "p_intent_id",
                MySqlRfc4122Guid.Encode(Guid.NewGuid()));
            AddBinary(command, "p_client_digest", peerHash);
            AddBinary(command, "p_mailbox_digest", hash);
            AddBinary(command, "p_nonce", nonce);
            AddBinary(command, "p_tag", tag);
            command.Parameters.Add("p_ciphertext", MySqlDbType.VarBinary).Value =
                ciphertext;
            command.Parameters.Add("p_key_version", MySqlDbType.Int32).Value = keyVersion;
            command.Parameters.Add("p_created_utc", MySqlDbType.DateTime).Value =
                nowUtc.UtcDateTime;
            command.Parameters.Add("p_expires_utc", MySqlDbType.DateTime).Value =
                nowUtc.AddMinutes(30).UtcDateTime;

            var result = await command.ExecuteScalarAsync(cancellationToken);
            if (result is not string resultCode || resultCode != "ACK")
                throw new InvalidOperationException("Registration ingestion unavailable.");
            // No account directory lookup; no existence, throttle or tenant
            // state response to the anonymous caller, even when quota reached.
        }
        finally
        {
            foreach (var secret in new[] {
                email, tenantAndEmail, tenantAndPeer, aad, hash, peerHash,
                ciphertext, nonce, tag })
                if (secret is not null) CryptographicOperations.ZeroMemory(secret);
        }
    }

    private static void AddBinary(
        MySqlCommand command, string name, byte[] value) =>
        command.Parameters.Add(name, MySqlDbType.Binary).Value = value;

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        CryptographicOperations.ZeroMemory(mailboxKey);
        CryptographicOperations.ZeroMemory(peerKey);
        CryptographicOperations.ZeroMemory(encryptionKey);
    }
}
