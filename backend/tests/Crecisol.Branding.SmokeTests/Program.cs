using Crecisol.Application.Branding;
using Crecisol.Domain.Branding;

var good = new BrandSnapshot(
    BrandScope.Platform, Guid.Empty, 1, BrandPublicationStatus.Published,
    "CRECISOL", "Tu negocio, a tu manera.",
    "Comercio · Servicios · Membresías · Crecimiento",
    "#062E65", "#078FE8", "/assets/crecisol-logo-oficial.png",
    null, DateTimeOffset.Parse("2026-11-08T00:00:00Z"));

var read = new ReadPublishedPlatformBrand(new Stub(good), TimeProvider.System);
var result = await read.ExecuteAsync(CancellationToken.None);
Check(result.Name == good.Name && result.Revision == 1 && result.LogoUrl == good.LogoUrl,
    "Public DTO should reflect published source.");
Check(result.CopyrightText.Contains(good.Name), "Copyright must follow canonical identity.");
Check(!typeof(PublicPlatformBrand).GetProperties().Any(p => p.Name == "ScopeId" || p.Name == "WebsiteUrl"),
    "Public DTO must not expose internal identity metadata.");

ExpectDenied(good with { Scope = BrandScope.Tenant, ScopeId = Guid.NewGuid() }, "Tenant leakage");
ExpectDenied(good with { Status = BrandPublicationStatus.Draft }, "Draft leakage");
ExpectDenied(good with { Name = "<script>alert(1)</script>" }, "Markup injection");
ExpectDenied(good with { PrimaryColor = "url(javascript:evil)" }, "CSS injection");
ExpectDenied(good with { LogoUrl = "//malicious.invalid/image.png" }, "Remote image injection");
ExpectDenied(good with { WebsiteUrl = "http://crecisol.invalid" }, "Insecure site URL");
ExpectDenied(good with { ScopeId = Guid.NewGuid() }, "Invalid platform scope");
ExpectDenied(good with { Revision = 0 }, "Invalid version");

Console.WriteLine("C01 branding smoke: 10 assertions/groups passed. No HTTP, UI or MySQL was exercised.");

void ExpectDenied(BrandSnapshot snapshot, string scenario)
{
    try
    {
        _ = new ReadPublishedPlatformBrand(new Stub(snapshot), TimeProvider.System)
            .ExecuteAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult();
        throw new Exception($"Security-negative assertion failed: {scenario}");
    }
    catch (InvalidOperationException) { /* expected business rejection */ }
}

void Check(bool condition, string description)
{
    if (!condition) throw new Exception("Assertion failed: " + description);
}

sealed class Stub(BrandSnapshot snapshot) : IPlatformBrandSource
{
    public ValueTask<BrandSnapshot> ReadPublishedAsync(CancellationToken cancellationToken)
        => ValueTask.FromResult(snapshot);
}
