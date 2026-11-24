using Crecisol.Domain.Tenancy;

var checks = 0;
var now = new DateTimeOffset(2026, 11, 24, 19, 30, 0, TimeSpan.Zero);
var tenantA = TenantId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
var tenantB = TenantId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
var actorA = TenantActorId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01"));
var actorB = TenantActorId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb02"));
var membershipId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa03");
var factorId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa04");

C06LockedIdentityEvidence Good() => new()
{
    VerifiedHostTenant = tenantA,
    SessionTenant = tenantA,
    MembershipTenant = tenantA,
    SessionActor = actorA,
    AccountActor = actorA,
    MembershipActor = actorA,
    FactorActor = actorA,
    SessionMembershipId = membershipId,
    MembershipId = membershipId,
    SessionFactorId = factorId,
    FactorId = factorId,
    SessionRevision = 4,
    AccountRevisionInSession = 8,
    CurrentAccountRevision = 8,
    MembershipRevisionInSession = 6,
    CurrentMembershipRevision = 6,
    FactorRevision = 3,
    VerifiedHostRevision = 5,
    TenantCatalogRevision = 7,
    VerifiedHostAddressMode = TenantAddressMode.Subdomain,
    TenantCatalogAddressMode = TenantAddressMode.Subdomain,
    SessionNotRevoked = true,
    AccountActive = true,
    AccountMailboxVerified = true,
    MembershipActive = true,
    FactorActive = true,
    SessionIssuedAtUtc = now.AddMinutes(-12),
    SessionLastSeenAtUtc = now.AddMinutes(-3),
    SessionExpiresAtUtc = now.AddMinutes(27),
    MfaVerifiedAtUtc = now.AddMinutes(-12),
    MembershipGrantedAtUtc = now.AddDays(-10),
    MembershipExpiresAtUtc = now.AddDays(10),
    ServerNowUtc = now,
    ServerIdleMinutes = 30
};

void Check(bool yes, string title)
{
    if (!yes) throw new Exception("C06-03 FAIL " + title);
    checks++;
    Console.WriteLine("PASS C06-03 " + title);
}
void Reject(C06LockedIdentityEvidence value, string title)
{
    try { _ = C06OperationalMembershipContext.VerifyLockedServerEvidence(value); }
    catch (UnauthorizedAccessException)
    { Check(true, title); return; }
    throw new Exception("C06-03 FAIL accepted invalid " + title);
}

var ok = C06OperationalMembershipContext.VerifyLockedServerEvidence(Good());
Check(ok.TenantId == tenantA && ok.ActorId == actorA &&
      ok.MembershipId == membershipId, "trusted actor/tenant/member from locked DB rows");
Check(ok.AccountRevision == 8 && ok.MembershipRevision == 6 &&
      ok.SessionRevision == 4, "immutable security and membership revision binding");
Check(ok.HostRevision == 5 && ok.TenantCatalogRevision == 7,
    "host and catalog revisions retained");
Check(!ok.AuthorizesPrivateResource, "identity context is not a resource grant");
Check(ok.VerifiedAtUtc == now && ok.ToString().Contains("redacted", StringComparison.Ordinal),
    "verified UTC and safe toString");
Check(!ok.ToString().Contains("aaaaaaaa", StringComparison.Ordinal) &&
      !Good().ToString().Contains("aaaaaaaa", StringComparison.Ordinal),
    "no actor, tenant, or membership key in logs");
Check(C06OperationalMembershipContext.VerifyLockedServerEvidence(
        Good() with { ServerIdleMinutes = 5 }).TenantId == tenantA,
    "valid shortest administrative idle policy");
Check(C06OperationalMembershipContext.VerifyLockedServerEvidence(
        Good() with { ServerIdleMinutes = 120 }).TenantId == tenantA,
    "valid longest administrative idle policy");
Check(C06OperationalMembershipContext.VerifyLockedServerEvidence(
        Good() with { VerifiedHostAddressMode = TenantAddressMode.CustomDomain,
                      TenantCatalogAddressMode = TenantAddressMode.CustomDomain }).TenantId == tenantA,
    "matching custom-domain address policy");
Check(C06OperationalMembershipContext.VerifyLockedServerEvidence(
        Good() with { ServerNowUtc = now.AddMinutes(26),
                      SessionExpiresAtUtc = now.AddMinutes(27),
                      SessionLastSeenAtUtc = now.AddMinutes(25) }).TenantId == tenantA,
    "valid identity just before session expiration");

