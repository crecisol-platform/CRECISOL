using Crecisol.Application.Identity;
using Crecisol.Domain.Tenancy;

namespace Crecisol.Infrastructure.Identity;

/// <summary>
/// One atomic C06-04 ABAC decision for the C05 private/read route. All input
/// except a branch/resource lookup selector comes from locked server rows.
/// This method is not exposed as an API or assignable permission service.
/// </summary>
public sealed partial class MySqlOperationalIdentityFlow
{
    private async Task<OperationalIdentityResult> C06ReadAuthorizedCase(
        Unit u, byte[] tenant, byte[] actor,
        Dictionary<string, object> membership,
        C06OperationalMembershipContext verifiedIdentity,
        OperationalIdentityRequest request)
    {
        // Selectors are NEVER permissions; tenant/actor/requiredAction are
        // backend-owned, with authenticated membership already locked in C05.
        var selectedBranch = GuidBytes(Field(request, "branchId"));
        var selectedResource = GuidBytes(Field(request, "resourceId"));
        var resource = await u.One("""
            SELECT r.tenant_id,r.branch_id,r.resource_id,r.owner_account_id,
                   r.resource_state,r.display_reference,b.active AS branch_active
              FROM c05_identity_private_resource r
              JOIN c05_identity_branch b
                ON b.tenant_id=r.tenant_id AND b.branch_id=r.branch_id
             WHERE r.tenant_id=@t AND r.branch_id=@b AND r.resource_id=@r
             FOR SHARE
            """, ("t", tenant), ("b", selectedBranch), ("r", selectedResource));
        if (resource is null) return new(false);

        // All exact active grants are read; duplicates deny rather than
        // selecting an arbitrary row with LIMIT 1 (unsafe ambiguity).
        var grants = await u.All("""
            SELECT tenant_id,account_id,branch_id,resource_id,scope_kind,
                   permission_code,grant_revision,granted_at_utc,expires_at_utc,revoked_at_utc
              FROM c05_identity_resource_grant
             WHERE tenant_id=@t AND account_id=@a AND branch_id=@b
               AND permission_code='RESOURCE_READ'
               AND revoked_at_utc IS NULL AND granted_at_utc<=UTC_TIMESTAMP(6)
               AND expires_at_utc>UTC_TIMESTAMP(6)
               AND ((scope_kind=1 AND resource_id=@r)
                    OR (scope_kind=2 AND resource_id IS NULL))
             LIMIT 2 FOR SHARE
            """, ("t", tenant), ("a", actor), ("b", selectedBranch),
            ("r", selectedResource));
        if (grants.Count != 1) return new(false);
        var grant = grants[0];
        var roleCode = Text(membership, "role_code");
        var grantScope = checked((int)Number(grant, "scope_kind"));

        C06ExactCaseAssignment? assigned = null;
        if (grantScope == 2 &&
            roleCode is not ("OWNER" or "ADMIN" or "tenant-owner" or "tenant-admin"))
        {
            // Regular employees with broad legacy branch grants MUST also have
            // one exact, time-limited C06 case assignment. The migration is
            // intentionally not a grant seed; if absent, SQL fails closed.
            var assignments = await u.All("""
                SELECT tenant_id,account_id,branch_id,resource_id,action_id,
                       assignment_state,revision,issued_at_utc,expires_at_utc,revoked_at_utc
                  FROM c06_case_resource_assignment
                 WHERE tenant_id=@t AND account_id=@a AND branch_id=@b
                   AND resource_id=@r AND action_id='identity.private.read'
                 LIMIT 2 FOR SHARE
                """, ("t", tenant), ("a", actor), ("b", selectedBranch),
                ("r", selectedResource));
            if (assignments.Count != 1) return new(false);
            var a = assignments[0];
            assigned = new C06ExactCaseAssignment
            {
                TenantId = DbTenant(Bytes(a, "tenant_id")),
                ActorId = DbActor(Bytes(a, "account_id")),
                BranchId = DbBranch(Bytes(a, "branch_id")),
                ResourceId = DbResource(Bytes(a, "resource_id")),
                Action = TenantRequiredAction.FromServerPolicy(Text(a, "action_id")),
                AssignmentState = checked((int)Number(a, "assignment_state")),
                Revision = Number(a, "revision"),
                IssuedAtUtc = Utc(a, "issued_at_utc"),
                ExpiresAtUtc = Utc(a, "expires_at_utc"),
                RevokedAtUtc = DbNullableUtc(a, "revoked_at_utc")
            };
        }

        var now = await u.One("SELECT UTC_TIMESTAMP(6) AS server_now_utc");
        if (now is null) return new(false);
        var evidence = new C06CaseAbacEvidence
        {
            Identity = verifiedIdentity,
            ResourceTenant = DbTenant(Bytes(resource, "tenant_id")),
            ResourceBranch = DbBranch(Bytes(resource, "branch_id")),
            ResourceId = DbResource(Bytes(resource, "resource_id")),
            ResourceOwner = DbActor(Bytes(resource, "owner_account_id")),
            SelectorBranch = DbBranch(selectedBranch),
            SelectorResource = DbResource(selectedResource),
            BranchActive = Number(resource, "branch_active") == 1,
            ResourceActive = Number(resource, "resource_state") == 1,
            RoleCode = roleCode,
            RequiredAction = TenantRequiredAction.FromServerPolicy("identity.private.read"),
            GrantTenant = DbTenant(Bytes(grant, "tenant_id")),
            GrantActor = DbActor(Bytes(grant, "account_id")),
            GrantBranch = DbBranch(Bytes(grant, "branch_id")),
            GrantResource = grant["resource_id"] is DBNull
                ? null : DbResource(Bytes(grant, "resource_id")),
            GrantPermissionCode = Text(grant, "permission_code"),
            GrantScope = grantScope,
            GrantRevision = Number(grant, "grant_revision"),
            GrantIssuedAtUtc = Utc(grant, "granted_at_utc"),
            GrantExpiresAtUtc = DbNullableUtc(grant, "expires_at_utc"),
            GrantRevokedAtUtc = DbNullableUtc(grant, "revoked_at_utc"),
            Assignment = assigned,
            CheckedAtUtc = Utc(now, "server_now_utc")
        };
        if (!C06CaseAbacPolicy.Allows(evidence)) return new(false);

        // The private reference is retrieved from the SAME locked resource row,
        // after authorization. No separate lookup, TOCTOU, or ownership bypass.
        var reference = Text(resource, "display_reference");
        return C06ProtectedContent.IsDisplayReference(reference) ? new(true, new { reference }) : new(false);
    }

    private static TenantId DbTenant(byte[] key) => TenantId.From(DbId(key));
    private static TenantActorId DbActor(byte[] key) => TenantActorId.From(DbId(key));
    private static TenantBranchId DbBranch(byte[] key) => TenantBranchId.From(DbId(key));
    private static TenantResourceId DbResource(byte[] key) => TenantResourceId.From(DbId(key));
    private static Guid DbId(byte[] key) =>
        key.Length == 16 ? new Guid(key, bigEndian: true) :
        throw new UnauthorizedAccessException("Invalid server-side identity key.");
    private static DateTimeOffset? DbNullableUtc(Dictionary<string, object> row, string column) =>
        row[column] is DBNull ? null : Utc(row, column);
}
