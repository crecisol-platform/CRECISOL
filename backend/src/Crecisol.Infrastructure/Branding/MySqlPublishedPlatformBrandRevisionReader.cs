using System.Globalization;
using Crecisol.Application.Branding;
using MySqlConnector;

namespace Crecisol.Infrastructure.Branding;

/// <summary>
/// Internal, read-only adapter for the C03-03 PLATFORM revision port.
/// Not registered in the API. An eventual trusted composition root must
/// supply the storage connection; requests/hosts/tenant IDs never select it.
/// This does NOT authorize publication or protect an eventual write race.
/// </summary>
public sealed class MySqlPublishedPlatformBrandRevisionReader(string? connectionString)
    : IPublishedPlatformBrandRevisionReader
{
    // Literal PLATFORM-only pointer, verified against an actually PUBLISHED
    // revision of the same scope. No tenant, draft, browser or actor input.
    private const string ReadPublishedRevisionSql = """
        SELECT r.revision
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

    public async Task<long?> ReadCurrentPublishedRevisionAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Read-only brand revision storage is not configured.");

        // The string is supplied only by a trusted caller, never by HTTP.
        // No SQL string interpolation, writes, retry or permissive fallback.
        var settings = new MySqlConnectionStringBuilder(connectionString)
        {
            ConnectionTimeout = 3,
            DefaultCommandTimeout = 3,
            Pooling = false
        };

        await using var db = new MySqlConnection(settings.ConnectionString);
        await db.OpenAsync(cancellationToken);
        await using var command = db.CreateCommand();
        command.CommandText = ReadPublishedRevisionSql;

        var value = await command.ExecuteScalarAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (value is null || value is DBNull)
            return null;

        var revision = Convert.ToInt64(value, CultureInfo.InvariantCulture);
        if (revision <= 0)
            throw new InvalidOperationException("Invalid published platform revision.");
        return revision;
    }
}
