using Crecisol.Api.Contracts;
using Crecisol.Application.Branding;
using Crecisol.Api.Modules.Branding;
using Crecisol.Api.Modules.Identity;
using Crecisol.Api.Configuration;

var builder = WebApplication.CreateBuilder(args);

// C06-09: one process logging sink with a closed, value-free record format.
// Do not let default request logging print raw paths, query strings or exceptions.
builder.Logging.ClearProviders();
builder.Logging.AddProvider(new C06SafeRuntimeLoggerProvider(Console.Out));

// Explicit fail-closed host list. No domain is assumed owned or verified.
// For local CI only: CRECISOL_C01_ALLOWED_HOSTS=127.0.0.1
var allowed = (Environment.GetEnvironmentVariable("CRECISOL_C01_ALLOWED_HOSTS") ?? "")
    .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
if (allowed.Length == 0 || allowed.Any(host =>
        host.Length > 253 || host.Contains('/') || host.Contains('\\') ||
        host.Contains(':') || host.Contains('*') || host.Any(char.IsWhiteSpace)))
    throw new InvalidOperationException("Explicit safe host allowlist is required.");

// C03-08: additional typed host and local-only configuration checks at the
// server composition root; no browser input, secret material or fallback.
LocalRuntimeSettings.ValidateHostAllowlist(allowed);
_ = LocalRuntimeSettings.Parse(
    Environment.GetEnvironmentVariable("CRECISOL_C03_08_PROFILE"),
    Environment.GetEnvironmentVariable("CRECISOL_C03_08_MAX_WORKER_BATCH"),
    Environment.GetEnvironmentVariable("CRECISOL_C03_08_ALLOW_EXTERNAL_NETWORK"),
    Environment.GetEnvironmentVariable("CRECISOL_C03_08_TEST_SOCKET"),
    allowed);
var trustedHosts = new HashSet<string>(allowed, StringComparer.OrdinalIgnoreCase);

// A connection string may originate ONLY from the server's environment,
// never Host, query, headers, tenant IDs or a published response.
var serverDsn = Environment.GetEnvironmentVariable("CRECISOL_C01_READONLY_DSN");
builder.Services.AddPublicBrandingModule(serverDsn);

var c05RegistrationMode = Environment.GetEnvironmentVariable("CRECISOL_C05_REGISTRATION_ENABLED");
if (c05RegistrationMode is not (null or "true" or "false"))
    throw new InvalidOperationException("Invalid C05 registration mode.");
var c05RegistrationEnabled = string.Equals(c05RegistrationMode, "true", StringComparison.Ordinal);
if (c05RegistrationEnabled)
    builder.Services.AddOperationalC05Registration(
        Environment.GetEnvironmentVariable("CRECISOL_C05_IDENTITY_DSN"));

var c05CompletionMode = Environment.GetEnvironmentVariable(
    "CRECISOL_C05_LOCAL_ACCOUNT_COMPLETION_ENABLED");
if (c05CompletionMode is not (null or "true" or "false"))
    throw new InvalidOperationException("Invalid local account completion mode.");
var c05CompletionEnabled = string.Equals(c05CompletionMode, "true", StringComparison.Ordinal);
if (c05CompletionEnabled)
{
    if (!c05RegistrationEnabled)
        throw new InvalidOperationException("Local account completion requires C05 registration storage.");
    builder.Services.AddLocalRegistrationCompletion(
        Environment.GetEnvironmentVariable("CRECISOL_C05_IDENTITY_DSN"),
        trustedHosts.Where(host => host.EndsWith(".example.test", StringComparison.OrdinalIgnoreCase))
            .ToArray());
}

var operationalMode = Environment.GetEnvironmentVariable("CRECISOL_C05_LOCAL_IDENTITY_ENABLED");
if (operationalMode is not (null or "true" or "false"))
    throw new InvalidOperationException("Invalid operational identity mode.");
