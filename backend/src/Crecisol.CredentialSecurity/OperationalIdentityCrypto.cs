using Crecisol.Application.Identity;

namespace Crecisol.CredentialSecurity;

public sealed class OperationalIdentityCrypto(Argon2idPasswordHasher hasher) : IOperationalIdentityCrypto
{
    public string HashPassword(string password) => hasher.CreateVerifier(password.AsSpan()).ExportForTrustedCredentialStore();
    public bool VerifyPassword(string password, string phc) => hasher.Verify(password.AsSpan(), phc).Matched;
    public bool VerifyTotp(byte[] seed, string code, DateTimeOffset now, long previous, out long step) =>
        TotpSha256Verifier.TryVerify(seed, code, now, previous, out step);
}
