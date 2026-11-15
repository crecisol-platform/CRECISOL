using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Crecisol.Infrastructure.Tenancy;
using Crecisol.NeutralRegistration.IntegrationTests;
using Crecisol.Api.Contracts;
using Crecisol.CredentialSecurity;
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
var integratedProofDsn = Environment.GetEnvironmentVariable("CRECISOL_C05_PROOF_TEST_DSN");
var candidateDsn = Environment.GetEnvironmentVariable("CRECISOL_C05_CANDIDATE_DSN");
var dispatchDsn = Environment.GetEnvironmentVariable("CRECISOL_C05_DISPATCH_DSN");
var activationDsn = Environment.GetEnvironmentVariable("CRECISOL_C05_ACTIVATION_DSN");
var directoryDsn = Environment.GetEnvironmentVariable("CRECISOL_C05_DIRECTORY_DSN");
var rateSetupDsn = Environment.GetEnvironmentVariable("CRECISOL_C05_RATE_SETUP_DSN");
var rateRunnerDsn = Environment.GetEnvironmentVariable("CRECISOL_C05_RATE_RUNNER_DSN");
var mfaSetupDsn = Environment.GetEnvironmentVariable("CRECISOL_C05_MFA_SETUP_DSN");
var mfaWorkerDsn = Environment.GetEnvironmentVariable("CRECISOL_C05_MFA_WORKER_DSN");
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
builder.Services.AddTransient<INeutralRegistrationIntentHandler, ApplicationNeutralRegistrationIntentHandler>();
builder.Services.AddTransient<NeutralRegistrationIntake>();
using var localMailbox = new LocalProofMailbox();
using var integratedProof = string.IsNullOrWhiteSpace(integratedProofDsn)
    ? null : new MySqlDisposableEmailPossessionAdapter(integratedProofDsn, key, 1, localMailbox);
if (integratedProof is not null)
{
    builder.Services.AddSingleton<ITrustedEmailPossessionLedger>(integratedProof);
    builder.Services.AddSingleton<ITrustedEmailPossessionDelivery>(integratedProof);
    builder.Services.AddTransient<VerifyNeutralRegistrationMailbox>();
}

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


// Integrated path ONLY in this isolated loopback test executable, never Program.cs.
app.MapPost("/__test_only/c05-integration/registration-intent",
    async (HttpContext context, NeutralRegistrationIntake intake) =>
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Pragma = "no-cache";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        if (!NeutralRegistrationTransportPreflight.IsEligible(context.Request))
            return Results.BadRequest();
        try
        {
            var body = new byte[checked((int)context.Request.ContentLength!.Value)];
            await context.Request.Body.ReadExactlyAsync(body.AsMemory(), context.RequestAborted);
            var trailing = new byte[1];
            if (await context.Request.Body.ReadAsync(trailing.AsMemory(), context.RequestAborted) != 0)
                return Results.BadRequest();
            var result = await intake.SubmitAsync(context.Request, body, context.RequestAborted);
            return result == NeutralRegistrationIntakeOutcome.Acknowledged
                ? Results.Json(new { Code = NeutralRegistrationReceipt.Acknowledged.Code },
                    statusCode: StatusCodes.Status202Accepted)
                : Results.BadRequest();
        }
        catch (UnauthorizedAccessException) { return Results.BadRequest(); }
        catch (EndOfStreamException) { return Results.BadRequest(); }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { throw; }
        catch (Exception) { return Results.StatusCode(StatusCodes.Status503ServiceUnavailable); }
    });

