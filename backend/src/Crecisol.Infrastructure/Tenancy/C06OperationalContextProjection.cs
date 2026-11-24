using Crecisol.Domain.Tenancy;

namespace Crecisol.Infrastructure.Identity;

/// <summary>
/// C06-03 projection from a C05 transaction's FOR SHARE/FOR UPDATE rows.
/// No browser parameter, claimed tenant, role label or JWT can enter here.
/// This partial type is used only by the existing local operational path.
/// </summary>
public sealed partial class MySqlOperationalIdentityFlow
{
    private static C06OperationalMembershipContext ResolveC06TenantContextFromLockedRows(
        Dictionary<string, object> verifiedHost,
        byte[] verifiedTenant,
        Dictionary<string, object> account,
        Dictionary<string, object> membership,
        Dictionary<string, object> session,
        Dictionary<string, object> factor,
        int trustedIdleMinutes, DateTimeOffset trustedDatabaseUtc)
    {
        ArgumentNullException.ThrowIfNull(verifiedHost);
        ArgumentNullException.ThrowIfNull(verifiedTenant);
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(membership);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(factor);

        static Guid Id(byte[] bytes) =>
            bytes.Length == 16 ? new Guid(bytes, bigEndian: true) :
                throw new UnauthorizedAccessException("Untrusted identity binary key.");
        static TenantId Tenant(byte[] bytes) => TenantId.From(Id(bytes));
        static TenantActorId Actor(byte[] bytes) => TenantActorId.From(Id(bytes));

        if (!Bytes(verifiedHost, "tenant_id").AsSpan().SequenceEqual(verifiedTenant))
            throw new UnauthorizedAccessException("Verified host tenant is inconsistent.");

        var evidence = new C06LockedIdentityEvidence
        {
            VerifiedHostTenant = Tenant(verifiedTenant),
            SessionTenant = Tenant(Bytes(session, "tenant_id")),
            MembershipTenant = Tenant(Bytes(membership, "tenant_id")),
            SessionActor = Actor(Bytes(session, "account_id")),
            AccountActor = Actor(Bytes(account, "account_id")),
            MembershipActor = Actor(Bytes(membership, "account_id")),
            FactorActor = Actor(Bytes(factor, "account_id")),
            SessionMembershipId = Id(Bytes(session, "membership_id")),
            MembershipId = Id(Bytes(membership, "membership_id")),
            SessionFactorId = Id(Bytes(session, "mfa_factor_id")),
            FactorId = Id(Bytes(factor, "factor_id")),
            SessionRevision = Number(session, "session_revision"),
            AccountRevisionInSession = Number(session, "account_revision"),
            CurrentAccountRevision = Number(account, "security_revision"),
            MembershipRevisionInSession = Number(session, "membership_revision"),
            CurrentMembershipRevision = Number(membership, "approval_revision"),
            FactorRevision = Number(factor, "factor_revision"),
            VerifiedHostRevision = Number(verifiedHost, "host_revision"),
            TenantCatalogRevision = Number(verifiedHost, "catalog_revision"),
            VerifiedHostAddressMode =
                (TenantAddressMode)checked((int)Number(verifiedHost, "address_mode")),
            TenantCatalogAddressMode =
                (TenantAddressMode)checked((int)Number(verifiedHost, "catalog_address_mode")),
            SessionNotRevoked = session["revoked_at_utc"] is DBNull,
            AccountActive = Number(account, "account_state") == 1,
            AccountMailboxVerified = account["mailbox_verified_at_utc"] is not DBNull,
            MembershipActive = Number(membership, "membership_state") == 1,
            FactorActive = Number(factor, "factor_state") == 1 &&
                factor["revoked_at_utc"] is DBNull,
            SessionIssuedAtUtc = Utc(session, "issued_at_utc"),
            SessionLastSeenAtUtc = Utc(session, "last_seen_at_utc"),
            SessionExpiresAtUtc = Utc(session, "expires_at_utc"),
            MfaVerifiedAtUtc = Utc(session, "mfa_verified_at_utc"),
            MembershipGrantedAtUtc = Utc(membership, "granted_at_utc"),
            MembershipExpiresAtUtc = Utc(membership, "expires_at_utc"),
            ServerNowUtc = trustedDatabaseUtc,
            ServerIdleMinutes = trustedIdleMinutes
        };
        return C06OperationalMembershipContext.VerifyLockedServerEvidence(evidence);
    }
}
