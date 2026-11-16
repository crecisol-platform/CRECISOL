using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Crecisol.Application.Identity;

namespace Crecisol.Infrastructure.Identity;

public sealed partial class MySqlOperationalIdentityFlow
{
    private static OperationalIdentityResult OperatorCaseReceipt(byte[] id, Dictionary<string, object> row) => new(true, new {
        caseId = new Guid(id, true), realm = "PLATFORM_OPERATOR", readyAtUtc = row["ready_at_utc"] is DBNull ? null : row["ready_at_utc"],
        blocked = Number(row, "blocked") == 1, consumed = row["consumed_at_utc"] is not DBNull });

    private static async Task RevokePlatformAuth(Unit u, byte[] actor, bool blockCases = true)
    {
        await u.Run("UPDATE c05_platform_session SET revoked_at_utc=UTC_TIMESTAMP(6) WHERE operator_id=@a AND revoked_at_utc IS NULL", ("a", actor));
        await u.Run("UPDATE c05_platform_challenge SET consumed_at_utc=UTC_TIMESTAMP(6) WHERE operator_id=@a AND consumed_at_utc IS NULL", ("a", actor));
        await u.Run("UPDATE c05_platform_factor_change SET consumed_at_utc=UTC_TIMESTAMP(6) WHERE operator_id=@a AND consumed_at_utc IS NULL", ("a", actor));
        await u.Run("UPDATE c05_platform_mailbox_change SET consumed_at_utc=UTC_TIMESTAMP(6) WHERE operator_id=@a AND consumed_at_utc IS NULL", ("a", actor));
        if (blockCases) await u.Run("UPDATE c05_platform_recovery_case SET blocked=1 WHERE operator_id=@a AND consumed_at_utc IS NULL", ("a", actor));
    }

    private async Task NotifyPlatformSecurity(Unit u, Dictionary<string, object> person, string action, bool previous = false)
    {
        var actor = Bytes(person, "operator_id");
        if (person["protected_contact"] is DBNull) throw new InvalidOperationException("Institutionally verified operator contact required.");
        var plain = Unprotect(Bytes(person, previous ? "previous_contact" : "protected_contact"), "platform-contact:" + Hex(actor));
        string mailbox;
        try { mailbox = Encoding.UTF8.GetString(plain); } finally { CryptographicOperations.ZeroMemory(plain); }
        var token = Token(); var id = Id();
        await u.Run("INSERT INTO c05_platform_identity_incident VALUES(@h,@a,UTC_TIMESTAMP(6)+INTERVAL 1 DAY,NULL)", ("h", Digest("platform-incident", token)), ("a", actor));
        var payload = Protect(JsonSerializer.SerializeToUtf8Bytes(new DeliveryPayload(mailbox, token, "NOTICE_" + action)), "delivery:" + Hex(id));
        await u.Run("INSERT INTO c05_identity_sensitive_outbox VALUES(@id,@p,UTC_TIMESTAMP(6)+INTERVAL 1 DAY,NULL)", ("id", id), ("p", payload));
    }

    private static async Task<bool> OperatorReviewsValid(Unit u, byte[] caseId)
    {
        var rows = await u.All("""
            SELECT r.reviewer_id FROM c05_platform_recovery_review r JOIN c05_platform_operator p ON p.operator_id=r.reviewer_id
            WHERE r.case_id=@id AND r.approved=1 AND p.active=1 AND p.recovery_pending=0 AND p.security_revision=r.reviewer_revision FOR SHARE
            """, ("id", caseId));
        return rows.Count == 2;
    }

