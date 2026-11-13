using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Crecisol.Application.Identity;
using Crecisol.Application.Tenancy;
using Crecisol.CredentialSecurity;
using Crecisol.Domain.Identity;
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

// Never execute against production or without intentional local MySQL8.4.
// Existing C03 full-project test discovery can compile and skip this executable.
if (args.Length == 0)
{
    Console.WriteLine("SKIP C05-05 HTTP/MySQL integration: --integration and disposable 8.4 required");
    return;
}
if (args.Length != 1 || args[0] != "--integration")
    throw new InvalidOperationException("Usage: --integration (C05-05 disposable localhost only)");

var runnerDsn = Environment.GetEnvironmentVariable("CRECISOL_C05_05_TEST_DSN");
var setupDsn = Environment.GetEnvironmentVariable("CRECISOL_C05_05_SETUP_DSN");
if (string.IsNullOrWhiteSpace(runnerDsn) || string.IsNullOrWhiteSpace(setupDsn))
    throw new InvalidOperationException("Protected disposable C05-05 test DSNs required.");
var config = new MySqlConnectionStringBuilder(setupDsn);
if (config.Database != "c05_05_disposable" || config.Server != "127.0.0.1")
    throw new InvalidOperationException("Refuse all non-disposable setup databases.");

var checks = 0;
void Check(bool condition, string description)
{
    if (!condition) throw new InvalidOperationException("FAIL C05-05 " + description);
    checks++;
}
async Task DenyAsync<T>(Func<Task> attempt, string description) where T : Exception
{
    try { await attempt(); }
    catch (T) { checks++; return; }
    throw new InvalidOperationException("FAIL C05-05 expected " + typeof(T).Name +
        " for " + description);
}

var tenantA = TenantId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
var tenantB = TenantId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
var actor = TenantActorId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01"));
var linkIdA = AccountTenantLinkId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa12"));
var linkIdB = AccountTenantLinkId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb12"));
var credentialId = AccountCredentialId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa34"));
var hostA = CanonicalTenantHost.Parse("alpha.example.test");
var hostB = CanonicalTenantHost.Parse("beta.example.test");
var now = DateTimeOffset.UtcNow;
var changed = now.AddDays(-1);
var granted = now.AddDays(-2);
var expires = now.AddHours(3);
const string email = "user@example.test";

await using var setup = new MySqlConnection(config.ConnectionString);
await setup.OpenAsync();

async Task<int> AdminAsync(string sql)
{
    await using var command = new MySqlCommand(sql, setup);
    return await command.ExecuteNonQueryAsync();
}
async Task<long> CountAsync(string sql)
{
    await using var command = new MySqlCommand(sql, setup);
    return Convert.ToInt64(await command.ExecuteScalarAsync());
}

foreach (var (tenant, link) in new[] {(tenantA, linkIdA), (tenantB, linkIdB)})
{
    await using var command = new MySqlCommand("""
        INSERT INTO c05_05_subjects
        (tenant_id, actor_id, link_id, account_revision, credential_revision,
         membership_revision, tenant_revision, host_revision, subject_revision,
         account_active, credential_active, membership_active,
         host_active, tenant_active, member_expires_utc)
        VALUES (@tenant,@actor,@link,7,3,5,9,8,1,1,1,1,1,1,@expires)
        """, setup);
    command.Parameters.Add("@tenant", MySqlDbType.Binary).Value =
        tenant.Value.ToByteArray(bigEndian: true);
    command.Parameters.Add("@actor", MySqlDbType.Binary).Value =
        actor.Value.ToByteArray(bigEndian: true);
    command.Parameters.Add("@link", MySqlDbType.Binary).Value =
        link.Value.ToByteArray(bigEndian: true);
    command.Parameters.Add("@expires", MySqlDbType.DateTime).Value =
        expires.UtcDateTime;
    Check(await command.ExecuteNonQueryAsync() == 1, "Fixture seeded scoped subject");
}

