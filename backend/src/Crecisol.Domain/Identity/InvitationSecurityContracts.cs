using System.Security.Cryptography;
using System.Text;
using Crecisol.Domain.Tenancy;

namespace Crecisol.Domain.Identity;

// C05-02 INTERNAL contracts: no HTTP signup, login, persistent SQL, session or grant.

public sealed record InvitationId
{
    private InvitationId(Guid value) => Value = value;
    public Guid Value { get; }
    public static InvitationId From(Guid value) =>
        value == Guid.Empty ? throw new InvalidOperationException("Invitation ID required.")
            : new InvitationId(value);
    public override string ToString() => "InvitationId[redacted]";
}

public sealed record RecipientChannelBindingId
{
    private RecipientChannelBindingId(Guid value) => Value = value;
    public Guid Value { get; }
    public static RecipientChannelBindingId From(Guid value) =>
        value == Guid.Empty ? throw new InvalidOperationException("Recipient binding required.")
            : new RecipientChannelBindingId(value);
    public override string ToString() => "RecipientChannelBindingId[redacted]";
}

public enum InvitationLifecycleState
{
    Pending = 1,
    Claimed = 2,
    Revoked = 3
}

/// <summary>
/// SHA-256 of a 256-bit CSPRNG invitation code with purpose separation.
/// A token hash is NOT a password verifier and never grants a user session.
/// Only a future protected persistence adapter may copy the digest to storage.
/// </summary>
public sealed class InvitationTokenDigest
{
    private static readonly byte[] DomainTag =
        Encoding.ASCII.GetBytes("CRECISOL/C05-02/invitation/v1/");
    private readonly byte[] digest;

    private InvitationTokenDigest(byte[] digest) => this.digest = digest;

    public static InvitationTokenDigest FromToken(ReadOnlySpan<byte> raw)
    {
        if (raw.Length != 32)
            throw new InvalidOperationException("Invitation code must contain 256 bits.");
        var message = new byte[DomainTag.Length + raw.Length];
        DomainTag.CopyTo(message, 0);
        raw.CopyTo(message.AsSpan(DomainTag.Length));
        try
        {
            return new InvitationTokenDigest(SHA256.HashData(message));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(message);
        }
    }

    /// <summary>
    /// Reconstitutes ONLY the 32-byte digest read by a restricted backend
    /// adapter, never the original invitation bearer. Rejects missing or
    /// malformed protected persistence data and does not expose the bytes.
    /// </summary>
    public static InvitationTokenDigest RestoreFromProtectedPersistence(
        ReadOnlySpan<byte> storedDigest)
    {
        if (storedDigest.Length != 32)
            throw new InvalidOperationException("Invalid protected invitation digest.");
        var copy = storedDigest.ToArray();
        var nonZero = 0;
        foreach (var item in copy)
            nonZero |= item;
        if (nonZero == 0)
        {
            CryptographicOperations.ZeroMemory(copy);
            throw new InvalidOperationException("Invalid protected invitation digest.");
        }
        return new InvitationTokenDigest(copy);
    }

    public bool Matches(ReadOnlySpan<byte> candidate)
    {
        if (candidate.Length != 32)
            return false;
        var computed = FromToken(candidate);
        return CryptographicOperations.FixedTimeEquals(digest, computed.digest);
    }

    public bool SameDigestAs(InvitationTokenDigest other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return CryptographicOperations.FixedTimeEquals(digest, other.digest);
    }

    // Explicit privileged adapter boundary. Never serialize/log this buffer.
    public byte[] CopyForProtectedPersistenceAdapter() => (byte[])digest.Clone();

    public override string ToString() => "InvitationTokenDigest[redacted]";
}

/// <summary>
/// Raw CSPRNG capability is transient, disposable, non-serializable by contract.
/// Ownership of the raw bytes can be transferred ONCE to a trusted sender;
/// sender must zero its owned buffer. A digest alone can be persisted.
/// </summary>
public sealed class EphemeralInvitationCode : IDisposable
{
    private byte[]? secret;
    private EphemeralInvitationCode(byte[] secret) => this.secret = secret;
    public static EphemeralInvitationCode Generate() =>
        new(RandomNumberGenerator.GetBytes(32));

