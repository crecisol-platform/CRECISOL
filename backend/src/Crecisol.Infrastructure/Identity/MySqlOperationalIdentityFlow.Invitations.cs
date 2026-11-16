using System.Text.Json;
using Crecisol.Application.Identity;

namespace Crecisol.Infrastructure.Identity;

public sealed partial class MySqlOperationalIdentityFlow
{
    private static async Task<OperationalIdentityResult> Terms(Unit u, byte[] tenant)
    {
        var terms = await u.One("SELECT terms_version,content_text,content_hash FROM c05_identity_terms WHERE tenant_id=@t AND active=1 AND published_at_utc<=UTC_TIMESTAMP(6) FOR SHARE", ("t", tenant));
        return terms is null ? new(false) : new(true, new
        { version = Text(terms, "terms_version"), text = Text(terms, "content_text"), hash = Hex(Bytes(terms, "content_hash")) });
    }

    private static async Task<bool> HasCurrentTerms(Unit u, byte[] tenant, byte[] actor) =>
        await u.One("""
            SELECT t.terms_version FROM c05_identity_terms t
            JOIN c05_identity_terms_acceptance a ON a.tenant_id=t.tenant_id AND a.terms_version=t.terms_version AND a.content_hash=t.content_hash
            WHERE t.tenant_id=@t AND a.account_id=@a AND t.active=1 AND t.published_at_utc<=UTC_TIMESTAMP(6) FOR SHARE
            """, ("t", tenant), ("a", actor)) is not null;

    private static async Task<Dictionary<string, object>?> MatchingTerms(Unit u, byte[] tenant, OperationalIdentityRequest r)
    {
        if (Field(r, "accepted") != "true" || Field(r, "hash").Length != 64 || !Field(r, "hash").All(Uri.IsHexDigit)) return null;
        var terms = await u.One("SELECT content_hash FROM c05_identity_terms WHERE tenant_id=@t AND terms_version=@v AND active=1 AND published_at_utc<=UTC_TIMESTAMP(6) FOR SHARE",
            ("t", tenant), ("v", Field(r, "version")));
        return terms is not null && Bytes(terms, "content_hash").SequenceEqual(Convert.FromHexString(Field(r, "hash"))) ? terms : null;
    }

    private static async Task<bool> AcceptTerms(Unit u, byte[] tenant, byte[] actor, OperationalIdentityRequest r)
    {
        var terms = await MatchingTerms(u, tenant, r);
        if (terms is null) return false;
        await u.Run("INSERT IGNORE INTO c05_identity_terms_acceptance VALUES(@t,@a,@v,@h,UTC_TIMESTAMP(6))",
            ("t", tenant), ("a", actor), ("v", Field(r, "version")), ("h", Bytes(terms, "content_hash")));
        return true;
    }

    private static Task<Dictionary<string, object>?> InvitationAuthority(Unit u, byte[] tenant, byte[] actor) =>
        u.One("""
            SELECT m.approval_revision FROM c05_identity_membership m
            JOIN c05_identity_account a ON a.account_id=m.account_id
            WHERE m.tenant_id=@t AND m.account_id=@a AND m.membership_state=1
            AND a.account_state=1 AND a.mailbox_verified_at_utc IS NOT NULL
            AND m.granted_at_utc<=UTC_TIMESTAMP(6) AND m.expires_at_utc>UTC_TIMESTAMP(6)
            AND EXISTS(SELECT 1 FROM c05_identity_resource_grant g WHERE g.tenant_id=m.tenant_id AND g.account_id=m.account_id
              AND g.permission_code='MEMBERSHIP_APPROVE' AND g.scope_kind=2 AND g.resource_id IS NULL AND g.branch_id=m.tenant_id
              AND g.revoked_at_utc IS NULL AND g.granted_at_utc<=UTC_TIMESTAMP(6) AND g.expires_at_utc>UTC_TIMESTAMP(6))
            FOR SHARE
            """, ("t", tenant), ("a", actor));

    private async Task<OperationalIdentityResult> IssueInvitation(Unit u, byte[] tenant,
        Dictionary<string, object> account, OperationalIdentityRequest r)
    {
        var actor = Bytes(account, "account_id");
        var authority = await InvitationAuthority(u, tenant, actor);
        if (authority is null) return new(false);
        var mailbox = CanonicalLoginHandle.Parse(Field(r, "mailbox")).Value;
        if (!mailbox.EndsWith("@example.test", StringComparison.Ordinal)) return new(false);
        var token = Token();
        await u.Run("INSERT INTO c05_identity_invitation VALUES(@t,@h,@target,@a,@v,UTC_TIMESTAMP(6)+INTERVAL 10 MINUTE,NULL)",
            ("t", tenant), ("h", Digest("invitation", token)), ("target", LoginDigest(mailbox)), ("a", actor), ("v", Number(authority, "approval_revision")));
        var id = Id();
        var payload = Protect(JsonSerializer.SerializeToUtf8Bytes(new DeliveryPayload(mailbox, token, "INVITATION")), "delivery:" + Hex(id));
        await u.Run("INSERT INTO c05_identity_sensitive_outbox VALUES(@id,@payload,UTC_TIMESTAMP(6)+INTERVAL 10 MINUTE,NULL)", ("id", id), ("payload", payload));
        // Delivery is neutral even if the recipient has never registered. The
        // invitation never creates an identity, factor, session or stronger role.
        return new(true, new { accepted = true });
    }

