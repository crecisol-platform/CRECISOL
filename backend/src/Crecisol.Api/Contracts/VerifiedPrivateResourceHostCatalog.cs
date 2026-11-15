using Crecisol.Application.Tenancy;
using Crecisol.Domain.Tenancy;

namespace Crecisol.Api.Contracts;

/// <summary>
/// Reuses the existing C04/C05 verified, versioned host resolver for private
/// reads. No second host-to-tenant table, no implicit default tenant and no
/// header-derived tenant context. Host alone never grants identity or access.
/// This adapter is NOT registered in product Program.cs.
/// </summary>
public sealed class VerifiedPrivateResourceHostCatalog : ITrustedPrivateResourceHostCatalog
{
    private readonly ResolveVerifiedTenantHost resolver;

    public VerifiedPrivateResourceHostCatalog(ResolveVerifiedTenantHost resolver)
    {
        this.resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    }

    public async Task<VerifiedTenantHostHint?> ResolveDirectHostAsync(
        string directHost, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CanonicalTenantHost canonical;
        try
        {
            canonical = CanonicalTenantHost.Parse(directHost);
        }
        catch (InvalidOperationException)
        {
            return null;
        }

        // The trusted resolver refuses missing, duplicate, expired, suspended
        // or revoked bindings and validates server-side UTC proof of ownership.
        // Never cache this hint across domain revisions.
        return await resolver.ResolveAsync(canonical, cancellationToken);
    }

    public override string ToString() =>
        "VerifiedPrivateResourceHostCatalog[redacted]";
}