Reject(Good() with { VerifiedHostTenant = tenantB }, "foreign host tenant denied");
Reject(Good() with { SessionTenant = tenantB }, "session from another company denied");
Reject(Good() with { MembershipTenant = tenantB }, "membership from another company denied");
Reject(Good() with { SessionActor = actorB }, "session account spoofing denied");
Reject(Good() with { AccountActor = actorB }, "account substitution denied");
Reject(Good() with { MembershipActor = actorB }, "foreign actor membership denied");
Reject(Good() with { FactorActor = actorB }, "foreign actor factor denied");
Reject(Good() with { SessionMembershipId = Guid.NewGuid() }, "foreign session membership denied");
Reject(Good() with { SessionMembershipId = Guid.Empty }, "empty session membership denied");
Reject(Good() with { MembershipId = Guid.Empty }, "empty membership denied");
Reject(Good() with { SessionFactorId = Guid.NewGuid() }, "wrong MFA factor denied");
Reject(Good() with { FactorId = Guid.Empty }, "empty MFA factor denied");
Reject(Good() with { SessionRevision = 0 }, "missing session version denied");
Reject(Good() with { AccountRevisionInSession = 7 }, "security revision from stale session denied");
Reject(Good() with { CurrentAccountRevision = 9 }, "account credential rotation denied");
Reject(Good() with { MembershipRevisionInSession = 5 }, "stale membership revision denied");
Reject(Good() with { CurrentMembershipRevision = 7 }, "membership revoked/changed during session denied");
Reject(Good() with { FactorRevision = 0 }, "missing factor revision denied");
Reject(Good() with { VerifiedHostRevision = 0 }, "missing verified host revision denied");
Reject(Good() with { TenantCatalogRevision = 0 }, "missing catalog revision denied");
Reject(Good() with { VerifiedHostAddressMode = TenantAddressMode.PathSlug }, "path slug host denied");
Reject(Good() with { TenantCatalogAddressMode = TenantAddressMode.CustomDomain }, "inconsistent address modes denied");
Reject(Good() with { SessionNotRevoked = false }, "revoked session denied");
Reject(Good() with { AccountActive = false }, "suspended account denied");
Reject(Good() with { AccountMailboxVerified = false }, "unverified account denied");
Reject(Good() with { MembershipActive = false }, "pending/suspended/revoked membership denied");
Reject(Good() with { FactorActive = false }, "revoked MFA factor denied");
Reject(Good() with { ServerIdleMinutes = 4 }, "unsafe too-short server idle policy denied");
Reject(Good() with { ServerIdleMinutes = 121 }, "invalid server idle policy denied");
Reject(Good() with { ServerNowUtc = now.AddMinutes(27) }, "session at expiry boundary denied");
Reject(Good() with { MembershipExpiresAtUtc = now }, "membership at expiry boundary denied");
Reject(Good() with { ServerNowUtc = now.AddMinutes(27),
                      SessionExpiresAtUtc = now.AddHours(1) }, "session idle expired despite long expiry denied");
Reject(Good() with { SessionIssuedAtUtc = now.AddMinutes(1) }, "session issued in future denied");
Reject(Good() with { SessionLastSeenAtUtc = now.AddMinutes(-20) }, "last activity before issuance denied");
Reject(Good() with { SessionLastSeenAtUtc = now.AddMinutes(1) }, "future activity denied");
Reject(Good() with { MfaVerifiedAtUtc = now.AddMinutes(1) }, "future MFA proof denied");
Reject(Good() with { MembershipGrantedAtUtc = now.AddMinutes(1) }, "not-yet-granted membership denied");
Reject(Good() with { ServerNowUtc = now.ToOffset(TimeSpan.FromHours(-6)) }, "non-UTC server clock denied");
Reject(Good() with { SessionLastSeenAtUtc = now.AddMinutes(-3).ToOffset(TimeSpan.FromHours(-6)) }, "non-UTC activity denied");
Reject(Good() with { MembershipExpiresAtUtc = default }, "missing membership expiry denied");
Reject(Good() with { VerifiedHostTenant = null! }, "missing trusted host tenant denied");
Reject(Good() with { SessionActor = null! }, "missing server actor denied");
try { _ = C06OperationalMembershipContext.VerifyLockedServerEvidence(null!); }
catch (ArgumentNullException) { Check(true, "null evidence denied"); goto FINISHED; }
throw new Exception("C06-03 FAIL null evidence accepted");
FINISHED:
Console.WriteLine($"C06-03 CONTRACTS PASS: {checks} positive and negative assertions; no HTTP permission granted.");
