using Crecisol.Domain.Common;

namespace Crecisol.Domain.Branding;

/// <summary>
/// Validated PROPOSAL metadata only. It DOES NOT publish or grant access.
/// Authorization, idempotency, audit, concurrency, publication and rollback
/// must be implemented server-side after the C01-03/C01-04 gates.
/// </summary>
public sealed record BrandPublicationProposal(
    BrandSnapshot Draft,
    long ExpectedCurrentPublishedRevision,
    Guid OperationId,
    string Reason)
{
    public BrandPublicationProposal Validate()
    {
        ArgumentNullException.ThrowIfNull(Draft);
        Draft.Validate();
        if (Draft.Status != BrandPublicationStatus.Draft)
            throw new InvalidOperationException("Only a draft may be proposed.");
        DomainRevision.RequireNewerThan(Draft.Revision, ExpectedCurrentPublishedRevision);
        if (OperationId == Guid.Empty)
            throw new InvalidOperationException("Publication requires an operation identifier.");
        BrandValidation.RequireText(Reason, 8, 280, nameof(Reason));
        return this;
    }
}
