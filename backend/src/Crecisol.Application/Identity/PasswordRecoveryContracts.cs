using Crecisol.Domain.Tenancy;

namespace Crecisol.Application.Identity;

/// <summary>
/// Backend-only unforgeable lookup value. Neither identity nor grant.
/// The caller can copy digest bytes only for the protected SQL adapter.
/// </summary>
public sealed class PasswordRecoveryTokenDigest
{
    private readonly byte[] bytes;

    private PasswordRecoveryTokenDigest(byte[] value) => bytes = value;

    public static PasswordRecoveryTokenDigest FromTrustedProtector(ReadOnlySpan<byte> digest)
    {
        if (digest.Length != 32 || digest.IndexOfAnyExcept((byte)0) < 0)
            throw new InvalidOperationException("Nonempty recovery token digest required.");
        return new PasswordRecoveryTokenDigest(digest.ToArray());
    }

    public byte[] CopyForTrustedLedger() => (byte[])bytes.Clone();
    public override string ToString() => "PasswordRecoveryTokenDigest[redacted]";
}

/// <summary>
/// Keyed and domain-separated hash of an email handle; never the email itself.
/// A mailbox digest cannot select a tenant, authenticate, or grant permissions.
/// </summary>
public sealed class PasswordRecoveryMailboxDigest
{
    private readonly byte[] bytes;

    private PasswordRecoveryMailboxDigest(byte[] value) => bytes = value;

    public static PasswordRecoveryMailboxDigest FromTrustedProtector(ReadOnlySpan<byte> digest)
    {
        if (digest.Length != 32 || digest.IndexOfAnyExcept((byte)0) < 0)
            throw new InvalidOperationException("Nonempty mailbox digest required.");
        return new PasswordRecoveryMailboxDigest(digest.ToArray());
    }

    public byte[] CopyForTrustedLedger() => (byte[])bytes.Clone();
    public override string ToString() => "PasswordRecoveryMailboxDigest[redacted]";
}

/// <summary>
/// The delivery token is not an authorization result; it must NEVER be logged,
/// sent in a GET/query-string URL, or returned by a public recovery request.
/// This wrapper exists only between the protector and a trusted delivery sink.
/// </summary>
public sealed class IssuedPasswordRecoveryToken
{
    private readonly string raw;
    private IssuedPasswordRecoveryToken(string value) => raw = value;

    public static IssuedPasswordRecoveryToken FromTrustedProtector(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length != 47 ||
            !value.StartsWith("cr6_", StringComparison.Ordinal) ||
            !value.AsSpan(4).ToString().All(c => c is >= 'A' and <= 'Z'
                or >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_'))
            throw new InvalidOperationException("Invalid recovery delivery envelope.");
        return new IssuedPasswordRecoveryToken(value);
    }

    public string RevealForTrustedDeliveryOnly() => raw;
    public override string ToString() => "IssuedPasswordRecoveryToken[redacted]";
}

public interface ITrustedPasswordRecoveryProtector
{
    IssuedPasswordRecoveryToken GenerateFresh();
    PasswordRecoveryTokenDigest? TryDigest(string? untrustedToken);
    PasswordRecoveryMailboxDigest DigestCanonicalMailbox(CanonicalLoginHandle mailbox);
}

public interface ITrustedPasswordRecoveryLedger
{
    // Returns false for unknown/disabled/unverified subjects. Does not reveal
    // an actor identifier, membership, email or challenge from the database.
    Task<bool> TryIssueAsync(
        TenantId verifiedTenant, PasswordRecoveryMailboxDigest verifiedMailbox,
        Guid challengeId, PasswordRecoveryTokenDigest digest,
        CancellationToken cancellationToken);

    // The one-time SQL transaction must check current verified mailbox,
    // actor, credential, tenant and membership, replace PHC, consume token
    // and revoke all sessions across EVERY tenant of this global actor.
    Task<bool> TryConsumeAsync(
        TenantId verifiedTenant, PasswordRecoveryTokenDigest digest,
        string newArgon2idPhc, CancellationToken cancellationToken);
}

/// <summary>
/// Test implementation is in-memory. A PRODUCTIVE system MUST use an atomic
/// encrypted delivery outbox and a real verified-mailbox provider. No caller
/// may obtain delivery status through public HTTP.
/// </summary>
public interface ITrustedPasswordRecoveryDeliverySink
{
    Task DeliverToVerifiedMailboxAsync(
        TenantId tenant, CanonicalLoginHandle registeredMailbox,
        IssuedPasswordRecoveryToken token, CancellationToken cancellationToken);
}

public interface ITrustedPasswordRecoveryPasswordEncoder
{
    string EncodeNewPassword(ReadOnlySpan<char> candidate);
}

/// <summary>
/// C05-06 backend-only. Public request is always neutral; no tenant selector,
/// actor, role, session or trusted transport is accepted from the browser.
/// Token issuance is never a password reset or authentication grant.
/// </summary>
public sealed class RecoverPasswordOnce
{
    private readonly ITrustedPasswordRecoveryProtector protector;
    private readonly ITrustedPasswordRecoveryLedger ledger;
    private readonly ITrustedPasswordRecoveryDeliverySink delivery;
    private readonly ITrustedPasswordRecoveryPasswordEncoder passwords;

    public RecoverPasswordOnce(
        ITrustedPasswordRecoveryProtector protector,
        ITrustedPasswordRecoveryLedger ledger,
        ITrustedPasswordRecoveryDeliverySink delivery,
        ITrustedPasswordRecoveryPasswordEncoder passwords)
    {
        this.protector = protector ?? throw new ArgumentNullException(nameof(protector));
        this.ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        this.delivery = delivery ?? throw new ArgumentNullException(nameof(delivery));
        this.passwords = passwords ?? throw new ArgumentNullException(nameof(passwords));
    }

    public async Task RequestAsync(CanonicalLoginHandle mailbox,
        VerifiedTenantHostHint verifiedHost, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mailbox);
        ArgumentNullException.ThrowIfNull(verifiedHost);
        cancellationToken.ThrowIfCancellationRequested();
        var token = protector.GenerateFresh();
        var digest = protector.TryDigest(token.RevealForTrustedDeliveryOnly()) ??
            throw new InvalidOperationException("Recovery token generator failed.");
        var mailboxDigest = protector.DigestCanonicalMailbox(mailbox);
        var issued = await ledger.TryIssueAsync(verifiedHost.TenantId, mailboxDigest,
            Guid.NewGuid(), digest, cancellationToken);

        // Always return the same public result for absent, disabled and
        // registered mailboxes. No token or account status goes to the API.
        if (!issued) return;
        try
        {
            await delivery.DeliverToVerifiedMailboxAsync(
                verifiedHost.TenantId, mailbox, token, cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // A future production outbox must atomically record dispatch and
            // support retries. A test sink failure cannot enumerate accounts.
        }
    }

    public async Task<bool> RedeemAsync(string? rawToken,
        ReadOnlyMemory<char> newPassword, VerifiedTenantHostHint verifiedHost,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verifiedHost);
        cancellationToken.ThrowIfCancellationRequested();
        var digest = protector.TryDigest(rawToken);
        if (digest is null) return false;
        // Argon2id policy and compromised-screen service fail closed. No
        // fallback to accepting a weak/unchecked password on any outage.
        var newPhc = passwords.EncodeNewPassword(newPassword.Span);
        return await ledger.TryConsumeAsync(
            verifiedHost.TenantId, digest, newPhc, cancellationToken);
    }

    public override string ToString() => "RecoverPasswordOnce[redacted]";
}
