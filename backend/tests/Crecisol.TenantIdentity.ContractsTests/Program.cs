using System.Reflection;
using Crecisol.Application.Tenancy;
using Crecisol.Domain.Tenancy;

var count = 0;
var now = new DateTimeOffset(2026, 11, 12, 18, 0, 0, TimeSpan.Zero);
var actorA = TenantActorId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01"));
var actorB = TenantActorId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb02"));
var tenantA = TenantId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
var tenantB = TenantId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
var hostA = CanonicalTenantHost.Parse("store-a.example.test");
var hostB = CanonicalTenantHost.Parse("store-b.example.test");
var authAt = now.AddDays(-3);
var checkedAt = now.AddDays(-1);
var sessionExpiry = now.AddDays(7);
var grantAt = now.AddDays(-4);
var changedAt = now.AddDays(-1);
var memberExpiry = now.AddDays(4);
var verifiedAt = now.AddDays(-2);
var hostExpiry = now.AddDays(10);

TrustedActorSessionSnapshot Session(
    TenantActorId? actor = null,
    ActorSessionState state = ActorSessionState.Active,
    long rev = 2,
    DateTimeOffset? authenticated = null,
    DateTimeOffset? checkedUtc = null,
    DateTimeOffset? expiry = null) =>
    TrustedActorSessionSnapshot.RestoreFromTrustedIdentityProvider(
        actor ?? actorA, state, rev,
        authenticated ?? authAt, checkedUtc ?? checkedAt, expiry ?? sessionExpiry);

TenantActorMembershipSnapshot Membership(
    TenantActorId? actor = null,
    TenantId? tenant = null,
    TenantMembershipState state = TenantMembershipState.Active,
    long rev = 3,
    DateTimeOffset? changed = null,
    DateTimeOffset? granted = null,
    DateTimeOffset? expiry = null) =>
    TenantActorMembershipSnapshot.RestoreFromTrustedMembershipCatalog(
        actor ?? actorA, tenant ?? tenantA, state, rev,
        changed ?? changedAt, granted ?? grantAt, expiry ?? memberExpiry);

TenantCatalogSnapshot Tenant(
    TenantId? tenant = null,
    TenantLifecycleState state = TenantLifecycleState.Active,
    TenantAddressMode address = TenantAddressMode.Subdomain,
    long rev = 4,
    DateTimeOffset? updated = null) =>
    TenantCatalogSnapshot.RestoreFromTrustedCatalog(
        tenant ?? tenantA, "tenant-internal", "Tenant display",
        state, address, rev, updated ?? changedAt);

VerifiedTenantHostBinding HostBinding(
    TenantId? tenant = null,
    CanonicalTenantHost? host = null,
    TenantAddressMode mode = TenantAddressMode.Subdomain,
    HostVerificationState state = HostVerificationState.Verified,
    long rev = 5,
    DateTimeOffset? changed = null,
    DateTimeOffset? verified = null,
    DateTimeOffset? expiry = null) =>
    VerifiedTenantHostBinding.RestoreFromTrustedCatalog(
        tenant ?? tenantA, host ?? hostA, mode, state, rev,
        changed ?? changedAt, verified ?? verifiedAt, expiry ?? hostExpiry);

var goodSession = Session();
var goodMembership = Membership();
var goodTenant = Tenant();
var goodHost = HostBinding();

Check(actorA != actorB && tenantA != tenantB, "Independent typed actors and tenants");
Check(actorA.ToString() == "TenantActorId[redacted]", "Actor ID redacted in logs");
Check(goodSession.IsActiveAtUtc(now), "Fresh verified session is active");
Check(goodMembership.IsActiveAtUtc(now), "Member with active grant is current");
Check(!goodSession.IsActiveAtUtc(sessionExpiry), "Session denied at exact expiry boundary");
Check(!goodMembership.IsActiveAtUtc(memberExpiry), "Membership denied at exact expiry boundary");
Check(goodSession.ToString() == "TrustedActorSessionSnapshot[redacted]",
    "Session snapshot never logs user details");
Check(goodMembership.ToString() == "TenantActorMembershipSnapshot[redacted]",
    "Membership snapshot never logs private details");

Expect<InvalidOperationException>(() => TenantActorId.From(Guid.Empty),
    "Empty actor ID rejected as invalid");
Expect<ArgumentNullException>(() =>
    TrustedActorSessionSnapshot.RestoreFromTrustedIdentityProvider(
        null!, ActorSessionState.Active, 1, authAt, checkedAt, sessionExpiry),
    "Session cannot have null actor");
