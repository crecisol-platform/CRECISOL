using System.Data;
using System.Security.Cryptography;
using Crecisol.Application.Identity;
using Crecisol.Domain.Tenancy;
using MySqlConnector;

namespace Crecisol.Infrastructure.Identity;

/// <summary>
/// Test-only, EXECUTE-only transactional MySQL 8.4 MFA adapter.
/// It does not own TOTP seeds, passkeys, passwords, production DSNs,
/// client-supplied tenant routing or any public API integration.
/// </summary>
public sealed class MySqlDisposableMfaLedger : ITrustedDisposableMfaLedger
{
    private readonly string dsn;

    public MySqlDisposableMfaLedger(string? trustedLocalTestDsn)
    {
        if (string.IsNullOrWhiteSpace(trustedLocalTestDsn))
            throw new InvalidOperationException("C05-08 local fixture DSN required.");
        var b=new MySqlConnectionStringBuilder(trustedLocalTestDsn)
        {
            Pooling=false, ConnectionTimeout=5, DefaultCommandTimeout=15
        };
        if (b.Server!="127.0.0.1" || b.Database!="c05_05_disposable" ||
            b.AllowLoadLocalInfile || b.Port is < 1 or > 65535)
            throw new InvalidOperationException("C05-08 forbids nonloopback or product DSN.");
        dsn=b.ConnectionString;
    }

    public async Task<bool> TryOpenEnrollmentAsync(PreparedContextualLoginEvidence proof,
        Guid factor, DisposableMfaKind kind, Guid challenge,
        byte[] challengeDigest, byte[] backupDigest, CancellationToken ct)
    {
        VerifyProof(proof);
        ValidateId(factor); ValidateId(challenge);
        ValidateDigest(challengeDigest); ValidateDigest(backupDigest);
        if (!Enum.IsDefined(kind)) return false;
        await using var connection=await ConnectAsync(ct);
        await using var cmd=new MySqlCommand("c05_08_open_enrollment",connection)
            {CommandType=CommandType.StoredProcedure};
        AddProof(cmd,proof);
        AddId(cmd,"@p_factor",factor);
        AddText(cmd,"@p_kind",kind==DisposableMfaKind.Totp?"TOTP":"PASSKEY");
        AddId(cmd,"@p_challenge",challenge);
        AddBytes(cmd,"@p_digest",challengeDigest);
        AddBytes(cmd,"@p_backup",backupDigest);
        return await RunAsync(cmd,"PENDING",ct);
    }

    public async Task<bool> TryOpenStepUpAsync(PreparedContextualLoginEvidence proof,
        Guid factor, Guid challenge, byte[] challengeDigest,
        Guid operation, CancellationToken ct)
    {
        VerifyProof(proof);
        ValidateId(factor);ValidateId(challenge);ValidateId(operation);
        ValidateDigest(challengeDigest);
        await using var connection=await ConnectAsync(ct);
        await using var cmd=new MySqlCommand("c05_08_open_stepup",connection)
            {CommandType=CommandType.StoredProcedure};
        AddProof(cmd,proof);
        AddId(cmd,"@p_factor",factor);
        AddId(cmd,"@p_challenge",challenge);
        AddBytes(cmd,"@p_digest",challengeDigest);
        AddId(cmd,"@p_operation",operation);
        AddText(cmd,"@p_risk","TENANT_SENSITIVE");
        return await RunAsync(cmd,"ISSUED",ct);
    }

    public async Task<bool> TryOpenReplacementAsync(
        PreparedContextualLoginEvidence proof,
        Guid oldFactor, byte[] backupDigest, Guid newFactor,
        DisposableMfaKind kind, Guid challenge, byte[] challengeDigest,
        CancellationToken ct)
    {
        VerifyProof(proof);
        ValidateId(oldFactor);ValidateId(newFactor);ValidateId(challenge);
        ValidateDigest(backupDigest);ValidateDigest(challengeDigest);
        if (!Enum.IsDefined(kind) || oldFactor==newFactor) return false;
        await using var connection=await ConnectAsync(ct);
        await using var cmd=new MySqlCommand("c05_08_open_replacement",connection)
            {CommandType=CommandType.StoredProcedure};
        AddProof(cmd,proof);
        AddId(cmd,"@p_old",oldFactor);
        AddBytes(cmd,"@p_backup",backupDigest);
        AddId(cmd,"@p_new",newFactor);
        AddText(cmd,"@p_new_kind",kind==DisposableMfaKind.Totp?"TOTP":"PASSKEY");
        AddId(cmd,"@p_challenge",challenge);
        AddBytes(cmd,"@p_digest",challengeDigest);
        return await RunAsync(cmd,"PENDING",ct);
    }

