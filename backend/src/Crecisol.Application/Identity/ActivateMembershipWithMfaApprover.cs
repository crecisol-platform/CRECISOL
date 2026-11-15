using Crecisol.Domain.Tenancy;

namespace Crecisol.Application.Identity;

/// <summary>
/// C05 isolated approval port. Database MUST derive the approver from a live
/// MFA-bound session digest, check a backend-granted memberships.approve action
/// and atomically activate only a verified PENDING tenant candidate.
/// A caller-provided actor, role, MFA boolean or tenant is never accepted.
/// </summary>
public interface ITrustedMfaMembershipApprovalStore
{
    Task<bool> TryActivateAsync(
        TenantId trustedTenant, long verifiedHostRevision,
        TenantActorId protectedCandidate, Guid backendApprovalId,
        Guid backendLinkId, SessionTokenDigest sessionDigest,
        CancellationToken cancellationToken);
}

/// <summary>
/// C05-P0: explicit, deny-by-default use case, NOT wired into product API.
/// No approval order, membership, or role is minted by this service directly;
/// the backend SQL procedure must recheck grant+MFA+session atomically.
/// </summary>
public sealed class ActivateMembershipWithMfaApprover
{
    private readonly ITrustedOpaqueSessionTokenProtector tokens;
    private readonly ITrustedMfaMembershipApprovalStore store;

    public ActivateMembershipWithMfaApprover(
        ITrustedOpaqueSessionTokenProtector tokens,
        ITrustedMfaMembershipApprovalStore store)
    {
        this.tokens=tokens??throw new ArgumentNullException(nameof(tokens));
        this.store=store??throw new ArgumentNullException(nameof(store));
    }

    public async Task<bool> ExecuteAsync(
        string? opaqueBearer, VerifiedTenantHostHint trustedHost,
        TenantActorId protectedCandidate, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(trustedHost);
        ArgumentNullException.ThrowIfNull(protectedCandidate);
        cancellationToken.ThrowIfCancellationRequested();

        if (trustedHost.Revision<1 ||
            protectedCandidate.Value==Guid.Empty)
            return false;
        // Only the server's keyed protector can construct a valid HMAC digest.
        // A client-supplied digest, tenant selector, approver ID or proof flag
        // is never accepted. Invalid tokens are denied BEFORE touching SQL.
        var digest=tokens.TryDigest(opaqueBearer);
        if (digest is null) return false;
        return await store.TryActivateAsync(
            trustedHost.TenantId,trustedHost.Revision,
            protectedCandidate,Guid.NewGuid(),Guid.NewGuid(),
            digest,cancellationToken);
    }

    public override string ToString() =>
        "ActivateMembershipWithMfaApprover[redacted]";
}
