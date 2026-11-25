using System.Text.Json;
using Crecisol.Api.Contracts;
using Crecisol.Application.Identity;
using Crecisol.CredentialSecurity;

var count=0;
void Check(bool pass,string label)
{
    if (!pass) throw new InvalidOperationException("C06-07 FAIL "+label);
    count++;
}
var now=DateTimeOffset.UtcNow;
var id=Guid.NewGuid();
var token=new string('a',64);
var backup=new string('b',32);
var codesList=Enumerable.Repeat(backup,10).ToArray();
var seed=new string('A',32);
var uri="otpauth://totp/CRECISOL?secret="+seed;
var crypto=new OperationalPasskeyCrypto();
var assertionJson=JsonSerializer.Deserialize<JsonElement>(crypto.AssertionOptions());
var registrationJson=JsonSerializer.Deserialize<JsonElement>(crypto.RegistrationOptions(id.ToByteArray(true),[]));
var samples=new Dictionary<string,object?>
{
    ["ack"]=new { accepted=true },
    ["challenge"]=new { stepUpRequired=true, challenge=token, enrollmentRequired=false },
    ["enroll"]=new { secret=seed, uri },
    ["sessionWithCodes"]=new OperationalSessionReceipt(token,now,codesList),
    ["sessionOnly"]=new OperationalSessionReceipt(token,now),
    ["sessions"]=new OperationalSessionInventory(now,new[]{new OperationalSessionSummary(id,now,now,now,"API","TOTP")}),
    ["keepalive"]=new { expiresAtUtc=now,idleMinutes=30 },
    ["pending"]=new { requests=new[]{new { id=id.ToString("D"),requestedAtUtc=now,mailboxHint="ab•••@example.test" }},nextCursor=(string?)null },
    ["reference"]=new { reference="PRIVATE-REFERENCE" },
    ["assertion"]=new { challenge=token,publicKey=assertionJson },
    ["registration"]=new { challenge=token,publicKey=registrationJson },
    ["passkeys"]=new { passkeys=new[]{new { id,createdAtUtc=now }} },
    ["terms"]=new { version="1",text="Terms",hash=token },
    ["pairing"]=new { token,nonce=token,fingerprint=token[..12],expiresAtUtc=now },
    ["events"]=new { events=new[]{new { action=(object)"login",outcome=(object)(byte)1,atUtc=(object)now.UtcDateTime }} },
    ["platformLogin"]=new { challenge=token,stepUpRequired=true },
    ["platformSession"]=new OperationalPlatformValidation(now),
    ["platformSessions"]=new { sessions=new[]{new { id,issuedAtUtc=now.UtcDateTime,expiresAtUtc=now.UtcDateTime,lastSeenAtUtc=now.UtcDateTime,device="API" }} },
    ["sessionPolicy"]=new { idleMinutes=30,revision=2L,canManage=true },
    ["caseReceipt"]=new { caseId=id,createdAtUtc=now,readyAtUtc=(object?)null,expiresAtUtc=now,blocked=false,consumed=false },
    ["caseId"]=new { caseId=id },
    ["signals"]=new { signals=new[]{new { kind="ACCOUNT",count=4L }} },
    ["profile"]=new { DisplayName="Test",Language="es-HN",TimeZone="America/Tegucigalpa",hasPhoto=false },
    ["photo"]=new { png=Convert.ToBase64String(C06ProtectedContent.EncodePhoto(new byte[64*64*4])),mime="image/png",width=64,height=64 },
    ["share"]=new { share=token,expiresAtUtc=now },
    ["platformFactor"]=new { challenge=token,secret=seed,uri },
    ["codes"]=new { accepted=true,recoveryCodes=codesList },
    ["assisted"]=new OperationalAssistedReceipt(id),
    ["operatorEnrollment"]=new { challenge=token,secret=seed,uri,enrollmentRequired=true },
    ["exceptionOpen"]=new { exceptionId=id,state="pending",expiresAtUtc=now },
    ["exceptionApprove"]=new { state="approved" },
    ["exceptionClose"]=new { state="closed" }
};
var routes=new Dictionary<string,string>
{
    ["logout"]="ack",
    ["sessions/revoke"]="ack",
    ["recovery/request"]="ack",
    ["recovery/complete"]="ack",
    ["password/change"]="ack",
    ["mailbox/request"]="ack",
    ["mailbox/complete"]="ack",
    ["memberships/decide"]="ack",
    ["memberships/request"]="ack",
    ["passkeys/register"]="ack",
    ["passkeys/revoke"]="ack",
    ["terms/accept"]="ack",
    ["invitations/issue"]="ack",
    ["invitations/accept"]="ack",
    ["sessions/revoke-all"]="ack",
    ["pairings/approve"]="ack",
    ["incidents/report"]="ack",
    ["platform/logout"]="ack",
    ["platform/sessions/revoke"]="ack",
    ["platform/sessions/revoke-all"]="ack",
    ["platform/profile/save"]="ack",
    ["platform/profile/photo-upload"]="ack",
    ["platform/profile/photo-remove"]="ack",
    ["platform/password/change"]="ack",
    ["platform/mailbox/request"]="ack",
    ["platform/mailbox/complete"]="ack",
    ["platform/incidents/report"]="ack",
    ["platform/review"]="ack",
    ["assisted/complete"]="ack",
    ["assisted/handoff-complete"]="ack",
    ["factor/recover-passkey"]="ack",
    ["profile/save"]="ack",
    ["profile/photo-upload"]="ack",
    ["profile/photo-remove"]="ack",
    ["login"]="challenge",
    ["factor/replace"]="challenge",
    ["mfa/enroll"]="enroll",
    ["mfa/verify"]="sessionWithCodes",
    ["sessions/rotate"]="sessionOnly",
    ["passkeys/login"]="sessionOnly",
    ["pairings/complete"]="sessionOnly",
    ["platform/mfa"]="sessionOnly",
    ["platform/sessions/rotate"]="sessionOnly",
    ["sessions"]="sessions",
    ["browser/restore"]="sessions",
    ["sessions/keepalive"]="keepalive",
    ["platform/sessions/keepalive"]="keepalive",
    ["memberships/pending"]="pending",
    ["private/read"]="reference",
    ["platform/breakglass/read"]="reference",
    ["passkeys/options"]="assertion",
    ["passkeys/register-options"]="registration",
    ["passkeys"]="passkeys",
    ["terms"]="terms",
    ["pairings/start"]="pairing",
    ["security/events"]="events",
    ["platform/login"]="platformLogin",
    ["platform/session"]="platformSession",
    ["platform/restore"]="platformSession",
    ["platform/sessions"]="platformSessions",
    ["platform/session-policy"]="sessionPolicy",
    ["platform/session-policy/update"]="sessionPolicy",
    ["platform/case"]="caseReceipt",
    ["platform/cases/find"]="caseId",
    ["platform/abuse-summary"]="signals",
    ["profile/read"]="profile",
    ["platform/profile/read"]="profile",
    ["profile/photo"]="photo",
    ["platform/profile/photo"]="photo",
    ["platform/release-share"]="share",
    ["platform/factor/replace"]="platformFactor",
    ["platform/factor/confirm"]="codes",
    ["platform/operator-recovery/enroll"]="codes",
    ["assisted/request"]="assisted",
    ["platform/operator-recovery/request"]="assisted",
    ["platform/operator-recovery/complete"]="operatorEnrollment",
    ["platform/breakglass/open"]="exceptionOpen",
    ["platform/breakglass/approve"]="exceptionApprove",
    ["platform/breakglass/close"]="exceptionClose",
};
var web=new JsonSerializerOptions(JsonSerializerDefaults.Web);
string Json(object? v)=>JsonSerializer.Serialize(v,web);
Check(routes.Count==79 && C06IdentityResponseProjection.Operations.ToHashSet().SetEquals(routes.Keys),"all 79 exact operations");
foreach(var (op,shape) in routes)
{
    Check(C06IdentityResponseProjection.TryApply(op,new(true,samples[shape]),out var safe)&&safe.Allowed&&safe.Value is not null,"positive shape "+op);
    Check(C06IdentityResponseProjection.TryApply(op,new(false,new { passwordPhc="DO-NOT-LEAK" }){ SessionExpiresAtUtc=now,SessionIdleMinutes=30 },out var denied)&&!denied.Allowed&&denied.Value is null&&denied.SessionExpiresAtUtc is null&&denied.SessionIdleMinutes is null,"denial strips payload "+op);
    Check(!C06IdentityResponseProjection.TryApply(op,new(true,new { passwordPhc="DO-NOT-LEAK",tenantId=id }),out var blocked)&&blocked.Value is null,"reject extra entity fields "+op);
}
Check(!C06IdentityResponseProjection.TryApply("unknown",new(true,new { accepted=true }),out _),"unknown operation");
Check(!C06IdentityResponseProjection.TryApply("private/read",new(true,new { reference="allowed",ownerAccountId=id }),out _),"extra private owner rejected");
Check(!C06IdentityResponseProjection.TryApply("private/read",new(true,new { reference=new { password="secret" }}),out _),"nested object in scalar rejected");
Check(!C06IdentityResponseProjection.TryApply("private/read",new(true,new UntrustedGetter()),out _),"arbitrary DTO rejected before getter");
Check(!C06IdentityResponseProjection.TryApply("private/read",new(true,new Dictionary<string,object?> { ["reference"]="private",["passwordPhc"]="secret" }),out _),"raw database dictionary denied");
Check(!C06IdentityResponseProjection.TryApply("private/read",new(true,new { reference="value" }){ SessionIdleMinutes=121 },out _),"invalid session metadata denied");
Check(!C06IdentityResponseProjection.TryApply("sessions",new(true,new { expiresAtUtc=now,sessions=new[]{new { id,issuedAtUtc=now,expiresAtUtc=now,lastSeenAtUtc=now,device="API",factor="TOTP",tokenHmac=token }} }),out _),"nested session digest denied");
Check(!C06IdentityResponseProjection.TryApply("memberships/pending",new(true,new { requests=new[]{new { id=id.ToString("D"),requestedAtUtc=now,mailboxHint="full.mailbox@example.test" }},nextCursor=(string?)null }),out _),"unmasked mailbox denied");
Check(!C06IdentityResponseProjection.TryApply("passkeys/login",new(true,new OperationalSessionReceipt(token,now,codesList)),out _),"backup codes not allowed in passkey login");
Check(!C06IdentityResponseProjection.TryApply("mfa/verify",new(true,new OperationalSessionReceipt(token,now,[backup])),out _),"invalid backup receipt");
Check(!C06IdentityResponseProjection.TryApply("profile/read",new(true,new { displayName="x",language="es",timeZone="UTC",hasPhoto=false,protectedProfile=token }),out _),"private profile envelope denied");
Check(!C06IdentityResponseProjection.TryApply("sessions/keepalive",new(true,new { expiresAtUtc=now,idleMinutes=999 }),out _),"invalid idle value");
Check(C06IdentityResponseProjection.TryApply("platform/case",new(true,new { caseId=id,realm="PLATFORM_OPERATOR",readyAtUtc=(object?)null,blocked=false,consumed=false }),out var caseSafe)&&!Json(caseSafe.Value).Contains("createdAtUtc"),"separate operator case projection");
using var malicious=JsonDocument.Parse("""{"challenge":"abc","rpId":"localhost","userVerification":"required","passwordPhc":"DO-NOT-LEAK","allowCredentials":[{"type":"public-key","id":"abc","internalOwner":"DO-NOT-LEAK"}]}""");
Check(C06IdentityResponseProjection.TryApply("passkeys/options",new(true,new { challenge=token,publicKey=malicious.RootElement }),out var protocol)&&!Json(protocol.Value).Contains("DO-NOT-LEAK"),"unknown WebAuthn root/nested fields stripped");
using var dup=JsonDocument.Parse("""{"challenge":"abc","challenge":"xyz"}""");
Check(!C06IdentityResponseProjection.TryApply("passkeys/options",new(true,new { challenge=token,publicKey=dup.RootElement }),out _),"duplicate protocol property rejected");
var receipt=C06IdentityResponseProjection.NeutralAssisted(new(true,new { reference=id,accepted=true,mailbox="secret" }));
Check(receipt.Accepted && receipt.Reference!=Guid.Empty && receipt.Reference!=id,"neutral fallback excludes arbitrary DTO");
Check(C06IdentityResponseProjection.NeutralAssisted(new(true,new OperationalAssistedReceipt(id))).Reference==id,"real neutral reference preserved");
Console.WriteLine($"PASS C06-07 response contracts={count}; 79 operations, real WebAuthn options, negative projections");

sealed class UntrustedGetter
{
    public string Reference => throw new Exception("Must never evaluate arbitrary getter");
}