    public async Task<DisposableMfaCounter?> ReadCounterAsync(TenantId tenant,
        TenantActorId actor, Guid factor, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        ArgumentNullException.ThrowIfNull(actor);
        ValidateId(factor);
        await using var connection=await ConnectAsync(ct);
        await using var cmd=new MySqlCommand("c05_08_read_counter",connection)
            {CommandType=CommandType.StoredProcedure};
        AddId(cmd,"@p_tenant",tenant.Value);
        AddId(cmd,"@p_actor",actor.Value);
        AddId(cmd,"@p_factor",factor);
        await using var reader=await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        var counter=reader.GetInt64(0);
        string kind=reader.GetString(1);
        string state=reader.GetString(2);
        if (counter < -1 || (kind!="TOTP" && kind!="PASSKEY") ||
            (state!="PENDING" && state!="ACTIVE" && state!="REVOKED"))
            throw new InvalidOperationException("Invalid MFA factor data.");
        if (await reader.ReadAsync(ct))
            throw new InvalidOperationException("Multiple rows for one factor.");
        return new DisposableMfaCounter(
            kind=="TOTP"?DisposableMfaKind.Totp:DisposableMfaKind.Passkey,
            state,counter);
    }

    public async Task<bool> TryCommitCryptographicProofAsync(TenantId tenant,
        TenantActorId actor, Guid factor, Guid challenge,
        byte[] challengeDigest, Guid operation,
        DisposableMfaProofPurpose purpose, long authenticatedCounter,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        ArgumentNullException.ThrowIfNull(actor);
        ValidateId(factor);ValidateId(challenge);ValidateId(operation);
        ValidateDigest(challengeDigest);
        if (!Enum.IsDefined(purpose) || authenticatedCounter<0) return false;
        await using var connection=await ConnectAsync(ct);
        await using var cmd=new MySqlCommand("c05_08_commit_proof",connection)
            {CommandType=CommandType.StoredProcedure};
        AddId(cmd,"@p_tenant",tenant.Value);
        AddId(cmd,"@p_actor",actor.Value);
        AddId(cmd,"@p_factor",factor);
        AddId(cmd,"@p_challenge",challenge);
        AddBytes(cmd,"@p_digest",challengeDigest);
        AddId(cmd,"@p_operation",operation);
        AddText(cmd,"@p_kind",purpose==DisposableMfaProofPurpose.Enroll
            ?"ENROLL":"STEPUP");
        cmd.Parameters.Add("@p_counter",MySqlDbType.Int64).Value=authenticatedCounter;
        return await RunAsync(cmd,purpose==DisposableMfaProofPurpose.Enroll
            ?"ACTIVE":"PROVED",ct);
    }

    private async Task<MySqlConnection> ConnectAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var db=new MySqlConnection(dsn);
        try { await db.OpenAsync(ct);return db; }
        catch { await db.DisposeAsync();throw; }
    }

    private static async Task<bool> RunAsync(
        MySqlCommand cmd,string expected,CancellationToken ct)
    {
        var result=await cmd.ExecuteScalarAsync(ct);
        if (result is not string status ||
            (status!="DENIED" && status!=expected))
            throw new InvalidOperationException("Unexpected protected MFA CAS response.");
        return status==expected;
    }

    private static void VerifyProof(PreparedContextualLoginEvidence proof)
    {
        ArgumentNullException.ThrowIfNull(proof);
        if (proof.AccountRevision<1 || proof.CredentialRevision<1 ||
            proof.MembershipRevision<1 || proof.TenantCatalogRevision<1 ||
            proof.HostRevision<1)
            throw new InvalidOperationException("Only fresh C05-04 revisions allowed.");
    }
    private static void ValidateId(Guid id)
    {
        if (id==Guid.Empty) throw new InvalidOperationException("MFA operation ID required.");
    }
    private static void ValidateDigest(byte[] digest)
    {
        if (digest is null || digest.Length!=32 ||
            digest.AsSpan().IndexOfAnyExcept((byte)0)<0)
            throw new InvalidOperationException("MFA keyed digest required.");
    }
    private static void AddId(MySqlCommand cmd,string name,Guid value) =>
        cmd.Parameters.Add(name,MySqlDbType.Binary).Value=
            value.ToByteArray(bigEndian:true);
    private static void AddText(MySqlCommand cmd,string name,string value) =>
        cmd.Parameters.Add(name,MySqlDbType.VarChar).Value=value;
    private static void AddBytes(MySqlCommand cmd,string name,byte[] bytes) =>
        cmd.Parameters.Add(name,MySqlDbType.Binary).Value=bytes;

    private static void AddProof(MySqlCommand cmd,PreparedContextualLoginEvidence proof)
    {
        AddId(cmd,"@p_tenant",proof.TenantId.Value);
        AddId(cmd,"@p_actor",proof.ActorId.Value);
        AddId(cmd,"@p_link",proof.LinkId.Value);
        cmd.Parameters.Add("@p_account",MySqlDbType.UInt64).Value=(ulong)proof.AccountRevision;
        cmd.Parameters.Add("@p_credential",MySqlDbType.UInt64).Value=(ulong)proof.CredentialRevision;
        cmd.Parameters.Add("@p_member",MySqlDbType.UInt64).Value=(ulong)proof.MembershipRevision;
        cmd.Parameters.Add("@p_tenant_rev",MySqlDbType.UInt64).Value=(ulong)proof.TenantCatalogRevision;
        cmd.Parameters.Add("@p_host",MySqlDbType.UInt64).Value=(ulong)proof.HostRevision;
    }

    public override string ToString() => "MySqlDisposableMfaLedger[redacted]";
}
