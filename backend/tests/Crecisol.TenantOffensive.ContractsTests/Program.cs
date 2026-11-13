using System.Reflection;
using Crecisol.Application.Tenancy;
using Crecisol.Domain.Tenancy;

// C04-11: synthetic adversarial contract tests ONLY; no private HTTP, JWT,
// productive DB router, R2, Redis, worker or customer system is contacted.
// A negative test succeeds only when real Domain/Application guards reject.
var positives = 0;
var negatives = 0;
var now = new DateTimeOffset(2026, 11, 12, 22, 0, 0, TimeSpan.Zero);
var earlier = now.AddDays(-2);
var granted = now.AddDays(-3);
var expires = now.AddDays(4);
var a = TenantId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
var b = TenantId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
var actorA = TenantActorId.From(Guid.Parse("11111111-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
var actorB = TenantActorId.From(Guid.Parse("22222222-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
var hostA = CanonicalTenantHost.Parse("alpha.example.test");
var hostB = CanonicalTenantHost.Parse("beta.example.test");
var branchA = TenantBranchId.From(Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"));
var branchB = TenantBranchId.From(Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001"));
// Deliberately identical opaque resource ID in two valid tenant catalogs.
var resourceId = TenantResourceId.From(Guid.Parse("eeeeeeee-0000-0000-0000-000000000001"));
var read = TenantRequiredAction.FromServerPolicy("cases.read");
var write = TenantRequiredAction.FromServerPolicy("cases.update");
var operation = Guid.Parse("33333333-3333-3333-3333-333333333333");
var secret = Enumerable.Range(1, 48).Select(x => checked((byte)x)).ToArray();

void Positive(bool test, string label)
{
    if (!test) throw new Exception("C04-11 POSITIVE FAILED: " + label);
    positives++;
}
void Deny(Action attack, string label)
{
    try { attack(); }
    catch (UnauthorizedAccessException) { negatives++; return; }
    catch (InvalidOperationException) { negatives++; return; }
    throw new Exception("C04-11 SECURITY BYPASS: " + label);
}
async Task DenyAsync(Func<Task> attack, string label)
{
    try { await attack(); }
    catch (UnauthorizedAccessException) { negatives++; return; }
    catch (InvalidOperationException) { negatives++; return; }
    throw new Exception("C04-11 SECURITY BYPASS: " + label);
}

// O11-03: host parser, verified catalog ambiguity, time and rebinding.
foreach (var attack in new string?[] {
    null, "", " ", "localhost", "shop.localhost", "127.0.0.1",
    "alpha.example.test:3306", "alpha.example.test/admin",
    "http://alpha.example.test", "user@alpha.example.test",
    "alpha.example.test?tenant=b", "*.example.test",
    "alpha.example.test\\admin", "alpha..example.test",
    "alpha.example.test\r\nX-Forwarded-Host:beta.example.test",
    ".alpha.example.test", "alpha.example.test.",
    "alpha.example.test\0", "alpha_example.test",
    "alpha.example.test ", " alpha.example.test"
})
    Deny(() => CanonicalTenantHost.Parse(attack), "O11-03 malformed host candidate");

var canonical = CanonicalTenantHost.Parse("ALPHA.Example.Test");
Positive(canonical == hostA, "O11-03 legitimate host normalized only for lookup");
VerifiedTenantHostBinding Host(TenantId tenant, CanonicalTenantHost host,
    HostVerificationState state = HostVerificationState.Verified,
    long rev = 2, DateTimeOffset? until = null) =>
    VerifiedTenantHostBinding.RestoreFromTrustedCatalog(
        tenant, host, TenantAddressMode.Subdomain, state, rev, earlier,
        granted, until ?? expires);
var hostProofA = Host(a, hostA);
var hostProofB = Host(b, hostB);
ResolveVerifiedTenantHost HostResolver(IReadOnlyList<VerifiedTenantHostBinding>? matches) =>
    new(new HostCatalog((_, _) => Task.FromResult(matches)), new FixedClock(now));
var hint = await HostResolver(new[] { hostProofA }).ResolveAsync(hostA, CancellationToken.None);
Positive(hint.TenantId == a, "O11-03 valid host yields A contextual hint ONLY");
await DenyAsync(() => HostResolver(new[] { hostProofA, Host(b, hostA) })
    .ResolveAsync(hostA, CancellationToken.None), "O11-03 two valid tenants share host");
await DenyAsync(() => HostResolver(new[] { hostProofA, hostProofA })
    .ResolveAsync(hostA, CancellationToken.None), "O11-03 duplicated same-tenant ownership");
await DenyAsync(() => HostResolver(new[] { hostProofB })
    .ResolveAsync(hostA, CancellationToken.None), "O11-03 wrong binding returned for host");
await DenyAsync(() => HostResolver(new[] { Host(a, hostA,
    HostVerificationState.Revoked) }).ResolveAsync(hostA, CancellationToken.None),
    "O11-03 revoked verification fails");
await DenyAsync(() => HostResolver(new[] { Host(a, hostA,
    until: now.AddSeconds(-1)) }).ResolveAsync(hostA, CancellationToken.None),
    "O11-03 expired host fails");
await DenyAsync(() => HostResolver(null).ResolveAsync(hostA, CancellationToken.None),
    "O11-03 missing host catalog fails closed");

TrustedActorSessionSnapshot Session(TenantActorId actor,
    ActorSessionState state = ActorSessionState.Active) =>
    TrustedActorSessionSnapshot.RestoreFromTrustedIdentityProvider(
        actor, state, 2, now.AddDays(-5), now.AddDays(-1), now.AddDays(5));
TenantActorMembershipSnapshot Member(TenantActorId actor, TenantId tenant,
    TenantMembershipState state = TenantMembershipState.Active) =>
    TenantActorMembershipSnapshot.RestoreFromTrustedMembershipCatalog(
        actor, tenant, state, 3, earlier, granted, expires);
TenantCatalogSnapshot Catalog(TenantId tenant,
    TenantLifecycleState state = TenantLifecycleState.Active) =>
    TenantCatalogSnapshot.RestoreFromTrustedCatalog(
        tenant, "synthetic-catalog", "Synthetic", state,
        TenantAddressMode.Subdomain, 4, earlier);
ResolveCurrentTenantIdentityContext Identity(
    TenantId tenant, TenantActorId actor, CanonicalTenantHost host,
    TenantActorMembershipSnapshot? membershipOverride = null,
    TenantCatalogSnapshot? catalogOverride = null,
    VerifiedTenantHostBinding? hostOverride = null,
    ActorSessionState sessionState = ActorSessionState.Active)
{
    return new ResolveCurrentTenantIdentityContext(
        new ActorReader(_ => ValueTask.FromResult<TrustedActorSessionSnapshot?>(
            Session(actor, sessionState))),
        new DirectHostReader(_ => ValueTask.FromResult<CanonicalTenantHost?>(host)),
        new ResolveVerifiedTenantHost(
            new HostCatalog((_, _) =>
                Task.FromResult<IReadOnlyList<VerifiedTenantHostBinding>?>(
                    new[] { hostOverride ?? Host(tenant, host) })),
            new FixedClock(now)),
        new MemberReader((_, _, _) =>
            Task.FromResult<IReadOnlyList<TenantActorMembershipSnapshot>?>(
                new[] { membershipOverride ?? Member(actor, tenant) })),
        new TenantReader((_, _) =>
            Task.FromResult<TenantCatalogSnapshot?>(catalogOverride ?? Catalog(tenant))),
        new FixedClock(now));
}

// O11-04: fake untrusted headers/claims are NEVER inputs to these backend
// ports; they cannot select a second tenant or privilege escalation.
var forgedEnvelope = new Dictionary<string, string> {
    ["Host"] = "beta.example.test",
    ["Forwarded"] = "host=beta.example.test",
    ["X-Forwarded-Host"] = "beta.example.test",
    ["X-Tenant-Id"] = b.Value.ToString(),
    ["Authorization"] = "Bearer forged-without-signature",
    ["role"] = "SuperAdmin",
    ["tenant_id"] = b.Value.ToString(),
    ["dsn"] = "mysql://unauthorized-host/tenant-b"
};
var verifiedContext = await Identity(a, actorA, hostA).ExecuteAsync(CancellationToken.None);
Positive(verifiedContext.TenantId == a && verifiedContext.ActorId == actorA &&
    forgedEnvelope["tenant_id"] == b.Value.ToString(),
    "O11-04 independent trusted identity unaffected by forged claim envelope");
await DenyAsync(() => Identity(a, actorA, hostA,
    membershipOverride: Member(actorA, b)).ExecuteAsync(CancellationToken.None),
    "O11-04 valid membership for B cannot impersonate A");
await DenyAsync(() => Identity(a, actorA, hostA,
    membershipOverride: Member(actorB, a)).ExecuteAsync(CancellationToken.None),
    "O11-04 stolen actor membership cannot impersonate A");
await DenyAsync(() => Identity(a, actorA, hostA,
    hostOverride: Host(b, hostA)).ExecuteAsync(CancellationToken.None),
    "O11-04 verified host rebound to B rejects A identity");
await DenyAsync(() => Identity(a, actorA, hostA,
    catalogOverride: Catalog(b)).ExecuteAsync(CancellationToken.None),
    "O11-04 protected tenant catalog swapped to B");
await DenyAsync(() => Identity(a, actorA, hostA,
    sessionState: ActorSessionState.Revoked).ExecuteAsync(CancellationToken.None),
    "O11-04 revoked actor cannot continue");
var verifiedB = await Identity(b, actorB, hostB).ExecuteAsync(CancellationToken.None);
Positive(verifiedB.TenantId == b && verifiedB.ActorId == actorB,
    "O11-04 legitimate B independent identity");

TrustedBranchResourceSnapshot Resource(TenantId tenant, TenantBranchId branch,
    TenantActorId owner, long rev = 4) =>
    TrustedBranchResourceSnapshot.RestoreFromTrustedResourceReader(
        tenant, branch, resourceId, owner, TrustedBranchState.Active,
        11, earlier, TrustedResourceState.Active, rev, earlier);
TrustedActorBranchGrantSnapshot Grant(TenantId tenant, TenantActorId actor,
    TenantBranchId branch, TenantRequiredAction action,
    BranchGrantState state = BranchGrantState.Active) =>
    TrustedActorBranchGrantSnapshot.RestoreFromTrustedGrantReader(
        actor, tenant, branch, action, BranchGrantScope.OwnerOnly,
        state, 7, earlier, granted, expires);

var aResource = Resource(a, branchA, actorA);
var bResource = Resource(b, branchB, actorB);
var aGrant = Grant(a, actorA, branchA, read);
var bGrant = Grant(b, actorB, branchB, read);
ResolveCurrentBranchResourceScope ScopeResolver(
    TenantId tenant, TenantActorId actor, CanonicalTenantHost host,
    Func<int, TenantId, TenantResourceId, TrustedBranchResourceSnapshot?> resource,
    Func<int, TenantActorId, TenantId, TenantBranchId, TenantRequiredAction,
        IReadOnlyList<TrustedActorBranchGrantSnapshot>?> grant)
{
    var resourceCalls = 0;
    var grantCalls = 0;
    return new ResolveCurrentBranchResourceScope(
        Identity(tenant, actor, host),
        new ResourceReader((t, id, _) =>
            Task.FromResult(resource(++resourceCalls, t, id))),
        new GrantReader((actorId, t, br, act, _) =>
            Task.FromResult(grant(++grantCalls, actorId, t, br, act))),
        new FixedClock(now));
}

// O11-01/O11-02: real C04-06 case use, not an invented authorization mock.
var validScopeA = await ScopeResolver(a, actorA, hostA,
    (_, _, _) => aResource, (_, _, _, _, _) => new[] { aGrant })
    .ExecuteAsync(resourceId, read, CancellationToken.None);
Positive(validScopeA.TenantId == a && validScopeA.ActorId == actorA &&
    validScopeA.ResourceId == resourceId, "O11-01 valid A scope");
var validScopeB = await ScopeResolver(b, actorB, hostB,
    (_, _, _) => bResource, (_, _, _, _, _) => new[] { bGrant })
    .ExecuteAsync(resourceId, read, CancellationToken.None);
Positive(validScopeB.TenantId == b && validScopeB.BranchId == branchB &&
    validScopeB.ResourceId == resourceId && validScopeA.TenantId != validScopeB.TenantId,
    "O11-01 B can use same resource_id only in B");
await DenyAsync(() => ScopeResolver(a, actorA, hostA,
    (_, _, _) => bResource, (_, _, _, _, _) => new[] { aGrant })
    .ExecuteAsync(resourceId, read, CancellationToken.None),
    "O11-01 BOLA A cannot access B resource by matching ID");
await DenyAsync(() => ScopeResolver(a, actorA, hostA,
    (_, _, _) => aResource, (_, _, _, _, _) => new[] { bGrant })
    .ExecuteAsync(resourceId, read, CancellationToken.None),
    "O11-02 B grant cannot authorize A resource");
await DenyAsync(() => ScopeResolver(a, actorA, hostA,
    (_, _, _) => Resource(a, branchB, actorA),
    (_, _, _, _, _) => new[] { aGrant })
    .ExecuteAsync(resourceId, read, CancellationToken.None),
    "O11-02 branch IDOR requires distinct matching grant");
await DenyAsync(() => ScopeResolver(a, actorA, hostA,
    (_, _, _) => Resource(a, branchA, actorB),
    (_, _, _, _, _) => new[] { aGrant })
    .ExecuteAsync(resourceId, read, CancellationToken.None),
    "O11-02 owner-only grant fails when owner changed");
await DenyAsync(() => ScopeResolver(a, actorA, hostA,
    (_, _, _) => aResource, (_, _, _, _, _) => new[] { aGrant })
    .ExecuteAsync(resourceId, write, CancellationToken.None),
    "O11-02 a read grant is never a write permission");
await DenyAsync(() => ScopeResolver(a, actorA, hostA,
    (_, _, _) => aResource, (_, _, _, _, _) => new[] { aGrant, aGrant })
    .ExecuteAsync(resourceId, read, CancellationToken.None),
    "O11-02 duplicated grants never silently choose one");
await DenyAsync(() => ScopeResolver(a, actorA, hostA,
    (n, _, _) => n == 1 ? aResource : Resource(a, branchA, actorB, 5),
    (_, _, _, _, _) => new[] { aGrant })
    .ExecuteAsync(resourceId, read, CancellationToken.None),
    "O11-09 owner/revision rebind during resolution denied");
await DenyAsync(() => ScopeResolver(a, actorA, hostA,
    (_, _, _) => aResource,
    (n, _, _, _, _) => n == 1 ? new[] { aGrant } : Array.Empty<TrustedActorBranchGrantSnapshot>())
    .ExecuteAsync(resourceId, read, CancellationToken.None),
    "O11-09 grant revoked between catalog reads denied");
await DenyAsync(() => ScopeResolver(a, actorA, hostA,
    (_, _, _) => aResource,
    (_, _, _, _, _) => new[] { Grant(a, actorA, branchA, read,
        BranchGrantState.Revoked) })
    .ExecuteAsync(resourceId, read, CancellationToken.None),
    "O11-09 revoked grant denied");

// O11-05: direct placement selector/DSN injection is not a parameter.
// The trusted backend selects A independently of the forged envelope.
var dedicatedB = TenantStoragePlacementSnapshot.RestoreFromTrustedCatalog(
    b, TenantPlacementMode.Dedicated,
    DedicatedStorageSlotId.FromTrustedCatalog(
        Guid.Parse("bbbbbbbb-ffff-ffff-ffff-ffffffffffff")), 3, earlier);
var sharedA = TenantStoragePlacementSnapshot.RestoreFromTrustedCatalog(
    a, TenantPlacementMode.Shared, null, 4, earlier);
var placement = await new ResolveCurrentTenantStorage(
    new PlacementAuth(a), new PlacementCatalog(_ => sharedA))
    .ExecuteAsync(CancellationToken.None);
Positive(placement == sharedA && placement.DedicatedSlot is null &&
    forgedEnvelope["dsn"].StartsWith("mysql://", StringComparison.Ordinal),
    "O11-05 forged external DSN cannot influence server placement");
await DenyAsync(() => new ResolveCurrentTenantStorage(
    new PlacementAuth(a), new PlacementCatalog(_ => dedicatedB))
    .ExecuteAsync(CancellationToken.None),
    "O11-05 trusted catalog returning B for authorized A rejected");
var catalogCalls = new PlacementCatalog(_ => sharedA);
await DenyAsync(() => new ResolveCurrentTenantStorage(
    new PlacementAuth(null), catalogCalls).ExecuteAsync(CancellationToken.None),
    "O11-05 unauthenticated DSN selector blocked before storage lookup");
Positive(catalogCalls.Calls == 0, "O11-05 no storage I/O before server policy");
Positive(typeof(ResolveCurrentTenantStorage).GetMethod("ExecuteAsync")!
    .GetParameters().Select(p => p.ParameterType).SequenceEqual(
        new[] { typeof(CancellationToken) }),
    "O11-05 route cannot accept HTTP tenant/DSN/slug selector");
Positive(typeof(TenantStoragePlacementSnapshot)
    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
    .All(p => p.PropertyType != typeof(string) && !p.CanWrite),
    "O11-05 protected placement metadata contains no DSN property");

// O11-06: stolen or replayed cache/job/media/export key is never an access grant.
string ActionFor(TenantArtifactKind kind) => kind switch
{
    TenantArtifactKind.Cache => "tenant.cache.read",
    TenantArtifactKind.Job => "tenant.job.execute",
    TenantArtifactKind.Media => "tenant.media.read",
    TenantArtifactKind.Export => "tenant.export.create",
    _ => throw new InvalidOperationException("Unknown artifact")
};
TrustedTenantArtifactScope Artifact(TenantId tenant, TenantActorId actor,
    TenantBranchId branch, TenantArtifactKind kind,
    long epoch = 1, string variant = "detail.v1") =>
    TrustedTenantArtifactScope.FromVerifiedBackendDecision(
        operation, kind, TenantArtifactEnvironment.ContinuousIntegration,
        tenant, branch, resourceId, actor,
        TenantRequiredAction.FromServerPolicy(ActionFor(kind)),
        TenantPlacementMode.Shared, null, TenantLifecycleState.Active,
        ActorSessionState.Active, TenantMembershipState.Active,
        TrustedBranchState.Active, TrustedResourceState.Active,
        epoch, 2, 3, 4, 5, 6, now.AddMinutes(-1),
        now.AddMinutes(8), variant);
foreach (var kind in Enum.GetValues<TenantArtifactKind>())
{
    var planA = Artifact(a, actorA, branchA, kind);
    var planB = Artifact(b, actorB, branchB, kind);
    var keyA = TenantPrivateArtifactKey.Derive(planA, secret);
    var keyB = TenantPrivateArtifactKey.Derive(planB, secret);
    Positive(keyA != keyB && TenantPrivateArtifactKey.IsCanonicalForKind(keyA, kind),
        "O11-06 distinct tenant keys for " + kind);
    Positive(keyA != TenantPrivateArtifactKey.Derive(
        Artifact(a, actorA, branchA, kind, variant: "summary.v1"), secret),
        "O11-06 canonical response variant isolates " + kind);
    var goodReceipt = TrustedTenantArtifactReceipt.FromVerifiedProtectedGateway(
        operation, kind, planA.IntentFingerprint, keyA, 1, 1,
        TenantArtifactReceiptState.Applied, now);
    TrustedTenantArtifactReceipt.RequireExactMatch(planA, keyA, goodReceipt, now);
    Positive(true, "O11-06 exact A receipt accepted " + kind);
    var stolenReceipt = TrustedTenantArtifactReceipt.FromVerifiedProtectedGateway(
        operation, kind, planB.IntentFingerprint, keyB, 1, 1,
        TenantArtifactReceiptState.Replayed, now);
    Deny(() => TrustedTenantArtifactReceipt.RequireExactMatch(
        planA, keyA, stolenReceipt, now),
        "O11-06 B receipt rejected for A " + kind);
    Deny(() => TrustedTenantArtifactReceipt.RequireExactMatch(
        Artifact(a, actorA, branchA, kind, epoch: 2), keyA, goodReceipt, now),
        "O11-06 epoch change revokes stale receipt " + kind);
    Deny(() => TrustedTenantArtifactReceipt.RequireExactMatch(
        planA, keyA, goodReceipt, now.AddMinutes(9)),
        "O11-06 stale receipt cannot authorize after expiry " + kind);
    var protectedGateway = new Gateway(now);
    var coordinator = new ApplyScopedTenantArtifact(
        new DecisionReader(_ => planB), new Deriver(secret), protectedGateway,
        new FixedClock(now));
    await DenyAsync(() => coordinator.ExecuteAsync(operation, planA,
        CancellationToken.None),
        "O11-06 malicious reader B cannot replace independently verified A " + kind);
    Positive(protectedGateway.Calls == 0,
        "O11-06 denied mismatch causes no gateway calls " + kind);
}
// Cancellation path never triggers external storage or a catalog fallback.
var cancelledGateway = new Gateway(now);
using var cancel = new CancellationTokenSource();
cancel.Cancel();
try
{
    await new ApplyScopedTenantArtifact(
        new DecisionReader(_ => Artifact(a, actorA, branchA, TenantArtifactKind.Job)),
        new Deriver(secret), cancelledGateway, new FixedClock(now))
        .ExecuteAsync(operation, Artifact(a, actorA, branchA, TenantArtifactKind.Job),
            cancel.Token);
    throw new Exception("C04-11 SECURITY BYPASS: cancellation must deny");
}
catch (OperationCanceledException) { negatives++; }
Positive(cancelledGateway.Calls == 0, "O11-09 cancellation never invokes protected gateway");

Positive(typeof(ResolveCurrentTenantIdentityContext)
    .GetMethod("ExecuteAsync")!.GetParameters().Select(p => p.ParameterType)
    .SequenceEqual(new[] { typeof(CancellationToken) }),
    "O11-04 actor/host resolver has no forged claim or tenant input");
Positive(typeof(ResolveCurrentBranchResourceScope)
    .GetMethod("ExecuteAsync")!.GetParameters().Select(p => p.ParameterType)
    .SequenceEqual(new[] { typeof(TenantResourceId), typeof(TenantRequiredAction),
        typeof(CancellationToken) }),
    "O11-01 resource selector and backend action, no arbitrary tenant input");

Console.WriteLine($"C04-11 offensive .NET 10 contracts SUCCESS: {positives} positive, " +
    $"{negatives} negative; vectors O11-01..06/O11-09, no private HTTP/DSN or production effects.");

sealed class FixedClock(DateTimeOffset now) : ITrustedHostUtcClock
{
    public DateTimeOffset GetUtcNow() => now;
}
sealed class HostCatalog(Func<CanonicalTenantHost, CancellationToken,
    Task<IReadOnlyList<VerifiedTenantHostBinding>?>> load) : ITrustedVerifiedHostCatalog
{
    public Task<IReadOnlyList<VerifiedTenantHostBinding>?> FindExactBindingsAsync(
        CanonicalTenantHost host, CancellationToken ct) => load(host, ct);
}
sealed class ActorReader(Func<CancellationToken, ValueTask<TrustedActorSessionSnapshot?>> load)
    : ITrustedCurrentActorSessionReader
{
    public ValueTask<TrustedActorSessionSnapshot?> ReadCurrentVerifiedSessionAsync(
        CancellationToken ct) => load(ct);
}
sealed class DirectHostReader(Func<CancellationToken, ValueTask<CanonicalTenantHost?>> load)
    : ITrustedCurrentDirectHostReader
{
    public ValueTask<CanonicalTenantHost?> ReadCurrentDirectHostAsync(CancellationToken ct) =>
        load(ct);
}
sealed class MemberReader(Func<TenantActorId, TenantId, CancellationToken,
    Task<IReadOnlyList<TenantActorMembershipSnapshot>?>> load)
    : ITrustedActorTenantMembershipReader
{
    public Task<IReadOnlyList<TenantActorMembershipSnapshot>?> FindExactMembershipsAsync(
        TenantActorId actor, TenantId tenant, CancellationToken ct) => load(actor, tenant, ct);
}
sealed class TenantReader(Func<TenantId, CancellationToken, Task<TenantCatalogSnapshot?>> load)
    : ITrustedTenantCatalogReader
{
    public Task<TenantCatalogSnapshot?> FindByIdAsync(TenantId id, CancellationToken ct) =>
        load(id, ct);
}
sealed class ResourceReader(Func<TenantId, TenantResourceId, CancellationToken,
    Task<TrustedBranchResourceSnapshot?>> load) : ITrustedTenantScopedResourceReader
{
    public Task<TrustedBranchResourceSnapshot?> FindByTenantAndResourceIdAsync(
        TenantId tenant, TenantResourceId resource, CancellationToken ct) =>
        load(tenant, resource, ct);
}
sealed class GrantReader(Func<TenantActorId, TenantId, TenantBranchId,
    TenantRequiredAction, CancellationToken,
    Task<IReadOnlyList<TrustedActorBranchGrantSnapshot>?>> load) : ITrustedActorBranchGrantReader
{
    public Task<IReadOnlyList<TrustedActorBranchGrantSnapshot>?> FindExactGrantsAsync(
        TenantActorId actor, TenantId tenant, TenantBranchId branch,
        TenantRequiredAction action, CancellationToken ct) =>
        load(actor, tenant, branch, action, ct);
}
sealed class PlacementAuth(TenantId? tenant) : ICurrentTenantPlacementAuthorization
{
    public ValueTask<TenantId?> ResolveAuthorizedCurrentTenantAsync(CancellationToken ct) =>
        ValueTask.FromResult(tenant);
}
sealed class PlacementCatalog(Func<TenantId, TenantStoragePlacementSnapshot?> load)
    : ITrustedTenantPlacementReader
{
    public int Calls { get; private set; }
    public Task<TenantStoragePlacementSnapshot?> FindPlacementAsync(
        TenantId tenant, CancellationToken ct)
    {
        Calls++;
        return Task.FromResult(load(tenant));
    }
}
sealed class DecisionReader(Func<int, TrustedTenantArtifactScope?> load)
    : ITrustedTenantArtifactDecisionReader
{
    public int Calls { get; private set; }
    public Task<TrustedTenantArtifactScope?> ReadCurrentApprovedScopeAsync(
        Guid operation, CancellationToken ct) => Task.FromResult(load(++Calls));
}
sealed class Deriver(byte[] key) : IProtectedTenantArtifactKeyDeriver
{
    public Task<string> DeriveOpaqueKeyAsync(TrustedTenantArtifactScope scope,
        CancellationToken ct) => Task.FromResult(
            TenantPrivateArtifactKey.Derive(scope, key));
}
sealed class Gateway(DateTimeOffset now) : IProtectedTenantArtifactGateway
{
    public int Calls { get; private set; }
    public Task<TrustedTenantArtifactReceipt> ApplyOrReplayAsync(
        TrustedTenantArtifactScope plan, string key, CancellationToken ct)
    {
        Calls++;
        return Task.FromResult(TrustedTenantArtifactReceipt.FromVerifiedProtectedGateway(
            plan.OperationId, plan.Kind, plan.IntentFingerprint, key,
            plan.TenantAccessEpoch, 1, TenantArtifactReceiptState.Applied, now));
    }
}
