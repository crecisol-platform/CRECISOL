using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Crecisol.Application.Identity;
using Konscious.Security.Cryptography;

namespace Crecisol.Infrastructure.Identity;

/// <summary>
/// Opaque internal envelope for a trusted credential store. Never serialize
/// this object to a public API, log it or treat it as a session assertion.
/// </summary>
public sealed class PasswordVerifierEnvelope
{
    internal PasswordVerifierEnvelope(string encoded) => Encoded = encoded;

    private string Encoded { get; }

    // Only the future protected credential directory may persist this verifier.
    // It is not plaintext and it does not confer access to any account or tenant.
    public string ExportForTrustedCredentialStore() => Encoded;

    public override string ToString() => "PasswordVerifierEnvelope[redacted]";
}

/// <summary>
/// C05-03: real Argon2id PHC password-verifier primitive. No DI registration,
/// users, login, JWTs, sessions, tenancy grants, database or HTTP routes.
/// </summary>
public sealed class Argon2idPasswordHasher
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private readonly PasswordHashingPolicy _policy;
    private readonly ITrustedCompromisedPasswordScreen _compromiseScreen;

    public Argon2idPasswordHasher(
        PasswordHashingPolicy policy,
        ITrustedCompromisedPasswordScreen compromiseScreen)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        _compromiseScreen = compromiseScreen ??
            throw new ArgumentNullException(nameof(compromiseScreen));
    }

    public PasswordVerifierEnvelope CreateVerifier(ReadOnlySpan<char> candidate)
    {
        if (!TryEncode(candidate, _policy.MinimumRunes,
                _policy.MaximumRunes, out var password))
            throw new InvalidOperationException("Password rejected by server policy.");

        byte[]? salt = null;
        byte[]? digest = null;
        try
        {
            // No fallback to "allowed": the server-side breach check is
            // REQUIRED. Unavailable/unknown/throw => no new credential.
            if (_compromiseScreen.Check(candidate) !=
                PasswordCompromiseVerdict.Allowed)
                throw new InvalidOperationException(
                    "Password rejected by server policy.");

            salt = RandomNumberGenerator.GetBytes(16);
            digest = Derive(password, salt, _policy.MemoryKib,
                _policy.Iterations);
            var phc = "$argon2id$v=19$m=" +
                      _policy.MemoryKib.ToString(CultureInfo.InvariantCulture) +
                      ",t=" +
                      _policy.Iterations.ToString(CultureInfo.InvariantCulture) +
                      ",p=1$" + EncodeUnpadded(salt) +
                      "$" + EncodeUnpadded(digest);
            return new PasswordVerifierEnvelope(phc);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(password);
            if (salt is not null)
                CryptographicOperations.ZeroMemory(salt);
            if (digest is not null)
                CryptographicOperations.ZeroMemory(digest);
        }
    }

    /// <summary>
    /// Compare an internal stored verifier. A match is not a login, account
    /// identity, tenant authorization or membership evidence. The caller
    /// must implement all those gates in later C05 microphases.
    /// </summary>
    public PasswordHashCheck Verify(
        ReadOnlySpan<char> candidate, string? storedVerifier)
    {
        if (!TryParse(storedVerifier, out var stored))
            return PasswordHashCheck.Denied;

        byte[]? password = null;
        byte[]? derived = null;
        try
        {
            // Existing credentials longer than a newly configured maximum
            // remain verifiable for controlled upgrade, within the hard cap.
            if (!TryEncode(candidate, 1, PasswordHashingPolicy.MaxAcceptedRunes,
                    out password))
                return PasswordHashCheck.Denied;

            derived = Derive(password, stored.Salt, stored.MemoryKib,
                stored.Iterations);
            var matched = CryptographicOperations.FixedTimeEquals(
                derived, stored.Digest);
            return matched
                ? new PasswordHashCheck(true,
                    stored.MemoryKib < _policy.MemoryKib ||
                    stored.Iterations < _policy.Iterations)
                : PasswordHashCheck.Denied;
        }
        finally
        {
            if (password is not null)
                CryptographicOperations.ZeroMemory(password);
            if (derived is not null)
                CryptographicOperations.ZeroMemory(derived);
            CryptographicOperations.ZeroMemory(stored.Salt);
            CryptographicOperations.ZeroMemory(stored.Digest);
        }
    }

    public override string ToString() => "Argon2idPasswordHasher[redacted]";

    private static byte[] Derive(
        byte[] password, byte[] salt, int memoryKib, int iterations)
    {
        using var argon = new Argon2id(password)
        {
            Salt = salt,
            DegreeOfParallelism = 1,
            MemorySize = memoryKib,
            Iterations = iterations
        };
        return argon.GetBytes(32);
    }

    private static bool TryEncode(
        ReadOnlySpan<char> candidate, int minRunes, int maxRunes,
        out byte[] password)
    {
        password = Array.Empty<byte>();
        if (candidate.Length == 0 ||
            candidate.Length > PasswordHashingPolicy.MaxUtf8Bytes)
            return false;

        var length = 0;
        foreach (var rune in candidate.EnumerateRunes())
        {
            if (Rune.IsControl(rune) || ++length > maxRunes)
                return false;
        }
        if (length < minRunes)
            return false;

        try
        {
            var byteCount = StrictUtf8.GetByteCount(candidate);
            if (byteCount > PasswordHashingPolicy.MaxUtf8Bytes)
                return false;
            password = new byte[byteCount];
            _ = StrictUtf8.GetBytes(candidate, password);
            return true;
        }
        catch (EncoderFallbackException)
        {
            // If the input is malformed UTF-16, never retain a partial
            // encoding of the private candidate in a managed buffer.
            CryptographicOperations.ZeroMemory(password);
            password = Array.Empty<byte>();
            return false;
        }
    }

    private static string EncodeUnpadded(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=');

    private readonly record struct ParsedVerifier(
        int MemoryKib, int Iterations, byte[] Salt, byte[] Digest);

    private static bool TryParse(string? encoded, out ParsedVerifier parsed)
    {
        parsed = default;
        if (string.IsNullOrEmpty(encoded) ||
            encoded.Length > 160 || encoded.Length < 80)
            return false;

        var parts = encoded.Split('$');
        if (parts.Length != 6 ||
            parts[0] != "" ||
            parts[1] != "argon2id" ||
            parts[2] != "v=19" ||
            !parts[3].EndsWith(",p=1", StringComparison.Ordinal))
            return false;

        var parameters = parts[3].Split(',');
        if (parameters.Length != 3 ||
            !parameters[0].StartsWith("m=", StringComparison.Ordinal) ||
            !parameters[1].StartsWith("t=", StringComparison.Ordinal) ||
            parameters[2] != "p=1" ||
            !int.TryParse(parameters[0].AsSpan(2), NumberStyles.None,
                CultureInfo.InvariantCulture, out var memoryKib) ||
            !int.TryParse(parameters[1].AsSpan(2), NumberStyles.None,
                CultureInfo.InvariantCulture, out var iterations) ||
            memoryKib < PasswordHashingPolicy.Argon2MinimumMemoryKib ||
            memoryKib > PasswordHashingPolicy.Argon2MaximumMemoryKib ||
            memoryKib % 1024 != 0 ||
            iterations < PasswordHashingPolicy.Argon2MinimumIterations ||
            iterations > PasswordHashingPolicy.Argon2MaximumIterations ||
            parameters[0] != "m=" +
                memoryKib.ToString(CultureInfo.InvariantCulture) ||
            parameters[1] != "t=" +
                iterations.ToString(CultureInfo.InvariantCulture))
            return false;

        if (!TryDecodeExact(parts[4], 16, out var salt))
            return false;

        if (!TryDecodeExact(parts[5], 32, out var digest))
        {
            CryptographicOperations.ZeroMemory(salt);
            return false;
        }
        parsed = new ParsedVerifier(memoryKib, iterations, salt, digest);
        return true;
    }

    private static bool TryDecodeExact(
        string segment, int expectedBytes, out byte[] decoded)
    {
        decoded = Array.Empty<byte>();
        // Canonical unpadded PHC base64. Do not allow alternate encodings.
        if (segment.Length != ((expectedBytes + 2) / 3 * 4) -
                              ((3 - expectedBytes % 3) % 3))
            return false;

        foreach (var c in segment)
        {
            if (!(c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or
                        >= '0' and <= '9' or '+' or '/'))
                return false;
        }

        try
        {
            var candidate = Convert.FromBase64String(
                segment.PadRight((segment.Length + 3) / 4 * 4, '='));
            if (candidate.Length != expectedBytes ||
                EncodeUnpadded(candidate) != segment)
            {
                CryptographicOperations.ZeroMemory(candidate);
                return false;
            }
            decoded = candidate;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
