using System.Data;
using System.Security.Cryptography;
using Crecisol.Application.Identity;
using Crecisol.Domain.Identity;
using Crecisol.Domain.Tenancy;
using Crecisol.Infrastructure.Tenancy;
using MySqlConnector;

namespace Crecisol.Infrastructure.Identity;

/// <summary>
/// C05-02 real MySQL adapter limited to the isolated integration-test schema.
/// No production routing, authenticated user, email verification or API/DI binding.
/// Only a server-held LOCAL test DSN is accepted. Client-provided host/tenant/DSN
/// can never select persistence. Calls the database CAS procedure for claims.
/// </summary>
public sealed class MySqlDisposableInvitationLedger : ITrustedInvitationLedger
{
    private readonly string trustedDsn;

    public MySqlDisposableInvitationLedger(string? serverHeldTestDsn)
    {
        if (string.IsNullOrWhiteSpace(serverHeldTestDsn))
            throw new InvalidOperationException("Trusted C05-02 test storage required.");

        var settings = new MySqlConnectionStringBuilder(serverHeldTestDsn)
        {
            Pooling = false,
            ConnectionTimeout = 5,
            DefaultCommandTimeout = 5
        };
        if (!string.Equals(settings.Database, "c05_02_disposable",
                StringComparison.Ordinal) ||
            !(string.Equals(settings.Server, "127.0.0.1", StringComparison.Ordinal) ||
              string.Equals(settings.Server, "localhost", StringComparison.OrdinalIgnoreCase) ||
              string.Equals(settings.Server, "::1", StringComparison.Ordinal)) ||
            settings.AllowLoadLocalInfile)
            throw new InvalidOperationException("Only local isolated C05-02 test storage is allowed.");
        trustedDsn = settings.ConnectionString;
    }

    public async Task<bool> TryInsertUniqueAsync(
        InvitationSnapshot snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        cancellationToken.ThrowIfCancellationRequested();
        if (snapshot.State != InvitationLifecycleState.Pending || snapshot.Revision != 1 ||
            snapshot.IssuedUtc != snapshot.ChangedUtc ||
            snapshot.Profile is not (AccountTenantProfileKind.Employee or AccountTenantProfileKind.Consumer))
            throw new InvalidOperationException("Only validated new pending invitations can be stored.");

        var digest = snapshot.Digest.CopyForProtectedPersistenceAdapter();
        try
        {
            await using var db = new MySqlConnection(trustedDsn);
            await db.OpenAsync(cancellationToken);
            // Least-privilege adapter has ONLY EXECUTE on a DB-owned atomic
            // issuance routine. This routine locks the recipient row, rechecks
            // policy and rejects duplicate active invitations in one transaction.
            await using var command = new MySqlCommand(
                "c05_02_issue_once", db) { CommandType = CommandType.StoredProcedure };
            AddId(command, "p_tenant", snapshot.TenantId.Value);
            AddId(command, "p_invitation", snapshot.Id.Value);
            AddId(command, "p_issuer", snapshot.IssuedBy.Value);
            AddId(command, "p_recipient", snapshot.Recipient.Value);
            command.Parameters.Add("p_profile", MySqlDbType.VarChar).Value =
                ProfileSql(snapshot.Profile);
            command.Parameters.Add("p_digest", MySqlDbType.Binary).Value = digest;
            command.Parameters.Add("p_terms", MySqlDbType.Int64).Value = snapshot.TermsVersion;
            command.Parameters.Add("p_issued", MySqlDbType.DateTime).Value =
                snapshot.IssuedUtc.UtcDateTime;
            command.Parameters.Add("p_expires", MySqlDbType.DateTime).Value =
                snapshot.ExpiresUtc.UtcDateTime;
            var result = await command.ExecuteScalarAsync(cancellationToken);
            if (result is not string response ||
                (response != "ISSUED" && response != "DENIED"))
                throw new InvalidOperationException("Unexpected protected invitation issuance result.");
            return response == "ISSUED";
        }
        finally
        {
            CryptographicOperations.ZeroMemory(digest);
        }
    }

    public async Task<IReadOnlyList<InvitationSnapshot>?> ReadExactAsync(
        TenantId verifiedTenant, InvitationId id,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verifiedTenant);
        ArgumentNullException.ThrowIfNull(id);
        cancellationToken.ThrowIfCancellationRequested();

