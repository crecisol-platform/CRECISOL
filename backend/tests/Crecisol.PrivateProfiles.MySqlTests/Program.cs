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
    Console.WriteLine("SKIP C05-09 private profile HTTP/MySQL: --integration required");
    return;
}
if(args.Length!=1 || args[0]!="--integration")
    throw new InvalidOperationException("C05-09 test runner accepts --integration only.");
var setupDsn=Environment.GetEnvironmentVariable("CRECISOL_C05_09_SETUP_DSN");
var runnerDsn=Environment.GetEnvironmentVariable("CRECISOL_C05_09_RUNNER_DSN");
if(string.IsNullOrWhiteSpace(setupDsn)||string.IsNullOrWhiteSpace(runnerDsn))
    throw new InvalidOperationException("C05-09 isolated DSNs required.");
var config=new MySqlConnectionStringBuilder(setupDsn);
if(config.Server!="127.0.0.1"||config.Database!="c05_05_disposable"||
   config.AllowLoadLocalInfile) throw new InvalidOperationException("Only localhost fixture DB allowed.");
var ledger=new MySqlDisposablePrivateProfileLedger(runnerDsn);
int checks=0;
void Check(bool condition,string label)
{
    if(!condition)throw new InvalidOperationException("FAIL C05-09 "+label);
    Interlocked.Increment(ref checks);
}
void Deny<T>(Action action,string label) where T:Exception
{
    try {action();}
    catch(T){Check(true,label);return;}
    throw new InvalidOperationException("FAIL C05-09 missing denial "+label);
}
async Task RejectSqlAsync(Func<Task> action,string label)
{
    try {await action();}
    catch(MySqlException){Check(true,label);return;}
    throw new InvalidOperationException("FAIL C05-09 MySQL must reject: "+label);
}
var tenantA=TenantId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
var tenantB=TenantId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
var actor=TenantActorId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01"));
var outsider=TenantActorId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa02"));
var platform=TenantActorId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa03"));
var linkA=AccountTenantLinkId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa12"));
var linkB=AccountTenantLinkId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbba12"));
var otherLink=AccountTenantLinkId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa22"));
var platformLink=AccountTenantLinkId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa33"));
var hostA=CanonicalTenantHost.Parse("alpha.example.test");
var hostB=CanonicalTenantHost.Parse("beta.example.test");
var changed=DateTimeOffset.UtcNow.AddDays(-2);
var expires=DateTimeOffset.UtcNow.AddHours(5);
const string mail="profile-owner@example.test";
const string otherMail="profile-other@example.test";
string password=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
string otherPassword=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
var hasher=new Argon2idPasswordHasher(PasswordHashingPolicy.Default,
    new FixtureCompromisedScreen());
string phc=hasher.CreateVerifier(password).ExportForTrustedCredentialStore();
string phcOther=hasher.CreateVerifier(otherPassword).ExportForTrustedCredentialStore();
string dummy=hasher.CreateVerifier(Convert.ToBase64String(
    RandomNumberGenerator.GetBytes(32))).ExportForTrustedCredentialStore();
byte[] encKey=RandomNumberGenerator.GetBytes(32);
using var cipher=new SyntheticPrivateProfileCipher(encKey);
CryptographicOperations.ZeroMemory(encKey);
byte[] mailKey=RandomNumberGenerator.GetBytes(32);
byte[] resetKey=RandomNumberGenerator.GetBytes(32);
using var mailboxProtector=new PasswordRecoveryTokenProtector(resetKey,mailKey);
CryptographicOperations.ZeroMemory(mailKey);
CryptographicOperations.ZeroMemory(resetKey);
Check(cipher.ToString()=="SyntheticPrivateProfileCipher[redacted]" &&
      ledger.ToString()=="MySqlDisposablePrivateProfileLedger[redacted]",
      "Private encryption and SQL adapter redact diagnostics");
var good=PrivateDisplayName.Parse("Alice Example");
var alternate=PrivateDisplayName.Parse("Jose Alvarez");
Check(good.ToString()=="PrivateDisplayName[redacted]" &&
      alternate.Value=="Jose Alvarez","Private names typed but not diagnostic text");
Deny<InvalidOperationException>(()=>PrivateDisplayName.Parse("A"),
    "One-letter display name rejected");
Deny<InvalidOperationException>(()=>PrivateDisplayName.Parse(" Alice"),
    "Untrimmed display name rejected");
Deny<InvalidOperationException>(()=>PrivateDisplayName.Parse("<img src=x>"),
    "HTML injection blocked");
Deny<InvalidOperationException>(()=>PrivateDisplayName.Parse("Alice\u202Eevil"),
    "Unicode bidi override rejected");
Deny<InvalidOperationException>(()=>PrivateDisplayName.Parse("Alice  Example"),
    "Invisible/ambiguous double spaces rejected");
Deny<InvalidOperationException>(()=>PrivateDisplayName.Parse(new string('A',81)),
    "Overlong display names rejected");
var encrypted=cipher.EncryptForTenantActor(tenantA,actor,good);
Check(encrypted.Length>31 && encrypted[0]==1 &&
      !Encoding.UTF8.GetString(encrypted).Contains(good.Value,StringComparison.Ordinal) &&
      cipher.DecryptForTenantActor(tenantA,actor,encrypted).Value==good.Value,
    "Real AES256-GCM encryption/decryption, no stored plaintext");
Deny<CryptographicException>(()=>cipher.DecryptForTenantActor(
    tenantB,actor,encrypted),"GCM AAD prevents same-actor tenant B read");
Deny<CryptographicException>(()=>cipher.DecryptForTenantActor(
    tenantA,outsider,encrypted),"GCM AAD prevents other actor same-tenant read");
