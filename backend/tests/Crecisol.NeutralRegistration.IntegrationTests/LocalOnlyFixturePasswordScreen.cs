using Crecisol.Application.Identity;

namespace Crecisol.NeutralRegistration.IntegrationTests;

// Used exclusively by the disposable C05 test runner. NOT an implementation
// of production compromised-password screening or account authorization.
internal sealed class LocalOnlyFixturePasswordScreen : ITrustedCompromisedPasswordScreen
{
    public PasswordCompromiseVerdict Check(ReadOnlySpan<char> candidate) =>
        candidate.Length >= PasswordHashingPolicy.MinAcceptedRunes
            ? PasswordCompromiseVerdict.Allowed
            : PasswordCompromiseVerdict.Compromised;
}