    public InvitationTokenDigest CreateDigest()
    {
        var bytes = Volatile.Read(ref secret);
        if (bytes is null)
            throw new InvalidOperationException("Invitation capability consumed.");
        return InvitationTokenDigest.FromToken(bytes);
    }

    public byte[] TransferToTrustedSenderOnce()
    {
        var bytes = Interlocked.Exchange(ref secret, null);
        if (bytes is null)
            throw new InvalidOperationException("Invitation capability already transferred.");
        return bytes;
    }

    public void Dispose()
    {
        var bytes = Interlocked.Exchange(ref secret, null);
        if (bytes is not null)
            CryptographicOperations.ZeroMemory(bytes);
    }

    public override string ToString() => "EphemeralInvitationCode[redacted]";
}

public sealed record InvitationIssuePolicyDecision
{
    private InvitationIssuePolicyDecision(bool allowed, TimeSpan lifetime, long termsVersion)
    {
        Allowed = allowed;
        Lifetime = lifetime;
        TermsVersion = termsVersion;
    }

    public bool Allowed { get; }
    public TimeSpan Lifetime { get; }
    public long TermsVersion { get; }

    public static InvitationIssuePolicyDecision FromTrustedPolicy(
        bool allowed, TimeSpan lifetime, long termsVersion)
    {
        if (termsVersion < 1 || lifetime < TimeSpan.FromMinutes(1) ||
            lifetime > TimeSpan.FromDays(3))
            throw new InvalidOperationException("Invalid server invitation policy bounds.");
        return new InvitationIssuePolicyDecision(allowed, lifetime, termsVersion);
    }

    public override string ToString() => "InvitationIssuePolicyDecision[redacted]";
}

/// <summary>
/// Stored metadata only: independent tenant, issuer, recipient channel, terms
/// version, revision, lifecycle and digest. Profile is not a permission.
/// Issuing Owner/SuperAdmin invitations is categorically unsupported here.
/// </summary>
public sealed record InvitationSnapshot
{
    private InvitationSnapshot(
        InvitationId id, TenantId tenant, TenantActorId issuedBy,
        RecipientChannelBindingId recipient, AccountTenantProfileKind profile,
        InvitationLifecycleState state, InvitationTokenDigest digest,
        long revision, long termsVersion,
        DateTimeOffset issuedUtc, DateTimeOffset changedUtc, DateTimeOffset expiresUtc)
    {
        Id = id; TenantId = tenant; IssuedBy = issuedBy; Recipient = recipient;
        Profile = profile; State = state; Digest = digest;
        Revision = revision; TermsVersion = termsVersion;
        IssuedUtc = issuedUtc; ChangedUtc = changedUtc; ExpiresUtc = expiresUtc;
    }

    public InvitationId Id { get; }
    public TenantId TenantId { get; }
    public TenantActorId IssuedBy { get; }
    public RecipientChannelBindingId Recipient { get; }
    public AccountTenantProfileKind Profile { get; }
    public InvitationLifecycleState State { get; }
    public InvitationTokenDigest Digest { get; }
    public long Revision { get; }
    public long TermsVersion { get; }
    public DateTimeOffset IssuedUtc { get; }
    public DateTimeOffset ChangedUtc { get; }
    public DateTimeOffset ExpiresUtc { get; }

    public static InvitationSnapshot RestoreFromTrustedInvitationStore(
        InvitationId id, TenantId tenant, TenantActorId issuedBy,
        RecipientChannelBindingId recipient, AccountTenantProfileKind profile,
        InvitationLifecycleState state, InvitationTokenDigest digest,
        long revision, long termsVersion,
        DateTimeOffset issuedUtc, DateTimeOffset changedUtc, DateTimeOffset expiresUtc)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(tenant);
        ArgumentNullException.ThrowIfNull(issuedBy);
        ArgumentNullException.ThrowIfNull(recipient);
        ArgumentNullException.ThrowIfNull(digest);
        if (profile is not (AccountTenantProfileKind.Employee or AccountTenantProfileKind.Consumer) ||
            !Enum.IsDefined(state) || revision <= 0 || termsVersion <= 0)
            throw new InvalidOperationException("Invalid invitation kind, lifecycle or version.");
        RequireUtc(issuedUtc); RequireUtc(changedUtc); RequireUtc(expiresUtc);
        if (issuedUtc > changedUtc || expiresUtc <= issuedUtc ||
            (state is InvitationLifecycleState.Pending or InvitationLifecycleState.Claimed &&
             changedUtc >= expiresUtc))
            throw new InvalidOperationException("Invalid invitation time window.");
        return new InvitationSnapshot(id, tenant, issuedBy, recipient,
            profile, state, digest, revision, termsVersion, issuedUtc, changedUtc, expiresUtc);
    }

    public bool IsPendingAtUtc(DateTimeOffset now)
    {
        RequireUtc(now);
        return State == InvitationLifecycleState.Pending &&
            IssuedUtc <= now && ChangedUtc <= now && now < ExpiresUtc;
    }

    public static void RequireUtc(DateTimeOffset now)
    {
        if (now == default || now.Offset != TimeSpan.Zero)
            throw new InvalidOperationException("Invitation time must be UTC.");
    }

    public override string ToString() => "InvitationSnapshot[redacted]";
}

