using System.Net;
using System.Security.Cryptography;
using System.Text;
using Crecisol.Application.Identity;
using Crecisol.Application.Tenancy;
using Crecisol.Domain.Identity;
using Crecisol.Domain.Tenancy;
using Crecisol.Infrastructure.Identity;
using Crecisol.Infrastructure.Tenancy;
using MySqlConnector;

// This is a real .NET 10 + MySQL 8.4 test with a deliberately LOCAL mailbox
// sink, not an external inbox provider or a production registration flow.
if (args.Length == 0)
{
    Console.WriteLine("SKIP C05-02 email possession MySQL: --integration required");
    return;
}
if (args.Length != 1 || args[0] != "--integration")
    throw new InvalidOperationException("Usage: --integration (local C05-02 fixture only)");

var adminDsn = Environment.GetEnvironmentVariable("CRECISOL_C05_02_PROOF_SETUP_DSN");
var ingestDsn = Environment.GetEnvironmentVariable("CRECISOL_C05_02_PROOF_INGEST_DSN");
var proofDsn = Environment.GetEnvironmentVariable("CRECISOL_C05_02_PROOF_TEST_DSN");
var masterHex = Environment.GetEnvironmentVariable("CRECISOL_C05_02_PROOF_MASTER_HEX");
if (string.IsNullOrWhiteSpace(adminDsn) || string.IsNullOrWhiteSpace(ingestDsn) ||
    string.IsNullOrWhiteSpace(proofDsn) || string.IsNullOrWhiteSpace(masterHex))
    throw new InvalidOperationException("C05-02 local mailbox proof credentials required.");
var master = Convert.FromHexString(masterHex);
if (master.Length != 32)
    throw new InvalidOperationException("Invalid protected local test key.");
var options = new MySqlConnectionStringBuilder(adminDsn);
if (options.Database != "c05_02_disposable" ||
    options.Server is not ("127.0.0.1" or "localhost" or "::1"))
    throw new InvalidOperationException("Only the isolated local fixture is permitted.");

var checks = 0;
void Pass(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException("FAIL C05-02 email proof: " + message);
    checks++;
    Console.WriteLine("PASS C05-02 email proof: " + message);
}

async Task RequireDeny(string sql, string message)
{
    await using var db = new MySqlConnection(proofDsn);
    await db.OpenAsync();
    await using var command = new MySqlCommand(sql, db);
    try { await command.ExecuteNonQueryAsync(); }
    catch (MySqlException error) when (error.Number is 1142 or 1143)
    {
        Pass(true, message);
        return;
    }
    throw new InvalidOperationException("FAIL C05-02 unexpected proof DB privilege: " + message);
}

const string A = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
const string B = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
var tenantA = TenantId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
var tenantB = TenantId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
var now = DateTimeOffset.UtcNow;
await using var admin = new MySqlConnection(options.ConnectionString);
await admin.OpenAsync();

async Task ExecuteAsync(string sql)
{
    await using var cmd = new MySqlCommand(sql, admin);
    await cmd.ExecuteNonQueryAsync();
}
async Task<int> CountAsync(string sql)
{
    await using var cmd = new MySqlCommand(sql, admin);
    return Convert.ToInt32(await cmd.ExecuteScalarAsync());
}

await ExecuteAsync($"""
    INSERT INTO c05_02_tenants VALUES
      (X'{A}','ACTIVE',42),(X'{B}','ACTIVE',42)
    """);
var directory = new SyntheticVerifiedHosts(now);
var host = new SyntheticCurrentHost();
var clock = new TestUtcClock();
var verifiedHost = new ResolveVerifiedTenantHost(directory, clock);
using var ingester = new MySqlDisposableNeutralRegistrationRecorder(ingestDsn,master,1);
using var sink = new IsolatedMailboxSink();
using var adapter = new MySqlDisposableEmailPossessionAdapter(proofDsn,master,1,sink);
var service = new VerifyNeutralRegistrationMailbox(
    host,verifiedHost,directory,clock,adapter,adapter);

