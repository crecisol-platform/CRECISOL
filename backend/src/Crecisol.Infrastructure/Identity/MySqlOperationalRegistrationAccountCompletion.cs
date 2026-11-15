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
/// Product-schema transition from one valid mailbox proof to a PENDING account
/// and PENDING membership. Active access, approval, MFA and session are never
/// produced here. Every mutable transition is checked in a definer procedure.
/// </summary>
public sealed class MySqlOperationalRegistrationAccountCompletion : IDisposable
{
    private readonly string dsn;
    private readonly byte[] mailboxHmacKey;
    private readonly byte[] peerHmacKey;
    private readonly byte[] loginHmacKey;
    private bool disposed;

    public MySqlOperationalRegistrationAccountCompletion(
        string? trustedDsn, ReadOnlySpan<byte> mailboxHmacKey,
        ReadOnlySpan<byte> peerHmacKey, ReadOnlySpan<byte> loginHmacKey)
    {
        if (string.IsNullOrWhiteSpace(trustedDsn) || mailboxHmacKey.Length != 32 ||
            peerHmacKey.Length != 32 || loginHmacKey.Length != 32)
            throw new InvalidOperationException("Operational account completion is not configured.");
        var settings = new MySqlConnectionStringBuilder(trustedDsn)
        {
            ConnectionTimeout = 3,
            DefaultCommandTimeout = 5,
            Pooling = true,
            MaximumPoolSize = 20,
            AllowLoadLocalInfile = false
        };
        dsn = settings.ConnectionString;
        this.mailboxHmacKey = mailboxHmacKey.ToArray();
        this.peerHmacKey = peerHmacKey.ToArray();
        this.loginHmacKey = loginHmacKey.ToArray();
    }

