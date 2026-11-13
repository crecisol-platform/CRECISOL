using Crecisol.Domain.Tenancy;

namespace Crecisol.Application.Identity;

/// <summary>
/// C05-05: 32-byte non-reversible HMAC digest of a 256-bit opaque session
/// token. It cannot be a bearer, actor identity, tenant selector or grant.
/// Copying its bytes is restricted to the trusted ledger boundary.
/// </summary>
public sealed class SessionTokenDigest
{
    private readonly byte[] value;

    private SessionTokenDigest(byte[] digest) => value = digest;

    public static SessionTokenDigest FromTrustedTokenProtector(ReadOnlySpan<byte> digest)
    {
        if (digest.Length != 32 || digest.IndexOfAnyExcept((byte)0) < 0)
            throw new InvalidOperationException("Session token digest must be nonempty SHA-256.");
        return new SessionTokenDigest(digest.ToArray());
    }

    public byte[] CopyForTrustedLedger() => (byte[])value.Clone();

    public override string ToString() => "SessionTokenDigest[redacted]";
}

/// <summary>
/// Raw opaque token is intentionally NOT a property and ToString never prints
/// it. The test-only HTTPS-equivalent loopback transport must use this explicit
/// method to send it once. Real browser transport/cookies are NOT yet wired.
/// </summary>
public sealed class IssuedSessionToken
{
    private IssuedSessionToken(string raw) => rawToken = raw;
    private readonly string rawToken;

    // A wrapper for transport only, NOT evidence of identity or permission.
    // The protector validates and keys the token before storage or use.
    public static IssuedSessionToken FromTrustedProtector(string raw)
    {
        if (string.IsNullOrEmpty(raw) || raw.Length != 47 ||
            !raw.StartsWith("cs5_", StringComparison.Ordinal) ||
            !raw.AsSpan(4).ToString().All(c => c is >= 'A' and <= 'Z'
                or >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_'))
            throw new InvalidOperationException("Noncanonical session token envelope.");
        return new IssuedSessionToken(raw);
    }

    public string RevealForTrustedTransport() => rawToken;

    public override string ToString() => "IssuedSessionToken[redacted]";
}

/// <summary>
/// A server-held cryptographic key is mandatory: none is stored in Git,
/// supplied by the browser or derived from the tenant/actor ID.
/// </summary>
public interface ITrustedOpaqueSessionTokenProtector
{
    IssuedSessionToken GenerateFresh();
    SessionTokenDigest? TryDigest(string? token);
}

/// <summary>
/// All database operations are atomic, use a server-verified tenant scope
/// and recheck account, credential, membership, host and tenant revisions.
/// The ledger may NEVER accept raw bearer tokens, DSNs, caller claims or roles.
/// </summary>
public interface ITrustedSessionLifecycleLedger
{
    Task<bool> TryIssueAsync(
        PreparedContextualLoginEvidence verifiedPreSession, Guid sessionId,
        SessionTokenDigest tokenDigest, CancellationToken cancellationToken);

    Task<bool> IsCurrentAsync(
        TenantId verifiedTenant, SessionTokenDigest tokenDigest,
        CancellationToken cancellationToken);

    Task<bool> TryRotateAsync(
        TenantId verifiedTenant, SessionTokenDigest oldDigest,
        SessionTokenDigest newDigest, CancellationToken cancellationToken);

    Task<bool> TryRevokeAsync(
        TenantId verifiedTenant, SessionTokenDigest currentDigest,
        CancellationToken cancellationToken);
}

/// <summary>
/// Internal-only orchestration over C05-04 password/context proof and atomic
/// ledger. NO DI or product HTTP routes, cookies, JWTs or authorization
/// decision; issue/rotate/revoke never return roles or grants.
/// </summary>
public sealed class ScopedSessionLifecycle
{
    private readonly ITrustedOpaqueSessionTokenProtector tokens;
    private readonly ITrustedSessionLifecycleLedger ledger;

    public ScopedSessionLifecycle(
        ITrustedOpaqueSessionTokenProtector tokens,
        ITrustedSessionLifecycleLedger ledger)
    {
        this.tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        this.ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
    }

    public async Task<IssuedSessionToken?> IssueAsync(
        ContextualLoginPreparation? login, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (login?.Status != ContextualLoginPreparationStatus.Ready ||
            login.Evidence is null)
            return null;

        var raw = tokens.GenerateFresh();
        var digest = tokens.TryDigest(raw.RevealForTrustedTransport());
        if (digest is null)
            throw new InvalidOperationException("Protected session token generation failed.");

        // This method never treats the C05-04 preparation as an authorization:
        // the protected SQL procedure must re-check all trusted revisions
        // against its own identity fixture within the issue transaction.
        if (!await ledger.TryIssueAsync(
                login.Evidence, Guid.NewGuid(), digest, cancellationToken))
            return null;

        return raw;
    }

    public async Task<bool> ValidateAsync(
        string? rawToken, VerifiedTenantHostHint trustedHost,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(trustedHost);
        cancellationToken.ThrowIfCancellationRequested();
        var digest = tokens.TryDigest(rawToken);
        return digest is not null && await ledger.IsCurrentAsync(
            trustedHost.TenantId, digest, cancellationToken);
    }

    public async Task<IssuedSessionToken?> RotateAsync(
        string? rawToken, VerifiedTenantHostHint trustedHost,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(trustedHost);
        cancellationToken.ThrowIfCancellationRequested();
        var previous = tokens.TryDigest(rawToken);
        if (previous is null) return null;
        var replacement = tokens.GenerateFresh();
        var next = tokens.TryDigest(replacement.RevealForTrustedTransport());
        if (next is null)
            throw new InvalidOperationException("Protected session rotation failed.");

        return await ledger.TryRotateAsync(
            trustedHost.TenantId, previous, next, cancellationToken)
            ? replacement : null;
    }

    public async Task<bool> LogoutAsync(
        string? rawToken, VerifiedTenantHostHint trustedHost,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(trustedHost);
        cancellationToken.ThrowIfCancellationRequested();
        var digest = tokens.TryDigest(rawToken);
        return digest is not null && await ledger.TryRevokeAsync(
            trustedHost.TenantId, digest, cancellationToken);
    }
}
