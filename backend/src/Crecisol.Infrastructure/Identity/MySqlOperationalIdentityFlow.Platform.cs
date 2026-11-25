using System.Security.Cryptography;
using Crecisol.Application.Identity;
using Crecisol.Domain.Authorization;
using MySqlConnector;

namespace Crecisol.Infrastructure.Identity;

public sealed partial class MySqlOperationalIdentityFlow
{
    // Realm boundaries are verified server-side before a tenant catalog lookup.
    // No tenant role, membership, session, or claim can act as an operator.
    private async Task<bool> PlatformQuota(string bucket, int limit, CancellationToken ct)
    {
        await using var db = new MySqlConnection(quotaDsn); await db.OpenAsync(ct);
        await using var tx = await db.BeginTransactionAsync(ct); var u = new Unit(db, tx, ct);
        var hash = Digest("platform-quota", bucket); var window = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 60;
        await u.Run("INSERT INTO c05_platform_attempt_quota VALUES(@h,@w,1) ON DUPLICATE KEY UPDATE attempts=LEAST(attempts+1,1000)", ("h", hash), ("w", window));
        var row = await u.One("SELECT attempts FROM c05_platform_attempt_quota WHERE bucket_hmac=@h AND window_utc=@w", ("h", hash), ("w", window));
        await tx.CommitAsync(ct); return Number(row!, "attempts") <= limit;
    }

