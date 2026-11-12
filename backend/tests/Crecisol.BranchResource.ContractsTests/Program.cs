using System.Reflection;
using Crecisol.Application.Tenancy;
using Crecisol.Domain.Tenancy;

var checks = 0;
var now = new DateTimeOffset(2026, 11, 12, 20, 0, 0, TimeSpan.Zero);
var actorA = TenantActorId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01"));
var actorB = TenantActorId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb02"));
var tenantA = TenantId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
var tenantB = TenantId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
var branchA = TenantBranchId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa11"));
var branchB = TenantBranchId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa12"));
var branchOtherTenant = TenantBranchId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb13"));
var resourceA = TenantResourceId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa21"));
var resourceB = TenantResourceId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa22"));
var resourceOtherTenant = TenantResourceId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb23"));
var actionRead = TenantRequiredAction.FromServerPolicy("cases.read");
var actionUpdate = TenantRequiredAction.FromServerPolicy("cases.update");
var hostA = CanonicalTenantHost.Parse("company-a.example.test");
var hostB = CanonicalTenantHost.Parse("company-b.example.test");
var changed = now.AddDays(-2);
var granted = now.AddDays(-3);
var expiry = now.AddDays(4);

TrustedBranchResourceSnapshot Resource(
    TenantId? tenant = null, TenantBranchId? branch = null,
    TenantResourceId? resource = null, TenantActorId? owner = null,
    TrustedResourceState state = TrustedResourceState.Active,
    long revision = 4, DateTimeOffset? updated = null,
    TrustedBranchState branchState = TrustedBranchState.Active,
    long branchRevision = 11, DateTimeOffset? branchUpdated = null) =>
    TrustedBranchResourceSnapshot.RestoreFromTrustedResourceReader(
        tenant ?? tenantA, branch ?? branchA, resource ?? resourceA,
        owner ?? actorA, branchState, branchRevision, branchUpdated ?? changed,
        state, revision, updated ?? changed);

TrustedActorBranchGrantSnapshot Grant(
    TenantActorId? actor = null, TenantId? tenant = null,
    TenantBranchId? branch = null, TenantRequiredAction? action = null,
    BranchGrantScope scope = BranchGrantScope.OwnerOnly,
    BranchGrantState state = BranchGrantState.Active,
    long revision = 7, DateTimeOffset? changedAt = null,
    DateTimeOffset? grantedAt = null, DateTimeOffset? validUntil = null) =>
    TrustedActorBranchGrantSnapshot.RestoreFromTrustedGrantReader(
        actor ?? actorA, tenant ?? tenantA, branch ?? branchA, action ?? actionRead,
        scope, state, revision, changedAt ?? changed, grantedAt ?? granted,
        validUntil ?? expiry);

var goodResource = Resource();
var goodGrant = Grant();

TrustedActorSessionSnapshot Session(TenantActorId actor, long revision = 2) =>
    TrustedActorSessionSnapshot.RestoreFromTrustedIdentityProvider(
        actor, ActorSessionState.Active, revision, now.AddDays(-5),
        now.AddDays(-2), now.AddDays(6));

TenantActorMembershipSnapshot Membership(TenantActorId actor, TenantId tenant) =>
    TenantActorMembershipSnapshot.RestoreFromTrustedMembershipCatalog(
        actor, tenant, TenantMembershipState.Active, 3, changed,
        granted, expiry);

VerifiedTenantHostBinding HostBinding(TenantId tenant, CanonicalTenantHost host) =>
    VerifiedTenantHostBinding.RestoreFromTrustedCatalog(
        tenant, host, TenantAddressMode.Subdomain, HostVerificationState.Verified,
        5, changed, granted, expiry);

TenantCatalogSnapshot Catalog(TenantId tenant) =>
    TenantCatalogSnapshot.RestoreFromTrustedCatalog(
        tenant, "tenant-internal", "Tenant display", TenantLifecycleState.Active,
        TenantAddressMode.Subdomain, 6, changed);

ResolveCurrentTenantIdentityContext Identity(
    TenantActorId? actor = null, TenantId? tenant = null,
    CanonicalTenantHost? host = null,
    Func<int, TrustedActorSessionSnapshot?>? sessionByCall = null)
{
    var a = actor ?? actorA;
    var t = tenant ?? tenantA;
    var h = host ?? hostA;
    var calls = 0;
    return new ResolveCurrentTenantIdentityContext(
        new FakeActorReader(_ => ValueTask.FromResult(
            sessionByCall is null ? Session(a) : sessionByCall(++calls))),
        new FakeCurrentHostReader(_ => ValueTask.FromResult<CanonicalTenantHost?>(h)),
        new ResolveVerifiedTenantHost(
            new FakeVerifiedHostCatalog((_, _) =>
                Task.FromResult<IReadOnlyList<VerifiedTenantHostBinding>?>(
                    new[] { HostBinding(t, h) })),
            new FakeClock(_ => now)),
        new FakeMembershipReader((_, _, _) =>
            Task.FromResult<IReadOnlyList<TenantActorMembershipSnapshot>?>(
                new[] { Membership(a, t) })),
        new FakeTenantCatalog((_, _) =>
            Task.FromResult<TenantCatalogSnapshot?>(Catalog(t))),
        new FakeClock(_ => now));
}

var baselineResources = new FakeResourceReader((_, _, _, _) =>
    Task.FromResult<TrustedBranchResourceSnapshot?>(goodResource));
var baselineGrants = new FakeGrantReader((_, _, _, _, _, _) =>
    Task.FromResult<IReadOnlyList<TrustedActorBranchGrantSnapshot>?>(
        new[] { goodGrant }));
var baselineClock = new FakeClock(_ => now);
var baseline = new ResolveCurrentBranchResourceScope(
    Identity(), baselineResources, baselineGrants, baselineClock);

var result = await baseline.ExecuteAsync(resourceA, actionRead, CancellationToken.None);
Check(result.ActorId == actorA && result.TenantId == tenantA &&
      result.BranchId == branchA && result.ResourceId == resourceA,
      "Positive A owner-only evidence scoped to exact tenant/branch/resource");
