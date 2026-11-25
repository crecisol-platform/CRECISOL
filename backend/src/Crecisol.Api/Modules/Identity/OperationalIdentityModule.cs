using System.Text.Json;
using System.Security.Cryptography;
using Crecisol.Api.Configuration;
using Crecisol.Api.Contracts;
using Crecisol.Application.Identity;
using Crecisol.CredentialSecurity;
using Crecisol.Infrastructure.Identity;

namespace Crecisol.Api.Modules.Identity;

public static class OperationalIdentityModule
{
    private static readonly IReadOnlyDictionary<string, string[]> Operations = new Dictionary<string, string[]>
    {
        ["login"] = ["mailbox", "password"],
        ["mfa/enroll"] = ["challenge"],
        ["mfa/verify"] = ["challenge", "code"],
        ["sessions"] = [], ["sessions/keepalive"] = [], ["sessions/rotate"] = [], ["logout"] = [],
        ["sessions/revoke"] = ["sessionId"],
        ["recovery/request"] = ["mailbox"],
        ["recovery/complete"] = ["token", "code", "newPassword"],
        ["password/change"] = ["currentPassword", "newPassword", "code"],
        ["factor/replace"] = ["currentPassword", "code"],
        ["mailbox/request"] = ["mailbox", "code"],
        ["mailbox/complete"] = ["token", "code"],
        ["memberships/pending"] = ["cursor"],
        ["memberships/decide"] = ["membershipId", "decision", "reason", "code"],
        ["memberships/request"] = ["mailbox", "password", "code", "proofId", "proofCode", "version", "hash", "accepted"],
        ["private/read"] = ["branchId", "resourceId"]
        , ["passkeys/options"] = [], ["passkeys/login"] = ["challenge", "response"],
        ["passkeys/register-options"] = ["code"],
        ["passkeys/register"] = ["challenge", "response"],
        ["passkeys"] = [], ["passkeys/revoke"] = ["factorId", "code"],
        ["terms"] = [], ["terms/accept"] = ["version", "hash", "accepted"],
        ["invitations/issue"] = ["mailbox", "code"],
        ["invitations/accept"] = ["mailbox", "password", "code", "token", "version", "hash", "accepted"],
        ["browser/restore"] = [], ["sessions/revoke-all"] = ["code"],
        ["pairings/start"] = ["publicKey", "signature"],
        ["pairings/approve"] = ["token", "fingerprint", "confirmed", "code"],
        ["pairings/complete"] = ["token", "signature"],
        ["incidents/report"] = ["token"], ["security/events"] = [],
        ["platform/login"] = ["mailbox", "password"], ["platform/mfa"] = ["challenge", "code"],
        ["platform/session"] = [], ["platform/restore"] = [], ["platform/logout"] = [],
        ["platform/sessions"] = [], ["platform/sessions/keepalive"] = [], ["platform/sessions/rotate"] = [],
        ["platform/session-policy"] = [], ["platform/session-policy/update"] = ["minutes", "expectedRevision", "code"],
        ["platform/sessions/revoke"] = ["sessionId"], ["platform/sessions/revoke-all"] = ["code"],
        ["platform/case"] = ["caseId"],
        ["platform/cases/find"] = ["mailbox", "realm", "reason", "code"],
        ["platform/abuse-summary"] = [],
        ["platform/profile/read"] = [], ["platform/profile/save"] = ["displayName", "language", "timeZone"],
        ["platform/profile/photo"] = [], ["platform/profile/photo-upload"] = ["pixels"], ["platform/profile/photo-remove"] = [],
        ["platform/release-share"] = ["caseId", "code"],
        ["platform/password/change"] = ["currentPassword", "newPassword", "code"],
        ["platform/factor/replace"] = ["currentPassword", "code"],
        ["platform/factor/confirm"] = ["challenge", "code"],
        ["platform/mailbox/request"] = ["mailbox", "code"], ["platform/mailbox/complete"] = ["token", "code"],
        ["platform/operator-recovery/request"] = ["mailbox", "automaticMethodsUnavailable"],
        ["platform/operator-recovery/complete"] = ["caseId", "shareA", "shareB", "newPassword"],
        ["platform/operator-recovery/enroll"] = ["caseId", "token", "code"],
        ["platform/incidents/report"] = ["token"],
        ["platform/review"] = ["caseId", "decision", "identityVerified", "evidenceHash", "reason", "code"],
        ["assisted/request"] = ["mailbox", "automaticMethodsUnavailable"],
        ["assisted/complete"] = ["caseId", "token", "newPassword"],
        ["assisted/handoff-complete"] = ["caseId", "shareA", "shareB", "newPassword"],
        ["factor/recover-passkey"] = ["token", "newPassword"],
        ["profile/read"] = [], ["profile/save"] = ["displayName", "language", "timeZone"],
        ["profile/photo"] = [], ["profile/photo-upload"] = ["pixels"], ["profile/photo-remove"] = []
    };

