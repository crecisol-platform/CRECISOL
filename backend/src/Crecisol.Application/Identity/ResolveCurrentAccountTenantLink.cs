using Crecisol.Application.Tenancy;
using Crecisol.Domain.Identity;
using Crecisol.Domain.Tenancy;

namespace Crecisol.Application.Identity;

/// <summary>
/// Backend-only account reader. The adapter must retrieve by a previously
/// VERIFIED actor, never by email/claims/query or a caller-provided GUID.
/// No HTTP endpoint, production implementation or database is registered.
/// </summary>
public interface ITrustedIdentityAccountReader
{
    Task<IdentityAccountSnapshot?> FindByVerifiedActorAsync(
        TenantActorId actorId, CancellationToken cancellationToken);
}

/// <summary>
/// Return ALL exact links for the actor AND tenant obtained through C04-05
/// verified session + direct-host + tenant membership resolution. Reject
/// duplicates instead of selecting first. No cross-tenant profile lookup.
/// </summary>
public interface ITrustedAccountTenantLinkReader
{
    Task<IReadOnlyList<AccountTenantLinkSnapshot>?> FindExactLinksAsync(
        TenantActorId actorId, TenantId tenantId, CancellationToken cancellationToken);
}

/// <summary>
/// C05-01 internal consistency check for an account and its independent tenant
/// membership. This does NOT authenticate a password, establish a session,
/// authorize a protected resource, issue a JWT or infer permissions from roles.
/// The only external argument is cancellation: no email/tenant/user/Host/claim.
/// </summary>
public sealed class ResolveCurrentAccountTenantLink
{
    private readonly ResolveCurrentTenantIdentityContext context;
    private readonly ITrustedIdentityAccountReader accounts;
    private readonly ITrustedAccountTenantLinkReader links;
    private readonly ITrustedHostUtcClock clock;

    public ResolveCurrentAccountTenantLink(
        ResolveCurrentTenantIdentityContext context,
        ITrustedIdentityAccountReader accounts,
        ITrustedAccountTenantLinkReader links,
        ITrustedHostUtcClock clock)
    {
        this.context = context ?? throw new ArgumentNullException(nameof(context));
        this.accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
        this.links = links ?? throw new ArgumentNullException(nameof(links));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<AccountTenantBindingEvidence> ExecuteAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // C04-05 has already correlated the backend-verified actor session,
        // trusted direct host, exact tenant membership and active tenant.
        // It is only internal evidence; a real authentication adapter does
        // not exist here and must be supplied in later C05 microphases.
        var identity = await context.ExecuteAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        var firstNow = clock.GetUtcNow();
        RequireUtc(firstNow);

        var account = await accounts.FindByVerifiedActorAsync(
            identity.ActorId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (account is null || account.ActorId != identity.ActorId ||
            !account.IsActiveAtUtc(firstNow))
            throw new UnauthorizedAccessException("Current account is not active.");

        var matches = await links.FindExactLinksAsync(
            identity.ActorId, identity.TenantId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (matches is null || matches.Count != 1 || matches[0] is null)
            throw new UnauthorizedAccessException("Unique tenant-account link required.");

        var link = matches[0];
        // Never trust a profile kind (owner/staff/consumer) as a permission.
        // A fresh link revision MUST equal C04-05 membership revision.
        if (link.ActorId != identity.ActorId ||
            link.TenantId != identity.TenantId ||
            link.Revision != identity.MembershipRevision ||
            !link.IsActiveAtUtc(firstNow))
            throw new UnauthorizedAccessException("Account link is not authorized.");

        // Concurrent suspension, revocation, rebind or duplicate creation
        // must not reuse a stale account/tenant snapshot.
        var latestAccount = await accounts.FindByVerifiedActorAsync(
            identity.ActorId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (latestAccount != account)
            throw new UnauthorizedAccessException("Account changed while resolving.");

        var latestLinks = await links.FindExactLinksAsync(
            identity.ActorId, identity.TenantId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (latestLinks is null || latestLinks.Count != 1 || latestLinks[0] != link)
            throw new UnauthorizedAccessException("Membership changed while resolving.");

        var latestIdentity = await context.ExecuteAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (latestIdentity != identity)
            throw new UnauthorizedAccessException("Verified tenant identity changed.");

        var finalNow = clock.GetUtcNow();
        RequireUtc(finalNow);
        cancellationToken.ThrowIfCancellationRequested();
        if (finalNow < firstNow)
            throw new InvalidOperationException("Identity clock moved backwards.");
        if (!account.IsActiveAtUtc(finalNow) || !link.IsActiveAtUtc(finalNow))
            throw new UnauthorizedAccessException("Account membership expired.");

        // No roles, branch rights, data access, storage choices, password
        // hashes or session material in this internal evidence.
        return AccountTenantBindingEvidence.FromTrustedInternalSnapshots(
            latestIdentity, latestAccount!, link, finalNow);
    }

    private static void RequireUtc(DateTimeOffset value)
    {
        if (value == default || value.Offset != TimeSpan.Zero)
            throw new InvalidOperationException("Identity reader clock must be UTC.");
    }
}
