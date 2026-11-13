using System.Collections.Concurrent;
using System.Data;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Crecisol.Domain.Identity;
using Crecisol.Domain.Tenancy;
using Crecisol.Infrastructure.Identity;
using Crecisol.Infrastructure.Tenancy;
using MySqlConnector;

// Real local MySQL8.4 and .NET10 only. No external SMTP, browser account,
// credential, grant or session. Mailboxes below are synthetic @example.test.
if (args.Length != 1 || args[0] != "--integration")
    throw new InvalidOperationException("Local --integration is required.");

var setupDsn = Environment.GetEnvironmentVariable("CRECISOL_C05_02_OUTBOX_SETUP_DSN")
    ?? throw new InvalidOperationException("Missing protected local setup connection.");
var ingestDsn = Environment.GetEnvironmentVariable("CRECISOL_C05_02_OUTBOX_INGEST_DSN")
    ?? throw new InvalidOperationException("Missing protected local ingestion connection.");
var workerDsn = Environment.GetEnvironmentVariable("CRECISOL_C05_02_OUTBOX_WORKER_DSN")
    ?? throw new InvalidOperationException("Missing restricted local worker connection.");
var masterHex = Environment.GetEnvironmentVariable("CRECISOL_C05_02_OUTBOX_MASTER_HEX")
    ?? throw new InvalidOperationException("Missing disposable local key.");
var master = Convert.FromHexString(masterHex);
if (master.Length != 32) throw new InvalidOperationException("Invalid local key.");
var adminOptions = new MySqlConnectionStringBuilder(setupDsn);
if (adminOptions.Database != "c05_02_disposable" ||
    adminOptions.Server is not ("127.0.0.1" or "localhost" or "::1"))
    throw new InvalidOperationException("Only the isolated local fixture may be tested.");

var checks = 0;
void Pass(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException("FAIL C05-02 outbox: " + message);
    checks++;
    Console.WriteLine("PASS C05-02 outbox: " + message);
}
const string HexA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
const string HexB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
var tenantA = TenantId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
var tenantB = TenantId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
await using var admin = new MySqlConnection(adminOptions.ConnectionString);
await admin.OpenAsync();

async Task RunSqlAsync(string sql)
{
    await using var command = new MySqlCommand(sql, admin);
    await command.ExecuteNonQueryAsync();
}
async Task<int> CountAsync(string sql)
{
    await using var command = new MySqlCommand(sql, admin);
    return Convert.ToInt32(await command.ExecuteScalarAsync());
}
async Task ExpectRestrictedSqlDenyAsync(string sql, string reason)
{
    await using var db = new MySqlConnection(workerDsn);
    await db.OpenAsync();
    await using var cmd = new MySqlCommand(sql, db);
    try { await cmd.ExecuteNonQueryAsync(); }
    catch (MySqlException e) when (e.Number is 1142 or 1143)
    {
        Pass(true, reason);
        return;
    }
    throw new InvalidOperationException("Restricted worker unexpectedly acquired table privileges.");
}

await RunSqlAsync($"""
    INSERT INTO c05_02_tenants VALUES
        (X'{HexA}','ACTIVE',42),(X'{HexB}','ACTIVE',42)
    """);
using var ingestion = new MySqlDisposableNeutralRegistrationRecorder(
    ingestDsn, master, 1);
using var sink = new IsolatedOutboxMailboxSink();
using var destination = new MySqlDisposableEmailPossessionAdapter(
    workerDsn, master, 1, sink);
using var outbox = new MySqlDisposableProofDeliveryOutbox(
    workerDsn, master, 1, destination);

async Task<Guid> AddIntentAsync(TenantId tenant, string mailbox)
{
    await ingestion.RecordWithoutEnumerationAsync(
        tenant, mailbox,
        IPAddress.Parse("127.0.0.1").MapToIPv6().GetAddressBytes(),
        DateTimeOffset.UtcNow, CancellationToken.None);
    var tenantBytes = MySqlRfc4122Guid.Encode(tenant.Value);
    var mailboxKey = HMACSHA256.HashData(
        master, "CRECISOL/C05-02/MAILBOX-FINGERPRINT/V1"u8);
    var plain = Encoding.UTF8.GetBytes(mailbox);
    var joined = new byte[tenantBytes.Length + plain.Length];
    byte[]? digest = null;
    try
    {
        tenantBytes.CopyTo(joined, 0);
        plain.CopyTo(joined, tenantBytes.Length);
        digest = HMACSHA256.HashData(mailboxKey, joined);
        await using var cmd = new MySqlCommand("""
            SELECT intent_id FROM c05_02_neutral_intents
              WHERE tenant_id=@tenant AND mailbox_digest=@digest
            """, admin);
        cmd.Parameters.Add("@tenant", MySqlDbType.Binary).Value = tenantBytes;
        cmd.Parameters.Add("@digest", MySqlDbType.Binary).Value = digest;
        var bytes = await cmd.ExecuteScalarAsync() as byte[];
        return bytes is null ? throw new InvalidOperationException(
            "Synthetic protected intent missing.") : MySqlRfc4122Guid.Decode(bytes);
    }
    finally
    {
        foreach (var buffer in new[] { mailboxKey, plain, joined, digest })
            if (buffer is not null) CryptographicOperations.ZeroMemory(buffer);
    }
}

