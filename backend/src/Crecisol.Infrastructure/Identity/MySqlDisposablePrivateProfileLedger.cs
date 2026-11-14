using System.Data;
using System.Security.Cryptography;
using Crecisol.Application.Identity;
using MySqlConnector;

namespace Crecisol.Infrastructure.Identity;

/// <summary>
/// C05-09 test-only InnoDB adapter. A runtime-only user has EXECUTE on three
/// scoped stored procedures, no raw table SELECT/UPDATE, no production DSN.
/// The trusted caller evidence is C05-04 freshly checked, never HTTP claims.
/// </summary>
public sealed class MySqlDisposablePrivateProfileLedger : ITrustedPrivateProfileLedger
{
    private readonly string dsn;
    public MySqlDisposablePrivateProfileLedger(string? trustedLocalTestDsn)
    {
        if(string.IsNullOrWhiteSpace(trustedLocalTestDsn))
            throw new InvalidOperationException("Local C05-09 fixture DSN required.");
        var config=new MySqlConnectionStringBuilder(trustedLocalTestDsn)
        {
            Pooling=false,ConnectionTimeout=5,DefaultCommandTimeout=15
        };
        if(config.Server!="127.0.0.1" ||
           config.Database!="c05_05_disposable" ||
           config.AllowLoadLocalInfile || config.Port is < 1 or > 65535)
            throw new InvalidOperationException("Only C05-09 disposable loopback DB allowed.");
        dsn=config.ConnectionString;
    }

    public async Task<TrustedEncryptedPrivateProfile?> ReadSelfAsync(
        PreparedContextualLoginEvidence proof,CancellationToken ct)
    {
        VerifyProof(proof);
        await using var db=await ConnectAsync(ct);
        await using var cmd=new MySqlCommand("c05_09_read_self",db)
        {CommandType=CommandType.StoredProcedure};
        AddProof(cmd,proof);
        await using var reader=await cmd.ExecuteReaderAsync(ct);
        if(!await reader.ReadAsync(ct))
            throw new InvalidOperationException("Private profile procedure returned no result.");
        string state=reader.GetString(0);
        if(state=="DENIED" || state=="NONE")
        {
            if(await reader.ReadAsync(ct))
                throw new InvalidOperationException("Unexpected profile duplicate.");
            return null;
        }
        if(state!="FOUND")
            throw new InvalidOperationException("Unrecognized private profile response.");
        var version=reader.GetInt64(1);
        byte[] cipher=(byte[])reader.GetValue(2);
        try
        {
            Guid? photo=null;
            if(!reader.IsDBNull(3))
            {
                byte[] raw=(byte[])reader.GetValue(3);
                if(raw.Length!=16) throw new InvalidOperationException("Bad photo ID.");
                photo=new Guid(raw,bigEndian:true);
            }
            var result=new TrustedEncryptedPrivateProfile(cipher,photo,version);
            if(await reader.ReadAsync(ct))
                throw new InvalidOperationException("Multiple scoped profile rows returned.");
            return result;
        }
        finally { CryptographicOperations.ZeroMemory(cipher); }
    }

    public async Task<bool> SaveSelfAsync(PreparedContextualLoginEvidence proof,
        ReadOnlyMemory<byte> encryptedName,Guid? trustedMediaId,
        long expectedRevision,CancellationToken ct)
    {
        VerifyProof(proof);
        if(expectedRevision<0 || trustedMediaId==Guid.Empty ||
           encryptedName.Length is < 31 or > 512 || encryptedName.Span[0]!=1)
            return false;
        await using var db=await ConnectAsync(ct);
        await using var cmd=new MySqlCommand("c05_09_save_self",db)
        {CommandType=CommandType.StoredProcedure};
        AddProof(cmd,proof);
        cmd.Parameters.Add("@p_expected",MySqlDbType.UInt64).Value=(ulong)expectedRevision;
        cmd.Parameters.Add("@p_cipher",MySqlDbType.Blob).Value=encryptedName.ToArray();
        cmd.Parameters.Add("@p_photo",MySqlDbType.Binary).Value=
            trustedMediaId.HasValue
            ?trustedMediaId.Value.ToByteArray(bigEndian:true)
            :DBNull.Value;
        try { return await RunAsync(cmd,"SAVED",ct); }
        finally
        {
            if(cmd.Parameters["@p_cipher"].Value is byte[] bytes)
                CryptographicOperations.ZeroMemory(bytes);
        }
    }

    public async Task<bool> EraseSelfAsync(PreparedContextualLoginEvidence proof,
        long expectedRevision,CancellationToken ct)
    {
        VerifyProof(proof);
        if(expectedRevision<1)return false;
        await using var db=await ConnectAsync(ct);
        await using var cmd=new MySqlCommand("c05_09_erase_self",db)
        {CommandType=CommandType.StoredProcedure};
        AddProof(cmd,proof);
        cmd.Parameters.Add("@p_expected",MySqlDbType.UInt64).Value=(ulong)expectedRevision;
        return await RunAsync(cmd,"ERASED",ct);
    }

    private async Task<MySqlConnection> ConnectAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var db=new MySqlConnection(dsn);
        try {await db.OpenAsync(ct);return db;}
        catch {await db.DisposeAsync();throw;}
    }

    private static async Task<bool> RunAsync(
        MySqlCommand cmd,string expected,CancellationToken ct)
    {
        var value=await cmd.ExecuteScalarAsync(ct);
        if(value is not string state ||
           (state!="DENIED"&&state!=expected))
            throw new InvalidOperationException("Unexpected scoped profile CAS outcome.");
        return state==expected;
    }
    private static void VerifyProof(PreparedContextualLoginEvidence proof)
    {
        ArgumentNullException.ThrowIfNull(proof);
        if(proof.AccountRevision<1 || proof.CredentialRevision<1 ||
           proof.MembershipRevision<1 || proof.TenantCatalogRevision<1 ||
           proof.HostRevision<1)
            throw new InvalidOperationException("Fresh C05-04 identity revisions required.");
    }
    private static void AddId(MySqlCommand cmd,string label,Guid id)=>
        cmd.Parameters.Add(label,MySqlDbType.Binary).Value=id.ToByteArray(bigEndian:true);
    private static void AddRevision(MySqlCommand cmd,string label,long value)=>
        cmd.Parameters.Add(label,MySqlDbType.UInt64).Value=(ulong)value;
    private static void AddProof(MySqlCommand cmd,PreparedContextualLoginEvidence proof)
    {
        AddId(cmd,"@p_tenant",proof.TenantId.Value);
        AddId(cmd,"@p_actor",proof.ActorId.Value);
        AddId(cmd,"@p_link",proof.LinkId.Value);
        AddRevision(cmd,"@p_account",proof.AccountRevision);
        AddRevision(cmd,"@p_credential",proof.CredentialRevision);
        AddRevision(cmd,"@p_membership",proof.MembershipRevision);
        AddRevision(cmd,"@p_tenant_revision",proof.TenantCatalogRevision);
        AddRevision(cmd,"@p_host",proof.HostRevision);
    }
    public override string ToString() => "MySqlDisposablePrivateProfileLedger[redacted]";
}
