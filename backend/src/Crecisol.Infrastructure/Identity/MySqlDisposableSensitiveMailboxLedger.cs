using System.Data;
using System.Security.Cryptography;
using Crecisol.Application.Identity;
using Crecisol.Domain.Tenancy;
using MySqlConnector;

namespace Crecisol.Infrastructure.Identity;

/// <summary>
/// C05-07: disposable MySQL8.4-only backend adapter. All effects are in SQL
/// SECURITY DEFINER routines. Runner has EXECUTE, no SELECT, UPDATE, DELETE,
/// PHC access or tenant-configured DSN. NEVER register in the product API.
/// </summary>
public sealed class MySqlDisposableSensitiveMailboxLedger : ITrustedSensitiveMailboxLedger
{
    private readonly string dsn;

    public MySqlDisposableSensitiveMailboxLedger(string? testHeldDsn)
    {
        if (string.IsNullOrWhiteSpace(testHeldDsn))
            throw new InvalidOperationException("Isolated C05-07 test connection required.");
        var options = new MySqlConnectionStringBuilder(testHeldDsn)
        {
            Pooling = false,
            ConnectionTimeout = 5,
            DefaultCommandTimeout = 12
        };
        if (options.Server != "127.0.0.1" ||
            options.Database != "c05_05_disposable" ||
            options.AllowLoadLocalInfile ||
            options.Port is < 1 or > 65535)
            throw new InvalidOperationException("Only disposable localhost MySQL8.4 is permitted.");
        dsn = options.ConnectionString;
    }

    public async Task<bool> TryBeginVerificationAsync(
        TenantId verifiedTenant, PasswordRecoveryMailboxDigest registeredMailbox,
        Guid verificationId, MailboxApprovalDigest challenge,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verifiedTenant);
        ArgumentNullException.ThrowIfNull(registeredMailbox);
        ArgumentNullException.ThrowIfNull(challenge);
        if (verificationId == Guid.Empty)
            throw new InvalidOperationException("New mailbox verification operation required.");
        var mailbox = registeredMailbox.CopyForTrustedLedger();
        var secret = challenge.CopyForTrustedLedger();
        try
        {
            await using var db = await ConnectAsync(cancellationToken);
            await using var command = new MySqlCommand(
                "c05_07_begin_verify", db) { CommandType = CommandType.StoredProcedure };
            AddGuid(command, "@p_tenant", verifiedTenant.Value);
            command.Parameters.Add("@p_mailbox", MySqlDbType.Binary).Value = mailbox;
            AddGuid(command, "@p_verification", verificationId);
            command.Parameters.Add("@p_digest", MySqlDbType.Binary).Value = secret;
            return await InvokeAsync(command, "ISSUED", cancellationToken);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(mailbox);
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    public async Task<bool> TryVerifyAsync(
        TenantId verifiedTenant, MailboxApprovalDigest challenge,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verifiedTenant);
        ArgumentNullException.ThrowIfNull(challenge);
        var secret = challenge.CopyForTrustedLedger();
        try
        {
            await using var db = await ConnectAsync(cancellationToken);
            await using var command = new MySqlCommand(
                "c05_07_confirm_verify", db) { CommandType = CommandType.StoredProcedure };
            AddGuid(command, "@p_tenant", verifiedTenant.Value);
            command.Parameters.Add("@p_digest", MySqlDbType.Binary).Value = secret;
            return await InvokeAsync(command, "VERIFIED", cancellationToken);
        }
        finally { CryptographicOperations.ZeroMemory(secret); }
    }