var a1 = await AddIntentAsync(tenantA, "one.a@example.test");
var a2 = await AddIntentAsync(tenantA, "failed.a@example.test");
var a3 = await AddIntentAsync(tenantA, "suspended.a@example.test");
var a4 = await AddIntentAsync(tenantA, "recovered.a@example.test");
var b1 = await AddIntentAsync(tenantB, "tampered.b@example.test");
var b2 = await AddIntentAsync(tenantB, "exhausted.b@example.test");
Pass(new[] { a1, a2, a3, a4, b1, b2 }.Distinct().Count() == 6,
    "Six independent encrypted intents across two isolated tenants");

try
{
    using var _ = new MySqlDisposableProofDeliveryOutbox(
        "Server=evil.example;Database=c05_02_disposable;User ID=x;Password=x",
        master, 1, destination);
    throw new InvalidOperationException("Remote MySQL incorrectly accepted.");
}
catch (InvalidOperationException e) when (e.Message.StartsWith(
    "Outbox requires isolated loopback", StringComparison.Ordinal))
{
    Pass(true, "Remote DSN blocked before any network connection");
}

Pass(!await outbox.StageOnceAsync(tenantB, a1, DateTimeOffset.UtcNow,
        CancellationToken.None),
    "Tenant B cannot stage a challenge for an A-owned intent");
var staged = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ =>
    outbox.StageOnceAsync(tenantA, a1, DateTimeOffset.UtcNow,
        CancellationToken.None)));
Pass(staged.Count(x => x) == 1,
    "Sixteen simultaneous issue attempts commit exactly one proof and one outbox row");
Pass(await CountAsync($"""
    SELECT COUNT(*) FROM c05_02_delivery_outbox
     WHERE tenant_id=X'{HexA}'
    """) == 1 &&
     await CountAsync($"""
    SELECT COUNT(*) FROM c05_02_mailbox_proofs
     WHERE tenant_id=X'{HexA}'
    """) == 1,
    "Proof and encrypted outbox were committed together");

Pass(await CountAsync("""
    SELECT COUNT(*) FROM c05_02_delivery_outbox
     WHERE LENGTH(nonce)=12 AND LENGTH(tag)=16 AND LENGTH(ciphertext)=32
       AND LENGTH(challenge_digest)=32 AND key_version=1
    """) == 1, "Only AEAD-protected fixed-size bearer and digest persisted");
await ExpectRestrictedSqlDenyAsync(
    "SELECT * FROM c05_02_delivery_outbox",
    "Worker cannot SELECT raw encrypted outbox table");
await ExpectRestrictedSqlDenyAsync(
    "UPDATE c05_02_delivery_outbox SET lifecycle='SENT'",
    "Worker cannot directly forge SENT state");
await ExpectRestrictedSqlDenyAsync(
    "SELECT * FROM c05_02_mailbox_proofs",
    "Worker cannot inspect proof state outside stored procedures");

var concurrent = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ =>
    outbox.ProcessOnceAsync(tenantA, a1, CancellationToken.None)));
Pass(concurrent.Count(x => x) == 1 &&
     sink.Count("one.a@example.test") == 1,
    "Sixteen competing workers: exactly one exclusive delivery lease and one local receipt");
Pass(await CountAsync("""
    SELECT COUNT(*) FROM c05_02_delivery_outbox WHERE lifecycle='SENT'
      AND lease_nonce IS NULL AND leased_until_utc IS NULL
    """) == 1,
    "Successful delivery closes the lease without creating any membership");
