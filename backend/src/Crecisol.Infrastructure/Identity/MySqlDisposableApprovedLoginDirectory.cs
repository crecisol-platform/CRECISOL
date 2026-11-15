using System.Security.Cryptography;
using System.Text;
using Crecisol.Application.Identity;
using Crecisol.Domain.Identity;
using Crecisol.Domain.Tenancy;
using Crecisol.Infrastructure.Tenancy;
using MySqlConnector;

namespace Crecisol.Infrastructure.Identity;

/// <summary>
/// Read-only, fixture-only C05 global login directory. A verified mailbox and
/// Argon2id PHC remain pending until a separately authorized, one-use approval
/// has created an ACTIVE tenant link. Realm, account and credential snapshots
/// are returned only to the existing internal C05-04 password use case.
/// NEVER register with public endpoints or treat a lookup as a bearer/grant.
/// </summary>
public sealed class MySqlDisposableApprovedLoginDirectory :
    ITrustedLoginCredentialDirectory, ITrustedLoginTenantLinkDirectory, IDisposable
{
    private readonly string dsn;
    private readonly byte[] digestKey;
    private bool disposed;

    public MySqlDisposableApprovedLoginDirectory(
        string? trustedLocalReadDsn, byte[] trustedMaster)
    {
        ArgumentNullException.ThrowIfNull(trustedMaster);
        if (trustedMaster.Length != 32 || string.IsNullOrWhiteSpace(trustedLocalReadDsn))
            throw new InvalidOperationException("Protected fixture-only login directory required.");
        var config = new MySqlConnectionStringBuilder(trustedLocalReadDsn)
        {
            Pooling=false, ConnectionTimeout=5, DefaultCommandTimeout=10
        };
        if (config.Server is not ("127.0.0.1" or "localhost" or "::1") ||
            config.Database != "c05_02_disposable" ||
            config.AllowLoadLocalInfile || config.Port is <1 or >65535)
            throw new InvalidOperationException("Login directory must be local disposable MySQL.");
        dsn = config.ConnectionString;
        digestKey = HMACSHA256.HashData(trustedMaster,
            "CRECISOL/C05/GLOBAL-LOGIN-KEY/V1"u8);
    }

    public async Task<IReadOnlyList<TrustedLoginCredentialSnapshot>?> FindExactAsync(
        CanonicalLoginHandle login, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(login);
        ct.ThrowIfCancellationRequested();
        var input = Encoding.UTF8.GetBytes(login.Value);
        var digest = HMACSHA256.HashData(digestKey, input);
        CryptographicOperations.ZeroMemory(input);
        try
        {
            await using var db = new MySqlConnection(dsn);
            await db.OpenAsync(ct);
            await using var cmd = new MySqlCommand("""
                SELECT c.actor_id,c.lifecycle,c.verifier_phc,c.created_utc,
                       m.lifecycle,m.changed_utc,m.expires_utc,t.lifecycle
                  FROM c05_02_candidate_accounts c
                  LEFT JOIN c05_02_approved_memberships m
                    ON m.actor_id=c.actor_id AND m.tenant_id=c.source_tenant_id
                  JOIN c05_02_tenants t ON t.tenant_id=c.source_tenant_id
                 WHERE c.login_digest=@digest
                 LIMIT 2
                """,db);
            cmd.Parameters.Add("@digest",MySqlDbType.Binary).Value=digest;
            var list = new List<TrustedLoginCredentialSnapshot>(2);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var actor = TenantActorId.From(
                    MySqlRfc4122Guid.Decode((byte[])reader.GetValue(0)));
                var state = reader.GetString(1);
                var phc = reader.GetString(2);
                var created = Utc(reader.GetDateTime(3));
                var hasLink = !reader.IsDBNull(4);
                var linkState = hasLink ? reader.GetString(4) : "";
                var changed = reader.IsDBNull(5) ? created : Utc(reader.GetDateTime(5));
                var expiry = reader.IsDBNull(6) ? DateTimeOffset.MinValue :
                    Utc(reader.GetDateTime(6));
                var tenantState = reader.GetString(7);
                var active = state=="ACTIVE" && linkState=="ACTIVE" &&
                    expiry>DateTimeOffset.UtcNow && tenantState=="ACTIVE";
                var revoked = state=="REVOKED" || linkState=="REVOKED";
                var account = IdentityAccountSnapshot.RestoreFromTrustedIdentityDirectory(
                    actor, revoked ? IdentityAccountState.Disabled :
                        active ? IdentityAccountState.Active :
                            IdentityAccountState.PendingVerification,
                    revoked ? 3 : active ? 2 : 1,
                    active || revoked ? changed : created);
                var credential = AccountCredentialMetadata.RestoreFromTrustedCredentialDirectory(
                    AccountCredentialId.From(actor.Value),actor,
                    AccountCredentialKind.PasswordVerifier,
                    revoked ? AccountCredentialState.Revoked :
                        active ? AccountCredentialState.Active :
                            AccountCredentialState.PendingEnrollment,
                    revoked ? 2 : 1,created);
                list.Add(TrustedLoginCredentialSnapshot.RestoreFromTrustedDirectory(
                    account,credential,TrustedLoginRealm.TenantAccount,phc));
            }
            return list;
        }
        finally { CryptographicOperations.ZeroMemory(digest); }
    }

    public async Task<IReadOnlyList<AccountTenantLinkSnapshot>?> FindAllByActorAsync(
        TenantActorId verifiedActor, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(verifiedActor);
        ct.ThrowIfCancellationRequested();
        await using var db = new MySqlConnection(dsn);
        await db.OpenAsync(ct);
        await using var cmd = new MySqlCommand("""
            SELECT tenant_id,link_id,lifecycle,revision,changed_utc,granted_utc,expires_utc
              FROM c05_02_approved_memberships
             WHERE actor_id=@actor LIMIT 34
            """,db);
        cmd.Parameters.Add("@actor",MySqlDbType.Binary).Value =
            MySqlRfc4122Guid.Encode(verifiedActor.Value);
        var links = new List<AccountTenantLinkSnapshot>();
        await using var rows = await cmd.ExecuteReaderAsync(ct);
        while (await rows.ReadAsync(ct))
        {
            var tenant = TenantId.From(
                MySqlRfc4122Guid.Decode((byte[])rows.GetValue(0)));
            var linkId = AccountTenantLinkId.From(
                MySqlRfc4122Guid.Decode((byte[])rows.GetValue(1)));
            var state = rows.GetString(2) switch
            {
                "ACTIVE" => TenantMembershipState.Active,
                "REVOKED" => TenantMembershipState.Revoked,
                _ => throw new InvalidOperationException("Unexpected protected membership state.")
            };
            links.Add(AccountTenantLinkSnapshot.RestoreFromTrustedMembershipDirectory(
                linkId,verifiedActor,tenant,AccountTenantProfileKind.Consumer,
                state,rows.GetInt64(3),Utc(rows.GetDateTime(4)),
                Utc(rows.GetDateTime(5)),Utc(rows.GetDateTime(6))));
        }
        return links;
    }

    private static DateTimeOffset Utc(DateTime value) =>
        new(DateTime.SpecifyKind(value,DateTimeKind.Utc));

    public void Dispose()
    {
        if (disposed) return;
        disposed=true;
        CryptographicOperations.ZeroMemory(digestKey);
    }

    public override string ToString() => "MySqlDisposableApprovedLoginDirectory[redacted]";
}
