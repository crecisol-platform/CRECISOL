using System.Security.Cryptography;
using System.Text;
using Crecisol.Application.Identity;
using Crecisol.Domain.Identity;
using Crecisol.Domain.Tenancy;
using Crecisol.Infrastructure.Tenancy;
using MySqlConnector;

namespace Crecisol.Infrastructure.Identity;

/// <summary>
/// Product-schema read adapter for the local operational identity flow. It
/// never grants access: the Application login use case still verifies the
/// password, account, verified host, tenant, and all active memberships.
/// The caller supplies the independently protected login HMAC key.
/// </summary>
public sealed class MySqlOperationalLoginDirectory :
    ITrustedLoginCredentialDirectory, ITrustedLoginTenantLinkDirectory, IDisposable
{
    private readonly string dsn;
    private readonly byte[] loginHmacKey;
    private bool disposed;

    public MySqlOperationalLoginDirectory(
        string? trustedDsn, ReadOnlySpan<byte> loginLookupHmacKey)
    {
        if (string.IsNullOrWhiteSpace(trustedDsn) || loginLookupHmacKey.Length != 32)
            throw new InvalidOperationException("Operational login directory is not configured.");

        var options = new MySqlConnectionStringBuilder(trustedDsn)
        {
            ConnectionTimeout = 3,
            DefaultCommandTimeout = 5,
            Pooling = true,
            MaximumPoolSize = 20,
            AllowLoadLocalInfile = false
        };
        if (options.AllowUserVariables || options.AllowLoadLocalInfile)
            throw new InvalidOperationException("Operational login directory configuration is unsafe.");
        dsn = options.ConnectionString;
        this.loginHmacKey = loginLookupHmacKey.ToArray();
    }

    public async Task<IReadOnlyList<TrustedLoginCredentialSnapshot>?> FindExactAsync(
        CanonicalLoginHandle login, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(login);
        cancellationToken.ThrowIfCancellationRequested();

        var loginBytes = Encoding.UTF8.GetBytes(login.Value);
        var loginDigest = HMACSHA256.HashData(loginHmacKey, loginBytes);
        CryptographicOperations.ZeroMemory(loginBytes);
        try
        {
            await using var db = await OpenAsync(cancellationToken);
            await using var command = new MySqlCommand("""
                SELECT account_id,account_state,security_revision,password_phc,
                       updated_at_utc
                  FROM c05_identity_account
                 WHERE login_lookup_hmac=@login
                 ORDER BY account_id
                 LIMIT 2
                """, db);
            command.Parameters.Add("@login", MySqlDbType.Binary).Value = loginDigest;
            var snapshots = new List<TrustedLoginCredentialSnapshot>(2);
            await using var rows = await command.ExecuteReaderAsync(cancellationToken);
            while (await rows.ReadAsync(cancellationToken))
            {
                var actor = TenantActorId.From(
                    MySqlRfc4122Guid.Decode((byte[])rows.GetValue(0)));
                var state = MapAccountState(rows.GetByte(1));
                var revision = checked((long)rows.GetUInt64(2));
                var verifier = rows.GetString(3);
                var changed = AsUtc(rows.GetDateTime(4));

                var account = IdentityAccountSnapshot.RestoreFromTrustedIdentityDirectory(
                    actor, state, revision, changed);
                var credentialState = state switch
                {
                    IdentityAccountState.Active => AccountCredentialState.Active,
                    IdentityAccountState.PendingVerification => AccountCredentialState.PendingEnrollment,
                    _ => AccountCredentialState.Revoked
                };
                var credential = AccountCredentialMetadata.RestoreFromTrustedCredentialDirectory(
                    AccountCredentialId.From(actor.Value), actor,
                    AccountCredentialKind.PasswordVerifier, credentialState,
                    revision, changed);
                snapshots.Add(TrustedLoginCredentialSnapshot.RestoreFromTrustedDirectory(
                    account, credential, TrustedLoginRealm.TenantAccount, verifier));
            }
            return snapshots;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(loginDigest);
        }
    }

    public async Task<IReadOnlyList<AccountTenantLinkSnapshot>?> FindAllByActorAsync(
        TenantActorId verifiedActorId, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(verifiedActorId);
        cancellationToken.ThrowIfCancellationRequested();

        await using var db = await OpenAsync(cancellationToken);
        await using var command = new MySqlCommand("""
            SELECT tenant_id,membership_id,membership_state,profile_kind,
                   approval_revision,changed_at_utc,granted_at_utc,expires_at_utc
              FROM c05_identity_membership
             WHERE account_id=@account
             ORDER BY tenant_id,membership_id
             LIMIT 33
            """, db);
        command.Parameters.Add("@account", MySqlDbType.Binary).Value =
            MySqlRfc4122Guid.Encode(verifiedActorId.Value);
        var result = new List<AccountTenantLinkSnapshot>();
        await using var rows = await command.ExecuteReaderAsync(cancellationToken);
        while (await rows.ReadAsync(cancellationToken))
        {
            var tenant = TenantId.From(MySqlRfc4122Guid.Decode((byte[])rows.GetValue(0)));
            var membership = AccountTenantLinkId.From(
                MySqlRfc4122Guid.Decode((byte[])rows.GetValue(1)));
            var state = MapMembershipState(rows.GetByte(2));
            var profile = MapProfile(rows.GetByte(3));
            var revision = checked((long)rows.GetUInt64(4));
            var changed = AsUtc(rows.GetDateTime(5));
            DateTimeOffset? granted = rows.IsDBNull(6) ? null : AsUtc(rows.GetDateTime(6));
            DateTimeOffset? expires = rows.IsDBNull(7) ? null : AsUtc(rows.GetDateTime(7));
            result.Add(AccountTenantLinkSnapshot.RestoreFromTrustedMembershipDirectory(
                membership, verifiedActorId, tenant, profile, state, revision,
                changed, granted, expires));
        }
        return result;
    }

    private async Task<MySqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var connection = new MySqlConnection(dsn);
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

    private static IdentityAccountState MapAccountState(byte state) => state switch
    {
        0 => IdentityAccountState.PendingVerification,
        1 => IdentityAccountState.Active,
        2 => IdentityAccountState.Suspended,
        3 => IdentityAccountState.Disabled,
        _ => throw new InvalidOperationException("Unknown operational account state.")
    };

    private static TenantMembershipState MapMembershipState(byte state) => state switch
    {
        0 => TenantMembershipState.Pending,
        1 => TenantMembershipState.Active,
        2 => TenantMembershipState.Suspended,
        3 => TenantMembershipState.Revoked,
        _ => throw new InvalidOperationException("Unknown operational membership state.")
    };

    private static AccountTenantProfileKind MapProfile(byte profile) => profile switch
    {
        1 => AccountTenantProfileKind.Owner,
        2 => AccountTenantProfileKind.Employee,
        3 => AccountTenantProfileKind.Consumer,
        _ => throw new InvalidOperationException("Unknown operational profile kind.")
    };

    private static DateTimeOffset AsUtc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        CryptographicOperations.ZeroMemory(loginHmacKey);
    }

    public override string ToString() => "MySqlOperationalLoginDirectory[redacted]";
}
