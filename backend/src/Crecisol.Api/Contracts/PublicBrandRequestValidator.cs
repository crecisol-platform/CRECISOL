using Microsoft.AspNetCore.Http;

namespace Crecisol.Api.Contracts;

/// <summary>
/// A bodyless anonymous GET has no authority-bearing inputs. The caller still
/// must pass the Program.cs host/forwarded-host allowlist and SQL publication gate.
/// </summary>
public static class PublicBrandRequestValidator
{
    public static bool IsValid(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return HttpMethods.IsGet(request.Method)
            && !request.QueryString.HasValue
            && !request.Headers.ContainsKey("Authorization")
            && !request.Headers.ContainsKey("X-Tenant-Id")
            && !request.Headers.ContainsKey("Content-Length")
            && !request.Headers.ContainsKey("Transfer-Encoding")
            && !request.Headers.ContainsKey("X-HTTP-Method-Override")
            && !request.Headers.ContainsKey("X-Original-URL")
            && !request.Headers.ContainsKey("X-Rewrite-URL");
    }
}
