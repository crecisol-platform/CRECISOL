using Crecisol.Application.Branding;
using Crecisol.Infrastructure.Branding;
using Microsoft.Extensions.DependencyInjection;

namespace Crecisol.Api.Modules.Branding;

/// <summary>
/// The public Branding vertical slice is composed explicitly at the API boundary.
/// Only one published PLATFORM read port and its scoped use case are exposed.
/// No assembly scanning, dynamic resolver, HTTP-driven connection selection or
/// unpublished/administrative revision service is registered here.
/// </summary>
public static class PublicBrandingModule
{
    public static IServiceCollection AddPublicBrandingModule(
        this IServiceCollection services, string? trustedServerDsn)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Fail closed instead of silently taking a previously registered
        // implementation (or allowing a second module to overwrite our port).
        if (services.Any(descriptor =>
            descriptor.ServiceType == typeof(IPublishedPlatformBrandReader) ||
            descriptor.ServiceType == typeof(ReadPublishedPlatformBrand)))
            throw new InvalidOperationException("Public Branding module is already registered.");

        // Trusted composition-root input only. The adapter checks missing DSN
        // on use and the public API returns a generic 503 (never a fallback).
        services.AddSingleton<IPublishedPlatformBrandReader>(
            new MySqlPublishedPlatformBrandReader(trustedServerDsn));
        services.AddScoped<ReadPublishedPlatformBrand>();
        return services;
    }
}