Check(result.RequiredAction == actionRead &&
      result.Scope == BranchGrantScope.OwnerOnly,
      "Action and owner-only mode preserved in internal result");
Check(result.ResourceRevision == 4 && result.BranchRevision == 11 &&
      result.BranchGrantRevision == 7 &&
      result.SessionRevision == 2 && result.MembershipRevision == 3 &&
      result.HostRevision == 5 && result.TenantCatalogRevision == 6,
      "All seven protected revision fields retained");
Check(result.ToString() == "TenantBranchResourceScopeEvidence[redacted]",
      "Resource evidence redactable without IDs or actions");
Check(baselineResources.Calls == 2 && baselineGrants.Calls == 2 &&
      baselineClock.Calls == 2,
      "Resolution re-reads exact resource and grant plus two UTC checks");
Check(baselineResources.LastTenant == tenantA &&
      baselineResources.LastResource == resourceA,
      "Resource lookup must be tenant scoped, never ID-only");
Check(baselineGrants.LastActor == actorA &&
      baselineGrants.LastTenant == tenantA &&
      baselineGrants.LastBranch == branchA &&
      baselineGrants.LastAction == actionRead,
      "Grant lookup binds actor tenant resource-derived branch and exact action");

var ownB = Resource(resource: resourceB, owner: actorB);
var explicitBranchGrant = Grant(scope: BranchGrantScope.BranchWide);
var branchEvidence = await New(
    resources: new FakeResourceReader((_, _, _, _) =>
        Task.FromResult<TrustedBranchResourceSnapshot?>(ownB)),
    grants: new FakeGrantReader((_, _, _, _, _, _) =>
        Task.FromResult<IReadOnlyList<TrustedActorBranchGrantSnapshot>?>(
            new[] { explicitBranchGrant })))
    .ExecuteAsync(resourceB, actionRead, CancellationToken.None);
Check(branchEvidence.BranchId == branchA &&
      branchEvidence.Scope == BranchGrantScope.BranchWide &&
      branchEvidence.ActorId == actorA,
      "Explicit branch-wide grant permits resource owned by B in same branch");

var grantB = Grant(tenant: tenantB, branch: branchOtherTenant, actor: actorB);
var resourceTenantB = Resource(tenant: tenantB, branch: branchOtherTenant,
    resource: resourceOtherTenant, owner: actorB);
var evidenceB = await New(
    identity: Identity(actorB, tenantB, hostB),
    resources: new FakeResourceReader((_, _, _, _) =>
        Task.FromResult<TrustedBranchResourceSnapshot?>(resourceTenantB)),
    grants: new FakeGrantReader((_, _, _, _, _, _) =>
        Task.FromResult<IReadOnlyList<TrustedActorBranchGrantSnapshot>?>(
            new[] { grantB })))
    .ExecuteAsync(resourceOtherTenant, actionRead, CancellationToken.None);
Check(evidenceB.TenantId == tenantB && evidenceB.ActorId == actorB &&
      evidenceB.BranchId == branchOtherTenant &&
      evidenceB.ResourceId == resourceOtherTenant,
      "Positive tenant B has independent branch/owner/actor evidence");

var branchBResource = Resource(resource: resourceB, branch: branchB);
var explicitBranchBGrant = Grant(branch: branchB);
var resultBranchB = await New(
    resources: new FakeResourceReader((_, _, _, _) =>
        Task.FromResult<TrustedBranchResourceSnapshot?>(branchBResource)),
    grants: new FakeGrantReader((_, _, _, _, _, _) =>
        Task.FromResult<IReadOnlyList<TrustedActorBranchGrantSnapshot>?>(
            new[] { explicitBranchBGrant })))
    .ExecuteAsync(resourceB, actionRead, CancellationToken.None);
Check(resultBranchB.BranchId == branchB,
      "Same tenant other branch requires its own exact grant");

var updateGrant = Grant(action: actionUpdate);
var updateEvidence = await New(
    grants: new FakeGrantReader((_, _, _, _, _, _) =>
        Task.FromResult<IReadOnlyList<TrustedActorBranchGrantSnapshot>?>(
            new[] { updateGrant })))
    .ExecuteAsync(resourceA, actionUpdate, CancellationToken.None);
Check(updateEvidence.RequiredAction == actionUpdate,
      "A new action is allowed only with explicit matching permission record");

Check(TenantBranchId.From(branchA.Value) == branchA &&
      TenantResourceId.From(resourceA.Value) == resourceA &&
      TenantRequiredAction.FromServerPolicy("cases.read") == actionRead,
      "Opaque ids and normalized action equality works");
Check(TenantRequiredAction.FromServerPolicy("case.ticket.read").Value ==
      "case.ticket.read", "Multi-component configurable action supported");
Check(TenantRequiredAction.FromServerPolicy("a.b").Value == "a.b",
      "Simple two-component action supported");
Check(TenantBranchId.From(branchA.Value).ToString() == "TenantBranchId[redacted]" &&
      TenantResourceId.From(resourceA.Value).ToString() == "TenantResourceId[redacted]" &&
      actionRead.ToString() == "TenantRequiredAction[redacted]",
      "All identifiers and action diagnostics redact values");

Expect<InvalidOperationException>(() => TenantBranchId.From(Guid.Empty),
    "Empty branch GUID cannot select a branch");
Expect<InvalidOperationException>(() => TenantResourceId.From(Guid.Empty),
    "Empty resource GUID cannot select a resource");
foreach (string? attack in new string?[] {
    null, "", " ", "cases", ".read", "cases.", "cases..read", "CASES.READ",
    "case.Read", "cases.read ", " cases.read", "cases.*", "*.read",
    "cases/admin.read", "cases\\read", "cases:read", "cases@read",
    "cases.read?tenant=b", "https://cases.read", "cases%2eread",
    "cases.read\nadmin", "cases.read\0", "cañes.read", "ca_ses.read",
    "cases.-read", "cases.read-", "0cases.read", "cases.0read",
    "cases.re ad", "case..x", "a." + new string('a', 33),
    "a." + new string('a', 121), "a.b.c.d.e"
})
    Expect<InvalidOperationException>(() => TenantRequiredAction.FromServerPolicy(attack),
        "Malformed action key cannot broaden required server action");

