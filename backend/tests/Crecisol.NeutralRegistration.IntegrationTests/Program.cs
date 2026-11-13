using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Crecisol.Application.Identity;
using Crecisol.Application.Tenancy;
using Crecisol.Domain.Identity;
using Crecisol.Domain.Tenancy;
using Crecisol.Infrastructure.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlConnector;

// A real .NET10 loopback HTTP + MySQL8.4 executable integration test, NOT
// authentication, account creation, email delivery or production DI activation.
if (args.Length == 0)
{
    Console.WriteLine("SKIP C05-02 neutral HTTP/MySQL integration: --integration required");
    return;
}
if (args.Length != 1 || args[0] != "--integration")
    throw new InvalidOperationException("Usage: --integration on disposable local MySQL8.4 only");

var adminDsn = Environment.GetEnvironmentVariable("CRECISOL_C05_02_NEUTRAL_ADMIN_DSN");
var limitedDsn = Environment.GetEnvironmentVariable("CRECISOL_C05_02_NEUTRAL_TEST_DSN");
var keyHex = Environment.GetEnvironmentVariable("CRECISOL_C05_02_NEUTRAL_MASTER_HEX");
if (string.IsNullOrWhiteSpace(adminDsn) ||
    string.IsNullOrWhiteSpace(limitedDsn) ||
    string.IsNullOrWhiteSpace(keyHex))
    throw new InvalidOperationException("Missing isolated C05-02 verification fixture.");
var key = Convert.FromHexString(keyHex);
if (key.Length != 32)
    throw new InvalidOperationException("Invalid server-owned C05-02 test key.");
var setup = new MySqlConnectionStringBuilder(adminDsn);
if (setup.Database != "c05_02_disposable" ||
    setup.Server is not ("127.0.0.1" or "localhost" or "::1"))
    throw new InvalidOperationException("Test may access only isolated local fixture.");

const string A = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
const string B = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
var now = DateTimeOffset.UtcNow;
var checks = 0;
void Pass(bool ok, string label)
{
    if (!ok) throw new InvalidOperationException("FAIL C05-02 neutral intake: " + label);
    checks++;
    Console.WriteLine("PASS C05-02 neutral intake: " + label);
}
async Task<int> ScalarAsync(MySqlConnection db, string sql)
{
    await using var command = new MySqlCommand(sql, db);
    return Convert.ToInt32(await command.ExecuteScalarAsync());
}
async Task ExecuteAsync(MySqlConnection db, string sql)
{
    await using var command = new MySqlCommand(sql, db);
    await command.ExecuteNonQueryAsync();
}
await using var admin = new MySqlConnection(setup.ConnectionString);
await admin.OpenAsync();
await ExecuteAsync(admin, $"""
    INSERT INTO c05_02_tenants (tenant_id,lifecycle,terms_version)
    VALUES (X'{A}','ACTIVE',42),(X'{B}','ACTIVE',42)
    """);

var directory = new TestTenantDirectory(now);
var builder = WebApplication.CreateBuilder();
builder.Logging.ClearProviders(); // Never log mailboxes or payloads from this harness.
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ITrustedHostUtcClock, SystemUtcClock>();
builder.Services.AddSingleton(directory);
builder.Services.AddSingleton<ITrustedVerifiedHostCatalog>(directory);
builder.Services.AddSingleton<ITrustedTenantCatalogReader>(directory);
builder.Services.AddSingleton<ITrustedCurrentDirectHostReader, TestDirectHostReader>();
builder.Services.AddSingleton<ITrustedRegistrationNetworkPeer, TestSocketPeerReader>();
builder.Services.AddSingleton<ResolveVerifiedTenantHost>();
builder.Services.AddSingleton<ITrustedNeutralRegistrationIntentRecorder>(_ =>
    new MySqlDisposableNeutralRegistrationRecorder(limitedDsn, key, 1));
builder.Services.AddTransient<RequestNeutralRegistrationIntent>();

