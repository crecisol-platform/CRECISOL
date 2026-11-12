using Crecisol.Domain.Tenancy;

namespace Crecisol.Application.Tenancy;

/// <summary>
/// Future BACKEND admin-workflow catalog: retrieve only an internally approved,
/// versioned plan, NOT a request body, Host, slug, cookie, X-Tenant-Id or DSN.
/// A known operationId is not an approval or a bearer credential.
/// </summary>
public interface ITrustedTenantProvisioningPlanReader
{
    Task<TrustedTenantProvisioningPlanSnapshot?> ReadByOperationIdAsync(
        TenantProvisioningOperationId operationId,
        CancellationToken cancellationToken);
}

/// <summary>
/// CRITICAL future storage contract: implementation MUST perform the operation
/// and Draft tenant insert in ONE durable InnoDB transaction, with uniqueness
/// on operation ID, tenant ID AND globally canonical business key. On a duplicate
/// it MUST read back and compare the FULL immutable intent fingerprint, actor,
/// tenant, key, mode and committed Draft row under lock; conflicting replay
/// MUST fail. It must check current approval under the SAME transaction.
/// When InnoDB chooses a deadlock/lock-timeout victim (1213/1205), future
/// adapters should retry the SAME immutable plan with a bounded backoff after
/// rollback, never retry semantic mismatches or turn 1644 into success.
/// It MUST NOT use insert-ignore, mutate existing rows, or publish Active.
/// No production adapter exists in C04-07.
/// </summary>
public interface IAtomicTenantProvisioningStore
{
    Task<AtomicTenantProvisioningCommit> CommitOrReplayAsync(
        TrustedTenantProvisioningPlanSnapshot approvedPlan,
        CancellationToken cancellationToken);
}

/// <summary>
/// Result FROM the future atomic store. Not proof of authentication.
/// </summary>
public sealed record AtomicTenantProvisioningCommit(
    TrustedTenantProvisioningReceipt Receipt,
    bool NewlyCreated);

/// <summary>
/// C04-07 internal fail-closed correlation of approved operation, deterministic
/// intent, atomic store receipt, and repeated approval validation. Does NOT
/// connect to API, DI, a real DB, a payment or a tenant/branch authorization.
/// </summary>
public sealed class ProvisionApprovedTenantDraft
{
    private readonly ITrustedTenantProvisioningPlanReader plans;
    private readonly IAtomicTenantProvisioningStore store;
    private readonly ITrustedHostUtcClock clock;

    public ProvisionApprovedTenantDraft(
        ITrustedTenantProvisioningPlanReader plans,
        IAtomicTenantProvisioningStore store,
        ITrustedHostUtcClock clock)
    {
        this.plans = plans ?? throw new ArgumentNullException(nameof(plans));
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>
    /// ONLY operationId is an opaque selector. No caller-provided tenant ID,
    /// actor ID, address/Host/slug, mode, plan, price, claims, or DSN. Caller
    /// authenticity is the responsibility of the future trusted plan adapter;
    /// it MUST NOT infer authority from possession of operationId.
    /// </summary>
    public async Task<TenantProvisioningResult> ExecuteAsync(
        TenantProvisioningOperationId operationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operationId);
        cancellationToken.ThrowIfCancellationRequested();

        var plan = await plans.ReadByOperationIdAsync(operationId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (plan is null || plan.OperationId != operationId)
            throw new UnauthorizedAccessException("An approved provisioning plan is required.");

        var startUtc = clock.GetUtcNow();
        if (!plan.IsApprovedAtUtc(startUtc))
            throw new UnauthorizedAccessException("Provisioning plan was not approved/current.");

        // The STORE, not this method, owns row locks, uniqueness and durable
        // atomicity. A lock-free find+insert in the adapter is FORBIDDEN.
        var committed = await store.CommitOrReplayAsync(plan, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (committed is null || committed.Receipt is null)
            throw new UnauthorizedAccessException("No durable provisioning receipt.");

        // Re-read protected approval and compare all record values. This is a
        // defense against mid-request revocation, NOT a distributed transaction.
        var updatedPlan = await plans.ReadByOperationIdAsync(operationId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (updatedPlan != plan)
            throw new UnauthorizedAccessException("Provisioning approval changed.");

        var finishUtc = clock.GetUtcNow();
        cancellationToken.ThrowIfCancellationRequested();
        if (finishUtc < startUtc)
            throw new UnauthorizedAccessException("Backend UTC clock went backwards.");

        return TenantProvisioningResult.FromAtomicStore(
            updatedPlan!, committed.Receipt, committed.NewlyCreated, finishUtc);
    }
}
