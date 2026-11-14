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
var ledger=new MySqlDisposablePreAuthRateLimitLedger(runnerDsn);
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
Check(ledger.ToString()=="MySqlDisposablePreAuthRateLimitLedger[redacted]"&&
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
