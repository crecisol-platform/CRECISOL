using Crecisol.Application.Tenancy;
using Crecisol.Domain.Identity;
using Crecisol.Domain.Tenancy;

namespace Crecisol.Application.Identity;

/// <summary>
/// C05-02 internal atomic issuance boundary. The persistence adapter MUST
/// revalidate the C04 issuer binding, action, branch, resource, owner scope
/// and current revisions *inside the same DB transaction* that inserts a
/// pending invitation. A successful insert grants no membership or session.
/// </summary>
public interface ITrustedScopedInvitationLedger
{
    Task<bool> TryIssueAuthorizedOnceAsync(
        InvitationSnapshot pending,
        AccountTenantBindingEvidence issuer,
        TenantBranchResourceScopeEvidence scope,
        CancellationToken cancellationToken);
}

/// <summary>
/// Test-only future-use-case contract: requires independently resolved
/// account/tenant evidence (C05-01), C04 scope evidence, issuer policy and
/// recipient eligibility. It is NOT an HTTP endpoint or login verification.
/// No production DI binding exists; C05-03+ remain untouched.
/// </summary>
public sealed class IssueScopedInternalInvitation
{
    private static readonly TenantRequiredAction RequiredAction =
        TenantRequiredAction.FromServerPolicy("identity.invitation.issue");

    private readonly ResolveCurrentAccountTenantLink issuerResolver;
    private readonly ResolveCurrentBranchResourceScope scopeResolver;
    private readonly ITrustedInvitationIssuerPolicy policy;
    private readonly ITrustedInvitationTargetDirectory targets;
    private readonly ITrustedScopedInvitationLedger ledger;
    private readonly ITrustedHostUtcClock clock;

    public IssueScopedInternalInvitation(
        ResolveCurrentAccountTenantLink issuerResolver,
        ResolveCurrentBranchResourceScope scopeResolver,
        ITrustedInvitationIssuerPolicy policy,
        ITrustedInvitationTargetDirectory targets,
        ITrustedScopedInvitationLedger ledger,
        ITrustedHostUtcClock clock)
    {
        this.issuerResolver = issuerResolver ??
            throw new ArgumentNullException(nameof(issuerResolver));
        this.scopeResolver = scopeResolver ??
            throw new ArgumentNullException(nameof(scopeResolver));
        this.policy = policy ?? throw new ArgumentNullException(nameof(policy));
        this.targets = targets ?? throw new ArgumentNullException(nameof(targets));
        this.ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    /// <summary>
    /// resource is only a SELECTOR; the resource's tenant/branch/owner and
    /// exact "identity.invitation.issue" action come from trusted server
    /// readers. The browser cannot choose a required action or tenant.
    /// </summary>
    public async Task<TrustedInvitationIssuance> ExecuteAsync(
        TenantResourceId resource,
        RecipientChannelBindingId recipient,
        AccountTenantProfileKind profile,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(recipient);
        cancellationToken.ThrowIfCancellationRequested();
        if (profile is not (AccountTenantProfileKind.Employee or
                            AccountTenantProfileKind.Consumer))
            throw new UnauthorizedAccessException("Invitation request unavailable.");

        var issuer = await issuerResolver.ExecuteAsync(cancellationToken);
        var scope = await scopeResolver.ExecuteAsync(
            resource, RequiredAction, cancellationToken);
        EnsureMatch(issuer, scope, resource);

        var decision = await policy.EvaluateAsync(
            issuer, profile, cancellationToken);
        if (decision is null || !decision.Allowed ||
            !await targets.IsEligibleAsync(issuer.TenantId, recipient, cancellationToken))
            throw new UnauthorizedAccessException("Invitation request unavailable.");

        var firstNow = clock.GetUtcNow();
        InvitationSnapshot.RequireUtc(firstNow);
        cancellationToken.ThrowIfCancellationRequested();

        // Async work may observe issuer revocation, ownership changes or
        // permission removal. Refresh all independent trusted catalog facts.
        var refreshedIssuer = await issuerResolver.ExecuteAsync(cancellationToken);
        var refreshedScope = await scopeResolver.ExecuteAsync(
            resource, RequiredAction, cancellationToken);
        var refreshedDecision = await policy.EvaluateAsync(
            refreshedIssuer, profile, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureMatch(refreshedIssuer, refreshedScope, resource);
        if (issuer != refreshedIssuer || scope != refreshedScope ||
            refreshedDecision != decision ||
            !await targets.IsEligibleAsync(issuer.TenantId, recipient, cancellationToken))
            throw new UnauthorizedAccessException("Invitation request unavailable.");

        var finalNow = clock.GetUtcNow();
        InvitationSnapshot.RequireUtc(finalNow);
        cancellationToken.ThrowIfCancellationRequested();
        if (finalNow < firstNow)
            throw new InvalidOperationException("Invitation clock moved backwards.");

        var capability = EphemeralInvitationCode.Generate();
        try
        {
            var pending = InvitationSnapshot.RestoreFromTrustedInvitationStore(
                InvitationId.From(Guid.NewGuid()), issuer.TenantId, issuer.ActorId,
                recipient, profile, InvitationLifecycleState.Pending,
                capability.CreateDigest(), 1, decision.TermsVersion,
                finalNow, finalNow, finalNow.Add(decision.Lifetime));
            if (!await ledger.TryIssueAuthorizedOnceAsync(
                    pending, issuer, scope, cancellationToken))
                throw new UnauthorizedAccessException("Invitation request unavailable.");

            cancellationToken.ThrowIfCancellationRequested();
            return new TrustedInvitationIssuance(pending.Id, capability);
        }
        catch
        {
            capability.Dispose();
            throw;
        }
    }

    private static void EnsureMatch(AccountTenantBindingEvidence issuer,
        TenantBranchResourceScopeEvidence scope, TenantResourceId resource)
    {
        if (issuer.ActorId != scope.ActorId ||
            issuer.TenantId != scope.TenantId ||
            issuer.SessionRevision != scope.SessionRevision ||
            issuer.LinkRevision != scope.MembershipRevision ||
            issuer.HostRevision != scope.HostRevision ||
            issuer.TenantCatalogRevision != scope.TenantCatalogRevision ||
            scope.ResourceId != resource ||
            scope.RequiredAction != RequiredAction)
            throw new UnauthorizedAccessException("Invitation authorization unavailable.");
    }
}