var sessionLedger = new MySqlDisposableSessionLedger(runnerDsn);
var key = RandomNumberGenerator.GetBytes(32);
using var tokens = new OpaqueSessionTokenProtector(key);
CryptographicOperations.ZeroMemory(key);
var sessions = new ScopedSessionLifecycle(tokens, sessionLedger);
var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
var screening = new FixtureCompromisedScreen();
var hasher = new Argon2idPasswordHasher(PasswordHashingPolicy.Default, screening);
var phc = hasher.CreateVerifier(password).ExportForTrustedCredentialStore();
var dummyPhc = hasher.CreateVerifier(Convert.ToBase64String(
    RandomNumberGenerator.GetBytes(32))).ExportForTrustedCredentialStore();
var crypto = new Argon2idLoginCandidateVerifier(hasher);

var account = IdentityAccountSnapshot.RestoreFromTrustedIdentityDirectory(
    actor, IdentityAccountState.Active, 7, changed);
var credential = AccountCredentialMetadata.RestoreFromTrustedCredentialDirectory(
    credentialId, actor, AccountCredentialKind.PasswordVerifier,
    AccountCredentialState.Active, 3, changed);
var credentialSnapshot = TrustedLoginCredentialSnapshot.RestoreFromTrustedDirectory(
    account, credential, TrustedLoginRealm.TenantAccount, phc);
var links = new[] {
    AccountTenantLinkSnapshot.RestoreFromTrustedMembershipDirectory(
        linkIdA, actor, tenantA, AccountTenantProfileKind.Employee,
        TenantMembershipState.Active, 5, changed, granted, expires),
    AccountTenantLinkSnapshot.RestoreFromTrustedMembershipDirectory(
        linkIdB, actor, tenantB, AccountTenantProfileKind.Employee,
        TenantMembershipState.Active, 5, changed, granted, expires)
};
var catalogA = TenantCatalogSnapshot.RestoreFromTrustedCatalog(
    tenantA, "alpha-internal", "Alpha synthetic", TenantLifecycleState.Active,
    TenantAddressMode.Subdomain, 9, changed);
var catalogB = TenantCatalogSnapshot.RestoreFromTrustedCatalog(
    tenantB, "beta-internal", "Beta synthetic", TenantLifecycleState.Active,
    TenantAddressMode.Subdomain, 9, changed);
var bindingA = VerifiedTenantHostBinding.RestoreFromTrustedCatalog(
    tenantA, hostA, TenantAddressMode.Subdomain, HostVerificationState.Verified,
    8, changed, granted, expires);
var bindingB = VerifiedTenantHostBinding.RestoreFromTrustedCatalog(
    tenantB, hostB, TenantAddressMode.Subdomain, HostVerificationState.Verified,
    8, changed, granted, expires);
var clock = new FixtureClock();
var hostResolver = new ResolveVerifiedTenantHost(
    new FixtureHostCatalog(hostA, hostB, bindingA, bindingB), clock);

PrepareContextualPasswordLogin LoginResolver(CanonicalTenantHost host) =>
    new(new FixtureCredentialDirectory(email, credentialSnapshot),
        new FixtureDummyVerifier(dummyPhc), crypto,
        new FixtureEntryReader(host), new FixtureLinkDirectory(actor, links),
        hostResolver, new FixtureTenantCatalog(tenantA, tenantB, catalogA, catalogB),
        clock);

Check(tokens.ToString() == "OpaqueSessionTokenProtector[redacted]" &&
      sessionLedger.ToString() == "MySqlDisposableSessionLedger[redacted]",
    "No cryptographic key/DSN in ToString");
Check(tokens.TryDigest(null) is null &&
      tokens.TryDigest("") is null &&
      tokens.TryDigest("cs5_") is null &&
      tokens.TryDigest("cs5_" + new string('?', 43)) is null &&
      tokens.TryDigest("cs5_" + new string('a', 42) + "=") is null,
    "Malformed/partial/padded tokens always rejected");
