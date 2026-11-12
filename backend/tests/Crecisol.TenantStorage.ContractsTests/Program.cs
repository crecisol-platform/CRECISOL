using System.Reflection;
using Crecisol.Application.Tenancy;
using Crecisol.Domain.Tenancy;

var passed = 0;
var now = new DateTimeOffset(2026, 11, 12, 17, 0, 0, TimeSpan.Zero);
var tenantA = TenantId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
var tenantB = TenantId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
var slotA = DedicatedStorageSlotId.FromTrustedCatalog(
    Guid.Parse("11111111-2222-3333-4444-555555555555"));
var slotB = DedicatedStorageSlotId.FromTrustedCatalog(
    Guid.Parse("66666666-7777-8888-9999-aaaaaaaaaaaa"));
var sharedA = TenantStoragePlacementSnapshot.RestoreFromTrustedCatalog(
    tenantA, TenantPlacementMode.Shared, null, 1, now);
var dedicatedA = TenantStoragePlacementSnapshot.RestoreFromTrustedCatalog(
    tenantA, TenantPlacementMode.Dedicated, slotA, 2, now);
var dedicatedB = TenantStoragePlacementSnapshot.RestoreFromTrustedCatalog(
    tenantB, TenantPlacementMode.Dedicated, slotB, 1, now);

Check(Enum.GetValues<TenantPlacementMode>().Length == 2,
    "Only Shared and Dedicated placement modes exist");
Check(sharedA.Mode == TenantPlacementMode.Shared && sharedA.DedicatedSlot is null,
    "Shared placement carries no dedicated slot");
Check(dedicatedA.Mode == TenantPlacementMode.Dedicated &&
      dedicatedA.DedicatedSlot == slotA && dedicatedA.Revision == 2,
    "Dedicated placement carries exactly a typed slot and revision");
Check(dedicatedB.TenantId == tenantB && dedicatedB.DedicatedSlot == slotB,
    "Separate tenant has its own independent slot reference");
Check(slotA.Value != slotB.Value && slotA != slotB,
    "Distinct trusted dedicated slots remain distinct");
Check(dedicatedA.UpdatedAtUtc.Offset == TimeSpan.Zero,
    "Placement timestamp remains UTC");
Check(slotA.ToString() == "DedicatedStorageSlotId[redacted]",
    "Dedicated slot diagnostics redact identifiers");
Check(dedicatedA.ToString() == "TenantStoragePlacementSnapshot[redacted]",
    "Placement diagnostics redact identifiers");

Expect<InvalidOperationException>(
    () => DedicatedStorageSlotId.FromTrustedCatalog(Guid.Empty),
    "Empty dedicated slot rejected");
Expect<ArgumentNullException>(
    () => TenantStoragePlacementSnapshot.RestoreFromTrustedCatalog(
        null!, TenantPlacementMode.Shared, null, 1, now),
    "Null tenant rejected");
Expect<InvalidOperationException>(
    () => TenantStoragePlacementSnapshot.RestoreFromTrustedCatalog(
        tenantA, (TenantPlacementMode)99, null, 1, now),
    "Unknown enum rejected");
Expect<InvalidOperationException>(
    () => TenantStoragePlacementSnapshot.RestoreFromTrustedCatalog(
        tenantA, TenantPlacementMode.Shared, slotA, 1, now),
    "Shared cannot contain dedicated slot");
Expect<InvalidOperationException>(
    () => TenantStoragePlacementSnapshot.RestoreFromTrustedCatalog(
        tenantA, TenantPlacementMode.Dedicated, null, 1, now),
    "Dedicated requires slot");
Expect<InvalidOperationException>(
    () => TenantStoragePlacementSnapshot.RestoreFromTrustedCatalog(
        tenantA, TenantPlacementMode.Shared, null, 0, now),
    "Zero placement revision rejected");
Expect<InvalidOperationException>(
    () => TenantStoragePlacementSnapshot.RestoreFromTrustedCatalog(
        tenantA, TenantPlacementMode.Shared, null, -2, now),
    "Negative placement revision rejected");
Expect<InvalidOperationException>(
    () => TenantStoragePlacementSnapshot.RestoreFromTrustedCatalog(
        tenantA, TenantPlacementMode.Shared, null, 1, default),
    "Missing UTC timestamp rejected");
Expect<InvalidOperationException>(
    () => TenantStoragePlacementSnapshot.RestoreFromTrustedCatalog(
        tenantA, TenantPlacementMode.Shared, null, 1, now.ToOffset(TimeSpan.FromHours(-6))),
    "Non UTC offset rejected");

