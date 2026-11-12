using Crecisol.Domain.Tenancy;
using Crecisol.Application.Tenancy;

var passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("C04-08 FAILED: " + name);
    passed++;
}
void Deny(Action action, string name)
{
    try
    {
        action();
        throw new Exception("C04-08 MUST DENY: " + name);
    }
    catch (UnauthorizedAccessException) { passed++; }
}
void Invalid(Action action, string name)
{
    try
    {
        action();
        throw new Exception("C04-08 MUST REJECT: " + name);
    }
    catch (InvalidOperationException) { passed++; }
}

var tenantA = TenantId.From(Guid.NewGuid());
var tenantB = TenantId.From(Guid.NewGuid());
var actor = TenantActorId.From(Guid.NewGuid());
var op = Guid.NewGuid();
var now = new DateTimeOffset(2026, 11, 12, 20, 0, 0, TimeSpan.Zero);

Check(TenantLifecycleTransitionPolicy.NextState(
    TenantLifecycleState.Active, TenantLifecycleAction.Suspend) ==
    TenantLifecycleState.Suspended, "active-to-suspended");
Check(TenantLifecycleTransitionPolicy.NextState(
    TenantLifecycleState.Suspended, TenantLifecycleAction.Reactivate) ==
    TenantLifecycleState.Active, "suspended-to-active");
Check(TenantLifecycleTransitionPolicy.NextState(
    TenantLifecycleState.Active, TenantLifecycleAction.Deactivate) ==
    TenantLifecycleState.Deactivated, "active-to-deactivated");
Check(TenantLifecycleTransitionPolicy.NextState(
    TenantLifecycleState.Suspended, TenantLifecycleAction.Deactivate) ==
    TenantLifecycleState.Deactivated, "suspended-to-deactivated");

foreach (var state in Enum.GetValues<TenantLifecycleState>())
{
    Check(TenantLifecycleTransitionPolicy.IsEligibleForAdditionalAuthorization(state) ==
        (state == TenantLifecycleState.Active), "no-implicit-access-" + state);
    foreach (var action in Enum.GetValues<TenantLifecycleAction>())
    {
        var allowed =
            state == TenantLifecycleState.Active &&
                action is TenantLifecycleAction.Suspend or TenantLifecycleAction.Deactivate ||
            state == TenantLifecycleState.Suspended &&
                action is TenantLifecycleAction.Reactivate or TenantLifecycleAction.Deactivate;
        if (!allowed)
            Deny(() => TenantLifecycleTransitionPolicy.NextState(state, action),
                "forbidden-" + state + "-" + action);
    }
}
Deny(() => TenantLifecycleTransitionPolicy.NextState(
    (TenantLifecycleState)999, TenantLifecycleAction.Suspend), "unknown-state");
Deny(() => TenantLifecycleTransitionPolicy.NextState(
    TenantLifecycleState.Active, (TenantLifecycleAction)999), "unknown-action");

var intent = TrustedTenantLifecycleIntent.FromTrustedBackoffice(
    tenantA, actor, op, TenantLifecycleAction.Suspend, 7, 11, "SECURITY_REVIEW", now);
var same = TrustedTenantLifecycleIntent.FromTrustedBackoffice(
    tenantA, actor, op, TenantLifecycleAction.Suspend, 7, 11, "SECURITY_REVIEW", now);
Check(intent.IntentFingerprint == same.IntentFingerprint, "stable-intent");
Check(intent.IntentFingerprint.Length == 64, "sha256-length");
Check(!intent.ToString().Contains(op.ToString(), StringComparison.Ordinal), "redacted-intent");

void Different(TrustedTenantLifecycleIntent other, string name) =>
    Check(other.IntentFingerprint != intent.IntentFingerprint, name);
Different(TrustedTenantLifecycleIntent.FromTrustedBackoffice(
    tenantB, actor, op, TenantLifecycleAction.Suspend, 7, 11, "SECURITY_REVIEW", now), "tenant-bound");
Different(TrustedTenantLifecycleIntent.FromTrustedBackoffice(
    tenantA, TenantActorId.From(Guid.NewGuid()), op,
    TenantLifecycleAction.Suspend, 7, 11, "SECURITY_REVIEW", now), "actor-bound");
