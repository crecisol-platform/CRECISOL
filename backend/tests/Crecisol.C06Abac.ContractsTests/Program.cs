using Crecisol.Domain.Tenancy;

int cases = 0;
var now = new DateTimeOffset(2026,11,24,22,0,0,TimeSpan.Zero);
var tenantA = TenantId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
var tenantB = TenantId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
var actorA = TenantActorId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01"));
var actorB = TenantActorId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa02"));
var actorForeign = TenantActorId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb01"));
var branch1 = TenantBranchId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa11"));
var branch2 = TenantBranchId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa12"));
var branchB = TenantBranchId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb11"));
var resource1 = TenantResourceId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa21"));
var resource2 = TenantResourceId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa22"));
var actionRead = TenantRequiredAction.FromServerPolicy("cases.read");
var actionWrite = TenantRequiredAction.FromServerPolicy("cases.write");

C06OperationalMembershipContext Identity() =>
    C06OperationalMembershipContext.VerifyLockedServerEvidence(new C06LockedIdentityEvidence
    {
        VerifiedHostTenant=tenantA,SessionTenant=tenantA,MembershipTenant=tenantA,
        SessionActor=actorA,AccountActor=actorA,MembershipActor=actorA,FactorActor=actorA,
        SessionMembershipId=Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa31"),
        MembershipId=Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa31"),
        SessionFactorId=Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa32"),
        FactorId=Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa32"),
        SessionRevision=2,AccountRevisionInSession=5,CurrentAccountRevision=5,
        MembershipRevisionInSession=8,CurrentMembershipRevision=8,
        FactorRevision=1,VerifiedHostRevision=3,TenantCatalogRevision=4,
        VerifiedHostAddressMode=TenantAddressMode.Subdomain,
        TenantCatalogAddressMode=TenantAddressMode.Subdomain,
        SessionNotRevoked=true,AccountActive=true,AccountMailboxVerified=true,
        MembershipActive=true,FactorActive=true,
        SessionIssuedAtUtc=now.AddMinutes(-12),SessionLastSeenAtUtc=now.AddMinutes(-2),
        SessionExpiresAtUtc=now.AddMinutes(28),MfaVerifiedAtUtc=now.AddMinutes(-12),
        MembershipGrantedAtUtc=now.AddDays(-1),MembershipExpiresAtUtc=now.AddDays(5),
        ServerNowUtc=now,ServerIdleMinutes=30
    });

var identity = Identity();
C06ExactCaseAssignment Assignment() => new()
{
    TenantId=tenantA, ActorId=actorA, BranchId=branch1, ResourceId=resource1,
    Action=actionRead,AssignmentState=1,Revision=2,
    IssuedAtUtc=now.AddMinutes(-10),ExpiresAtUtc=now.AddMinutes(30),
    RevokedAtUtc=null
};
C06CaseAbacEvidence Owned() => new()
{
    Identity=identity,ResourceTenant=tenantA,ResourceBranch=branch1,ResourceId=resource1,
    ResourceOwner=actorA,SelectorBranch=branch1,SelectorResource=resource1,
    BranchActive=true,ResourceActive=true,RoleCode="MEMBER",RequiredAction=actionRead,
    GrantTenant=tenantA,GrantActor=actorA,GrantBranch=branch1,
    GrantResource=resource1,GrantPermissionCode="RESOURCE_READ",
    GrantScope=1,GrantRevision=5,GrantIssuedAtUtc=now.AddMinutes(-10),
    GrantExpiresAtUtc=now.AddMinutes(30),GrantRevokedAtUtc=null,
    Assignment=null,CheckedAtUtc=now
};
C06CaseAbacEvidence BranchWide() => Owned() with
{
    GrantScope=2,GrantResource=null,ResourceOwner=actorB,Assignment=null
};
void Yes(bool condition,string name)
{
    if(!condition)throw new Exception("C06-04 FAIL "+name);
    cases++; Console.WriteLine("PASS C06-04 "+name);
}
void No(C06CaseAbacEvidence snapshot,string name) =>
    Yes(!C06CaseAbacPolicy.Allows(snapshot),name);

