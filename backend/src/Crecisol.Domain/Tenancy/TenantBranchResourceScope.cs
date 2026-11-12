namespace Crecisol.Domain.Tenancy;

/// <summary>Opaque branch identifier. It cannot grant tenant or branch access.</summary>
public sealed record TenantBranchId
{
    private TenantBranchId(Guid value) => Value = value;
    public Guid Value { get; }
    public static TenantBranchId From(Guid value) =>
        value != Guid.Empty ? new TenantBranchId(value)
            : throw new InvalidOperationException("Branch identifier is required.");
    public override string ToString() => "TenantBranchId[redacted]";
}

/// <summary>A resource ID is only an untrusted lookup selector, not permission.</summary>
public sealed record TenantResourceId
{
    private TenantResourceId(Guid value) => Value = value;
    public Guid Value { get; }
    public static TenantResourceId From(Guid value) =>
        value != Guid.Empty ? new TenantResourceId(value)
            : throw new InvalidOperationException("Resource identifier is required.");
    public override string ToString() => "TenantResourceId[redacted]";
}

/// <summary>
/// Canonical parametrizable action key, e.g. cases.read or cases.item.update.
/// Construct ONLY from a backend-owned route/policy. Never use request data to
/// determine the required action. This key by itself grants no permission.
/// </summary>
public sealed record TenantRequiredAction
{
    private TenantRequiredAction(string value) => Value = value;
    public string Value { get; }

    public static TenantRequiredAction FromServerPolicy(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Length is < 3 or > 120 ||
            !string.Equals(raw, raw.Trim(), StringComparison.Ordinal))
            throw new InvalidOperationException("Invalid required action key.");

        var parts = raw.Split('.');
        if (parts.Length is < 2 or > 4 || parts.Any(part =>
                part.Length is < 1 or > 32 ||
                part[0] is < 'a' or > 'z' ||
                part[^1] == '-' ||
                !part.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '-')))
            throw new InvalidOperationException("Invalid required action labels.");
        return new TenantRequiredAction(raw);
    }

    public override string ToString() => "TenantRequiredAction[redacted]";
}

public enum TrustedResourceState
{
    Active = 1,
    Suspended = 2,
    Deleted = 3
}

public enum TrustedBranchState
{
    Active = 1,
    Suspended = 2,
    Deactivated = 3
}

/// <summary>
/// Resource ownership + branch metadata from a future tenant-scoped SERVER
/// catalog. Does not contain or expose the private resource content.
/// </summary>
public sealed record TrustedBranchResourceSnapshot
{
    private TrustedBranchResourceSnapshot(
        TenantId tenantId, TenantBranchId branchId, TenantResourceId resourceId,
        TenantActorId ownerActorId, TrustedBranchState branchState,
        long branchRevision, DateTimeOffset branchUpdatedAtUtc,
        TrustedResourceState state, long revision, DateTimeOffset updatedAtUtc)
    {
        TenantId = tenantId;
        BranchId = branchId;
        ResourceId = resourceId;
        OwnerActorId = ownerActorId;
        BranchState = branchState;
        BranchRevision = branchRevision;
        BranchUpdatedAtUtc = branchUpdatedAtUtc;
        State = state;
        Revision = revision;
        UpdatedAtUtc = updatedAtUtc;
    }

    public TenantId TenantId { get; }
    public TenantBranchId BranchId { get; }
    public TenantResourceId ResourceId { get; }
    public TenantActorId OwnerActorId { get; }
    public TrustedBranchState BranchState { get; }
    public long BranchRevision { get; }
    public DateTimeOffset BranchUpdatedAtUtc { get; }
    public TrustedResourceState State { get; }
    public long Revision { get; }
    public DateTimeOffset UpdatedAtUtc { get; }

    public static TrustedBranchResourceSnapshot RestoreFromTrustedResourceReader(
        TenantId tenantId, TenantBranchId branchId, TenantResourceId resourceId,
        TenantActorId ownerActorId, TrustedBranchState branchState,
        long branchRevision, DateTimeOffset branchUpdatedAtUtc,
        TrustedResourceState state, long revision, DateTimeOffset updatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(tenantId);
        ArgumentNullException.ThrowIfNull(branchId);
        ArgumentNullException.ThrowIfNull(resourceId);
        ArgumentNullException.ThrowIfNull(ownerActorId);
        if (!Enum.IsDefined(state) || revision <= 0 ||
            !Enum.IsDefined(branchState) || branchRevision <= 0)
            throw new InvalidOperationException("Invalid resource/branch lifecycle or revision.");
        RequireUtc(updatedAtUtc);
        RequireUtc(branchUpdatedAtUtc);
        return new TrustedBranchResourceSnapshot(
            tenantId, branchId, resourceId, ownerActorId, branchState,
            branchRevision, branchUpdatedAtUtc, state, revision, updatedAtUtc);
    }

