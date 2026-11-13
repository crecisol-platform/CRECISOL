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
            await using var tx = await db.BeginTransactionAsync(
                IsolationLevel.ReadCommitted, cancellationToken);

            // Lock verified recipient row to serialize ALL pending issuances
            // for this tenant/recipient across concurrent backend connections.
            // Recheck lifecycle, action scope and current version inside SQL,
            // not only via the caller's previously approved in-memory policy.
            await using (var auth = new MySqlCommand("""
                SELECT r.recipient_id
                  FROM c05_02_recipients r
                  JOIN c05_02_tenants t ON t.tenant_id=r.tenant_id
                  JOIN c05_02_issuers s ON s.tenant_id=r.tenant_id
                 WHERE r.tenant_id=@tenant AND r.recipient_id=@recipient
                   AND s.actor_id=@issuer AND s.enabled=1
                   AND t.lifecycle='ACTIVE'
                   AND t.terms_version=@terms
                   AND r.actor_id<>s.actor_id
                   AND r.channel_verified=1
                   AND UTC_TIMESTAMP(6)<r.verified_until_utc
                   AND ((@profile='EMPLOYEE' AND s.profile_scope='INVITE_EMPLOYEE')
                     OR (@profile='CONSUMER' AND s.profile_scope='INVITE_CONSUMER'))
                 FOR UPDATE
                """, db, tx))
            {
                AddId(auth, "@tenant", snapshot.TenantId.Value);
                AddId(auth, "@recipient", snapshot.Recipient.Value);
                AddId(auth, "@issuer", snapshot.IssuedBy.Value);
                auth.Parameters.Add("@terms", MySqlDbType.Int64).Value = snapshot.TermsVersion;
                auth.Parameters.Add("@profile", MySqlDbType.VarChar).Value =
                    ProfileSql(snapshot.Profile);
                if (await auth.ExecuteScalarAsync(cancellationToken) is not byte[])
                    return false;
            }

            // No second live unexpired invitation is allowed for a recipient
            // (regardless of Employee/Consumer): lock row held above provides
            // the serialization point for distinct invitation IDs/tokens.
            await using (var duplicate = new MySqlCommand("""
                SELECT invitation_id FROM c05_02_invitations
                 WHERE tenant_id=@tenant AND recipient_id=@recipient
                   AND lifecycle='PENDING'
                   AND expires_at_utc>UTC_TIMESTAMP(6)
                 LIMIT 1 FOR UPDATE
                """, db, tx))
            {
                AddId(duplicate, "@tenant", snapshot.TenantId.Value);
                AddId(duplicate, "@recipient", snapshot.Recipient.Value);
                if (await duplicate.ExecuteScalarAsync(cancellationToken) is not null)
                    return false;
            }

            // No clear bearer can be persisted: only domain-separated digest.
            await using var insert = new MySqlCommand("""
                INSERT INTO c05_02_invitations
                  (tenant_id,invitation_id,issuer_id,recipient_id,profile_scope,
                   token_digest,lifecycle,revision,terms_version,issued_at_utc,
                   expires_at_utc,claimed_at_utc)
                VALUES (@tenant,@invitation,@issuer,@recipient,@profile,
                        @digest,'PENDING',1,@terms,@issued,@expires,NULL)
                """, db, tx);
            AddId(insert, "@tenant", snapshot.TenantId.Value);
            AddId(insert, "@invitation", snapshot.Id.Value);
            AddId(insert, "@issuer", snapshot.IssuedBy.Value);
            AddId(insert, "@recipient", snapshot.Recipient.Value);
            insert.Parameters.Add("@profile", MySqlDbType.VarChar).Value =
                ProfileSql(snapshot.Profile);
            insert.Parameters.Add("@digest", MySqlDbType.Binary).Value = digest;
            insert.Parameters.Add("@terms", MySqlDbType.Int64).Value = snapshot.TermsVersion;
            insert.Parameters.Add("@issued", MySqlDbType.DateTime).Value =
                snapshot.IssuedUtc.UtcDateTime;
            insert.Parameters.Add("@expires", MySqlDbType.DateTime).Value =
                snapshot.ExpiresUtc.UtcDateTime;

            try
            {
                if (await insert.ExecuteNonQueryAsync(cancellationToken) != 1)
                    return false;
                await tx.CommitAsync(cancellationToken);
                return true;
            }
            catch (MySqlException ex) when (ex.Number is 1062 or 1452)
            {
                return false;
            }
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
