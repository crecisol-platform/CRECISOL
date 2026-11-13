using Crecisol.Domain.Tenancy;

namespace Crecisol.Application.Identity;

/// <summary>
/// C05-07: a purpose-specific, server-keyed digest of a 256-bit bearer proof.
/// Never an account, session, tenant selection or authorization decision.
/// </summary>
public sealed class MailboxApprovalDigest
{
    private readonly byte[] bytes;
    private MailboxApprovalDigest(byte[] value) => bytes = value;
    public static MailboxApprovalDigest FromTrustedProtector(ReadOnlySpan<byte> value)
    {
        if (value.Length != 32 || value.IndexOfAnyExcept((byte)0) < 0)
            throw new InvalidOperationException("Protected mailbox-approval digest required.");
        return new MailboxApprovalDigest(value.ToArray());
    }
    public byte[] CopyForTrustedLedger() => (byte[])bytes.Clone();
    public override string ToString() => "MailboxApprovalDigest[redacted]";
}

/// <summary>
/// Opaque delivery-only secret: never a URL query value, public API response,
/// trace, log, persistent plaintext field or reusable login credential.
/// </summary>
public sealed class IssuedMailboxApproval
{
    private readonly string raw;
    private IssuedMailboxApproval(string value) => raw = value;
    public static IssuedMailboxApproval FromTrustedProtector(string value)
    {
        if (value is null || value.Length != 47 ||
            !value.StartsWith("cv7_", StringComparison.Ordinal) ||
            !value.AsSpan(4).ToString().All(c => c is >= 'A' and <= 'Z'
                or >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_'))
            throw new InvalidOperationException("Noncanonical mailbox-approval envelope.");
        return new IssuedMailboxApproval(value);
    }
    public string RevealForTrustedDeliveryOnly() => raw;
    public override string ToString() => "IssuedMailboxApproval[redacted]";
}

public enum TrustedMailboxProofDeliveryKind
{
    VerifyRegisteredMailbox = 1,
    ConfirmPreviousMailbox = 2,
    ConfirmReplacementMailbox = 3
}

public interface ITrustedMailboxApprovalProtector
{
    IssuedMailboxApproval GenerateFresh();
    MailboxApprovalDigest? TryDigest(string? untrustedToken);
}

/// <summary>
/// Backend-owned, encrypted, durable outbox is required before product use.
/// The test implementation is memory-only with synthetic recipient mailboxes.
/// </summary>
public interface ITrustedMailboxApprovalDelivery
{
    Task DeliverAsync(TenantId tenant, CanonicalLoginHandle recipient,
        IssuedMailboxApproval token, TrustedMailboxProofDeliveryKind kind,
        CancellationToken cancellationToken);
}

/// <summary>
/// No browser-controlled actor, tenant identifier, membership, role, DB
/// locator, email verifier or reauthentication verdict is accepted.
/// All mutations MUST lock and compare latest identity revisions in SQL.
/// </summary>
public interface ITrustedSensitiveMailboxLedger
{
    Task<bool> TryBeginVerificationAsync(
        TenantId verifiedTenant, PasswordRecoveryMailboxDigest registeredMailbox,
        Guid verificationId, MailboxApprovalDigest challenge,
        CancellationToken cancellationToken);

    Task<bool> TryVerifyAsync(
        TenantId verifiedTenant, MailboxApprovalDigest challenge,
        CancellationToken cancellationToken);

    Task<bool> TryBeginChangeAsync(
        PreparedContextualLoginEvidence verifiedPasswordProof,
        PasswordRecoveryMailboxDigest previousMailbox,
        PasswordRecoveryMailboxDigest replacementMailbox,
        Guid changeId, MailboxApprovalDigest previousProof,
        MailboxApprovalDigest replacementProof, CancellationToken cancellationToken);

    Task<bool> TryConfirmChangeAsync(
        TenantId verifiedTenant, MailboxApprovalDigest challenge,
        CancellationToken cancellationToken);
}

/// <summary>
/// C05-07 internal-only email possession, reauthentication and auditable
/// dual-mailbox approval. This never performs enrollment, issues a session,
/// changes MFA, grants a role, changes tenant or exposes a productive route.
/// </summary>
public sealed class VerifyAndChangeSensitiveMailbox
{
    private readonly ITrustedMailboxApprovalProtector tokens;
    private readonly ITrustedPasswordRecoveryProtector mailboxDigest;
    private readonly ITrustedSensitiveMailboxLedger ledger;
    private readonly ITrustedMailboxApprovalDelivery delivery;

    public VerifyAndChangeSensitiveMailbox(
        ITrustedMailboxApprovalProtector tokens,
        ITrustedPasswordRecoveryProtector mailboxDigest,
        ITrustedSensitiveMailboxLedger ledger,
        ITrustedMailboxApprovalDelivery delivery)
    {
        this.tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        this.mailboxDigest = mailboxDigest ??
            throw new ArgumentNullException(nameof(mailboxDigest));
        this.ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        this.delivery = delivery ?? throw new ArgumentNullException(nameof(delivery));
    }

    /// <summary>
    /// Neutral public response, independent of whether a registered but
    /// unverified address exists. No enrollment or registration is performed.
    /// </summary>
    public async Task RequestVerificationAsync(
        CanonicalLoginHandle registeredMailbox,
        VerifiedTenantHostHint verifiedHost, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registeredMailbox);
        ArgumentNullException.ThrowIfNull(verifiedHost);
        cancellationToken.ThrowIfCancellationRequested();

        var raw = tokens.GenerateFresh();
        var tokenDigest = tokens.TryDigest(raw.RevealForTrustedDeliveryOnly()) ??
            throw new InvalidOperationException("Secure verification token generation failed.");
        var storedMailbox = mailboxDigest.DigestCanonicalMailbox(registeredMailbox);
        if (!await ledger.TryBeginVerificationAsync(
                verifiedHost.TenantId, storedMailbox, Guid.NewGuid(),
                tokenDigest, cancellationToken))
            return;
        // In production, only an atomic encrypted delivery outbox derived
        // from the VERIFIED directory address may replace this test sink.
        await delivery.DeliverAsync(verifiedHost.TenantId, registeredMailbox, raw,
            TrustedMailboxProofDeliveryKind.VerifyRegisteredMailbox,
            cancellationToken);
    }

    public async Task<bool> ConfirmVerificationAsync(
        string? secret, VerifiedTenantHostHint verifiedHost,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verifiedHost);
        cancellationToken.ThrowIfCancellationRequested();
        var digest = tokens.TryDigest(secret);
        return digest is not null && await ledger.TryVerifyAsync(
            verifiedHost.TenantId, digest, cancellationToken);
    }