var acceptedCode = sink.Take("one.a@example.test");
try
{
    var digest = EmailPossessionDigest.FromToken(tenantA, a1, acceptedCode);
    Pass(await destination.TryVerifyOnceAsync(tenantA,a1,digest,
        DateTimeOffset.UtcNow,CancellationToken.None),
        "Stored independent email challenge verifies exactly once");
    Pass(!await destination.TryVerifyOnceAsync(tenantA,a1,digest,
        DateTimeOffset.UtcNow,CancellationToken.None),
        "Verified challenge cannot be replayed");
    Pass(!await outbox.ProcessOnceAsync(tenantA,a1,CancellationToken.None),
        "SENT outbox cannot re-deliver the same challenge");
}
finally { CryptographicOperations.ZeroMemory(acceptedCode); }

Pass(await outbox.StageOnceAsync(tenantA,a2,DateTimeOffset.UtcNow,
    CancellationToken.None), "A second exact tenant intent stages independently");
sink.FailFor = "failed.a@example.test";
Pass(!await outbox.ProcessOnceAsync(tenantA,a2,CancellationToken.None),
    "Simulated transport failure does not mark delivery as sent");
sink.FailFor = null;
Pass(await CountAsync($"""
    SELECT COUNT(*) FROM c05_02_delivery_outbox
     WHERE tenant_id=X'{HexA}' AND lifecycle='REVOKED'
    """) == 1 &&
    await CountAsync($"""
    SELECT COUNT(*) FROM c05_02_mailbox_proofs
     WHERE tenant_id=X'{HexA}' AND lifecycle='REVOKED'
    """) == 1,
    "Failed delivery atomically revokes outbox and pending email proof");
Pass(!await outbox.StageOnceAsync(tenantA,a2,DateTimeOffset.UtcNow,
    CancellationToken.None), "Revoked proof cannot be replaced by a fresh code");

Pass(await outbox.StageOnceAsync(tenantB,b1,DateTimeOffset.UtcNow,
    CancellationToken.None), "Tenant B has independent outbox capacity");
await RunSqlAsync($"""
    UPDATE c05_02_delivery_outbox SET tag=UNHEX(REPEAT('AB',16))
      WHERE tenant_id=X'{HexB}'
    """);
Pass(!await outbox.ProcessOnceAsync(tenantB,b1,CancellationToken.None) &&
     sink.Count("tampered.b@example.test") == 0,
    "Tampered AEAD tag cannot cause delivery or expose a bearer");
Pass(await CountAsync($"""
    SELECT COUNT(*) FROM c05_02_mailbox_proofs
     WHERE tenant_id=X'{HexB}' AND lifecycle='REVOKED'
    """) == 1, "Tampered ciphertext transaction revokes proof");

Pass(await outbox.StageOnceAsync(tenantA,a3,DateTimeOffset.UtcNow,
    CancellationToken.None), "Pending A proof can be staged before tenant suspension");
await RunSqlAsync($"""
    UPDATE c05_02_tenants SET lifecycle='SUSPENDED'
     WHERE tenant_id=X'{HexA}'
    """);
Pass(!await outbox.ProcessOnceAsync(tenantA,a3,CancellationToken.None) &&
     sink.Count("suspended.a@example.test") == 0,
    "Suspended tenant cannot dispatch any pending message");
await RunSqlAsync($"""
    UPDATE c05_02_tenants SET lifecycle='ACTIVE'
     WHERE tenant_id=X'{HexA}'
    """);

async Task<bool> ReserveWithoutSendingAsync(TenantId tenant, Guid intentId, byte[] lease)
{
    await using var db = new MySqlConnection(workerDsn);
    await db.OpenAsync();
    await using var cmd = new MySqlCommand("c05_02_outbox_claim", db)
    { CommandType = CommandType.StoredProcedure };
    cmd.Parameters.Add("p_tenant", MySqlDbType.Binary).Value =
        MySqlRfc4122Guid.Encode(tenant.Value);
    cmd.Parameters.Add("p_intent", MySqlDbType.Binary).Value =
        MySqlRfc4122Guid.Encode(intentId);
    cmd.Parameters.Add("p_lease", MySqlDbType.Binary).Value = lease;
    await using var reader = await cmd.ExecuteReaderAsync();
    return await reader.ReadAsync();
}
async Task ExpireLeaseForCrashSimulationAsync(TenantId tenant, Guid intentId)
{
    await using var cmd = new MySqlCommand("""
        UPDATE c05_02_delivery_outbox
           SET leased_until_utc=DATE_SUB(UTC_TIMESTAMP(6), INTERVAL 1 SECOND)
         WHERE tenant_id=@tenant AND intent_id=@intent AND lifecycle='LEASED'
        """,admin);
    cmd.Parameters.Add("@tenant", MySqlDbType.Binary).Value =
        MySqlRfc4122Guid.Encode(tenant.Value);
    cmd.Parameters.Add("@intent", MySqlDbType.Binary).Value =
        MySqlRfc4122Guid.Encode(intentId);
    Pass(await cmd.ExecuteNonQueryAsync() == 1,
        "Controlled crash test expires only the specific worker lease");
}
Pass(await outbox.StageOnceAsync(tenantA,a4,DateTimeOffset.UtcNow,
    CancellationToken.None), "Crash-recovery challenge staged");
