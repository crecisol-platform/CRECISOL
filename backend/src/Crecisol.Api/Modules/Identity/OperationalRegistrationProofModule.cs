using System.Buffers;
using System.Security.Cryptography;
using Crecisol.Api.Configuration;
using Crecisol.Api.Contracts;
using Crecisol.Application.Identity;
using Crecisol.Application.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Crecisol.Api.Modules.Identity;

/// <summary>
/// Host-scoped, one-use registration mailbox confirmation. This proves only
/// control of a mailbox; it does not create accounts or grant membership.
/// </summary>
public static class OperationalRegistrationProofModule
{
    public const string RoutePattern = "/api/v1/identity/registration-proofs/confirm";

    public static RouteHandlerBuilder MapOperationalRegistrationProofConfirmation(
        this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        _ = endpoints.ServiceProvider.GetRequiredService<ITrustedRegistrationMailboxProofLedger>();
        return endpoints.MapPost(RoutePattern, async (
            HttpContext context,
            HttpTrustedRegistrationRequest directRequest,
            ResolveVerifiedTenantHost verifiedHostResolver,
            ITrustedRegistrationMailboxProofLedger ledger,
            CancellationToken cancellationToken) =>
        {
            SetPrivateHeaders(context.Response);
            if (context.Request.ContentLength is null or <= 0 or > RegistrationMailboxProofPayload.MaxBytes ||
                !NeutralRegistrationTransportPreflight.IsEligible(context.Request))
                return Results.Accepted(value: new { accepted = true });

            byte[]? rented = null;
            byte[]? token = null;
            try
            {
                rented = ArrayPool<byte>.Shared.Rent(checked((int)context.Request.ContentLength.Value));
                var total = 0;
                while (total < context.Request.ContentLength.Value)
                {
                    var read = await context.Request.Body.ReadAsync(
                        rented.AsMemory(total, checked((int)context.Request.ContentLength.Value) - total),
                        cancellationToken);
                    if (read == 0) return Results.Accepted(value: new { accepted = true });
                    total += read;
                }

                if (!RegistrationMailboxProofPayload.TryParse(
                        rented.AsMemory(0, total), out var payload) || payload is null)
                    return Results.Accepted(value: new { accepted = true });

                var directHost = await directRequest.ReadCurrentDirectHostAsync(cancellationToken);
                if (directHost is null) return Results.Accepted(value: new { accepted = true });
                var verifiedHost = await verifiedHostResolver.ResolveAsync(directHost, cancellationToken);
                token = Convert.FromHexString(payload.Code);
                _ = await ledger.TryConfirmOnceAsync(
                    verifiedHost.TenantId, payload.ProofId, token, cancellationToken);
                return Results.Accepted(value: new { accepted = true });
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                // Keep proof/account/tenant existence indistinguishable to callers.
                return Results.Accepted(value: new { accepted = true });
            }
            finally
            {
                if (token is not null) CryptographicOperations.ZeroMemory(token);
                if (rented is not null)
                {
                    Array.Clear(rented);
                    ArrayPool<byte>.Shared.Return(rented);
                }
            }
        })
        .AllowAnonymous()
        .WithName("ConfirmNeutralRegistrationMailboxProof");
    }

    private static void SetPrivateHeaders(HttpResponse response)
    {
        response.Headers.CacheControl = "no-store";
        response.Headers.Pragma = "no-cache";
        response.Headers["X-Content-Type-Options"] = "nosniff";
        response.Headers["Referrer-Policy"] = "no-referrer";
    }
}
