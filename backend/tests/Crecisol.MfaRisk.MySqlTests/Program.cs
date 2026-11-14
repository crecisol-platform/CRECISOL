using System.Buffers.Binary;
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

if (args.Length==0)
{
    Console.WriteLine("SKIP C05-08 real MFA HTTP/MySQL: --integration required");
    return;
}
if (args.Length!=1 || args[0]!="--integration")
    throw new InvalidOperationException("C05-08 accepts --integration only.");
string? setupDsn=Environment.GetEnvironmentVariable("CRECISOL_C05_08_SETUP_DSN");
string? runnerDsn=Environment.GetEnvironmentVariable("CRECISOL_C05_08_RUNNER_DSN");
if (string.IsNullOrWhiteSpace(setupDsn)||string.IsNullOrWhiteSpace(runnerDsn))
    throw new InvalidOperationException("C05-08 disposable MySQL DSNs required.");
var setupConfig=new MySqlConnectionStringBuilder(setupDsn);
if (setupConfig.Server!="127.0.0.1" ||
    setupConfig.Database!="c05_05_disposable" || setupConfig.AllowLoadLocalInfile)
    throw new InvalidOperationException("C05-08 strictly local disposable fixture.");
var ledger=new MySqlDisposableMfaLedger(runnerDsn);
var sessionLedger=new MySqlDisposableSessionLedger(runnerDsn);
int checks=0;
void Check(bool yes,string what)
{
    if (!yes) throw new InvalidOperationException("FAIL C05-08 "+what);
    Interlocked.Increment(ref checks);
}
async Task RejectSql(Func<Task> action,string reason)
{
    try { await action(); }
    catch(MySqlException) { Check(true,reason); return; }
    throw new InvalidOperationException("FAIL expected MySQL rejection: "+reason);
}

var tenantA=TenantId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
var tenantB=TenantId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
var actor=TenantActorId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01"));
var outsider=TenantActorId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb02"));
var platform=TenantActorId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb03"));
var linkA=AccountTenantLinkId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa12"));
var linkB=AccountTenantLinkId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbba12"));
var hostA=CanonicalTenantHost.Parse("alpha.example.test");
var hostB=CanonicalTenantHost.Parse("beta.example.test");
var now=DateTimeOffset.UtcNow;
var changed=now.AddDays(-2);
var expires=now.AddHours(5);
string login="mfa-user@example.test";
string currentPassword=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
var hasher=new Argon2idPasswordHasher(PasswordHashingPolicy.Default,
    new FixtureCompromisedScreen());
string phc=hasher.CreateVerifier(currentPassword).ExportForTrustedCredentialStore();
string dummy=hasher.CreateVerifier(Convert.ToBase64String(
    RandomNumberGenerator.GetBytes(32))).ExportForTrustedCredentialStore();
var mailKey=RandomNumberGenerator.GetBytes(32);
var recoveryKey=RandomNumberGenerator.GetBytes(32);
var backupKey=RandomNumberGenerator.GetBytes(32);
var challengeKey=RandomNumberGenerator.GetBytes(32);
var sessionKey=RandomNumberGenerator.GetBytes(32);
using var mailboxProtector=new PasswordRecoveryTokenProtector(recoveryKey,mailKey);
using var backupProtector=new MfaRecoveryCodeProtector(backupKey);
using var challengeProtector=new DisposableMfaChallengeProtector(challengeKey);
using var sessionProtector=new OpaqueSessionTokenProtector(sessionKey);
CryptographicOperations.ZeroMemory(mailKey);
CryptographicOperations.ZeroMemory(recoveryKey);
CryptographicOperations.ZeroMemory(backupKey);
CryptographicOperations.ZeroMemory(challengeKey);
CryptographicOperations.ZeroMemory(sessionKey);
var fixtureSecrets=new FixtureMfaSecrets(challengeProtector);
var mfa=new DisposableMfaRiskUseCases(
    new FixtureImmediateReauthentication(),ledger,fixtureSecrets,
    challengeProtector,new DisposableMfaBackupAdapter(backupProtector));

Check(mfa.ToString()=="DisposableMfaRiskUseCases[redacted]" &&
      ledger.ToString()=="MySqlDisposableMfaLedger[redacted]" &&
      challengeProtector.ToString()=="DisposableMfaChallengeProtector[redacted]",
    "MFA service, ledger and secrets redact diagnostics");
Check(challengeProtector.TryDigest("cv7_"+new string('A',43)) is null &&
      challengeProtector.TryDigest("ma8_"+new string('!',43)) is null &&
      challengeProtector.TryDigest(null) is null &&
      backupProtector.TryDigest(challengeProtector.GenerateForFixtureOnly()) is null,
    "Different HMAC purposes, malformed challenges and backup transplants denied");
try { _=new MySqlDisposableMfaLedger("Server=example.com;Database=c05_05_disposable;Uid=x"); }
catch(InvalidOperationException) { Check(true,"No external MySQL fixture endpoint"); }
try { _=new MySqlDisposableMfaLedger("Server=127.0.0.1;Database=crecisol_live;Uid=x"); }
catch(InvalidOperationException) { Check(true,"No production MySQL DB"); }

