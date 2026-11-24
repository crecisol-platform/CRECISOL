namespace Crecisol.Domain.Tenancy;

/// <summary>
/// Input shape for a server-only, locked C05 operational SQL transaction.
/// This is NOT an identity assertion from browser, cookie claims or JWT.
/// Construct only from the operational MySQL adapter after a verified host,
/// HMAC session, active account/membership and MFA checks.
/// </summary>
public sealed record C06LockedIdentityEvidence
{
    public required TenantId VerifiedHostTenant { get; init; }
    public required TenantId SessionTenant { get; init; }
    public required TenantId MembershipTenant { get; init; }
    public required TenantActorId SessionActor { get; init; }
    public required TenantActorId AccountActor { get; init; }
    public required TenantActorId MembershipActor { get; init; }
    public required TenantActorId FactorActor { get; init; }
    public required Guid SessionMembershipId { get; init; }
    public required Guid MembershipId { get; init; }
    public required Guid SessionFactorId { get; init; }
    public required Guid FactorId { get; init; }
    public required long SessionRevision { get; init; }
    public required long AccountRevisionInSession { get; init; }
    public required long CurrentAccountRevision { get; init; }
    public required long MembershipRevisionInSession { get; init; }
    public required long CurrentMembershipRevision { get; init; }
    public required long FactorRevision { get; init; }
    public required long VerifiedHostRevision { get; init; }
    public required long TenantCatalogRevision { get; init; }
    public required TenantAddressMode VerifiedHostAddressMode { get; init; }
    public required TenantAddressMode TenantCatalogAddressMode { get; init; }
    public required bool SessionNotRevoked { get; init; }
    public required bool AccountActive { get; init; }
    public required bool AccountMailboxVerified { get; init; }
    public required bool MembershipActive { get; init; }
    public required bool FactorActive { get; init; }
    public required DateTimeOffset SessionIssuedAtUtc { get; init; }
    public required DateTimeOffset SessionLastSeenAtUtc { get; init; }
    public required DateTimeOffset SessionExpiresAtUtc { get; init; }
    public required DateTimeOffset MfaVerifiedAtUtc { get; init; }
    public required DateTimeOffset MembershipGrantedAtUtc { get; init; }
    public required DateTimeOffset MembershipExpiresAtUtc { get; init; }
    public required DateTimeOffset ServerNowUtc { get; init; }
    public required int ServerIdleMinutes { get; init; }

    public override string ToString() => "C06LockedIdentityEvidence[redacted;server_only]";
}

/// <summary>
/// An immutable tenant/actor/membership binding revalidated inside the same
/// backend transaction as a protected C05 operation. This is IDENTITY CONTEXT,
/// never an authorization to access a private resource or a reusable token.
/// C06-04 must independently check action, branch, owner and resource state.
/// </summary>
public sealed record C06OperationalMembershipContext
{
    private C06OperationalMembershipContext(
        TenantId tenantId, TenantActorId actorId, Guid membershipId,
        long accountRevision, long membershipRevision, long sessionRevision,
        long hostRevision, long catalogRevision, DateTimeOffset verifiedAtUtc)
    {
        TenantId = tenantId;
        ActorId = actorId;
        MembershipId = membershipId;
        AccountRevision = accountRevision;
        MembershipRevision = membershipRevision;
        SessionRevision = sessionRevision;
        HostRevision = hostRevision;
        TenantCatalogRevision = catalogRevision;
        VerifiedAtUtc = verifiedAtUtc;
    }

    public TenantId TenantId { get; }
    public TenantActorId ActorId { get; }
    public Guid MembershipId { get; }
    public long AccountRevision { get; }
    public long MembershipRevision { get; }
    public long SessionRevision { get; }
    public long HostRevision { get; }
    public long TenantCatalogRevision { get; }
    public DateTimeOffset VerifiedAtUtc { get; }

    /// <summary>Identity evidence alone never grants read, write, export,
    /// billing, platform administration, branch or private resource access.</summary>
    public bool AuthorizesPrivateResource => false;

