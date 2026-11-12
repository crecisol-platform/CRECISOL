using Crecisol.Domain.Tenancy;

namespace Crecisol.Application.Tenancy;

/// <summary>
/// Backend-owned authorization port. A future implementation MUST validate the
/// signed-in platform actor, action, step-up and effective scope server-side.
/// There is no permissive production implementation or DI registration in C04-01.
/// Host, slug, tenant ID, caller boolean and browser headers grant nothing.
/// </summary>
public interface IPlatformCatalogReadAuthorization
{
    ValueTask<bool> AllowsInternalCatalogReadAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Restricted read port. An eventual adapter MUST use server-owned catalog
/// storage, enforce tenant ID equality and never derive a database from a caller.
/// No write, list-all, create, or hostname lookup operation is exposed.
/// </summary>
public interface ITrustedTenantCatalogReader
{
    Task<TenantCatalogSnapshot?> FindByIdAsync(
        TenantId id, CancellationToken cancellationToken);
}

/// <summary>
/// Internal-only projection for a future authorized platform administration flow.
/// There is no public HTTP contract, tenant data, connection string, internal
/// legal name, storage placement, membership, token, or verified-host claim.
/// </summary>
public sealed record InternalTenantCatalogView(
    Guid TenantId,
    string DisplayName,
    TenantLifecycleState State,
    TenantAddressMode AddressMode,
    long Revision);

/// <summary>
/// Checks backend authorization BEFORE any catalog lookup and rejects a reader
/// that supplies a record for a different tenant. This does not implement
/// actual authentication or per-resource tenancy for downstream chapters.
/// </summary>
public sealed class ReadInternalTenantCatalog
{
    private readonly IPlatformCatalogReadAuthorization authorization;
    private readonly ITrustedTenantCatalogReader reader;

    public ReadInternalTenantCatalog(
        IPlatformCatalogReadAuthorization authorization,
        ITrustedTenantCatalogReader reader)
    {
        this.authorization = authorization ??
            throw new ArgumentNullException(nameof(authorization));
        this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
    }

    public async Task<InternalTenantCatalogView?> ExecuteAsync(
        TenantId id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        cancellationToken.ThrowIfCancellationRequested();

        if (!await authorization.AllowsInternalCatalogReadAsync(cancellationToken))
            throw new UnauthorizedAccessException("Internal tenant catalog access denied.");

        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = await reader.FindByIdAsync(id, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        if (snapshot is null) return null;
        if (snapshot.TenantId != id)
            throw new InvalidOperationException("Catalog reader returned another tenant.");

        // Project a strict allowlist, not the source object or arbitrary data.
        return new InternalTenantCatalogView(
            snapshot.TenantId.Value,
            snapshot.DisplayName,
            snapshot.State,
            snapshot.AddressMode,
            snapshot.Revision);
    }
}
