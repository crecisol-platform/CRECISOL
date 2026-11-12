namespace Crecisol.Domain.Tenancy;

/// <summary>
/// Opaque actor key. Possessing a GUID, including one in a JWT claim,
/// does NOT prove the user is authenticated or a member of any tenant.
/// </summary>
public sealed record TenantActorId
{
    private TenantActorId(Guid value) => Value = value;

    public Guid Value { get; }

    public static TenantActorId From(Guid value)
    {
        if (value == Guid.Empty)
            throw new InvalidOperationException("An actor identifier is required.");
        return new TenantActorId(value);
    }

    public override string ToString() => "TenantActorId[redacted]";
}

public enum ActorSessionState
{
    Active = 1,
    Suspended = 2,
    Revoked = 3
}

/// <summary>
/// Immutable snapshot from a future trusted backend identity adapter after
/// signature, issuer, audience, session revocation and account checks.
/// C04-05 provides ONLY the contract, not that cryptographic validation.
/// </summary>
public sealed record TrustedActorSessionSnapshot
{
    private TrustedActorSessionSnapshot(
        TenantActorId actorId,
        ActorSessionState state,
        long revision,
        DateTimeOffset authenticatedAtUtc,
        DateTimeOffset lastValidatedAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        ActorId = actorId;
        State = state;
        Revision = revision;
        AuthenticatedAtUtc = authenticatedAtUtc;
        LastValidatedAtUtc = lastValidatedAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    public TenantActorId ActorId { get; }
    public ActorSessionState State { get; }
    public long Revision { get; }
    public DateTimeOffset AuthenticatedAtUtc { get; }
    public DateTimeOffset LastValidatedAtUtc { get; }
    public DateTimeOffset ExpiresAtUtc { get; }

    public static TrustedActorSessionSnapshot RestoreFromTrustedIdentityProvider(
        TenantActorId actorId,
        ActorSessionState state,
        long revision,
        DateTimeOffset authenticatedAtUtc,
        DateTimeOffset lastValidatedAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        ArgumentNullException.ThrowIfNull(actorId);
        if (!Enum.IsDefined(state) || revision <= 0)
            throw new InvalidOperationException("Invalid session lifecycle or revision.");
        RequireUtc(authenticatedAtUtc);
        RequireUtc(lastValidatedAtUtc);
        RequireUtc(expiresAtUtc);
        if (authenticatedAtUtc > lastValidatedAtUtc ||
            lastValidatedAtUtc >= expiresAtUtc)
            throw new InvalidOperationException("Invalid session verification window.");
        return new TrustedActorSessionSnapshot(
            actorId, state, revision, authenticatedAtUtc, lastValidatedAtUtc, expiresAtUtc);
    }

    public bool IsActiveAtUtc(DateTimeOffset nowUtc)
    {
        RequireUtc(nowUtc);
        return State == ActorSessionState.Active &&
            AuthenticatedAtUtc <= nowUtc &&
            LastValidatedAtUtc <= nowUtc &&
            nowUtc < ExpiresAtUtc;
    }

    internal static void RequireUtc(DateTimeOffset value)
    {
        if (value == default || value.Offset != TimeSpan.Zero)
            throw new InvalidOperationException("Trusted identity timestamps must be UTC.");
    }

    public override string ToString() => "TrustedActorSessionSnapshot[redacted]";
}

public enum TenantMembershipState
{
    Pending = 1,
    Active = 2,
    Suspended = 3,
    Revoked = 4
}

/// <summary>
/// An internal actor-to-tenant membership snapshot. This does not carry roles,
/// branch/resource rights, any frontend permissions or a database selector.
/// </summary>
public sealed record TenantActorMembershipSnapshot
{
    private TenantActorMembershipSnapshot(
        TenantActorId actorId,
        TenantId tenantId,
        TenantMembershipState state,
        long revision,
        DateTimeOffset changedAtUtc,
        DateTimeOffset? grantedAtUtc,
        DateTimeOffset? validUntilUtc)
    {
        ActorId = actorId;
        TenantId = tenantId;
        State = state;
        Revision = revision;
        ChangedAtUtc = changedAtUtc;
        GrantedAtUtc = grantedAtUtc;
        ValidUntilUtc = validUntilUtc;
    }

    public TenantActorId ActorId { get; }
    public TenantId TenantId { get; }
    public TenantMembershipState State { get; }
    public long Revision { get; }
    public DateTimeOffset ChangedAtUtc { get; }
    public DateTimeOffset? GrantedAtUtc { get; }
    public DateTimeOffset? ValidUntilUtc { get; }