    private async Task<OperationalIdentityResult> Platform(Unit u, OperationalIdentityRequest r, CancellationToken ct)
    {
        if (!await PlatformQuota("peer:" + r.Peer, 40, ct)) return new(false);
        if (r.Operation.StartsWith("platform/operator-recovery/", StringComparison.Ordinal) || r.Operation == "platform/incidents/report")
            return await PlatformOperatorRecovery(u, r, ct);
        if (r.Operation == "platform/login")
        {
            var mailbox = CanonicalLoginHandle.Parse(Field(r, "mailbox")).Value;
            if (!await PlatformQuota("login:" + mailbox, 5, ct)) return new(false);
            var row = await u.One("SELECT * FROM c05_platform_operator WHERE login_hmac=@h FOR UPDATE", ("h", Digest("platform-login", mailbox)));
            var valid = crypto.VerifyPassword(Field(r, "password"), row is null ? dummyPhc : Text(row, "password_phc"));
            if (row is null || !valid || Number(row, "active") != 1 || Number(row, "recovery_pending") != 0) return new(false);
            u.Actor = Bytes(row, "operator_id"); var challenge = Token();
            await u.Run("INSERT INTO c05_platform_challenge(token_hmac,operator_id,security_revision,expires_at_utc) VALUES(@h,@a,@v,UTC_TIMESTAMP(6)+INTERVAL 2 MINUTE)",
                ("h", Digest("platform-challenge", challenge)), ("a", u.Actor), ("v", Number(row, "security_revision")));
            return new(true, new { challenge, stepUpRequired = true });
        }
        if (r.Operation == "platform/mfa")
        {
            var hash = Digest("platform-challenge", Field(r, "challenge"));
            var peek = await u.One("SELECT operator_id FROM c05_platform_challenge WHERE token_hmac=@h", ("h", hash));
            if (peek is null) return new(false);
            var actor = Bytes(peek, "operator_id");
            if (!await PlatformQuota("mfa:" + Hex(actor), 5, ct)) return new(false);
            var person = await u.One("SELECT * FROM c05_platform_operator WHERE operator_id=@a AND active=1 AND recovery_pending=0 FOR UPDATE", ("a", actor));
            var challenge = await u.One("SELECT * FROM c05_platform_challenge WHERE token_hmac=@h AND consumed_at_utc IS NULL AND expires_at_utc>UTC_TIMESTAMP(6) AND attempts<5 FOR UPDATE", ("h", hash));
            if (person is null || challenge is null || Number(person, "security_revision") != Number(challenge, "security_revision")) return new(false);
            u.Actor = actor;
            await u.Run("UPDATE c05_platform_challenge SET attempts=attempts+1 WHERE token_hmac=@h", ("h", hash));
            if (!await PlatformTotp(u, person, Field(r, "code"))) return new(false);
            u.VerifiedActor = true;
            await u.Run("UPDATE c05_platform_challenge SET consumed_at_utc=UTC_TIMESTAMP(6) WHERE token_hmac=@h", ("h", hash));
            return await IssuePlatformSession(u, person);
        }
        if (r.Bearer is not { Length: 64 }) return new(false);
        var tokenHash = Digest("platform-session", r.Bearer);
        var identity = await u.One("SELECT operator_id FROM c05_platform_session WHERE token_hmac=@h", ("h", tokenHash));
        if (identity is null) return new(false);
        var principal = await u.One("SELECT * FROM c05_platform_operator WHERE operator_id=@a AND active=1 AND recovery_pending=0 FOR UPDATE", ("a", Bytes(identity, "operator_id")));
        var idle = await SessionIdleMinutes(u, r.Operation == "platform/session-policy/update");
        var session = await u.One("SELECT * FROM c05_platform_session WHERE token_hmac=@h AND revoked_at_utc IS NULL AND expires_at_utc>UTC_TIMESTAMP(6) AND last_seen_at_utc>UTC_TIMESTAMP(6)-INTERVAL @idle MINUTE FOR UPDATE", ("h", tokenHash), ("idle", idle));
        if (session is null)
        {
            await u.Run("""
                UPDATE c05_platform_session SET revoked_at_utc=UTC_TIMESTAMP(6)
                WHERE token_hmac=@h AND revoked_at_utc IS NULL
                  AND (expires_at_utc<=UTC_TIMESTAMP(6)
                       OR last_seen_at_utc<=UTC_TIMESTAMP(6)-INTERVAL @idle MINUTE)
                """, ("h", tokenHash), ("idle", idle));
            return new(false);
        }
        if (principal is null || Number(principal, "security_revision") != Number(session, "security_revision")) return new(false);
        u.Actor = Bytes(principal, "operator_id");
        u.VerifiedActor = true;
        await SlidePlatformSession(u, tokenHash, idle);
        // All authenticated fresh-proof operations pass through the purpose
        // gate before any handler. Existing per-action quotas, factor checks,
        // password checks, grants and recovery challenges remain mandatory.
        if (C06SensitiveOperationPolicy.RequiresFreshFactor(C06StepUpRealm.Platform, r.Operation) &&
            !C06PlatformStepUpPurpose(u, principal, r)) return new(false);
        if (C06ExceptionalAccessPolicy.IsOperation(r.Operation))
        {
            if (Environment.GetEnvironmentVariable("CRECISOL_C06_BREAKGLASS_ENABLED") != "true")
                return new(false);
            return await C06ExceptionalAccess(u, principal, session, r, ct);
        }
        if (r.Operation == "platform/sessions/keepalive")
            return new(true, new { expiresAtUtc = u.SessionExpiresAtUtc, idleMinutes = idle });
        if (r.Operation is "platform/session-policy" or "platform/session-policy/update")
            return await PlatformSessionPolicy(u, principal, tokenHash, r, ct);
        if (r.Operation.StartsWith("platform/profile/", StringComparison.Ordinal))
            return await Profile(u, [], u.Actor, r with { Operation = r.Operation[9..] }, platformRealm: true);
        if (r.Operation is "platform/session" or "platform/restore") return new(true, new OperationalPlatformValidation(u.SessionExpiresAtUtc!.Value));
        if (r.Operation == "platform/sessions")
        {
            var rows = await u.All("SELECT session_id,created_at_utc,expires_at_utc,last_seen_at_utc,device_label FROM c05_platform_session WHERE operator_id=@a AND security_revision=@v AND revoked_at_utc IS NULL AND expires_at_utc>UTC_TIMESTAMP(6) AND last_seen_at_utc>UTC_TIMESTAMP(6)-INTERVAL @idle MINUTE ORDER BY last_seen_at_utc DESC",
                ("a", u.Actor), ("v", Number(principal, "security_revision")), ("idle", idle));
            return new(true, new { sessions = rows.Select(row => new { id = new Guid(Bytes(row, "session_id"), true), issuedAtUtc = row["created_at_utc"],
                expiresAtUtc = row["expires_at_utc"], lastSeenAtUtc = row["last_seen_at_utc"], device = row["device_label"] }) });
        }
        if (r.Operation == "platform/sessions/rotate")
        {
            u.SessionMethod = Text(session, "auth_method");
            await u.Run("UPDATE c05_platform_session SET revoked_at_utc=UTC_TIMESTAMP(6) WHERE token_hmac=@h", ("h", tokenHash));
            return await IssuePlatformSession(u, principal, (DateTime)session["mfa_at_utc"]);
        }
        if (r.Operation == "platform/sessions/revoke")
            return new(await u.Run("UPDATE c05_platform_session SET revoked_at_utc=UTC_TIMESTAMP(6) WHERE operator_id=@a AND session_id=@id AND revoked_at_utc IS NULL",
                ("a", u.Actor), ("id", GuidBytes(Field(r, "sessionId")))) == 1, new { accepted = true });
        if (r.Operation == "platform/sessions/revoke-all")
        {
            if (!await PlatformQuota("revoke-all:" + Hex(u.Actor), 5, ct) || !await PlatformTotp(u, principal, Field(r, "code"))) return new(false);
            await RevokePlatformAuth(u, u.Actor); await NotifyPlatformSecurity(u, principal, "PLATFORM_SESSIONS_REVOKED");
            return new(true, new { accepted = true });
        }
        if (r.Operation == "platform/abuse-summary")
        {
            var rows = await u.All("SELECT signal_kind,COUNT(*) AS signals FROM c05_identity_abuse_signal WHERE created_at_utc>UTC_TIMESTAMP(6)-INTERVAL 1 DAY GROUP BY signal_kind");
            return new(true, new { signals = rows.Select(row => new { kind = row["signal_kind"], count = row["signals"] }) });
        }
        if (r.Operation == "platform/cases/find")
        {
            if (Field(r, "reason").Trim().Length < 12 || !await PlatformQuota("find-case:" + Hex(u.Actor), 5, ct) ||
                !await PlatformTotp(u, principal, Field(r, "code"))) return new(false);
            var mailbox = CanonicalLoginHandle.Parse(Field(r, "mailbox")).Value;
            u.PurposeHash = Digest("identity-support-purpose", Field(r, "reason"));
            Dictionary<string, object>? found = Field(r, "realm") switch
            {
                "PLATFORM_OPERATOR" => await u.One("SELECT c.case_id FROM c05_platform_recovery_case c JOIN c05_platform_operator p ON p.operator_id=c.operator_id WHERE p.login_hmac=@h AND c.blocked=0 AND c.consumed_at_utc IS NULL AND c.expires_at_utc>UTC_TIMESTAMP(6)", ("h", Digest("platform-login", mailbox))),
                "TENANT_IDENTITY" => await u.One("SELECT c.case_id FROM c05_identity_assisted_case c JOIN c05_identity_account a ON a.account_id=c.account_id WHERE a.login_lookup_hmac=@h AND c.blocked=0 AND c.consumed_at_utc IS NULL AND c.expires_at_utc>UTC_TIMESTAMP(6)", ("h", LoginDigest(mailbox))),
                _ => null
            };
            if (found is null) return new(false);
            u.Subject = Bytes(found, "case_id");
            return new(true, new { caseId = new Guid(u.Subject, true) });
        }
        if (r.Operation == "platform/password/change")
        {
            if (!await PlatformQuota("change:" + Hex(u.Actor), 5, ct) ||
                !await PlatformTotp(u, principal, Field(r, "code")) ||
                !crypto.VerifyPassword(Field(r, "currentPassword"), Text(principal, "password_phc"))) return new(false);
            var phc = crypto.HashPassword(Field(r, "newPassword"));
            await u.Run("UPDATE c05_platform_operator SET password_phc=@p,security_revision=security_revision+1 WHERE operator_id=@a", ("p", phc), ("a", u.Actor));
            await RevokePlatformAuth(u, u.Actor);
            await NotifyPlatformSecurity(u, principal, "PLATFORM_PASSWORD_CHANGED");
            return new(true, new { accepted = true });
        }
        if (r.Operation is "platform/factor/replace" or "platform/factor/confirm")
            return await PlatformFactorChange(u, principal, tokenHash, r, ct);
        if (r.Operation is "platform/mailbox/request" or "platform/mailbox/complete")
            return await PlatformMailbox(u, principal, tokenHash, r, ct);
        if (r.Operation == "platform/logout")
        {
            await u.Run("UPDATE c05_platform_session SET revoked_at_utc=UTC_TIMESTAMP(6) WHERE token_hmac=@h", ("h", tokenHash));
            return new(true, new { accepted = true });
        }
        var caseId = GuidBytes(Field(r, "caseId")); u.Subject = caseId;
        var casePeek = await u.One("SELECT tenant_id,account_id FROM c05_identity_assisted_case WHERE case_id=@id", ("id", caseId));
        var operatorCase = await u.One("SELECT * FROM c05_platform_recovery_case WHERE case_id=@id", ("id", caseId));
        if (operatorCase is not null)
        {
            if (casePeek is not null) return new(false);
            if (r.Operation == "platform/case") return OperatorCaseReceipt(caseId, operatorCase);
            if (r.Operation is not ("platform/review" or "platform/release-share") ||
                !await PlatformQuota("review:" + Hex(u.Actor), 5, ct) || !await PlatformTotp(u, principal, Field(r, "code"))) return new(false);
            return await ReviewOperatorRecovery(u, caseId, principal, r);
        }
        if (casePeek is null) return new(false);
        if (r.Operation == "platform/release-share")
        {
            if (!await PlatformQuota("review:" + Hex(u.Actor), 5, ct) || !await PlatformTotp(u, principal, Field(r, "code"))) return new(false);
            return await ReleaseAssistedShare(u, casePeek, caseId, principal);
        }
        if (r.Operation == "platform/case")
        {
            var row = await u.One("SELECT created_at_utc,ready_at_utc,expires_at_utc,blocked,consumed_at_utc FROM c05_identity_assisted_case WHERE case_id=@id", ("id", caseId));
            return new(true, new { caseId = new Guid(caseId, true), createdAtUtc = row!["created_at_utc"], readyAtUtc = row["ready_at_utc"] is DBNull ? null : row["ready_at_utc"],
                expiresAtUtc = row["expires_at_utc"], blocked = Number(row, "blocked") == 1, consumed = row["consumed_at_utc"] is not DBNull });
        }
        if (r.Operation != "platform/review" || Field(r, "identityVerified") != "true" ||
            Field(r, "evidenceHash").Length != 64 || !Field(r, "evidenceHash").All(Uri.IsHexDigit) ||
            !await PlatformQuota("review:" + Hex(u.Actor), 5, ct) || !await PlatformTotp(u, principal, Field(r, "code"))) return new(false);
        return await ReviewAssisted(u, casePeek, caseId, principal, r);
    }

