using Crecisol.Domain.Tenancy;

namespace Crecisol.Application.Identity;

/// <summary>
/// Separate, fixture-only contract for a SESSION WITH COMMITTED MFA.
/// The older C05-05 password-only issuance routine MUST NOT be granted to
/// this worker. This port never trusts client MFA flags or raw codes.
/// </summary>
public interface ITrustedMfaBoundSessionLedger
{
    Task<bool> TryIssueAfterCommittedStepUpAsync(
        PreparedContextualLoginEvidence prepared, Guid factorId,
        Guid challengeId, Guid operationId, Guid sessionId,
        SessionTokenDigest tokenDigest, CancellationToken cancellationToken);
}

/// <summary>
/// Generates a strong opaque token only as a transport envelope; it can be
/// returned exclusively after the trusted SQL transaction has checked and
/// consumed a fresh cryptographic MFA proof for this exact actor/tenant.
/// Not a production session issuer and never a resource access grant.
/// </summary>
public sealed class DisposableMfaBoundSessionIssuance
{
    private readonly ITrustedOpaqueSessionTokenProtector tokens;
    private readonly ITrustedMfaBoundSessionLedger ledger;

    public DisposableMfaBoundSessionIssuance(
        ITrustedOpaqueSessionTokenProtector tokens,
        ITrustedMfaBoundSessionLedger ledger)
    {
        this.tokens=tokens??throw new ArgumentNullException(nameof(tokens));
        this.ledger=ledger??throw new ArgumentNullException(nameof(ledger));
    }

    public async Task<IssuedSessionToken?> TryIssueAsync(
        ContextualLoginPreparation? verifiedPasswordAndMembership,
        VerifiedTenantHostHint trustedHost,
        DisposableMfaChallenge? backendChallenge,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(trustedHost);
        cancellationToken.ThrowIfCancellationRequested();
        if (verifiedPasswordAndMembership?.Status != ContextualLoginPreparationStatus.Ready ||
            verifiedPasswordAndMembership.Evidence is not { } proof ||
            proof.TenantId!=trustedHost.TenantId ||
            proof.HostRevision!=trustedHost.Revision ||
            proof.HostRevision<1 ||
            backendChallenge is null ||
            backendChallenge.FactorId==Guid.Empty ||
            backendChallenge.ChallengeId==Guid.Empty ||
            backendChallenge.OperationId==Guid.Empty)
            return null;

        // No raw TOTP, user-submitted "MFA passed" bool, role, user
        // agent or client-selected tenant can reach the SQL boundary.
        var token=tokens.GenerateFresh();
        var digest=tokens.TryDigest(token.RevealForTrustedTransport());
        if (digest is null)
            throw new InvalidOperationException("Protected session token HMAC unavailable.");
        return await ledger.TryIssueAfterCommittedStepUpAsync(
            proof,backendChallenge.FactorId,backendChallenge.ChallengeId,
            backendChallenge.OperationId,Guid.NewGuid(),digest,cancellationToken)
            ? token : null;
    }

    public override string ToString()=>"DisposableMfaBoundSessionIssuance[redacted]";
}
