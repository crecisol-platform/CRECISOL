using System.Globalization;
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
if(fixture.Port!=runtime.Port) throw new InvalidOperationException("Fixture and runtime must share isolated database.");
await using var admin=new MySqlConnection(fixture.ConnectionString);
await admin.OpenAsync();
static Guid Id(string hex)=>new(Convert.FromHexString(string.Concat(Enumerable.Repeat(hex,16))),true);
static byte[] Key(Guid id)=>id.ToByteArray(true);
static string Hex(byte[] b)=>Convert.ToHexString(b);
var tenant=Key(Id("11"));var foreign=Key(Id("22"));
var actor=Key(Id("31"));var subject=Key(Id("34"));var other=Key(Id("32"));
var member=Key(Id("41"));
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
    Check(await Denied("delegation/prepare",fields),"terms acceptance required");
    await Exec("INSERT INTO c05_identity_terms_acceptance VALUES(@t,@a,'delegation-v1',@h,UTC_TIMESTAMP(6))",
        ("t",tenant),("a",actor),("h",termsHash));
    foreach(var reader in new[]{subject,other})
        await Exec("INSERT INTO c05_identity_terms_acceptance VALUES(@t,@a,'delegation-v1',@h,UTC_TIMESTAMP(6))",
            ("t",tenant),("a",reader),("h",termsHash));
    var readFields=new Dictionary<string,string>{{"branchId",Id("71").ToString("D")},{"resourceId",Id("73").ToString("D")}};
    Check(await Denied("delegation/read",readFields,subjectToken),"no delegation no private read");
    Check(await Denied("delegation/prepare",fields),"missing commercial laboratory evidence denied");
    await Exec("""
        INSERT INTO c06_delegation_lab_entitlement VALUES(@t,1,1,UTC_TIMESTAMP(6)-INTERVAL 1 MINUTE,
            UTC_TIMESTAMP(6)+INTERVAL 1 HOUR)
        """,("t",tenant));
    Check(await Denied("delegation/prepare",fields),"role and ceiling without own read denied");
    await Exec("""
        INSERT INTO c05_identity_resource_grant
        (tenant_id,grant_id,account_id,branch_id,resource_id,scope_kind,permission_code,granted_by,
         granted_at_utc,expires_at_utc)
        VALUES(@t,@g,@a,@b,NULL,2,'RESOURCE_READ',@a,UTC_TIMESTAMP(6)-INTERVAL 1 MINUTE,
               UTC_TIMESTAMP(6)+INTERVAL 1 HOUR)
        """,("t",tenant),("g",Key(Guid.NewGuid())),("a",actor),("b",Key(Id("71"))));
    Check(await Denied("delegation/prepare",fields,host:"delegation-b.example.test"),"foreign host session denied");
    Check(await Denied("delegation/prepare",fields,Hex(RandomNumberGenerator.GetBytes(32))),"forged bearer denied");
    Check(await Denied("delegation/prepare",Change(fields,"membershipId",Id("43").ToString("D"))),"foreign subject denied");
    Check(await Denied("delegation/prepare",Change(fields,"branchId",Id("72").ToString("D"))),"wrong branch denied");
    Check(await Denied("delegation/prepare",Change(fields,"roleRevision","2")),"stale role revision denied");
    Check(await Count("c06_delegation_confirmation")==0,"initial denials create no confirmation");
    var stale=await Prepare(fields);
    await Exec("UPDATE c06_delegation_lab_entitlement SET revision=2,enabled=0 WHERE tenant_id=@t",("t",tenant));
    Check(await Denied("delegation/confirm",Confirm(fields,stale,"000000")),"suspended entitlement denied");
    await Exec("UPDATE c06_delegation_lab_entitlement SET revision=3,enabled=1 WHERE tenant_id=@t",("t",tenant));
    Check(await Denied("delegation/confirm",Confirm(fields,stale,"000000")),"changed entitlement invalidates confirmation");
    var prepared=await Prepare(fields);
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
    if(phase>=24)await Task.Delay(TimeSpan.FromSeconds(31-phase));
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
    var issued=await Call("delegation/confirm",Confirm(fields,prepared,first));
    Check(issued.Allowed && await Count("c06_delegation_grant")==1,"real SHA1 TOTP and issuance commit together");
    Check(Convert.ToInt64(await Scalar("SELECT last_accepted_step FROM c05_identity_mfa_factor WHERE account_id=@a AND factor_id=@f",
        ("a",actor),("f",factor)))==step-1,"real monotonic MFA counter committed");
    Check(Convert.ToInt64(await Scalar("SELECT COUNT(*) FROM c06_delegation_confirmation WHERE consumed_at_utc IS NOT NULL"))==1,
        "one confirmation consumed");
    Check(await Denied("delegation/confirm",Confirm(fields,prepared,first)),"confirmed challenge cannot replay");
    Check(await Count("c06_delegation_authority")==1,"issuance provenance committed with MFA");
    var delegated=await Call("delegation/read",readFields,subjectToken);
    Check(delegated.Allowed && System.Text.Json.JsonSerializer.Serialize(delegated.Value)=="{\"reference\":\"SYNTHETIC-A\"}",
        "real recipient non-owner reads exact authorized reference");
    Check(await Denied("private/read",readFields,subjectToken),"historic private read does not activate candidate");
    Check(await Denied("delegation/read",readFields,otherToken),"another member cannot borrow delegated authority");
    Check(await Denied("delegation/read",readFields,subjectToken,"delegation-b.example.test"),"colliding foreign tenant denied");
    Check(await Denied("delegation/read",Change(readFields,"resourceId",Id("74").ToString("D")),subjectToken),"different resource denied");
    var wrongBranch=Change(Change(readFields,"branchId",Id("72").ToString("D")),"resourceId",Id("74").ToString("D"));
    Check(await Denied("delegation/read",wrongBranch,subjectToken),"other branch and own resource do not inherit delegated access");
    Check(await Denied("delegation/read",Change(readFields,"tenantId",Id("22").ToString("D")),subjectToken),"injected tenant field denied");
    Environment.SetEnvironmentVariable("CRECISOL_C06_DELEGATION_LAB_ENABLED",null);
    Check(await Denied("delegation/read",readFields,subjectToken),"candidate disabled denies even active grant");
    Environment.SetEnvironmentVariable("CRECISOL_C06_DELEGATION_LAB_ENABLED","true");
    Check((await Call("delegation/read",readFields,subjectToken)).Allowed,"same live scope revalidated");

    var revoke=Change(Change(Change(fields,"command","revoke"),"grantRevision","1"),"expiresAtUtc","none");
    var revokeProof=await Prepare(revoke);
    Check(await Denied("delegation/confirm",Confirm(revoke,revokeProof,first)),"same TOTP cannot authorize another intent");
    Check(await Count("c06_delegation_revocation")==0,"replayed MFA cannot revoke");
    if(TotpSha1Verifier.StepAt(DateTimeOffset.UtcNow)>step)
        second=TotpSha1Verifier.ComputeCode(seed,TotpSha1Verifier.StepAt(DateTimeOffset.UtcNow));
    Check((await Call("delegation/confirm",Confirm(revoke,revokeProof,second))).Allowed,
        "fresh real TOTP commits exact revocation");
    Check(await Count("c06_delegation_grant")==1 && await Count("c06_delegation_revocation")==1,
        "immutable grant and revocation retained");
    Check(await Denied("delegation/confirm",Confirm(revoke,revokeProof,second)),"revocation replay denied");
    Check(await Denied("delegation/read",readFields,subjectToken),"committed revocation denies next read using same session");
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
    Console.WriteLine($"PASS C06-11 FLOW MYSQL checks={checks}; actual C05 session, SHA1 TOTP, one-use confirmation, isolated exact read and revocation");

}
finally
{
    Environment.SetEnvironmentVariable("CRECISOL_C06_DELEGATION_LAB_ENABLED",null);
    foreach(var bytes in new[]{seed,tokenKey,encKey,loginKey,mailboxKey,ciphertext})CryptographicOperations.ZeroMemory(bytes);
}
