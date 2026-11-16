using Crecisol.Application.Identity;
using System.Security.Cryptography;
using System.Text;

namespace Crecisol.CredentialSecurity;

public sealed class OperationalIdentityCrypto(Argon2idPasswordHasher hasher) : IOperationalIdentityCrypto
{
    public string HashPassword(string password) => hasher.CreateVerifier(password.AsSpan()).ExportForTrustedCredentialStore();
    public bool VerifyPassword(string password, string phc) => hasher.Verify(password.AsSpan(), phc).Matched;
    public bool VerifyTotp(byte[] seed, string code, DateTimeOffset now, long previous, out long step) =>
        TotpSha256Verifier.TryVerify(seed, code, now, previous, out step);
    public bool VerifyPairing(byte[] publicKey, string message, byte[] signature)
    {
        try
        {
            using var verifier = ECDsa.Create();
            verifier.ImportSubjectPublicKeyInfo(publicKey, out var read);
            return read == publicKey.Length && verifier.KeySize == 256 &&
                verifier.ExportParameters(false).Curve.Oid.Value == "1.2.840.10045.3.1.7" && signature.Length == 64 &&
                verifier.VerifyData(Encoding.UTF8.GetBytes(message), signature, HashAlgorithmName.SHA256,
                    DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }
        catch (CryptographicException) { return false; }
    }
}
