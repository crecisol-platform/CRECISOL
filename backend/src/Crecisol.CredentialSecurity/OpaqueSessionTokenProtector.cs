using System.Security.Cryptography;
using System.Text;
using Crecisol.Application.Identity;

namespace Crecisol.CredentialSecurity;

/// <summary>
/// C05-05 backend-only 256-bit unpredictable opaque tokens. A separate
/// 256-bit+ protected server key creates HMAC-SHA256 persistence indexes.
/// The key must come from protected runtime configuration, never source or
/// tenant IDs. This adapter is NOT registered in the production API.
/// </summary>
public sealed class OpaqueSessionTokenProtector : ITrustedOpaqueSessionTokenProtector,
    IDisposable
{
    private const string Prefix = "cs5_";
    private const int TokenBytes = 32;
    private readonly byte[] key;
    private bool disposed;

    public OpaqueSessionTokenProtector(ReadOnlySpan<byte> serverKey)
    {
        if (serverKey.Length is < 32 or > 64 ||
            serverKey.IndexOfAnyExcept((byte)0) < 0)
            throw new InvalidOperationException("Protected random session HMAC key required.");
        key = serverKey.ToArray();
    }

    public IssuedSessionToken GenerateFresh()
    {
        ThrowIfDisposed();
        var random = RandomNumberGenerator.GetBytes(TokenBytes);
        try
        {
            return IssuedSessionToken.FromTrustedProtector(Prefix + ToBase64Url(random));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(random);
        }
    }

    public SessionTokenDigest? TryDigest(string? token)
    {
        ThrowIfDisposed();
        // 32 raw bytes => exactly 43 unpadded base64url ASCII characters.
        if (token is null || token.Length != Prefix.Length + 43 ||
            !token.StartsWith(Prefix, StringComparison.Ordinal))
            return null;

        var encoded = token.AsSpan(Prefix.Length);
        if (!encoded.ToString().All(c => c is >= 'A' and <= 'Z'
                or >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_'))
            return null;

        byte[]? decoded = null;
        byte[]? raw = null;
        byte[]? mac = null;
        try
        {
            var padded = encoded.ToString().Replace('-', '+').Replace('_', '/') + "=";
            decoded = Convert.FromBase64String(padded);
            if (decoded.Length != TokenBytes ||
                ToBase64Url(decoded) != encoded.ToString())
                return null;

            raw = Encoding.ASCII.GetBytes(token);
            mac = HMACSHA256.HashData(key, raw);
            return SessionTokenDigest.FromTrustedTokenProtector(mac);
        }
        catch (FormatException)
        {
            return null;
        }
        finally
        {
            if (decoded is not null) CryptographicOperations.ZeroMemory(decoded);
            if (raw is not null) CryptographicOperations.ZeroMemory(raw);
            if (mac is not null) CryptographicOperations.ZeroMemory(mac);
        }
    }

    private static string ToBase64Url(ReadOnlySpan<byte> value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }

    public void Dispose()
    {
        if (disposed) return;
        CryptographicOperations.ZeroMemory(key);
        disposed = true;
        GC.SuppressFinalize(this);
    }

    public override string ToString() => "OpaqueSessionTokenProtector[redacted]";
}
