using System.Security.Cryptography;
using System.Text;
using Crecisol.Application.Identity;
using Crecisol.Domain.Identity;
using Crecisol.Domain.Tenancy;

namespace Crecisol.Infrastructure.Identity;

public sealed partial class MySqlOperationalIdentityFlow
{
    private async Task<OperationalIdentityResult> RequestAdditionalMembership(Unit u, byte[] tenant, OperationalIdentityRequest r, CancellationToken ct)
    {
        var mailbox = CanonicalLoginHandle.Parse(Field(r, "mailbox")).Value;
        if (!await Reserve(tenant, "join:" + mailbox, 5, ct)) return new(false);
        var account = await u.One("SELECT * FROM c05_identity_account WHERE login_lookup_hmac=@l FOR UPDATE", ("l", LoginDigest(mailbox)));
        var passwordValid = crypto.VerifyPassword(Field(r, "password"), account is null ? dummyPhc : Text(account, "password_phc"));
        if (account is null || !passwordValid || Number(account, "account_state") != 1 || account["mailbox_verified_at_utc"] is DBNull) return new(false);
        var actor = Bytes(account, "account_id"); u.Actor = actor;
        if (await u.One("SELECT membership_id FROM c05_identity_membership WHERE tenant_id=@t AND account_id=@a FOR UPDATE", ("t", tenant), ("a", actor)) is not null) return new(false);
        var factor = await u.One("SELECT * FROM c05_identity_mfa_factor WHERE account_id=@a AND factor_kind=1 AND factor_state=1 AND revoked_at_utc IS NULL FOR UPDATE", ("a", actor));
        if (factor is null || !await AcceptTotp(u, actor, factor, Field(r, "code"))) return new(false);
        u.VerifiedActor = true;
        if (await MatchingTerms(u, tenant, r) is null) return new(false);
        var proofId = GuidBytes(Field(r, "proofId")); var proof = Convert.FromHexString(Field(r, "proofCode"));
        if (proof.Length != 32) return new(false);
        var mailboxBytes = Encoding.UTF8.GetBytes(mailbox); var material = tenant.Concat(mailboxBytes).ToArray();
        try
        {
            var digest = EmailPossessionDigest.FromToken(TenantId.From(new Guid(tenant, true)), new Guid(proofId, true), proof).CopyForProtectedDatabase();
            var consumed = await u.One("CALL c05_operational_consume_join_proof(@t,@p,@h,@mail)", ("t", tenant), ("p", proofId),
                ("h", digest), ("mail", HMACSHA256.HashData(mailboxHmacKey, material)));
            if (consumed is null || Number(consumed, "consumed") != 1) return new(false);
            await u.Run("""
                INSERT INTO c05_identity_membership(tenant_id,membership_id,account_id,membership_state,role_code,created_at_utc)
                VALUES(@t,@id,@a,0,'PENDING',UTC_TIMESTAMP(6))
                """, ("t", tenant), ("id", Id()), ("a", actor));
            if (!await AcceptTerms(u, tenant, actor, r)) throw new InvalidOperationException("Pinned terms changed inside transition.");
            return new(true, new { accepted = true });
        }
        finally { CryptographicOperations.ZeroMemory(proof); CryptographicOperations.ZeroMemory(material); CryptographicOperations.ZeroMemory(mailboxBytes); }
    }
}