var randomToken = tokens.GenerateFresh();
var randomString = randomToken.RevealForTrustedTransport();
Check(randomString.Length == 47 && randomString.StartsWith("cs5_", StringComparison.Ordinal) &&
      randomToken.ToString() == "IssuedSessionToken[redacted]",
    "CSPRNG 256-bit opaque token explicitly transferable and redacted");
var firstDigest = tokens.TryDigest(randomString);
Check(firstDigest is not null &&
      firstDigest.CopyForTrustedLedger().Length == 32 &&
      firstDigest.ToString() == "SessionTokenDigest[redacted]",
    "Digest is 32 bytes and not printed");
var nextRandom = tokens.GenerateFresh().RevealForTrustedTransport();
Check(nextRandom != randomString &&
      !CryptographicOperations.FixedTimeEquals(
          tokens.TryDigest(nextRandom)!.CopyForTrustedLedger(),
          firstDigest.CopyForTrustedLedger()),
    "Fresh tokens generate different keyed digests");
var differentKey = RandomNumberGenerator.GetBytes(32);
using (var otherProtector = new OpaqueSessionTokenProtector(differentKey))
{
    Check(!CryptographicOperations.FixedTimeEquals(
            otherProtector.TryDigest(randomString)!.CopyForTrustedLedger(),
            firstDigest.CopyForTrustedLedger()),
        "Wrong server HMAC key cannot validate persisted session digest");
}
CryptographicOperations.ZeroMemory(differentKey);
await DenyAsync<InvalidOperationException>(async () =>
    { _ = new MySqlDisposableSessionLedger("Server=example.org;Database=c05_05_disposable;User ID=x"); await Task.CompletedTask; },
    "External DSN rejected");
await DenyAsync<InvalidOperationException>(async () =>
    { _ = new MySqlDisposableSessionLedger("Server=127.0.0.1;Database=crecisol_live;User ID=x"); await Task.CompletedTask; },
    "Production DSN rejected");
await DenyAsync<InvalidOperationException>(async () =>
    { _ = new OpaqueSessionTokenProtector(new byte[32]); await Task.CompletedTask; },
    "All-zero server HMAC key rejected");
await DenyAsync<MySqlException>(async () =>
{
    await using var connection = new MySqlConnection(runnerDsn);
    await connection.OpenAsync();
    await using var command = new MySqlCommand(
        "SELECT COUNT(*) FROM c05_05_sessions", connection);
    await command.ExecuteScalarAsync();
}, "DB runner has EXECUTE only, cannot read session digests");

// Actual C05-04 credential and direct-host resolution BEFORE SQL issue.
var proofA = await LoginResolver(hostA).ExecuteAsync(
    CanonicalLoginHandle.Parse(email), password.AsMemory(), CancellationToken.None);
var proofB = await LoginResolver(hostB).ExecuteAsync(
    CanonicalLoginHandle.Parse(email), password.AsMemory(), CancellationToken.None);
Check(proofA.Status == ContextualLoginPreparationStatus.Ready &&
      proofA.Evidence?.TenantId == tenantA &&
      proofB.Status == ContextualLoginPreparationStatus.Ready &&
      proofB.Evidence?.TenantId == tenantB,
    "Genuine Argon2id with two verified host contexts A and B");
var falseProof = await LoginResolver(hostA).ExecuteAsync(
    CanonicalLoginHandle.Parse(email), "not-the-secret".AsMemory(),
    CancellationToken.None);
Check(falseProof.Status == ContextualLoginPreparationStatus.Denied,
    "Incorrect Argon2id proof cannot prepare session");
Check(await sessions.IssueAsync(falseProof, CancellationToken.None) is null,
    "Denied pre-session proof cannot create database session");

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
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next(context);
});