var authorizeA = new TestAuthorization(tenantA);
var storeA = new TestPlacementReader((_, _) => Task.FromResult<TenantStoragePlacementSnapshot?>(dedicatedA));
var resolverA = new ResolveCurrentTenantStorage(authorizeA, storeA);
var resultA = await resolverA.ExecuteAsync(CancellationToken.None);
Check(resultA == dedicatedA, "Authorized dedicated decision returns trusted catalog snapshot");
Check(authorizeA.Calls == 1 && storeA.Calls == 1,
    "Authorize then read exactly once");
Check(storeA.LastTenant == tenantA,
    "Reader receives server-authorized typed tenant, not user-provided string");

var sharedResolver = new ResolveCurrentTenantStorage(
    new TestAuthorization(tenantA),
    new TestPlacementReader((_, _) => Task.FromResult<TenantStoragePlacementSnapshot?>(sharedA)));
var sharedResult = await sharedResolver.ExecuteAsync(CancellationToken.None);
Check(sharedResult.Mode == TenantPlacementMode.Shared && sharedResult.DedicatedSlot is null,
    "Authorized shared decision never produces dedicated slot");
var otherResolver = new ResolveCurrentTenantStorage(
    new TestAuthorization(tenantB),
    new TestPlacementReader((_, _) => Task.FromResult<TenantStoragePlacementSnapshot?>(dedicatedB)));
Check((await otherResolver.ExecuteAsync(CancellationToken.None)).TenantId == tenantB,
    "Tenant B selects only its own trusted snapshot");

var deniedStore = new TestPlacementReader((_, _) => Task.FromResult<TenantStoragePlacementSnapshot?>(dedicatedA));
await ExpectAsync<UnauthorizedAccessException>(
    () => new ResolveCurrentTenantStorage(new TestAuthorization(null), deniedStore)
        .ExecuteAsync(CancellationToken.None),
    "Denied actor cannot resolve any placement");
Check(deniedStore.Calls == 0,
    "Denial happens before catalog I/O");
var forbiddenStore = new TestPlacementReader((_, _) => Task.FromResult<TenantStoragePlacementSnapshot?>(dedicatedB));
await ExpectAsync<InvalidOperationException>(
    () => new ResolveCurrentTenantStorage(new TestAuthorization(tenantA), forbiddenStore)
        .ExecuteAsync(CancellationToken.None),
    "Reader for tenant A returning tenant B rejected (no cross tenant)");
var missingStore = new TestPlacementReader((_, _) => Task.FromResult<TenantStoragePlacementSnapshot?>(null));
await ExpectAsync<InvalidOperationException>(
    () => new ResolveCurrentTenantStorage(new TestAuthorization(tenantA), missingStore)
        .ExecuteAsync(CancellationToken.None),
    "Unknown placement fails closed without shared fallback");
var brokenCatalog = new TestPlacementReader((_, _) =>
    Task.FromException<TenantStoragePlacementSnapshot?>(new InvalidOperationException("Catalog failed")));
await ExpectAsync<InvalidOperationException>(
    () => new ResolveCurrentTenantStorage(new TestAuthorization(tenantA), brokenCatalog)
        .ExecuteAsync(CancellationToken.None),
    "Catalog error does not enable fallback");
var authFailedStore = new TestPlacementReader((_, _) => Task.FromResult<TenantStoragePlacementSnapshot?>(sharedA));
await ExpectAsync<InvalidOperationException>(
    () => new ResolveCurrentTenantStorage(
        new TestAuthorization(tenantA) { Fail = true }, authFailedStore)
        .ExecuteAsync(CancellationToken.None),
    "Authorization failure rejects storage decision");
Check(authFailedStore.Calls == 0,
    "Authorization exception cannot call catalog");
Expect<ArgumentNullException>(
    () => new ResolveCurrentTenantStorage(null!, storeA),
    "No permissive missing authorization dependency");
Expect<ArgumentNullException>(
    () => new ResolveCurrentTenantStorage(authorizeA, null!),
    "No permissive missing catalog dependency");

using var cancelled = new CancellationTokenSource();
cancelled.Cancel();
var cancelledAuth = new TestAuthorization(tenantA);
var cancelledStore = new TestPlacementReader((_, _) => Task.FromResult<TenantStoragePlacementSnapshot?>(sharedA));
await ExpectAsync<OperationCanceledException>(
    () => new ResolveCurrentTenantStorage(cancelledAuth, cancelledStore)
        .ExecuteAsync(cancelled.Token),
    "Cancellation before authorization rejects access");
Check(cancelledAuth.Calls == 0 && cancelledStore.Calls == 0,
    "Pre-cancel never contacts authorization or catalog");
