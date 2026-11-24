using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Crecisol.Application.Identity;
using MySqlConnector;

namespace Crecisol.Infrastructure.Identity;

/// <summary>
/// One operational schema and one transaction per security transition. Account,
/// membership, factor and session are revalidated under locks, never from claims.
/// The opt-in composition is restricted to isolated loopback/example.test use.
/// </summary>
public sealed partial class MySqlOperationalIdentityFlow : IOperationalIdentityFlow, IDisposable
{
    private readonly string dsn;
    private readonly byte[] tokenKey, encryptionKey, loginKey, mailboxHmacKey;
    private readonly IOperationalIdentityCrypto crypto;
    private readonly IOperationalPasskeyCrypto passkeys;
    private readonly string quotaDsn;
    private readonly string dummyPhc;
    private readonly SemaphoreSlim requestSlots = new(8, 8);
    private int queuedRequests;

    public MySqlOperationalIdentityFlow(string trustedDsn, byte[] tokenKey,
        byte[] encryptionKey, byte[] loginKey, IOperationalIdentityCrypto crypto, IOperationalPasskeyCrypto passkeys, byte[] mailboxHmacKey)
    {
        var options = new MySqlConnectionStringBuilder(trustedDsn);
        if (options.Server is not ("127.0.0.1" or "localhost" or "::1") ||
            options.Database != "crecisol_c05" || options.AllowLoadLocalInfile ||
            options.AllowUserVariables || new[] { tokenKey, encryptionKey, loginKey, mailboxHmacKey }.Any(k => k.Length != 32))
            throw new InvalidOperationException("Isolated identity configuration required.");
        options.ConnectionTimeout = 3;
        options.DefaultCommandTimeout = 8;
        options.MaximumPoolSize = 20;
        dsn = options.ConnectionString;
        // Quota reservations commit independently of denied transitions. They
        // must not wait for a connection held by every concurrent outer request.
        options.ApplicationName = "CRECISOL-C05-quota";
        quotaDsn = options.ConnectionString;
        this.tokenKey = tokenKey.ToArray();
        this.encryptionKey = encryptionKey.ToArray();
        this.loginKey = loginKey.ToArray();
        this.mailboxHmacKey = mailboxHmacKey.ToArray();
        this.crypto = crypto;
        this.passkeys = passkeys;
        dummyPhc = crypto.HashPassword(Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
    }

    public async Task<OperationalIdentityResult> ExecuteAsync(OperationalIdentityRequest request, CancellationToken ct)
    {
        if (Interlocked.Increment(ref queuedRequests) > 64)
        {
            Interlocked.Decrement(ref queuedRequests);
            return new(false);
        }
        try
        {
            await requestSlots.WaitAsync(ct);
            try { return await ExecuteCoreAsync(request, ct); }
            finally { requestSlots.Release(); }
        }
        finally { Interlocked.Decrement(ref queuedRequests); }
    }

    private async Task<OperationalIdentityResult> ExecuteCoreAsync(OperationalIdentityRequest request, CancellationToken ct)
    {
        await using var db = new MySqlConnection(dsn);
        await db.OpenAsync(ct);
        await using var tx = await db.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var unit = new Unit(db, tx, ct) { DeviceLabel = request.DeviceLabel is "Chromium" or "Firefox" or "Safari" ? request.DeviceLabel : "API",
            PeerHash = Digest("session-peer:" + request.Host, request.Peer) };
        // Fail closed until C05-017 retired all TOTP-SHA256 factors.
        if (await unit.One("SELECT policy_id FROM c05_totp_sha1_policy WHERE policy_id=1 AND status=1") is null)
            return new(false);
        if (request.Operation.StartsWith("platform/", StringComparison.Ordinal))
        {
            if (request.Host != "platform-admin.example.test") return new(false);
            var platformResult = await Platform(unit, request, ct);
            await unit.Run("INSERT INTO c05_platform_identity_audit(audit_id,operator_id,case_id,action_code,allowed,created_at_utc,purpose_hmac) VALUES(@id,@a,@case,@op,@ok,UTC_TIMESTAMP(6),@purpose)",
                ("id", Id()), ("a", unit.VerifiedActor ? (object?)unit.Actor ?? DBNull.Value : DBNull.Value), ("case", (object?)unit.Subject ?? DBNull.Value),
                ("op", request.Operation), ("ok", platformResult.Allowed), ("purpose", (object?)unit.PurposeHash ?? DBNull.Value));
            await tx.CommitAsync(ct);
            return platformResult with { SessionExpiresAtUtc = unit.SessionExpiresAtUtc, SessionIdleMinutes = unit.IdleMinutes };
        }
        var host = await unit.One("""
            SELECT h.tenant_id FROM c04_verified_tenant_host h
            JOIN c04_tenant_catalog t ON t.tenant_id=h.tenant_id
            WHERE h.canonical_host=@host AND h.verification_state=2
            AND h.verified_at_utc<=UTC_TIMESTAMP(6) AND h.valid_until_utc>UTC_TIMESTAMP(6)
            AND t.lifecycle_state=3 FOR SHARE
            """, ("host", request.Host));
        if (host is null) return new(false);
        var tenant = Bytes(host, "tenant_id");
        // Persist quota before costly crypto, even when the operation is denied.
        if (!await Reserve(tenant, "peer:" + request.Peer, 80, ct) ||
            !await Reserve(tenant, "tenant", 400, ct) ||
            !await Reserve(tenant, "route:" + request.Operation + ":" + request.Peer, 40, ct)) return new(false);
        OperationalIdentityResult result;
        if (request.Operation == "login") result = await Login(unit, tenant, request, ct);
        else if (request.Operation is "passkeys/options" or "passkeys/login")
            result = await PasskeyLogin(unit, tenant, request, ct);
        else if (request.Operation == "terms") result = await Terms(unit, tenant);
        else if (request.Operation == "invitations/accept") result = await AcceptInvitation(unit, tenant, request, ct);
        else if (request.Operation == "memberships/request") result = await RequestAdditionalMembership(unit, tenant, request, ct);
        else if (request.Operation is "pairings/start" or "pairings/complete") result = await Pairing(unit, tenant, request, ct);
        else if (request.Operation == "incidents/report") result = await ReportIncident(unit, tenant, request);
        else if (request.Operation.StartsWith("assisted/", StringComparison.Ordinal)) result = await Assisted(unit, tenant, request, ct);
        else if (request.Operation.StartsWith("mfa/", StringComparison.Ordinal))
            result = await Mfa(unit, tenant, request, ct);
        else if (request.Operation == "recovery/request")
            result = await RequestRecovery(unit, tenant, request, ct);
        else if (request.Operation == "recovery/complete")
            result = await CompleteRecovery(unit, tenant, request, ct);
        else result = await Authenticated(unit, tenant, request, ct);
        await Audit(unit, tenant, request.Operation, result.Allowed);
        await tx.CommitAsync(ct);
        return result with { SessionExpiresAtUtc = unit.SessionExpiresAtUtc, SessionIdleMinutes = unit.IdleMinutes };
    }

    private async Task<bool> Reserve(byte[] tenant, string bucket, int maximum, CancellationToken ct)
    {
        await using var db = new MySqlConnection(quotaDsn);
        await db.OpenAsync(ct);
        await using var tx = await db.BeginTransactionAsync(ct);
        var u = new Unit(db, tx, ct);
        var digest = Digest("quota", bucket);
        var window = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 60;
        await u.Run("""
            INSERT INTO c05_identity_attempt_quota VALUES(@t,@b,@w,1)
            ON DUPLICATE KEY UPDATE attempts=LEAST(attempts+1,1000)
            """, ("t", tenant), ("b", digest), ("w", window));
        var row = await u.One("SELECT attempts FROM c05_identity_attempt_quota WHERE tenant_id=@t AND bucket_hmac=@b AND window_utc=@w",
            ("t", tenant), ("b", digest), ("w", window));
        if (Number(row!, "attempts") == maximum + 1)
        {
            var kind = bucket == "tenant" ? "TENANT" : bucket.StartsWith("peer:", StringComparison.Ordinal) ? "PEER" :
                bucket.StartsWith("route:", StringComparison.Ordinal) ? "ROUTE" : "ACCOUNT";
            await u.Run("INSERT IGNORE INTO c05_identity_abuse_signal VALUES(@t,@b,@w,@kind,UTC_TIMESTAMP(6))",
                ("t", tenant), ("b", digest), ("w", window), ("kind", kind));
        }
        await tx.CommitAsync(ct);
        return Number(row!, "attempts") <= maximum;
    }

    // Local SMTP delivery is durable and at-least-once. Proof consumption is
    // one-shot even if SMTP accepted a message before a dispatcher crash.
    public async Task DispatchOneAsync(int smtpPort, CancellationToken ct)
    {
        await using var db = new MySqlConnection(dsn);
        await db.OpenAsync(ct);
        await using var tx = await db.BeginTransactionAsync(ct);
        var u = new Unit(db, tx, ct);
        var row = await u.One("SELECT * FROM c05_identity_sensitive_outbox WHERE delivered_at_utc IS NULL AND expires_at_utc>UTC_TIMESTAMP(6) LIMIT 1 FOR UPDATE SKIP LOCKED");
        if (row is null) return;
        var bytes = Unprotect(Bytes(row, "protected_payload"), "delivery:" + Hex(Bytes(row, "message_id")));
        try
        {
            var payload = JsonSerializer.Deserialize<DeliveryPayload>(bytes) ?? throw new InvalidOperationException("Invalid outbox.");
            var sink = new LoopbackOnlySmtpProofSink(smtpPort);
            if (payload.Purpose.StartsWith("NOTICE_", StringComparison.Ordinal))
                await sink.DeliverSecurityInTestOnlyAsync(payload.Mailbox, Convert.FromHexString(payload.Token), payload.Purpose, ct);
            else await sink.DeliverCapabilityInTestOnlyAsync(payload.Mailbox, Convert.FromHexString(payload.Token), payload.Purpose, ct);
            await u.Run("UPDATE c05_identity_sensitive_outbox SET delivered_at_utc=UTC_TIMESTAMP(6) WHERE message_id=@id", ("id", Bytes(row, "message_id")));
            await tx.CommitAsync(ct);
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private byte[] Protect(byte[] plaintext, string purpose)
    {
        var result = new byte[28 + plaintext.Length];
        RandomNumberGenerator.Fill(result.AsSpan(0, 12));
        using var aes = new AesGcm(encryptionKey, 16);
        aes.Encrypt(result.AsSpan(0, 12), plaintext, result.AsSpan(28), result.AsSpan(12, 16), Encoding.UTF8.GetBytes("C05/operational/v1/" + purpose));
        return result;
    }
    private byte[] Unprotect(byte[] envelope, string purpose)
    {
        var result = new byte[envelope.Length - 28];
        using var aes = new AesGcm(encryptionKey, 16);
        aes.Decrypt(envelope.AsSpan(0, 12), envelope.AsSpan(28), envelope.AsSpan(12, 16), result, Encoding.UTF8.GetBytes("C05/operational/v1/" + purpose));
        return result;
    }
    private byte[] Digest(string purpose, string value) => HMACSHA256.HashData(tokenKey, Encoding.UTF8.GetBytes("C05/" + purpose + ":" + value));
    private byte[] LoginDigest(string mailbox) => HMACSHA256.HashData(loginKey, Encoding.UTF8.GetBytes(mailbox));
    private static string Token() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private static byte[] Id() => Guid.NewGuid().ToByteArray(true);
    private static byte[] GuidBytes(string value) => Guid.TryParse(value, out var id) && id != Guid.Empty ? id.ToByteArray(true) : throw new InvalidOperationException("Invalid identifier.");
    private static string Hex(byte[] bytes) => Convert.ToHexString(bytes);
    private static string Field(OperationalIdentityRequest r, string name) => r.Fields.TryGetValue(name, out var value) && value.Length > 0 && value.Length <= (name is "response" or "pixels" ? 24000 : 512) ? value : throw new InvalidOperationException("Invalid request.");
    private static byte[] Bytes(Dictionary<string, object> row, string key) => (byte[])row[key];
    private static string Text(Dictionary<string, object> row, string key) => (string)row[key];
    private static long Number(Dictionary<string, object> row, string key) => Convert.ToInt64(row[key]);
    private static DateTimeOffset Utc(Dictionary<string, object> row, string key) => new(DateTime.SpecifyKind((DateTime)row[key], DateTimeKind.Utc));
    private static string Base32(byte[] bytes)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var result = new StringBuilder();
        int value = 0, bits = 0;
        foreach (var b in bytes)
        {
            value = (value << 8) | b; bits += 8;
            while (bits >= 5) { result.Append(alphabet[(value >> (bits - 5)) & 31]); bits -= 5; }
        }
        if (bits > 0) result.Append(alphabet[(value << (5 - bits)) & 31]);
        return result.ToString();
    }
    // The URI is temporary enrollment material: never persist or log it.
    // A non-identifying stable label distinguishes multiple CRECISOL accounts
    // in free authenticators without exposing an email or tenant identifier.
    private static string TotpUri(byte[] actor, byte[] seed, string realm) =>
        "otpauth://totp/" + Uri.EscapeDataString("CRECISOL:" + realm + "-" + Hex(actor)[..8]) +
        "?secret=" + Base32(seed) + "&issuer=CRECISOL&algorithm=SHA1&digits=6&period=30";

    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(tokenKey);
        CryptographicOperations.ZeroMemory(encryptionKey);
        CryptographicOperations.ZeroMemory(loginKey);
        CryptographicOperations.ZeroMemory(mailboxHmacKey);
    }
    public override string ToString() => "MySqlOperationalIdentityFlow[redacted]";
    private sealed record ProofPayload(string Mailbox, string Binding);
    private sealed record DeliveryPayload(string Mailbox, string Token, string Purpose);

    private sealed class Unit(MySqlConnection db, MySqlTransaction tx, CancellationToken ct)
    {
        public byte[]? Actor { get; set; }
        public byte[]? Subject { get; set; }
        public bool VerifiedActor { get; set; }
        public int? IdleMinutes { get; set; }
        public long? PolicyRevision { get; set; }
        public DateTimeOffset? SessionExpiresAtUtc { get; set; }
        public byte[]? PurposeHash { get; set; }
        public string DeviceLabel { get; set; } = "API";
        public byte[] PeerHash { get; set; } = [];
        public string SessionMethod { get; set; } = "LEGACY";
        private MySqlCommand Command(string sql, (string, object)[] parameters)
        {
            var command = new MySqlCommand(sql, db, tx);
            foreach (var (name, value) in parameters) command.Parameters.AddWithValue("@" + name, value);
            return command;
        }
        public async Task<int> Run(string sql, params (string, object)[] parameters)
        {
            await using var command = Command(sql, parameters);
            return await command.ExecuteNonQueryAsync(ct);
        }
        public async Task<List<Dictionary<string, object>>> All(string sql, params (string, object)[] parameters)
        {
            await using var command = Command(sql, parameters);
            await using var rows = await command.ExecuteReaderAsync(ct);
            var result = new List<Dictionary<string, object>>();
            while (await rows.ReadAsync(ct))
            {
                var row = new Dictionary<string, object>(StringComparer.Ordinal);
                for (int i = 0; i < rows.FieldCount; i++) row.Add(rows.GetName(i), rows.GetValue(i));
                result.Add(row);
            }
            return result;
        }
        public async Task<Dictionary<string, object>?> One(string sql, params (string, object)[] parameters)
        {
            var rows = await All(sql, parameters);
            return rows.Count == 1 ? rows[0] : null;
        }
    }
}