Yes(C06CaseAbacPolicy.Allows(Owned()),"C05 owner-only resource read still authorized");
Yes(C06CaseAbacPolicy.Allows(Owned() with { RoleCode="tenant-staff" }),
    "owned case with verified tenant-staff role");
Yes(C06CaseAbacPolicy.Allows(Owned() with { RoleCode="tenant-viewer" }),
    "owned case with verified viewer role");
Yes(C06CaseAbacPolicy.Allows(BranchWide() with { RoleCode="tenant-owner" }),
    "owner role with explicitly scoped branch grant");
Yes(C06CaseAbacPolicy.Allows(BranchWide() with { RoleCode="tenant-admin" }),
    "admin role with explicitly scoped branch grant");
Yes(C06CaseAbacPolicy.Allows(BranchWide() with { RoleCode="OWNER" }),
    "legacy owner with explicitly scoped branch grant");
Yes(C06CaseAbacPolicy.Allows(BranchWide() with { RoleCode="ADMIN" }),
    "legacy admin with explicitly scoped branch grant");
Yes(C06CaseAbacPolicy.Allows(BranchWide() with { Assignment=Assignment() }),
    "ordinary employee assigned a specific non-owned case");
Yes(C06CaseAbacPolicy.Allows(BranchWide() with
    { RoleCode="tenant-branch-manager",Assignment=Assignment() }),
    "branch manager with exact assignment");
Yes(C06CaseAbacPolicy.Allows(BranchWide() with
    { RoleCode="tenant-viewer",Assignment=Assignment() }),
    "viewer with exact read-only case assignment");
Yes(!Owned().ToString().Contains("aaaaaaaa",StringComparison.Ordinal) &&
    !Assignment().ToString().Contains("aaaaaaaa",StringComparison.Ordinal),
    "no IDs leaked by policy snapshots");
No(BranchWide(),"same-company staff cannot see coworker case with branch grant alone");
No(BranchWide() with { ResourceOwner=actorA },
    "non-privileged branch grant cannot implicitly read all owned cases");
No(Owned() with { ResourceOwner=actorB },
    "owner-only grant cannot read another employee's case");
No(Owned() with { ResourceId=resource2 },
    "grant for one resource cannot authorize a different resource");
No(Owned() with { SelectorResource=resource2 },
    "untrusted resource selector cannot act as authorization");
No(Owned() with { SelectorBranch=branch2 },
    "URL branch selector mismatch denied");
No(Owned() with { ResourceBranch=branch2 },
    "DB branch mismatch denied");
No(Owned() with { ResourceTenant=tenantB },
    "other tenant's resource denied");
No(Owned() with { GrantTenant=tenantB },
    "other tenant's grant denied");
No(Owned() with { GrantActor=actorB },
    "other employee's grant denied");
No(Owned() with { GrantActor=actorForeign },
    "foreign organization employee grant denied");
No(Owned() with { GrantBranch=branch2 },
    "grant of same tenant, different branch denied");
No(Owned() with { GrantBranch=branchB },
    "grant of another tenant branch denied");
No(Owned() with { RoleCode="platform-operator" },
    "PLATFORM operator role cannot open tenant cases");
No(Owned() with { RoleCode="superadmin" },
    "SuperAdmin label cannot authorize tenant cases");
No(Owned() with { RoleCode="tenant-custom" },
    "custom role never inherits private read permission");
No(Owned() with { RoleCode="REJECTED" },
    "rejected member role denied");
No(Owned() with { RoleCode="member" },
    "unlisted or case-changed role denied");
No(Owned() with { RequiredAction=actionWrite },
    "server route action must match case read");
No(Owned() with { GrantPermissionCode="RESOURCE_WRITE" },
    "wrong permission code denied");
