using System.Security.Cryptography;
using System.Text;
using Crecisol.Application.Identity;

namespace Crecisol.CredentialSecurity;

/// <summary>Integrity-checked, pinned public baseline, evaluated entirely
/// offline. This is a finite baseline, not a claim of complete breach coverage.
/// Missing/tampered resources prevent startup; candidates never leave the host.</summary>
public sealed class KnownCompromisedPasswordScreen : ITrustedCompromisedPasswordScreen
{
    private readonly HashSet<string> hashes;
    public KnownCompromisedPasswordScreen()
    {
        using var resource = typeof(KnownCompromisedPasswordScreen).Assembly.GetManifestResourceStream(
            "Crecisol.CredentialSecurity.Resources.compromised-password-sha256.txt")
            ?? throw new InvalidOperationException("Password screen unavailable.");
        using var bytes = new MemoryStream(); resource.CopyTo(bytes);
        var data = bytes.ToArray();
        if (Convert.ToHexString(SHA256.HashData(data)) != "22EA3343CC984812AA0265A938B79D51F3536678BD6E6A799899C29543EB0420")
            throw new InvalidOperationException("Password screen integrity failure.");
        hashes = new HashSet<string>(Encoding.ASCII.GetString(data).Split('\n', StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);
        if (hashes.Count != 10001 || hashes.Any(h => h.Length != 64 || !h.All(Uri.IsHexDigit)))
            throw new InvalidOperationException("Password screen invalid.");
        // Retain the previous bounded local policy's known compromised values.
        foreach (var value in new[] { "password123456", "123456789012345", "qwerty123456789", "letmein12345678" })
            hashes.Add(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))));
    }

    public PasswordCompromiseVerdict Check(ReadOnlySpan<char> candidate)
    {
        if (candidate.Length < PasswordHashingPolicy.MinAcceptedRunes) return PasswordCompromiseVerdict.Compromised;
        var bytes = new byte[Encoding.UTF8.GetByteCount(candidate)];
        try
        {
            Encoding.UTF8.GetBytes(candidate, bytes);
            return hashes.Contains(Convert.ToHexString(SHA256.HashData(bytes)))
                ? PasswordCompromiseVerdict.Compromised : PasswordCompromiseVerdict.Allowed;
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    public override string ToString() => "KnownCompromisedPasswordScreen[public-pinned-baseline]";
}
