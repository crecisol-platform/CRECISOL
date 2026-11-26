namespace Crecisol.Application.Identity;

/// <summary>Internal isolated preparation receipt. Not a session, grant or MFA proof.</summary>
public sealed record C06DelegationPreparation(string Challenge, DateTimeOffset ExpiresAtUtc)
{
    public override string ToString() => "C06DelegationPreparation[redacted;not_authorization]";
}
