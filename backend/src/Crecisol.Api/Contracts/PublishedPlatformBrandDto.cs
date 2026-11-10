using Crecisol.Application.Branding;

namespace Crecisol.Api.Contracts;

/// <summary>
/// Explicit allowlisted transport contract. Unlike Application/Domain models, this
/// type is the ONLY object serialized by the anonymous branding endpoint.
/// Its JSON property names match the existing Angular public parser.
/// </summary>
public sealed record PublishedPlatformBrandDto(
    long Revision,
    string Name,
    string Slogan,
    string Descriptor,
    string PrimaryColor,
    string AccentColor,
    string LogoUrl,
    string? WebsiteUrl,
    DateTimeOffset PublishedAt,
    PublishedBrandPresentationDto? Presentation)
{
    public static PublishedPlatformBrandDto FromApplication(PublicPlatformBrand published)
    {
        ArgumentNullException.ThrowIfNull(published);
        var presentation = published.Presentation;
        return new PublishedPlatformBrandDto(
            published.Revision,
            published.Name,
            published.Slogan,
            published.Descriptor,
            published.PrimaryColor,
            published.AccentColor,
            published.LogoUrl,
            published.WebsiteUrl,
            published.PublishedAt,
            presentation is null ? null : new PublishedBrandPresentationDto(
                presentation.FaviconUrl,
                presentation.TypographyToken,
                presentation.IconSetToken,
                presentation.SeoTitle,
                presentation.SeoDescription,
                presentation.CopyrightNotice));
    }
}

/// <summary>Public presentation tokens only; never tenant, scope, draft or SQL state.</summary>
public sealed record PublishedBrandPresentationDto(
    string FaviconUrl,
    string TypographyToken,
    string IconSetToken,
    string SeoTitle,
    string SeoDescription,
    string? CopyrightNotice);
