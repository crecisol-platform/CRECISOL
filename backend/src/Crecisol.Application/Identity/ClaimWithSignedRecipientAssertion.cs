using Crecisol.Application.Tenancy;
using Crecisol.Domain.Identity;
using Crecisol.Domain.Tenancy;

namespace Crecisol.Application.Identity;

/// <summary>
/// TEST-ONLY next security gate. A signed assertion is never an invitation,
/// email-verification receipt, role, tenant membership, or login credential.
/// The backend must verify the signature with a private key and revalidate
/// the asserted recipient-session against current MySQL state atomically
/// with invitation consumption. Caller-supplied subject IDs are forbidden.
/// </summary>
public interface ITrustedSignedRecipientClaimLedger
{
    Task<bool> TryClaimWithVerifiedAssertionOnceAsync(
        TenantId verifiedTenant,
        InvitationId invitationId,
        ReadOnlyMemory<byte> rawInvitationCapability,
        ReadOnlyMemory<byte> signedRecipientAssertion,
        DateTimeOffset trustedNowUtc,
        CancellationToken cancellationToken);
}

/// <summary>
/// C05-02 test-only composition of verified host/catalog and a signed recipient
/// claim. The future production login + real inbox verifier are NOT installed.
/// No HTTP route, DI registration, account grant, cookie or tenant selector.
/// </summary>
public sealed class ClaimWithSignedRecipientAssertion
{
    private readonly ITrustedCurrentDirectHostReader directHost;
    private readonly ResolveVerifiedTenantHost hostResolver;
    private readonly ITrustedTenantCatalogReader tenants;
    private readonly ITrustedSignedRecipientClaimLedger ledger;
    private readonly ITrustedHostUtcClock clock;

    public ClaimWithSignedRecipientAssertion(
        ITrustedCurrentDirectHostReader directHost,
        ResolveVerifiedTenantHost hostResolver,
        ITrustedTenantCatalogReader tenants,
        ITrustedSignedRecipientClaimLedger ledger,
        ITrustedHostUtcClock clock)
    {
        this.directHost = directHost ?? throw new ArgumentNullException(nameof(directHost));
        this.hostResolver = hostResolver ?? throw new ArgumentNullException(nameof(hostResolver));
        this.tenants = tenants ?? throw new ArgumentNullException(nameof(tenants));
        this.ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<InternalInvitationClaimReceipt> ExecuteAsync(
        InvitationId invitationId,
        ReadOnlyMemory<byte> rawInvitationCapability,
        ReadOnlyMemory<byte> signedRecipientAssertion,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invitationId);
        cancellationToken.ThrowIfCancellationRequested();
        if (rawInvitationCapability.Length != 32 || signedRecipientAssertion.Length != 125)
            throw new UnauthorizedAccessException("Invitation claim unavailable.");

        var direct = await directHost.ReadCurrentDirectHostAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (direct is null)
            throw new UnauthorizedAccessException("Invitation claim unavailable.");

        var host = await hostResolver.ResolveAsync(direct, cancellationToken);
        var catalog = await tenants.FindByIdAsync(host.TenantId, cancellationToken);
        var firstNow = clock.GetUtcNow();
        InvitationSnapshot.RequireUtc(firstNow);
        cancellationToken.ThrowIfCancellationRequested();
        if (catalog is null || catalog.TenantId != host.TenantId ||
            catalog.AddressMode != host.AddressMode ||
            catalog.State != TenantLifecycleState.Active ||
            catalog.UpdatedAtUtc > firstNow)
            throw new UnauthorizedAccessException("Invitation claim unavailable.");

        var currentDirect = await directHost.ReadCurrentDirectHostAsync(cancellationToken);
        var freshHost = await hostResolver.ResolveAsync(direct, cancellationToken);
        var freshCatalog = await tenants.FindByIdAsync(host.TenantId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (currentDirect != direct || freshHost != host || freshCatalog != catalog)
            throw new UnauthorizedAccessException("Invitation claim unavailable.");

        var finalNow = clock.GetUtcNow();
        InvitationSnapshot.RequireUtc(finalNow);
        if (finalNow < firstNow || catalog.UpdatedAtUtc > finalNow)
            throw new UnauthorizedAccessException("Invitation claim unavailable.");

        if (!await ledger.TryClaimWithVerifiedAssertionOnceAsync(
            host.TenantId, invitationId, rawInvitationCapability,
            signedRecipientAssertion, finalNow, cancellationToken))
            throw new UnauthorizedAccessException("Invitation claim unavailable.");

        // An acknowledged consumption ONLY. It is NOT identity enrollment,
        // membership authorization, account creation or a logged-in session.
        return new InternalInvitationClaimReceipt(invitationId);
    }
}
