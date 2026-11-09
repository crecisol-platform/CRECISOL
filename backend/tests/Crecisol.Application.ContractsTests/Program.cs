using Crecisol.Application.Branding;
using Crecisol.Domain.Branding;

var assertions = 0;
var operationId = Guid.Parse("33333333-3333-3333-3333-333333333333");
var foreignTenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");
var platformDraft = new BrandSnapshot(
    BrandScope.Platform, Guid.Empty, 2, BrandPublicationStatus.Draft,
    "Marca institucional", "Tu negocio, a tu manera.",
    "Comercio y servicios parametrizables",
    "#123456", "#ABCDEF", "/assets/logo.png", null, default);
var proposal = new BrandPublicationProposal(
    platformDraft, 1, operationId, "Publicar identidad aprobada.");

using var active = new CancellationTokenSource();
var validPort = new FixedRevisionReader(1);
var validResult = await new AssessPlatformBrandPublicationProposal(validPort)
    .ExecuteAsync(proposal, active.Token);
Check(validResult.CurrentPublishedRevision == 1, "Current revision is internal metadata");
Check(validResult.ProposedDraftRevision == 2, "Proposed revision is internal metadata");
Check(validPort.Calls == 1, "Exactly one read through the port");
Check(validPort.ObservedToken == active.Token, "CancellationToken forwarded unchanged");

var emptyPort = new FixedRevisionReader(null);
var initial = await new AssessPlatformBrandPublicationProposal(emptyPort)
    .ExecuteAsync(proposal with
    {
        Draft = platformDraft with { Revision = 1 },
        ExpectedCurrentPublishedRevision = 0
    }, CancellationToken.None);
Check(initial.CurrentPublishedRevision == 0 && initial.ProposedDraftRevision == 1,
    "Missing published revision has explicit zero semantics");
Check(emptyPort.Calls == 1, "Unpublished platform still uses read-only port");

await Deny(proposal, null, "Missing current revision must not match expected one");
await Deny(proposal, 0, "Stale expected revision must not be treated as current");
await Deny(proposal, 2, "Externally changed current revision must be rejected");
await Deny(proposal, -1, "Port may not supply negative revision");
await Deny(proposal, long.MaxValue, "Port current revision cannot bypass expected version");

await DenyBeforePort(proposal with
    { Draft = platformDraft with { Scope = BrandScope.Tenant, ScopeId = foreignTenantId } },
    "Tenant proposal rejected before any data read");
await DenyBeforePort(proposal with
    { Draft = platformDraft with { ScopeId = foreignTenantId } },
    "Platform scope with tenant identifier rejected");
await DenyBeforePort(proposal with
    { Draft = platformDraft with { Status = BrandPublicationStatus.Published,
        PublishedAt = DateTimeOffset.UtcNow } },
    "Published object cannot be submitted as a draft");
await DenyBeforePort(proposal with { ExpectedCurrentPublishedRevision = -1 },
    "Negative expected version rejected before port");
await DenyBeforePort(proposal with { ExpectedCurrentPublishedRevision = 2 },
    "Equal proposed and expected versions rejected before port");
await DenyBeforePort(proposal with { OperationId = Guid.Empty },
    "Missing operation id rejected before port");
await DenyBeforePort(proposal with { Reason = "x" },
    "Unvalidated publication reason rejected before port");
await DenyBeforePort(proposal with
    { Draft = platformDraft with { Name = "<img src=x onerror=alert(1)>" } },
    "Untrusted HTML denied before data read");
await DenyBeforePort(proposal with
    { Draft = platformDraft with { PrimaryColor = "url(javascript:evil)" } },
    "Untrusted CSS rejected before data read");

var nullPort = new FixedRevisionReader(1);
var nullRejected = false;
try
{
    await new AssessPlatformBrandPublicationProposal(nullPort)
        .ExecuteAsync(null!, CancellationToken.None);
}
catch (ArgumentNullException) { nullRejected = true; }
Check(nullRejected && nullPort.Calls == 0, "Null proposal fails before port");

var nullConstructorRejected = false;
try { _ = new AssessPlatformBrandPublicationProposal(null!); }
catch (ArgumentNullException) { nullConstructorRejected = true; }
Check(nullConstructorRejected, "Missing reader dependency rejected");

