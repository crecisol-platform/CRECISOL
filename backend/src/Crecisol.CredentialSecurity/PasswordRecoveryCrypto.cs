using System.Security.Cryptography;
using System.Text;
using Crecisol.Application.Identity;

namespace Crecisol.CredentialSecurity;

/// <summary>
/// Dedicated 256-bit CSPRNG reset tokens. The reset HMAC secret and canonical
/// email-HMAC secret are distinct from session-token keys. All keys must be
/// loaded via trusted protected runtime settings; this class is NOT wired into
/// the production CRECISOL API.
/// </summary>
public sealed class PasswordRecoveryTokenProtector :
    ITrustedPasswordRecoveryProtector, IDisposable
{
    private const string Prefix = "cr6_";
    private readonly byte[] tokenKey;
    private readonly byte[] mailboxKey;
    private bool disposed;

    public PasswordRecoveryTokenProtector(
        ReadOnlySpan<byte> tokenHmacKey, ReadOnlySpan<byte> mailboxHmacKey)
    {
        if (!ValidKey(tokenHmacKey) || !ValidKey(mailboxHmacKey) ||
            (tokenHmacKey.Length == mailboxHmacKey.Length &&
             CryptographicOperations.FixedTimeEquals(tokenHmacKey, mailboxHmacKey)))
            throw new InvalidOperationException("Independent protected recovery keys required.");

        tokenKey = tokenHmacKey.ToArray();
        mailboxKey = mailboxHmacKey.ToArray();
    }

    private static bool ValidKey(ReadOnlySpan<byte> key) =>
        key.Length is >= 32 and <= 64 && key.IndexOfAnyExcept((byte)0) >= 0;

    public IssuedPasswordRecoveryToken GenerateFresh()
    {
        ThrowIfDisposed();
        var bytes = RandomNumberGenerator.GetBytes(32);
        try
        {
            var code = Convert.ToBase64String(bytes).TrimEnd('=')
                .Replace('+', '-').Replace('/', '_');
            return IssuedPasswordRecoveryToken.FromTrustedProtector(Prefix + code);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    public PasswordRecoveryTokenDigest? TryDigest(string? untrustedToken)
    {
        ThrowIfDisposed();
        if (untrustedToken is null || untrustedToken.Length != 47 ||
            !untrustedToken.StartsWith(Prefix, StringComparison.Ordinal))
            return null;
        var encoded = untrustedToken.AsSpan(Prefix.Length);
        foreach (var c in encoded)
            if (!(c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or
                    >= '0' and <= '9' or '-' or '_'))
                return null;
        byte[]? parsed = null;
        byte[]? ascii = null;
        byte[]? mac = null;
        try
        {
            var restored = encoded.ToString().Replace('-', '+').Replace('_', '/') + "=";
            parsed = Convert.FromBase64String(restored);
            if (parsed.Length != 32 ||
                Convert.ToBase64String(parsed).TrimEnd('=')
                    .Replace('+', '-').Replace('/', '_') != encoded.ToString())
                return null;

            ascii = Encoding.ASCII.GetBytes(untrustedToken);
            mac = HMACSHA256.HashData(tokenKey, ascii);
            return PasswordRecoveryTokenDigest.FromTrustedProtector(mac);
        }
        catch (FormatException)
        {
            return null;
        }
        finally
        {
            if (parsed is not null) CryptographicOperations.ZeroMemory(parsed);
            if (ascii is not null) CryptographicOperations.ZeroMemory(ascii);
            if (mac is not null) CryptographicOperations.ZeroMemory(mac);
        }
    }

    public PasswordRecoveryMailboxDigest DigestCanonicalMailbox(CanonicalLoginHandle mailbox)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(mailbox);
        var source = Encoding.UTF8.GetBytes("crecisol:c05-06:mailbox:v1:" + mailbox.Value);
        byte[]? mac = null;
        try
        {
            mac = HMACSHA256.HashData(mailboxKey, source);
            return PasswordRecoveryMailboxDigest.FromTrustedProtector(mac);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(source);
            if (mac is not null) CryptographicOperations.ZeroMemory(mac);
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);

    public void Dispose()
    {
        if (disposed) return;
        CryptographicOperations.ZeroMemory(tokenKey);
        CryptographicOperations.ZeroMemory(mailboxKey);
        disposed = true;
        GC.SuppressFinalize(this);
    }

    public override string ToString() => "PasswordRecoveryTokenProtector[redacted]";
}

/// <summary>
/// Delegates ALL password policy, breached-password screening, salt and Argon2id
/// to the already audited C05-03 primitive; never accept an unavailable
/// compromised-password service. The PHC is passed only to protected SQL.
/// </summary>
public sealed class Argon2idRecoveryPasswordEncoder :
    ITrustedPasswordRecoveryPasswordEncoder
{
    private readonly Argon2idPasswordHasher hasher;

    public Argon2idRecoveryPasswordEncoder(Argon2idPasswordHasher hasher) =>
        this.hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));

    public string EncodeNewPassword(ReadOnlySpan<char> candidate) =>
        hasher.CreateVerifier(candidate).ExportForTrustedCredentialStore();

    public override string ToString() => "Argon2idRecoveryPasswordEncoder[redacted]";
}
