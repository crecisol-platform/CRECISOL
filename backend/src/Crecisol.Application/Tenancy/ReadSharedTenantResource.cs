using Crecisol.Domain.Tenancy;

namespace Crecisol.Application.Tenancy;

/// <summary>
/// One branch authorized for the current backend actor. This is not an HTTP DTO.
/// A tenant ID or branch ID alone never provides authorization; only a trusted
/// resolver may create a scope after identity, membership, action and branch checks.
/// Actual resolver implementation belongs to C04-05/06, and NONE is registered now.
/// </summary>
public sealed record AuthorizedSharedReadScope
{
    private AuthorizedSharedReadScope(TenantId tenantId, Guid branchId)
    {
        TenantId = tenantId;
        BranchId = branchId;
    }

    public TenantId TenantId { get; }
    public Guid BranchId { get; }

    public static AuthorizedSharedReadScope FromVerifiedBackendDecision(
        TenantId tenantId, Guid branchId)
    {
        ArgumentNullException.ThrowIfNull(tenantId);
        if (branchId == Guid.Empty)
            throw new InvalidOperationException("An authorized branch is required.");
        return new AuthorizedSharedReadScope(tenantId, branchId);
    }

    public override string ToString() => "AuthorizedSharedReadScope[redacted]";
}

/// <summary>
/// Future server-owned policy: returns null unless a real authenticated actor
/// has an explicitly authorized tenant, branch, operation and membership.
/// Cannot take caller-provided IDs, role flags, hosts, cookies or DSNs.
/// </summary>
public interface IAuthorizedSharedReadScopeResolver
{
    ValueTask<AuthorizedSharedReadScope?> ResolveForCurrentActorAsync(
        CancellationToken cancellationToken);
}

/// <summary>Internal-only resource projection. No public tenant endpoint exists.</summary>
public sealed record SharedResourceReadResult(
    TenantId TenantId, Guid BranchId, Guid ResourceId, string ExternalKey)
{
    public override string ToString() => "SharedResourceReadResult[redacted]";
}

/// <summary>
/// Port for a strictly scoped SQL SELECT: the reader must apply tenant,
/// branch and resource predicates independently of primary/foreign keys.
/// </summary>
public interface ISharedTenantResourceReader
{
    Task<SharedResourceReadResult?> FindAsync(
        AuthorizedSharedReadScope scope, Guid resourceId,
        CancellationToken cancellationToken);
}

/// <summary>
/// Never takes tenant/branch IDs from the caller; always resolves the server
/// actor's permitted scope BEFORE touching storage. No permissive resolver/DI
/// registration is provided by C04-02.
/// </summary>
public sealed class ReadSharedTenantResource(
    IAuthorizedSharedReadScopeResolver authorization,
    ISharedTenantResourceReader reader)
{
    private readonly IAuthorizedSharedReadScopeResolver authorization =
        authorization ?? throw new ArgumentNullException(nameof(authorization));
    private readonly ISharedTenantResourceReader reader =
        reader ?? throw new ArgumentNullException(nameof(reader));

    public async Task<SharedResourceReadResult?> ExecuteAsync(
        Guid resourceId, CancellationToken cancellationToken)
    {
        if (resourceId == Guid.Empty)
            throw new InvalidOperationException("A resource identifier is required.");
        cancellationToken.ThrowIfCancellationRequested();

        var scope = await authorization.ResolveForCurrentActorAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (scope is null)
            throw new UnauthorizedAccessException("Resource read denied.");

        var result = await reader.FindAsync(scope, resourceId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        if (result is not null &&
            (result.TenantId != scope.TenantId ||
             result.BranchId != scope.BranchId ||
             result.ResourceId != resourceId))
            throw new InvalidOperationException("Resource reader violated authorized scope.");

        return result;
    }
}