using var duringAuth = new CancellationTokenSource();
var afterAuthStore = new TestPlacementReader((_, _) => Task.FromResult<TenantStoragePlacementSnapshot?>(sharedA));
var cancellingAuth = new TestAuthorization(tenantA) { OnResolve = () => duringAuth.Cancel() };
await ExpectAsync<OperationCanceledException>(
    () => new ResolveCurrentTenantStorage(cancellingAuth, afterAuthStore)
        .ExecuteAsync(duringAuth.Token),
    "Cancellation during authorization rejects access");
Check(afterAuthStore.Calls == 0, "Auth cancellation prevents catalog read");
using var duringStorage = new CancellationTokenSource();
var cancellingStore = new TestPlacementReader((_, _) => Task.FromResult<TenantStoragePlacementSnapshot?>(sharedA))
    { OnFind = () => duringStorage.Cancel() };
await ExpectAsync<OperationCanceledException>(
    () => new ResolveCurrentTenantStorage(new TestAuthorization(tenantA), cancellingStore)
        .ExecuteAsync(duringStorage.Token),
    "Cancellation during placement read rejects return");

var input = typeof(ResolveCurrentTenantStorage)
    .GetMethod(nameof(ResolveCurrentTenantStorage.ExecuteAsync))!;
Check(input.GetParameters().Length == 1 &&
      input.GetParameters()[0].ParameterType == typeof(CancellationToken),
    "Use case accepts NO tenant, host, cookie, header or DSN argument");
var domainProperties = typeof(TenantStoragePlacementSnapshot)
    .GetProperties(BindingFlags.Instance | BindingFlags.Public);
Check(domainProperties.Length == 5 &&
      domainProperties.All(p => !p.CanWrite && p.PropertyType != typeof(string)),
    "Trusted placement has five read-only typed properties and NO string DSN/host");
Check(typeof(DedicatedStorageSlotId)
    .GetProperties(BindingFlags.Instance | BindingFlags.Public)
    .All(p => !p.CanWrite && p.PropertyType == typeof(Guid)),
    "Dedicated slot contains only an immutable opaque GUID");
Check(typeof(ICurrentTenantPlacementAuthorization)
    .GetMethods().Single().GetParameters().Single().ParameterType ==
      typeof(CancellationToken),
    "Placement authorization has no user-provided selector");
Check(typeof(ITrustedTenantPlacementReader)
    .GetMethods().Single().GetParameters().Select(p => p.ParameterType)
    .SequenceEqual(new[] { typeof(TenantId), typeof(CancellationToken) }),
    "Trusted catalog accepts only server-selected typed tenant and cancellation");
Check(typeof(TenantStoragePlacementSnapshot).Assembly.GetName().Name == "Crecisol.Domain" &&
      typeof(ResolveCurrentTenantStorage).Assembly.GetName().Name == "Crecisol.Application",
    "Domain/Application layers remain separate without infrastructure dependencies");

Console.WriteLine($"PASS C04-03 .NET10 placement contracts: {passed} positive and negative assertions; " +
    "shared/dedicated metadata, no DSN, authorization ordering and tenant mismatch. " +
    "No real actor authorization, database routing, API, DI or migrations certified.");

void Check(bool ok, string name)
{
    if (!ok) throw new Exception("FAIL C04-03: " + name);
    passed++;
}
void Expect<T>(Action action, string name) where T : Exception
{
    try { action(); }
    catch (T) { Check(true, name); return; }
    throw new Exception("FAIL C04-03 expected " + typeof(T).Name + ": " + name);
}
async Task ExpectAsync<T>(Func<Task> action, string name) where T : Exception
{
    try { await action(); }
    catch (T) { Check(true, name); return; }
    throw new Exception("FAIL C04-03 expected " + typeof(T).Name + ": " + name);
}

sealed class TestAuthorization(TenantId? tenant) : ICurrentTenantPlacementAuthorization
{
    public int Calls { get; private set; }
    public bool Fail { get; init; }
    public Action? OnResolve { get; init; }
    public ValueTask<TenantId?> ResolveAuthorizedCurrentTenantAsync(
        CancellationToken cancellationToken)
    {
        Calls++;
        OnResolve?.Invoke();
        if (Fail) throw new InvalidOperationException("Authorization provider unavailable");
        return ValueTask.FromResult(tenant);
    }
}

sealed class TestPlacementReader(
    Func<TenantId, CancellationToken, Task<TenantStoragePlacementSnapshot?>> load)
    : ITrustedTenantPlacementReader
{
    public int Calls { get; private set; }
    public TenantId? LastTenant { get; private set; }
    public Action? OnFind { get; init; }
    public Task<TenantStoragePlacementSnapshot?> FindPlacementAsync(
        TenantId authorizedTenantId, CancellationToken cancellationToken)
    {
        Calls++;
        LastTenant = authorizedTenantId;
        OnFind?.Invoke();
        return load(authorizedTenantId, cancellationToken);
    }
}
