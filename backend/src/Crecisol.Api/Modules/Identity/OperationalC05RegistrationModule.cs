using Crecisol.Api.Configuration;
using Crecisol.Api.Contracts;
using Crecisol.Application.Identity;
using Crecisol.Application.Tenancy;
using Crecisol.Infrastructure.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Crecisol.Api.Modules.Identity;

/// <summary>
/// Explicit product composition for the C05 neutral registration path.
/// Nothing is activated unless the operator opts in with server-only settings.
/// All secrets are independent 256-bit environment values; no defaults exist.
/// </summary>
public static class OperationalC05RegistrationModule
{
    public static IServiceCollection AddOperationalC05Registration(
        this IServiceCollection services, string? trustedDsn,
        Func<string, string?>? readSetting = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (string.IsNullOrWhiteSpace(trustedDsn))
            throw new InvalidOperationException("C05 identity storage is not configured.");
        readSetting ??= Environment.GetEnvironmentVariable;
        var keyVersionText = readSetting("CRECISOL_C05_KEY_VERSION");
        if (!int.TryParse(keyVersionText, out var keyVersion) || keyVersion < 1)
            throw new InvalidOperationException("C05 key version is not configured.");

        var mailboxKey = ReadKey(readSetting, "CRECISOL_C05_MAILBOX_HMAC_KEY");
        var peerKey = ReadKey(readSetting, "CRECISOL_C05_PEER_HMAC_KEY");
        var encryptionKey = ReadKey(readSetting, "CRECISOL_C05_MAILBOX_ENCRYPTION_KEY");
        if (services.Any(d => d.ServiceType == typeof(NeutralRegistrationIntake)))
            throw new InvalidOperationException("C05 registration is already composed.");

        services.AddHttpContextAccessor();
        services.AddSingleton<HttpTrustedRegistrationRequest>();
        services.AddSingleton<ITrustedCurrentDirectHostReader>(sp =>
            sp.GetRequiredService<HttpTrustedRegistrationRequest>());
        services.AddSingleton<ITrustedRegistrationNetworkPeer>(sp =>
            sp.GetRequiredService<HttpTrustedRegistrationRequest>());
        services.AddSingleton<SystemHostUtcClock>();
        services.AddSingleton<ITrustedHostUtcClock>(sp => sp.GetRequiredService<SystemHostUtcClock>());
        services.AddSingleton<MySqlOperationalIdentityCatalog>(_ =>
            new MySqlOperationalIdentityCatalog(trustedDsn));
        services.AddSingleton<ITrustedTenantCatalogReader>(sp =>
            sp.GetRequiredService<MySqlOperationalIdentityCatalog>());
        services.AddSingleton<ITrustedVerifiedHostCatalog>(sp =>
            sp.GetRequiredService<MySqlOperationalIdentityCatalog>());
        services.AddSingleton<ResolveVerifiedTenantHost>();
        var recorder = new MySqlOperationalNeutralRegistrationRecorder(
            trustedDsn, keyVersion, mailboxKey, peerKey, encryptionKey);
        System.Security.Cryptography.CryptographicOperations.ZeroMemory(mailboxKey);
        System.Security.Cryptography.CryptographicOperations.ZeroMemory(peerKey);
        System.Security.Cryptography.CryptographicOperations.ZeroMemory(encryptionKey);
        services.AddSingleton<ITrustedNeutralRegistrationIntentRecorder>(recorder);
        services.AddScoped<RequestNeutralRegistrationIntent>();
        services.AddScoped<INeutralRegistrationIntentHandler,
            ApplicationNeutralRegistrationIntentHandler>();
        services.AddScoped<NeutralRegistrationIntake>();
        return services;
    }

    private static byte[] ReadKey(Func<string, string?> readSetting, string name)
    {
        var value = readSetting(name);
        byte[]? bytes = null;
        try
        {
            bytes = string.IsNullOrWhiteSpace(value) ? null : Convert.FromHexString(value);
            if (bytes is null || bytes.Length != 32)
                throw new InvalidOperationException("C05 cryptographic configuration is invalid.");
            return bytes;
        }
        catch (FormatException)
        {
            if (bytes is not null) System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes);
            throw new InvalidOperationException("C05 cryptographic configuration is invalid.");
        }
    }
}

