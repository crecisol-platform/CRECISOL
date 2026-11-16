using System.Security.Cryptography;
using System.Text;
using Crecisol.Application.Identity;

namespace Crecisol.Api.Modules.Identity;

/// <summary>Isolated browser transport. The legacy explicit-bearer transport
/// remains for non-browser contract clients and never accepts ambient cookies.</summary>
internal sealed class OperationalBrowserSessionTransport(byte[] key, string cookieName = "__Host-crecisol-c05") : IDisposable
{
    public const string CookieName = "__Host-crecisol-c05";
    public static bool IsBrowser(HttpRequest request) => request.Headers["X-Crecisol-Browser"] == "1";
    public static bool IsLocalOrigin(string origin) => origin is
        "http://localhost:4200" or "http://127.0.0.1:4200" or
        "http://localhost:4201" or "http://127.0.0.1:4201";

    public bool TryRead(HttpContext context, string operation, out string? session)
    {
        session = null;
        var request = context.Request;
        if (!IsLocalOrigin(request.Headers.Origin.ToString()) || request.Headers.ContainsKey("Authorization")) return false;
        if (!request.Cookies.TryGetValue(cookieName, out var value)) return true;
        if (value.Length != 64 || !value.All(Uri.IsHexDigit)) return false;
        session = value;
        // Bootstrap returns only same-origin CSRF material after server session
        // validation. This custom-header POST cannot be forged by a form or
        // cross-origin JS; no CORS is enabled and the Origin must match exactly.
        if (operation is "browser/restore" or "platform/restore") return true;
        var csrf = request.Headers["X-Crecisol-CSRF"].ToString();
        return csrf.Length == 64 && csrf.All(Uri.IsHexDigit) &&
            CryptographicOperations.FixedTimeEquals(Convert.FromHexString(csrf), Convert.FromHexString(Csrf(value)));
    }

    public IResult Receipt(HttpContext context, string operation, OperationalIdentityResult result, string? session)
    {
        if (result.Value is OperationalSessionReceipt issued && result.Allowed)
        {
            context.Response.Cookies.Append(cookieName, issued.SessionToken, new CookieOptions
            {
                Secure = true, HttpOnly = true, SameSite = SameSiteMode.Strict,
                Path = "/", Expires = issued.ExpiresAtUtc, IsEssential = true
            });
            return Results.Ok(new { csrfToken = Csrf(issued.SessionToken), issued.ExpiresAtUtc, issued.RecoveryCodes });
        }
        if (operation is "browser/restore" or "platform/restore")
        {
            if (result.Allowed && session is not null && result.Value is IOperationalSessionWindow window)
                return Results.Ok(new { csrfToken = Csrf(session), window.ExpiresAtUtc });
            Clear(context);
            return Results.Unauthorized();
        }
        if ((operation is "sessions" or "platform/session" or "platform/sessions" && !result.Allowed) || (result.Allowed && operation is
            "logout" or "platform/logout" or "platform/sessions/revoke-all" or "platform/password/change" or "platform/factor/confirm" or "platform/mailbox/complete" or "password/change" or "mailbox/complete" or "factor/replace" or "passkeys/revoke" or "sessions/revoke-all" or "factor/recover-passkey")) Clear(context);
        return result.Allowed ? Results.Ok(result.Value ?? new { accepted = true }) : Results.Unauthorized();
    }

    internal void Clear(HttpContext context) => context.Response.Cookies.Delete(cookieName,
        new CookieOptions { Secure = true, HttpOnly = true, SameSite = SameSiteMode.Strict, Path = "/" });
    private string Csrf(string session) => Convert.ToHexString(HMACSHA256.HashData(key,
        Encoding.ASCII.GetBytes("C05/browser/csrf:" + cookieName + ":" + session)));
    public void Dispose() => CryptographicOperations.ZeroMemory(key);
}

internal sealed record OperationalPlatformBrowserTransport(OperationalBrowserSessionTransport Value) : IDisposable
{
    public void Dispose() => Value.Dispose();
}