    /// <summary>
    /// Requires a fresh C05-04 genuine Argon2id credential recheck whose
    /// ContextualLoginPreparation is ready and HOST matches current source.
    /// Protected SQL revalidates revisions atomically before making a change
    /// pending; the two mailbox recipients independently confirm afterwards.
    /// Neither request nor password verification alters the actual mailbox.
    /// </summary>
    public async Task RequestChangeAsync(
        CanonicalLoginHandle previousMailbox, CanonicalLoginHandle nextMailbox,
        ContextualLoginPreparation? passwordReauthentication,
        VerifiedTenantHostHint verifiedHost, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(previousMailbox);
        ArgumentNullException.ThrowIfNull(nextMailbox);
        ArgumentNullException.ThrowIfNull(verifiedHost);
        cancellationToken.ThrowIfCancellationRequested();

        if (passwordReauthentication?.Status != ContextualLoginPreparationStatus.Ready ||
            passwordReauthentication.Evidence is not { } proof ||
            proof.TenantId != verifiedHost.TenantId ||
            proof.HostRevision != verifiedHost.Revision ||
            proof.HostRevision < 1 ||
            previousMailbox.Value == nextMailbox.Value)
            return;

        var previous = mailboxDigest.DigestCanonicalMailbox(previousMailbox);
        var next = mailboxDigest.DigestCanonicalMailbox(nextMailbox);
        var oldRaw = tokens.GenerateFresh();
        var newRaw = tokens.GenerateFresh();
        var oldDigest = tokens.TryDigest(oldRaw.RevealForTrustedDeliveryOnly());
        var newDigest = tokens.TryDigest(newRaw.RevealForTrustedDeliveryOnly());
        if (oldDigest is null || newDigest is null)
            throw new InvalidOperationException("Secure dual-mailbox proof generation failed.");

        if (!await ledger.TryBeginChangeAsync(
                proof, previous, next, Guid.NewGuid(),
                oldDigest, newDigest, cancellationToken))
            return;

        // A real implementation must commit an encrypted outbox atomically
        // with the challenge; a partial delivery remains pending, not approved.
        await delivery.DeliverAsync(verifiedHost.TenantId, previousMailbox, oldRaw,
            TrustedMailboxProofDeliveryKind.ConfirmPreviousMailbox, cancellationToken);
        await delivery.DeliverAsync(verifiedHost.TenantId, nextMailbox, newRaw,
            TrustedMailboxProofDeliveryKind.ConfirmReplacementMailbox,
            cancellationToken);
    }

    public async Task<bool> ConfirmChangeAsync(string? secret,
        VerifiedTenantHostHint verifiedHost, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verifiedHost);
        cancellationToken.ThrowIfCancellationRequested();
        var digest = tokens.TryDigest(secret);
        return digest is not null && await ledger.TryConfirmChangeAsync(
            verifiedHost.TenantId, digest, cancellationToken);
    }

    public override string ToString() => "VerifyAndChangeSensitiveMailbox[redacted]";
}
