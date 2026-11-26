using System.Globalization;
using Crecisol.Application.Identity;

namespace Crecisol.Api.Contracts;

/// <summary>Version 3 laboratory transport overlay, not a grant source.
/// The canonical historical 79 contracts remain unchanged. No generic
/// serialization, anonymous role claim, or arbitrary capability is accepted.
/// </summary>
public static class C06DelegationHttpContract
{
    private static readonly IReadOnlyDictionary<string,string[]> Fields = new Dictionary<string,string[]>(StringComparer.Ordinal)
    {
        ["delegation/prepare"] = ["command", "branchId", "resourceId", "membershipId", "roleId", "roleRevision", "membershipRevision", "ceilingRevision", "grantRevision", "expiresAtUtc", "reason"],
        ["delegation/confirm"] = ["command", "branchId", "resourceId", "membershipId", "roleId", "roleRevision", "membershipRevision", "ceilingRevision", "grantRevision", "expiresAtUtc", "reason", "challenge", "code"],
        ["delegation/read"] = ["branchId", "resourceId"],
        ["delegation/inspect"] = ["branchId", "resourceId", "membershipId", "roleId"],
        ["delegation/catalog"] = ["cursor"],
    };

    public static bool Enabled =>
        Environment.GetEnvironmentVariable("CRECISOL_C06_DELEGATION_LAB_ENABLED") == "true";
    public static IReadOnlyCollection<string> Operations => Fields.Keys.ToArray();
    public static bool TryFields(string operation,out string[]? fields)
    {
        fields=Fields.TryGetValue(operation,out var found) ? found.ToArray() : null;
        return fields is not null;
    }

    public static bool TryApply(string operation,OperationalIdentityResult result,out OperationalIdentityResult safe)
    {
        safe=new(false);
        if(!Fields.ContainsKey(operation)) return false;
        if(!result.Allowed) return true; // Never project a denied result's data.
        if(result.SessionIdleMinutes is not (>=5 and <=120) ||
           result.SessionExpiresAtUtc is not { } sessionUntil || sessionUntil.Offset!=TimeSpan.Zero)
            return false;
        if(operation=="delegation/catalog")
        {
            if(result.Value is not C06DelegationCatalog catalog || catalog.Items is null ||
               catalog.Items.Count>8 ||
               (catalog.NextCursor is { } cursor && (cursor.Length!=136 ||
                   cursor.Any(ch=>!(char.IsAsciiLetterOrDigit(ch) || ch is '+' or '/' or '=')))))
                return false;
            var rows=new List<object>(catalog.Items.Count);
            var seen=new HashSet<(Guid,Guid,Guid,Guid)>();
            foreach(var item in catalog.Items)
            {
                if(item is null || item.BranchId==Guid.Empty || item.ResourceId==Guid.Empty ||
                   item.MembershipId==Guid.Empty || item.RoleId==Guid.Empty ||
                   !seen.Add((item.BranchId,item.ResourceId,item.MembershipId,item.RoleId)) ||
                   !TryApply("delegation/inspect",result with {Value=item.Inspection},out var projected) ||
                   !projected.Allowed || projected.Value is null)return false;
                rows.Add(new {branchId=item.BranchId,resourceId=item.ResourceId,membershipId=item.MembershipId,
                    roleId=item.RoleId,inspection=projected.Value});
            }
            safe=new(true,new {items=rows,nextCursor=catalog.NextCursor})
                {SessionExpiresAtUtc=sessionUntil,SessionIdleMinutes=result.SessionIdleMinutes};
            return true;
        }
        if(operation=="delegation/inspect")
        {
            if(result.Value is not C06DelegationInspection inspection ||
               inspection.RoleCode is not ("tenant-staff" or "tenant-viewer" or "tenant-branch-manager") ||
               inspection.RoleRevision is <=0 or long.MaxValue ||
               inspection.MembershipRevision is <=0 or long.MaxValue ||
               inspection.CeilingRevision is <=0 or long.MaxValue ||
               inspection.CanAssign==inspection.CanRevoke ||
               (inspection.CanAssign && (inspection.GrantRevision<0 ||
                  inspection.GrantRevision>long.MaxValue-3 || inspection.GrantRevision%2!=0 ||
                  inspection.MaxExpiresAtUtc is not { } max || max.Offset!=TimeSpan.Zero || max<=DateTimeOffset.UtcNow)) ||
               (inspection.CanRevoke && (inspection.GrantRevision<1 || inspection.GrantRevision>long.MaxValue-2 ||
                  inspection.GrantRevision%2!=1 || inspection.MaxExpiresAtUtc is not null)))
                return false;
            // Decimal strings preserve exact BIGINT revisions in JavaScript.
            safe=new(true,new
            {
                roleCode=inspection.RoleCode,
                roleRevision=inspection.RoleRevision.ToString(CultureInfo.InvariantCulture),
                membershipRevision=inspection.MembershipRevision.ToString(CultureInfo.InvariantCulture),
                ceilingRevision=inspection.CeilingRevision.ToString(CultureInfo.InvariantCulture),
                grantRevision=inspection.GrantRevision.ToString(CultureInfo.InvariantCulture),
                canAssign=inspection.CanAssign,canRevoke=inspection.CanRevoke,
                maxExpiresAtUtc=inspection.MaxExpiresAtUtc
            }) {SessionExpiresAtUtc=sessionUntil,SessionIdleMinutes=result.SessionIdleMinutes};
            return true;
        }
        if(operation=="delegation/prepare")
        {
            var now=DateTimeOffset.UtcNow;
            if(result.Value is not C06DelegationPreparation receipt ||
               receipt.Challenge is not {Length:64} || !receipt.Challenge.All(Uri.IsHexDigit) ||
               receipt.ExpiresAtUtc.Offset!=TimeSpan.Zero || receipt.ExpiresAtUtc<=now ||
               receipt.ExpiresAtUtc>now.AddMinutes(5) || receipt.ExpiresAtUtc>sessionUntil)
                return false;
            safe=new(true,new {challenge=receipt.Challenge,expiresAtUtc=receipt.ExpiresAtUtc})
            {SessionExpiresAtUtc=sessionUntil,SessionIdleMinutes=result.SessionIdleMinutes};
            return true;
        }
        // Reuse the closed historical projection of the identical shape.
        // Null is not a valid positive acknowledgement for this overlay.
        if(result.Value is null) return false;
        return C06IdentityResponseProjection.TryApply(
            operation=="delegation/read" ? "private/read" : "memberships/decide",result,out safe);
    }
}
