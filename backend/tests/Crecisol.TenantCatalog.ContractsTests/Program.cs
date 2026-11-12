using System.Text.Json;
using Crecisol.Application.Tenancy;
using Crecisol.Domain.Tenancy;

var count = 0;
var first = TenantId.From(Guid.Parse("11111111-1111-1111-1111-111111111111"));
var second = TenantId.From(Guid.Parse("22222222-2222-2222-2222-222222222222"));
var when = new DateTimeOffset(2026, 11, 12, 0, 0, 0, TimeSpan.Zero);

Check(first == TenantId.From(first.Value), "Immutable tenant ID equality");
Check(first != second, "Tenants remain distinct");
Check(typeof(TenantId).GetProperty(nameof(TenantId.Value))?.SetMethod is null,
    "Tenant ID cannot be overwritten");
Expect<InvalidOperationException>(() => TenantId.From(Guid.Empty),
    "Empty tenant identifier is rejected");

var firstRecord = Snapshot(first, TenantLifecycleState.Draft);
Check(firstRecord.TenantId == first && firstRecord.Revision == 1,
    "Trusted tenant snapshot has identity and revision");
Check(firstRecord.DisplayName == "Café Central",
    "Unicode names are normalized NFC");
Check(firstRecord.InternalName == "Café registrado",
    "Internal label is separate from display name");
Check(firstRecord.State == TenantLifecycleState.Draft,
    "Draft status is a catalog label only");
Check(firstRecord.AddressMode == TenantAddressMode.PathSlug,
    "Address mode is descriptive, not host verification");
Check(!firstRecord.ToString().Contains("Café", StringComparison.Ordinal) &&
      !first.ToString().Contains("11111111", StringComparison.Ordinal),
    "Diagnostic stringification redacts tenant identifiers and names");
Check(typeof(TenantCatalogSnapshot).GetProperties().All(p => p.SetMethod is null),
    "Catalog snapshot cannot be mutated by a caller");

foreach (var state in Enum.GetValues<TenantLifecycleState>())
{
    var item = Snapshot(first, state);
    Check(item.State == state, "All defined catalog states rehydrate: " + state);
}
foreach (var mode in Enum.GetValues<TenantAddressMode>())
{
    var item = TenantCatalogSnapshot.RestoreFromTrustedCatalog(
        first, "Café registrado", "Café Central", TenantLifecycleState.PendingVerification,
        mode, 2, when);
    Check(item.AddressMode == mode, "All address modes are metadata: " + mode);
}
Expect<ArgumentNullException>(() => TenantCatalogSnapshot.RestoreFromTrustedCatalog(
    null!, "Café registrado", "Café Central", TenantLifecycleState.Draft,
    TenantAddressMode.PathSlug, 1, when), "Missing typed ID rejected");
Expect<InvalidOperationException>(() => TenantCatalogSnapshot.RestoreFromTrustedCatalog(
    first, "Café registrado", "Café Central", (TenantLifecycleState)0,
    TenantAddressMode.PathSlug, 1, when), "Invalid status zero");
Expect<InvalidOperationException>(() => TenantCatalogSnapshot.RestoreFromTrustedCatalog(
    first, "Café registrado", "Café Central", (TenantLifecycleState)999,
    TenantAddressMode.PathSlug, 1, when), "Invalid status nonzero");
Expect<InvalidOperationException>(() => TenantCatalogSnapshot.RestoreFromTrustedCatalog(
    first, "Café registrado", "Café Central", TenantLifecycleState.Draft,
    (TenantAddressMode)44, 1, when), "Unknown address mode rejected");
Expect<InvalidOperationException>(() => TenantCatalogSnapshot.RestoreFromTrustedCatalog(
    first, "Café registrado", "Café Central", TenantLifecycleState.Draft,
    TenantAddressMode.PathSlug, 0, when), "Zero revision rejected");
Expect<InvalidOperationException>(() => TenantCatalogSnapshot.RestoreFromTrustedCatalog(
    first, "Café registrado", "Café Central", TenantLifecycleState.Draft,
    TenantAddressMode.PathSlug, -1, when), "Negative revision rejected");
Expect<InvalidOperationException>(() => TenantCatalogSnapshot.RestoreFromTrustedCatalog(
    first, "Café registrado", "Café Central", TenantLifecycleState.Draft,
    TenantAddressMode.PathSlug, 1, default), "Missing UTC timestamp rejected");
