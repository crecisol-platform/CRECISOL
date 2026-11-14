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

if(args.Length==0)
{
    Console.WriteLine("SKIP C05-10 real rate-limited HTTP/MySQL: --integration required");
    return;
}
if(args.Length!=1 || args[0]!="--integration")
    throw new InvalidOperationException("C05-10 tests accept --integration only.");
string? setupDsn=Environment.GetEnvironmentVariable("CRECISOL_C05_10_SETUP_DSN");
string? runnerDsn=Environment.GetEnvironmentVariable("CRECISOL_C05_10_RUNNER_DSN");
if(string.IsNullOrWhiteSpace(setupDsn)||string.IsNullOrWhiteSpace(runnerDsn))
    throw new InvalidOperationException("C05-10 disposable MySQL fixtures required.");
var setupConfig=new MySqlConnectionStringBuilder(setupDsn);
if(setupConfig.Server!="127.0.0.1"||
   setupConfig.Database!="c05_10_disposable"||
   setupConfig.AllowLoadLocalInfile)
    throw new InvalidOperationException("C05-10 may use only disposable loopback DB.");
var realLedger=new MySqlDisposablePreAuthRateLimitLedger(runnerDsn);
var ledger=new FixtureRateLimitDiagnosticLedger(realLedger);
int checks=0;
void Check(bool ok,string label)
{
    if(!ok)throw new InvalidOperationException("FAIL C05-10 "+label);
    Interlocked.Increment(ref checks);
}
void Deny<T>(Action action,string label) where T:Exception
{
    try{action();}
    catch(T){Check(true,label);return;}
    throw new InvalidOperationException("FAIL C05-10 denial absent: "+label);
}
async Task RejectSql(Func<Task> action,string label)
{
    try{await action();}
    catch(MySqlException){Check(true,label);return;}
    throw new InvalidOperationException("FAIL C05-10 SQL bypass: "+label);
}
var tenantA=TenantId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
var tenantB=TenantId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
var actor=TenantActorId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01"));
var linkA=AccountTenantLinkId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa12"));
var linkB=AccountTenantLinkId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbba12"));
var hostA=CanonicalTenantHost.Parse("alpha.example.test");
var hostB=CanonicalTenantHost.Parse("beta.example.test");
var changed=DateTimeOffset.UtcNow.AddDays(-2);
var expires=DateTimeOffset.UtcNow.AddHours(5);
const string login="rate-owner@example.test";
const string unknown="unknown-rate@example.test";
const string raceLogin="race-limiter@example.test";
string password=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
string wrongPassword=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
var hasher=new Argon2idPasswordHasher(PasswordHashingPolicy.Default,
    new FixtureCompromisedScreen());
string phc=hasher.CreateVerifier(password).ExportForTrustedCredentialStore();
string dummy=hasher.CreateVerifier(Convert.ToBase64String(
    RandomNumberGenerator.GetBytes(32))).ExportForTrustedCredentialStore();
byte[] secret=RandomNumberGenerator.GetBytes(32);
using var protector=new SyntheticPreAuthDigestProtector(secret);
CryptographicOperations.ZeroMemory(secret);
Check(realLedger.ToString()=="MySqlDisposablePreAuthRateLimitLedger[redacted]"&&
      protector.ToString()=="SyntheticPreAuthDigestProtector[redacted]"&&
      PreAuthKeyedDigest.FromTrustedProtector(RandomNumberGenerator.GetBytes(32))
      .ToString()=="PreAuthKeyedDigest[redacted]",
      "Rate-limit services, keyed digest and private adapter redact all diagnostics");
Deny<InvalidOperationException>(()=>new SyntheticPreAuthDigestProtector(new byte[32]),
    "All-zero HMAC key refused");
Deny<InvalidOperationException>(()=>new MySqlDisposablePreAuthRateLimitLedger(
    "Server=example.com;Database=c05_10_disposable;Uid=x"),
    "Offsite limiter DB is forbidden");
Deny<InvalidOperationException>(()=>new MySqlDisposablePreAuthRateLimitLedger(
    "Server=127.0.0.1;Database=CRECISOL_PRODUCTION;Uid=x"),
    "Production limiter DB is forbidden");
Deny<InvalidOperationException>(()=>PreAuthKeyedDigest.FromTrustedProtector(new byte[32]),
    "Zero digest refused");
Deny<InvalidOperationException>(()=>protector.FingerprintSocketPeer(
    tenantA,PreAuthRateLimitPurpose.PasswordLogin,IPAddress.None),
    "Unspecified/broadcast socket origin denied");

var ownerLogin=CanonicalLoginHandle.Parse(login);
var unknownLogin=CanonicalLoginHandle.Parse(unknown);
var sourceIp=IPAddress.Loopback;
bool Same(PreAuthKeyedDigest a,PreAuthKeyedDigest b)
{
    byte[] x=a.CopyForTrustedLedger();
    byte[] y=b.CopyForTrustedLedger();
    try{return CryptographicOperations.FixedTimeEquals(x,y);}
    finally{CryptographicOperations.ZeroMemory(x);CryptographicOperations.ZeroMemory(y);}
}
var subjectA=protector.FingerprintSubject(
    tenantA,PreAuthRateLimitPurpose.PasswordLogin,ownerLogin);
var subjectB=protector.FingerprintSubject(
    tenantB,PreAuthRateLimitPurpose.PasswordLogin,ownerLogin);
var sourceA=protector.FingerprintSocketPeer(
    tenantA,PreAuthRateLimitPurpose.PasswordLogin,sourceIp);
var sourceB=protector.FingerprintSocketPeer(
    tenantB,PreAuthRateLimitPurpose.PasswordLogin,sourceIp);
