using Crecisol.Domain.Tenancy;

namespace Crecisol.Application.Tenancy;

/// <summary>
/// FUTURE backend actor adapter: must validate token/cookie cryptographically,
/// issuer/audience/session revocation and active account on each request.
/// Do NOT construct actor evidence from a bare claim, header or user GUID.
/// </summary>
public interface ITrustedCurrentActorSessionReader
{
    ValueTask<TrustedActorSessionSnapshot?> ReadCurrentVerifiedSessionAsync(
        CancellationToken cancellationToken);
}

/// <summary>
/// FUTURE backend direct-host adapter, enforcing the C04-04 transport policy
/// BEFORE returning a typed canonical host. Host alone is never membership.
/// No untrusted Forwarded, query, slug or X-Tenant-Id selectors permitted.
/// </summary>
public interface ITrustedCurrentDirectHostReader
{
    ValueTask<CanonicalTenantHost?> ReadCurrentDirectHostAsync(
        CancellationToken cancellationToken);
}

/// <summary>
/// Restricted membership port. Returns ALL matching rows to detect duplicate
/// or conflicting grants. A real adapter must query protected backend storage
/// with actor AND tenant predicates, never accept caller-supplied tenant IDs.
/// </summary>
public interface ITrustedActorTenantMembershipReader
{
    Task<IReadOnlyList<TenantActorMembershipSnapshot>?> FindExactMembershipsAsync(
        TenantActorId actorId,
        TenantId tenantId,
        CancellationToken cancellationToken);
}

/// <summary>
/// C04-05 internal, fail-closed SESSION + VERIFIED HOST + MEMBERSHIP + ACTIVE
/// TENANT correlation. Result is context EVIDENCE, not resource authorization.
/// NO production identity reader/DI/HTTP endpoints have been implemented.
/// </summary>
public sealed class ResolveCurrentTenantIdentityContext
{
    private readonly ITrustedCurrentActorSessionReader sessions;
    private readonly ITrustedCurrentDirectHostReader currentHost;
    private readonly ResolveVerifiedTenantHost verifiedHostResolver;
    private readonly ITrustedActorTenantMembershipReader membershipReader;
    private readonly ITrustedTenantCatalogReader tenants;
    private readonly ITrustedHostUtcClock clock;

    public ResolveCurrentTenantIdentityContext(
        ITrustedCurrentActorSessionReader sessions,
        ITrustedCurrentDirectHostReader currentHost,
        ResolveVerifiedTenantHost verifiedHostResolver,
        ITrustedActorTenantMembershipReader membershipReader,
        ITrustedTenantCatalogReader tenants,
        ITrustedHostUtcClock clock)
    {
        this.sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        this.currentHost = currentHost ?? throw new ArgumentNullException(nameof(currentHost));
        this.verifiedHostResolver = verifiedHostResolver ??
            throw new ArgumentNullException(nameof(verifiedHostResolver));
        this.membershipReader = membershipReader ??
            throw new ArgumentNullException(nameof(membershipReader));
        this.tenants = tenants ?? throw new ArgumentNullException(nameof(tenants));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>
    /// A request cannot supply actor ID, tenant ID, JWT claims, user role, host,
    /// branch, scope, or connection string. Only cancellation is an input.
    /// </summary>
    public async Task<TenantIdentityContextEvidence> ExecuteAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Authentication is a prerequisite, checked BEFORE host or tenant data.
        var session = await sessions.ReadCurrentVerifiedSessionAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (session is null)
            throw new UnauthorizedAccessException("Verified actor session required.");

        var initialNow = clock.GetUtcNow();
        if (!session.IsActiveAtUtc(initialNow))
            throw new UnauthorizedAccessException("Actor session is not active.");

        var host = await currentHost.ReadCurrentDirectHostAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (host is null)
            throw new UnauthorizedAccessException("Verified host context is required.");

        var hostHint = await verifiedHostResolver.ResolveAsync(host, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        // Host is NOT enough: bind the authenticated actor to EXACTLY this
        // verified tenant. Never list all tenants or choose the first grant.
        var matches = await membershipReader.FindExactMembershipsAsync(
            session.ActorId, hostHint.TenantId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (matches is null || matches.Count != 1 || matches[0] is null)
            throw new UnauthorizedAccessException("Unique tenant membership required.");

        var membership = matches[0];
        if (membership.ActorId != session.ActorId ||
            membership.TenantId != hostHint.TenantId)
            throw new InvalidOperationException("Membership belongs to another actor or tenant.");
        if (!membership.IsActiveAtUtc(initialNow))
            throw new UnauthorizedAccessException("Membership is not active.");

        var catalog = await tenants.FindByIdAsync(hostHint.TenantId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (catalog is null || catalog.TenantId != hostHint.TenantId ||
            catalog.State != TenantLifecycleState.Active ||
            catalog.AddressMode != hostHint.AddressMode ||
            catalog.UpdatedAtUtc > initialNow)
            throw new UnauthorizedAccessException("Tenant is unavailable or out of scope.");

        // A concurrent domain rebind/revision change must not silently
        // reuse a context for a different tenant within the same operation.
        var recheckedHost = await verifiedHostResolver.ResolveAsync(host, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (recheckedHost != hostHint)
            throw new UnauthorizedAccessException("Host binding changed during resolution.");

        // Check expiry again after all asynchronous reads to avoid returning
        // a context on the stale side of the session/membership window.
        var finalNow = clock.GetUtcNow();
        cancellationToken.ThrowIfCancellationRequested();
        if (!session.IsActiveAtUtc(finalNow) ||
            !membership.IsActiveAtUtc(finalNow) ||
            catalog.UpdatedAtUtc > finalNow)
            throw new UnauthorizedAccessException("Tenant identity context expired.");

        return TenantIdentityContextEvidence.FromVerifiedInternalEvidence(
            session, membership, hostHint, catalog);
    }
}
