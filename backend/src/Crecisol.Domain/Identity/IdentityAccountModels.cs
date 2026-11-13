using Crecisol.Domain.Tenancy;

namespace Crecisol.Domain.Identity;

// C05-01 contains INTERNAL account and verifier METADATA only. Nothing here
// validates a password, creates a login session or grants tenant permissions.

public sealed record AccountCredentialId
{
    private AccountCredentialId(Guid value) => Value = value;
    public Guid Value { get; }

    public static AccountCredentialId From(Guid value) =>
        value == Guid.Empty ? throw new InvalidOperationException("Credential identifier required.")
            : new AccountCredentialId(value);

    public override string ToString() => "AccountCredentialId[redacted]";
}

public sealed record AccountTenantLinkId
{
    private AccountTenantLinkId(Guid value) => Value = value;
    public Guid Value { get; }

    public static AccountTenantLinkId From(Guid value) =>
        value == Guid.Empty ? throw new InvalidOperationException("Membership identifier required.")
            : new AccountTenantLinkId(value);

    public override string ToString() => "AccountTenantLinkId[redacted]";
}

public enum IdentityAccountState
{
    PendingVerification = 1,
    Active = 2,
    Suspended = 3,
    Disabled = 4
}

public enum AccountCredentialKind
{
    PasswordVerifier = 1,
    PasskeyPublicKey = 2,
    TotpVerifier = 3,
    BackupCodeVerifier = 4
}

public enum AccountCredentialState
{
    PendingEnrollment = 1,
    Active = 2,
    Revoked = 3
}

/// <summary>
/// Profile category ONLY: Owner, Employee and Consumer grant no role/action.
/// Platform operators have a separate realm and never inherit tenant grants.
/// </summary>
public enum AccountTenantProfileKind
{
    Owner = 1,
    Employee = 2,
    Consumer = 3
}

public enum PlatformOperatorState
{
    Pending = 1,
    Active = 2,
    Suspended = 3,
    Revoked = 4
}

internal static class IdentityModelValidation
{
    public static void RequireUtc(DateTimeOffset value)
    {
        if (value == default || value.Offset != TimeSpan.Zero)
            throw new InvalidOperationException("Identity timestamp must be UTC.");
    }

    public static void RequireRevision(long value)
    {
        if (value < 1)
            throw new InvalidOperationException("Identity revision must be positive.");
    }

    public static void RequireNow(DateTimeOffset nowUtc) => RequireUtc(nowUtc);
}

/// <summary>
/// Global actor metadata, independent of tenants, profiles, email and grants.
/// This is NOT a signed authentication assertion or validated session.
/// </summary>
public sealed record IdentityAccountSnapshot
{
    private IdentityAccountSnapshot(TenantActorId actorId, IdentityAccountState state,
        long revision, DateTimeOffset changedAtUtc)
    {
        ActorId = actorId;
        State = state;
        Revision = revision;
        ChangedAtUtc = changedAtUtc;
    }

    public TenantActorId ActorId { get; }
    public IdentityAccountState State { get; }
    public long Revision { get; }
    public DateTimeOffset ChangedAtUtc { get; }

    public static IdentityAccountSnapshot RestoreFromTrustedIdentityDirectory(
        TenantActorId actorId, IdentityAccountState state, long revision,
        DateTimeOffset changedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(actorId);
        if (!Enum.IsDefined(state))
            throw new InvalidOperationException("Unknown identity account state.");
        IdentityModelValidation.RequireRevision(revision);
        IdentityModelValidation.RequireUtc(changedAtUtc);
        return new IdentityAccountSnapshot(actorId, state, revision, changedAtUtc);
    }

    public bool IsActiveAtUtc(DateTimeOffset nowUtc)
    {
        IdentityModelValidation.RequireNow(nowUtc);
        return State == IdentityAccountState.Active && ChangedAtUtc <= nowUtc;
    }

    public override string ToString() => "IdentityAccountSnapshot[redacted]";
}

/// <summary>
/// A non-secret description of a credential. No password hash, token, TOTP
/// seed, recovery code, signed assertion, or credential payload can be stored.
/// Active metadata is NOT cryptographic verification or login evidence.
/// </summary>
public sealed record AccountCredentialMetadata
{
    private AccountCredentialMetadata(
        AccountCredentialId credentialId, TenantActorId actorId,
        AccountCredentialKind kind, AccountCredentialState state,
        long revision, DateTimeOffset changedAtUtc)
    {
        CredentialId = credentialId;
        ActorId = actorId;
        Kind = kind;
        State = state;
        Revision = revision;
        ChangedAtUtc = changedAtUtc;
    }

