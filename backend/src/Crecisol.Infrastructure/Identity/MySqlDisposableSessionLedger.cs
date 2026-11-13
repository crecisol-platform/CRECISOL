using System.Data;
using System.Security.Cryptography;
using Crecisol.Application.Identity;
using Crecisol.Domain.Tenancy;
using MySqlConnector;

namespace Crecisol.Infrastructure.Identity;

/// <summary>
/// C05-05 only: genuine MySQL8.4 stored-procedure CAS on a disposable fixture.
/// The database runner has EXECUTE only, never table SELECT/UPDATE. This is
/// deliberately NOT a production ledger, does NOT wire the public API and
/// refuses any DSN outside a loopback-only c05_05_disposable schema.
/// </summary>
public sealed class MySqlDisposableSessionLedger : ITrustedSessionLifecycleLedger
{
    private readonly string trustedTestDsn;

    public MySqlDisposableSessionLedger(string? serverHeldTestDsn)
    {
        if (string.IsNullOrWhiteSpace(serverHeldTestDsn))
            throw new InvalidOperationException("Protected local session ledger required.");
        var config = new MySqlConnectionStringBuilder(serverHeldTestDsn)
        {
            Pooling = false,
            ConnectionTimeout = 5,
            DefaultCommandTimeout = 10
        };
        if (config.Database != "c05_05_disposable" ||
            config.Server != "127.0.0.1" ||
            config.AllowLoadLocalInfile ||
            config.Port is < 1 or > 65535)
            throw new InvalidOperationException("Only localhost C05-05 disposable MySQL allowed.");
        trustedTestDsn = config.ConnectionString;
    }

    public async Task<bool> TryIssueAsync(
        PreparedContextualLoginEvidence prepared, Guid sessionId,
        SessionTokenDigest tokenDigest, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        ArgumentNullException.ThrowIfNull(tokenDigest);
        if (sessionId == Guid.Empty ||
            prepared.AccountRevision < 1 || prepared.CredentialRevision < 1 ||
            prepared.MembershipRevision < 1 || prepared.TenantCatalogRevision < 1 ||
            prepared.HostRevision < 0)
            throw new InvalidOperationException("Session issue requires fresh identity revisions.");

        var bytes = tokenDigest.CopyForTrustedLedger();
        try
        {
            await using var db = await ConnectAsync(cancellationToken);
            await using var command = new MySqlCommand(
                "c05_05_issue_once", db) { CommandType = CommandType.StoredProcedure };
            AddId(command, "@p_session", sessionId);
            AddId(command, "@p_tenant", prepared.TenantId.Value);
            AddId(command, "@p_actor", prepared.ActorId.Value);
            AddId(command, "@p_link", prepared.LinkId.Value);
            AddRevision(command, "@p_account_revision", prepared.AccountRevision);
            AddRevision(command, "@p_credential_revision", prepared.CredentialRevision);
            AddRevision(command, "@p_membership_revision", prepared.MembershipRevision);
            AddRevision(command, "@p_tenant_revision", prepared.TenantCatalogRevision);
            AddRevision(command, "@p_host_revision", prepared.HostRevision);
            command.Parameters.Add("@p_digest", MySqlDbType.Binary).Value = bytes;
            return await InvokeAsync(command, "ISSUED", cancellationToken);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    public async Task<bool> IsCurrentAsync(
        TenantId verifiedTenant, SessionTokenDigest tokenDigest,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verifiedTenant);
        ArgumentNullException.ThrowIfNull(tokenDigest);
        var bytes = tokenDigest.CopyForTrustedLedger();
        try
        {
            await using var db = await ConnectAsync(cancellationToken);
            await using var command = new MySqlCommand(
                "c05_05_is_current", db) { CommandType = CommandType.StoredProcedure };
            AddId(command, "@p_tenant", verifiedTenant.Value);
            command.Parameters.Add("@p_digest", MySqlDbType.Binary).Value = bytes;
            return await InvokeAsync(command, "CURRENT", cancellationToken);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    public async Task<bool> TryRotateAsync(
        TenantId verifiedTenant, SessionTokenDigest oldDigest,
        SessionTokenDigest newDigest, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verifiedTenant);
        ArgumentNullException.ThrowIfNull(oldDigest);
        ArgumentNullException.ThrowIfNull(newDigest);
        var oldBytes = oldDigest.CopyForTrustedLedger();
        var newBytes = newDigest.CopyForTrustedLedger();
        try
        {
            if (CryptographicOperations.FixedTimeEquals(oldBytes, newBytes))
                return false;
            await using var db = await ConnectAsync(cancellationToken);
            await using var command = new MySqlCommand(
                "c05_05_rotate_once", db) { CommandType = CommandType.StoredProcedure };
            AddId(command, "@p_tenant", verifiedTenant.Value);
            command.Parameters.Add("@p_old", MySqlDbType.Binary).Value = oldBytes;
            command.Parameters.Add("@p_new", MySqlDbType.Binary).Value = newBytes;
            return await InvokeAsync(command, "ROTATED", cancellationToken);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(oldBytes);
            CryptographicOperations.ZeroMemory(newBytes);
        }
    }

    public async Task<bool> TryRevokeAsync(
        TenantId verifiedTenant, SessionTokenDigest currentDigest,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verifiedTenant);
        ArgumentNullException.ThrowIfNull(currentDigest);
        var bytes = currentDigest.CopyForTrustedLedger();
        try
        {
            await using var db = await ConnectAsync(cancellationToken);
            await using var command = new MySqlCommand(
                "c05_05_logout_once", db) { CommandType = CommandType.StoredProcedure };
            AddId(command, "@p_tenant", verifiedTenant.Value);
            command.Parameters.Add("@p_digest", MySqlDbType.Binary).Value = bytes;
            return await InvokeAsync(command, "REVOKED", cancellationToken);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private async Task<MySqlConnection> ConnectAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var connection = new MySqlConnection(trustedTestDsn);
        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private static async Task<bool> InvokeAsync(
        MySqlCommand command, string success, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is not string status ||
            (status != "DENIED" && status != success))
            throw new InvalidOperationException("Unexpected protected session CAS response.");
        return status == success;
    }

    private static void AddId(MySqlCommand command, string name, Guid value) =>
        command.Parameters.Add(name, MySqlDbType.Binary).Value =
            value.ToByteArray(bigEndian: true);

    private static void AddRevision(MySqlCommand command, string name, long value) =>
        command.Parameters.Add(name, MySqlDbType.Int64).Value = value;

    public override string ToString() => "MySqlDisposableSessionLedger[redacted]";
}
