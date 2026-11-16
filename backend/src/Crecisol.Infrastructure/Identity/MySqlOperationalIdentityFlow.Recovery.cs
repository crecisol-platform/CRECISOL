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
    private async Task<OperationalIdentityResult> RequestRecovery(Unit u, byte[] tenant, OperationalIdentityRequest r, CancellationToken ct)
    {
        var mailbox = CanonicalLoginHandle.Parse(Field(r, "mailbox")).Value;
        if (!await Reserve(tenant, "recovery:" + mailbox, 3, ct)) return new(true, new { accepted = true });
        var account = await u.One("SELECT * FROM c05_identity_account WHERE login_lookup_hmac=@l AND account_state=1 AND mailbox_verified_at_utc IS NOT NULL FOR UPDATE", ("l", LoginDigest(mailbox)));
        if (account is not null && await Membership(u, tenant, Bytes(account, "account_id")) is not null)
        {
            await InitializeContact(u, Bytes(account, "account_id"), mailbox);
            await StageProof(u, tenant, account, "RECOVERY", mailbox, "");
        }
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
        var factor = await u.One("SELECT * FROM c05_identity_mfa_factor WHERE account_id=@a AND factor_kind=1 AND factor_state=1 AND revoked_at_utc IS NULL FOR UPDATE", ("a", actor));
        // Recovery never bypasses MFA, including privileged accounts.
        if (factor is null || !await AcceptTotp(u, actor, factor, Field(r, "code"))) return new(false);
        await ReplacePassword(u, tenant, actor, crypto.HashPassword(Field(r, "newPassword")));
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

    private async Task ReplacePassword(Unit u, byte[] tenant, byte[] actor, string phc)
    {
        await u.Run("UPDATE c05_identity_account SET password_phc=@p,security_revision=security_revision+1,updated_at_utc=UTC_TIMESTAMP(6) WHERE account_id=@a", ("p", phc), ("a", actor));
        await RevokeAll(u, actor);
        await NotifySecurity(u, tenant, actor, "PASSWORD_CHANGED");
    }

    private static async Task RevokeAll(Unit u, byte[] actor)
    {
        await u.Run("UPDATE c05_identity_session SET revoked_at_utc=UTC_TIMESTAMP(6) WHERE account_id=@a AND revoked_at_utc IS NULL", ("a", actor));
        await u.Run("UPDATE c05_identity_challenge SET consumed_at_utc=UTC_TIMESTAMP(6) WHERE account_id=@a AND consumed_at_utc IS NULL", ("a", actor));
        await u.Run("UPDATE c05_identity_recovery SET consumed_at_utc=UTC_TIMESTAMP(6) WHERE account_id=@a AND consumed_at_utc IS NULL", ("a", actor));
        await u.Run("UPDATE c05_identity_pairing SET consumed_at_utc=UTC_TIMESTAMP(6) WHERE account_id=@a AND consumed_at_utc IS NULL", ("a", actor));
        await u.Run("UPDATE c05_identity_mfa_reset_authorization SET consumed_at_utc=UTC_TIMESTAMP(6) WHERE account_id=@a AND consumed_at_utc IS NULL", ("a", actor));
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
            ("t", tenant), ("id", Id()), ("actor", u.VerifiedActor ? (object?)u.Actor ?? DBNull.Value : DBNull.Value),
            ("subject", (object?)u.Subject ?? (!u.VerifiedActor ? (object?)u.Actor : null) ?? DBNull.Value), ("action", action), ("outcome", allowed ? 1 : 0), ("ctx", Digest("audit", Token())));
}
