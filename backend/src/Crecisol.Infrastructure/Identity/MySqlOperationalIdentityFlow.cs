using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Crecisol.Application.Identity;
using MySqlConnector;

namespace Crecisol.Infrastructure.Identity;

/// <summary>
/// One operational schema and one transaction per security transition. Account,
/// membership, factor and session are revalidated under locks, never from claims.
/// The opt-in composition is restricted to isolated loopback/example.test use.
/// </summary>
public sealed class MySqlOperationalIdentityFlow : IOperationalIdentityFlow, IDisposable
{
    private readonly string dsn;
    private readonly byte[] tokenKey, encryptionKey, loginKey;
    private readonly IOperationalIdentityCrypto crypto;
    private readonly string dummyPhc;

    public MySqlOperationalIdentityFlow(string trustedDsn, byte[] tokenKey,
        byte[] encryptionKey, byte[] loginKey, IOperationalIdentityCrypto crypto)
    {
        var options = new MySqlConnectionStringBuilder(trustedDsn);
        if (options.Server is not ("127.0.0.1" or "localhost" or "::1") ||
            options.Database != "crecisol_c05" || options.AllowLoadLocalInfile ||
            options.AllowUserVariables || new[] { tokenKey, encryptionKey, loginKey }.Any(k => k.Length != 32))
            throw new InvalidOperationException("Isolated identity configuration required.");
        options.ConnectionTimeout = 3;
        options.DefaultCommandTimeout = 8;
        options.MaximumPoolSize = 20;
        dsn = options.ConnectionString;
        this.tokenKey = tokenKey.ToArray();
        this.encryptionKey = encryptionKey.ToArray();
        this.loginKey = loginKey.ToArray();
        this.crypto = crypto;
        dummyPhc = crypto.HashPassword(Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
    }

    public async Task<OperationalIdentityResult> ExecuteAsync(OperationalIdentityRequest request, CancellationToken ct)
    {
        await using var db = new MySqlConnection(dsn);
        await db.OpenAsync(ct);
        await using var tx = await db.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var unit = new Unit(db, tx, ct);
        var host = await unit.One("""
            SELECT h.tenant_id FROM c04_verified_tenant_host h
            JOIN c04_tenant_catalog t ON t.tenant_id=h.tenant_id
            WHERE h.canonical_host=@host AND h.verification_state=2
            AND h.verified_at_utc<=UTC_TIMESTAMP(6) AND h.valid_until_utc>UTC_TIMESTAMP(6)
            AND t.lifecycle_state=3 FOR SHARE
            """, ("host", request.Host));
        if (host is null) return new(false);
        var tenant = Bytes(host, "tenant_id");
        // Persist quota before costly crypto, even when the operation is denied.
        if (!await Reserve(tenant, "peer:" + request.Peer, 80, ct)) return new(false);
        OperationalIdentityResult result;
        if (request.Operation == "login") result = await Login(unit, tenant, request, ct);
        else if (request.Operation.StartsWith("mfa/", StringComparison.Ordinal))
            result = await Mfa(unit, tenant, request, ct);
        else if (request.Operation == "recovery/request")
            result = await RequestRecovery(unit, tenant, request, ct);
        else if (request.Operation == "recovery/complete")
            result = await CompleteRecovery(unit, tenant, request, ct);
        else result = await Authenticated(unit, tenant, request, ct);
        await Audit(unit, tenant, request.Operation, result.Allowed);
        await tx.CommitAsync(ct);
        return result;
    }

    private async Task<bool> Reserve(byte[] tenant, string bucket, int maximum, CancellationToken ct)
    {
        await using var db = new MySqlConnection(dsn);
        await db.OpenAsync(ct);
        await using var tx = await db.BeginTransactionAsync(ct);
        var u = new Unit(db, tx, ct);
        var digest = Digest("quota", bucket);
        var window = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 60;
        await u.Run("""
            INSERT INTO c05_identity_attempt_quota VALUES(@t,@b,@w,1)
            ON DUPLICATE KEY UPDATE attempts=LEAST(attempts+1,1000)
            """, ("t", tenant), ("b", digest), ("w", window));
        var row = await u.One("SELECT attempts FROM c05_identity_attempt_quota WHERE tenant_id=@t AND bucket_hmac=@b AND window_utc=@w",
            ("t", tenant), ("b", digest), ("w", window));
        await tx.CommitAsync(ct);
        return Number(row!, "attempts") <= maximum;
    }

    private async Task<OperationalIdentityResult> Login(Unit u, byte[] tenant, OperationalIdentityRequest r, CancellationToken ct)
    {
        var mailbox = CanonicalLoginHandle.Parse(Field(r, "mailbox")).Value;
        if (!await Reserve(tenant, "login:" + mailbox, 8, ct)) return new(false);
        var account = await u.One("SELECT * FROM c05_identity_account WHERE login_lookup_hmac=@l FOR UPDATE",
            ("l", LoginDigest(mailbox)));
        // Identical KDF for unknown/pending accounts. No account identifier is returned.
        var passwordValid = crypto.VerifyPassword(Field(r, "password"), account is null ? dummyPhc : Text(account, "password_phc"));
        if (account is null || !passwordValid || Number(account, "account_state") != 1 ||
            account["mailbox_verified_at_utc"] is DBNull) return new(false);
        var actor = Bytes(account, "account_id");
        u.Actor = actor;
        var member = await Membership(u, tenant, actor);
        if (member is null) return new(false);
        var factor = await u.One("SELECT factor_id FROM c05_identity_mfa_factor WHERE account_id=@a AND factor_state=1 AND revoked_at_utc IS NULL FOR UPDATE", ("a", actor));
        // Revoking an enrolled factor must never downgrade an account back to
        // password-only enrollment. Replacement has its own step-up challenge.
        if (factor is null && await u.One("SELECT factor_id FROM c05_identity_mfa_factor WHERE account_id=@a AND factor_state=2 LIMIT 1 FOR UPDATE", ("a", actor)) is not null)
            return new(false);
        return await Challenge(u, tenant, account, member, factor is null);
    }

    private async Task<OperationalIdentityResult> Challenge(Unit u, byte[] tenant,
        Dictionary<string, object> account, Dictionary<string, object> member, bool enrollmentRequired, byte[]? replaceFactor = null)
    {
        var challenge = Token();
        await u.Run("""
            INSERT INTO c05_identity_challenge
            (tenant_id,token_hmac,account_id,membership_id,account_revision,membership_revision,replace_factor_id,expires_at_utc)
            VALUES(@t,@h,@a,@m,@ar,@mr,@replace,UTC_TIMESTAMP(6)+INTERVAL 5 MINUTE)
            """, ("t", tenant), ("h", Digest("challenge", challenge)), ("a", Bytes(account, "account_id")),
            ("m", Bytes(member, "membership_id")), ("ar", Number(account, "security_revision")),
            ("mr", Number(member, "approval_revision")), ("replace", (object?)replaceFactor ?? DBNull.Value));
        return new(true, new { stepUpRequired = true, challenge, enrollmentRequired });
    }

    private async Task<OperationalIdentityResult> Mfa(Unit u, byte[] tenant, OperationalIdentityRequest r, CancellationToken ct)
    {
        var hash = Digest("challenge", Field(r, "challenge"));
        var peek = await u.One("SELECT account_id FROM c05_identity_challenge WHERE tenant_id=@t AND token_hmac=@h", ("t", tenant), ("h", hash));
        if (peek is null) return new(false);
        var actor = Bytes(peek, "account_id");
        if (!await Reserve(tenant, "mfa:" + Hex(actor), 8, ct)) return new(false);
        var account = await Account(u, actor);
        var member = await Membership(u, tenant, actor);
        var challenge = await u.One("""
            SELECT * FROM c05_identity_challenge WHERE tenant_id=@t AND token_hmac=@h
            AND consumed_at_utc IS NULL AND expires_at_utc>UTC_TIMESTAMP(6) AND attempts<5 FOR UPDATE
            """, ("t", tenant), ("h", hash));
        if (account is null || member is null || challenge is null ||
            Number(account, "security_revision") != Number(challenge, "account_revision") ||
            Number(member, "approval_revision") != Number(challenge, "membership_revision")) return new(false);
        if (r.Operation == "mfa/enroll")
        {
            var activeFactor = await u.One("SELECT factor_id FROM c05_identity_mfa_factor WHERE account_id=@a AND factor_state=1 AND revoked_at_utc IS NULL FOR UPDATE", ("a", actor));
            var replacement = challenge["replace_factor_id"] is not DBNull;
            if (challenge["factor_id"] is not DBNull ||
                (!replacement && activeFactor is not null) ||
                (replacement && (activeFactor is null || !Bytes(activeFactor, "factor_id").SequenceEqual(Bytes(challenge, "replace_factor_id")))))
                return new(false);
            var factorId = Id();
            var seed = RandomNumberGenerator.GetBytes(32);
            try
            {
                var envelope = Protect(seed, "factor:" + Hex(actor) + Hex(factorId));
                await u.Run("""
                    INSERT INTO c05_identity_mfa_factor
                    (account_id,factor_id,factor_kind,protected_secret,secret_nonce,secret_tag,key_version,factor_state,enrolled_at_utc)
                    VALUES(@a,@f,1,@c,@n,@tag,1,0,UTC_TIMESTAMP(6))
                    """, ("a", actor), ("f", factorId), ("c", envelope[28..]), ("n", envelope[..12]), ("tag", envelope[12..28]));
                await u.Run("UPDATE c05_identity_challenge SET factor_id=@f WHERE tenant_id=@t AND token_hmac=@h", ("f", factorId), ("t", tenant), ("h", hash));
                var secret = Base32(seed);
                return new(true, new { secret, uri = "otpauth://totp/CRECISOL:cuenta?secret=" + secret + "&issuer=CRECISOL&algorithm=SHA256&digits=6&period=30" });
            }
            finally { CryptographicOperations.ZeroMemory(seed); }
        }
        await u.Run("UPDATE c05_identity_challenge SET attempts=attempts+1 WHERE tenant_id=@t AND token_hmac=@h", ("t", tenant), ("h", hash));
        var factor = challenge["factor_id"] is DBNull
            ? await u.One("SELECT * FROM c05_identity_mfa_factor WHERE account_id=@a AND factor_state=1 AND revoked_at_utc IS NULL FOR UPDATE", ("a", actor))
            : await u.One("SELECT * FROM c05_identity_mfa_factor WHERE account_id=@a AND factor_id=@f AND factor_state=0 FOR UPDATE",
                ("a", actor), ("f", Bytes(challenge, "factor_id")));
        var replacing = challenge["replace_factor_id"] is not DBNull;
        if (challenge["factor_id"] is not DBNull)
        {
            var active = await u.One("SELECT factor_id FROM c05_identity_mfa_factor WHERE account_id=@a AND factor_state=1 AND revoked_at_utc IS NULL FOR UPDATE", ("a", actor));
            if ((!replacing && active is not null) || (replacing &&
                (active is null || !Bytes(active, "factor_id").SequenceEqual(Bytes(challenge, "replace_factor_id"))))) return new(false);
        }
        if (factor is null || !await AcceptTotp(u, actor, factor, Field(r, "code"), !replacing)) return new(false);
        if (replacing)
        {
            await u.Run("UPDATE c05_identity_mfa_factor SET factor_state=2,revoked_at_utc=UTC_TIMESTAMP(6),factor_revision=factor_revision+1 WHERE account_id=@a AND factor_id=@f", ("a", actor), ("f", Bytes(challenge, "replace_factor_id")));
            await u.Run("UPDATE c05_identity_mfa_factor SET factor_state=1 WHERE account_id=@a AND factor_id=@f", ("a", actor), ("f", Bytes(factor, "factor_id")));
            await u.Run("UPDATE c05_identity_mfa_backup SET consumed_at_utc=UTC_TIMESTAMP(6) WHERE account_id=@a AND consumed_at_utc IS NULL", ("a", actor));
            await u.Run("UPDATE c05_identity_account SET security_revision=security_revision+1,updated_at_utc=UTC_TIMESTAMP(6) WHERE account_id=@a", ("a", actor));
            account["security_revision"] = Number(account, "security_revision") + 1;
            await RevokeAll(u, actor);
        }
        List<string>? recoveryCodes = null;
        if (challenge["factor_id"] is not DBNull)
        {
            recoveryCodes = [];
            for (int i = 0; i < 10; i++)
            {
                var backup = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
                recoveryCodes.Add(backup);
                await u.Run("INSERT INTO c05_identity_mfa_backup VALUES(@a,@h,NULL)",
                    ("a", actor), ("h", Digest("backup:" + Hex(actor), backup)));
            }
        }
        await u.Run("UPDATE c05_identity_challenge SET consumed_at_utc=UTC_TIMESTAMP(6) WHERE tenant_id=@t AND token_hmac=@h", ("t", tenant), ("h", hash));
        var issued = await IssueSession(u, tenant, account, member, Bytes(factor, "factor_id"), null);
        return recoveryCodes is null ? issued : new(true,
            ((OperationalSessionReceipt)issued.Value!) with { RecoveryCodes = recoveryCodes });
    }

    private async Task<bool> AcceptTotp(Unit u, byte[] actor, Dictionary<string, object> factor, string code, bool activate = true)
    {
        if (code.Length == 32 && code.All(Uri.IsHexDigit) && Number(factor, "factor_state") == 1)
        {
            var digest = Digest("backup:" + Hex(actor), code.ToUpperInvariant());
            var backup = await u.One("SELECT token_hmac FROM c05_identity_mfa_backup WHERE account_id=@a AND token_hmac=@h AND consumed_at_utc IS NULL FOR UPDATE",
                ("a", actor), ("h", digest));
            if (backup is null) return false;
            return await u.Run("UPDATE c05_identity_mfa_backup SET consumed_at_utc=UTC_TIMESTAMP(6) WHERE account_id=@a AND token_hmac=@h AND consumed_at_utc IS NULL",
                ("a", actor), ("h", digest)) == 1;
        }
        var factorId = Bytes(factor, "factor_id");
        var envelope = Bytes(factor, "secret_nonce").Concat(Bytes(factor, "secret_tag")).Concat(Bytes(factor, "protected_secret")).ToArray();
        var seed = Unprotect(envelope, "factor:" + Hex(actor) + Hex(factorId));
        try
        {
            var last = factor["last_accepted_step"] is DBNull ? -1 : Number(factor, "last_accepted_step");
            if (!crypto.VerifyTotp(seed, code, DateTimeOffset.UtcNow, last, out var step)) return false;
            await u.Run("UPDATE c05_identity_mfa_factor SET last_accepted_step=@s,factor_state=IF(@activate,1,factor_state) WHERE account_id=@a AND factor_id=@f",
                ("s", step), ("a", actor), ("f", factorId), ("activate", activate));
            return true;
        }
        finally { CryptographicOperations.ZeroMemory(seed); }
    }

    private async Task<OperationalIdentityResult> IssueSession(Unit u, byte[] tenant,
        Dictionary<string, object> account, Dictionary<string, object> member, byte[] factor, byte[]? family,
        DateTime? absoluteExpiry = null, DateTime? originalMfa = null)
    {
        var token = Token();
        var id = Id();
        var expiry = absoluteExpiry is null ? DateTimeOffset.UtcNow.AddMinutes(30) :
            new DateTimeOffset(DateTime.SpecifyKind(absoluteExpiry.Value, DateTimeKind.Utc));
        await u.Run("""
            INSERT INTO c05_identity_session
            (tenant_id,session_id,account_id,membership_id,token_hmac,session_family,mfa_factor_id,mfa_verified_at_utc,
             issued_at_utc,expires_at_utc,account_revision,membership_revision,last_seen_at_utc)
            VALUES(@t,@id,@a,@m,@h,@family,@f,@mfa,UTC_TIMESTAMP(6),@expiry,@ar,@mr,UTC_TIMESTAMP(6))
            """, ("t", tenant), ("id", id), ("a", Bytes(account, "account_id")), ("m", Bytes(member, "membership_id")),
            ("h", Digest("session", token)), ("family", family ?? id), ("f", factor),
            ("ar", Number(account, "security_revision")), ("mr", Number(member, "approval_revision")),
            ("expiry", expiry.UtcDateTime), ("mfa", originalMfa ?? DateTime.UtcNow));
        return new(true, new OperationalSessionReceipt(token, expiry));
    }

    private async Task<OperationalIdentityResult> Authenticated(Unit u, byte[] tenant, OperationalIdentityRequest r, CancellationToken ct)
    {
        if (r.Bearer is not { Length: 64 }) return new(false);
        var hash = Digest("session", r.Bearer);
        var peek = await u.One("SELECT account_id FROM c05_identity_session WHERE tenant_id=@t AND token_hmac=@h", ("t", tenant), ("h", hash));
        if (peek is null) return new(false);
        var actor = Bytes(peek, "account_id");
        var account = await Account(u, actor);
        var member = await Membership(u, tenant, actor);
        var session = await u.One("""
            SELECT * FROM c05_identity_session WHERE tenant_id=@t AND token_hmac=@h
            AND revoked_at_utc IS NULL AND expires_at_utc>UTC_TIMESTAMP(6)
            AND last_seen_at_utc>UTC_TIMESTAMP(6)-INTERVAL 15 MINUTE FOR UPDATE
            """, ("t", tenant), ("h", hash));
        if (account is null || member is null || session is null ||
            !Bytes(session, "membership_id").SequenceEqual(Bytes(member, "membership_id")) ||
            Number(session, "account_revision") != Number(account, "security_revision") ||
            Number(session, "membership_revision") != Number(member, "approval_revision")) return new(false);
        var factor = await u.One("SELECT * FROM c05_identity_mfa_factor WHERE account_id=@a AND factor_id=@f AND factor_state=1 AND revoked_at_utc IS NULL FOR UPDATE",
            ("a", actor), ("f", Bytes(session, "mfa_factor_id")));
        if (factor is null) return new(false);
        await u.Run("UPDATE c05_identity_session SET last_seen_at_utc=UTC_TIMESTAMP(6) WHERE tenant_id=@t AND token_hmac=@h", ("t", tenant), ("h", hash));
        if (r.Operation == "sessions")
        {
            var rows = await u.All("SELECT session_id,issued_at_utc,expires_at_utc FROM c05_identity_session WHERE tenant_id=@t AND account_id=@a AND revoked_at_utc IS NULL AND expires_at_utc>UTC_TIMESTAMP(6)", ("t", tenant), ("a", actor));
            return new(true, new { sessions = rows.Select(row => new { id = new Guid(Bytes(row, "session_id"), true), issuedAtUtc = row["issued_at_utc"], expiresAtUtc = row["expires_at_utc"] }) });
        }
        if (r.Operation is "logout" or "sessions/revoke" or "sessions/rotate")
        {
            var id = r.Operation == "sessions/revoke" ? GuidBytes(Field(r, "sessionId")) : Bytes(session, "session_id");
            var changed = await u.Run("UPDATE c05_identity_session SET revoked_at_utc=UTC_TIMESTAMP(6) WHERE tenant_id=@t AND account_id=@a AND session_id=@id AND revoked_at_utc IS NULL",
                ("t", tenant), ("a", actor), ("id", id));
            if (changed != 1) return new(false);
            if (r.Operation != "sessions/rotate") return new(true, new { accepted = true });
            // Rotation never refreshes the absolute MFA lifetime.
            return await IssueSession(u, tenant, account, member, Bytes(factor, "factor_id"), Bytes(session, "session_family"),
                (DateTime)session["expires_at_utc"], (DateTime)session["mfa_verified_at_utc"]);
        }
        if (r.Operation == "private/read")
        {
            var branch = GuidBytes(Field(r, "branchId"));
            var resourceId = GuidBytes(Field(r, "resourceId"));
            var resource = await u.One("SELECT r.* FROM c05_identity_private_resource r JOIN c05_identity_branch b USING(tenant_id,branch_id) WHERE r.tenant_id=@t AND r.branch_id=@b AND r.resource_id=@r AND r.resource_state=1 AND b.active=1 FOR SHARE",
                ("t", tenant), ("b", branch), ("r", resourceId));
            if (resource is null) return new(false);
            var grant = await u.One("""
                SELECT grant_id FROM c05_identity_resource_grant WHERE tenant_id=@t AND account_id=@a
                AND branch_id=@b AND permission_code='RESOURCE_READ' AND revoked_at_utc IS NULL
                AND granted_at_utc<=UTC_TIMESTAMP(6) AND (expires_at_utc IS NULL OR expires_at_utc>UTC_TIMESTAMP(6))
                AND ((scope_kind=1 AND resource_id=@r AND @owner=@a) OR (scope_kind=2 AND resource_id IS NULL))
                LIMIT 1 FOR SHARE
                """, ("t", tenant), ("a", actor), ("b", branch), ("r", resourceId), ("owner", Bytes(resource, "owner_account_id")));
            return grant is null ? new(false) : new(true, new { reference = Text(resource, "display_reference") });
        }
        // All mutations below require a fresh factor proof; the browser cannot
        // declare step-up, approve itself, change roles, or choose a grant.
        if (!await Reserve(tenant, "stepup:" + Hex(actor), 8, ct) ||
            !await AcceptTotp(u, actor, factor, Field(r, "code"))) return new(false);
        if (r.Operation == "factor/replace")
        {
            if (!crypto.VerifyPassword(Field(r, "currentPassword"), Text(account, "password_phc"))) return new(false);
            // Keep the current factor usable until the new seed is actually
            // proven. Interrupting enrollment cannot strand the account.
            await RevokeAll(u, actor);
            return await Challenge(u, tenant, account, member, true, Bytes(factor, "factor_id"));
        }
        if (r.Operation == "password/change")
        {
            if (!crypto.VerifyPassword(Field(r, "currentPassword"), Text(account, "password_phc"))) return new(false);
            await ReplacePassword(u, actor, crypto.HashPassword(Field(r, "newPassword")));
            return new(true, new { accepted = true });
        }
        if (r.Operation == "mailbox/request")
        {
            var mailbox = CanonicalLoginHandle.Parse(Field(r, "mailbox")).Value;
            if (!mailbox.EndsWith("@example.test", StringComparison.Ordinal)) return new(false);
            await StageProof(u, tenant, account, "MAILBOX", mailbox, Hex(Bytes(session, "session_id")));
            return new(true, new { accepted = true });
        }
        if (r.Operation == "mailbox/complete")
        {
            var proof = await Proof(u, tenant, Field(r, "token"), "MAILBOX");
            if (proof is null || !Bytes(proof, "account_id").SequenceEqual(actor) ||
                Number(proof, "account_revision") != Number(account, "security_revision")) return new(false);
            var payload = DecodePayload(proof, tenant);
            if (payload.Binding != Hex(Bytes(session, "session_id"))) return new(false);
            var envelope = Protect(Encoding.UTF8.GetBytes(payload.Mailbox), "mailbox:" + Hex(actor));
            await u.Run("""
                UPDATE c05_identity_account SET login_lookup_hmac=@l,mailbox_ciphertext=@c,mailbox_nonce=@n,mailbox_tag=@tag,
                mailbox_key_version=1,mailbox_verified_at_utc=UTC_TIMESTAMP(6),security_revision=security_revision+1,updated_at_utc=UTC_TIMESTAMP(6) WHERE account_id=@a
                """, ("l", LoginDigest(payload.Mailbox)), ("c", envelope[28..]), ("n", envelope[..12]), ("tag", envelope[12..28]), ("a", actor));
            await RevokeAll(u, actor);
            return new(true, new { accepted = true });
        }
        if (r.Operation == "memberships/decide")
        {
            var authority = await u.One("""
                SELECT grant_id FROM c05_identity_resource_grant WHERE tenant_id=@t AND account_id=@a
                AND permission_code='MEMBERSHIP_APPROVE' AND scope_kind=2 AND resource_id IS NULL
                AND revoked_at_utc IS NULL AND granted_at_utc<=UTC_TIMESTAMP(6)
                AND expires_at_utc>UTC_TIMESTAMP(6) LIMIT 1 FOR SHARE
                """, ("t", tenant), ("a", actor));
            if (authority is null) return new(false);
            var target = await u.One("SELECT account_id FROM c05_identity_membership WHERE tenant_id=@t AND membership_id=@m", ("t", tenant), ("m", GuidBytes(Field(r, "membershipId"))));
            if (target is null || Bytes(target, "account_id").SequenceEqual(actor)) return new(false);
            var targetId = Bytes(target, "account_id");
            u.Subject = targetId;
            var targetAccount = await u.One("SELECT account_state FROM c05_identity_account WHERE account_id=@a AND mailbox_verified_at_utc IS NOT NULL FOR UPDATE", ("a", targetId));
            if (targetAccount is null || Number(targetAccount, "account_state") is not (0 or 1)) return new(false);
            var targetMember = await u.One("SELECT * FROM c05_identity_membership WHERE tenant_id=@t AND account_id=@a AND membership_state=0 FOR UPDATE", ("t", tenant), ("a", targetId));
            if (targetMember is null) return new(false);
            var approve = Field(r, "decision") == "approve";
            if (!approve && Field(r, "decision") != "reject") return new(false);
            await u.Run("""
                INSERT INTO c05_identity_membership_approval VALUES(@t,@m,@a,@s,@d,@reason,UTC_TIMESTAMP(6))
                """, ("t", tenant), ("m", Bytes(targetMember, "membership_id")), ("a", actor), ("s", Bytes(session, "session_id")),
                ("d", approve ? 1 : 2), ("reason", Digest("reason", Field(r, "reason"))));
            await u.Run("""
                UPDATE c05_identity_membership SET membership_state=@state,role_code=@role,approval_revision=approval_revision+1,
                approved_by=@approver,approved_at_utc=UTC_TIMESTAMP(6),granted_at_utc=IF(@state=1,UTC_TIMESTAMP(6),NULL),
                expires_at_utc=IF(@state=1,UTC_TIMESTAMP(6)+INTERVAL 1 YEAR,NULL) WHERE tenant_id=@t AND account_id=@a
                """, ("state", approve ? 1 : 3), ("role", approve ? "MEMBER" : "REJECTED"), ("approver", actor), ("t", tenant), ("a", targetId));
            if (approve) await u.Run("UPDATE c05_identity_account SET account_state=1,updated_at_utc=UTC_TIMESTAMP(6) WHERE account_id=@a AND account_state=0", ("a", targetId));
            return new(true, new { accepted = true });
        }
        return new(false);
    }

    private async Task<OperationalIdentityResult> RequestRecovery(Unit u, byte[] tenant, OperationalIdentityRequest r, CancellationToken ct)
    {
        var mailbox = CanonicalLoginHandle.Parse(Field(r, "mailbox")).Value;
        if (!await Reserve(tenant, "recovery:" + mailbox, 3, ct)) return new(true, new { accepted = true });
        var account = await u.One("SELECT * FROM c05_identity_account WHERE login_lookup_hmac=@l AND account_state=1 AND mailbox_verified_at_utc IS NOT NULL FOR UPDATE", ("l", LoginDigest(mailbox)));
        if (account is not null && await Membership(u, tenant, Bytes(account, "account_id")) is not null)
            await StageProof(u, tenant, account, "RECOVERY", mailbox, "");
        return new(true, new { accepted = true });
    }

    private async Task<OperationalIdentityResult> CompleteRecovery(Unit u, byte[] tenant, OperationalIdentityRequest r, CancellationToken ct)
    {
        var token = Field(r, "token");
        if (!await Reserve(tenant, "reset:" + token, 5, ct)) return new(false);
        var peek = await u.One("SELECT account_id FROM c05_identity_recovery WHERE tenant_id=@t AND token_hmac=@h AND purpose='RECOVERY'", ("t", tenant), ("h", Digest("RECOVERY", token)));
        if (peek is null) return new(false);
        var actor = Bytes(peek, "account_id");
        var account = await Account(u, actor);
        var member = await Membership(u, tenant, actor);
        var proof = await Proof(u, tenant, token, "RECOVERY");
        if (account is null || member is null || proof is null ||
            Number(proof, "account_revision") != Number(account, "security_revision")) return new(false);
        var factor = await u.One("SELECT * FROM c05_identity_mfa_factor WHERE account_id=@a AND factor_state=1 AND revoked_at_utc IS NULL FOR UPDATE", ("a", actor));
        // Recovery never bypasses MFA, including privileged accounts.
        if (factor is null || !await AcceptTotp(u, actor, factor, Field(r, "code"))) return new(false);
        await ReplacePassword(u, actor, crypto.HashPassword(Field(r, "newPassword")));
        return new(true, new { accepted = true });
    }

    private async Task StageProof(Unit u, byte[] tenant, Dictionary<string, object> account, string purpose, string mailbox, string binding)
    {
        if (!mailbox.EndsWith("@example.test", StringComparison.Ordinal)) throw new InvalidOperationException("Synthetic mailbox required.");
        var token = Token();
        var actor = Bytes(account, "account_id");
        var digest = Digest(purpose, token);
        await u.Run("UPDATE c05_identity_recovery SET consumed_at_utc=UTC_TIMESTAMP(6) WHERE account_id=@a AND purpose=@p AND consumed_at_utc IS NULL", ("a", actor), ("p", purpose));
        var proof = Protect(JsonSerializer.SerializeToUtf8Bytes(new ProofPayload(mailbox, binding)), "proof:" + Hex(tenant) + Hex(digest));
        await u.Run("INSERT INTO c05_identity_recovery (account_id,recovery_id,token_hmac,created_at_utc,expires_at_utc,tenant_id,purpose,account_revision,protected_payload) VALUES(@a,@id,@h,UTC_TIMESTAMP(6),UTC_TIMESTAMP(6)+INTERVAL 10 MINUTE,@t,@p,@v,@payload)",
            ("t", tenant), ("h", digest), ("a", actor), ("id", Id()), ("p", purpose), ("v", Number(account, "security_revision")), ("payload", proof));
        var id = Id();
        var mail = Protect(JsonSerializer.SerializeToUtf8Bytes(new DeliveryPayload(mailbox, token, purpose)), "delivery:" + Hex(id));
        await u.Run("INSERT INTO c05_identity_sensitive_outbox VALUES(@id,@payload,UTC_TIMESTAMP(6)+INTERVAL 10 MINUTE,NULL)", ("id", id), ("payload", mail));
    }

    private Task<Dictionary<string, object>?> Proof(Unit u, byte[] tenant, string token, string purpose) =>
        u.One("SELECT * FROM c05_identity_recovery WHERE tenant_id=@t AND token_hmac=@h AND purpose=@p AND consumed_at_utc IS NULL AND expires_at_utc>UTC_TIMESTAMP(6) FOR UPDATE", ("t", tenant), ("h", Digest(purpose, token)), ("p", purpose));

    private ProofPayload DecodePayload(Dictionary<string, object> proof, byte[] tenant)
    {
        var bytes = Unprotect(Bytes(proof, "protected_payload"), "proof:" + Hex(tenant) + Hex(Bytes(proof, "token_hmac")));
        try { return JsonSerializer.Deserialize<ProofPayload>(bytes) ?? throw new InvalidOperationException("Invalid proof."); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private async Task ReplacePassword(Unit u, byte[] actor, string phc)
    {
        await u.Run("UPDATE c05_identity_account SET password_phc=@p,security_revision=security_revision+1,updated_at_utc=UTC_TIMESTAMP(6) WHERE account_id=@a", ("p", phc), ("a", actor));
        await RevokeAll(u, actor);
    }

    private static async Task RevokeAll(Unit u, byte[] actor)
    {
        await u.Run("UPDATE c05_identity_session SET revoked_at_utc=UTC_TIMESTAMP(6) WHERE account_id=@a AND revoked_at_utc IS NULL", ("a", actor));
        await u.Run("UPDATE c05_identity_challenge SET consumed_at_utc=UTC_TIMESTAMP(6) WHERE account_id=@a AND consumed_at_utc IS NULL", ("a", actor));
        await u.Run("UPDATE c05_identity_recovery SET consumed_at_utc=UTC_TIMESTAMP(6) WHERE account_id=@a AND consumed_at_utc IS NULL", ("a", actor));
    }

    private static Task<Dictionary<string, object>?> Account(Unit u, byte[] actor)
    {
        u.Actor = actor;
        return u.One("SELECT * FROM c05_identity_account WHERE account_id=@a AND account_state=1 AND mailbox_verified_at_utc IS NOT NULL FOR UPDATE", ("a", actor));
    }
    private static Task<Dictionary<string, object>?> Membership(Unit u, byte[] tenant, byte[] actor) =>
        u.One("SELECT * FROM c05_identity_membership WHERE tenant_id=@t AND account_id=@a AND membership_state=1 AND granted_at_utc<=UTC_TIMESTAMP(6) AND expires_at_utc>UTC_TIMESTAMP(6) FOR UPDATE", ("t", tenant), ("a", actor));

    private Task<int> Audit(Unit u, byte[] tenant, string action, bool allowed) =>
        u.Run("INSERT INTO c05_identity_audit VALUES(@t,@id,@actor,@subject,@action,@outcome,@ctx,UTC_TIMESTAMP(6))",
            ("t", tenant), ("id", Id()), ("actor", (object?)u.Actor ?? DBNull.Value),
            ("subject", (object?)u.Subject ?? DBNull.Value), ("action", action), ("outcome", allowed ? 1 : 0), ("ctx", Digest("audit", Token())));

    // Local SMTP delivery is durable and at-least-once. Proof consumption is
    // one-shot even if SMTP accepted a message before a dispatcher crash.
    public async Task DispatchOneAsync(int smtpPort, CancellationToken ct)
    {
        await using var db = new MySqlConnection(dsn);
        await db.OpenAsync(ct);
        await using var tx = await db.BeginTransactionAsync(ct);
        var u = new Unit(db, tx, ct);
        var row = await u.One("SELECT * FROM c05_identity_sensitive_outbox WHERE delivered_at_utc IS NULL AND expires_at_utc>UTC_TIMESTAMP(6) LIMIT 1 FOR UPDATE SKIP LOCKED");
        if (row is null) return;
        var bytes = Unprotect(Bytes(row, "protected_payload"), "delivery:" + Hex(Bytes(row, "message_id")));
        try
        {
            var payload = JsonSerializer.Deserialize<DeliveryPayload>(bytes) ?? throw new InvalidOperationException("Invalid outbox.");
            await new LoopbackOnlySmtpProofSink(smtpPort).DeliverInTestOnlyAsync(payload.Mailbox, Convert.FromHexString(payload.Token), ct);
            await u.Run("UPDATE c05_identity_sensitive_outbox SET delivered_at_utc=UTC_TIMESTAMP(6) WHERE message_id=@id", ("id", Bytes(row, "message_id")));
            await tx.CommitAsync(ct);
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    private byte[] Protect(byte[] plaintext, string purpose)
    {
        var result = new byte[28 + plaintext.Length];
        RandomNumberGenerator.Fill(result.AsSpan(0, 12));
        using var aes = new AesGcm(encryptionKey, 16);
        aes.Encrypt(result.AsSpan(0, 12), plaintext, result.AsSpan(28), result.AsSpan(12, 16), Encoding.UTF8.GetBytes("C05/operational/v1/" + purpose));
        return result;
    }
    private byte[] Unprotect(byte[] envelope, string purpose)
    {
        var result = new byte[envelope.Length - 28];
        using var aes = new AesGcm(encryptionKey, 16);
        aes.Decrypt(envelope.AsSpan(0, 12), envelope.AsSpan(28), envelope.AsSpan(12, 16), result, Encoding.UTF8.GetBytes("C05/operational/v1/" + purpose));
        return result;
    }
    private byte[] Digest(string purpose, string value) => HMACSHA256.HashData(tokenKey, Encoding.UTF8.GetBytes("C05/" + purpose + ":" + value));
    private byte[] LoginDigest(string mailbox) => HMACSHA256.HashData(loginKey, Encoding.UTF8.GetBytes(mailbox));
    private static string Token() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private static byte[] Id() => Guid.NewGuid().ToByteArray(true);
    private static byte[] GuidBytes(string value) => Guid.TryParse(value, out var id) && id != Guid.Empty ? id.ToByteArray(true) : throw new InvalidOperationException("Invalid identifier.");
    private static string Hex(byte[] bytes) => Convert.ToHexString(bytes);
    private static string Field(OperationalIdentityRequest r, string name) => r.Fields.TryGetValue(name, out var value) && value.Length is > 0 and <= 512 ? value : throw new InvalidOperationException("Invalid request.");
    private static byte[] Bytes(Dictionary<string, object> row, string key) => (byte[])row[key];
    private static string Text(Dictionary<string, object> row, string key) => (string)row[key];
    private static long Number(Dictionary<string, object> row, string key) => Convert.ToInt64(row[key]);
    private static string Base32(byte[] bytes)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var result = new StringBuilder();
        int value = 0, bits = 0;
        foreach (var b in bytes)
        {
            value = (value << 8) | b; bits += 8;
            while (bits >= 5) { result.Append(alphabet[(value >> (bits - 5)) & 31]); bits -= 5; }
        }
        if (bits > 0) result.Append(alphabet[(value << (5 - bits)) & 31]);
        return result.ToString();
    }
    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(tokenKey);
        CryptographicOperations.ZeroMemory(encryptionKey);
        CryptographicOperations.ZeroMemory(loginKey);
    }
    public override string ToString() => "MySqlOperationalIdentityFlow[redacted]";
    private sealed record ProofPayload(string Mailbox, string Binding);
    private sealed record DeliveryPayload(string Mailbox, string Token, string Purpose);

    private sealed class Unit(MySqlConnection db, MySqlTransaction tx, CancellationToken ct)
    {
        public byte[]? Actor { get; set; }
        public byte[]? Subject { get; set; }
        private MySqlCommand Command(string sql, (string, object)[] parameters)
        {
            var command = new MySqlCommand(sql, db, tx);
            foreach (var (name, value) in parameters) command.Parameters.AddWithValue("@" + name, value);
            return command;
        }
        public async Task<int> Run(string sql, params (string, object)[] parameters)
        {
            await using var command = Command(sql, parameters);
            return await command.ExecuteNonQueryAsync(ct);
        }
        public async Task<List<Dictionary<string, object>>> All(string sql, params (string, object)[] parameters)
        {
            await using var command = Command(sql, parameters);
            await using var rows = await command.ExecuteReaderAsync(ct);
            var result = new List<Dictionary<string, object>>();
            while (await rows.ReadAsync(ct))
            {
                var row = new Dictionary<string, object>(StringComparer.Ordinal);
                for (int i = 0; i < rows.FieldCount; i++) row.Add(rows.GetName(i), rows.GetValue(i));
                result.Add(row);
            }
            return result;
        }
        public async Task<Dictionary<string, object>?> One(string sql, params (string, object)[] parameters)
        {
            var rows = await All(sql, parameters);
            return rows.Count == 1 ? rows[0] : null;
        }
    }
}
