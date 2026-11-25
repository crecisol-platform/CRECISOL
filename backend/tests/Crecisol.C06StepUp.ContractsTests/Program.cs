using Crecisol.Domain.Authorization;

var count = 0;
void Check(bool result, string label)
{
    if (!result) throw new InvalidOperationException("C06-05 FAIL " + label);
    count++;
}
const string validShape = "123456";
const string backupShape = "0123456789abcdef0123456789abcdef";
const string reason = "Revisión de autorización solicitada";
string[] tenant = ["passkeys/register-options", "passkeys/revoke", "invitations/issue", "sessions/revoke-all", "pairings/approve", "factor/replace", "password/change", "mailbox/request", "mailbox/complete", "memberships/decide"];
string[] platform = ["platform/sessions/revoke-all", "platform/cases/find", "platform/password/change", "platform/factor/replace", "platform/mailbox/request", "platform/mailbox/complete", "platform/review", "platform/release-share", "platform/session-policy/update"];
foreach (var (realm, other, actions) in new[] {
    (C06StepUpRealm.Tenant, C06StepUpRealm.Platform, tenant),
    (C06StepUpRealm.Platform, C06StepUpRealm.Tenant, platform) })
{
    foreach (var action in actions)
    {
        Check(C06SensitiveOperationPolicy.RequiresFreshFactor(realm, action), action + " exact operation");
        Check(C06SensitiveOperationPolicy.AcceptsInput(realm, action, validShape, reason), action + " TOTP shape only");
        Check(C06SensitiveOperationPolicy.AcceptsInput(realm, action, backupShape, reason), action + " backup shape only");
        Check(!C06SensitiveOperationPolicy.AcceptsInput(other, action, validShape, reason), action + " wrong realm");
        foreach (var bad in new string?[] { null, "", "12345", "1234567", "12345x", "１２３４５６", "g"+backupShape[1..] })
            Check(!C06SensitiveOperationPolicy.AcceptsInput(realm, action, bad, reason), action + " malformed proof");
        Check(!C06SensitiveOperationPolicy.AcceptsInput(realm, action.ToUpperInvariant(), validShape, reason), action + " case mismatch");
        Check(!C06SensitiveOperationPolicy.AcceptsInput(realm, action + "/extra", validShape, reason), action + " unknown suffix");
        if (C06SensitiveOperationPolicy.RequiresHumanReason(realm, action))
        {
            foreach (var badReason in new string?[] { null, "", "   ", "-------", new string('-', 16), "Causa\ninválida de prueba", new string('X', 513) })
                Check(!C06SensitiveOperationPolicy.AcceptsInput(realm, action, validShape, badReason), action + " invalid reason");
        }
        else Check(C06SensitiveOperationPolicy.AcceptsInput(realm, action, validShape, null), action + " server-defined purpose");
    }
}
foreach (var operation in new string?[] { null, "", "authorization.role.assign", "cases.read", "private/read", "platform/breakglass/open", "passkeys/register", "platform/factor/confirm", "login", "platform/mfa" })
{
    Check(!C06SensitiveOperationPolicy.AcceptsInput(C06StepUpRealm.Tenant, operation, validShape, reason), "unclassified tenant input");
    Check(!C06SensitiveOperationPolicy.AcceptsInput(C06StepUpRealm.Platform, operation, validShape, reason), "unclassified platform input");
}
Check(!C06SensitiveOperationPolicy.AcceptsInput((C06StepUpRealm)99, "password/change", validShape, reason), "unknown realm");
Check(!C06SensitiveOperationPolicy.AcceptsInput(C06StepUpRealm.Tenant, "memberships/decide", validShape, new string('X', 241)), "tenant reason too long");
Check(C06SensitiveOperationPolicy.AcceptsInput(C06StepUpRealm.Tenant, "memberships/decide", validShape, "Motivo 1"), "existing tenant minimum");
Check(!C06SensitiveOperationPolicy.AcceptsInput(C06StepUpRealm.Platform, "platform/review", validShape, "Motivo 1"), "platform reason minimum");
Console.WriteLine($"PASS C06-05 input contracts={count}; no assertion claims cryptographic proof or authorization");
