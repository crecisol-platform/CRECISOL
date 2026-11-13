using System.Security.Cryptography;
using System.Text;
using Crecisol.Domain.Tenancy;

namespace Crecisol.Domain.Identity;

/// <summary>
/// 256-bit independent email-possession challenge. Not an invitation token,
/// credential, identity, membership, account grant, or login proof. This
/// purpose and tenant/intent binding MUST be independently verified.
/// </summary>
public sealed class EmailPossessionDigest
{
    private static readonly byte[] Purpose =
        Encoding.ASCII.GetBytes("CRECISOL/C05-02/mailbox-possession/v1/");
    private readonly byte[] digest;

    private EmailPossessionDigest(byte[] digest) => this.digest = digest;

    public static EmailPossessionDigest FromToken(
        TenantId trustedTenant, Guid intentId, ReadOnlySpan<byte> rawToken)
    {
        ArgumentNullException.ThrowIfNull(trustedTenant);
        if (intentId == Guid.Empty || rawToken.Length != 32)
            throw new InvalidOperationException("A bound 256-bit email challenge is required.");

        var input = new byte[Purpose.Length + 16 + 16 + 32];
        try
        {
            Purpose.CopyTo(input, 0);
            trustedTenant.Value.ToByteArray(bigEndian: true)
                .CopyTo(input, Purpose.Length);
            intentId.ToByteArray(bigEndian: true)
                .CopyTo(input, Purpose.Length + 16);
            rawToken.CopyTo(input.AsSpan(Purpose.Length + 32));
            return new EmailPossessionDigest(SHA256.HashData(input));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(input);
        }
    }

    public static EmailPossessionDigest FromProtectedStorage(ReadOnlySpan<byte> stored)
    {
        if (stored.Length != 32)
            throw new InvalidOperationException("Protected email digest must be 32 bytes.");
        var clone = stored.ToArray();
        if (!clone.Any(x => x != 0))
        {
            CryptographicOperations.ZeroMemory(clone);
            throw new InvalidOperationException("Protected email digest unavailable.");
        }
        return new EmailPossessionDigest(clone);
    }

    public bool Matches(
        TenantId tenant, Guid intentId, ReadOnlySpan<byte> token)
    {
        if (token.Length != 32 || intentId == Guid.Empty) return false;
        var candidate = FromToken(tenant, intentId, token);
        return CryptographicOperations.FixedTimeEquals(digest, candidate.digest);
    }

    /// <summary>Only a backend restricted DB adapter may obtain the digest.</summary>
    public byte[] CopyForProtectedDatabase() => (byte[])digest.Clone();

    public override string ToString() => "EmailPossessionDigest[redacted]";
}

/// <summary>
/// CSPRNG secret, transferred ONCE to the backend's trusted mailbox-delivery
/// port and cleared immediately after delivery. Never put in logs, HTTP ACK,
/// DB rows, public DTOs, client-side code or a persistent outbox in cleartext.
/// </summary>
public sealed class EphemeralEmailPossessionCode : IDisposable
{
    private byte[]? secret;

    private EphemeralEmailPossessionCode(byte[] secret) => this.secret = secret;

    public static EphemeralEmailPossessionCode Generate() =>
        new(RandomNumberGenerator.GetBytes(32));

    public EmailPossessionDigest CreateDigest(TenantId tenant, Guid intentId)
    {
        var current = Volatile.Read(ref secret);
        if (current is null)
            throw new InvalidOperationException("Email proof code is already transferred.");
        return EmailPossessionDigest.FromToken(tenant, intentId, current);
    }

    public byte[] TransferToTrustedDeliveryOnce() =>
        Interlocked.Exchange(ref secret, null) ??
        throw new InvalidOperationException("Email proof code was already transferred.");

    public void Dispose()
    {
        var current = Interlocked.Exchange(ref secret, null);
        if (current is not null)
            CryptographicOperations.ZeroMemory(current);
    }

    public override string ToString() => "EphemeralEmailPossessionCode[redacted]";
}
