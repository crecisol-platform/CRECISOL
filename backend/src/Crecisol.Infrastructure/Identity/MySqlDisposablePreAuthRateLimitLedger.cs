using System.Data;
using System.Security.Cryptography;
using Crecisol.Application.Identity;
using Crecisol.Application.Tenancy;
using Crecisol.Domain.Tenancy;
using Crecisol.Domain.Identity;
using MySqlConnector;

namespace Crecisol.Infrastructure.Identity;

/// <summary>
/// C05-10 disposable MySQL8.4-only adapter. No production DSN, read rights,
/// bulk log collector, real authentication or caller-supplied tenant locator.
/// MySQL checks tenant purpose/policy/host revision and CAS under FOR UPDATE.
/// </summary>
public sealed class MySqlDisposablePreAuthRateLimitLedger :
    ITrustedPreAuthRateLimitLedger
{
    private readonly string dsn;

    public MySqlDisposablePreAuthRateLimitLedger(string? localTestDsn)
    {
        if(string.IsNullOrWhiteSpace(localTestDsn))
            throw new InvalidOperationException("C05-10 disposable MySQL required.");
        var b=new MySqlConnectionStringBuilder(localTestDsn)
        {
            Pooling=false,ConnectionTimeout=5,DefaultCommandTimeout=15
        };
        if(b.Server!="127.0.0.1" || b.Database!="c05_10_disposable" ||
           b.AllowLoadLocalInfile || b.Port is <1 or >65535)
            throw new InvalidOperationException("C05-10 forbids external/product MySQL.");
        dsn=b.ConnectionString;
    }

    public async Task<PreAuthAdmissionStatus> AdmitAsync(
        VerifiedTenantHostHint verifiedHost,PreAuthRateLimitPurpose purpose,
        Guid serverAttemptId,PreAuthKeyedDigest source,PreAuthKeyedDigest subject,
        CancellationToken ct)
    {
        Validate(verifiedHost,purpose,serverAttemptId,source,subject);
        await using var connection=await ConnectAsync(ct);
        await using var command=new MySqlCommand("c05_10_admit_once",connection)
        {CommandType=CommandType.StoredProcedure};
        AddScope(command,verifiedHost,purpose,serverAttemptId,
            includeHostRevision:true);
        byte[] sourceBytes=source.CopyForTrustedLedger();
        byte[] subjectBytes=subject.CopyForTrustedLedger();
        try
        {
            AddDigests(command,sourceBytes,subjectBytes);
            var status=await command.ExecuteScalarAsync(ct);
            return status switch
            {
                "ADMITTED" => PreAuthAdmissionStatus.Admitted,
                "LIMITED" => PreAuthAdmissionStatus.Limited,
                "DENIED" => PreAuthAdmissionStatus.Denied,
                _ => throw new InvalidOperationException("Unknown private rate limiter response.")
            };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(sourceBytes);
            CryptographicOperations.ZeroMemory(subjectBytes);
        }
    }

    public async Task<bool> CompleteAsync(
        VerifiedTenantHostHint verifiedHost,PreAuthRateLimitPurpose purpose,
        Guid serverAttemptId,PreAuthKeyedDigest source,PreAuthKeyedDigest subject,
        PreAuthPrivateOutcome privateOutcome,CancellationToken ct)
    {
        Validate(verifiedHost,purpose,serverAttemptId,source,subject);
        if(!Enum.IsDefined(privateOutcome))
            throw new InvalidOperationException("Unknown private authentication verdict.");
        await using var connection=await ConnectAsync(ct);
        await using var command=new MySqlCommand("c05_10_complete_once",connection)
        {CommandType=CommandType.StoredProcedure};
        AddScope(command,verifiedHost,purpose,serverAttemptId,
            includeHostRevision:false);
        byte[] sourceBytes=source.CopyForTrustedLedger();
        byte[] subjectBytes=subject.CopyForTrustedLedger();
        try
        {
            AddDigests(command,sourceBytes,subjectBytes);
            command.Parameters.Add("@p_outcome",MySqlDbType.VarChar).Value=
                privateOutcome==PreAuthPrivateOutcome.Verified?"VERIFIED":"REJECTED";
            var status=await command.ExecuteScalarAsync(ct);
            return status switch
            {
                "RECORDED" => true,
                "DENIED" => false,
                _ => throw new InvalidOperationException("Unknown private audit result.")
            };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(sourceBytes);
            CryptographicOperations.ZeroMemory(subjectBytes);
        }
    }

    private async Task<MySqlConnection> ConnectAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var connection=new MySqlConnection(dsn);
        try{await connection.OpenAsync(ct);return connection;}
        catch{await connection.DisposeAsync();throw;}
    }
    private static void Validate(VerifiedTenantHostHint host,
        PreAuthRateLimitPurpose purpose,Guid attempt,
        PreAuthKeyedDigest source,PreAuthKeyedDigest subject)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(subject);
        if(host.Revision<1 || attempt==Guid.Empty || !Enum.IsDefined(purpose))
            throw new InvalidOperationException("Fresh scoped server admission required.");
    }
    private static void AddScope(MySqlCommand command,VerifiedTenantHostHint host,
        PreAuthRateLimitPurpose purpose,Guid attempt,bool includeHostRevision)
    {
        command.Parameters.Add("@p_tenant",MySqlDbType.Binary).Value=
            host.TenantId.Value.ToByteArray(bigEndian:true);
        command.Parameters.Add("@p_purpose",MySqlDbType.VarChar).Value=
            purpose switch
            {
                PreAuthRateLimitPurpose.PasswordLogin=>"LOGIN",
                PreAuthRateLimitPurpose.PasswordRecovery=>"RECOVERY",
                PreAuthRateLimitPurpose.MfaChallenge=>"MFA",
                _=>throw new InvalidOperationException("Unknown protected action.")
            };
        if(includeHostRevision)
            command.Parameters.Add("@p_host_revision",MySqlDbType.UInt64)
                .Value=(ulong)host.Revision;
        command.Parameters.Add("@p_attempt",MySqlDbType.Binary).Value=
            attempt.ToByteArray(bigEndian:true);
    }
    private static void AddDigests(MySqlCommand cmd,byte[] source,byte[] subject)
    {
        cmd.Parameters.Add("@p_source",MySqlDbType.Binary).Value=source;
        cmd.Parameters.Add("@p_subject",MySqlDbType.Binary).Value=subject;
    }
    public override string ToString() =>
        "MySqlDisposablePreAuthRateLimitLedger[redacted]";
}