await using var app = builder.Build();
app.MapPost("/__test_only/c05-02/public/registration-intent",
    async (HttpContext context, RequestNeutralRegistrationIntent useCase) =>
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Pragma = "no-cache";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";

        if (context.Request.ContentLength is > 512)
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        RegistrationPayload? payload;
        try
        {
            payload = await context.Request.ReadFromJsonAsync<RegistrationPayload>(
                cancellationToken: context.RequestAborted);
        }
        catch (System.Text.Json.JsonException)
        {
            payload = null;
        }
        try
        {
            await useCase.ExecuteAsync(payload?.Email, context.RequestAborted);
        }
        catch (UnauthorizedAccessException)
        {
            return Results.BadRequest();
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Fail closed without infrastructure details. Existence is never queried.
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
        return Results.Json(new { Code = NeutralRegistrationReceipt.Acknowledged.Code },
            statusCode: StatusCodes.Status202Accepted);
    });

await app.StartAsync();
var local = app.Urls.Single(x => x.StartsWith("http://127.0.0.1:", StringComparison.Ordinal));
using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

async Task<Observed> SendAsync(string host, string? mailbox,
    IReadOnlyDictionary<string, string>? extraHeaders = null)
{
    using var req = new HttpRequestMessage(HttpMethod.Post,
        local + "/__test_only/c05-02/public/registration-intent");
    req.Headers.Host = host;
    req.Content = JsonContent.Create(new RegistrationPayload(mailbox));
    if (extraHeaders is not null)
        foreach (var kv in extraHeaders)
            req.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
    using var resp = await client.SendAsync(req);
    return new Observed(resp.StatusCode, await resp.Content.ReadAsStringAsync(),
        resp.Headers.TryGetValues("Cache-Control", out var cache) &&
            cache.Contains("no-store"),
        resp.Headers.Contains("Set-Cookie"),
        resp.Headers.Contains("Location"));
}

var known = await SendAsync("a.crecisol.test", "Known@Example.TEST");
var unknown = await SendAsync("a.crecisol.test", "Unknown@Example.TEST");
Pass(known.Status == HttpStatusCode.Accepted && known.Body == unknown.Body &&
     unknown.Status == known.Status,
    "Known and unknown syntactically valid mailboxes have exactly same HTTP status and body");
Pass(known.CacheDenied && unknown.CacheDenied &&
     !known.HasCookie && !unknown.HasCookie &&
     !known.HasLocation && !unknown.HasLocation,
    "Neutral HTTP receipt has no cookie, credential, redirect or cache");
Pass(known.Body.Contains("REQUEST_ACCEPTED_IF_ELIGIBLE", StringComparison.Ordinal) &&
     !known.Body.Contains("Known@", StringComparison.Ordinal),
    "Constant public response excludes recipient and account state");