async Task<Guid> AddIntentAsync(TenantId tenant, string mailbox)
{
    await ingester.RecordWithoutEnumerationAsync(
        tenant,mailbox,IPAddress.Parse("127.0.0.1").MapToIPv6().GetAddressBytes(),
        DateTimeOffset.UtcNow,CancellationToken.None);
    var prefix = MySqlRfc4122Guid.Encode(tenant.Value);
    var mailboxKey = HMACSHA256.HashData(master,
        "CRECISOL/C05-02/MAILBOX-FINGERPRINT/V1"u8);
    var plain = Encoding.UTF8.GetBytes(mailbox);
    var scoped = new byte[16 + plain.Length];
    prefix.CopyTo(scoped,0);
    plain.CopyTo(scoped,16);
    var digest = HMACSHA256.HashData(mailboxKey,scoped);
    try
    {
        await using var cmd = new MySqlCommand("""
            SELECT intent_id FROM c05_02_neutral_intents
             WHERE tenant_id=@tenant AND mailbox_digest=@digest
            """,admin);
        cmd.Parameters.Add("@tenant",MySqlDbType.Binary).Value = prefix;
        cmd.Parameters.Add("@digest",MySqlDbType.Binary).Value = digest;
        var raw = await cmd.ExecuteScalarAsync();
        return raw is byte[] row ? MySqlRfc4122Guid.Decode(row) :
            throw new InvalidOperationException("Synthetic encrypted intent unavailable.");
    }
    finally
    {
        CryptographicOperations.ZeroMemory(mailboxKey);
        CryptographicOperations.ZeroMemory(plain);
        CryptographicOperations.ZeroMemory(scoped);
        CryptographicOperations.ZeroMemory(digest);
    }
}

var a1 = await AddIntentAsync(tenantA,"owner.a@example.test");
var a2 = await AddIntentAsync(tenantA,"owner.second@example.test");
var a3 = await AddIntentAsync(tenantA,"owner.tamper@example.test");
var b1 = await AddIntentAsync(tenantB,"owner.b@example.test");
Pass(a1 != a2 && a2 != a3 && a1 != b1,
    "Opaque intents are distinct and tied to independently encrypted tenant mailboxes");

Pass(EmailPossessionDigest.FromToken(
        tenantA,a1,RandomNumberGenerator.GetBytes(32)).ToString() ==
     "EmailPossessionDigest[redacted]",
    "Challenge digest formatting hides protected hash");
using (var ephemeral = EphemeralEmailPossessionCode.Generate())
{
    var computed = ephemeral.CreateDigest(tenantA,a1);
    var oneTime = ephemeral.TransferToTrustedDeliveryOnce();
    try
    {
        Pass(computed.Matches(tenantA,a1,oneTime),
            "Domain-separated 256-bit challenge matches exact tenant and intent");
        Pass(!computed.Matches(tenantB,a1,oneTime) &&
             !computed.Matches(tenantA,a2,oneTime),
            "Same bearer fails across a different tenant or intent");
        try
        {
            _ = ephemeral.TransferToTrustedDeliveryOnce();
            throw new InvalidOperationException("Second secret transfer erroneously allowed.");
        }
        catch (InvalidOperationException e) when (
            e.Message == "Email proof code was already transferred.")
        {
            Pass(true,"Ephemeral capability can be transferred only once");
        }
    }
    finally { CryptographicOperations.ZeroMemory(oneTime); }
}

try
{
    using var _ = new MySqlDisposableEmailPossessionAdapter(
        "Server=example.org;Database=c05_02_disposable;User ID=fake;Password=fake",
        master,1,sink);
    throw new InvalidOperationException("Remote DSN accepted.");
}
catch (InvalidOperationException e) when (e.Message.StartsWith(
    "Mailbox proof must use local",StringComparison.Ordinal))
{
    Pass(true,"Cannot route mailbox proof to attacker-provided remote DSN");
}

host.Value = "a.crecisol.test";
var receiptA = await service.RequestProofAsync(a1,CancellationToken.None);
Pass(ReferenceEquals(receiptA,NeutralRegistrationReceipt.Acknowledged) &&
     sink.Available("owner.a@example.test"),
    "A intent returns neutral receipt and secret reaches ONLY protected A mailbox sink");
