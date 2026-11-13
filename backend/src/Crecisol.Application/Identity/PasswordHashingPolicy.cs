namespace Crecisol.Application.Identity;

// C05-03: password quality and KDF resource boundaries only.
// This policy cannot authenticate anyone or grant a tenant membership.
public enum PasswordCompromiseVerdict
{
    Allowed = 1,
    Compromised = 2,
    Unavailable = 3
}

/// <summary>
/// Trusted backend-only screening port. A production composition must provide
/// a real, protected, current compromised-password screen; NEVER allow on outage.
/// No browser-supplied verdict, no logs and no plaintext persistence.
/// </summary>
public interface ITrustedCompromisedPasswordScreen
{
    PasswordCompromiseVerdict Check(ReadOnlySpan<char> candidate);
}

/// <summary>
/// Immutable, versioned server-side policy. Administrative configuration may
/// raise values within safe bounds, never reduce the security floor.
/// No business prices, tenant permissions, JWTs or login here.
/// </summary>
public sealed class PasswordHashingPolicy
{
    public const int MinAcceptedRunes = 15;
    public const int MinAcceptedMaximum = 64;
    public const int MaxAcceptedRunes = 256;
    public const int MaxUtf8Bytes = 1024;
    public const int Argon2MinimumMemoryKib = 19456; // 19 MiB
    public const int Argon2MaximumMemoryKib = 65536; // 64 MiB
    public const int Argon2MinimumIterations = 2;
    public const int Argon2MaximumIterations = 4;

    private PasswordHashingPolicy(
        int minimumRunes, int maximumRunes, int memoryKib, int iterations,
        long policyRevision)
    {
        MinimumRunes = minimumRunes;
        MaximumRunes = maximumRunes;
        MemoryKib = memoryKib;
        Iterations = iterations;
        PolicyRevision = policyRevision;
    }

    public int MinimumRunes { get; }
    public int MaximumRunes { get; }
    public int MemoryKib { get; }
    public int Iterations { get; }
    public int Parallelism => 1;
    public long PolicyRevision { get; }

    public static PasswordHashingPolicy Default { get; } =
        Create(15, 128, 19456, 2, 1);

    public static PasswordHashingPolicy Create(
        int minimumRunes, int maximumRunes, int memoryKib, int iterations,
        long policyRevision)
    {
        if (minimumRunes < MinAcceptedRunes ||
            minimumRunes > MaxAcceptedRunes ||
            maximumRunes < MinAcceptedMaximum ||
            maximumRunes > MaxAcceptedRunes ||
            maximumRunes < minimumRunes ||
            memoryKib < Argon2MinimumMemoryKib ||
            memoryKib > Argon2MaximumMemoryKib ||
            memoryKib % 1024 != 0 ||
            iterations < Argon2MinimumIterations ||
            iterations > Argon2MaximumIterations ||
            policyRevision < 1)
            throw new InvalidOperationException(
                "Credential policy violates approved security limits.");

        return new PasswordHashingPolicy(
            minimumRunes, maximumRunes, memoryKib, iterations, policyRevision);
    }

    public override string ToString() => "PasswordHashingPolicy[redacted]";
}

/// <summary>
/// Result of comparing a candidate with an internal verifier. This is
/// NOT authentication evidence, a session, a bearer credential or a grant.
/// </summary>
public readonly record struct PasswordHashCheck(bool Matched, bool NeedsRehash)
{
    public static PasswordHashCheck Denied => new(false, false);
}
