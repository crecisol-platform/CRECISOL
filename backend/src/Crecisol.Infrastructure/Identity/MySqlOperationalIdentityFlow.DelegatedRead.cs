using Crecisol.Application.Identity;
using Crecisol.Domain.Tenancy;

namespace Crecisol.Infrastructure.Identity;

public sealed partial class MySqlOperationalIdentityFlow
{
    // Internal candidate path only. No HTTP registration and no fallback to
    // delegated authority in historical private/read. Every read reconstructs
    // authority under locks; a cache, a role label or an old token is not a grant.
    private async Task<OperationalIdentityResult> C06ReadDelegatedCase(
        Unit u,byte[] tenant,byte[] actor,Dictionary<string,object> account,
        Dictionary<string,object> member,Dictionary<string,object> session,
        Dictionary<string,object> sessionFactor,int idle,OperationalIdentityRequest request)
    {
        if(Environment.GetEnvironmentVariable("CRECISOL_C06_DELEGATION_LAB_ENABLED")!="true" ||
           !request.Host.EndsWith(".example.test",StringComparison.Ordinal) ||
           !u.VerifiedActor || u.VerifiedHost is null ||
           request.Fields.Count!=2 ||
           !request.Fields.TryGetValue("branchId",out var branchText) ||
           !request.Fields.TryGetValue("resourceId",out var resourceText) ||
           !Guid.TryParseExact(branchText,"D",out var branchId) || branchId==Guid.Empty ||
           !Guid.TryParseExact(resourceText,"D",out var resourceId) || resourceId==Guid.Empty ||
           Text(member,"role_code") is not ("MEMBER" or "STAFF" or "VIEWER" or "MANAGER" or
                "tenant-staff" or "tenant-viewer" or "tenant-branch-manager") ||
           !await HasCurrentTerms(u,tenant,actor)) return new(false);
        var branch=branchId.ToByteArray(true);
        var resource=resourceId.ToByteArray(true);
        // Query only authorization metadata. Exact tenant joins and stored
        // issuance provenance bind the original source grant, not a replacement
        // with the same scope. No active-revision change is silently forgiven.
        var row=await u.One("""
            SELECT g.tenant_id,g.branch_id,g.resource_id,g.action_id,g.revision,
                   g.issued_at_utc,g.expires_at_utc,g.subject_membership_id,
                   x.owner_account_id,b.active AS branch_active,x.resource_state,
                   im.account_id AS issuer_account_id,a.source_grant_id,
                   LEAST(g.expires_at_utc,im.expires_at_utc,sm.expires_at_utc,
                         c.valid_until_utc,src.expires_at_utc,ent.valid_until_utc) AS effective_until_utc
            FROM c06_delegation_cycle_head h
            JOIN c06_delegation_grant g ON g.tenant_id=h.tenant_id AND g.grant_id=h.grant_id
                AND g.subject_membership_id=h.subject_membership_id AND g.branch_id=h.branch_id
                AND g.resource_id=h.resource_id AND g.action_id=h.action_id AND g.role_id=h.role_id
                AND g.revision=h.issuance_revision
            JOIN c06_delegation_authority a ON a.tenant_id=g.tenant_id AND a.grant_id=g.grant_id
            JOIN c06_delegation_confirmation cf ON cf.tenant_id=a.tenant_id AND cf.challenge_hmac=a.challenge_hmac
            JOIN c05_identity_membership sm ON sm.tenant_id=g.tenant_id AND sm.membership_id=g.subject_membership_id
            JOIN c05_identity_account sa ON sa.account_id=sm.account_id
            JOIN c05_identity_membership im ON im.tenant_id=g.tenant_id AND im.membership_id=g.issuer_membership_id
            JOIN c05_identity_account ia ON ia.account_id=im.account_id
            JOIN c06_delegation_role role ON role.tenant_id=g.tenant_id AND role.role_id=g.role_id
            JOIN c06_delegation_ceiling c ON c.tenant_id=g.tenant_id AND c.actor_membership_id=g.issuer_membership_id
                AND c.branch_id=g.branch_id AND c.resource_id=g.resource_id AND c.action_id=g.action_id
            JOIN c05_identity_branch b ON b.tenant_id=g.tenant_id AND b.branch_id=g.branch_id
            JOIN c05_identity_private_resource x ON x.tenant_id=g.tenant_id AND x.branch_id=g.branch_id AND x.resource_id=g.resource_id
            JOIN c05_identity_resource_grant src ON src.tenant_id=a.tenant_id AND src.grant_id=a.source_grant_id
            JOIN c06_delegation_lab_entitlement ent ON ent.tenant_id=g.tenant_id
            LEFT JOIN c06_delegation_revocation v ON v.tenant_id=g.tenant_id AND v.grant_id=g.grant_id
            WHERE g.tenant_id=@t AND g.branch_id=@b AND g.resource_id=@r
              AND g.subject_membership_id=@m AND sm.account_id=@a
              AND g.action_id='identity.private.read' AND h.revision=g.revision
              AND h.terminal_at_utc IS NULL AND MOD(g.revision,2)=1 AND v.grant_id IS NULL
              AND g.issued_at_utc<=UTC_TIMESTAMP(6) AND g.expires_at_utc>UTC_TIMESTAMP(6)
              AND sm.approval_revision=g.subject_revision AND sm.membership_state=1
              AND sm.granted_at_utc<=UTC_TIMESTAMP(6) AND sm.expires_at_utc>UTC_TIMESTAMP(6)
              AND sa.security_revision=a.subject_account_revision AND sa.account_state=1
              AND sa.mailbox_verified_at_utc IS NOT NULL
              AND im.account_id<>sm.account_id AND im.approval_revision=g.issuer_revision
              AND im.membership_state=1 AND im.role_code IN('OWNER','ADMIN','tenant-owner','tenant-admin')
              AND im.granted_at_utc<=UTC_TIMESTAMP(6) AND im.expires_at_utc>UTC_TIMESTAMP(6)
              AND ia.security_revision=a.issuer_account_revision AND ia.account_state=1
              AND ia.mailbox_verified_at_utc IS NOT NULL
              AND role.revision=g.role_revision AND role.retired_at_utc IS NULL
              AND role.role_code IN('tenant-staff','tenant-viewer','tenant-branch-manager')
              AND role.created_at_utc<=UTC_TIMESTAMP(6)
              AND c.revision=g.ceiling_revision AND c.may_assign=1 AND c.revoked_at_utc IS NULL
              AND c.valid_from_utc<=UTC_TIMESTAMP(6) AND c.valid_until_utc>UTC_TIMESTAMP(6)
              AND b.active=1 AND x.resource_state=1 AND x.owner_account_id=a.owner_account_id
              AND src.account_id=im.account_id AND src.branch_id=g.branch_id
              AND src.permission_code='RESOURCE_READ' AND src.grant_revision=a.source_revision
              AND src.revoked_at_utc IS NULL AND src.granted_at_utc<=UTC_TIMESTAMP(6)
              AND src.expires_at_utc>UTC_TIMESTAMP(6)
              AND ((src.scope_kind=2 AND src.resource_id IS NULL) OR
                   (src.scope_kind=1 AND src.resource_id=g.resource_id AND x.owner_account_id=im.account_id))
              AND ent.enabled=1 AND ent.revision=a.entitlement_revision
              AND ent.valid_from_utc<=UTC_TIMESTAMP(6) AND ent.valid_until_utc>UTC_TIMESTAMP(6)
              AND cf.actor_membership_id=g.issuer_membership_id AND cf.consumed_at_utc=g.issued_at_utc
            LIMIT 2 FOR SHARE
            """,("t",tenant),("b",branch),("r",resource),("m",Bytes(member,"membership_id")),("a",actor));
        if(row is null) return new(false);
        var issuer=Bytes(row,"issuer_account_id");
        if(!await HasCurrentTerms(u,tenant,issuer)) return new(false);
        // Preserve the historical ABAC rule: ambiguous original direct
        // authority denies. Never resolve it using a first-match lookup.
        var sources=await u.All("""
            SELECT grant_id FROM c05_identity_resource_grant
            WHERE tenant_id=@t AND account_id=@a AND branch_id=@b AND permission_code='RESOURCE_READ'
              AND revoked_at_utc IS NULL AND granted_at_utc<=UTC_TIMESTAMP(6)
              AND expires_at_utc>UTC_TIMESTAMP(6)
              AND ((scope_kind=1 AND resource_id=@r) OR (scope_kind=2 AND resource_id IS NULL))
            LIMIT 2 FOR SHARE
            """,("t",tenant),("a",issuer),("b",branch),("r",resource));
        if(sources.Count!=1 || !Bytes(sources[0],"grant_id").SequenceEqual(Bytes(row,"source_grant_id")))
            return new(false);
        var clock=await u.One("SELECT UTC_TIMESTAMP(6) AS trusted_now_utc");
        if(clock is null) return new(false);
        var now=Utc(clock,"trusted_now_utc");
        var identity=ResolveC06TenantContextFromLockedRows(u.VerifiedHost,tenant,account,member,session,sessionFactor,idle,now);
        var scopeTenant=DbTenant(Bytes(row,"tenant_id"));
        var scopeBranch=DbBranch(Bytes(row,"branch_id"));
        var scopeResource=DbResource(Bytes(row,"resource_id"));
        var action=TenantRequiredAction.FromServerPolicy(Text(row,"action_id"));
        var until=Utc(row,"effective_until_utc");
        // Translate only the fully verified exact delegation into the
        // existing ABAC branch-context + exact case assignment model.
        // This is not a legacy branch grant, and no such row is ever created.
        var proof=new C06CaseAbacEvidence
        {
            Identity=identity,ResourceTenant=scopeTenant,ResourceBranch=scopeBranch,
            ResourceId=scopeResource,ResourceOwner=DbActor(Bytes(row,"owner_account_id")),
            SelectorBranch=DbBranch(branch),SelectorResource=DbResource(resource),
            BranchActive=Number(row,"branch_active")==1,ResourceActive=Number(row,"resource_state")==1,
            RoleCode=Text(member,"role_code"),RequiredAction=action,
            GrantTenant=scopeTenant,GrantActor=identity.ActorId,GrantBranch=scopeBranch,GrantResource=null,
            GrantPermissionCode="RESOURCE_READ",GrantScope=2,GrantRevision=Number(row,"revision"),
            GrantIssuedAtUtc=Utc(row,"issued_at_utc"),GrantExpiresAtUtc=until,GrantRevokedAtUtc=null,
            Assignment=new C06ExactCaseAssignment
            {
                TenantId=scopeTenant,ActorId=identity.ActorId,BranchId=scopeBranch,ResourceId=scopeResource,
                Action=action,AssignmentState=1,Revision=Number(row,"revision"),
                IssuedAtUtc=Utc(row,"issued_at_utc"),ExpiresAtUtc=until,RevokedAtUtc=null
            },
            CheckedAtUtc=now
        };
        if(!C06CaseAbacPolicy.Allows(proof)) return new(false);
        // Content is selected only AFTER authorization. The exact resource row
        // is already held by the same transaction; no separate unlocked lookup.
        var content=await u.One("""
            SELECT display_reference FROM c05_identity_private_resource
            WHERE tenant_id=@t AND branch_id=@b AND resource_id=@r AND resource_state=1 FOR SHARE
            """,("t",tenant),("b",branch),("r",resource));
        return content is not null && C06ProtectedContent.IsDisplayReference(Text(content,"display_reference"))
            ? new(true,new {reference=Text(content,"display_reference")}) : new(false);
    }
}
