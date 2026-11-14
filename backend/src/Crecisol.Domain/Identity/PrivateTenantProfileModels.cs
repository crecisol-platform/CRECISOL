using System.Globalization;
using System.Text;

namespace Crecisol.Domain.Identity;

/// <summary>
/// C05-09 personal display text only. All profile data is private to
/// (tenant, actor); this is NOT a name granting authorization or a login ID.
/// </summary>
public sealed record PrivateDisplayName
{
    private PrivateDisplayName(string value) => Value = value;
    public string Value { get; }

    public static PrivateDisplayName Parse(string? raw)
    {
        if (raw is null || raw.Length is < 2 or > 160 ||
            raw != raw.Trim() || !raw.IsNormalized(NormalizationForm.FormC))
            throw new InvalidOperationException("Invalid private profile display name.");
        var length = 0;
        var previousSpace = false;
        foreach (Rune rune in raw.EnumerateRunes())
        {
            length++;
            if (length > 80 || Rune.IsControl(rune) ||
                rune.Value is 0x200B or 0x200C or 0x200D or 0x202A or 0x202B
                    or 0x202C or 0x202D or 0x202E or 0x2066 or 0x2067
                    or 0x2068 or 0x2069 or 0xFEFF)
                throw new InvalidOperationException("Unsafe profile display name.");
            bool space = rune.Value == 0x20;
            var category = Rune.GetUnicodeCategory(rune);
            bool acceptable = Rune.IsLetterOrDigit(rune) ||
                category is UnicodeCategory.NonSpacingMark
                    or UnicodeCategory.SpacingCombiningMark ||
                space || rune.Value is 0x27 or 0x2D or 0x2E;
            if (!acceptable || (space && previousSpace))
                throw new InvalidOperationException("Unsafe profile display name.");
            previousSpace = space;
        }
        if (length < 2 || raw[0] is ' ' or '-' or '.' or '\'' ||
            raw[^1] is ' ' or '-' or '.' or '\'')
            throw new InvalidOperationException("Invalid private profile display name.");
        return new PrivateDisplayName(raw);
    }

    public override string ToString() => "PrivateDisplayName[redacted]";
}

/// <summary>
/// Internal-only self profile view; a photo ID is an opaque pointer, not an
/// access link, object key, media bearer, role, membership or permission.
/// </summary>
public sealed record PrivateTenantProfileView
{
    public PrivateTenantProfileView(PrivateDisplayName name, Guid? photoId, long revision)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        if (photoId==Guid.Empty || revision<1)
            throw new InvalidOperationException("Invalid tenant profile view.");
        PhotoId=photoId;
        Revision=revision;
    }
    public PrivateDisplayName Name { get; }
    public Guid? PhotoId { get; }
    public long Revision { get; }
    public override string ToString() => "PrivateTenantProfileView[redacted]";
}