var aCode = sink.Take("owner.a@example.test");
try
{
    var hash = EmailPossessionDigest.FromToken(tenantA,a1,aCode);
    await using (var cmd = new MySqlCommand("""
        SELECT challenge_digest,lifecycle,attempts
          FROM c05_02_mailbox_proofs
         WHERE tenant_id=@tenant AND intent_id=@intent
        """,admin))
    {
        cmd.Parameters.Add("@tenant",MySqlDbType.Binary).Value =
            MySqlRfc4122Guid.Encode(tenantA.Value);
        cmd.Parameters.Add("@intent",MySqlDbType.Binary).Value =
            MySqlRfc4122Guid.Encode(a1);
        await using var rows = await cmd.ExecuteReaderAsync();
        Pass(await rows.ReadAsync() && rows.GetString(1)=="PENDING" &&
             rows.GetByte(2)==1 && CryptographicOperations.FixedTimeEquals(
                 (byte[])rows.GetValue(0),hash.CopyForProtectedDatabase()),
            "Database stores only bound proof digest and PENDING state, never bearer");
    }

    var knownSecret = Convert.ToHexString(aCode);
    var wrongToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    Pass(ReferenceEquals(
        await service.ConfirmProofAsync(a1,wrongToken,CancellationToken.None),
        NeutralRegistrationReceipt.Acknowledged) &&
        await CountAsync($"SELECT COUNT(*) FROM c05_02_mailbox_proofs WHERE tenant_id=X'{A}' AND lifecycle='PENDING'")==1,
        "Incorrect proof returns neutral response and does not change verified state");
    host.Value = "b.crecisol.test";
    Pass(ReferenceEquals(
        await service.ConfirmProofAsync(a1,knownSecret,CancellationToken.None),
        NeutralRegistrationReceipt.Acknowledged) &&
        await CountAsync($"SELECT COUNT(*) FROM c05_02_mailbox_proofs WHERE tenant_id=X'{B}'")==0,
        "Possessing A mailbox token under B verified host cannot verify or insert B proof");
    host.Value = "a.crecisol.test";

    // SQL CAS, not a JVM lock, decides the winner for 16 real MySQL clients.
    var successes = await Task.WhenAll(Enumerable.Range(0,16).Select(_ =>
        Task.Run(() => adapter.TryVerifyOnceAsync(
            tenantA,a1,hash,DateTimeOffset.UtcNow,CancellationToken.None))));
    Pass(successes.Count(x=>x)==1,
        "Sixteen concurrent MySQL proof confirmations produce exactly one winner");
    Pass(await CountAsync($"SELECT COUNT(*) FROM c05_02_mailbox_proofs WHERE tenant_id=X'{A}' AND lifecycle='VERIFIED'")==1,
        "Exactly one immutable verified proof persists in A, none in B");
    Pass(!await adapter.TryVerifyOnceAsync(
            tenantA,a1,hash,DateTimeOffset.UtcNow,CancellationToken.None),
        "A consumed email proof cannot be replayed");
    var deliveredBefore = sink.Delivered;
    Pass(ReferenceEquals(
        await service.RequestProofAsync(a1,CancellationToken.None),
        NeutralRegistrationReceipt.Acknowledged) &&
        sink.Delivered==deliveredBefore,
        "Already VERIFIED intent cannot issue another bearer or reveal existence");
}
finally { CryptographicOperations.ZeroMemory(aCode); }

host.Value="b.crecisol.test";
sink.FailNext = true;
Pass(ReferenceEquals(
    await service.RequestProofAsync(b1,CancellationToken.None),
    NeutralRegistrationReceipt.Acknowledged) &&
    await CountAsync($"SELECT COUNT(*) FROM c05_02_mailbox_proofs WHERE tenant_id=X'{B}' AND lifecycle='REVOKED'")==1,
    "Failed trusted mailbox delivery revokes challenge, returns constant neutral ACK");
Pass(ReferenceEquals(
    await service.RequestProofAsync(b1,CancellationToken.None),
    NeutralRegistrationReceipt.Acknowledged) &&
    sink.Available("owner.b@example.test"),
    "A revoked undelivered proof can be safely replaced within bounded retry count");
