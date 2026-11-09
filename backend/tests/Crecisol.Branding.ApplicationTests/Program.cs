using Crecisol.Application.Branding;
using Crecisol.Domain.Branding;

var count = 0;
var publishedTime = new DateTimeOffset(2026, 11, 8, 10, 0, 0, TimeSpan.Zero);
var global = new BrandSnapshot(
    BrandScope.Platform, Guid.Empty, 2, BrandPublicationStatus.Published,
    "CRECISOL", "Tu negocio, a tu manera.",
    "Comercio · Servicios · Membresías · Crecimiento",
    "#062E65", "#078FE8", "/assets/brand.png",
    null, publishedTime)
{
    Presentation = new BrandPresentation(
        "/assets/favicon.webp", "system", "outline",
        "CRECISOL institucional", "Identidad institucional publicada.",
        null)
};

var useCase = new ReadPublishedPlatformBrand(new FixedReader(global));
var result = await useCase.ExecuteAsync(CancellationToken.None);
Check(result?.Name == "CRECISOL", "Platform published is mapped");
Check(result?.Revision == 2, "Version included");
Check(result?.PublishedAt == publishedTime, "UTC preserved");
Check(result?.Presentation?.SeoTitle == "CRECISOL institucional", "Presentation mapped");
Check(result?.Presentation?.IconSetToken == "outline", "Only validated tokens");
Check(result?.WebsiteUrl is null, "Missing optional origin does not inject default");
Check(result?.LogoUrl == "/assets/brand.png", "Local brand path preserved");

var noPresentation = await new ReadPublishedPlatformBrand(
    new FixedReader(global with { Presentation = null })).ExecuteAsync(CancellationToken.None);
Check(noPresentation?.Presentation is null, "Optional presentation safely absent");

var missing = await new ReadPublishedPlatformBrand(new FixedReader(null))
    .ExecuteAsync(CancellationToken.None);
Check(missing is null, "No published snapshot means no public fallback");

var foreignId = Guid.Parse("22222222-2222-2222-2222-222222222222");
await Deny(global with { Scope = BrandScope.Tenant, ScopeId = foreignId },
    "Published tenant cannot be platform");
await Deny(global with { Status = BrandPublicationStatus.Draft, PublishedAt = default },
    "Draft cannot be anonymous");
await Deny(global with { Name = "<img src=x onerror=alert(1)>" },
    "Database markup cannot escape domain validation");
await Deny(global with { PrimaryColor = "url(javascript:bad)" },
    "Database CSS injection cannot escape domain validation");
await Deny(global with { LogoUrl = "https://invalid.example/logo.png" },
    "Remote active content is denied");
await Deny(global with { Presentation = global.Presentation! with
    { TypographyToken = "evil" } }, "Arbitrary font token rejected");

var responseProperties = typeof(PublicPlatformBrand).GetProperties()
    .Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
Check(!responseProperties.Contains("ScopeId"), "Tenant identifier never in public DTO");
Check(!responseProperties.Contains("Scope"), "Internal scope never in public DTO");
Check(!responseProperties.Contains("Status"), "Internal publication state never in DTO");
Check(!responseProperties.Contains("OperationId"), "Publication tokens never in DTO");

Console.WriteLine($"PASS C01-04 Application: {count} typed positive/negative assertions, no DB or API.");

void Check(bool condition, string label)
{
    if (!condition) throw new Exception("FAIL: " + label);
    count++;
}

async Task Deny(BrandSnapshot snapshot, string label)
{
    var denied = false;
    try
    {
        await new ReadPublishedPlatformBrand(new FixedReader(snapshot))
            .ExecuteAsync(CancellationToken.None);
    }
    catch (InvalidOperationException) { denied = true; }
    Check(denied, label);
}

sealed class FixedReader(BrandSnapshot? snapshot) : IPublishedPlatformBrandReader
{
    public Task<BrandSnapshot?> FindPublishedPlatformAsync(CancellationToken ct)
        => Task.FromResult(snapshot);
}
