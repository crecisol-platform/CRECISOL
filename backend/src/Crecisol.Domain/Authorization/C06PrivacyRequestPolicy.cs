namespace Crecisol.Domain.Authorization;

/// <summary>Closed self-service privacy request vocabulary, not a legal
/// determination, deletion instruction, export authority or reviewer grant.
/// Retention, legal basis, holds and final disposition require independent
/// assessed policy. Unknown categories and rights never enable a mutation.</summary>
public static class C06PrivacyRequestPolicy
{
    public const string Received="received";
    public const string AssessmentPending="pending";
    public static bool IsRight(string value)=>value is "access" or "rectify" or "erase" or "object";
    public static bool IsCategory(string value)=>value is "profile" or "identity" or "security" or "case-records";
    public static bool IsRequest(string right,string category)=>IsRight(right)&&IsCategory(category);
    // Intake, not fulfilment: only verified own identity can enter the queue.
    // No commercial flag or consent/terms acceptance can substitute identity
    // or automatically reject a request about a subject's own data.
    public static bool MaySubmit(Guid requestKey,string right,string category,string reason)=>
        requestKey!=Guid.Empty && IsRequest(right,category) && reason.Length is >=8 and <=240 &&
        reason.Trim().Length>=8 && !reason.Any(char.IsControl) && reason.Any(char.IsLetterOrDigit);
}
