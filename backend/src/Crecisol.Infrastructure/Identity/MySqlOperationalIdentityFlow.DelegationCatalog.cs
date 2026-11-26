using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using Crecisol.Application.Identity;

namespace Crecisol.Infrastructure.Identity;

public sealed partial class MySqlOperationalIdentityFlow
{
    private const int DelegationPageSize=8;
    private const int DelegationCursorBytes=72;
    // Authenticated encryption protects even the last scanned but denied
    // selector. Purpose binds it to this exact host/session and revisions.
    private string DelegationCursorPurpose(Unit u,byte[] tenant,byte[] actor,
        Dictionary<string,object> account,Dictionary<string,object> member,
        Dictionary<string,object> session,Dictionary<string,object> factor,long entitlementRevision) =>
        string.Join(":", "c06-delegation-catalog/v1",Hex(tenant),Hex(actor),Hex(Bytes(member,"membership_id")),
            Hex(Bytes(session,"session_id")),Hex(Bytes(factor,"factor_id")),
            Number(account,"security_revision").ToString(CultureInfo.InvariantCulture),
            Number(member,"approval_revision").ToString(CultureInfo.InvariantCulture),
            Number(session,"session_revision").ToString(CultureInfo.InvariantCulture),
            Number(factor,"factor_revision").ToString(CultureInfo.InvariantCulture),
            Number(u.VerifiedHost!,"host_revision").ToString(CultureInfo.InvariantCulture),
            Number(u.VerifiedHost!,"catalog_revision").ToString(CultureInfo.InvariantCulture),
            entitlementRevision.ToString(CultureInfo.InvariantCulture));

    private bool TryDelegationCursor(string cursor,string purpose,DateTimeOffset now,out byte[] last)
    {
        last=new byte[64];
        if(cursor=="start")return true;
        if(cursor.Length!=136)return false;
        byte[]? plain=null;
        try
        {
            var envelope=Convert.FromBase64String(cursor);
            if(envelope.Length!=DelegationCursorBytes+28 ||
               Convert.ToBase64String(envelope)!=cursor)return false;
            plain=Unprotect(envelope,purpose);
            var expiry=BinaryPrimitives.ReadInt64BigEndian(plain);
            if(expiry<=now.ToUnixTimeSeconds() || expiry>now.ToUnixTimeSeconds()+300)return false;
            plain.AsSpan(8,64).CopyTo(last);
            for(int i=0;i<64;i+=16)
                if(DbId(last.AsSpan(i,16).ToArray())==Guid.Empty)return false;
            return true;
        }
        catch(FormatException){return false;}
        catch(CryptographicException){return false;}
        finally {if(plain is not null)CryptographicOperations.ZeroMemory(plain);}
    }

    private string SealDelegationCursor(Dictionary<string,object> row,string purpose,DateTimeOffset now)
    {
        var plain=new byte[DelegationCursorBytes];
        try
        {
            BinaryPrimitives.WriteInt64BigEndian(plain,now.ToUnixTimeSeconds()+300);
            Bytes(row,"branch_id").CopyTo(plain,8);
            Bytes(row,"resource_id").CopyTo(plain,24);
            Bytes(row,"membership_id").CopyTo(plain,40);
            Bytes(row,"role_id").CopyTo(plain,56);
            return Convert.ToBase64String(Protect(plain,purpose));
        }
        finally {CryptographicOperations.ZeroMemory(plain);}
    }

