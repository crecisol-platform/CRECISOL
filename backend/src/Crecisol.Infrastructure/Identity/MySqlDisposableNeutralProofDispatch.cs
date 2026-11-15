using System.Data;
using Crecisol.Domain.Tenancy;
using Crecisol.Infrastructure.Tenancy;
using MySqlConnector;

namespace Crecisol.Infrastructure.Identity;

/// <summary>
/// Protected backend-only (tenant,intent) from a MySQL transaction. This is
/// NOT a bearer credential or tenant authorization; no mailbox is exposed.
/// </summary>
public sealed record ClaimedNeutralProofDispatch(TenantId Tenant, Guid Intent)
{
    public override string ToString() => "ClaimedNeutralProofDispatch[redacted]";
}

/// <summary>
/// Disposable C05-02 SQL outbox claim/complete adapter. The worker NEVER gets
/// a client-specified tenant, mailbox, actor or intent. The DB only releases
/// committed, unexpired intents for active tenants with a one-use lease.
/// No service registration in the CRECISOL production API.
/// </summary>
public sealed class MySqlDisposableNeutralProofDispatch
{
    private readonly string dsn;

    public MySqlDisposableNeutralProofDispatch(string? trustedLocalWorkerDsn)
    {
        if (string.IsNullOrWhiteSpace(trustedLocalWorkerDsn))
            throw new InvalidOperationException("Protected local dispatch worker required.");
        var options = new MySqlConnectionStringBuilder(trustedLocalWorkerDsn)
        {
            Pooling = false,
            ConnectionTimeout = 5,
            DefaultCommandTimeout = 10
        };
        if (options.Database != "c05_02_disposable" ||
            options.Server is not ("127.0.0.1" or "localhost" or "::1") ||
            options.AllowLoadLocalInfile ||
            options.Port is < 1 or > 65535)
            throw new InvalidOperationException("Dispatch is permitted only on disposable loopback MySQL.");
        dsn = options.ConnectionString;
    }

    public async Task<ClaimedNeutralProofDispatch?> ClaimNextAsync(
        Guid serverOwnedLeaseId, CancellationToken cancellationToken)
    {
        if (serverOwnedLeaseId == Guid.Empty)
            throw new InvalidOperationException("Nonempty server-held dispatch lease required.");
        cancellationToken.ThrowIfCancellationRequested();
        await using var db = new MySqlConnection(dsn);
        await db.OpenAsync(cancellationToken);
        await using var cmd = new MySqlCommand("c05_02_dispatch_claim", db)
        {
            CommandType = CommandType.StoredProcedure
        };
        cmd.Parameters.Add("p_owner", MySqlDbType.Binary).Value =
            MySqlRfc4122Guid.Encode(serverOwnedLeaseId);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken) ||
            reader.IsDBNull(0) || reader.IsDBNull(1))
            return null;
        var tenantBytes = (byte[])reader.GetValue(0);
        var intentBytes = (byte[])reader.GetValue(1);
        if (tenantBytes.Length != 16 || intentBytes.Length != 16 ||
            await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException("Ambiguous disposable dispatch lease.");
        var tenant = TenantId.From(MySqlRfc4122Guid.Decode(tenantBytes));
        var intent = MySqlRfc4122Guid.Decode(intentBytes);
        if (intent == Guid.Empty)
            throw new InvalidOperationException("Malformed server-claimed intent.");
        return new ClaimedNeutralProofDispatch(tenant, intent);
    }

    public async Task<bool> CompleteAsync(
        ClaimedNeutralProofDispatch protectedClaim, Guid serverOwnedLeaseId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(protectedClaim);
        if (protectedClaim.Intent == Guid.Empty || serverOwnedLeaseId == Guid.Empty)
            throw new InvalidOperationException("Valid dispatch claim and lease required.");
        cancellationToken.ThrowIfCancellationRequested();
        await using var db = new MySqlConnection(dsn);
        await db.OpenAsync(cancellationToken);
        await using var cmd = new MySqlCommand("c05_02_dispatch_complete", db)
        {
            CommandType = CommandType.StoredProcedure
        };
        cmd.Parameters.Add("p_tenant", MySqlDbType.Binary).Value =
            MySqlRfc4122Guid.Encode(protectedClaim.Tenant.Value);
        cmd.Parameters.Add("p_intent", MySqlDbType.Binary).Value =
            MySqlRfc4122Guid.Encode(protectedClaim.Intent);
        cmd.Parameters.Add("p_owner", MySqlDbType.Binary).Value =
            MySqlRfc4122Guid.Encode(serverOwnedLeaseId);
        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result switch
        {
            "COMPLETED" => true,
            "DENIED" => false,
            _ => throw new InvalidOperationException("Unknown protected dispatch completion result.")
        };
    }

    public override string ToString() => "MySqlDisposableNeutralProofDispatch[redacted]";
}