byte[] badEnvelope=(byte[])encrypted.Clone();
badEnvelope[^1]^=0x01;
Deny<CryptographicException>(()=>cipher.DecryptForTenantActor(
    tenantA,actor,badEnvelope),"GCM tag detects DB ciphertext tamper");
CryptographicOperations.ZeroMemory(encrypted);
CryptographicOperations.ZeroMemory(badEnvelope);
Deny<InvalidOperationException>(()=>new MySqlDisposablePrivateProfileLedger(
    "Server=example.com;Database=c05_05_disposable;Uid=x"),
    "No offsite MySQL profile adapter");
Deny<InvalidOperationException>(()=>new MySqlDisposablePrivateProfileLedger(
    "Server=127.0.0.1;Database=CRECISOL_PRODUCTION;Uid=x"),
    "No production MySQL profile adapter");

await using var setup=new MySqlConnection(config.ConnectionString);
await setup.OpenAsync();
async Task<long> ScalarAsync(string sql)
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
    Check(await cmd.ExecuteNonQueryAsync()==1,"Synthetic account bound to exact tenant/link");
}
async Task SeedCredentialAsync(TenantActorId who,string verifier,string realm)
{
    await using var cmd=new MySqlCommand("""
      INSERT INTO c05_06_credentials
       (actor_id,verifier_phc,credential_revision,credential_active,realm)
      VALUES(@actor,@phc,3,1,@realm)
      """,setup);
    cmd.Parameters.Add("@actor",MySqlDbType.Binary).Value=who.Value.ToByteArray(bigEndian:true);
    cmd.Parameters.Add("@phc",MySqlDbType.VarChar).Value=verifier;
    cmd.Parameters.Add("@realm",MySqlDbType.VarChar).Value=realm;
    Check(await cmd.ExecuteNonQueryAsync()==1,"Real Argon2id fixture verifier seeded");
}
async Task SeedMailboxAsync(TenantId tenant,TenantActorId who,string address)
{
    byte[] digest=mailboxProtector.DigestCanonicalMailbox(
        CanonicalLoginHandle.Parse(address)).CopyForTrustedLedger();
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
        Check(await cmd.ExecuteNonQueryAsync()==1,"Synthetic verified mailbox has only HMAC index");
    }
    finally{CryptographicOperations.ZeroMemory(digest);}
}
await SeedSubjectAsync(tenantA,actor,linkA);
await SeedSubjectAsync(tenantB,actor,linkB);
await SeedSubjectAsync(tenantA,outsider,otherLink);
await SeedSubjectAsync(tenantA,platform,platformLink);
await SeedCredentialAsync(actor,phc,"TENANT");
await SeedCredentialAsync(outsider,phcOther,"TENANT");
await SeedCredentialAsync(platform,phc,"PLATFORM");
await SeedMailboxAsync(tenantA,actor,mail);
await SeedMailboxAsync(tenantB,actor,mail);
await SeedMailboxAsync(tenantA,outsider,otherMail);
await SeedMailboxAsync(tenantA,platform,"platform-profile@example.test");

var photoA=Guid.NewGuid();
var photoB=Guid.NewGuid();
var photoOther=Guid.NewGuid();
var pendingA=Guid.NewGuid();
var revokedA=Guid.NewGuid();
var blockedA=Guid.NewGuid();
var mismatchedKeyA=Guid.NewGuid();

async Task SeedPhotoAsync(TenantId tenant,TenantActorId who,Guid mediaId,
    string state="VERIFIED",string scan="CLEAN",string? badKey=null,
    string mime="image/webp",long size=1200)
{
    var key=badKey??$"profiles/{tenant.Value:N}/{who.Value:N}/{mediaId:N}.webp";
    await using var cmd=new MySqlCommand("""
      INSERT INTO c05_09_media_manifest
      (tenant_id,actor_id,media_id,object_key,content_type,byte_length,
       content_sha256,state,scan_state,is_private,checked_utc)
      VALUES(@t,@a,@id,@key,@mime,@bytes,@sha,@state,@scan,1,UTC_TIMESTAMP(6))
      """,setup);
    cmd.Parameters.Add("@t",MySqlDbType.Binary).Value=tenant.Value.ToByteArray(bigEndian:true);
    cmd.Parameters.Add("@a",MySqlDbType.Binary).Value=who.Value.ToByteArray(bigEndian:true);
    cmd.Parameters.Add("@id",MySqlDbType.Binary).Value=mediaId.ToByteArray(bigEndian:true);
    cmd.Parameters.Add("@key",MySqlDbType.VarChar).Value=key;
    cmd.Parameters.Add("@mime",MySqlDbType.VarChar).Value=mime;
    cmd.Parameters.Add("@bytes",MySqlDbType.UInt64).Value=(ulong)size;
    cmd.Parameters.Add("@sha",MySqlDbType.Binary).Value=RandomNumberGenerator.GetBytes(32);
    cmd.Parameters.Add("@state",MySqlDbType.VarChar).Value=state;
    cmd.Parameters.Add("@scan",MySqlDbType.VarChar).Value=scan;
    Check(await cmd.ExecuteNonQueryAsync()==1,"Only trusted fixture creates media manifest");
}
await SeedPhotoAsync(tenantA,actor,photoA);
await SeedPhotoAsync(tenantB,actor,photoB);
await SeedPhotoAsync(tenantA,outsider,photoOther);
await SeedPhotoAsync(tenantA,actor,pendingA,state:"PENDING");
await SeedPhotoAsync(tenantA,actor,revokedA,state:"REVOKED");
await SeedPhotoAsync(tenantA,actor,blockedA,scan:"BLOCKED");
await SeedPhotoAsync(tenantA,actor,mismatchedKeyA,badKey:"../../outside.webp");
await RejectSqlAsync(()=>SeedPhotoAsync(tenantA,actor,Guid.NewGuid(),
    mime:"image/svg+xml"),"SVG active content violates manifest MIME constraint");