async Task<(CanonicalTenantHost Host, VerifiedTenantHostHint Hint)?> TrustedHostAsync(
    HttpContext context)
{
    if (context.Request.Headers.ContainsKey("Forwarded") ||
        context.Request.Headers.ContainsKey("X-Forwarded-Host") ||
        context.Request.Headers.ContainsKey("X-Tenant-Id") ||
        context.Request.Host.Port.HasValue)
        return null;
    try
    {
        var host = CanonicalTenantHost.Parse(context.Request.Host.Host);
        if (host != hostA && host != hostB) return null;
        var verified = await hostResolver.ResolveAsync(
            host, context.RequestAborted);
        return (host, verified);
    }
    catch (UnauthorizedAccessException) { return null; }
    catch (InvalidOperationException) { return null; }
}
string? Bearer(HttpContext context)
{
    var authorization = context.Request.Headers.Authorization.ToString();
    return authorization.StartsWith("Bearer ", StringComparison.Ordinal)
        ? authorization[7..] : null;
}

// Explicit TEST-ONLY loopback API. Never registered in Crecisol.Api/Program.cs.
app.MapPost("/__c05_05_test__/login", async (HttpContext context) =>
{
    var target = await TrustedHostAsync(context);
    if (target is null) return Results.BadRequest();
    if (context.Request.ContentLength is null or > 2048 ||
        !(context.Request.ContentType?.StartsWith(
            "application/json", StringComparison.OrdinalIgnoreCase) ?? false))
        return Results.BadRequest();
    string? login;
    string? enteredPassword;
    try
    {
        using var document = await JsonDocument.ParseAsync(
            context.Request.Body, new JsonDocumentOptions {MaxDepth=4},
            context.RequestAborted);
        var json = document.RootElement;
        if (json.ValueKind != JsonValueKind.Object ||
            json.EnumerateObject().Count() != 2 ||
            !json.TryGetProperty("email", out var mail) ||
            !json.TryGetProperty("password", out var candidate) ||
            mail.ValueKind != JsonValueKind.String ||
            candidate.ValueKind != JsonValueKind.String)
            return Results.BadRequest();
        login = mail.GetString();
        enteredPassword = candidate.GetString();
    }
    catch (JsonException) { return Results.BadRequest(); }

    try
    {
        var prepared = await LoginResolver(target.Value.Host).ExecuteAsync(
            CanonicalLoginHandle.Parse(login), (enteredPassword ?? "").AsMemory(),
            context.RequestAborted);
        var issued = await sessions.IssueAsync(prepared, context.RequestAborted);
        return issued is null ? Results.Unauthorized() :
            Results.Json(new { token=issued.RevealForTrustedTransport() });
    }
    catch (InvalidOperationException) { return Results.BadRequest(); }
});
app.MapGet("/__c05_05_test__/validate", async (HttpContext context) =>
{
    var target = await TrustedHostAsync(context);
    if (target is null) return Results.BadRequest();
    return await sessions.ValidateAsync(Bearer(context), target.Value.Hint,
        context.RequestAborted)
        ? Results.NoContent() : Results.Unauthorized();
});
app.MapPost("/__c05_05_test__/rotate", async (HttpContext context) =>
{
    var target = await TrustedHostAsync(context);
    if (target is null) return Results.BadRequest();
    var rotated = await sessions.RotateAsync(Bearer(context), target.Value.Hint,
        context.RequestAborted);
    return rotated is null ? Results.Unauthorized() :
        Results.Json(new { token=rotated.RevealForTrustedTransport() });
});
app.MapPost("/__c05_05_test__/logout", async (HttpContext context) =>
{
    var target = await TrustedHostAsync(context);
    if (target is null) return Results.BadRequest();
    _ = await sessions.LogoutAsync(Bearer(context), target.Value.Hint,
        context.RequestAborted);
    // Neutral response does not disclose if a session was already revoked.
    return Results.NoContent();
});

await app.StartAsync();
var address = app.Services.GetRequiredService<IServer>().Features
    .Get<IServerAddressesFeature>()?.Addresses.Single();