    public static IServiceCollection AddLocalOperationalIdentity(this IServiceCollection services, string dsn)
    {
        if (Environment.GetEnvironmentVariable("CRECISOL_C05_KEY_VERSION") != "1")
            throw new InvalidOperationException("Operational identity requires key version 1 in this isolated composition.");
        static byte[] Key(string name)
        {
            var bytes = Convert.FromHexString(Environment.GetEnvironmentVariable(name) ?? "");
            if (bytes.Length != 32) throw new InvalidOperationException("C05 operational keys required.");
            return bytes;
        }
        services.AddSingleton<IOperationalIdentityCrypto, OperationalIdentityCrypto>();
        services.AddSingleton<IOperationalPasskeyCrypto, OperationalPasskeyCrypto>();
        services.AddSingleton(_ => new OperationalBrowserSessionTransport(Key("CRECISOL_C05_TOKEN_HMAC_KEY")));
        services.AddSingleton(_ => new OperationalPlatformBrowserTransport(new OperationalBrowserSessionTransport(
            Key("CRECISOL_C05_TOKEN_HMAC_KEY"), "__Host-crecisol-platform-c05")));
        services.AddSingleton(sp =>
        {
            var tokenKey = Key("CRECISOL_C05_TOKEN_HMAC_KEY");
            var encryptionKey = Key("CRECISOL_C05_FACTOR_ENCRYPTION_KEY");
            var loginKey = Key("CRECISOL_C05_LOGIN_LOOKUP_HMAC_KEY");
            var mailboxKey = Key("CRECISOL_C05_MAILBOX_HMAC_KEY");
            try { return new MySqlOperationalIdentityFlow(dsn, tokenKey, encryptionKey, loginKey, sp.GetRequiredService<IOperationalIdentityCrypto>(), sp.GetRequiredService<IOperationalPasskeyCrypto>(), mailboxKey); }
            finally
            {
                CryptographicOperations.ZeroMemory(tokenKey);
                CryptographicOperations.ZeroMemory(encryptionKey);
                CryptographicOperations.ZeroMemory(loginKey);
                CryptographicOperations.ZeroMemory(mailboxKey);
            }
        });
        services.AddSingleton<IOperationalIdentityFlow>(sp => sp.GetRequiredService<MySqlOperationalIdentityFlow>());
        services.AddHostedService<LocalSensitiveProofDelivery>();
        return services;
    }

    // C06-06 is a separate, explicitly enabled local-only surface. The
    // historic Operations dictionary and its contracts are not replaced.
    private static readonly IReadOnlyDictionary<string, string[]> C06BreakglassOperations = new Dictionary<string, string[]>
    {
        ["platform/breakglass/open"] = ["tenantId", "branchId", "resourceId", "reason", "code"],
        ["platform/breakglass/approve"] = ["exceptionId", "tenantId", "branchId", "resourceId", "reason", "reviewReason", "code"],
        ["platform/breakglass/read"] = ["exceptionId", "code"],
        ["platform/breakglass/close"] = ["exceptionId"],
    };