Expect<InvalidOperationException>(() => Session(state: (ActorSessionState)77),
    "Unknown session state rejected");
Expect<InvalidOperationException>(() => Session(rev: 0),
    "Zero session revision rejected");
Expect<InvalidOperationException>(() => Session(rev: -3),
    "Negative session revision rejected");
Expect<InvalidOperationException>(() => Session(authenticated: default(DateTimeOffset)),
    "Missing authentication timestamp rejected");
Expect<InvalidOperationException>(() =>
    Session(authenticated: authAt.ToOffset(TimeSpan.FromHours(-6))),
    "Session authentication time cannot be non-UTC");
Expect<InvalidOperationException>(() =>
    Session(checkedUtc: checkedAt.ToOffset(TimeSpan.FromHours(-6))),
    "Last validation time cannot be non-UTC");
Expect<InvalidOperationException>(() =>
    Session(expiry: sessionExpiry.ToOffset(TimeSpan.FromHours(-6))),
    "Session expiry cannot be non-UTC");
Expect<InvalidOperationException>(() => Session(authenticated: now, checkedUtc: checkedAt),
    "Last validation cannot precede authentication");
Expect<InvalidOperationException>(() => Session(expiry: checkedAt),
    "Last validation must precede expiry");
Expect<InvalidOperationException>(() => goodSession.IsActiveAtUtc(now.ToOffset(TimeSpan.FromHours(-6))),
    "Non-UTC evaluation time fails closed");
Check(!Session(state: ActorSessionState.Suspended).IsActiveAtUtc(now),
    "Suspended session denied even before expiration");
Check(!Session(state: ActorSessionState.Revoked).IsActiveAtUtc(now),
    "Revoked session denied even before expiration");
Check(!Session(authenticated: now.AddDays(1), checkedUtc: now.AddDays(1))
    .IsActiveAtUtc(now), "Future session is not active");

Expect<ArgumentNullException>(() =>
    TenantActorMembershipSnapshot.RestoreFromTrustedMembershipCatalog(
        null!, tenantA, TenantMembershipState.Active, 1, changedAt, grantAt, memberExpiry),
    "Null member actor denied");
Expect<ArgumentNullException>(() =>
    TenantActorMembershipSnapshot.RestoreFromTrustedMembershipCatalog(
        actorA, null!, TenantMembershipState.Active, 1, changedAt, grantAt, memberExpiry),
    "Null member tenant denied");
Expect<InvalidOperationException>(() => Membership(state: (TenantMembershipState)200),
    "Unknown membership state rejected");
Expect<InvalidOperationException>(() => Membership(rev: 0),
    "Zero membership revision rejected");
Expect<InvalidOperationException>(() => Membership(rev: -1),
    "Negative membership revision rejected");
Expect<InvalidOperationException>(() => Membership(changed: default(DateTimeOffset)),
    "Missing membership change timestamp rejected");
Expect<InvalidOperationException>(() =>
    Membership(changed: changedAt.ToOffset(TimeSpan.FromHours(-6))),
    "Non-UTC membership change rejected");
Expect<InvalidOperationException>(() =>
    Membership(granted: grantAt.ToOffset(TimeSpan.FromHours(-6))),
    "Non-UTC membership grant rejected");
Expect<InvalidOperationException>(() =>
    Membership(expiry: memberExpiry.ToOffset(TimeSpan.FromHours(-6))),
    "Non-UTC member expiry rejected");
Expect<InvalidOperationException>(() =>
    Membership(granted: now.AddHours(1)),
    "Grant cannot postdate membership catalog change");
Expect<InvalidOperationException>(() =>
    Membership(expiry: grantAt),
    "Zero lifetime membership rejected");
Expect<InvalidOperationException>(() =>
    TenantActorMembershipSnapshot.RestoreFromTrustedMembershipCatalog(
        actorA, tenantA, TenantMembershipState.Active, 1, changedAt, null, null),
    "Active membership without dates rejected");
Expect<InvalidOperationException>(() =>
    TenantActorMembershipSnapshot.RestoreFromTrustedMembershipCatalog(
        actorA, tenantA, TenantMembershipState.Active, 1, changedAt, grantAt, null),
    "Half-complete membership window rejected");
Expect<InvalidOperationException>(() =>
    TenantActorMembershipSnapshot.RestoreFromTrustedMembershipCatalog(
        actorA, tenantA, TenantMembershipState.Pending, 1, changedAt, grantAt, memberExpiry),
    "Pending membership cannot pretend to be active");