if (address is null || !address.StartsWith("http://127.0.0.1:",
        StringComparison.Ordinal))
    throw new InvalidOperationException("TEST-ONLY API must listen on loopback.");
using var client = new HttpClient(new SocketsHttpHandler {
    UseProxy = false, AllowAutoRedirect = false
}) { BaseAddress = new Uri(address), Timeout = TimeSpan.FromSeconds(30) };

HttpRequestMessage Request(HttpMethod method, string route, string host, string? bearer = null)
{
    var message = new HttpRequestMessage(method, route);
    message.Headers.Host = host;
    if (bearer is not null)
        message.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", bearer);
    return message;
}
async Task<(HttpStatusCode, string?, bool)> HttpLogin(
    string host, string suppliedPassword, string mail=email,
    string? extraJson=null, string? spoofTenant=null)
{
    using var message = Request(HttpMethod.Post, "/__c05_05_test__/login", host);
    if (spoofTenant is not null)
        message.Headers.TryAddWithoutValidation("X-Tenant-Id", spoofTenant);
    message.Content = new StringContent(extraJson ?? JsonSerializer.Serialize(
        new {email=mail, password=suppliedPassword}), Encoding.UTF8, "application/json");
    using var response = await client.SendAsync(message);
    string? raw = null;
    if (response.IsSuccessStatusCode && response.Content.Headers.ContentLength != 0)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        if (doc.RootElement.TryGetProperty("token", out var token))
            raw = token.GetString();
    }
    var noCache = response.Headers.CacheControl?.NoStore == true &&
        !response.Headers.Contains("Set-Cookie");
    return (response.StatusCode, raw, noCache);
}
async Task<HttpStatusCode> HttpProbe(string host, string? raw)
{
    using var message = Request(HttpMethod.Get, "/__c05_05_test__/validate", host, raw);
    using var response = await client.SendAsync(message);
    return response.StatusCode;
}
async Task<(HttpStatusCode, string?)> HttpRotate(string host, string? raw)
{
    using var message = Request(HttpMethod.Post, "/__c05_05_test__/rotate", host, raw);
    using var response = await client.SendAsync(message);
    string? next = null;
    if (response.IsSuccessStatusCode)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        next = doc.RootElement.GetProperty("token").GetString();
    }
    return (response.StatusCode, next);
}
async Task<HttpStatusCode> HttpLogout(string host, string? raw)
{
    using var message = Request(HttpMethod.Post, "/__c05_05_test__/logout", host, raw);
    using var response = await client.SendAsync(message);
    return response.StatusCode;
}

var (issuedStatus, issuedA, cacheSafe) = await HttpLogin(hostA.Value, password);
Check(issuedStatus == HttpStatusCode.OK && issuedA is not null && cacheSafe,
    "REAL loopback HTTP POST login returns opaque bearer no-store, no cookie");
Check(await HttpProbe(hostA.Value, issuedA) == HttpStatusCode.NoContent,
    "Real HTTP GET confirms current token A");
Check(await HttpProbe(hostB.Value, issuedA) == HttpStatusCode.Unauthorized,
    "Host B cannot validate bearer minted under tenant A");
Check((await HttpRotate(hostB.Value, issuedA)).Item1 == HttpStatusCode.Unauthorized,
    "Host B cannot rotate tenant A bearer");
Check(await HttpLogout(hostB.Value, issuedA) == HttpStatusCode.NoContent &&
      await HttpProbe(hostA.Value, issuedA) == HttpStatusCode.NoContent,
    "Cross-tenant logout is neutral and cannot revoke tenant A");
Check(await HttpProbe(hostA.Value, nextRandom) == HttpStatusCode.Unauthorized &&
      await HttpProbe(hostA.Value, randomString[..^1] + "x")
          == HttpStatusCode.Unauthorized,
    "Unknown/random forged bearer is denied");
Check((await HttpLogin(hostA.Value, "wrong-long-password")).Item1 ==
      HttpStatusCode.Unauthorized,
    "Wrong password cannot issue a session");
