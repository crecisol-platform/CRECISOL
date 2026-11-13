using Crecisol.Application.Tenancy;
using Crecisol.Domain.Tenancy;

var positive = 0;
var negative = 0;
void Check(bool ok, string title)
{
    if (!ok) throw new Exception("C04-10 ASSERTION FAILED: " + title);
    positive++;
}
void Reject<T>(Action callback, string title) where T : Exception
{
    try { callback(); }
    catch (T) { negative++; return; }
    throw new Exception("C04-10 MUST DENY " + title + " (" + typeof(T).Name + ")");
}
async Task RejectAsync<T>(Func<Task> callback, string title) where T : Exception
{
    try { await callback(); }
    catch (T) { negative++; return; }
    throw new Exception("C04-10 MUST DENY " + title + " (" + typeof(T).Name + ")");
}

var now = new DateTimeOffset(2026, 11, 12, 21, 0, 0, TimeSpan.Zero);
var tenantA = TenantId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
var tenantB = TenantId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
var branchA = TenantBranchId.From(Guid.Parse("c0000000-0000-0000-0000-000000000001"));
var branchB = TenantBranchId.From(Guid.Parse("c0000000-0000-0000-0000-000000000002"));
var resourceA = TenantResourceId.From(Guid.Parse("d0000000-0000-0000-0000-000000000001"));
var resourceB = TenantResourceId.From(Guid.Parse("d0000000-0000-0000-0000-000000000002"));
var actorA = TenantActorId.From(Guid.Parse("e0000000-0000-0000-0000-000000000001"));
var actorB = TenantActorId.From(Guid.Parse("e0000000-0000-0000-0000-000000000002"));
var slotA = DedicatedStorageSlotId.FromTrustedCatalog(
    Guid.Parse("f0000000-0000-0000-0000-000000000001"));
var slotB = DedicatedStorageSlotId.FromTrustedCatalog(
    Guid.Parse("f0000000-0000-0000-0000-000000000002"));
var operationA = Guid.Parse("11111111-1111-1111-1111-111111111111");
var operationB = Guid.Parse("22222222-2222-2222-2222-222222222222");
var keySecret = Enumerable.Range(7, 48).Select(i => checked((byte)i)).ToArray();

string ActionKey(TenantArtifactKind kind) => kind switch
{
    TenantArtifactKind.Cache => "tenant.cache.read",
    TenantArtifactKind.Job => "tenant.job.execute",
    TenantArtifactKind.Media => "tenant.media.read",
    TenantArtifactKind.Export => "tenant.export.create",
    _ => "tenant.invalid.action"
};
TrustedTenantArtifactScope Scope(TenantArtifactKind kind,
    TenantId? tenant = null, TenantBranchId? branch = null,
    TenantResourceId? resource = null, TenantActorId? actor = null,
    Guid? operation = null, TenantArtifactEnvironment environment = TenantArtifactEnvironment.ContinuousIntegration,
    TenantPlacementMode placement = TenantPlacementMode.Shared,
    DedicatedStorageSlotId? slot = null,
    TenantLifecycleState tenantState = TenantLifecycleState.Active,
    ActorSessionState sessionState = ActorSessionState.Active,
    TenantMembershipState membershipState = TenantMembershipState.Active,
    TrustedBranchState branchState = TrustedBranchState.Active,
    TrustedResourceState resourceState = TrustedResourceState.Active,
    long tenantEpoch = 7, long sessionRevision = 3,
    long membershipRevision = 2, long branchRevision = 4,
    long resourceRevision = 5, long placementRevision = 9,
    string? requiredAction = null, DateTimeOffset? verifiedAt = null,
    DateTimeOffset? expiresAt = null, string serverVariant = "detail.v1") =>
    TrustedTenantArtifactScope.FromVerifiedBackendDecision(
        operation ?? operationA, kind, environment,
        tenant ?? tenantA, branch ?? branchA, resource ?? resourceA, actor ?? actorA,
        TenantRequiredAction.FromServerPolicy(requiredAction ?? ActionKey(kind)),
        placement, slot, tenantState, sessionState, membershipState,
        branchState, resourceState,
        tenantEpoch, sessionRevision, membershipRevision, branchRevision,
        resourceRevision, placementRevision, verifiedAt ?? now.AddMinutes(-1),
        expiresAt ?? now.AddMinutes(10), serverVariant);

