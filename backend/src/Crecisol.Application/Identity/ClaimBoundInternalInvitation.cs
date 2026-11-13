using Crecisol.Application.Tenancy;
using Crecisol.Domain.Identity;
using Crecisol.Domain.Tenancy;

namespace Crecisol.Application.Identity;

/// <summary>
/// C05-02 test-only contract. Issuance MUST persist the exact tenant/issuer
/// link/branch/resource/action/revisions in the same transaction as the invite.
/// Claim MUST compare those recorded fields with CURRENT protected SQL rows in
/// the same single-row, one-use CAS. Neither operation can create memberships.
/// </summary>
public interface ITrustedBoundInvitationLedger : ITrustedScopedInvitationLedger
{
    Task<bool> TryClaimBoundOnceAtomicallyAsync(
        TenantId verifiedTenant, InvitationId invitationId,
        RecipientChannelBindingId verifiedChannel, TenantActorId verifiedActor,
        InvitationTokenDigest digest, DateTimeOffset nowUtc,
        CancellationToken cancellationToken);
}

/// <summary>
/// No active login or private API is created by this INTERNAL C05-02 case.
/// The future recipient identity and possession channel must be verified by
/// trusted backend adapters, not by any browser-provided email or actor ID.
/// The SQL port independently validates recipient, terms and current issuer
/// permissions, including the *original* resource and action scope.
/// </summary>
public sealed class ClaimBoundInternalInvitation
{
    private readonly ITrustedCurrentDirectHostReader directHost;
    private readonly ResolveVerifiedTenantHost hostResolver;
    private readonly ITrustedTenantCatalogReader tenants;
    private readonly ITrustedCurrentRecipientChannel recipients;
    private readonly ITrustedBoundInvitationLedger ledger;
    private readonly ITrustedHostUtcClock clock;

    public ClaimBoundInternalInvitation(
        ITrustedCurrentDirectHostReader directHost,
        ResolveVerifiedTenantHost hostResolver,
        ITrustedTenantCatalogReader tenants,
        ITrustedCurrentRecipientChannel recipients,
        ITrustedBoundInvitationLedger ledger,
        ITrustedHostUtcClock clock)
    {
        this.directHost = directHost ?? throw new ArgumentNullException(nameof(directHost));
        this.hostResolver = hostResolver ?? throw new ArgumentNullException(nameof(hostResolver));
        this.tenants = tenants ?? throw new ArgumentNullException(nameof(tenants));
        this.recipients = recipients ?? throw new ArgumentNullException(nameof(recipients));
        this.ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<InternalInvitationClaimReceipt> ExecuteAsync(
        InvitationId invitationId, ReadOnlyMemory<byte> rawToken,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invitationId);
        cancellationToken.ThrowIfCancellationRequested();
        if (rawToken.Length != 32)
            throw new UnauthorizedAccessException("Invitation claim unavailable.");

        var direct = await directHost.ReadCurrentDirectHostAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (direct is null)
            throw new UnauthorizedAccessException("Invitation claim unavailable.");
        var verified = await hostResolver.ResolveAsync(direct, cancellationToken);
        var catalog = await tenants.FindByIdAsync(verified.TenantId, cancellationToken);
        var recipient = await recipients.ReadVerifiedAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        var first = clock.GetUtcNow();
        InvitationSnapshot.RequireUtc(first);
        if (catalog is null || catalog.TenantId != verified.TenantId ||
            catalog.AddressMode != verified.AddressMode ||
            catalog.State != TenantLifecycleState.Active ||
            catalog.UpdatedAtUtc > first ||
            recipient is null || !recipient.IsValidAtUtc(first))
            throw new UnauthorizedAccessException("Invitation claim unavailable.");

        // Cached evidence does not grant a claim. Repeat only to catch drift
        // ahead of the SQL linearization point; SQL is ALWAYS authoritative.
        var currentDirect = await directHost.ReadCurrentDirectHostAsync(cancellationToken);
        if (currentDirect != direct)
            throw new UnauthorizedAccessException("Invitation claim unavailable.");
        var refreshedHost = await hostResolver.ResolveAsync(direct, cancellationToken);
        var refreshedCatalog = await tenants.FindByIdAsync(verified.TenantId, cancellationToken);
        var refreshedRecipient = await recipients.ReadVerifiedAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (refreshedHost != verified || refreshedCatalog != catalog ||
            refreshedRecipient != recipient)
            throw new UnauthorizedAccessException("Invitation claim unavailable.");

        var final = clock.GetUtcNow();
        InvitationSnapshot.RequireUtc(final);
        if (final < first || !recipient.IsValidAtUtc(final))
            throw new UnauthorizedAccessException("Invitation claim unavailable.");

        var digest = InvitationTokenDigest.FromToken(rawToken.Span);
        if (!await ledger.TryClaimBoundOnceAtomicallyAsync(
                verified.TenantId, invitationId, recipient.Recipient,
                recipient.ActorId, digest, final, cancellationToken))
            throw new UnauthorizedAccessException("Invitation claim unavailable.");

        // ONLY a consumed invitation; never a grant, credential, role or session.
        return new InternalInvitationClaimReceipt(invitationId);
    }
}