    private async Task<OperationalIdentityResult> C06DelegationCatalogPage(Unit u,byte[] tenant,byte[] actor,
        Dictionary<string,object> account,Dictionary<string,object> member,Dictionary<string,object> session,
        Dictionary<string,object> factor,int idle,OperationalIdentityRequest request,CancellationToken ct)
    {
        if(Environment.GetEnvironmentVariable("CRECISOL_C06_DELEGATION_LAB_ENABLED")!="true" ||
           !request.Host.EndsWith(".example.test",StringComparison.Ordinal) || !u.VerifiedActor || u.VerifiedHost is null ||
           Text(member,"role_code") is not ("OWNER" or "ADMIN" or "tenant-owner" or "tenant-admin") ||
           request.Fields.Count!=1 || !request.Fields.TryGetValue("cursor",out var cursor) || cursor is null ||
           !await HasCurrentTerms(u,tenant,actor))return new(false);
        var ent=await u.One("""
            SELECT revision,valid_until_utc FROM c06_delegation_lab_entitlement
            WHERE tenant_id=@t AND enabled=1 AND valid_from_utc<=UTC_TIMESTAMP(6)
              AND valid_until_utc>UTC_TIMESTAMP(6) FOR SHARE
            """,("t",tenant));
        var clock=await u.One("SELECT UTC_TIMESTAMP(6) AS trusted_now_utc");
        if(ent is null || clock is null || Number(ent,"revision")<=0)return new(false);
        var now=Utc(clock,"trusted_now_utc");
        var purpose=DelegationCursorPurpose(u,tenant,actor,account,member,session,factor,Number(ent,"revision"));
        if(!TryDelegationCursor(cursor,purpose,now,out var last))return new(false);

        // Bounded candidate metadata, not a general membership directory.
        // Filter by the administrator's exact live ceilings and direct
        // source grant or existing revocable issuance. No private reference,
        // profile, mailbox, credential or arbitrary role is selected.
        var rows=await u.All("""
            SELECT c.branch_id,c.resource_id,m.membership_id,role.role_id
            FROM c06_delegation_ceiling c
            JOIN c05_identity_branch b ON b.tenant_id=c.tenant_id AND b.branch_id=c.branch_id
            JOIN c05_identity_private_resource x ON x.tenant_id=c.tenant_id AND x.branch_id=c.branch_id AND x.resource_id=c.resource_id
            JOIN c05_identity_membership m ON m.tenant_id=c.tenant_id AND m.membership_id<>c.actor_membership_id
            JOIN c06_delegation_role role ON role.tenant_id=c.tenant_id AND role.retired_at_utc IS NULL
            LEFT JOIN c06_delegation_grant g ON g.tenant_id=c.tenant_id AND g.branch_id=c.branch_id
              AND g.resource_id=c.resource_id AND g.action_id=c.action_id AND g.subject_membership_id=m.membership_id
            LEFT JOIN c06_delegation_revocation v ON v.tenant_id=g.tenant_id AND v.grant_id=g.grant_id
            WHERE c.tenant_id=@t AND c.actor_membership_id=@m AND m.account_id<>@a
              AND c.action_id='identity.private.read' AND c.revoked_at_utc IS NULL
              AND c.valid_from_utc<=UTC_TIMESTAMP(6) AND c.valid_until_utc>UTC_TIMESTAMP(6)
              AND role.created_at_utc<=UTC_TIMESTAMP(6) AND b.active=1 AND x.resource_state=1
              AND ((g.grant_id IS NULL AND c.may_assign=1 AND m.membership_state=1
                    AND m.granted_at_utc<=UTC_TIMESTAMP(6) AND m.expires_at_utc>UTC_TIMESTAMP(6)
                    AND m.role_code IN('MEMBER','STAFF','VIEWER','MANAGER','tenant-staff','tenant-viewer','tenant-branch-manager')
                    AND EXISTS(SELECT 1 FROM c05_identity_resource_grant src
                      WHERE src.tenant_id=c.tenant_id AND src.account_id=@a AND src.branch_id=c.branch_id
                        AND src.permission_code='RESOURCE_READ' AND src.revoked_at_utc IS NULL
                        AND src.granted_at_utc<=UTC_TIMESTAMP(6) AND src.expires_at_utc>UTC_TIMESTAMP(6)
                        AND ((src.scope_kind=2 AND src.resource_id IS NULL) OR
                             (src.scope_kind=1 AND src.resource_id=c.resource_id AND x.owner_account_id=@a))))
                   OR (g.role_id=role.role_id AND v.grant_id IS NULL AND c.may_revoke=1))
              AND (@first=1 OR (c.branch_id,c.resource_id,m.membership_id,role.role_id)>(@lb,@lr,@lm,@lrole))
            ORDER BY c.branch_id,c.resource_id,m.membership_id,role.role_id
            LIMIT 9 FOR SHARE
            """,("t",tenant),("m",Bytes(member,"membership_id")),("a",actor),("first",cursor=="start"?1:0),
            ("lb",last[..16]),("lr",last[16..32]),("lm",last[32..48]),("lrole",last[48..64]));
        var items=new List<C06DelegationCatalogItem>(DelegationPageSize);
        var originalSubject=u.Subject;
        try
        {
            foreach(var row in rows.Take(DelegationPageSize))
            {
                ct.ThrowIfCancellationRequested();
                var branch=DbId(Bytes(row,"branch_id"));var resource=DbId(Bytes(row,"resource_id"));
                var membership=DbId(Bytes(row,"membership_id"));var role=DbId(Bytes(row,"role_id"));
                var fields=new Dictionary<string,string>(StringComparer.Ordinal)
                {
                    ["branchId"]=branch.ToString("D"),["resourceId"]=resource.ToString("D"),
                    ["membershipId"]=membership.ToString("D"),["roleId"]=role.ToString("D")
                };
                // Same transaction and exact same CanPrepare as standalone
                // inspection. Candidate query alone NEVER authorizes output.
                var result=await C06DelegationTransition(u,tenant,actor,account,member,session,factor,idle,
                    request with {Operation="delegation/inspect",Fields=fields},ct);
                if(result.Allowed && result.Value is C06DelegationInspection inspection)
                    items.Add(new(branch,resource,membership,role,inspection));
            }
        }
        finally {u.Subject=originalSubject;}
        // Cursor advances by scanned keys, even if a candidate was denied.
        // No concealed truncation and no total count of unapproved members.
        var next=rows.Count>DelegationPageSize ? SealDelegationCursor(rows[DelegationPageSize-1],purpose,now) : null;
        return new(true,new C06DelegationCatalog(items,next));
    }
}
