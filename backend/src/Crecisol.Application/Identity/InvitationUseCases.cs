using Crecisol.Application.Tenancy;
using Crecisol.Domain.Identity;
using Crecisol.Domain.Tenancy;

namespace Crecisol.Application.Identity;

/// <summary>
/// Server-side invitation issue authorization: action/branch/owner and tenant
/// state MUST be independently checked; C05-01 binding is not a role grant.
/// This future port has no production adapter or HTTP activation in C05-02.
/// </summary>
public interface ITrustedInvitationIssuerPolicy
{
    Task<InvitationIssuePolicyDecision?> EvaluateAsync(
        AccountTenantBindingEvidence issuer,
        AccountTenantProfileKind proposedProfile,
        CancellationToken cancellationToken);
}

/// <summary>
/// Future trusted directory verifies that this opaque recipient handle is
/// eligible to be invited into the resolved tenant; a GUID or email does NOT
/// prove channel control. No address enumeration is returned to the caller.
/// </summary>
public interface ITrustedInvitationTargetDirectory
{
    Task<bool> IsEligibleAsync(
        TenantId verifiedTenant, RecipientChannelBindingId recipient,
        CancellationToken cancellationToken);
}

/// <summary>
/// Must store only hashed token metadata; uniqueness and idempotency must use
/// tenant-scoped transaction constraints. Claim MUST atomically check tenant
/// lifecycle, revision, recipient, token digest, expiry and pending state,
/// transition exactly one row, and NEVER grant account memberships/sessions.
/// This port is intentionally NOT wired to a production SQL implementation.
/// </summary>
public interface ITrustedInvitationLedger
{
    Task<bool> TryInsertUniqueAsync(
        InvitationSnapshot snapshot, CancellationToken cancellationToken);

    Task<IReadOnlyList<InvitationSnapshot>?> ReadExactAsync(
        TenantId verifiedTenant, InvitationId id, CancellationToken cancellationToken);

    Task<bool> TryClaimOnceAtomicallyAsync(
        InvitationSnapshot expected, TenantActorId verifiedRecipientActor,
        DateTimeOffset nowUtc, CancellationToken cancellationToken);
}

/// <summary>Proof of channel possession; a future C05-07 adapter must verify it.</summary>
public interface ITrustedCurrentRecipientChannel
{
    Task<VerifiedRecipientChannel?> ReadVerifiedAsync(CancellationToken cancellationToken);
}

/// <summary>Terms acceptance comes from tenant-scoped backend ledger only.</summary>
public interface ITrustedInvitationConsentLedger
{
    Task<VerifiedInvitationTermsAcceptance?> ReadAcceptedAsync(
        TenantId verifiedTenant, RecipientChannelBindingId verifiedRecipient,
        CancellationToken cancellationToken);
}

/// <summary>
/// Returned only inside backend orchestration. Owns a disposable 256-bit
/// one-time capability for future trusted delivery. Never serialize to a
/// public DTO/log, use as a bearer session, or emit over unauthenticated API.
/// </summary>
public sealed class TrustedInvitationIssuance : IDisposable
{
    internal TrustedInvitationIssuance(InvitationId id,
        EphemeralInvitationCode deliveryCapability)
    {
        Id = id;
        DeliveryCapability = deliveryCapability;
    }

    public InvitationId Id { get; }
    public EphemeralInvitationCode DeliveryCapability { get; }
    public void Dispose() => DeliveryCapability.Dispose();
    public override string ToString() => "TrustedInvitationIssuance[redacted]";
}

/// <summary>
/// Issues an unconsumed capability only after C04/C05-01 verified context AND
/// independent backend invitation policy. No client tenant_id, host, role,
// password, or email can bypass this. Profile Owner/SuperAdmin not supported.
/// </summary>
public sealed class IssueInternalInvitation
{
    private readonly ResolveCurrentAccountTenantLink issuerResolver;
    private readonly ITrustedInvitationIssuerPolicy policy;
    private readonly ITrustedInvitationTargetDirectory targets;
    private readonly ITrustedInvitationLedger ledger;
    private readonly ITrustedHostUtcClock clock;