    public static C06OperationalMembershipContext VerifyLockedServerEvidence(
        C06LockedIdentityEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        if (evidence.VerifiedHostTenant is null || evidence.SessionTenant is null ||
            evidence.MembershipTenant is null || evidence.SessionActor is null ||
            evidence.AccountActor is null || evidence.MembershipActor is null ||
            evidence.FactorActor is null)
            throw new UnauthorizedAccessException("Verified actor and tenant required.");

        if (evidence.VerifiedHostTenant != evidence.SessionTenant ||
            evidence.VerifiedHostTenant != evidence.MembershipTenant ||
            evidence.SessionActor != evidence.AccountActor ||
            evidence.SessionActor != evidence.MembershipActor ||
            evidence.SessionActor != evidence.FactorActor ||
            evidence.SessionMembershipId == Guid.Empty ||
            evidence.MembershipId == Guid.Empty ||
            evidence.SessionMembershipId != evidence.MembershipId ||
            evidence.SessionFactorId == Guid.Empty ||
            evidence.FactorId == Guid.Empty ||
            evidence.SessionFactorId != evidence.FactorId)
            throw new UnauthorizedAccessException("Account, session, MFA and membership scope mismatch.");

        if (evidence.SessionRevision <= 0 ||
            evidence.AccountRevisionInSession <= 0 ||
            evidence.CurrentAccountRevision <= 0 ||
            evidence.MembershipRevisionInSession <= 0 ||
            evidence.CurrentMembershipRevision <= 0 ||
            evidence.FactorRevision <= 0 ||
            evidence.VerifiedHostRevision <= 0 ||
            evidence.TenantCatalogRevision <= 0 ||
            evidence.AccountRevisionInSession != evidence.CurrentAccountRevision ||
            evidence.MembershipRevisionInSession != evidence.CurrentMembershipRevision)
            throw new UnauthorizedAccessException("Session or membership revision invalid.");

        if (evidence.VerifiedHostAddressMode is not
                (TenantAddressMode.Subdomain or TenantAddressMode.CustomDomain) ||
            evidence.VerifiedHostAddressMode != evidence.TenantCatalogAddressMode)
            throw new UnauthorizedAccessException("Tenant host and catalog address modes conflict.");

        if (!evidence.SessionNotRevoked || !evidence.AccountActive ||
            !evidence.AccountMailboxVerified || !evidence.MembershipActive ||
            !evidence.FactorActive)
            throw new UnauthorizedAccessException("Session, actor or membership is inactive.");

        if (evidence.ServerIdleMinutes is < 5 or > 120)
            throw new UnauthorizedAccessException("Invalid server inactivity policy.");

        foreach (var time in new[] {
            evidence.SessionIssuedAtUtc, evidence.SessionLastSeenAtUtc,
            evidence.SessionExpiresAtUtc, evidence.MfaVerifiedAtUtc,
            evidence.MembershipGrantedAtUtc, evidence.MembershipExpiresAtUtc,
            evidence.ServerNowUtc
        })
            if (time == default || time.Offset != TimeSpan.Zero)
                throw new UnauthorizedAccessException("Verified identity timestamps must be UTC.");

        if (evidence.SessionIssuedAtUtc > evidence.ServerNowUtc ||
            evidence.SessionLastSeenAtUtc < evidence.SessionIssuedAtUtc ||
            evidence.SessionLastSeenAtUtc > evidence.ServerNowUtc ||
            evidence.MfaVerifiedAtUtc > evidence.ServerNowUtc ||
            evidence.MembershipGrantedAtUtc > evidence.ServerNowUtc ||
            evidence.ServerNowUtc >= evidence.SessionExpiresAtUtc ||
            evidence.ServerNowUtc >= evidence.MembershipExpiresAtUtc ||
            evidence.ServerNowUtc - evidence.SessionLastSeenAtUtc >=
                TimeSpan.FromMinutes(evidence.ServerIdleMinutes))
            throw new UnauthorizedAccessException("Verified membership or session has expired.");

        return new C06OperationalMembershipContext(
            evidence.VerifiedHostTenant, evidence.SessionActor,
            evidence.MembershipId, evidence.CurrentAccountRevision,
            evidence.CurrentMembershipRevision, evidence.SessionRevision,
            evidence.VerifiedHostRevision, evidence.TenantCatalogRevision,
            evidence.ServerNowUtc);
    }

    public override string ToString() => "C06OperationalMembershipContext[redacted;not_authorization]";
}
