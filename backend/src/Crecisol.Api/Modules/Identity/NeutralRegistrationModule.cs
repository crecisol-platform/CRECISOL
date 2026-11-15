using System.Buffers;
using Crecisol.Api.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Crecisol.Api.Modules.Identity;

/// <summary>
/// Neutral registration transport. The module exposes no account lookup,
/// tenant selector, password, role, invitation decision, or session.
/// Production composition must provide the trusted Application handler.
/// </summary>
public static class NeutralRegistrationModule
{
    public const string RoutePattern = "/api/v1/identity/registration-intents";
    private const int MaxBodyBytes = NeutralRegistrationPayload.MaxBytes;

    public static RouteHandlerBuilder MapNeutralRegistrationIntent(
        this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        _ = endpoints.ServiceProvider.GetRequiredService<NeutralRegistrationIntake>();

        return endpoints.MapPost(RoutePattern, async (
            HttpContext context,
            NeutralRegistrationIntake intake,
            CancellationToken cancellationToken) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers.Pragma = "no-cache";
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";

            byte[]? rented = null;
            try
            {
                var length = context.Request.ContentLength;
                if (length is null or <= 0 or > MaxBodyBytes ||
                    !NeutralRegistrationTransportPreflight.IsEligible(context.Request))
                    return Results.Accepted(value: new { accepted = true });

                rented = ArrayPool<byte>.Shared.Rent(checked((int)length.Value));
                var total = 0;
                while (total < length.Value)
                {
                    var read = await context.Request.Body.ReadAsync(
                        rented.AsMemory(total, checked((int)length.Value) - total),
                        cancellationToken);
                    if (read == 0) break;
                    total += read;
                }

                _ = await intake.SubmitAsync(context.Request,
                    rented.AsMemory(0, total), cancellationToken);
                return Results.Accepted(value: new { accepted = true });
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                // Neutral receipts suppress tenant, mailbox, account and SQL state.
                return Results.Accepted(value: new { accepted = true });
            }
            finally
            {
                if (rented is not null)
                {
                    Array.Clear(rented);
                    ArrayPool<byte>.Shared.Return(rented);
                }
            }
        })
        .AllowAnonymous()
        .WithName("RequestNeutralIdentityRegistration");
    }
}