    public AccountCredentialId CredentialId { get; }
    public TenantActorId ActorId { get; }
    public AccountCredentialKind Kind { get; }
    public AccountCredentialState State { get; }
    public long Revision { get; }
    public DateTimeOffset ChangedAtUtc { get; }

    public static AccountCredentialMetadata RestoreFromTrustedCredentialDirectory(
        AccountCredentialId credentialId, TenantActorId actorId,
        AccountCredentialKind kind, AccountCredentialState state,
        long revision, DateTimeOffset changedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(credentialId);
        ArgumentNullException.ThrowIfNull(actorId);
        if (!Enum.IsDefined(kind) || !Enum.IsDefined(state))
            throw new InvalidOperationException("Unknown credential metadata type or state.");
        IdentityModelValidation.RequireRevision(revision);
        IdentityModelValidation.RequireUtc(changedAtUtc);
        return new AccountCredentialMetadata(
            credentialId, actorId, kind, state, revision, changedAtUtc);
    }

    public override string ToString() => "AccountCredentialMetadata[redacted]";
}

/// <summary>
/// Independent, tenant-bound and revocable identity linkage. The profile kind
/// conveys display context, NEVER owner/branch/resource/action authorization.
/// Its revision must agree with the C04-05 trusted membership snapshot.
/// </summary>
public sealed record AccountTenantLinkSnapshot
{
    private AccountTenantLinkSnapshot(
        AccountTenantLinkId linkId, TenantActorId actorId, TenantId tenantId,
        AccountTenantProfileKind profileKind, TenantMembershipState state,
        long revision, DateTimeOffset changedAtUtc,
        DateTimeOffset? grantedAtUtc, DateTimeOffset? expiresAtUtc)
    {
        LinkId = linkId;
        ActorId = actorId;
        TenantId = tenantId;
        ProfileKind = profileKind;
        State = state;
        Revision = revision;
        ChangedAtUtc = changedAtUtc;
        GrantedAtUtc = grantedAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    public AccountTenantLinkId LinkId { get; }
    public TenantActorId ActorId { get; }
    public TenantId TenantId { get; }
    public AccountTenantProfileKind ProfileKind { get; }
    public TenantMembershipState State { get; }
    public long Revision { get; }
    public DateTimeOffset ChangedAtUtc { get; }
    public DateTimeOffset? GrantedAtUtc { get; }
    public DateTimeOffset? ExpiresAtUtc { get; }

    public static AccountTenantLinkSnapshot RestoreFromTrustedMembershipDirectory(
        AccountTenantLinkId linkId, TenantActorId actorId, TenantId tenantId,
        AccountTenantProfileKind profileKind, TenantMembershipState state,
        long revision, DateTimeOffset changedAtUtc,
        DateTimeOffset? grantedAtUtc, DateTimeOffset? expiresAtUtc)
    {
        ArgumentNullException.ThrowIfNull(linkId);
        ArgumentNullException.ThrowIfNull(actorId);
        ArgumentNullException.ThrowIfNull(tenantId);
        if (!Enum.IsDefined(profileKind) || !Enum.IsDefined(state))
            throw new InvalidOperationException("Unknown profile or membership state.");
        IdentityModelValidation.RequireRevision(revision);
        IdentityModelValidation.RequireUtc(changedAtUtc);

        if (grantedAtUtc.HasValue != expiresAtUtc.HasValue)
            throw new InvalidOperationException("Membership dates must be paired.");
        if (grantedAtUtc.HasValue)
        {
            IdentityModelValidation.RequireUtc(grantedAtUtc.Value);
            IdentityModelValidation.RequireUtc(expiresAtUtc!.Value);
            if (grantedAtUtc.Value > changedAtUtc ||
                expiresAtUtc.Value <= grantedAtUtc.Value)
                throw new InvalidOperationException("Invalid membership activation interval.");
        }
        if (state == TenantMembershipState.Active && !grantedAtUtc.HasValue)
            throw new InvalidOperationException("An active membership requires an activation interval.");
        if (state == TenantMembershipState.Pending && grantedAtUtc.HasValue)
            throw new InvalidOperationException("A pending membership cannot have a grant.");

        return new AccountTenantLinkSnapshot(
            linkId, actorId, tenantId, profileKind, state, revision,
            changedAtUtc, grantedAtUtc, expiresAtUtc);
    }

    public bool IsActiveAtUtc(DateTimeOffset nowUtc)
    {
        IdentityModelValidation.RequireNow(nowUtc);
        return State == TenantMembershipState.Active &&
            GrantedAtUtc.HasValue && ExpiresAtUtc.HasValue &&
            ChangedAtUtc <= nowUtc && GrantedAtUtc.Value <= nowUtc &&
            nowUtc < ExpiresAtUtc.Value;
    }

    public override string ToString() => "AccountTenantLinkSnapshot[redacted]";
}

/// <summary>
/// A separate platform operator DIRECTORY record, not a SuperAdmin bypass,
/// not a tenant membership, and not private-data authorization. Privileged
/// operations still require future MFA, step-up, reason, expiry and audit.
/// </summary>
public sealed record PlatformOperatorDirectoryEntry
{
    private PlatformOperatorDirectoryEntry(
        TenantActorId actorId, PlatformOperatorState state,
        long revision, DateTimeOffset changedAtUtc)
    {
        ActorId = actorId;
        State = state;
        Revision = revision;
        ChangedAtUtc = changedAtUtc;
    }

