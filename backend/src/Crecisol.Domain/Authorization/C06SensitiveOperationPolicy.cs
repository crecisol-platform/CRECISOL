namespace Crecisol.Domain.Authorization;

/// <summary>Identifies the verified realm of a C05 identity transition.</summary>
public enum C06StepUpRealm { Tenant, Platform }

/// <summary>
/// Closed list of existing operations that consume fresh C05 factor proof.
/// This validates request input only: a true result is never an MFA grant,
/// identity, membership, resource permission, or authorization decision.
/// </summary>
public static class C06SensitiveOperationPolicy
{
    public static bool RequiresFreshFactor(C06StepUpRealm realm, string? operation) =>
        realm switch
        {
            C06StepUpRealm.Tenant => operation is
                "passkeys/register-options" or
                "passkeys/revoke" or
                "invitations/issue" or
                "sessions/revoke-all" or
                "pairings/approve" or
                "factor/replace" or
                "password/change" or
                "mailbox/request" or
                "mailbox/complete" or
                "memberships/decide",
            C06StepUpRealm.Platform => operation is
                "platform/sessions/revoke-all" or
                "platform/cases/find" or
                "platform/password/change" or
                "platform/factor/replace" or
                "platform/mailbox/request" or
                "platform/mailbox/complete" or
                "platform/review" or
                "platform/release-share" or
                "platform/session-policy/update",
            _ => false
        };

    public static bool RequiresHumanReason(C06StepUpRealm realm, string? operation) =>
        (realm == C06StepUpRealm.Tenant && operation == "memberships/decide") ||
        (realm == C06StepUpRealm.Platform && operation is "platform/cases/find" or "platform/review");

    public static bool AcceptsInput(C06StepUpRealm realm, string? operation, string? code, string? reason)
    {
        if (!RequiresFreshFactor(realm, operation) || code is null) return false;
        // Preserve the existing C05 RFC6238 TOTP and one-use backup formats.
        if (!(code.Length == 6 && code.All(char.IsAsciiDigit)) &&
            !(code.Length == 32 && code.All(Uri.IsHexDigit))) return false;
        if (!RequiresHumanReason(realm, operation)) return true;
        var min = realm == C06StepUpRealm.Tenant ? 8 : 12;
        var max = realm == C06StepUpRealm.Tenant ? 240 : 512;
        // This is a bounded, nonempty statement of purpose, not proof that a
        // claimed reason is true. Keep the existing HTTP size contracts.
        return reason is not null && reason.Length <= max &&
            reason.Trim().Length >= min && reason.Any(char.IsLetterOrDigit) &&
            !reason.Any(char.IsControl);
    }
}