var bCode = sink.Take("owner.b@example.test");
try
{
    Pass(await CountAsync($"SELECT attempts FROM c05_02_mailbox_proofs WHERE tenant_id=X'{B}'")==2,
        "Delivery retry is counted in protected per-intent MySQL state");
    var receiptB = await service.ConfirmProofAsync(
        b1,Convert.ToHexString(bCode),CancellationToken.None);
    Pass(ReferenceEquals(receiptB,NeutralRegistrationReceipt.Acknowledged) &&
         await CountAsync($"SELECT COUNT(*) FROM c05_02_mailbox_proofs WHERE tenant_id=X'{B}' AND lifecycle='VERIFIED'")==1,
        "B mailbox possession confirmed once, with neutral HTTP-ready receipt");
}
finally { CryptographicOperations.ZeroMemory(bCode); }

host.Value="a.crecisol.test";
await service.RequestProofAsync(a2,CancellationToken.None);
var secondCode = sink.Take("owner.second@example.test");
try
{
    var proof = EmailPossessionDigest.FromToken(tenantA,a2,secondCode);
    await ExecuteAsync($"""
        UPDATE c05_02_mailbox_proofs
           SET issued_utc=DATE_SUB(UTC_TIMESTAMP(6),INTERVAL 20 MINUTE),
               expires_utc=DATE_SUB(UTC_TIMESTAMP(6),INTERVAL 1 SECOND)
         WHERE tenant_id=X'{A}'
           AND intent_id=X'{Convert.ToHexString(MySqlRfc4122Guid.Encode(a2))}'
        """);
    Pass(!await adapter.TryVerifyOnceAsync(
        tenantA,a2,proof,DateTimeOffset.UtcNow,CancellationToken.None),
        "Expired proof cannot be confirmed despite possession of the correct secret");
}
finally { CryptographicOperations.ZeroMemory(secondCode); }

await ExecuteAsync($"""
    UPDATE c05_02_neutral_intents SET tag=REPEAT(X'00',16)
     WHERE tenant_id=X'{A}' AND intent_id=X'{Convert.ToHexString(MySqlRfc4122Guid.Encode(a3))}'
    """);
var beforeTamper = sink.Delivered;
Pass(ReferenceEquals(
    await service.RequestProofAsync(a3,CancellationToken.None),
    NeutralRegistrationReceipt.Acknowledged) &&
    sink.Delivered==beforeTamper &&
    await CountAsync($"SELECT COUNT(*) FROM c05_02_mailbox_proofs WHERE tenant_id=X'{A}' AND lifecycle='REVOKED'")==1,
    "Tampered AEAD envelope denies delivery and safely revokes protected proof");

await ExecuteAsync($"UPDATE c05_02_tenants SET lifecycle='SUSPENDED' WHERE tenant_id=X'{B}'");
var prevB=sink.Delivered;
host.Value="b.crecisol.test";
Pass(ReferenceEquals(
    await service.RequestProofAsync(Guid.NewGuid(),CancellationToken.None),
    NeutralRegistrationReceipt.Acknowledged) &&
    sink.Delivered==prevB &&
    await CountAsync($"SELECT COUNT(*) FROM c05_02_mailbox_proofs WHERE tenant_id=X'{B}'")==1,
    "Suspended tenant cannot issue proofs or discover intent state");

host.Value="unverified.crecisol.test";
try
{
    await service.RequestProofAsync(a1,CancellationToken.None);
    throw new InvalidOperationException("Unverified host was allowed.");
}
catch (UnauthorizedAccessException)
{
    Pass(true,"Unverified host cannot select a tenant or begin a proof flow");
}
host.Value="a.crecisol.test";

await RequireDeny("SELECT * FROM c05_02_mailbox_proofs",
    "Proof service role cannot SELECT internal verification records");
await RequireDeny("UPDATE c05_02_mailbox_proofs SET lifecycle='VERIFIED' WHERE 1=0",
    "Proof service role cannot directly mark unverified mailbox as VERIFIED");
await RequireDeny("INSERT INTO c05_02_mailbox_proofs(tenant_id) VALUES (NULL)",
    "Proof service role cannot insert forged proof bypassing stored procedure");
