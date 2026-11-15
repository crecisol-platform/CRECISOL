using System.Buffers.Binary;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using Crecisol.Application.Identity;
using Crecisol.Domain.Tenancy;
using Crecisol.Infrastructure.Tenancy;
using MySqlConnector;

namespace Crecisol.Infrastructure.Identity;

/// <summary>
/// Local disposable C05 fixture ONLY. Produces a globally unique PENDING
/// candidate, never an active account, membership, role, MFA or session.
/// Reads the mailbox exclusively from a protected verified-tenant intent.
/// </summary>
public sealed class MySqlDisposableVerifiedCredentialCandidateStore : IDisposable
{
    private readonly string dsn;
    private readonly byte[] encryptionKey;
    private readonly byte[] loginKey;
    private readonly int version;
    private bool disposed;

    public MySqlDisposableVerifiedCredentialCandidateStore(
        string? trustedDsn, byte[] trustedMaster, int keyVersion)
    {
        ArgumentNullException.ThrowIfNull(trustedMaster);
        if (trustedMaster.Length != 32 || keyVersion < 1 ||
            string.IsNullOrWhiteSpace(trustedDsn))
            throw new InvalidOperationException("Protected local candidate store required.");
        var config = new MySqlConnectionStringBuilder(trustedDsn)
        {
            Pooling = false, ConnectionTimeout = 5, DefaultCommandTimeout = 15
        };
        if (config.Database != "c05_02_disposable" ||
            config.Server is not ("127.0.0.1" or "localhost" or "::1") ||
            config.AllowLoadLocalInfile)
            throw new InvalidOperationException("Only disposable local MySQL permitted.");
        dsn = config.ConnectionString;
        version = keyVersion;
        encryptionKey = HMACSHA256.HashData(
            trustedMaster, "CRECISOL/C05-02/MAILBOX-ENCRYPTION/V1"u8);
        loginKey = HMACSHA256.HashData(
            trustedMaster, "CRECISOL/C05/GLOBAL-LOGIN-KEY/V1"u8);
    }

    public async Task<bool> TryCreatePendingAsync(
        TenantId verifiedTenant, Guid intent, string trustedPhc, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(verifiedTenant);
        ct.ThrowIfCancellationRequested();
        if (intent == Guid.Empty || string.IsNullOrEmpty(trustedPhc) ||
            trustedPhc.Length is < 80 or > 160 ||
            !trustedPhc.StartsWith("$argon2id$v=19$", StringComparison.Ordinal) ||
            trustedPhc.Any(ch => ch < 33 || ch > 126))
            return false;

        byte[]? nonce = null, tag = null, encrypted = null, mailboxDigest = null;
        byte[]? aad = null, plaintext = null, canonical = null, globalDigest = null;
        try
        {
            await using var db = new MySqlConnection(dsn);
            await db.OpenAsync(ct);
            await using (var read = new MySqlCommand("""
                SELECT nonce,tag,ciphertext,key_version,mailbox_digest,expires_at_utc
                FROM c05_02_neutral_intents
                WHERE tenant_id=@tenant AND intent_id=@intent LIMIT 2
                """, db))
            {
                read.Parameters.Add("@tenant", MySqlDbType.Binary).Value =
                    MySqlRfc4122Guid.Encode(verifiedTenant.Value);
                read.Parameters.Add("@intent", MySqlDbType.Binary).Value =
                    MySqlRfc4122Guid.Encode(intent);
                await using var reader = await read.ExecuteReaderAsync(ct);
                if (!await reader.ReadAsync(ct)) return false;
                nonce = (byte[])reader.GetValue(0);
                tag = (byte[])reader.GetValue(1);
                encrypted = (byte[])reader.GetValue(2);
                var keyVersion = reader.GetInt32(3);
                mailboxDigest = (byte[])reader.GetValue(4);
                var expires = reader.GetDateTime(5);
                if (nonce.Length != 12 || tag.Length != 16 ||
                    encrypted.Length is < 6 or > 254 ||
                    mailboxDigest.Length != 32 || keyVersion != version ||
                    expires <= DateTime.UtcNow || await reader.ReadAsync(ct))
                    return false;
            }

            aad = new byte[52];
            MySqlRfc4122Guid.Encode(verifiedTenant.Value).CopyTo(aad, 0);
            BinaryPrimitives.WriteInt32BigEndian(aad.AsSpan(16, 4), version);
            mailboxDigest.CopyTo(aad, 20);
            plaintext = new byte[encrypted.Length];
            using (var aes = new AesGcm(encryptionKey, 16))
                aes.Decrypt(nonce, encrypted, tag, plaintext, aad);
            var storedMailbox = Encoding.UTF8.GetString(plaintext);
            if (!string.Equals(storedMailbox,
                    RequestNeutralRegistrationIntent.CanonicalMailboxOrNull(storedMailbox),
                    StringComparison.Ordinal))
                return false;

            // Reconcile case-sensitive inbox spelling with case-folded login:
            // a collision MUST be denied by the global unique DB constraint.
            // Do not merge two proven mailboxes into a single actor.
            var login = CanonicalLoginHandle.Parse(storedMailbox);
            canonical = Encoding.UTF8.GetBytes(login.Value);
            globalDigest = HMACSHA256.HashData(loginKey, canonical);
            await using var command = new MySqlCommand(
                "c05_02_create_pending_candidate", db)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.Add("p_tenant", MySqlDbType.Binary).Value =
                MySqlRfc4122Guid.Encode(verifiedTenant.Value);
            command.Parameters.Add("p_intent", MySqlDbType.Binary).Value =
                MySqlRfc4122Guid.Encode(intent);
            command.Parameters.Add("p_actor", MySqlDbType.Binary).Value =
                MySqlRfc4122Guid.Encode(Guid.NewGuid());
            command.Parameters.Add("p_login_digest", MySqlDbType.Binary).Value = globalDigest;
            command.Parameters.Add("p_mailbox_digest", MySqlDbType.Binary).Value = mailboxDigest;
            command.Parameters.Add("p_phc", MySqlDbType.VarChar).Value = trustedPhc;
            var state = await command.ExecuteScalarAsync(ct);
            return state switch
            {
                "PENDING" => true,
                "DENIED" => false,
                _ => throw new InvalidOperationException("Candidate transaction unavailable.")
            };
        }
        catch (CryptographicException) { return false; }
        catch (FormatException) { return false; }
        finally
        {
            foreach (var b in new[] { nonce,tag,encrypted,mailboxDigest,aad,
                                      plaintext,canonical,globalDigest })
                if (b is not null) CryptographicOperations.ZeroMemory(b);
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        CryptographicOperations.ZeroMemory(encryptionKey);
        CryptographicOperations.ZeroMemory(loginKey);
    }
}
