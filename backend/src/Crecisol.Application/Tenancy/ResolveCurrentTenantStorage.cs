using Crecisol.Domain.Tenancy;

namespace Crecisol.Application.Tenancy;

/// <summary>
/// Future authenticated SERVER actor policy. Returns a tenant only after
/// identity, membership, intended action, branch and resource checks where
/// applicable. Not implemented or registered by C04-03.
/// Cannot accept tenant IDs, host names, cookies, claims or DSNs from HTTP.
/// </summary>
public interface ICurrentTenantPlacementAuthorization
{
    ValueTask<TenantId?> ResolveAuthorizedCurrentTenantAsync(
        CancellationToken cancellationToken);
}

/// <summary>
/// Future trusted platform catalogue, not a client-supplied storage selector.
/// This port returns metadata ONLY, never a connection string or credentials.
/// </summary>
public interface ITrustedTenantPlacementReader
{
    Task<TenantStoragePlacementSnapshot?> FindPlacementAsync(
        TenantId authorizedTenantId,
        CancellationToken cancellationToken);
}

/// <summary>
/// C04-03 internal placement decision. Fail closed if no actor permission,
/// unknown placement, failed catalog, cancellation or tenant mismatches.
/// No HTTP endpoint/DI or storage connection is enabled in this microphase.
/// </summary>
public sealed class ResolveCurrentTenantStorage
{
    private readonly ICurrentTenantPlacementAuthorization authorization;
    private readonly ITrustedTenantPlacementReader reader;

    public ResolveCurrentTenantStorage(
        ICurrentTenantPlacementAuthorization authorization,
        ITrustedTenantPlacementReader reader)
    {
        this.authorization = authorization ??
            throw new ArgumentNullException(nameof(authorization));
        this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
    }

    /// <summary>
    /// No client-provided tenant/host/DSN parameters are accepted. The only
    /// public input controls cancellation. Future trusted backend policies
    /// must supply the authenticated tenant; a GUID is not authority.
    /// </summary>
    public async Task<TenantStoragePlacementSnapshot> ExecuteAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var tenant = await authorization.ResolveAuthorizedCurrentTenantAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        if (tenant is null)
            throw new UnauthorizedAccessException("Tenant placement selection denied.");

        var placement = await reader.FindPlacementAsync(tenant, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        if (placement is null)
            throw new InvalidOperationException("Trusted tenant placement unavailable.");
        if (placement.TenantId != tenant)
            throw new InvalidOperationException("Placement catalogue returned another tenant.");

        return placement;
    }
}
