using Crecisol.Domain.Authorization;
using Crecisol.Domain.Tenancy;

int count=0;
var now=new DateTimeOffset(2026,11,25,22,0,0,TimeSpan.Zero);
var tenant=TenantId.From(Guid.NewGuid());
var otherTenant=TenantId.From(Guid.NewGuid());
var actor=TenantActorId.From(Guid.NewGuid());
var subject=TenantActorId.From(Guid.NewGuid());
var otherSubject=TenantActorId.From(Guid.NewGuid());
var branch=TenantBranchId.From(Guid.NewGuid());
var otherBranch=TenantBranchId.From(Guid.NewGuid());
var resource=TenantResourceId.From(Guid.NewGuid());
var otherResource=TenantResourceId.From(Guid.NewGuid());
var actorMembership=Guid.NewGuid();
var subjectMembership=Guid.NewGuid();
var role=Guid.NewGuid();
var factor=Guid.NewGuid();

var identity=C06OperationalMembershipContext.VerifyLockedServerEvidence(new C06LockedIdentityEvidence
{
    VerifiedHostTenant=tenant,SessionTenant=tenant,MembershipTenant=tenant,
    SessionActor=actor,AccountActor=actor,MembershipActor=actor,FactorActor=actor,
    SessionMembershipId=actorMembership,MembershipId=actorMembership,
    SessionFactorId=factor,FactorId=factor,SessionRevision=2,
    AccountRevisionInSession=5,CurrentAccountRevision=5,MembershipRevisionInSession=8,
    CurrentMembershipRevision=8,FactorRevision=1,VerifiedHostRevision=3,TenantCatalogRevision=4,
    VerifiedHostAddressMode=TenantAddressMode.Subdomain,TenantCatalogAddressMode=TenantAddressMode.Subdomain,
    SessionNotRevoked=true,AccountActive=true,AccountMailboxVerified=true,MembershipActive=true,FactorActive=true,
    SessionIssuedAtUtc=now.AddMinutes(-12),SessionLastSeenAtUtc=now.AddMinutes(-2),
    SessionExpiresAtUtc=now.AddMinutes(28),MfaVerifiedAtUtc=now.AddMinutes(-12),
    MembershipGrantedAtUtc=now.AddDays(-1),MembershipExpiresAtUtc=now.AddDays(5),
    ServerNowUtc=now,ServerIdleMinutes=30
});
var scope=new C06DelegationScope(tenant,branch,resource,TenantRequiredAction.FromServerPolicy("identity.private.read"));
var assign=new C06DelegationIntent(C06DelegationCommand.Assign,scope,subject,subjectMembership,role,3,4,2,0,now.AddHours(1));
var ceiling=new C06DelegationCeiling(scope,actor,actorMembership,2,true,true,now.AddHours(-1),now.AddHours(2),null);
C06DelegationStepUp Proof(C06DelegationIntent i) => new(i,tenant,actor,actorMembership,
    identity.SessionRevision,identity.AccountRevision,identity.MembershipRevision,now);
C06CaseAbacEvidence OwnRead(string roleCode="tenant-owner") => new()
{
    Identity=identity,ResourceTenant=tenant,ResourceBranch=branch,ResourceId=resource,
    ResourceOwner=subject,SelectorBranch=branch,SelectorResource=resource,
    BranchActive=true,ResourceActive=true,RoleCode=roleCode,RequiredAction=scope.Action,
    GrantTenant=tenant,GrantActor=actor,GrantBranch=branch,GrantResource=null,
    GrantPermissionCode="RESOURCE_READ",GrantScope=2,GrantRevision=5,
    GrantIssuedAtUtc=now.AddHours(-1),GrantExpiresAtUtc=now.AddHours(2),
    GrantRevokedAtUtc=null,Assignment=null,CheckedAtUtc=now
};
C06LockedDelegationEvidence Valid(C06DelegationIntent i) => new()
{
    Identity=identity,Intent=i,VerifiedResourceScope=scope,BranchActive=true,ResourceActive=true,
    CommercialAccessVerified=true,ActorRoleCode="tenant-owner",SubjectTenant=tenant,
    SubjectActor=subject,SubjectMembershipId=subjectMembership,SubjectMembershipRevision=4,
    SubjectMembershipActive=true,SubjectMembershipUntilUtc=now.AddDays(1),
    VerifiedRoleId=role,RoleTenant=tenant,RoleRevision=3,TargetRoleCode="tenant-staff",
    RoleActive=true,Ceiling=ceiling,ActorEffectiveRead=OwnRead(),StepUp=Proof(i),Existing=null,NowUtc=now
};
var grant=new C06DelegationGrant(scope,subject,subjectMembership,role,1,now.AddMinutes(-1),now.AddHours(1),null);
var revoke=assign with { Command=C06DelegationCommand.Revoke,ExpectedGrantRevision=1,UntilUtc=null };
var a=Valid(assign);
var r=Valid(revoke) with { Existing=grant };
void Check(bool ok,string name)
{
    if(!ok) throw new Exception("C06-11 DELEGATION FAIL "+name);
    count++; Console.WriteLine("PASS C06-11 DELEGATION "+name);
}
void Deny(C06LockedDelegationEvidence? e,string name) => Check(C06ScopedRoleDelegationPolicy.Decide(e) is null,name);
void Allow(C06LockedDelegationEvidence e,C06DelegationCommand command,long expected,long next,string name) =>
    Check(C06ScopedRoleDelegationPolicy.Decide(e) is { } d && d.Command==command &&
        d.ExpectedRevision==expected && d.NextRevision==next,name);
