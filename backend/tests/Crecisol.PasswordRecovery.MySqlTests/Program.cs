using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Crecisol.Application.Identity;
using Crecisol.Application.Tenancy;
using Crecisol.CredentialSecurity;
using Crecisol.Domain.Tenancy;
using Crecisol.Infrastructure.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlConnector;

// This executable never runs a local database unless --integration is
// explicitly supplied with a disposable loopback MySQL8.4 fixture.
if (args.Length == 0)
{
    Console.WriteLine("SKIP C05-06 HTTP/MySQL integration: --integration and disposable 8.4 required");
    return;
}
if (args.Length != 1 || args[0] != "--integration")
    throw new InvalidOperationException("Only --integration with local fixture is permitted");

var runnerDsn = Environment.GetEnvironmentVariable("CRECISOL_C05_06_RUNNER_DSN");
var setupDsn = Environment.GetEnvironmentVariable("CRECISOL_C05_06_SETUP_DSN");
if (string.IsNullOrWhiteSpace(runnerDsn) || string.IsNullOrWhiteSpace(setupDsn))
    throw new InvalidOperationException("Protected disposable recovery test DSNs required");
var settings = new MySqlConnectionStringBuilder(setupDsn);
if (settings.Server != "127.0.0.1" || settings.Database != "c05_05_disposable")
    throw new InvalidOperationException("Non-disposable setup connection is forbidden");

var checks = 0;
void Check(bool yes, string label)
{
    if (!yes) throw new InvalidOperationException("FAIL C05-06: " + label);
    checks++;
}
async Task DenyAsync<T>(Func<Task> attempt, string label) where T : Exception
{
    try { await attempt(); }
    catch (T) { checks++; return; }
    throw new InvalidOperationException("FAIL C05-06 expected " +
        typeof(T).Name + " for " + label);
}

var tenantA = TenantId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
var tenantB = TenantId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
var actor = TenantActorId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01"));
var otherActor = TenantActorId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb02"));
var platformActor = TenantActorId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb03"));
var hostA = CanonicalTenantHost.Parse("alpha.example.test");
var hostB = CanonicalTenantHost.Parse("beta.example.test");
var now = DateTimeOffset.UtcNow;
var issuedAt = now.AddDays(-3);
var expires = now.AddHours(3);
const string email = "shared@example.test";
const string otherEmail = "other@example.test";
const string platformEmail = "operator@example.test";
const string newPassword = "Secure-new-credential-2026!alpha#";
const string secondPassword = "Distinct-new-credential-2026!beta#";
const string racePassword = "Unique-race-winner-2026!gamma#";

using var rootKey = RandomNumberGenerator.Create();
var k1 = RandomNumberGenerator.GetBytes(32);
var k2 = RandomNumberGenerator.GetBytes(32);
var sessionKey = RandomNumberGenerator.GetBytes(32);
using var recoveryTokens = new PasswordRecoveryTokenProtector(k1, k2);
using var sessionTokens = new OpaqueSessionTokenProtector(sessionKey);
CryptographicOperations.ZeroMemory(k1);
CryptographicOperations.ZeroMemory(k2);
CryptographicOperations.ZeroMemory(sessionKey);
var compromised = new FixtureCompromisedScreen();
var hasher = new Argon2idPasswordHasher(PasswordHashingPolicy.Default, compromised);
var oldPassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
var oldPhc = hasher.CreateVerifier(oldPassword).ExportForTrustedCredentialStore();
var otherPhc = hasher.CreateVerifier(Convert.ToBase64String(
    RandomNumberGenerator.GetBytes(32))).ExportForTrustedCredentialStore();
var sink = new FixtureDeliverySink();
var ledger = new MySqlDisposablePasswordRecoveryLedger(runnerDsn);
var sessions = new MySqlDisposableSessionLedger(runnerDsn);
var recover = new RecoverPasswordOnce(recoveryTokens, ledger, sink,
    new Argon2idRecoveryPasswordEncoder(hasher));

Check(recoveryTokens.ToString() == "PasswordRecoveryTokenProtector[redacted]" &&
      recover.ToString() == "RecoverPasswordOnce[redacted]" &&
      ledger.ToString() == "MySqlDisposablePasswordRecoveryLedger[redacted]",
    "No recovery secret or connection string can appear in diagnostics");
