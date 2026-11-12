using Crecisol.Domain.Tenancy;

namespace Crecisol.Application.Tenancy;

/// <summary>
/// Trusted server-side selector for a future MFA/4-eyes administrative decision.
/// The reader MUST authenticate and authorize the initiating principal independently;
/// knowing an operation ID must not grant access.
/// </summary>
public interface ITrustedTenantLifecycleDecisionReader
{
    Task<TrustedTenantLifecycleIntent?> ReadAuthorizedIntentAsync(
        Guid operationId, CancellationToken cancellationToken);
}

/// <summary>
/// Future atomic storage adapter. Within ONE transaction it MUST:
/// 1. Verify the current authorized decision/actor, exact tenant, state and CAS revision.
/// 2. Compare full operation fingerprint for replay; deny conflicting reuse.
/// 3. Persist state + monotonically increased revision/access epoch.
/// 4. Persist ONE immutable audit event and revocation/outbox watermark.
/// 5. Roll back EVERYTHING on any failure, including audit or epoch publication.
/// A successful adapter commit must never rely on a cached read from this service.
/// No adapter/DB/API is connected in C04-08.
/// </summary>
public interface IAtomicTenantLifecycleTransitionStore
{
    Task<TrustedTenantLifecycleReceipt> CommitOrReplayAsync(
        TrustedTenantLifecycleIntent authorizedIntent, CancellationToken cancellationToken);
}

/// <summary>
/// Non-authorizing internal coordinator. C04-08 is not an HTTP, session or
/// access revocation implementation. All production guarantees depend on
/// future adapter transaction and fresh backend authorization enforcement.
/// </summary>
public sealed class ChangeTenantLifecycle
{
    private readonly ITrustedTenantLifecycleDecisionReader decisions;
    private readonly IAtomicTenantLifecycleTransitionStore store;
    private readonly ITrustedHostUtcClock clock;

    public ChangeTenantLifecycle(
        ITrustedTenantLifecycleDecisionReader decisions,
        IAtomicTenantLifecycleTransitionStore store, ITrustedHostUtcClock clock)
    {
        this.decisions = decisions ?? throw new ArgumentNullException(nameof(decisions));
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<TrustedTenantLifecycleReceipt> ExecuteAsync(
        Guid operationId, CancellationToken cancellationToken)
    {
        if (operationId == Guid.Empty)
            throw new UnauthorizedAccessException("A trusted lifecycle decision is required.");
        cancellationToken.ThrowIfCancellationRequested();

        var intent = await decisions.ReadAuthorizedIntentAsync(operationId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (intent is null || intent.OperationId != operationId)
            throw new UnauthorizedAccessException("Lifecycle decision denied.");

        var startedAtUtc = clock.GetUtcNow();
        RequireUtc(startedAtUtc);
        if (intent.RequestedAtUtc > startedAtUtc)
            throw new UnauthorizedAccessException("Lifecycle decision is from the future.");

        // The store is the sole atomic authorization/transition/CAS/audit gate.
        var receipt = await store.CommitOrReplayAsync(intent, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (receipt is null)
            throw new UnauthorizedAccessException("Lifecycle transaction did not commit.");

        var currentIntent = await decisions.ReadAuthorizedIntentAsync(operationId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var finishedAtUtc = clock.GetUtcNow();
        RequireUtc(finishedAtUtc);
        if (finishedAtUtc < startedAtUtc || currentIntent != intent ||
            receipt.TenantId != intent.TenantId ||
            receipt.OperationId != intent.OperationId ||
            !string.Equals(receipt.IntentFingerprint, intent.IntentFingerprint, StringComparison.Ordinal) ||
            receipt.Revision != checked(intent.ExpectedRevision + 1) ||
            receipt.AccessEpoch != checked(intent.ExpectedAccessEpoch + 1) ||
            receipt.CommittedAtUtc < intent.RequestedAtUtc ||
            receipt.CommittedAtUtc > finishedAtUtc)
            throw new UnauthorizedAccessException("Lifecycle receipt/approval mismatch.");

        // The state of the previous committed row must be validated and
        // transitioned by the transactional store; this only checks the target.
        if (intent.Action == TenantLifecycleAction.Suspend &&
            receipt.State != TenantLifecycleState.Suspended ||
            intent.Action == TenantLifecycleAction.Reactivate &&
            receipt.State != TenantLifecycleState.Active ||
            intent.Action == TenantLifecycleAction.Deactivate &&
            receipt.State != TenantLifecycleState.Deactivated)
            throw new UnauthorizedAccessException("Unexpected lifecycle transition.");

        return receipt;
    }

    private static void RequireUtc(DateTimeOffset instant)
    {
        if (instant == default || instant.Offset != TimeSpan.Zero)
            throw new UnauthorizedAccessException("Backend UTC clock required.");
    }
}
