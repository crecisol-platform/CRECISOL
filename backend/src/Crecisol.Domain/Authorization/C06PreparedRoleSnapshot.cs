using System.Text.RegularExpressions;
using Crecisol.Domain.Tenancy;

namespace Crecisol.Domain.Authorization;

/// <summary>
/// C06-02 is an INTERNAL role-assignment model, not a bearer permission.
/// None of its candidates may authorize HTTP or a private resource. All
/// actions in C06-01 remain unbound to a C06 runtime policy.
/// </summary>
public sealed record C06PreparedRoleSnapshot
{
    private C06PreparedRoleSnapshot(
        TenantId tenantId, TenantActorId actorId, TenantBranchId branchId,
        Guid membershipId, Guid roleId, string roleCode,
        TenantRequiredAction actionId, bool membershipActive, bool branchActive,
        bool roleActive, bool revoked, long roleRevision,
        long assignmentRevision, DateTimeOffset fromUtc, DateTimeOffset untilUtc)
    {
        TenantId = tenantId;
        ActorId = actorId;
        BranchId = branchId;
        MembershipId = membershipId;
        RoleId = roleId;
        RoleCode = roleCode;
        ActionId = actionId;
        MembershipActive = membershipActive;
        BranchActive = branchActive;
        RoleActive = roleActive;
        Revoked = revoked;
        RoleRevision = roleRevision;
        AssignmentRevision = assignmentRevision;
        FromUtc = fromUtc;
        UntilUtc = untilUtc;
    }

    public TenantId TenantId { get; }
    public TenantActorId ActorId { get; }
    public TenantBranchId BranchId { get; }
    public Guid MembershipId { get; }
    public Guid RoleId { get; }
    public string RoleCode { get; }
    public TenantRequiredAction ActionId { get; }
    public bool MembershipActive { get; }
    public bool BranchActive { get; }
    public bool RoleActive { get; }
    public bool Revoked { get; }
    public long RoleRevision { get; }
    public long AssignmentRevision { get; }
    public DateTimeOffset FromUtc { get; }
    public DateTimeOffset UntilUtc { get; }

    public static C06PreparedRoleSnapshot RestoreFromTrustedStore(
        TenantId tenantId, TenantActorId actorId, TenantBranchId branchId,
        Guid membershipId, Guid roleId, string roleCode,
        TenantRequiredAction actionId, bool membershipActive, bool branchActive,
        bool roleActive, bool revoked, long roleRevision,
        long assignmentRevision, DateTimeOffset fromUtc, DateTimeOffset untilUtc,
        bool actionGrantable)
    {
        ArgumentNullException.ThrowIfNull(tenantId);
        ArgumentNullException.ThrowIfNull(actorId);
        ArgumentNullException.ThrowIfNull(branchId);
        ArgumentNullException.ThrowIfNull(actionId);
        // A grantable capability here means a policy has been activated ahead
        // of its gate. Fail closed instead of returning an authorization signal.
        if (actionGrantable)
            throw new UnauthorizedAccessException("C06-02 actions cannot authorize resources.");
        if (membershipId == Guid.Empty || roleId == Guid.Empty ||
            roleRevision < 1 || assignmentRevision < 1 ||
            string.IsNullOrEmpty(roleCode) || roleCode.Length > 64 ||
            !Regex.IsMatch(roleCode, @"\A[a-z][a-z0-9-]{2,63}\z",
                RegexOptions.CultureInvariant))
            throw new InvalidOperationException("Invalid prepared role metadata.");
        if (fromUtc == default || untilUtc == default ||
            fromUtc.Offset != TimeSpan.Zero || untilUtc.Offset != TimeSpan.Zero ||
            fromUtc >= untilUtc)
            throw new InvalidOperationException("Invalid prepared role UTC window.");
        return new C06PreparedRoleSnapshot(
            tenantId, actorId, branchId, membershipId, roleId, roleCode,
            actionId, membershipActive, branchActive, roleActive, revoked,
            roleRevision, assignmentRevision, fromUtc, untilUtc);
    }

    public bool IsCurrentCandidateFor(
        TenantId verifiedTenant, TenantActorId verifiedActor,
        TenantBranchId backendBranch, TenantRequiredAction backendAction,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(verifiedTenant);
        ArgumentNullException.ThrowIfNull(verifiedActor);
        ArgumentNullException.ThrowIfNull(backendBranch);
        ArgumentNullException.ThrowIfNull(backendAction);
        if (nowUtc == default || nowUtc.Offset != TimeSpan.Zero)
            throw new InvalidOperationException("Trusted authorization clock must be UTC.");
        return TenantId == verifiedTenant &&
            ActorId == verifiedActor &&
            BranchId == backendBranch &&
            ActionId == backendAction &&
            MembershipActive && BranchActive && RoleActive && !Revoked &&
            FromUtc <= nowUtc && nowUtc < UntilUtc;
    }

    public override string ToString() => "C06PreparedRoleSnapshot[redacted;not_authorization]";
}