Check(recoveryTokens.TryDigest(null) is null &&
      recoveryTokens.TryDigest("") is null &&
      recoveryTokens.TryDigest("cr6_") is null &&
      recoveryTokens.TryDigest("cr6_" + new string('!',43)) is null &&
      recoveryTokens.TryDigest("cr6_" + new string('A',42)+"=") is null,
    "No malformed, padded or truncated token accepted");
var sample = recoveryTokens.GenerateFresh();
var sampleRaw = sample.RevealForTrustedDeliveryOnly();
var sampleDigest = recoveryTokens.TryDigest(sampleRaw);
Check(sampleRaw.Length == 47 &&
      sample.ToString() == "IssuedPasswordRecoveryToken[redacted]" &&
      sampleDigest is not null &&
      sampleDigest.ToString() == "PasswordRecoveryTokenDigest[redacted]" &&
      sampleDigest.CopyForTrustedLedger().Length == 32,
    "256-bit opaque reset secret and HMAC digest are typed, canonical and redacted");
var differentRaw = recoveryTokens.GenerateFresh().RevealForTrustedDeliveryOnly();
Check(differentRaw != sampleRaw &&
      !CryptographicOperations.FixedTimeEquals(
          recoveryTokens.TryDigest(differentRaw)!.CopyForTrustedLedger(),
          sampleDigest!.CopyForTrustedLedger()),
    "Fresh recovery challenges have unique keyed HMAC digests");
var wrongK1 = RandomNumberGenerator.GetBytes(32);
var wrongK2 = RandomNumberGenerator.GetBytes(32);
using (var wrong = new PasswordRecoveryTokenProtector(wrongK1, wrongK2))
{
    Check(!CryptographicOperations.FixedTimeEquals(
        wrong.TryDigest(sampleRaw)!.CopyForTrustedLedger(),
        sampleDigest.CopyForTrustedLedger()),
        "Independent server keys cannot read another recovery HMAC");
}
CryptographicOperations.ZeroMemory(wrongK1);
CryptographicOperations.ZeroMemory(wrongK2);
await DenyAsync<InvalidOperationException>(async () =>
{
    _ = new PasswordRecoveryTokenProtector(new byte[32], new byte[32]);
    await Task.CompletedTask;
}, "Missing independent nonzero HMAC keys fail closed");
await DenyAsync<InvalidOperationException>(async () =>
{
    _ = new MySqlDisposablePasswordRecoveryLedger(
        "Server=example.com;Database=c05_05_disposable;User ID=unsafe");
    await Task.CompletedTask;
}, "External recovery database prohibited");
await DenyAsync<InvalidOperationException>(async () =>
{
    _ = new MySqlDisposablePasswordRecoveryLedger(
        "Server=127.0.0.1;Database=crecisol_prod;User ID=unsafe");
    await Task.CompletedTask;
}, "Non-disposable recovery DB prohibited");

