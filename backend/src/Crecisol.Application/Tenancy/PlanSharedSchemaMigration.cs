using Crecisol.Domain.Tenancy;

namespace Crecisol.Application.Tenancy;

/// <summary>
/// Platform authority for GLOBAL shared schema migrations. It cannot accept a
/// TenantId, host, URL, DSN or HTTP claim. An operation ID is a selector only;
/// a future authenticated, MFA-protected, audited backoffice is mandatory.
/// </summary>
public interface ITrustedPlatformSchemaApprovalReader
{
    Task<TrustedSharedSchemaMigrationPlan?> ReadCurrentApprovedSharedSchemaPlanAsync(
        Guid operationId, CancellationToken cancellationToken);
}

/// <summary>
/// Future GLOBAL shared schema migration executor. Must enforce one platform
/// target, schema revision CAS, fencing, maintenance/backups, ledger checksum
/// and compatibility over ALL tenants before any DDL.
/// No actual implementation, connection or DI exists in C04-09.
/// </summary>
public interface IProtectedSharedSchemaMigrationExecutor
{
    Task<TrustedSharedSchemaMigrationReceipt> ExecuteOrReplayAsync(
        TrustedSharedSchemaMigrationPlan approvedPlan, CancellationToken cancellationToken);
}

public sealed class PlanSharedSchemaMigration
{
    private readonly ITrustedPlatformSchemaApprovalReader approvals;
    private readonly IProtectedSharedSchemaMigrationExecutor executor;
    private readonly ITrustedHostUtcClock clock;

    public PlanSharedSchemaMigration(
        ITrustedPlatformSchemaApprovalReader approvals,
        IProtectedSharedSchemaMigrationExecutor executor,
        ITrustedHostUtcClock clock)
    {
        this.approvals = approvals ?? throw new ArgumentNullException(nameof(approvals));
        this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<TrustedSharedSchemaMigrationReceipt> ExecuteAsync(
        Guid operationId, CancellationToken cancellationToken)
    {
        if (operationId == Guid.Empty)
            throw new UnauthorizedAccessException("Platform schema migration denied.");
        cancellationToken.ThrowIfCancellationRequested();
        var plan = await approvals.ReadCurrentApprovedSharedSchemaPlanAsync(
            operationId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (plan is null || plan.OperationId != operationId ||
            !plan.IsApprovedAtUtc(clock.GetUtcNow()))
            throw new UnauthorizedAccessException("Platform schema approval denied.");

        // Physical effects require the FUTURE executor to recheck approval,
        // schema-wide blast radius and protected target within its own gate.
        var receipt = await executor.ExecuteOrReplayAsync(plan, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var current = await approvals.ReadCurrentApprovedSharedSchemaPlanAsync(
            operationId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (current is null || current != plan)
            throw new UnauthorizedAccessException("Platform decision changed in flight.");

        TrustedSharedSchemaMigrationReceipt.RequireExactMatch(
            current, receipt, clock.GetUtcNow());
        return receipt;
    }
}
