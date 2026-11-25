namespace Crecisol.Domain.Authorization;

/// <summary>A narrow, separately governed C06-06 exception; no ordinary
/// platform role, membership, or C06-04 grant implies this authority.</summary>
public static class C06ExceptionalAccessPolicy
{
    public const int MaximumMinutes = 10;
    public static bool IsOperation(string? operation) => operation is
        "platform/breakglass/open" or "platform/breakglass/approve" or
        "platform/breakglass/read" or "platform/breakglass/close";

    public static bool ValidPurpose(string? value) => value is not null &&
        value.Length <= 512 && value.Trim().Length >= 12 &&
        value.Any(char.IsLetterOrDigit) && !value.Any(char.IsControl);

    public static bool ValidProofShape(string? value) => value is not null &&
        ((value.Length == 6 && value.All(char.IsAsciiDigit)) ||
         (value.Length == 32 && value.All(Uri.IsHexDigit)));

    public static bool ValidIdentifier(string? value) =>
        Guid.TryParseExact(value, "D", out var id) && id != Guid.Empty;

    public static bool IndependentReview(Guid requester, Guid reviewer) =>
        requester != Guid.Empty && reviewer != Guid.Empty && requester != reviewer;

    // Pure evidence contract, never a grant minting function. Infrastructure
    // supplies all values from locked server rows and a real factor verifier.
    public static bool AllowsRead(C06ExceptionalReadEvidence e) =>
        e.ActorVerified && e.CurrentSession && e.FreshFactorVerified &&
        e.RequestGrantActive && e.ReviewGrantActive && e.ReviewerActive &&
        e.ResourceActive && e.IndependentApproval && !e.Closed &&
        e.Actor != Guid.Empty && e.Actor == e.Requester &&
        e.Session != Guid.Empty && e.Session == e.RequestSession &&
        e.SecurityRevision > 0 && e.SecurityRevision == e.RequestRevision &&
        e.Now.Offset == TimeSpan.Zero && e.RequestedAt.Offset == TimeSpan.Zero &&
        e.ApprovedAt.Offset == TimeSpan.Zero && e.ExpiresAt.Offset == TimeSpan.Zero &&
        e.RequestedAt <= e.ApprovedAt && e.ApprovedAt <= e.Now &&
        e.Now < e.ExpiresAt && e.ExpiresAt > e.ApprovedAt &&
        e.ExpiresAt - e.RequestedAt <= TimeSpan.FromMinutes(MaximumMinutes);
}

public sealed record C06ExceptionalReadEvidence(
    bool ActorVerified, bool CurrentSession, bool FreshFactorVerified,
    bool RequestGrantActive, bool ReviewGrantActive, bool ReviewerActive,
    bool ResourceActive, bool IndependentApproval, bool Closed,
    Guid Actor, Guid Requester, Guid Session, Guid RequestSession,
    long SecurityRevision, long RequestRevision,
    DateTimeOffset RequestedAt, DateTimeOffset ApprovedAt,
    DateTimeOffset ExpiresAt, DateTimeOffset Now);
