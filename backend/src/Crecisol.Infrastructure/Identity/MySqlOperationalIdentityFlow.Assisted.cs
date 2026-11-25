using System.Security.Cryptography;
using System.Text.Json;
using Crecisol.Application.Identity;

namespace Crecisol.Infrastructure.Identity;

public sealed partial class MySqlOperationalIdentityFlow
{
    private async Task<OperationalIdentityResult> Assisted(Unit u, byte[] tenant, OperationalIdentityRequest r, CancellationToken ct)
    {
        if (r.Operation == "assisted/request")
        {
            var reference = Guid.NewGuid(); var receipt = new OperationalIdentityResult(true, new OperationalAssistedReceipt(reference));
            var mailbox = CanonicalLoginHandle.Parse(Field(r, "mailbox")).Value;
            if (Field(r, "automaticMethodsUnavailable") != "true" || !await Reserve(tenant, "assisted:" + mailbox, 3, ct)) return receipt;
            var account = await u.One("SELECT * FROM c05_identity_account WHERE login_lookup_hmac=@l AND account_state=1 AND mailbox_verified_at_utc IS NOT NULL FOR UPDATE", ("l", LoginDigest(mailbox)));
            if (account is null) return receipt;
            var actor = Bytes(account, "account_id");
            if (await Membership(u, tenant, actor) is null) return receipt;
            // Expired cases release their unique active-account slot. No active
            // request can be used to suspend a victim or revoke their sessions.
            await u.Run("UPDATE c05_identity_assisted_case SET blocked=1 WHERE account_id=@a AND expires_at_utc<=UTC_TIMESTAMP(6) AND consumed_at_utc IS NULL", ("a", actor));
            if (await u.One("SELECT case_id FROM c05_identity_assisted_case WHERE account_id=@a AND blocked=0 AND consumed_at_utc IS NULL", ("a", actor)) is not null) return receipt;
            await InitializeContact(u, actor, mailbox);
            var contact = await u.One("SELECT * FROM c05_identity_contact WHERE account_id=@a FOR UPDATE", ("a", actor));
            var previous = contact!["previous_mailbox"] is not DBNull && (DateTime)contact["changed_at_utc"] > DateTime.UtcNow.AddDays(-7);
            var anchor = Bytes(contact, previous ? "previous_mailbox" : "protected_mailbox");
            await u.Run("""
                INSERT INTO c05_identity_assisted_case
                (tenant_id,case_id,account_id,account_revision,protected_anchor,created_at_utc,expires_at_utc)
                VALUES(@t,@id,@a,@v,@anchor,UTC_TIMESTAMP(6),UTC_TIMESTAMP(6)+INTERVAL 2 DAY)
                """, ("t", tenant), ("id", reference.ToByteArray(true)), ("a", actor), ("v", Number(account, "security_revision")), ("anchor", anchor));
            u.Actor = actor;
            await NotifySecurity(u, tenant, actor, "ASSISTED_REQUESTED");
            if (previous) await NotifySecurity(u, tenant, actor, "ASSISTED_REQUESTED", true);
            return receipt;
        }
        var caseId = GuidBytes(Field(r, "caseId"));
        var peek = await u.One("SELECT account_id FROM c05_identity_assisted_case WHERE tenant_id=@t AND case_id=@id", ("t", tenant), ("id", caseId));
        if (peek is null || !await Reserve(tenant, "assisted-complete:" + Hex(caseId), 5, ct)) return new(false);
        var accountId = Bytes(peek, "account_id");
        var identity = await Account(u, accountId);
        var member = await Membership(u, tenant, accountId);
        var row = await u.One("""
            SELECT * FROM c05_identity_assisted_case WHERE tenant_id=@t AND case_id=@id AND blocked=0 AND consumed_at_utc IS NULL
            AND ready_at_utc<=UTC_TIMESTAMP(6) AND expires_at_utc>UTC_TIMESTAMP(6) FOR UPDATE
            """, ("t", tenant), ("id", caseId));
        if (identity is null || member is null || row is null || Number(identity, "security_revision") != Number(row, "account_revision") ||
            !await HasTwoValidReviews(u, tenant, caseId)) return new(false);
        if (r.Operation == "assisted/handoff-complete")
        {
            var shares = await u.All("SELECT share_hmac FROM c05_identity_assisted_handoff WHERE tenant_id=@t AND case_id=@id AND expires_at_utc>UTC_TIMESTAMP(6) LIMIT 3 FOR UPDATE", ("t", tenant), ("id", caseId));
            var a = Digest("handoff:" + Hex(caseId), Field(r, "shareA")); var b = Digest("handoff:" + Hex(caseId), Field(r, "shareB"));
            if (a.SequenceEqual(b) || shares.Count != 2 || !shares.Any(s => CryptographicOperations.FixedTimeEquals(Bytes(s, "share_hmac"), a)) ||
                !shares.Any(s => CryptographicOperations.FixedTimeEquals(Bytes(s, "share_hmac"), b))) return new(false);
        }
        else if (row["token_hmac"] is DBNull || !CryptographicOperations.FixedTimeEquals(Bytes(row, "token_hmac"), Digest("assisted:" + Hex(caseId), Field(r, "token")))) return new(false);
        var phc = crypto.HashPassword(Field(r, "newPassword"));
        await u.Run("UPDATE c05_identity_assisted_case SET consumed_at_utc=UTC_TIMESTAMP(6) WHERE tenant_id=@t AND case_id=@id", ("t", tenant), ("id", caseId));
        await ResetAuthenticators(u, tenant, accountId, phc);
        await NotifySecurity(u, tenant, accountId, "ASSISTED_COMPLETED");
        // Tenant audit has the subject, with the platform operators redacted.
        // Their identities and evidence digests remain in PLATFORM_IDENTITY.
        return new(true, new { accepted = true });
    }