Expect<ArgumentNullException>(() =>
    TrustedBranchResourceSnapshot.RestoreFromTrustedResourceReader(
        null!, branchA, resourceA, actorA, TrustedBranchState.Active, 11, changed,
        TrustedResourceState.Active, 1, changed),
    "Resource snapshot cannot have null tenant");
Expect<ArgumentNullException>(() =>
    TrustedBranchResourceSnapshot.RestoreFromTrustedResourceReader(
        tenantA, null!, resourceA, actorA, TrustedResourceState.Active, 1, changed),
    "Resource snapshot cannot have null branch");
Expect<ArgumentNullException>(() =>
    TrustedBranchResourceSnapshot.RestoreFromTrustedResourceReader(
        tenantA, branchA, null!, actorA, TrustedResourceState.Active, 1, changed),
    "Resource snapshot cannot have null resource ID");
Expect<ArgumentNullException>(() =>
    TrustedBranchResourceSnapshot.RestoreFromTrustedResourceReader(
        tenantA, branchA, resourceA, null!, TrustedBranchState.Active, 11, changed,
        TrustedResourceState.Active, 1, changed),
    "Resource snapshot cannot have null owner");
Expect<InvalidOperationException>(() => Resource(revision: 0),
    "Zero resource revision rejected");
Expect<InvalidOperationException>(() => Resource(revision: -7),
    "Negative resource revision rejected");
Expect<InvalidOperationException>(() => Resource(branchRevision: 0),
    "Zero branch revision rejected");
Expect<InvalidOperationException>(() => Resource(branchRevision: -2),
    "Negative branch revision rejected");
Expect<InvalidOperationException>(() => Resource(branchState: (TrustedBranchState)77),
    "Unknown branch state rejected");
Expect<InvalidOperationException>(() =>
    Resource(branchUpdated: changed.ToOffset(TimeSpan.FromHours(-6))),
    "Non-UTC branch timestamp rejected");
Expect<InvalidOperationException>(() => Resource(state: (TrustedResourceState)77),
    "Unknown resource state rejected");
Expect<InvalidOperationException>(() => Resource(updated: default(DateTimeOffset)),
    "Missing resource timestamp rejected");
Expect<InvalidOperationException>(() =>
    Resource(updated: changed.ToOffset(TimeSpan.FromHours(-6))),
    "Non-UTC resource timestamp rejected");
Check(!Resource(state: TrustedResourceState.Suspended).IsActiveAtUtc(now),
    "Suspended resource fails even when ID and owner match");
Check(!Resource(state: TrustedResourceState.Deleted).IsActiveAtUtc(now),
    "Deleted resource fails even when ID and owner match");
Check(!Resource(branchState: TrustedBranchState.Suspended).IsActiveAtUtc(now),
    "Suspended branch denies all scoped resources");
Check(!Resource(branchState: TrustedBranchState.Deactivated).IsActiveAtUtc(now),
    "Deactivated branch denies all scoped resources");
Check(!Resource(branchUpdated: now.AddHours(1)).IsActiveAtUtc(now),
    "Future branch catalog snapshot cannot grant access");
Check(!Resource(updated: now.AddDays(1)).IsActiveAtUtc(now),
    "Future resource metadata cannot be used");
Expect<InvalidOperationException>(() =>
    goodResource.IsActiveAtUtc(now.ToOffset(TimeSpan.FromHours(-6))),
    "Resource validation rejects non-UTC evaluation time");

Expect<ArgumentNullException>(() =>
    TrustedActorBranchGrantSnapshot.RestoreFromTrustedGrantReader(
        null!, tenantA, branchA, actionRead, BranchGrantScope.OwnerOnly,
        BranchGrantState.Active, 1, changed, granted, expiry),
    "Grant cannot have null actor");
Expect<ArgumentNullException>(() =>
    TrustedActorBranchGrantSnapshot.RestoreFromTrustedGrantReader(
        actorA, null!, branchA, actionRead, BranchGrantScope.OwnerOnly,
        BranchGrantState.Active, 1, changed, granted, expiry),
    "Grant cannot have null tenant");
Expect<ArgumentNullException>(() =>
    TrustedActorBranchGrantSnapshot.RestoreFromTrustedGrantReader(
        actorA, tenantA, null!, actionRead, BranchGrantScope.OwnerOnly,
        BranchGrantState.Active, 1, changed, granted, expiry),
    "Grant cannot have null branch");
Expect<ArgumentNullException>(() =>
    TrustedActorBranchGrantSnapshot.RestoreFromTrustedGrantReader(
        actorA, tenantA, branchA, null!, BranchGrantScope.OwnerOnly,
        BranchGrantState.Active, 1, changed, granted, expiry),
    "Grant cannot have null required action");
Expect<InvalidOperationException>(() => Grant(scope: (BranchGrantScope)99),
    "Unknown branch grant scope denied");
Expect<InvalidOperationException>(() => Grant(state: (BranchGrantState)99),
    "Unknown grant lifecycle denied");
Expect<InvalidOperationException>(() => Grant(revision: 0),
    "Zero grant revision rejected");
Expect<InvalidOperationException>(() => Grant(revision: -4),
    "Negative grant revision rejected");
Expect<InvalidOperationException>(() => Grant(changedAt: default(DateTimeOffset)),
    "Missing grant change timestamp rejected");
Expect<InvalidOperationException>(() =>
    Grant(changedAt: changed.ToOffset(TimeSpan.FromHours(-6))),
    "Non-UTC grant change timestamp rejected");
Expect<InvalidOperationException>(() =>
    Grant(grantedAt: granted.ToOffset(TimeSpan.FromHours(-6))),
    "Non-UTC grant activation timestamp rejected");