Different(TrustedTenantLifecycleIntent.FromTrustedBackoffice(
    tenantA, actor, op, TenantLifecycleAction.Reactivate, 7, 11, "SECURITY_REVIEW", now), "action-bound");
Different(TrustedTenantLifecycleIntent.FromTrustedBackoffice(
    tenantA, actor, op, TenantLifecycleAction.Suspend, 8, 11, "SECURITY_REVIEW", now), "revision-bound");
Different(TrustedTenantLifecycleIntent.FromTrustedBackoffice(
    tenantA, actor, op, TenantLifecycleAction.Suspend, 7, 12, "SECURITY_REVIEW", now), "epoch-bound");
Different(TrustedTenantLifecycleIntent.FromTrustedBackoffice(
    tenantA, actor, op, TenantLifecycleAction.Suspend, 7, 11, "LEGAL_HOLD", now), "reason-bound");
Different(TrustedTenantLifecycleIntent.FromTrustedBackoffice(
    tenantA, actor, Guid.NewGuid(), TenantLifecycleAction.Suspend, 7, 11, "SECURITY_REVIEW", now), "operation-bound");

Invalid(() => TrustedTenantLifecycleIntent.FromTrustedBackoffice(
    tenantA, actor, Guid.Empty, TenantLifecycleAction.Suspend, 7, 11, "SECURITY_REVIEW", now), "empty-operation");
Invalid(() => TrustedTenantLifecycleIntent.FromTrustedBackoffice(
    tenantA, actor, op, (TenantLifecycleAction)999, 7, 11, "SECURITY_REVIEW", now), "unknown-action-intent");
Invalid(() => TrustedTenantLifecycleIntent.FromTrustedBackoffice(
    tenantA, actor, op, TenantLifecycleAction.Suspend, 0, 11, "SECURITY_REVIEW", now), "zero-revision");
Invalid(() => TrustedTenantLifecycleIntent.FromTrustedBackoffice(
    tenantA, actor, op, TenantLifecycleAction.Suspend, 7, 0, "SECURITY_REVIEW", now), "zero-epoch");
Invalid(() => TrustedTenantLifecycleIntent.FromTrustedBackoffice(
    tenantA, actor, op, TenantLifecycleAction.Suspend, 7, 11, "bad reason", now), "unsafe-reason");
Invalid(() => TrustedTenantLifecycleIntent.FromTrustedBackoffice(
    tenantA, actor, op, TenantLifecycleAction.Suspend, 7, 11, "SECURITY_REVIEW",
    now.ToOffset(TimeSpan.FromHours(-6))), "non-utc-intent");

var receipt = TrustedTenantLifecycleReceipt.FromTrustedAtomicStore(
    tenantA, op, intent.IntentFingerprint, TenantLifecycleState.Suspended, 8, 12, now);
Check(receipt.State == TenantLifecycleState.Suspended, "suspended-receipt");
Check(receipt.AccessEpoch == 12, "access-epoch-bumped");
Check(receipt.Revision == 8, "revision-bumped");
Check(!receipt.ToString().Contains(intent.IntentFingerprint, StringComparison.Ordinal), "redacted-receipt");
Invalid(() => TrustedTenantLifecycleReceipt.FromTrustedAtomicStore(
    tenantA, op, "not-a-fingerprint", TenantLifecycleState.Suspended, 8, 12, now), "invalid-fingerprint");
Invalid(() => TrustedTenantLifecycleReceipt.FromTrustedAtomicStore(
    tenantA, op, intent.IntentFingerprint, TenantLifecycleState.Suspended, 0, 12, now), "invalid-receipt-revision");
Invalid(() => TrustedTenantLifecycleReceipt.FromTrustedAtomicStore(
    tenantA, op, intent.IntentFingerprint, TenantLifecycleState.Suspended, 8, 0, now), "invalid-receipt-epoch");


var reader = new FakeDecisionReader(intent);
var store = new FakeLifecycleStore(receipt);
var clock = new FakeClock(now.AddSeconds(1));
var service = new ChangeTenantLifecycle(reader, store, clock);
var result = await service.ExecuteAsync(op, CancellationToken.None);
Check(result == receipt, "application-valid-receipt");
Check(store.Calls == 1 && reader.Calls == 2, "two-decision-checks-one-store");
Check((await service.ExecuteAsync(op, CancellationToken.None)) == receipt,
    "identical-replay-returns-same-receipt");

