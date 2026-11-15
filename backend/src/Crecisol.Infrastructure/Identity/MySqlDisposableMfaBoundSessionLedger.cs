using System.Data;
using System.Security.Cryptography;
using Crecisol.Application.Identity;
using MySqlConnector;

namespace Crecisol.Infrastructure.Identity;

/// <summary>
/// Strict local-only C05 MFA-to-session test adapter. The SQL account has
/// EXECUTE for issue_after_mfa_once, validation, rotate and logout ONLY;
/// it must never have c05_05_issue_once or direct table access.
/// No public API DI, no production DSN and no client-owned tenant routing.
/// </summary>
public sealed class MySqlDisposableMfaBoundSessionLedger : ITrustedMfaBoundSessionLedger
{
    private readonly string dsn;

    public MySqlDisposableMfaBoundSessionLedger(string? trustedLocalDsn)
    {
        if (string.IsNullOrWhiteSpace(trustedLocalDsn))
            throw new InvalidOperationException("Local MFA/session worker required.");
        var config=new MySqlConnectionStringBuilder(trustedLocalDsn)
        {
            Pooling=false,ConnectionTimeout=5,DefaultCommandTimeout=15
        };
        if (config.Server!="127.0.0.1" ||
            config.Database!="c05_05_disposable" ||
            config.AllowLoadLocalInfile ||
            config.Port is <1 or >65535)
            throw new InvalidOperationException("MFA/session store must be disposable loopback.");
        dsn=config.ConnectionString;
    }

    public async Task<bool> TryIssueAfterCommittedStepUpAsync(
        PreparedContextualLoginEvidence prepared,Guid factorId,
        Guid challengeId,Guid operationId,Guid sessionId,
        SessionTokenDigest tokenDigest,CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        ArgumentNullException.ThrowIfNull(tokenDigest);
        if (factorId==Guid.Empty || challengeId==Guid.Empty ||
            operationId==Guid.Empty || sessionId==Guid.Empty ||
            prepared.HostRevision<1 || prepared.AccountRevision<1 ||
            prepared.CredentialRevision<1 || prepared.MembershipRevision<1 ||
            prepared.TenantCatalogRevision<1)
            return false;
        var digest=tokenDigest.CopyForTrustedLedger();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await using var db=new MySqlConnection(dsn);
            await db.OpenAsync(cancellationToken);
            await using var cmd=new MySqlCommand(
                "c05_05_issue_after_mfa_once",db)
            {
                CommandType=CommandType.StoredProcedure
            };
            AddGuid(cmd,"p_tenant",prepared.TenantId.Value);
            AddGuid(cmd,"p_actor",prepared.ActorId.Value);
            AddGuid(cmd,"p_link",prepared.LinkId.Value);
            AddRevision(cmd,"p_account",prepared.AccountRevision);
            AddRevision(cmd,"p_credential",prepared.CredentialRevision);
            AddRevision(cmd,"p_member",prepared.MembershipRevision);
            AddRevision(cmd,"p_tenant_rev",prepared.TenantCatalogRevision);
            AddRevision(cmd,"p_host",prepared.HostRevision);
            AddGuid(cmd,"p_factor",factorId);
            AddGuid(cmd,"p_challenge",challengeId);
            AddGuid(cmd,"p_operation",operationId);
            AddGuid(cmd,"p_session",sessionId);
            cmd.Parameters.Add("p_token_digest",MySqlDbType.Binary).Value=digest;
            var result=await cmd.ExecuteScalarAsync(cancellationToken);
            return result switch
            {
                "ISSUED"=>true,
                "DENIED"=>false,
                _=>throw new InvalidOperationException(
                    "Unknown protected MFA-bound session issuance result.")
            };
        }
        finally {CryptographicOperations.ZeroMemory(digest);}
    }

    private static void AddGuid(MySqlCommand cmd,string name,Guid id) =>
        cmd.Parameters.Add(name,MySqlDbType.Binary).Value=
            id.ToByteArray(bigEndian:true);

    private static void AddRevision(MySqlCommand cmd,string name,long revision)=>
        cmd.Parameters.Add(name,MySqlDbType.UInt64).Value=(ulong)revision;

    public override string ToString()=>"MySqlDisposableMfaBoundSessionLedger[redacted]";
}
