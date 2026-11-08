using System.Text.RegularExpressions;

namespace Crecisol.Domain.Branding;

/// <summary>
/// Contract-only validation for values received from an UNTRUSTED configuration
/// source. Passing validation NEVER grants an actor rights to read or publish.
/// </summary>
public static class BrandValidation
{
    private static readonly Regex HexRgb = new(
        "^#[0-9a-fA-F]{6}$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(150));

    private static readonly Regex LocalAssetPath = new(
        "^/assets/[a-zA-Z0-9-]{1,80}[.](png|webp)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(150));

    public static void RequireText(string? text, int min, int max, string field)
    {
        if (text is null || text.Length < min || text.Length > max ||
            string.IsNullOrWhiteSpace(text) ||
            text.Any(character => char.IsControl(character) ||
                character is '<' or '>' or '\u202a' or '\u202b' or '\u202c'
                             or '\u202d' or '\u202e' or '\u2066' or '\u2067'
                             or '\u2068' or '\u2069' or '\u200b'))
            throw new InvalidOperationException($"Invalid brand text field: {field}.");
    }

    public static void RequireRgb(string? color, string field)
    {
        if (color is null || !HexRgb.IsMatch(color))
            throw new InvalidOperationException($"Invalid brand RGB field: {field}.");
    }

    public static void RequireLocalAsset(string? assetPath, string field)
    {
        if (assetPath is null || !LocalAssetPath.IsMatch(assetPath))
            throw new InvalidOperationException($"Invalid local brand asset: {field}.");
    }

    public static void RequireHttpsOrigin(string? url, string field)
    {
        if (url is null) return;
        if (url.Length > 255 ||
            !Uri.TryCreate(url, UriKind.Absolute, out var parsed) ||
            parsed.Scheme != Uri.UriSchemeHttps ||
            parsed.HostNameType != UriHostNameType.Dns ||
            parsed.UserInfo.Length != 0 ||
            parsed.Query.Length != 0 ||
            parsed.Fragment.Length != 0 ||
            parsed.AbsolutePath != "/" ||
            parsed.IsLoopback)
            throw new InvalidOperationException($"Invalid HTTPS origin: {field}.");

        // An HTTPS-shaped URL is not proof of domain ownership. A verified-host
        // registry and authorization are required BEFORE any public routing.
    }
}
