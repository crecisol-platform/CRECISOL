using Crecisol.Api.Modules.Branding;
using Crecisol.Application.Branding;
using Crecisol.Infrastructure.Branding;
using Microsoft.Extensions.DependencyInjection;

var count = 0;
void Check(bool assertion, string message)
{
    if (!assertion) throw new InvalidOperationException("FAIL C03-06: " + message);
    count++;
}

var services = new ServiceCollection();
Check(ReferenceEquals(services.AddPublicBrandingModule(null), services),
    "Registration returns same explicit service collection");
Check(services.Count == 2, "Exactly two public Branding descriptors registered");

var readerDesc = services.Single(d => d.ServiceType == typeof(IPublishedPlatformBrandReader));
var useCaseDesc = services.Single(d => d.ServiceType == typeof(ReadPublishedPlatformBrand));
Check(readerDesc.Lifetime == ServiceLifetime.Singleton, "Reader is singleton");
Check(useCaseDesc.Lifetime == ServiceLifetime.Scoped, "Read case is scoped");
Check(readerDesc.ImplementationInstance is MySqlPublishedPlatformBrandReader,
    "Only audited public MySQL reader is registered");
Check(useCaseDesc.ImplementationType == typeof(ReadPublishedPlatformBrand),
    "Scoped application case is activated by typed constructor");

using (var provider = services.BuildServiceProvider(new ServiceProviderOptions
       { ValidateScopes = true, ValidateOnBuild = true }))
{
    var reader = provider.GetRequiredService<IPublishedPlatformBrandReader>();
    Check(reader.GetType() == typeof(MySqlPublishedPlatformBrandReader),
        "Public port resolves only public read-only adapter");
    Check(ReferenceEquals(reader, provider.GetRequiredService<IPublishedPlatformBrandReader>()),
        "Reader singleton identity stable");
    Check(provider.GetService<IPublishedPlatformBrandRevisionReader>() is null,
        "Private revision reader port is not composed");
    Check(provider.GetService<AssessPlatformBrandPublicationProposal>() is null,
        "Internal proposal preflight not exposed");
    Check(provider.GetService<MySqlPublishedPlatformBrandRevisionReader>() is null,
        "Internal Infrastructure adapter not exposed");
    Check(provider.GetService<MySqlPublishedPlatformBrandReader>() is null,
        "Adapter concrete class is not independently resolvable");
    Check(provider.GetService<object>() is null,
        "No untyped dependency resolver registered");

    using var scopeA = provider.CreateScope();
    using var scopeB = provider.CreateScope();
    var a1 = scopeA.ServiceProvider.GetRequiredService<ReadPublishedPlatformBrand>();
    var a2 = scopeA.ServiceProvider.GetRequiredService<ReadPublishedPlatformBrand>();
    var b = scopeB.ServiceProvider.GetRequiredService<ReadPublishedPlatformBrand>();
    Check(ReferenceEquals(a1, a2), "Same request scope returns one case instance");
    Check(!ReferenceEquals(a1, b), "Different request scopes use different cases");
    Check(ReferenceEquals(
          scopeA.ServiceProvider.GetRequiredService<IPublishedPlatformBrandReader>(),
          scopeB.ServiceProvider.GetRequiredService<IPublishedPlatformBrandReader>()),
          "Public reader singleton shared across scopes without tenant selection");
    var rejected = false;
    try { _ = provider.GetRequiredService<ReadPublishedPlatformBrand>(); }
    catch (InvalidOperationException) { rejected = true; }
    Check(rejected, "Scoped use case cannot be resolved from singleton root");
    rejected = false;
    try { _ = await a1.ExecuteAsync(CancellationToken.None); }
    catch (InvalidOperationException) { rejected = true; }
    Check(rejected, "Missing trusted DSN fails closed at use (public API emits 503)");
}

var doubled = false;
try { services.AddPublicBrandingModule("another untrusted value"); }
catch (InvalidOperationException) { doubled = true; }
Check(doubled, "Duplicate Branding registration rejected");
Check(services.Count == 2, "Failed repeat did not mutate registration collection");

var conflictingReader = new ServiceCollection();
conflictingReader.AddSingleton<IPublishedPlatformBrandReader>(
    new MySqlPublishedPlatformBrandReader(null));
var before = conflictingReader.Count;
var rejectedOverride = false;
try { conflictingReader.AddPublicBrandingModule(null); }
catch (InvalidOperationException) { rejectedOverride = true; }
Check(rejectedOverride && conflictingReader.Count == before,
    "Existing public port cannot be silently overridden");

var conflictingCase = new ServiceCollection();
conflictingCase.AddScoped<ReadPublishedPlatformBrand>();
before = conflictingCase.Count;
rejectedOverride = false;
try { conflictingCase.AddPublicBrandingModule(null); }
catch (InvalidOperationException) { rejectedOverride = true; }
Check(rejectedOverride && conflictingCase.Count == before,
    "Existing case cannot be silently overridden");

var rejectedNull = false;
try { PublicBrandingModule.AddPublicBrandingModule(null!, null); }
catch (ArgumentNullException) { rejectedNull = true; }
Check(rejectedNull, "Null service collection rejected before any writes");

var withDsn = new ServiceCollection();
withDsn.AddPublicBrandingModule("Server=localhost;Database=disposable;Uid=readonly;Pwd=not-a-secret");
using (var provider = withDsn.BuildServiceProvider(new ServiceProviderOptions
       { ValidateScopes = true, ValidateOnBuild = true }))
{
    Check(provider.GetRequiredService<IPublishedPlatformBrandReader>() is
          MySqlPublishedPlatformBrandReader,
          "Trusted storage input still uses same fixed PLATFORM reader");
    Check(provider.GetService<IPublishedPlatformBrandRevisionReader>() is null,
          "Even configured slice does not expose internal revision port");
}

Console.WriteLine($"PASS C03-06: {count} executable registration/lifetime/fail-closed assertions.");
