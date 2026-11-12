using Crecisol.Domain.Tenancy;

namespace Crecisol.Application.Tenancy;

/// <summary>
/// A protected SERVER-SIDE catalogue lookup. Must return every matching
/// binding so ambiguity is rejected rather than silently choosing a tenant.
/// No input from cookies, claims or untrusted forwarding headers.
/// </summary>
public interface ITrustedVerifiedHostCatalog
{
    Task<IReadOnlyList<VerifiedTenantHostBinding>?> FindExactBindingsAsync(
        CanonicalTenantHost host,
        CancellationToken cancellationToken);
}

/// <summary>UTC clock supplied by backend, never caller query/header.</summary>
public interface ITrustedHostUtcClock
{
    DateTimeOffset GetUtcNow();
}

/// <summary>
/// Resolves a verified host to an INTERNAL CONTEXT HINT only.
/// The host remains untrusted and grants NO membership, access or DSN choice.
/// Not registered in API/DI. C04-05/06 must authorize private requests.
/// </summary>
public sealed class ResolveVerifiedTenantHost
{
    private readonly ITrustedVerifiedHostCatalog catalog;
    private readonly ITrustedHostUtcClock clock;

    public ResolveVerifiedTenantHost(
        ITrustedVerifiedHostCatalog catalog,
        ITrustedHostUtcClock clock)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<VerifiedTenantHostHint> ResolveAsync(
        CanonicalTenantHost host,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(host);
        cancellationToken.ThrowIfCancellationRequested();

        var matches = await catalog.FindExactBindingsAsync(host, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        // A missing, duplicate or corrupt catalogue entry cannot route a
        // request. Even duplicates belonging to the SAME tenant are rejected.
        if (matches is null || matches.Count != 1 || matches[0] is null)
            throw new UnauthorizedAccessException("Host is not uniquely verified.");

        var binding = matches[0];
        if (binding.Host != host)
            throw new InvalidOperationException("Catalogue host mismatch.");

        var nowUtc = clock.GetUtcNow();
        cancellationToken.ThrowIfCancellationRequested();
        if (!binding.IsVerifiedAt(nowUtc))
            throw new UnauthorizedAccessException("Host ownership is not currently verified.");

        return new VerifiedTenantHostHint(
            binding.TenantId, binding.AddressMode, binding.Revision);
    }
}
