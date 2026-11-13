using System.Collections.Concurrent;
using System.Net;
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

// NEVER run a fixture or listener except by explicit integration invocation.
if (args.Length == 0)
{
    Console.WriteLine("SKIP C05-07 real MySQL/HTTP: --integration and disposable MySQL8.4 required");
    return;
}
if (args.Length != 1 || args[0] != "--integration")
    throw new InvalidOperationException("Only --integration with disposable local MySQL is accepted.");

var runnerDsn = Environment.GetEnvironmentVariable("CRECISOL_C05_07_RUNNER_DSN");
var setupDsn = Environment.GetEnvironmentVariable("CRECISOL_C05_07_SETUP_DSN");
if (string.IsNullOrWhiteSpace(runnerDsn) || string.IsNullOrWhiteSpace(setupDsn))
    throw new InvalidOperationException("Protected disposable C05-07 test DSNs required.");
var setupSettings = new MySqlConnectionStringBuilder(setupDsn);
if (setupSettings.Server != "127.0.0.1" || setupSettings.Database != "c05_05_disposable")
    throw new InvalidOperationException("Product/nonlocal setup database forbidden.");

var checks = 0;
void Check(bool yes, string label)
{
    if (!yes) throw new InvalidOperationException("FAIL C05-07: " + label);
    checks++;
}
async Task DenyAsync<T>(Func<Task> attempt, string label) where T : Exception
{
    try { await attempt(); }
    catch (T) { checks++; return; }
    throw new InvalidOperationException(
        "FAIL C05-07 expected " + typeof(T).Name + ": " + label);
}

var tenantA = TenantId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
var tenantB = TenantId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
var actor = TenantActorId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01"));
var otherActor = TenantActorId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb02"));
var platformActor = TenantActorId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb03"));
var takenActor = TenantActorId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb04"));
var linkA = AccountTenantLinkId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa12"));
var linkB = AccountTenantLinkId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb12"));
var credentialId = AccountCredentialId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa34"));
var hostA = CanonicalTenantHost.Parse("alpha.example.test");
var hostB = CanonicalTenantHost.Parse("beta.example.test");
var now = DateTimeOffset.UtcNow;
var changed = now.AddDays(-2);
var granted = now.AddDays(-2);
var expires = now.AddHours(4);
const string oldEmail = "shared@example.test";
const string newEmail = "shared-new@example.test";
const string secondEmail = "shared-next@example.test";
const string occupiedEmail = "taken@example.test";
const string verifyEmail = "unverified@example.test";
const string platformEmail = "platform@example.test";

var recoveryTokenKey = RandomNumberGenerator.GetBytes(32);
var mailboxIndexKey = RandomNumberGenerator.GetBytes(32);
var approvalKey = RandomNumberGenerator.GetBytes(32);
var sessionKey = RandomNumberGenerator.GetBytes(32);
using var recoveryProtector = new PasswordRecoveryTokenProtector(
    recoveryTokenKey, mailboxIndexKey);
using var approvalProtector = new MailboxApprovalTokenProtector(approvalKey);
using var sessionProtector = new OpaqueSessionTokenProtector(sessionKey);
CryptographicOperations.ZeroMemory(recoveryTokenKey);
CryptographicOperations.ZeroMemory(mailboxIndexKey);
CryptographicOperations.ZeroMemory(approvalKey);
CryptographicOperations.ZeroMemory(sessionKey);
var ledger = new MySqlDisposableSensitiveMailboxLedger(runnerDsn);
var sessionLedger = new MySqlDisposableSessionLedger(runnerDsn);
var delivery = new FixtureMailboxDelivery();
var screening = new FixtureCompromisedScreen();
var hasher = new Argon2idPasswordHasher(PasswordHashingPolicy.Default,screening);
var currentPassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
var phc = hasher.CreateVerifier(currentPassword).ExportForTrustedCredentialStore();
var dummyPhc = hasher.CreateVerifier(Convert.ToBase64String(
    RandomNumberGenerator.GetBytes(32))).ExportForTrustedCredentialStore();

Check(approvalProtector.ToString()=="MailboxApprovalTokenProtector[redacted]" &&
      ledger.ToString()=="MySqlDisposableSensitiveMailboxLedger[redacted]",
    "No HMAC key, DSN or bearer leaks through diagnostics");
Check(approvalProtector.TryDigest(null) is null &&
      approvalProtector.TryDigest("") is null &&
      approvalProtector.TryDigest("cv7_") is null &&
      approvalProtector.TryDigest("cv7_" + new string('!',43)) is null &&
      approvalProtector.TryDigest("cv7_" + new string('A',42)+"=") is null,
    "Malformed, truncated and padded approval tokens rejected");
var tokenSample=approvalProtector.GenerateFresh();
var tokenRaw=tokenSample.RevealForTrustedDeliveryOnly();
var tokenDigest=approvalProtector.TryDigest(tokenRaw) ??
    throw new InvalidOperationException("Test approval token could not be hashed.");
Check(tokenRaw.Length==47 &&
      tokenSample.ToString()=="IssuedMailboxApproval[redacted]" &&
      tokenDigest.ToString()=="MailboxApprovalDigest[redacted]" &&
      tokenDigest.CopyForTrustedLedger().Length==32,
    "C05-07 opaque 256-bit canonical token is only exposed for delivery");
Check(recoveryProtector.TryDigest(tokenRaw) is null,
    "C05-07 approval token cannot be used as a C05-06 reset token");
Check(approvalProtector.TryDigest(
        recoveryProtector.GenerateFresh().RevealForTrustedDeliveryOnly()) is null,
    "C05-06 recovery token cannot be used for C05-07 approval");
var wrongKey=RandomNumberGenerator.GetBytes(32);
using(var wrongProtector=new MailboxApprovalTokenProtector(wrongKey))
{
    var wrongDigest=wrongProtector.TryDigest(tokenRaw) ??
        throw new InvalidOperationException("Wrong-key fixture token not parsed");
    Check(!CryptographicOperations.FixedTimeEquals(
        wrongDigest.CopyForTrustedLedger(),tokenDigest.CopyForTrustedLedger()),
        "Independent C05-07 HMAC key changes the digest");
}
CryptographicOperations.ZeroMemory(wrongKey);
await DenyAsync<InvalidOperationException>(async () =>
{
    _ = new MailboxApprovalTokenProtector(new byte[32]);
    await Task.CompletedTask;
}, "Empty HMAC key never accepted");
await DenyAsync<InvalidOperationException>(async () =>
{
    _ = new MySqlDisposableSensitiveMailboxLedger(
        "Server=example.com;Database=c05_05_disposable;User ID=x");
    await Task.CompletedTask;
}, "No external SQL host");
await DenyAsync<InvalidOperationException>(async () =>
{
    _ = new MySqlDisposableSensitiveMailboxLedger(
        "Server=127.0.0.1;Database=crecisol_live;User ID=x");
    await Task.CompletedTask;
}, "No production SQL database");