Expect<InvalidOperationException>(() =>
    Grant(validUntil: expiry.ToOffset(TimeSpan.FromHours(-6))),
    "Non-UTC grant expiration timestamp rejected");
Expect<InvalidOperationException>(() => Grant(grantedAt: now.AddDays(1)),
    "Future grant cannot postdate catalog change");
Expect<InvalidOperationException>(() => Grant(validUntil: granted),
    "Zero grant window rejected");
Expect<InvalidOperationException>(() =>
    TrustedActorBranchGrantSnapshot.RestoreFromTrustedGrantReader(
        actorA, tenantA, branchA, actionRead, BranchGrantScope.OwnerOnly,
        BranchGrantState.Active, 1, changed, null, null),
    "Active grant requires trusted grant time and expiry");
Expect<InvalidOperationException>(() =>
    TrustedActorBranchGrantSnapshot.RestoreFromTrustedGrantReader(
        actorA, tenantA, branchA, actionRead, BranchGrantScope.OwnerOnly,
        BranchGrantState.Active, 1, changed, granted, null),
    "Incomplete grant expiry denied");
Expect<InvalidOperationException>(() =>
    Grant(state: BranchGrantState.Pending),
    "Pending grant cannot carry active-window evidence");
Check(!Grant(state: BranchGrantState.Suspended).IsActiveAtUtc(now),
    "Suspended explicit branch grant denied");
Check(!Grant(state: BranchGrantState.Revoked).IsActiveAtUtc(now),
    "Revoked explicit branch grant denied");
Check(!Grant(validUntil: now).IsActiveAtUtc(now),
    "Grant expired at exact UTC boundary denied");
Check(!Grant(changedAt: now.AddHours(2), grantedAt: now.AddHours(1),
    validUntil: now.AddDays(2)).IsActiveAtUtc(now),
    "Future grant does not authorize current resource");
Expect<InvalidOperationException>(() =>
    goodGrant.IsActiveAtUtc(now.ToOffset(TimeSpan.FromHours(-6))),
    "Grant validation rejects non-UTC evaluation time");
Check(goodGrant.ToString() == "TrustedActorBranchGrantSnapshot[redacted]" &&
      goodResource.ToString() == "TrustedBranchResourceSnapshot[redacted]",
      "Trusted catalogs do not log owner/tenant/branch IDs");

async Task Deny<T>(
    string label,
    TenantResourceId? id = null,
    TenantRequiredAction? action = null,
    ResolveCurrentTenantIdentityContext? identity = null,
    FakeResourceReader? resources = null,
    FakeGrantReader? grants = null,
    ITrustedHostUtcClock? clock = null)
    where T : Exception =>
    await ExpectAsync<T>(() => New(identity, resources, grants, clock).ExecuteAsync(
        id ?? resourceA, action ?? actionRead, CancellationToken.None), label);

await Deny<UnauthorizedAccessException>("Cross-tenant B resource ID rejected by tenant A",
    resources: new FakeResourceReader((_, _, _, _) =>
        Task.FromResult<TrustedBranchResourceSnapshot?>(resourceTenantB)));
await Deny<UnauthorizedAccessException>("Wrong resource ID rejected despite A tenant match",
    id: resourceB);
await Deny<UnauthorizedAccessException>("No resource catalog row rejected",
    resources: new FakeResourceReader((_, _, _, _) =>
        Task.FromResult<TrustedBranchResourceSnapshot?>(null)));
await Deny<UnauthorizedAccessException>("Wrong branch grant cannot authorize resource in branch B",
    id: resourceB,
    resources: new FakeResourceReader((_, _, _, _) =>
        Task.FromResult<TrustedBranchResourceSnapshot?>(branchBResource)));
await Deny<UnauthorizedAccessException>("Another tenant's grant cannot authorize A",
    grants: new FakeGrantReader((_, _, _, _, _, _) =>
        Task.FromResult<IReadOnlyList<TrustedActorBranchGrantSnapshot>?>(
            new[] { Grant(tenant: tenantB) })));
await Deny<UnauthorizedAccessException>("Another actor's grant cannot authorize A",
    grants: new FakeGrantReader((_, _, _, _, _, _) =>
        Task.FromResult<IReadOnlyList<TrustedActorBranchGrantSnapshot>?>(
            new[] { Grant(actor: actorB) })));
await Deny<UnauthorizedAccessException>("Another branch grant cannot authorize A branch",
    grants: new FakeGrantReader((_, _, _, _, _, _) =>
        Task.FromResult<IReadOnlyList<TrustedActorBranchGrantSnapshot>?>(
            new[] { Grant(branch: branchB) })));
await Deny<UnauthorizedAccessException>("A read grant cannot authorize update action",
    action: actionUpdate);
await Deny<UnauthorizedAccessException>("Wrong permission record cannot authorize update",
    action: actionUpdate,
    grants: new FakeGrantReader((_, _, _, _, _, _) =>
        Task.FromResult<IReadOnlyList<TrustedActorBranchGrantSnapshot>?>(
            new[] { goodGrant })));
await Deny<UnauthorizedAccessException>("OwnerOnly grant cannot authorize other owner's resource",
    id: resourceB,
    resources: new FakeResourceReader((_, _, _, _) =>
        Task.FromResult<TrustedBranchResourceSnapshot?>(ownB)));
await Deny<UnauthorizedAccessException>("Suspended resource cannot be selected",
    resources: new FakeResourceReader((_, _, _, _) =>
        Task.FromResult<TrustedBranchResourceSnapshot?>(
            Resource(state: TrustedResourceState.Suspended))));
await Deny<UnauthorizedAccessException>("Deleted resource cannot be selected",
    resources: new FakeResourceReader((_, _, _, _) =>
        Task.FromResult<TrustedBranchResourceSnapshot?>(
            Resource(state: TrustedResourceState.Deleted))));