Expect<InvalidOperationException>(() => TenantCatalogSnapshot.RestoreFromTrustedCatalog(
    first, "Café registrado", "Café Central", TenantLifecycleState.Draft,
    TenantAddressMode.PathSlug, 1, when.ToOffset(TimeSpan.FromHours(-6))),
    "Non-UTC catalog timestamps rejected");
foreach (var unsafeName in new[] {
    "", " ", "a", " Café Central", "Café Central ",
    "<img src=x onerror=alert(1)>", "Ti/enda", "Ti\\enda",
    "Safe\nUnsafe", "Safe\u202EUnsafe", "Café\u200B Central",
    new string('x', 121) })
{
    Expect<InvalidOperationException>(() => TenantCatalogSnapshot.RestoreFromTrustedCatalog(
        first, "Café registrado", unsafeName, TenantLifecycleState.Draft,
        TenantAddressMode.PathSlug, 1, when), "Unsafe display label rejected");
    Expect<InvalidOperationException>(() => TenantCatalogSnapshot.RestoreFromTrustedCatalog(
        first, unsafeName, "Café Central", TenantLifecycleState.Draft,
        TenantAddressMode.PathSlug, 1, when), "Unsafe internal label rejected");
}

var grant = new Policy(true);
var reader = new Reader(id => id == first ? firstRecord : null);
var useCase = new ReadInternalTenantCatalog(grant, reader);
using var active = new CancellationTokenSource();
var view = await useCase.ExecuteAsync(first, active.Token);
Check(view is not null, "Authorized read returns internal catalog view");
Check(view?.TenantId == first.Value && view.DisplayName == "Café Central" &&
      view.State == TenantLifecycleState.Draft && view.Revision == 1,
      "Internal projection includes only allowed descriptive fields");
Check(grant.Calls == 1 && reader.Calls == 1,
    "Policy checked before one read, no writes");
Check(grant.SeenToken == active.Token && reader.SeenToken == active.Token,
    "Cancellation context forwarded unchanged");
var serialized = JsonSerializer.Serialize(view);
foreach (var forbidden in new[] {
    "InternalName", "ConnectionString", "Dsn", "Database", "Secret",
    "Placement", "Claims", "Host", "Token", "Membership", "Owner", "Password"})
    Check(!serialized.Contains(forbidden, StringComparison.OrdinalIgnoreCase),
        "Internal projection never serializes " + forbidden);
Check(view!.GetType().GetProperties().Length == 5,
    "Response has exactly five allowlisted properties");
Check(await useCase.ExecuteAsync(second, CancellationToken.None) is null,
    "Authorized read for unknown ID returns no data");
Check(reader.Calls == 2, "Missing tenant never triggers a cross-tenant fallback");

var denied = new Policy(false);
var guardReader = new Reader(_ => firstRecord);
var denialCase = new ReadInternalTenantCatalog(denied, guardReader);
await ExpectAsync<UnauthorizedAccessException>(
    () => denialCase.ExecuteAsync(first, CancellationToken.None),
    "No backend permission -> reject valid tenant ID");
await ExpectAsync<UnauthorizedAccessException>(
    () => denialCase.ExecuteAsync(second, CancellationToken.None),
    "Forged tenant ID does not grant permission");
Check(guardReader.Calls == 0 && denied.Calls == 2,
    "Denied actor cannot even query catalog or enumerate tenants");

var wrongReader = new Reader(_ => Snapshot(second, TenantLifecycleState.Active));
await ExpectAsync<InvalidOperationException>(
    () => new ReadInternalTenantCatalog(new Policy(true), wrongReader)
        .ExecuteAsync(first, CancellationToken.None),
    "Hostile reader returning another tenant must fail closed");
Check(wrongReader.Calls == 1, "Mismatched data is never silently accepted");

await ExpectAsync<ArgumentNullException>(
    () => useCase.ExecuteAsync(null!, CancellationToken.None),
    "Null requested ID rejected before authorization");
Expect<ArgumentNullException>(() => new ReadInternalTenantCatalog(null!, reader),
    "Missing authorizer rejected by constructor");
Expect<ArgumentNullException>(() => new ReadInternalTenantCatalog(grant, null!),
    "Missing trusted reader rejected by constructor");

using var cancelled = new CancellationTokenSource();
cancelled.Cancel();
var cancelledAuth = new Policy(true);
var cancelledReader = new Reader(_ => firstRecord);
await ExpectAsync<OperationCanceledException>(
    () => new ReadInternalTenantCatalog(cancelledAuth, cancelledReader)
        .ExecuteAsync(first, cancelled.Token),
    "Pre-cancelled request must not check privileges or query catalog");