await using var setup = new MySqlConnection(settings.ConnectionString);
await setup.OpenAsync();
async Task<int> AdminAsync(string sql)
{
    await using var c = new MySqlCommand(sql, setup);
    return await c.ExecuteNonQueryAsync();
}
async Task<long> CountAsync(string sql)
{
    await using var c = new MySqlCommand(sql, setup);
    return Convert.ToInt64(await c.ExecuteScalarAsync());
}
async Task<string> StoredPhcAsync()
{
    await using var c = new MySqlCommand("""
        SELECT verifier_phc FROM c05_06_credentials WHERE actor_id=@actor
        """, setup);
    c.Parameters.Add("@actor", MySqlDbType.Binary).Value =
        actor.Value.ToByteArray(bigEndian: true);
    return (string)(await c.ExecuteScalarAsync() ??
        throw new InvalidOperationException("Missing disposable password verifier"));
}
async Task SeedSubjectAsync(TenantId tenant, TenantActorId who, Guid link)
{
    await using var c = new MySqlCommand("""
      INSERT INTO c05_05_subjects
        (tenant_id,actor_id,link_id,account_revision,credential_revision,
         membership_revision,tenant_revision,host_revision,subject_revision,
         account_active,credential_active,membership_active,
         host_active,tenant_active,member_expires_utc)
      VALUES (@tenant,@actor,@link,7,3,5,9,8,1,1,1,1,1,1,@expires)
      """, setup);
    c.Parameters.Add("@tenant", MySqlDbType.Binary).Value =
        tenant.Value.ToByteArray(bigEndian: true);
    c.Parameters.Add("@actor", MySqlDbType.Binary).Value =
        who.Value.ToByteArray(bigEndian: true);
    c.Parameters.Add("@link", MySqlDbType.Binary).Value =
        link.ToByteArray(bigEndian: true);
    c.Parameters.Add("@expires", MySqlDbType.DateTime).Value = expires.UtcDateTime;
    Check(await c.ExecuteNonQueryAsync() == 1, "Scoped synthetic subject was seeded");
}
async Task SeedCredentialAsync(TenantActorId who, string phc, string realm)
{
    await using var c = new MySqlCommand("""
      INSERT INTO c05_06_credentials
        (actor_id,verifier_phc,credential_revision,credential_active,realm)
      VALUES (@actor,@phc,3,1,@realm)
      """, setup);
    c.Parameters.Add("@actor", MySqlDbType.Binary).Value =
        who.Value.ToByteArray(bigEndian: true);
    c.Parameters.Add("@phc", MySqlDbType.VarChar).Value = phc;
    c.Parameters.Add("@realm", MySqlDbType.VarChar).Value = realm;
    Check(await c.ExecuteNonQueryAsync() == 1, "Argon2id PHC and realm stored in fixture only");
}
async Task SeedMailboxAsync(TenantId tenant, TenantActorId who,
    string registered, bool verified)
{
    var hash = recoveryTokens.DigestCanonicalMailbox(CanonicalLoginHandle.Parse(registered))
        .CopyForTrustedLedger();
    try
    {
        await using var c = new MySqlCommand("""
          INSERT INTO c05_06_mailboxes
            (tenant_id,actor_id,mailbox_digest,mailbox_revision,verified)
          VALUES (@tenant,@actor,@digest,1,@verified)
          """, setup);
        c.Parameters.Add("@tenant", MySqlDbType.Binary).Value =
            tenant.Value.ToByteArray(bigEndian: true);
        c.Parameters.Add("@actor", MySqlDbType.Binary).Value =
            who.Value.ToByteArray(bigEndian: true);
        c.Parameters.Add("@digest", MySqlDbType.Binary).Value = hash;
        c.Parameters.Add("@verified", MySqlDbType.Byte).Value = verified ? 1 : 0;
        Check(await c.ExecuteNonQueryAsync() == 1,
            "Verified-mailbox digest and status seeded without plaintext email");
    }
    finally { CryptographicOperations.ZeroMemory(hash); }
}
async Task<SessionTokenDigest> SeedSessionAsync(TenantId tenant, TenantActorId who)
{
    var raw = sessionTokens.GenerateFresh().RevealForTrustedTransport();
    var d = sessionTokens.TryDigest(raw) ??
        throw new InvalidOperationException("C05-05 test token failed to digest");
    var hash = d.CopyForTrustedLedger();
    try
    {
        await using var c = new MySqlCommand("""
          INSERT INTO c05_05_sessions
            (session_id,tenant_id,actor_id,token_digest,generation,
             subject_revision,issued_utc,expires_utc,absolute_expires_utc,revoked_utc)
          VALUES (@session,@tenant,@actor,@digest,1,1,UTC_TIMESTAMP(6),
                  DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 15 MINUTE),
                  DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 8 HOUR),NULL)
          """, setup);
        c.Parameters.Add("@session", MySqlDbType.Binary).Value =
            Guid.NewGuid().ToByteArray(bigEndian: true);
        c.Parameters.Add("@tenant", MySqlDbType.Binary).Value =
            tenant.Value.ToByteArray(bigEndian: true);
        c.Parameters.Add("@actor", MySqlDbType.Binary).Value =
            who.Value.ToByteArray(bigEndian: true);
        c.Parameters.Add("@digest", MySqlDbType.Binary).Value = hash;
        Check(await c.ExecuteNonQueryAsync() == 1,
            "Issued a fixture session for independent C05-05 validation");
    }
    finally { CryptographicOperations.ZeroMemory(hash); }
    return d;
}

