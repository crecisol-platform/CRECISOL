using System.Text.Json;
using System.Text.Json.Serialization;
using Crecisol.Application.Branding;
using Crecisol.Domain.Branding;

namespace Crecisol.Infrastructure.Branding;

/// <summary>
/// Local C01 bootstrap adapter only. Replace with authorized versioned DB repository
/// before exposing business-specific branding or administrative changes.
/// Path is server-owned and NEVER comes from a request or Host header.
/// </summary>
public sealed class JsonPlatformBrandSource(string absoluteTrustedPath) : IPlatformBrandSource
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    public async ValueTask<BrandSnapshot> ReadPublishedAsync(CancellationToken cancellationToken)
    {
        var json = await File.ReadAllTextAsync(absoluteTrustedPath, cancellationToken);
        var snapshot = JsonSerializer.Deserialize<BrandSnapshot>(json, Options)
            ?? throw new InvalidOperationException("Platform brand is not configured.");
        return snapshot.RequirePublicPlatform();
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
