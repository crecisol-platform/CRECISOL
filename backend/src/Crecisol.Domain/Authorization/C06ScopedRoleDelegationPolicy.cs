using Crecisol.Domain.Tenancy;

namespace Crecisol.Domain.Authorization;

/// <summary>
/// First bounded C06-11 delegation policy: one existing tenant-private read
/// capability, exact branch/resource, no grant of administration itself.
/// This is a server-only domain decision, NOT a credential, persistence layer,
/// HTTP contract or replacement for C06CaseAbacPolicy. The adapter MUST load
/// locked current rows and consume the factor proof in the same transaction.
/// </summary>
public enum C06DelegationCommand { Assign = 1, Revoke = 2 }

public sealed record C06DelegationScope(
    TenantId Tenant, TenantBranchId Branch, TenantResourceId Resource, TenantRequiredAction Action)
{
    public override string ToString() => "C06DelegationScope[redacted]";
}

/// <summary>Backend-owned operation intent. The selector revision is a
/// precondition, never a source of authority.</summary>
public sealed record C06DelegationIntent(
    C06DelegationCommand Command, C06DelegationScope Scope,
    TenantActorId Subject, Guid SubjectMembershipId, Guid RoleId,
    long ExpectedRoleRevision, long ExpectedSubjectMembershipRevision,
    long ExpectedCeilingRevision, long ExpectedGrantRevision, DateTimeOffset? UntilUtc)
{
    public override string ToString() => "C06DelegationIntent[redacted]";
}

/// <summary>One exact target role/action grant read under lock. Null represents
/// absence only after a unique-key lookup inside the transaction.</summary>
public sealed record C06DelegationGrant(
    C06DelegationScope Scope, TenantActorId Subject, Guid SubjectMembershipId,
    Guid RoleId, long Revision, DateTimeOffset FromUtc, DateTimeOffset UntilUtc,
    DateTimeOffset? RevokedAtUtc)
{
    public override string ToString() => "C06DelegationGrant[redacted]";
}

/// <summary>
/// Explicit current delegation ceiling, from a trusted store, never a label
/// or a C06-02 proposal. No chain of subdelegation is granted by this policy.
/// </summary>
public sealed record C06DelegationCeiling(
    C06DelegationScope Scope, TenantActorId Actor, Guid ActorMembershipId,
    long Revision, bool MayAssign, bool MayRevoke, DateTimeOffset FromUtc,
    DateTimeOffset UntilUtc, DateTimeOffset? RevokedAtUtc)
{
    public override string ToString() => "C06DelegationCeiling[redacted]";
}

/// <summary>
/// Evidence of already validated and atomically consumed operation-specific
/// step-up. Must be constructed exclusively inside the trusted adapter;
/// checking these fields does not verify TOTP or prevent replay by itself.
/// </summary>
public sealed record C06DelegationStepUp(
    C06DelegationIntent Intent, TenantId Tenant, TenantActorId Actor,
    Guid ActorMembershipId, long SessionRevision, long AccountRevision,
    long MembershipRevision, DateTimeOffset ConsumedAtUtc)
{
    public override string ToString() => "C06DelegationStepUp[redacted]";
}

/// <summary>All evidence from one locked backend transaction. A browser must
/// never be allowed to deserialize this type as evidence of its own rights.</summary>
public sealed record C06LockedDelegationEvidence
{
    public required C06OperationalMembershipContext Identity { get; init; }
    public required C06DelegationIntent Intent { get; init; }
    public required C06DelegationScope VerifiedResourceScope { get; init; }
    public required bool BranchActive { get; init; }
    public required bool ResourceActive { get; init; }
    public required bool CommercialAccessVerified { get; init; }
    public required string ActorRoleCode { get; init; }
    public required TenantId SubjectTenant { get; init; }
    public required TenantActorId SubjectActor { get; init; }
    public required Guid SubjectMembershipId { get; init; }
    public required long SubjectMembershipRevision { get; init; }
    public required bool SubjectMembershipActive { get; init; }
    public required DateTimeOffset SubjectMembershipUntilUtc { get; init; }
    public required Guid VerifiedRoleId { get; init; }
    public required TenantId RoleTenant { get; init; }
    public required long RoleRevision { get; init; }
    public required string TargetRoleCode { get; init; }
    public required bool RoleActive { get; init; }
    public required C06DelegationCeiling Ceiling { get; init; }
    public required C06CaseAbacEvidence? ActorEffectiveRead { get; init; }
    public required C06DelegationStepUp StepUp { get; init; }
    public required C06DelegationGrant? Existing { get; init; }
    public required DateTimeOffset NowUtc { get; init; }