await SeedSubjectAsync(tenantA, actor,
    Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa12"));
await SeedSubjectAsync(tenantB, actor,
    Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb12"));
await SeedSubjectAsync(tenantB, otherActor,
    Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb22"));
await SeedSubjectAsync(tenantB, platformActor,
    Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb33"));
await SeedCredentialAsync(actor, oldPhc, "TENANT");
await SeedCredentialAsync(otherActor, otherPhc, "TENANT");
await SeedCredentialAsync(platformActor, otherPhc, "PLATFORM");
await SeedMailboxAsync(tenantA, actor, email, true);
await SeedMailboxAsync(tenantB, actor, email, true);
await SeedMailboxAsync(tenantB, otherActor, otherEmail, false);
await SeedMailboxAsync(tenantB, platformActor, platformEmail, true);

var sessionA = await SeedSessionAsync(tenantA, actor);
var sessionB = await SeedSessionAsync(tenantB, actor);
var sessionOther = await SeedSessionAsync(tenantB, otherActor);
Check(await sessions.IsCurrentAsync(tenantA, sessionA, CancellationToken.None) &&
      await sessions.IsCurrentAsync(tenantB, sessionB, CancellationToken.None) &&
      await sessions.IsCurrentAsync(tenantB, sessionOther, CancellationToken.None),
    "Pre-reset sessions A/B and unrelated actor B are really current in MySQL");
await DenyAsync<MySqlException>(async () =>
{
    await using var c = new MySqlConnection(runnerDsn);
    await c.OpenAsync();
    await using var command = new MySqlCommand(
        "SELECT verifier_phc FROM c05_06_credentials", c);
    await command.ExecuteScalarAsync();
}, "Procedure runner cannot SELECT any protected PHC or mailbox");

var bindingA = VerifiedTenantHostBinding.RestoreFromTrustedCatalog(
    tenantA, hostA, TenantAddressMode.Subdomain,
    HostVerificationState.Verified, 8, issuedAt, issuedAt, expires);
var bindingB = VerifiedTenantHostBinding.RestoreFromTrustedCatalog(
    tenantB, hostB, TenantAddressMode.Subdomain,
    HostVerificationState.Verified, 8, issuedAt, issuedAt, expires);
var hostResolver = new ResolveVerifiedTenantHost(
    new FixtureHostCatalog(hostA,hostB,bindingA,bindingB), new FixtureClock());

var builder = WebApplication.CreateBuilder(new WebApplicationOptions {
    Args = Array.Empty<string>(), EnvironmentName = "Production"
});
builder.Logging.ClearProviders();
builder.WebHost.UseUrls("http://127.0.0.1:0");
await using var app = builder.Build();
app.UseExceptionHandler(error => error.Run(context =>
{
    context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
    return Task.CompletedTask;
}));
app.Use(async (context, next) =>
{
    context.Response.Headers.CacheControl = "no-store";
    context.Response.Headers.Pragma = "no-cache";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    await next(context);
});

async Task<VerifiedTenantHostHint?> HostContextAsync(HttpContext context)
{
    if (context.Request.Host.Port.HasValue ||
        context.Request.Headers.ContainsKey("Forwarded") ||
        context.Request.Headers.ContainsKey("X-Forwarded-Host") ||
        context.Request.Headers.ContainsKey("X-Tenant-Id") ||
        context.Request.Headers.ContainsKey("Authorization") ||
        context.Request.QueryString.HasValue)
        return null;
    try
    {
        var host = CanonicalTenantHost.Parse(context.Request.Host.Host);
        if (host != hostA && host != hostB) return null;
        return await hostResolver.ResolveAsync(host, context.RequestAborted);
    }
    catch (UnauthorizedAccessException) { return null; }
    catch (InvalidOperationException) { return null; }
}

static async Task<JsonDocument?> TryReadJson(HttpContext context, int maxBytes)
{
    if (context.Request.ContentLength is null or <= 0 ||
        context.Request.ContentLength > maxBytes ||
        !(context.Request.ContentType?.StartsWith(
            "application/json", StringComparison.OrdinalIgnoreCase) ?? false))
        return null;
    try
    {
        return await JsonDocument.ParseAsync(context.Request.Body,
            new JsonDocumentOptions { MaxDepth = 4 }, context.RequestAborted);
    }
    catch (JsonException) { return null; }
}

// NOTE: This entire HTTP API exists only in this isolated .NET test executable.
// Never add these routes to backend/src/Crecisol.Api/Program.cs.
app.MapPost("/__c05_06_test__/request", async (HttpContext context) =>
{
    var host = await HostContextAsync(context);
    if (host is null) return Results.BadRequest();
    using var doc = await TryReadJson(context, 1024);
    if (doc is null) return Results.BadRequest();
    var json = doc.RootElement;
    if (json.ValueKind != JsonValueKind.Object ||
        json.EnumerateObject().Count() != 1 ||
        !json.TryGetProperty("email", out var mail) ||
        mail.ValueKind != JsonValueKind.String)
        return Results.BadRequest();
    try
    {
        await recover.RequestAsync(CanonicalLoginHandle.Parse(mail.GetString()),
            host, context.RequestAborted);
        return Results.Accepted();
    }
    catch (InvalidOperationException)
    {
        // Malformed handle, not account existence, causes 400.
        return Results.BadRequest();
    }
});
app.MapPost("/__c05_06_test__/redeem", async (HttpContext context) =>
{
    var host = await HostContextAsync(context);
    if (host is null) return Results.BadRequest();
    using var doc = await TryReadJson(context, 2048);
    if (doc is null) return Results.BadRequest();
    var json = doc.RootElement;
    if (json.ValueKind != JsonValueKind.Object ||
        json.EnumerateObject().Count() != 2 ||
        !json.TryGetProperty("token", out var code) ||
        !json.TryGetProperty("newPassword", out var password) ||
        code.ValueKind != JsonValueKind.String ||
        password.ValueKind != JsonValueKind.String)
        return Results.BadRequest();
    try
    {
        var succeeded = await recover.RedeemAsync(code.GetString(),
            (password.GetString() ?? "").AsMemory(), host, context.RequestAborted);
        return succeeded ? Results.NoContent() : Results.Unauthorized();
    }
    catch (InvalidOperationException)
    {
        // Neutral token/PHC/policy failures do not reveal a verified account.
        return Results.Unauthorized();
    }
});
await app.StartAsync();
var bind = app.Services.GetRequiredService<IServer>().Features
    .Get<IServerAddressesFeature>()?.Addresses.Single();
if (bind is null || !bind.StartsWith("http://127.0.0.1:", StringComparison.Ordinal))
    throw new InvalidOperationException("C05-06 TEST API may bind ONLY to loopback.");
using var client = new HttpClient(new SocketsHttpHandler {
    UseProxy = false, AllowAutoRedirect = false
}) { BaseAddress = new Uri(bind), Timeout = TimeSpan.FromSeconds(60) };

HttpRequestMessage Request(string route, string host, string payload,
    string? fakeTenant = null, bool forwarded = false)
{
    var m = new HttpRequestMessage(HttpMethod.Post, route);
    m.Headers.Host = host;
    if (fakeTenant is not null)
        m.Headers.TryAddWithoutValidation("X-Tenant-Id", fakeTenant);
    if (forwarded)
        m.Headers.TryAddWithoutValidation("X-Forwarded-Host", "alpha.example.test");
    m.Content = new StringContent(payload, Encoding.UTF8, "application/json");
    return m;
}
async Task<(HttpStatusCode Status, bool Neutral)> HttpRequestAsync(
    string host, string mail, string? fakeTenant=null, bool forwarded=false)
{
    using var req = Request("/__c05_06_test__/request", host,
        JsonSerializer.Serialize(new {email=mail}),fakeTenant,forwarded);
    using var response = await client.SendAsync(req);
    var text = await response.Content.ReadAsStringAsync();
    var noToken = text.Length == 0 &&
        !response.Headers.Contains("Set-Cookie") &&
        response.Headers.CacheControl?.NoStore == true &&
        response.Headers.TryGetValues("Referrer-Policy", out var values) &&
        values.Contains("no-referrer");
    return (response.StatusCode, noToken);
}
async Task<HttpStatusCode> HttpRedeemAsync(string host, string code, string candidate)
{
    using var req = Request("/__c05_06_test__/redeem",host,
        JsonSerializer.Serialize(new {token=code,newPassword=candidate}));
    using var response = await client.SendAsync(req);
    Check(!response.Headers.Contains("Set-Cookie") &&
          response.Headers.CacheControl?.NoStore == true,
        "No cookie, JWT or cache emitted during recovery");
    return response.StatusCode;
}
string Latest(TenantId tenant) => sink.Latest(tenant);

// Account existence is indistinguishable at HTTP boundary.
var initial = await HttpRequestAsync(hostA.Value, email);
var unknown = await HttpRequestAsync(hostA.Value, "not-listed@example.test");
Check(initial == (HttpStatusCode.Accepted, true) &&
      unknown == initial && sink.Count == 1,
    "Known and unknown account request yield identical neutral HTTP 202/no token");
Check((await HttpRequestAsync(hostA.Value, email,
        tenantB.Value.ToString())).Status == HttpStatusCode.BadRequest &&
      (await HttpRequestAsync(hostA.Value,email,forwarded:true)).Status ==
        HttpStatusCode.BadRequest &&
      (await HttpRequestAsync("unowned.example.test",email)).Status ==
        HttpStatusCode.BadRequest,
    "Forged tenant/forwarded/unverified hosts are rejected before recovery SQL");
Check((await HttpRequestAsync(hostB.Value,platformEmail)).Status ==
      HttpStatusCode.Accepted &&
      sink.Count == 1,
    "Platform operator realm cannot issue tenant password reset");
Check((await HttpRequestAsync(hostB.Value,otherEmail)).Status ==
      HttpStatusCode.Accepted && sink.Count == 1,
    "Unverified mailbox cannot receive a reset token despite neutral HTTP");
var originalA = Latest(tenantA);

var validB = await HttpRequestAsync(hostB.Value,email);
Check(validB == initial && sink.Count == 2,
    "Independently verified host B can issue tenant-bound challenge");
var originalB = Latest(tenantB);
Check(await HttpRedeemAsync(hostA.Value, originalB,newPassword) ==
      HttpStatusCode.Unauthorized &&
      await HttpRedeemAsync(hostB.Value,originalA,newPassword) ==
      HttpStatusCode.Unauthorized,
    "A/B cross-tenant token redemption denied");
Check(await HttpRedeemAsync(hostA.Value,originalA,newPassword) ==
      HttpStatusCode.Unauthorized,
    "Issuing a new recovery link revoked all prior links for same global actor");
Check(await HttpRedeemAsync(hostB.Value,originalB,newPassword) ==
      HttpStatusCode.NoContent,
    "Valid 10-minute recovery token consumed atomically in real HTTP/MySQL");
Check(await HttpRedeemAsync(hostB.Value,originalB,newPassword) ==
      HttpStatusCode.Unauthorized,
    "Once-consumed password reset token can NEVER be reused");
Check(!hasher.Verify(oldPassword,await StoredPhcAsync()).Matched &&
      hasher.Verify(newPassword,await StoredPhcAsync()).Matched,
    "New Argon2id PHC replaces old password and old password is rejected");
Check(!await sessions.IsCurrentAsync(tenantA,sessionA,CancellationToken.None) &&
      !await sessions.IsCurrentAsync(tenantB,sessionB,CancellationToken.None) &&
      await sessions.IsCurrentAsync(tenantB,sessionOther,CancellationToken.None),
    "Reset revokes ALL actor sessions A/B, never an unrelated B actor");
Check(await CountAsync($"""
    SELECT COUNT(*) FROM c05_05_sessions
    WHERE actor_id=UNHEX('{actor.Value:N}') AND revoked_utc IS NOT NULL
    """) == 2, "Global reset durably revoked exactly the affected actor sessions");
Check(await CountAsync($"""
    SELECT COUNT(*) FROM c05_05_subjects
    WHERE actor_id=UNHEX('{actor.Value:N}') AND credential_revision=4
    """) == 2, "Credential revision increment propagated to every tenant subject");
Check(await CountAsync($"""
    SELECT COUNT(*) FROM c05_06_challenges
    WHERE actor_id=UNHEX('{actor.Value:N}') AND state='CONSUMED'
    """) == 1, "Exactly one recovery challenge persisted as consumed");

var issuedAgain = await HttpRequestAsync(hostA.Value,email);
Check(issuedAgain == initial && sink.Count == 3,
    "A new reset link may be issued after a valid password change");
var weakToken = Latest(tenantA);
compromised.Verdict = PasswordCompromiseVerdict.Compromised;
Check(await HttpRedeemAsync(hostA.Value,weakToken,secondPassword) ==
      HttpStatusCode.Unauthorized, "Compromised-password screen blocks reset");
compromised.Verdict = PasswordCompromiseVerdict.Unavailable;
Check(await HttpRedeemAsync(hostA.Value,weakToken,secondPassword) ==
      HttpStatusCode.Unauthorized,
    "Compromised-screen outage FAILS CLOSED, never weak fallback");
compromised.Verdict = PasswordCompromiseVerdict.Allowed;
Check(await HttpRedeemAsync(hostA.Value,weakToken,"short") ==
      HttpStatusCode.Unauthorized &&
      await HttpRedeemAsync(hostA.Value,weakToken,secondPassword) ==
      HttpStatusCode.NoContent,
    "Weak passwords refused, valid policy Argon2id reset still succeeds");
Check(!hasher.Verify(newPassword,await StoredPhcAsync()).Matched &&
      hasher.Verify(secondPassword,await StoredPhcAsync()).Matched,
    "Second reset truly replaces verifier rather than reusing previous PHC");

await HttpRequestAsync(hostB.Value,email);
var expiredToken = Latest(tenantB);
var expiredDigest = recoveryTokens.TryDigest(expiredToken)!.CopyForTrustedLedger();
try
{
    await using var c = new MySqlCommand("""
        UPDATE c05_06_challenges
           SET issued_utc=DATE_SUB(UTC_TIMESTAMP(6),INTERVAL 20 MINUTE),
               expires_utc=DATE_SUB(UTC_TIMESTAMP(6),INTERVAL 10 MINUTE)
         WHERE token_digest=@digest
        """,setup);
    c.Parameters.Add("@digest",MySqlDbType.Binary).Value=expiredDigest;
    Check(await c.ExecuteNonQueryAsync()==1,
        "Expired fixture token with valid storage shape");
}
finally { CryptographicOperations.ZeroMemory(expiredDigest); }
Check(await HttpRedeemAsync(hostB.Value,expiredToken,secondPassword) ==
      HttpStatusCode.Unauthorized,
    "Expired challenge cannot be consumed even with correct digest");

await HttpRequestAsync(hostB.Value,email);
var mailboxToken=Latest(tenantB);
Check(await AdminAsync($"""
    UPDATE c05_06_mailboxes SET verified=0
     WHERE tenant_id=UNHEX('{tenantB.Value:N}')
       AND actor_id=UNHEX('{actor.Value:N}')
    """)==1 &&
      await HttpRedeemAsync(hostB.Value,mailboxToken,secondPassword)==
      HttpStatusCode.Unauthorized,
    "Unverified mailbox blocks a pending legitimate recovery link");
Check(await AdminAsync($"""
    UPDATE c05_06_mailboxes SET verified=1
     WHERE tenant_id=UNHEX('{tenantB.Value:N}')
       AND actor_id=UNHEX('{actor.Value:N}')
    """)==1 &&
      await HttpRedeemAsync(hostB.Value,mailboxToken,secondPassword)==
      HttpStatusCode.Unauthorized,
    "Off-then-on mailbox re-verification cannot resurrect stale link");

await HttpRequestAsync(hostA.Value,email);
var membershipToken=Latest(tenantA);
Check(await AdminAsync($"""
    UPDATE c05_05_subjects SET membership_active=0
     WHERE tenant_id=UNHEX('{tenantA.Value:N}')
       AND actor_id=UNHEX('{actor.Value:N}')
    """)==1 &&
      await HttpRedeemAsync(hostA.Value,membershipToken,secondPassword)==
      HttpStatusCode.Unauthorized,
    "Revoked tenant membership blocks pending recovery token");
Check(await AdminAsync($"""
    UPDATE c05_05_subjects SET membership_active=1
     WHERE tenant_id=UNHEX('{tenantA.Value:N}')
       AND actor_id=UNHEX('{actor.Value:N}')
    """)==1 &&
      await HttpRedeemAsync(hostA.Value,membershipToken,secondPassword)==
      HttpStatusCode.Unauthorized,
    "Restoring membership cannot resurrect prior challenge after revision change");

Check(await AdminAsync($"""
    UPDATE c05_06_mailboxes SET verified=1
     WHERE tenant_id=UNHEX('{tenantB.Value:N}')
       AND actor_id=UNHEX('{otherActor.Value:N}')
    """)==1 &&
      (await HttpRequestAsync(hostB.Value,otherEmail)).Status ==
      HttpStatusCode.Accepted,
    "A separate verified B mailbox can issue its own challenge");
var otherToken=Latest(tenantB);
Check(await HttpRedeemAsync(hostA.Value,otherToken,secondPassword)==
      HttpStatusCode.Unauthorized &&
      await sessions.IsCurrentAsync(tenantB,sessionOther,CancellationToken.None),
    "Unrelated B token remains strictly isolated from A and does not revoke session");
Check(await HttpRedeemAsync(hostB.Value,otherToken,secondPassword)==
      HttpStatusCode.NoContent &&
      !await sessions.IsCurrentAsync(tenantB,sessionOther,CancellationToken.None),
    "B-only credential reset correctly revokes its own remaining session");

// Real HTTP + InnoDB 16-way CAS race. A fresh valid actor token may be
// redeemed by EXACTLY one client. The old token cannot be replayed.
await HttpRequestAsync(hostA.Value,email);
var raceToken=Latest(tenantA);
var results=await Task.WhenAll(Enumerable.Range(0,16).Select(_ =>
    HttpRedeemAsync(hostA.Value,raceToken,racePassword)));
Check(results.Count(s=>s==HttpStatusCode.NoContent)==1 &&
      results.Count(s=>s==HttpStatusCode.Unauthorized)==15,
    "16 concurrent REAL HTTP/MySQL reset attempts: exactly ONE winner, 15 denied");
Check(await HttpRedeemAsync(hostA.Value,raceToken,racePassword) ==
      HttpStatusCode.Unauthorized &&
      hasher.Verify(racePassword,await StoredPhcAsync()).Matched,
    "Same token cannot be replayed after winning the atomic race");

Check(await CountAsync("""
    SELECT COUNT(*) FROM information_schema.columns
     WHERE table_schema=DATABASE()
       AND table_name IN ('c05_06_challenges','c05_06_mailboxes')
       AND (column_name LIKE '%token_raw%' OR column_name LIKE '%email%'
         OR column_name LIKE '%password%' OR column_name LIKE '%secret%'
         OR column_name LIKE '%cookie%' OR column_name LIKE '%jwt%')
    """)==0, "Recovery SQL never persists email, plaintext token or password");
Check(await CountAsync("""
    SELECT COUNT(*) FROM c05_06_challenges
     WHERE state='CONSUMED' AND consumed_utc IS NULL
    """)==0, "Every consumed one-time link has a persisted consumption timestamp");
Check(await CountAsync("""
    SELECT COUNT(*) FROM c05_06_challenges
     WHERE state='PENDING' AND expires_utc<=issued_utc
    """)==0, "Challenge expiry window never becomes nonpositive");
Check(await CountAsync("""
    SELECT COUNT(*) FROM c05_06_credentials
     WHERE verifier_phc NOT LIKE '$argon2id$v=19$%'
    """)==0, "Protected credential storage contains only Argon2id PHC verifiers");

await app.StopAsync();
Console.WriteLine($"PASS CRECISOL C05-06 .NET10 real HTTP/MySQL8.4: {checks} assertions");
Console.WriteLine("PASS neutral anti-enumeration, 10min one-use token, A/B scope, Argon2id replacement, sessions A+B revoked, 16-way CAS one winner, expired/replayed/rebound denied");
Console.WriteLine("LIMIT: disposable local HTTP fixture and in-memory delivery only; NO SMTP, production account recovery, real customers, MFA bypass, live API or deployment.");

sealed class FixtureCompromisedScreen : ITrustedCompromisedPasswordScreen
{
    public PasswordCompromiseVerdict Verdict { get; set; }=PasswordCompromiseVerdict.Allowed;
    public PasswordCompromiseVerdict Check(ReadOnlySpan<char> candidate) => Verdict;
}

sealed class FixtureClock : ITrustedHostUtcClock
{
    public DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow;
}

sealed class FixtureHostCatalog(
    CanonicalTenantHost a,CanonicalTenantHost b,
    VerifiedTenantHostBinding bindingA,VerifiedTenantHostBinding bindingB)
    : ITrustedVerifiedHostCatalog
{
    public Task<IReadOnlyList<VerifiedTenantHostBinding>?> FindExactBindingsAsync(
        CanonicalTenantHost host,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        IReadOnlyList<VerifiedTenantHostBinding> bindings=
            host==a ? new[]{bindingA} :
            host==b ? new[]{bindingB} :
            Array.Empty<VerifiedTenantHostBinding>();
        return Task.FromResult<IReadOnlyList<VerifiedTenantHostBinding>?>(bindings);
    }
}

sealed class FixtureDeliverySink : ITrustedPasswordRecoveryDeliverySink
{
    private readonly ConcurrentQueue<(TenantId Tenant,string Raw)> delivered=new();
    public int Count=>delivered.Count;
    public Task DeliverToVerifiedMailboxAsync(
        TenantId tenant,CanonicalLoginHandle registeredMailbox,
        IssuedPasswordRecoveryToken token,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        delivered.Enqueue((tenant,token.RevealForTrustedDeliveryOnly()));
        return Task.CompletedTask;
    }
    public string Latest(TenantId tenant)=>
        delivered.Last(x=>x.Tenant==tenant).Raw;
}