Check((await HttpLogin(hostA.Value, password, "unknown@example.test")).Item1 ==
      HttpStatusCode.Unauthorized,
    "Unknown account cannot issue a session");
Check((await HttpLogin(hostA.Value, password, extraJson:
       "{\"email\":\"user@example.test\",\"password\":\"x\",\"tenantId\":\"forged\"}"
      )).Item1 == HttpStatusCode.BadRequest,
    "Client-provided tenant selector rejected by exact HTTP payload");
Check((await HttpLogin(hostA.Value, password, spoofTenant:tenantB.Value.ToString())).Item1 ==
      HttpStatusCode.BadRequest,
    "Client tenant-id header rejected at transport");
Check((await HttpLogin("unowned.example.test", password)).Item1 ==
      HttpStatusCode.BadRequest,
    "Unverified host is denied before password and SQL");

var (rotateStatus, rotatedA) = await HttpRotate(hostA.Value, issuedA);
Check(rotateStatus == HttpStatusCode.OK && rotatedA is not null &&
      rotatedA != issuedA, "Real HTTP rotation returns NEW unpredictable bearer");
Check(await HttpProbe(hostA.Value, issuedA) == HttpStatusCode.Unauthorized &&
      await HttpProbe(hostA.Value, rotatedA) == HttpStatusCode.NoContent,
    "Old token invalid after atomic rotation, new token current");
Check((await HttpRotate(hostA.Value, issuedA)).Item1 == HttpStatusCode.Unauthorized,
    "Replayed old rotation token never resurrects session");
Check(await HttpProbe(hostA.Value, rotatedA) == HttpStatusCode.NoContent,
    "Old-token replay cannot invalidate current valid token by itself");
Check(await HttpLogout(hostA.Value, rotatedA) == HttpStatusCode.NoContent &&
      await HttpProbe(hostA.Value, rotatedA) == HttpStatusCode.Unauthorized,
    "Logout really revokes token immediately in MySQL");
Check(await HttpLogout(hostA.Value, rotatedA) == HttpStatusCode.NoContent &&
      (await HttpRotate(hostA.Value, rotatedA)).Item1 == HttpStatusCode.Unauthorized,
    "Idempotent HTTP logout and no resurrection after repeated logout");

var (bStatus, issuedB, _) = await HttpLogin(hostB.Value, password);
Check(bStatus == HttpStatusCode.OK && issuedB is not null &&
      await HttpProbe(hostB.Value, issuedB) == HttpStatusCode.NoContent,
    "Same global actor can have independent verified B session");
Check(await HttpProbe(hostA.Value, issuedB) == HttpStatusCode.Unauthorized,
    "A cannot validate independently issued B token");
Check(await HttpLogout(hostB.Value, issuedB) == HttpStatusCode.NoContent &&
      await HttpProbe(hostB.Value, issuedB) == HttpStatusCode.Unauthorized,
    "B logout revokes B without creating A privilege");

// REAL 16-way database CAS race from 16 competing HTTP clients.
// Exactly one can rotate the same token; 15 stale requests are DENIED.
var (_, raceToken, _) = await HttpLogin(hostA.Value, password);
Check(raceToken is not null, "Created race source session");
var raced = await Task.WhenAll(Enumerable.Range(0, 16)
    .Select(_ => HttpRotate(hostA.Value, raceToken)));
var winners = raced.Where(x => x.Item1 == HttpStatusCode.OK).ToArray();
Check(winners.Length == 1 && raced.Count(x => x.Item1 == HttpStatusCode.Unauthorized) == 15,
    "16 concurrent real HTTP+InnoDB rotations yield one winner and 15 denied");
var raceWinner = winners[0].Item2;
Check(raceWinner is not null &&
      await HttpProbe(hostA.Value, raceWinner) == HttpStatusCode.NoContent &&
      await HttpProbe(hostA.Value, raceToken) == HttpStatusCode.Unauthorized,
    "Race winner remains usable and stale token cannot be reused");