Check(!Same(subjectA,subjectB) && !Same(sourceA,sourceB) &&
      !Same(subjectA,sourceA) &&
      !Same(subjectA,protector.FingerprintSubject(
          tenantA,PreAuthRateLimitPurpose.PasswordRecovery,ownerLogin)) &&
      !Same(sourceA,protector.FingerprintSocketPeer(
          tenantA,PreAuthRateLimitPurpose.MfaChallenge,sourceIp)),
    "HMAC scope separates tenants, source/subject domains and protected actions");
Check(Same(sourceA,protector.FingerprintSocketPeer(
       tenantA,PreAuthRateLimitPurpose.PasswordLogin,
       IPAddress.Parse("::ffff:127.0.0.1"))),
    "IPv4-mapped IPv6 cannot multiply one source budget");
Check(Same(protector.FingerprintSocketPeer(tenantA,
       PreAuthRateLimitPurpose.PasswordLogin,IPAddress.Parse("2001:db8:1:2::1")),
       protector.FingerprintSocketPeer(tenantA,
       PreAuthRateLimitPurpose.PasswordLogin,IPAddress.Parse("2001:db8:1:2::abcd"))),
    "IPv6 /64 suffix changes cannot multiply the same origin budget");
Check(!Same(protector.FingerprintSocketPeer(tenantA,
       PreAuthRateLimitPurpose.PasswordLogin,IPAddress.Parse("2001:db8:1:2::1")),
       protector.FingerprintSocketPeer(tenantA,
       PreAuthRateLimitPurpose.PasswordLogin,IPAddress.Parse("2001:db8:1:3::1"))),
    "IPv6 distinct /64 source fingerprints stay distinct");

await using var setup=new MySqlConnection(setupConfig.ConnectionString);
await setup.OpenAsync();
async Task<long> ScalarAsync(string statement)
{
    await using var cmd=new MySqlCommand(statement,setup);
    return Convert.ToInt64(await cmd.ExecuteScalarAsync());
}
async Task SeedPolicyAsync(TenantId tenant,string purpose,
    int subjectLimit,int sourceLimit,bool enabled=true)
{
    await using var cmd=new MySqlCommand("""
      INSERT INTO c05_10_policies(tenant_id,purpose,subject_limit,source_limit,
        window_seconds,cooldown_seconds,enabled,host_revision,revision,changed_utc)
      VALUES (@t,@purpose,@subject,@source,900,900,@enabled,8,1,UTC_TIMESTAMP(6))
      """,setup);
    cmd.Parameters.Add("@t",MySqlDbType.Binary).Value=tenant.Value.ToByteArray(bigEndian:true);
    cmd.Parameters.Add("@purpose",MySqlDbType.VarChar).Value=purpose;
    cmd.Parameters.Add("@subject",MySqlDbType.UInt16).Value=(ushort)subjectLimit;
    cmd.Parameters.Add("@source",MySqlDbType.UInt16).Value=(ushort)sourceLimit;
    cmd.Parameters.Add("@enabled",MySqlDbType.Byte).Value=enabled?1:0;
    Check(await cmd.ExecuteNonQueryAsync()==1,"Versioned protected policy fixture seeded");
}
await SeedPolicyAsync(tenantA,"LOGIN",5,12);
await SeedPolicyAsync(tenantB,"LOGIN",7,14);
await SeedPolicyAsync(tenantA,"RECOVERY",3,6);
await SeedPolicyAsync(tenantA,"MFA",2,4);
Check(await ScalarAsync("SELECT COUNT(*) FROM c05_10_policies")==4,
      "4 tenant-scoped action policies configured only by fixture owner");
await RejectSql(async()=>
{
    await using var conn=new MySqlConnection(runnerDsn);
    await conn.OpenAsync();
    await using var cmd=new MySqlCommand(
        "SELECT source_digest,subject_digest FROM c05_10_access_traces",conn);
    _=await cmd.ExecuteScalarAsync();
},"Runner with EXECUTE only cannot inspect private HMAC access traces");
await RejectSql(async()=>
{
    await using var conn=new MySqlConnection(runnerDsn);
    await conn.OpenAsync();
    await using var cmd=new MySqlCommand(
        "UPDATE c05_10_policies SET subject_limit=999",conn);
    _=await cmd.ExecuteNonQueryAsync();
},"Runner with EXECUTE only cannot disable/configure enforcement");

var account=IdentityAccountSnapshot.RestoreFromTrustedIdentityDirectory(
    actor,IdentityAccountState.Active,7,changed);
var credential=AccountCredentialMetadata.RestoreFromTrustedCredentialDirectory(
    AccountCredentialId.From(Guid.NewGuid()),actor,
    AccountCredentialKind.PasswordVerifier,AccountCredentialState.Active,3,changed);
var trusted=TrustedLoginCredentialSnapshot.RestoreFromTrustedDirectory(
    account,credential,TrustedLoginRealm.TenantAccount,phc);
var links=new[]{
    AccountTenantLinkSnapshot.RestoreFromTrustedMembershipDirectory(
        linkA,actor,tenantA,AccountTenantProfileKind.Owner,
        TenantMembershipState.Active,5,changed,changed,expires),
    AccountTenantLinkSnapshot.RestoreFromTrustedMembershipDirectory(
        linkB,actor,tenantB,AccountTenantProfileKind.Employee,
        TenantMembershipState.Active,5,changed,changed,expires)
};
var catalogA=TenantCatalogSnapshot.RestoreFromTrustedCatalog(
    tenantA,"tenant-a-internal","A synthetic",TenantLifecycleState.Active,
    TenantAddressMode.Subdomain,9,changed);