await using var setup = new MySqlConnection(setupSettings.ConnectionString);
await setup.OpenAsync();
async Task<int> AdminAsync(string sql)
{
    await using var cmd=new MySqlCommand(sql,setup);
    return await cmd.ExecuteNonQueryAsync();
}
async Task<long> CountAsync(string sql)
{
    await using var cmd=new MySqlCommand(sql,setup);
    return Convert.ToInt64(await cmd.ExecuteScalarAsync());
}
async Task<byte[]> MailboxInSqlAsync(TenantId tenant,TenantActorId who)
{
    await using var cmd=new MySqlCommand("""
      SELECT mailbox_digest FROM c05_06_mailboxes
       WHERE tenant_id=@tenant AND actor_id=@actor
      """,setup);
    cmd.Parameters.Add("@tenant",MySqlDbType.Binary).Value=
        tenant.Value.ToByteArray(bigEndian:true);
    cmd.Parameters.Add("@actor",MySqlDbType.Binary).Value=
        who.Value.ToByteArray(bigEndian:true);
    return (byte[])(await cmd.ExecuteScalarAsync() ??
        throw new InvalidOperationException("Missing synthetic mailbox"));
}
async Task SeedSubjectAsync(TenantId tenant,TenantActorId who,Guid link)
{
    await using var cmd=new MySqlCommand("""
      INSERT INTO c05_05_subjects
      (tenant_id,actor_id,link_id,account_revision,credential_revision,
       membership_revision,tenant_revision,host_revision,subject_revision,
       account_active,credential_active,membership_active,host_active,
       tenant_active,member_expires_utc)
      VALUES (@t,@a,@l,7,3,5,9,8,1,1,1,1,1,1,@until)
      """,setup);
    cmd.Parameters.Add("@t",MySqlDbType.Binary).Value=
        tenant.Value.ToByteArray(bigEndian:true);
    cmd.Parameters.Add("@a",MySqlDbType.Binary).Value=
        who.Value.ToByteArray(bigEndian:true);
    cmd.Parameters.Add("@l",MySqlDbType.Binary).Value=
        link.ToByteArray(bigEndian:true);
    cmd.Parameters.Add("@until",MySqlDbType.DateTime).Value=expires.UtcDateTime;
    Check(await cmd.ExecuteNonQueryAsync()==1,"Scoped synthetic member");
}
async Task SeedCredentialAsync(TenantActorId who,string realm)
{
    await using var cmd=new MySqlCommand("""
      INSERT INTO c05_06_credentials
       (actor_id,verifier_phc,credential_revision,credential_active,realm)
      VALUES (@actor,@phc,3,1,@realm)
      """,setup);
    cmd.Parameters.Add("@actor",MySqlDbType.Binary).Value=
        who.Value.ToByteArray(bigEndian:true);
    cmd.Parameters.Add("@phc",MySqlDbType.VarChar).Value=phc;
    cmd.Parameters.Add("@realm",MySqlDbType.VarChar).Value=realm;
    Check(await cmd.ExecuteNonQueryAsync()==1,"Protected test PHC and realm");
}
async Task SeedMailboxAsync(TenantId tenant,TenantActorId who,
    string registered,bool verified)
{
    var h=recoveryProtector.DigestCanonicalMailbox(
        CanonicalLoginHandle.Parse(registered)).CopyForTrustedLedger();
    try
    {
        await using var cmd=new MySqlCommand("""
          INSERT INTO c05_06_mailboxes
           (tenant_id,actor_id,mailbox_digest,mailbox_revision,verified)
          VALUES (@t,@a,@digest,1,@v)
          """,setup);
        cmd.Parameters.Add("@t",MySqlDbType.Binary).Value=
            tenant.Value.ToByteArray(bigEndian:true);
        cmd.Parameters.Add("@a",MySqlDbType.Binary).Value=
            who.Value.ToByteArray(bigEndian:true);
        cmd.Parameters.Add("@digest",MySqlDbType.Binary).Value=h;
        cmd.Parameters.Add("@v",MySqlDbType.Byte).Value=verified?1:0;
        Check(await cmd.ExecuteNonQueryAsync()==1,"Mailbox HMAC inserted without email");
    }
    finally { CryptographicOperations.ZeroMemory(h); }
}
async Task<string> SeedSessionAsync(TenantId tenant,TenantActorId who)
{
    var bearer=sessionProtector.GenerateFresh().RevealForTrustedTransport();
    var d=sessionProtector.TryDigest(bearer)?.CopyForTrustedLedger() ??
        throw new InvalidOperationException("Fresh fixture session token missing");
    try
    {
        await using var cmd=new MySqlCommand("""
          INSERT INTO c05_05_sessions
           (session_id,tenant_id,actor_id,token_digest,generation,
            subject_revision,issued_utc,expires_utc,absolute_expires_utc,revoked_utc)
          VALUES (@id,@tenant,@actor,@digest,1,
                  (SELECT subject_revision FROM c05_05_subjects
                    WHERE tenant_id=@tenant AND actor_id=@actor),
                  UTC_TIMESTAMP(6),
                  DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 15 MINUTE),
                  DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 8 HOUR),NULL)
          """,setup);
        cmd.Parameters.Add("@id",MySqlDbType.Binary).Value=
            Guid.NewGuid().ToByteArray(bigEndian:true);
        cmd.Parameters.Add("@tenant",MySqlDbType.Binary).Value=
            tenant.Value.ToByteArray(bigEndian:true);
        cmd.Parameters.Add("@actor",MySqlDbType.Binary).Value=
            who.Value.ToByteArray(bigEndian:true);
        cmd.Parameters.Add("@digest",MySqlDbType.Binary).Value=d;
        Check(await cmd.ExecuteNonQueryAsync()==1,
            "Synthetic pre-change session stored only with digest");
    }
    finally { CryptographicOperations.ZeroMemory(d); }
    return bearer;
}

