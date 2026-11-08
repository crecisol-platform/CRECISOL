using System.Text.RegularExpressions;

namespace Crecisol.Domain.Branding;

public enum BrandScope { Platform, Tenant }
public enum BrandPublicationStatus { Draft, Published }

/// <summary>
/// A typed immutable snapshot. Scope and actor are NEVER chosen by browser input.
/// This is a C01 read-model; tenant resolution and administrative writes are out of scope.
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
    private static readonly Regex HexColor = new(
        "^#[0-9a-fA-F]{6}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex AssetPath = new(
        "^/assets/[a-zA-Z0-9-]+[.]png$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public BrandSnapshot Validate()
    {
        if (!Enum.IsDefined(Scope) || !Enum.IsDefined(Status))
            throw new InvalidOperationException("Unknown brand scope or publication status.");
        if ((Scope == BrandScope.Platform && ScopeId != Guid.Empty) ||
            (Scope == BrandScope.Tenant && ScopeId == Guid.Empty))
            throw new InvalidOperationException("Brand scope and identifier disagree.");
        if (Revision < 1) throw new InvalidOperationException("Brand revision must be positive.");
        CheckText(Name, 2, 100, nameof(Name));
        CheckText(Slogan, 2, 160, nameof(Slogan));
        CheckText(Descriptor, 2, 180, nameof(Descriptor));
        if (!HexColor.IsMatch(PrimaryColor) || !HexColor.IsMatch(AccentColor))
            throw new InvalidOperationException("Invalid brand color.");
        if (!AssetPath.IsMatch(LogoUrl))
            throw new InvalidOperationException("Logo must be an approved local PNG path.");
        if (WebsiteUrl is not null &&
            (!Uri.TryCreate(WebsiteUrl, UriKind.Absolute, out var url) ||
             url.Scheme != Uri.UriSchemeHttps || url.UserInfo.Length != 0))
            throw new InvalidOperationException("Website URL requires HTTPS and no user info.");
        if (Status == BrandPublicationStatus.Published && PublishedAt == default)
            throw new InvalidOperationException("Published brand requires a publication timestamp.");
        return this;
    }

    public BrandSnapshot RequirePublicPlatform()
    {
        Validate();
        if (Scope != BrandScope.Platform || ScopeId != Guid.Empty ||
            Status != BrandPublicationStatus.Published)
            throw new InvalidOperationException("Only the published platform brand is public.");
        return this;
    }

    private static void CheckText(string? value, int min, int max, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length < min || value.Length > max ||
            value.Any(c => char.IsControl(c) || c is '<' or '>' or '\u202e' or '\u202d'))
            throw new InvalidOperationException($"Invalid brand field: {name}.");
    }
}
