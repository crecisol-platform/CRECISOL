using Crecisol.Application.Identity;

namespace Crecisol.CredentialSecurity;

/// <summary>
/// Concrete C05-04 adapter for the C05-03 audited Argon2id implementation.
/// Only backend-owned verified records may supply the PHC; this class neither
/// reads an account by user ID nor creates session/JWT/tenant permissions.
/// The enclosing composition root is deliberately NOT wired in the API.
/// </summary>
public sealed class Argon2idLoginCandidateVerifier : ITrustedLoginPasswordVerifier
{
    private readonly Argon2idPasswordHasher hasher;

    public Argon2idLoginCandidateVerifier(Argon2idPasswordHasher hasher)
    {
        this.hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
    }

    public PasswordHashCheck Verify(ReadOnlyMemory<char> candidate, string storedPhc) =>
        hasher.Verify(candidate.Span, storedPhc);

    public override string ToString() => "Argon2idLoginCandidateVerifier[redacted]";
}
