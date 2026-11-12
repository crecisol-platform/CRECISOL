using System.Globalization;
using System.Text;
using Crecisol.Domain.Common;

namespace Crecisol.Domain.Tenancy;

/// <summary>Catalog lifecycle metadata, not a session or an access-control decision.</summary>
public enum TenantLifecycleState
{
    Draft = 1,
    PendingVerification = 2,
    Active = 3,
    Suspended = 4,
    Deactivated = 5
}

/// <summary>
/// Only the three commercial address modalities. It contains no host, URL,
/// DNS assertion, connection string, database name, or storage placement.
/// </summary>
public enum TenantAddressMode
{
    PathSlug = 1,
    Subdomain = 2,
    CustomDomain = 3
}

/// <summary>
/// Immutable, internal catalog read snapshot. Creation and transitions are NOT
/// provided here: administrative provisioning, atomic writes, domain ownership,
/// tenancy authorization and placement belong to future gated microphases.
/// </summary>
public sealed record TenantCatalogSnapshot
{
    private TenantCatalogSnapshot(
        TenantId tenantId, string internalName, string displayName,
        TenantLifecycleState state, TenantAddressMode addressMode,
        long revision, DateTimeOffset updatedAtUtc)
    {
        TenantId = tenantId;
        InternalName = internalName;
        DisplayName = displayName;
        State = state;
        AddressMode = addressMode;
        Revision = revision;
        UpdatedAtUtc = updatedAtUtc;
    }

    public TenantId TenantId { get; }
    public string InternalName { get; }
    public string DisplayName { get; }
    public TenantLifecycleState State { get; }
    public TenantAddressMode AddressMode { get; }
    public long Revision { get; }
    public DateTimeOffset UpdatedAtUtc { get; }

    // Records otherwise include internal and potentially personal fields in ToString.
    public override string ToString() => "TenantCatalogSnapshot[redacted]";

    /// <summary>
    /// Deserializes only from a future trusted backend catalog adapter.
    /// Callers MUST NOT hydrate from request headers, host, cookies or claims.
    /// This method cannot create, publish, or authorize a tenant.
    /// </summary>
    public static TenantCatalogSnapshot RestoreFromTrustedCatalog(
        TenantId tenantId, string internalName, string displayName,
        TenantLifecycleState state, TenantAddressMode addressMode,
        long revision, DateTimeOffset updatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(tenantId);
        if (!Enum.IsDefined(state) || !Enum.IsDefined(addressMode))
            throw new InvalidOperationException("Unknown catalog enum value.");

        DomainRevision.From(revision);
        if (updatedAtUtc == default || updatedAtUtc.Offset != TimeSpan.Zero)
            throw new InvalidOperationException("Catalog update timestamp must be UTC.");

        return new TenantCatalogSnapshot(
            tenantId, RequireCatalogName(internalName, nameof(internalName)),
            RequireCatalogName(displayName, nameof(displayName)),
            state, addressMode, revision, updatedAtUtc);
    }

    private static string RequireCatalogName(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Length is < 2 or > 120 ||
            !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            throw new InvalidOperationException("Invalid catalog " + field + ".");

        // No invisible formatting, bidi overrides, markup delimiters, slashes
        // or C0/C1 characters in platform-controlled tenant labels.
        foreach (var c in value)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (char.IsControl(c) ||
                category is UnicodeCategory.Format or UnicodeCategory.Surrogate ||
                c is '<' or '>' or '/' or '\\')
                throw new InvalidOperationException("Unsafe catalog " + field + ".");
        }

        return value.Normalize(NormalizationForm.FormC);
    }
}