Check(await HttpLogout(hostA.Value, raceWinner) == HttpStatusCode.NoContent &&
      await HttpProbe(hostA.Value, raceWinner) == HttpStatusCode.Unauthorized,
    "Concurrent rotation result still revocable");

// Independent DB-side authorization invalidates (and permanently revokes)
// sessions when account/credential/membership/host/tenant revision changes.
var (_, revToken, _) = await HttpLogin(hostA.Value, password);
Check(revToken is not null && await HttpProbe(hostA.Value, revToken) ==
      HttpStatusCode.NoContent, "Started active session for revocation race");
Check(await AdminAsync($"""
    UPDATE c05_05_subjects
       SET credential_active=0, subject_revision=subject_revision+1
     WHERE tenant_id=UNHEX('{tenantA.Value:N}')
    """) == 1, "Fixture credential revocation committed by distinct connection");
Check(await HttpProbe(hostA.Value, revToken) == HttpStatusCode.Unauthorized,
    "Credential revoked in MySQL denies existing session");
Check(await AdminAsync($"""
    UPDATE c05_05_subjects SET credential_active=1
     WHERE tenant_id=UNHEX('{tenantA.Value:N}')
    """) == 1 && await HttpProbe(hostA.Value, revToken) ==
      HttpStatusCode.Unauthorized,
    "Reactivating credential does NOT resurrect revoked session");
Check((await HttpRotate(hostA.Value, revToken)).Item1 ==
      HttpStatusCode.Unauthorized, "Credential-revoked bearer cannot rotate");
Check((await HttpLogin(hostA.Value, password)).Item1 ==
      HttpStatusCode.OK, "A new, reverified login may create a different session");

var (_, expiryToken, _) = await HttpLogin(hostB.Value, password);
Check(expiryToken is not null, "Fixture issued token for short expiry");
var expiryDigest = tokens.TryDigest(expiryToken)!.CopyForTrustedLedger();
try
{
    await using var expiryCmd = new MySqlCommand("""
        UPDATE c05_05_sessions
           SET expires_utc=DATE_ADD(issued_utc, INTERVAL 1 MICROSECOND)
         WHERE token_digest=@digest
        """, setup);
    expiryCmd.Parameters.Add("@digest", MySqlDbType.Binary).Value = expiryDigest;
    Check(await expiryCmd.ExecuteNonQueryAsync() == 1,
        "Expiry mutated only inside disposable privileged fixture");
}
finally { CryptographicOperations.ZeroMemory(expiryDigest); }
Check(await HttpProbe(hostB.Value, expiryToken) == HttpStatusCode.Unauthorized &&
      (await HttpRotate(hostB.Value, expiryToken)).Item1 ==
        HttpStatusCode.Unauthorized,
    "Expired session can neither authorize nor rotate even when digest matches");

// Controlled DB introspection: only HMAC and scoped metadata, no raw token.
Check(await CountAsync("""
    SELECT COUNT(*) FROM information_schema.columns
     WHERE table_schema=DATABASE() AND table_name='c05_05_sessions'
       AND (column_name LIKE '%token_raw%' OR column_name LIKE '%cookie%'
            OR column_name LIKE '%password%' OR column_name LIKE '%email%'
            OR column_name LIKE '%secret%' OR column_name LIKE '%jwt%')
    """) == 0, "No plaintext token, email, password, cookie, JWT or secret DB columns");
Check(await CountAsync("""
    SELECT COUNT(*) FROM c05_05_sessions
     WHERE LENGTH(token_digest)<>32 OR generation<1
    """) == 0, "Every persisted session contains only full-length HMAC digest and revision");
Check(await CountAsync("""
    SELECT COUNT(*) FROM c05_05_sessions WHERE revoked_utc IS NOT NULL
    """) >= 3, "Real MySQL stored revocations, never test-only in-memory flags");