    private async Task<OperationalIdentityResult> AcceptInvitation(Unit u, byte[] tenant, OperationalIdentityRequest r, CancellationToken ct)
    {
        var mailbox = CanonicalLoginHandle.Parse(Field(r, "mailbox")).Value;
        if (!await Reserve(tenant, "invitation:" + mailbox, 5, ct)) return new(false);
        var digest = Digest("invitation", Field(r, "token"));
        var invitation = await u.One("SELECT * FROM c05_identity_invitation WHERE tenant_id=@t AND token_hmac=@h AND consumed_at_utc IS NULL AND expires_at_utc>UTC_TIMESTAMP(6) FOR UPDATE", ("t", tenant), ("h", digest));
        if (invitation is null || !Bytes(invitation, "target_login_hmac").SequenceEqual(LoginDigest(mailbox))) return new(false);
        var authority = await InvitationAuthority(u, tenant, Bytes(invitation, "inviter_account_id"));
        if (authority is null || Number(authority, "approval_revision") != Number(invitation, "inviter_membership_revision")) return new(false);
        var account = await u.One("SELECT * FROM c05_identity_account WHERE login_lookup_hmac=@l FOR UPDATE", ("l", LoginDigest(mailbox)));
        var passwordValid = crypto.VerifyPassword(Field(r, "password"), account is null ? dummyPhc : Text(account, "password_phc"));
        if (account is null || !passwordValid || account["mailbox_verified_at_utc"] is DBNull || Number(account, "account_state") is not (0 or 1)) return new(false);
        var actor = Bytes(account, "account_id"); u.Actor = actor;
        if (actor.SequenceEqual(Bytes(invitation, "inviter_account_id"))) return new(false);
        var factor = await u.One("SELECT * FROM c05_identity_mfa_factor WHERE account_id=@a AND factor_kind=1 AND factor_state=1 AND revoked_at_utc IS NULL FOR UPDATE", ("a", actor));
        if (factor is not null)
        {
            if (!await AcceptTotp(u, actor, factor, Field(r, "code"))) return new(false);
        }
        else if (Number(account, "account_state") != 0 || Field(r, "code") != "enroll" ||
            await u.One("SELECT factor_id FROM c05_identity_mfa_factor WHERE account_id=@a LIMIT 1", ("a", actor)) is not null) return new(false);
        var member = await u.One("SELECT * FROM c05_identity_membership WHERE tenant_id=@t AND account_id=@a FOR UPDATE", ("t", tenant), ("a", actor));
        if (member is not null && Number(member, "membership_state") != 0) return new(false);
        if (!await AcceptTerms(u, tenant, actor, r)) return new(false);
        if (member is null)
            await u.Run("""
                INSERT INTO c05_identity_membership
                (tenant_id,membership_id,account_id,membership_state,role_code,approved_by,approved_at_utc,created_at_utc,granted_at_utc,expires_at_utc)
                VALUES(@t,@m,@a,1,'MEMBER',@by,UTC_TIMESTAMP(6),UTC_TIMESTAMP(6),UTC_TIMESTAMP(6),UTC_TIMESTAMP(6)+INTERVAL 1 YEAR)
                """, ("t", tenant), ("m", Id()), ("a", actor), ("by", Bytes(invitation, "inviter_account_id")));
        else
            await u.Run("""
                UPDATE c05_identity_membership SET membership_state=1,role_code='MEMBER',approval_revision=approval_revision+1,
                approved_by=@by,approved_at_utc=UTC_TIMESTAMP(6),granted_at_utc=UTC_TIMESTAMP(6),expires_at_utc=UTC_TIMESTAMP(6)+INTERVAL 1 YEAR
                WHERE tenant_id=@t AND account_id=@a
                """, ("t", tenant), ("a", actor), ("by", Bytes(invitation, "inviter_account_id")));
        await u.Run("UPDATE c05_identity_account SET account_state=1,updated_at_utc=UTC_TIMESTAMP(6) WHERE account_id=@a AND account_state=0", ("a", actor));
        await u.Run("UPDATE c05_identity_invitation SET consumed_at_utc=UTC_TIMESTAMP(6) WHERE tenant_id=@t AND token_hmac=@h", ("t", tenant), ("h", digest));
        return new(true, new { accepted = true });
    }
}
