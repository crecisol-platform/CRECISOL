using System.Data;
using System.Security.Cryptography;
using Crecisol.Application.Identity;
using Crecisol.Domain.Tenancy;
using Crecisol.Infrastructure.Tenancy;
using MySqlConnector;

namespace Crecisol.Infrastructure.Identity;

/// <summary>
/// C05 disposable single-host approval transaction only. This worker has
/// EXECUTE on one procedure, never SELECT/UPDATE of approvals, MFA factors,
/// token digests, candidate credentials or membership rows.
/// Must NOT be registered against production DSNs.
/// </summary>
public sealed class MySqlDisposableMfaMembershipApprovalStore
    : ITrustedMfaMembershipApprovalStore
{
    private readonly string dsn;

    public MySqlDisposableMfaMembershipApprovalStore(string? trustedLocalDsn)
    {
        if(string.IsNullOrWhiteSpace(trustedLocalDsn))
            throw new InvalidOperationException("Disposable MFA approval worker required.");
        var builder=new MySqlConnectionStringBuilder(trustedLocalDsn)
        {
            Pooling=false,ConnectionTimeout=5,DefaultCommandTimeout=15
        };
        if (builder.Server is not ("127.0.0.1" or "localhost" or "::1") ||
            builder.Database!="c05_02_disposable" ||
            builder.Port is < 1 or > 65535 || builder.AllowLoadLocalInfile)
            throw new InvalidOperationException(
                "MFA approval worker may only use the disposable local schema.");
        dsn=builder.ConnectionString;
    }

    public async Task<bool> TryActivateAsync(
        TenantId trustedTenant,long verifiedHostRevision,
        TenantActorId protectedCandidate,Guid backendApprovalId,
        Guid backendLinkId,SessionTokenDigest sessionDigest,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(trustedTenant);
        ArgumentNullException.ThrowIfNull(protectedCandidate);
        ArgumentNullException.ThrowIfNull(sessionDigest);
        cancellationToken.ThrowIfCancellationRequested();
        if(verifiedHostRevision<1 || protectedCandidate.Value==Guid.Empty ||
           backendApprovalId==Guid.Empty || backendLinkId==Guid.Empty)
            return false;

        var digest=sessionDigest.CopyForTrustedLedger();
        try
        {
            await using var db=new MySqlConnection(dsn);
            await db.OpenAsync(cancellationToken);
            await using var cmd=new MySqlCommand("c05_02_activate_by_mfa_approver",db)
            {
                CommandType=CommandType.StoredProcedure
            };
            cmd.Parameters.Add("p_tenant",MySqlDbType.Binary).Value=
                MySqlRfc4122Guid.Encode(trustedTenant.Value);
            cmd.Parameters.Add("p_host_revision",MySqlDbType.UInt64).Value=
                (ulong)verifiedHostRevision;
            cmd.Parameters.Add("p_candidate",MySqlDbType.Binary).Value=
                MySqlRfc4122Guid.Encode(protectedCandidate.Value);
            cmd.Parameters.Add("p_approval",MySqlDbType.Binary).Value=
                MySqlRfc4122Guid.Encode(backendApprovalId);
            cmd.Parameters.Add("p_link",MySqlDbType.Binary).Value=
                MySqlRfc4122Guid.Encode(backendLinkId);
            cmd.Parameters.Add("p_token_digest",MySqlDbType.Binary).Value=digest;
            var result=await cmd.ExecuteScalarAsync(cancellationToken);
            return result switch
            {
                "ACTIVATED" => true,
                "DENIED" => false,
                _ => throw new InvalidOperationException(
                    "Unknown protected MFA membership approval result.")
            };
        }
        finally {CryptographicOperations.ZeroMemory(digest);}
    }

    public override string ToString() =>
        "MySqlDisposableMfaMembershipApprovalStore[redacted]";
}