var hostileTenant = await SendAsync("a.crecisol.test", "test@business.test",
    new Dictionary<string,string> {["X-Tenant-Id"] = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"});
Pass(hostileTenant.Status == HttpStatusCode.Accepted &&
     hostileTenant.Body == known.Body,
    "Attacker-controlled X-Tenant-Id never selects a tenant or changes neutral response");
var bReceipt = await SendAsync("b.crecisol.test", "Known@Example.TEST");
Pass(bReceipt.Status == HttpStatusCode.Accepted &&
     bReceipt.Body == known.Body, "Same email across tenant A/B still has a neutral receipt");

var malformed = await SendAsync("a.crecisol.test", "not-an-email");
Pass(malformed.Status == HttpStatusCode.Accepted &&
     malformed.Body == known.Body,
    "Malformed address cannot enumerate accounts or create public errors");
var foreignHost = await SendAsync("foreign.crecisol.test", "Known@Example.TEST");
Pass(foreignHost.Status == HttpStatusCode.BadRequest,
    "Unknown direct host is rejected before any persistence");
var forwarded = await SendAsync("a.crecisol.test", "Known@Example.TEST",
    new Dictionary<string,string> {["X-Forwarded-Host"] = "b.crecisol.test"});
Pass(forwarded.Status == HttpStatusCode.BadRequest,
    "Spoofed forwarded host cannot become a tenant");
var forbiddenForwarded = await SendAsync("a.crecisol.test", "Known@Example.TEST",
    new Dictionary<string,string> {["Forwarded"] = "host=b.crecisol.test"});
Pass(forbiddenForwarded.Status == HttpStatusCode.BadRequest,
    "RFC Forwarded cannot become a trusted host");

Pass(await ScalarAsync(admin,
    $"SELECT COUNT(*) FROM c05_02_neutral_intents WHERE tenant_id=X'{A}'") == 3 &&
     await ScalarAsync(admin,
    $"SELECT COUNT(*) FROM c05_02_neutral_intents WHERE tenant_id=X'{B}'") == 1,
    "Only three valid A intents and one B intent persisted, never a foreign/invalid host");
Pass(await ScalarAsync(admin,
    $"SELECT COUNT(*) FROM c05_02_neutral_intents WHERE tenant_id=X'{A}' AND key_version=1") == 3,
    "All protected A intents retain explicit nonzero crypto key version");
Pass(await ScalarAsync(admin,
    $"SELECT COUNT(*) FROM c05_02_neutral_intents WHERE tenant_id=X'{A}' AND "+
    "LENGTH(ciphertext)=LENGTH(mailbox_digest)") == 0,
    "Opaque fingerprints are not plain email values stored as ciphertext");

// Verify AES-GCM envelope independently: exact canonical mailbox, distinct
// tenant-scoped keyed fingerprints and AEAD tamper rejection.
await using (var cmd = new MySqlCommand($"""
    SELECT mailbox_digest,nonce,tag,ciphertext,key_version
      FROM c05_02_neutral_intents WHERE tenant_id=X'{A}'
     ORDER BY captured_at_utc,intent_id LIMIT 1
    """, admin))
await using (var reader = await cmd.ExecuteReaderAsync())
{
    Pass(await reader.ReadAsync(), "Encrypted registration envelope is present");
    var hash = (byte[])reader.GetValue(0);
    var nonce = (byte[])reader.GetValue(1);
    var tag = (byte[])reader.GetValue(2);
    var ciphertext = (byte[])reader.GetValue(3);
    var version = reader.GetInt32(4);
    var tenant = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa").ToByteArray(bigEndian: true);
    var aad = new byte[52];
    tenant.CopyTo(aad, 0);
    System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(aad.AsSpan(16,4),version);
    hash.CopyTo(aad,20);
    var aesKey = HMACSHA256.HashData(key, "CRECISOL/C05-02/MAILBOX-ENCRYPTION/V1"u8);
    var clear = new byte[ciphertext.Length];
    try
    {
        using var cipher = new AesGcm(aesKey, 16);
        cipher.Decrypt(nonce,ciphertext,tag,clear,aad);
        Pass(Encoding.UTF8.GetString(clear) == "Known@example.test",
            "Encrypted mailbox decrypts only under server key+exact tenant+AAd");
        ciphertext[0] ^= 0x01;
        try
        {
            cipher.Decrypt(nonce,ciphertext,tag,clear,aad);
            throw new InvalidOperationException("FAIL tampered ciphertext accepted");
        }
        catch (AuthenticationTagMismatchException)
        {
            Pass(true, "AES-GCM rejects tampered ciphertext");
        }
    }
    finally
    {
        CryptographicOperations.ZeroMemory(aesKey);
        CryptographicOperations.ZeroMemory(clear);
        CryptographicOperations.ZeroMemory(aad);
        CryptographicOperations.ZeroMemory(hash);
        CryptographicOperations.ZeroMemory(nonce);
        CryptographicOperations.ZeroMemory(tag);
        CryptographicOperations.ZeroMemory(ciphertext);
    }
}

var aDigest = "";
var bDigest = "";
await using (var cmd = new MySqlCommand($"""
    SELECT HEX(mailbox_digest) FROM c05_02_neutral_intents
     WHERE tenant_id=X'{A}' ORDER BY captured_at_utc,intent_id LIMIT 1
    """,admin))
    aDigest = (string)(await cmd.ExecuteScalarAsync() ??
        throw new InvalidOperationException("A digest absent"));
await using (var cmd = new MySqlCommand($"""
    SELECT HEX(mailbox_digest) FROM c05_02_neutral_intents
     WHERE tenant_id=X'{B}' LIMIT 1
    """,admin))
    bDigest = (string)(await cmd.ExecuteScalarAsync() ??
        throw new InvalidOperationException("B digest absent"));
Pass(aDigest != bDigest, "Same mailbox fingerprint is cryptographically tenant-scoped");

// An attacker can forge X-Forwarded-For but cannot escape the socket-derived
// quota. 8 requests/peer/tenant/15min; every rate-limited request still 202.
for (var i = 0; i < 16; i++)
{
    var receipt = await SendAsync("a.crecisol.test", "sample+"+i+"@example.test",
        new Dictionary<string,string> {["X-Forwarded-For"] = "198.51.100."+(i+1)});
    Pass(receipt.Status == HttpStatusCode.Accepted && receipt.Body == known.Body,
        "Throttled/forwarder-spoofed HTTP always returns the same neutral ACK");
}
Pass(await ScalarAsync(admin,
    $"SELECT COUNT(*) FROM c05_02_neutral_intents WHERE tenant_id=X'{A}'") == 8,
    "Real MySQL atomic per-peer quota limits tenant A to eight persisted intents");
Pass(await ScalarAsync(admin,
    $"SELECT COUNT(*) FROM c05_02_neutral_intents WHERE tenant_id=X'{B}'") == 1,
    "Tenant A quota never exhausts tenant B independent quota");

// Revocation of tenant A between public intake and the SQL mutation is checked
// by the SQL procedure itself, not merely by the trusted directory fake.
await ExecuteAsync(admin,$"UPDATE c05_02_tenants SET lifecycle='SUSPENDED' WHERE tenant_id=X'{A}'");
var countBefore = await ScalarAsync(admin,
    $"SELECT SUM(attempts) FROM c05_02_neutral_quota WHERE tenant_id=X'{A}'");
var suspended = await SendAsync("a.crecisol.test","new+address@example.test");
var countAfter = await ScalarAsync(admin,
    $"SELECT SUM(attempts) FROM c05_02_neutral_quota WHERE tenant_id=X'{A}'");
Pass(suspended.Status == HttpStatusCode.Accepted && suspended.Body == known.Body &&
    countAfter == countBefore,
    "Suspended tenant receives neutral response but SQL creates no intent or quota entry");
await ExecuteAsync(admin,$"UPDATE c05_02_tenants SET lifecycle='ACTIVE' WHERE tenant_id=X'{A}'");

// Validate least-privilege role cannot inspect or write personal data.
async Task RequireDbDeny(string query, string label)
{
    await using var denied = new MySqlConnection(limitedDsn);
    await denied.OpenAsync();
    await using var cmd = new MySqlCommand(query,denied);
    try
    {
        await cmd.ExecuteNonQueryAsync();
    }
    catch (MySqlException ex) when (ex.Number is 1142 or 1143)
    {
        Pass(true,label);
        return;
    }
    throw new InvalidOperationException("FAIL direct database access allowed: "+label);
}
await RequireDbDeny("SELECT * FROM c05_02_neutral_intents",
    "Ingestion role cannot SELECT encrypted email or even mailbox digests");
await RequireDbDeny("UPDATE c05_02_tenants SET lifecycle='SUSPENDED' WHERE 1=0",
    "Ingestion role cannot mutate tenant state");
await RequireDbDeny("DELETE FROM c05_02_neutral_intents WHERE 1=0",
    "Ingestion role cannot delete private intent records");
await RequireDbDeny("INSERT INTO c05_02_neutral_quota (tenant_id) VALUES (NULL)",
    "Ingestion role cannot change or bypass the quota table");
await RequireDbDeny("UPDATE c05_02_neutral_intents SET key_version=2 WHERE 1=0",
    "Ingestion role cannot replace cryptographic metadata");
await app.StopAsync();
CryptographicOperations.ZeroMemory(key);

Console.WriteLine($"PASS C05-02 REAL neutral HTTP + MySqlConnector/MySQL8.4: {checks} assertions");
Console.WriteLine("LIMIT: loopback test host only; no real mailbox delivery, account registration or authenticated membership");

// Types are test-private; no platform/tenant operator account is created.
internal sealed record RegistrationPayload(string? Email);
internal sealed record Observed(HttpStatusCode Status, string Body,
    bool CacheDenied, bool HasCookie, bool HasLocation);

internal sealed class SystemUtcClock : ITrustedHostUtcClock
{
    public DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow;
}

internal sealed class TestDirectHostReader(IHttpContextAccessor context)
    : ITrustedCurrentDirectHostReader
{
    public ValueTask<CanonicalTenantHost?> ReadCurrentDirectHostAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var request = context.HttpContext?.Request;
        if (request is null || request.Host.Port.HasValue ||
            request.Headers.ContainsKey("Forwarded") ||
            request.Headers.ContainsKey("X-Forwarded-Host") ||
            request.Host.Host is not ("a.crecisol.test" or "b.crecisol.test"))
            return ValueTask.FromResult<CanonicalTenantHost?>(null);
        return ValueTask.FromResult<CanonicalTenantHost?>(
            CanonicalTenantHost.Parse(request.Host.Host));
    }
}

internal sealed class TestSocketPeerReader(IHttpContextAccessor context)
    : ITrustedRegistrationNetworkPeer
{
    public ValueTask<ReadOnlyMemory<byte>?> ReadVerifiedPeerAddressAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var peer = context.HttpContext?.Connection.RemoteIpAddress;
        if (peer is null) return ValueTask.FromResult<ReadOnlyMemory<byte>?>(null);
        var bytes = peer.MapToIPv6().GetAddressBytes();
        return ValueTask.FromResult<ReadOnlyMemory<byte>?>(bytes);
    }
}

