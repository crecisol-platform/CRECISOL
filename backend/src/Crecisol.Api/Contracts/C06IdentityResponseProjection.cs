using System.Collections;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Crecisol.Application.Identity;

namespace Crecisol.Api.Contracts;

/// <summary>Closed identity HTTP response contracts. Authorization remains
/// exclusively in the underlying use case. Never serialize a raw service object.</summary>
public static class C06IdentityResponseProjection
{
    private delegate object? Projection(object? value);
    private sealed record Field(string Name, Projection Read, bool Optional = false);
    private static readonly IReadOnlyDictionary<string, Projection> Rules = Build();
    public static IReadOnlyCollection<string> Operations => Rules.Keys.ToArray();

    public static bool TryApply(string operation, OperationalIdentityResult result, out OperationalIdentityResult safe)
    {
        safe = new(false);
        if (!Rules.TryGetValue(operation, out var rule)) return false;
        if (!result.Allowed) return true;
        try
        {
            var value = rule(result.Value);
            if (result.SessionIdleMinutes is < 5 or > 120) return false;
            safe = new(true, value)
            {
                SessionExpiresAtUtc = result.SessionExpiresAtUtc,
                SessionIdleMinutes = result.SessionIdleMinutes
            };
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or TargetInvocationException or OverflowException)
        {
            // Never log rejected field values or exception details.
            return false;
        }
    }

    public static OperationalAssistedReceipt NeutralAssisted(OperationalIdentityResult result) =>
        result.Allowed && result.Value is OperationalAssistedReceipt { Accepted: true } receipt && receipt.Reference != Guid.Empty
            ? new(receipt.Reference) : new(Guid.NewGuid());