    public IssueInternalInvitation(
        ResolveCurrentAccountTenantLink issuerResolver,
        ITrustedInvitationIssuerPolicy policy,
        ITrustedInvitationTargetDirectory targets,
        ITrustedInvitationLedger ledger,
        ITrustedHostUtcClock clock)
    {
        this.issuerResolver = issuerResolver ?? throw new ArgumentNullException(nameof(issuerResolver));
        this.policy = policy ?? throw new ArgumentNullException(nameof(policy));
        this.targets = targets ?? throw new ArgumentNullException(nameof(targets));
        this.ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<TrustedInvitationIssuance> ExecuteAsync(
        RecipientChannelBindingId recipient,
        AccountTenantProfileKind profile,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recipient);
        if (profile is not (AccountTenantProfileKind.Employee or AccountTenantProfileKind.Consumer))
            throw new UnauthorizedAccessException("Invitation request unavailable.");
        cancellationToken.ThrowIfCancellationRequested();

        var issuer = await issuerResolver.ExecuteAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var decision = await policy.EvaluateAsync(issuer, profile, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (decision is null || !decision.Allowed ||
            !await targets.IsEligibleAsync(issuer.TenantId, recipient, cancellationToken))
            throw new UnauthorizedAccessException("Invitation request unavailable.");

        var firstNow = clock.GetUtcNow();
        InvitationSnapshot.RequireUtc(firstNow);
        cancellationToken.ThrowIfCancellationRequested();

        // All changes to policy/identity while issuing fail closed.
        var refreshedIssuer = await issuerResolver.ExecuteAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var refreshedPolicy = await policy.EvaluateAsync(refreshedIssuer, profile, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (refreshedIssuer != issuer || refreshedPolicy != decision ||
            !await targets.IsEligibleAsync(issuer.TenantId, recipient, cancellationToken))
            throw new UnauthorizedAccessException("Invitation request unavailable.");

        var finalNow = clock.GetUtcNow();
        InvitationSnapshot.RequireUtc(finalNow);
        if (finalNow < firstNow)
            throw new InvalidOperationException("Invitation clock moved backwards.");
        cancellationToken.ThrowIfCancellationRequested();

        var capability = EphemeralInvitationCode.Generate();
        try
        {
            var snapshot = InvitationSnapshot.RestoreFromTrustedInvitationStore(
                InvitationId.From(Guid.NewGuid()), issuer.TenantId, issuer.ActorId,
                recipient, profile, InvitationLifecycleState.Pending,
                capability.CreateDigest(), 1, decision.TermsVersion,
                finalNow, finalNow, finalNow.Add(decision.Lifetime));
            if (!await ledger.TryInsertUniqueAsync(snapshot, cancellationToken))
                throw new UnauthorizedAccessException("Invitation request unavailable.");
            cancellationToken.ThrowIfCancellationRequested();
            // No invitation acceptance, account membership or session created.
            return new TrustedInvitationIssuance(snapshot.Id, capability);
        }
        catch
        {
            capability.Dispose();
            throw;
        }
    }
}

/// <summary>
/// An acknowledged claim only. It is NOT a grant, signed identity assertion,
/// verified login, membership, tenant selector or access to private data.
/// </summary>
public sealed class InternalInvitationClaimReceipt
{
    internal InternalInvitationClaimReceipt(InvitationId id) => Id = id;
    public InvitationId Id { get; }
    public override string ToString() => "InternalInvitationClaimReceipt[redacted]";
}

/// <summary>
/// Validates current direct host, active tenant, verified recipient proof and
/// terms ledger; consumes ONE pending token using the atomic future port.
/// No HTTP route, email sender, customer DB writer or session adapter exists.
/// </summary>
public sealed class ClaimInternalInvitation
{
    private readonly ITrustedCurrentDirectHostReader directHost;
    private readonly ResolveVerifiedTenantHost hostResolver;
    private readonly ITrustedTenantCatalogReader tenants;
    private readonly ITrustedCurrentRecipientChannel recipients;
    private readonly ITrustedInvitationConsentLedger terms;
    private readonly ITrustedInvitationLedger ledger;
    private readonly ITrustedHostUtcClock clock;

    public ClaimInternalInvitation(
        ITrustedCurrentDirectHostReader directHost,
        ResolveVerifiedTenantHost hostResolver,
        ITrustedTenantCatalogReader tenants,
        ITrustedCurrentRecipientChannel recipients,
        ITrustedInvitationConsentLedger terms,
        ITrustedInvitationLedger ledger,
        ITrustedHostUtcClock clock)
    {
        this.directHost = directHost ?? throw new ArgumentNullException(nameof(directHost));
        this.hostResolver = hostResolver ?? throw new ArgumentNullException(nameof(hostResolver));
        this.tenants = tenants ?? throw new ArgumentNullException(nameof(tenants));
        this.recipients = recipients ?? throw new ArgumentNullException(nameof(recipients));
        this.terms = terms ?? throw new ArgumentNullException(nameof(terms));
        this.ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<InternalInvitationClaimReceipt> ExecuteAsync(
        InvitationId invitationId, ReadOnlyMemory<byte> rawCode,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(invitationId);
        cancellationToken.ThrowIfCancellationRequested();
        if (rawCode.Length != 32)
            throw new UnauthorizedAccessException("Invitation claim unavailable.");

        var host = await directHost.ReadCurrentDirectHostAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (host is null)
            throw new UnauthorizedAccessException("Invitation claim unavailable.");
        var verifiedHost = await hostResolver.ResolveAsync(host, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var tenant = await tenants.FindByIdAsync(verifiedHost.TenantId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (tenant is null || tenant.TenantId != verifiedHost.TenantId ||
            tenant.State != TenantLifecycleState.Active ||
            tenant.AddressMode != verifiedHost.AddressMode)
            throw new UnauthorizedAccessException("Invitation claim unavailable.");

        var recipient = await recipients.ReadVerifiedAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var firstNow = clock.GetUtcNow();
        InvitationSnapshot.RequireUtc(firstNow);
        if (recipient is null || !recipient.IsValidAtUtc(firstNow))
            throw new UnauthorizedAccessException("Invitation claim unavailable.");

        var accepted = await terms.ReadAcceptedAsync(
            verifiedHost.TenantId, recipient.Recipient, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var matches = await ledger.ReadExactAsync(
            verifiedHost.TenantId, invitationId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (matches is null || matches.Count != 1 || matches[0] is null)
            throw new UnauthorizedAccessException("Invitation claim unavailable.");
        var invitation = matches[0];
        if (invitation.Id != invitationId ||
            invitation.TenantId != verifiedHost.TenantId ||
            invitation.Recipient != recipient.Recipient ||
            invitation.IssuedBy == recipient.ActorId ||
            !invitation.IsPendingAtUtc(firstNow) ||
            accepted is null || accepted.TenantId != verifiedHost.TenantId ||
            accepted.Recipient != recipient.Recipient ||
            accepted.TermsVersion != invitation.TermsVersion ||
            accepted.AcceptedUtc > firstNow ||
            !invitation.Digest.Matches(rawCode.Span))
            throw new UnauthorizedAccessException("Invitation claim unavailable.");

        // The final transaction still MUST gate tenant status and token state.
        // These second checks reject observable drift before that transaction.
        var refreshedHost = await hostResolver.ResolveAsync(host, cancellationToken);
        var refreshedTenant = await tenants.FindByIdAsync(verifiedHost.TenantId, cancellationToken);
        var refreshedRecipient = await recipients.ReadVerifiedAsync(cancellationToken);
        var refreshedTerms = await terms.ReadAcceptedAsync(
            verifiedHost.TenantId, recipient.Recipient, cancellationToken);
        var refreshedMatches = await ledger.ReadExactAsync(
            verifiedHost.TenantId, invitationId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (refreshedHost != verifiedHost || refreshedTenant != tenant ||
            refreshedRecipient != recipient || refreshedTerms != accepted ||
            refreshedMatches is null || refreshedMatches.Count != 1 ||
            refreshedMatches[0] is null ||
            refreshedMatches[0].Revision != invitation.Revision ||
            refreshedMatches[0].State != InvitationLifecycleState.Pending ||
            !refreshedMatches[0].Digest.SameDigestAs(invitation.Digest))
            throw new UnauthorizedAccessException("Invitation claim unavailable.");

        var finalNow = clock.GetUtcNow();
        InvitationSnapshot.RequireUtc(finalNow);
        if (finalNow < firstNow)
            throw new InvalidOperationException("Invitation clock moved backwards.");
        if (!recipient.IsValidAtUtc(finalNow) || !invitation.IsPendingAtUtc(finalNow) ||
            tenant.UpdatedAtUtc > finalNow || accepted.AcceptedUtc > finalNow)
            throw new UnauthorizedAccessException("Invitation claim unavailable.");
        cancellationToken.ThrowIfCancellationRequested();

        if (!await ledger.TryClaimOnceAtomicallyAsync(
                invitation, recipient.ActorId, finalNow, cancellationToken))
            throw new UnauthorizedAccessException("Invitation claim unavailable.");
        cancellationToken.ThrowIfCancellationRequested();

        // A successful claim is ONLY a consumed invitation; actual tenant
        // membership, account creation, email proof and sessions are gated
        // integrations in later phases.
        return new InternalInvitationClaimReceipt(invitation.Id);
    }
}
