using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Crecisol.Application.Identity;
using Crecisol.Domain.Authorization;
using Crecisol.Domain.Tenancy;
using Crecisol.Infrastructure.Authorization;

namespace Crecisol.Infrastructure.Identity;

/// <summary>
/// Isolated C06-11 preparation and confirmation inside the existing locked
/// C05 transaction. Neither operation is registered as HTTP or browser API.
/// No real commercial contract, role bootstrap or private-read activation.
/// </summary>
public sealed partial class MySqlOperationalIdentityFlow
{
    private const string DelegationAction = "identity.private.read";
    private sealed record DelegationSelection(
        C06DelegationCommand Command, Guid Branch, Guid Resource, Guid Membership, Guid Role,
        long RoleRevision, long SubjectRevision, long CeilingRevision, long GrantRevision,
        DateTimeOffset? Until, string Reason, string? Challenge, string? Code)
    {
        public override string ToString() => "DelegationSelection[redacted]";
    }

    private static DelegationSelection? ParseDelegation(OperationalIdentityRequest r)
    {
        var confirm = r.Operation == "delegation/confirm";
        string[] names = ["command","branchId","resourceId","membershipId","roleId",
            "roleRevision","membershipRevision","ceilingRevision","grantRevision","expiresAtUtc","reason"];
        if (r.Fields.Count != names.Length + (confirm ? 2 : 0) ||
            names.Any(n => !r.Fields.TryGetValue(n,out var v) || v is null || v.Length is < 1 or > 512) ||
            (confirm && (!r.Fields.TryGetValue("challenge",out var token) || token is not {Length:64} ||
                !token.All(Uri.IsHexDigit) || !r.Fields.TryGetValue("code",out var code) ||
                code is not {Length:6} || !code.All(char.IsAsciiDigit))))
            return null;
        string F(string name) => r.Fields[name];
        var command = F("command") switch
        {
            "assign" => C06DelegationCommand.Assign,
            "revoke" => C06DelegationCommand.Revoke,
            _ => (C06DelegationCommand)0
        };
        static bool Key(string value, out Guid id) => Guid.TryParseExact(value,"D",out id) && id!=Guid.Empty;
        static bool Revision(string value,out long n) => value.Length<=19 &&
            long.TryParse(value,NumberStyles.None,CultureInfo.InvariantCulture,out n) && n>=0;
        if (!Enum.IsDefined(command) || !Key(F("branchId"),out var branch) || !Key(F("resourceId"),out var resource) ||
            !Key(F("membershipId"),out var membership) || !Key(F("roleId"),out var role) ||
            !Revision(F("roleRevision"),out var rr) || rr==0 ||
            !Revision(F("membershipRevision"),out var mr) || mr==0 ||
            !Revision(F("ceilingRevision"),out var cr) || cr==0 ||
            !Revision(F("grantRevision"),out var gr) ||
            !MySqlC06DelegationLedger.ValidReason(F("reason"))) return null;
        DateTimeOffset? until = null;
        if (command==C06DelegationCommand.Assign)
        {
            if (!DateTimeOffset.TryParseExact(F("expiresAtUtc"),"O",CultureInfo.InvariantCulture,
                    DateTimeStyles.None,out var expiry) || expiry.Offset!=TimeSpan.Zero ||
                expiry.Ticks%10!=0) return null;
            until=expiry;
        }
        else if (F("expiresAtUtc")!="none") return null;
        return new(command,branch,resource,membership,role,rr,mr,cr,gr,until,F("reason"),
            confirm ? F("challenge").ToUpperInvariant() : null,confirm ? F("code") : null);
    }