    public async Task<bool> TryReserveBeforeHashAsync(
        TenantId tenant, ReadOnlyMemory<byte> actualPeer, DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        Check(tenant, cancellationToken);
        if (actualPeer.Length != 16 || nowUtc.Offset != TimeSpan.Zero) return false;
        var tenantBytes = MySqlRfc4122Guid.Encode(tenant.Value);
        var material = new byte[32];
        var digest = Array.Empty<byte>();
        try
        {
            tenantBytes.CopyTo(material, 0);
            actualPeer.Span.CopyTo(material.AsSpan(16));
            digest = HMACSHA256.HashData(peerHmacKey, material);
            var utc = nowUtc.UtcDateTime;
            var window = new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour,
                utc.Minute, 0, DateTimeKind.Utc);
            await using var db = await OpenAsync(cancellationToken);
            await using var command = new MySqlCommand(
                "c05_operational_reserve_registration_completion", db)
            { CommandType = CommandType.StoredProcedure };
            command.Parameters.Add("p_tenant", MySqlDbType.Binary).Value = tenantBytes;
            command.Parameters.Add("p_peer", MySqlDbType.Binary).Value = digest;
            command.Parameters.Add("p_window", MySqlDbType.DateTime).Value = window;
            return await command.ExecuteScalarAsync(cancellationToken) as string == "ADMITTED";
        }
        finally
        {
            CryptographicOperations.ZeroMemory(tenantBytes);
            CryptographicOperations.ZeroMemory(material);
            if (digest.Length > 0) CryptographicOperations.ZeroMemory(digest);
        }
    }

    public async Task<string> CompletePendingAsync(
        TenantId tenant, Guid proofId, ReadOnlyMemory<byte> token,
        string canonicalMailbox, string passwordPhc, DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        Check(tenant, cancellationToken);
        if (proofId == Guid.Empty || token.Length != 32 || nowUtc.Offset != TimeSpan.Zero ||
            RequestNeutralRegistrationIntent.CanonicalMailboxOrNull(canonicalMailbox) != canonicalMailbox ||
            !IsArgon2idPhc(passwordPhc))
            return "DENIED";

        var tenantBytes = MySqlRfc4122Guid.Encode(tenant.Value);
        var proofBytes = MySqlRfc4122Guid.Encode(proofId);
        var mailboxBytes = Encoding.UTF8.GetBytes(canonicalMailbox);
        var mailboxMaterial = new byte[tenantBytes.Length + mailboxBytes.Length];
        byte[]? mailboxDigest = null;
        byte[]? loginBytes = null;
        byte[]? loginDigest = null;
        byte[]? proofDigest = null;
        try
        {
            tenantBytes.CopyTo(mailboxMaterial, 0);
            mailboxBytes.CopyTo(mailboxMaterial, tenantBytes.Length);
            mailboxDigest = HMACSHA256.HashData(mailboxHmacKey, mailboxMaterial);
            var login = CanonicalLoginHandle.Parse(canonicalMailbox);
            loginBytes = Encoding.UTF8.GetBytes(login.Value);
            loginDigest = HMACSHA256.HashData(loginHmacKey, loginBytes);
            proofDigest = EmailPossessionDigest.FromToken(tenant, proofId, token.Span)
                .CopyForProtectedDatabase();

            await using var db = await OpenAsync(cancellationToken);
            await using var command = new MySqlCommand(
                "c05_operational_complete_pending_registration", db)
            { CommandType = CommandType.StoredProcedure };
            command.Parameters.Add("p_tenant", MySqlDbType.Binary).Value = tenantBytes;
            command.Parameters.Add("p_proof", MySqlDbType.Binary).Value = proofBytes;
            command.Parameters.Add("p_proof_digest", MySqlDbType.Binary).Value = proofDigest;
            command.Parameters.Add("p_mailbox_digest", MySqlDbType.Binary).Value = mailboxDigest;
            command.Parameters.Add("p_login_digest", MySqlDbType.Binary).Value = loginDigest;
            command.Parameters.Add("p_password_phc", MySqlDbType.VarChar, 512).Value = passwordPhc;
            command.Parameters.Add("p_account", MySqlDbType.Binary).Value =
                MySqlRfc4122Guid.Encode(Guid.NewGuid());
            command.Parameters.Add("p_membership", MySqlDbType.Binary).Value =
                MySqlRfc4122Guid.Encode(Guid.NewGuid());
            command.Parameters.Add("p_created", MySqlDbType.DateTime).Value = nowUtc.UtcDateTime;
            return await command.ExecuteScalarAsync(cancellationToken) as string ?? "DENIED";
        }
        finally
        {
            CryptographicOperations.ZeroMemory(tenantBytes);
            CryptographicOperations.ZeroMemory(proofBytes);
            CryptographicOperations.ZeroMemory(mailboxBytes);
            CryptographicOperations.ZeroMemory(mailboxMaterial);
            if (mailboxDigest is not null) CryptographicOperations.ZeroMemory(mailboxDigest);
            if (loginBytes is not null) CryptographicOperations.ZeroMemory(loginBytes);
            if (loginDigest is not null) CryptographicOperations.ZeroMemory(loginDigest);
            if (proofDigest is not null) CryptographicOperations.ZeroMemory(proofDigest);
        }
    }

    private async Task<MySqlConnection> OpenAsync(CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var connection = new MySqlConnection(dsn);
        try { await connection.OpenAsync(ct); return connection; }
        catch { await connection.DisposeAsync(); throw; }
    }

    private static bool IsArgon2idPhc(string? value) =>
        value is { Length: >= 80 and <= 512 } &&
        value.StartsWith("$argon2id$v=19$", StringComparison.Ordinal) &&
        value.All(character => character is >= (char)33 and <= (char)126);

    private void Check(TenantId tenant, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(tenant);
        ct.ThrowIfCancellationRequested();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        CryptographicOperations.ZeroMemory(mailboxHmacKey);
        CryptographicOperations.ZeroMemory(peerHmacKey);
        CryptographicOperations.ZeroMemory(loginHmacKey);
    }

    public override string ToString() =>
        "MySqlOperationalRegistrationAccountCompletion[redacted]";
}