TrustedTenantArtifactReceipt Receipt(TrustedTenantArtifactScope scope,
    string? key = null, Guid? operation = null, string? fingerprint = null,
    TenantArtifactKind? kind = null, long? tenantEpoch = null,
    long generation = 1, DateTimeOffset? confirmedAt = null,
    TenantArtifactReceiptState state = TenantArtifactReceiptState.Applied) =>
    TrustedTenantArtifactReceipt.FromVerifiedProtectedGateway(
        operation ?? scope.OperationId, kind ?? scope.Kind,
        fingerprint ?? scope.IntentFingerprint,
        key ?? TenantPrivateArtifactKey.Derive(scope, keySecret),
        tenantEpoch ?? scope.TenantAccessEpoch, generation, state,
        confirmedAt ?? now);

foreach (var kind in Enum.GetValues<TenantArtifactKind>())
{
    var x = Scope(kind);
    var key = TenantPrivateArtifactKey.Derive(x, keySecret);
    var same = Scope(kind);
    Check(x.IsFreshAtUtc(now), kind + " authorized snapshot freshness");
    Check(x == same, kind + " deterministic trusted snapshot");
    Check(key == TenantPrivateArtifactKey.Derive(same, keySecret),
        kind + " exact deterministic protected key");
    Check(TenantPrivateArtifactKey.IsCanonicalForKind(key, kind),
        kind + " canonical HMAC key");
    Check(!key.Contains(tenantA.Value.ToString("N"), StringComparison.Ordinal),
        kind + " tenant opaque in key");
    Check(!key.Contains(branchA.Value.ToString("N"), StringComparison.Ordinal),
        kind + " branch opaque in key");
    Check(!key.Contains(actorA.Value.ToString("N"), StringComparison.Ordinal),
        kind + " actor opaque in key");
    Check(x.IsolationFingerprint.Length == 64 && x.IntentFingerprint.Length == 64,
        kind + " binding fingerprints fixed length");
    Check(TenantPrivateArtifactKey.Derive(Scope(kind, tenant: tenantB), keySecret) != key,
        kind + " valid tenant B never aliases valid tenant A");
    Check(TenantPrivateArtifactKey.Derive(Scope(kind, branch: branchB), keySecret) != key,
        kind + " branch B isolated");
    Check(TenantPrivateArtifactKey.Derive(Scope(kind, resource: resourceB), keySecret) != key,
        kind + " resource B isolated");
    Check(TenantPrivateArtifactKey.Derive(Scope(kind, actor: actorB), keySecret) != key,
        kind + " actor B isolated");
    Check(TenantPrivateArtifactKey.Derive(Scope(kind, tenantEpoch: 8), keySecret) != key,
        kind + " epoch rotation prevents stale cache/job/media/export");
    Check(TenantPrivateArtifactKey.Derive(Scope(kind, sessionRevision: 4), keySecret) != key,
        kind + " session revision isolates");
    Check(TenantPrivateArtifactKey.Derive(Scope(kind, membershipRevision: 3), keySecret) != key,
        kind + " membership revision isolates");
    Check(TenantPrivateArtifactKey.Derive(Scope(kind, branchRevision: 5), keySecret) != key,
        kind + " branch revision isolates");
    Check(TenantPrivateArtifactKey.Derive(Scope(kind, resourceRevision: 6), keySecret) != key,
        kind + " resource revision isolates");
    Check(TenantPrivateArtifactKey.Derive(Scope(kind, serverVariant: "summary.v1"),
        keySecret) != key, kind + " distinct server-side projection cannot alias key");
    Check(TenantPrivateArtifactKey.Derive(Scope(kind, placementRevision: 10), keySecret) != key,
        kind + " placement revision isolates");
    Check(TenantPrivateArtifactKey.Derive(Scope(kind,
        environment: TenantArtifactEnvironment.Staging), keySecret) != key,
        kind + " environment namespace isolated");
    Check(TenantPrivateArtifactKey.Derive(Scope(kind,
        placement: TenantPlacementMode.Dedicated, slot: slotA), keySecret) != key,
        kind + " Shared and Dedicated never alias");
    Check(TenantPrivateArtifactKey.Derive(Scope(kind,
        placement: TenantPlacementMode.Dedicated, slot: slotA), keySecret) !=
        TenantPrivateArtifactKey.Derive(Scope(kind,
        placement: TenantPlacementMode.Dedicated, slot: slotB), keySecret),
        kind + " Dedicated slots isolated");
    Check(Receipt(x).OpaqueKey == key, kind + " valid receipt binds exact HMAC");
    TrustedTenantArtifactReceipt.RequireExactMatch(x, key, Receipt(x), now);
    Check(true, kind + " matching receipt accepted");
    Check(x.ToString() == "TrustedTenantArtifactScope[redacted]" &&
        Receipt(x).ToString() == "TrustedTenantArtifactReceipt[redacted]",
        kind + " intent and receipt redacted");
    Check(x.IsolationFingerprint == Scope(kind, operation: operationB).IsolationFingerprint,
        kind + " private resource scope independent of operation");
    Check(x.IntentFingerprint != Scope(kind, operation: operationB).IntentFingerprint,
        kind + " intent bound to operation");
    var alternateKey = TenantPrivateArtifactKey.Derive(
        Scope(kind, operation: operationB), keySecret);
    Check((alternateKey == key) == (kind == TenantArtifactKind.Cache),
        kind + " cache stable but jobs/media/exports operation-scoped");
    Check(!x.IsFreshAtUtc(now.AddMinutes(20)), kind + " expiry blocks late consumers");

    Reject<UnauthorizedAccessException>(() => Scope(kind,
        tenantState: TenantLifecycleState.Suspended), kind + " suspended tenant");
    Reject<UnauthorizedAccessException>(() => Scope(kind,
        tenantState: TenantLifecycleState.Deactivated), kind + " deactivated tenant");
    Reject<UnauthorizedAccessException>(() => Scope(kind,
        sessionState: ActorSessionState.Revoked), kind + " revoked actor session");
    Reject<UnauthorizedAccessException>(() => Scope(kind,
        membershipState: TenantMembershipState.Revoked), kind + " revoked membership");
    Reject<UnauthorizedAccessException>(() => Scope(kind,
        branchState: TrustedBranchState.Suspended), kind + " suspended branch");
    Reject<UnauthorizedAccessException>(() => Scope(kind,
        resourceState: TrustedResourceState.Suspended), kind + " suspended resource");
    Reject<UnauthorizedAccessException>(() => Scope(kind,
        tenantEpoch: 0), kind + " invalid tenant epoch");
    Reject<UnauthorizedAccessException>(() => Scope(kind,
        sessionRevision: 0), kind + " invalid session revision");
    Reject<UnauthorizedAccessException>(() => Scope(kind,
        placementRevision: 0), kind + " invalid placement revision");
    Reject<UnauthorizedAccessException>(() => Scope(kind,
        requiredAction: "tenant.invalid.action"), kind + " forged action/purpose");
    Reject<UnauthorizedAccessException>(() => Scope(kind,
        serverVariant: ""), kind + " variant must be explicitly server-owned");
    Reject<UnauthorizedAccessException>(() => Scope(kind,
        serverVariant: "../private"), kind + " traversal-like cache projection forbidden");
    Reject<UnauthorizedAccessException>(() => Scope(kind,
        serverVariant: "Detail.v1"), kind + " noncanonical variant case forbidden");
    Reject<UnauthorizedAccessException>(() => Scope(kind,
        serverVariant: new string('x', 49)), kind + " oversized variant forbidden");
    Reject<UnauthorizedAccessException>(() => Scope(kind,
        serverVariant: "detail/v1"), kind + " raw path cannot be used as variant");
    Reject<UnauthorizedAccessException>(() => Scope(kind,
        operation: Guid.Empty), kind + " empty operation");
    Reject<UnauthorizedAccessException>(() => Scope(kind,
        environment: (TenantArtifactEnvironment)900), kind + " unknown environment");
    Reject<UnauthorizedAccessException>(() => Scope(kind,
        placement: TenantPlacementMode.Dedicated), kind + " Dedicated missing slot");
    Reject<UnauthorizedAccessException>(() => Scope(kind,
        placement: TenantPlacementMode.Shared, slot: slotA), kind + " Shared forbidden slot");
    Check(!Scope(kind, verifiedAt: now.AddMinutes(2)).IsFreshAtUtc(now),
        kind + " future approval never usable now");
    Reject<UnauthorizedAccessException>(() => Scope(kind,
        expiresAt: now.AddMinutes(20)), kind + " oversize approval lifetime");
    Reject<UnauthorizedAccessException>(() => Scope(kind,
        verifiedAt: now.ToOffset(TimeSpan.FromHours(-6))), kind + " non-UTC approval");
    Reject<UnauthorizedAccessException>(() => Scope(kind,
        expiresAt: now.AddMinutes(-3)), kind + " inverted approval interval");
}

