using Crecisol.Application.Tenancy;
using Crecisol.Domain.Identity;
using Crecisol.Domain.Tenancy;

namespace Crecisol.Application.Identity;

/// <summary>
/// A server-owned socket identity (IPv4 mapped to 16 bytes or IPv6 16 bytes).
/// Never derive this from Forwarded, X-Forwarded-For, query, cookie or body.
/// This is an internal future port; no production HTTP/DI binding exists.
/// </summary>
public interface ITrustedRegistrationNetworkPeer
{
    ValueTask<ReadOnlyMemory<byte>?> ReadVerifiedPeerAddressAsync(
        CancellationToken cancellationToken);
}

/// <summary>
/// Trusted ingestion boundary. An implementation can only persist a neutral
/// intent and MUST NOT issue accounts, memberships, login credentials, sessions
/// or a secret to the requesting browser. No account-existence lookup permitted.
/// </summary>
public interface ITrustedNeutralRegistrationIntentRecorder
{
    Task RecordWithoutEnumerationAsync(
        TenantId verifiedTenant, string canonicalMailbox,
        ReadOnlyMemory<byte> verifiedPeerAddress,
        DateTimeOffset nowUtc, CancellationToken cancellationToken);
}

/// <summary>
/// C05-02 non-enumerating, PUBLIC REGISTRATION INTENT ONLY. Anonymous mailbox
/// input cannot become a tenant selector, trusted identity, or permission.
/// All syntactically valid mailboxes take the same backend path regardless of
/// whether an account exists: no account lookup is ever performed.
/// </summary>
public sealed class RequestNeutralRegistrationIntent
{
    private readonly ITrustedCurrentDirectHostReader directHost;
    private readonly ResolveVerifiedTenantHost hosts;
    private readonly ITrustedTenantCatalogReader tenants;
    private readonly ITrustedRegistrationNetworkPeer peer;
    private readonly ITrustedNeutralRegistrationIntentRecorder recorder;
    private readonly ITrustedHostUtcClock clock;

    public RequestNeutralRegistrationIntent(
        ITrustedCurrentDirectHostReader directHost,
        ResolveVerifiedTenantHost hosts,
        ITrustedTenantCatalogReader tenants,
        ITrustedRegistrationNetworkPeer peer,
        ITrustedNeutralRegistrationIntentRecorder recorder,
        ITrustedHostUtcClock clock)
    {
        this.directHost = directHost ?? throw new ArgumentNullException(nameof(directHost));
        this.hosts = hosts ?? throw new ArgumentNullException(nameof(hosts));
        this.tenants = tenants ?? throw new ArgumentNullException(nameof(tenants));
        this.peer = peer ?? throw new ArgumentNullException(nameof(peer));
        this.recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<NeutralRegistrationReceipt> ExecuteAsync(
        string? requestedMailbox, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // A malformed mailbox receives the same public receipt without any
        // delivery or persistence. This is not an account-existence signal.
        var canonical = CanonicalMailboxOrNull(requestedMailbox);

        var direct = await directHost.ReadCurrentDirectHostAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (direct is null)
            throw new UnauthorizedAccessException("Public host context unavailable.");
        var verified = await hosts.ResolveAsync(direct, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var initialTenant = await tenants.FindByIdAsync(
            verified.TenantId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        var now = clock.GetUtcNow();
        InvitationSnapshot.RequireUtc(now);
        if (initialTenant is null || initialTenant.TenantId != verified.TenantId ||
            initialTenant.AddressMode != verified.AddressMode ||
            initialTenant.State != TenantLifecycleState.Active ||
            initialTenant.UpdatedAtUtc > now ||
            canonical is null)
            return NeutralRegistrationReceipt.Acknowledged;

        var socketPeer = await peer.ReadVerifiedPeerAddressAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (socketPeer is null || socketPeer.Value.Length != 16 ||
            socketPeer.Value.Span.IndexOfAnyExcept((byte)0) < 0)
            throw new UnauthorizedAccessException("Trusted socket peer required.");

        // Prevent use of a stale or re-bound verified host before any write.
        var refreshed = await hosts.ResolveAsync(direct, cancellationToken);
        var latest = await tenants.FindByIdAsync(verified.TenantId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var finalNow = clock.GetUtcNow();
        InvitationSnapshot.RequireUtc(finalNow);
        if (finalNow < now)
            throw new InvalidOperationException("Registration request clock moved backwards.");
        if (refreshed != verified || latest != initialTenant ||
            finalNow < latest!.UpdatedAtUtc ||
            latest.State != TenantLifecycleState.Active)
            return NeutralRegistrationReceipt.Acknowledged;

        await recorder.RecordWithoutEnumerationAsync(
            verified.TenantId, canonical, socketPeer.Value, finalNow,
            cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        // Exactly the same singleton receipt for known and unknown accounts.
        // A durable intent is never an account grant or a session.
        return NeutralRegistrationReceipt.Acknowledged;
    }

    /// <summary>
    /// Conservative ASCII mailbox syntax: preserve the local part byte-for-
    /// byte, lowercase DNS only; no provider-specific alias or dot stripping.
    /// Never expose the normalized mailbox through error messages or logs.
    /// This is input validation only and does not prove email ownership.
    /// </summary>
    public static string? CanonicalMailboxOrNull(string? email)
    {
        if (email is null || email.Length is < 6 or > 254 ||
            !string.Equals(email, email.Trim(), StringComparison.Ordinal) ||
            email.Any(c => c < 33 || c > 126))
            return null;

        var at = email.IndexOf('@');
        if (at is < 1 or > 64 || at != email.LastIndexOf('@') ||
            at >= email.Length - 4)
            return null;
        var local = email[..at];
        if (local[0] == '.' || local[^1] == '.' || local.Contains("..", StringComparison.Ordinal) ||
            !local.All(c =>
                c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or
                     '.' or '_' or '+' or '-' or '%'))
            return null;
        var domain = email[(at + 1)..];
        var labels = domain.Split('.');
        if (labels.Length < 2 || labels.Any(label =>
            label.Length is < 1 or > 63 || label[0] == '-' || label[^1] == '-' ||
            !label.All(c => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or
                >= '0' and <= '9' or '-')) ||
            labels[^1].Length < 2 || !labels[^1].Any(c =>
                c is >= 'a' and <= 'z' or >= 'A' and <= 'Z'))
            return null;
        return string.Concat(local, "@", domain.ToLowerInvariant());
    }
}