async Task DenyAsync(Func<Task> action, string name)
{
    try
    {
        await action();
        throw new Exception("C04-08 MUST DENY: " + name);
    }
    catch (UnauthorizedAccessException) { passed++; }
}

reader.Intent = null;
await DenyAsync(async () => { await service.ExecuteAsync(op, CancellationToken.None); },
    "missing-admin-decision");
reader.Intent = intent;
store.Receipt = TrustedTenantLifecycleReceipt.FromTrustedAtomicStore(
    tenantB, op, intent.IntentFingerprint, TenantLifecycleState.Suspended, 8, 12, now);
await DenyAsync(async () => { await service.ExecuteAsync(op, CancellationToken.None); },
    "cross-tenant-receipt");
store.Receipt = TrustedTenantLifecycleReceipt.FromTrustedAtomicStore(
    tenantA, op, intent.IntentFingerprint, TenantLifecycleState.Suspended, 8, 11, now);
await DenyAsync(async () => { await service.ExecuteAsync(op, CancellationToken.None); },
    "stale-authorization-epoch");
store.Receipt = TrustedTenantLifecycleReceipt.FromTrustedAtomicStore(
    tenantA, op, intent.IntentFingerprint, TenantLifecycleState.Active, 8, 12, now);
await DenyAsync(async () => { await service.ExecuteAsync(op, CancellationToken.None); },
    "wrong-target-state");
store.Receipt = TrustedTenantLifecycleReceipt.FromTrustedAtomicStore(
    tenantA, Guid.NewGuid(), intent.IntentFingerprint, TenantLifecycleState.Suspended, 8, 12, now);
await DenyAsync(async () => { await service.ExecuteAsync(op, CancellationToken.None); },
    "different-operation-receipt");
store.Receipt = TrustedTenantLifecycleReceipt.FromTrustedAtomicStore(
    tenantA, op, new string('a', 64), TenantLifecycleState.Suspended, 8, 12, now);
await DenyAsync(async () => { await service.ExecuteAsync(op, CancellationToken.None); },
    "different-intent-receipt");
store.Receipt = receipt;
reader.ReadCount = 0;
reader.ChangeOnSecondRead = true;
await DenyAsync(async () => { await service.ExecuteAsync(op, CancellationToken.None); },
    "approval-revoked-mid-flight");
reader.ChangeOnSecondRead = false;
reader.ReadCount = 0;
await DenyAsync(async () => { await service.ExecuteAsync(Guid.NewGuid(), CancellationToken.None); },
    "unknown-operation");
await DenyAsync(async () => { await service.ExecuteAsync(Guid.Empty, CancellationToken.None); },
    "empty-operation-app");

Console.WriteLine($"C04-08 Domain/Application positive/negative assertions SUCCESS: {passed}");

sealed class FakeClock(DateTimeOffset now) : ITrustedHostUtcClock
{
    public DateTimeOffset GetUtcNow() => now;
}

sealed class FakeDecisionReader(TrustedTenantLifecycleIntent initial) : ITrustedTenantLifecycleDecisionReader
{
    public TrustedTenantLifecycleIntent? Intent { get; set; } = initial;
    public bool ChangeOnSecondRead { get; set; }
    public int ReadCount { get; set; }
    public int Calls => ReadCount;
    public Task<TrustedTenantLifecycleIntent?> ReadAuthorizedIntentAsync(Guid operation, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        ReadCount++;
        return Task.FromResult(ChangeOnSecondRead && ReadCount % 2 == 0
            ? null : Intent);
    }
}

sealed class FakeLifecycleStore(TrustedTenantLifecycleReceipt initial) : IAtomicTenantLifecycleTransitionStore
{
    public TrustedTenantLifecycleReceipt Receipt { get; set; } = initial;
    public int Calls { get; private set; }
    public Task<TrustedTenantLifecycleReceipt> CommitOrReplayAsync(
        TrustedTenantLifecycleIntent intent, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Calls++;
        return Task.FromResult(Receipt);
    }
}