    public static TenantActorMembershipSnapshot RestoreFromTrustedMembershipCatalog(
        TenantActorId actorId,
        TenantId tenantId,
        TenantMembershipState state,
        long revision,
        DateTimeOffset changedAtUtc,
        DateTimeOffset? grantedAtUtc,
        DateTimeOffset? validUntilUtc)
    {
        ArgumentNullException.ThrowIfNull(actorId);
        ArgumentNullException.ThrowIfNull(tenantId);
        if (!Enum.IsDefined(state) || revision <= 0)
            throw new InvalidOperationException("Invalid membership lifecycle or revision.");
        TrustedActorSessionSnapshot.RequireUtc(changedAtUtc);
        if (grantedAtUtc.HasValue != validUntilUtc.HasValue)
            throw new InvalidOperationException("Membership window must be complete.");
        if (grantedAtUtc.HasValue)
        {
            TrustedActorSessionSnapshot.RequireUtc(grantedAtUtc.Value);
            TrustedActorSessionSnapshot.RequireUtc(validUntilUtc!.Value);
            if (grantedAtUtc.Value > changedAtUtc ||
                validUntilUtc.Value <= grantedAtUtc.Value)
                throw new InvalidOperationException("Invalid membership activation window.");
        }
        if (state == TenantMembershipState.Active && !grantedAtUtc.HasValue)
            throw new InvalidOperationException("Active membership requires verified dates.");
        if (state == TenantMembershipState.Pending && grantedAtUtc.HasValue)
            throw new InvalidOperationException("Pending membership cannot claim active grants.");

        return new TenantActorMembershipSnapshot(
            actorId, tenantId, state, revision, changedAtUtc, grantedAtUtc, validUntilUtc);
    }

    public bool IsActiveAtUtc(DateTimeOffset nowUtc)
    {
        TrustedActorSessionSnapshot.RequireUtc(nowUtc);
        return State == TenantMembershipState.Active &&
            GrantedAtUtc.HasValue && ValidUntilUtc.HasValue &&
            ChangedAtUtc <= nowUtc && GrantedAtUtc.Value <= nowUtc &&
            nowUtc < ValidUntilUtc.Value;
    }

    public override string ToString() => "TenantActorMembershipSnapshot[redacted]";
}

/// <summary>
/// NON-AUTHORIZING internal evidence for downstream C04-06 scope checks.
/// Never treat this as a bearer credential or as permission to access
/// branches, resources, tenant data, admin console, storage or billing.
/// </summary>
public sealed record TenantIdentityContextEvidence
{
    private TenantIdentityContextEvidence(
        TenantActorId actorId,
        TenantId tenantId,
        long sessionRevision,
        long membershipRevision,
        long hostRevision,
        long tenantCatalogRevision)
    {
        ActorId = actorId;
        TenantId = tenantId;
        SessionRevision = sessionRevision;
        MembershipRevision = membershipRevision;
        HostRevision = hostRevision;
        TenantCatalogRevision = tenantCatalogRevision;
    }

    public TenantActorId ActorId { get; }
    public TenantId TenantId { get; }
    public long SessionRevision { get; }
    public long MembershipRevision { get; }
    public long HostRevision { get; }
    public long TenantCatalogRevision { get; }

    public static TenantIdentityContextEvidence FromVerifiedInternalEvidence(
        TrustedActorSessionSnapshot session,
        TenantActorMembershipSnapshot membership,
        VerifiedTenantHostHint host,
        TenantCatalogSnapshot catalog)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(membership);
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(catalog);
        if (session.State != ActorSessionState.Active ||
            membership.State != TenantMembershipState.Active ||
            catalog.State != TenantLifecycleState.Active ||
            session.ActorId != membership.ActorId ||
            host.TenantId != membership.TenantId ||
            catalog.TenantId != host.TenantId ||
            catalog.AddressMode != host.AddressMode ||
            session.Revision <= 0 || membership.Revision <= 0 ||
            host.Revision <= 0 || catalog.Revision <= 0)
            throw new InvalidOperationException("Inconsistent internal identity evidence.");
        return new TenantIdentityContextEvidence(
            session.ActorId, host.TenantId, session.Revision, membership.Revision,
            host.Revision, catalog.Revision);
    }

    public override string ToString() => "TenantIdentityContextEvidence[redacted]";
}