Check(TenantPrivateArtifactKey.Derive(Scope(TenantArtifactKind.Cache), keySecret) !=
    TenantPrivateArtifactKey.Derive(Scope(TenantArtifactKind.Job), keySecret),
    "cache cannot alias job type");
Check(TenantPrivateArtifactKey.Derive(Scope(TenantArtifactKind.Media), keySecret) !=
    TenantPrivateArtifactKey.Derive(Scope(TenantArtifactKind.Export), keySecret),
    "media cannot alias export type");

Reject<UnauthorizedAccessException>(() =>
    TenantPrivateArtifactKey.Derive(Scope(TenantArtifactKind.Cache), new byte[31]),
    "weak key must not derive protected entry");
Reject<UnauthorizedAccessException>(() =>
    TenantPrivateArtifactKey.Derive(Scope(TenantArtifactKind.Cache), []),
    "empty key must not derive protected entry");
Check(!TenantPrivateArtifactKey.IsCanonicalForKind(
    "c10:v1:cache:" + new string('g', 64), TenantArtifactKind.Cache),
    "invalid nonhex HMAC rejected");
Check(!TenantPrivateArtifactKey.IsCanonicalForKind(
    "c10:v1:cache:" + new string('A', 64), TenantArtifactKind.Cache),
    "uppercase HMAC rejected");