    public override string ToString() => "C06LockedDelegationEvidence[redacted;server_only]";
}

/// <summary>Versioned mutation decision only. The writer must apply a CAS
/// under the original lock, update the authorization revision, append audit,
/// and commit atomically. No decision here grants a private-resource read.</summary>
public sealed record C06DelegationDecision(
    C06DelegationCommand Command, long ExpectedRevision, long NextRevision)
{
    public override string ToString() => "C06DelegationDecision[redacted;not_a_grant]";
}

public static class C06ScopedRoleDelegationPolicy
{
    // Initial reviewed allowlist: deliberately not cases.read or a new module.
    // Expanding it requires an independently verified catalog/policy overlay.
    private const string ReadAction = "identity.private.read";
    private static readonly HashSet<string> Administrators = new(StringComparer.Ordinal)
        { "OWNER", "ADMIN", "tenant-owner", "tenant-admin" };
    private static readonly HashSet<string> RecipientRoles = new(StringComparer.Ordinal)
        { "MEMBER", "STAFF", "VIEWER", "MANAGER", "tenant-staff", "tenant-viewer",
          "tenant-branch-manager" };

    /// <summary>Checks eligibility only, never produces a mutation decision or proof.</summary>
    public static bool CanPrepare(C06LockedDelegationEvidence? e) => DecideCore(e, false) is not null;

    public static C06DelegationDecision? Decide(C06LockedDelegationEvidence? e) => DecideCore(e, true);