await RejectSqlAsync(()=>SeedPhotoAsync(tenantA,actor,Guid.NewGuid(),
    size:5000000),"Oversized image exceeds private avatar policy");
await RejectSqlAsync(async()=>
{
    await using var db=new MySqlConnection(runnerDsn);
    await db.OpenAsync();
    await using var cmd=new MySqlCommand("SELECT pii_ciphertext FROM c05_09_private_profiles",db);
    _=await cmd.ExecuteScalarAsync();
},"EXECUTE-only profile runner cannot read encrypted PII table");
await RejectSqlAsync(async()=>
{
    await using var db=new MySqlConnection(runnerDsn);
    await db.OpenAsync();
    await using var cmd=new MySqlCommand("UPDATE c05_09_media_manifest SET state='VERIFIED'",db);
    _=await cmd.ExecuteNonQueryAsync();
},"EXECUTE-only profile runner cannot spoof verified media record");

var account=IdentityAccountSnapshot.RestoreFromTrustedIdentityDirectory(
    actor,IdentityAccountState.Active,7,changed);
var credential=AccountCredentialMetadata.RestoreFromTrustedCredentialDirectory(
    AccountCredentialId.From(Guid.NewGuid()),actor,
    AccountCredentialKind.PasswordVerifier,AccountCredentialState.Active,3,changed);
var trusted=TrustedLoginCredentialSnapshot.RestoreFromTrustedDirectory(
    account,credential,TrustedLoginRealm.TenantAccount,phc);
var otherAccount=IdentityAccountSnapshot.RestoreFromTrustedIdentityDirectory(
    outsider,IdentityAccountState.Active,7,changed);
var otherCredential=AccountCredentialMetadata.RestoreFromTrustedCredentialDirectory(
    AccountCredentialId.From(Guid.NewGuid()),outsider,
    AccountCredentialKind.PasswordVerifier,AccountCredentialState.Active,3,changed);
var otherTrusted=TrustedLoginCredentialSnapshot.RestoreFromTrustedDirectory(
    otherAccount,otherCredential,TrustedLoginRealm.TenantAccount,phcOther);