await Deny<UnauthorizedAccessException>("Suspended branch cannot authorize resource",
    resources: new FakeResourceReader((_, _, _, _) =>
        Task.FromResult<TrustedBranchResourceSnapshot?>(
            Resource(branchState: TrustedBranchState.Suspended))));
await Deny<UnauthorizedAccessException>("Deactivated branch cannot authorize resource",
    resources: new FakeResourceReader((_, _, _, _) =>
        Task.FromResult<TrustedBranchResourceSnapshot?>(
            Resource(branchState: TrustedBranchState.Deactivated))));
await Deny<UnauthorizedAccessException>("Future-dated resource cannot be selected",
    resources: new FakeResourceReader((_, _, _, _) =>
        Task.FromResult<TrustedBranchResourceSnapshot?>(
            Resource(updated: now.AddMinutes(10)))));
await Deny<UnauthorizedAccessException>("No exact grant denied",
    grants: new FakeGrantReader((_, _, _, _, _, _) =>
        Task.FromResult<IReadOnlyList<TrustedActorBranchGrantSnapshot>?>(
            Array.Empty<TrustedActorBranchGrantSnapshot>())));
await Deny<UnauthorizedAccessException>("Null grant catalog response denied",
    grants: new FakeGrantReader((_, _, _, _, _, _) =>
        Task.FromResult<IReadOnlyList<TrustedActorBranchGrantSnapshot>?>(null)));
await Deny<UnauthorizedAccessException>("Duplicate grant is ambiguous even for same actor",
    grants: new FakeGrantReader((_, _, _, _, _, _) =>
        Task.FromResult<IReadOnlyList<TrustedActorBranchGrantSnapshot>?>(
            new[] { goodGrant, goodGrant })));
await Deny<UnauthorizedAccessException>("Grant ambiguity across branch/owner modes denied",
    grants: new FakeGrantReader((_, _, _, _, _, _) =>
        Task.FromResult<IReadOnlyList<TrustedActorBranchGrantSnapshot>?>(
            new[] { goodGrant, explicitBranchGrant })));
await Deny<UnauthorizedAccessException>("Suspended grant fails",
    grants: new FakeGrantReader((_, _, _, _, _, _) =>
        Task.FromResult<IReadOnlyList<TrustedActorBranchGrantSnapshot>?>(
            new[] { Grant(state: BranchGrantState.Suspended) })));
await Deny<UnauthorizedAccessException>("Revoked grant fails",
    grants: new FakeGrantReader((_, _, _, _, _, _) =>
        Task.FromResult<IReadOnlyList<TrustedActorBranchGrantSnapshot>?>(
            new[] { Grant(state: BranchGrantState.Revoked) })));
await Deny<UnauthorizedAccessException>("Grant expires at current instant",
    grants: new FakeGrantReader((_, _, _, _, _, _) =>
        Task.FromResult<IReadOnlyList<TrustedActorBranchGrantSnapshot>?>(
            new[] { Grant(validUntil: now) })));
await Deny<UnauthorizedAccessException>("Future grant fails",
    grants: new FakeGrantReader((_, _, _, _, _, _) =>
        Task.FromResult<IReadOnlyList<TrustedActorBranchGrantSnapshot>?>(
            new[] { Grant(changedAt: now.AddMinutes(2),
                grantedAt: now.AddMinutes(1), validUntil: now.AddHours(1)) })));
await Deny<UnauthorizedAccessException>("Reassignment from branch A to B mid-request fails",
    resources: new FakeResourceReader((n, _, _, _) =>
        Task.FromResult<TrustedBranchResourceSnapshot?>(
            n == 1 ? goodResource : Resource(branch: branchB, revision: 8))));
await Deny<UnauthorizedAccessException>("Resource owner changed mid-request fails",
    resources: new FakeResourceReader((n, _, _, _) =>
        Task.FromResult<TrustedBranchResourceSnapshot?>(
            n == 1 ? goodResource : Resource(owner: actorB, revision: 8))));
await Deny<UnauthorizedAccessException>("Resource revision changed mid-request fails",
    resources: new FakeResourceReader((n, _, _, _) =>
        Task.FromResult<TrustedBranchResourceSnapshot?>(
            n == 1 ? goodResource : Resource(revision: 99))));
await Deny<UnauthorizedAccessException>("Branch suspended mid-request fails",
    resources: new FakeResourceReader((n, _, _, _) =>
        Task.FromResult<TrustedBranchResourceSnapshot?>(
            n == 1 ? goodResource : Resource(branchState: TrustedBranchState.Suspended))));
await Deny<UnauthorizedAccessException>("Branch revision changed mid-request fails",
    resources: new FakeResourceReader((n, _, _, _) =>
        Task.FromResult<TrustedBranchResourceSnapshot?>(
            n == 1 ? goodResource : Resource(branchRevision: 99))));
await Deny<UnauthorizedAccessException>("Resource deleted mid-request fails",
    resources: new FakeResourceReader((n, _, _, _) =>
        Task.FromResult<TrustedBranchResourceSnapshot?>(
            n == 1 ? goodResource : Resource(state: TrustedResourceState.Deleted))));
await Deny<UnauthorizedAccessException>("Grant revoked mid-request fails",
    grants: new FakeGrantReader((n, _, _, _, _, _) =>
        Task.FromResult<IReadOnlyList<TrustedActorBranchGrantSnapshot>?>(
            new[] { n == 1 ? goodGrant : Grant(state: BranchGrantState.Revoked) })));
await Deny<UnauthorizedAccessException>("Grant re-scoped OwnerOnly to BranchWide mid-request fails",
    grants: new FakeGrantReader((n, _, _, _, _, _) =>
        Task.FromResult<IReadOnlyList<TrustedActorBranchGrantSnapshot>?>(
            new[] { n == 1 ? goodGrant : explicitBranchGrant })));
await Deny<UnauthorizedAccessException>("Grant revision changed mid-request fails",
    grants: new FakeGrantReader((n, _, _, _, _, _) =>
        Task.FromResult<IReadOnlyList<TrustedActorBranchGrantSnapshot>?>(
            new[] { n == 1 ? goodGrant : Grant(revision: 9) })));
