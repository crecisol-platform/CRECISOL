using System.Text.Json;
using Crecisol.Api.Contracts;
using Crecisol.Application.Branding;
using Microsoft.AspNetCore.Http;

var assertions = 0;
void Check(bool ok, string message)
{
    if (!ok) throw new InvalidOperationException("FAIL C03-05: " + message);
    assertions++;
}

var published = new PublicPlatformBrand(
    2, "CRECISOL", "Tu negocio, a tu manera.", "Comercio y servicios",
    "#062E65", "#078FE8", "/assets/brand.png", null,
    new DateTimeOffset(2026, 11, 9, 12, 30, 0, TimeSpan.Zero),
    new PublicBrandPresentation(
        "/assets/favicon.webp", "system", "outline",
        "CRECISOL institucional", "Identidad institucional publicada.", null));

var dto = PublishedPlatformBrandDto.FromApplication(published);
Check(dto.Revision == 2 && dto.Name == "CRECISOL", "Public revision and name retained");
Check(dto.PublishedAt.Offset == TimeSpan.Zero, "Publication stays UTC");
Check(dto.WebsiteUrl is null, "Null optional website retained");
Check(dto.Presentation?.TypographyToken == "system", "Presentation token retained");
Check(dto.Presentation?.CopyrightNotice is null, "Null copyright retained");

var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
var json = JsonSerializer.Serialize(dto, options);
using (var doc = JsonDocument.Parse(json))
{
    var root = doc.RootElement;
    var keys = root.EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
    var expected = new HashSet<string>(StringComparer.Ordinal)
    {
        "revision", "name", "slogan", "descriptor", "primaryColor",
        "accentColor", "logoUrl", "websiteUrl", "publishedAt", "presentation"
    };
    Check(keys.SetEquals(expected), "JSON exposes exactly the Angular public top-level keys");
    Check(root.GetProperty("revision").GetInt64() == 2, "Revision serializes as number");
    Check(root.GetProperty("primaryColor").GetString() == "#062E65", "Colors survive mapping");
    Check(root.GetProperty("websiteUrl").ValueKind == JsonValueKind.Null, "Nullable website in JSON");
    var at = root.GetProperty("publishedAt").GetString();
    Check(at is not null && (at.EndsWith("+00:00", StringComparison.Ordinal) ||
                             at.EndsWith("Z", StringComparison.Ordinal)),
        "Published timestamp JSON remains UTC");
    var view = root.GetProperty("presentation");
    var fields = view.EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
    var publicFields = new HashSet<string>(StringComparer.Ordinal)
    {
        "faviconUrl", "typographyToken", "iconSetToken",
        "seoTitle", "seoDescription", "copyrightNotice"
    };
    Check(fields.SetEquals(publicFields), "Presentation JSON allowlist is exact");
    Check(view.GetProperty("faviconUrl").GetString() == "/assets/favicon.webp",
        "Favicon from published presentation");
    Check(view.GetProperty("copyrightNotice").ValueKind == JsonValueKind.Null,
        "Optional copyright retains null JSON");
    Check(!json.Contains("scope", StringComparison.OrdinalIgnoreCase) &&
          !json.Contains("tenant", StringComparison.OrdinalIgnoreCase) &&
          !json.Contains("password", StringComparison.OrdinalIgnoreCase) &&
          !json.Contains("connection", StringComparison.OrdinalIgnoreCase),
        "Serialized JSON has no private metadata");
}

var nullPresentation = PublishedPlatformBrandDto.FromApplication(
    published with { Presentation = null });
using (var doc = JsonDocument.Parse(JsonSerializer.Serialize(nullPresentation, options)))
    Check(doc.RootElement.GetProperty("presentation").ValueKind == JsonValueKind.Null,
        "Absent presentation serializes explicitly as null");

var nullRejected = false;
try { _ = PublishedPlatformBrandDto.FromApplication(null!); }
catch (ArgumentNullException) { nullRejected = true; }
Check(nullRejected, "Null application result rejected");

var topProperties = typeof(PublishedPlatformBrandDto).GetProperties()
    .Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
var subProperties = typeof(PublishedBrandPresentationDto).GetProperties()
    .Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
foreach (var forbidden in new[] {
    "TenantId", "Scope", "ScopeId", "Status", "Draft", "Dsn",
    "ConnectionString", "Authorization", "Secret", "Token", "OperationId"
})
{
    Check(!topProperties.Contains(forbidden), "No forbidden DTO property: " + forbidden);
    Check(!subProperties.Contains(forbidden), "No forbidden presentation field: " + forbidden);
}

HttpRequest NewRequest(string method = "GET")
{
    var ctx = new DefaultHttpContext();
    ctx.Request.Method = method;
    return ctx.Request;
}
Check(PublicBrandRequestValidator.IsValid(NewRequest()), "Plain anonymous GET accepted");
foreach (var method in new[] { "HEAD", "OPTIONS", "POST", "PUT", "PATCH", "DELETE", "TRACE" })
    Check(!PublicBrandRequestValidator.IsValid(NewRequest(method)),
        "Non-GET rejected: " + method);

void Deny(string label, Action<HttpRequest> attack)
{
    var request = NewRequest();
    attack(request);
    Check(!PublicBrandRequestValidator.IsValid(request), label);
}
Deny("Any query rejected", r => r.QueryString = new QueryString("?tenant=22"));
Deny("Empty query marker rejected", r => r.QueryString = new QueryString("?"));
Deny("Untrusted Authorization denied", r => r.Headers["Authorization"] = "Bearer forged");
Deny("Untrusted tenant header denied", r => r.Headers["X-Tenant-Id"] = "22");
Deny("Request body denied", r => r.ContentLength = 1);
Deny("Zero content-length still denied", r => r.Headers["Content-Length"] = "0");
Deny("Chunked body denied", r => r.Headers["Transfer-Encoding"] = "chunked");
Deny("Method override denied", r => r.Headers["X-HTTP-Method-Override"] = "DELETE");
Deny("Rewritten URL denied", r => r.Headers["X-Original-URL"] = "/api/v1/admin");
Deny("Rewrite header denied", r => r.Headers["X-Rewrite-URL"] = "/api/v1/tenant");
var nullRequestRejected = false;
try { _ = PublicBrandRequestValidator.IsValid(null!); }
catch (ArgumentNullException) { nullRequestRejected = true; }
Check(nullRequestRejected, "Validator requires a real request");

Console.WriteLine($"PASS C03-05: {assertions} executable DTO/JSON/HTTP validation assertions.");