    public async Task<bool> TryBeginChangeAsync(
        PreparedContextualLoginEvidence proof,
        PasswordRecoveryMailboxDigest previousMailbox,
        PasswordRecoveryMailboxDigest replacementMailbox,
        Guid changeId, MailboxApprovalDigest previousProof,
        MailboxApprovalDigest replacementProof,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(proof);
        ArgumentNullException.ThrowIfNull(previousMailbox);
        ArgumentNullException.ThrowIfNull(replacementMailbox);
        ArgumentNullException.ThrowIfNull(previousProof);
        ArgumentNullException.ThrowIfNull(replacementProof);
        if (changeId == Guid.Empty)
            throw new InvalidOperationException("New change operation ID required.");
        var oldMail = previousMailbox.CopyForTrustedLedger();
        var newMail = replacementMailbox.CopyForTrustedLedger();
        var oldToken = previousProof.CopyForTrustedLedger();
        var newToken = replacementProof.CopyForTrustedLedger();
        try
        {
            if (CryptographicOperations.FixedTimeEquals(oldMail, newMail) ||
                CryptographicOperations.FixedTimeEquals(oldToken, newToken))
                return false;

            await using var db = await ConnectAsync(cancellationToken);
            await using var command = new MySqlCommand(
                "c05_07_begin_change", db) { CommandType = CommandType.StoredProcedure };
            AddGuid(command, "@p_tenant", proof.TenantId.Value);
            AddGuid(command, "@p_actor", proof.ActorId.Value);
            AddGuid(command, "@p_link", proof.LinkId.Value);
            AddRevision(command, "@p_account_rev", proof.AccountRevision);
            AddRevision(command, "@p_credential_rev", proof.CredentialRevision);
            AddRevision(command, "@p_membership_rev", proof.MembershipRevision);
            AddRevision(command, "@p_tenant_rev", proof.TenantCatalogRevision);
            AddRevision(command, "@p_host_rev", proof.HostRevision);
            command.Parameters.Add("@p_old", MySqlDbType.Binary).Value = oldMail;
            command.Parameters.Add("@p_new", MySqlDbType.Binary).Value = newMail;
            AddGuid(command, "@p_change", changeId);
            command.Parameters.Add("@p_old_token", MySqlDbType.Binary).Value = oldToken;
            command.Parameters.Add("@p_new_token", MySqlDbType.Binary).Value = newToken;
            return await InvokeAsync(command, "ISSUED", cancellationToken);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(oldMail);
            CryptographicOperations.ZeroMemory(newMail);
            CryptographicOperations.ZeroMemory(oldToken);
            CryptographicOperations.ZeroMemory(newToken);
        }
    }

    public async Task<bool> TryConfirmChangeAsync(
        TenantId verifiedTenant, MailboxApprovalDigest challenge,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verifiedTenant);
        ArgumentNullException.ThrowIfNull(challenge);
        var secret = challenge.CopyForTrustedLedger();
        try
        {
            await using var db = await ConnectAsync(cancellationToken);
            await using var command = new MySqlCommand(
                "c05_07_confirm_change", db) { CommandType = CommandType.StoredProcedure };
            AddGuid(command, "@p_tenant", verifiedTenant.Value);
            command.Parameters.Add("@p_token", MySqlDbType.Binary).Value = secret;
            var result = await InvokeResultAsync(command, cancellationToken);
            return result is "RECORDED" or "APPLIED";
        }
        finally { CryptographicOperations.ZeroMemory(secret); }
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
        MySqlCommand command, string accepted, CancellationToken ct) =>
        await InvokeResultAsync(command, ct) == accepted;

    private static async Task<string> InvokeResultAsync(
        MySqlCommand command, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var result = await command.ExecuteScalarAsync(ct);
        if (result is not string status ||
            status is not ("ISSUED" or "VERIFIED" or "RECORDED" or "APPLIED" or "DENIED"))
            throw new InvalidOperationException("Unexpected protected mailbox CAS response.");
        return status;
    }

    private static void AddGuid(MySqlCommand command, string name, Guid value) =>
        command.Parameters.Add(name, MySqlDbType.Binary).Value =
            value.ToByteArray(bigEndian: true);

    private static void AddRevision(MySqlCommand command, string name, long revision)
    {
        if (revision < 1)
            throw new InvalidOperationException("Trusted positive revision required.");
        command.Parameters.Add(name, MySqlDbType.UInt64).Value =
            checked((ulong)revision);
    }

    public override string ToString() => "MySqlDisposableSensitiveMailboxLedger[redacted]";
}
