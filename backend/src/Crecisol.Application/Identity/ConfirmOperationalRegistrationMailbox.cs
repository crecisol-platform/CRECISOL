using Crecisol.Domain.Tenancy;

namespace Crecisol.Application.Identity;

/// <summary>
/// Confirms only a server-resolved tenant's one-time registration mailbox
/// proof. A successful proof is not an account, membership, or login grant.
/// </summary>
public interface ITrustedRegistrationMailboxProofLedger
{
    Task<bool> TryConfirmOnceAsync(TenantId verifiedTenant, Guid proofId,
        ReadOnlyMemory<byte> token, CancellationToken cancellationToken);
}