No(Owned() with { GrantPermissionCode="*" },
    "wildcard permission denied");
No(Owned() with { BranchActive=false },
    "suspended branch denied");
No(Owned() with { ResourceActive=false },
    "deleted case denied");
No(Owned() with { GrantScope=0 },
    "missing scope denied");
No(Owned() with { GrantScope=3 },
    "unknown grant scope denied");
No(Owned() with { GrantResource=null },
    "missing case identifier in owner-only grant denied");
No(Owned() with { GrantResource=resource2 },
    "grant resource differs from case denied");
No(Owned() with { GrantRevision=0 },
    "revoked/unversioned grant denied");
No(Owned() with { GrantIssuedAtUtc=now.AddMinutes(1) },
    "future grant denied");
No(Owned() with { GrantExpiresAtUtc=now },
    "expired grant denied at exact expiry instant");
No(Owned() with { GrantExpiresAtUtc=null },
    "non-expiring private read grant denied");
No(Owned() with { GrantRevokedAtUtc=now.AddMinutes(-1) },
    "revoked grant denied");
No(Owned() with { CheckedAtUtc=now.AddMinutes(-5) },
    "untrusted policy clock older than identity validation denied");
No(Owned() with { CheckedAtUtc=now.ToOffset(TimeSpan.FromHours(-6)) },
    "non UTC DB policy clock denied");
No(Owned() with { Identity=null! },
    "missing verified identity denied");
No(BranchWide() with { Assignment=Assignment() with { TenantId=tenantB } },
    "case assignment from another tenant denied");
No(BranchWide() with { Assignment=Assignment() with { ActorId=actorB } },
    "case assignment for coworker denied");
No(BranchWide() with { Assignment=Assignment() with { ActorId=actorForeign } },
    "case assignment for foreign actor denied");
No(BranchWide() with { Assignment=Assignment() with { BranchId=branch2 } },
    "case assignment for another branch denied");
No(BranchWide() with { Assignment=Assignment() with { ResourceId=resource2 } },
    "another case in same branch remains forbidden");
No(BranchWide() with { Assignment=Assignment() with { Action=actionWrite } },
    "assignment action must match exact case read");
No(BranchWide() with { Assignment=Assignment() with { AssignmentState=2 } },
    "suspended assignment denied");
No(BranchWide() with { Assignment=Assignment() with { AssignmentState=3 } },
    "revoked assignment denied");
No(BranchWide() with { Assignment=Assignment() with { Revision=0 } },
    "unversioned assignment denied");
No(BranchWide() with { Assignment=Assignment() with { RevokedAtUtc=now } },
    "revocation time blocks assigned case");
No(BranchWide() with { Assignment=Assignment() with { IssuedAtUtc=now.AddMinutes(1) } },
    "assignment not yet granted denied");
No(BranchWide() with { Assignment=Assignment() with { ExpiresAtUtc=now } },
    "assigned case expired at boundary denied");
No(BranchWide() with { Assignment=Assignment() with { ExpiresAtUtc=now.ToOffset(TimeSpan.FromHours(-6)) } },
    "non-UTC assignment clock denied");
No(BranchWide() with { RoleCode="tenant-custom",Assignment=Assignment() },
    "explicit assignment cannot activate empty custom role");
No(BranchWide() with { RoleCode="platform-operator",Assignment=Assignment() },
    "explicit assignment cannot convert platform identity");
No(Owned() with { Assignment=Assignment() },
    "owner-only legacy scope cannot smuggle unrelated assignment");
No(BranchWide() with { RoleCode="tenant-admin", Assignment=Assignment() },
    "administrative wildcard cannot be replaced by a custom assignment without proper scope");
Yes(!C06CaseAbacPolicy.Allows(null),"null evidence is denied by default");
Console.WriteLine($"C06-04 ABAC CONTRACTS PASS: {cases} assertions (same-tenant IDOR, actor, action, branch, ownership and revocation).");
