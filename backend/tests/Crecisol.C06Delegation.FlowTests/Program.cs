using System.Globalization;
using System.Net;
using System.Text.Json;
using Crecisol.C06Delegation.FlowTests;
using Crecisol.Api.Contracts;
using System.Security.Cryptography;
using System.Text;
using Crecisol.Application.Identity;
using Crecisol.CredentialSecurity;
using Crecisol.Infrastructure.Identity;
using MySqlConnector;

// Actual C05 transaction, actual encrypted TOTP seed and production verifier.
// All rows and credentials are isolated disposable fixtures. No HTTP exposure.
static MySqlConnectionStringBuilder Isolated(string variable,string user)
{
    var dsn=new MySqlConnectionStringBuilder(Environment.GetEnvironmentVariable(variable) ??
        throw new InvalidOperationException("Missing isolated flow DSN."));
    if(dsn.Server!="127.0.0.1" || dsn.Database!="crecisol_c05" || dsn.UserID!=user ||
       dsn.Port==3306 || dsn.Port==0)
        throw new InvalidOperationException("Only random-port isolated fixture permitted.");
    return dsn;
}
var fixture=Isolated("C06_DELEGATION_FIXTURE_DSN","c06_delegation_fixture_test");
var runtime=Isolated("C06_DELEGATION_FLOW_DSN","c06_delegation_flow_test");
var rollbackRuntime=Isolated("C06_DELEGATION_ROLLBACK_DSN","c06_delegation_rollback_test");
if(fixture.Port!=runtime.Port || fixture.Port!=rollbackRuntime.Port)
    throw new InvalidOperationException("All isolated fixture connections must use the same random port.");
await using var admin=new MySqlConnection(fixture.ConnectionString);
await admin.OpenAsync();
static Guid Id(string hex)=>new(Convert.FromHexString(string.Concat(Enumerable.Repeat(hex,16))),true);
static byte[] Key(Guid id)=>id.ToByteArray(true);
static string Hex(byte[] b)=>Convert.ToHexString(b);
var tenant=Key(Id("11"));var foreign=Key(Id("22"));
var actor=Key(Id("31"));var subject=Key(Id("34"));var other=Key(Id("32"));
var member=Key(Id("41"));var sourceGrant=Key(Guid.NewGuid());
var factor=Key(Guid.NewGuid());var session=Key(Guid.NewGuid());var session2=Key(Guid.NewGuid());
var token=Hex(RandomNumberGenerator.GetBytes(32));var token2=Hex(RandomNumberGenerator.GetBytes(32));
var subjectToken=Hex(RandomNumberGenerator.GetBytes(32));var otherToken=Hex(RandomNumberGenerator.GetBytes(32));
var tokenKey=RandomNumberGenerator.GetBytes(32);var encKey=RandomNumberGenerator.GetBytes(32);
var loginKey=RandomNumberGenerator.GetBytes(32);var mailboxKey=RandomNumberGenerator.GetBytes(32);
var seed=TotpSha1Verifier.GenerateSeed();
var nonce=RandomNumberGenerator.GetBytes(12);var ciphertext=new byte[seed.Length];var tag=new byte[16];
using(var aes=new AesGcm(encKey,16))
    aes.Encrypt(nonce,seed,ciphertext,tag,Encoding.UTF8.GetBytes("C05/operational/v1/factor:"+Hex(actor)+Hex(factor)));
byte[] SessionHash(string value)=>HMACSHA256.HashData(tokenKey,Encoding.UTF8.GetBytes("C05/session:"+value));
async Task<int> Exec(string sql,params (string,object)[] ps)
{
    await using var cmd=new MySqlCommand(sql,admin);
    foreach(var (n,v) in ps)cmd.Parameters.AddWithValue("@"+n,v);
    return await cmd.ExecuteNonQueryAsync();
}
async Task<object?> Scalar(string sql,params (string,object)[] ps)
{
    await using var cmd=new MySqlCommand(sql,admin);
    foreach(var (n,v) in ps)cmd.Parameters.AddWithValue("@"+n,v);
    return await cmd.ExecuteScalarAsync();
}
async Task<long> Count(string table)
{
    if(table is not ("c06_delegation_grant" or "c06_delegation_revocation" or "c06_delegation_confirmation" or "c06_delegation_authority"))
        throw new InvalidOperationException();
    return Convert.ToInt64(await Scalar("SELECT COUNT(*) FROM "+table));
}
int checks=0;
void Check(bool ok,string label)
{
    if(!ok)throw new InvalidOperationException("C06-11 FLOW FAIL "+label);
    checks++;Console.WriteLine("PASS C06-11 FLOW "+label);
}
var termsHash=SHA256.HashData(Encoding.UTF8.GetBytes("Synthetic delegation terms"));
await Exec("""
    INSERT INTO c04_verified_tenant_host
    VALUES('delegation-a.example.test',@t,2,2,1,UTC_TIMESTAMP(6)-INTERVAL 1 MINUTE,
           UTC_TIMESTAMP(6)-INTERVAL 1 MINUTE,UTC_TIMESTAMP(6)+INTERVAL 1 DAY),
          ('delegation-b.example.test',@u,2,2,1,UTC_TIMESTAMP(6)-INTERVAL 1 MINUTE,
           UTC_TIMESTAMP(6)-INTERVAL 1 MINUTE,UTC_TIMESTAMP(6)+INTERVAL 1 DAY)
    """,("t",tenant),("u",foreign));
await Exec("""
    INSERT INTO c05_identity_mfa_factor
    (account_id,factor_id,factor_kind,protected_secret,secret_nonce,secret_tag,key_version,factor_state,enrolled_at_utc)
    VALUES(@a,@f,1,@cipher,@nonce,@tag,1,1,UTC_TIMESTAMP(6)-INTERVAL 2 MINUTE)
    """,("a",actor),("f",factor),("cipher",ciphertext),("nonce",nonce),("tag",tag));