Check(!Membership(state: TenantMembershipState.Suspended).IsActiveAtUtc(now),
    "Suspended membership denied");
Check(!Membership(state: TenantMembershipState.Revoked).IsActiveAtUtc(now),
    "Revoked membership denied");
Check(!Membership(granted: now.AddHours(2), changed: now.AddHours(3),
    expiry: now.AddDays(4)).IsActiveAtUtc(now),
    "Future membership is not active");
Expect<InvalidOperationException>(() => goodMembership.IsActiveAtUtc(
    now.ToOffset(TimeSpan.FromHours(-6))), "Non-UTC membership clock denied");

var baselineSession = new TestActorReader(_ =>
    ValueTask.FromResult<TrustedActorSessionSnapshot?>(goodSession));
var baselineDirectHost = new TestDirectHostReader(_ =>
    ValueTask.FromResult<CanonicalTenantHost?>(hostA));
var baselineHostCatalog = new TestHostCatalog((_, _) =>
    Task.FromResult<IReadOnlyList<VerifiedTenantHostBinding>?>(new[] { goodHost }));
var baselineMemberships = new TestMembershipReader((_, _, _) =>
    Task.FromResult<IReadOnlyList<TenantActorMembershipSnapshot>?>(new[] { goodMembership }));
var baselineTenants = new TestTenantReader((_, _) =>
    Task.FromResult<TenantCatalogSnapshot?>(goodTenant));
var baselineClock = new TestClock(_ => now);
var normal = NewResolver(baselineSession, baselineDirectHost, baselineHostCatalog,
    baselineMemberships, baselineTenants, baselineClock);
var evidence = await normal.ExecuteAsync(CancellationToken.None);
Check(evidence.ActorId == actorA && evidence.TenantId == tenantA,
    "Session, host, membership and catalog produce exactly tenant A evidence");
Check(evidence.SessionRevision == 2 && evidence.MembershipRevision == 3 &&
      evidence.HostRevision == 5 && evidence.TenantCatalogRevision == 4,
    "Evidence retains all four independent backend revisions");
Check(evidence.ToString() == "TenantIdentityContextEvidence[redacted]",
    "No tenant identity details in evidence diagnostics");
Check(baselineSession.Calls == 1 && baselineDirectHost.Calls == 1 &&
      baselineHostCatalog.Calls == 2 && baselineMemberships.Calls == 1 &&
      baselineTenants.Calls == 1 && baselineClock.Calls == 4,
    "Authentication FIRST; twice verified host; one scoped membership; one tenant read");
Check(baselineMemberships.LastActor == actorA && baselineMemberships.LastTenant == tenantA,
    "Membership reader always receives verified actor AND host tenant");
Check(baselineTenants.LastTenant == tenantA && baselineHostCatalog.LastHost == hostA,
    "Tenant catalog and host catalog receive only backend validated exact scopes");

// Two memberships for the same actor are safe when the host binds one tenant.
var grantB = Membership(tenant: tenantB);
var hostBindB = HostBinding(tenant: tenantB, host: hostB);
var tenantBActive = Tenant(tenant: tenantB);
var evidenceB = await NewResolver(
    currentHost: new TestDirectHostReader(_ => ValueTask.FromResult<CanonicalTenantHost?>(hostB)),
    hostCatalog: new TestHostCatalog((_, _) =>
        Task.FromResult<IReadOnlyList<VerifiedTenantHostBinding>?>(new[] { hostBindB })),
    memberships: new TestMembershipReader((_, tenant, _) =>
        Task.FromResult<IReadOnlyList<TenantActorMembershipSnapshot>?>(
            tenant == tenantB ? new[] { grantB } : new[] { goodMembership })),
    tenantCatalog: new TestTenantReader((_, _) =>
        Task.FromResult<TenantCatalogSnapshot?>(tenantBActive)))
    .ExecuteAsync(CancellationToken.None);
Check(evidenceB.TenantId == tenantB && evidenceB.ActorId == actorA,
    "Actor with A and B memberships receives ONLY the tenant from a verified host");

async Task Denied<T>(string name,
    ITrustedCurrentActorSessionReader? actor = null,
    ITrustedCurrentDirectHostReader? currentHost = null,
    ITrustedVerifiedHostCatalog? hostCatalog = null,
    ITrustedActorTenantMembershipReader? memberships = null,
    ITrustedTenantCatalogReader? tenantCatalog = null,
    ITrustedHostUtcClock? clock = null) where T : Exception =>
    await ExpectAsync<T>(() =>
        NewResolver(actor, currentHost, hostCatalog, memberships, tenantCatalog, clock)
            .ExecuteAsync(CancellationToken.None), name);

