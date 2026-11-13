using Crecisol.Domain.Tenancy;

namespace Crecisol.Application.Tenancy;

/// <summary>
/// Future protected backend decision reader. This MUST derive tenant/actor/
/// branch/resource from verified session, membership and catalog ownership,
/// never from a tenant_id, Host, URL, job JSON, claims alone or object key.
/// Rechecking must occur at enqueue/dequeue and each effect/commit.
/// </summary>
public interface ITrustedTenantArtifactDecisionReader
{
    Task<TrustedTenantArtifactScope?> ReadCurrentApprovedScopeAsync(
        Guid operationId, CancellationToken cancellationToken);
}

/// <summary>
/// Future backend HMAC provider. It owns the separate private key material,
/// rotation rules and environment; no secret passes to browser, queue payload,
/// files, logs or the coordinator. A cache key is NOT an access grant.
/// </summary>
public interface IProtectedTenantArtifactKeyDeriver
{
    Task<string> DeriveOpaqueKeyAsync(
        TrustedTenantArtifactScope scope, CancellationToken cancellationToken);
}

/// <summary>
/// Future transactional tenant-scoped executor. This interface has NO adapter
/// or DI registration in C04-10. At physical enqueue/dequeue/use it MUST again
/// validate exactly the plan+tenant/actor/branch/resource/epoch/placement,
/// reject revoked/expired states, lock/fence jobs and validate owner before
/// cache read, blob signing, media preview or export. No unscoped fallbacks.
/// </summary>
public interface IProtectedTenantArtifactGateway
{
    Task<TrustedTenantArtifactReceipt> ApplyOrReplayAsync(
        TrustedTenantArtifactScope scope, string opaqueKey,
        CancellationToken cancellationToken);
}

/// <summary>
/// Side-effect-free coordination except for calling a FUTURE gateway port.
/// An operation GUID is an UNTRUSTED selector. This use case has no HTTP,
/// storage, Redis, R2, SQL, queue or hosted worker connection. Receipt checks
/// AFTER the port cannot retroactively undo side effects; the future gateway
/// MUST independently recheck the protected catalog at its atomic commit.
/// </summary>
public sealed class ApplyScopedTenantArtifact
{
    private readonly ITrustedTenantArtifactDecisionReader decisions;
    private readonly IProtectedTenantArtifactKeyDeriver keys;
    private readonly IProtectedTenantArtifactGateway gateway;
    private readonly ITrustedHostUtcClock clock;

    public ApplyScopedTenantArtifact(ITrustedTenantArtifactDecisionReader decisions,
        IProtectedTenantArtifactKeyDeriver keys,
        IProtectedTenantArtifactGateway gateway, ITrustedHostUtcClock clock)
    {
        this.decisions = decisions ?? throw new ArgumentNullException(nameof(decisions));
        this.keys = keys ?? throw new ArgumentNullException(nameof(keys));
        this.gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<TrustedTenantArtifactReceipt> ExecuteAsync(
        Guid operationId, TrustedTenantArtifactScope serverAuthorizedScope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(serverAuthorizedScope);
        // This is the expected identity/resource scope from a FUTURE verified
        // caller; an operation ID or a trusted reader's lookup cannot silently
        // replace the tenant/actor/branch/resource already authorized.
        if (operationId == Guid.Empty ||
            serverAuthorizedScope.OperationId != operationId ||
            !serverAuthorizedScope.IsFreshAtUtc(clock.GetUtcNow()))
            throw new UnauthorizedAccessException("Protected artifact scope required.");

        cancellationToken.ThrowIfCancellationRequested();
        var approved = await decisions.ReadCurrentApprovedScopeAsync(
            operationId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (approved is null || approved != serverAuthorizedScope ||
            !approved.IsFreshAtUtc(clock.GetUtcNow()))
            throw new UnauthorizedAccessException("Artifact scope unavailable.");

        var opaqueKey = await keys.DeriveOpaqueKeyAsync(approved, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (!TenantPrivateArtifactKey.IsCanonicalForKind(opaqueKey, approved.Kind))
            throw new UnauthorizedAccessException("Invalid protected artifact key.");

        // No adapter invocation if approval changed while deriving the key.
        var beforeEffect = await decisions.ReadCurrentApprovedScopeAsync(
            operationId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (beforeEffect is null || beforeEffect != approved ||
            !beforeEffect.IsFreshAtUtc(clock.GetUtcNow()))
            throw new UnauthorizedAccessException("Artifact scope changed before operation.");

        // A future gateway MUST validate again UNDER ITS OWN atomic lock.
        var receipt = await gateway.ApplyOrReplayAsync(
            beforeEffect, opaqueKey, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        var afterEffect = await decisions.ReadCurrentApprovedScopeAsync(
            operationId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (afterEffect is null || afterEffect != beforeEffect)
            throw new UnauthorizedAccessException("Artifact scope changed during operation.");

        TrustedTenantArtifactReceipt.RequireExactMatch(
            afterEffect, opaqueKey, receipt, clock.GetUtcNow());
        return receipt;
    }
}
