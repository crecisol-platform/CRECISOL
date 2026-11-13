using System.Data;
using System.Security.Cryptography;
using Crecisol.Application.Identity;
using Crecisol.Domain.Tenancy;
using MySqlConnector;

namespace Crecisol.Infrastructure.Identity;

/// <summary>
/// C05-06: restricted to the isolated local C05-05+C05-06 MySQL8.4 fixture.
/// Protected procedures atomically issue one-time tokens, change Argon2id PHC,
/// consume the challenge and revoke all sessions across the global actor.
/// No SELECT/UPDATE privileges, production DSN, actual identity or API wiring.
/// </summary>
public sealed class MySqlDisposablePasswordRecoveryLedger : ITrustedPasswordRecoveryLedger
{
    private readonly string dsn;

    public MySqlDisposablePasswordRecoveryLedger(string? serverHeldTestDsn)
    {
        if (string.IsNullOrWhiteSpace(serverHeldTestDsn))
            throw new InvalidOperationException("Protected C05-06 local recovery storage required.");
        var settings = new MySqlConnectionStringBuilder(serverHeldTestDsn)
        {
            Pooling = false,
            ConnectionTimeout = 5,
            DefaultCommandTimeout = 12
        };
        if (settings.Database != "c05_05_disposable" ||
            settings.Server != "127.0.0.1" ||
            settings.AllowLoadLocalInfile ||
            settings.Port is < 1 or > 65535)
            throw new InvalidOperationException("Only the isolated localhost recovery fixture is permitted.");
        dsn = settings.ConnectionString;
    }

    public async Task<bool> TryIssueAsync(
        TenantId verifiedTenant, PasswordRecoveryMailboxDigest verifiedMailbox,
        Guid challengeId, PasswordRecoveryTokenDigest digest,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verifiedTenant);
        ArgumentNullException.ThrowIfNull(verifiedMailbox);
        ArgumentNullException.ThrowIfNull(digest);
        if (challengeId == Guid.Empty)
            throw new InvalidOperationException("New challenge ID required.");

        var mailbox = verifiedMailbox.CopyForTrustedLedger();
        var token = digest.CopyForTrustedLedger();
        try
        {
            await using var db = await ConnectAsync(cancellationToken);
            await using var command = new MySqlCommand(
                "c05_06_issue_once", db) { CommandType = CommandType.StoredProcedure };
            AddId(command, "@p_tenant", verifiedTenant.Value);
            command.Parameters.Add("@p_mailbox_digest", MySqlDbType.Binary).Value = mailbox;
            AddId(command, "@p_challenge", challengeId);
            command.Parameters.Add("@p_token_digest", MySqlDbType.Binary).Value = token;
            return await InvokeAsync(command, "ISSUED", cancellationToken);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(mailbox);
            CryptographicOperations.ZeroMemory(token);
        }
    }

    public async Task<bool> TryConsumeAsync(
        TenantId verifiedTenant, PasswordRecoveryTokenDigest digest,
        string newArgon2idPhc, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verifiedTenant);
        ArgumentNullException.ThrowIfNull(digest);
        if (string.IsNullOrEmpty(newArgon2idPhc) ||
            newArgon2idPhc.Length is < 80 or > 160 ||
            !newArgon2idPhc.StartsWith("$argon2id$v=19$", StringComparison.Ordinal))
            throw new InvalidOperationException("Trusted Argon2id PHC required.");

        var token = digest.CopyForTrustedLedger();
        try
        {
            await using var db = await ConnectAsync(cancellationToken);
            await using var command = new MySqlCommand(
                "c05_06_consume_once", db) { CommandType = CommandType.StoredProcedure };
            AddId(command, "@p_tenant", verifiedTenant.Value);
            command.Parameters.Add("@p_token_digest", MySqlDbType.Binary).Value = token;
            command.Parameters.Add("@p_new_verifier", MySqlDbType.VarChar).Value = newArgon2idPhc;
            return await InvokeAsync(command, "RESET", cancellationToken);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(token);
        }
    }

    private async Task<MySqlConnection> ConnectAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var db = new MySqlConnection(dsn);
        try
        {
            await db.OpenAsync(ct);
            return db;
        }
        catch
        {
            await db.DisposeAsync();
            throw;
        }
    }

    private static async Task<bool> InvokeAsync(
        MySqlCommand command, string positive, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var result = await command.ExecuteScalarAsync(ct);
        if (result is not string outcome ||
            (outcome != positive && outcome != "DENIED"))
            throw new InvalidOperationException("Unexpected protected recovery CAS response.");
        return outcome == positive;
    }

    private static void AddId(MySqlCommand command, string name, Guid value) =>
        command.Parameters.Add(name, MySqlDbType.Binary).Value =
            value.ToByteArray(bigEndian: true);

    public override string ToString() => "MySqlDisposablePasswordRecoveryLedger[redacted]";
}
