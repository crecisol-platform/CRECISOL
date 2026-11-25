namespace Crecisol.Domain.Tenancy;

/// <summary>
/// Authoritative C06-04 policy for the ONLY current tenant-private case read
/// operation: private/read -> cases.read -> RESOURCE_READ.
/// Inputs MUST be constructed from locked backend DB rows, except selectors
/// which remain untrusted and are compared to database resource identity.
/// No C06-02 proposal, role label or browser-supplied attribute is a grant.
/// </summary>
public static class C06CaseAbacPolicy
{
    private static readonly HashSet<string> ReadRoles = new(StringComparer.Ordinal)
    {
        "MEMBER", "STAFF", "VIEWER", "MANAGER", "OWNER", "ADMIN",
        "tenant-owner", "tenant-admin", "tenant-branch-manager",
        "tenant-staff", "tenant-viewer"
    };

    private static readonly HashSet<string> PrivilegedBranchReaders = new(StringComparer.Ordinal)
    {
        "OWNER", "ADMIN", "tenant-owner", "tenant-admin"
    };

    /// <summary>
    /// Deny unless identity+role+action+tenant+branch+owner/exact resource
    /// and an independently revocable grant are all current in one transaction.
    /// A branch-wide grant to an ordinary employee is NOT enough: there must
    /// also be a server-stored, time-bound assignment to THIS exact case.
    /// </summary>
    public static bool Allows(C06CaseAbacEvidence? e)
    {
        if (e is null || e.Identity is null || e.ResourceTenant is null ||
            e.ResourceBranch is null || e.ResourceId is null ||
            e.ResourceOwner is null || e.SelectorBranch is null ||
            e.SelectorResource is null || e.GrantTenant is null ||
            e.GrantActor is null || e.GrantBranch is null ||
            e.RequiredAction is null || e.RoleCode is null)
            return false;

        if (!Utc(e.CheckedAtUtc) || e.Identity.VerifiedAtUtc > e.CheckedAtUtc ||
            !ReadRoles.Contains(e.RoleCode) ||
            e.RequiredAction.Value != "cases.read" ||
            e.GrantPermissionCode != "RESOURCE_READ" ||
            e.Identity.TenantId != e.ResourceTenant ||
            e.Identity.TenantId != e.GrantTenant ||
            e.Identity.ActorId != e.GrantActor ||
            e.ResourceBranch != e.SelectorBranch ||
            e.ResourceId != e.SelectorResource ||
            e.ResourceBranch != e.GrantBranch ||
            !e.BranchActive || !e.ResourceActive ||
            e.GrantRevision <= 0 || !Utc(e.GrantIssuedAtUtc) ||
            e.GrantIssuedAtUtc > e.CheckedAtUtc ||
            e.GrantExpiresAtUtc is not { } expires ||
            !Utc(expires) || e.CheckedAtUtc >= expires ||
            e.GrantRevokedAtUtc is not null)
            return false;

        // Legacy scope 1: specific resource owned by this actor. This preserves
        // the successful C05 owner-only path without widening its semantics.
        if (e.GrantScope == 1)
            return e.GrantResource is not null &&
                e.GrantResource == e.ResourceId &&
                e.ResourceOwner == e.Identity.ActorId &&
                e.Assignment is null;

        if (e.GrantScope != 2 || e.GrantResource is not null)
            return false;

        // Only an already-verified privileged tenant role PLUS an explicit,
        // current branch grant may read branch-wide. PLATFORM is never eligible.
        if (PrivilegedBranchReaders.Contains(e.RoleCode))
            return e.Assignment is null;

        // Two ordinary employees in one branch still need different explicit
        // resource assignments. A broad branch grant alone NEVER suffices.
        var a = e.Assignment;
        return a is not null &&
            a.TenantId == e.Identity.TenantId &&
            a.ActorId == e.Identity.ActorId &&
            a.BranchId == e.ResourceBranch &&
            a.ResourceId == e.ResourceId &&
            a.Action.Value == e.RequiredAction.Value &&
            a.AssignmentState == 1 &&
            a.Revision > 0 &&
            a.RevokedAtUtc is null &&
            Utc(a.IssuedAtUtc) &&
            Utc(a.ExpiresAtUtc) &&
            a.IssuedAtUtc <= e.CheckedAtUtc &&
            e.CheckedAtUtc < a.ExpiresAtUtc;
    }

    private static bool Utc(DateTimeOffset time) =>
        time != default && time.Offset == TimeSpan.Zero;
}

/// <summary>
/// Trusted tenant+resource+grant snapshot assembled in a single locked C05
/// operational transaction. Selectors are compared, never trusted as grants.
/// All returned/exposed status is a plain boolean; no sensitive deny reasons.
/// </summary>
public sealed record C06CaseAbacEvidence
{
    public required C06OperationalMembershipContext Identity { get; init; }
    public required TenantId ResourceTenant { get; init; }
    public required TenantBranchId ResourceBranch { get; init; }
    public required TenantResourceId ResourceId { get; init; }
    public required TenantActorId ResourceOwner { get; init; }
    public required TenantBranchId SelectorBranch { get; init; }
    public required TenantResourceId SelectorResource { get; init; }
    public required bool BranchActive { get; init; }
    public required bool ResourceActive { get; init; }
    public required string RoleCode { get; init; }
    public required TenantRequiredAction RequiredAction { get; init; }
    public required TenantId GrantTenant { get; init; }
    public required TenantActorId GrantActor { get; init; }
    public required TenantBranchId GrantBranch { get; init; }
    public required TenantResourceId? GrantResource { get; init; }
    public required string GrantPermissionCode { get; init; }
    public required int GrantScope { get; init; }
    public required long GrantRevision { get; init; }
    public required DateTimeOffset GrantIssuedAtUtc { get; init; }
    public required DateTimeOffset? GrantExpiresAtUtc { get; init; }
    public required DateTimeOffset? GrantRevokedAtUtc { get; init; }
    public required C06ExactCaseAssignment? Assignment { get; init; }
    public required DateTimeOffset CheckedAtUtc { get; init; }

    public override string ToString() => "C06CaseAbacEvidence[redacted;not_a_token]";
}

/// <summary>
/// The only way an ordinary employee with a branch-wide legacy read grant
/// can inspect a case assigned to a different owner. Never created from a
/// request and never generated by C06-02 role proposals.
/// </summary>
public sealed record C06ExactCaseAssignment
{
    public required TenantId TenantId { get; init; }
    public required TenantActorId ActorId { get; init; }
    public required TenantBranchId BranchId { get; init; }
    public required TenantResourceId ResourceId { get; init; }
    public required TenantRequiredAction Action { get; init; }
    public required int AssignmentState { get; init; }
    public required long Revision { get; init; }
    public required DateTimeOffset IssuedAtUtc { get; init; }
    public required DateTimeOffset ExpiresAtUtc { get; init; }
    public required DateTimeOffset? RevokedAtUtc { get; init; }

    public override string ToString() => "C06ExactCaseAssignment[redacted;not_a_grant]";
}
