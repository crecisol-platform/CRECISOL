using Crecisol.Domain.Branding;

namespace Crecisol.Application.Branding;

/// <summary>Port restricted to published PLATFORM identity. It never accepts a tenant id,
/// browser host, claims, or a destination DSN from a caller.</summary>
public interface IPublishedPlatformBrandReader
{
    Task<BrandSnapshot?> FindPublishedPlatformAsync(CancellationToken cancellationToken);
}

/// <summary>The sole anonymous branding use case; no drafts or tenant publication allowed.</summary>
public sealed class ReadPublishedPlatformBrand(IPublishedPlatformBrandReader reader)
{
    public async Task<PublicPlatformBrand?> ExecuteAsync(CancellationToken cancellationToken)
    {
        var snapshot = await reader.FindPublishedPlatformAsync(cancellationToken);
        if (snapshot is null) return null;
        snapshot.RequirePublicPlatform();
        var presentation = snapshot.Presentation is null ? null :
            new PublicBrandPresentation(
                snapshot.Presentation.FaviconUrl,
                snapshot.Presentation.TypographyToken,
                snapshot.Presentation.IconSetToken,
                snapshot.Presentation.SeoTitle,
                snapshot.Presentation.SeoDescription,
                snapshot.Presentation.CopyrightNotice);
        return new PublicPlatformBrand(
            snapshot.Revision, snapshot.Name, snapshot.Slogan, snapshot.Descriptor,
            snapshot.PrimaryColor, snapshot.AccentColor, snapshot.LogoUrl,
            snapshot.WebsiteUrl, snapshot.PublishedAt, presentation);
    }
}

/// <summary>Deliberately excludes tenant/scope identifiers, preview and publication controls.</summary>
public sealed record PublicPlatformBrand(
    long Revision,
    string Name,
    string Slogan,
    string Descriptor,
    string PrimaryColor,
    string AccentColor,
    string LogoUrl,
    string? WebsiteUrl,
    DateTimeOffset PublishedAt,
    PublicBrandPresentation? Presentation);

public sealed record PublicBrandPresentation(
    string FaviconUrl,
    string TypographyToken,
    string IconSetToken,
    string SeoTitle,
    string SeoDescription,
    string? CopyrightNotice);