    private async Task<OperationalIdentityResult> PlatformOperatorRecovery(Unit u, OperationalIdentityRequest r, CancellationToken ct)
    {
        if (r.Operation == "platform/incidents/report")
        {
            var hash = Digest("platform-incident", Field(r, "token"));
            var peek = await u.One("SELECT operator_id FROM c05_platform_identity_incident WHERE token_hmac=@h", ("h", hash));
            if (peek is null) return new(false);
            var actor = Bytes(peek, "operator_id");
            await u.One("SELECT operator_id FROM c05_platform_operator WHERE operator_id=@a FOR UPDATE", ("a", actor));
            var notice = await u.One("SELECT operator_id FROM c05_platform_identity_incident WHERE token_hmac=@h AND reported_at_utc IS NULL AND expires_at_utc>UTC_TIMESTAMP(6) FOR UPDATE", ("h", hash));
            if (notice is null) return new(false);
            u.Actor = actor; await RevokePlatformAuth(u, actor);
            await u.Run("UPDATE c05_platform_identity_incident SET reported_at_utc=UTC_TIMESTAMP(6) WHERE token_hmac=@h", ("h", hash));
            return new(true, new { accepted = true });
        }
        if (r.Operation == "platform/operator-recovery/request")
        {
            var reference = Guid.NewGuid(); var receipt = new OperationalIdentityResult(true, new OperationalAssistedReceipt(reference));
            var mailbox = CanonicalLoginHandle.Parse(Field(r, "mailbox")).Value;
            if (Field(r, "automaticMethodsUnavailable") != "true" || !await PlatformQuota("recovery:" + mailbox, 3, ct)) return receipt;
            var person = await u.One("SELECT * FROM c05_platform_operator WHERE login_hmac=@h AND active=1 FOR UPDATE", ("h", Digest("platform-login", mailbox)));
            if (person is null || person["protected_contact"] is DBNull) return receipt;
            var actor = Bytes(person, "operator_id");
            await u.Run("UPDATE c05_platform_recovery_case SET blocked=1 WHERE operator_id=@a AND consumed_at_utc IS NULL AND expires_at_utc<=UTC_TIMESTAMP(6)", ("a", actor));
            if (await u.One("SELECT case_id FROM c05_platform_recovery_case WHERE operator_id=@a AND blocked=0 AND consumed_at_utc IS NULL", ("a", actor)) is not null) return receipt;
            await u.Run("INSERT INTO c05_platform_recovery_case(case_id,operator_id,operator_revision,created_at_utc,expires_at_utc) VALUES(@id,@a,@v,UTC_TIMESTAMP(6),UTC_TIMESTAMP(6)+INTERVAL 2 DAY)",
                ("id", reference.ToByteArray(true)), ("a", actor), ("v", Number(person, "security_revision")));
            u.Actor = actor; u.Subject = reference.ToByteArray(true);
            await NotifyPlatformSecurity(u, person, "PLATFORM_RECOVERY_REQUESTED");
            if (person["previous_contact"] is not DBNull && (DateTime)person["contact_changed_at_utc"] > DateTime.UtcNow.AddDays(-7))
                await NotifyPlatformSecurity(u, person, "PLATFORM_RECOVERY_REQUESTED", true);
            return receipt;
        }
        var id = GuidBytes(Field(r, "caseId")); u.Subject = id;
        var owner = await u.One("SELECT operator_id FROM c05_platform_recovery_case WHERE case_id=@id", ("id", id));
        if (owner is null || !await PlatformQuota("recover-complete:" + Hex(id), 5, ct)) return new(false);
        var personRow = await u.One("SELECT * FROM c05_platform_operator WHERE operator_id=@a AND active=1 FOR UPDATE", ("a", Bytes(owner, "operator_id")));
        var row = await u.One("SELECT * FROM c05_platform_recovery_case WHERE case_id=@id AND blocked=0 AND consumed_at_utc IS NULL AND ready_at_utc<=UTC_TIMESTAMP(6) AND expires_at_utc>UTC_TIMESTAMP(6) FOR UPDATE", ("id", id));
        if (personRow is null || row is null || Number(personRow, "security_revision") != Number(row, "operator_revision") || !await OperatorReviewsValid(u, id)) return new(false);
        var target = Bytes(personRow, "operator_id"); u.Actor = target;
        if (r.Operation == "platform/operator-recovery/complete")
        {
            var shares = await u.All("SELECT share_hmac FROM c05_platform_recovery_share WHERE case_id=@id AND expires_at_utc>UTC_TIMESTAMP(6) FOR UPDATE", ("id", id));
            var a = Digest("operator-share:" + Hex(id), Field(r, "shareA")); var b = Digest("operator-share:" + Hex(id), Field(r, "shareB"));
            if (a.SequenceEqual(b) || shares.Count != 2 || !shares.Any(s => CryptographicOperations.FixedTimeEquals(Bytes(s, "share_hmac"), a)) ||
                !shares.Any(s => CryptographicOperations.FixedTimeEquals(Bytes(s, "share_hmac"), b))) return new(false);
            var phc = crypto.HashPassword(Field(r, "newPassword")); var seed = RandomNumberGenerator.GetBytes(32); var token = Token();
            try
            {
                await u.Run("UPDATE c05_platform_recovery_case SET pending_password_phc=@p,pending_seed=@seed,enrollment_hmac=@h,enrollment_expires_at_utc=UTC_TIMESTAMP(6)+INTERVAL 5 MINUTE WHERE case_id=@id",
                    ("p", phc), ("seed", Protect(seed, "platform-factor:" + Hex(target))), ("h", Digest("operator-enrollment", token)), ("id", id));
                await u.Run("UPDATE c05_platform_operator SET recovery_pending=1 WHERE operator_id=@a", ("a", target));
                await RevokePlatformAuth(u, target, false);
                return new(true, new { challenge = token, secret = Base32(seed), enrollmentRequired = true });
            }
            finally { CryptographicOperations.ZeroMemory(seed); }
        }
        if (r.Operation != "platform/operator-recovery/enroll" || row["enrollment_hmac"] is DBNull ||
            (DateTime)row["enrollment_expires_at_utc"] <= DateTime.UtcNow ||
            !CryptographicOperations.FixedTimeEquals(Bytes(row, "enrollment_hmac"), Digest("operator-enrollment", Field(r, "token")))) return new(false);
        var newSeed = Unprotect(Bytes(row, "pending_seed"), "platform-factor:" + Hex(target));
        try
        {
            if (!crypto.VerifyTotp(newSeed, Field(r, "code"), DateTimeOffset.UtcNow, -1, out var step)) return new(false);
            u.VerifiedActor = true;
            await u.Run("UPDATE c05_platform_operator SET password_phc=@p,protected_seed=@seed,security_revision=security_revision+1,last_totp_step=@step,recovery_pending=0 WHERE operator_id=@a",
                ("p", Text(row, "pending_password_phc")), ("seed", Bytes(row, "pending_seed")), ("step", step), ("a", target));
            await u.Run("UPDATE c05_platform_recovery_case SET consumed_at_utc=UTC_TIMESTAMP(6),pending_password_phc=NULL,pending_seed=NULL,enrollment_hmac=NULL WHERE case_id=@id", ("id", id));
            await RevokePlatformAuth(u, target);
            await NotifyPlatformSecurity(u, personRow, "PLATFORM_RECOVERY_COMPLETED");
            return new(true, new { accepted = true, recoveryCodes = await NewPlatformBackups(u, target) });
        }
        finally { CryptographicOperations.ZeroMemory(newSeed); }
    }