var links=new[]{
    AccountTenantLinkSnapshot.RestoreFromTrustedMembershipDirectory(
        linkA,actor,tenantA,AccountTenantProfileKind.Owner,
        TenantMembershipState.Active,5,changed,changed,expires),
    AccountTenantLinkSnapshot.RestoreFromTrustedMembershipDirectory(
        linkB,actor,tenantB,AccountTenantProfileKind.Employee,
        TenantMembershipState.Active,5,changed,changed,expires)
};
var otherLinks=new[]{
    AccountTenantLinkSnapshot.RestoreFromTrustedMembershipDirectory(
        otherLink,outsider,tenantA,AccountTenantProfileKind.Consumer,
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
PrepareContextualPasswordLogin Login(CanonicalTenantHost host,bool isOther) =>
    new(new FixtureCredentialDirectory(isOther?otherMail:mail,isOther?otherTrusted:trusted),
        new FixtureDummyVerifier(dummy),
        new Argon2idLoginCandidateVerifier(hasher),
        new FixtureEntryReader(host),
        new FixtureLinkDirectory(isOther?outsider:actor,isOther?otherLinks:links),
        resolver,new FixtureTenantCatalog(tenantA,tenantB,catalogA,catalogB),clock);
var hintA=await resolver.ResolveAsync(hostA,CancellationToken.None);
var hintB=await resolver.ResolveAsync(hostB,CancellationToken.None);
Check(hintA.TenantId==tenantA&&hintB.TenantId==tenantB&&
      hintA.Revision==8&&hintB.Revision==8,
      "Server verified host bindings select tenant A/B without selector");
var prepared=await Login(hostA,false).ExecuteAsync(
    CanonicalLoginHandle.Parse(mail),password.AsMemory(),CancellationToken.None);
Check(prepared.Status==ContextualLoginPreparationStatus.Ready &&
      prepared.Evidence is not null,
      "Real C05-04 Argon2id input verifies C05-09 owner");
var proofA=prepared.Evidence!;
var otherPrepared=await Login(hostA,true).ExecuteAsync(
    CanonicalLoginHandle.Parse(otherMail),otherPassword.AsMemory(),CancellationToken.None);
Check(otherPrepared.Status==ContextualLoginPreparationStatus.Ready &&
      otherPrepared.Evidence is not null &&
      otherPrepared.Evidence.ActorId==outsider,
      "Independent actor real Argon2id proof is tenant A self-only");
var otherProof=otherPrepared.Evidence!;
var passwordChecks=new SemaphoreSlim(3,3);
var reauth=new FixtureProfileReauthentication(async (address,candidate,hint,ct)=>
{
    await passwordChecks.WaitAsync(ct);
    try
    {
        var requestedHost=hint.TenantId==tenantA?hostA:hostB;
        return await Login(requestedHost,address.Value==otherMail).ExecuteAsync(
            address,candidate,ct);
    }
    finally {passwordChecks.Release();}
});
var profiles=new PrivateTenantProfileUseCases(reauth,ledger,cipher);
Check(profiles.ToString()=="PrivateTenantProfileUseCases[redacted]",
      "Use case ToString redacts PII and credentials");

// A trusted pre-session proof MUST still be matched to current SQL revisions.
// It is not a bearer, role, session or authorization in any production API.
Check(await ledger.ReadSelfAsync(proofA,CancellationToken.None) is null &&
      await ledger.ReadSelfAsync(otherProof,CancellationToken.None) is null,
    "Absent tenant profiles return none to their OWN verified actor");

var builder=WebApplication.CreateBuilder(new WebApplicationOptions{
    Args=Array.Empty<string>(),EnvironmentName="Production"
});
builder.Logging.ClearProviders();
builder.WebHost.UseUrls("http://127.0.0.1:0");
await using var app=builder.Build();
app.Use(async(c,next)=>
{
    c.Response.Headers.CacheControl="no-store";
    c.Response.Headers.Pragma="no-cache";
    c.Response.Headers["Referrer-Policy"]="no-referrer";
    try{await next();}
    catch(InvalidOperationException){c.Response.StatusCode=403;}
    catch(CryptographicException){c.Response.StatusCode=403;}
});
async Task<VerifiedTenantHostHint?> HostAsync(HttpContext c)
{
    if(c.Request.Host.Port.HasValue || c.Request.QueryString.HasValue ||
       c.Request.Headers.ContainsKey("Forwarded") ||
       c.Request.Headers.ContainsKey("X-Forwarded-Host") ||
       c.Request.Headers.ContainsKey("X-Tenant-Id") ||
       c.Request.Headers.ContainsKey("X-Actor-Id") ||
       c.Request.Headers.ContainsKey("Authorization") ||
       c.Request.Headers.ContainsKey("Cookie"))
        return null;
    var raw=c.Request.Host.Host;
    if(raw!=hostA.Value && raw!=hostB.Value)return null;
    try{return await resolver.ResolveAsync(
        CanonicalTenantHost.Parse(raw),c.RequestAborted);}
    catch(InvalidOperationException){return null;}
    catch(UnauthorizedAccessException){return null;}
}
static async Task<JsonDocument?> BodyAsync(HttpContext c)
{
    if(c.Request.ContentLength is null or <=0 or >2048 ||
       !(c.Request.ContentType?.StartsWith("application/json",
         StringComparison.OrdinalIgnoreCase)??false))return null;
    try{return await JsonDocument.ParseAsync(c.Request.Body,
        new JsonDocumentOptions{MaxDepth=4},c.RequestAborted);}
    catch(JsonException){return null;}
}
static bool HasExactly(JsonElement el,params string[] keys)=>
    el.ValueKind==JsonValueKind.Object &&
    el.EnumerateObject().Count()==keys.Length &&
    keys.All(k=>el.TryGetProperty(k,out _));
static string Text(JsonElement e,string key)=>e.GetProperty(key).GetString()??"";
app.MapPost("/__c05_09_test__/save",async (HttpContext c)=>
{
    var host=await HostAsync(c);
    if(host is null)return Results.BadRequest();
    using var d=await BodyAsync(c);
    if(d is null || !HasExactly(d.RootElement,"email","password","name","revision","photoId"))
        return Results.BadRequest();
    var data=d.RootElement;
    if(data.GetProperty("email").ValueKind!=JsonValueKind.String ||
       data.GetProperty("password").ValueKind!=JsonValueKind.String ||
       data.GetProperty("name").ValueKind!=JsonValueKind.String ||
       !data.GetProperty("revision").TryGetInt64(out long rev))
        return Results.BadRequest();
    Guid? photo=null;
    var input=data.GetProperty("photoId");
    if(input.ValueKind==JsonValueKind.String)
    {
        if(!Guid.TryParse(input.GetString(),out Guid parsed))
            return Results.BadRequest();
        photo=parsed;
    }
    else if(input.ValueKind!=JsonValueKind.Null)
        return Results.BadRequest();
    try
    {
        bool saved=await profiles.SaveSelfAsync(
            CanonicalLoginHandle.Parse(Text(data,"email")),
            Text(data,"password").AsMemory(),host,Text(data,"name"),
            photo,rev,c.RequestAborted);
        return saved?Results.NoContent():Results.StatusCode(403);
    }
    catch(InvalidOperationException){return Results.StatusCode(403);}
});
app.MapPost("/__c05_09_test__/read",async(HttpContext c)=>
{
    var host=await HostAsync(c);
    if(host is null)return Results.BadRequest();
    using var d=await BodyAsync(c);
    if(d is null || !HasExactly(d.RootElement,"email","password") ||
       d.RootElement.GetProperty("email").ValueKind!=JsonValueKind.String ||
       d.RootElement.GetProperty("password").ValueKind!=JsonValueKind.String)
        return Results.BadRequest();
    try
    {
        var row=await profiles.ReadSelfAsync(
            CanonicalLoginHandle.Parse(Text(d.RootElement,"email")),
            Text(d.RootElement,"password").AsMemory(),host,c.RequestAborted);
        return row is null?Results.NotFound():Results.Json(new{
            displayName=row.Name.Value,revision=row.Revision,
            hasPhoto=row.PhotoId.HasValue,photoId=row.PhotoId
        });
    }
    catch(InvalidOperationException){return Results.NotFound();}
    catch(CryptographicException){return Results.NotFound();}
});
app.MapPost("/__c05_09_test__/erase",async(HttpContext c)=>
{
    var host=await HostAsync(c);
    if(host is null)return Results.BadRequest();
    using var d=await BodyAsync(c);
    if(d is null || !HasExactly(d.RootElement,"email","password","revision") ||
       d.RootElement.GetProperty("email").ValueKind!=JsonValueKind.String ||
       d.RootElement.GetProperty("password").ValueKind!=JsonValueKind.String ||
       !d.RootElement.GetProperty("revision").TryGetInt64(out long rev))
        return Results.BadRequest();
    try
    {
        bool deleted=await profiles.EraseSelfAsync(
            CanonicalLoginHandle.Parse(Text(d.RootElement,"email")),
            Text(d.RootElement,"password").AsMemory(),host,rev,c.RequestAborted);
        return deleted?Results.NoContent():Results.StatusCode(403);
    }
    catch(InvalidOperationException){return Results.StatusCode(403);}
});
await app.StartAsync();
string? endpoint=app.Services.GetRequiredService<IServer>()
    .Features.Get<IServerAddressesFeature>()?.Addresses.Single();
if(endpoint is null ||
   !endpoint.StartsWith("http://127.0.0.1:",StringComparison.Ordinal))
    throw new InvalidOperationException("C05-09 HTTP MUST bind loopback only.");
using var client=new HttpClient(new SocketsHttpHandler{
    UseProxy=false,AllowAutoRedirect=false
}){BaseAddress=new Uri(endpoint),Timeout=TimeSpan.FromSeconds(180)};
async Task<(HttpStatusCode Code,string Body)> RequestAsync(
    string route,string host,object data,string? spoof=null,
    bool forwarded=false)
{
    using var request=new HttpRequestMessage(HttpMethod.Post,
        "/__c05_09_test__/"+route);
    request.Headers.Host=host;
    if(spoof is not null)
        request.Headers.TryAddWithoutValidation("X-Tenant-Id",spoof);
    if(forwarded)
        request.Headers.TryAddWithoutValidation("X-Forwarded-Host","beta.example.test");
    request.Content=new StringContent(JsonSerializer.Serialize(data),Encoding.UTF8,
        "application/json");
    using var resp=await client.SendAsync(request);
    string body=await resp.Content.ReadAsStringAsync();
    Check(resp.Headers.CacheControl?.NoStore==true &&
          !resp.Headers.Contains("Set-Cookie") &&
          !body.Contains("profiles/",StringComparison.Ordinal) &&
          !body.Contains("cloudflare",StringComparison.OrdinalIgnoreCase) &&
          !body.Contains("r2.dev",StringComparison.OrdinalIgnoreCase),
        "Private profile HTTP never sends object storage route, presigned URL, cache or cookie");
    if(resp.StatusCode!=HttpStatusCode.OK)
        Check(body.Length==0,"Denied/mutation HTTP returns no PII, URL or object key");
    return(resp.StatusCode,body);
}
Task<(HttpStatusCode Code,string Body)> Save(
    string host,string email,string pass,string name,long revision,Guid? photo)=>
    RequestAsync("save",host,new{email,password=pass,name,revision,photoId=photo});
Task<(HttpStatusCode Code,string Body)> Read(
    string host,string email,string pass)=>
    RequestAsync("read",host,new{email,password=pass});
Task<(HttpStatusCode Code,string Body)> Erase(
    string host,string email,string pass,long revision)=>
    RequestAsync("erase",host,new{email,password=pass,revision});


// Genuine HTTP / MySQL8.4 adversarial requests (no product API routing).
Check((await Read(hostA.Value,mail,password)).Code==HttpStatusCode.NotFound,
    "No guessed profile or PII appears before owned profile creation");
Check((await Save("evil.example.test",mail,password,"Alice Example",0,photoA))
      .Code==HttpStatusCode.BadRequest,
    "Unverified Host cannot create any profile");
Check((await RequestAsync("save",hostA.Value,
      new{email=mail,password,name="Alice Example",revision=0,photoId=photoA},
      spoof:tenantB.Value.ToString())).Code==HttpStatusCode.BadRequest,
    "Forged X-Tenant-Id cannot select another tenant");
Check((await RequestAsync("save",hostA.Value,
      new{email=mail,password,name="Alice Example",revision=0,photoId=photoA},
      forwarded:true)).Code==HttpStatusCode.BadRequest,
    "Forwarded host cannot override direct host backend");
Check((await RequestAsync("save",hostA.Value,
      new{email=mail,password,name="Alice Example",revision=0,photoId=photoA,
          actorId=outsider.Value})).Code==HttpStatusCode.BadRequest,
    "ActorId from browser is forbidden, even when owned tenant");
Check((await Save(hostA.Value,mail,"incorrect","Alice Example",0,photoA))
      .Code==HttpStatusCode.Forbidden &&
      (await Read(hostA.Value,mail,"incorrect")).Code==HttpStatusCode.NotFound,
    "Wrong Argon2id password cannot create or read PII");
Check((await Save(hostA.Value,otherMail,password,"Alice Example",0,photoA))
      .Code==HttpStatusCode.Forbidden,
    "Different account email cannot borrow a password");
Check((await Save(hostA.Value,mail,password,"<script>",0,photoA))
      .Code==HttpStatusCode.Forbidden,
    "Unsafe profile name cannot reach storage");

async Task<bool> RejectedPhoto(Guid id,string label)
{
    bool denied=(await Save(hostA.Value,mail,password,"Alice Example",0,id))
       .Code==HttpStatusCode.Forbidden;
    Check(denied,label);
    return denied;
}
_ = await RejectedPhoto(photoB,"Tenant B media ID cannot attach to tenant A profile");
_ = await RejectedPhoto(photoOther,"Same tenant different actor media ID cannot attach");
_ = await RejectedPhoto(pendingA,"Pending upload cannot become private profile photo");
_ = await RejectedPhoto(revokedA,"Revoked upload cannot become private profile photo");
_ = await RejectedPhoto(blockedA,"Unscanned or blocked content cannot become photo");
_ = await RejectedPhoto(mismatchedKeyA,"Traversal/foreign key cannot be referenced despite verified state");
Check((await Read(hostA.Value,mail,password)).Code==HttpStatusCode.NotFound,
      "Rejected uploads never create a partial profile or leak PII");

Check((await Save(hostA.Value,mail,password,"Alice Example",0,photoA))
      .Code==HttpStatusCode.NoContent,
      "Valid owner self update stores only encrypted PII and private pointer on tenant A");
var aFirst=await Read(hostA.Value,mail,password);
Check(aFirst.Code==HttpStatusCode.OK &&
      aFirst.Body.Contains("\"displayName\":\"Alice Example\"",StringComparison.Ordinal) &&
      aFirst.Body.Contains(photoA.ToString("D"),StringComparison.OrdinalIgnoreCase) &&
      aFirst.Body.Contains("\"revision\":1",StringComparison.Ordinal),
      "A read decrypts own private PII, revision and opaque photo ID, no object URL");
Check((await Save(hostB.Value,mail,password,"Beta Example",0,photoB))
      .Code==HttpStatusCode.NoContent,
      "Same global actor has an independent profile and photo in tenant B");
var bFirst=await Read(hostB.Value,mail,password);
Check(bFirst.Code==HttpStatusCode.OK &&
      bFirst.Body.Contains("\"displayName\":\"Beta Example\"",StringComparison.Ordinal) &&
      bFirst.Body.Contains(photoB.ToString("D"),StringComparison.OrdinalIgnoreCase) &&
      !bFirst.Body.Contains("Alice Example",StringComparison.Ordinal) &&
      !bFirst.Body.Contains(photoA.ToString("D"),StringComparison.OrdinalIgnoreCase),
      "Tenant B profile never returns A PII or A photo ID");
var aAgain=await Read(hostA.Value,mail,password);
Check(aAgain.Body.Contains("\"displayName\":\"Alice Example\"",StringComparison.Ordinal) &&
      !aAgain.Body.Contains("Beta Example",StringComparison.Ordinal) &&
      !aAgain.Body.Contains(photoB.ToString("D"),StringComparison.OrdinalIgnoreCase),
      "Tenant A profile never returns B PII or B photo ID");

Check((await Read(hostA.Value,otherMail,otherPassword)).Code==HttpStatusCode.NotFound,
      "Distinct active actor in tenant A cannot see owner profile");
Check((await Save(hostA.Value,otherMail,otherPassword,"Other Consumer",0,photoA))
      .Code==HttpStatusCode.Forbidden,
      "Consumer profile kind grants no ability to use owner's photo");
Check((await Save(hostA.Value,otherMail,otherPassword,"Other Consumer",0,photoOther))
      .Code==HttpStatusCode.NoContent,
      "Distinct actor can create only its own independently scoped private profile");
var otherView=await Read(hostA.Value,otherMail,otherPassword);
Check(otherView.Code==HttpStatusCode.OK &&
      otherView.Body.Contains("Other Consumer",StringComparison.Ordinal) &&
      !otherView.Body.Contains("Alice Example",StringComparison.Ordinal),
      "Other actor view never includes owner PII");
Check((await Read(hostB.Value,otherMail,otherPassword)).Code==HttpStatusCode.NotFound,
      "Consumer without B membership cannot read A data through B verified host");
Check((await Save(hostA.Value,mail,password,"Alice Replaced",1,photoOther))
      .Code==HttpStatusCode.Forbidden,
      "Existing profile cannot be switched to foreign actor photo");
Check((await Save(hostA.Value,mail,password,"Alice Replaced",1,photoB))
      .Code==HttpStatusCode.Forbidden,
      "Existing profile cannot be switched to another tenant photo");
Check(await ScalarAsync("SELECT COUNT(*) FROM c05_09_private_profiles")==3,
      "Exactly 3 independent tenant/actor profiles persisted in isolated MySQL");

string aHex=tenantA.Value.ToString("N");
string bHex=tenantB.Value.ToString("N");
string actorHex=actor.Value.ToString("N");
string otherHex=outsider.Value.ToString("N");
Check(await ScalarAsync($"""
    SELECT LOCATE(HEX(CONVERT('Alice Example' USING utf8mb4)),
           HEX(pii_ciphertext)) FROM c05_09_private_profiles
     WHERE tenant_id=UNHEX('{aHex}') AND actor_id=UNHEX('{actorHex}')
    """)==0,"Private personal name is NOT stored as plaintext in MySQL");
Check(await ScalarAsync("""
    SELECT COUNT(*) FROM information_schema.columns
     WHERE table_schema=DATABASE()
       AND table_name IN ('c05_09_private_profiles','c05_09_profile_audit')
       AND (column_name LIKE '%email%' OR column_name LIKE '%password%'
         OR column_name LIKE '%token%' OR column_name LIKE '%name%'
         OR column_name LIKE '%url%' OR column_name LIKE '%secret%'
         OR column_name LIKE '%file_content%')
    """)==0,"Private profile and audit schemas contain NO PII name, token, key or URL columns");

// 16 real HTTP writes using the SAME optimistic revision. Password is ALWAYS
// Argon2id-verified; limiter caps memory load, DB CAS permits exactly 1 winner.
var race=await Task.WhenAll(Enumerable.Range(0,16).Select(_=>
    Save(hostA.Value,mail,password,"Alice Updated",1,photoA)));
Check(race.Count(x=>x.Code==HttpStatusCode.NoContent)==1 &&
      race.Count(x=>x.Code==HttpStatusCode.Forbidden)==15,
    "16 concurrent HTTP profile CAS updates: exactly one winner and fifteen denied");
var aSecond=await Read(hostA.Value,mail,password);
Check(aSecond.Code==HttpStatusCode.OK &&
      aSecond.Body.Contains("\"displayName\":\"Alice Updated\"",StringComparison.Ordinal) &&
      aSecond.Body.Contains("\"revision\":2",StringComparison.Ordinal),
    "Concurrent updates commit exactly one encrypted private name revision");
Check(await ScalarAsync($"""
     SELECT COUNT(*) FROM c05_09_profile_audit
      WHERE tenant_id=UNHEX('{aHex}') AND actor_id=UNHEX('{actorHex}')
        AND event_type='UPDATED'
     """)==1,"Append-only audit records exactly one successful CAS modification");
Check((await Save(hostA.Value,mail,password,"Stale Attempt",1,null))
      .Code==HttpStatusCode.Forbidden,
      "Stale revision cannot remove photo or overwrite PII");
Check((await Read(hostB.Value,mail,password)).Body.Contains("Beta Example",
      StringComparison.Ordinal),
    "Parallel A revision changes never overwrite B profile or photo");

// A photo may be later revoked. Profile read must hide its opaque ID
// instead of revealing a stale object pointer from a revoked manifest.
await using(var revoke=new MySqlCommand($"""
    UPDATE c05_09_media_manifest SET state='REVOKED'
     WHERE tenant_id=UNHEX('{aHex}') AND actor_id=UNHEX('{actorHex}')
       AND media_id=UNHEX('{photoA:N}')
    """,setup))
    Check(await revoke.ExecuteNonQueryAsync()==1,
        "Trusted fixture revokes formerly VERIFIED photo");
var noPhoto=await Read(hostA.Value,mail,password);
Check(noPhoto.Code==HttpStatusCode.OK &&
      noPhoto.Body.Contains("\"hasPhoto\":false",StringComparison.Ordinal) &&
      !noPhoto.Body.Contains(photoA.ToString("D"),StringComparison.OrdinalIgnoreCase) &&
      noPhoto.Body.Contains("Alice Updated",StringComparison.Ordinal),
    "Revocation hides private photo ID without leaking storage reference");
Check((await Save(hostA.Value,mail,password,"New Attempt",2,photoA))
      .Code==HttpStatusCode.Forbidden,
      "Revoked photo cannot be resurrected as an active profile photo");
Check((await Save(hostA.Value,mail,password,"Alice Updated",2,null))
      .Code==HttpStatusCode.NoContent,
      "Owner can intentionally clear a profile photo pointer using CAS");
Check((await Erase(hostA.Value,mail,"wrong",3)).Code==HttpStatusCode.Forbidden,
      "Incorrect Argon2id cannot erase someone else's personal data");
Check((await Erase(hostA.Value,mail,password,2)).Code==HttpStatusCode.Forbidden,
      "Old CAS version cannot erase newer private profile");
Check((await Erase(hostA.Value,mail,password,3)).Code==HttpStatusCode.NoContent,
      "Owner's versioned erasure clears encrypted name and photo atomically");
Check((await Read(hostA.Value,mail,password)).Code==HttpStatusCode.NotFound,
      "Erased profile no longer returns PII, placeholder or photo ID");
Check((await Erase(hostA.Value,mail,password,3)).Code==HttpStatusCode.Forbidden,
      "Erasure replay cannot repeat audit mutation");
Check(await ScalarAsync($"""
    SELECT COUNT(*) FROM c05_09_private_profiles
     WHERE tenant_id=UNHEX('{aHex}') AND actor_id=UNHEX('{actorHex}')
       AND pii_ciphertext IS NULL AND photo_id IS NULL AND profile_revision=4
    """)==1,"DB contains no PII envelope or photo pointer after erasure");
Check((await Read(hostB.Value,mail,password)).Body.Contains("Beta Example",
      StringComparison.Ordinal) &&
      (await Read(hostA.Value,otherMail,otherPassword)).Body.Contains("Other Consumer",
      StringComparison.Ordinal),
      "Erasing A owner does not erase tenant B or other actor A");

// Mailbox de-verification stops private PII access, despite an old password.
await using(var off=new MySqlCommand($"""
    UPDATE c05_06_mailboxes SET verified=0
    WHERE tenant_id=UNHEX('{bHex}') AND actor_id=UNHEX('{actorHex}')
    """,setup))
    Check(await off.ExecuteNonQueryAsync()==1,
        "Fixture de-verifies tenant B mailbox");
Check((await Read(hostB.Value,mail,password)).Code==HttpStatusCode.NotFound &&
      (await Save(hostB.Value,mail,password,"Wrongly Recovered",1,null))
        .Code==HttpStatusCode.Forbidden,
    "Unverified B mailbox cannot read or mutate B private PII");
await using(var on=new MySqlCommand($"""
    UPDATE c05_06_mailboxes SET verified=1
    WHERE tenant_id=UNHEX('{bHex}') AND actor_id=UNHEX('{actorHex}')
    """,setup))
    Check(await on.ExecuteNonQueryAsync()==1,"Fixture re-verifies B mailbox");
Check((await Read(hostB.Value,mail,password)).Body.Contains("Beta Example",
      StringComparison.Ordinal),"Reverified tenant B still sees own profile only");

// Changing host revision and then restoring active status cannot reuse a
// previously prepared proof carrying the old version (host revision=8).
await using(var rev=new MySqlCommand($"""
    UPDATE c05_05_subjects SET host_revision=host_revision+1
    WHERE tenant_id=UNHEX('{bHex}') AND actor_id=UNHEX('{actorHex}')
    """,setup))
    Check(await rev.ExecuteNonQueryAsync()==1,"Fixture bumps verified host revision in B");
Check((await Read(hostB.Value,mail,password)).Code==HttpStatusCode.NotFound &&
      (await Save(hostB.Value,mail,password,"Stale Host",1,null))
        .Code==HttpStatusCode.Forbidden,
    "Host revision mismatch always denies PII despite valid old password");
await using(var suspend=new MySqlCommand($"""
    UPDATE c05_05_subjects SET membership_active=0, membership_revision=membership_revision+1
    WHERE tenant_id=UNHEX('{aHex}') AND actor_id=UNHEX('{otherHex}')
    """,setup))
    Check(await suspend.ExecuteNonQueryAsync()==1,
        "Fixture suspends other actor membership and advances revision");
Check((await Read(hostA.Value,otherMail,otherPassword)).Code==
       HttpStatusCode.NotFound,
    "Suspended tenant membership cannot read other actor private PII");
await using(var restore=new MySqlCommand($"""
    UPDATE c05_05_subjects SET membership_active=1
    WHERE tenant_id=UNHEX('{aHex}') AND actor_id=UNHEX('{otherHex}')
    """,setup))
    Check(await restore.ExecuteNonQueryAsync()==1,
        "Fixture restores membership without restoring old proof revision");
Check((await Read(hostA.Value,otherMail,otherPassword)).Code==
       HttpStatusCode.NotFound,
    "Reactivated membership cannot resurrect an old profile authorization");

// Platform operators cannot borrow tenant profile permissions even when
// fixture records are syntactically complete.
await using(var admin=new MySqlConnection(runnerDsn))
{
    await admin.OpenAsync();
    await using var cmd=new MySqlCommand("c05_09_read_self",admin)
        {CommandType=System.Data.CommandType.StoredProcedure};
    void Id(string key,Guid v)=>cmd.Parameters.Add(key,MySqlDbType.Binary).Value=
        v.ToByteArray(bigEndian:true);
    void Rev(string key,ulong v)=>cmd.Parameters.Add(key,MySqlDbType.UInt64).Value=v;
    Id("@p_tenant",tenantA.Value);
    Id("@p_actor",platform.Value);
    Id("@p_link",platformLink.Value);
    Rev("@p_account",7); Rev("@p_credential",3); Rev("@p_membership",5);
    Rev("@p_tenant_revision",9); Rev("@p_host",8);
    await using var output=await cmd.ExecuteReaderAsync();
    Check(await output.ReadAsync() && output.GetString(0)=="DENIED",
        "PLATFORM realm denied by MySQL despite correct tenant-style fixture revisions");
}
await RejectSqlAsync(async()=>
{
    await using var cmd=new MySqlCommand(
        "UPDATE c05_09_profile_audit SET event_type='CREATED' WHERE audit_id=1",setup);
    await cmd.ExecuteNonQueryAsync();
},"Append-only profile audit UPDATE prohibited even for fixture admin");
await RejectSqlAsync(async()=>
{
    await using var cmd=new MySqlCommand(
        "DELETE FROM c05_09_profile_audit WHERE audit_id=1",setup);
    await cmd.ExecuteNonQueryAsync();
},"Append-only profile audit DELETE prohibited even for fixture admin");
Check(await ScalarAsync("""
    SELECT COUNT(*) FROM information_schema.columns
    WHERE table_schema=DATABASE() AND table_name='c05_09_media_manifest'
      AND (data_type IN ('blob','longblob','mediumblob','tinyblob')
          OR column_name LIKE '%url%' OR column_name LIKE '%binary%'
          OR column_name LIKE '%image_data%')
    """)==0,
    "R2-style media manifest stores metadata/reference only, not image bytes or URL");
Check(await ScalarAsync($"""
    SELECT COUNT(*) FROM c05_09_profile_audit WHERE tenant_id=UNHEX('{aHex}')
      AND actor_id=UNHEX('{actorHex}') AND event_type='ERASED'
    """)==1,"Private PII erasure is auditable without PII in immutable log");
Check(reauth.Calls>=25,"Every private read/update/delete triggered real C05-04 Argon2id reauthentication");

await app.StopAsync();
Console.WriteLine($"PASS CRECISOL C05-09 REAL HTTP/.NET10/MySQL8.4: {checks} checks");
Console.WriteLine("PASS self-only AES256-GCM, photo manifest pointer, A/B and actor IDOR denial, 16-way CAS, privacy erasure, revocation and audit immutability");
Console.WriteLine("NO-GO production: no real Cloudflare upload, antivirus, MIME content verification, KMS, login session, CDN URLs or user accounts.");

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



sealed class FixtureProfileReauthentication : ITrustedImmediatePasswordReauthentication
{
    private readonly Func<CanonicalLoginHandle,ReadOnlyMemory<char>,VerifiedTenantHostHint,
        CancellationToken,Task<ContextualLoginPreparation>> callback;
    private long calls;
    public FixtureProfileReauthentication(
        Func<CanonicalLoginHandle,ReadOnlyMemory<char>,VerifiedTenantHostHint,
            CancellationToken,Task<ContextualLoginPreparation>> callback) =>
        this.callback=callback??throw new ArgumentNullException(nameof(callback));
    public long Calls=>Interlocked.Read(ref calls);
    public Task<ContextualLoginPreparation> VerifyFreshAsync(
        CanonicalLoginHandle handle,ReadOnlyMemory<char> candidate,
        VerifiedTenantHostHint host,CancellationToken ct)
    {
        Interlocked.Increment(ref calls);
        // Fixture must pass the ACTUAL candidate to C05-04 Argon2id.
        return callback(handle,candidate,host,ct);
    }
}