    private async Task<bool> PlatformTotp(Unit u, Dictionary<string, object> person, string code)
    {
        var actor = Bytes(person, "operator_id");
        if (code.Length == 32 && code.All(Uri.IsHexDigit))
        {
            var consumed = await u.Run("UPDATE c05_platform_mfa_backup SET consumed_at_utc=UTC_TIMESTAMP(6) WHERE operator_id=@a AND token_hmac=@h AND consumed_at_utc IS NULL",
                ("a", actor), ("h", Digest("platform-backup:" + Hex(actor), code.ToUpperInvariant()))) == 1;
            if (consumed) u.SessionMethod = "BACKUP";
            return consumed;
        }
        var seed = Unprotect(Bytes(person, "protected_seed"), "platform-factor:" + Hex(actor));
        try
        {
            if (!crypto.VerifyTotp(seed, code, DateTimeOffset.UtcNow, person["last_totp_step"] is DBNull ? -1 : Number(person, "last_totp_step"), out var step)) return false;
            await u.Run("UPDATE c05_platform_operator SET last_totp_step=@s WHERE operator_id=@a", ("s", step), ("a", actor));
            u.SessionMethod = "TOTP";
            return true;
        }
        finally { CryptographicOperations.ZeroMemory(seed); }
    }

    private async Task<OperationalIdentityResult> IssuePlatformSession(Unit u, Dictionary<string, object> person, DateTime? mfa = null)
    {
        var token = Token();
        var expiry = DateTimeOffset.UtcNow.AddMinutes(await SessionIdleMinutes(u));
        u.SessionExpiresAtUtc = expiry;
        await u.Run("""
            INSERT INTO c05_platform_session(token_hmac,operator_id,security_revision,mfa_at_utc,expires_at_utc,last_seen_at_utc,revoked_at_utc,session_id,created_at_utc,device_label,peer_hmac,auth_method)
            VALUES(@h,@a,@v,@mfa,@expiry,UTC_TIMESTAMP(6),NULL,@id,UTC_TIMESTAMP(6),@device,@peer,@method)
            """, ("h", Digest("platform-session", token)), ("a", Bytes(person, "operator_id")), ("v", Number(person, "security_revision")),
            ("mfa", mfa ?? DateTime.UtcNow), ("expiry", expiry.UtcDateTime), ("id", Id()), ("device", u.DeviceLabel), ("peer", u.PeerHash), ("method", u.SessionMethod));
        return new(true, new OperationalSessionReceipt(token, expiry));
    }
}