foreach(var pair in new[]{(session,token),(session2,token2)})
    await Exec("""
        INSERT INTO c05_identity_session
        (tenant_id,session_id,account_id,membership_id,token_hmac,session_family,mfa_factor_id,
         mfa_verified_at_utc,issued_at_utc,expires_at_utc,last_seen_at_utc,auth_method)
        VALUES(@t,@s,@a,@m,@h,@s,@f,UTC_TIMESTAMP(6)-INTERVAL 2 MINUTE,
            UTC_TIMESTAMP(6)-INTERVAL 2 MINUTE,UTC_TIMESTAMP(6)+INTERVAL 25 MINUTE,
            UTC_TIMESTAMP(6)-INTERVAL 1 MINUTE,'TOTP')
        """,("t",tenant),("s",pair.Item1),("a",actor),("m",member),("h",SessionHash(pair.Item2)),("f",factor));
// Actual locked recipient sessions; neither recipient has a direct read grant.
foreach(var reader in new[]{(Account:subject,Membership:Key(Id("44")),Token:subjectToken),
                             (Account:other,Membership:Key(Id("42")),Token:otherToken)})
{
    var fid=Key(Guid.NewGuid());var sid=Key(Guid.NewGuid());
    var localSeed=TotpSha1Verifier.GenerateSeed();
    var n=RandomNumberGenerator.GetBytes(12);var cipher=new byte[localSeed.Length];var t=new byte[16];
    try
    {
        using(var aes=new AesGcm(encKey,16))
            aes.Encrypt(n,localSeed,cipher,t,Encoding.UTF8.GetBytes("C05/operational/v1/factor:"+Hex(reader.Account)+Hex(fid)));
        await Exec("""
            INSERT INTO c05_identity_mfa_factor
            (account_id,factor_id,factor_kind,protected_secret,secret_nonce,secret_tag,key_version,factor_state,enrolled_at_utc)
            VALUES(@a,@f,1,@c,@n,@tag,1,1,UTC_TIMESTAMP(6)-INTERVAL 2 MINUTE)
            """,("a",reader.Account),("f",fid),("c",cipher),("n",n),("tag",t));
        await Exec("""
            INSERT INTO c05_identity_session
            (tenant_id,session_id,account_id,membership_id,token_hmac,session_family,mfa_factor_id,
             mfa_verified_at_utc,issued_at_utc,expires_at_utc,last_seen_at_utc,auth_method)
            VALUES(@t,@s,@a,@m,@h,@s,@f,UTC_TIMESTAMP(6)-INTERVAL 2 MINUTE,
                UTC_TIMESTAMP(6)-INTERVAL 2 MINUTE,UTC_TIMESTAMP(6)+INTERVAL 25 MINUTE,
                UTC_TIMESTAMP(6)-INTERVAL 1 MINUTE,'TOTP')
            """,("t",tenant),("s",sid),("a",reader.Account),("m",reader.Membership),
            ("h",SessionHash(reader.Token)),("f",fid));
    }
    finally {CryptographicOperations.ZeroMemory(localSeed);CryptographicOperations.ZeroMemory(cipher);}
}
await Exec("""
    INSERT INTO c05_identity_terms(tenant_id,terms_version,content_text,content_hash,published_at_utc,active)
    VALUES(@t,'delegation-v1','Synthetic delegation terms',@h,UTC_TIMESTAMP(6)-INTERVAL 1 MINUTE,1)
    """,("t",tenant),("h",termsHash));
var now=new DateTimeOffset(DateTime.SpecifyKind((DateTime)(await Scalar("SELECT UTC_TIMESTAMP(6)"))!,DateTimeKind.Utc));
var expiry=now.AddMinutes(20).ToString("O",CultureInfo.InvariantCulture);
var fields=new Dictionary<string,string>(StringComparer.Ordinal)
{
    ["command"]="assign",["branchId"]=Id("71").ToString("D"),["resourceId"]=Id("73").ToString("D"),
    ["membershipId"]=Id("44").ToString("D"),["roleId"]=Id("61").ToString("D"),
    ["roleRevision"]="1",["membershipRevision"]="1",["ceilingRevision"]="1",["grantRevision"]="0",
    ["expiresAtUtc"]=expiry,["reason"]="Synthetic exact resource delegation"
};
Dictionary<string,string> Change(Dictionary<string,string> src,string name,string value)
{
    var copy=new Dictionary<string,string>(src,StringComparer.Ordinal);copy[name]=value;return copy;
}
var crypto=new OperationalIdentityCrypto(new Argon2idPasswordHasher(PasswordHashingPolicy.Default,
    new KnownCompromisedPasswordScreen()));
using var flow=new MySqlOperationalIdentityFlow(runtime.ConnectionString,tokenKey,encKey,loginKey,
    crypto,new OperationalPasskeyCrypto(),mailboxKey);
// Same production flow, with a disposable SQL principal lacking the
// final provenance INSERT permission. A real late failure must roll back.
using var rollbackFlow=new MySqlOperationalIdentityFlow(rollbackRuntime.ConnectionString,tokenKey,encKey,loginKey,
    crypto,new OperationalPasskeyCrypto(),mailboxKey);
Task<OperationalIdentityResult> Call(string op,Dictionary<string,string> f,string? bearer=null,
    string host="delegation-a.example.test") =>
    flow.ExecuteAsync(new(host,"::ffff:127.0.0.1",op,f,bearer??token),CancellationToken.None);
async Task<C06DelegationPreparation> Prepare(Dictionary<string,string> f)
{
    var result=await Call("delegation/prepare",f);
    Check(result.Allowed && result.Value is C06DelegationPreparation,"actual locked preparation");
    return (C06DelegationPreparation)result.Value!;
}
Dictionary<string,string> Confirm(Dictionary<string,string> f,C06DelegationPreparation p,string code)=>
    new(f,StringComparer.Ordinal){{"challenge",p.Challenge},{"code",code}};
async Task<bool> Denied(string op,Dictionary<string,string> f,string? bearer=null,string host="delegation-a.example.test")=>
    !(await Call(op,f,bearer,host)).Allowed;