Allow(a,C06DelegationCommand.Assign,0,1,"owner with explicit exact ceiling and bound proof");
Allow(a with { ActorRoleCode="tenant-admin",ActorEffectiveRead=OwnRead("tenant-admin") },C06DelegationCommand.Assign,0,1,"admin requires same explicit ceiling");
Allow(r,C06DelegationCommand.Revoke,1,2,"owner revokes exact current grant");
Allow(r with { SubjectMembershipActive=false },C06DelegationCommand.Revoke,1,2,"revocation can close inactive subject");
Allow(r with { Existing=grant with { UntilUtc=now } },C06DelegationCommand.Revoke,1,2,"revocation can close expired grant");
Deny(null,"missing evidence");
Deny(a with { ActorRoleCode="platform-operator" },"platform operator not tenant delegator");
Deny(a with { ActorRoleCode="superadmin" },"no superadmin bypass");
Deny(a with { ActorRoleCode="tenant-staff" },"staff cannot delegate");
Deny(a with { ActorRoleCode="tenant-branch-manager" },"branch manager cannot delegate");
Deny(a with { TargetRoleCode="tenant-owner" },"no owner promotion");
Deny(a with { TargetRoleCode="tenant-admin" },"no admin promotion");
Deny(a with { TargetRoleCode="platform-operator" },"no platform target");
Deny(a with { TargetRoleCode="tenant-custom" },"unreviewed role not enabled");
Deny(a with { CommercialAccessVerified=false },"unknown or suspended contract fails closed");
Deny(a with { BranchActive=false },"inactive branch");
Deny(a with { ResourceActive=false },"inactive resource");
Deny(a with { SubjectTenant=otherTenant },"foreign subject tenant");
Deny(a with { RoleTenant=otherTenant },"foreign role tenant");
Deny(a with { SubjectActor=otherSubject },"wrong actor selector");
Deny(a with { SubjectMembershipId=Guid.NewGuid() },"wrong subject membership");
Deny(a with { SubjectMembershipRevision=0 },"unversioned membership");
Deny(a with { SubjectMembershipRevision=5 },"changed subject membership revision");
Deny(a with { SubjectMembershipActive=false },"inactive member cannot receive grant");
Deny(a with { SubjectMembershipUntilUtc=now },"expired membership");
Deny(a with { SubjectMembershipUntilUtc=now.AddMinutes(1) },"grant cannot outlive membership");
Deny(a with { VerifiedRoleId=Guid.NewGuid() },"foreign role selector");
Deny(a with { RoleRevision=4 },"stale role revision");
Deny(a with { RoleActive=false },"inactive role");
Deny(a with { VerifiedResourceScope=scope with { Tenant=otherTenant } },"cross tenant resource");
Deny(a with { VerifiedResourceScope=scope with { Branch=otherBranch } },"cross branch resource");
Deny(a with { VerifiedResourceScope=scope with { Resource=otherResource } },"IDOR resource");
Deny(a with { NowUtc=now.AddSeconds(1) },"identity not from same locked clock");
Deny(a with { Ceiling=ceiling with { Actor=otherSubject } },"borrowed ceiling");
Deny(a with { Ceiling=ceiling with { ActorMembershipId=subjectMembership } },"foreign ceiling membership");
Deny(a with { Ceiling=ceiling with { Scope=scope with { Tenant=otherTenant } } },"foreign ceiling tenant");
Deny(a with { Ceiling=ceiling with { Scope=scope with { Branch=otherBranch } } },"foreign ceiling branch");
Deny(a with { Ceiling=ceiling with { Scope=scope with { Resource=otherResource } } },"foreign ceiling resource");
Deny(a with { Ceiling=ceiling with { MayAssign=false } },"role label alone cannot grant");
Deny(r with { Ceiling=ceiling with { MayRevoke=false } },"revoke outside ceiling");
Deny(a with { Ceiling=ceiling with { Revision=0 } },"unversioned ceiling");
Deny(a with { Ceiling=ceiling with { Revision=3 } },"changed ceiling revision");
Deny(a with { ActorEffectiveRead=null },"ceiling does not substitute own read");
Deny(a with { ActorEffectiveRead=OwnRead() with { GrantRevokedAtUtc=now } },"revoked own permission");
Deny(a with { ActorEffectiveRead=OwnRead() with { GrantExpiresAtUtc=now.AddMinutes(1) } },"cannot grant longer than own authority");
Deny(a with { ActorEffectiveRead=OwnRead() with { GrantActor=otherSubject } },"cannot borrow effective permission");
Deny(a with { ActorEffectiveRead=OwnRead() with { SelectorResource=otherResource } },"own ABAC selector mismatch");
Deny(a with { ActorEffectiveRead=OwnRead() with { RoleCode="tenant-admin" } },"inconsistent actor role evidence");
Deny(a with { Ceiling=ceiling with { FromUtc=now.AddMinutes(1) } },"future ceiling");
Deny(a with { Ceiling=ceiling with { UntilUtc=now } },"expired ceiling");
Deny(a with { Ceiling=ceiling with { RevokedAtUtc=now } },"revoked ceiling");
Deny(a with { Ceiling=ceiling with { UntilUtc=now.AddMinutes(1) } },"grant outlives ceiling");
Deny(a with { StepUp=null! },"missing consumed step-up");
Deny(a with { StepUp=Proof(revoke) },"proof for other operation");
Deny(a with { StepUp=Proof(assign with { Subject=otherSubject }) },"proof for other subject");
Deny(a with { StepUp=Proof(assign with { RoleId=Guid.NewGuid() }) },"proof for other role");
Deny(a with { StepUp=Proof(assign with { ExpectedRoleRevision=4 }) },"proof for other role revision");
Deny(a with { StepUp=Proof(assign with { ExpectedSubjectMembershipRevision=5 }) },"proof for other membership revision");
Deny(a with { StepUp=Proof(assign with { ExpectedCeilingRevision=3 }) },"proof for other ceiling revision");
Deny(a with { StepUp=Proof(assign with { UntilUtc=now.AddMinutes(30) }) },"proof for other expiry");
Deny(a with { StepUp=a.StepUp with { Actor=otherSubject } },"proof actor mismatch");
Deny(a with { StepUp=a.StepUp with { Tenant=otherTenant } },"proof tenant mismatch");
Deny(a with { StepUp=a.StepUp with { SessionRevision=3 } },"proof from rotated session");
Deny(a with { StepUp=a.StepUp with { AccountRevision=6 } },"proof from stale account");
Deny(a with { StepUp=a.StepUp with { MembershipRevision=7 } },"proof from stale membership");
Deny(a with { StepUp=a.StepUp with { ConsumedAtUtc=now.AddSeconds(-1) } },"proof not from same transaction clock");
var self=assign with { Subject=actor,SubjectMembershipId=actorMembership };
Deny(Valid(self) with { SubjectActor=actor,SubjectMembershipId=actorMembership },"self-grant with otherwise matching evidence");
foreach(var code in new[]{"cases.read","authorization.role.assign","authorization.role.delegate","finance.report.export","platform.breakglass.read"})
{
    var s=scope with { Action=TenantRequiredAction.FromServerPolicy(code) };
    var i=assign with { Scope=s };
    Deny(Valid(i) with { VerifiedResourceScope=s,Ceiling=ceiling with { Scope=s } },"no activation "+code);
}
Deny(Valid(assign with { UntilUtc=null }),"assignment requires expiry");
Deny(Valid(assign with { UntilUtc=now }),"expiry must be future");
Deny(Valid(assign with { UntilUtc=now.AddHours(1).ToOffset(TimeSpan.FromHours(1)) }),"expiry UTC required");
Deny(Valid(assign with { ExpectedGrantRevision=1 }),"missing expected existing grant");
Deny(Valid(assign with { ExpectedGrantRevision=1 }) with { Existing=grant },"no implicit overwrite");
Deny(Valid(assign with { ExpectedGrantRevision=1 }) with { Existing=grant with { RevokedAtUtc=now } },"no reactivation");
Deny(r with { Existing=null },"revoke cannot create");
Deny(r with { Existing=grant with { Revision=2 } },"stale concurrent revision");
Deny(r with { Existing=grant with { RevokedAtUtc=now } },"already revoked");
Deny(r with { Existing=grant with { Scope=scope with { Resource=otherResource } } },"cross resource grant");
Deny(r with { Existing=grant with { Subject=otherSubject } },"cross actor grant");
Deny(r with { Existing=grant with { RoleId=Guid.NewGuid() } },"cross role grant");
Deny(Valid(revoke with { ExpectedGrantRevision=long.MaxValue }) with { Existing=grant with { Revision=long.MaxValue } },"version overflow");
Deny(Valid(revoke with { UntilUtc=now.AddHours(1) }) with { Existing=grant },"revoke cannot extend expiry");
Check(!a.ToString().Contains(actor.Value.ToString(),StringComparison.OrdinalIgnoreCase) &&
      !assign.ToString().Contains(subject.Value.ToString(),StringComparison.OrdinalIgnoreCase),"redacted evidence representations");
Console.WriteLine($"PASS C06-11 DELEGATION DOMAIN contracts={count}; no HTTP, DB or replay runtime claim");
