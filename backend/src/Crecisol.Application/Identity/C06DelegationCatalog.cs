namespace Crecisol.Application.Identity;

/// <summary>Only candidates authorized by the current locked delegation policy.
/// Selectors are not grants, and the protected continuation is not a credential.
/// No account profile, owner identity, private content, or generic permission.</summary>
public sealed record C06DelegationCatalogItem(Guid BranchId,Guid ResourceId,Guid MembershipId,Guid RoleId,
    C06DelegationInspection Inspection)
{
    public override string ToString() => "C06DelegationCatalogItem[redacted]";
}
public sealed record C06DelegationCatalog(IReadOnlyList<C06DelegationCatalogItem> Items,string? NextCursor)
{
    public override string ToString() => "C06DelegationCatalog[redacted]";
}
