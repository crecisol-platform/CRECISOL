namespace Crecisol.Application.Identity;

public sealed record C06PrivacyPreparation(string Challenge,DateTimeOffset ExpiresAtUtc)
{
    public override string ToString()=>"C06PrivacyPreparation[redacted]";
}
/// <summary>Own request receipt only. No subject ID, reason, legal conclusion,
/// underlying record, export, deletion capability or third-party information.</summary>
public sealed record C06PrivacyReceipt(Guid RequestId,string Right,string Category,DateTimeOffset SubmittedAtUtc)
{
    public override string ToString()=>"C06PrivacyReceipt[redacted]";
}
public sealed record C06PrivacyRequestPage(IReadOnlyList<C06PrivacyReceipt> Items,string? NextCursor)
{
    public override string ToString()=>"C06PrivacyRequestPage[redacted]";
}