    private async Task<OperationalIdentityResult> C06DelegationTransition(
        Unit u, byte[] tenant, byte[] actor, Dictionary<string,object> account,
        Dictionary<string,object> member, Dictionary<string,object> session,
        Dictionary<string,object> sessionFactor, int idle, OperationalIdentityRequest r,
        CancellationToken ct)
    {
        if (Environment.GetEnvironmentVariable("CRECISOL_C06_DELEGATION_LAB_ENABLED")!="true" ||
            !r.Host.EndsWith(".example.test",StringComparison.Ordinal) || !u.VerifiedActor ||
            u.VerifiedHost is null) return new(false);
        var s=ParseDelegation(r);
        if(s is null || !await HasCurrentTerms(u,tenant,actor)) return new(false);
        var branch=s.Branch.ToByteArray(true);
        var resource=s.Resource.ToByteArray(true);
        var target=s.Membership.ToByteArray(true);
        var roleId=s.Role.ToByteArray(true);
        var actorMembership=Bytes(member,"membership_id");
        var sessionId=Bytes(session,"session_id");

        // All authorization metadata is read from the same transaction.
        // No private display_reference or credential payload is selected here.
        var ent=await u.One("""
            SELECT revision,enabled,valid_from_utc,valid_until_utc
            FROM c06_delegation_lab_entitlement WHERE tenant_id=@t FOR SHARE
            """,("t",tenant));
        if(ent is null || Number(ent,"enabled")!=1) return new(false);
        var scoped=await u.One("""
            SELECT r.tenant_id,r.branch_id,r.resource_id,r.owner_account_id,r.resource_state,
                   b.active AS branch_active
            FROM c05_identity_private_resource r JOIN c05_identity_branch b
              ON b.tenant_id=r.tenant_id AND b.branch_id=r.branch_id
            WHERE r.tenant_id=@t AND r.branch_id=@b AND r.resource_id=@r FOR SHARE
            """,("t",tenant),("b",branch),("r",resource));
        var subject=await u.One("""
            SELECT m.tenant_id,m.membership_id,m.account_id,m.role_code,m.approval_revision,
                   m.membership_state,m.granted_at_utc,m.expires_at_utc,
                   a.account_state,a.mailbox_verified_at_utc
            FROM c05_identity_membership m JOIN c05_identity_account a ON a.account_id=m.account_id
            WHERE m.tenant_id=@t AND m.membership_id=@m FOR SHARE
            """,("t",tenant),("m",target));
        var role=await u.One("""
            SELECT tenant_id,role_id,role_code,revision,created_at_utc,retired_at_utc
            FROM c06_delegation_role WHERE tenant_id=@t AND role_id=@role FOR SHARE
            """,("t",tenant),("role",roleId));
        var ceiling=await u.One("""
            SELECT tenant_id,actor_membership_id,branch_id,resource_id,action_id,revision,
                   may_assign,may_revoke,valid_from_utc,valid_until_utc,revoked_at_utc
            FROM c06_delegation_ceiling
            WHERE tenant_id=@t AND actor_membership_id=@m AND branch_id=@b
              AND resource_id=@r AND action_id='identity.private.read' FOR SHARE
            """,("t",tenant),("m",actorMembership),("b",branch),("r",resource));
        if(scoped is null || subject is null || role is null || ceiling is null) return new(false);

        Dictionary<string,object>? own=null;
        if(s.Command==C06DelegationCommand.Assign)
        {
            own=await u.One("""
                SELECT grant_id,tenant_id,account_id,branch_id,resource_id,permission_code,
                       scope_kind,grant_revision,granted_at_utc,expires_at_utc,revoked_at_utc
                FROM c05_identity_resource_grant
                WHERE tenant_id=@t AND account_id=@a AND branch_id=@b
                  AND permission_code='RESOURCE_READ' AND revoked_at_utc IS NULL
                  AND granted_at_utc<=UTC_TIMESTAMP(6) AND expires_at_utc>UTC_TIMESTAMP(6)
                  AND ((scope_kind=1 AND resource_id=@r) OR (scope_kind=2 AND resource_id IS NULL))
                LIMIT 2 FOR SHARE
                """,("t",tenant),("a",actor),("b",branch),("r",resource));
            if(own is null) return new(false);
        }
        // The active TOTP row is locked before both eligibility and consumption.
        // The existing C05 verifier and monotonic last_accepted_step are reused.
        var stepFactor=await u.One("""
            SELECT * FROM c05_identity_mfa_factor
            WHERE account_id=@a AND factor_kind=1 AND factor_state=1
              AND revoked_at_utc IS NULL FOR UPDATE
            """,("a",actor));
        if(stepFactor is null) return new(false);
        var subjectActor=DbActor(Bytes(subject,"account_id"));
        var scope=new C06DelegationScope(DbTenant(tenant),DbBranch(branch),DbResource(resource),
            TenantRequiredAction.FromServerPolicy(DelegationAction));
        var intent=new C06DelegationIntent(s.Command,scope,subjectActor,s.Membership,s.Role,
            s.RoleRevision,s.SubjectRevision,s.CeilingRevision,s.GrantRevision,s.Until);
        var ledger=u.DelegationLedger();
        var existing=await ledger.ReadEvidenceAsync(intent,ct);
        var clock=await u.One("SELECT UTC_TIMESTAMP(6) AS trusted_now_utc");
        if(clock is null) return new(false);
        var now=Utc(clock,"trusted_now_utc");
        var identity=ResolveC06TenantContextFromLockedRows(u.VerifiedHost,tenant,account,member,
            session,sessionFactor,idle,now);
        var entUntil=Utc(ent,"valid_until_utc");
        if(Number(ent,"revision")<=0 || Utc(ent,"valid_from_utc")>now || entUntil<=now ||
            (s.Until is { } expires && (expires>entUntil || expires>Utc(member,"expires_at_utc"))))
            return new(false);
        var verifiedScope=new C06DelegationScope(DbTenant(Bytes(scoped,"tenant_id")),
            DbBranch(Bytes(scoped,"branch_id")),DbResource(Bytes(scoped,"resource_id")),scope.Action);
        var actorRole=Text(member,"role_code");
        C06CaseAbacEvidence? ownRead=own is null ? null : new()
        {
            Identity=identity, ResourceTenant=verifiedScope.Tenant, ResourceBranch=verifiedScope.Branch,
            ResourceId=verifiedScope.Resource, ResourceOwner=DbActor(Bytes(scoped,"owner_account_id")),
            SelectorBranch=scope.Branch,SelectorResource=scope.Resource,
            BranchActive=Number(scoped,"branch_active")==1,ResourceActive=Number(scoped,"resource_state")==1,
            RoleCode=actorRole, RequiredAction=scope.Action,GrantTenant=DbTenant(Bytes(own,"tenant_id")),
            GrantActor=DbActor(Bytes(own,"account_id")),GrantBranch=DbBranch(Bytes(own,"branch_id")),
            GrantResource=own["resource_id"] is DBNull ? null : DbResource(Bytes(own,"resource_id")),
            GrantPermissionCode=Text(own,"permission_code"),GrantScope=checked((int)Number(own,"scope_kind")),
            GrantRevision=Number(own,"grant_revision"),GrantIssuedAtUtc=Utc(own,"granted_at_utc"),
            GrantExpiresAtUtc=DbNullableUtc(own,"expires_at_utc"),GrantRevokedAtUtc=DbNullableUtc(own,"revoked_at_utc"),
            Assignment=null,CheckedAtUtc=now
        };
        var e=new C06LockedDelegationEvidence
        {
            Identity=identity,Intent=intent,VerifiedResourceScope=verifiedScope,
            BranchActive=Number(scoped,"branch_active")==1,ResourceActive=Number(scoped,"resource_state")==1,
            CommercialAccessVerified=true,ActorRoleCode=actorRole,SubjectTenant=DbTenant(Bytes(subject,"tenant_id")),
            SubjectActor=subjectActor,SubjectMembershipId=DbId(Bytes(subject,"membership_id")),
            SubjectMembershipRevision=Number(subject,"approval_revision"),
            SubjectMembershipActive=Number(subject,"membership_state")==1 && Number(subject,"account_state")==1 &&
                subject["mailbox_verified_at_utc"] is not DBNull &&
                DbNullableUtc(subject,"granted_at_utc") is { } granted && granted<=now &&
                Text(subject,"role_code") is ("MEMBER" or "STAFF" or "VIEWER" or "MANAGER" or
                    "tenant-staff" or "tenant-viewer" or "tenant-branch-manager"),
            SubjectMembershipUntilUtc=DbNullableUtc(subject,"expires_at_utc")??default,
            VerifiedRoleId=DbId(Bytes(role,"role_id")),RoleTenant=DbTenant(Bytes(role,"tenant_id")),
            RoleRevision=Number(role,"revision"),TargetRoleCode=Text(role,"role_code"),
            RoleActive=role["retired_at_utc"] is DBNull && Utc(role,"created_at_utc")<=now,
            Ceiling=new(new(DbTenant(Bytes(ceiling,"tenant_id")),DbBranch(Bytes(ceiling,"branch_id")),
                DbResource(Bytes(ceiling,"resource_id")),TenantRequiredAction.FromServerPolicy(Text(ceiling,"action_id"))),
                identity.ActorId,DbId(Bytes(ceiling,"actor_membership_id")),Number(ceiling,"revision"),
                Number(ceiling,"may_assign")==1,Number(ceiling,"may_revoke")==1,
                Utc(ceiling,"valid_from_utc"),Utc(ceiling,"valid_until_utc"),DbNullableUtc(ceiling,"revoked_at_utc")),
            ActorEffectiveRead=ownRead,Existing=existing,NowUtc=now,
            // The preparation check cannot return a mutation decision.
            // A real StepUp is constructed ONLY after the locked C05 verifier succeeds.
            StepUp=null!
        };
        if(!C06ScopedRoleDelegationPolicy.CanPrepare(e)) return new(false);
        var binding=DelegationConfirmationBinding(e,DbId(sessionId),s.Reason,ent,stepFactor,own);
        try
        {
            if(r.Operation=="delegation/prepare")
            {
                var count=await u.One("""
                    SELECT COUNT(*) AS pending FROM c06_delegation_confirmation
                    WHERE tenant_id=@t AND session_id=@s AND consumed_at_utc IS NULL
                      AND expires_at_utc>UTC_TIMESTAMP(6)
                    """,("t",tenant),("s",sessionId));
                // The session row is already locked; this bounds outstanding proofs.
                if(count is null || Number(count,"pending")>=3) return new(false);
                var until=new[]{now.AddMinutes(5),entUntil,e.Ceiling.UntilUtc,
                    Utc(member,"expires_at_utc"),Utc(session,"expires_at_utc"),
                    intent.UntilUtc??DateTimeOffset.MaxValue,
                    ownRead?.GrantExpiresAtUtc??DateTimeOffset.MaxValue}.Min();
                if(until<=now) return new(false);
                var token=Token();
                var tokenHash=Digest("c06-delegation-confirmation-token",token);
                try
                {
                    if(await u.Run("""
                        INSERT INTO c06_delegation_confirmation
                        (tenant_id,challenge_hmac,actor_membership_id,session_id,binding_hmac,issued_at_utc,expires_at_utc)
                        VALUES(@t,@h,@m,@s,@binding,@now,@until)
                        """,("t",tenant),("h",tokenHash),("m",actorMembership),("s",sessionId),
                        ("binding",binding),("now",now.UtcDateTime),("until",until.UtcDateTime))!=1)
                        throw new InvalidOperationException("Delegation preparation was not stored.");
                    return new(true,new C06DelegationPreparation(token,until));
                }
                finally { CryptographicOperations.ZeroMemory(tokenHash); }
            }
            var hash=Digest("c06-delegation-confirmation-token",s.Challenge!);
            try
            {
                var confirmation=await u.One("""
                    SELECT binding_hmac FROM c06_delegation_confirmation
                    WHERE tenant_id=@t AND session_id=@s AND actor_membership_id=@m
                      AND challenge_hmac=@h AND consumed_at_utc IS NULL AND expires_at_utc>UTC_TIMESTAMP(6)
                    FOR UPDATE
                    """,("t",tenant),("s",sessionId),("m",actorMembership),("h",hash));
                if(confirmation is null || !CryptographicOperations.FixedTimeEquals(
                       Bytes(confirmation,"binding_hmac"),binding)) return new(false);
                if(!await Reserve(tenant,"stepup:"+Hex(actor),8,ct) ||
                   !await AcceptTotp(u,actor,stepFactor,s.Code!,activate:false)) return new(false);
                // One-use confirmation and monotonic factor step, ledger and C05
                // audit must commit together or the entire caller transaction rolls back.
                if(await u.Run("""
                    UPDATE c06_delegation_confirmation SET consumed_at_utc=@now
                    WHERE tenant_id=@t AND session_id=@s AND actor_membership_id=@m
                      AND challenge_hmac=@h AND consumed_at_utc IS NULL AND expires_at_utc>UTC_TIMESTAMP(6)
                    """,("now",now.UtcDateTime),("t",tenant),("s",sessionId),("m",actorMembership),("h",hash))!=1)
                    throw new InvalidOperationException("Delegation confirmation lost its revision.");
                var proved=e with {StepUp=new(intent,identity.TenantId,identity.ActorId,identity.MembershipId,
                    identity.SessionRevision,identity.AccountRevision,identity.MembershipRevision,now)};
                if(!await ledger.AppendAsync(proved,DbId(sessionId),tokenKey,s.Reason,ct))
                    throw new InvalidOperationException("Delegation decision changed before append.");
                u.Subject=Bytes(subject,"account_id");
                return new(true,new {accepted=true});
            }
            finally { CryptographicOperations.ZeroMemory(hash); }
        }
        finally { CryptographicOperations.ZeroMemory(binding); }
    }

