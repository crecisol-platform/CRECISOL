using Crecisol.Application.Branding;
using Crecisol.Infrastructure.Branding;

var builder = WebApplication.CreateBuilder(args);

// Explicit fail-closed host list. No domain is assumed owned or verified.
// For local CI only: CRECISOL_C01_ALLOWED_HOSTS=127.0.0.1
var allowed = (Environment.GetEnvironmentVariable("CRECISOL_C01_ALLOWED_HOSTS") ?? "")
    .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
if (allowed.Length == 0 || allowed.Any(host =>
        host.Length > 253 || host.Contains('/') || host.Contains('\\') ||
        host.Contains(':') || host.Contains('*') || host.Any(char.IsWhiteSpace)))
    throw new InvalidOperationException("Explicit safe host allowlist is required.");

var trustedHosts = new HashSet<string>(allowed, StringComparer.OrdinalIgnoreCase);

// A connection string may originate ONLY from the server's environment,
// never Host, query, headers, tenant IDs or a published response.
var serverDsn = Environment.GetEnvironmentVariable("CRECISOL_C01_READONLY_DSN");
builder.Services.AddSingleton<IPublishedPlatformBrandReader>(
    _ => new MySqlPublishedPlatformBrandReader(serverDsn));
builder.Services.AddScoped<ReadPublishedPlatformBrand>();

var app = builder.Build();

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
app.MapGet("/api/v1/public/branding/platform",
    async (ReadPublishedPlatformBrand useCase, HttpContext context, CancellationToken ct) =>
    {
        if (context.Request.QueryString.HasValue ||
            context.Request.Headers.ContainsKey("X-Tenant-Id") ||
            context.Request.Headers.ContainsKey("Authorization"))
            return Results.BadRequest();

        try
        {
            var brand = await useCase.ExecuteAsync(ct);
            return brand is null ? Results.NotFound() : Results.Json(brand);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Fail closed. Do NOT return SQL errors, connection strings,
            // invalid private records, drafts or stack traces.
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    })
    .AllowAnonymous()
    .WithName("ReadPublishedPlatformBrand");

app.Run();