await Denied<UnauthorizedAccessException>("Anonymous session cannot select a tenant",
    actor: new TestActorReader(_ =>
        ValueTask.FromResult<TrustedActorSessionSnapshot?>(null)));
await Denied<UnauthorizedAccessException>("Revoked session cannot select a tenant",
    actor: new TestActorReader(_ =>
        ValueTask.FromResult<TrustedActorSessionSnapshot?>(Session(state: ActorSessionState.Revoked))));
await Denied<UnauthorizedAccessException>("Suspended session cannot select a tenant",
    actor: new TestActorReader(_ =>
        ValueTask.FromResult<TrustedActorSessionSnapshot?>(Session(state: ActorSessionState.Suspended))));
await Denied<UnauthorizedAccessException>("Expired session cannot select a tenant",
    actor: new TestActorReader(_ =>
        ValueTask.FromResult<TrustedActorSessionSnapshot?>(Session(expiry: now))));
await Denied<UnauthorizedAccessException>("Future authentication cannot select a tenant",
    actor: new TestActorReader(_ =>
        ValueTask.FromResult<TrustedActorSessionSnapshot?>(
            Session(authenticated: now.AddDays(1), checkedUtc: now.AddDays(1)))));
await Denied<UnauthorizedAccessException>("Missing validated direct host cannot choose first membership",
    currentHost: new TestDirectHostReader(_ =>
        ValueTask.FromResult<CanonicalTenantHost?>(null)));
await Denied<UnauthorizedAccessException>("Missing verified host binding denied",
    hostCatalog: new TestHostCatalog((_, _) =>
        Task.FromResult<IReadOnlyList<VerifiedTenantHostBinding>?>(
            Array.Empty<VerifiedTenantHostBinding>())));
await Denied<UnauthorizedAccessException>("Two tenant bindings share host: deny ambiguity",
    hostCatalog: new TestHostCatalog((_, _) =>
        Task.FromResult<IReadOnlyList<VerifiedTenantHostBinding>?>(new[] {
            goodHost, HostBinding(tenant: tenantB)
        })));
await Denied<UnauthorizedAccessException>("No membership in host A denied",
    memberships: new TestMembershipReader((_, _, _) =>
        Task.FromResult<IReadOnlyList<TenantActorMembershipSnapshot>?>(
            Array.Empty<TenantActorMembershipSnapshot>())));
await Denied<UnauthorizedAccessException>("Null membership catalog response denied",
    memberships: new TestMembershipReader((_, _, _) =>
        Task.FromResult<IReadOnlyList<TenantActorMembershipSnapshot>?>(null)));
await Denied<UnauthorizedAccessException>("Duplicate A membership rejected",
    memberships: new TestMembershipReader((_, _, _) =>
        Task.FromResult<IReadOnlyList<TenantActorMembershipSnapshot>?>(new[] {
            goodMembership, goodMembership
        })));
await Denied<UnauthorizedAccessException>("Membership A and B ambiguity rejected without first-match",
    memberships: new TestMembershipReader((_, _, _) =>
        Task.FromResult<IReadOnlyList<TenantActorMembershipSnapshot>?>(new[] {
            grantB, goodMembership
        })));
await Denied<InvalidOperationException>("Membership for actor B cannot authorize actor A",
    memberships: new TestMembershipReader((_, _, _) =>
        Task.FromResult<IReadOnlyList<TenantActorMembershipSnapshot>?>(new[] {
            Membership(actor: actorB)
        })));
await Denied<InvalidOperationException>("Membership for tenant B cannot authorize host A",
    memberships: new TestMembershipReader((_, _, _) =>
        Task.FromResult<IReadOnlyList<TenantActorMembershipSnapshot>?>(new[] { grantB })));
await Denied<UnauthorizedAccessException>("Suspended membership cannot select tenant",
    memberships: new TestMembershipReader((_, _, _) =>
        Task.FromResult<IReadOnlyList<TenantActorMembershipSnapshot>?>(new[] {
            Membership(state: TenantMembershipState.Suspended)
        })));
await Denied<UnauthorizedAccessException>("Revoked membership cannot select tenant",
    memberships: new TestMembershipReader((_, _, _) =>
        Task.FromResult<IReadOnlyList<TenantActorMembershipSnapshot>?>(new[] {
            Membership(state: TenantMembershipState.Revoked)
        })));
