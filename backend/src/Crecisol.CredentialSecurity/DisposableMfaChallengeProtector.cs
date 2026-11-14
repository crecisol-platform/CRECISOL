using System.Security.Cryptography;
using System.Text;
using Crecisol.Application.Identity;

namespace Crecisol.CredentialSecurity;

/// <summary>
/// Only for C05-08 disposable integration tests; independent server-only
/// 256-bit HMAC key, purpose prefix and strict canonical base64url encoding.
/// Never an authorization credential, login cookie or productive API token.
/// </summary>
public sealed class DisposableMfaChallengeProtector :
    ITrustedDisposableMfaChallengeProtector, IDisposable
{
    private readonly byte[] key;
    private bool disposed;
    private const string Prefix = "ma8_";

    public DisposableMfaChallengeProtector(ReadOnlySpan<byte> keyMaterial)
    {
        if (keyMaterial.Length is < 32 or > 64 ||
            keyMaterial.IndexOfAnyExcept((byte)0) < 0)
            throw new InvalidOperationException("Independent MFA challenge key required.");
        key=keyMaterial.ToArray();
    }

    public string GenerateForFixtureOnly()
    {
        ObjectDisposedException.ThrowIf(disposed,this);
        byte[] bytes=RandomNumberGenerator.GetBytes(32);
        try { return Prefix + ToBase64Url(bytes); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    public byte[]? TryDigest(string? token)
    {
        ObjectDisposedException.ThrowIf(disposed,this);
        byte[]? source=TryChallengeBytes(token);
        if (source is null) return null;
        byte[] raw=Encoding.ASCII.GetBytes(token!);
        try { return HMACSHA256.HashData(key,raw); }
        finally
        {
            CryptographicOperations.ZeroMemory(raw);
            CryptographicOperations.ZeroMemory(source);
        }
    }

    public byte[]? TryChallengeBytes(string? token)
    {
        ObjectDisposedException.ThrowIf(disposed,this);
        if (token is null || token.Length!=47 ||
            !token.StartsWith(Prefix,StringComparison.Ordinal))
            return null;
        var encoded=token[Prefix.Length..];
        foreach(char c in encoded)
            if (c is not (>= 'a' and <= 'z' or >= 'A' and <= 'Z'
                or >= '0' and <= '9' or '-' or '_'))
                return null;
        byte[]? decoded=null;
        try
        {
            decoded=Convert.FromBase64String(
                encoded.Replace('-','+').Replace('_','/')+"=");
            if (decoded.Length!=32 ||
                encoded!=ToBase64Url(decoded) ||
                decoded.AsSpan().IndexOfAnyExcept((byte)0)<0)
            {
                CryptographicOperations.ZeroMemory(decoded);
                return null;
            }
            return decoded;
        }
        catch(FormatException)
        {
            if (decoded is not null) CryptographicOperations.ZeroMemory(decoded);
            return null;
        }
    }

    private static string ToBase64Url(ReadOnlySpan<byte> value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+','-').Replace('/','_');

    public void Dispose()
    {
        if (disposed) return;
        CryptographicOperations.ZeroMemory(key);
        disposed=true;
        GC.SuppressFinalize(this);
    }

    public override string ToString() => "DisposableMfaChallengeProtector[redacted]";
}

/// <summary>Adapter to the independent C05-08 backup HMAC protector.</summary>
public sealed class DisposableMfaBackupAdapter :
    ITrustedDisposableMfaBackupProtector
{
    private readonly MfaRecoveryCodeProtector protector;
    public DisposableMfaBackupAdapter(MfaRecoveryCodeProtector protector) =>
        this.protector=protector??throw new ArgumentNullException(nameof(protector));
    public string GenerateForFixtureOnly() =>
        protector.GenerateForTrustedOneTimeDeliveryOnly();
    public byte[]? TryDigest(string? code) => protector.TryDigest(code);
    public override string ToString() => "DisposableMfaBackupAdapter[redacted]";
}
