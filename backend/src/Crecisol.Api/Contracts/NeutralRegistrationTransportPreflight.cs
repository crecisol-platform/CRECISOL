using Crecisol.Api.Configuration;
using Microsoft.AspNetCore.Http;

namespace Crecisol.Api.Contracts;

/// <summary>
/// Transport preflight ONLY for a future isolated registration-intent route.
/// Success does not prove email possession, account, tenant membership, or
/// authorization; no identity route is wired by this class.
/// </summary>
public static class NeutralRegistrationTransportPreflight
{
    public const long MaxJsonBytes = 4096;

    public static bool IsEligible(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        // No proxy trust policy or tenant resolution is available at this layer.
        if (!StrictVerifiedHostTransport.TryReadDirectHost(request, out _))
            return false;

        if (!HttpMethods.IsPost(request.Method) ||
            request.QueryString.HasValue ||
            !string.Equals(request.ContentType, "application/json", StringComparison.OrdinalIgnoreCase) ||
            request.ContentLength is null or <= 0 or > MaxJsonBytes)
            return false;

        // Only a bounded JSON body and the direct Host may carry data.
        // Never accept an authority-bearing selector or method override here.
        foreach (var name in request.Headers.Keys)
        {
            if (name.Equals("Authorization", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Cookie", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("X-Tenant-Id", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("X-Actor-Id", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("X-Role", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("X-HTTP-Method-Override", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("X-Original-Method", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }
}