    private static IReadOnlyDictionary<string, Projection> Build()
    {
        Projection text = Text(512), date = Date, id = Identifier, token = Hex(64), number = Integer, boolean = Boolean;
        var backupCodes = Array(Hex(32), 10);
        var ackFields = Object(F("accepted", True));
        Projection ack = v => v is null ? new Dictionary<string, object?> { ["accepted"] = true } : ackFields(v);
        var challenge = Object(F("stepUpRequired", True), F("challenge", token), F("enrollmentRequired", boolean));
        var enroll = Object(F("secret", Base32), F("uri", TotpUri));
        Projection sessionWithCodes = v => Session(v, true);
        Projection sessionOnly = v => Session(v, false);
        var sessionItem = Object(F("id", id), F("issuedAtUtc", date), F("expiresAtUtc", date), F("lastSeenAtUtc", date), F("device", Text(64)), F("factor", Text(32)));
        var sessions = Object(F("expiresAtUtc", date), F("sessions", Array(sessionItem, 4096)));
        var keepalive = Object(F("expiresAtUtc", date), F("idleMinutes", Idle));
        var pendingItem = Object(F("id", id), F("requestedAtUtc", date), F("mailboxHint", MaskedMailbox));
        var pending = Object(F("requests", Array(pendingItem, 100)), F("nextCursor", Nullable(Text(256))));
        var reference = Object(F("reference", text));

        // WebAuthn standard fields only: omit unsupported extensions and library
        // metadata, reject duplicate properties and malformed known values.
        var descriptor = Protocol(F("type", Literal("public-key")), F("id", Base64Url), O("transports", Array(Text(32), 16)));
        var rp = Protocol(F("id", Literal("localhost")), F("name", Literal("CRECISOL local")));
        var user = Protocol(F("id", Base64Url), F("name", Hex(32)), F("displayName", Literal("Cuenta CRECISOL")));
        var parameters = Protocol(F("type", Literal("public-key")), F("alg", number));
        var selection = Protocol(O("authenticatorAttachment", Text(32)), O("residentKey", Text(32)), O("requireResidentKey", boolean), O("userVerification", Text(32)));
        var assertionOptions = Protocol(F("challenge", Base64Url), O("timeout", number), O("rpId", Literal("localhost")),
            O("allowCredentials", Array(descriptor, 8)), O("userVerification", Text(32)), O("hints", Array(Text(32), 16)));
        var creationOptions = Protocol(F("rp", rp), F("user", user), F("challenge", Base64Url), F("pubKeyCredParams", Array(parameters, 32)),
            O("timeout", number), O("excludeCredentials", Array(descriptor, 8)), O("authenticatorSelection", selection), O("attestation", Text(32)),
            O("hints", Array(Text(32), 16)), O("attestationFormats", Array(Text(64), 16)));
        var assertion = Object(F("challenge", token), F("publicKey", assertionOptions));
        var registration = Object(F("challenge", token), F("publicKey", creationOptions));
        var passkeys = Object(F("passkeys", Array(Object(F("id", id), F("createdAtUtc", date)), 8)));
        var terms = Object(F("version", text), F("text", Text(262144)), F("hash", Hex(64)));
        var pairing = Object(F("token", token), F("nonce", Hex(64)), F("fingerprint", Hex(12)), F("expiresAtUtc", date));
        var events = Object(F("events", Array(Object(F("action", Text(96)), F("outcome", number), F("atUtc", date)), 50)));
        var platformLogin = Object(F("challenge", token), F("stepUpRequired", True));
        var platformSession = Object(F("expiresAtUtc", date), F("realm", Literal("PLATFORM_IDENTITY")));
        var platformSessions = Object(F("sessions", Array(Object(F("id", id), F("issuedAtUtc", date), F("expiresAtUtc", date), F("lastSeenAtUtc", date), F("device", Text(64))), 4096)));
        var sessionPolicy = Object(F("idleMinutes", Idle), F("revision", number), F("canManage", boolean));
        var tenantCase = Object(F("caseId", id), F("createdAtUtc", date), F("readyAtUtc", Nullable(date)), F("expiresAtUtc", date), F("blocked", boolean), F("consumed", boolean));
        var operatorCase = Object(F("caseId", id), F("realm", Literal("PLATFORM_OPERATOR")), F("readyAtUtc", Nullable(date)), F("blocked", boolean), F("consumed", boolean));
        Projection caseReceipt = v => HasField(v, "realm") ? operatorCase(v) : tenantCase(v);
        var caseId = Object(F("caseId", id));
        var signals = Object(F("signals", Array(Object(F("kind", Text(32)), F("count", number)), 32)));
        var profile = Object(F("displayName", Text(80)), F("language", Text(16)), F("timeZone", Text(64)), F("hasPhoto", boolean));
        var photo = Object(F("png", Text(32768)), F("mime", Literal("image/png")), F("width", LiteralNumber(64)), F("height", LiteralNumber(64)));
        var share = Object(F("share", token), F("expiresAtUtc", date));
        var platformFactor = Object(F("challenge", token), F("secret", Base32), F("uri", TotpUri));
        var codes = Object(F("accepted", True), F("recoveryCodes", backupCodes));
        var assisted = Object(F("reference", id), F("accepted", True));
        var operatorEnrollment = Object(F("challenge", token), F("secret", Base32), F("uri", TotpUri), F("enrollmentRequired", True));
        var exceptionOpen = Object(F("exceptionId", id), F("state", Literal("pending")), F("expiresAtUtc", date));
        var exceptionApprove = Object(F("state", Literal("approved")));
        var exceptionClose = Object(F("state", Literal("closed")));
        return new Dictionary<string, Projection>(StringComparer.Ordinal)
        {
            ["logout"] = ack,
            ["sessions/revoke"] = ack,
            ["recovery/request"] = ack,
            ["recovery/complete"] = ack,
            ["password/change"] = ack,
            ["mailbox/request"] = ack,
            ["mailbox/complete"] = ack,
            ["memberships/decide"] = ack,
            ["memberships/request"] = ack,
            ["passkeys/register"] = ack,
            ["passkeys/revoke"] = ack,
            ["terms/accept"] = ack,
            ["invitations/issue"] = ack,
            ["invitations/accept"] = ack,
            ["sessions/revoke-all"] = ack,
            ["pairings/approve"] = ack,
            ["incidents/report"] = ack,
            ["platform/logout"] = ack,
            ["platform/sessions/revoke"] = ack,
            ["platform/sessions/revoke-all"] = ack,
            ["platform/profile/save"] = ack,
            ["platform/profile/photo-upload"] = ack,
            ["platform/profile/photo-remove"] = ack,
            ["platform/password/change"] = ack,
            ["platform/mailbox/request"] = ack,
            ["platform/mailbox/complete"] = ack,
            ["platform/incidents/report"] = ack,
            ["platform/review"] = ack,
            ["assisted/complete"] = ack,
            ["assisted/handoff-complete"] = ack,
            ["factor/recover-passkey"] = ack,
            ["profile/save"] = ack,
            ["profile/photo-upload"] = ack,
            ["profile/photo-remove"] = ack,
            ["login"] = challenge,
            ["factor/replace"] = challenge,
            ["mfa/enroll"] = enroll,
            ["mfa/verify"] = sessionWithCodes,
            ["sessions/rotate"] = sessionOnly,
            ["passkeys/login"] = sessionOnly,
            ["pairings/complete"] = sessionOnly,
            ["platform/mfa"] = sessionOnly,
            ["platform/sessions/rotate"] = sessionOnly,
            ["sessions"] = sessions,
            ["browser/restore"] = sessions,
            ["sessions/keepalive"] = keepalive,
            ["platform/sessions/keepalive"] = keepalive,
            ["memberships/pending"] = pending,
            ["private/read"] = reference,
            ["platform/breakglass/read"] = reference,
            ["passkeys/options"] = assertion,
            ["passkeys/register-options"] = registration,
            ["passkeys"] = passkeys,
            ["terms"] = terms,
            ["pairings/start"] = pairing,
            ["security/events"] = events,
            ["platform/login"] = platformLogin,
            ["platform/session"] = platformSession,
            ["platform/restore"] = platformSession,
            ["platform/sessions"] = platformSessions,
            ["platform/session-policy"] = sessionPolicy,
            ["platform/session-policy/update"] = sessionPolicy,
            ["platform/case"] = caseReceipt,
            ["platform/cases/find"] = caseId,
            ["platform/abuse-summary"] = signals,
            ["profile/read"] = profile,
            ["platform/profile/read"] = profile,
            ["profile/photo"] = photo,
            ["platform/profile/photo"] = photo,
            ["platform/release-share"] = share,
            ["platform/factor/replace"] = platformFactor,
            ["platform/factor/confirm"] = codes,
            ["platform/operator-recovery/enroll"] = codes,
            ["assisted/request"] = assisted,
            ["platform/operator-recovery/request"] = assisted,
            ["platform/operator-recovery/complete"] = operatorEnrollment,
            ["platform/breakglass/open"] = exceptionOpen,
            ["platform/breakglass/approve"] = exceptionApprove,
            ["platform/breakglass/close"] = exceptionClose,
        };
    }

