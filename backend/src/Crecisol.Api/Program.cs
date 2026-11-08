using Crecisol.Application.Branding;
using Crecisol.Infrastructure.Branding;

var builder = WebApplication.CreateBuilder(args);

// This adapter is a versioned local bootstrap, NOT tenant persistence or CMS editing.
var configurationPath = Path.Combine(
    AppContext.BaseDirectory, "Configuration", "platform-brand.published.json");
builder.Services.AddSingleton<IPlatformBrandSource>(
    new JsonPlatformBrandSource(configurationPath));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ReadPublishedPlatformBrand>();

// Local development UI only. No arbitrary CORS origins and no credentials.
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options => options.AddPolicy("CrecisolLocalUi", policy =>
        policy.WithOrigins("http://localhost:4200", "http://127.0.0.1:4200")
              .WithMethods("GET")
              .WithHeaders("Accept", "Content-Type")));
}

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
else
{
    app.UseCors("CrecisolLocalUi");
}

// Only PUBLIC platform metadata. No tenant or administrative endpoints exist yet.
app.MapGet("/api/v1/public/branding/platform",
    async (ReadPublishedPlatformBrand read, HttpContext context, CancellationToken ct) =>
    {
        var brand = await read.ExecuteAsync(ct);
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.XContentTypeOptions = "nosniff";
        return Results.Ok(brand);
    })
    .WithName("GetPublishedPlatformBrand")
    .Produces<PublicPlatformBrand>(StatusCodes.Status200OK);

app.Run();
