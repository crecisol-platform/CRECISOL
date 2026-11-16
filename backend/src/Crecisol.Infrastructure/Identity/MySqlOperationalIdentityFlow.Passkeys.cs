using System.Text.Json;
using Crecisol.Application.Identity;

namespace Crecisol.Infrastructure.Identity;

public sealed partial class MySqlOperationalIdentityFlow
{
    private async Task<OperationalIdentityResult> PasskeyLogin(Unit u, byte[] tenant, OperationalIdentityRequest r, CancellationToken ct)
    {
        if (r.Operation == "passkeys/options")
            return await PasskeyCeremony(u, tenant, "LOGIN", passkeys.AssertionOptions(), null, null);
        var response = Field(r, "response");
        var id = passkeys.ReadCredentialId(response);
        if (id.Length is < 16 or > 1024) return new(false);
        var key = await u.One("SELECT * FROM c05_identity_passkey WHERE credential_id=@id", ("id", id));
        if (key is null) return new(false);
        var actor = Bytes(key, "account_id");
        if (!await Reserve(tenant, "passkey:" + Hex(actor), 8, ct)) return new(false);
        var account = await Account(u, actor);
        var member = await Membership(u, tenant, actor);
        if (account is null || member is null) return new(false);
        var factor = await u.One("SELECT factor_id FROM c05_identity_mfa_factor WHERE account_id=@a AND factor_id=@f AND factor_kind=2 AND factor_state=1 AND revoked_at_utc IS NULL FOR UPDATE",
            ("a", actor), ("f", Bytes(key, "factor_id")));
        var ceremony = await TakePasskeyCeremony(u, tenant, Field(r, "challenge"), "LOGIN");
        if (factor is null || ceremony is null) return new(false);
        // Account lock serializes counters and revocation. Re-read after locking.
        key = await u.One("SELECT * FROM c05_identity_passkey WHERE credential_id=@id FOR UPDATE", ("id", id));
        uint counter;
        try
        {
            counter = await passkeys.VerifyAsync(Text(ceremony, "options_json"), response,
                new(id, Bytes(key!, "public_key"), checked((uint)Number(key!, "signature_count"))), actor, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception) { return new(false); }
        await u.Run("UPDATE c05_identity_passkey SET signature_count=@c WHERE credential_id=@id", ("c", counter), ("id", id));
        u.VerifiedActor = true;
        u.SessionMethod = "WEBAUTHN";
        return await IssueSession(u, tenant, account, member, Bytes(factor, "factor_id"), null);
    }

    private async Task<OperationalIdentityResult> AuthenticatedPasskey(Unit u, byte[] tenant, OperationalIdentityRequest r,
        Dictionary<string, object> account, Dictionary<string, object> session, CancellationToken ct)
    {
        var actor = Bytes(account, "account_id");
        if (r.Operation == "passkeys")
        {
            var rows = await u.All("SELECT f.factor_id,f.enrolled_at_utc FROM c05_identity_mfa_factor f WHERE account_id=@a AND factor_kind=2 AND factor_state=1 AND revoked_at_utc IS NULL", ("a", actor));
            return new(true, new { passkeys = rows.Select(row => new { id = new Guid(Bytes(row, "factor_id"), true), createdAtUtc = row["enrolled_at_utc"] }) });
        }
        if (r.Operation == "passkeys/revoke")
        {
            var changed = await u.Run("UPDATE c05_identity_mfa_factor SET factor_state=2,revoked_at_utc=UTC_TIMESTAMP(6),factor_revision=factor_revision+1 WHERE account_id=@a AND factor_id=@f AND factor_kind=2 AND factor_state=1 AND revoked_at_utc IS NULL",
                ("a", actor), ("f", GuidBytes(Field(r, "factorId"))));
            if (changed != 1) return new(false);
            await RevokeAll(u, actor);
            await NotifySecurity(u, tenant, actor, "PASSKEY_REVOKED");
            return new(true, new { accepted = true });
        }
        if (r.Operation == "passkeys/register-options")
        {
            var existing = await u.All("SELECT p.credential_id FROM c05_identity_passkey p JOIN c05_identity_mfa_factor f USING(account_id,factor_id) WHERE p.account_id=@a AND f.factor_state=1 AND f.revoked_at_utc IS NULL", ("a", actor));
            if (existing.Count >= 8) return new(false);
            return await PasskeyCeremony(u, tenant, "REGISTER", passkeys.RegistrationOptions(actor,
                existing.Select(row => Bytes(row, "credential_id")).ToArray()), account, session);
        }
        var ceremony = await TakePasskeyCeremony(u, tenant, Field(r, "challenge"), "REGISTER");
        if (ceremony is null || !Bytes(ceremony, "account_id").SequenceEqual(actor) ||
            !Bytes(ceremony, "session_id").SequenceEqual(Bytes(session, "session_id")) ||
            Number(ceremony, "account_revision") != Number(account, "security_revision")) return new(false);
        // Concurrent options cannot bypass the credential cap.
        var count = await u.All("SELECT factor_id FROM c05_identity_mfa_factor WHERE account_id=@a AND factor_kind=2 AND factor_state=1", ("a", actor));
        if (count.Count >= 8) return new(false);
        OperationalPasskey credential;
        try
        {
            credential = await passkeys.RegisterAsync(Text(ceremony, "options_json"), Field(r, "response"),
                async id => await u.One("SELECT credential_id FROM c05_identity_passkey WHERE credential_id=@id", ("id", id)) is null, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception) { return new(false); }
        var factor = Id();
        // No private key or synthetic TOTP seed: the WebAuthn credential is a
        // distinct factor and its public key is stored in its own typed ledger.
        await u.Run("""
            INSERT INTO c05_identity_mfa_factor
            (account_id,factor_id,factor_kind,protected_secret,secret_nonce,secret_tag,key_version,factor_state,enrolled_at_utc)
            VALUES(@a,@f,2,X'',REPEAT(CHAR(0),12),REPEAT(CHAR(0),16),1,1,UTC_TIMESTAMP(6))
            """, ("a", actor), ("f", factor));
        await u.Run("INSERT INTO c05_identity_passkey VALUES(@id,@a,@f,@pk,@c,UTC_TIMESTAMP(6))",
            ("id", credential.CredentialId), ("a", actor), ("f", factor), ("pk", credential.PublicKey), ("c", credential.Counter));
        await NotifySecurity(u, tenant, actor, "PASSKEY_ENROLLED");
        return new(true, new { accepted = true });
    }

    private async Task<OperationalIdentityResult> PasskeyCeremony(Unit u, byte[] tenant, string kind, string options,
        Dictionary<string, object>? account, Dictionary<string, object>? session)
    {
        var challenge = Token();
        await u.Run("""
            INSERT INTO c05_identity_passkey_ceremony VALUES(@t,@h,@kind,@a,@s,@v,@options,UTC_TIMESTAMP(6)+INTERVAL 2 MINUTE,NULL)
            """, ("t", tenant), ("h", Digest("passkey", challenge)), ("kind", kind),
            ("a", account is null ? DBNull.Value : Bytes(account, "account_id")),
            ("s", session is null ? DBNull.Value : Bytes(session, "session_id")),
            ("v", account is null ? DBNull.Value : Number(account, "security_revision")), ("options", options));
        return new(true, new { challenge, publicKey = JsonSerializer.Deserialize<JsonElement>(options) });
    }

    private async Task<Dictionary<string, object>?> TakePasskeyCeremony(Unit u, byte[] tenant, string token, string kind)
    {
        var digest = Digest("passkey", token);
        var row = await u.One("SELECT * FROM c05_identity_passkey_ceremony WHERE tenant_id=@t AND token_hmac=@h AND ceremony_kind=@kind AND consumed_at_utc IS NULL AND expires_at_utc>UTC_TIMESTAMP(6) FOR UPDATE",
            ("t", tenant), ("h", digest), ("kind", kind));
        if (row is not null)
            await u.Run("UPDATE c05_identity_passkey_ceremony SET consumed_at_utc=UTC_TIMESTAMP(6) WHERE tenant_id=@t AND token_hmac=@h", ("t", tenant), ("h", digest));
        return row;
    }
}
