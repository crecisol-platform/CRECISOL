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
var reauth=new FixtureProfileReauthentication(async (address,hint,ct)=>
{
    await passwordChecks.WaitAsync(ct);
    try
    {
        var requestedHost=hint.TenantId==tenantA?hostA:hostB;
        return await Login(requestedHost,address.Value==otherMail).ExecuteAsync(
            address,address.Value==otherMail?
                otherPassword.AsMemory():password.AsMemory(),ct);
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
    private readonly Func<CanonicalLoginHandle,VerifiedTenantHostHint,
        CancellationToken,Task<ContextualLoginPreparation>> callback;
    private long calls;
    public FixtureProfileReauthentication(
        Func<CanonicalLoginHandle,VerifiedTenantHostHint,CancellationToken,
            Task<ContextualLoginPreparation>> callback) =>
        this.callback=callback??throw new ArgumentNullException(nameof(callback));
    public long Calls=>Interlocked.Read(ref calls);
    public Task<ContextualLoginPreparation> VerifyFreshAsync(
        CanonicalLoginHandle handle,ReadOnlyMemory<char> candidate,
        VerifiedTenantHostHint host,CancellationToken ct)
    {
        Interlocked.Increment(ref calls);
        // Fixture must pass the ACTUAL candidate to C05-04 Argon2id.
        return callback(handle,host,ct);
    }
}