if (integratedProof is not null)
{
    // These routes exist ONLY in the loopback test executable, not in the product API.
    app.MapPost("/__test_only/c05-integration/proof-request",
        async (HttpContext ctx, VerifyNeutralRegistrationMailbox verifier) =>
        {
            LocalProofHttp.SetNeutralHeaders(ctx);
            var data = await LocalProofHttp.ReadAsync(ctx.Request, false, ctx.RequestAborted);
            if (data is null) return Results.BadRequest();
            try
            {
                _ = await verifier.RequestProofAsync(data.Intent, ctx.RequestAborted);
                return Results.Json(new { Code = NeutralRegistrationReceipt.Acknowledged.Code },
                    statusCode: StatusCodes.Status202Accepted);
            }
            catch (UnauthorizedAccessException) { return Results.BadRequest(); }
            catch (OperationCanceledException) when (ctx.RequestAborted.IsCancellationRequested) { throw; }
            catch (Exception) { return Results.StatusCode(StatusCodes.Status503ServiceUnavailable); }
        });
    app.MapPost("/__test_only/c05-integration/proof-confirm",
        async (HttpContext ctx, VerifyNeutralRegistrationMailbox verifier) =>
        {
            LocalProofHttp.SetNeutralHeaders(ctx);
            var data = await LocalProofHttp.ReadAsync(ctx.Request, true, ctx.RequestAborted);
            if (data is null) return Results.BadRequest();
            try
            {
                _ = await verifier.ConfirmProofAsync(
                    data.Intent, data.Code, ctx.RequestAborted);
                return Results.Json(new { Code = NeutralRegistrationReceipt.Acknowledged.Code },
                    statusCode: StatusCodes.Status202Accepted);
            }
            catch (UnauthorizedAccessException) { return Results.BadRequest(); }
            catch (OperationCanceledException) when (ctx.RequestAborted.IsCancellationRequested) { throw; }
            catch (Exception) { return Results.StatusCode(StatusCodes.Status503ServiceUnavailable); }
        });
}

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
// Forty real parallel .NET MySqlConnector calls from a SECOND trusted socket
// address must admit exactly 8 rows in the same 15-minute quota bucket.
// This deliberately stresses simultaneous initial quota creation and renewal
// rather than only sequential HTTP requests.
var priorCount = await ScalarAsync(admin,
    $"SELECT COUNT(*) FROM c05_02_neutral_intents WHERE tenant_id=X'{A}'");
var competingPeer = IPAddress.Parse("2001:db8::ff").GetAddressBytes();
using (var concurrentLedger = new MySqlDisposableNeutralRegistrationRecorder(
    limitedDsn, key, 1))
{
    var race = Enumerable.Range(0, 40).Select(i =>
        Task.Run(() => concurrentLedger.RecordWithoutEnumerationAsync(
            TenantId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")),
            "parallel+" + i + "@example.test", competingPeer,
            DateTimeOffset.UtcNow, CancellationToken.None)));
    await Task.WhenAll(race);
}
var afterRace = await ScalarAsync(admin,
    $"SELECT COUNT(*) FROM c05_02_neutral_intents WHERE tenant_id=X'{A}'");
Pass(afterRace == priorCount + 8,
    "Forty concurrent real MySQL intake calls admit exactly eight within a tenant quota");
Pass(await ScalarAsync(admin,
    $"SELECT COUNT(*) FROM c05_02_neutral_intents WHERE tenant_id=X'{B}'") == 1,
    "Concurrent tenant A quota cannot insert or suppress tenant B records");


// The new integrated path is tested AFTER the historical MySQL quota and race
// cases, preserving all previous counts and ensuring tenant B still has quota.
async Task<Observed> SendBridgeAsync(string host, string json,
    IReadOnlyDictionary<string, string>? headers = null, string media = "application/json")
{
    using var req = new HttpRequestMessage(HttpMethod.Post,
        local + "/__test_only/c05-integration/registration-intent");
    req.Headers.Host = host;
    var bytes = new ByteArrayContent(Encoding.UTF8.GetBytes(json));
    bytes.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(media);
    req.Content = bytes;
    if (headers is not null)
        foreach (var pair in headers) req.Headers.TryAddWithoutValidation(pair.Key, pair.Value);
    using var res = await client.SendAsync(req);
    return new Observed(res.StatusCode, await res.Content.ReadAsStringAsync(),
        res.Headers.TryGetValues("Cache-Control", out var cache) && cache.Contains("no-store"),
        res.Headers.Contains("Set-Cookie"), res.Headers.Contains("Location"));
}
var aBeforeBridge = await ScalarAsync(admin,
    $"SELECT COUNT(*) FROM c05_02_neutral_intents WHERE tenant_id=X'{A}'");
var bBeforeBridge = await ScalarAsync(admin,
    $"SELECT COUNT(*) FROM c05_02_neutral_intents WHERE tenant_id=X'{B}'");
var bridge = await SendBridgeAsync("b.crecisol.test", "{\"mailbox\":\"Integrated+One@EXAMPLE.TEST\"}");
Pass(bridge.Status == HttpStatusCode.Accepted && bridge.Body == known.Body &&
     bridge.CacheDenied && !bridge.HasCookie && !bridge.HasLocation,
    "Actual HTTP intake is neutral, with no session, redirect or cache");