    private static InvalidOperationException Invalid() => new("Identity response contract violation.");
    private static Field F(string name, Projection read) => new(name, read);
    private static Field O(string name, Projection read) => new(name, read, true);
    private static Projection Object(params Field[] fields) => v => ReadObject(v, fields, false);
    private static Projection Protocol(params Field[] fields) => v => ReadObject(v, fields, true);
    private static Projection Nullable(Projection read) => v => v is null or DBNull ? null : read(v);
    private static Projection Literal(string expected) => v => (string)Text(512)(v)! == expected ? expected : throw Invalid();
    private static Projection LiteralNumber(long expected) => v => (long)Integer(v)! == expected ? expected : throw Invalid();
    private static bool Approved(Type type) =>
        type == typeof(OperationalSessionInventory) || type == typeof(OperationalSessionSummary) ||
        type == typeof(OperationalPlatformValidation) || type == typeof(OperationalAssistedReceipt) ||
        (type.IsSealed && type.IsDefined(typeof(CompilerGeneratedAttribute), false) &&
            type.Name.Contains("AnonymousType", StringComparison.Ordinal) && type.Namespace is null);

    private static Dictionary<string, PropertyInfo> Properties(object? value)
    {
        if (value is null || !Approved(value.GetType())) throw Invalid();
        return value.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ToDictionary(p => JsonNamingPolicy.CamelCase.ConvertName(p.Name), StringComparer.Ordinal);
    }