await Denied<UnauthorizedAccessException>("Expired membership cannot select tenant",
    memberships: new TestMembershipReader((_, _, _) =>
        Task.FromResult<IReadOnlyList<TenantActorMembershipSnapshot>?>(new[] {
            Membership(expiry: now)
        })));
await Denied<UnauthorizedAccessException>("Future membership cannot select tenant",
    memberships: new TestMembershipReader((_, _, _) =>
        Task.FromResult<IReadOnlyList<TenantActorMembershipSnapshot>?>(new[] {
            Membership(changed: now.AddHours(2), granted: now.AddHours(1),
                expiry: now.AddDays(6))
        })));
await Denied<UnauthorizedAccessException>("Tenant catalog missing denied",
    tenantCatalog: new TestTenantReader((_, _) =>
        Task.FromResult<TenantCatalogSnapshot?>(null)));
await Denied<UnauthorizedAccessException>("Inactive tenant Draft denied",
    tenantCatalog: new TestTenantReader((_, _) =>
        Task.FromResult<TenantCatalogSnapshot?>(Tenant(state: TenantLifecycleState.Draft))));
await Denied<UnauthorizedAccessException>("Suspended tenant denied",
    tenantCatalog: new TestTenantReader((_, _) =>
        Task.FromResult<TenantCatalogSnapshot?>(Tenant(state: TenantLifecycleState.Suspended))));
await Denied<UnauthorizedAccessException>("Deactivated tenant denied",
    tenantCatalog: new TestTenantReader((_, _) =>
        Task.FromResult<TenantCatalogSnapshot?>(Tenant(state: TenantLifecycleState.Deactivated))));
await Denied<UnauthorizedAccessException>("Tenant B returned for host A rejected",
    tenantCatalog: new TestTenantReader((_, _) =>
        Task.FromResult<TenantCatalogSnapshot?>(tenantBActive)));
await Denied<UnauthorizedAccessException>("Address mode mismatch rejected",
    tenantCatalog: new TestTenantReader((_, _) =>
        Task.FromResult<TenantCatalogSnapshot?>(
            Tenant(address: TenantAddressMode.CustomDomain))));
await Denied<UnauthorizedAccessException>("Future tenant catalog snapshot rejected",
    tenantCatalog: new TestTenantReader((_, _) =>
        Task.FromResult<TenantCatalogSnapshot?>(
            Tenant(updated: now.AddHours(2)))));
await Denied<InvalidOperationException>("Host binding for another hostname denied",
    hostCatalog: new TestHostCatalog((_, _) =>
        Task.FromResult<IReadOnlyList<VerifiedTenantHostBinding>?>(new[] { hostBindB })));
await Denied<InvalidOperationException>("Actor provider error fail closed",
    actor: new TestActorReader(_ =>
        ValueTask.FromException<TrustedActorSessionSnapshot?>(
            new InvalidOperationException("Identity unavailable"))));
await Denied<InvalidOperationException>("Membership provider error fail closed",
    memberships: new TestMembershipReader((_, _, _) =>
        Task.FromException<IReadOnlyList<TenantActorMembershipSnapshot>?>(
            new InvalidOperationException("Membership unavailable"))));
await Denied<InvalidOperationException>("Tenant provider error fail closed",
    tenantCatalog: new TestTenantReader((_, _) =>
        Task.FromException<TenantCatalogSnapshot?>(
            new InvalidOperationException("Catalog unavailable"))));
await Denied<InvalidOperationException>("Host provider error fail closed",
    hostCatalog: new TestHostCatalog((_, _) =>
        Task.FromException<IReadOnlyList<VerifiedTenantHostBinding>?>(
            new InvalidOperationException("Host catalog unavailable"))));
await Denied<InvalidOperationException>("Backend non-UTC clock rejected",
    clock: new TestClock(_ => now.ToOffset(TimeSpan.FromHours(-6))));

// Host reassigned mid-flight: two resolver calls must not silently switch tenant.
await Denied<UnauthorizedAccessException>("Changed tenant binding between host checks rejected",
    hostCatalog: new TestHostCatalog((_, _) =>
        Task.FromResult<IReadOnlyList<VerifiedTenantHostBinding>?>(
            new[] { goodHost }))
        { AlternateAfterFirst = new[] { HostBinding(tenant: tenantB) } });
await Denied<UnauthorizedAccessException>("Host verification revision changed mid-flight rejected",
    hostCatalog: new TestHostCatalog((_, _) =>
        Task.FromResult<IReadOnlyList<VerifiedTenantHostBinding>?>(
            new[] { goodHost }))
        { AlternateAfterFirst = new[] { HostBinding(rev: 7) } });