Check(!TenantPrivateArtifactKey.IsCanonicalForKind(
    "c10:v1:cache:" + new string('0', 63), TenantArtifactKind.Cache),
    "truncated HMAC rejected");
Check(!TenantPrivateArtifactKey.IsCanonicalForKind(
    "c10:v1:job:" + new string('a', 64), TenantArtifactKind.Cache),
    "cross-purpose HMAC rejected");
Check(!TenantPrivateArtifactKey.IsCanonicalForKind(
    null, TenantArtifactKind.Cache), "null HMAC rejected");
Check(!TenantPrivateArtifactKey.IsCanonicalForKind(
    "c10:v1:cache:" + new string('0', 64), (TenantArtifactKind)999),
    "unknown purpose HMAC rejected");

var scopeCache = Scope(TenantArtifactKind.Cache);
var cacheKey = TenantPrivateArtifactKey.Derive(scopeCache, keySecret);
var cleanReceipt = Receipt(scopeCache);
TrustedTenantArtifactReceipt.RequireExactMatch(scopeCache, cacheKey, cleanReceipt, now);
Check(true, "synthetic cache receipt exact scope verified");
Reject<UnauthorizedAccessException>(() => TrustedTenantArtifactReceipt.RequireExactMatch(
    scopeCache, cacheKey, Receipt(scopeCache, operation: operationB), now),
    "replay receipt may not use another operation");
Reject<UnauthorizedAccessException>(() => TrustedTenantArtifactReceipt.RequireExactMatch(
    scopeCache, cacheKey, Receipt(scopeCache,
        fingerprint: Scope(TenantArtifactKind.Cache, tenant: tenantB).IntentFingerprint), now),
    "valid tenant B fingerprint forbidden for A");
Reject<UnauthorizedAccessException>(() => TrustedTenantArtifactReceipt.RequireExactMatch(
    scopeCache, cacheKey, Receipt(scopeCache,
        key: TenantPrivateArtifactKey.Derive(
            Scope(TenantArtifactKind.Cache, tenant: tenantB), keySecret)), now),
    "valid tenant B key forbidden for A");