    private static bool HasField(object? value, string name) => Properties(value).ContainsKey(name);
    private static object ReadObject(object? value, Field[] fields, bool protocol)
    {
        var output = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (protocol)
        {
            if (value is not JsonElement { ValueKind: JsonValueKind.Object } json) throw Invalid();
            var properties = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (var p in json.EnumerateObject())
                if (properties.Count >= 64 || !properties.TryAdd(p.Name, p.Value)) throw Invalid();
            foreach (var field in fields)
            {
                if (!properties.TryGetValue(field.Name, out var item) || item.ValueKind == JsonValueKind.Null)
                {
                    if (!field.Optional) throw Invalid();
                    continue;
                }
                output.Add(field.Name, field.Read(item));
            }
            return output;
        }
        var members = Properties(value);
        // Validate shape before invoking any getter. Do not read arbitrary DTOs.
        if (members.Count != fields.Length || members.Keys.Any(k => !fields.Any(f => f.Name == k))) throw Invalid();
        foreach (var field in fields)
        {
            if (!members.TryGetValue(field.Name, out var p) || p.GetIndexParameters().Length != 0) throw Invalid();
            output.Add(field.Name, field.Read(p.GetValue(value)));
        }
        return output;
    }
    private static Projection Array(Projection element, int maximum) => value =>
    {
        IEnumerable sequence;
        if (value is JsonElement { ValueKind: JsonValueKind.Array } json)
        {
            if (json.GetArrayLength() > maximum) throw Invalid();
            sequence = json.EnumerateArray().ToArray();
        }
        else if (value is IEnumerable items && value is not string &&
            (value.GetType().IsArray || value.GetType().Assembly == typeof(Enumerable).Assembly ||
             (value.GetType().IsGenericType && value.GetType().GetGenericTypeDefinition() == typeof(List<>))))
            sequence = items;
        else throw Invalid();
        var result = new List<object?>();
        foreach (var item in sequence)
        {
            if (result.Count >= maximum) throw Invalid();
            result.Add(element(item));
        }
        return result.ToArray();
    };
    private static Projection Text(int maximum) => value =>
    {
        var text = value switch { string s => s, JsonElement { ValueKind: JsonValueKind.String } j => j.GetString(), _ => null };
        return text is not null && text.Length <= maximum ? text : throw Invalid();
    };
    private static object? Boolean(object? value) => value switch
    {
        bool b => b,
        JsonElement { ValueKind: JsonValueKind.True } => true,
        JsonElement { ValueKind: JsonValueKind.False } => false,
        _ => throw Invalid()
    };
    private static object? True(object? value) => Boolean(value) is true ? true : throw Invalid();
    private static object? Integer(object? value) => value switch
    {
        byte v => (long)v, sbyte v => (long)v, short v => (long)v, ushort v => (long)v,
        int v => (long)v, uint v => (long)v, long v => v,
        ulong v when v <= long.MaxValue => (long)v,
        JsonElement { ValueKind: JsonValueKind.Number } j when j.TryGetInt64(out var n) => n,
        _ => throw Invalid()
    };
    private static object? Idle(object? value) => Integer(value) is long n && n is >= 5 and <= 120 ? n : throw Invalid();
    private static object? Date(object? value) => value is DateTime or DateTimeOffset ? value : throw Invalid();
    private static object? Identifier(object? value) =>
        value is Guid g && g != Guid.Empty ? g :
        value is string s && Guid.TryParseExact(s, "D", out var id) && id != Guid.Empty ? s : throw Invalid();
    private static Projection Hex(int length) => value =>
    {
        var s = (string)Text(length)(value)!;
        return s.Length == length && s.All(Uri.IsHexDigit) ? s : throw Invalid();
    };
    private static object? Base32(object? value)
    {
        var s = (string)Text(64)(value)!;
        return s.Length is >= 16 and <= 64 && s.All(c => c is >= 'A' and <= 'Z' or >= '2' and <= '7') ? s : throw Invalid();
    }
    private static object? TotpUri(object? value)
    {
        var s = (string)Text(4096)(value)!;
        return s.StartsWith("otpauth://totp/", StringComparison.Ordinal) ? s : throw Invalid();
    }
    private static object? Base64Url(object? value)
    {
        var s = (string)Text(2048)(value)!;
        return s.Length > 0 && s.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '=') ? s : throw Invalid();
    }
    private static object? MaskedMailbox(object? value)
    {
        var s = (string)Text(260)(value)!;
        if (s == "Correo verificado; identidad no disponible") return s;
        var at = s.IndexOf("•••@", StringComparison.Ordinal);
        return at is 1 or 2 && s.IndexOf('@') == at + 3 && s.LastIndexOf('@') == at + 3 && s.Length > at + 4 ? s : throw Invalid();
    }
    private static object Session(object? value, bool mayIncludeBackups)
    {
        if (value is not OperationalSessionReceipt receipt) throw Invalid();
        _ = Hex(64)(receipt.SessionToken);
        if (receipt.RecoveryCodes is not null && !mayIncludeBackups) throw Invalid();
        string[]? codes = null;
        if (receipt.RecoveryCodes is not null)
        {
            var projected = (object?[])Array(Hex(32), 10)(receipt.RecoveryCodes)!;
            if (projected.Length != 10) throw Invalid();
            codes = projected.Cast<string>().ToArray();
        }
        return new OperationalSessionReceipt(receipt.SessionToken, receipt.ExpiresAtUtc, codes);
    }
}
