using Crecisol.Domain.Tenancy;

namespace Crecisol.Application.Tenancy;

/// <summary>
/// Future verified backoffice reader: authenticates privileged operator,
/// checks MFA/approval and resolves intended tenant from a protected catalog.
/// Knowing operationId is NOT permission. No HTTP body, hostname or DSN input.
/// </summary>
public interface ITrustedTenantMigrationApprovalReader
{
    Task<TrustedTenantMigrationPlan?> ReadCurrentApprovedPlanAsync(
        Guid operationId, CancellationToken cancellationToken);
}

/// <summary>
/// Future executor owns atomic protected ledger reservation, fencing,
/// rechecking approval and scope before opening a database, and explicit
/// recovery from MySQL implicit DDL commits. No implementation is connected.
/// Must reject a mismatched slot/tenant, stale placement and conflicting hash.
/// </summary>
public interface IProtectedTenantMigrationExecutor
{
    Task<TrustedTenantMigrationReceipt> ExecuteOrReplayAsync(
        TrustedTenantMigrationPlan plan, CancellationToken cancellationToken);
}

/// <summary>
/// C04-09 internal coordination only. This cannot run SQL, open connections,
/// authorize a principal or guarantee rollback of DDL. The future executor
/// MUST fail closed on incomplete/failed migration and verify schema metadata.
/// </summary>
public sealed class PlanTenantMigration
{
    private readonly ITrustedTenantMigrationApprovalReader approvals;
    private readonly IProtectedTenantMigrationExecutor executor;
    private readonly ITrustedHostUtcClock clock;

    public PlanTenantMigration(ITrustedTenantMigrationApprovalReader approvals,
        IProtectedTenantMigrationExecutor executor, ITrustedHostUtcClock clock)
    {
        this.approvals = approvals ?? throw new ArgumentNullException(nameof(approvals));
        this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<TrustedTenantMigrationReceipt> ExecuteAsync(
        Guid operationId, CancellationToken cancellationToken)
    {
        if (operationId == Guid.Empty)
            throw new UnauthorizedAccessException("Trusted migration approval required.");
        cancellationToken.ThrowIfCancellationRequested();
        var plan = await approvals.ReadCurrentApprovedPlanAsync(operationId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (plan is null || plan.OperationId != operationId ||
            !plan.IsApprovedAtUtc(clock.GetUtcNow()))
            throw new UnauthorizedAccessException("Migration approval absent or expired.");

        // All trusted authorization/placement/CAS/fencing validations must be
        // repeated in the FUTURE protected executor before physical effects.
        var receipt = await executor.ExecuteOrReplayAsync(plan, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var confirmed = await approvals.ReadCurrentApprovedPlanAsync(operationId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (confirmed is null || confirmed != plan)
            throw new UnauthorizedAccessException("Migration decision changed during operation.");

        TrustedTenantMigrationReceipt.RequireExactMatch(confirmed, receipt, clock.GetUtcNow());
        return receipt;
    }
}