    private static C06DelegationDecision? DecideCore(C06LockedDelegationEvidence? e, bool requireStepUp)
    {
        if (e is null || e.Identity is null || e.Intent is null ||
            e.Ceiling is null || (requireStepUp && e.StepUp is null) || e.SubjectTenant is null ||
            e.SubjectActor is null || e.RoleTenant is null ||
            e.ActorRoleCode is null || e.TargetRoleCode is null)
            return null;

        var i = e.Intent;
        var c = e.Ceiling;
        var p = e.StepUp;
        var now = e.NowUtc;
        if (!Utc(now) || e.Identity.VerifiedAtUtc != now ||
            !Enum.IsDefined(i.Command) || !ValidScope(i.Scope) ||
            !ValidScope(e.VerifiedResourceScope) || i.Scope != e.VerifiedResourceScope ||
            i.Scope.Tenant != e.Identity.TenantId ||
            !e.BranchActive || !e.ResourceActive || !e.CommercialAccessVerified ||
            !Administrators.Contains(e.ActorRoleCode) ||
            !RecipientRoles.Contains(e.TargetRoleCode) ||
            i.Subject is null || i.Subject == e.Identity.ActorId ||
            i.Subject != e.SubjectActor || e.SubjectTenant != i.Scope.Tenant ||
            i.SubjectMembershipId == Guid.Empty || i.SubjectMembershipId != e.SubjectMembershipId ||
            i.SubjectMembershipId == e.Identity.MembershipId ||
            e.SubjectMembershipRevision <= 0 ||
            i.ExpectedSubjectMembershipRevision != e.SubjectMembershipRevision ||
            i.RoleId == Guid.Empty || i.RoleId != e.VerifiedRoleId ||
            e.RoleTenant != i.Scope.Tenant || !e.RoleActive ||
            e.RoleRevision <= 0 || i.ExpectedRoleRevision != e.RoleRevision ||
            i.ExpectedGrantRevision < 0 || i.ExpectedGrantRevision == long.MaxValue)
            return null;

        if (!ValidScope(c.Scope) || c.Scope != i.Scope || c.Actor != e.Identity.ActorId ||
            c.ActorMembershipId != e.Identity.MembershipId || c.Revision <= 0 ||
            i.ExpectedCeilingRevision != c.Revision ||
            !Utc(c.FromUtc) || !Utc(c.UntilUtc) ||
            c.FromUtc > now || c.UntilUtc <= now || c.RevokedAtUtc is not null ||
            (i.Command == C06DelegationCommand.Assign && !c.MayAssign) ||
            (i.Command == C06DelegationCommand.Revoke && !c.MayRevoke))
            return null;

        // The one-use cryptographic verification belongs to the adapter.
        // Exact intent binding and same transaction clock are enforced here.
        if (requireStepUp && (p is null || p.Intent != i || p.Tenant != e.Identity.TenantId || p.Actor != e.Identity.ActorId ||
            p.ActorMembershipId != e.Identity.MembershipId ||
            p.SessionRevision != e.Identity.SessionRevision ||
            p.AccountRevision != e.Identity.AccountRevision ||
            p.MembershipRevision != e.Identity.MembershipRevision ||
            p.ConsumedAtUtc != now))
            return null;

        var g = e.Existing;
        if (g is not null && (!ValidScope(g.Scope) || g.Scope != i.Scope ||
            g.Subject != i.Subject || g.SubjectMembershipId != i.SubjectMembershipId ||
            g.RoleId != i.RoleId || g.Revision <= 0 || g.Revision != i.ExpectedGrantRevision ||
            !Utc(g.FromUtc) || !Utc(g.UntilUtc) || g.FromUtc > now || g.FromUtc >= g.UntilUtc ||
            (g.RevokedAtUtc.HasValue && (!Utc(g.RevokedAtUtc.Value) ||
             g.RevokedAtUtc.Value < g.FromUtc || g.RevokedAtUtc.Value > now))))
            return null;
        if (g is null && i.ExpectedGrantRevision != 0)
            return null;

        if (i.Command == C06DelegationCommand.Revoke)
        {
            // Revocation may close an expired grant or one belonging to an
            // inactive subject; it must not create a row or revive a grant.
            if (i.UntilUtc is not null || g is null || g.RevokedAtUtc is not null)
                return null;
        }
        else
        {
            // No implicit extension, reactivation, or overwrite. New grants
            // require a separate reviewed lifecycle, not reuse of revoked rows.
            var own = e.ActorEffectiveRead;
            if (own is null || own.Identity != e.Identity || own.RoleCode != e.ActorRoleCode ||
                own.CheckedAtUtc != now || own.ResourceTenant != i.Scope.Tenant ||
                own.ResourceBranch != i.Scope.Branch || own.ResourceId != i.Scope.Resource ||
                own.RequiredAction != i.Scope.Action || !C06CaseAbacPolicy.Allows(own) ||
                g is not null || !e.SubjectMembershipActive ||
                !Utc(e.SubjectMembershipUntilUtc) || e.SubjectMembershipUntilUtc <= now ||
                i.UntilUtc is not { } until || !Utc(until) || until <= now ||
                until > c.UntilUtc || until > e.SubjectMembershipUntilUtc ||
                own.GrantExpiresAtUtc is not { } ownUntil || until > ownUntil)
                return null;
        }

        return new C06DelegationDecision(i.Command, i.ExpectedGrantRevision, i.ExpectedGrantRevision + 1);
    }

    private static bool ValidScope(C06DelegationScope? s) =>
        s is not null && s.Tenant is not null && s.Branch is not null &&
        s.Resource is not null && s.Action is not null && s.Action.Value == ReadAction;

    private static bool Utc(DateTimeOffset time) => time != default && time.Offset == TimeSpan.Zero;
}
