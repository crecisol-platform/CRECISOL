using System.Security.Cryptography;
using System.Text;
using Crecisol.Application.Identity;

namespace Crecisol.CredentialSecurity;

/// <summary>
/// C05-07 test-only composition supplies a separate purpose-specific HMAC
/// key. This key MUST NOT be shared with sessions, password recovery, or
/// canonical mailbox lookup. No key is persisted or generated in source.
/// </summary>
public sealed class MailboxApprovalTokenProtector :
    ITrustedMailboxApprovalProtector, IDisposable
{
    private const string Prefix = "cv7_";
    private readonly byte[] secret;
    private bool disposed;

    public MailboxApprovalTokenProtector(ReadOnlySpan<byte> serverHeldKey)
    {
        if (serverHeldKey.Length is < 32 or > 64 ||
            serverHeldKey.IndexOfAnyExcept((byte)0) < 0)
            throw new InvalidOperationException("Protected independent mailbox proof HMAC key required.");
        secret = serverHeldKey.ToArray();
    }

    public IssuedMailboxApproval GenerateFresh()
    {
        ThrowIfDisposed();
        var bytes = RandomNumberGenerator.GetBytes(32);
        try
        {
            return IssuedMailboxApproval.FromTrustedProtector(
                Prefix + Convert.ToBase64String(bytes).TrimEnd('=')
                    .Replace('+', '-').Replace('/', '_'));
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    public MailboxApprovalDigest? TryDigest(string? token)
    {
        ThrowIfDisposed();
        if (token is null || token.Length != 47 ||
            !token.StartsWith(Prefix, StringComparison.Ordinal))
            return null;

        var encoded = token.AsSpan(4);
        foreach (var c in encoded)
            if (!(c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or
                    >= '0' and <= '9' or '-' or '_'))
                return null;

        byte[]? decoded = null;
        byte[]? ascii = null;
        byte[]? mac = null;
        try
        {
            decoded = Convert.FromBase64String(
                encoded.ToString().Replace('-', '+').Replace('_', '/') + "=");
            if (decoded.Length != 32 ||
                Convert.ToBase64String(decoded).TrimEnd('=')
                    .Replace('+', '-').Replace('/', '_') != encoded.ToString())
                return null;

            ascii = Encoding.ASCII.GetBytes(token);
            mac = HMACSHA256.HashData(secret, ascii);
            return MailboxApprovalDigest.FromTrustedProtector(mac);
        }
        catch (FormatException) { return null; }
        finally
        {
            if (decoded is not null) CryptographicOperations.ZeroMemory(decoded);
            if (ascii is not null) CryptographicOperations.ZeroMemory(ascii);
            if (mac is not null) CryptographicOperations.ZeroMemory(mac);
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);

    public void Dispose()
    {
        if (disposed) return;
        CryptographicOperations.ZeroMemory(secret);
        disposed = true;
        GC.SuppressFinalize(this);
    }

    public override string ToString() => "MailboxApprovalTokenProtector[redacted]";
}