Check(cancelledAuth.Calls == 0 && cancelledReader.Calls == 0,
    "Cancellation before authorization does zero work");

using var duringGrant = new CancellationTokenSource();
var cancelGrant = new Policy(true) { OnCall = () => duringGrant.Cancel() };
var untouchedReader = new Reader(_ => firstRecord);
await ExpectAsync<OperationCanceledException>(
    () => new ReadInternalTenantCatalog(cancelGrant, untouchedReader)
        .ExecuteAsync(first, duringGrant.Token),
    "Cancellation during authorization stops before storage");
Check(untouchedReader.Calls == 0, "Cancelled policy cannot query reader");

using var duringRead = new CancellationTokenSource();
var cancelReader = new Reader(_ => firstRecord) { OnRead = () => duringRead.Cancel() };
await ExpectAsync<OperationCanceledException>(
    () => new ReadInternalTenantCatalog(new Policy(true), cancelReader)
        .ExecuteAsync(first, duringRead.Token),
    "Cancellation during read never returns a snapshot");

var throwingPolicy = new Policy(true) { Throw = true };
var neverRead = new Reader(_ => firstRecord);
await ExpectAsync<InvalidOperationException>(
    () => new ReadInternalTenantCatalog(throwingPolicy, neverRead)
        .ExecuteAsync(first, CancellationToken.None),
    "Unavailable policy fails closed");
Check(neverRead.Calls == 0, "Failing authorization cannot read storage");

var throwingReader = new Reader(_ => firstRecord) { Throw = true };
await ExpectAsync<InvalidOperationException>(
    () => new ReadInternalTenantCatalog(new Policy(true), throwingReader)
        .ExecuteAsync(first, CancellationToken.None),
    "Unavailable catalog does not silently trust host or fall back");
Check(typeof(ITrustedTenantCatalogReader).GetMethods().Length == 1,
    "Catalog port has only read-by-ID, no list/creation/commands");
Check(typeof(IPlatformCatalogReadAuthorization).GetMethods().Length == 1,
    "Authorization port cannot accept an untrusted caller-controlled role flag");

Console.WriteLine("PASS C04-01: " + count +
    " .NET10 Domain/Application contracts, negative access, ID mismatch, " +
    "cancel/failure, metadata projection and immutability assertions. " +
    "No catalog storage/API, membership auth or cross-tenant DB certified.");
return;

TenantCatalogSnapshot Snapshot(TenantId id, TenantLifecycleState state) =>
    TenantCatalogSnapshot.RestoreFromTrustedCatalog(
        id, "Café registrado", "Cafe\u0301 Central", state,
        TenantAddressMode.PathSlug, 1, when);

void Check(bool condition, string message)
{
    if (!condition) throw new Exception("FAIL C04-01: " + message);
    count++;
}
void Expect<T>(Action action, string message) where T : Exception
{
    try { action(); }
    catch (T) { Check(true, message); return; }
    throw new Exception("FAIL C04-01: expected " + typeof(T).Name + ": " + message);
}
async Task ExpectAsync<T>(Func<Task> action, string message) where T : Exception
{
    try { await action(); }
    catch (T) { Check(true, message); return; }
    throw new Exception("FAIL C04-01: expected " + typeof(T).Name + ": " + message);
}

sealed class Policy(bool allowed) : IPlatformCatalogReadAuthorization
{
    public int Calls { get; private set; }
    public bool Throw { get; init; }
    public CancellationToken SeenToken { get; private set; }
    public Action? OnCall { get; init; }
    public ValueTask<bool> AllowsInternalCatalogReadAsync(CancellationToken ct)
    {
        Calls++;
        SeenToken = ct;
        OnCall?.Invoke();
        if (Throw) throw new InvalidOperationException("Policy unavailable");
        return ValueTask.FromResult(allowed);
    }
}
sealed class Reader(Func<TenantId, TenantCatalogSnapshot?> query)
    : ITrustedTenantCatalogReader
{
    public int Calls { get; private set; }
    public bool Throw { get; init; }
    public CancellationToken SeenToken { get; private set; }
    public Action? OnRead { get; init; }
    public Task<TenantCatalogSnapshot?> FindByIdAsync(TenantId id, CancellationToken ct)
    {
        Calls++;
        SeenToken = ct;
        OnRead?.Invoke();
        if (Throw) throw new InvalidOperationException("Catalog unavailable");
        return Task.FromResult(query(id));
    }
}