try
{
    Environment.SetEnvironmentVariable("CRECISOL_C06_DELEGATION_LAB_ENABLED",null);
    Check(await Denied("delegation/prepare",fields),"disabled composition denied");
    Environment.SetEnvironmentVariable("CRECISOL_C06_DELEGATION_LAB_ENABLED","true");
    Environment.SetEnvironmentVariable("CRECISOL_C06_PRIVACY_REQUESTS_LAB_ENABLED","true");
    await using var wire=await DelegationWireFixture.Start(runtime.ConnectionString,flow,tokenKey,encKey,loginKey,mailboxKey);
    async Task WireDenied(string operation,Dictionary<string,string> body,HttpStatusCode status,
        string? bearer=null,string host="delegation-a.example.test",string? raw=null,
        string? extraHeader=null,string? origin=null,bool browser=false,bool csrf=true)
    {
        using var response=await wire.Send(operation,body,bearer,host,raw,extraHeader,origin,browser,csrf);
        Check(response.StatusCode==status && (await response.Content.ReadAsByteArrayAsync()).Length==0 &&
            response.Headers.CacheControl is {NoStore:true,Private:true} && !response.Headers.Contains("Set-Cookie"),
            "HTTP neutral "+operation+" "+(int)status);
    }
    async Task<JsonElement> WireAllowed(string operation,Dictionary<string,string> body,string bearer,
        string[] exactFields,bool browser=false)
    {
        using var response=await wire.Send(operation,body,bearer,browser:browser);
        Check(response.StatusCode==HttpStatusCode.OK && response.Headers.CacheControl is {NoStore:true,Private:true},
            "HTTP allowed private "+operation);
        Check(response.Headers.Contains("Set-Cookie")==browser,"HTTP cookie isolation "+operation);
        if(browser)
        {
            var cookie=response.Headers.GetValues("Set-Cookie").Single();
            Check(cookie.Contains("httponly",StringComparison.OrdinalIgnoreCase) &&
                cookie.Contains("secure",StringComparison.OrdinalIgnoreCase) &&
                cookie.Contains("samesite=strict",StringComparison.OrdinalIgnoreCase),"HTTP browser secure cookie");
        }
        using var json=JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync());
        Check(json.RootElement.ValueKind==JsonValueKind.Object &&
            json.RootElement.EnumerateObject().Select(p=>p.Name).Order().SequenceEqual(exactFields.Order()),
            "HTTP exact response "+operation);
        return json.RootElement.Clone();
    }
    Check(await Denied("delegation/prepare",fields),"terms acceptance required");
    await Exec("INSERT INTO c05_identity_terms_acceptance VALUES(@t,@a,'delegation-v1',@h,UTC_TIMESTAMP(6))",
        ("t",tenant),("a",actor),("h",termsHash));
    foreach(var reader in new[]{subject,other})
        await Exec("INSERT INTO c05_identity_terms_acceptance VALUES(@t,@a,'delegation-v1',@h,UTC_TIMESTAMP(6))",
            ("t",tenant),("a",reader),("h",termsHash));
    var readFields=new Dictionary<string,string>{{"branchId",Id("71").ToString("D")},{"resourceId",Id("73").ToString("D")}};
    var inspectFields=new Dictionary<string,string>(readFields)
    {
        ["membershipId"]=fields["membershipId"],["roleId"]=fields["roleId"]
    };
    string[] inspectionShape=["roleCode","roleRevision","membershipRevision","ceilingRevision","grantRevision",
        "canAssign","canRevoke","maxExpiresAtUtc"];
    Check(await Denied("delegation/inspect",inspectFields),"inspection cannot bypass missing entitlement");
    Check(await Denied("delegation/read",readFields,subjectToken),"no delegation no private read");
    Check(await Denied("delegation/prepare",fields),"missing commercial laboratory evidence denied");
    await Exec("""
        INSERT INTO c06_delegation_lab_entitlement VALUES(@t,1,1,UTC_TIMESTAMP(6)-INTERVAL 1 MINUTE,
            UTC_TIMESTAMP(6)+INTERVAL 1 HOUR)
        """,("t",tenant));
    Check(await Denied("delegation/prepare",fields),"role and ceiling without own read denied");
    Check(await Denied("delegation/inspect",inspectFields),"inspection cannot bypass missing own authority");
    await Exec("""
        INSERT INTO c05_identity_resource_grant
        (tenant_id,grant_id,account_id,branch_id,resource_id,scope_kind,permission_code,granted_by,
         granted_at_utc,expires_at_utc)
        VALUES(@t,@g,@a,@b,NULL,2,'RESOURCE_READ',@a,UTC_TIMESTAMP(6)-INTERVAL 1 MINUTE,
               UTC_TIMESTAMP(6)+INTERVAL 1 HOUR)
        """,("t",tenant),("g",sourceGrant),("a",actor),("b",Key(Id("71"))));
    Check(await Denied("delegation/prepare",fields,host:"delegation-b.example.test"),"foreign host session denied");
    Check(await Denied("delegation/prepare",fields,Hex(RandomNumberGenerator.GetBytes(32))),"forged bearer denied");
    Check(await Denied("delegation/prepare",Change(fields,"membershipId",Id("43").ToString("D"))),"foreign subject denied");
    Check(await Denied("delegation/prepare",Change(fields,"branchId",Id("72").ToString("D"))),"wrong branch denied");
    Check(await Denied("delegation/prepare",Change(fields,"roleRevision","2")),"stale role revision denied");
    var offered=await WireAllowed("delegation/inspect",inspectFields,token,inspectionShape,browser:true);
    Check(offered.GetProperty("roleCode").GetString()=="tenant-staff" &&
        offered.GetProperty("roleRevision").GetString()=="1" &&
        offered.GetProperty("membershipRevision").GetString()=="1" &&
        offered.GetProperty("ceilingRevision").GetString()=="1" &&
        offered.GetProperty("grantRevision").GetString()=="0" &&
        offered.GetProperty("canAssign").GetBoolean() && !offered.GetProperty("canRevoke").GetBoolean() &&
        offered.GetProperty("maxExpiresAtUtc").GetDateTimeOffset()>now.AddMinutes(20),
        "inspection returns only server derived assign preconditions and bound");
    Check(await Count("c06_delegation_confirmation")==0 && await Count("c06_delegation_grant")==0 &&
        await Scalar("SELECT last_accepted_step FROM c05_identity_mfa_factor WHERE account_id=@a AND factor_id=@f",
            ("a",actor),("f",factor)) is DBNull,"inspection creates no authority or MFA proof");
    Check(await Count("c06_delegation_confirmation")==0,"initial denials create no confirmation");
    var catalogFields=new Dictionary<string,string>{{"cursor","start"}};
    var catalog=await WireAllowed("delegation/catalog",catalogFields,token,["items","nextCursor"],browser:true);
    var entries=catalog.GetProperty("items").EnumerateArray().ToArray();
    var continuation=catalog.GetProperty("nextCursor").GetString()!;
    Check(entries.Length==8 && continuation.Length==136,"catalog bounded first page and opaque cursor");
    var nextPage=await WireAllowed("delegation/catalog",Change(catalogFields,"cursor",continuation),token,["items","nextCursor"]);
    var nextEntries=nextPage.GetProperty("items").EnumerateArray().ToArray();
    Check(nextEntries.Length==2 && nextPage.GetProperty("nextCursor").ValueKind==JsonValueKind.Null,
        "catalog stable keyset final page");
    var allEntries=entries.Concat(nextEntries).ToArray();
    Check(allEntries.Select(x=>x.GetProperty("membershipId").GetGuid()).Distinct().Count()==10 &&
        allEntries.All(x=>x.GetProperty("branchId").GetGuid()==Id("71") &&
            x.GetProperty("resourceId").GetGuid()==Id("73") && x.GetProperty("roleId").GetGuid()==Id("61") &&
            x.EnumerateObject().Select(p=>p.Name).Order().SequenceEqual(
                new[]{"branchId","resourceId","membershipId","roleId","inspection"}.Order()) &&
            x.GetProperty("inspection").EnumerateObject().Select(p=>p.Name).Order().SequenceEqual(inspectionShape.Order()) &&
            x.GetProperty("inspection").GetProperty("canAssign").GetBoolean()),
        "catalog only exact authorized unique selectors and closed inspection");
    await WireDenied("delegation/catalog",Change(catalogFields,"cursor",continuation),HttpStatusCode.Unauthorized,token2);
    var tampered=(continuation[0]=='A'?'B':'A')+continuation[1..];
    await WireDenied("delegation/catalog",Change(catalogFields,"cursor",tampered),HttpStatusCode.Unauthorized,token);
    Check(await Count("c06_delegation_confirmation")==0 && await Count("c06_delegation_grant")==0,
        "catalog creates no authority");
    var stale=await Prepare(fields);
    await Exec("UPDATE c06_delegation_lab_entitlement SET revision=2,enabled=0 WHERE tenant_id=@t",("t",tenant));
    Check(await Denied("delegation/confirm",Confirm(fields,stale,"000000")),"suspended entitlement denied");
    await Exec("UPDATE c06_delegation_lab_entitlement SET revision=3,enabled=1 WHERE tenant_id=@t",("t",tenant));
    Check(await Denied("delegation/confirm",Confirm(fields,stale,"000000")),"changed entitlement invalidates confirmation");
    await WireDenied("delegation/catalog",Change(catalogFields,"cursor",continuation),HttpStatusCode.Unauthorized,token);
    // Real HTTP negative matrix for all three operations; no authority from
    // caller-provided tenant/header/extra property or malformed JSON.
    var confirmShape=Confirm(fields,stale,"000000");
    foreach(var entry in new[]{(Op:"delegation/prepare",Body:fields),(Op:"delegation/confirm",Body:confirmShape),
                               (Op:"delegation/read",Body:readFields)})
    {
        await WireDenied(entry.Op,entry.Body,HttpStatusCode.Unauthorized);
        await WireDenied(entry.Op,entry.Body,HttpStatusCode.Unauthorized,Hex(RandomNumberGenerator.GetBytes(32)));
        await WireDenied(entry.Op,entry.Body,HttpStatusCode.Unauthorized,token,"delegation-b.example.test");
        await WireDenied(entry.Op,entry.Body,HttpStatusCode.BadRequest,token,extraHeader:"X-Tenant-Id");
        await WireDenied(entry.Op,Change(entry.Body,"tenantId",Id("22").ToString("D")),HttpStatusCode.BadRequest,token);
        var firstField=entry.Body.First();
        var duplicate=JsonSerializer.Serialize(entry.Body).TrimEnd('}')+","+
            JsonSerializer.Serialize(firstField.Key)+":"+JsonSerializer.Serialize(firstField.Value)+"}";
        await WireDenied(entry.Op,entry.Body,HttpStatusCode.BadRequest,token,raw:duplicate);
        await WireDenied(entry.Op,entry.Body,HttpStatusCode.BadRequest,token,origin:"https://untrusted.example.test");
        await WireDenied(entry.Op,entry.Body,HttpStatusCode.BadRequest,token,browser:true,csrf:false);
    }
    Check(await Count("c06_delegation_confirmation")==1 && await Count("c06_delegation_grant")==0,
        "HTTP negative matrix leaves authority unchanged");
    var receipt=await WireAllowed("delegation/prepare",fields,token,["challenge","expiresAtUtc"],browser:true);
    var prepared=new C06DelegationPreparation(receipt.GetProperty("challenge").GetString()!,
        receipt.GetProperty("expiresAtUtc").GetDateTimeOffset());
    Check(prepared.Challenge.Length==64 && prepared.ExpiresAtUtc>now &&
        prepared.ExpiresAtUtc<=now.AddMinutes(6),"bounded opaque confirmation");
    Check(await Denied("delegation/confirm",Confirm(fields,prepared,"000000"),token2),"confirmation bound to actual session ID");
    Check(await Denied("delegation/confirm",Confirm(Change(fields,"reason","Different synthetic reason"),prepared,"000000")),
        "confirmation bound to reason");
    Check(await Denied("delegation/confirm",Confirm(Change(fields,"grantRevision","1"),prepared,"000000")),
        "confirmation bound to exact transition");
    // Choose the previous currently accepted RFC6238 step, then the current step
    // for revocation. No waiting for a real user's authenticator.
    var phase=DateTimeOffset.UtcNow.ToUnixTimeSeconds()%30;
    if(phase>=18)await Task.Delay(TimeSpan.FromSeconds(31-phase));
    var step=TotpSha1Verifier.StepAt(DateTimeOffset.UtcNow);
    var first=TotpSha1Verifier.ComputeCode(seed,step-1);
    var second=TotpSha1Verifier.ComputeCode(seed,step);
    var wrong=Enumerable.Range(0,10).Select(x=>x.ToString("D6",CultureInfo.InvariantCulture))
        .First(x=>x!=first && x!=second);
    Check(await Denied("delegation/confirm",Confirm(fields,prepared,wrong)),"actual wrong TOTP denied");
    Check(await Count("c06_delegation_grant")==0 && await Count("c06_delegation_revocation")==0,
        "denials preserve authorization ledger");
    Check(await Scalar("SELECT last_accepted_step FROM c05_identity_mfa_factor WHERE account_id=@a AND factor_id=@f",
        ("a",actor),("f",factor)) is DBNull,"failed confirmations do not consume TOTP");
    var successAuditsBefore=Convert.ToInt64(await Scalar(
        "SELECT COUNT(*) FROM c05_identity_audit WHERE tenant_id=@t AND action_code='delegation/confirm' AND outcome_code=1",("t",tenant)));
    bool provenanceDenied=false;
    try
    {
        await rollbackFlow.ExecuteAsync(new("delegation-a.example.test","::ffff:127.0.0.1",
            "delegation/confirm",Confirm(fields,prepared,first),token),CancellationToken.None);
    }
    catch(MySqlException ex) when(ex.Number==1142 &&
        ex.Message.Contains("INSERT",StringComparison.OrdinalIgnoreCase) &&
        ex.Message.Contains("c06_delegation_authority",StringComparison.Ordinal))
    { provenanceDenied=true; }
    Check(provenanceDenied,"real SQL provenance insert permission failure");
    Check(await Count("c06_delegation_grant")==0 && await Count("c06_delegation_authority")==0 &&
        await Count("c06_delegation_revocation")==0,"failed provenance rolls back grant and events");
    Check(await Scalar("SELECT last_accepted_step FROM c05_identity_mfa_factor WHERE account_id=@a AND factor_id=@f",
        ("a",actor),("f",factor)) is DBNull,"failed provenance rolls back actual MFA consumption");
    Check(Convert.ToInt64(await Scalar("SELECT COUNT(*) FROM c06_delegation_confirmation WHERE consumed_at_utc IS NOT NULL"))==0,
        "failed provenance rolls back challenge consumption");
    Check(Convert.ToInt64(await Scalar(
        "SELECT COUNT(*) FROM c05_identity_audit WHERE tenant_id=@t AND action_code='delegation/confirm' AND outcome_code=1",
        ("t",tenant)))==successAuditsBefore,"failed provenance commits no success audit");
    var issued=await WireAllowed("delegation/confirm",Confirm(fields,prepared,first),token,["accepted"]);
    Check(issued.GetProperty("accepted").GetBoolean() && await Count("c06_delegation_grant")==1,
        "real SHA1 TOTP and issuance commit together");
    Check(Convert.ToInt64(await Scalar("SELECT last_accepted_step FROM c05_identity_mfa_factor WHERE account_id=@a AND factor_id=@f",
        ("a",actor),("f",factor)))==step-1,"real monotonic MFA counter committed");
    Check(Convert.ToInt64(await Scalar("SELECT COUNT(*) FROM c06_delegation_confirmation WHERE consumed_at_utc IS NOT NULL"))==1,
        "one confirmation consumed");
    Check(await Denied("delegation/confirm",Confirm(fields,prepared,first)),"confirmed challenge cannot replay");
    Check(await Count("c06_delegation_authority")==1,"issuance provenance committed with MFA");
    var delegated=await Call("delegation/read",readFields,subjectToken);
    Check(delegated.Allowed && System.Text.Json.JsonSerializer.Serialize(delegated.Value)=="{\"reference\":\"SYNTHETIC-A\"}",
        "real recipient non-owner reads exact authorized reference");
    var wireRead=await WireAllowed("delegation/read",readFields,subjectToken,["reference"],browser:true);
    Check(wireRead.GetProperty("reference").GetString()=="SYNTHETIC-A","HTTP recipient exact reference");
    var revokeOffer=await WireAllowed("delegation/inspect",inspectFields,token,inspectionShape);
    Check(!revokeOffer.GetProperty("canAssign").GetBoolean() && revokeOffer.GetProperty("canRevoke").GetBoolean() &&
        revokeOffer.GetProperty("grantRevision").GetString()=="1" &&
        revokeOffer.GetProperty("maxExpiresAtUtc").ValueKind==JsonValueKind.Null,
        "inspection returns revoke only for existing exact grant");
    await WireDenied("delegation/read",readFields,HttpStatusCode.Unauthorized,otherToken);
    await WireDenied("delegation/read",readFields,HttpStatusCode.Unauthorized,subjectToken,"delegation-b.example.test");
    // Pure closed-projection negatives are independent of the live wire test.
    var denial=new OperationalIdentityResult(false,new {reference="SYNTHETIC-A",secret="forbidden"});
    Check(C06DelegationHttpContract.TryApply("delegation/read",denial,out var safeDenial) &&
        !safeDenial.Allowed && safeDenial.Value is null,"HTTP projection denial strips arbitrary payload");
    var transportWindow=new OperationalIdentityResult(true,new {reference="SYNTHETIC-A",secret="forbidden"})
        {SessionIdleMinutes=30,SessionExpiresAtUtc=DateTimeOffset.UtcNow.AddMinutes(20)};
    Check(!C06DelegationHttpContract.TryApply("delegation/read",transportWindow,out _),"HTTP projection rejects extra private fields");
    Check(!C06DelegationHttpContract.TryApply("delegation/confirm",transportWindow with {Value=null},out _),
        "HTTP projection rejects null success acknowledgement");
    Check(!C06DelegationHttpContract.TryApply("delegation/prepare",transportWindow with
        {Value=new C06DelegationPreparation(new string('A',64),DateTimeOffset.UtcNow.AddMinutes(10))},out _),
        "HTTP projection rejects excessive confirmation expiry");

    Check(await Denied("private/read",readFields,subjectToken),"historic private read does not activate candidate");
    Check(await Denied("delegation/read",readFields,otherToken),"another member cannot borrow delegated authority");
    Check(await Denied("delegation/read",readFields,subjectToken,"delegation-b.example.test"),"colliding foreign tenant denied");
    Check(await Denied("delegation/read",Change(readFields,"resourceId",Id("74").ToString("D")),subjectToken),"different resource denied");
    var wrongBranch=Change(Change(readFields,"branchId",Id("72").ToString("D")),"resourceId",Id("74").ToString("D"));
    Check(await Denied("delegation/read",wrongBranch,subjectToken),"other branch and own resource do not inherit delegated access");
    Check(await Denied("delegation/read",Change(readFields,"tenantId",Id("22").ToString("D")),subjectToken),"injected tenant field denied");
    Environment.SetEnvironmentVariable("CRECISOL_C06_DELEGATION_LAB_ENABLED",null);
    Check(await Denied("delegation/read",readFields,subjectToken),"candidate disabled denies even active grant");
    await WireDenied("delegation/read",readFields,HttpStatusCode.NotFound,subjectToken);
    await WireDenied("delegation/prepare",fields,HttpStatusCode.NotFound,token);
    await WireDenied("delegation/confirm",confirmShape,HttpStatusCode.NotFound,token);
    Environment.SetEnvironmentVariable("CRECISOL_C06_DELEGATION_LAB_ENABLED","true");
    Check((await Call("delegation/read",readFields,subjectToken)).Allowed,"same live scope revalidated");

    // Independent committed fixture mutations; restore synthetic baseline
    // ONLY between cases, not a production reactivation or lifecycle API.
    async Task VerifyInvalidation(string label,string mutate,string restore,params (string,object)[] ps)
    {
        Check(await Exec(mutate,ps)==1,label+" fixture changed exactly one row");
        try
        {
            var denied=await Call("delegation/read",readFields,subjectToken);
            Check(!denied.Allowed && denied.Value is null,label+" same-session read denied without payload");
            Check(await Count("c06_delegation_grant")==1 && await Count("c06_delegation_authority")==1 &&
                await Count("c06_delegation_revocation")==0,label+" immutable events preserved");
        }
        finally
        {
            if(await Exec(restore,ps)!=1)
                throw new InvalidOperationException("C06-11 fixture baseline restoration failed: "+label);
        }
        Check((await Call("delegation/read",readFields,subjectToken)).Allowed,label+" isolated baseline verified");
    }
    await VerifyInvalidation("branch inactive",
        "UPDATE c05_identity_branch SET active=0 WHERE tenant_id=@t AND branch_id=@b",
        "UPDATE c05_identity_branch SET active=1 WHERE tenant_id=@t AND branch_id=@b",
        ("t",tenant),("b",Key(Id("71"))));
    await VerifyInvalidation("resource inactive",
        "UPDATE c05_identity_private_resource SET resource_state=0 WHERE tenant_id=@t AND branch_id=@b AND resource_id=@r",
        "UPDATE c05_identity_private_resource SET resource_state=1 WHERE tenant_id=@t AND branch_id=@b AND resource_id=@r",
        ("t",tenant),("b",Key(Id("71"))),("r",Key(Id("73"))));
    await VerifyInvalidation("resource ownership changed",
        "UPDATE c05_identity_private_resource SET owner_account_id=@newOwner WHERE tenant_id=@t AND branch_id=@b AND resource_id=@r",
        "UPDATE c05_identity_private_resource SET owner_account_id=@originalOwner WHERE tenant_id=@t AND branch_id=@b AND resource_id=@r",
        ("t",tenant),("b",Key(Id("71"))),("r",Key(Id("73"))),("newOwner",actor),("originalOwner",other));
    await VerifyInvalidation("issuer account revision changed",
        "UPDATE c05_identity_account SET security_revision=security_revision+1 WHERE account_id=@a",
        "UPDATE c05_identity_account SET security_revision=security_revision-1 WHERE account_id=@a",
        ("a",actor));
    await VerifyInvalidation("issuer membership revision changed",
        "UPDATE c05_identity_membership SET approval_revision=approval_revision+1 WHERE tenant_id=@t AND membership_id=@m",
        "UPDATE c05_identity_membership SET approval_revision=approval_revision-1 WHERE tenant_id=@t AND membership_id=@m",
        ("t",tenant),("m",member));
    await VerifyInvalidation("original source revision changed",
        "UPDATE c05_identity_resource_grant SET grant_revision=grant_revision+1 WHERE tenant_id=@t AND grant_id=@g",
        "UPDATE c05_identity_resource_grant SET grant_revision=grant_revision-1 WHERE tenant_id=@t AND grant_id=@g",
        ("t",tenant),("g",sourceGrant));
    await VerifyInvalidation("original source revoked",
        "UPDATE c05_identity_resource_grant SET revoked_at_utc=UTC_TIMESTAMP(6) WHERE tenant_id=@t AND grant_id=@g",
        "UPDATE c05_identity_resource_grant SET revoked_at_utc=NULL WHERE tenant_id=@t AND grant_id=@g",
        ("t",tenant),("g",sourceGrant));


    var revoke=Change(Change(Change(fields,"command","revoke"),"grantRevision","1"),"expiresAtUtc","none");
    var revokeProof=await Prepare(revoke);
    Check(await Denied("delegation/confirm",Confirm(revoke,revokeProof,first)),"same TOTP cannot authorize another intent");
    Check(await Count("c06_delegation_revocation")==0,"replayed MFA cannot revoke");
    if(TotpSha1Verifier.StepAt(DateTimeOffset.UtcNow)>step)
        second=TotpSha1Verifier.ComputeCode(seed,TotpSha1Verifier.StepAt(DateTimeOffset.UtcNow));
    var revokedWire=await WireAllowed("delegation/confirm",Confirm(revoke,revokeProof,second),token,["accepted"],browser:true);
    Check(revokedWire.GetProperty("accepted").GetBoolean(),"fresh real TOTP commits exact revocation");
    Check(await Count("c06_delegation_grant")==1 && await Count("c06_delegation_revocation")==1,
        "immutable grant and revocation retained");
    Check(await Denied("delegation/confirm",Confirm(revoke,revokeProof,second)),"revocation replay denied");
    Check(await Denied("delegation/read",readFields,subjectToken),"committed revocation denies next read using same session");
    await WireDenied("delegation/read",readFields,HttpStatusCode.Unauthorized,subjectToken,browser:true);
    Check(await Count("c06_delegation_authority")==1,"revocation preserves immutable provenance");

    Check(await Denied("delegation/prepare",fields),"revoked scope cannot be reinstated");
    Check(Convert.ToInt64(await Scalar("SELECT COUNT(*) FROM c05_identity_resource_grant"))==1,
        "legacy grants unchanged");
    Check(Convert.ToInt64(await Scalar("SELECT COUNT(*) FROM c05_identity_private_resource WHERE display_reference IN ('SYNTHETIC-A','SYNTHETIC-B','SYNTHETIC-C')"))==3,
        "original private resources unchanged");
    Check(Convert.ToInt64(await Scalar("SELECT COUNT(*) FROM c06_delegation_grant WHERE tenant_id=@u",("u",foreign)))==0,
        "no foreign grant");
    Check(Convert.ToInt64(await Scalar("SELECT COUNT(*) FROM c05_identity_audit WHERE tenant_id=@t AND action_code='delegation/confirm' AND outcome_code=1",("t",tenant)))==2,
        "C05 audit committed with both confirmed transitions");
    // Second actual grant, distinct recipient; suspension and resumption must
    // invalidate its revision permanently. Never mutate a live grant to test it.
    var otherFields=Change(fields,"membershipId",Id("42").ToString("D"));
    var otherProof=await Prepare(otherFields);
    var accepted=Convert.ToInt64(await Scalar("SELECT last_accepted_step FROM c05_identity_mfa_factor WHERE account_id=@a AND factor_id=@f",
        ("a",actor),("f",factor)));
    while(TotpSha1Verifier.StepAt(DateTimeOffset.UtcNow)<=accepted)
        await Task.Delay(TimeSpan.FromMilliseconds(500));
    var fresh=TotpSha1Verifier.ComputeCode(seed,TotpSha1Verifier.StepAt(DateTimeOffset.UtcNow));
    Check((await Call("delegation/confirm",Confirm(otherFields,otherProof,fresh))).Allowed,"second independent confirmed grant");
    Check((await Call("delegation/read",readFields,otherToken)).Allowed,"second exact grant is effective");
    await Exec("UPDATE c06_delegation_lab_entitlement SET revision=4,enabled=0 WHERE tenant_id=@t",("t",tenant));
    Check(await Denied("delegation/read",readFields,otherToken),"suspended commercial authority denies next read");
    await Exec("UPDATE c06_delegation_lab_entitlement SET revision=5,enabled=1 WHERE tenant_id=@t",("t",tenant));
    Check(await Denied("delegation/read",readFields,otherToken),"new entitlement revision cannot revive prior grant");
    Check(await Count("c06_delegation_grant")==2 && await Count("c06_delegation_authority")==2 &&
        await Count("c06_delegation_revocation")==1,"invalidation preserves grant and provenance history");
    // Preserve the real C05 quota. Additional cases use a new natural
    // window, not deletion of quota rows or increased runtime limits.
    var nextWindow=DateTimeOffset.UtcNow.ToUnixTimeSeconds()/60+1;
    while(DateTimeOffset.UtcNow.ToUnixTimeSeconds()/60<nextWindow)
        await Task.Delay(TimeSpan.FromMilliseconds(250));
    await WireDenied("delegation/inspect",inspectFields,HttpStatusCode.Unauthorized,token);
    var otherInspect=Change(inspectFields,"membershipId",Id("42").ToString("D"));
    await WireDenied("delegation/inspect",otherInspect,HttpStatusCode.Unauthorized);
    await WireDenied("delegation/inspect",otherInspect,HttpStatusCode.Unauthorized,Hex(RandomNumberGenerator.GetBytes(32)));
    await WireDenied("delegation/inspect",otherInspect,HttpStatusCode.Unauthorized,token,"delegation-b.example.test");
    await WireDenied("delegation/inspect",otherInspect,HttpStatusCode.BadRequest,token,extraHeader:"X-Tenant-Id");
    await WireDenied("delegation/inspect",Change(otherInspect,"tenantId",Id("22").ToString("D")),HttpStatusCode.BadRequest,token);
    var duplicateInspection=JsonSerializer.Serialize(otherInspect).TrimEnd('}')+","+
        JsonSerializer.Serialize("roleId")+":"+JsonSerializer.Serialize(otherInspect["roleId"])+"}";
    await WireDenied("delegation/inspect",otherInspect,HttpStatusCode.BadRequest,token,raw:duplicateInspection);
    await WireDenied("delegation/inspect",otherInspect,HttpStatusCode.BadRequest,token,origin:"https://untrusted.example.test");
    await WireDenied("delegation/inspect",otherInspect,HttpStatusCode.BadRequest,token,browser:true,csrf:false);
    await WireDenied("delegation/inspect",otherInspect,HttpStatusCode.Unauthorized,otherToken);
    await WireDenied("delegation/inspect",Change(otherInspect,"membershipId",Id("43").ToString("D")),HttpStatusCode.Unauthorized,token);
    await WireDenied("delegation/inspect",Change(otherInspect,"membershipId",Id("41").ToString("D")),HttpStatusCode.Unauthorized,token);
    await WireDenied("delegation/inspect",Change(otherInspect,"branchId",Id("72").ToString("D")),HttpStatusCode.Unauthorized,token);
    await WireDenied("delegation/inspect",Change(otherInspect,"roleId",Guid.NewGuid().ToString("D")),HttpStatusCode.Unauthorized,token);
    await WireDenied("delegation/inspect",Change(otherInspect,"grantRevision","1"),HttpStatusCode.BadRequest,token);
    var invalidInspection=new OperationalIdentityResult(true,
        new C06DelegationInspection("tenant-admin",1,1,1,0,true,false,DateTimeOffset.UtcNow.AddMinutes(1)))
        {SessionIdleMinutes=30,SessionExpiresAtUtc=DateTimeOffset.UtcNow.AddMinutes(20)};
    Check(!C06DelegationHttpContract.TryApply("delegation/inspect",invalidInspection,out _),"inspection projection rejects escalated role");
    Check(!C06DelegationHttpContract.TryApply("delegation/inspect",invalidInspection with
        {Value=new C06DelegationInspection("tenant-staff",1,1,1,0,true,true,DateTimeOffset.UtcNow.AddMinutes(1))},out _),
        "inspection projection rejects incompatible commands");
    Check(C06DelegationHttpContract.TryApply("delegation/inspect",invalidInspection with
        {Value=new C06DelegationInspection("tenant-staff",1,9007199254740993L,1,0,true,false,DateTimeOffset.UtcNow.AddMinutes(1))},out var precise) &&
        JsonSerializer.Serialize(precise.Value).Contains("\"membershipRevision\":\"9007199254740993\"",StringComparison.Ordinal),
        "inspection preserves BIGINT revision precision at HTTP boundary");
    Check(await Count("c06_delegation_grant")==2 && await Count("c06_delegation_revocation")==1 &&
        await Count("c06_delegation_authority")==2,"inspection denials preserve authorization history");
    await WireDenied("delegation/catalog",catalogFields,HttpStatusCode.Unauthorized);
    await WireDenied("delegation/catalog",catalogFields,HttpStatusCode.Unauthorized,Hex(RandomNumberGenerator.GetBytes(32)));
    await WireDenied("delegation/catalog",catalogFields,HttpStatusCode.Unauthorized,token,"delegation-b.example.test");
    await WireDenied("delegation/catalog",catalogFields,HttpStatusCode.BadRequest,token,extraHeader:"X-Tenant-Id");
    await WireDenied("delegation/catalog",Change(catalogFields,"tenantId",Id("22").ToString("D")),HttpStatusCode.BadRequest,token);
    await WireDenied("delegation/catalog",catalogFields,HttpStatusCode.BadRequest,token,raw:"{\"cursor\":\"start\",\"cursor\":\"start\"}");
    await WireDenied("delegation/catalog",catalogFields,HttpStatusCode.BadRequest,token,origin:"https://untrusted.example.test");
    await WireDenied("delegation/catalog",catalogFields,HttpStatusCode.BadRequest,token,browser:true,csrf:false);
    await WireDenied("delegation/catalog",catalogFields,HttpStatusCode.Unauthorized,otherToken);
    var pageAfter=await WireAllowed("delegation/catalog",catalogFields,token,["items","nextCursor"]);
    Check(pageAfter.GetProperty("items").EnumerateArray().All(x=>x.GetProperty("membershipId").GetGuid()!=Id("44")),
        "revoked issuance absent from actionable catalog");
    var invalidRow=new C06DelegationCatalogItem(Id("71"),Id("73"),Id("42"),Id("61"),
        new C06DelegationInspection("tenant-admin",1,1,1,0,true,false,DateTimeOffset.UtcNow.AddMinutes(1)));
    Check(!C06DelegationHttpContract.TryApply("delegation/catalog",invalidInspection with
        {Value=new C06DelegationCatalog([invalidRow],null)},out _),"catalog projection rejects escalated nested row");
    Check(await Count("c06_delegation_grant")==2 && await Count("c06_delegation_revocation")==1 &&
        await Count("c06_delegation_authority")==2,"catalog denials preserve authorization history");
    Console.WriteLine($"PASS C06-11 FLOW MYSQL checks={checks}; actual C05 session, SHA1 TOTP, one-use confirmation, isolated exact read and revocation");
    // Reset only time window naturally, never persisted quota or MFA counters.
    var privacyWindow=DateTimeOffset.UtcNow.ToUnixTimeSeconds()/60+1;
    while(DateTimeOffset.UtcNow.ToUnixTimeSeconds()/60<privacyWindow)
        await Task.Delay(TimeSpan.FromMilliseconds(250));
    checks+=await PrivacyRequestFixture.Run(wire,admin,tenant,actor,factor,seed,token,token2,otherToken);
    Console.WriteLine($"PASS C06-11 TOTAL HTTP MYSQL checks={checks}");


}
finally
{
    Environment.SetEnvironmentVariable("CRECISOL_C06_DELEGATION_LAB_ENABLED",null);
    Environment.SetEnvironmentVariable("CRECISOL_C06_PRIVACY_REQUESTS_LAB_ENABLED",null);
    foreach(var bytes in new[]{seed,tokenKey,encKey,loginKey,mailboxKey,ciphertext})CryptographicOperations.ZeroMemory(bytes);
}