var catalogB=TenantCatalogSnapshot.RestoreFromTrustedCatalog(
    tenantB,"tenant-b-internal","B synthetic",TenantLifecycleState.Active,
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
PrepareContextualPasswordLogin Login(CanonicalTenantHost host)=>
    new(new FixtureCredentialDirectory(login,trusted),
        new FixtureDummyVerifier(dummy),
        new Argon2idLoginCandidateVerifier(hasher),
        new FixtureEntryReader(host),
        new FixtureLinkDirectory(actor,links),resolver,
        new FixtureTenantCatalog(tenantA,tenantB,catalogA,catalogB),clock);
var hintA=await resolver.ResolveAsync(hostA,CancellationToken.None);
var hintB=await resolver.ResolveAsync(hostB,CancellationToken.None);
Check(hintA.TenantId==tenantA && hintB.TenantId==tenantB &&
      hintA.Revision==8 && hintB.Revision==8,
      "Two verified tenant hosts and revisions from C04 catalogue");
Check((await Login(hostA).ExecuteAsync(
      ownerLogin,password.AsMemory(),CancellationToken.None)).Status==
          ContextualLoginPreparationStatus.Ready &&
      (await Login(hostA).ExecuteAsync(
      ownerLogin,wrongPassword.AsMemory(),CancellationToken.None)).Status==
          ContextualLoginPreparationStatus.Denied &&
      (await Login(hostA).ExecuteAsync(
      unknownLogin,password.AsMemory(),CancellationToken.None)).Status==
          ContextualLoginPreparationStatus.Denied,
      "C05-04 Argon2id and real dummy PHC distinguish correct/incorrect internally");

var builder=WebApplication.CreateBuilder(new WebApplicationOptions{
    Args=Array.Empty<string>(),EnvironmentName="Production"
});
builder.Logging.ClearProviders();
builder.WebHost.UseUrls("http://127.0.0.1:0");
await using var app=builder.Build();
app.Use(async (context,next)=>
{
    context.Response.Headers.CacheControl="no-store";
    context.Response.Headers.Pragma="no-cache";
    context.Response.Headers["Referrer-Policy"]="no-referrer";
    context.Response.Headers["X-Content-Type-Options"]="nosniff";
    await next();
});
using var argon2Slots=new SemaphoreSlim(2,2);
app.MapPost("/__c05_10_test__/password-login",async(HttpContext context)=>
{
    if(context.Request.ContentLength is null or <=0 or >2048 ||
       !(context.Request.ContentType?.StartsWith("application/json",
          StringComparison.OrdinalIgnoreCase)??false) ||
       context.Request.QueryString.HasValue)
        return Results.BadRequest();
    JsonDocument? data;
    try
    {
        data=await JsonDocument.ParseAsync(context.Request.Body,
            new JsonDocumentOptions{MaxDepth=4},context.RequestAborted);
    }
    catch(JsonException){return Results.BadRequest();}
    using(data)
    {
        var root=data.RootElement;
        if(root.ValueKind!=JsonValueKind.Object ||
           root.EnumerateObject().Count()!=2 ||
           !root.TryGetProperty("email",out var rawEmail) ||
           !root.TryGetProperty("password",out var rawPassword) ||
           rawEmail.ValueKind!=JsonValueKind.String ||
           rawPassword.ValueKind!=JsonValueKind.String)
            return Results.BadRequest();
        CanonicalLoginHandle handle;
        try{handle=CanonicalLoginHandle.Parse(rawEmail.GetString());}
        catch(InvalidOperationException){return Results.StatusCode(202);}
        string candidate=rawPassword.GetString()??"";
        var verifier=new FixturePasswordVerifier(async(h,p,ct)=>
        {
            string raw=context.Request.Host.Host;
            if(raw!=hostA.Value && raw!=hostB.Value)
                throw new UnauthorizedAccessException("Unverified direct host.");
            await argon2Slots.WaitAsync(ct);
            try
            {
                return await Login(CanonicalTenantHost.Parse(raw))
                    .ExecuteAsync(h,p,ct);
            }
            finally {argon2Slots.Release();}
        });
        var service=new ThrottledContextualPasswordAttempt(
            new FixturePreAuthHostReader(context,resolver,hostA,hostB),
            new FixturePreAuthNetworkReader(context),
            protector,ledger,verifier);
        var status=await service.AttemptAsync(handle,candidate.AsMemory(),
            context.RequestAborted);
        return Results.StatusCode(status switch
        {
            PreAuthPublicDisposition.Accepted=>202,
            PreAuthPublicDisposition.RateLimited=>429,
            _=>503
        });
    }
});
await app.StartAsync();
var addresses=app.Services.GetRequiredService<IServer>().Features
    .Get<IServerAddressesFeature>()?.Addresses;
string? endpoint=addresses?.Single();
if(endpoint is null ||
   !endpoint.StartsWith("http://127.0.0.1:",StringComparison.Ordinal))
    throw new InvalidOperationException("C05-10 HTTP test MUST only bind loopback.");
using var client=new HttpClient(new SocketsHttpHandler{
    UseProxy=false,AllowAutoRedirect=false
}){BaseAddress=new Uri(endpoint),Timeout=TimeSpan.FromSeconds(180)};

async Task<HttpStatusCode> PostAsync(
    string directHost,string email,string pass,
    string? fakeXForwardedFor=null,string? fakeTenant=null,
    string? fakeAuthorization=null)
{
    using var request=new HttpRequestMessage(HttpMethod.Post,
        "/__c05_10_test__/password-login");
    request.Headers.Host=directHost;
    if(fakeXForwardedFor is not null)
        request.Headers.TryAddWithoutValidation("X-Forwarded-For",fakeXForwardedFor);
    if(fakeTenant is not null)
        request.Headers.TryAddWithoutValidation("X-Tenant-Id",fakeTenant);
    if(fakeAuthorization is not null)
        request.Headers.TryAddWithoutValidation("Authorization",fakeAuthorization);
    request.Content=new StringContent(
        JsonSerializer.Serialize(new{email,password=pass}),
        Encoding.UTF8,"application/json");
    using var response=await client.SendAsync(request);
    string payload=await response.Content.ReadAsStringAsync();
    Check(response.Headers.CacheControl?.NoStore==true &&
          !response.Headers.Contains("Set-Cookie") &&
          !response.Headers.Contains("Retry-After") &&
          payload.Length==0,
        "HTTP neutral empty body, no secret, session, cache, retry enumeration or cookie");
    return response.StatusCode;
}


// True concurrent HTTP admission from a common socket peer and unknown
// mailbox. SQL SOURCE then SUBJECT CAS must admit exactly five, block eleven.
var bruteRace=await Task.WhenAll(Enumerable.Range(0,16).Select(_=>
    PostAsync(hostA.Value,raceLogin,wrongPassword)));
Console.WriteLine("C05-10 SYNTHETIC race HTTP aggregate only: accepted="+
    bruteRace.Count(x=>x==HttpStatusCode.Accepted)+
    " limited="+bruteRace.Count(x=>x==(HttpStatusCode)429)+
    " unavailable="+bruteRace.Count(x=>x==HttpStatusCode.ServiceUnavailable)+
    " other="+bruteRace.Count(x=>x!=HttpStatusCode.Accepted &&
                              x!=(HttpStatusCode)429 &&
                              x!=HttpStatusCode.ServiceUnavailable)+
    " sql_fault_categories="+ledger.SafeAggregateDiagnostics());
Check(bruteRace.Count(x=>x==HttpStatusCode.Accepted)==5 &&
      bruteRace.Count(x=>x==(HttpStatusCode)429)==11,
      "16 concurrent HTTP brute force admissions => exactly 5 accepted and 11 blocked");
Check(await ScalarAsync("""
    SELECT COUNT(*) FROM c05_10_access_outcomes
      WHERE tenant_id=UNHEX('aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa')
        AND purpose='LOGIN' AND outcome='REJECTED'
    """)==5,
    "All five admitted synthetic unknown attempts did actual C05-04 dummy Argon2id");
await ForceWindowExpiryAsync(tenantA,"LOGIN");

// Real ASP.NET10 HTTP / MySQL8.4 brute force. These endpoints run only
// in this executable; Crecisol.Api/Program.cs remains PUBLIC BRANDING ONLY.
Check((await PostAsync("evil.example.test",login,wrongPassword))==
      HttpStatusCode.ServiceUnavailable,
      "Unverified host denied before SQL/PBKDF/Argon2id; no tenant from query");
Check((await PostAsync(hostA.Value,login,wrongPassword,
      fakeXForwardedFor:"10.9.8.7",fakeTenant:tenantB.Value.ToString()))==
      HttpStatusCode.Accepted,
      "First wrong password consumes real A source/subject SQL budget; spoofed headers ignored");
Check((await PostAsync(hostA.Value,unknown,wrongPassword))==
      HttpStatusCode.Accepted,
      "Unknown mailbox has same neutral 202 as existing account with wrong password");
Check((await PostAsync(hostA.Value,login,password))==
      HttpStatusCode.Accepted,
      "Correct real C05-04 Argon2id credential has identical 202 without session or token");
Check(await ScalarAsync("""
    SELECT COUNT(*) FROM c05_10_access_outcomes
    WHERE purpose='LOGIN' AND outcome='VERIFIED'
    """)==1,"Valid authenticated internal proof recorded ONLY as keyed audit, never exposed");
Check(await ScalarAsync("""
    SELECT COUNT(*) FROM c05_10_access_outcomes
    WHERE purpose='LOGIN' AND outcome='REJECTED'
    """)==7,"Five concurrent unknown plus invalid and unknown later remain private rejected outcomes");
Check((await PostAsync(hostB.Value,login,password))==
      HttpStatusCode.Accepted,
      "Same global actor independently uses tenant B verified host and rate policy");
Check((await PostAsync(hostA.Value,login,wrongPassword))==
      HttpStatusCode.Accepted,
      "Different retry of A email remains within budget before threshold");

// A now has three owner-email attempts and one unknown; two more owner
// attempts admitted, sixth owner attempt denied; B independent.
Check((await PostAsync(hostA.Value,login,wrongPassword))==
      HttpStatusCode.Accepted &&
      (await PostAsync(hostA.Value,login,wrongPassword))==
      HttpStatusCode.Accepted,
      "Per-subject five-attempt A budget reached with no leaks");
Check((await PostAsync(hostA.Value,login,password,
      fakeXForwardedFor:"192.0.2.123",
      fakeTenant:tenantB.Value.ToString(),
      fakeAuthorization:"Bearer totally-untrusted-secret"))==
      (HttpStatusCode)429,
      "Sixth attempt throttled despite correct password and forged source/tenant/bearer headers");
Check((await PostAsync(hostB.Value,login,password))==
      HttpStatusCode.Accepted,
      "A locked subject cannot deny B independently verified actor/tenant login");
Check(await ScalarAsync("""
    SELECT COUNT(*) FROM c05_10_access_traces
    WHERE purpose='LOGIN' AND disposition='LIMITED'
    """)>=1,"First per-subject lockout leaves append-only LIMITED trace");
Check(await ScalarAsync("""
    SELECT COUNT(*) FROM c05_10_access_outcomes
    WHERE outcome='VERIFIED'
    """)==3,"Only genuinely verified C05-04 password proofs receive private VERIFIED outcomes");

// Further same subject packets during cooldown DO NOT create fresh trace
// rows; exposure and disk amplification are bounded by source/subject.
long before=await ScalarAsync("""
    SELECT COUNT(*) FROM c05_10_access_traces
    WHERE purpose='LOGIN' AND disposition='LIMITED'
    """);
for(int i=0;i<5;i++)
    Check((await PostAsync(hostA.Value,login,wrongPassword))==
          (HttpStatusCode)429,
          "Active cooldown rejects packet without password verifier");
long after=await ScalarAsync("""
    SELECT COUNT(*) FROM c05_10_access_traces
    WHERE purpose='LOGIN' AND disposition='LIMITED'
    """);
Check(after>=before && after-before<=1,
      "Repeated throttled packets do not create one audit row per request");

// Source cap: after previous attempts from origin A, fresh names use the
// remaining source budget; rotating email cannot evade an IP-based cap.
bool sourceBlocked=false;
int freshAdmitted=0;
for(int i=0;i<12;i++)
{
    var result=await PostAsync(hostA.Value,
        $"source-rotation-{i}@example.test",wrongPassword);
    if(result==(HttpStatusCode)429){sourceBlocked=true;break;}
    Check(result==HttpStatusCode.Accepted,"Rotated unknown login neutral admission");
    freshAdmitted++;
}
Check(sourceBlocked && freshAdmitted<=12,
      "Rotating unlimited unknown mailboxes cannot exceed source budget");
Check((await PostAsync(hostA.Value,"spoof-another@example.test",wrongPassword,
      fakeXForwardedFor:"198.51.100.42",fakeTenant:tenantB.Value.ToString()))==
      (HttpStatusCode)429,
      "Source cooldown resistant to X-Forwarded-For/X-Tenant-Id spoof");
Check((await PostAsync(hostB.Value,unknown,wrongPassword))==
      HttpStatusCode.Accepted,
      "B unknown subject still available after A source cooldown");
Check(await ScalarAsync("""
    SELECT COUNT(*) FROM c05_10_access_traces
    WHERE purpose='LOGIN' AND disposition='ADMITTED'
    """)<=20,
      "Source cap avoids unbounded per-request audit/admission amplification");

// Direct SQL uses freshly issued server scope, never a browser ID.
var recoverySource=protector.FingerprintSocketPeer(
    tenantA,PreAuthRateLimitPurpose.PasswordRecovery,sourceIp);
var recoverySubject=protector.FingerprintSubject(
    tenantA,PreAuthRateLimitPurpose.PasswordRecovery,ownerLogin);
var mfaSource=protector.FingerprintSocketPeer(
    tenantA,PreAuthRateLimitPurpose.MfaChallenge,sourceIp);
var mfaSubject=protector.FingerprintSubject(
    tenantA,PreAuthRateLimitPurpose.MfaChallenge,ownerLogin);
Guid firstRecovery=Guid.NewGuid();
Check(await ledger.AdmitAsync(hintA,PreAuthRateLimitPurpose.PasswordRecovery,
      firstRecovery,recoverySource,recoverySubject,CancellationToken.None)==
      PreAuthAdmissionStatus.Admitted,
      "Recovery has separate trusted-purpose budget, not bypassing login");
var wrongSubject=protector.FingerprintSubject(
    tenantA,PreAuthRateLimitPurpose.PasswordRecovery,unknownLogin);
Check(!await ledger.CompleteAsync(hintA,PreAuthRateLimitPurpose.PasswordRecovery,
      firstRecovery,recoverySource,wrongSubject,PreAuthPrivateOutcome.Rejected,
      CancellationToken.None),
      "Cannot finish somebody else's attempted identity even if same tenant");
Check(!await ledger.CompleteAsync(hintB,PreAuthRateLimitPurpose.PasswordRecovery,
      firstRecovery,recoverySource,recoverySubject,PreAuthPrivateOutcome.Rejected,
      CancellationToken.None),
      "Cross-tenant completion cannot mark A trace as B");
Check(!await ledger.CompleteAsync(hintA,PreAuthRateLimitPurpose.MfaChallenge,
      firstRecovery,recoverySource,recoverySubject,PreAuthPrivateOutcome.Rejected,
      CancellationToken.None),
      "Wrong-purpose completion cannot reuse recovery trace");

// Concurrent trace completion is an atomic one-shot, just like sessions.
// Test private rejection only; recovery/MFA API and delivery NOT integrated.
bool[] finish=await Task.WhenAll(Enumerable.Range(0,16).Select(_=>
    ledger.CompleteAsync(hintA,PreAuthRateLimitPurpose.PasswordRecovery,
        firstRecovery,recoverySource,recoverySubject,
        PreAuthPrivateOutcome.Rejected,CancellationToken.None)));
Check(finish.Count(x=>x)==1 && finish.Count(x=>!x)==15,
      "16 concurrent attempts to finish one trace: only one immutable outcome");
Check(!await ledger.CompleteAsync(hintA,PreAuthRateLimitPurpose.PasswordRecovery,
      firstRecovery,recoverySource,recoverySubject,
      PreAuthPrivateOutcome.Verified,CancellationToken.None),
      "Replay cannot upgrade previously denied attempt to VERIFIED");
Check(await ledger.AdmitAsync(hintB,PreAuthRateLimitPurpose.PasswordRecovery,
      Guid.NewGuid(),recoverySource,recoverySubject,CancellationToken.None)==
      PreAuthAdmissionStatus.Denied,
      "No B recovery policy means fail-closed rather than unlimited");

Guid mfaAttempt=Guid.NewGuid();
Check(await ledger.AdmitAsync(hintA,PreAuthRateLimitPurpose.MfaChallenge,
      mfaAttempt,mfaSource,mfaSubject,CancellationToken.None)==
      PreAuthAdmissionStatus.Admitted,
      "MFA policy/budget is isolated at data-contract level only; no MFA proof issued");
Check(await ledger.AdmitAsync(hintA,PreAuthRateLimitPurpose.MfaChallenge,
      Guid.NewGuid(),mfaSource,mfaSubject,CancellationToken.None)==
      PreAuthAdmissionStatus.Admitted,
      "Second synthetic MFA budget admitted");
Guid limitedMfa=Guid.NewGuid();
Check(await ledger.AdmitAsync(hintA,PreAuthRateLimitPurpose.MfaChallenge,
      limitedMfa,mfaSource,mfaSubject,CancellationToken.None)==
      PreAuthAdmissionStatus.Limited &&
      !await ledger.CompleteAsync(hintA,PreAuthRateLimitPurpose.MfaChallenge,
      limitedMfa,mfaSource,mfaSubject,PreAuthPrivateOutcome.Verified,
      CancellationToken.None),
      "MFA lockout not finishable or converted into an authorized factor");
Check(await ledger.AdmitAsync(hintB,PreAuthRateLimitPurpose.MfaChallenge,
      Guid.NewGuid(),mfaSource,mfaSubject,CancellationToken.None)==
      PreAuthAdmissionStatus.Denied,
      "Absent B MFA policy denies by default");

// Time is MySQL, not browser-controlled: move fixtures into completed
// windows and past locks to show cooldown reset without waiting 15 min.
async Task ForceWindowExpiryAsync(TenantId tenant,string purpose)
{
    await using var cmd=new MySqlCommand("""
      UPDATE c05_10_buckets
         SET attempts=99,
             window_started_utc=DATE_SUB(UTC_TIMESTAMP(6),INTERVAL 4000 SECOND),
             blocked_until_utc=DATE_SUB(UTC_TIMESTAMP(6),INTERVAL 1 SECOND)
       WHERE tenant_id=@t AND purpose=@purpose
      """,setup);
    cmd.Parameters.Add("@t",MySqlDbType.Binary).Value=
        tenant.Value.ToByteArray(bigEndian:true);
    cmd.Parameters.Add("@purpose",MySqlDbType.VarChar).Value=purpose;
    Check(await cmd.ExecuteNonQueryAsync()>0,
        "Protected fixture can simulate expiry of both admission dimensions");
}
await ForceWindowExpiryAsync(tenantA,"LOGIN");
Check((await PostAsync(hostA.Value,login,password))==
      HttpStatusCode.Accepted,
      "DB-enforced expiry permits fresh login after old cooldown only");
Check(await ScalarAsync("""
    SELECT COUNT(*) FROM c05_10_access_outcomes
    WHERE purpose='LOGIN' AND outcome='VERIFIED'
    """)==4,"Expired window still executes real Argon2id and records private result");

string aHex=tenantA.Value.ToString("N");
string bHex=tenantB.Value.ToString("N");
await using(var update=new MySqlCommand($"""
    UPDATE c05_10_policies
       SET subject_limit=2,source_limit=4,revision=revision+1,
           changed_utc=UTC_TIMESTAMP(6)
     WHERE tenant_id=UNHEX('{aHex}') AND purpose='LOGIN'
    """,setup))
    Check(await update.ExecuteNonQueryAsync()==1,
          "Fixture admin can update bounded per-tenant versioned policy");
await ForceWindowExpiryAsync(tenantA,"LOGIN");
Check((await PostAsync(hostA.Value,login,wrongPassword))==
      HttpStatusCode.Accepted &&
      (await PostAsync(hostA.Value,login,wrongPassword))==
      HttpStatusCode.Accepted &&
      (await PostAsync(hostA.Value,login,password))==
      (HttpStatusCode)429,
      "Updated policy 2/4 enforced immediately without browser selectors");
Check((await PostAsync(hostB.Value,login,password))==
      HttpStatusCode.Accepted,
      "A policy updates never change B budgets or private traces");

// Host catalogue revision mismatch fails closed in SQL even if the caller
// retained an old verified-host hint. Changing policy is fixture-only.
await using(var change=new MySqlCommand($"""
    UPDATE c05_10_policies SET host_revision=host_revision+1,
    revision=revision+1 WHERE tenant_id=UNHEX('{aHex}') AND purpose='LOGIN'
    """,setup))
    Check(await change.ExecuteNonQueryAsync()==1,
          "Fixture simulates host catalogue rebind without upgrading hint");
Check((await PostAsync(hostA.Value,login,password))==
      HttpStatusCode.ServiceUnavailable,
      "Stale host revision cannot re-use old limiter policy");
await using(var restore=new MySqlCommand($"""
    UPDATE c05_10_policies SET host_revision=8,revision=revision+1
    WHERE tenant_id=UNHEX('{aHex}') AND purpose='LOGIN'
    """,setup))
    Check(await restore.ExecuteNonQueryAsync()==1,
          "Fixture restores valid host policy revision");
await using(var disable=new MySqlCommand($"""
    UPDATE c05_10_policies SET enabled=0,revision=revision+1
    WHERE tenant_id=UNHEX('{aHex}') AND purpose='LOGIN'
    """,setup))
    Check(await disable.ExecuteNonQueryAsync()==1,
          "Fixture disables policy without authorizing bypass");
Check((await PostAsync(hostA.Value,login,password))==
      HttpStatusCode.ServiceUnavailable,
      "Disabled or missing policy denies BEFORE password verifier");
await using(var enable=new MySqlCommand($"""
    UPDATE c05_10_policies SET enabled=1,revision=revision+1
    WHERE tenant_id=UNHEX('{aHex}') AND purpose='LOGIN'
    """,setup))
    Check(await enable.ExecuteNonQueryAsync()==1,"Fixture restores scoped policy");

// Missing trusted socket, host, and SQL must never call Argon2id.
int fakeVerifications=0;
var countingVerifier=new FixturePasswordVerifier((_,_,_)=>
{
    Interlocked.Increment(ref fakeVerifications);
    return Task.FromResult(ContextualLoginPreparation.Denied);
});
var noSource=new ThrottledContextualPasswordAttempt(
    new FixtureHostHintReader(hintA),new FixtureNetworkReader(null),
    protector,ledger,countingVerifier);
var noHost=new ThrottledContextualPasswordAttempt(
    new FixtureHostHintReader(null),new FixtureNetworkReader(sourceIp),
    protector,ledger,countingVerifier);
var noDatabase=new ThrottledContextualPasswordAttempt(
    new FixtureHostHintReader(hintA),new FixtureNetworkReader(sourceIp),
    protector,new FixtureUnavailableLedger(),countingVerifier);
Check(await noSource.AttemptAsync(ownerLogin,password.AsMemory(),
          CancellationToken.None)==PreAuthPublicDisposition.Unavailable &&
      await noHost.AttemptAsync(ownerLogin,password.AsMemory(),
          CancellationToken.None)==PreAuthPublicDisposition.Unavailable &&
      await noDatabase.AttemptAsync(ownerLogin,password.AsMemory(),
          CancellationToken.None)==PreAuthPublicDisposition.Unavailable &&
      fakeVerifications==0,
      "Missing real network/verified host/MySQL is fail-closed; zero Argon2id calls");

// Audits must never expose raw PII, origin, HTTP, token or password columns.
Check(await ScalarAsync("""
    SELECT COUNT(*) FROM information_schema.columns
    WHERE table_schema=DATABASE()
      AND table_name IN ('c05_10_access_traces','c05_10_access_outcomes')
      AND (column_name REGEXP 'ip|email|password|token|cookie|bearer|secret|header|user_agent|body|phc|url'
       OR data_type IN ('blob','mediumblob','longblob','tinyblob','text','mediumtext','longtext'))
    """)==0,
      "Audit tables have no raw IP/email/password/token/header/body/URL fields");
Check(await ScalarAsync($"""
    SELECT COUNT(*) FROM c05_10_access_traces
    WHERE tenant_id=UNHEX('{aHex}') AND
     (LOCATE(HEX(CONVERT('{login}' USING utf8mb4)),HEX(CONCAT(
       attempt_id,source_digest,subject_digest)))>0 OR
      LOCATE(HEX(CONVERT('127.0.0.1' USING utf8mb4)),HEX(CONCAT(
       attempt_id,source_digest,subject_digest)))>0)
    """)==0,
      "No raw mailbox or 127.0.0.1 socket string within persisted trace digests");
Check(await ScalarAsync($"""
    SELECT COUNT(*) FROM c05_10_access_traces
    WHERE tenant_id=UNHEX('{aHex}') AND
          purpose='LOGIN' AND policy_revision>=2
    """)>=1,
      "Traces preserve trusted policy revision, not browser-supplied limits");
Check(await ScalarAsync($"""
    SELECT COUNT(*) FROM c05_10_access_traces
    WHERE tenant_id=UNHEX('{bHex}') AND purpose='LOGIN'
    """)>=1,
      "B traces are tenant-tagged and never pooled under tenant A");

// Audit tables are immutable even to privileged fixture user, while
// the least-privileged runtime cannot directly SELECT or DELETE anything.
await RejectSql(async()=>
{
    await using var command=new MySqlCommand(
        "UPDATE c05_10_access_traces SET disposition='ADMITTED'",setup);
    await command.ExecuteNonQueryAsync();
},"Append-only access trace cannot be UPDATEd by SQL privileges");
await RejectSql(async()=>
{
    await using var command=new MySqlCommand(
        "DELETE FROM c05_10_access_traces",setup);
    await command.ExecuteNonQueryAsync();
},"Append-only access trace cannot be DELETEd by SQL privileges");
await RejectSql(async()=>
{
    await using var command=new MySqlCommand(
        "UPDATE c05_10_access_outcomes SET outcome='VERIFIED'",setup);
    await command.ExecuteNonQueryAsync();
},"Append-only authentication outcome cannot be modified");
await RejectSql(async()=>
{
    await using var command=new MySqlCommand(
        "DELETE FROM c05_10_access_outcomes",setup);
    await command.ExecuteNonQueryAsync();
},"Append-only authentication outcome cannot be removed");
await RejectSql(async()=>
{
    await using var conn=new MySqlConnection(runnerDsn);
    await conn.OpenAsync();
    await using var command=new MySqlCommand(
        "DELETE FROM c05_10_access_traces",conn);
    await command.ExecuteNonQueryAsync();
},"EXECUTE-only user cannot delete private audit rows");

// POSTMERGE stability gate: repeat real HTTP bursts against the SAME
// tenant/source/subject after guarded window expiry, with the strict
// original 5/11 rule. This reproduces the historical 503 timing failure
// under InnoDB contention instead of treating a single lucky run as PASS.
await using(var stablePolicy=new MySqlCommand("""
    UPDATE c05_10_policies
       SET subject_limit=5,source_limit=12,enabled=1,host_revision=8,
           revision=revision+1,changed_utc=UTC_TIMESTAMP(6)
     WHERE tenant_id=UNHEX('aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa')
       AND purpose='LOGIN'
    """,setup))
    Check(await stablePolicy.ExecuteNonQueryAsync()==1,
          "Restore reviewed 5/12 synthetic policy before repeated concurrency");
for(int round=1;round<=5;round++)
{
    await ForceWindowExpiryAsync(tenantA,"LOGIN");
    var burst=await Task.WhenAll(Enumerable.Range(0,16).Select(_=>
        PostAsync(hostA.Value,raceLogin,wrongPassword)));
    int allowed=burst.Count(x=>x==HttpStatusCode.Accepted);
    int limited=burst.Count(x=>x==(HttpStatusCode)429);
    int unavailable=burst.Count(x=>x==HttpStatusCode.ServiceUnavailable);
    Console.WriteLine("C05-10 STRESS aggregate only: round="+round+
        " admitted="+allowed+" limited="+limited+" unavailable="+unavailable+
        " sql_fault_categories="+ledger.SafeAggregateDiagnostics());
    Check(allowed==5 && limited==11 && unavailable==0,
          "Repeated 16-way HTTP burst must admit 5/limit 11 without SQL 503");
}

await app.StopAsync();
Console.WriteLine($"PASS CRECISOL C05-10 REAL HTTP/.NET10/MySQL8.4: {checks} checks");
Console.WriteLine("PASS atomic pre-Argon2id dual budgets, source rotation, tenant A/B, 16 one-shot completions, audit HMAC, fail-closed and no token/password storage");
Console.WriteLine("NO-GO product: not wired to live API, no reverse proxy trust, distributed shield, real data, telemetry vendor or login session");

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





sealed class FixturePreAuthHostReader(HttpContext context,
    ResolveVerifiedTenantHost resolver,CanonicalTenantHost a,CanonicalTenantHost b)
    :ITrustedPreAuthTenantHostReader
{
    public async ValueTask<VerifiedTenantHostHint?> ReadServerVerifiedTenantAsync(
        CancellationToken ct)
    {
        string direct=context.Request.Host.Host;
        if(context.Request.Host.Port.HasValue ||
           context.Request.QueryString.HasValue ||
           (direct!=a.Value && direct!=b.Value))
            return null;
        try{return await resolver.ResolveAsync(
            CanonicalTenantHost.Parse(direct),ct);}
        catch(UnauthorizedAccessException){return null;}
        catch(InvalidOperationException){return null;}
    }
}
sealed class FixturePreAuthNetworkReader(HttpContext context)
    :ITrustedPreAuthNetworkReader
{
    public ValueTask<IPAddress?> ReadServerSocketPeerAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return ValueTask.FromResult(context.Connection.RemoteIpAddress);
    }
}
sealed class FixturePasswordVerifier(
    Func<CanonicalLoginHandle,ReadOnlyMemory<char>,CancellationToken,
         Task<ContextualLoginPreparation>> impl)
    :ITrustedThrottledPasswordVerifier
{
    public Task<ContextualLoginPreparation> VerifyAsync(
        CanonicalLoginHandle login,ReadOnlyMemory<char> candidate,CancellationToken ct)
        =>impl(login,candidate,ct);
}
sealed class FixtureHostHintReader(VerifiedTenantHostHint? host)
    :ITrustedPreAuthTenantHostReader
{
    public ValueTask<VerifiedTenantHostHint?> ReadServerVerifiedTenantAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return ValueTask.FromResult(host);
    }
}
sealed class FixtureNetworkReader(IPAddress? peer):ITrustedPreAuthNetworkReader
{
    public ValueTask<IPAddress?> ReadServerSocketPeerAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return ValueTask.FromResult(peer);
    }
}

