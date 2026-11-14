using System.Security.Cryptography;
using System.Text;

namespace Crecisol.CredentialSecurity;

/// <summary>
/// Backup codes are NOT authenticators for private access: they may only
/// request a replacement PENDING enrollment following fresh password proof.
/// Storage must atomically consume one HMAC digest and never reactivate codes.
/// The key is server-held, separate from login, recovery and mailbox HMAC.
/// </summary>
public sealed class MfaRecoveryCodeProtector : IDisposable
{
    private const string Prefix = "cm8_";
    private readonly byte[] key;
    private bool disposed;

    public MfaRecoveryCodeProtector(ReadOnlySpan<byte> serverHeldKey)
    {
        if (serverHeldKey.Length is < 32 or > 64 ||
            serverHeldKey.IndexOfAnyExcept((byte)0) < 0)
            throw new InvalidOperationException("Independent MFA recovery HMAC key required.");
        key = serverHeldKey.ToArray();
    }

    public string GenerateForTrustedOneTimeDeliveryOnly()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        byte[] entropy = RandomNumberGenerator.GetBytes(32);
        try
        {
            return Prefix + Convert.ToBase64String(entropy).TrimEnd('=')
                .Replace('+', '-').Replace('/', '_');
        }
        finally { CryptographicOperations.ZeroMemory(entropy); }
    }

    public byte[]? TryDigest(string? untrustedCode)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (untrustedCode is null || untrustedCode.Length != 47 ||
            !untrustedCode.StartsWith(Prefix, StringComparison.Ordinal))
            return null;
        string code = untrustedCode[Prefix.Length..];
        foreach (char c in code)
            if (c is not (>= 'a' and <= 'z' or >= 'A' and <= 'Z' or
                         >= '0' and <= '9' or '-' or '_'))
                return null;
        byte[]? bytes = null;
        byte[]? original = null;
        try
        {
            bytes = Convert.FromBase64String(
                code.Replace('-', '+').Replace('_', '/') + "=");
            if (bytes.Length != 32 || code != Convert.ToBase64String(bytes)
                    .TrimEnd('=').Replace('+', '-').Replace('/', '_'))
                return null;
            original = Encoding.ASCII.GetBytes(untrustedCode);
            return HMACSHA256.HashData(key, original);
        }
        catch (FormatException) { return null; }
        finally
        {
            if (bytes is not null) CryptographicOperations.ZeroMemory(bytes);
            if (original is not null) CryptographicOperations.ZeroMemory(original);
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        CryptographicOperations.ZeroMemory(key);
        disposed = true;
        GC.SuppressFinalize(this);
    }

    public override string ToString() => "MfaRecoveryCodeProtector[redacted]";
}
