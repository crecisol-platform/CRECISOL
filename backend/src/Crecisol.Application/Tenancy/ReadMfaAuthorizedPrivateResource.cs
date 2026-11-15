using Crecisol.Application.Identity;
using Crecisol.Domain.Tenancy;

namespace Crecisol.Application.Tenancy;

/// <summary>
/// Private reference returned only after the same transaction validates
/// the MFA-bound session, tenant, branch, exact policy action and ownership.
/// The opaque resource ID itself and this result are NEVER a grant.
/// </summary>
public sealed record AuthorizedPrivateResourceReference
{
    private AuthorizedPrivateResourceReference(string externalKey)
        => ExternalKey=externalKey;

    public string ExternalKey { get; }

    public static AuthorizedPrivateResourceReference FromTrustedAtomicRead(
        string? externalKey)
    {
        if (string.IsNullOrWhiteSpace(externalKey) ||
            externalKey.Length>100 ||
            externalKey!=externalKey.Trim())
            throw new InvalidOperationException("Invalid protected resource reference.");
        return new AuthorizedPrivateResourceReference(externalKey);
    }

    public override string ToString()=>"AuthorizedPrivateResourceReference[redacted]";
}

public interface ITrustedMfaScopedPrivateResourceStore
{
    Task<AuthorizedPrivateResourceReference?> ReadAtomicallyAsync(
        TenantId backendVerifiedTenant,
        long backendVerifiedHostRevision,
        SessionTokenDigest verifiedTokenDigest,
        TenantResourceId untrustedResourceSelector,
        TenantRequiredAction backendOwnedAction,
        CancellationToken cancellationToken);
}

/// <summary>
/// C05 test-only integration boundary. A browser may supply only its opaque
/// session and a resource selector. Tenant/host come from the trusted backend
/// host resolver, the exact required action from the backend route policy.
/// It never accepts actor, branch, role, permission, DSN or tenant from HTTP.
/// </summary>
public sealed class ReadMfaAuthorizedPrivateResource
{
    private readonly ITrustedOpaqueSessionTokenProtector tokens;
    private readonly ITrustedMfaScopedPrivateResourceStore store;
    private readonly TenantRequiredAction routeAction;

    public ReadMfaAuthorizedPrivateResource(
        ITrustedOpaqueSessionTokenProtector tokens,
        ITrustedMfaScopedPrivateResourceStore store,
        TenantRequiredAction backendOwnedRouteAction)
    {
        this.tokens=tokens??throw new ArgumentNullException(nameof(tokens));
        this.store=store??throw new ArgumentNullException(nameof(store));
        routeAction=backendOwnedRouteAction??
            throw new ArgumentNullException(nameof(backendOwnedRouteAction));
    }

    public async Task<AuthorizedPrivateResourceReference> ExecuteAsync(
        string? presentedOpaqueSession,
        VerifiedTenantHostHint backendVerifiedHost,
        TenantResourceId untrustedResourceSelector,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(backendVerifiedHost);
        ArgumentNullException.ThrowIfNull(untrustedResourceSelector);
        cancellationToken.ThrowIfCancellationRequested();
        if (backendVerifiedHost.Revision<1)
            throw new UnauthorizedAccessException("Private resource unavailable.");

        var digest=tokens.TryDigest(presentedOpaqueSession);
        if (digest is null)
            throw new UnauthorizedAccessException("Private resource unavailable.");
        var reference=await store.ReadAtomicallyAsync(
            backendVerifiedHost.TenantId,backendVerifiedHost.Revision,
            digest,untrustedResourceSelector,routeAction,cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return reference??
            throw new UnauthorizedAccessException("Private resource unavailable.");
    }

    public override string ToString()=>"ReadMfaAuthorizedPrivateResource[redacted]";
}