    private async Task<OperationalIdentityResult> ReleaseAssistedShare(Unit u, Dictionary<string, object> peek, byte[] caseId,
        Dictionary<string, object> reviewer)
    {
        var tenant = Bytes(peek, "tenant_id"); var actor = Bytes(peek, "account_id"); var operatorId = Bytes(reviewer, "operator_id");
        var identity = await Account(u, actor);
        var row = await u.One("SELECT account_revision FROM c05_identity_assisted_case WHERE tenant_id=@t AND case_id=@id AND blocked=0 AND consumed_at_utc IS NULL AND ready_at_utc<=UTC_TIMESTAMP(6) AND expires_at_utc>UTC_TIMESTAMP(6) FOR UPDATE", ("t", tenant), ("id", caseId));
        if (identity is null || row is null || Number(identity, "security_revision") != Number(row, "account_revision") || !await HasTwoValidReviews(u, tenant, caseId) ||
            await u.One("SELECT operator_id FROM c05_identity_assisted_review WHERE tenant_id=@t AND case_id=@id AND operator_id=@a AND approved=1", ("t", tenant), ("id", caseId), ("a", operatorId)) is null) return new(false);
        var share = Token();
        await u.Run("""
            INSERT INTO c05_identity_assisted_handoff VALUES(@t,@id,@a,@h,UTC_TIMESTAMP(6)+INTERVAL 30 MINUTE)
            ON DUPLICATE KEY UPDATE share_hmac=@h,expires_at_utc=UTC_TIMESTAMP(6)+INTERVAL 30 MINUTE
            """, ("t", tenant), ("id", caseId), ("a", operatorId), ("h", Digest("handoff:" + Hex(caseId), share)));
        return new(true, new { share, expiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(30) });
    }

    private static async Task<bool> HasTwoValidReviews(Unit u, byte[] tenant, byte[] caseId)
    {
        var rows = await u.All("""
            SELECT r.operator_id FROM c05_identity_assisted_review r
            JOIN c05_platform_operator p ON p.operator_id=r.operator_id AND p.active=1 AND p.recovery_pending=0 AND p.security_revision=r.operator_revision
            WHERE r.tenant_id=@t AND r.case_id=@id AND r.approved=1 LIMIT 3 FOR SHARE
            """, ("t", tenant), ("id", caseId));
        return rows.Count == 2;
    }