Pass(await ScalarAsync(admin,
    $"SELECT COUNT(*) FROM c05_02_neutral_intents WHERE tenant_id=X'{B}'")
    == bBeforeBridge + 1 &&
    await ScalarAsync(admin,
    $"SELECT COUNT(*) FROM c05_02_neutral_intents WHERE tenant_id=X'{A}'")
    == aBeforeBridge,
    "Actual HTTP to Application to MySQL persists one tenant B intent, zero A");
async Task DenyBridgeAsync(string name, string host, string json,
    IReadOnlyDictionary<string, string>? headers = null, string media = "application/json")
{
    var response = await SendBridgeAsync(host, json, headers, media);
    Pass(response.Status == HttpStatusCode.BadRequest && response.CacheDenied &&
         !response.HasCookie && !response.HasLocation,
        "Integrated pre-database denial " + name);
}
await DenyBridgeAsync("forged tenant A from B", "b.crecisol.test",
    "{\"mailbox\":\"other@example.test\"}",
    new Dictionary<string,string> { ["X-Tenant-Id"] = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa" });
await DenyBridgeAsync("forged actor", "b.crecisol.test",
    "{\"mailbox\":\"other@example.test\"}",
    new Dictionary<string,string> { ["X-Actor-Id"] = "forged" });
await DenyBridgeAsync("forged role", "b.crecisol.test",
    "{\"mailbox\":\"other@example.test\",\"role\":\"SuperAdmin\"}");
await DenyBridgeAsync("forged Forwarded host", "b.crecisol.test",
    "{\"mailbox\":\"other@example.test\"}",
    new Dictionary<string,string> { ["X-Forwarded-Host"] = "a.crecisol.test" });
await DenyBridgeAsync("forged XFF", "b.crecisol.test",
    "{\"mailbox\":\"other@example.test\"}",
    new Dictionary<string,string> { ["X-Forwarded-For"] = "198.51.100.9" });
await DenyBridgeAsync("JSON duplicate", "b.crecisol.test",
    "{\"mailbox\":\"a@example.test\",\"mailbox\":\"b@example.test\"}");
await DenyBridgeAsync("JSON tenant selector", "b.crecisol.test",
    "{\"mailbox\":\"other@example.test\",\"tenant\":\"A\"}");
await DenyBridgeAsync("invalid media", "b.crecisol.test",
    "{\"mailbox\":\"other@example.test\"}", media: "text/plain");
await DenyBridgeAsync("unknown direct host", "foreign.crecisol.test",
    "{\"mailbox\":\"other@example.test\"}");
Pass(await ScalarAsync(admin,
    $"SELECT COUNT(*) FROM c05_02_neutral_intents WHERE tenant_id=X'{B}'")
    == bBeforeBridge + 1 &&
    await ScalarAsync(admin,
    $"SELECT COUNT(*) FROM c05_02_neutral_intents WHERE tenant_id=X'{A}'")
    == aBeforeBridge,
    "Adversarial intake requests do not write records in either tenant");

if (integratedProof is not null)
{
    // Select a non-public opaque ID only through the protected fixture admin connection.
    byte[] storedId;
    await using (var cmd = new MySqlCommand(
        "SELECT intent_id FROM c05_02_neutral_intents WHERE tenant_id=X'" +
        B + "' ORDER BY captured_at_utc DESC, intent_id DESC LIMIT 1", admin))
    {
        storedId = (byte[])(await cmd.ExecuteScalarAsync() ??
            throw new InvalidOperationException("Fixture intent missing."));
    }
    var proofIntent = MySqlRfc4122Guid.Decode(storedId);
    CryptographicOperations.ZeroMemory(storedId);
    var intentJson = "{\"intentId\":\"" + proofIntent.ToString("D") + "\"}";

    async Task<Observed> SendProofAsync(string endpoint, string host, string json,
        IReadOnlyDictionary<string,string>? headers = null)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post,
            local + "/__test_only/c05-integration/" + endpoint);
        message.Headers.Host = host;
        var payload = new ByteArrayContent(Encoding.UTF8.GetBytes(json));
        payload.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        message.Content = payload;
        if (headers is not null)
            foreach (var pair in headers)
                message.Headers.TryAddWithoutValidation(pair.Key, pair.Value);
        using var response = await client.SendAsync(message);
        return new Observed(response.StatusCode,
            await response.Content.ReadAsStringAsync(),
            response.Headers.TryGetValues("Cache-Control", out var cache) &&
                cache.Contains("no-store"), response.Headers.Contains("Set-Cookie"),
            response.Headers.Contains("Location"));
    }

    var deliveriesBefore = localMailbox.Delivered;
    var start = await SendProofAsync("proof-request", "b.crecisol.test", intentJson);
    Pass(start.Status == HttpStatusCode.Accepted &&
         start.Body == known.Body && start.CacheDenied &&
         !start.HasCookie && !start.HasLocation &&
         localMailbox.Delivered == deliveriesBefore + 1 &&
         localMailbox.Has("Integrated+One@example.test"),
         "Same-process HTTP registration to one-time encrypted mailbox proof delivery");
    var challenge = localMailbox.Take("Integrated+One@example.test");
    try
    {
        var code = Convert.ToHexString(challenge);
        var foreignRequest = await SendProofAsync("proof-request", "a.crecisol.test", intentJson);
        Pass(foreignRequest.Status == HttpStatusCode.Accepted &&
             foreignRequest.Body == start.Body &&
             localMailbox.Delivered == deliveriesBefore + 1,
             "Foreign tenant cannot request another delivery from protected intent");

        var bad = await SendProofAsync("proof-confirm", "b.crecisol.test",
            "{\"intentId\":\"" + proofIntent.ToString("D") +
            "\",\"code\":\"" + new string('0',64) + "\"}");
        Pass(bad.Status == HttpStatusCode.Accepted &&
             bad.Body == start.Body &&
             await ScalarAsync(admin, "SELECT COUNT(*) FROM c05_02_mailbox_proofs WHERE tenant_id=X'" +
                B + "' AND lifecycle='PENDING'") == 1,
             "Invalid token is neutral and leaves B proof pending");

        var cross = await SendProofAsync("proof-confirm", "a.crecisol.test",
            "{\"intentId\":\"" + proofIntent.ToString("D") + "\",\"code\":\"" + code + "\"}");
        Pass(cross.Status == HttpStatusCode.Accepted &&
             cross.Body == start.Body &&
             await ScalarAsync(admin, "SELECT COUNT(*) FROM c05_02_mailbox_proofs WHERE tenant_id=X'" +
                A + "' AND lifecycle='VERIFIED'") == 0,
             "B token on tenant A cannot assert mailbox possession");

        var validBody = "{\"intentId\":\"" + proofIntent.ToString("D") +
            "\",\"code\":\"" + code + "\"}";
        var valid = await SendProofAsync("proof-confirm", "b.crecisol.test", validBody);
        Pass(valid.Status == HttpStatusCode.Accepted &&
             valid.Body == start.Body && valid.CacheDenied &&
             !valid.HasCookie && !valid.HasLocation &&
             await ScalarAsync(admin, "SELECT COUNT(*) FROM c05_02_mailbox_proofs WHERE tenant_id=X'" +
                B + "' AND lifecycle='VERIFIED'") == 1,
             "One-time proof accepted only by B and MySQL CAS, without session");
        var replay = await SendProofAsync("proof-confirm", "b.crecisol.test", validBody);
        Pass(replay.Status == HttpStatusCode.Accepted &&
             replay.Body == start.Body &&
             await ScalarAsync(admin, "SELECT COUNT(*) FROM c05_02_mailbox_proofs WHERE tenant_id=X'" +
                B + "' AND lifecycle='VERIFIED'") == 1,
             "Proof replay is neutral and cannot create another verified record");
    }
    finally { CryptographicOperations.ZeroMemory(challenge); }

    if (!string.IsNullOrWhiteSpace(candidateDsn))
    {
        // This is a BACKEND-ONLY trusted fixture handoff. No public route
        // accepts a PHC string, actor ID or candidate enrollment command.
        using var pending = new MySqlDisposableVerifiedCredentialCandidateStore(
            candidateDsn, key, 1);
        var tenantB = TenantId.From(
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
        var tenantA = TenantId.From(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
        var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(36));
        var hasher = new Argon2idPasswordHasher(
            PasswordHashingPolicy.Default, new LocalOnlyFixturePasswordScreen());
        var phc = hasher.CreateVerifier(password).ExportForTrustedCredentialStore();

        Pass(!await pending.TryCreatePendingAsync(
                 tenantA, proofIntent, phc, CancellationToken.None),
            "Tenant A cannot promote B's verified mailbox intent");
        Pass(!await pending.TryCreatePendingAsync(
                 tenantB, proofIntent, "not-a-phc", CancellationToken.None),
            "Untrusted or malformed verifier never reaches candidate procedure");
        Pass(!await pending.TryCreatePendingAsync(
                 tenantB, Guid.NewGuid(), phc, CancellationToken.None),
            "Unknown mailbox intent cannot provision a candidate");

        var first = await Task.WhenAll(Enumerable.Range(0, 12)
            .Select(_ => Task.Run(() => pending.TryCreatePendingAsync(
                tenantB, proofIntent, phc, CancellationToken.None))));
        Pass(first.Count(wrote => wrote) == 1 &&
             await ScalarAsync(admin,
                 "SELECT COUNT(*) FROM c05_02_candidate_accounts") == 1,
            "Twelve racing verified-proof enrollments create exactly one pending account");
        var candidateState = "";
        var candidatePhc = "";
        await using (var read = new MySqlCommand(
            "SELECT lifecycle,verifier_phc FROM c05_02_candidate_accounts", admin))
        {
            await using var rows = await read.ExecuteReaderAsync();
            if (await rows.ReadAsync())
            {
                candidateState = rows.GetString(0);
                candidatePhc = rows.GetString(1);
            }
        }
        Pass(candidateState == "PENDING_MEMBERSHIP" &&
             string.Equals(candidatePhc, phc, StringComparison.Ordinal) &&
             hasher.Verify(password, candidatePhc).Matched,
            "Pending-only credential is a real Argon2id PHC, no active membership");
        Pass(!await pending.TryCreatePendingAsync(
                 tenantB, proofIntent, phc, CancellationToken.None),
            "Replay of an already-promoted verified proof is denied");

        // Least privilege: the candidate connector can execute only the
        // procedure and read encrypted intent columns, never account records.
        var directReadDenied = false;
        await using (var limited = new MySqlConnection(candidateDsn))
        {
            await limited.OpenAsync();
            await using var read = new MySqlCommand(
                "SELECT verifier_phc FROM c05_02_candidate_accounts", limited);
            try { _ = await read.ExecuteScalarAsync(); }
            catch (MySqlException ex) when (ex.Number is 1142 or 1143)
            {
                directReadDenied = true;
            }
        }
        Pass(directReadDenied, "Pending candidate DB role cannot SELECT credential verifiers");
        if (string.IsNullOrWhiteSpace(activationDsn) ||
            string.IsNullOrWhiteSpace(directoryDsn) ||
            string.IsNullOrWhiteSpace(rateSetupDsn) ||
            string.IsNullOrWhiteSpace(rateRunnerDsn) ||
            string.IsNullOrWhiteSpace(mfaSetupDsn) ||
            string.IsNullOrWhiteSpace(mfaWorkerDsn))
            throw new InvalidOperationException(
                "Integrated C05 candidate requires a separate approved-membership and login-directory fixture.");
        await LocalApprovedMembershipHarness.VerifyAsync(
            admin, activationDsn, directoryDsn, rateSetupDsn, rateRunnerDsn,
            mfaSetupDsn, mfaWorkerDsn, key, password, proofIntent,
            directory, directory, new SystemUtcClock(), Pass, CancellationToken.None);

        async Task<Guid> LatestBIntentAsync()
        {
            await using var query = new MySqlCommand(
                "SELECT intent_id FROM c05_02_neutral_intents WHERE tenant_id=X'" +
                B + "' ORDER BY captured_at_utc DESC, intent_id DESC LIMIT 1", admin);
            var raw = (byte[])(await query.ExecuteScalarAsync() ??
                throw new InvalidOperationException("Latest protected intent unavailable."));
            try { return MySqlRfc4122Guid.Decode(raw); }
            finally { CryptographicOperations.ZeroMemory(raw); }
        }

        var unproved = await SendBridgeAsync("b.crecisol.test",
            "{\"mailbox\":\"Unverified@Example.test\"}");
        var unprovedIntent = await LatestBIntentAsync();
        Pass(unproved.Status == HttpStatusCode.Accepted &&
             !await pending.TryCreatePendingAsync(
                 tenantB, unprovedIntent, phc, CancellationToken.None),
             "Recorded mailbox without verified proof can never create a credential");

        var variant = await SendBridgeAsync("b.crecisol.test",
            "{\"mailbox\":\"integrated+one@example.test\"}");
        var variantIntent = await LatestBIntentAsync();
        var variantJson = "{\"intentId\":\"" + variantIntent.ToString("D") + "\"}";
        var variantStart = await SendProofAsync(
            "proof-request", "b.crecisol.test", variantJson);
        Pass(variant.Status == HttpStatusCode.Accepted &&
             variantStart.Status == HttpStatusCode.Accepted &&
             localMailbox.Has("integrated+one@example.test"),
            "Distinct case-sensitive mailbox is independently challenged");
        var variantToken = localMailbox.Take("integrated+one@example.test");
        try
        {
            var verifyBody = "{\"intentId\":\"" + variantIntent.ToString("D") +
                "\",\"code\":\"" + Convert.ToHexString(variantToken) + "\"}";
            var verifiedVariant = await SendProofAsync(
                "proof-confirm", "b.crecisol.test", verifyBody);
            Pass(verifiedVariant.Status == HttpStatusCode.Accepted &&
                 !await pending.TryCreatePendingAsync(
                     tenantB, variantIntent, phc, CancellationToken.None) &&
                 await ScalarAsync(admin,
                     "SELECT COUNT(*) FROM c05_02_candidate_accounts") == 1,
                "Case-folded login collision fails closed without merging distinct inboxes");
        }
        finally { CryptographicOperations.ZeroMemory(variantToken); }
    }

    var deliveriesBeforeInvalidProofs = localMailbox.Delivered;
    async Task DenyProofAsync(string label, string endpoint, string json,
        IReadOnlyDictionary<string,string>? headers = null,
        string host = "b.crecisol.test")
    {
        var denied = await SendProofAsync(endpoint, host, json, headers);
        Pass(denied.Status == HttpStatusCode.BadRequest &&
             denied.CacheDenied && !denied.HasCookie && !denied.HasLocation,
             "Proof transport fails closed: " + label);
    }
    await DenyProofAsync("forged tenant selector", "proof-request",
        "{\"intentId\":\"" + proofIntent.ToString("D") + "\",\"tenantId\":\"A\"}");
    await DenyProofAsync("duplicate intent key", "proof-request",
        "{\"intentId\":\"" + proofIntent.ToString("D") + "\",\"intentId\":\"" +
        Guid.NewGuid().ToString("D") + "\"}");
    await DenyProofAsync("forged actor", "proof-request", intentJson,
        new Dictionary<string,string> { ["X-Actor-Id"] = "spoof" });
    await DenyProofAsync("forwarded host", "proof-request", intentJson,
        new Dictionary<string,string> { ["X-Forwarded-Host"] = "a.crecisol.test" });
    await DenyProofAsync("unverified host", "proof-request", intentJson,
        host: "foreign.crecisol.test");
    await DenyProofAsync("mailbox injection in proof", "proof-confirm",
        "{\"intentId\":\"" + proofIntent.ToString("D") +
        "\",\"mailbox\":\"victim@example.test\"}");
    Pass(localMailbox.Delivered == deliveriesBeforeInvalidProofs,
         "Malformed, foreign and replay requests deliver no additional token");

    if (!string.IsNullOrWhiteSpace(dispatchDsn))
    {
        // The internal worker receives a tenant/intent from a committed SQL
        // outbox row, NOT from the anonymous registration response or query.
        // Older fixture requests are marked handled by the test-only setup
        // connection so this section can isolate one fresh enqueue.
        await ExecuteAsync(admin, """
            UPDATE c05_02_proof_dispatch SET state='COMPLETED',
                claim_owner=NULL,claimed_until_utc=NULL
            WHERE state<>'COMPLETED'
            """);
        var queue = new MySqlDisposableNeutralProofDispatch(dispatchDsn);
        var autoBefore = localMailbox.Delivered;
        var auto = await SendBridgeAsync("b.crecisol.test",
            "{\"mailbox\":\"Dispatch+Receiver@Example.test\"}");
        Pass(auto.Status == HttpStatusCode.Accepted &&
             auto.Body == known.Body && auto.CacheDenied &&
             !auto.HasCookie && !auto.HasLocation &&
             localMailbox.Delivered == autoBefore &&
             await ScalarAsync(admin,
                 "SELECT COUNT(*) FROM c05_02_proof_dispatch WHERE state='READY'") == 1,
            "Public anonymous registration returns no intent or bearer while SQL enqueues one private dispatch");

        var concurrent = await Task.WhenAll(Enumerable.Range(0, 16)
            .Select(_ => Task.Run(async () =>
            {
                var owner = Guid.NewGuid();
                var claim = await queue.ClaimNextAsync(owner, CancellationToken.None);
                return (Owner: owner, Claim: claim);
            })));
        var winners = concurrent.Where(x => x.Claim is not null).ToArray();
        Pass(winners.Length == 1 &&
             winners[0].Claim!.Tenant.Value == Guid.Parse(
                 "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb") &&
             winners[0].Claim!.Intent != Guid.Empty,
            "Sixteen concurrent workers safely claim one opaque B intent exactly once");
        var winner = winners[0];
        var claimed = winner.Claim!;
        var tenantBForDispatch = TenantId.From(
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
        var tenantAForDispatch = TenantId.From(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
        var wrongTenantClaim = new ClaimedNeutralProofDispatch(
            tenantAForDispatch, claimed.Intent);
        Pass(!await queue.CompleteAsync(
                 wrongTenantClaim, winner.Owner, CancellationToken.None) &&
             !await queue.CompleteAsync(
                 claimed, Guid.NewGuid(), CancellationToken.None) &&
             !await queue.CompleteAsync(
                 claimed, winner.Owner, CancellationToken.None),
            "Foreign tenant, wrong owner and unissued proof cannot complete a claimed dispatch");

        // The trusted fixture host catalog maps SQL-owned tenant to a
        // verified hostname. No header, body or browser chooses that scope.
        var trustedHost = claimed.Tenant.Value == tenantBForDispatch.Value
            ? "b.crecisol.test" : throw new InvalidOperationException(
                "Unknown tenant returned by trusted dispatch queue.");
        var proofStart = await SendProofAsync("proof-request", trustedHost,
            "{\"intentId\":\"" + claimed.Intent.ToString("D") + "\"}");
        Pass(proofStart.Status == HttpStatusCode.Accepted &&
             proofStart.Body == known.Body &&
             localMailbox.Delivered == autoBefore + 1 &&
             localMailbox.Has("Dispatch+Receiver@example.test"),
            "Server-owned dispatch issues proof to recorded mailbox without browser-provided intent");
        var deliveredLink = localMailbox.TakeLink("Dispatch+Receiver@example.test");
        try
        {
            Pass(deliveredLink.Tenant.Value == tenantBForDispatch.Value &&
                 deliveredLink.Intent == claimed.Intent &&
                 deliveredLink.Token.Length == 32,
                "Only the trusted mailbox link sink receives B intent and secret");
            Pass(await queue.CompleteAsync(
                     claimed, winner.Owner, CancellationToken.None) &&
                 !await queue.CompleteAsync(
                     claimed, winner.Owner, CancellationToken.None),
                "Proof-backed dispatch completion is atomic and cannot be replayed");
            Pass(await queue.ClaimNextAsync(
                     Guid.NewGuid(), CancellationToken.None) is null &&
                 await ScalarAsync(admin,
                     "SELECT COUNT(*) FROM c05_02_proof_dispatch WHERE state='COMPLETED'") > 0,
                "Committed dispatch leaves no stale READY work for another worker");

            var fromA = await SendProofAsync("proof-confirm", "a.crecisol.test",
                "{\"intentId\":\"" + deliveredLink.Intent.ToString("D") +
                "\",\"code\":\"" + Convert.ToHexString(deliveredLink.Token) + "\"}");
            Pass(fromA.Status == HttpStatusCode.Accepted &&
                 await ScalarAsync(admin,
                     "SELECT COUNT(*) FROM c05_02_mailbox_proofs WHERE tenant_id=X'" +
                     B + "' AND intent_id=X'" + claimed.Intent.ToString("N") +
                     "' AND lifecycle='PENDING'") == 1,
                "Recipient link used on tenant A does not verify tenant B");
            var expectedBody = "{\"intentId\":\"" + deliveredLink.Intent.ToString("D") +
                "\",\"code\":\"" + Convert.ToHexString(deliveredLink.Token) + "\"}";
            var completed = await SendProofAsync(
                "proof-confirm", trustedHost, expectedBody);
            var replay = await SendProofAsync(
                "proof-confirm", trustedHost, expectedBody);
            Pass(completed.Status == HttpStatusCode.Accepted &&
                 completed.Body == proofStart.Body &&
                 replay.Status == HttpStatusCode.Accepted &&
                 replay.Body == proofStart.Body &&
                 await ScalarAsync(admin,
                     "SELECT COUNT(*) FROM c05_02_mailbox_proofs WHERE tenant_id=X'" +
                     B + "' AND intent_id=X'" + claimed.Intent.ToString("N") +
                     "' AND lifecycle='VERIFIED'") == 1,
                "Trusted recipient link verifies only B mailbox once; replay stays neutral");

            if (!string.IsNullOrWhiteSpace(candidateDsn))
            {
                using var candidate = new MySqlDisposableVerifiedCredentialCandidateStore(
                    candidateDsn, key, 1);
                var hasher = new Argon2idPasswordHasher(
                    PasswordHashingPolicy.Default, new LocalOnlyFixturePasswordScreen());
                var syntheticPassword = Convert.ToBase64String(
                    RandomNumberGenerator.GetBytes(36));
                var phc = hasher.CreateVerifier(
                    syntheticPassword).ExportForTrustedCredentialStore();
                var before = await ScalarAsync(admin,
                    "SELECT COUNT(*) FROM c05_02_candidate_accounts");
                Pass(await candidate.TryCreatePendingAsync(
                         tenantBForDispatch, deliveredLink.Intent, phc, CancellationToken.None) &&
                     await ScalarAsync(admin,
                         "SELECT COUNT(*) FROM c05_02_candidate_accounts") == before + 1 &&
                     !await candidate.TryCreatePendingAsync(
                         tenantBForDispatch, deliveredLink.Intent, phc, CancellationToken.None),
                    "Anonymous intent -> trusted outbox -> recipient link -> verified proof -> pending Argon2id is one fail-closed path");
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(deliveredLink.Token);
        }

        // A crashed worker's lease may be reclaimed by another worker, but
        // its old owner can never complete the new lease or send a session.
        var retryIntake = await SendBridgeAsync("b.crecisol.test",
            "{\"mailbox\":\"Dispatch+Retry@Example.test\"}");
        var staleOwner = Guid.NewGuid();
        var stale = await queue.ClaimNextAsync(staleOwner, CancellationToken.None);
        Pass(retryIntake.Status == HttpStatusCode.Accepted && stale is not null &&
             stale.Tenant.Value == tenantBForDispatch.Value,
             "A second verified-host intake is queued for a trusted worker");
        await ExecuteAsync(admin,
            "UPDATE c05_02_proof_dispatch SET claimed_until_utc=" +
            "DATE_SUB(UTC_TIMESTAMP(6),INTERVAL 1 SECOND) WHERE tenant_id=X'" + B +
            "' AND intent_id=X'" + stale!.Intent.ToString("N") + "' AND state='CLAIMED'");
        var nextOwner = Guid.NewGuid();
        var reclaimed = await queue.ClaimNextAsync(
            nextOwner, CancellationToken.None);
        Pass(reclaimed is not null && reclaimed.Intent == stale!.Intent &&
             !await queue.CompleteAsync(stale!, staleOwner, CancellationToken.None),
             "Expired SQL lease is reclaimed once; stale owner cannot acknowledge it");

        var secondDelivery = await SendProofAsync("proof-request",
            "b.crecisol.test", "{\"intentId\":\"" + reclaimed!.Intent.ToString("D") + "\"}");
        Pass(secondDelivery.Status == HttpStatusCode.Accepted &&
             await queue.CompleteAsync(
                 reclaimed!, nextOwner, CancellationToken.None) &&
             localMailbox.Has("Dispatch+Retry@example.test"),
             "Reclaimed lease dispatches only to its recorded B mailbox");
        var remaining = localMailbox.TakeLink("Dispatch+Retry@example.test");
        CryptographicOperations.ZeroMemory(remaining.Token);

        var sqlReadDenied = false;
        await using (var limited = new MySqlConnection(dispatchDsn))
        {
            await limited.OpenAsync();
            await using var attempted = new MySqlCommand(
                "SELECT mailbox_digest FROM c05_02_neutral_intents LIMIT 1", limited);
            try { _ = await attempted.ExecuteScalarAsync(); }
            catch (MySqlException ex) when (ex.Number is 1142 or 1143)
            {
                sqlReadDenied = true;
            }
        }
        Pass(sqlReadDenied, "EXECUTE-only dispatch worker cannot SELECT mailbox or credential tables");
        Pass(await queue.ClaimNextAsync(
                 Guid.NewGuid(), CancellationToken.None) is null,
             "No tenant-scoped outbox item can be claimed twice after completion");
    }

}

await app.StopAsync();
CryptographicOperations.ZeroMemory(key);

Console.WriteLine($"PASS C05-02 REAL neutral HTTP + MySqlConnector/MySQL8.4: {checks} assertions");
Console.WriteLine("LIMIT: loopback only; pending credential is NOT account activation, tenant membership, MFA or session");

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
