using System.Security.Cryptography;
using Crecisol.Application.Identity;

namespace Crecisol.Infrastructure.Identity;

public sealed partial class MySqlOperationalIdentityFlow
{
    private async Task<OperationalIdentityResult> Pairing(Unit u, byte[] tenant, OperationalIdentityRequest r, CancellationToken ct)
    {
        if (r.Operation == "pairings/start")
        {
            var key = Convert.FromHexString(Field(r, "publicKey"));
            // Proof of possession even when creating a request: malformed or
            // unsupported public keys never enter an approvable ledger.
            if (key.Length > 256 || !crypto.VerifyPairing(key, "CRECISOL:C05:PAIR:START", Convert.FromHexString(Field(r, "signature")))) return new(false);
            var token = Token(); var nonce = RandomNumberGenerator.GetBytes(32);
            await u.Run("INSERT INTO c05_identity_pairing(tenant_id,token_hmac,public_key,nonce,expires_at_utc) VALUES(@t,@h,@key,@n,UTC_TIMESTAMP(6)+INTERVAL 2 MINUTE)",
                ("t", tenant), ("h", Digest("pairing", token)), ("key", key), ("n", nonce));
            return new(true, new { token, nonce = Hex(nonce), fingerprint = PairingFingerprint(key), expiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(2) });
        }
        var digest = Digest("pairing", Field(r, "token"));
        var peek = await u.One("SELECT account_id FROM c05_identity_pairing WHERE tenant_id=@t AND token_hmac=@h AND approved_at_utc IS NOT NULL", ("t", tenant), ("h", digest));
        if (peek is null || peek["account_id"] is DBNull) return new(false);
        var actor = Bytes(peek, "account_id");
        if (!await Reserve(tenant, "pairing:" + Hex(actor), 5, ct)) return new(false);
        var account = await Account(u, actor);
        var member = await Membership(u, tenant, actor);
        var row = await u.One("SELECT * FROM c05_identity_pairing WHERE tenant_id=@t AND token_hmac=@h AND approved_at_utc IS NOT NULL AND expires_at_utc>UTC_TIMESTAMP(6) AND consumed_at_utc IS NULL FOR UPDATE", ("t", tenant), ("h", digest));
        if (account is null || member is null || row is null || Number(account, "security_revision") != Number(row, "account_revision") ||
            Number(member, "approval_revision") != Number(row, "membership_revision")) return new(false);
        var factor = await u.One("SELECT factor_id FROM c05_identity_mfa_factor WHERE account_id=@a AND factor_id=@f AND factor_state=1 AND revoked_at_utc IS NULL FOR UPDATE", ("a", actor), ("f", Bytes(row, "factor_id")));
        if (factor is null || !crypto.VerifyPairing(Bytes(row, "public_key"),
            "CRECISOL:C05:PAIR:" + Field(r, "token") + ":" + Hex(Bytes(row, "nonce")), Convert.FromHexString(Field(r, "signature")))) return new(false);
        await u.Run("UPDATE c05_identity_pairing SET consumed_at_utc=UTC_TIMESTAMP(6) WHERE tenant_id=@t AND token_hmac=@h", ("t", tenant), ("h", digest));
        u.VerifiedActor = true;
        u.SessionMethod = "PAIRING";
        return await IssueSession(u, tenant, account, member, Bytes(row, "factor_id"), null);
    }

    private async Task<OperationalIdentityResult> ApprovePairing(Unit u, byte[] tenant, Dictionary<string, object> account,
        Dictionary<string, object> member, Dictionary<string, object> session, OperationalIdentityRequest r)
    {
        if (Field(r, "confirmed") != "true") return new(false);
        var digest = Digest("pairing", Field(r, "token"));
        var row = await u.One("SELECT public_key FROM c05_identity_pairing WHERE tenant_id=@t AND token_hmac=@h AND approved_at_utc IS NULL AND consumed_at_utc IS NULL AND expires_at_utc>UTC_TIMESTAMP(6) FOR UPDATE", ("t", tenant), ("h", digest));
        if (row is null || Field(r, "fingerprint") != PairingFingerprint(Bytes(row, "public_key"))) return new(false);
        await u.Run("""
            UPDATE c05_identity_pairing SET account_id=@a,membership_id=@m,account_revision=@ar,membership_revision=@mr,
            factor_id=@f,approved_at_utc=UTC_TIMESTAMP(6) WHERE tenant_id=@t AND token_hmac=@h
            """, ("t", tenant), ("h", digest), ("a", Bytes(account, "account_id")), ("m", Bytes(member, "membership_id")),
            ("ar", Number(account, "security_revision")), ("mr", Number(member, "approval_revision")), ("f", Bytes(session, "mfa_factor_id")));
        return new(true, new { accepted = true });
    }
    private static string PairingFingerprint(byte[] key) => Hex(SHA256.HashData(key))[..12];
}
