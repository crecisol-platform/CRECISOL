using Crecisol.Application.Identity;

namespace Crecisol.Api.Contracts;

/// <summary>Version 1 laboratory transport overlay, not a grant source.
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
