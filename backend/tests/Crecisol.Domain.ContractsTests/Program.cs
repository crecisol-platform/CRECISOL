using Crecisol.Domain.Branding;
using Crecisol.Domain.Common;

var assertions = 0;
var tenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
var otherTenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");

var initial = DomainRevision.From(1);
Pass(initial.Value == 1, "Positive revision");
Pass(DomainRevision.From(1) == initial, "Revision has value equality");
Pass(DomainRevision.From(long.MaxValue).Value == long.MaxValue, "Largest valid revision");
Pass(initial.Successor().Value == 2 && initial.Value == 1, "Immutable revision successor");
Pass(DomainRevision.RequireNewerThan(1, 0).Value == 1, "First revision may follow zero");
Pass(DomainRevision.RequireNewerThan(9, 8).Value == 9, "Revision strictly advances");
Pass(DomainRevision.RequireNewerThan(long.MaxValue, long.MaxValue - 1).Value == long.MaxValue,
    "Maximum valid proposed revision");
Pass(typeof(DomainRevision).GetProperty(nameof(DomainRevision.Value))?.SetMethod is null,
    "Revision has no public setter");

Reject(() => DomainRevision.From(0), "Zero revision is invalid");
Reject(() => DomainRevision.From(-1), "Negative revision is invalid");
Reject(() => DomainRevision.RequireNewerThan(0, 0), "Zero proposed revision rejected");
Reject(() => DomainRevision.RequireNewerThan(1, -1), "Negative expected revision rejected");
Reject(() => DomainRevision.RequireNewerThan(5, 5), "Repeated revision rejected");
Reject(() => DomainRevision.RequireNewerThan(4, 5), "Backward revision rejected");
Reject(() => DomainRevision.From(long.MaxValue).Successor(), "Revision overflow rejected");

var platform = BrandScopeKey.Create(BrandScope.Platform, Guid.Empty);
var tenant = BrandScopeKey.Create(BrandScope.Tenant, tenantId);
Pass(platform.Scope == BrandScope.Platform && platform.ScopeId == Guid.Empty, "Valid platform key");
Pass(tenant.Scope == BrandScope.Tenant && tenant.ScopeId == tenantId, "Valid tenant key");
Pass(tenant == BrandScopeKey.Create(BrandScope.Tenant, tenantId), "Identity has value equality");
Pass(tenant != BrandScopeKey.Create(BrandScope.Tenant, otherTenantId),
    "Distinct tenants cannot share identity values");
Pass(platform != tenant, "Platform and tenant keys are different");
Pass(typeof(BrandScopeKey).GetProperties().All(p => p.SetMethod is null),
    "Scope key has no public setter");
Reject(() => BrandScopeKey.Create((BrandScope)0, Guid.Empty), "Unknown scope zero rejected");
Reject(() => BrandScopeKey.Create((BrandScope)99, tenantId), "Unknown scope rejected");
Reject(() => BrandScopeKey.Create(BrandScope.Platform, tenantId), "Platform with tenant id rejected");
Reject(() => BrandScopeKey.Create(BrandScope.Tenant, Guid.Empty), "Tenant with empty id rejected");

var draft = new BrandSnapshot(
    BrandScope.Tenant, tenantId, 2, BrandPublicationStatus.Draft,
    "Tienda de prueba", "Venta de prueba", "Identidad parametrizable",
    "#123456", "#ABCDEF", "/assets/logo.png", null, default);
Pass(draft.Validate() == draft, "Existing brand entity retains validation");
var preview = BrandPreviewReference.FromDraft(draft).Validate();
Pass(preview.ScopeId == tenantId && preview.DraftRevision == draft.Revision,
    "Preview is intent metadata only");
var proposal = new BrandPublicationProposal(
    draft, 1, Guid.Parse("33333333-3333-3333-3333-333333333333"),
    "Publicar cambio validado.");
Pass(proposal.Validate() == proposal, "Existing proposal uses revision rule");
Pass((proposal with { ExpectedCurrentPublishedRevision = 0 }).Validate() is not null,
    "No previously published revision is allowed");
Reject(() => (proposal with { ExpectedCurrentPublishedRevision = 2 }).Validate(),
    "Stale proposal rejected");
Reject(() => (proposal with { ExpectedCurrentPublishedRevision = -1 }).Validate(),
    "Negative expected publication revision rejected");
Reject(() => (draft with { Revision = 0 }).Validate(), "Invalid brand version rejected");
Reject(() => new BrandPreviewReference(BrandScope.Platform, tenantId, 1).Validate(),
    "Fake platform identity in preview rejected");
Reject(() => new BrandPreviewReference(BrandScope.Tenant, tenantId, 0).Validate(),
    "Zero preview revision rejected");

Pass(!typeof(BrandScopeKey).GetProperties().Any(p =>
    p.Name.Contains("Token", StringComparison.OrdinalIgnoreCase) ||
    p.Name.Contains("Permission", StringComparison.OrdinalIgnoreCase)),
    "Domain key does not expose an authorization capability");
Console.WriteLine($"PASS C03-02: {assertions} pure Domain assertions. " +
    "No private auth, tenant authorization, persistence, MySQL or API claimed.");
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
    if (!rejected) throw new Exception("Invalid Domain contract was accepted: " + name);
    assertions++;
}
