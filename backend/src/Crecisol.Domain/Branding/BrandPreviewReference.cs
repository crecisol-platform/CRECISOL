using Crecisol.Domain.Common;

namespace Crecisol.Domain.Branding;

/// <summary>
/// Non-secret *intent* to view an unpublished revision. It conveys NO
/// authentication, ownership, membership, authorization or capability token.
/// Future Application code must first verify the authenticated actor, tenant,
/// branch, scope and revision before returning any draft content.
/// </summary>
public sealed record BrandPreviewReference(
    BrandScope Scope,
    Guid ScopeId,
    long DraftRevision)
{
    public static BrandPreviewReference FromDraft(BrandSnapshot draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        draft.Validate();
        if (draft.Status != BrandPublicationStatus.Draft)
            throw new InvalidOperationException("Preview requires a draft.");
        return new BrandPreviewReference(draft.Scope, draft.ScopeId, draft.Revision);
    }

    public BrandPreviewReference Validate()
    {
        BrandScopeKey.Create(Scope, ScopeId);
        DomainRevision.From(DraftRevision);
        return this;
    }
}
