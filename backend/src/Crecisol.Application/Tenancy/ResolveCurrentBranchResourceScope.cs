using Crecisol.Domain.Tenancy;

namespace Crecisol.Application.Tenancy;

/// <summary>
/// Future backend-only resource metadata port. A real adapter MUST enforce
/// WHERE tenant_id = @authorizedTenant AND resource_id = @candidateResource.
/// Returns no private content, no DSN, no browser-supplied branch/tenant.
/// </summary>
public interface ITrustedTenantScopedResourceReader
{
    Task<TrustedBranchResourceSnapshot?> FindByTenantAndResourceIdAsync(
        TenantId authorizedTenantId, TenantResourceId resourceId,
        CancellationToken cancellationToken);
}

/// <summary>
/// Future backend grants catalog. Query ALL exact matching grants under
/// actor+tenant+branch+required-action so duplicates FAIL CLOSED. Never grant
/// wildcard/global permissions or select a different tenant/branch.
/// </summary>
public interface ITrustedActorBranchGrantReader
{
    Task<IReadOnlyList<TrustedActorBranchGrantSnapshot>?> FindExactGrantsAsync(
        TenantActorId authenticatedActorId, TenantId authorizedTenantId,
        TenantBranchId resourceBranchId, TenantRequiredAction requiredAction,
        CancellationToken cancellationToken);
}

/// <summary>
/// C04-06 INTERNAL scope evaluation. No actual protected resource is read or
/// written. Input resource ID is an UNTRUSTED SELECTOR, required action MUST
/// come from backend route policy, never client headers/queries/claims.
/// NO HTTP endpoint, production auth adapter, DB, DI or data access is enabled.
/// A returned proof is not a bearer token or an atomic data-access grant.
/// </summary>
public sealed class ResolveCurrentBranchResourceScope
{
    private readonly ResolveCurrentTenantIdentityContext identityResolver;
    private readonly ITrustedTenantScopedResourceReader resources;
    private readonly ITrustedActorBranchGrantReader grants;
    private readonly ITrustedHostUtcClock clock;

    public ResolveCurrentBranchResourceScope(
        ResolveCurrentTenantIdentityContext identityResolver,
        ITrustedTenantScopedResourceReader resources,
        ITrustedActorBranchGrantReader grants,
        ITrustedHostUtcClock clock)
    {
        this.identityResolver = identityResolver ??
            throw new ArgumentNullException(nameof(identityResolver));
        this.resources = resources ?? throw new ArgumentNullException(nameof(resources));
        this.grants = grants ?? throw new ArgumentNullException(nameof(grants));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>
    /// No caller-selected tenant, branch, role, actor, Host or storage. The
    /// resource ID is an opaque candidate only. requiredAction is specified by
    /// the server operation policy, never by a browser "permission" field.
    /// </summary>
    public async Task<TenantBranchResourceScopeEvidence> ExecuteAsync(
        TenantResourceId resourceId,
        TenantRequiredAction requiredAction,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resourceId);
        ArgumentNullException.ThrowIfNull(requiredAction);
        cancellationToken.ThrowIfCancellationRequested();

        // C04-05 MUST authenticate and scope tenant before even looking up a
        // resource. Never use a tenant_id/branch_id supplied by the browser.
        var identity = await identityResolver.ExecuteAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        var startUtc = clock.GetUtcNow();
        ValidateUtc(startUtc);

        // Composite tenant + resource lookup prevents a tenant A actor from
        // loading B's resource even if B's ID is known.
        var resource = await resources.FindByTenantAndResourceIdAsync(
            identity.TenantId, resourceId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (resource is null ||
            resource.TenantId != identity.TenantId ||
            resource.ResourceId != resourceId ||
            !resource.IsActiveAtUtc(startUtc))
            throw new UnauthorizedAccessException("Resource is not available in this tenant.");

        // Branch is derived ONLY from trusted resource metadata, never from
        // caller URL/header. Exact action must be in an explicit branch grant.
        var candidates = await grants.FindExactGrantsAsync(
            identity.ActorId, identity.TenantId, resource.BranchId,
            requiredAction, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (candidates is null || candidates.Count != 1 || candidates[0] is null)
            throw new UnauthorizedAccessException("Unique branch scope grant is required.");

        var grant = candidates[0];
        ValidateExactGrant(identity, resource, requiredAction, grant, startUtc);

        // Recheck both protected catalog snapshots to catch changed owner,
        // branch, resource status, grant mode or revocation during async work.
        var freshResource = await resources.FindByTenantAndResourceIdAsync(
            identity.TenantId, resourceId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (freshResource != resource)
            throw new UnauthorizedAccessException("Resource scope changed during resolution.");

        var freshCandidates = await grants.FindExactGrantsAsync(
            identity.ActorId, identity.TenantId, resource.BranchId,
            requiredAction, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (freshCandidates is null || freshCandidates.Count != 1 ||
            freshCandidates[0] != grant)
            throw new UnauthorizedAccessException("Branch grant changed during resolution.");

        // Refresh C04-05 session/host/membership evidence to detect tenant or
        // actor revocation/rebinding. Snapshot comparison is NOT a DB transaction.
        var refreshedIdentity = await identityResolver.ExecuteAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (refreshedIdentity != identity)
            throw new UnauthorizedAccessException("Actor tenant context changed.");

        var finishUtc = clock.GetUtcNow();
        ValidateUtc(finishUtc);
        cancellationToken.ThrowIfCancellationRequested();
        if (finishUtc < startUtc ||
            !resource.IsActiveAtUtc(finishUtc) ||
            !grant.IsActiveAtUtc(finishUtc))
            throw new UnauthorizedAccessException("Resource or branch scope is no longer valid.");

        // Evidence only. Future HTTP resource reads/writes MUST enforce an
        // atomic authorized query (tenant+branch+resource+owner/revision).
        return TenantBranchResourceScopeEvidence.FromTrustedInternalSnapshots(
            refreshedIdentity, freshResource!, grant, requiredAction, finishUtc);
    }

    private static void ValidateExactGrant(
        TenantIdentityContextEvidence identity,
        TrustedBranchResourceSnapshot resource,
        TenantRequiredAction requiredAction,
        TrustedActorBranchGrantSnapshot grant,
        DateTimeOffset nowUtc)
    {
        if (grant.ActorId != identity.ActorId ||
            grant.TenantId != identity.TenantId ||
            grant.BranchId != resource.BranchId ||
            grant.RequiredAction != requiredAction ||
            !grant.IsActiveAtUtc(nowUtc) ||
            (grant.Scope == BranchGrantScope.OwnerOnly &&
             resource.OwnerActorId != identity.ActorId))
            throw new UnauthorizedAccessException("Branch or owner authorization denied.");
    }

    private static void ValidateUtc(DateTimeOffset value)
    {
        if (value == default || value.Offset != TimeSpan.Zero)
            throw new InvalidOperationException("Backend scope clock must be UTC.");
    }
}