await Deny<UnauthorizedAccessException>("Grant list became ambiguous mid-request fails",
    grants: new FakeGrantReader((n, _, _, _, _, _) =>
        Task.FromResult<IReadOnlyList<TrustedActorBranchGrantSnapshot>?>(
            n == 1 ? new[] { goodGrant } : new[] { goodGrant, goodGrant })));
await Deny<UnauthorizedAccessException>("Grant expires during asynchronous work",
    grants: new FakeGrantReader((_, _, _, _, _, _) =>
        Task.FromResult<IReadOnlyList<TrustedActorBranchGrantSnapshot>?>(
            new[] { Grant(validUntil: now.AddMinutes(1)) })),
    clock: new FakeClock(n => n == 2 ? now.AddMinutes(2) : now));
await Deny<UnauthorizedAccessException>("Resource snapshot becomes future-dated on clock reversal",
    clock: new FakeClock(n => n == 2 ? now.AddDays(-4) : now));
await Deny<InvalidOperationException>("Invalid non-UTC backend scope clock fails closed",
    clock: new FakeClock(_ => now.ToOffset(TimeSpan.FromHours(-6))));
await Deny<UnauthorizedAccessException>("C04-05 actor revision change during work fails",
    identity: Identity(sessionByCall: n =>
        Session(actorA, revision: n == 1 ? 2 : 11)));
await Deny<UnauthorizedAccessException>("C04-05 host tenant changes during work fails",
    identity: Identity(sessionByCall: n => n == 1 ? Session(actorA) : null));
await Deny<InvalidOperationException>("Resource reader failure cannot fall back",
    resources: new FakeResourceReader((_, _, _, _) =>
        Task.FromException<TrustedBranchResourceSnapshot?>(
            new InvalidOperationException("Backend resource catalog unavailable"))));
await Deny<InvalidOperationException>("Branch grants reader failure cannot fall back",
    grants: new FakeGrantReader((_, _, _, _, _, _) =>
        Task.FromException<IReadOnlyList<TrustedActorBranchGrantSnapshot>?>(
            new InvalidOperationException("Backend grants unavailable"))));

var nullResultGrant = new FakeGrantReader((_, _, _, _, _, _) =>
    Task.FromResult<IReadOnlyList<TrustedActorBranchGrantSnapshot>?>(
        new[] { goodGrant }));
await ExpectAsync<UnauthorizedAccessException>(() =>
    New(resources: new FakeResourceReader((_, _, _, _) =>
        Task.FromResult<TrustedBranchResourceSnapshot?>(null)),
        grants: nullResultGrant).ExecuteAsync(
            resourceA, actionRead, CancellationToken.None),
    "No resource must not leak or lookup any grant");
Check(nullResultGrant.Calls == 0, "Resource denial happens before grant lookup");

var emptyGrant = new FakeGrantReader((_, _, _, _, _, _) =>
    Task.FromResult<IReadOnlyList<TrustedActorBranchGrantSnapshot>?>(
        Array.Empty<TrustedActorBranchGrantSnapshot>()));
var resourcesBeforeDenial = new FakeResourceReader((_, _, _, _) =>
    Task.FromResult<TrustedBranchResourceSnapshot?>(goodResource));
await ExpectAsync<UnauthorizedAccessException>(() =>
    New(resources: resourcesBeforeDenial, grants: emptyGrant)
        .ExecuteAsync(resourceA, actionRead, CancellationToken.None),
    "No grant must not access scoped resource content or re-read resource");
Check(resourcesBeforeDenial.Calls == 1,
    "Denied missing grant stops before recheck or any private data read");

using (var pre = new CancellationTokenSource())
{
    pre.Cancel();
    var neverResources = new FakeResourceReader((_, _, _, _) =>
        Task.FromResult<TrustedBranchResourceSnapshot?>(goodResource));
    await ExpectAsync<OperationCanceledException>(() =>
        New(resources: neverResources).ExecuteAsync(resourceA, actionRead, pre.Token),
        "Pre-cancel denied before C04-05 identity");
    Check(neverResources.Calls == 0, "Pre-cancel stops before scoped lookup");
}
using (var midResource = new CancellationTokenSource())
{
    var neverGrant = new FakeGrantReader((_, _, _, _, _, _) =>
        Task.FromResult<IReadOnlyList<TrustedActorBranchGrantSnapshot>?>(
            new[] { goodGrant }));
    await ExpectAsync<OperationCanceledException>(() =>
        New(resources: new FakeResourceReader((_, _, _, _) => {
            midResource.Cancel();
            return Task.FromResult<TrustedBranchResourceSnapshot?>(goodResource);
        }), grants: neverGrant)
        .ExecuteAsync(resourceA, actionRead, midResource.Token),
        "Cancellation after resource metadata must stop before grants");
    Check(neverGrant.Calls == 0, "Cancelled resource lookup does not access grants");
}
using (var midGrant = new CancellationTokenSource())
{
    var onlyOneResourceRead = new FakeResourceReader((_, _, _, _) =>
        Task.FromResult<TrustedBranchResourceSnapshot?>(goodResource));
    await ExpectAsync<OperationCanceledException>(() =>
        New(resources: onlyOneResourceRead,
            grants: new FakeGrantReader((_, _, _, _, _, _) => {
                midGrant.Cancel();
                return Task.FromResult<IReadOnlyList<TrustedActorBranchGrantSnapshot>?>(
                    new[] { goodGrant });
            })).ExecuteAsync(resourceA, actionRead, midGrant.Token),
        "Cancellation after grant denies before recheck");
    Check(onlyOneResourceRead.Calls == 1, "Cancelled grant cannot re-read resource");
}
using (var afterSecondResource = new CancellationTokenSource())
{
    var notReadAgain = new FakeGrantReader((_, _, _, _, _, _) =>
        Task.FromResult<IReadOnlyList<TrustedActorBranchGrantSnapshot>?>(
            new[] { goodGrant }));
    await ExpectAsync<OperationCanceledException>(() =>
        New(resources: new FakeResourceReader((n, _, _, _) => {
            if (n == 2) afterSecondResource.Cancel();
            return Task.FromResult<TrustedBranchResourceSnapshot?>(goodResource);
        }), grants: notReadAgain)
        .ExecuteAsync(resourceA, actionRead, afterSecondResource.Token),
        "Cancellation during resource recheck denies");
    Check(notReadAgain.Calls == 1, "No grant recheck after cancellation");
}
using (var afterGrantRecheck = new CancellationTokenSource())
{
    await ExpectAsync<OperationCanceledException>(() =>
        New(grants: new FakeGrantReader((n, _, _, _, _, _) => {
            if (n == 2) afterGrantRecheck.Cancel();
            return Task.FromResult<IReadOnlyList<TrustedActorBranchGrantSnapshot>?>(
                new[] { goodGrant });
        })).ExecuteAsync(resourceA, actionRead, afterGrantRecheck.Token),
        "Cancellation after grant recheck denies evidence");
}
using (var lastClock = new CancellationTokenSource())
{
    await ExpectAsync<OperationCanceledException>(() =>
        New(clock: new FakeClock(n => {
            if (n == 2) lastClock.Cancel();
            return now;
        })).ExecuteAsync(resourceA, actionRead, lastClock.Token),
        "Cancellation after final clock denies evidence publication");
}

