using Crecisol.Application.Authorization;
using Crecisol.Domain.Authorization;
using Crecisol.Domain.Tenancy;
using Crecisol.Infrastructure.Tenancy;
using MySqlConnector;

namespace Crecisol.Infrastructure.Authorization;

/// <summary>
/// C06-02 READ-ONLY adapter for non-authorizing role proposals.
/// Only server-managed DSN and independently verified identity/scope may call
/// this adapter. Not registered in Program.cs; not an HTTP authorization source.
/// Single tenant/account/branch/action SELECT; no dynamic SQL or UI grants.
/// </summary>
public sealed class MySqlPreparedRoleReader : ITrustedC06PreparedRoleReader
{
    private readonly string dsn;

    public MySqlPreparedRoleReader(string? trustedServerDsn)
    {
        if (string.IsNullOrWhiteSpace(trustedServerDsn))
            throw new InvalidOperationException("Prepared role reader requires a server-only DSN.");
        var options = new MySqlConnectionStringBuilder(trustedServerDsn)
        {
            ConnectionTimeout = 3,
            DefaultCommandTimeout = 5,
            Pooling = false,
            AllowLoadLocalInfile = false,
        };
        if (options.AllowUserVariables || options.AllowLoadLocalInfile)
            throw new InvalidOperationException("Unsafe prepared role reader configuration.");
        dsn = options.ConnectionString;
    }

    public async Task<IReadOnlyList<C06PreparedRoleSnapshot>?> ReadExactCandidatesAsync(
        TenantId verifiedTenant, TenantActorId verifiedActor,
        TenantBranchId backendBranch, TenantRequiredAction serverAction,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verifiedTenant);
        ArgumentNullException.ThrowIfNull(verifiedActor);
        ArgumentNullException.ThrowIfNull(backendBranch);
        ArgumentNullException.ThrowIfNull(serverAction);
        cancellationToken.ThrowIfCancellationRequested();

        await using var db = new MySqlConnection(dsn);
        await db.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand("""
            SELECT p.tenant_id, m.account_id, p.branch_id,
                   p.membership_id, p.role_id, r.role_code,
                   CASE WHEN m.membership_state = 1
                             AND m.granted_at_utc <= UTC_TIMESTAMP(6)
                             AND m.expires_at_utc > UTC_TIMESTAMP(6)
                             AND acct.account_state = 1 THEN 1 ELSE 0 END AS membership_active,
                   b.active, r.role_state, p.proposal_state, p.revoked_at_utc,
                   r.role_revision, p.revision, p.valid_from_utc, p.valid_until_utc,
                   a.grantable, rc.proposal_only
              FROM c06_membership_role_proposal p
              JOIN c05_identity_membership m
                ON m.tenant_id = p.tenant_id AND m.membership_id = p.membership_id
              JOIN c05_identity_account acct ON acct.account_id = m.account_id
              JOIN c05_identity_branch b
                ON b.tenant_id = p.tenant_id AND b.branch_id = p.branch_id
              JOIN c06_tenant_role r
                ON r.tenant_id = p.tenant_id AND r.role_id = p.role_id
              JOIN c06_role_capability_proposal rc
                ON rc.tenant_id = p.tenant_id AND rc.role_id = p.role_id
              JOIN c06_capability_registry a
                ON a.action_id = rc.action_id AND a.realm = rc.realm
             WHERE p.tenant_id = @tenant AND m.account_id = @actor
               AND p.branch_id = @branch AND rc.action_id = @action
               AND rc.realm = 1
             ORDER BY p.role_id, p.membership_id
             LIMIT 129
            """, db);
        command.Parameters.Add("@tenant", MySqlDbType.Binary).Value =
            MySqlRfc4122Guid.Encode(verifiedTenant.Value);
        command.Parameters.Add("@actor", MySqlDbType.Binary).Value =
            MySqlRfc4122Guid.Encode(verifiedActor.Value);
        command.Parameters.Add("@branch", MySqlDbType.Binary).Value =
            MySqlRfc4122Guid.Encode(backendBranch.Value);
        command.Parameters.Add("@action", MySqlDbType.VarChar).Value =
            serverAction.Value;

        var result = new List<C06PreparedRoleSnapshot>();
        await using var rows = await command.ExecuteReaderAsync(cancellationToken);
        while (await rows.ReadAsync(cancellationToken))
        {
            // Reverify exact row keys even though SQL already filters; a broken
            // future adapter/query must not silently return a foreign row.
            if (MySqlRfc4122Guid.Decode((byte[])rows.GetValue(0)) != verifiedTenant.Value ||
                MySqlRfc4122Guid.Decode((byte[])rows.GetValue(1)) != verifiedActor.Value ||
                MySqlRfc4122Guid.Decode((byte[])rows.GetValue(2)) != backendBranch.Value ||
                !rows.GetBoolean(16))
                throw new UnauthorizedAccessException("Untrusted proposed role storage row.");
            result.Add(C06PreparedRoleSnapshot.RestoreFromTrustedStore(
                verifiedTenant, verifiedActor, backendBranch,
                MySqlRfc4122Guid.Decode((byte[])rows.GetValue(3)),
                MySqlRfc4122Guid.Decode((byte[])rows.GetValue(4)),
                rows.GetString(5), serverAction,
                rows.GetByte(6) == 1, rows.GetBoolean(7), rows.GetByte(8) == 1,
                rows.GetByte(9) != 1 || !rows.IsDBNull(10),
                checked((long)rows.GetUInt64(11)),
                checked((long)rows.GetUInt64(12)),
                AsUtc(rows.GetDateTime(13)), AsUtc(rows.GetDateTime(14)),
                rows.GetBoolean(15)));
        }
        return result;
    }

    private static DateTimeOffset AsUtc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    public override string ToString() => "MySqlPreparedRoleReader[redacted;read_only]";
}
