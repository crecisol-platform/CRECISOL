using System.Data;
using System.Security.Cryptography;
using Crecisol.Application.Identity;
using Crecisol.Application.Tenancy;
using Crecisol.Domain.Tenancy;
using MySqlConnector;

namespace Crecisol.Infrastructure.Tenancy;

/// <summary>
/// C05 disposable MySQL8.4 adapter. The SQL worker is EXECUTE-only on one
/// protected read procedure, not SELECT on resources, grants or sessions.
/// A resource reference is returned only after atomic protected SQL checks.
/// Must NEVER be registered in product API or used with production storage.
/// </summary>
public sealed class MySqlDisposableMfaPrivateResourceStore :
    ITrustedMfaScopedPrivateResourceStore
{
    private readonly string dsn;

    public MySqlDisposableMfaPrivateResourceStore(string? localOnlyDsn)
    {
        if (string.IsNullOrWhiteSpace(localOnlyDsn))
            throw new InvalidOperationException("Disposable private resource store required.");
        var builder=new MySqlConnectionStringBuilder(localOnlyDsn)
        {
            Pooling=false,ConnectionTimeout=5,DefaultCommandTimeout=15
        };
        if (builder.Server!="127.0.0.1" ||
            builder.Database!="c05_05_disposable" ||
            builder.AllowLoadLocalInfile ||
            builder.Port is <1 or >65535)
            throw new InvalidOperationException("Private resource store must be loopback fixture.");
        dsn=builder.ConnectionString;
    }

    public async Task<AuthorizedPrivateResourceReference?> ReadAtomicallyAsync(
        TenantId backendVerifiedTenant,
        long backendVerifiedHostRevision,
        SessionTokenDigest verifiedTokenDigest,
        TenantResourceId untrustedResourceSelector,
        TenantRequiredAction backendOwnedAction,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(backendVerifiedTenant);
        ArgumentNullException.ThrowIfNull(verifiedTokenDigest);
        ArgumentNullException.ThrowIfNull(untrustedResourceSelector);
        ArgumentNullException.ThrowIfNull(backendOwnedAction);
        cancellationToken.ThrowIfCancellationRequested();
        if (backendVerifiedHostRevision<1)
            return null;
        var secret=verifiedTokenDigest.CopyForTrustedLedger();
        try
        {
            await using var db=new MySqlConnection(dsn);
            await db.OpenAsync(cancellationToken);
            await using var cmd=new MySqlCommand(
                "c05_05_read_private_resource",db)
            {
                CommandType=CommandType.StoredProcedure
            };
            cmd.Parameters.Add("p_tenant",MySqlDbType.Binary).Value=
                backendVerifiedTenant.Value.ToByteArray(bigEndian:true);
            cmd.Parameters.Add("p_host_rev",MySqlDbType.UInt64).Value=
                (ulong)backendVerifiedHostRevision;
            cmd.Parameters.Add("p_token_digest",MySqlDbType.Binary).Value=secret;
            cmd.Parameters.Add("p_resource",MySqlDbType.Binary).Value=
                untrustedResourceSelector.Value.ToByteArray(bigEndian:true);
            cmd.Parameters.Add("p_action",MySqlDbType.VarChar).Value=
                backendOwnedAction.Value;
            await using var rows=await cmd.ExecuteReaderAsync(cancellationToken);
            if (!await rows.ReadAsync(cancellationToken))
                throw new InvalidOperationException("Missing protected authorization result.");
            var status=rows.GetString(0);
            var externalKey=rows.IsDBNull(1)?null:rows.GetString(1);
            if (await rows.ReadAsync(cancellationToken))
                throw new InvalidOperationException("Multiple authorization results.");
            return status switch
            {
                "DENIED" when externalKey is null=>null,
                "ALLOWED" when externalKey is not null=>
                    AuthorizedPrivateResourceReference.FromTrustedAtomicRead(externalKey),
                _=>throw new InvalidOperationException(
                    "Unexpected protected authorization response.")
            };
        }
        finally {CryptographicOperations.ZeroMemory(secret);}
    }

    public override string ToString()=>"MySqlDisposableMfaPrivateResourceStore[redacted]";
}