    public static void MapLocalOperationalIdentity(this IEndpointRouteBuilder endpoints)
    {
        // Validate all configuration and crypto before accepting any request.
        _ = endpoints.ServiceProvider.GetRequiredService<IOperationalIdentityFlow>();
        if (!C06IdentityResponseProjection.Operations.ToHashSet(StringComparer.Ordinal).SetEquals(Operations.Keys.Concat(C06BreakglassOperations.Keys)))
            throw new InvalidOperationException("Identity response inventory mismatch.");
        foreach (var operation in Operations.Keys)
        {
            var selected = operation;
            endpoints.MapPost(selected.StartsWith("platform/", StringComparison.Ordinal)
                ? "/api/v1/platform-identity/" + selected[9..] : "/api/v1/identity/" + selected,
                (HttpContext context, IOperationalIdentityFlow flow, CancellationToken ct) => Handle(context, flow, selected, ct));
        }
        if (Environment.GetEnvironmentVariable("CRECISOL_C06_BREAKGLASS_ENABLED") == "true")
            foreach (var operation in C06BreakglassOperations.Keys)
            {
                var selected = operation;
                endpoints.MapPost("/api/v1/platform-identity/" + selected[9..],
                    (HttpContext context, IOperationalIdentityFlow flow, CancellationToken ct) => Handle(context, flow, selected, ct));
            }
    }

