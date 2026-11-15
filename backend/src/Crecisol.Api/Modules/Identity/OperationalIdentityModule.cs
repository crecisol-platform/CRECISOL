using System.Text.Json;
using System.Security.Cryptography;
using Crecisol.Api.Configuration;
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
        ["sessions"] = [], ["sessions/rotate"] = [], ["logout"] = [],
        ["sessions/revoke"] = ["sessionId"],
        ["recovery/request"] = ["mailbox"],
        ["recovery/complete"] = ["token", "code", "newPassword"],
        ["password/change"] = ["currentPassword", "newPassword", "code"],
        ["factor/replace"] = ["currentPassword", "code"],
        ["mailbox/request"] = ["mailbox", "code"],
        ["mailbox/complete"] = ["token", "code"],
        ["memberships/decide"] = ["membershipId", "decision", "reason", "code"],
        ["private/read"] = ["branchId", "resourceId"]
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
        services.AddSingleton(sp =>
        {
            var tokenKey = Key("CRECISOL_C05_TOKEN_HMAC_KEY");
            var encryptionKey = Key("CRECISOL_C05_FACTOR_ENCRYPTION_KEY");
            var loginKey = Key("CRECISOL_C05_LOGIN_LOOKUP_HMAC_KEY");
            try { return new MySqlOperationalIdentityFlow(dsn, tokenKey, encryptionKey, loginKey, sp.GetRequiredService<IOperationalIdentityCrypto>()); }
            finally
            {
                CryptographicOperations.ZeroMemory(tokenKey);
                CryptographicOperations.ZeroMemory(encryptionKey);
                CryptographicOperations.ZeroMemory(loginKey);
            }
        });
        services.AddSingleton<IOperationalIdentityFlow>(sp => sp.GetRequiredService<MySqlOperationalIdentityFlow>());
        services.AddHostedService<LocalSensitiveProofDelivery>();
        return services;
    }

    public static void MapLocalOperationalIdentity(this IEndpointRouteBuilder endpoints)
    {
        // Validate all configuration and crypto before accepting any request.
        _ = endpoints.ServiceProvider.GetRequiredService<IOperationalIdentityFlow>();
        foreach (var operation in Operations.Keys)
        {
            var selected = operation;
            endpoints.MapPost("/api/v1/identity/" + selected,
                (HttpContext context, IOperationalIdentityFlow flow, CancellationToken ct) => Handle(context, flow, selected, ct));
        }
    }

    private static async Task<IResult> Handle(HttpContext context, IOperationalIdentityFlow flow, string operation, CancellationToken ct)
    {
        var request = context.Request;
        try
        {
            if (context.Connection.RemoteIpAddress is null || !System.Net.IPAddress.IsLoopback(context.Connection.RemoteIpAddress) ||
                !StrictVerifiedHostTransport.TryReadDirectHost(request, out var host) || host is null ||
                !host.Value.EndsWith(".example.test", StringComparison.Ordinal) ||
                request.QueryString.HasValue || request.ContentLength is null or < 2 or > 4096 ||
                !string.Equals(request.ContentType, "application/json", StringComparison.OrdinalIgnoreCase) ||
                request.Headers.Keys.Any(h => h.StartsWith("X-Tenant", StringComparison.OrdinalIgnoreCase) ||
                    h.StartsWith("X-Actor", StringComparison.OrdinalIgnoreCase) || h.Equals("X-Role", StringComparison.OrdinalIgnoreCase) ||
                    h.Contains("Method", StringComparison.OrdinalIgnoreCase)) ||
                request.Headers.ContainsKey("Cookie") || request.Headers.ContainsKey("Transfer-Encoding") ||
                (request.Headers.TryGetValue("Sec-Fetch-Site", out var site) && site == "cross-site"))
                return Results.BadRequest();
            // No CORS and no ambient credentials. Origin is checked separately
            // from the server-verified host because Angular's local dev proxy
            // uses a loopback origin and a reserved test Host on its backend hop.
            if (request.Headers.TryGetValue("Origin", out var origin) &&
                origin != "http://127.0.0.1:4200" && origin != "http://" + host.Value && origin != "https://" + host.Value)
                return Results.BadRequest();
            var bearer = request.Headers.Authorization.ToString();
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
                        !Operations[operation].Contains(property.Name) ||
                        !fields.TryAdd(property.Name, property.Value.GetString()!) ||
                        fields[property.Name].Length is < 1 or > 512) return Results.BadRequest();
                if (fields.Count != Operations[operation].Length) return Results.BadRequest();
                foreach (var (name, value) in fields)
                {
                    if ((name is "challenge" or "token") && (value.Length != 64 || !value.All(Uri.IsHexDigit))) return Results.BadRequest();
                    if (name == "code" && !((value.Length == 6 && value.All(char.IsAsciiDigit)) ||
                        (value.Length == 32 && value.All(Uri.IsHexDigit)))) return Results.BadRequest();
                    if (name.Contains("Password", StringComparison.OrdinalIgnoreCase) && value.Length > 128) return Results.BadRequest();
                }
            }
            finally { CryptographicOperations.ZeroMemory(bytes); }
            var peer = context.Connection.RemoteIpAddress?.MapToIPv6().ToString();
            if (peer is null) return Results.BadRequest();
            var result = await flow.ExecuteAsync(new(host.Value, peer, operation, fields,
                bearer.Length == 0 ? null : bearer[7..]), ct);
            if (operation == "recovery/request") return Results.Accepted(value: new { accepted = true });
            return result.Allowed ? Results.Ok(result.Value ?? new { accepted = true }) : Results.Unauthorized();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (JsonException) { return Results.BadRequest(); }
        catch (EndOfStreamException) { return Results.BadRequest(); }
        catch (Exception)
        {
            // No exception, SQL, secret, mailbox or private payload enters logs.
            return operation == "recovery/request" ? Results.Accepted(value: new { accepted = true }) : Results.Unauthorized();
        }
    }
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