    public bool IsActiveAtUtc(DateTimeOffset nowUtc)
    {
        RequireUtc(nowUtc);
        return State == TrustedResourceState.Active &&
            BranchState == TrustedBranchState.Active &&
            UpdatedAtUtc <= nowUtc && BranchUpdatedAtUtc <= nowUtc;
    }

    internal static void RequireUtc(DateTimeOffset value)
    {
        if (value == default || value.Offset != TimeSpan.Zero)
            throw new InvalidOperationException("Trusted scope clock must be UTC.");
    }

    public override string ToString() => "TrustedBranchResourceSnapshot[redacted]";
}

public enum BranchGrantState
{
    Pending = 1,
    Active = 2,
    Suspended = 3,
    Revoked = 4
}

public enum BranchGrantScope
{
    OwnerOnly = 1,
    BranchWide = 2
}

/// <summary>
/// One EXPLICIT grant for one actor, tenant, branch, required action, and owner
/// restriction. No inherited role, global admin or other branch is authorized.
/// This is trusted catalog metadata, not a client assertion.
/// </summary>
public sealed record TrustedActorBranchGrantSnapshot
{
    private TrustedActorBranchGrantSnapshot(
        TenantActorId actorId, TenantId tenantId, TenantBranchId branchId,
        TenantRequiredAction requiredAction, BranchGrantScope scope,
        BranchGrantState state, long revision, DateTimeOffset changedAtUtc,
        DateTimeOffset? grantedAtUtc, DateTimeOffset? validUntilUtc)
    {
        ActorId = actorId;
        TenantId = tenantId;
        BranchId = branchId;
        RequiredAction = requiredAction;
        Scope = scope;
        State = state;
        Revision = revision;
        ChangedAtUtc = changedAtUtc;
        GrantedAtUtc = grantedAtUtc;
        ValidUntilUtc = validUntilUtc;
    }

    public TenantActorId ActorId { get; }
    public TenantId TenantId { get; }
    public TenantBranchId BranchId { get; }
    public TenantRequiredAction RequiredAction { get; }
    public BranchGrantScope Scope { get; }
    public BranchGrantState State { get; }
    public long Revision { get; }
    public DateTimeOffset ChangedAtUtc { get; }
    public DateTimeOffset? GrantedAtUtc { get; }
    public DateTimeOffset? ValidUntilUtc { get; }

    public static TrustedActorBranchGrantSnapshot RestoreFromTrustedGrantReader(
        TenantActorId actorId, TenantId tenantId, TenantBranchId branchId,
        TenantRequiredAction requiredAction, BranchGrantScope scope,
        BranchGrantState state, long revision, DateTimeOffset changedAtUtc,
        DateTimeOffset? grantedAtUtc, DateTimeOffset? validUntilUtc)
    {
        ArgumentNullException.ThrowIfNull(actorId);
        ArgumentNullException.ThrowIfNull(tenantId);
        ArgumentNullException.ThrowIfNull(branchId);
        ArgumentNullException.ThrowIfNull(requiredAction);
        if (!Enum.IsDefined(scope) || !Enum.IsDefined(state) || revision <= 0)
            throw new InvalidOperationException("Invalid grant scope, lifecycle or revision.");
        TrustedBranchResourceSnapshot.RequireUtc(changedAtUtc);
        if (grantedAtUtc.HasValue != validUntilUtc.HasValue)
            throw new InvalidOperationException("Incomplete grant window.");
        if (grantedAtUtc.HasValue)
        {
            TrustedBranchResourceSnapshot.RequireUtc(grantedAtUtc.Value);
            TrustedBranchResourceSnapshot.RequireUtc(validUntilUtc!.Value);
            if (grantedAtUtc.Value > changedAtUtc ||
                validUntilUtc.Value <= grantedAtUtc.Value)
                throw new InvalidOperationException("Invalid grant time window.");
        }
        if (state == BranchGrantState.Active && !grantedAtUtc.HasValue)
            throw new InvalidOperationException("Active grant requires evidence and expiry.");
        if (state == BranchGrantState.Pending && grantedAtUtc.HasValue)
            throw new InvalidOperationException("Pending grant cannot claim verified access.");
        return new TrustedActorBranchGrantSnapshot(
            actorId, tenantId, branchId, requiredAction, scope, state, revision,
            changedAtUtc, grantedAtUtc, validUntilUtc);
    }

