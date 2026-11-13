using Crecisol.Application.Tenancy;
using Crecisol.Domain.Tenancy;

var passed = 0;
void Check(bool ok, string label)
{
    if (!ok) throw new Exception("FAIL C04-09 " + label);
    passed++;
}
void Reject<T>(Action fn, string label) where T : Exception
{
    try { fn(); }
    catch (T) { passed++; return; }
    throw new Exception("FAIL C04-09 expected " + typeof(T).Name + ": " + label);
}
async Task RejectAsync<T>(Func<Task> fn, string label) where T : Exception
{
    try { await fn(); }
    catch (T) { passed++; return; }
    throw new Exception("FAIL C04-09 expected " + typeof(T).Name + ": " + label);
}

var now = new DateTimeOffset(2026, 11, 12, 18, 30, 0, TimeSpan.Zero);
var tenantA = TenantId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
var tenantB = TenantId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
var slotA = DedicatedStorageSlotId.FromTrustedCatalog(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"));
var slotB = DedicatedStorageSlotId.FromTrustedCatalog(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"));
var op = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
var hashA = new string('a', 64);
var hashB = new string('b', 64);
TenantStoragePlacementSnapshot Placement(TenantId tenant, TenantPlacementMode mode,
    DedicatedStorageSlotId? slot, long revision = 3) =>
    TenantStoragePlacementSnapshot.RestoreFromTrustedCatalog(
        tenant, mode, slot, revision, now.AddHours(-1));
TrustedTenantMigrationPlan Plan(TenantId? tenant = null,
    TenantPlacementMode mode = TenantPlacementMode.Shared,
    DedicatedStorageSlotId? slot = null, long revision = 3,
    string version = "schema_2026_09", string? checksum = null,
    Guid? operation = null) =>
    TrustedTenantMigrationPlan.FromTrustedCatalog(operation ?? op,
        Placement(tenant ?? tenantA, mode, slot, revision),
        version, checksum ?? hashA, now.AddHours(2));

var shared = Plan();
var dedicated = Plan(mode: TenantPlacementMode.Dedicated, slot: slotA);
Check(shared.Mode == TenantPlacementMode.Shared && shared.Slot is null, "shared-placement");
Check(dedicated.Mode == TenantPlacementMode.Dedicated && dedicated.Slot == slotA, "dedicated-placement");
Check(shared.IntentFingerprint == Plan().IntentFingerprint, "deterministic-fingerprint");
Check(shared.IntentFingerprint != dedicated.IntentFingerprint, "mode-slot-fingerprint");
Check(shared.IntentFingerprint != Plan(tenant: tenantB).IntentFingerprint, "tenant-fingerprint");
Check(dedicated.IntentFingerprint != Plan(mode: TenantPlacementMode.Dedicated, slot: slotB).IntentFingerprint,
    "dedicated-slot-fingerprint");
Check(shared.IntentFingerprint != Plan(revision: 4).IntentFingerprint, "placement-revision-fingerprint");
Check(shared.IntentFingerprint != Plan(version: "schema_2026_10").IntentFingerprint, "version-fingerprint");
Check(shared.IntentFingerprint != Plan(checksum: hashB).IntentFingerprint, "checksum-fingerprint");
Check(shared.IntentFingerprint != Plan(operation: Guid.NewGuid()).IntentFingerprint, "operation-fingerprint");
Check(shared.IsApprovedAtUtc(now) && !shared.IsApprovedAtUtc(now.AddDays(1)), "utc-approval-expiry");
Check(shared.ToString() == "TrustedTenantMigrationPlan[redacted]", "redacted-plan");
Reject<InvalidOperationException>(() => TrustedTenantMigrationPlan.FromTrustedCatalog(
    Guid.Empty, Placement(tenantA, TenantPlacementMode.Shared, null),
    "schema_2026_09", hashA, now.AddHours(1)), "empty-operation");
Reject<InvalidOperationException>(() => Plan(version: "../tenant-b"), "unsafe-version");
Reject<InvalidOperationException>(() => Plan(checksum: "not-sha256"), "invalid-checksum");
Reject<InvalidOperationException>(() => Plan(version: "SCHEMA_2026"), "uppercase-version");

TrustedTenantMigrationReceipt Receipt(TrustedTenantMigrationPlan plan,
    TenantId? tenant = null, DedicatedStorageSlotId? slot = null,
    TenantPlacementMode? mode = null, long? revision = null,
    string? version = null, string? hash = null,
    string? fingerprint = null, Guid? operation = null) =>
    TrustedTenantMigrationReceipt.FromTrustedVerifiedLedger(
        tenant ?? plan.TenantId, operation ?? plan.OperationId, mode ?? plan.Mode,
        slot ?? plan.Slot, revision ?? plan.PlacementRevision,
        version ?? plan.MigrationVersion, hash ?? plan.Checksum,
        fingerprint ?? plan.IntentFingerprint, TenantMigrationReceiptStatus.Applied, now);

var good = Receipt(shared);
TrustedTenantMigrationReceipt.RequireExactMatch(shared, good, now);
Check(true, "matching-receipt-accepted");
Check(good.ToString() == "TrustedTenantMigrationReceipt[redacted]", "redacted-receipt");
Reject<UnauthorizedAccessException>(() => TrustedTenantMigrationReceipt.RequireExactMatch(
    shared, Receipt(shared, tenant: tenantB), now), "cross-tenant-receipt");
Reject<UnauthorizedAccessException>(() => TrustedTenantMigrationReceipt.RequireExactMatch(
    shared, Receipt(shared, mode: TenantPlacementMode.Dedicated, slot: slotA), now), "cross-mode-receipt");
Reject<UnauthorizedAccessException>(() => TrustedTenantMigrationReceipt.RequireExactMatch(
    dedicated, Receipt(dedicated, slot: slotB), now), "wrong-dedicated-slot");
Reject<UnauthorizedAccessException>(() => TrustedTenantMigrationReceipt.RequireExactMatch(
    shared, Receipt(shared, revision: 4), now), "stale-placement-revision");
Reject<UnauthorizedAccessException>(() => TrustedTenantMigrationReceipt.RequireExactMatch(
    shared, Receipt(shared, hash: hashB), now), "checksum-mismatch");
Reject<UnauthorizedAccessException>(() => TrustedTenantMigrationReceipt.RequireExactMatch(
    shared, Receipt(shared, version: "schema_2026_10"), now), "version-mismatch");
Reject<UnauthorizedAccessException>(() => TrustedTenantMigrationReceipt.RequireExactMatch(
    shared, Receipt(shared, operation: Guid.NewGuid()), now), "operation-mismatch");
Reject<UnauthorizedAccessException>(() => TrustedTenantMigrationReceipt.RequireExactMatch(
    shared, Receipt(shared, fingerprint: hashB), now), "intent-mismatch");
Reject<UnauthorizedAccessException>(() => TrustedTenantMigrationReceipt.RequireExactMatch(
    shared, good, now.AddDays(1)), "expired-approval-receipt");

var approvals = new FakeApprovals(shared);
var executor = new FakeExecutor(good);
var service = new PlanTenantMigration(approvals, executor, new FakeUtcClock(now));
Check(await service.ExecuteAsync(op, CancellationToken.None) == good, "application-success");
Check(approvals.Reads == 2 && executor.Calls == 1, "double-approval-check");
approvals.Current = null;
await RejectAsync<UnauthorizedAccessException>(
    async () => { await service.ExecuteAsync(op, CancellationToken.None); }, "no-approval-denied");
approvals.Current = shared;
executor.Current = Receipt(shared, tenant: tenantB);
await RejectAsync<UnauthorizedAccessException>(
    async () => { await service.ExecuteAsync(op, CancellationToken.None); }, "application-cross-tenant-denied");
executor.Current = good;
approvals.DropOnSecondRead = true;
approvals.Reads = 0;
await RejectAsync<UnauthorizedAccessException>(
    async () => { await service.ExecuteAsync(op, CancellationToken.None); }, "approval-revoked-midflight");
approvals.DropOnSecondRead = false;
approvals.Reads = 0;
await RejectAsync<UnauthorizedAccessException>(
    async () => { await service.ExecuteAsync(Guid.Empty, CancellationToken.None); }, "empty-operation-denied");
await RejectAsync<UnauthorizedAccessException>(
    async () => { await service.ExecuteAsync(Guid.NewGuid(), CancellationToken.None); }, "mismatched-operation");
Console.WriteLine($"C04-09 .NET10 migration scope assertions: {passed} SUCCESS (synthetic only)");

sealed class FakeUtcClock(DateTimeOffset at) : ITrustedHostUtcClock
{
    public DateTimeOffset GetUtcNow() => at;
}
sealed class FakeApprovals(TrustedTenantMigrationPlan plan) : ITrustedTenantMigrationApprovalReader
{
    public TrustedTenantMigrationPlan? Current { get; set; } = plan;
    public bool DropOnSecondRead { get; set; }
    public int Reads { get; set; }
    public Task<TrustedTenantMigrationPlan?> ReadCurrentApprovedPlanAsync(Guid operationId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Reads++;
        return Task.FromResult(DropOnSecondRead && Reads % 2 == 0 ? null : Current);
    }
}
sealed class FakeExecutor(TrustedTenantMigrationReceipt receipt) : IProtectedTenantMigrationExecutor
{
    public TrustedTenantMigrationReceipt Current { get; set; } = receipt;
    public int Calls { get; private set; }
    public Task<TrustedTenantMigrationReceipt> ExecuteOrReplayAsync(
        TrustedTenantMigrationPlan plan, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Calls++;
        return Task.FromResult(Current);
    }
}