    private static async Task<IResult> Handle(HttpContext context, IOperationalIdentityFlow flow, string operation, CancellationToken ct)
    {
        var request = context.Request;
        var exceptional = C06BreakglassOperations.TryGetValue(operation, out var exceptionalFields);
        if (exceptional && Environment.GetEnvironmentVariable("CRECISOL_C06_BREAKGLASS_ENABLED") != "true")
            return Results.NotFound();
        var expectedFields = exceptional ? exceptionalFields! : Operations[operation];
        // All identity responses, including denials and neutral receipts, are private.
        context.Response.Headers.CacheControl = "no-store, private";
        context.Response.Headers.Pragma = "no-cache";
        var browser = OperationalBrowserSessionTransport.IsBrowser(request);
        try
        {
            if (context.Connection.RemoteIpAddress is null || !System.Net.IPAddress.IsLoopback(context.Connection.RemoteIpAddress) ||
                !StrictVerifiedHostTransport.TryReadDirectHost(request, out var host) || host is null ||
                !host.Value.EndsWith(".example.test", StringComparison.Ordinal) ||
                request.QueryString.HasValue || request.ContentLength is null or < 2 or > 32768 ||
                !string.Equals(request.ContentType, "application/json", StringComparison.OrdinalIgnoreCase) ||
                request.Headers.Keys.Any(h => h.StartsWith("X-Tenant", StringComparison.OrdinalIgnoreCase) ||
                    h.StartsWith("X-Actor", StringComparison.OrdinalIgnoreCase) || h.Equals("X-Role", StringComparison.OrdinalIgnoreCase) ||
                    h.Contains("Method", StringComparison.OrdinalIgnoreCase)) ||
                (!browser && request.Headers.ContainsKey("Cookie")) || request.Headers.ContainsKey("Transfer-Encoding") ||
                (request.Headers.TryGetValue("Sec-Fetch-Site", out var site) && site == "cross-site"))
                return Results.BadRequest();
            // No CORS and no ambient credentials. Origin is checked separately
            // from the server-verified host because Angular's local dev proxy
            // uses a loopback origin and a reserved test Host on its backend hop.
            if (request.Headers.TryGetValue("Origin", out var origin) &&
                origin != "http://127.0.0.1:4200" && origin != "http://localhost:4200" &&
                origin != "http://127.0.0.1:4201" && origin != "http://localhost:4201" &&
                origin != "http://" + host.Value && origin != "https://" + host.Value)
                return Results.BadRequest();
            var bearer = request.Headers.Authorization.ToString();
            var browserTransport = operation.StartsWith("platform/", StringComparison.Ordinal)
                ? context.RequestServices.GetRequiredService<OperationalPlatformBrowserTransport>().Value
                : context.RequestServices.GetRequiredService<OperationalBrowserSessionTransport>();
            string? browserSession = null;
            if (browser)
            {
                if (!browserTransport.TryRead(context, operation, out browserSession)) return Results.BadRequest();
                bearer = browserSession is null ? "" : "Bearer " + browserSession;
                // The local cookie is removed even if the database is down or
                // the session expired; server revocation still runs below.
                if (operation is "logout" or "platform/logout") browserTransport.Clear(context);
            }
            else if (operation is "browser/restore" or "platform/restore") return Results.BadRequest();
            if (bearer.Length > 0 && (!bearer.StartsWith("Bearer ", StringComparison.Ordinal) || bearer.Length != 71 || !bearer[7..].All(Uri.IsHexDigit)))
                return Results.BadRequest();
            var bytes = new byte[checked((int)request.ContentLength.Value)];
            Dictionary<string, string> fields;
            try
            {
                await request.Body.ReadExactlyAsync(bytes, ct);
                using var json = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 2 });
                if (json.RootElement.ValueKind != JsonValueKind.Object) return Results.BadRequest();
                fields = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var property in json.RootElement.EnumerateObject())
                    if (property.Value.ValueKind != JsonValueKind.String ||
                        !expectedFields.Contains(property.Name) ||
                        !fields.TryAdd(property.Name, property.Value.GetString()!) ||
                        fields[property.Name].Length < 1 ||
                        fields[property.Name].Length > (property.Name is "response" or "pixels" ? 24000 : 512)) return Results.BadRequest();
                if (fields.Count != expectedFields.Length) return Results.BadRequest();
                foreach (var (name, value) in fields)
                {
                    if ((name is "challenge" or "token") && (value.Length != 64 || !value.All(Uri.IsHexDigit))) return Results.BadRequest();
                    if (name == "code" && !(operation == "invitations/accept" && value == "enroll") && !((value.Length == 6 && value.All(char.IsAsciiDigit)) ||
                        (value.Length == 32 && value.All(Uri.IsHexDigit)))) return Results.BadRequest();
                    if (name.Contains("Password", StringComparison.OrdinalIgnoreCase) && value.Length > 128) return Results.BadRequest();
                    // The review justification is a security/audit contract, not only
                    // an HTML minlength control that an API caller can bypass.
                    if (operation == "memberships/decide" && name == "reason" &&
                        (value.Trim().Length < 8 || value.Length > 240)) return Results.BadRequest();
                    if (name is "minutes" or "expectedRevision" &&
                        (value.Length > 10 || !value.All(char.IsAsciiDigit))) return Results.BadRequest();
                }
            }
            finally { CryptographicOperations.ZeroMemory(bytes); }
            var peer = context.Connection.RemoteIpAddress?.MapToIPv6().ToString();
            if (peer is null) return Results.BadRequest();
            var result = await flow.ExecuteAsync(new(host.Value, peer, operation == "browser/restore" ? "sessions" : operation, fields,
                bearer.Length == 0 ? null : bearer[7..], DeviceLabel(request.Headers.UserAgent.ToString())), ct);
            if (operation is "recovery/request" or "incidents/report" or "memberships/request") return Results.Accepted(value: new { accepted = true });
            if (operation is "assisted/request" or "platform/operator-recovery/request")
                return Results.Accepted(value: C06IdentityResponseProjection.NeutralAssisted(result));
            if (operation == "platform/incidents/report") return Results.Accepted(value: new { accepted = true });
            if (!C06IdentityResponseProjection.TryApply(operation, result, out var safe))
                return Results.Unauthorized();
            if (browser) return browserTransport.Receipt(context, operation, safe, browserSession);
            return safe.Allowed ? Results.Ok(safe.Value) : Results.Unauthorized();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (JsonException) { return Results.BadRequest(); }
        catch (EndOfStreamException) { return Results.BadRequest(); }
        catch (Exception)
        {
            // No exception, SQL, secret, mailbox or private payload enters logs.
            if (operation is "assisted/request" or "platform/operator-recovery/request") return Results.Accepted(value: new OperationalAssistedReceipt(Guid.NewGuid()));
            if (operation == "platform/incidents/report") return Results.Accepted(value: new { accepted = true });
            return operation is "recovery/request" or "incidents/report" or "memberships/request" ? Results.Accepted(value: new { accepted = true }) : Results.Unauthorized();
        }
    }

    private static string DeviceLabel(string agent) => agent.Contains("Chrome/", StringComparison.Ordinal) || agent.Contains("Chromium/", StringComparison.Ordinal)
        ? "Chromium" : agent.Contains("Firefox/", StringComparison.Ordinal) ? "Firefox" : agent.Contains("Safari/", StringComparison.Ordinal) ? "Safari" : "API";
}

internal sealed class LocalSensitiveProofDelivery(MySqlOperationalIdentityFlow flow) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var port = int.Parse(Environment.GetEnvironmentVariable("CRECISOL_C05_LOCAL_SMTP_PORT")!);
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await flow.DispatchOneAsync(port, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) { /* Durable row remains pending; never log its payload. */ }
            await Task.Delay(1000, stoppingToken);
        }
    }
}
