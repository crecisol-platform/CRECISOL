using System.Security.Cryptography;
using Crecisol.Domain.Identity;
using Crecisol.Domain.Tenancy;

namespace Crecisol.Application.Identity;

/// <summary>
/// Envelope crypto is server-owned; the only persistent profile personal
/// text is authenticated ciphertext. No HTTP caller provides keys or AAD.
/// </summary>
public interface ITrustedPrivateProfileCipher
{
    byte[] EncryptForTenantActor(TenantId tenant, TenantActorId actor,
        PrivateDisplayName text);
    PrivateDisplayName DecryptForTenantActor(TenantId tenant, TenantActorId actor,
        ReadOnlySpan<byte> envelope);
}

public sealed class TrustedEncryptedPrivateProfile
{
    public TrustedEncryptedPrivateProfile(byte[] envelope, Guid? photoId, long revision)
    {
        if (revision < 1 || envelope is not { Length: >= 32 and <= 512 } ||
            photoId==Guid.Empty)
            throw new InvalidOperationException("Malformed protected profile result.");
        Ciphertext = (byte[])envelope.Clone();
        PhotoId = photoId;
        Revision = revision;
    }
    public byte[] Ciphertext { get; }
    public Guid? PhotoId { get; }
    public long Revision { get; }
    public override string ToString() => "TrustedEncryptedPrivateProfile[redacted]";
}

/// <summary>
/// MySQL rechecks original C05-04 versions under locks at each operation.
/// Only profile SELF; no guessed actor, role, branch, tenant or media key
/// supplied by browser. Photo IDs are revalidated against private VERIFIED
/// clean manifest records owned by exact (tenant, actor).
/// </summary>
public interface ITrustedPrivateProfileLedger
{
    Task<TrustedEncryptedPrivateProfile?> ReadSelfAsync(
        PreparedContextualLoginEvidence proof, CancellationToken ct);
    Task<bool> SaveSelfAsync(PreparedContextualLoginEvidence proof,
        ReadOnlyMemory<byte> encryptedName, Guid? trustedMediaId,
        long expectedRevision, CancellationToken ct);
    Task<bool> EraseSelfAsync(PreparedContextualLoginEvidence proof,
        long expectedRevision, CancellationToken ct);
}

/// <summary>
/// C05-09 internal-only self-service profile contracts. Reauthentication
/// occurs INSIDE every request via real C05-04 Argon2id and verified host.
/// This is deliberately NOT wired to product HTTP or live identities.
/// </summary>
public sealed class PrivateTenantProfileUseCases
{
    private readonly ITrustedImmediatePasswordReauthentication reauth;
    private readonly ITrustedPrivateProfileLedger profiles;
    private readonly ITrustedPrivateProfileCipher cipher;

    public PrivateTenantProfileUseCases(
        ITrustedImmediatePasswordReauthentication reauth,
        ITrustedPrivateProfileLedger profiles, ITrustedPrivateProfileCipher cipher)
    {
        this.reauth=reauth??throw new ArgumentNullException(nameof(reauth));
        this.profiles=profiles??throw new ArgumentNullException(nameof(profiles));
        this.cipher=cipher??throw new ArgumentNullException(nameof(cipher));
    }

    private async Task<PreparedContextualLoginEvidence?> FreshAsync(
        CanonicalLoginHandle mailbox, ReadOnlyMemory<char> password,
        VerifiedTenantHostHint host, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(mailbox);
        ArgumentNullException.ThrowIfNull(host);
        ct.ThrowIfCancellationRequested();
        var result=await reauth.VerifyFreshAsync(mailbox,password,host,ct);
        var proof=result?.Evidence;
        return result?.Status==ContextualLoginPreparationStatus.Ready &&
            proof is not null && proof.TenantId==host.TenantId &&
            proof.HostRevision==host.Revision && proof.HostRevision>0 &&
            proof.AccountRevision>0 && proof.CredentialRevision>0 &&
            proof.MembershipRevision>0 && proof.TenantCatalogRevision>0
            ? proof : null;
    }

    public async Task<PrivateTenantProfileView?> ReadSelfAsync(
        CanonicalLoginHandle mailbox, ReadOnlyMemory<char> password,
        VerifiedTenantHostHint host, CancellationToken ct)
    {
        var proof=await FreshAsync(mailbox,password,host,ct);
        if(proof is null) return null;
        var row=await profiles.ReadSelfAsync(proof,ct);
        if(row is null) return null;
        try
        {
            var name=cipher.DecryptForTenantActor(proof.TenantId,proof.ActorId,
                row.Ciphertext);
            return new PrivateTenantProfileView(name,row.PhotoId,row.Revision);
        }
        finally { CryptographicOperations.ZeroMemory(row.Ciphertext); }
    }

    public async Task<bool> SaveSelfAsync(
        CanonicalLoginHandle mailbox, ReadOnlyMemory<char> password,
        VerifiedTenantHostHint host, string? requestedName,
        Guid? requestedMediaId, long expectedRevision, CancellationToken ct)
    {
        if (expectedRevision<0 || requestedMediaId==Guid.Empty) return false;
        // Verify UI values before expensive Argon2id, without logging them.
        PrivateDisplayName name;
        try { name=PrivateDisplayName.Parse(requestedName); }
        catch(InvalidOperationException) { return false; }
        var proof=await FreshAsync(mailbox,password,host,ct);
        if(proof is null) return false;
        byte[] envelope=cipher.EncryptForTenantActor(proof.TenantId,proof.ActorId,name);
        try
        {
            return await profiles.SaveSelfAsync(proof,envelope,
                requestedMediaId,expectedRevision,ct);
        }
        finally { CryptographicOperations.ZeroMemory(envelope); }
    }

    public async Task<bool> EraseSelfAsync(
        CanonicalLoginHandle mailbox, ReadOnlyMemory<char> password,
        VerifiedTenantHostHint host, long expectedRevision, CancellationToken ct)
    {
        if (expectedRevision<1) return false;
        var proof=await FreshAsync(mailbox,password,host,ct);
        return proof is not null &&
            await profiles.EraseSelfAsync(proof,expectedRevision,ct);
    }

    public override string ToString() => "PrivateTenantProfileUseCases[redacted]";
}
