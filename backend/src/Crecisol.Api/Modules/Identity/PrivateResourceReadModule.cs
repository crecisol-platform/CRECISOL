using Crecisol.Api.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Crecisol.Api.Modules.Identity;

/// <summary>
/// Reusable C05 private read route. It is deliberately NOT invoked by the
/// product Program.cs until operational identity, verified host catalog,
/// real SQL stores and authenticated browser tests have passed their gates.
/// The disposable Kestrel harness uses this SAME route and DI composition.
/// </summary>
public static class PrivateResourceReadModule
{
    public const string RoutePattern = "/api/v1/private/cases/{resourceId}";

    public static RouteHandlerBuilder MapMfaVerifiedPrivateResourceRead(
        this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        // Fail at startup when real trusted backend dependencies are missing.
        // No anonymous fallback, no default tenant or implicit account.
        _ = endpoints.ServiceProvider.GetRequiredService<PrivateResourceHttpRead>();

        // The sole handler validates a fresh MFA-bound bearer and atomically
        // authorizes backend-owned action, tenant, branch, actor and resource.
        return endpoints.MapGet(
            RoutePattern,
            static (HttpContext context, PrivateResourceHttpRead reader,
                string resourceId) =>
                reader.ExecuteAsync(context, resourceId, context.RequestAborted))
            .WithName("ReadMfaVerifiedPrivateCase");
    }
}