await using var setup=new MySqlConnection(setupConfig.ConnectionString);
await setup.OpenAsync();
async Task<long> CountAsync(string sql)
{
    await using var cmd=new MySqlCommand(sql,setup);
    return Convert.ToInt64(await cmd.ExecuteScalarAsync());
}
async Task SeedSubjectAsync(TenantId tenant,TenantActorId who,AccountTenantLinkId link)
{
    await using var cmd=new MySqlCommand("""
      INSERT INTO c05_05_subjects
      (tenant_id,actor_id,link_id,account_revision,credential_revision,
       membership_revision,tenant_revision,host_revision,subject_revision,
       account_active,credential_active,membership_active,host_active,
       tenant_active,member_expires_utc)
      VALUES (@tenant,@actor,@link,7,3,5,9,8,1,1,1,1,1,1,@expires)
      """,setup);
    cmd.Parameters.Add("@tenant",MySqlDbType.Binary).Value=tenant.Value.ToByteArray(bigEndian:true);
    cmd.Parameters.Add("@actor",MySqlDbType.Binary).Value=who.Value.ToByteArray(bigEndian:true);
    cmd.Parameters.Add("@link",MySqlDbType.Binary).Value=link.Value.ToByteArray(bigEndian:true);
    cmd.Parameters.Add("@expires",MySqlDbType.DateTime).Value=expires.UtcDateTime;
    Check(await cmd.ExecuteNonQueryAsync()==1,"Synthetic isolated identity seeded");
}
async Task SeedCredentialAsync(TenantActorId who,string realm)
{
    await using var cmd=new MySqlCommand("""
      INSERT INTO c05_06_credentials
        (actor_id,verifier_phc,credential_revision,credential_active,realm)
      VALUES(@actor,@phc,3,1,@realm)
      """,setup);
    cmd.Parameters.Add("@actor",MySqlDbType.Binary).Value=who.Value.ToByteArray(bigEndian:true);
    cmd.Parameters.Add("@phc",MySqlDbType.VarChar).Value=phc;
    cmd.Parameters.Add("@realm",MySqlDbType.VarChar).Value=realm;
    Check(await cmd.ExecuteNonQueryAsync()==1,"Argon2id PHC fixture only, realm checked");
}
async Task SeedMailboxAsync(TenantId tenant,TenantActorId who,string mail)
{
    byte[] digest=mailboxProtector.DigestCanonicalMailbox(
        CanonicalLoginHandle.Parse(mail)).CopyForTrustedLedger();
    try
    {
        await using var cmd=new MySqlCommand("""
          INSERT INTO c05_06_mailboxes
            (tenant_id,actor_id,mailbox_digest,mailbox_revision,verified)
          VALUES(@t,@a,@digest,1,1)
          """,setup);
        cmd.Parameters.Add("@t",MySqlDbType.Binary).Value=tenant.Value.ToByteArray(bigEndian:true);
        cmd.Parameters.Add("@a",MySqlDbType.Binary).Value=who.Value.ToByteArray(bigEndian:true);
        cmd.Parameters.Add("@digest",MySqlDbType.Binary).Value=digest;
        Check(await cmd.ExecuteNonQueryAsync()==1,"Verified synthetic mailbox HMAC only");
    }
    finally { CryptographicOperations.ZeroMemory(digest); }
}
await SeedSubjectAsync(tenantA,actor,linkA);
await SeedSubjectAsync(tenantB,actor,linkB);
await SeedSubjectAsync(tenantB,outsider,
    AccountTenantLinkId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb22")));
await SeedSubjectAsync(tenantB,platform,
    AccountTenantLinkId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb33")));
await SeedCredentialAsync(actor,"TENANT");
await SeedCredentialAsync(outsider,"TENANT");
await SeedCredentialAsync(platform,"PLATFORM");
await SeedMailboxAsync(tenantA,actor,login);
await SeedMailboxAsync(tenantB,actor,login);
await SeedMailboxAsync(tenantB,outsider,"outsider@example.test");
await SeedMailboxAsync(tenantB,platform,"platform@example.test");

var account=IdentityAccountSnapshot.RestoreFromTrustedIdentityDirectory(
    actor,IdentityAccountState.Active,7,changed);
var credential=AccountCredentialMetadata.RestoreFromTrustedCredentialDirectory(
    AccountCredentialId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa34")),
    actor,AccountCredentialKind.PasswordVerifier,AccountCredentialState.Active,3,changed);
var trusted=TrustedLoginCredentialSnapshot.RestoreFromTrustedDirectory(
    account,credential,TrustedLoginRealm.TenantAccount,phc);
var links=new[]{
    AccountTenantLinkSnapshot.RestoreFromTrustedMembershipDirectory(
        linkA,actor,tenantA,AccountTenantProfileKind.Employee,
        TenantMembershipState.Active,5,changed,changed,expires),
    AccountTenantLinkSnapshot.RestoreFromTrustedMembershipDirectory(
        linkB,actor,tenantB,AccountTenantProfileKind.Employee,
        TenantMembershipState.Active,5,changed,changed,expires)
};
var catalogA=TenantCatalogSnapshot.RestoreFromTrustedCatalog(
    tenantA,"alpha-internal","Alpha synthetic",TenantLifecycleState.Active,
    TenantAddressMode.Subdomain,9,changed);
var catalogB=TenantCatalogSnapshot.RestoreFromTrustedCatalog(
    tenantB,"beta-internal","Beta synthetic",TenantLifecycleState.Active,
    TenantAddressMode.Subdomain,9,changed);
var bindingA=VerifiedTenantHostBinding.RestoreFromTrustedCatalog(
    tenantA,hostA,TenantAddressMode.Subdomain,
    HostVerificationState.Verified,8,changed,changed,expires);
var bindingB=VerifiedTenantHostBinding.RestoreFromTrustedCatalog(
    tenantB,hostB,TenantAddressMode.Subdomain,
    HostVerificationState.Verified,8,changed,changed,expires);
var clock=new FixtureClock();
var resolver=new ResolveVerifiedTenantHost(
    new FixtureHostCatalog(hostA,hostB,bindingA,bindingB),clock);
PrepareContextualPasswordLogin Login(CanonicalTenantHost host) =>
    new(new FixtureCredentialDirectory(login,trusted),
        new FixtureDummyVerifier(dummy),
        new Argon2idLoginCandidateVerifier(hasher),
        new FixtureEntryReader(host),
        new FixtureLinkDirectory(actor,links),resolver,
        new FixtureTenantCatalog(tenantA,tenantB,catalogA,catalogB),
        clock);
var reauth=new FixtureImmediateReauthentication(hint =>
    Login(hint.TenantId==tenantA?hostA:hostB));
mfa=new DisposableMfaRiskUseCases(reauth,ledger,fixtureSecrets,
    challengeProtector,new DisposableMfaBackupAdapter(backupProtector));
var hintA=await resolver.ResolveAsync(hostA,CancellationToken.None);
var hintB=await resolver.ResolveAsync(hostB,CancellationToken.None);
Check(hintA.TenantId==tenantA && hintB.TenantId==tenantB &&
      hintA.Revision==8 && hintB.Revision==8,
    "Verified host maps A/B independently on backend");

var loginResult=await Login(hostA).ExecuteAsync(
    CanonicalLoginHandle.Parse(login),currentPassword.AsMemory(),CancellationToken.None);
Check(loginResult.Status==ContextualLoginPreparationStatus.Ready &&
      loginResult.Evidence is not null &&
      (await Login(hostA).ExecuteAsync(CanonicalLoginHandle.Parse(login),
          "wrong".AsMemory(),CancellationToken.None)).Status==
          ContextualLoginPreparationStatus.Denied,
    "Real current C05-04 Argon2id password proof is mandatory");
var proofA=loginResult.Evidence!;
var loginB=await Login(hostB).ExecuteAsync(
    CanonicalLoginHandle.Parse(login),currentPassword.AsMemory(),CancellationToken.None);
Check(loginB.Status==ContextualLoginPreparationStatus.Ready &&
      loginB.Evidence is not null,
    "Global actor legitimately has distinct tenant B membership");
var proofB=loginB.Evidence!;

async Task<string> IssueSessionAsync(PreparedContextualLoginEvidence proof)
{
    string bearer=sessionProtector.GenerateFresh().RevealForTrustedTransport();
    var digest=sessionProtector.TryDigest(bearer)!;
    Check(await sessionLedger.TryIssueAsync(proof,Guid.NewGuid(),digest,CancellationToken.None),
        "Live C05-05 MySQL session issued with synthetic authenticated context");
    return bearer;
}
string bearerA=await IssueSessionAsync(proofA);
string bearerB=await IssueSessionAsync(proofB);
Check(await sessionLedger.IsCurrentAsync(tenantA,
        sessionProtector.TryDigest(bearerA)!,CancellationToken.None) &&
      await sessionLedger.IsCurrentAsync(tenantB,
        sessionProtector.TryDigest(bearerB)!,CancellationToken.None),
    "Both global-actor tenant A/B sessions are initially active");
await RejectSql(async()=>
{
    await using var db=new MySqlConnection(runnerDsn);
    await db.OpenAsync();
    await using var cmd=new MySqlCommand("SELECT * FROM c05_08_backup_codes",db);
    _=await cmd.ExecuteScalarAsync();
},"EXECUTE-only MFA runner cannot read backup digests");
await RejectSql(async()=>
{
    await using var db=new MySqlConnection(runnerDsn);
    await db.OpenAsync();
    await using var cmd=new MySqlCommand("UPDATE c05_08_factors SET state='ACTIVE'",db);
    _=await cmd.ExecuteNonQueryAsync();
},"EXECUTE-only MFA runner cannot directly activate factors");

var queue=new ConcurrentQueue<(
    DisposableMfaChallenge Handle, DisposableMfaProofPurpose Purpose)>();
var pending=new ConcurrentDictionary<Guid,(
    DisposableMfaChallenge Handle, DisposableMfaProofPurpose Purpose)>();
void Track(DisposableMfaChallenge handle,DisposableMfaProofPurpose purpose)
{
    pending[handle.ChallengeId]=(handle,purpose);
    queue.Enqueue((handle,purpose));
}
DisposableMfaChallenge Last()=>queue.Last().Handle;

var builder=WebApplication.CreateBuilder(new WebApplicationOptions {
    Args=Array.Empty<string>(),EnvironmentName="Production"
});
builder.Logging.ClearProviders();
builder.WebHost.UseUrls("http://127.0.0.1:0");
await using var app=builder.Build();
app.UseExceptionHandler(error=>error.Run(c=>
{
    c.Response.StatusCode=503;
    return Task.CompletedTask;
}));
app.Use(async(c,next)=>
{
    c.Response.Headers.CacheControl="no-store";
    c.Response.Headers.Pragma="no-cache";
    c.Response.Headers["Referrer-Policy"]="no-referrer";
    await next(c);
});
async Task<VerifiedTenantHostHint?> HostAsync(HttpContext c)
{
    if(c.Request.Host.Port.HasValue || c.Request.QueryString.HasValue ||
       c.Request.Headers.ContainsKey("Forwarded") ||
       c.Request.Headers.ContainsKey("X-Forwarded-Host") ||
       c.Request.Headers.ContainsKey("X-Tenant-Id") ||
       c.Request.Headers.ContainsKey("Authorization") ||
       c.Request.Headers.ContainsKey("Cookie"))
        return null;
    var incoming=c.Request.Host.Host;
    if (incoming!=hostA.Value && incoming!=hostB.Value) return null;
    try
    {
        return await resolver.ResolveAsync(
            CanonicalTenantHost.Parse(incoming),c.RequestAborted);
    }
    catch(InvalidOperationException) { return null; }
    catch(UnauthorizedAccessException) { return null; }
}
static async Task<JsonDocument?> BodyAsync(HttpContext c,int max=4096)
{
    if(c.Request.ContentLength is null or <=0 ||
       c.Request.ContentLength>max ||
       !(c.Request.ContentType?.StartsWith("application/json",
          StringComparison.OrdinalIgnoreCase)??false))
        return null;
    try
    {
        return await JsonDocument.ParseAsync(c.Request.Body,
            new JsonDocumentOptions {MaxDepth=4},c.RequestAborted);
    }
    catch(JsonException) { return null; }
}
static bool Exact(JsonElement body,params string[] keys)
{
    return body.ValueKind==JsonValueKind.Object &&
           body.EnumerateObject().Count()==keys.Length &&
           keys.All(k=>body.TryGetProperty(k,out var x)&&x.ValueKind==JsonValueKind.String);
}
static string Value(JsonElement body,string key)=>body.GetProperty(key).GetString()??"";
static DisposableMfaKind? Kind(string kind)=>kind switch
{
    "TOTP"=>DisposableMfaKind.Totp,
    "PASSKEY"=>DisposableMfaKind.Passkey,
    _=>null
};
app.MapPost("/__c05_08_test__/enroll",async(HttpContext c)=>
{
    var host=await HostAsync(c);
    if(host is null) return Results.BadRequest();
    using var doc=await BodyAsync(c);
    if(doc is null || !Exact(doc.RootElement,"email","password","kind"))
        return Results.BadRequest();
    var body=doc.RootElement;
    var kind=Kind(Value(body,"kind"));
    if(kind is null) return Results.BadRequest();
    try
    {
        var result=await mfa.BeginEnrollmentAsync(
            CanonicalLoginHandle.Parse(Value(body,"email")),
            Value(body,"password").AsMemory(),host,kind.Value,c.RequestAborted);
        if(result is not null) Track(result,DisposableMfaProofPurpose.Enroll);
        return Results.Accepted(); // uniform; never return seed/backup/token/identity
    }
    catch(InvalidOperationException) { return Results.BadRequest(); }
});
app.MapPost("/__c05_08_test__/stepup",async(HttpContext c)=>
{
    var host=await HostAsync(c);
    if(host is null) return Results.BadRequest();
    using var doc=await BodyAsync(c);
    if(doc is null || !Exact(doc.RootElement,"email","password","factorId"))
        return Results.BadRequest();
    var body=doc.RootElement;
    if(!Guid.TryParse(Value(body,"factorId"),out var factor))
        return Results.BadRequest();
    try
    {
        var result=await mfa.BeginStepUpAsync(
            CanonicalLoginHandle.Parse(Value(body,"email")),
            Value(body,"password").AsMemory(),host,factor,
            Guid.NewGuid(),c.RequestAborted);
        if(result is not null) Track(result,DisposableMfaProofPurpose.StepUp);
        return Results.Accepted(); // no action grant / tenant selector
    }
    catch(InvalidOperationException) { return Results.BadRequest(); }
});
app.MapPost("/__c05_08_test__/recover",async(HttpContext c)=>
{
    var host=await HostAsync(c);
    if(host is null) return Results.BadRequest();
    using var doc=await BodyAsync(c);
    if(doc is null || !Exact(doc.RootElement,
        "email","password","factorId","backupCode","kind"))
        return Results.BadRequest();
    var body=doc.RootElement;
    if(!Guid.TryParse(Value(body,"factorId"),out var factor) ||
       Kind(Value(body,"kind")) is not { } kind)
        return Results.BadRequest();
    try
    {
        var result=await mfa.BeginRecoveryReplacementAsync(
            CanonicalLoginHandle.Parse(Value(body,"email")),
            Value(body,"password").AsMemory(),host,factor,
            Value(body,"backupCode"),kind,c.RequestAborted);
        if(result is not null) Track(result,DisposableMfaProofPurpose.Enroll);
        return Results.Accepted(); // backup never creates a step-up grant
    }
    catch(InvalidOperationException) { return Results.BadRequest(); }
});
app.MapPost("/__c05_08_test__/prove",async(HttpContext c)=>
{
    var host=await HostAsync(c);
    if(host is null) return Results.BadRequest();
    using var doc=await BodyAsync(c,8192);
    if(doc is null) return Results.BadRequest();
    var body=doc.RootElement;
    bool totp=Exact(body,"challengeId","code");
    bool passkey=Exact(body,"challengeId","credentialId",
        "clientData","authenticatorData","signature");
    if(!totp && !passkey) return Results.BadRequest();
    if(!Guid.TryParse(Value(body,"challengeId"),out var id) ||
        !pending.TryGetValue(id,out var operation))
        return Results.Unauthorized();
    try
    {
        bool proven;
        if(totp)
            proven=await mfa.ProveTotpAsync(host,actor,operation.Handle,
                operation.Purpose,Value(body,"code"),c.RequestAborted);
        else
        {
            var assertion=new DisposableMfaAssertion(
                Convert.FromBase64String(Value(body,"credentialId")),
                Convert.FromBase64String(Value(body,"clientData")),
                Convert.FromBase64String(Value(body,"authenticatorData")),
                Convert.FromBase64String(Value(body,"signature")));
            proven=await mfa.ProveFixturePasskeyAsync(host,actor,
                operation.Handle,operation.Purpose,assertion,c.RequestAborted);
        }
        return proven?Results.NoContent():Results.Unauthorized();
    }
    catch(FormatException) { return Results.BadRequest(); }
    catch(InvalidOperationException) { return Results.Unauthorized(); }
});
await app.StartAsync();
string? endpoint=app.Services.GetRequiredService<IServer>().Features
    .Get<IServerAddressesFeature>()?.Addresses.Single();
if(endpoint is null ||
   !endpoint.StartsWith("http://127.0.0.1:",StringComparison.Ordinal))
    throw new InvalidOperationException("C05-08 test harness must bind loopback.");
using var client=new HttpClient(new SocketsHttpHandler {
    UseProxy=false,AllowAutoRedirect=false
}) {BaseAddress=new Uri(endpoint),Timeout=TimeSpan.FromSeconds(90)};
async Task<HttpStatusCode> RequestAsync(string route,string host,object payload,
    string? forgedTenant=null,bool forgedForwarded=false)
{
    using var request=new HttpRequestMessage(HttpMethod.Post,
        "/__c05_08_test__/"+route);
    request.Headers.Host=host;
    if(forgedTenant is not null)
        request.Headers.TryAddWithoutValidation("X-Tenant-Id",forgedTenant);
    if(forgedForwarded)
        request.Headers.TryAddWithoutValidation("X-Forwarded-Host","alpha.example.test");
    request.Content=new StringContent(JsonSerializer.Serialize(payload),Encoding.UTF8,"application/json");
    using var response=await client.SendAsync(request);
    Check(response.Headers.CacheControl?.NoStore==true &&
          !response.Headers.Contains("Set-Cookie") &&
          (await response.Content.ReadAsStringAsync()).Length==0,
        "Test HTTP response contains no bearer, seed, key, backup or PII");
    return response.StatusCode;
}
Task<HttpStatusCode> Enroll(string host,string pass,string kind="TOTP")=>
    RequestAsync("enroll",host,new{email=login,password=pass,kind});
Task<HttpStatusCode> Stepup(string host,string pass,Guid factor)=>
    RequestAsync("stepup",host,new{email=login,password=pass,factorId=factor.ToString()});
Task<HttpStatusCode> Recover(string host,string pass,Guid old,string backup,string kind)=>
    RequestAsync("recover",host,new{
        email=login,password=pass,factorId=old.ToString(),backupCode=backup,kind
    });
Task<HttpStatusCode> ProveTotp(string host,DisposableMfaChallenge handle,string code)=>
    RequestAsync("prove",host,new{challengeId=handle.ChallengeId.ToString(),code});
Task<HttpStatusCode> ProvePasskey(string host,DisposableMfaChallenge handle,
    DisposableMfaAssertion assertion)=>
    RequestAsync("prove",host,new{
        challengeId=handle.ChallengeId.ToString(),
        credentialId=Convert.ToBase64String(assertion.CredentialId),
        clientData=Convert.ToBase64String(assertion.ClientDataJson),
        authenticatorData=Convert.ToBase64String(assertion.AuthenticatorData),
        signature=Convert.ToBase64String(assertion.Signature)
    });

int before=queue.Count;
Check(await Enroll(hostA.Value,"incorrect")==HttpStatusCode.Accepted &&
      queue.Count==before,
    "Wrong Argon2id cannot begin MFA enrollment or disclose account status");
Check(await Enroll("evil.example.test",currentPassword)==HttpStatusCode.BadRequest &&
      await RequestAsync("enroll",hostA.Value,new{
          email=login,password=currentPassword,kind="TOTP"},
          tenantB.Value.ToString())==HttpStatusCode.BadRequest &&
      await RequestAsync("enroll",hostA.Value,new{
          email=login,password=currentPassword,kind="TOTP"},
          forgedForwarded:true)==HttpStatusCode.BadRequest &&
      queue.Count==before,
    "Unowned host, X-Tenant-Id and forged proxy host cannot select another tenant");
Check(await Enroll(hostA.Value,currentPassword)==HttpStatusCode.Accepted &&
      queue.Count==before+1,
    "Real Argon2id and C05-07 verified mailbox allow PENDING TOTP in tenant A");
var initial=Last();
Check(initial.BackupCodeForTrustedFixtureOnly?.StartsWith("cm8_")==true &&
      (await ledger.ReadCounterAsync(tenantA,actor,initial.FactorId,CancellationToken.None))
        ?.State=="PENDING",
    "Initial factor and single-use backup are pending, no active MFA yet");
Check(await ProveTotp(hostA.Value,initial,"999999")==HttpStatusCode.Unauthorized &&
      await ProveTotp(hostB.Value,initial,fixtureSecrets.TotpCode(initial.FactorId))==
        HttpStatusCode.Unauthorized,
    "Wrong TOTP and cross-tenant verification denied");
Check(await ProveTotp(hostA.Value,initial,fixtureSecrets.TotpCode(initial.FactorId))==
      HttpStatusCode.NoContent &&
      await ProveTotp(hostA.Value,initial,fixtureSecrets.TotpCode(initial.FactorId))==
        HttpStatusCode.Unauthorized,
    "Enrollment requires fresh genuine TOTP and cannot be replayed");
Check((await ledger.ReadCounterAsync(tenantA,actor,initial.FactorId,CancellationToken.None))
       ?.State=="ACTIVE",
    "Real SQL CAS activates factor only after cryptographic enrollment proof");
Check(await Enroll(hostA.Value,currentPassword)==HttpStatusCode.Accepted &&
      queue.Count==before+1,
    "Duplicate ACTIVE factor cannot be re-enrolled as bypass");

Check(await Stepup(hostA.Value,"wrong",initial.FactorId)==HttpStatusCode.Accepted &&
      queue.Count==before+1,
    "Wrong password cannot issue a risk-bound step-up challenge");
Check(await Stepup(hostB.Value,currentPassword,initial.FactorId)==HttpStatusCode.Accepted &&
      queue.Count==before+1,
    "Tenant A TOTP factor cannot be used for tenant B step-up");
Check(await Stepup(hostA.Value,currentPassword,initial.FactorId)==HttpStatusCode.Accepted &&
      queue.Count==before+2,
    "Fresh password with active factor issues operation-bound step-up only");
var oldStep=Last();
Check(await ProveTotp(hostA.Value,oldStep,fixtureSecrets.TotpCode(initial.FactorId))==
      HttpStatusCode.Unauthorized,
    "Even distinct challenge cannot replay TOTP in previously committed tick");
fixtureSecrets.MoveTrustedTimeForward(TimeSpan.FromSeconds(40));
var validCode=fixtureSecrets.TotpCode(initial.FactorId);
var sixteen=await Task.WhenAll(Enumerable.Range(0,16).Select(_=>
    ProveTotp(hostA.Value,oldStep,validCode)));
Check(sixteen.Count(x=>x==HttpStatusCode.NoContent)==1 &&
      sixteen.Count(x=>x==HttpStatusCode.Unauthorized)==15,
    "16 concurrent genuine HTTP/MySQL8.4 step-up redemptions: exactly 1 winner / 15 denied");
Check(await ProveTotp(hostA.Value,oldStep,validCode)==HttpStatusCode.Unauthorized,
    "Committed step-up challenge cannot be repeated");
Check(await CountAsync("SELECT COUNT(*) FROM c05_08_security_audit WHERE event_type='STEPUP_PROVED'")==1,
    "One and only one audited risk-bound step-up proof");

Check(await Stepup(hostA.Value,currentPassword,initial.FactorId)==HttpStatusCode.Accepted,
    "New short-lived step-up can be issued for explicit expiry test");
var expiredStep=Last();
await using(var expire=new MySqlCommand("""
    UPDATE c05_08_challenges
       SET issued_utc=DATE_SUB(UTC_TIMESTAMP(6),INTERVAL 10 MINUTE),
           expires_utc=DATE_SUB(UTC_TIMESTAMP(6),INTERVAL 5 MINUTE)
     WHERE challenge_id=@id
    """,setup))
{
    expire.Parameters.Add("@id",MySqlDbType.Binary).Value=
        expiredStep.ChallengeId.ToByteArray(bigEndian:true);
    Check(await expire.ExecuteNonQueryAsync()==1,
        "Trusted fixture shortens MySQL step-up expiry to an expired window");
}
fixtureSecrets.MoveTrustedTimeForward(TimeSpan.FromSeconds(40));
Check(await ProveTotp(hostA.Value,expiredStep,
        fixtureSecrets.TotpCode(initial.FactorId))==HttpStatusCode.Unauthorized,
    "Expired step-up cannot be consumed despite a fresh correct TOTP");

Check(await Enroll(hostB.Value,currentPassword,"PASSKEY")==HttpStatusCode.Accepted,
    "Passkey fixture is independently enrollable for tenant B");
var bPasskey=Last();
var bAssertion=fixtureSecrets.SignFixtureAssertion(bPasskey,1);
Check(await ProvePasskey(hostA.Value,bPasskey,bAssertion)==HttpStatusCode.Unauthorized,
    "Signed B passkey cannot enroll on A host");
Check(await ProvePasskey(hostB.Value,bPasskey,
        fixtureSecrets.SignFixtureAssertion(bPasskey,1,
            forgedOrigin:"https://evil.example.test"))==HttpStatusCode.Unauthorized,
    "Signed phishing origin cannot register as verified passkey");
Check(await ProvePasskey(hostB.Value,bPasskey,bAssertion)==HttpStatusCode.NoContent &&
      await ProvePasskey(hostB.Value,bPasskey,bAssertion)==HttpStatusCode.Unauthorized,
    "WebAuthn ES256/UP+UV/RP/challenge/counter proof activates synthetic passkey once");
Check(await Stepup(hostB.Value,currentPassword,bPasskey.FactorId)==HttpStatusCode.Accepted,
    "Active passkey B can issue B risk-bound step-up");
var bStep=Last();
// Toggle the registered mailbox before proving an already issued challenge;
// C05-06 revision triggers make restoration strictly different from uninterrupted verification.
Check(await CountAsync($"""
    SELECT mailbox_revision FROM c05_06_mailboxes
     WHERE tenant_id=UNHEX('{tenantB.Value:N}')
       AND actor_id=UNHEX('{actor.Value:N}')
    """)==1, "Verified B mailbox starts at revision one");
await using(var off=new MySqlCommand($"""
    UPDATE c05_06_mailboxes SET verified=0
     WHERE tenant_id=UNHEX('{tenantB.Value:N}')
       AND actor_id=UNHEX('{actor.Value:N}')
    """,setup))
    Check(await off.ExecuteNonQueryAsync()==1,"Fixture de-verifies B mailbox");
await using(var on=new MySqlCommand($"""
    UPDATE c05_06_mailboxes SET verified=1
     WHERE tenant_id=UNHEX('{tenantB.Value:N}')
       AND actor_id=UNHEX('{actor.Value:N}')
    """,setup))
    Check(await on.ExecuteNonQueryAsync()==1,"Fixture restores B mailbox");
Check(await CountAsync($"""
    SELECT mailbox_revision FROM c05_06_mailboxes
     WHERE tenant_id=UNHEX('{tenantB.Value:N}')
       AND actor_id=UNHEX('{actor.Value:N}')
    """)==3, "Mailbox off/on increments revision twice");
Check(await ProvePasskey(hostB.Value,bStep,
        fixtureSecrets.SignFixtureAssertion(bStep,2))==HttpStatusCode.Unauthorized,
    "Old B passkey challenge cannot revive after mailbox deverification/reverification");
Check(await Stepup(hostB.Value,currentPassword,bPasskey.FactorId)==
      HttpStatusCode.Accepted,"New B step-up is allowed after renewed verification");
bStep=Last();

Check(await ProvePasskey(hostB.Value,bStep,
        fixtureSecrets.SignFixtureAssertion(bStep,2,crossOrigin:true))==
            HttpStatusCode.Unauthorized,
    "Cryptographically signed crossOrigin phishing attempt denied");
Check(await ProvePasskey(hostB.Value,bStep,fixtureSecrets.SignFixtureAssertion(bStep,2))==
      HttpStatusCode.NoContent,
    "Synthetic passkey ES256 challenge succeeds only with increasing persisted signCount");

string? backup=initial.BackupCodeForTrustedFixtureOnly;
if (backup is null) throw new InvalidOperationException("Fixture backup not delivered");
int qBefore=queue.Count;
Check(await Recover(hostA.Value,"wrong",initial.FactorId,backup,"PASSKEY")==
      HttpStatusCode.Accepted && queue.Count==qBefore,
    "Recovery code without fresh C05-04 Argon2id is NOT a bypass");
Check(await Recover(hostB.Value,currentPassword,initial.FactorId,backup,"PASSKEY")==
      HttpStatusCode.Accepted && queue.Count==qBefore,
    "Backup code of tenant A cannot replace factor of tenant B");
Check(await Recover(hostA.Value,currentPassword,initial.FactorId,backup,"PASSKEY")==
      HttpStatusCode.Accepted && queue.Count==qBefore+1,
    "Correct current password plus one-use HMAC code creates only PENDING replacement");
var replacement=Last();
Check((await ledger.ReadCounterAsync(tenantA,actor,initial.FactorId,CancellationToken.None))
        ?.State=="ACTIVE" &&
      (await ledger.ReadCounterAsync(tenantA,actor,replacement.FactorId,CancellationToken.None))
        ?.State=="PENDING",
    "Recovery DOES NOT disable old factor or activate new factor before proof");
Check(await Recover(hostA.Value,currentPassword,initial.FactorId,backup,"PASSKEY")==
      HttpStatusCode.Accepted && queue.Count==qBefore+1,
    "Consumed backup cannot create any second replacement");
Check(await Stepup(hostA.Value,currentPassword,initial.FactorId)==HttpStatusCode.Accepted &&
      queue.Count==qBefore+1,
    "Pending replacement blocks fresh step-up of the old active factor");
Check(await ProveTotp(hostA.Value,oldStep,fixtureSecrets.TotpCode(initial.FactorId))==
      HttpStatusCode.Unauthorized,
    "Recovery invalidates earlier step-up proof permanently");
Check(await ProvePasskey(hostA.Value,replacement,
        fixtureSecrets.SignFixtureAssertion(replacement,1,
            forgedOrigin:"https://evil.example.test"))==HttpStatusCode.Unauthorized &&
      (await ledger.ReadCounterAsync(tenantA,actor,replacement.FactorId,CancellationToken.None))
          ?.State=="PENDING",
    "An unverified replacement stays pending and cannot retire active MFA");
Check(await ProvePasskey(hostA.Value,replacement,
        fixtureSecrets.SignFixtureAssertion(replacement,1))==
      HttpStatusCode.NoContent,
    "New synthetic ES256 factor must prove before replacement is committed");
Check((await ledger.ReadCounterAsync(tenantA,actor,initial.FactorId,CancellationToken.None))
        ?.State=="REVOKED" &&
      (await ledger.ReadCounterAsync(tenantA,actor,replacement.FactorId,CancellationToken.None))
        ?.State=="ACTIVE" &&
      (await ledger.ReadCounterAsync(tenantB,actor,bPasskey.FactorId,CancellationToken.None))
        ?.State=="ACTIVE",
    "Atomic replacement revokes only A old factor, preserving B factor");
Check(!await sessionLedger.IsCurrentAsync(tenantA,
        sessionProtector.TryDigest(bearerA)!,CancellationToken.None) &&
      !await sessionLedger.IsCurrentAsync(tenantB,
        sessionProtector.TryDigest(bearerB)!,CancellationToken.None),
    "Recovery replacement revokes all old actor sessions in both tenants DURABLY");
Check(await CountAsync($"""
    SELECT COUNT(*) FROM c05_05_subjects
    WHERE actor_id=UNHEX('{actor.Value:N}') AND subject_revision=2
    """)==2, "Session-bound actor subject revisions increased in A/B");
Check(await CountAsync("SELECT COUNT(*) FROM c05_08_security_audit WHERE event_type='FACTOR_REPLACED'")==1,
    "Exactly one append-only factor replacement record, no backup in audit");
Check(await ProvePasskey(hostA.Value,replacement,
        fixtureSecrets.SignFixtureAssertion(replacement,2))==
      HttpStatusCode.Unauthorized &&
      await Stepup(hostA.Value,currentPassword,initial.FactorId)==
        HttpStatusCode.Accepted,
    "Replacement challenge replay denied and revoked factor not usable");
Check(await Stepup(hostA.Value,currentPassword,replacement.FactorId)==
      HttpStatusCode.Accepted,
    "Only newly ACTIVE factor may issue next operation-bound step-up");
var newStep=Last();
Check(await ProvePasskey(hostA.Value,newStep,
        fixtureSecrets.SignFixtureAssertion(newStep,2))==
      HttpStatusCode.NoContent,
    "New factor still requires its OWN separate step-up after recovery");
Check(await CountAsync("""
    SELECT COUNT(*) FROM c05_08_challenges
    WHERE state='CONSUMED' AND challenge_kind='STEPUP'
    """)==3,
    "Durable step-up proof count matches A TOTP, B passkey and A new passkey");

await RejectSql(async()=>
{
    await using var cmd=new MySqlCommand(
        "UPDATE c05_08_security_audit SET event_type='STEPUP_PROVED' WHERE event_id=1",setup);
    await cmd.ExecuteNonQueryAsync();
},"Audit trigger forbids UPDATE");
await RejectSql(async()=>
{
    await using var cmd=new MySqlCommand(
        "DELETE FROM c05_08_security_audit WHERE event_id=1",setup);
    await cmd.ExecuteNonQueryAsync();
},"Audit trigger forbids DELETE");
Check(await CountAsync("""
    SELECT COUNT(*) FROM information_schema.columns
     WHERE table_schema=DATABASE() AND table_name='c05_08_security_audit'
       AND (column_name LIKE '%secret%' OR column_name LIKE '%token%'
          OR column_name LIKE '%password%' OR column_name LIKE '%digest%'
          OR column_name LIKE '%email%' OR column_name LIKE '%code%')
    """)==0, "Append-only audit stores no password, code, token, digest or email");
Check(await CountAsync("""
    SELECT COUNT(*) FROM information_schema.columns
     WHERE table_schema=DATABASE() AND table_name='c05_08_factors'
       AND (column_name LIKE '%seed%' OR column_name LIKE '%private%'
         OR column_name LIKE '%secret%' OR column_name LIKE '%email%')
    """)==0, "Factor state schema stores no TOTP seed or private credential");
Check(reauth.Calls>=8,
    "Each enrollment, replacement and risk issuance rechecked Argon2id on C05-04");

// A valid proof issued before membership suspension remains invalid even
// if the operator restores membership; subject_revision is monotonic.
Check(await Stepup(hostA.Value,currentPassword,replacement.FactorId)==
      HttpStatusCode.Accepted,"Issue challenge before membership test");
var staleMember=Last();
await using(var suspend=new MySqlCommand($"""
    UPDATE c05_05_subjects SET membership_active=0
     WHERE tenant_id=UNHEX('{tenantA.Value:N}')
       AND actor_id=UNHEX('{actor.Value:N}')
    """,setup))
    Check(await suspend.ExecuteNonQueryAsync()==1,
        "Fixture suspends A membership, revision advances");
Check(await ProvePasskey(hostA.Value,staleMember,
        fixtureSecrets.SignFixtureAssertion(staleMember,3))==
      HttpStatusCode.Unauthorized,
    "Suspended tenant A membership denies pending MFA proof");
await using(var restore=new MySqlCommand($"""
    UPDATE c05_05_subjects SET membership_active=1
     WHERE tenant_id=UNHEX('{tenantA.Value:N}')
       AND actor_id=UNHEX('{actor.Value:N}')
    """,setup))
    Check(await restore.ExecuteNonQueryAsync()==1,
        "Fixture restores membership but not prior authority revision");
Check(await ProvePasskey(hostA.Value,staleMember,
        fixtureSecrets.SignFixtureAssertion(staleMember,3))==
      HttpStatusCode.Unauthorized,
    "Reactivated membership cannot resurrect old challenge");

// C05-06 password reset or credential rotation may not remove active MFA.
// Even if a cached fixture directory briefly reports old Argon2id proof,
// global credential revision mismatch must fail closed in MySQL.
await using(var revokeCredential=new MySqlCommand($"""
    UPDATE c05_06_credentials SET credential_active=0
     WHERE actor_id=UNHEX('{actor.Value:N}')
    """,setup))
    Check(await revokeCredential.ExecuteNonQueryAsync()==1,
        "Synthetic global credential revoked for reset boundary");
await using(var reactivateCredential=new MySqlCommand($"""
    UPDATE c05_06_credentials SET credential_active=1
     WHERE actor_id=UNHEX('{actor.Value:N}')
    """,setup))
    Check(await reactivateCredential.ExecuteNonQueryAsync()==1,
        "Synthetic global credential reactivated with NEW revision");
Check(await Stepup(hostA.Value,currentPassword,replacement.FactorId)==
      HttpStatusCode.Accepted &&
      (await ledger.ReadCounterAsync(tenantA,actor,replacement.FactorId,CancellationToken.None))
        ?.State=="ACTIVE",
    "Password reset does not bypass or delete MFA; stale identity revisions deny further step-up");

await app.StopAsync();
fixtureSecrets.Dispose();
Console.WriteLine($"PASS CRECISOL C05-08 REAL HTTP/.NET10/MySQL8.4: {checks} checks");
Console.WriteLine("PASS enrollment TOTP and trusted-fixture ES256, risk step-up, 16-way CAS, one-use backup replacement, host+tenant isolation and global session revocation");
Console.WriteLine("NO-GO production: no real WebAuthn enrollment/attestation, credential custody, user sessions, KMS, rate-limit, network APIs or deployment.");

sealed class FixtureImmediateReauthentication(
    Func<VerifiedTenantHostHint,PrepareContextualPasswordLogin>? factory=null)
    : ITrustedImmediatePasswordReauthentication
{
    private readonly Func<VerifiedTenantHostHint,PrepareContextualPasswordLogin>? resolver=factory;
    private int count;
    public int Calls=>Volatile.Read(ref count);
    public Task<ContextualLoginPreparation> VerifyFreshAsync(
        CanonicalLoginHandle handle,ReadOnlyMemory<char> password,
        VerifiedTenantHostHint host,CancellationToken ct)
    {
        Interlocked.Increment(ref count);
        return resolver is null?Task.FromResult(ContextualLoginPreparation.Denied):
            resolver(host).ExecuteAsync(handle,password,ct);
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
    string expected,TrustedLoginCredentialSnapshot credential)
    : ITrustedLoginCredentialDirectory
{
    public Task<IReadOnlyList<TrustedLoginCredentialSnapshot>?> FindExactAsync(
        CanonicalLoginHandle candidate,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        IReadOnlyList<TrustedLoginCredentialSnapshot> all=
            candidate.Value==expected?new[]{credential}:
                Array.Empty<TrustedLoginCredentialSnapshot>();
        return Task.FromResult<IReadOnlyList<TrustedLoginCredentialSnapshot>?>(all);
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
sealed class FixtureEntryReader(CanonicalTenantHost host):ITrustedLoginEntryReader
{
    public ValueTask<TrustedLoginEntry?> ReadServerVerifiedEntryAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return ValueTask.FromResult<TrustedLoginEntry?>(TrustedLoginEntry.ForTenantHost(host));
    }
}
sealed class FixtureLinkDirectory(
    TenantActorId actor,IReadOnlyList<AccountTenantLinkSnapshot> links)
    : ITrustedLoginTenantLinkDirectory
{
    public Task<IReadOnlyList<AccountTenantLinkSnapshot>?> FindAllByActorAsync(
        TenantActorId supplied,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<AccountTenantLinkSnapshot>?>(
            supplied==actor?links:Array.Empty<AccountTenantLinkSnapshot>());
    }
}
sealed class FixtureHostCatalog(CanonicalTenantHost a,CanonicalTenantHost b,
    VerifiedTenantHostBinding ba,VerifiedTenantHostBinding bb)
    : ITrustedVerifiedHostCatalog
{
    public Task<IReadOnlyList<VerifiedTenantHostBinding>?> FindExactBindingsAsync(
        CanonicalTenantHost host,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        IReadOnlyList<VerifiedTenantHostBinding> value=
            host==a?new[]{ba}:host==b?new[]{bb}:
                Array.Empty<VerifiedTenantHostBinding>();
        return Task.FromResult<IReadOnlyList<VerifiedTenantHostBinding>?>(value);
    }
}
sealed class FixtureTenantCatalog(TenantId a,TenantId b,
    TenantCatalogSnapshot ca,TenantCatalogSnapshot cb) : ITrustedTenantCatalogReader
{
    public Task<TenantCatalogSnapshot?> FindByIdAsync(TenantId tenant,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<TenantCatalogSnapshot?>(tenant==a?ca:tenant==b?cb:null);
    }
}

sealed class FixtureMfaSecrets : ITrustedDisposableMfaSecrets, IDisposable
{
    private sealed record Fixture(TenantId Tenant,TenantActorId Actor,
        DisposableMfaKind Kind,byte[]? Seed,
        byte[]? CredentialId,ECDsa? Signer,string Rp);
    private readonly ConcurrentDictionary<Guid,Fixture> factors=new();
    private readonly DisposableMfaChallengeProtector protector;
    private long offsetSeconds;
    public FixtureMfaSecrets(DisposableMfaChallengeProtector protector) =>
        this.protector=protector;
    public void MoveTrustedTimeForward(TimeSpan span) =>
        Interlocked.Add(ref offsetSeconds,(long)span.TotalSeconds);
    private DateTimeOffset TrustedNow()=>
        DateTimeOffset.UtcNow.AddSeconds(Interlocked.Read(ref offsetSeconds));

    public void RegisterPendingFixture(Guid id,TenantId tenant,
        TenantActorId actor,DisposableMfaKind kind)
    {
        byte[]? seed=kind==DisposableMfaKind.Totp?
            TotpSha256Verifier.GenerateSeed():null;
        var signer=kind==DisposableMfaKind.Passkey?
            ECDsa.Create(ECCurve.NamedCurves.nistP256):null;
        byte[]? credential=signer is null?null:RandomNumberGenerator.GetBytes(32);
        string rp=tenant.Value.ToString("D").StartsWith("aaaaaaaa",StringComparison.Ordinal)?
            "alpha.example.test":"beta.example.test";
        if(!factors.TryAdd(id,new Fixture(tenant,actor,kind,seed,credential,signer,rp)))
            throw new InvalidOperationException("Duplicate protected test factor.");
    }
    public void DiscardPendingFixture(Guid id)
    {
        if(factors.TryRemove(id,out var v))
        {
            if(v.Seed is not null) CryptographicOperations.ZeroMemory(v.Seed);
            v.Signer?.Dispose();
        }
    }
    public string TotpCode(Guid id)
    {
        if(!factors.TryGetValue(id,out var value) || value.Seed is null)
            throw new InvalidOperationException("Missing synthetic factor secret.");
        return TotpSha256Verifier.ComputeCode(value.Seed,
            TotpSha256Verifier.StepAt(TrustedNow()));
    }
    public bool TryVerifyTotp(Guid id,TenantId tenant,TenantActorId actor,
        string? candidate,long last,out long next)
    {
        next=-1;
        return factors.TryGetValue(id,out var v) &&
            v.Tenant==tenant && v.Actor==actor &&
            v.Seed is not null &&
            TotpSha256Verifier.TryVerify(v.Seed,candidate,TrustedNow(),
                last,out next);
    }
    public DisposableMfaAssertion SignFixtureAssertion(
        DisposableMfaChallenge handle,uint count,
        bool crossOrigin=false,string? forgedOrigin=null)
    {
        if(!factors.TryGetValue(handle.FactorId,out var v) ||
           v.Signer is null || v.CredentialId is null)
            throw new InvalidOperationException("Test signer missing.");
        byte[] challenge=protector.TryChallengeBytes(handle.TokenForTrustedFixtureOnly) ??
            throw new InvalidOperationException("Challenge not parseable.");
        try
        {
            byte[] client=JsonSerializer.SerializeToUtf8Bytes(new {
                type="webauthn.get",
                challenge=SyntheticWebAuthnEs256Verifier.EncodeChallenge(challenge),
                origin=forgedOrigin??"https://"+v.Rp,
                crossOrigin
            });
            byte[] authData=new byte[37];
            SHA256.HashData(Encoding.ASCII.GetBytes(v.Rp)).CopyTo(authData,0);
            authData[32]=0x05;
            BinaryPrimitives.WriteUInt32BigEndian(authData.AsSpan(33,4),count);
            byte[] clientHash=SHA256.HashData(client);
            byte[] signed=new byte[authData.Length+32];
            authData.CopyTo(signed,0);
            clientHash.CopyTo(signed,authData.Length);
            try
            {
                return new DisposableMfaAssertion((byte[])v.CredentialId.Clone(),
                    client,authData,v.Signer.SignData(signed,HashAlgorithmName.SHA256,
                        DSASignatureFormat.Rfc3279DerSequence));
            }
            finally
            {
                CryptographicOperations.ZeroMemory(clientHash);
                CryptographicOperations.ZeroMemory(signed);
            }
        }
        finally { CryptographicOperations.ZeroMemory(challenge); }
    }
    public bool TryVerifyFixturePasskey(Guid id,TenantId tenant,TenantActorId actor,
        ReadOnlySpan<byte> challenge,DisposableMfaAssertion assertion,
        long last,out long counter)
    {
        counter=-1;
        if(!factors.TryGetValue(id,out var v) ||
           v.Tenant!=tenant || v.Actor!=actor ||
           v.Signer is null || v.CredentialId is null ||
           last< -1 || last>uint.MaxValue)
            return false;
        bool good=SyntheticWebAuthnEs256Verifier.VerifyTrustedFixtureAssertion(
            v.Signer.ExportSubjectPublicKeyInfo(),v.CredentialId,
            assertion.CredentialId,v.Rp,"https://"+v.Rp,challenge,
            assertion.ClientDataJson,assertion.AuthenticatorData,
            assertion.Signature,(uint)Math.Max(0,last),out uint next);
        if(!good) return false;
        counter=next;
        return true;
    }
    public void Dispose()
    {
        foreach(Guid id in factors.Keys) DiscardPendingFixture(id);
    }
}
