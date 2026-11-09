using System.Data;
using System.Globalization;
using Crecisol.Application.Branding;
using Crecisol.Domain.Branding;
using MySqlConnector;

namespace Crecisol.Infrastructure.Branding;

/// <summary>
/// Single-purpose, read-only MySQL 8.4 adapter. No tenant id, host or connection
/// string is accepted from an HTTP request; no fallback to unpublished brand data.
/// Database user must have SELECT on publications/revisions only.
/// </summary>
public sealed class MySqlPublishedPlatformBrandReader(string? connectionString)
    : IPublishedPlatformBrandReader
{
    // Fixed, parameter-free query: neither browser nor host supplies a scope.
    private const string ReadPublishedPlatformSql = """
        SELECT r.revision, r.publication_status,
               r.name, r.slogan, r.descriptor, r.primary_color, r.accent_color,
               r.logo_url, r.website_url, r.favicon_url, r.typography_token,
               r.icon_set_token, r.seo_title, r.seo_description,
               r.copyright_notice, r.published_at_utc
        FROM brand_publications AS p
        INNER JOIN brand_revisions AS r
          ON r.scope_kind = p.scope_kind
         AND r.scope_id = p.scope_id
         AND r.revision = p.published_revision
         AND r.publication_status = p.publication_status
        WHERE p.scope_kind = 1
          AND p.scope_id = X'00000000000000000000000000000000'
          AND p.publication_status = 2
          AND r.scope_kind = 1
          AND r.scope_id = X'00000000000000000000000000000000'
          AND r.publication_status = 2
        LIMIT 1
        """;

    public async Task<BrandSnapshot?> FindPublishedPlatformAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("The platform brand storage is not configured.");

        var cs = new MySqlConnectionStringBuilder(connectionString)
        {
            ConnectionTimeout = 3,
            DefaultCommandTimeout = 3,
            Pooling = true,
            MaximumPoolSize = 5
        };

        await using var db = new MySqlConnection(cs.ConnectionString);
        await db.OpenAsync(cancellationToken);
        await using var command = db.CreateCommand();
        command.CommandText = ReadPublishedPlatformSql;

        await using var row = await command.ExecuteReaderAsync(
            CommandBehavior.SingleRow, cancellationToken);
        if (!await row.ReadAsync(cancellationToken))
            return null;

        string Required(string field) => row.GetString(row.GetOrdinal(field));
        string? Optional(string field)
        {
            var ordinal = row.GetOrdinal(field);
            return row.IsDBNull(ordinal) ? null : row.GetString(ordinal);
        }

        var favicon = Optional("favicon_url");
        var presentation = favicon is null ? null : new BrandPresentation(
            favicon, Required("typography_token"), Required("icon_set_token"),
            Required("seo_title"), Required("seo_description"),
            Optional("copyright_notice"));

        // MySQL DATETIME(6) has no time zone. The schema contract stores UTC.
        var utc = DateTime.SpecifyKind(
            row.GetDateTime(row.GetOrdinal("published_at_utc")), DateTimeKind.Utc);

        var snapshot = new BrandSnapshot(
            BrandScope.Platform,
            Guid.Empty,
            Convert.ToInt64(row.GetValue(row.GetOrdinal("revision")), CultureInfo.InvariantCulture),
            (BrandPublicationStatus)Convert.ToInt32(
                row.GetValue(row.GetOrdinal("publication_status")), CultureInfo.InvariantCulture),
            Required("name"), Required("slogan"), Required("descriptor"),
            Required("primary_color"), Required("accent_color"),
            Required("logo_url"), Optional("website_url"),
            new DateTimeOffset(utc)) { Presentation = presentation };

        // Database CHECK/FK are not sufficient to permit anonymous publication.
        return snapshot.RequirePublicPlatform();
    }
}
