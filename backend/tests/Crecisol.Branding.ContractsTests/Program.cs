using Crecisol.Domain.Branding;

var assertions = 0;
var utc = new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);
var good = new BrandSnapshot(
    BrandScope.Platform, Guid.Empty, 1, BrandPublicationStatus.Published,
    "CRECISOL", "Tu negocio, a tu manera.",
    "Comercio · Servicios · Membresías · Crecimiento",
    "#062E65", "#078FE8", "/assets/crecisol-logo-oficial.png",
    null, utc)
{
    Presentation = new BrandPresentation(
        "/assets/crecisol-favicon.png", "system", "outline",
        "CRECISOL", "Comercio, servicios, membresías y crecimiento.",
        null)
};

var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
var tenantDraft = good with
{
    Scope = BrandScope.Tenant, ScopeId = tenantId, Revision = 4,
    Status = BrandPublicationStatus.Draft, PublishedAt = default,
    Name = "Comercio de prueba", WebsiteUrl = "https://tienda.example.test"
};

Pass(good.Validate() == good, "Valid published PLATFORM identity");
Pass(good.RequirePublicPlatform() == good, "Published PLATFORM is public");
Pass(tenantDraft.Validate() == tenantDraft, "Valid isolated TENANT draft");
Pass(tenantDraft.Presentation?.Validate() is not null, "Safe presentation tokens");
Pass(good with { WebsiteUrl = "https://example.test" } is not null, "Immutable copy is available");
Pass((good with { WebsiteUrl = "https://example.test" }).Validate().WebsiteUrl == "https://example.test",
    "HTTPS origin accepted without claiming proof of domain ownership");
Pass((good with { Presentation = null }).Validate().Presentation is null,
    "Optional presentation contract remains backward-compatible with C01-04 draft");

Reject(() => (good with { Scope = BrandScope.Tenant }).Validate(), "Tenant with zero platform identifier");
Reject(() => (good with { ScopeId = tenantId }).Validate(), "Platform with foreign tenant identifier");
Reject(() => (good with { Scope = (BrandScope)99 }).Validate(), "Unknown scope");
Reject(() => (good with { Status = (BrandPublicationStatus)99 }).Validate(), "Unknown publication status");
Reject(() => (good with { Revision = 0 }).Validate(), "Zero version");
Reject(() => (good with { Revision = -1 }).Validate(), "Negative version");
Reject(() => (good with { Name = "<script>alert(1)</script>" }).Validate(), "Markup injection");
Reject(() => (good with { Name = "Bidi\u202eName" }).Validate(), "Bidi spoofing");
Reject(() => (good with { Slogan = "A\nB" }).Validate(), "Control character");
Reject(() => (good with { Descriptor = " " }).Validate(), "Blank descriptor");
Reject(() => (good with { PrimaryColor = "url(javascript:alert(1))" }).Validate(), "CSS injection");
Reject(() => (good with { AccentColor = "#fff" }).Validate(), "Non-hex-6 theme value");
Reject(() => (good with { LogoUrl = "//attacker.invalid/x.png" }).Validate(), "Protocol relative asset");
Reject(() => (good with { LogoUrl = "/assets/../private.png" }).Validate(), "Asset path traversal");
Reject(() => (good with { LogoUrl = "/assets/logo.svg" }).Validate(), "Active SVG image");
Reject(() => (good with { WebsiteUrl = "http://example.test" }).Validate(), "HTTP downgrade");
Reject(() => (good with { WebsiteUrl = "javascript:alert(1)" }).Validate(), "Javascript URL");
Reject(() => (good with { WebsiteUrl = "https://user:pass@example.test" }).Validate(), "URL credentials");
Reject(() => (good with { WebsiteUrl = "https://example.test/path" }).Validate(), "Unverified path routing");
Reject(() => (good with { WebsiteUrl = "https://example.test/?token=x" }).Validate(), "URL token query");
Reject(() => (good with { WebsiteUrl = "https://localhost" }).Validate(), "Local-domain routing");
Reject(() => (good with { PublishedAt = default }).Validate(), "Published timestamp required");
Reject(() => (good with { PublishedAt = utc.ToOffset(TimeSpan.FromHours(-6)) }).Validate(),
    "Published timestamp must be UTC");