internal sealed class TestTenantDirectory : ITrustedTenantCatalogReader,
    ITrustedVerifiedHostCatalog
{
    private readonly Dictionary<string, VerifiedTenantHostBinding> hosts = new();
    private readonly Dictionary<Guid,TenantCatalogSnapshot> tenants = new();

    public TestTenantDirectory(DateTimeOffset nowUtc)
    {
        Add("a.crecisol.test", Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), nowUtc);
        Add("b.crecisol.test", Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), nowUtc);
    }

    private void Add(string name, Guid id, DateTimeOffset now)
    {
        var tenant = TenantId.From(id);
        var host = CanonicalTenantHost.Parse(name);
        var changed = now.AddMinutes(-2);
        hosts[name] = VerifiedTenantHostBinding.RestoreFromTrustedCatalog(
            tenant,host,TenantAddressMode.Subdomain,
            HostVerificationState.Verified,1,changed,changed,now.AddHours(2));
        tenants[id] = TenantCatalogSnapshot.RestoreFromTrustedCatalog(
            tenant,"Private Tenant","Tenant Display",
            TenantLifecycleState.Active,TenantAddressMode.Subdomain,1,changed);
    }

    public Task<IReadOnlyList<VerifiedTenantHostBinding>?> FindExactBindingsAsync(
        CanonicalTenantHost host, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<VerifiedTenantHostBinding> matches =
            hosts.TryGetValue(host.Value, out var found)
                ? new[] { found } : Array.Empty<VerifiedTenantHostBinding>();
        return Task.FromResult<IReadOnlyList<VerifiedTenantHostBinding>?>(matches);
    }

    public Task<TenantCatalogSnapshot?> FindByIdAsync(
        TenantId id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        tenants.TryGetValue(id.Value,out var result);
        return Task.FromResult(result);
    }
}