var crashLease = RandomNumberGenerator.GetBytes(16);
try
{
    Pass(await ReserveWithoutSendingAsync(tenantA,a4,crashLease),
        "First worker obtains exclusive lease before simulated crash");
    Pass(!await outbox.ProcessOnceAsync(tenantA,a4,CancellationToken.None),
        "A competing worker cannot steal an unexpired lease");
    await ExpireLeaseForCrashSimulationAsync(tenantA,a4);
    Pass(await outbox.ProcessOnceAsync(tenantA,a4,CancellationToken.None) &&
         sink.Count("recovered.a@example.test") == 1,
        "Expired lease recovers the SAME encrypted code without issuing another proof");
    Pass(await CountAsync($"""
        SELECT COUNT(*) FROM c05_02_delivery_outbox
         WHERE tenant_id=X'{HexA}' AND attempts=2 AND lifecycle='SENT'
        """) == 1,
        "Crash recovery increments bounded dispatch attempts");
}
finally { CryptographicOperations.ZeroMemory(crashLease); }

Pass(await outbox.StageOnceAsync(tenantB,b2,DateTimeOffset.UtcNow,
    CancellationToken.None), "Independent B challenge staged for retry exhaustion");
for (var i = 0; i < 3; i++)
{
    var key = RandomNumberGenerator.GetBytes(16);
    try
    {
        Pass(await ReserveWithoutSendingAsync(tenantB,b2,key),
            "A distinct simulated crash claim consumes one of three bounded leases");
        await ExpireLeaseForCrashSimulationAsync(tenantB,b2);
    }
    finally { CryptographicOperations.ZeroMemory(key); }
}
var fourthLease = RandomNumberGenerator.GetBytes(16);
try
{
    Pass(!await ReserveWithoutSendingAsync(tenantB,b2,fourthLease),
        "Fourth claim cannot deliver after exhausting retry bound");
}
finally { CryptographicOperations.ZeroMemory(fourthLease); }
Pass(await CountAsync($"""
    SELECT COUNT(*) FROM c05_02_delivery_outbox
     WHERE tenant_id=X'{HexB}' AND lifecycle='REVOKED' AND attempts=3
    """) == 1 &&
    await CountAsync($"""
    SELECT COUNT(*) FROM c05_02_mailbox_proofs
     WHERE tenant_id=X'{HexB}' AND lifecycle='REVOKED'
    """) == 2,
    "Exhausted lease budget atomically revokes pending challenge");
Pass(sink.Count("exhausted.b@example.test") == 0,
    "No message delivered after lease exhaustion");

CryptographicOperations.ZeroMemory(master);
Console.WriteLine($"PASS C05-02 protected local delivery outbox: {checks} real assertions; " +
    "NO SMTP, identity, membership, session or functional certification");

public sealed class IsolatedOutboxMailboxSink : ITrustedLocalProofMailboxSink, IDisposable
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<byte[]>> boxes = new();
    public string? FailFor { get; set; }

    public Task DeliverInTestOnlyAsync(
        string storedCanonicalMailbox, ReadOnlyMemory<byte> capability,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!storedCanonicalMailbox.EndsWith("@example.test",StringComparison.Ordinal) ||
            capability.Length != 32)
            throw new InvalidOperationException("Only synthetic local mail is allowed.");
        if (storedCanonicalMailbox == FailFor)
            throw new InvalidOperationException("Synthetic transport failure");
        boxes.GetOrAdd(storedCanonicalMailbox,_ => new ConcurrentQueue<byte[]>())
            .Enqueue(capability.ToArray());
        return Task.CompletedTask;
    }

    public int Count(string mailbox) =>
        boxes.TryGetValue(mailbox, out var queue) ? queue.Count : 0;

    public byte[] Take(string mailbox)
    {
        if (!boxes.TryGetValue(mailbox, out var queue) ||
            !queue.TryDequeue(out var code))
            throw new InvalidOperationException("No local proof arrived.");
        return code;
    }

    public void Dispose()
    {
        foreach (var queue in boxes.Values)
            while (queue.TryDequeue(out var bytes))
                CryptographicOperations.ZeroMemory(bytes);
        boxes.Clear();
    }
}