Check(screening.Calls == 2, "Breach policy exercised only on synthetic PHC creation");

await app.StopAsync();

Console.WriteLine($"PASS CRECISOL C05-05 .NET10 real loopback HTTP/MySQL8.4: {checks} assertions");
Console.WriteLine("PASS two tenants A/B, Argon2id, opaque HMAC, issue, validate, rotate, logout, expiry, revoked credential, 16 concurrent HTTP rotations one winner");
Console.WriteLine("LIMIT: localhost disposable fixtures only. No production login, authenticated account directory, MFA, privileged SuperAdmin, HTTPS exposure or live sessions.");

sealed class FixtureCompromisedScreen : ITrustedCompromisedPasswordScreen
{
    public int Calls { get; private set; }
    public PasswordCompromiseVerdict Check(ReadOnlySpan<char> candidate)
    {
        Calls++;
        return PasswordCompromiseVerdict.Allowed;
    }
}
sealed class FixtureClock : ITrustedHostUtcClock
{
    public DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow;
}
sealed class FixtureCredentialDirectory(
    string expected, TrustedLoginCredentialSnapshot valid)
    : ITrustedLoginCredentialDirectory
{
    public Task<IReadOnlyList<TrustedLoginCredentialSnapshot>?> FindExactAsync(
        CanonicalLoginHandle login, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        IReadOnlyList<TrustedLoginCredentialSnapshot> matches =
            login.Value == expected ? new[] {valid} : Array.Empty<TrustedLoginCredentialSnapshot>();
        return Task.FromResult<IReadOnlyList<TrustedLoginCredentialSnapshot>?>(matches);
    }
}
sealed class FixtureDummyVerifier(string dummy) : ITrustedUnknownLoginVerifier
{
    public ValueTask<string?> ReadDummyVerifierAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return ValueTask.FromResult<string?>(dummy);
    }
}
sealed class FixtureEntryReader(CanonicalTenantHost host) : ITrustedLoginEntryReader
{
    public ValueTask<TrustedLoginEntry?> ReadServerVerifiedEntryAsync(
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return ValueTask.FromResult<TrustedLoginEntry?>(TrustedLoginEntry.ForTenantHost(host));
    }
}
sealed class FixtureLinkDirectory(
    TenantActorId actor, IReadOnlyList<AccountTenantLinkSnapshot> records)
    : ITrustedLoginTenantLinkDirectory
{
    public Task<IReadOnlyList<AccountTenantLinkSnapshot>?> FindAllByActorAsync(
        TenantActorId candidate, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<AccountTenantLinkSnapshot>?>(
            candidate == actor ? records : Array.Empty<AccountTenantLinkSnapshot>());
    }
}
sealed class FixtureHostCatalog(
    CanonicalTenantHost hostA, CanonicalTenantHost hostB,
    VerifiedTenantHostBinding bindingA, VerifiedTenantHostBinding bindingB)
    : ITrustedVerifiedHostCatalog
{
    public Task<IReadOnlyList<VerifiedTenantHostBinding>?> FindExactBindingsAsync(
        CanonicalTenantHost candidate, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        IReadOnlyList<VerifiedTenantHostBinding> matches =
            candidate == hostA ? new[] {bindingA} :
            candidate == hostB ? new[] {bindingB} :
            Array.Empty<VerifiedTenantHostBinding>();
        return Task.FromResult<IReadOnlyList<VerifiedTenantHostBinding>?>(matches);
    }
}
sealed class FixtureTenantCatalog(
    TenantId tenantA, TenantId tenantB,
    TenantCatalogSnapshot catalogA, TenantCatalogSnapshot catalogB)
    : ITrustedTenantCatalogReader
{
    public Task<TenantCatalogSnapshot?> FindByIdAsync(
        TenantId candidate, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<TenantCatalogSnapshot?>(
            candidate == tenantA ? catalogA : candidate == tenantB ? catalogB : null);
    }
}