    private async Task<IReadOnlyList<string>> NewPlatformBackups(Unit u, byte[] actor)
    {
        await u.Run("UPDATE c05_platform_mfa_backup SET consumed_at_utc=UTC_TIMESTAMP(6) WHERE operator_id=@a AND consumed_at_utc IS NULL", ("a", actor));
        var codes = new List<string>();
        for (var i = 0; i < 10; i++)
        {
            var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)); codes.Add(code);
            await u.Run("INSERT INTO c05_platform_mfa_backup VALUES(@a,@h,NULL)", ("a", actor), ("h", Digest("platform-backup:" + Hex(actor), code)));
        }
        return codes;
    }

    private async Task<OperationalIdentityResult> PlatformFactorChange(Unit u, Dictionary<string, object> person, byte[] sessionHash,
        OperationalIdentityRequest r, CancellationToken ct)
    {
        var actor = Bytes(person, "operator_id");
        if (!await PlatformQuota("factor-change:" + Hex(actor), 5, ct)) return new(false);
        if (r.Operation == "platform/factor/replace")
        {
            if (!await PlatformTotp(u, person, Field(r, "code")) || !crypto.VerifyPassword(Field(r, "currentPassword"), Text(person, "password_phc"))) return new(false);
            var seed = RandomNumberGenerator.GetBytes(32); var challenge = Token();
            try
            {
                await u.Run("INSERT INTO c05_platform_factor_change VALUES(@h,@a,@s,@v,@seed,UTC_TIMESTAMP(6)+INTERVAL 5 MINUTE,NULL)",
                    ("h", Digest("operator-factor-change", challenge)), ("a", actor), ("s", sessionHash), ("v", Number(person, "security_revision")),
                    ("seed", Protect(seed, "platform-factor:" + Hex(actor))));
                return new(true, new { challenge, secret = Base32(seed) });
            }
            finally { CryptographicOperations.ZeroMemory(seed); }
        }
        var hash = Digest("operator-factor-change", Field(r, "challenge"));
        var row = await u.One("SELECT * FROM c05_platform_factor_change WHERE token_hmac=@h AND operator_id=@a AND session_hmac=@s AND consumed_at_utc IS NULL AND expires_at_utc>UTC_TIMESTAMP(6) FOR UPDATE",
            ("h", hash), ("a", actor), ("s", sessionHash));
        if (row is null || Number(row, "operator_revision") != Number(person, "security_revision")) return new(false);
        var newSeed = Unprotect(Bytes(row, "protected_seed"), "platform-factor:" + Hex(actor));
        try
        {
            if (!crypto.VerifyTotp(newSeed, Field(r, "code"), DateTimeOffset.UtcNow, -1, out var step)) return new(false);
            await u.Run("UPDATE c05_platform_operator SET protected_seed=@seed,last_totp_step=@step,security_revision=security_revision+1 WHERE operator_id=@a",
                ("seed", Bytes(row, "protected_seed")), ("step", step), ("a", actor));
            await RevokePlatformAuth(u, actor);
            await NotifyPlatformSecurity(u, person, "PLATFORM_MFA_REPLACED");
            return new(true, new { accepted = true, recoveryCodes = await NewPlatformBackups(u, actor) });
        }
        finally { CryptographicOperations.ZeroMemory(newSeed); }
    }

    private async Task<OperationalIdentityResult> ReviewOperatorRecovery(Unit u, byte[] id, Dictionary<string, object> reviewer, OperationalIdentityRequest r)
    {
        var peek = await u.One("SELECT operator_id FROM c05_platform_recovery_case WHERE case_id=@id", ("id", id));
        var reviewerId = Bytes(reviewer, "operator_id");
        if (peek is null || Bytes(peek, "operator_id").SequenceEqual(reviewerId)) return new(false);
        var person = await u.One("SELECT * FROM c05_platform_operator WHERE operator_id=@a AND active=1 FOR UPDATE", ("a", Bytes(peek, "operator_id")));
        var row = await u.One("SELECT * FROM c05_platform_recovery_case WHERE case_id=@id AND blocked=0 AND consumed_at_utc IS NULL AND expires_at_utc>UTC_TIMESTAMP(6) FOR UPDATE", ("id", id));
        if (person is null || row is null || Number(person, "security_revision") != Number(row, "operator_revision")) return new(false);
        if (r.Operation == "platform/release-share")
        {
            if (row["ready_at_utc"] is DBNull || (DateTime)row["ready_at_utc"] > DateTime.UtcNow || !await OperatorReviewsValid(u, id) ||
                await u.One("SELECT reviewer_id FROM c05_platform_recovery_review WHERE case_id=@id AND reviewer_id=@a AND approved=1", ("id", id), ("a", reviewerId)) is null) return new(false);
            var share = Token();
            await u.Run("INSERT INTO c05_platform_recovery_share VALUES(@id,@a,@h,UTC_TIMESTAMP(6)+INTERVAL 30 MINUTE) ON DUPLICATE KEY UPDATE share_hmac=@h,expires_at_utc=UTC_TIMESTAMP(6)+INTERVAL 30 MINUTE",
                ("id", id), ("a", reviewerId), ("h", Digest("operator-share:" + Hex(id), share)));
            return new(true, new { share, expiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(30) });
        }
        var approve = Field(r, "decision") == "approve";
        var evidence = Field(r, "evidenceHash");
        if (Field(r, "identityVerified") != "true" || (!approve && Field(r, "decision") != "reject") || evidence.Length != 64 ||
            !evidence.All(Uri.IsHexDigit) || evidence.All(c => c == '0') || Field(r, "reason").Trim().Length < 12 || row["ready_at_utc"] is not DBNull ||
            await u.One("SELECT reviewer_id FROM c05_platform_recovery_review WHERE case_id=@id AND reviewer_id=@a", ("id", id), ("a", reviewerId)) is not null) return new(false);
        await u.Run("INSERT INTO c05_platform_recovery_review VALUES(@id,@a,@v,@ok,@e,@reason,UTC_TIMESTAMP(6))", ("id", id), ("a", reviewerId),
            ("v", Number(reviewer, "security_revision")), ("ok", approve), ("e", Convert.FromHexString(evidence)), ("reason", Digest("operator-review-reason", Field(r, "reason"))));
        if (!approve) await u.Run("UPDATE c05_platform_recovery_case SET blocked=1 WHERE case_id=@id", ("id", id));
        else if (await OperatorReviewsValid(u, id))
        {
            await u.Run("UPDATE c05_platform_recovery_case SET ready_at_utc=UTC_TIMESTAMP(6)+INTERVAL 1 DAY,expires_at_utc=UTC_TIMESTAMP(6)+INTERVAL 2 DAY WHERE case_id=@id", ("id", id));
            await NotifyPlatformSecurity(u, person, "PLATFORM_RECOVERY_APPROVED");
            if (person["previous_contact"] is not DBNull && (DateTime)person["contact_changed_at_utc"] > DateTime.UtcNow.AddDays(-7))
                await NotifyPlatformSecurity(u, person, "PLATFORM_RECOVERY_APPROVED", true);
        }
        return new(true, new { accepted = true });
    }
}