await RequireDeny("DELETE FROM c05_02_mailbox_proofs WHERE 1=0",
    "Proof service role cannot erase protected evidence");
await RequireDeny("UPDATE c05_02_neutral_intents SET key_version=2 WHERE 1=0",
    "Proof service role cannot modify trusted encrypted recipient metadata");

CryptographicOperations.ZeroMemory(master);
Console.WriteLine($"PASS C05-02 .NET10 + MySQL8.4 independent mailbox proof: {checks} actual assertions");
Console.WriteLine("LIMIT: local in-memory delivery sink ONLY; no SMTP, real inbox, identity, grants or sessions");

internal sealed class TestUtcClock : ITrustedHostUtcClock
{
    public DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow;
}

internal sealed class SyntheticCurrentHost : ITrustedCurrentDirectHostReader
{
    public string Value { get; set; } = "a.crecisol.test";
    public ValueTask<CanonicalTenantHost?> ReadCurrentDirectHostAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<CanonicalTenantHost?>(CanonicalTenantHost.Parse(Value));
    }
}

internal sealed class SyntheticVerifiedHosts :
    ITrustedVerifiedHostCatalog,ITrustedTenantCatalogReader
{
    private readonly Dictionary<string,VerifiedTenantHostBinding> hosts=new();
    private readonly Dictionary<Guid,TenantCatalogSnapshot> tenants=new();

    public SyntheticVerifiedHosts(DateTimeOffset now)
    {
        Add("a.crecisol.test",Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),now);
        Add("b.crecisol.test",Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),now);
    }

    private void Add(string host,Guid id,DateTimeOffset now)
    {
        var tid=TenantId.From(id);
        var canonical=CanonicalTenantHost.Parse(host);
        var earlier=now.AddMinutes(-2);
        hosts.Add(host,VerifiedTenantHostBinding.RestoreFromTrustedCatalog(
            tid,canonical,TenantAddressMode.Subdomain,HostVerificationState.Verified,
            1,earlier,earlier,now.AddHours(2)));
        tenants.Add(id,TenantCatalogSnapshot.RestoreFromTrustedCatalog(
            tid,"Internal Business","Public Label",
            TenantLifecycleState.Active,TenantAddressMode.Subdomain,1,earlier));
    }

    public Task<IReadOnlyList<VerifiedTenantHostBinding>?> FindExactBindingsAsync(
        CanonicalTenantHost host,CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<VerifiedTenantHostBinding> values=
            hosts.TryGetValue(host.Value,out var found)
                ? new[] {found} : Array.Empty<VerifiedTenantHostBinding>();
        return Task.FromResult<IReadOnlyList<VerifiedTenantHostBinding>?>(values);
    }

    public Task<TenantCatalogSnapshot?> FindByIdAsync(
        TenantId tenant,CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        tenants.TryGetValue(tenant.Value,out var row);
        return Task.FromResult(row);
    }
}

internal sealed class IsolatedMailboxSink : ITrustedLocalProofMailboxSink,IDisposable
{
    private readonly Dictionary<string,byte[]> values=new(StringComparer.Ordinal);
    public bool FailNext { get; set; }
    public int Delivered { get; private set; }
    public async Task DeliverInTestOnlyAsync(
        string mailbox,ReadOnlyMemory<byte> secret,CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (FailNext)
        {
            FailNext=false;
            throw new InvalidOperationException("Synthetic mailbox sink failure.");
        }
        if (secret.Length!=32 || !mailbox.EndsWith("@example.test",StringComparison.Ordinal))
            throw new InvalidOperationException("No real email delivery is permitted.");
        await Task.Yield();
        values[mailbox]=secret.ToArray();
        Delivered++;
    }
    public bool Available(string mailbox) => values.ContainsKey(mailbox);
    public byte[] Take(string mailbox)
    {
        var secret=values[mailbox];
        values.Remove(mailbox);
        return secret;
    }
    public void Dispose()
    {
        foreach(var x in values.Values)
            CryptographicOperations.ZeroMemory(x);
        values.Clear();
    }
}
