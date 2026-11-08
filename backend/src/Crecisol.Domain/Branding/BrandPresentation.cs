namespace Crecisol.Domain.Branding;

/// <summary>
/// Data-only theme/SEO tokens. Frontend maps tokens to a fixed trusted registry;
/// these strings are never executed as CSS/JS/HTML or external icon fonts.
/// </summary>
public sealed record BrandPresentation(
    string FaviconUrl,
    string TypographyToken,
    string IconSetToken,
    string SeoTitle,
    string SeoDescription,
    string? CopyrightNotice)
{
    private static readonly IReadOnlySet<string> TypographyTokens =
        new HashSet<string>(StringComparer.Ordinal) { "system", "humanist", "editorial" };
    private static readonly IReadOnlySet<string> IconTokens =
        new HashSet<string>(StringComparer.Ordinal) { "outline", "solid" };

    public BrandPresentation Validate()
    {
        BrandValidation.RequireLocalAsset(FaviconUrl, nameof(FaviconUrl));
        if (!TypographyTokens.Contains(TypographyToken))
            throw new InvalidOperationException("Unknown typography token.");
        if (!IconTokens.Contains(IconSetToken))
            throw new InvalidOperationException("Unknown icon set token.");
        BrandValidation.RequireText(SeoTitle, 2, 120, nameof(SeoTitle));
        BrandValidation.RequireText(SeoDescription, 2, 260, nameof(SeoDescription));
        if (CopyrightNotice is not null)
            BrandValidation.RequireText(CopyrightNotice, 2, 220, nameof(CopyrightNotice));
        return this;
    }
}