await Denied<UnauthorizedAccessException>("Session expires during asynchronous operation",
    clock: new TestClock(n => n == 4 ? sessionExpiry : now));
await Denied<UnauthorizedAccessException>("Membership expires during asynchronous operation",
    clock: new TestClock(n => n == 4 ? memberExpiry : now));

var anonymousReader = new TestActorReader(_ =>
    ValueTask.FromResult<TrustedActorSessionSnapshot?>(null));
var neverHost = new TestDirectHostReader(_ =>
    ValueTask.FromResult<CanonicalTenantHost?>(hostA));
var neverMembership = new TestMembershipReader((_, _, _) =>
    Task.FromResult<IReadOnlyList<TenantActorMembershipSnapshot>?>(new[] { goodMembership }));
await ExpectAsync<UnauthorizedAccessException>(() =>
    NewResolver(anonymousReader, neverHost, memberships: neverMembership)
        .ExecuteAsync(CancellationToken.None),
    "Authorization failure must happen before ANY host or membership read");
Check(neverHost.Calls == 0 && neverMembership.Calls == 0,
    "Denied anonymous actor never accesses host, membership or tenant data");

var deniedMembership = new TestMembershipReader((_, _, _) =>
    Task.FromResult<IReadOnlyList<TenantActorMembershipSnapshot>?>(
        Array.Empty<TenantActorMembershipSnapshot>()));
var neverTenantCatalog = new TestTenantReader((_, _) =>
    Task.FromResult<TenantCatalogSnapshot?>(goodTenant));
await ExpectAsync<UnauthorizedAccessException>(() =>
    NewResolver(memberships: deniedMembership, tenantCatalog: neverTenantCatalog)
        .ExecuteAsync(CancellationToken.None),
    "Membership denial must prevent tenant catalog access");
Check(neverTenantCatalog.Calls == 0, "Failed membership prevents tenant lookup");

using (var tokenSource = new CancellationTokenSource())
{
    tokenSource.Cancel();
    var a = new TestActorReader(_ => ValueTask.FromResult<TrustedActorSessionSnapshot?>(goodSession));
    await ExpectAsync<OperationCanceledException>(() =>
        NewResolver(actor: a).ExecuteAsync(tokenSource.Token),
        "Cancelled request denied before identity read");
    Check(a.Calls == 0, "Pre-cancel never reads identity");
}
using (var afterActor = new CancellationTokenSource())
{
    var nextHost = new TestDirectHostReader(_ => ValueTask.FromResult<CanonicalTenantHost?>(hostA));
    await ExpectAsync<OperationCanceledException>(() =>
        NewResolver(actor: new TestActorReader(_ => {
            afterActor.Cancel();
            return ValueTask.FromResult<TrustedActorSessionSnapshot?>(goodSession);
        }), currentHost: nextHost).ExecuteAsync(afterActor.Token),
        "Cancellation after identity lookup denies");
    Check(nextHost.Calls == 0, "Cancelled after identity does not read host");
}
using (var afterMembership = new CancellationTokenSource())
{
    var nextTenant = new TestTenantReader((_, _) => Task.FromResult<TenantCatalogSnapshot?>(goodTenant));
    await ExpectAsync<OperationCanceledException>(() =>
        NewResolver(memberships: new TestMembershipReader((_, _, _) => {
            afterMembership.Cancel();
            return Task.FromResult<IReadOnlyList<TenantActorMembershipSnapshot>?>(
                new[] { goodMembership });
        }), tenantCatalog: nextTenant).ExecuteAsync(afterMembership.Token),
        "Cancellation after membership lookup denies");
    Check(nextTenant.Calls == 0, "Cancelled membership request cannot read tenant catalog");
}
using (var last = new CancellationTokenSource())
{
    await ExpectAsync<OperationCanceledException>(() =>
        NewResolver(clock: new TestClock(n => {
            if (n == 4) last.Cancel();
            return now;
        })).ExecuteAsync(last.Token),
        "Cancellation after final clock check prevents context publication");
}

Expect<ArgumentNullException>(() => NewResolver(
    actor: null, forceNullActor: true), "Missing trusted actor dependency denied");
Expect<ArgumentNullException>(() => NewResolver(
    currentHost: null, forceNullHost: true), "Missing trusted host dependency denied");
Expect<ArgumentNullException>(() => NewResolver(
    clock: null, forceNullClock: true), "Missing trusted UTC clock dependency denied");