using var cancelled = new CancellationTokenSource();
cancelled.Cancel();
var cancelledPort = new FixedRevisionReader(1);
await ExpectCancelled(
    () => new AssessPlatformBrandPublicationProposal(cancelledPort)
        .ExecuteAsync(proposal, cancelled.Token),
    "Pre-cancelled operation must fail closed");
Check(cancelledPort.Calls == 0, "Pre-cancelled command does not read");

using var cancelledDuringRead = new CancellationTokenSource();
var cancellationPort = new FixedRevisionReader(1)
{
    OnRead = () => cancelledDuringRead.Cancel()
};
await ExpectCancelled(
    () => new AssessPlatformBrandPublicationProposal(cancellationPort)
        .ExecuteAsync(proposal, cancelledDuringRead.Token),
    "Cancellation after port read is honored");
Check(cancellationPort.Calls == 1, "Cancellation after read does not retry");

var brokenPort = new ThrowingReader();
var failedClosed = false;
try
{
    await new AssessPlatformBrandPublicationProposal(brokenPort)
        .ExecuteAsync(proposal, CancellationToken.None);
}
catch (InvalidOperationException) { failedClosed = true; }
Check(failedClosed && brokenPort.Calls == 1, "Port failure propagates without fallback");

var responseProperties = typeof(PlatformBrandPublicationPreflight).GetProperties()
    .Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
foreach (var forbidden in new[] { "TenantId", "ScopeId", "OperationId",
                                  "Reason", "ConnectionString", "Dsn", "Token", "Authorized" })
    Check(!responseProperties.Contains(forbidden),
        "Preflight result must not expose " + forbidden);

Check(typeof(IPublishedPlatformBrandRevisionReader).GetMethods().Length == 1,
    "Read-only port exposes one operation, no write API");
Check(typeof(AssessPlatformBrandPublicationProposal).GetMethods()
        .Where(x => x.DeclaringType == typeof(AssessPlatformBrandPublicationProposal))
        .Count() == 1,
    "Use case exposes only the internal ExecuteAsync operation");

Console.WriteLine($"PASS C03-03: {assertions} read-only Application port/use-case assertions. " +
    "No publication, persistence, authorization, private API, database or deployment tested.");
return;

void Check(bool ok, string label)
{
    if (!ok) throw new Exception("FAIL C03-03: " + label);
    assertions++;
}

async Task Deny(BrandPublicationProposal candidate, long? current, string label)
{
    var port = new FixedRevisionReader(current);
    var rejected = false;
    try
    {
        await new AssessPlatformBrandPublicationProposal(port)
            .ExecuteAsync(candidate, CancellationToken.None);
    }
    catch (InvalidOperationException) { rejected = true; }
    Check(rejected && port.Calls == 1, label);
}

async Task DenyBeforePort(BrandPublicationProposal candidate, string label)
{
    var port = new FixedRevisionReader(1);
    var rejected = false;
    try
    {
        await new AssessPlatformBrandPublicationProposal(port)
            .ExecuteAsync(candidate, CancellationToken.None);
    }
    catch (InvalidOperationException) { rejected = true; }
    Check(rejected && port.Calls == 0, label);
}

async Task ExpectCancelled(Func<Task> operation, string label)
{
    var rejected = false;
    try { await operation(); }
    catch (OperationCanceledException) { rejected = true; }
    Check(rejected, label);
}

sealed class FixedRevisionReader(long? revision) : IPublishedPlatformBrandRevisionReader
{
    public int Calls { get; private set; }
    public CancellationToken ObservedToken { get; private set; }
    public Action? OnRead { get; init; }

    public Task<long?> ReadCurrentPublishedRevisionAsync(CancellationToken cancellationToken)
    {
        Calls++;
        ObservedToken = cancellationToken;
        OnRead?.Invoke();
        return Task.FromResult(revision);
    }
}

sealed class ThrowingReader : IPublishedPlatformBrandRevisionReader
{
    public int Calls { get; private set; }
    public Task<long?> ReadCurrentPublishedRevisionAsync(CancellationToken ct)
    {
        Calls++;
        throw new InvalidOperationException("Simulated infrastructure failure.");
    }
}