Reject<UnauthorizedAccessException>(() => TrustedTenantArtifactReceipt.RequireExactMatch(
    scopeCache, cacheKey, Receipt(scopeCache, tenantEpoch: 8), now),
    "stale epoch receipt cannot authorize replay");
Reject<UnauthorizedAccessException>(() => TrustedTenantArtifactReceipt.RequireExactMatch(
    scopeCache, cacheKey, Receipt(scopeCache, confirmedAt: now.AddSeconds(1)), now),
    "future signed operation receipt forbidden");
Reject<UnauthorizedAccessException>(() => TrustedTenantArtifactReceipt.RequireExactMatch(
    scopeCache, cacheKey, Receipt(scopeCache), now.AddMinutes(12)),
    "expired scope cannot validate old receipt");
Reject<UnauthorizedAccessException>(() => TrustedTenantArtifactReceipt.RequireExactMatch(
    Scope(TenantArtifactKind.Cache, operation: operationB), cacheKey, cleanReceipt, now),
    "different operation receipt must not replay under same cache key");

Reject<InvalidOperationException>(() => Receipt(scopeCache, generation: 0),
    "missing job/cache fencing");
Reject<InvalidOperationException>(() => Receipt(scopeCache,
    state: (TenantArtifactReceiptState)999), "undefined receipt state");
Reject<InvalidOperationException>(() => Receipt(scopeCache,
    confirmedAt: now.ToOffset(TimeSpan.FromHours(-6))),
    "non-UTC receipt");
Reject<InvalidOperationException>(() => Receipt(scopeCache,
    key: "example-private-url"), "unscoped direct URL cannot be a receipt");
Reject<InvalidOperationException>(() => Receipt(scopeCache,
    kind: TenantArtifactKind.Job), "cache key cannot impersonate job receipt");
Reject<InvalidOperationException>(() => Receipt(scopeCache,
    fingerprint: new string('A', 64)), "noncanonical fingerprint receipt");

foreach (var kind in Enum.GetValues<TenantArtifactKind>())
{
    var plan = Scope(kind);
    var reader = new FakeDecisionReader(plan);
    var deriver = new FakeKeyDeriver(keySecret);
    var gateway = new FakeArtifactGateway(now);
    var useCase = new ApplyScopedTenantArtifact(
        reader, deriver, gateway, new FixedClock(now));
    var receipt = await useCase.ExecuteAsync(operationA, plan, CancellationToken.None);
    Check(receipt.OperationId == operationA && receipt.Kind == kind,
        kind + " apply valid future gateway receipt");
    Check(reader.Reads == 3 && gateway.Calls == 1,
        kind + " current permission read at prepare, pre-effect and post-effect");

    reader.Reads = 0;
    gateway.ResultState = TenantArtifactReceiptState.Replayed;
    Check((await useCase.ExecuteAsync(operationA, plan, CancellationToken.None)).State ==
        TenantArtifactReceiptState.Replayed, kind + " replay receipt exact");
    gateway.ResultState = TenantArtifactReceiptState.Applied;
    reader.Current = null;
    await RejectAsync<UnauthorizedAccessException>(
        async () => { await useCase.ExecuteAsync(operationA, plan, CancellationToken.None); },
        kind + " missing verified catalog approval");
    reader.Current = plan;
    reader.OverrideRead = count => count % 3 == 2 ? null : plan;
    reader.Reads = 0;
    var previousCalls = gateway.Calls;
    await RejectAsync<UnauthorizedAccessException>(
        async () => { await useCase.ExecuteAsync(operationA, plan, CancellationToken.None); },
        kind + " approval revoked before gateway");
    Check(gateway.Calls == previousCalls, kind + " no effects after early revocation");
    // Explicit third-read revocation: the gateway is a fake, NOT a real write.
    reader.OverrideRead = count => count == 3 ? null : plan;
    reader.Reads = 0;
    await RejectAsync<UnauthorizedAccessException>(
        async () => { await useCase.ExecuteAsync(operationA, plan, CancellationToken.None); },
        kind + " revoked while gateway was handling operation");
    reader.OverrideRead = null;
    reader.Reads = 0;

    reader.Current = Scope(kind, tenant: tenantB);
    await RejectAsync<UnauthorizedAccessException>(
        async () => { await useCase.ExecuteAsync(operationA, plan, CancellationToken.None); },
        kind + " same operation id cannot be reused for valid B");
    reader.Current = plan;
    await RejectAsync<UnauthorizedAccessException>(
        async () => { await useCase.ExecuteAsync(operationB, plan, CancellationToken.None); },
        kind + " operation selector mismatch");

    gateway.ForgeReceipt = (p, key) => Receipt(p,
        fingerprint: Scope(kind, tenant: tenantB).IntentFingerprint);
    await RejectAsync<UnauthorizedAccessException>(
        async () => { await useCase.ExecuteAsync(operationA, plan, CancellationToken.None); },
        kind + " malicious gateway returns cross-tenant fingerprint");
    gateway.ForgeReceipt = (p, key) => Receipt(p,
        key: TenantPrivateArtifactKey.Derive(Scope(kind, tenant: tenantB), keySecret));
    await RejectAsync<UnauthorizedAccessException>(
        async () => { await useCase.ExecuteAsync(operationA, plan, CancellationToken.None); },
        kind + " malicious gateway returns cross-tenant key");
    gateway.ForgeReceipt = null;

    deriver.ReturnInvalid = true;
    previousCalls = gateway.Calls;
    await RejectAsync<UnauthorizedAccessException>(
        async () => { await useCase.ExecuteAsync(operationA, plan, CancellationToken.None); },
        kind + " malformed HMAC denied before gateway");
    Check(gateway.Calls == previousCalls, kind + " no effect when key is malformed");
    deriver.ReturnInvalid = false;

    using var cancelled = new CancellationTokenSource();
    cancelled.Cancel();
    await RejectAsync<OperationCanceledException>(
        async () => { await useCase.ExecuteAsync(operationA, plan, cancelled.Token); },
        kind + " cancellation fails closed");
}