    private byte[] DelegationConfirmationBinding(C06LockedDelegationEvidence e,Guid sessionId,string reason,
        Dictionary<string,object> entitlement,Dictionary<string,object> factor,Dictionary<string,object>? own)
    {
        var (purpose,intent)=MySqlC06DelegationLedger.Digests(e,sessionId,tokenKey,reason);
        try
        {
            using var stream=new MemoryStream();
            using var writer=new BinaryWriter(stream,Encoding.UTF8,leaveOpen:true);
            writer.Write("CRECISOL/C06/delegation/confirmation/v1");
            writer.Write(intent);
            writer.Write(Bytes(factor,"factor_id"));
            writer.Write(Number(factor,"factor_revision"));
            writer.Write(Number(entitlement,"revision"));
            writer.Write(Utc(entitlement,"valid_from_utc").UtcTicks);
            writer.Write(Utc(entitlement,"valid_until_utc").UtcTicks);
            writer.Write(own is not null);
            if(own is not null)
            {
                writer.Write(Bytes(own,"grant_id"));
                writer.Write(Number(own,"grant_revision"));
                writer.Write(Utc(own,"granted_at_utc").UtcTicks);
                writer.Write(Utc(own,"expires_at_utc").UtcTicks);
            }
            writer.Flush();
            var data=stream.ToArray();
            try { return HMACSHA256.HashData(tokenKey,data); }
            finally { CryptographicOperations.ZeroMemory(data); }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(purpose);
            CryptographicOperations.ZeroMemory(intent);
        }
    }
}
