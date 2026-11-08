namespace Crecisol.Domain.Branding;

public enum BrandScope { Platform = 1, Tenant = 2 }
public enum BrandPublicationStatus { Draft = 1, Published = 2 }

/// <summary>
/// Immutable versioned identity. PLATFORM and TENANT must remain separated.
/// This is a CONTRACT, not an authorization decision or persistence service.
/// Preview is a restricted read of a Draft, NOT a third publication status.
/// </summary>
public sealed record BrandSnapshot(
    BrandScope Scope,
    Guid ScopeId,
    long Revision,
    BrandPublicationStatus Status,
    string Name,
    string Slogan,
    string Descriptor,
    string PrimaryColor,
    string AccentColor,
    string LogoUrl,
    string? WebsiteUrl,
    DateTimeOffset PublishedAt)
{
    /// <summary>
    /// Optional extra presentation tokens, validated if supplied. Current initial
    /// read model is intentionally backward-compatible with the draft PR #10.
    /// </summary>
    public BrandPresentation? Presentation { get; init; }

    public BrandSnapshot Validate()
    {
        if (!Enum.IsDefined(Scope) || !Enum.IsDefined(Status))
            throw new InvalidOperationException("Unknown brand scope or publication status.");
        if ((Scope == BrandScope.Platform && ScopeId != Guid.Empty) ||
            (Scope == BrandScope.Tenant && ScopeId == Guid.Empty))
            throw new InvalidOperationException("Brand scope and identifier disagree.");
        if (Revision < 1)
            throw new InvalidOperationException("Brand revision must be positive.");

        BrandValidation.RequireText(Name, 2, 100, nameof(Name));
        BrandValidation.RequireText(Slogan, 2, 160, nameof(Slogan));
        BrandValidation.RequireText(Descriptor, 2, 180, nameof(Descriptor));
        BrandValidation.RequireRgb(PrimaryColor, nameof(PrimaryColor));
        BrandValidation.RequireRgb(AccentColor, nameof(AccentColor));
        BrandValidation.RequireLocalAsset(LogoUrl, nameof(LogoUrl));
        BrandValidation.RequireHttpsOrigin(WebsiteUrl, nameof(WebsiteUrl));
        Presentation?.Validate();

        if (Status == BrandPublicationStatus.Draft && PublishedAt != default)
            throw new InvalidOperationException("Draft must not carry a publication timestamp.");
        if (Status == BrandPublicationStatus.Published &&
            (PublishedAt == default || PublishedAt.Offset != TimeSpan.Zero))
            throw new InvalidOperationException("Published brand requires a UTC publication timestamp.");
        return this;
    }

    /// <summary>
    /// Only publication approved for anonymous PLATFORM metadata.
    /// No tenant, preview or draft data may pass this public-only gate.
    /// </summary>
    public BrandSnapshot RequirePublicPlatform()
    {
        Validate();
        if (Scope != BrandScope.Platform || Status != BrandPublicationStatus.Published)
            throw new InvalidOperationException("Only published PLATFORM identity is public.");
        return this;
    }
}
