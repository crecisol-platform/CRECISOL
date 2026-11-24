using Crecisol.Domain.Authorization;
using Crecisol.Domain.Tenancy;
using Crecisol.Application.Tenancy;

namespace Crecisol.Application.Authorization;

/// <summary>Read metadata only. A backend adapter must join exact tenant,
/// account, membership, role, branch and action columns. Never trust browser
/// role labels, JWT role claims or an unscoped lookup.</summary>
public interface ITrustedC06PreparedRoleReader
{
    Task<IReadOnlyList<C06PreparedRoleSnapshot>?> ReadExactCandidatesAsync(
        TenantId verifiedTenant, TenantActorId verifiedActor,
        TenantBranchId backendBranch, TenantRequiredAction serverAction,
        CancellationToken cancellationToken);
}

/// <summary>Not a permission token: no caller can grant access from this
/// inspection result. Real policy activation requires C06-03/04 gates.</summary>
public sealed record C06PreparedRoleInspection(int CandidateCount)
{
    public bool AuthorizesPrivateResource => false;
    public override string ToString() => "C06PreparedRoleInspection[redacted;not_authorization]";
}

/// <summary>
/// C06-02 internal application use case. Identity evidence comes from the C04
/// verified resolver; backend branch and action must come from server-owned
/// policies. No HTTP/DI/production adapter exists in this microphase.
/// </summary>
public sealed class InspectPreparedTenantRoles
{
    private readonly ITrustedC06PreparedRoleReader reader;
    private readonly ITrustedHostUtcClock clock;

    public InspectPreparedTenantRoles(
        ITrustedC06PreparedRoleReader reader, ITrustedHostUtcClock clock)
    {
        this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<C06PreparedRoleInspection> ExecuteAsync(
        TenantIdentityContextEvidence verifiedIdentity,
        TenantBranchId backendBranch,
        TenantRequiredAction backendRequiredAction,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verifiedIdentity);
        ArgumentNullException.ThrowIfNull(backendBranch);
        ArgumentNullException.ThrowIfNull(backendRequiredAction);
        cancellationToken.ThrowIfCancellationRequested();
        if (verifiedIdentity.MembershipRevision < 1 ||
            verifiedIdentity.SessionRevision < 1 ||
            verifiedIdentity.HostRevision < 1 ||
            verifiedIdentity.TenantCatalogRevision < 1)
            throw new UnauthorizedAccessException("Invalid verified identity evidence.");

        var candidates = await reader.ReadExactCandidatesAsync(
            verifiedIdentity.TenantId, verifiedIdentity.ActorId, backendBranch,
            backendRequiredAction, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (candidates is null || candidates.Count > 128)
            throw new UnauthorizedAccessException("Prepared role catalog unavailable.");

        var now = clock.GetUtcNow();
        if (now == default || now.Offset != TimeSpan.Zero)
            throw new InvalidOperationException("Trusted role clock must be UTC.");
        var seen = new HashSet<(Guid RoleId, Guid MembershipId)>();
        var valid = 0;
        foreach (var item in candidates)
        {
            // An untrusted or broken adapter may never silently return another
            // actor, tenant, branch or action, even when that row is inactive.
            if (item is null ||
                item.TenantId != verifiedIdentity.TenantId ||
                item.ActorId != verifiedIdentity.ActorId ||
                item.BranchId != backendBranch ||
                item.ActionId != backendRequiredAction ||
                !seen.Add((item.RoleId, item.MembershipId)))
                throw new UnauthorizedAccessException("Role scope is inconsistent.");
            if (item.IsCurrentCandidateFor(
                verifiedIdentity.TenantId, verifiedIdentity.ActorId,
                backendBranch, backendRequiredAction, now))
                valid++;
        }
        return new C06PreparedRoleInspection(valid);
    }
}