        await using var db = new MySqlConnection(trustedDsn);
        await db.OpenAsync(cancellationToken);
        await using var command = new MySqlCommand("""
            SELECT tenant_id, invitation_id, issuer_id, recipient_id,
                   profile_scope, token_digest, lifecycle, revision,
                   terms_version, issued_at_utc, expires_at_utc, claimed_at_utc
              FROM c05_02_invitations
             WHERE tenant_id=@tenant AND invitation_id=@invitation
             LIMIT 2
            """, db);
        AddId(command, "@tenant", verifiedTenant.Value);
        AddId(command, "@invitation", id.Value);
        await using var rows = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<InvitationSnapshot>();
        while (await rows.ReadAsync(cancellationToken))
        {
            var rawDigest = (byte[])rows.GetValue(rows.GetOrdinal("token_digest"));
            try
            {
                var tenant = TenantId.From(ReadId(rows, "tenant_id"));
                var invite = InvitationId.From(ReadId(rows, "invitation_id"));
                if (tenant != verifiedTenant || invite != id)
                    throw new UnauthorizedAccessException("Foreign invitation storage row.");
                if (!Enum.TryParse<AccountTenantProfileKind>(
                        rows.GetString("profile_scope"), true, out var profile) ||
                    !Enum.TryParse<InvitationLifecycleState>(
                        rows.GetString("lifecycle"), true, out var state))
                    throw new InvalidOperationException("Invalid protected invitation row.");

                var issued = AsUtc(rows.GetDateTime("issued_at_utc"));
                var claimedOrdinal = rows.GetOrdinal("claimed_at_utc");
                var changed = rows.IsDBNull(claimedOrdinal)
                    ? issued : AsUtc(rows.GetDateTime(claimedOrdinal));
                result.Add(InvitationSnapshot.RestoreFromTrustedInvitationStore(
                    invite, tenant, TenantActorId.From(ReadId(rows, "issuer_id")),
                    RecipientChannelBindingId.From(ReadId(rows, "recipient_id")),
                    profile, state,
                    InvitationTokenDigest.RestoreFromProtectedPersistence(rawDigest),
                    rows.GetInt64("revision"), rows.GetInt64("terms_version"),
                    issued, changed, AsUtc(rows.GetDateTime("expires_at_utc"))));
            }
            finally
            {
                CryptographicOperations.ZeroMemory(rawDigest);
            }
        }
        return result;
    }

    public async Task<bool> TryClaimOnceAtomicallyAsync(
        InvitationSnapshot expected, TenantActorId verifiedRecipientActor,
        DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(verifiedRecipientActor);
        InvitationSnapshot.RequireUtc(nowUtc);
        cancellationToken.ThrowIfCancellationRequested();
        if (!expected.IsPendingAtUtc(nowUtc) || expected.IssuedBy == verifiedRecipientActor)
            return false;

        var digest = expected.Digest.CopyForProtectedPersistenceAdapter();
        try
        {
            await using var db = new MySqlConnection(trustedDsn);
            await db.OpenAsync(cancellationToken);
            await using var command = new MySqlCommand(
                "c05_02_claim_once", db) { CommandType = CommandType.StoredProcedure };
            AddId(command, "p_tenant", expected.TenantId.Value);
            AddId(command, "p_invitation", expected.Id.Value);
            AddId(command, "p_actor", verifiedRecipientActor.Value);
            AddId(command, "p_recipient", expected.Recipient.Value);
            command.Parameters.Add("p_digest", MySqlDbType.Binary).Value = digest;
            command.Parameters.Add("p_terms", MySqlDbType.Int64).Value = expected.TermsVersion;
            command.Parameters.Add("p_revision", MySqlDbType.Int64).Value = expected.Revision;
            var result = await command.ExecuteScalarAsync(cancellationToken);
            if (result is not string response ||
                (response != "CLAIMED" && response != "DENIED"))
                throw new InvalidOperationException("Unexpected protected invitation CAS result.");
            return response == "CLAIMED";
        }
        finally
        {
            CryptographicOperations.ZeroMemory(digest);
        }
    }

    private static string ProfileSql(AccountTenantProfileKind profile) => profile switch
    {
        AccountTenantProfileKind.Employee => "EMPLOYEE",
        AccountTenantProfileKind.Consumer => "CONSUMER",
        _ => throw new InvalidOperationException("Invitation profile unavailable.")
    };

    private static void AddId(MySqlCommand command, string name, Guid value) =>
        command.Parameters.Add(name, MySqlDbType.Binary).Value =
            MySqlRfc4122Guid.Encode(value);

    private static Guid ReadId(MySqlDataReader rows, string column) =>
        MySqlRfc4122Guid.Decode((byte[])rows.GetValue(rows.GetOrdinal(column)));

    private static DateTimeOffset AsUtc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
