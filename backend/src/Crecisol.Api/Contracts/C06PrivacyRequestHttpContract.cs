using Crecisol.Application.Identity;
using Crecisol.Domain.Authorization;

namespace Crecisol.Api.Contracts;

/// <summary>Closed self-service privacy intake overlay, independently enabled.
/// Not a permission to export, access underlying data, erase or review.</summary>
public static class C06PrivacyRequestHttpContract
{
    private static readonly IReadOnlyDictionary<string,string[]> Fields=new Dictionary<string,string[]>(StringComparer.Ordinal)
    {
        ["privacy/requests/list"] = ["cursor"],
        ["privacy/requests/prepare"] = ["requestKey", "right", "category", "reason"],
        ["privacy/requests/confirm"] = ["requestKey", "right", "category", "reason", "challenge", "code"],
    };
    public static bool Enabled=>Environment.GetEnvironmentVariable("CRECISOL_C06_PRIVACY_REQUESTS_LAB_ENABLED")=="true";
    public static IReadOnlyCollection<string> Operations=>Fields.Keys.ToArray();
    public static bool TryFields(string operation,out string[]? fields)
    {
        fields=Fields.TryGetValue(operation,out var found)?found.ToArray():null;
        return fields is not null;
    }
    private static object? Receipt(C06PrivacyReceipt? r)
    {
        if(r is null||r.RequestId==Guid.Empty||!C06PrivacyRequestPolicy.IsRequest(r.Right,r.Category)||
           r.SubmittedAtUtc.Offset!=TimeSpan.Zero||r.SubmittedAtUtc<=DateTimeOffset.UnixEpoch||
           r.SubmittedAtUtc>DateTimeOffset.UtcNow.AddSeconds(1))return null;
        return new {requestId=r.RequestId,right=r.Right,category=r.Category,submittedAtUtc=r.SubmittedAtUtc,
            status=C06PrivacyRequestPolicy.Received,assessment=new
            {
                identity="mfa-verified",purpose="pending",legalBasis="pending",retention="pending",
                legalHold="pending",disposition="pending"
            }};
    }
    public static bool TryApply(string operation,OperationalIdentityResult result,out OperationalIdentityResult safe)
    {
        safe=new(false);
        if(!Fields.ContainsKey(operation))return false;
        if(!result.Allowed)return true;
        if(result.SessionIdleMinutes is not (>=5 and <=120)||
           result.SessionExpiresAtUtc is not { } until||until.Offset!=TimeSpan.Zero)return false;
        object? value;
        if(operation=="privacy/requests/prepare")
        {
            if(result.Value is not C06PrivacyPreparation p||p.Challenge is not {Length:64}||
               !p.Challenge.All(Uri.IsHexDigit)||p.ExpiresAtUtc.Offset!=TimeSpan.Zero||
               p.ExpiresAtUtc<=DateTimeOffset.UtcNow||p.ExpiresAtUtc>DateTimeOffset.UtcNow.AddMinutes(5)||p.ExpiresAtUtc>until)
                return false;
            value=new {challenge=p.Challenge,expiresAtUtc=p.ExpiresAtUtc};
        }
        else if(operation=="privacy/requests/confirm")
        {
            value=Receipt(result.Value as C06PrivacyReceipt);
            if(value is null)return false;
        }
        else
        {
            if(result.Value is not C06PrivacyRequestPage page||page.Items is null||page.Items.Count>20||
               (page.NextCursor is { } cursor&&(cursor.Length is <120 or >160||
                    cursor.Any(ch=>!(char.IsAsciiLetterOrDigit(ch)||ch=='/')))))return false;
            var rows=new List<object>(page.Items.Count);var seen=new HashSet<Guid>();
            foreach(var r in page.Items)
            {
                var row=Receipt(r);
                if(row is null||!seen.Add(r.RequestId))return false;
                rows.Add(row);
            }
            value=new {items=rows,nextCursor=page.NextCursor};
        }
        safe=new(true,value){SessionIdleMinutes=result.SessionIdleMinutes,SessionExpiresAtUtc=until};
        return true;
    }
}