Console.WriteLine($"C04-10 .NET10 private artifact tests: {positive} positive; {negative} negative; " +
    "4 types cache/job/media/export; no adapter, worker, URL, Redis, R2 or production connection.");

sealed class FixedClock(DateTimeOffset now) : ITrustedHostUtcClock
{
    public DateTimeOffset GetUtcNow() => now;
}

sealed class FakeDecisionReader(TrustedTenantArtifactScope initial)
    : ITrustedTenantArtifactDecisionReader
{
    public TrustedTenantArtifactScope? Current { get; set; } = initial;
    public Func<int, TrustedTenantArtifactScope?>? OverrideRead { get; set; }
    public int Reads { get; set; }

    public Task<TrustedTenantArtifactScope?> ReadCurrentApprovedScopeAsync(
        Guid operationId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Reads++;
        return Task.FromResult(OverrideRead is null ? Current : OverrideRead(Reads));
    }
}

sealed class FakeKeyDeriver(byte[] protectedSyntheticKey)
    : IProtectedTenantArtifactKeyDeriver
{
    public bool ReturnInvalid { get; set; }

    public Task<string> DeriveOpaqueKeyAsync(
        TrustedTenantArtifactScope scope, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ReturnInvalid ? "NOT_A_SCOPED_KEY" :
            TenantPrivateArtifactKey.Derive(scope, protectedSyntheticKey));
    }
}

sealed class FakeArtifactGateway(DateTimeOffset now)
    : IProtectedTenantArtifactGateway
{
    public int Calls { get; private set; }
    public TenantArtifactReceiptState ResultState { get; set; } =
        TenantArtifactReceiptState.Applied;
    public Func<TrustedTenantArtifactScope, string, TrustedTenantArtifactReceipt>?
        ForgeReceipt { get; set; }

    public Task<TrustedTenantArtifactReceipt> ApplyOrReplayAsync(
        TrustedTenantArtifactScope scope, string opaqueKey,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Calls++;
        return Task.FromResult(ForgeReceipt is not null
            ? ForgeReceipt(scope, opaqueKey)
            : TrustedTenantArtifactReceipt.FromVerifiedProtectedGateway(
                scope.OperationId, scope.Kind, scope.IntentFingerprint,
                opaqueKey, scope.TenantAccessEpoch, 1, ResultState, now));
    }
}