/// <summary>
/// Test-only numerical SQL error categories to investigate racing 503, not
/// credential/IP/Host/email/DSN/SQL exceptions or audit payload logging.
/// </summary>
sealed class FixtureRateLimitDiagnosticLedger(
    MySqlDisposablePreAuthRateLimitLedger inner):ITrustedPreAuthRateLimitLedger
{
    private readonly ConcurrentDictionary<string,int> counts=new(StringComparer.Ordinal);
    private void Inc(string name)=>counts.AddOrUpdate(name,1,(_,n)=>n+1);
    public string SafeAggregateDiagnostics()=>string.Join(",",
        counts.OrderBy(x=>x.Key,StringComparer.Ordinal)
            .Select(x=>x.Key+":"+x.Value));
    public async Task<PreAuthAdmissionStatus> AdmitAsync(
        VerifiedTenantHostHint host,PreAuthRateLimitPurpose purpose,Guid attempt,
        PreAuthKeyedDigest source,PreAuthKeyedDigest subject,CancellationToken ct)
    {
        try
        {
            var result=await inner.AdmitAsync(host,purpose,attempt,source,subject,ct);
            if(result==PreAuthAdmissionStatus.Denied) Inc("ADMIT_DENIED");
            return result;
        }
        catch(MySqlException ex){Inc("ADMIT_MYSQL_"+ex.Number);throw;}
        catch(TimeoutException){Inc("ADMIT_TIMEOUT");throw;}
        catch(InvalidOperationException){Inc("ADMIT_INVALID");throw;}
    }
    public async Task<bool> CompleteAsync(
        VerifiedTenantHostHint host,PreAuthRateLimitPurpose purpose,Guid attempt,
        PreAuthKeyedDigest source,PreAuthKeyedDigest subject,
        PreAuthPrivateOutcome outcome,CancellationToken ct)
    {
        try
        {
            bool result=await inner.CompleteAsync(
                host,purpose,attempt,source,subject,outcome,ct);
            if(!result)Inc("COMPLETE_DENIED");
            return result;
        }
        catch(MySqlException ex){Inc("COMPLETE_MYSQL_"+ex.Number);throw;}
        catch(TimeoutException){Inc("COMPLETE_TIMEOUT");throw;}
        catch(InvalidOperationException){Inc("COMPLETE_INVALID");throw;}
    }
}

sealed class FixtureUnavailableLedger:ITrustedPreAuthRateLimitLedger
{
    public Task<PreAuthAdmissionStatus> AdmitAsync(
        VerifiedTenantHostHint host,PreAuthRateLimitPurpose purpose,
        Guid attempt,PreAuthKeyedDigest source,PreAuthKeyedDigest subject,CancellationToken ct)
        =>throw new InvalidOperationException("Simulated inaccessible fixture DB.");
    public Task<bool> CompleteAsync(
        VerifiedTenantHostHint host,PreAuthRateLimitPurpose purpose,
        Guid attempt,PreAuthKeyedDigest source,PreAuthKeyedDigest subject,
        PreAuthPrivateOutcome outcome,CancellationToken ct)
        =>throw new InvalidOperationException("Simulated inaccessible fixture DB.");
}
