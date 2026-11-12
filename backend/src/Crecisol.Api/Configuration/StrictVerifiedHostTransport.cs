using Crecisol.Domain.Tenancy;
using Microsoft.AspNetCore.Http;

namespace Crecisol.Api.Configuration;

/// <summary>
/// C04-04 isolated ASP.NET request boundary for a DIRECT Host only.
/// Not registered in Program.cs: existing public branding host controls remain
/// unchanged. A trusted reverse proxy policy has NOT been established.
/// No client-supplied forwarding fields may choose a tenant or storage.
/// </summary>
public static class StrictVerifiedHostTransport
{
    public static bool TryReadDirectHost(
        HttpRequest request,
        out CanonicalTenantHost? canonicalHost)
    {
        ArgumentNullException.ThrowIfNull(request);
        canonicalHost = null;

        // Reject ALL X-Forwarded-* rather than maintaining an incomplete
        // allowlist. RFC 7239 Forwarded and legacy rewrites are not trusted.
        if (request.Headers.Keys.Any(header =>
                header.Equals("Forwarded", StringComparison.OrdinalIgnoreCase) ||
                header.StartsWith("X-Forwarded-", StringComparison.OrdinalIgnoreCase) ||
                header.Equals("X-Original-Host", StringComparison.OrdinalIgnoreCase) ||
                header.Equals("X-Original-URL", StringComparison.OrdinalIgnoreCase) ||
                header.Equals("X-Rewrite-URL", StringComparison.OrdinalIgnoreCase)))
            return false;

        var values = request.Headers.Host;
        if (values.Count != 1 || string.IsNullOrEmpty(values[0]))
            return false;

        try
        {
            canonicalHost = CanonicalTenantHost.Parse(values[0]);
            return true;
        }
        catch (InvalidOperationException)
        {
            // No header, DNS syntax or malicious host is surfaced to clients.
            return false;
        }
    }
}
