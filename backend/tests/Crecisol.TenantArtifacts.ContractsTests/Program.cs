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
    DateTimeOffset? expiresAt = null) =>
    TrustedTenantArtifactScope.FromVerifiedBackendDecision(
        operation ?? operationA, kind, environment,
        tenant ?? tenantA, branch ?? branchA, resource ?? resourceA, actor ?? actorA,
        TenantRequiredAction.FromServerPolicy(requiredAction ?? ActionKey(kind)),
        placement, slot, tenantState, sessionState, membershipState,
        branchState, resourceState,
        tenantEpoch, sessionRevision, membershipRevision, branchRevision,
        resourceRevision, placementRevision, verifiedAt ?? now.AddMinutes(-1),
        expiresAt ?? now.AddMinutes(10));

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
        operation: Guid.Empty), kind + " empty operation");
    Reject<UnauthorizedAccessException>(() => Scope(kind,
        environment: (TenantArtifactEnvironment)900), kind + " unknown environment");
    Reject<UnauthorizedAccessException>(() => Scope(kind,
        placement: TenantPlacementMode.Dedicated), kind + " Dedicated missing slot");
    Reject<UnauthorizedAccessException>(() => Scope(kind,
        placement: TenantPlacementMode.Shared, slot: slotA), kind + " Shared forbidden slot");
    Reject<UnauthorizedAccessException>(() => Scope(kind,
        verifiedAt: now.AddMinutes(2)), kind + " future approval window"); // receipt path below checks
}