Expect<ArgumentNullException>(() => New(nullIdentity: true),
    "Missing identity resolver cannot be silently allowed");
Expect<ArgumentNullException>(() => New(nullResources: true),
    "Missing resource reader cannot be silently allowed");
Expect<ArgumentNullException>(() => New(nullGrants: true),
    "Missing grant reader cannot be silently allowed");
Expect<ArgumentNullException>(() => New(nullClock: true),
    "Missing UTC backend clock cannot be silently allowed");
await ExpectAsync<ArgumentNullException>(() =>
    baseline.ExecuteAsync(null!, actionRead, CancellationToken.None),
    "Null candidate resource ID denied");
await ExpectAsync<ArgumentNullException>(() =>
    baseline.ExecuteAsync(resourceA, null!, CancellationToken.None),
    "Null required server action denied");

var baseIdentity = await Identity().ExecuteAsync(CancellationToken.None);
var forgedAction = TenantRequiredAction.FromServerPolicy("cases.update");
Expect<UnauthorizedAccessException>(() =>
    TenantBranchResourceScopeEvidence.FromTrustedInternalSnapshots(
        baseIdentity, goodResource, goodGrant, forgedAction, now),
    "Direct evidence constructor rejects action mismatch");
Expect<UnauthorizedAccessException>(() =>
    TenantBranchResourceScopeEvidence.FromTrustedInternalSnapshots(
        baseIdentity, Resource(tenant: tenantB), goodGrant, actionRead, now),
    "Direct evidence constructor rejects cross-tenant resource");
Expect<UnauthorizedAccessException>(() =>
    TenantBranchResourceScopeEvidence.FromTrustedInternalSnapshots(
        baseIdentity, goodResource, Grant(branch: branchB), actionRead, now),
    "Direct evidence constructor rejects cross-branch grant");
Expect<UnauthorizedAccessException>(() =>
    TenantBranchResourceScopeEvidence.FromTrustedInternalSnapshots(
        baseIdentity, Resource(owner: actorB), goodGrant, actionRead, now),
    "Direct evidence constructor rejects owner mismatch");
Expect<UnauthorizedAccessException>(() =>
    TenantBranchResourceScopeEvidence.FromTrustedInternalSnapshots(
        baseIdentity, Resource(state: TrustedResourceState.Deleted), goodGrant,
        actionRead, now), "Direct evidence constructor rejects deleted resource");
Expect<UnauthorizedAccessException>(() =>
    TenantBranchResourceScopeEvidence.FromTrustedInternalSnapshots(
        baseIdentity, Resource(branchState: TrustedBranchState.Suspended),
        goodGrant, actionRead, now),
    "Direct evidence constructor rejects suspended branch");
Expect<UnauthorizedAccessException>(() =>
    TenantBranchResourceScopeEvidence.FromTrustedInternalSnapshots(
        baseIdentity, goodResource, Grant(state: BranchGrantState.Revoked),
        actionRead, now), "Direct evidence constructor rejects revoked grant");

var signature = typeof(ResolveCurrentBranchResourceScope)
    .GetMethod(nameof(ResolveCurrentBranchResourceScope.ExecuteAsync))!;
Check(signature.GetParameters().Select(p => p.ParameterType).SequenceEqual(
        new[] { typeof(TenantResourceId), typeof(TenantRequiredAction),
                typeof(CancellationToken) }),
    "Only untrusted resource selector + SERVER-required action + cancellation; no tenant/branch");
Check(typeof(ITrustedTenantScopedResourceReader).GetMethods().Single()
    .GetParameters().Select(p => p.ParameterType).SequenceEqual(
        new[] { typeof(TenantId), typeof(TenantResourceId), typeof(CancellationToken) }),
    "Scoped resource lookup requires trusted tenant AND resource composite");
Check(typeof(ITrustedActorBranchGrantReader).GetMethods().Single()
    .GetParameters().Select(p => p.ParameterType).SequenceEqual(
        new[] { typeof(TenantActorId), typeof(TenantId), typeof(TenantBranchId),
                typeof(TenantRequiredAction), typeof(CancellationToken) }),
    "Grant lookup requires actor+tenant+resource branch+backend action");
Check(typeof(TenantBranchResourceScopeEvidence)
    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
    .All(p => !p.CanWrite && p.PropertyType != typeof(string)),
    "Evidence carries immutable typed context and revisions, no DSN/role/string/token");
Check(typeof(ResolveCurrentBranchResourceScope).Assembly.GetName().Name ==
    "Crecisol.Application" && typeof(TenantBranchResourceScopeEvidence)
    .Assembly.GetName().Name == "Crecisol.Domain",
    "Domain/Application separation retained");