/// <summary>
/// This is a synthetic trusted-channel assertion, NOT a real email verification,
/// session or identity provider. An adapter must validate ownership and expiry.
/// </summary>
public sealed record VerifiedRecipientChannel
{
    private VerifiedRecipientChannel(TenantActorId actor,
        RecipientChannelBindingId recipient, long revision,
        DateTimeOffset verifiedUtc, DateTimeOffset expiresUtc)
    {
        ActorId = actor; Recipient = recipient; Revision = revision;
        VerifiedUtc = verifiedUtc; ExpiresUtc = expiresUtc;
    }

    public TenantActorId ActorId { get; }
    public RecipientChannelBindingId Recipient { get; }
    public long Revision { get; }
    public DateTimeOffset VerifiedUtc { get; }
    public DateTimeOffset ExpiresUtc { get; }

    public static VerifiedRecipientChannel FromTrustedBackendVerification(
        TenantActorId actor, RecipientChannelBindingId recipient, long revision,
        DateTimeOffset verifiedUtc, DateTimeOffset expiresUtc)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(recipient);
        InvitationSnapshot.RequireUtc(verifiedUtc);
        InvitationSnapshot.RequireUtc(expiresUtc);
        if (revision <= 0 || expiresUtc <= verifiedUtc)
            throw new InvalidOperationException("Invalid trusted recipient evidence.");
        return new VerifiedRecipientChannel(actor, recipient, revision, verifiedUtc, expiresUtc);
    }

    public bool IsValidAtUtc(DateTimeOffset now)
    {
        InvitationSnapshot.RequireUtc(now);
        return VerifiedUtc <= now && now < ExpiresUtc;
    }

    public override string ToString() => "VerifiedRecipientChannel[redacted]";
}

/// <summary>Recorded versioned acceptance from protected backend, never query/body alone.</summary>
public sealed record VerifiedInvitationTermsAcceptance
{
    private VerifiedInvitationTermsAcceptance(
        TenantId tenant, RecipientChannelBindingId recipient, long termsVersion,
        DateTimeOffset acceptedUtc)
    {
        TenantId = tenant; Recipient = recipient;
        TermsVersion = termsVersion; AcceptedUtc = acceptedUtc;
    }

    public TenantId TenantId { get; }
    public RecipientChannelBindingId Recipient { get; }
    public long TermsVersion { get; }
    public DateTimeOffset AcceptedUtc { get; }

    public static VerifiedInvitationTermsAcceptance FromTrustedConsentLedger(
        TenantId tenant, RecipientChannelBindingId recipient,
        long termsVersion, DateTimeOffset acceptedUtc)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        ArgumentNullException.ThrowIfNull(recipient);
        InvitationSnapshot.RequireUtc(acceptedUtc);
        if (termsVersion <= 0)
            throw new InvalidOperationException("Terms version must be positive.");
        return new VerifiedInvitationTermsAcceptance(tenant, recipient,
            termsVersion, acceptedUtc);
    }

    public override string ToString() => "VerifiedInvitationTermsAcceptance[redacted]";
}

/// <summary>Uniform, non-enumerating public-facing response TYPE. No PII.</summary>
public sealed class NeutralRegistrationReceipt
{
    private NeutralRegistrationReceipt() { }
    public static NeutralRegistrationReceipt Acknowledged { get; } = new();
    public string Code => "REQUEST_ACCEPTED_IF_ELIGIBLE";
    public override string ToString() => "NeutralRegistrationReceipt[redacted]";
}
