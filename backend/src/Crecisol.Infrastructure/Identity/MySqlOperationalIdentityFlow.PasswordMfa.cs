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
public sealed partial class MySqlOperationalIdentityFlow
{
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
        await InitializeContact(u, actor, mailbox);
        var member = await Membership(u, tenant, actor);
        if (member is null) return new(false);
        var factor = await u.One("SELECT factor_id FROM c05_identity_mfa_factor WHERE account_id=@a AND factor_kind=1 AND factor_state=1 AND revoked_at_utc IS NULL FOR UPDATE", ("a", actor));
        // Revoking an enrolled factor must never downgrade an account back to
        // password-only enrollment. Replacement has its own step-up challenge.
        var resetRequired = false;
        if (factor is null && await u.One("SELECT factor_id FROM c05_identity_mfa_factor WHERE account_id=@a AND factor_kind=1 AND factor_state=2 LIMIT 1 FOR UPDATE", ("a", actor)) is not null)
        {
            var reset = await u.One("SELECT account_revision FROM c05_identity_mfa_reset_authorization WHERE account_id=@a AND tenant_id=@t AND consumed_at_utc IS NULL AND expires_at_utc>UTC_TIMESTAMP(6) FOR UPDATE", ("a", actor), ("t", tenant));
            if (reset is null || Number(reset, "account_revision") != Number(account, "security_revision")) return new(false);
            resetRequired = true;
        }
        return await Challenge(u, tenant, account, member, factor is null, resetRequired: resetRequired);
    }

    private async Task<OperationalIdentityResult> Challenge(Unit u, byte[] tenant,
        Dictionary<string, object> account, Dictionary<string, object> member, bool enrollmentRequired, byte[]? replaceFactor = null, bool resetRequired = false)
    {
        var challenge = Token();
        await u.Run("""
            INSERT INTO c05_identity_challenge
            (tenant_id,token_hmac,account_id,membership_id,account_revision,membership_revision,replace_factor_id,expires_at_utc,reset_required)
            VALUES(@t,@h,@a,@m,@ar,@mr,@replace,UTC_TIMESTAMP(6)+INTERVAL 5 MINUTE,@reset)
            """, ("t", tenant), ("h", Digest("challenge", challenge)), ("a", Bytes(account, "account_id")),
            ("m", Bytes(member, "membership_id")), ("ar", Number(account, "security_revision")),
            ("mr", Number(member, "approval_revision")), ("replace", (object?)replaceFactor ?? DBNull.Value), ("reset", resetRequired));
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
        if (Number(challenge, "reset_required") == 1)
        {
            var reset = await u.One("SELECT account_revision FROM c05_identity_mfa_reset_authorization WHERE account_id=@a AND tenant_id=@t AND consumed_at_utc IS NULL AND expires_at_utc>UTC_TIMESTAMP(6) FOR UPDATE", ("a", actor), ("t", tenant));
            if (reset is null || Number(reset, "account_revision") != Number(account, "security_revision")) return new(false);
        }
        if (r.Operation == "mfa/enroll")
        {
            var activeFactor = await u.One("SELECT factor_id FROM c05_identity_mfa_factor WHERE account_id=@a AND factor_kind=1 AND factor_state=1 AND revoked_at_utc IS NULL FOR UPDATE", ("a", actor));
            var replacement = challenge["replace_factor_id"] is not DBNull;
            if (challenge["factor_id"] is not DBNull ||
                (!replacement && activeFactor is not null) ||
                (replacement && (activeFactor is null || !Bytes(activeFactor, "factor_id").SequenceEqual(Bytes(challenge, "replace_factor_id")))))
                return new(false);
            var factorId = Id();
            var seed = RandomNumberGenerator.GetBytes(20);
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
                return new(true, new { secret, uri = TotpUri(actor, seed, "cuenta") });
            }
            finally { CryptographicOperations.ZeroMemory(seed); }
        }
        await u.Run("UPDATE c05_identity_challenge SET attempts=attempts+1 WHERE tenant_id=@t AND token_hmac=@h", ("t", tenant), ("h", hash));
        var factor = challenge["factor_id"] is DBNull
            ? await u.One("SELECT * FROM c05_identity_mfa_factor WHERE account_id=@a AND factor_kind=1 AND factor_state=1 AND revoked_at_utc IS NULL FOR UPDATE", ("a", actor))
            : await u.One("SELECT * FROM c05_identity_mfa_factor WHERE account_id=@a AND factor_id=@f AND factor_state=0 FOR UPDATE",
                ("a", actor), ("f", Bytes(challenge, "factor_id")));
        var replacing = challenge["replace_factor_id"] is not DBNull;
        if (challenge["factor_id"] is not DBNull)
        {
            var active = await u.One("SELECT factor_id FROM c05_identity_mfa_factor WHERE account_id=@a AND factor_kind=1 AND factor_state=1 AND revoked_at_utc IS NULL FOR UPDATE", ("a", actor));
            if ((!replacing && active is not null) || (replacing &&
                (active is null || !Bytes(active, "factor_id").SequenceEqual(Bytes(challenge, "replace_factor_id"))))) return new(false);
        }
        if (factor is null || !await AcceptTotp(u, actor, factor, Field(r, "code"), !replacing)) return new(false);
        u.VerifiedActor = true;
        u.SessionMethod = Field(r, "code").Length == 32 ? "BACKUP" : "TOTP";
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
            await u.Run("UPDATE c05_identity_mfa_reset_authorization SET consumed_at_utc=UTC_TIMESTAMP(6) WHERE account_id=@a AND consumed_at_utc IS NULL", ("a", actor));
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
        if (challenge["factor_id"] is not DBNull) await NotifySecurity(u, tenant, actor, replacing ? "MFA_REPLACED" : "MFA_ENROLLED");
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
        DateTime? originalMfa = null)
    {
        var token = Token();
        var id = Id();
        // Session expiration slides after genuine validated activity; MFA challenge TTLs do not.
        var expiry = DateTimeOffset.UtcNow.AddMinutes(await SessionIdleMinutes(u));
        u.SessionExpiresAtUtc = expiry;
        await u.Run("""
            INSERT INTO c05_identity_session
            (tenant_id,session_id,account_id,membership_id,token_hmac,session_family,mfa_factor_id,mfa_verified_at_utc,
             issued_at_utc,expires_at_utc,account_revision,membership_revision,last_seen_at_utc,device_label,peer_hmac,auth_method)
            VALUES(@t,@id,@a,@m,@h,@family,@f,@mfa,UTC_TIMESTAMP(6),@expiry,@ar,@mr,UTC_TIMESTAMP(6),@device,@peer,@method)
            """, ("t", tenant), ("id", id), ("a", Bytes(account, "account_id")), ("m", Bytes(member, "membership_id")),
            ("h", Digest("session", token)), ("family", family ?? id), ("f", factor),
            ("ar", Number(account, "security_revision")), ("mr", Number(member, "approval_revision")),
            ("expiry", expiry.UtcDateTime), ("mfa", originalMfa ?? DateTime.UtcNow), ("device", u.DeviceLabel), ("peer", u.PeerHash), ("method", u.SessionMethod));
        return new(true, new OperationalSessionReceipt(token, expiry));
    }
}