    public TenantActorId ActorId { get; }
    public PlatformOperatorState State { get; }
    public long Revision { get; }
    public DateTimeOffset ChangedAtUtc { get; }

    public static PlatformOperatorDirectoryEntry RestoreFromTrustedPlatformDirectory(
        TenantActorId actorId, PlatformOperatorState state,
        long revision, DateTimeOffset changedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(actorId);
        if (!Enum.IsDefined(state))
            throw new InvalidOperationException("Unknown platform operator state.");
        IdentityModelValidation.RequireRevision(revision);
        IdentityModelValidation.RequireUtc(changedAtUtc);
        return new PlatformOperatorDirectoryEntry(actorId, state, revision, changedAtUtc);
    }

    public override string ToString() => "PlatformOperatorDirectoryEntry[redacted]";
}

/// <summary>
/// INTERNAL consistency evidence only; never a bearer, role or resource grant.
/// A future operation must separately revalidate C04 branch/resource policy,
/// session cryptography, account revocation and data-store transaction.
/// </summary>
public sealed record AccountTenantBindingEvidence
{
    private AccountTenantBindingEvidence(
        TenantActorId actorId, TenantId tenantId, AccountTenantLinkId linkId,
        AccountTenantProfileKind profileKind, long accountRevision,
        long linkRevision, long sessionRevision, long hostRevision,
        long tenantCatalogRevision)
    {
        ActorId = actorId;
        TenantId = tenantId;
        LinkId = linkId;
        ProfileKind = profileKind;
        AccountRevision = accountRevision;
        LinkRevision = linkRevision;
        SessionRevision = sessionRevision;
        HostRevision = hostRevision;
        TenantCatalogRevision = tenantCatalogRevision;
    }

    public TenantActorId ActorId { get; }
    public TenantId TenantId { get; }
    public AccountTenantLinkId LinkId { get; }
    public AccountTenantProfileKind ProfileKind { get; }
    public long AccountRevision { get; }
    public long LinkRevision { get; }
    public long SessionRevision { get; }
    public long HostRevision { get; }
    public long TenantCatalogRevision { get; }

    public static AccountTenantBindingEvidence FromTrustedInternalSnapshots(
        TenantIdentityContextEvidence context, IdentityAccountSnapshot account,
        AccountTenantLinkSnapshot link, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(link);
        IdentityModelValidation.RequireNow(nowUtc);
        if (account.ActorId != context.ActorId ||
            link.ActorId != context.ActorId ||
            link.TenantId != context.TenantId ||
            link.Revision != context.MembershipRevision ||
            !account.IsActiveAtUtc(nowUtc) ||
            !link.IsActiveAtUtc(nowUtc))
            throw new UnauthorizedAccessException("Account-to-tenant evidence is not current.");

        return new AccountTenantBindingEvidence(
            context.ActorId, context.TenantId, link.LinkId, link.ProfileKind,
            account.Revision, link.Revision, context.SessionRevision,
            context.HostRevision, context.TenantCatalogRevision);
    }

    public override string ToString() => "AccountTenantBindingEvidence[redacted]";
}