await SeedSubjectAsync(tenantA,actor,linkA.Value);
await SeedSubjectAsync(tenantB,actor,linkB.Value);
await SeedSubjectAsync(tenantB,otherActor,
    Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb22"));
await SeedSubjectAsync(tenantB,platformActor,
    Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb33"));
await SeedSubjectAsync(tenantB,takenActor,
    Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb44"));
await SeedCredentialAsync(actor,"TENANT");
await SeedCredentialAsync(otherActor,"TENANT");
await SeedCredentialAsync(platformActor,"PLATFORM");
await SeedCredentialAsync(takenActor,"TENANT");
await SeedMailboxAsync(tenantA,actor,oldEmail,true);
await SeedMailboxAsync(tenantB,actor,oldEmail,true);
await SeedMailboxAsync(tenantB,otherActor,verifyEmail,false);
await SeedMailboxAsync(tenantB,platformActor,platformEmail,false);
await SeedMailboxAsync(tenantB,takenActor,occupiedEmail,true);

var account=IdentityAccountSnapshot.RestoreFromTrustedIdentityDirectory(
    actor,IdentityAccountState.Active,7,changed);
var credential=AccountCredentialMetadata.RestoreFromTrustedCredentialDirectory(
    credentialId,actor,AccountCredentialKind.PasswordVerifier,
    AccountCredentialState.Active,3,changed);
var trustedCredential=TrustedLoginCredentialSnapshot.RestoreFromTrustedDirectory(
    account,credential,TrustedLoginRealm.TenantAccount,phc);
var links=new[]{
    AccountTenantLinkSnapshot.RestoreFromTrustedMembershipDirectory(
        linkA,actor,tenantA,AccountTenantProfileKind.Employee,
        TenantMembershipState.Active,5,changed,granted,expires),
    AccountTenantLinkSnapshot.RestoreFromTrustedMembershipDirectory(
        linkB,actor,tenantB,AccountTenantProfileKind.Employee,
        TenantMembershipState.Active,5,changed,granted,expires)
};
var catalogA=TenantCatalogSnapshot.RestoreFromTrustedCatalog(
    tenantA,"alpha-internal","Alpha synthetic",TenantLifecycleState.Active,
    TenantAddressMode.Subdomain,9,changed);
var catalogB=TenantCatalogSnapshot.RestoreFromTrustedCatalog(
    tenantB,"beta-internal","Beta synthetic",TenantLifecycleState.Active,
    TenantAddressMode.Subdomain,9,changed);
var bindingA=VerifiedTenantHostBinding.RestoreFromTrustedCatalog(
    tenantA,hostA,TenantAddressMode.Subdomain,HostVerificationState.Verified,
    8,changed,granted,expires);
var bindingB=VerifiedTenantHostBinding.RestoreFromTrustedCatalog(
    tenantB,hostB,TenantAddressMode.Subdomain,HostVerificationState.Verified,
    8,changed,granted,expires);
var clock=new FixtureClock();
var resolver=new ResolveVerifiedTenantHost(
    new FixtureHostCatalog(hostA,hostB,bindingA,bindingB),clock);
var currentLogin=oldEmail;
PrepareContextualPasswordLogin LoginResolver(CanonicalTenantHost host) =>
    new(new FixtureCredentialDirectory(()=>currentLogin,trustedCredential),
        new FixtureDummyVerifier(dummyPhc),
        new Argon2idLoginCandidateVerifier(hasher),
        new FixtureEntryReader(host),
        new FixtureLinkDirectory(actor,links),
        resolver,new FixtureTenantCatalog(tenantA,tenantB,catalogA,catalogB),
        clock);

var freshReauthentication=new FixtureImmediateReauthentication(
    hint => LoginResolver(hint.TenantId==tenantA ? hostA : hostB));
var sensitive = new VerifyAndChangeSensitiveMailbox(
    approvalProtector,recoveryProtector,ledger,delivery,freshReauthentication);
Check(sensitive.ToString()=="VerifyAndChangeSensitiveMailbox[redacted]",
    "Sensitive email use case cannot log secrets or credentials");

var loginCorrect=await LoginResolver(hostA).ExecuteAsync(
    CanonicalLoginHandle.Parse(oldEmail),currentPassword.AsMemory(),
    CancellationToken.None);
var loginWrong=await LoginResolver(hostA).ExecuteAsync(
    CanonicalLoginHandle.Parse(oldEmail),"wrong-password".AsMemory(),
    CancellationToken.None);
Check(loginCorrect.Status==ContextualLoginPreparationStatus.Ready &&
      loginCorrect.Evidence?.TenantId==tenantA &&
      loginCorrect.Evidence?.HostRevision==8 &&
      loginWrong.Status==ContextualLoginPreparationStatus.Denied,
    "Real C05-04 Argon2id proof and verified host required for sensitive changes");

await DenyAsync<MySqlException>(async () =>
{
    await using var connection=new MySqlConnection(runnerDsn);
    await connection.OpenAsync();
    await using var cmd=new MySqlCommand(
        "SELECT mailbox_digest FROM c05_06_mailboxes",connection);
    await cmd.ExecuteScalarAsync();
}, "Procedure runner cannot read mailbox HMAC or private account metadata");
await DenyAsync<MySqlException>(async () =>
{
    await using var connection=new MySqlConnection(runnerDsn);
    await connection.OpenAsync();
    await using var cmd=new MySqlCommand(
        "SELECT event_type FROM c05_07_security_audit",connection);
    await cmd.ExecuteScalarAsync();
}, "Runner cannot query private security audit log");

var builder=WebApplication.CreateBuilder(new WebApplicationOptions {
    Args=Array.Empty<string>(),EnvironmentName="Production"
});
builder.Logging.ClearProviders();
builder.WebHost.UseUrls("http://127.0.0.1:0");
await using var app=builder.Build();
app.UseExceptionHandler(error => error.Run(context =>
{
    context.Response.StatusCode=StatusCodes.Status503ServiceUnavailable;
    return Task.CompletedTask;
}));
app.Use(async(context,next)=>
{
    context.Response.Headers.CacheControl="no-store";
    context.Response.Headers.Pragma="no-cache";
    context.Response.Headers["Referrer-Policy"]="no-referrer";
    context.Response.Headers["X-Content-Type-Options"]="nosniff";
    await next(context);
});

async Task<(CanonicalTenantHost Host,VerifiedTenantHostHint Hint)?> HostAsync(
    HttpContext context)
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
        var host=CanonicalTenantHost.Parse(context.Request.Host.Host);
        if(host!=hostA && host!=hostB) return null;
        var hint=await resolver.ResolveAsync(host,context.RequestAborted);
        return (host,hint);
    }
    catch (InvalidOperationException) { return null; }
    catch (UnauthorizedAccessException) { return null; }
}
static async Task<JsonDocument?> ReadJson(HttpContext context,int maxSize)
{
    if (context.Request.ContentLength is null or <= 0 ||
        context.Request.ContentLength>maxSize ||
        !(context.Request.ContentType?.StartsWith(
            "application/json",StringComparison.OrdinalIgnoreCase) ?? false))
        return null;
    try
    {
        return await JsonDocument.ParseAsync(context.Request.Body,
            new JsonDocumentOptions {MaxDepth=4},context.RequestAborted);
    }
    catch (JsonException) { return null; }
}

// All endpoints exist ONLY in the executable, never Crecisol.Api/Program.cs.
app.MapPost("/__c05_07_test__/verify/request",async(HttpContext c)=>
{
    var host=await HostAsync(c);
    if (host is null) return Results.BadRequest();
    using var doc=await ReadJson(c,1024);
    if(doc is null) return Results.BadRequest();
    var body=doc.RootElement;
    if(body.ValueKind!=JsonValueKind.Object ||
       body.EnumerateObject().Count()!=1 ||
       !body.TryGetProperty("email",out var mail) ||
       mail.ValueKind!=JsonValueKind.String)
        return Results.BadRequest();
    try
    {
        await sensitive.RequestVerificationAsync(
            CanonicalLoginHandle.Parse(mail.GetString()),host.Value.Hint,
            c.RequestAborted);
        return Results.Accepted();
    }
    catch (InvalidOperationException) { return Results.BadRequest(); }
});
app.MapPost("/__c05_07_test__/verify/confirm",async(HttpContext c)=>
{
    var host=await HostAsync(c);
    if(host is null) return Results.BadRequest();
    using var doc=await ReadJson(c,1024);
    if(doc is null) return Results.BadRequest();
    var body=doc.RootElement;
    if(body.ValueKind!=JsonValueKind.Object ||
       body.EnumerateObject().Count()!=1 ||
       !body.TryGetProperty("token",out var code) ||
       code.ValueKind!=JsonValueKind.String)
        return Results.BadRequest();
    return await sensitive.ConfirmVerificationAsync(
        code.GetString(),host.Value.Hint,c.RequestAborted)
        ? Results.NoContent() : Results.Unauthorized();
});
app.MapPost("/__c05_07_test__/change/request",async(HttpContext c)=>
{
    var host=await HostAsync(c);
    if(host is null) return Results.BadRequest();
    using var doc=await ReadJson(c,2048);
    if(doc is null) return Results.BadRequest();
    var body=doc.RootElement;
    if(body.ValueKind!=JsonValueKind.Object ||
       body.EnumerateObject().Count()!=3 ||
       !body.TryGetProperty("oldEmail",out var old) ||
       !body.TryGetProperty("newEmail",out var next) ||
       !body.TryGetProperty("password",out var pwd) ||
       old.ValueKind!=JsonValueKind.String ||
       next.ValueKind!=JsonValueKind.String ||
       pwd.ValueKind!=JsonValueKind.String)
        return Results.BadRequest();
    try
    {
        var previous=CanonicalLoginHandle.Parse(old.GetString());
        var replacement=CanonicalLoginHandle.Parse(next.GetString());
        await sensitive.RequestChangeAsync(previous,replacement,
            (pwd.GetString()??"").AsMemory(),host.Value.Hint,c.RequestAborted);
        return Results.Accepted();
    }
    catch (InvalidOperationException) { return Results.BadRequest(); }
});
app.MapPost("/__c05_07_test__/change/confirm",async(HttpContext c)=>
{
    var host=await HostAsync(c);
    if(host is null) return Results.BadRequest();
    using var doc=await ReadJson(c,1024);
    if(doc is null) return Results.BadRequest();
    var body=doc.RootElement;
    if(body.ValueKind!=JsonValueKind.Object ||
       body.EnumerateObject().Count()!=1 ||
       !body.TryGetProperty("token",out var code) ||
       code.ValueKind!=JsonValueKind.String)
        return Results.BadRequest();
    return await sensitive.ConfirmChangeAsync(
        code.GetString(),host.Value.Hint,c.RequestAborted)
        ? Results.NoContent() : Results.Unauthorized();
});
await app.StartAsync();
var endpoint=app.Services.GetRequiredService<IServer>().Features
    .Get<IServerAddressesFeature>()?.Addresses.Single();
if(endpoint is null || !endpoint.StartsWith(
       "http://127.0.0.1:",StringComparison.Ordinal))
    throw new InvalidOperationException("C05-07 test API must bind loopback only.");
using var client=new HttpClient(new SocketsHttpHandler {
    UseProxy=false,AllowAutoRedirect=false
}) {BaseAddress=new Uri(endpoint),Timeout=TimeSpan.FromSeconds(75)};

HttpRequestMessage Req(string route,string host,string body,
    string? forgedTenant=null,bool forwarded=false)
{
    var request=new HttpRequestMessage(HttpMethod.Post,route);
    request.Headers.Host=host;
    if(forgedTenant is not null)
        request.Headers.TryAddWithoutValidation("X-Tenant-Id",forgedTenant);
    if(forwarded)
        request.Headers.TryAddWithoutValidation("X-Forwarded-Host","alpha.example.test");
    request.Content=new StringContent(body,Encoding.UTF8,"application/json");
    return request;
}
async Task<(HttpStatusCode Status,bool Neutral)> HttpAsync(
    string route,string host,object body,string? forgedTenant=null,
    bool forwarded=false)
{
    using var req=Req(route,host,JsonSerializer.Serialize(body),forgedTenant,forwarded);
    using var response=await client.SendAsync(req);
    var payload=await response.Content.ReadAsStringAsync();
    var neutral=payload.Length==0 &&
        !response.Headers.Contains("Set-Cookie") &&
        response.Headers.CacheControl?.NoStore==true &&
        response.Headers.TryGetValues("Referrer-Policy",out var values) &&
        values.Contains("no-referrer");
    return(response.StatusCode,neutral);
}
Task<(HttpStatusCode Status,bool Neutral)> VerifyReq(string host,string email)=>
    HttpAsync("/__c05_07_test__/verify/request",host,new {email});
Task<(HttpStatusCode Status,bool Neutral)> VerifyConfirm(string host,string token)=>
    HttpAsync("/__c05_07_test__/verify/confirm",host,new {token});
Task<(HttpStatusCode Status,bool Neutral)> ChangeReq(
    string host,string previous,string next,string password)=>
    HttpAsync("/__c05_07_test__/change/request",host,
        new {oldEmail=previous,newEmail=next,password});
Task<(HttpStatusCode Status,bool Neutral)> ChangeConfirm(string host,string token)=>
    HttpAsync("/__c05_07_test__/change/confirm",host,new {token});

const string requestVerify="/__c05_07_test__/verify/request";
var unverified=await VerifyReq(hostB.Value,verifyEmail);
var absent=await VerifyReq(hostB.Value,"unknown@example.test");
Check(unverified==(HttpStatusCode.Accepted,true) &&
      absent==unverified && delivery.Count==1,
    "Verified/unknown mailbox requests share HTTP 202 empty no-cache response");
Check((await VerifyReq(hostB.Value,platformEmail)).Status==
      HttpStatusCode.Accepted && delivery.Count==1,
    "Platform operator cannot use tenant mailbox verification channel");
Check((await HttpAsync(requestVerify,hostB.Value,new {email=verifyEmail},
        tenantA.Value.ToString())).Status==HttpStatusCode.BadRequest &&
      (await HttpAsync(requestVerify,hostB.Value,new {email=verifyEmail},
        forwarded:true)).Status==HttpStatusCode.BadRequest &&
      (await VerifyReq("unowned.example.test",verifyEmail)).Status==
        HttpStatusCode.BadRequest,
    "Forged tenant, forwarded headers and unowned hosts denied");
var verifyFirst=delivery.Latest(tenantB,verifyEmail,
    TrustedMailboxProofDeliveryKind.VerifyRegisteredMailbox);

Check((await VerifyReq(hostB.Value,verifyEmail))==unverified &&
      delivery.Count==2,
    "Reissuing verification replaces previous pending link");
var verifySecond=delivery.Latest(tenantB,verifyEmail,
    TrustedMailboxProofDeliveryKind.VerifyRegisteredMailbox);
Check((await VerifyConfirm(hostB.Value,verifyFirst)).Status==
      HttpStatusCode.Unauthorized,
    "Reissued verification invalidates prior confirmation link");
var verifySecondDigest=approvalProtector.TryDigest(verifySecond)!
    .CopyForTrustedLedger();
try
{
    await using var cmd=new MySqlCommand("""
      UPDATE c05_07_verifications
         SET issued_utc=DATE_SUB(UTC_TIMESTAMP(6),INTERVAL 20 MINUTE),
             expires_utc=DATE_SUB(UTC_TIMESTAMP(6),INTERVAL 10 MINUTE)
       WHERE token_digest=@digest
      """,setup);
    cmd.Parameters.Add("@digest",MySqlDbType.Binary).Value=verifySecondDigest;
    Check(await cmd.ExecuteNonQueryAsync()==1,
        "Expire a genuine 10-minute mailbox link in local MySQL");
}
finally { CryptographicOperations.ZeroMemory(verifySecondDigest); }
Check((await VerifyConfirm(hostB.Value,verifySecond)).Status==
      HttpStatusCode.Unauthorized,
    "Expired verification link cannot confirm the registered mailbox");
Check((await VerifyReq(hostB.Value,verifyEmail))==unverified &&
      delivery.Count==3,"Fresh verification can be issued after expiry");
var verifyThird=delivery.Latest(tenantB,verifyEmail,
    TrustedMailboxProofDeliveryKind.VerifyRegisteredMailbox);
Check((await VerifyConfirm(hostA.Value,verifyThird)).Status==
      HttpStatusCode.Unauthorized,
    "Tenant A cannot confirm a mailbox belonging to B");
Check((await VerifyConfirm(hostB.Value,verifyThird))==
      (HttpStatusCode.NoContent,true),
    "Valid B verification uses one-time SQL CAS and no token response");
Check((await VerifyConfirm(hostB.Value,verifyThird)).Status==
      HttpStatusCode.Unauthorized &&
      (await VerifyConfirm(hostB.Value,verifySecond)).Status==
        HttpStatusCode.Unauthorized,
    "Verification link replay and expired link stay denied permanently");
Check(await CountAsync($"""
    SELECT COUNT(*) FROM c05_06_mailboxes
    WHERE tenant_id=UNHEX('{tenantB.Value:N}')
      AND actor_id=UNHEX('{otherActor.Value:N}')
      AND verified=1 AND mailbox_revision=2
    """)==1,
    "Verified mailbox and automatic revision bump committed in InnoDB");
Check((await VerifyReq(hostB.Value,verifyEmail))==unverified &&
      delivery.Count==3,
    "Already verified mailbox does not receive another verification link");

var sessionA=await SeedSessionAsync(tenantA,actor);
var sessionB=await SeedSessionAsync(tenantB,actor);
var otherSession=await SeedSessionAsync(tenantB,otherActor);
Check(await sessionLedger.IsCurrentAsync(tenantA,
        sessionProtector.TryDigest(sessionA)!,CancellationToken.None) &&
      await sessionLedger.IsCurrentAsync(tenantB,
        sessionProtector.TryDigest(sessionB)!,CancellationToken.None) &&
      await sessionLedger.IsCurrentAsync(tenantB,
        sessionProtector.TryDigest(otherSession)!,CancellationToken.None),
    "Two active sessions A/B and unrelated actor B exist before mailbox change");

var oldHmac=recoveryProtector.DigestCanonicalMailbox(
    CanonicalLoginHandle.Parse(oldEmail)).CopyForTrustedLedger();
try
{
    var currentA=await MailboxInSqlAsync(tenantA,actor);
    Check(CryptographicOperations.FixedTimeEquals(currentA,oldHmac),
        "Initial verified mailbox in A has only canonical digest");
    Check(CryptographicOperations.FixedTimeEquals(
        await MailboxInSqlAsync(tenantB,actor),oldHmac),
        "B retains independent mailbox binding for same actor");
}
finally { CryptographicOperations.ZeroMemory(oldHmac); }

var baselineDelivery=delivery.Count;
var badPassword=await ChangeReq(hostA.Value,oldEmail,newEmail,"incorrect");
var unknownLogin=await ChangeReq(hostA.Value,"nonexistent@example.test",newEmail,
    currentPassword);
Check(badPassword==(HttpStatusCode.Accepted,true) &&
      unknownLogin==badPassword &&
      delivery.Count==baselineDelivery,
    "Incorrect Argon2id/unknown account changes yield neutral HTTP and no approval links");
Check(freshReauthentication.Calls>=2,
    "Each sensitive request MUST invoke fresh C05-04 Argon2id verification inside use case");
Check((await ChangeReq(hostA.Value,oldEmail,oldEmail,currentPassword)).Status==
      HttpStatusCode.Accepted && delivery.Count==baselineDelivery,
    "Equal old/new mailbox never starts sensitive change");
Check((await ChangeReq(hostA.Value,oldEmail,occupiedEmail,currentPassword)).Status==
      HttpStatusCode.Accepted && delivery.Count==baselineDelivery,
    "Globally occupied mailbox cannot be silently reassigned to another actor");
Check((await HttpAsync("/__c05_07_test__/change/request",
        hostA.Value,new{oldEmail,newEmail,password=currentPassword},
        tenantB.Value.ToString())).Status==HttpStatusCode.BadRequest,
    "No browser tenant selector can initiate a sensitive change");

var pending=await ChangeReq(hostA.Value,oldEmail,newEmail,currentPassword);
Check(pending==(HttpStatusCode.Accepted,true) &&
      delivery.Count==baselineDelivery+2,
    "Fresh C05-04 Argon2id password proof initiates TWO independent deliveries");
var oldProof=delivery.Latest(tenantA,oldEmail,
    TrustedMailboxProofDeliveryKind.ConfirmPreviousMailbox);
var newProof=delivery.Latest(tenantA,newEmail,
    TrustedMailboxProofDeliveryKind.ConfirmReplacementMailbox);
Check(oldProof!=newProof &&
      (await ChangeConfirm(hostB.Value,newProof)).Status==
        HttpStatusCode.Unauthorized &&
      (await ChangeConfirm(hostA.Value,"cr6_"+new string('A',43))).Status==
        HttpStatusCode.Unauthorized,
    "Independent C05-07 proofs cannot be used cross-tenant or as recovery tokens");
var preapproval=await MailboxInSqlAsync(tenantA,actor);
var expectedNext=recoveryProtector.DigestCanonicalMailbox(
    CanonicalLoginHandle.Parse(newEmail)).CopyForTrustedLedger();
Check(!CryptographicOperations.FixedTimeEquals(preapproval,expectedNext) &&
      await CountAsync("SELECT COUNT(*) FROM c05_07_security_audit WHERE event_type='MAILBOX_CHANGED'")==0,
    "Starting a change NEVER mutates mailbox or creates a changed audit event");
Check((await ChangeConfirm(hostA.Value,newProof))==
      (HttpStatusCode.NoContent,true) &&
      (await ChangeConfirm(hostA.Value,newProof)).Status==
        HttpStatusCode.Unauthorized,
    "New-mailbox confirmation alone is recorded once, never enough to change");
Check(!CryptographicOperations.FixedTimeEquals(
        await MailboxInSqlAsync(tenantA,actor),expectedNext),
    "New address alone cannot replace old verified address");
Check((await ChangeConfirm(hostA.Value,oldProof))==
      (HttpStatusCode.NoContent,true),
    "Original mailbox confirmation plus new mailbox confirms final atomic change");
Check(CryptographicOperations.FixedTimeEquals(
        await MailboxInSqlAsync(tenantA,actor),expectedNext) &&
      !CryptographicOperations.FixedTimeEquals(
        await MailboxInSqlAsync(tenantB,actor),expectedNext),
    "Applied change is scoped to A mailbox; B identity binding remains isolated");
CryptographicOperations.ZeroMemory(expectedNext);
Check((await ChangeConfirm(hostA.Value,oldProof)).Status==
      HttpStatusCode.Unauthorized &&
      (await ChangeConfirm(hostA.Value,newProof)).Status==
        HttpStatusCode.Unauthorized,
    "Both one-use links permanently denied after successful application");
Check(!await sessionLedger.IsCurrentAsync(tenantA,
        sessionProtector.TryDigest(sessionA)!,CancellationToken.None) &&
      !await sessionLedger.IsCurrentAsync(tenantB,
        sessionProtector.TryDigest(sessionB)!,CancellationToken.None) &&
      await sessionLedger.IsCurrentAsync(tenantB,
        sessionProtector.TryDigest(otherSession)!,CancellationToken.None),
    "Sensitive email change revokes all actor A/B sessions, not another actor");
Check(await CountAsync($"""
    SELECT COUNT(*) FROM c05_05_sessions
     WHERE actor_id=UNHEX('{actor.Value:N}') AND revoked_utc IS NOT NULL
    """)==2,
    "Global actor session revocation persisted in MySQL, not just memory");
Check(await CountAsync($"""
    SELECT COUNT(*) FROM c05_05_subjects
     WHERE actor_id=UNHEX('{actor.Value:N}') AND subject_revision=2
    """)==2,
    "Subject revision advanced in BOTH tenants to prevent stale access");
Check(await CountAsync("""
    SELECT COUNT(*) FROM c05_07_security_audit
     WHERE event_type='MAILBOX_CHANGED'
    """)==1 &&
      await CountAsync("""
    SELECT COUNT(*) FROM c05_07_security_audit
     WHERE event_type='PREVIOUS_CONFIRMED'
    """)==1 &&
      await CountAsync("""
    SELECT COUNT(*) FROM c05_07_security_audit
     WHERE event_type='REPLACEMENT_CONFIRMED'
    """)==1,
    "Append-only audit binds two independent confirmations to one applied change");

await DenyAsync<MySqlException>(async () =>
{
    await using var c=new MySqlCommand("""
      UPDATE c05_07_security_audit SET event_type='MAILBOX_CHANGED'
       WHERE event_id=1
      """,setup);
    await c.ExecuteNonQueryAsync();
}, "Append-only database audit cannot be modified");
await DenyAsync<MySqlException>(async () =>
{
    await using var c=new MySqlCommand("""
      DELETE FROM c05_07_security_audit WHERE event_id=1
      """,setup);
    await c.ExecuteNonQueryAsync();
}, "Append-only database audit cannot be deleted");

// Fixture's protected global login directory is updated only AFTER the
// successful CAS; it is deliberately not a real product identity directory.
currentLogin=newEmail;
var afterChange=await LoginResolver(hostA).ExecuteAsync(
    CanonicalLoginHandle.Parse(newEmail),currentPassword.AsMemory(),
    CancellationToken.None);
Check(afterChange.Status==ContextualLoginPreparationStatus.Ready,
    "A freshly verified C05-04 login can reauthenticate the new mailbox");

// Suspended then restored membership MUST invalidate an already issued link.
await ChangeReq(hostA.Value,newEmail,secondEmail,currentPassword);
var staleOld=delivery.Latest(tenantA,newEmail,
    TrustedMailboxProofDeliveryKind.ConfirmPreviousMailbox);
Check(await AdminAsync($"""
    UPDATE c05_05_subjects SET membership_active=0
     WHERE tenant_id=UNHEX('{tenantA.Value:N}')
       AND actor_id=UNHEX('{actor.Value:N}')
    """)==1 &&
      (await ChangeConfirm(hostA.Value,staleOld)).Status==
      HttpStatusCode.Unauthorized,
    "Revoked membership denies pending sensitive confirmation");
Check(await AdminAsync($"""
    UPDATE c05_05_subjects SET membership_active=1
     WHERE tenant_id=UNHEX('{tenantA.Value:N}')
       AND actor_id=UNHEX('{actor.Value:N}')
    """)==1 &&
      (await ChangeConfirm(hostA.Value,staleOld)).Status==
      HttpStatusCode.Unauthorized,
    "Restoring a revoked membership never resurrects stale challenge");

// A mailbox off/on verification transition invalidates the old approval.
await ChangeReq(hostA.Value,newEmail,secondEmail,currentPassword);
var staleNew=delivery.Latest(tenantA,secondEmail,
    TrustedMailboxProofDeliveryKind.ConfirmReplacementMailbox);
Check(await AdminAsync($"""
    UPDATE c05_06_mailboxes SET verified=0
     WHERE tenant_id=UNHEX('{tenantA.Value:N}')
       AND actor_id=UNHEX('{actor.Value:N}')
    """)==1 &&
      (await ChangeConfirm(hostA.Value,staleNew)).Status==
        HttpStatusCode.Unauthorized,
    "Deverification invalidates pending new address proof");
Check(await AdminAsync($"""
    UPDATE c05_06_mailboxes SET verified=1
     WHERE tenant_id=UNHEX('{tenantA.Value:N}')
       AND actor_id=UNHEX('{actor.Value:N}')
    """)==1 &&
      (await ChangeConfirm(hostA.Value,staleNew)).Status==
        HttpStatusCode.Unauthorized,
    "Re-verification cannot resurrect an earlier sensitive change");

await ChangeReq(hostA.Value,newEmail,secondEmail,currentPassword);
var expiryOld=delivery.Latest(tenantA,newEmail,
    TrustedMailboxProofDeliveryKind.ConfirmPreviousMailbox);
var expiryHash=approvalProtector.TryDigest(expiryOld)!.CopyForTrustedLedger();
try
{
    await using var cmd=new MySqlCommand("""
       UPDATE c05_07_changes
          SET issued_utc=DATE_SUB(UTC_TIMESTAMP(6),INTERVAL 10 MINUTE),
              expires_utc=DATE_SUB(UTC_TIMESTAMP(6),INTERVAL 5 MINUTE)
        WHERE old_token_digest=@digest
      """,setup);
    cmd.Parameters.Add("@digest",MySqlDbType.Binary).Value=expiryHash;
    Check(await cmd.ExecuteNonQueryAsync()==1,
        "Shorten a genuine five-minute dual approval window only in fixture");
}
finally { CryptographicOperations.ZeroMemory(expiryHash); }
Check((await ChangeConfirm(hostA.Value,expiryOld)).Status==
      HttpStatusCode.Unauthorized,
    "Expired previous-address approval cannot be used");

// 16 concurrent HTTP clients compete to finish the second dual-mail change.
// Old approval alone cannot change the address; exactly ONE new approval wins.
await ChangeReq(hostA.Value,newEmail,secondEmail,currentPassword);
var raceOld=delivery.Latest(tenantA,newEmail,
    TrustedMailboxProofDeliveryKind.ConfirmPreviousMailbox);
var raceNew=delivery.Latest(tenantA,secondEmail,
    TrustedMailboxProofDeliveryKind.ConfirmReplacementMailbox);
Check((await ChangeConfirm(hostA.Value,raceOld)).Status==
      HttpStatusCode.NoContent,
    "First old-mail proof recorded before concurrent second-mail redemption");
var race=await Task.WhenAll(Enumerable.Range(0,16).Select(_=>
    ChangeConfirm(hostA.Value,raceNew)));
var raceDistribution=string.Join(", ",race.GroupBy(x=>x.Status)
    .OrderBy(x=>(int)x.Key).Select(x=>$"{(int)x.Key}:{x.Count()}"));
Check(race.Count(x=>x.Status==HttpStatusCode.NoContent)==1 &&
      race.Count(x=>x.Status==HttpStatusCode.Unauthorized)==15,
    "16 concurrent real HTTP/MySQL second confirmations: exactly 1 winner and 15 denied; observed "+raceDistribution);
var secondDigest=recoveryProtector.DigestCanonicalMailbox(
    CanonicalLoginHandle.Parse(secondEmail)).CopyForTrustedLedger();
try
{
    Check(CryptographicOperations.FixedTimeEquals(
          await MailboxInSqlAsync(tenantA,actor),secondDigest) &&
          (await ChangeConfirm(hostA.Value,raceNew)).Status==
             HttpStatusCode.Unauthorized,
      "Concurrent result committed once and stale second proof cannot be replayed");
}
finally { CryptographicOperations.ZeroMemory(secondDigest); }

Check(await CountAsync("""
    SELECT COUNT(*) FROM c05_07_security_audit
     WHERE event_type='MAILBOX_CHANGED'
    """)==2, "Exactly 2 legitimate changed-mail events, none for denied attempts");
Check(await CountAsync("""
    SELECT COUNT(*) FROM c05_07_changes
     WHERE state='APPLIED' AND
       (old_confirmed_utc IS NULL OR new_confirmed_utc IS NULL)
    """)==0, "No email may be changed without BOTH one-use mailbox proofs");
Check(await CountAsync("""
    SELECT COUNT(*) FROM information_schema.columns
     WHERE table_schema=DATABASE() AND table_name='c05_07_security_audit'
       AND (column_name LIKE '%email%' OR column_name LIKE '%mailbox%'
         OR column_name LIKE '%token%' OR column_name LIKE '%digest%'
         OR column_name LIKE '%password%' OR column_name LIKE '%secret%')
    """)==0, "Immutable audit never stores token, digest, mailbox, password or email");
Check(await CountAsync("""
    SELECT COUNT(*) FROM c05_07_changes
     WHERE state='APPLIED' AND old_token_digest=new_token_digest
    """)==0, "Old/new mailbox approvals always use independent secrets");
await app.StopAsync();

Console.WriteLine($"PASS CRECISOL C05-07 .NET10 real HTTP/MySQL8.4: {checks} assertions");
Console.WriteLine("PASS verified mailbox, reauth Argon2id, independent old+new confirmations, five-minute expiry, 16-way one-winner CAS, audit immutability, A/B isolation and durable session revocation");
Console.WriteLine("LIMIT: local test HTTP, memory mailbox sink and disposable MySQL only. NO real SMTP, account email change, MFA, production identity or deployment.");

sealed class FixtureImmediateReauthentication(
    Func<VerifiedTenantHostHint,PrepareContextualPasswordLogin> resolver)
    : ITrustedImmediatePasswordReauthentication
{
    private int count;
    public int Calls=>Volatile.Read(ref count);
    public Task<ContextualLoginPreparation> VerifyFreshAsync(
        CanonicalLoginHandle handle,ReadOnlyMemory<char> password,
        VerifiedTenantHostHint trustedHost,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Interlocked.Increment(ref count);
        return resolver(trustedHost).ExecuteAsync(handle,password,ct);
    }
}
sealed class FixtureCompromisedScreen : ITrustedCompromisedPasswordScreen
{
    public PasswordCompromiseVerdict Check(ReadOnlySpan<char> _) =>
        PasswordCompromiseVerdict.Allowed;
}
sealed class FixtureClock : ITrustedHostUtcClock
{
    public DateTimeOffset GetUtcNow()=>DateTimeOffset.UtcNow;
}
sealed class FixtureCredentialDirectory(
    Func<string> expected,TrustedLoginCredentialSnapshot credential)
    : ITrustedLoginCredentialDirectory
{
    public Task<IReadOnlyList<TrustedLoginCredentialSnapshot>?> FindExactAsync(
        CanonicalLoginHandle candidate,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        IReadOnlyList<TrustedLoginCredentialSnapshot> matches=
            candidate.Value==expected()?new[]{credential}:
            Array.Empty<TrustedLoginCredentialSnapshot>();
        return Task.FromResult<IReadOnlyList<TrustedLoginCredentialSnapshot>?>(matches);
    }
}
sealed class FixtureDummyVerifier(string phc) : ITrustedUnknownLoginVerifier
{
    public ValueTask<string?> ReadDummyVerifierAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return ValueTask.FromResult<string?>(phc);
    }
}
sealed class FixtureEntryReader(CanonicalTenantHost host) : ITrustedLoginEntryReader
{
    public ValueTask<TrustedLoginEntry?> ReadServerVerifiedEntryAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return ValueTask.FromResult<TrustedLoginEntry?>(
            TrustedLoginEntry.ForTenantHost(host));
    }
}
sealed class FixtureLinkDirectory(
    TenantActorId actor,IReadOnlyList<AccountTenantLinkSnapshot> links)
    : ITrustedLoginTenantLinkDirectory
{
    public Task<IReadOnlyList<AccountTenantLinkSnapshot>?> FindAllByActorAsync(
        TenantActorId candidate,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<AccountTenantLinkSnapshot>?>(
            candidate==actor?links:Array.Empty<AccountTenantLinkSnapshot>());
    }
}
sealed class FixtureHostCatalog(
    CanonicalTenantHost a,CanonicalTenantHost b,
    VerifiedTenantHostBinding ba,VerifiedTenantHostBinding bb)
    : ITrustedVerifiedHostCatalog
{
    public Task<IReadOnlyList<VerifiedTenantHostBinding>?> FindExactBindingsAsync(
        CanonicalTenantHost host,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        IReadOnlyList<VerifiedTenantHostBinding> matches=
            host==a?new[]{ba}:host==b?new[]{bb}:
            Array.Empty<VerifiedTenantHostBinding>();
        return Task.FromResult<IReadOnlyList<VerifiedTenantHostBinding>?>(matches);
    }
}
sealed class FixtureTenantCatalog(
    TenantId a,TenantId b,TenantCatalogSnapshot ca,TenantCatalogSnapshot cb)
    : ITrustedTenantCatalogReader
{
    public Task<TenantCatalogSnapshot?> FindByIdAsync(TenantId tenant,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<TenantCatalogSnapshot?>(
            tenant==a?ca:tenant==b?cb:null);
    }
}
sealed class FixtureMailboxDelivery : ITrustedMailboxApprovalDelivery
{
    private readonly ConcurrentQueue<(
        TenantId Tenant,string Mail,TrustedMailboxProofDeliveryKind Kind,string Raw)> sent=new();
    public int Count=>sent.Count;
    public Task DeliverAsync(TenantId tenant,CanonicalLoginHandle recipient,
        IssuedMailboxApproval token,TrustedMailboxProofDeliveryKind kind,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        sent.Enqueue((tenant,recipient.Value,kind,token.RevealForTrustedDeliveryOnly()));
        return Task.CompletedTask;
    }
    public string Latest(TenantId tenant,string mail,TrustedMailboxProofDeliveryKind kind)=>
        sent.Last(x=>x.Tenant==tenant && x.Mail==mail && x.Kind==kind).Raw;
}