var operationalIdentityEnabled = operationalMode == "true";
if (operationalIdentityEnabled)
{
    if (!c05CompletionEnabled)
        throw new InvalidOperationException("Operational identity requires isolated account completion.");
    builder.Services.AddLocalOperationalIdentity(Environment.GetEnvironmentVariable("CRECISOL_C05_IDENTITY_DSN")!);
}

var app = builder.Build();
var safeHttpOutcome = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(C06SafeRuntimeLoggerProvider.OutcomeCategory);

// Protect both identity realms before host validation, routing denials and
// exception handling. Register first so even a downstream header reset
// (including the exception handler) cannot remove the private/no-store policy.
// Path selection affects caching only; it never grants access or selects a tenant.
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api/v1/identity", StringComparison.OrdinalIgnoreCase) ||
        context.Request.Path.StartsWithSegments("/api/v1/platform-identity", StringComparison.OrdinalIgnoreCase) ||
        context.Request.Path.StartsWithSegments("/api/v1/private", StringComparison.OrdinalIgnoreCase) ||
        context.Request.Path.StartsWithSegments("/api/v1/media", StringComparison.OrdinalIgnoreCase) ||
        context.Request.Path.StartsWithSegments("/api/v1/downloads", StringComparison.OrdinalIgnoreCase) ||
        context.Request.Path.StartsWithSegments("/api/v1/exports", StringComparison.OrdinalIgnoreCase) ||
        context.Request.Path.StartsWithSegments("/api/v1/logs", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.CacheControl = "no-store, private";
            context.Response.Headers.Pragma = "no-cache";
            context.Response.Headers["Cross-Origin-Resource-Policy"] = "same-origin";
            return Task.CompletedTask;
        });
    }
    await next(context);
    // Numeric HTTP outcome only; never use a path, user, header or exception as a log field.
    safeHttpOutcome.LogInformation(new EventId(context.Response.StatusCode), "HTTP outcome");
});

// Safe failure across every environment, with no server exception details.
app.UseExceptionHandler(error => error.Run(context =>
{
    context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
    return Task.CompletedTask;
}));

app.Use(async (context, next) =>
{
    context.Response.Headers.CacheControl = "no-store";
    context.Response.Headers.Pragma = "no-cache";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";

    // Do not trust forwarded host, ambiguous Host or browser-provided context.
    if (!trustedHosts.Contains(context.Request.Host.Host) ||
        context.Request.Headers.ContainsKey("X-Forwarded-Host") ||
        context.Request.Headers.ContainsKey("Forwarded"))
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }
    await next(context);
});

// This is the ONLY route in C01-04. No admin, draft, preview, tenant,
// publication/writes, authentication or deployment endpoint is registered.
app.MapGet("/api/v1/public/branding/platform", HandlePublishedBrand)
    .AllowAnonymous()
    .WithName("ReadPublishedPlatformBrand");

if (c05RegistrationEnabled)
    app.MapNeutralRegistrationIntent();
if (c05RegistrationEnabled && !c05CompletionEnabled)
    app.MapOperationalRegistrationProofConfirmation();
if (c05CompletionEnabled)
    app.MapOperationalRegistrationCompletion();
if (operationalIdentityEnabled)
    app.MapLocalOperationalIdentity();

app.Run();

static async Task<IResult> HandlePublishedBrand(
    ReadPublishedPlatformBrand useCase, HttpContext context, CancellationToken ct)
{
    if (context.Request.QueryString.HasValue ||
        context.Request.Headers.ContainsKey("X-Tenant-Id") ||
        context.Request.Headers.ContainsKey("Authorization") ||
        !PublicBrandRequestValidator.IsValid(context.Request))
        return Results.BadRequest();

    try
    {
        var brand = await useCase.ExecuteAsync(ct);
        if (brand is null) return Results.NotFound();
        return Results.Json(PublishedPlatformBrandDto.FromApplication(brand));
    }
    catch (OperationCanceledException) when (ct.IsCancellationRequested)
    {
        throw;
    }
    catch (Exception)
    {
        // Fail closed. Never return SQL errors, DSNs or invalid private data.
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
}

