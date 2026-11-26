namespace Crecisol.Application.Identity;

/// <summary>Advisory snapshot for one explicit scope/recipient/role selection.
/// Not a permission, proof, session or authority to submit a mutation.
/// Every preparation and confirmation revalidates current rows independently.
/// </summary>
public sealed record C06DelegationInspection(string RoleCode,long RoleRevision,
    long MembershipRevision,long CeilingRevision,long GrantRevision,
    bool CanAssign,bool CanRevoke,DateTimeOffset? MaxExpiresAtUtc)
{
    public override string ToString() => "C06DelegationInspection[redacted;not_authorization]";
}
