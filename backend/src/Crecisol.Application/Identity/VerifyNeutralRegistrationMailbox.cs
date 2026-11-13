using System.Security.Cryptography;
using Crecisol.Application.Tenancy;
using Crecisol.Domain.Identity;
using Crecisol.Domain.Tenancy;

namespace Crecisol.Application.Identity;

/// <summary>
/// The sole trusted email-proof DB port. Implementations MUST verify tenant
/// state and exact retained intent in the same transaction as any transition.
/// A public caller cannot choose a tenant or recipient mailbox.
/// </summary>
public interface ITrustedEmailPossessionLedger
{
    Task<bool> TryIssueOnceAsync(
        TenantId tenant, Guid intentId, EmailPossessionDigest digest,
        DateTimeOffset nowUtc, DateTimeOffset expiresUtc,
        CancellationToken cancellationToken);

    Task<bool> TryVerifyOnceAsync(
        TenantId tenant, Guid intentId, EmailPossessionDigest suppliedDigest,
        DateTimeOffset nowUtc, CancellationToken cancellationToken);

    Task<bool> TryRevokeUndeliveredAsync(
        TenantId tenant, Guid intentId, EmailPossessionDigest expectedDigest,
        CancellationToken cancellationToken);
}

/// <summary>
/// Protected server-side sender, NOT a browser-chosen mailbox and NOT a
/// production email transport. Resolve the destination solely from the
/// encrypted, exact (tenant,intent) record, authenticating its AEAD envelope.
/// The secret must never be returned to the registration HTTP client.
/// </summary>
public interface ITrustedEmailPossessionDelivery
{
    Task DeliverToRecordedMailboxAsync(
        TenantId tenant, Guid intentId, ReadOnlyMemory<byte> secret,
        CancellationToken cancellationToken);
}

/// <summary>
/// C05-02 verification challenge coordinator. A challenge is NOT an account,
/// membership, credential, trusted user identity or authenticated session.
/// Public request/confirmation both return the SAME neutral receipt.
/// </summary>
public sealed class VerifyNeutralRegistrationMailbox
{
    private readonly ITrustedCurrentDirectHostReader directHost;
    private readonly ResolveVerifiedTenantHost verifiedHosts;
    private readonly ITrustedTenantCatalogReader tenants;
    private readonly ITrustedHostUtcClock clock;
    private readonly ITrustedEmailPossessionLedger ledger;
    private readonly ITrustedEmailPossessionDelivery delivery;

    public VerifyNeutralRegistrationMailbox(
        ITrustedCurrentDirectHostReader directHost,
        ResolveVerifiedTenantHost verifiedHosts,
        ITrustedTenantCatalogReader tenants,
        ITrustedHostUtcClock clock,
        ITrustedEmailPossessionLedger ledger,
        ITrustedEmailPossessionDelivery delivery)
    {
        this.directHost = directHost ?? throw new ArgumentNullException(nameof(directHost));
        this.verifiedHosts = verifiedHosts ?? throw new ArgumentNullException(nameof(verifiedHosts));
        this.tenants = tenants ?? throw new ArgumentNullException(nameof(tenants));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        this.delivery = delivery ?? throw new ArgumentNullException(nameof(delivery));
    }

    /// <summary>
    /// Only the opaque intent ID may be supplied by a client. No mailbox,
    /// tenant, SMTP address, role, source host or DSN is accepted as a grant.
    /// </summary>
    public async Task<NeutralRegistrationReceipt> RequestProofAsync(
        Guid intentId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var tenant = await ResolveActiveHostTenantAsync(cancellationToken);
        if (tenant is null || intentId == Guid.Empty)
            return NeutralRegistrationReceipt.Acknowledged;

        var now = clock.GetUtcNow();
        InvitationSnapshot.RequireUtc(now);
        using var challenge = EphemeralEmailPossessionCode.Generate();
        var digest = challenge.CreateDigest(tenant, intentId);
        if (!await ledger.TryIssueOnceAsync(
                tenant, intentId, digest, now, now.AddMinutes(10), cancellationToken))
            return NeutralRegistrationReceipt.Acknowledged;

        // Do not deliver on rebind/suspension between the issue and the send.
        var refreshedTenant = await ResolveActiveHostTenantAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (refreshedTenant != tenant)
        {
            await ledger.TryRevokeUndeliveredAsync(
                tenant, intentId, digest, cancellationToken);
            return NeutralRegistrationReceipt.Acknowledged;
        }

        var bytes = challenge.TransferToTrustedDeliveryOnce();
        try
        {
            // Sender only sees a stored, authenticated (tenant,intent)
            // mailbox; it is forbidden to log, persist or reflect this code.
            await delivery.DeliverToRecordedMailboxAsync(
                tenant, intentId, bytes, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await ledger.TryRevokeUndeliveredAsync(
                tenant, intentId, digest, CancellationToken.None);
            throw;
        }
        catch (Exception)
        {
            // A failed channel never creates a proof. If cleanup itself
            // fails, the unknown code has not been returned to the browser
            // and expires; no account or grant can be produced.
            try
            {
                await ledger.TryRevokeUndeliveredAsync(
                    tenant, intentId, digest, CancellationToken.None);
            }
            catch (Exception) { /* fail closed: ephemeral code is wiped */ }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }

        return NeutralRegistrationReceipt.Acknowledged;
    }

    /// <summary>
    /// Confirmation is deliberately non-enumerating and returns no proof flag.
    /// A later protected backend component must check proof state separately;
    /// this operation never authorizes tenancy or creates a session.
    /// </summary>
    public async Task<NeutralRegistrationReceipt> ConfirmProofAsync(
        Guid intentId, string? hexadecimalCode, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var tenant = await ResolveActiveHostTenantAsync(cancellationToken);
        if (tenant is null || intentId == Guid.Empty || hexadecimalCode is null ||
            hexadecimalCode.Length != 64)
            return NeutralRegistrationReceipt.Acknowledged;

        byte[] secret;
        try
        {
            secret = Convert.FromHexString(hexadecimalCode);
        }
        catch (FormatException)
        {
            return NeutralRegistrationReceipt.Acknowledged;
        }

        try
        {
            var digest = EmailPossessionDigest.FromToken(tenant, intentId, secret);
            _ = await ledger.TryVerifyOnceAsync(
                tenant, intentId, digest, clock.GetUtcNow(), cancellationToken);
            return NeutralRegistrationReceipt.Acknowledged;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    private async Task<TenantId?> ResolveActiveHostTenantAsync(
        CancellationToken cancellationToken)
    {
        var host = await directHost.ReadCurrentDirectHostAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (host is null)
            throw new UnauthorizedAccessException("Direct host required.");
        var verified = await verifiedHosts.ResolveAsync(host, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = await tenants.FindByIdAsync(
            verified.TenantId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var now = clock.GetUtcNow();
        InvitationSnapshot.RequireUtc(now);
        if (snapshot is null || snapshot.TenantId != verified.TenantId ||
            snapshot.AddressMode != verified.AddressMode ||
            snapshot.State != TenantLifecycleState.Active ||
            snapshot.UpdatedAtUtc > now)
            return null;
        var again = await verifiedHosts.ResolveAsync(host, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (again != verified)
            return null;
        return verified.TenantId;
    }
}
