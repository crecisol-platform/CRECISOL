using Crecisol.Application.Branding;
using Crecisol.Infrastructure.Branding;
using MySqlConnector;

if (args.Length != 1)
    throw new InvalidOperationException("Supply exactly one test mode.");

var assertions = 0;
var adapterType = typeof(MySqlPublishedPlatformBrandRevisionReader);

if (args[0] == "contracts")
{
    Check(typeof(IPublishedPlatformBrandRevisionReader).IsAssignableFrom(adapterType),
        "Adapter implements the C03-03 Application port");
    Check(!typeof(IPublishedPlatformBrandReader).IsAssignableFrom(adapterType),
        "Revision adapter is not an anonymous public brand reader");
    Check(adapterType.GetConstructors().Length == 1,
        "Explicit constructor only; no service locator");
    Check(adapterType.GetConstructors()[0].GetParameters().Length == 1 &&
          adapterType.GetConstructors()[0].GetParameters()[0].ParameterType == typeof(string),
        "Storage origin is selected outside the request");
    Check(adapterType.GetMethods().Count(method => method.DeclaringType == adapterType) == 1,
        "Exactly one read operation exposed; no write/admin API");
    Check(adapterType.GetInterfaces().Length == 1,
        "Only the narrow Application port is implemented");

    await ExpectInvalidConfig(null, "No DSN fails closed");
    await ExpectInvalidConfig("", "Empty DSN fails closed");
    await ExpectInvalidConfig("  ", "Whitespace DSN fails closed");

    using var cancelled = new CancellationTokenSource();
    cancelled.Cancel();
    var cancellationRejected = false;
    try
    {
        await new MySqlPublishedPlatformBrandRevisionReader(null)
            .ReadCurrentPublishedRevisionAsync(cancelled.Token);
    }
    catch (OperationCanceledException) { cancellationRejected = true; }
    Check(cancellationRejected, "Pre-cancelled operation is rejected before storage");

    Check(adapterType.Assembly.GetName().Name == "Crecisol.Infrastructure",
        "Adapter remains in Infrastructure");
    Check(typeof(IPublishedPlatformBrandRevisionReader).Assembly.GetName().Name ==
          "Crecisol.Application", "Port stays in Application");
}
else
{
    // This environment variable exists ONLY in a disposable, isolated CI test.
    // Production API does not register this adapter or read this variable.
    var dsn = Environment.GetEnvironmentVariable("CRECISOL_C03_04_TEST_DSN");
    if (string.IsNullOrWhiteSpace(dsn))
        throw new InvalidOperationException("CI test DSN is required.");
    var reader = new MySqlPublishedPlatformBrandRevisionReader(dsn);

    switch (args[0])
    {
        case "published":
            Check(await reader.ReadCurrentPublishedRevisionAsync(CancellationToken.None) == 2,
                "Exactly the current PLATFORM publication is returned, never draft or tenant");
            break;
        case "updated":
            Check(await reader.ReadCurrentPublishedRevisionAsync(CancellationToken.None) == 4,
                "New PLATFORM published pointer is read without caching");
            break;
        case "absent":
            Check(await reader.ReadCurrentPublishedRevisionAsync(CancellationToken.None) is null,
                "Missing PLATFORM pointer never falls back to tenant or draft");
            break;
        case "denied":
            var rejected = false;
            try
            {
                await reader.ReadCurrentPublishedRevisionAsync(CancellationToken.None);
            }
            catch (MySqlException e) when (e.Number == 1142)
            {
                rejected = true;
            }
            Check(rejected, "Revoked SELECT fails closed, with no fallback");
            break;
        default:
            throw new InvalidOperationException("Unknown test mode.");
    }
}

Console.WriteLine($"PASS C03-04: {args[0]} ({assertions} executable assertions); " +
    "no HTTP, tenant switching, publication, data writes, or production.");

void Check(bool condition, string message)
{
    if (!condition) throw new Exception("FAIL C03-04: " + message);
    assertions++;
}

async Task ExpectInvalidConfig(string? value, string message)
{
    var rejected = false;
    try
    {
        await new MySqlPublishedPlatformBrandRevisionReader(value)
            .ReadCurrentPublishedRevisionAsync(CancellationToken.None);
    }
    catch (InvalidOperationException) { rejected = true; }
    Check(rejected, message);
}