Reject(() => (tenantDraft with { PublishedAt = utc }).Validate(), "Draft cannot have publication timestamp");
Reject(() => tenantDraft.RequirePublicPlatform(), "Tenant draft rejected from anonymous output");
Reject(() => (tenantDraft with { Status = BrandPublicationStatus.Published, PublishedAt = utc })
    .RequirePublicPlatform(), "Published TENANT identity cannot become public PLATFORM");

var presentation = good.Presentation!;
Reject(() => (good with { Presentation = presentation with { TypographyToken = "url(https://cdn.invalid/a)" } }).Validate(),
    "Untrusted font token");
Reject(() => (good with { Presentation = presentation with { IconSetToken = "<svg>" } }).Validate(),
    "Executable icon token");
Reject(() => (good with { Presentation = presentation with { FaviconUrl = "https://cdn.invalid/a.png" } }).Validate(),
    "Remote favicon");
Reject(() => (good with { Presentation = presentation with { SeoTitle = "<img src=x>" } }).Validate(),
    "HTML SEO title");
Reject(() => (good with { Presentation = presentation with { SeoDescription = "\r" } }).Validate(),
    "Control in SEO text");
Reject(() => (good with { Presentation = presentation with { CopyrightNotice = "<script>" } }).Validate(),
    "Untrusted copyright HTML");

var refToDraft = BrandPreviewReference.FromDraft(tenantDraft).Validate();
Pass(refToDraft.Scope == BrandScope.Tenant &&
    refToDraft.ScopeId == tenantId &&
    refToDraft.DraftRevision == tenantDraft.Revision, "Preview stores metadata only");
Pass(!typeof(BrandPreviewReference).GetProperties().Any(p => p.Name is "Name" or "LogoUrl" or "WebsiteUrl"),
    "Preview reference contains no brand data or access token");
Reject(() => BrandPreviewReference.FromDraft(good), "Published revision cannot be draft preview");
Reject(() => new BrandPreviewReference(BrandScope.Tenant, Guid.Empty, 1).Validate(),
    "Invalid preview scope/tenant");
Reject(() => new BrandPreviewReference(BrandScope.Platform, Guid.Empty, 0).Validate(),
    "Invalid preview revision");

var proposed = new BrandPublicationProposal(tenantDraft, 3, Guid.NewGuid(),
    "Publicar versión con identidad aprobada.");
Pass(proposed.Validate() == proposed, "Valid metadata-only publication proposal");
Reject(() => (proposed with { Draft = good }).Validate(), "Cannot propose already published record");
Reject(() => (proposed with { ExpectedCurrentPublishedRevision = 4 }).Validate(),
    "Stale publication revision");
Reject(() => (proposed with { ExpectedCurrentPublishedRevision = -1 }).Validate(),
    "Negative expected version");
Reject(() => (proposed with { OperationId = Guid.Empty }).Validate(),
    "Missing idempotent operation ID");
Reject(() => (proposed with { Reason = "<script>" }).Validate(),
    "Unsafe publication reason");
Reject(() => (proposed with { Reason = "ok" }).Validate(),
    "Publication reason too short");

Console.WriteLine($"PASS C01-02: {assertions} contract assertions. No API, auth, MySQL, UI or VPS tested.");
return;

void Pass(bool ok, string name)
{
    if (!ok) throw new Exception("Expected success: " + name);
    assertions++;
}

void Reject(Action action, string name)
{
    var rejected = false;
    try { action(); }
    catch (InvalidOperationException) { rejected = true; }
    if (!rejected) throw new Exception("Security-negative test not rejected: " + name);
    assertions++;
}