Check(goodResource.GetType().Assembly.GetName().Name == "Crecisol.Domain" &&
      goodGrant.GetType().Assembly.GetName().Name == "Crecisol.Domain",
    "Trusted metadata contracts remain framework independent");

Console.WriteLine($"PASS C04-06 .NET10 branch/resource/owner/action: {checks} " +
    "positive and hostile checks, A/B tenant/branch, owner-only, explicit branch-wide, " +
    "action mismatch, revocation, duplication, expiry, rebind and cancellation. " +
    "NO production membership, branch authorization DB, real private HTTP BOLA, " +
    "atomic data access, DI, DNS or deployment certified.");

ResolveCurrentBranchResourceScope New(
    ResolveCurrentTenantIdentityContext? identity = null,
    FakeResourceReader? resources = null,
    FakeGrantReader? grants = null,
    ITrustedHostUtcClock? clock = null,
    bool nullIdentity = false, bool nullResources = false,
    bool nullGrants = false, bool nullClock = false)
{
    return new ResolveCurrentBranchResourceScope(
        nullIdentity ? null! : identity ?? Identity(),
        nullResources ? null! : resources ?? new FakeResourceReader((_, _, _, _) =>
            Task.FromResult<TrustedBranchResourceSnapshot?>(goodResource)),
        nullGrants ? null! : grants ?? new FakeGrantReader((_, _, _, _, _, _) =>
            Task.FromResult<IReadOnlyList<TrustedActorBranchGrantSnapshot>?>(
                new[] { goodGrant })),
        nullClock ? null! : clock ?? new FakeClock(_ => now));
}
void Check(bool value, string label)
{
    if (!value) throw new Exception("FAIL C04-06: " + label);
    checks++;
}
void Expect<T>(Action action, string label) where T : Exception
{
    try { action(); }
    catch (T) { Check(true, label); return; }
    throw new Exception("FAIL C04-06 expected " + typeof(T).Name + ": " + label);
}
async Task ExpectAsync<T>(Func<Task> action, string label) where T : Exception
{
    try { await action(); }
    catch (T) { Check(true, label); return; }
    throw new Exception("FAIL C04-06 expected " + typeof(T).Name + ": " + label);
}

sealed class FakeActorReader(Func<CancellationToken, ValueTask<TrustedActorSessionSnapshot?>> read)
    : ITrustedCurrentActorSessionReader
{
    public ValueTask<TrustedActorSessionSnapshot?> ReadCurrentVerifiedSessionAsync(
        CancellationToken ct) => read(ct);
}
sealed class FakeCurrentHostReader(Func<CancellationToken, ValueTask<CanonicalTenantHost?>> read)
    : ITrustedCurrentDirectHostReader
{
    public ValueTask<CanonicalTenantHost?> ReadCurrentDirectHostAsync(
        CancellationToken ct) => read(ct);
}
sealed class FakeVerifiedHostCatalog(Func<CanonicalTenantHost, CancellationToken,
    Task<IReadOnlyList<VerifiedTenantHostBinding>?>> read) : ITrustedVerifiedHostCatalog
{
    public Task<IReadOnlyList<VerifiedTenantHostBinding>?> FindExactBindingsAsync(
        CanonicalTenantHost host, CancellationToken ct) => read(host, ct);
}
sealed class FakeMembershipReader(Func<TenantActorId, TenantId, CancellationToken,
    Task<IReadOnlyList<TenantActorMembershipSnapshot>?>> read)
    : ITrustedActorTenantMembershipReader
{
    public Task<IReadOnlyList<TenantActorMembershipSnapshot>?> FindExactMembershipsAsync(
        TenantActorId actor, TenantId tenant, CancellationToken ct) =>
        read(actor, tenant, ct);
}
sealed class FakeTenantCatalog(
    Func<TenantId, CancellationToken, Task<TenantCatalogSnapshot?>> read)
    : ITrustedTenantCatalogReader
{
    public Task<TenantCatalogSnapshot?> FindByIdAsync(TenantId tenant, CancellationToken ct) =>
        read(tenant, ct);
}
sealed class FakeResourceReader(
    Func<int, TenantId, TenantResourceId, CancellationToken,
        Task<TrustedBranchResourceSnapshot?>> read)
    : ITrustedTenantScopedResourceReader
{
    public int Calls { get; private set; }
    public TenantId? LastTenant { get; private set; }
    public TenantResourceId? LastResource { get; private set; }
    public Task<TrustedBranchResourceSnapshot?> FindByTenantAndResourceIdAsync(
        TenantId tenant, TenantResourceId resource, CancellationToken ct)
    {
        Calls++;
        LastTenant = tenant;
        LastResource = resource;
        return read(Calls, tenant, resource, ct);
    }
}
sealed class FakeGrantReader(
    Func<int, TenantActorId, TenantId, TenantBranchId, TenantRequiredAction,
        CancellationToken, Task<IReadOnlyList<TrustedActorBranchGrantSnapshot>?>> read)
    : ITrustedActorBranchGrantReader
{
    public int Calls { get; private set; }
    public TenantActorId? LastActor { get; private set; }
    public TenantId? LastTenant { get; private set; }
    public TenantBranchId? LastBranch { get; private set; }
    public TenantRequiredAction? LastAction { get; private set; }
    public Task<IReadOnlyList<TrustedActorBranchGrantSnapshot>?> FindExactGrantsAsync(
        TenantActorId actor, TenantId tenant, TenantBranchId branch,
        TenantRequiredAction action, CancellationToken ct)
    {
        Calls++;
        LastActor = actor;
        LastTenant = tenant;
        LastBranch = branch;
        LastAction = action;
        return read(Calls, actor, tenant, branch, action, ct);
    }
}
sealed class FakeClock(Func<int, DateTimeOffset> now)
    : ITrustedHostUtcClock
{
    public int Calls { get; private set; }
    public DateTimeOffset GetUtcNow() => now(++Calls);
}
