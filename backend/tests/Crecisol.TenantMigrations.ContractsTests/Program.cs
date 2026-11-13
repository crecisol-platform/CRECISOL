using Crecisol.Application.Tenancy;
using Crecisol.Domain.Tenancy;

var count = 0;
void Check(bool ok, string label)
{
    if (!ok) throw new Exception("FAIL C04-09 " + label);
    count++;
}
void Reject<T>(Action callback, string label) where T : Exception
{
    try { callback(); }
    catch (T) { count++; return; }
    throw new Exception("FAIL C04-09 expected " + typeof(T).Name + ": " + label);
}
async Task RejectAsync<T>(Func<Task> callback, string label) where T : Exception
{
    try { await callback(); }
    catch (T) { count++; return; }
    throw new Exception("FAIL C04-09 expected " + typeof(T).Name + ": " + label);
}
var now = new DateTimeOffset(2026, 11, 12, 23, 30, 0, TimeSpan.Zero);
var tenantA = TenantId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
var tenantB = TenantId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
var slotA = DedicatedStorageSlotId.FromTrustedCatalog(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"));
var slotB = DedicatedStorageSlotId.FromTrustedCatalog(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"));
var operation = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
var hashA = new string('a', 64);
var hashB = new string('b', 64);
TenantStoragePlacementSnapshot Placement(
    TenantId tenant, TenantPlacementMode mode, DedicatedStorageSlotId? slot, long rev = 3) =>
    TenantStoragePlacementSnapshot.RestoreFromTrustedCatalog(
        tenant, mode, slot, rev, now.AddDays(-1));

TrustedTenantMigrationPlan Dedicated(
    TenantId? tenant = null, DedicatedStorageSlotId? slot = null,
    long rev = 3, string version = "schema_2026_09", string? hash = null,
    Guid? op = null, DateTimeOffset? expires = null) =>
    TrustedTenantMigrationPlan.FromTrustedCatalog(
        op ?? operation, Placement(tenant ?? tenantA, TenantPlacementMode.Dedicated, slot ?? slotA, rev),
        version, hash ?? hashA, expires ?? now.AddHours(2));

TrustedSharedSchemaMigrationPlan Shared(
    long revision = 17, string version = "schema_2026_09", string? hash = null,
    Guid? op = null, DateTimeOffset? expires = null) =>
    TrustedSharedSchemaMigrationPlan.FromTrustedPlatformApproval(
        op ?? operation, ProtectedSharedSchemaFamily.TenantResources,
        revision, version, hash ?? hashA, expires ?? now.AddHours(2));

var dedicated = Dedicated();
var shared = Shared();
Check(dedicated.Mode == TenantPlacementMode.Dedicated && dedicated.Slot == slotA,
    "dedicated-plan-binds-tenant-and-slot");
Check(shared.SchemaFamily == ProtectedSharedSchemaFamily.TenantResources,
    "shared-global-schema-family");
Check(!typeof(TrustedSharedSchemaMigrationPlan).GetProperties().Any(
    x => x.PropertyType == typeof(TenantId) ||
         x.PropertyType == typeof(DedicatedStorageSlotId)),
    "shared-global-must-not-select-tenant-or-slot");
Check(shared.ToString() == "TrustedSharedSchemaMigrationPlan[redacted]" &&
      dedicated.ToString() == "TrustedTenantMigrationPlan[redacted]",
    "plans-redact-identifiers");

Reject<InvalidOperationException>(() => TrustedTenantMigrationPlan.FromTrustedCatalog(
    operation, Placement(tenantA, TenantPlacementMode.Shared, null),
    "schema_2026_09", hashA, now.AddHours(2)), "shared-ddl-cannot-use-tenant-plan");
Check(shared.IntentFingerprint == Shared().IntentFingerprint &&
      dedicated.IntentFingerprint == Dedicated().IntentFingerprint,
    "deterministic-global-and-dedicated-intents");
Check(shared.IntentFingerprint != dedicated.IntentFingerprint,
    "global-and-dedicated-domains-separate");
Check(shared.IntentFingerprint != Shared(revision: 18).IntentFingerprint,
    "global-revision-bound");
Check(shared.IntentFingerprint != Shared(hash: hashB).IntentFingerprint,
    "global-checksum-bound");
Check(shared.IntentFingerprint != Shared(version: "schema_2026_10").IntentFingerprint,
    "global-version-bound");
Check(shared.IntentFingerprint != Shared(op: Guid.NewGuid()).IntentFingerprint,
    "global-operation-bound");
Check(dedicated.IntentFingerprint != Dedicated(tenant: tenantB).IntentFingerprint,
    "dedicated-tenant-bound");
Check(dedicated.IntentFingerprint != Dedicated(slot: slotB).IntentFingerprint,
    "dedicated-slot-bound");
Check(dedicated.IntentFingerprint != Dedicated(rev: 4).IntentFingerprint,
    "dedicated-revision-bound");
Check(dedicated.IntentFingerprint != Dedicated(version: "schema_2026_10").IntentFingerprint,
    "dedicated-version-bound");
Check(dedicated.IntentFingerprint != Dedicated(hash: hashB).IntentFingerprint,
    "dedicated-checksum-bound");
Check(dedicated.IntentFingerprint != Dedicated(op: Guid.NewGuid()).IntentFingerprint,
    "dedicated-operation-bound");

Check(shared.IsApprovedAtUtc(now) && dedicated.IsApprovedAtUtc(now),
    "current-utc-approvals");
Check(!shared.IsApprovedAtUtc(now.AddHours(2)) &&
      !dedicated.IsApprovedAtUtc(now.AddHours(2)),
    "exact-expiry-denies");
Check(!shared.IsApprovedAtUtc(now.ToOffset(TimeSpan.FromHours(-6))) &&
      !dedicated.IsApprovedAtUtc(now.ToOffset(TimeSpan.FromHours(-6))),
    "non-utc-now-denies");
Reject<InvalidOperationException>(() => Shared(op: Guid.Empty), "empty-global-operation");
Reject<InvalidOperationException>(() => Shared(revision: 0), "invalid-global-revision");
Reject<InvalidOperationException>(() => Shared(hash: "abc"), "invalid-global-checksum");
Reject<InvalidOperationException>(() => Shared(version: "../schema"), "unsafe-global-version");
Reject<InvalidOperationException>(() => Shared(version: "ABC"), "invalid-global-version");
Reject<InvalidOperationException>(() => TrustedSharedSchemaMigrationPlan.FromTrustedPlatformApproval(
    operation, (ProtectedSharedSchemaFamily)999, 3, "schema_1", hashA, now.AddHours(2)),
    "unknown-shared-schema-family");
Reject<InvalidOperationException>(() => Dedicated(op: Guid.Empty), "empty-dedicated-operation");
Reject<InvalidOperationException>(() => Dedicated(rev: 0), "invalid-dedicated-revision");
Reject<InvalidOperationException>(() => Dedicated(hash: "bad"), "invalid-dedicated-checksum");
Reject<InvalidOperationException>(() => Dedicated(version: "../tenantb"), "unsafe-dedicated-version");
Reject<InvalidOperationException>(() => Dedicated(version: "UPPER"), "invalid-dedicated-version");

TrustedTenantMigrationReceipt DedicatedReceipt(
    TrustedTenantMigrationPlan plan, TenantId? tenant = null,
    DedicatedStorageSlotId? slot = null, long? revision = null,
    string? version = null, string? checksum = null,
    string? fingerprint = null, Guid? op = null, DateTimeOffset? verified = null) =>
    TrustedTenantMigrationReceipt.FromTrustedVerifiedLedger(
        tenant ?? plan.TenantId, op ?? plan.OperationId, plan.Mode, slot ?? plan.Slot,
        revision ?? plan.PlacementRevision,
        version ?? plan.MigrationVersion, checksum ?? plan.Checksum,
        fingerprint ?? plan.IntentFingerprint, TenantMigrationReceiptStatus.Applied,
        verified ?? now);

TrustedSharedSchemaMigrationReceipt SharedReceipt(
    TrustedSharedSchemaMigrationPlan plan, long? committedRev = null,
    string? version = null, string? checksum = null,
    string? fingerprint = null, Guid? op = null, DateTimeOffset? verified = null) =>
    TrustedSharedSchemaMigrationReceipt.FromTrustedVerifiedLedger(
        op ?? plan.OperationId, plan.SchemaFamily,
        committedRev ?? checked(plan.ExpectedSchemaRevision + 1),
        version ?? plan.MigrationVersion, checksum ?? plan.Checksum,
        fingerprint ?? plan.IntentFingerprint, TenantMigrationReceiptStatus.Applied,
        verified ?? now);

var dreceipt = DedicatedReceipt(dedicated);
var sreceipt = SharedReceipt(shared);
TrustedTenantMigrationReceipt.RequireExactMatch(dedicated, dreceipt, now);
TrustedSharedSchemaMigrationReceipt.RequireExactMatch(shared, sreceipt, now);
Check(true, "both-verified-receipts-match");
Check(dreceipt.ToString() == "TrustedTenantMigrationReceipt[redacted]" &&
      sreceipt.ToString() == "TrustedSharedSchemaMigrationReceipt[redacted]",
      "receipts-redact-identifiers");
Reject<UnauthorizedAccessException>(() => TrustedTenantMigrationReceipt.RequireExactMatch(
    dedicated, DedicatedReceipt(dedicated, tenant: tenantB), now),
    "tenant-B-receipt-never-valid-for-A");
Reject<UnauthorizedAccessException>(() => TrustedTenantMigrationReceipt.RequireExactMatch(
    dedicated, DedicatedReceipt(dedicated, slot: slotB), now),
    "wrong-dedicated-slot");
Reject<UnauthorizedAccessException>(() => TrustedTenantMigrationReceipt.RequireExactMatch(
    dedicated, DedicatedReceipt(dedicated, revision: 4), now),
    "stale-placement-revision");
Reject<UnauthorizedAccessException>(() => TrustedTenantMigrationReceipt.RequireExactMatch(
    dedicated, DedicatedReceipt(dedicated, version: "schema_2026_10"), now),
    "dedicated-version-mismatch");
Reject<UnauthorizedAccessException>(() => TrustedTenantMigrationReceipt.RequireExactMatch(
    dedicated, DedicatedReceipt(dedicated, checksum: hashB), now),
    "dedicated-checksum-mismatch");
Reject<UnauthorizedAccessException>(() => TrustedTenantMigrationReceipt.RequireExactMatch(
    dedicated, DedicatedReceipt(dedicated, fingerprint: hashB), now),
    "dedicated-fingerprint-mismatch");
Reject<UnauthorizedAccessException>(() => TrustedTenantMigrationReceipt.RequireExactMatch(
    dedicated, DedicatedReceipt(dedicated, op: Guid.NewGuid()), now),
    "dedicated-operation-mismatch");
Reject<UnauthorizedAccessException>(() => TrustedSharedSchemaMigrationReceipt.RequireExactMatch(
    shared, SharedReceipt(shared, committedRev: 19), now),
    "global-CAS-revision-mismatch");
Reject<UnauthorizedAccessException>(() => TrustedSharedSchemaMigrationReceipt.RequireExactMatch(
    shared, SharedReceipt(shared, version: "schema_2026_10"), now),
    "global-version-mismatch");
Reject<UnauthorizedAccessException>(() => TrustedSharedSchemaMigrationReceipt.RequireExactMatch(
    shared, SharedReceipt(shared, checksum: hashB), now),
    "global-checksum-mismatch");
Reject<UnauthorizedAccessException>(() => TrustedSharedSchemaMigrationReceipt.RequireExactMatch(
    shared, SharedReceipt(shared, fingerprint: hashB), now),
    "global-fingerprint-mismatch");
Reject<UnauthorizedAccessException>(() => TrustedSharedSchemaMigrationReceipt.RequireExactMatch(
    shared, SharedReceipt(shared, op: Guid.NewGuid()), now),
    "global-operation-mismatch");
Reject<UnauthorizedAccessException>(() => TrustedSharedSchemaMigrationReceipt.RequireExactMatch(
    shared, sreceipt, now.AddHours(3)), "global-expired-receipt");
Reject<UnauthorizedAccessException>(() => TrustedTenantMigrationReceipt.RequireExactMatch(
    dedicated, dreceipt, now.AddHours(3)), "dedicated-expired-receipt");
Reject<InvalidOperationException>(() => TrustedTenantMigrationReceipt.FromTrustedVerifiedLedger(
    tenantA, operation, TenantPlacementMode.Shared, null, 3, "schema_2026_09",
    hashA, dedicated.IntentFingerprint, TenantMigrationReceiptStatus.Applied, now),
    "cannot-fabricate-shared-via-dedicated-receipt");
Reject<InvalidOperationException>(() => TrustedSharedSchemaMigrationReceipt.FromTrustedVerifiedLedger(
    operation, ProtectedSharedSchemaFamily.TenantResources, 18,
    "schema_2026_09", hashA.ToUpperInvariant(), shared.IntentFingerprint,
    TenantMigrationReceiptStatus.Applied, now), "reject-noncanonical-global-receipt-hash");

var dReader = new FakeDedicatedApprovals(dedicated);
var dExec = new FakeDedicatedExecutor(dreceipt);
var dUseCase = new PlanTenantMigration(dReader, dExec, new FixedClock(now));
Check(await dUseCase.ExecuteAsync(operation, CancellationToken.None) == dreceipt,
    "dedicated-application-success");
Check(dReader.Reads == 2 && dExec.Calls == 1,
    "dedicated-double-approval-and-one-execution");
dReader.Current = null;
await RejectAsync<UnauthorizedAccessException>(
    async () => { await dUseCase.ExecuteAsync(operation, CancellationToken.None); },
    "dedicated-missing-approval");
dReader.Current = dedicated;
dExec.Current = DedicatedReceipt(dedicated, tenant: tenantB);
await RejectAsync<UnauthorizedAccessException>(
    async () => { await dUseCase.ExecuteAsync(operation, CancellationToken.None); },
    "dedicated-cross-tenant-executor-rejected");
dExec.Current = dreceipt;
dReader.DropOnSecondRead = true; dReader.Reads = 0;
await RejectAsync<UnauthorizedAccessException>(
    async () => { await dUseCase.ExecuteAsync(operation, CancellationToken.None); },
    "dedicated-approval-revoked-during-execution");
dReader.DropOnSecondRead = false; dReader.Reads = 0;
await RejectAsync<UnauthorizedAccessException>(
    async () => { await dUseCase.ExecuteAsync(Guid.Empty, CancellationToken.None); },
    "dedicated-empty-selector");
await RejectAsync<UnauthorizedAccessException>(
    async () => { await dUseCase.ExecuteAsync(Guid.NewGuid(), CancellationToken.None); },
    "dedicated-operation-scope-mismatch");

var sReader = new FakeSharedApprovals(shared);
var sExec = new FakeSharedExecutor(sreceipt);
var sUseCase = new PlanSharedSchemaMigration(sReader, sExec, new FixedClock(now));
Check(await sUseCase.ExecuteAsync(operation, CancellationToken.None) == sreceipt,
    "global-shared-application-success");
Check(sReader.Reads == 2 && sExec.Calls == 1,
    "global-double-platform-approval");
sReader.Current = null;
await RejectAsync<UnauthorizedAccessException>(
    async () => { await sUseCase.ExecuteAsync(operation, CancellationToken.None); },
    "global-missing-approval");
sReader.Current = shared;
sExec.Current = SharedReceipt(shared, committedRev: 20);
await RejectAsync<UnauthorizedAccessException>(
    async () => { await sUseCase.ExecuteAsync(operation, CancellationToken.None); },
    "global-invalid-revision-receipt");
sExec.Current = sreceipt;
sReader.DropOnSecondRead = true; sReader.Reads = 0;
await RejectAsync<UnauthorizedAccessException>(
    async () => { await sUseCase.ExecuteAsync(operation, CancellationToken.None); },
    "global-approval-revoked-during-execution");
sReader.DropOnSecondRead = false;
await RejectAsync<UnauthorizedAccessException>(
    async () => { await sUseCase.ExecuteAsync(Guid.Empty, CancellationToken.None); },
    "global-empty-operation");
await RejectAsync<UnauthorizedAccessException>(
    async () => { await sUseCase.ExecuteAsync(Guid.NewGuid(), CancellationToken.None); },
    "global-operation-scope-mismatch");
using var cancelled = new CancellationTokenSource();
cancelled.Cancel();
await RejectAsync<OperationCanceledException>(
    async () => { await sUseCase.ExecuteAsync(operation, cancelled.Token); },
    "global-cancelled-before-authority-read");

Console.WriteLine($"C04-09 Domain/Application scope positive/negative assertions SUCCESS: {count}");

sealed class FixedClock(DateTimeOffset when) : ITrustedHostUtcClock
{
    public DateTimeOffset GetUtcNow() => when;
}
sealed class FakeDedicatedApprovals(TrustedTenantMigrationPlan plan) :
    ITrustedTenantMigrationApprovalReader
{
    public TrustedTenantMigrationPlan? Current { get; set; } = plan;
    public bool DropOnSecondRead { get; set; }
    public int Reads { get; set; }
    public Task<TrustedTenantMigrationPlan?> ReadCurrentApprovedPlanAsync(
        Guid id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Reads++;
        return Task.FromResult(DropOnSecondRead && Reads % 2 == 0 ? null : Current);
    }
}
sealed class FakeDedicatedExecutor(TrustedTenantMigrationReceipt receipt) :
    IProtectedTenantMigrationExecutor
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
sealed class FakeSharedApprovals(TrustedSharedSchemaMigrationPlan plan) :
    ITrustedPlatformSchemaApprovalReader
{
    public TrustedSharedSchemaMigrationPlan? Current { get; set; } = plan;
    public bool DropOnSecondRead { get; set; }
    public int Reads { get; set; }
    public Task<TrustedSharedSchemaMigrationPlan?> ReadCurrentApprovedSharedSchemaPlanAsync(
        Guid id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Reads++;
        return Task.FromResult(DropOnSecondRead && Reads % 2 == 0 ? null : Current);
    }
}
sealed class FakeSharedExecutor(TrustedSharedSchemaMigrationReceipt receipt) :
    IProtectedSharedSchemaMigrationExecutor
{
    public TrustedSharedSchemaMigrationReceipt Current { get; set; } = receipt;
    public int Calls { get; private set; }
    public Task<TrustedSharedSchemaMigrationReceipt> ExecuteOrReplayAsync(
        TrustedSharedSchemaMigrationPlan plan, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Calls++;
        return Task.FromResult(Current);
    }
}