Expect<InvalidOperationException>(() =>
    TenantIdentityContextEvidence.FromVerifiedInternalEvidence(
        goodSession, Membership(tenant: tenantB),
        new VerifiedTenantHostHint(tenantA, TenantAddressMode.Subdomain, 5), goodTenant),
    "Cross-tenant binding cannot manufacture context evidence");
Expect<InvalidOperationException>(() =>
    TenantIdentityContextEvidence.FromVerifiedInternalEvidence(
        goodSession, Membership(actor: actorB),
        new VerifiedTenantHostHint(tenantA, TenantAddressMode.Subdomain, 5), goodTenant),
    "Cross-actor binding cannot manufacture context evidence");
Expect<InvalidOperationException>(() =>
    TenantIdentityContextEvidence.FromVerifiedInternalEvidence(
        goodSession, goodMembership,
        new VerifiedTenantHostHint(tenantB, TenantAddressMode.Subdomain, 5), goodTenant),
    "Forged host hint cannot manufacture A context evidence");
Expect<InvalidOperationException>(() =>
    TenantIdentityContextEvidence.FromVerifiedInternalEvidence(
        goodSession, goodMembership,
        new VerifiedTenantHostHint(tenantA, TenantAddressMode.Subdomain, 5),
        Tenant(address: TenantAddressMode.CustomDomain)),
    "Host/address mismatch cannot manufacture context evidence");

var execute = typeof(ResolveCurrentTenantIdentityContext)
    .GetMethod(nameof(ResolveCurrentTenantIdentityContext.ExecuteAsync))!;
Check(execute.GetParameters().Select(p => p.ParameterType)
    .SequenceEqual(new[] { typeof(CancellationToken) }),
    "Use case accepts ONLY cancellation: no tenant_id, host, claim, cookie or DSN");
Check(typeof(ITrustedCurrentActorSessionReader).GetMethods().Single()
    .GetParameters().Select(p => p.ParameterType)
    .SequenceEqual(new[] { typeof(CancellationToken) }),
    "Backend actor provider cannot accept caller-supplied actor GUID/claims");
Check(typeof(ITrustedCurrentDirectHostReader).GetMethods().Single()
    .GetParameters().Select(p => p.ParameterType)
    .SequenceEqual(new[] { typeof(CancellationToken) }),
    "Backend direct host provider has no override from request parameters");
Check(typeof(ITrustedActorTenantMembershipReader).GetMethods().Single()
    .GetParameters().Select(p => p.ParameterType)
    .SequenceEqual(new[] { typeof(TenantActorId), typeof(TenantId), typeof(CancellationToken) }),
    "Membership lookup requires BOTH typed actor and host-verified tenant");
Check(typeof(TenantIdentityContextEvidence).GetProperties(BindingFlags.Public |
    BindingFlags.Instance).All(p => !p.CanWrite && p.PropertyType != typeof(string)),
    "Context evidence has ONLY immutable typed keys and revisions, no role/branch/DSN");
Check(typeof(ResolveCurrentTenantIdentityContext).Assembly.GetName().Name ==
    "Crecisol.Application" &&
    typeof(TenantIdentityContextEvidence).Assembly.GetName().Name == "Crecisol.Domain",
    "Identity policy remains in clean Domain/Application layers");

Console.WriteLine($"PASS C04-05 .NET10 actor-host-membership-tenant boundary: {count} checks " +
    "including cross-tenant A/B, revocation, ambiguous membership, expired sessions, " +
    "host rebind, cancellation and no tenant_id input. " +
    "Identity adapter, membership database, HTTP/DI, branch/resource rights and " +
    "production access NOT IMPLEMENTED or certified.");

