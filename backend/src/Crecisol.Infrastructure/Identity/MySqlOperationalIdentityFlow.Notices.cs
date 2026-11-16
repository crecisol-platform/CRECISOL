using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Crecisol.Application.Identity;

namespace Crecisol.Infrastructure.Identity;

public sealed partial class MySqlOperationalIdentityFlow
{
    private Task InitializeContact(Unit u, byte[] actor, string verifiedMailbox) =>
        u.Run("INSERT IGNORE INTO c05_identity_contact SELECT account_id,@mail,NULL,mailbox_verified_at_utc FROM c05_identity_account WHERE account_id=@a AND mailbox_verified_at_utc IS NOT NULL", ("a", actor),
            ("mail", Protect(Encoding.UTF8.GetBytes(verifiedMailbox), "contact:" + Hex(actor))));

    private Task ChangeContact(Unit u, byte[] actor, string verifiedMailbox) =>
        u.Run("UPDATE c05_identity_contact SET previous_mailbox=protected_mailbox,protected_mailbox=@mail,changed_at_utc=UTC_TIMESTAMP(6) WHERE account_id=@a",
            ("a", actor), ("mail", Protect(Encoding.UTF8.GetBytes(verifiedMailbox), "contact:" + Hex(actor))));

    private async Task<string> ContactMailbox(Unit u, byte[] actor, bool previous = false)
    {
        var row = await u.One("SELECT * FROM c05_identity_contact WHERE account_id=@a FOR UPDATE", ("a", actor))
            ?? throw new InvalidOperationException("Verified contact unavailable.");
        var envelope = previous ? Bytes(row, "previous_mailbox") : Bytes(row, "protected_mailbox");
        var raw = Unprotect(envelope, "contact:" + Hex(actor));
        try { return Encoding.UTF8.GetString(raw); }
        finally { CryptographicOperations.ZeroMemory(raw); }
    }

    private async Task NotifySecurity(Unit u, byte[] tenant, byte[] actor, string action, bool previous = false)
    {
        var mailbox = await ContactMailbox(u, actor, previous);
        var token = Token(); var id = Id();
        await u.Run("INSERT INTO c05_identity_incident VALUES(@t,@h,@a,@event,UTC_TIMESTAMP(6),UTC_TIMESTAMP(6)+INTERVAL 1 DAY,NULL)",
            ("t", tenant), ("h", Digest("incident", token)), ("a", actor), ("event", action));
        var mail = Protect(JsonSerializer.SerializeToUtf8Bytes(new DeliveryPayload(mailbox, token, "NOTICE_" + action)), "delivery:" + Hex(id));
        await u.Run("INSERT INTO c05_identity_sensitive_outbox VALUES(@id,@mail,UTC_TIMESTAMP(6)+INTERVAL 1 DAY,NULL)", ("id", id), ("mail", mail));
    }

    private async Task<OperationalIdentityResult> ReportIncident(Unit u, byte[] tenant, OperationalIdentityRequest r)
    {
        var digest = Digest("incident", Field(r, "token"));
        var peek = await u.One("SELECT account_id FROM c05_identity_incident WHERE tenant_id=@t AND token_hmac=@h", ("t", tenant), ("h", digest));
        if (peek is null) return new(false);
        var actor = Bytes(peek, "account_id");
        // A report can revoke active sessions even while a support case has
        // suspended normal login; it cannot change a password or grant access.
        var account = await u.One("SELECT account_id FROM c05_identity_account WHERE account_id=@a FOR UPDATE", ("a", actor));
        var notice = await u.One("SELECT account_id FROM c05_identity_incident WHERE tenant_id=@t AND token_hmac=@h AND reported_at_utc IS NULL AND expires_at_utc>UTC_TIMESTAMP(6) FOR UPDATE", ("t", tenant), ("h", digest));
        if (account is null || notice is null) return new(false);
        u.Actor = actor;
        await u.Run("UPDATE c05_identity_incident SET reported_at_utc=UTC_TIMESTAMP(6) WHERE tenant_id=@t AND token_hmac=@h", ("t", tenant), ("h", digest));
        await RevokeAll(u, actor);
        await u.Run("UPDATE c05_identity_assisted_case SET blocked=1 WHERE account_id=@a AND consumed_at_utc IS NULL", ("a", actor));
        return new(true, new { accepted = true });
    }
}
