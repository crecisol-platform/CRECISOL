using System.Text;
using System.Text.Json;
using Crecisol.Application.Identity;

namespace Crecisol.Infrastructure.Identity;

public sealed partial class MySqlOperationalIdentityFlow
{
    private async Task<OperationalIdentityResult> PlatformMailbox(Unit u, Dictionary<string, object> person, byte[] sessionHash,
        OperationalIdentityRequest r, CancellationToken ct)
    {
        var actor = Bytes(person, "operator_id");
        if (!await PlatformQuota("mailbox:" + Hex(actor), 5, ct) || !await PlatformTotp(u, person, Field(r, "code"))) return new(false);
        if (r.Operation == "platform/mailbox/request")
        {
            var mailbox = CanonicalLoginHandle.Parse(Field(r, "mailbox")).Value;
            if (!mailbox.EndsWith("@example.test", StringComparison.Ordinal)) return new(false);
            var token = Token(); var id = Id();
            await u.Run("INSERT INTO c05_platform_mailbox_change VALUES(@h,@a,@s,@v,@mail,@login,@conflict,UTC_TIMESTAMP(6)+INTERVAL 10 MINUTE,NULL)",
                ("h", Digest("platform-mailbox", token)), ("a", actor), ("s", sessionHash), ("v", Number(person, "security_revision")),
                ("mail", Protect(Encoding.UTF8.GetBytes(mailbox), "platform-contact:" + Hex(actor))),
                ("login", Digest("platform-login", mailbox)), ("conflict", LoginDigest(mailbox)));
            var payload = Protect(JsonSerializer.SerializeToUtf8Bytes(new DeliveryPayload(mailbox, token, "PLATFORM_MAILBOX")), "delivery:" + Hex(id));
            await u.Run("INSERT INTO c05_identity_sensitive_outbox VALUES(@id,@p,UTC_TIMESTAMP(6)+INTERVAL 10 MINUTE,NULL)", ("id", id), ("p", payload));
            await NotifyPlatformSecurity(u, person, "PLATFORM_MAILBOX_REQUESTED");
            return new(true, new { accepted = true });
        }
        var hash = Digest("platform-mailbox", Field(r, "token"));
        var row = await u.One("SELECT * FROM c05_platform_mailbox_change WHERE token_hmac=@h AND operator_id=@a AND session_hmac=@s AND consumed_at_utc IS NULL AND expires_at_utc>UTC_TIMESTAMP(6) FOR UPDATE",
            ("h", hash), ("a", actor), ("s", sessionHash));
        if (row is null || Number(row, "operator_revision") != Number(person, "security_revision")) return new(false);
        await NotifyPlatformSecurity(u, person, "PLATFORM_MAILBOX_CHANGED");
        await u.Run("""
            UPDATE c05_platform_operator SET previous_contact=protected_contact,protected_contact=@mail,contact_changed_at_utc=UTC_TIMESTAMP(6),
            login_hmac=@login,conflict_login_hmac=@conflict,security_revision=security_revision+1 WHERE operator_id=@a
            """, ("a", actor), ("mail", Bytes(row, "protected_contact")), ("login", Bytes(row, "login_hmac")), ("conflict", Bytes(row, "conflict_login_hmac")));
        await RevokePlatformAuth(u, actor);
        var updated = await u.One("SELECT * FROM c05_platform_operator WHERE operator_id=@a", ("a", actor));
        await NotifyPlatformSecurity(u, updated!, "PLATFORM_MAILBOX_CONFIRMED");
        return new(true, new { accepted = true });
    }
}