ResolveCurrentTenantIdentityContext NewResolver(
    ITrustedCurrentActorSessionReader? actor = null,
    ITrustedCurrentDirectHostReader? currentHost = null,
    ITrustedVerifiedHostCatalog? hostCatalog = null,
    ITrustedActorTenantMembershipReader? memberships = null,
    ITrustedTenantCatalogReader? tenantCatalog = null,
    ITrustedHostUtcClock? clock = null,
    bool forceNullActor = false,
    bool forceNullHost = false,
    bool forceNullClock = false)
{
    var sessionReader = forceNullActor ? null : actor ??
        new TestActorReader(_ => ValueTask.FromResult<TrustedActorSessionSnapshot?>(goodSession));
    var directHost = forceNullHost ? null : currentHost ??
        new TestDirectHostReader(_ => ValueTask.FromResult<CanonicalTenantHost?>(hostA));
    var utcClock = forceNullClock ? null : clock ?? new TestClock(_ => now);
    var hosts = hostCatalog ?? new TestHostCatalog((_, _) =>
        Task.FromResult<IReadOnlyList<VerifiedTenantHostBinding>?>(new[] { goodHost }));
    var members = memberships ?? new TestMembershipReader((_, _, _) =>
        Task.FromResult<IReadOnlyList<TenantActorMembershipSnapshot>?>(new[] { goodMembership }));
    var tenantReader = tenantCatalog ?? new TestTenantReader((_, _) =>
        Task.FromResult<TenantCatalogSnapshot?>(goodTenant));

    return new ResolveCurrentTenantIdentityContext(
        sessionReader!, directHost!, new ResolveVerifiedTenantHost(hosts, utcClock!),
        members, tenantReader, utcClock!);
}
void Check(bool condition, string label)
{
    if (!condition) throw new Exception("FAIL C04-05: " + label);
    count++;
}
void Expect<T>(Action action, string label) where T : Exception
{
    try { action(); }
    catch (T) { Check(true, label); return; }
    throw new Exception("FAIL C04-05 expected " + typeof(T).Name + ": " + label);
}
async Task ExpectAsync<T>(Func<Task> action, string label) where T : Exception
{
    try { await action(); }
    catch (T) { Check(true, label); return; }
    throw new Exception("FAIL C04-05 expected " + typeof(T).Name + ": " + label);
}

sealed class TestActorReader(
    Func<CancellationToken, ValueTask<TrustedActorSessionSnapshot?>> read)
    : ITrustedCurrentActorSessionReader
{
    public int Calls { get; private set; }
    public ValueTask<TrustedActorSessionSnapshot?> ReadCurrentVerifiedSessionAsync(
        CancellationToken ct)
    {
        Calls++;
        return read(ct);
    }
}
sealed class TestDirectHostReader(
    Func<CancellationToken, ValueTask<CanonicalTenantHost?>> read)
    : ITrustedCurrentDirectHostReader
{
    public int Calls { get; private set; }
    public ValueTask<CanonicalTenantHost?> ReadCurrentDirectHostAsync(CancellationToken ct)
    {
        Calls++;
        return read(ct);
    }
}
sealed class TestHostCatalog(
    Func<CanonicalTenantHost, CancellationToken,
        Task<IReadOnlyList<VerifiedTenantHostBinding>?>> read)
    : ITrustedVerifiedHostCatalog
{
    public int Calls { get; private set; }
    public CanonicalTenantHost? LastHost { get; private set; }
    public IReadOnlyList<VerifiedTenantHostBinding>? AlternateAfterFirst { get; init; }
    public Task<IReadOnlyList<VerifiedTenantHostBinding>?> FindExactBindingsAsync(
        CanonicalTenantHost host, CancellationToken ct)
    {
        Calls++;
        LastHost = host;
        if (Calls > 1 && AlternateAfterFirst is not null)
            return Task.FromResult<IReadOnlyList<VerifiedTenantHostBinding>?>(
                AlternateAfterFirst);
        return read(host, ct);
    }
}
sealed class TestMembershipReader(
    Func<TenantActorId, TenantId, CancellationToken,
        Task<IReadOnlyList<TenantActorMembershipSnapshot>?>> read)
    : ITrustedActorTenantMembershipReader
{
    public int Calls { get; private set; }
    public TenantActorId? LastActor { get; private set; }
    public TenantId? LastTenant { get; private set; }
    public Task<IReadOnlyList<TenantActorMembershipSnapshot>?> FindExactMembershipsAsync(
        TenantActorId actorId, TenantId tenantId, CancellationToken ct)
    {
        Calls++;
        LastActor = actorId;
        LastTenant = tenantId;
        return read(actorId, tenantId, ct);
    }
}
sealed class TestTenantReader(
    Func<TenantId, CancellationToken, Task<TenantCatalogSnapshot?>> read)
    : ITrustedTenantCatalogReader
{
    public int Calls { get; private set; }
    public TenantId? LastTenant { get; private set; }
    public Task<TenantCatalogSnapshot?> FindByIdAsync(TenantId id, CancellationToken ct)
    {
        Calls++;
        LastTenant = id;
        return read(id, ct);
    }
}
sealed class TestClock(Func<int, DateTimeOffset> getTime) : ITrustedHostUtcClock
{
    public int Calls { get; private set; }
    public DateTimeOffset GetUtcNow()
    {
        Calls++;
        return getTime(Calls);
    }
}
