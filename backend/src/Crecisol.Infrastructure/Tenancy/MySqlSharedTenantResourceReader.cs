using System.Data;
using Crecisol.Application.Tenancy;
using Crecisol.Domain.Tenancy;
using MySqlConnector;

namespace Crecisol.Infrastructure.Tenancy;

/// <summary>
/// C04-02 restricted SHARED schema reader. An authenticated authorized-scope
/// resolver is required by Application and is intentionally NOT implemented,
/// bound in DI, or exposed through HTTP in this microphase. Only a trusted
/// backend composition root may supply a DSN, never a host or tenant request.
/// </summary>
public sealed class MySqlSharedTenantResourceReader(string? trustedConnectionString)
    : ISharedTenantResourceReader
{
    private const string ReadScopedResourceSql = """
        SELECT tenant_id, branch_id, resource_id, external_key
          FROM c04_resources
         WHERE tenant_id = @tenant_id
           AND branch_id = @branch_id
           AND resource_id = @resource_id
         LIMIT 1
        """;

    public async Task<SharedResourceReadResult?> FindAsync(
        AuthorizedSharedReadScope scope, Guid resourceId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (resourceId == Guid.Empty)
            throw new InvalidOperationException("Resource ID must not be empty.");
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(trustedConnectionString))
            throw new InvalidOperationException("Shared read storage is not configured.");

        var options = new MySqlConnectionStringBuilder(trustedConnectionString)
        {
            ConnectionTimeout = 3,
            DefaultCommandTimeout = 3,
            Pooling = false
        };
        await using var db = new MySqlConnection(options.ConnectionString);
        await db.OpenAsync(cancellationToken);
        await using var command = db.CreateCommand();
        command.CommandText = ReadScopedResourceSql;
        command.CommandType = CommandType.Text;

        command.Parameters.Add("@tenant_id", MySqlDbType.Binary).Value =
            MySqlRfc4122Guid.Encode(scope.TenantId.Value);
        command.Parameters.Add("@branch_id", MySqlDbType.Binary).Value =
            MySqlRfc4122Guid.Encode(scope.BranchId);
        command.Parameters.Add("@resource_id", MySqlDbType.Binary).Value =
            MySqlRfc4122Guid.Encode(resourceId);

        await using var rows = await command.ExecuteReaderAsync(
            CommandBehavior.SingleRow, cancellationToken);
        if (!await rows.ReadAsync(cancellationToken))
            return null;

        var actualTenant = TenantId.From(MySqlRfc4122Guid.Decode(
            (byte[])rows.GetValue(rows.GetOrdinal("tenant_id"))));
        var actualBranch = MySqlRfc4122Guid.Decode(
            (byte[])rows.GetValue(rows.GetOrdinal("branch_id")));
        var actualResource = MySqlRfc4122Guid.Decode(
            (byte[])rows.GetValue(rows.GetOrdinal("resource_id")));
        var externalKey = rows.GetString(rows.GetOrdinal("external_key"));

        if (actualTenant != scope.TenantId ||
            actualBranch != scope.BranchId ||
            actualResource != resourceId)
            throw new InvalidOperationException("Scoped MySQL reader returned foreign data.");

        cancellationToken.ThrowIfCancellationRequested();
        return new SharedResourceReadResult(
            actualTenant, actualBranch, actualResource, externalKey);
    }
}