    public bool IsActiveAtUtc(DateTimeOffset nowUtc)
    {
        TrustedBranchResourceSnapshot.RequireUtc(nowUtc);
        return State == BranchGrantState.Active &&
            GrantedAtUtc.HasValue && ValidUntilUtc.HasValue &&
            ChangedAtUtc <= nowUtc && GrantedAtUtc.Value <= nowUtc &&
            nowUtc < ValidUntilUtc.Value;
    }

    public override string ToString() => "TrustedActorBranchGrantSnapshot[redacted]";
}

/// <summary>
/// A versioned INTERNAL SCOPE CHECK result, never a bearer credential, a
/// permission to open private content, or a storage-connection selector.
/// Every data operation must reauthorize atomically with its scoped query.
/// </summary>
public sealed record TenantBranchResourceScopeEvidence
{
    private TenantBranchResourceScopeEvidence(
        TenantActorId actorId, TenantId tenantId, TenantBranchId branchId,
        TenantResourceId resourceId, TenantRequiredAction requiredAction,
        BranchGrantScope scope, long sessionRevision, long membershipRevision,
        long hostRevision, long tenantCatalogRevision, long resourceRevision,
        long branchRevision, long branchGrantRevision)
    {
        ActorId = actorId;
        TenantId = tenantId;
        BranchId = branchId;
        ResourceId = resourceId;
        RequiredAction = requiredAction;
        Scope = scope;
        SessionRevision = sessionRevision;
        MembershipRevision = membershipRevision;
        HostRevision = hostRevision;
        TenantCatalogRevision = tenantCatalogRevision;
        ResourceRevision = resourceRevision;
        BranchRevision = branchRevision;
        BranchGrantRevision = branchGrantRevision;
    }

    public TenantActorId ActorId { get; }
    public TenantId TenantId { get; }
    public TenantBranchId BranchId { get; }
    public TenantResourceId ResourceId { get; }
    public TenantRequiredAction RequiredAction { get; }
    public BranchGrantScope Scope { get; }
    public long SessionRevision { get; }
    public long MembershipRevision { get; }
    public long HostRevision { get; }
    public long TenantCatalogRevision { get; }
    public long ResourceRevision { get; }
    public long BranchRevision { get; }
    public long BranchGrantRevision { get; }

    public static TenantBranchResourceScopeEvidence FromTrustedInternalSnapshots(
        TenantIdentityContextEvidence identity,
        TrustedBranchResourceSnapshot resource,
        TrustedActorBranchGrantSnapshot grant,
        TenantRequiredAction requiredAction,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentNullException.ThrowIfNull(requiredAction);
        TrustedBranchResourceSnapshot.RequireUtc(nowUtc);

        if (identity.ActorId != grant.ActorId ||
            identity.TenantId != resource.TenantId ||
            identity.TenantId != grant.TenantId ||
            resource.BranchId != grant.BranchId ||
            grant.RequiredAction != requiredAction ||
            !resource.IsActiveAtUtc(nowUtc) ||
            !grant.IsActiveAtUtc(nowUtc) ||
            (grant.Scope == BranchGrantScope.OwnerOnly &&
             resource.OwnerActorId != identity.ActorId) ||
            identity.SessionRevision <= 0 ||
            identity.MembershipRevision <= 0 ||
            identity.HostRevision <= 0 ||
            identity.TenantCatalogRevision <= 0 ||
            resource.Revision <= 0 || resource.BranchRevision <= 0 ||
            grant.Revision <= 0)
            throw new UnauthorizedAccessException("Resource scope evidence is not valid.");

        return new TenantBranchResourceScopeEvidence(
            identity.ActorId, identity.TenantId, resource.BranchId, resource.ResourceId,
            requiredAction, grant.Scope, identity.SessionRevision,
            identity.MembershipRevision, identity.HostRevision,
            identity.TenantCatalogRevision, resource.Revision,
            resource.BranchRevision, grant.Revision);
    }

    public override string ToString() => "TenantBranchResourceScopeEvidence[redacted]";
}
