using Crecisol.Application.Identity;
using Crecisol.Application.Tenancy;
using Crecisol.Domain.Tenancy;
using Microsoft.AspNetCore.Http;

namespace Crecisol.Api.Contracts;

/// <summary>
/// Only the backend's trusted, revisioned host catalog may issue this hint.
/// A Host header is a lookup key, NEVER an assertion of a tenant or grant.
/// </summary>
public interface ITrustedPrivateResourceHostCatalog
{
    Task<VerifiedTenantHostHint?> ResolveDirectHostAsync(
        string directHost, CancellationToken cancellationToken);
}

/// <summary>
/// HTTP boundary for C05 private reads. Not registered in the product API.
/// In the C05 disposable test host this composes the real MFA-bound,
/// atomic MySQL authorization with untrusted HTTP input. It must remain
/// disabled in Program.cs until the operational identity graph is certified.
/// </summary>
public sealed class PrivateResourceHttpRead
{
    private readonly ITrustedPrivateResourceHostCatalog hosts;
    private readonly ReadMfaAuthorizedPrivateResource read;

    public PrivateResourceHttpRead(
        ITrustedPrivateResourceHostCatalog hosts,
        ReadMfaAuthorizedPrivateResource read)
    {
        this.hosts = hosts ?? throw new ArgumentNullException(nameof(hosts));
        this.read = read ?? throw new ArgumentNullException(nameof(read));
    }

    public async Task<IResult> ExecuteAsync(
        HttpContext context, string? rawResourceId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Pragma = "no-cache";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";

        var request = context.Request;
        // No cookies, forwarding, browser-selected context, query or body.
        // The browser only presents an opaque bearer and resource selector.
        if (!HttpMethods.IsGet(request.Method) ||
            request.QueryString.HasValue ||
            request.ContentLength is > 0 ||
            request.Headers.ContainsKey("Transfer-Encoding") ||
            request.Headers.ContainsKey("Forwarded") ||
            request.Headers.ContainsKey("Cookie") ||
            request.Headers.ContainsKey("Proxy-Authorization") ||
            request.Headers.ContainsKey("Origin") ||
            request.Headers.Keys.Any(key =>
                key.StartsWith("X-", StringComparison.OrdinalIgnoreCase)) ||
            !request.Headers.TryGetValue("Host", out var directHostValues) ||
            directHostValues.Count != 1 ||
            request.Host.Port is not null ||
            string.IsNullOrWhiteSpace(request.Host.Host) ||
            !Guid.TryParseExact(rawResourceId, "D", out var resourceId) ||
            resourceId == Guid.Empty ||
            !request.Headers.TryGetValue("Authorization", out var authValues) ||
            authValues.Count != 1)
            return Results.NotFound();

        var authorization = authValues[0];
        if (authorization is null ||
            authorization.Length is < 15 or > 128 ||
            !authorization.StartsWith("Bearer ", StringComparison.Ordinal) ||
            authorization[7..].Any(character => char.IsWhiteSpace(character) || character == ','))
            return Results.NotFound();

        try
        {
            var trustedHost = await hosts.ResolveDirectHostAsync(
                request.Host.Host, cancellationToken);
            if (trustedHost is null || trustedHost.Revision < 1)
                return Results.NotFound();
            var reference = await read.ExecuteAsync(
                authorization[7..], trustedHost,
                TenantResourceId.From(resourceId), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return Results.Json(new { externalKey = reference.ExternalKey },
                statusCode: StatusCodes.Status200OK);
        }
        catch (UnauthorizedAccessException)
        {
            // Exactly the same response for absent, foreign, revoked or
            // unauthorized resources. Never reveal actor, tenant or grant.
            return Results.NotFound();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // No SQL detail, bearer, token digest or tenant ID in responses.
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }

    public override string ToString() => "PrivateResourceHttpRead[redacted]";
}
