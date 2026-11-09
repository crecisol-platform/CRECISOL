using Crecisol.Domain.Branding;
using Crecisol.Domain.Common;

namespace Crecisol.Application.Branding;

/// <summary>
/// Read-only port for the current published PLATFORM revision.
/// This contract receives no tenant, host, actor, connection string or DSN.
/// A future infrastructure adapter must independently enforce data ownership.
/// </summary>
public interface IPublishedPlatformBrandRevisionReader
{
    Task<long?> ReadCurrentPublishedRevisionAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Internal assessment result: NOT a publication receipt, permission, lock or
/// externally safe DTO. No operation id, actor, tenant or publication reason.
/// </summary>
public sealed record PlatformBrandPublicationPreflight(
    long CurrentPublishedRevision,
    long ProposedDraftRevision);

/// <summary>
/// Validates a PLATFORM publication proposal against a read-only revision port.
/// NO writes, authorization, idempotency, transactions or HTTP endpoints.
/// Must not be wired to untrusted callers until backend auth and ownership
/// controls are implemented in their authorized microphases.
/// </summary>
public sealed class AssessPlatformBrandPublicationProposal
{
    private readonly IPublishedPlatformBrandRevisionReader reader;

    public AssessPlatformBrandPublicationProposal(IPublishedPlatformBrandRevisionReader reader)
    {
        this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
    }

    public async Task<PlatformBrandPublicationPreflight> ExecuteAsync(
        BrandPublicationProposal proposal,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        cancellationToken.ThrowIfCancellationRequested();

        // Validate all untrusted Domain fields before any read from the port.
        proposal.Validate();
        if (proposal.Draft.Scope != BrandScope.Platform)
            throw new InvalidOperationException("Only PLATFORM proposals can be assessed.");

        var published = await reader.ReadCurrentPublishedRevisionAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (published is < 0)
            throw new InvalidOperationException("Invalid published revision returned by port.");

        var current = published ?? 0;
        if (current != proposal.ExpectedCurrentPublishedRevision)
            throw new InvalidOperationException("Published revision changed; proposal is stale.");

        DomainRevision.RequireNewerThan(proposal.Draft.Revision, current);
        return new PlatformBrandPublicationPreflight(current, proposal.Draft.Revision);
    }
}