    private async Task<OperationalIdentityResult> ReviewAssisted(Unit u, Dictionary<string, object> peek,
        byte[] caseId, Dictionary<string, object> reviewer, OperationalIdentityRequest r)
    {
        var tenant = Bytes(peek, "tenant_id"); var actor = Bytes(peek, "account_id");
        var identity = await Account(u, actor);
        var row = await u.One("SELECT * FROM c05_identity_assisted_case WHERE tenant_id=@t AND case_id=@id AND blocked=0 AND consumed_at_utc IS NULL AND expires_at_utc>UTC_TIMESTAMP(6) FOR UPDATE", ("t", tenant), ("id", caseId));
        if (identity is null || row is null || Number(identity, "security_revision") != Number(row, "account_revision") ||
            Bytes(identity, "login_lookup_hmac").SequenceEqual(Bytes(reviewer, "conflict_login_hmac")) || row["ready_at_utc"] is not DBNull ||
            await u.One("SELECT operator_id FROM c05_identity_assisted_review WHERE tenant_id=@t AND case_id=@id AND operator_id=@a", ("t", tenant), ("id", caseId), ("a", Bytes(reviewer, "operator_id"))) is not null)
            return new(false);
        var approve = Field(r, "decision") == "approve";
        if ((!approve && Field(r, "decision") != "reject") || Field(r, "reason").Trim().Length < 12 ||
            Field(r, "evidenceHash").All(c => c == '0')) return new(false);
        await u.Run("INSERT INTO c05_identity_assisted_review VALUES(@t,@id,@a,@v,@ok,@e,@reason,UTC_TIMESTAMP(6))",
            ("t", tenant), ("id", caseId), ("a", Bytes(reviewer, "operator_id")), ("v", Number(reviewer, "security_revision")),
            ("ok", approve), ("e", Convert.FromHexString(Field(r, "evidenceHash"))), ("reason", Digest("platform-review-reason", Field(r, "reason"))));
        if (!approve) await u.Run("UPDATE c05_identity_assisted_case SET blocked=1 WHERE tenant_id=@t AND case_id=@id", ("t", tenant), ("id", caseId));
        else if (await HasTwoValidReviews(u, tenant, caseId))
        {
            var token = Token();
            await u.Run("UPDATE c05_identity_assisted_case SET token_hmac=@h,ready_at_utc=UTC_TIMESTAMP(6)+INTERVAL 1 DAY,expires_at_utc=UTC_TIMESTAMP(6)+INTERVAL 2 DAY WHERE tenant_id=@t AND case_id=@id",
                ("h", Digest("assisted:" + Hex(caseId), token)), ("t", tenant), ("id", caseId));
            var raw = Unprotect(Bytes(row, "protected_anchor"), "contact:" + Hex(actor));
            string mailbox;
            try { mailbox = System.Text.Encoding.UTF8.GetString(raw); }
            finally { CryptographicOperations.ZeroMemory(raw); }
            var id = Id();
            var payload = Protect(JsonSerializer.SerializeToUtf8Bytes(new DeliveryPayload(mailbox, token, "ASSISTED")), "delivery:" + Hex(id));
            await u.Run("INSERT INTO c05_identity_sensitive_outbox VALUES(@id,@p,UTC_TIMESTAMP(6)+INTERVAL 2 DAY,NULL)", ("id", id), ("p", payload));
            await NotifySecurity(u, tenant, actor, "ASSISTED_APPROVED");
            var contact = await u.One("SELECT previous_mailbox,changed_at_utc FROM c05_identity_contact WHERE account_id=@a", ("a", actor));
            if (contact!["previous_mailbox"] is not DBNull && (DateTime)contact["changed_at_utc"] > DateTime.UtcNow.AddDays(-7))
                await NotifySecurity(u, tenant, actor, "ASSISTED_APPROVED", true);
        }
        var operatorId = u.Actor; var subject = u.Subject; var verified = u.VerifiedActor;
        u.Actor = null; u.Subject = actor; u.VerifiedActor = false;
        await Audit(u, tenant, "PLATFORM_ASSISTED_REVIEW", true);
        u.Actor = operatorId; u.Subject = subject; u.VerifiedActor = verified;
        return new(true, new { accepted = true });
    }

    private async Task ResetAuthenticators(Unit u, byte[] tenant, byte[] actor, string phc)
    {
        await u.Run("UPDATE c05_identity_mfa_factor SET factor_state=2,revoked_at_utc=UTC_TIMESTAMP(6),factor_revision=factor_revision+1 WHERE account_id=@a AND factor_state<>2", ("a", actor));
        await u.Run("UPDATE c05_identity_mfa_backup SET consumed_at_utc=UTC_TIMESTAMP(6) WHERE account_id=@a AND consumed_at_utc IS NULL", ("a", actor));
        await ReplacePassword(u, tenant, actor, phc);
        var identity = await Account(u, actor);
        await u.Run("""
            INSERT INTO c05_identity_mfa_reset_authorization VALUES(@a,@t,@v,UTC_TIMESTAMP(6)+INTERVAL 30 MINUTE,NULL)
            ON DUPLICATE KEY UPDATE tenant_id=@t,account_revision=@v,expires_at_utc=UTC_TIMESTAMP(6)+INTERVAL 30 MINUTE,consumed_at_utc=NULL
            """, ("a", actor), ("t", tenant), ("v", Number(identity!, "security_revision")));
    }

    private async Task<OperationalIdentityResult> RecoverUsingPasskey(Unit u, byte[] tenant, Dictionary<string, object> account,
        Dictionary<string, object> session, Dictionary<string, object> factor, OperationalIdentityRequest r)
    {
        if (Number(factor, "factor_kind") != 2 || Text(session, "auth_method") != "WEBAUTHN" ||
            (DateTime)session["mfa_verified_at_utc"] < DateTime.UtcNow.AddMinutes(-2)) return new(false);
        var actor = Bytes(account, "account_id");
        var proof = await Proof(u, tenant, Field(r, "token"), "RECOVERY");
        if (proof is null || !Bytes(proof, "account_id").SequenceEqual(actor) || Number(proof, "account_revision") != Number(account, "security_revision")) return new(false);
        await ResetAuthenticators(u, tenant, actor, crypto.HashPassword(Field(r, "newPassword")));
        await NotifySecurity(u, tenant, actor, "PASSKEY_RECOVERY");
        return new(true, new { accepted = true });
    }
}
