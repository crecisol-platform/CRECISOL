using Crecisol.Domain.Branding;

namespace Crecisol.Application.Branding;

public interface IPlatformBrandSource
{
    ValueTask<BrandSnapshot> ReadPublishedAsync(CancellationToken cancellationToken);
}

public sealed record PublicPlatformBrand(
    long Revision,
    string Name,
    string Slogan,
    string Descriptor,
    string PrimaryColor,
    string AccentColor,
    string LogoUrl,
    string CopyrightText);

/// <summary>Explicit read-only public projection. Never exposes tenant config or draft data.</summary>
public sealed class ReadPublishedPlatformBrand(
    IPlatformBrandSource source,
    TimeProvider clock)
{
    public async ValueTask<PublicPlatformBrand> ExecuteAsync(CancellationToken ct)
    {
        var snapshot = (await source.ReadPublishedAsync(ct)).RequirePublicPlatform();
        return new PublicPlatformBrand(
            snapshot.Revision,
            snapshot.Name,
            snapshot.Slogan,
            snapshot.Descriptor,
            snapshot.PrimaryColor,
            snapshot.AccentColor,
            snapshot.LogoUrl,
            $"© {clock.GetUtcNow().Year} {snapshot.Name}");
    }
}
