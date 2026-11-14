namespace Crecisol.Domain.Identity;

/// <summary>
/// C05-10 internal admission categories. Distinct per-tenant policies and
/// HMAC domains prevent a browser from selecting another action or tenant.
/// </summary>
public enum PreAuthRateLimitPurpose
{
    PasswordLogin = 1,
    PasswordRecovery = 2,
    MfaChallenge = 3
}

public enum PreAuthAdmissionStatus
{
    Admitted = 1,
    Limited = 2,
    Denied = 3
}

/// <summary>
/// Neutral disposition. "Accepted" is intentionally identical whether a
/// mailbox exists, password is correct, or identity is ambiguous. This is
/// NOT a session, bearer, authentication result or permission.
/// </summary>
public enum PreAuthPublicDisposition
{
    Accepted = 1,
    RateLimited = 2,
    Unavailable = 3
}

public enum PreAuthPrivateOutcome
{
    Verified = 1,
    Rejected = 2
}

/// <summary>
/// Purpose-specific, keyed HMAC-SHA256. The digest alone is not a bearer,
/// identity, tenant grant, session, IP or login credential.
/// </summary>
public sealed class PreAuthKeyedDigest
{
    private readonly byte[] value;
    private PreAuthKeyedDigest(byte[] bytes) => value=bytes;
    public static PreAuthKeyedDigest FromTrustedProtector(ReadOnlySpan<byte> bytes)
    {
        if(bytes.Length!=32 || bytes.IndexOfAnyExcept((byte)0)<0)
            throw new InvalidOperationException("Protected 256-bit rate-limit digest required.");
        return new PreAuthKeyedDigest(bytes.ToArray());
    }

    public byte[] CopyForTrustedLedger() => (byte[])value.Clone();
    public override string ToString() => "PreAuthKeyedDigest[redacted]";
}
