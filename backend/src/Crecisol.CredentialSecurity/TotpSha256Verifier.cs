using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Crecisol.CredentialSecurity;

/// <summary>
/// RFC 6238 HMAC-SHA256, 30-second step and six-digit MFA. Enrollment seed
/// custody and the monotonic counter MUST be provided by the protected
/// server storage adapter. No TOTP seed is embedded or logged here.
/// </summary>
public static class TotpSha256Verifier
{
    public const int IntervalSeconds = 30;
    public const int MaxPastSteps = 1;

    public static byte[] GenerateSeed() => RandomNumberGenerator.GetBytes(32);

    public static long StepAt(DateTimeOffset trustedUtc)
    {
        long unix = trustedUtc.ToUnixTimeSeconds();
        if (unix < 0) throw new ArgumentOutOfRangeException(nameof(trustedUtc));
        return unix / IntervalSeconds;
    }

    /// <summary>Eight-digit mode is solely for published RFC 6238 vectors.</summary>
    public static string ComputeCode(ReadOnlySpan<byte> seed, long step, int digits = 6)
    {
        if (seed.Length is < 20 or > 128 || seed.IndexOfAnyExcept((byte)0) < 0 ||
            step < 0 || digits is not (6 or 8))
            throw new InvalidOperationException("Protected TOTP seed, step and digits required.");
        Span<byte> counter = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counter, step);
        byte[] mac = HMACSHA256.HashData(seed, counter);
        try
        {
            int offset = mac[^1] & 15;
            int value = ((mac[offset] & 0x7f) << 24) |
                        (mac[offset + 1] << 16) |
                        (mac[offset + 2] << 8) |
                        mac[offset + 3];
            int divisor = digits == 6 ? 1_000_000 : 100_000_000;
            return (value % divisor).ToString("D" + digits, CultureInfo.InvariantCulture);
        }
        finally { CryptographicOperations.ZeroMemory(mac); }
    }

    /// <summary>
    /// A candidate is proof ONLY when its step is strictly greater than
    /// the last committed step. Caller MUST atomically persist the step
    /// when consuming the challenge or a concurrent replay is possible.
    /// Future steps are never accepted.
    /// </summary>
    public static bool TryVerify(ReadOnlySpan<byte> seed, string? candidate,
        DateTimeOffset trustedUtc, long lastCommittedStep, out long acceptedStep)
    {
        acceptedStep = -1;
        if (candidate is null || candidate.Length != 6 ||
            lastCommittedStep < -1 || seed.Length is < 20 or > 128 ||
            seed.IndexOfAnyExcept((byte)0) < 0)
            return false;
        foreach (char ch in candidate)
            if (ch is < '0' or > '9') return false;

        long now = StepAt(trustedUtc);
        byte[] supplied = Encoding.ASCII.GetBytes(candidate);
        try
        {
            for (long step = now; step >= now - MaxPastSteps && step >= 0; step--)
            {
                if (step <= lastCommittedStep) continue;
                byte[] expected = Encoding.ASCII.GetBytes(ComputeCode(seed, step));
                try
                {
                    if (CryptographicOperations.FixedTimeEquals(supplied, expected))
                    {
                        acceptedStep = step;
                        return true;
                    }
                }
                finally { CryptographicOperations.ZeroMemory(expected); }
            }
            return false;
        }
        finally { CryptographicOperations.ZeroMemory(supplied); }
    }
}
