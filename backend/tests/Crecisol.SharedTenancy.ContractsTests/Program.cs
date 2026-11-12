using Crecisol.Application.Tenancy;
using Crecisol.Domain.Tenancy;
using Crecisol.Infrastructure.Tenancy;

var checks = 0;
var tenantA = TenantId.From(Guid.Parse("11111111-1111-1111-1111-111111111111"));
var tenantB = TenantId.From(Guid.Parse("22222222-2222-2222-2222-222222222222"));
var branch = Guid.Parse("33333333-3333-3333-3333-333333333333");
var item = Guid.Parse("55555555-5555-5555-5555-555555555555");
var onlyA = Guid.Parse("77777777-7777-7777-7777-777777777777");
var otherBranch = Guid.Parse("88888888-8888-8888-8888-888888888888");
var scopeA = AuthorizedSharedReadScope.FromVerifiedBackendDecision(tenantA, branch);
var scopeB = AuthorizedSharedReadScope.FromVerifiedBackendDecision(tenantB, branch);
var value = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff");

Check(Convert.ToHexString(MySqlRfc4122Guid.Encode(value)) ==
    "00112233445566778899AABBCCDDEEFF",
    "RFC 4122 GUID storage vector is in network order, not mixed-endian");
Check(MySqlRfc4122Guid.Decode(MySqlRfc4122Guid.Encode(value)) == value,
    "GUID binary round-trip preserves canonical UUID");
Check(MySqlRfc4122Guid.Decode(MySqlRfc4122Guid.Encode(branch)) == branch,
    "Repeated-byte branch UUID round-trip");
Check(MySqlRfc4122Guid.Decode(MySqlRfc4122Guid.Encode(tenantA.Value)) ==
    tenantA.Value, "Tenant UUID round-trip");
Expect<InvalidOperationException>(() => MySqlRfc4122Guid.Encode(Guid.Empty),
    "Cannot write zero UUID");
Expect<InvalidOperationException>(() => MySqlRfc4122Guid.Decode(new byte[16]),
    "Cannot read zero UUID");
Expect<InvalidOperationException>(() => MySqlRfc4122Guid.Decode(new byte[15]),
    "Cannot read truncated UUID");
Expect<InvalidOperationException>(() => MySqlRfc4122Guid.Decode(new byte[17]),
    "Cannot read extended UUID");
Expect<ArgumentNullException>(() => MySqlRfc4122Guid.Decode(null!),
    "Cannot read null UUID bytes");
Expect<ArgumentNullException>(
    () => AuthorizedSharedReadScope.FromVerifiedBackendDecision(null!, branch),
    "A scope requires a typed tenant");
Expect<InvalidOperationException>(
    () => AuthorizedSharedReadScope.FromVerifiedBackendDecision(tenantA, Guid.Empty),
    "A scope requires a non-zero branch");
Check(scopeA.ToString() == "AuthorizedSharedReadScope[redacted]",
    "Authenticated scope diagnostic never exposes identifiers");
Check(typeof(IAuthorizedSharedReadScopeResolver).GetMethods().Length == 1 &&
      typeof(IAuthorizedSharedReadScopeResolver).GetMethods()[0]
          .GetParameters().Length == 1,
    "Resolver only accepts cancellation: no client tenant ID, branch or caller role");
Check(typeof(ReadSharedTenantResource).Assembly.GetName().Name ==
    "Crecisol.Application",
    "Security ordering remains in Application");
Check(typeof(MySqlSharedTenantResourceReader).Assembly.GetName().Name ==
    "Crecisol.Infrastructure",
    "SQL adapter only in Infrastructure");

var authorizedReader = new FakeReader((_, id) =>
    new SharedResourceReadResult(tenantA, branch, id, "a-resource"));
var resolverA = new FakeResolver(scopeA);
var useCase = new ReadSharedTenantResource(resolverA, authorizedReader);
var authorized = await useCase.ExecuteAsync(item, CancellationToken.None);
Check(authorized?.TenantId == tenantA && authorized?.ExternalKey == "a-resource",
    "Authorized actor can read a scope-matching resource");
Check(resolverA.Calls == 1 && authorizedReader.Calls == 1,
    "Authorizer runs before a single storage read");
Check(authorizedReader.LastScope == scopeA,
    "Only scope minted by backend authorization reaches SQL reader");
Check(authorized?.ToString() == "SharedResourceReadResult[redacted]",
    "Private resource diagnostic redacts identifiers and data");

var denyResolver = new FakeResolver(null);
var denyReader = new FakeReader((_, _) =>
    new SharedResourceReadResult(tenantA, branch, item, "private"));
await ExpectAsync<UnauthorizedAccessException>(
    () => new ReadSharedTenantResource(denyResolver, denyReader)
        .ExecuteAsync(item, CancellationToken.None),
    "Unidentified/unapproved actor is denied");
Check(denyReader.Calls == 0, "Denied policy never touches SQL reader");
var foreignTenantReader = new FakeReader((_, id) =>
    new SharedResourceReadResult(tenantB, branch, id, "b-secret"));
await ExpectAsync<InvalidOperationException>(
    () => new ReadSharedTenantResource(new FakeResolver(scopeA), foreignTenantReader)
        .ExecuteAsync(item, CancellationToken.None),
    "Forged foreign tenant row rejected after storage");
var foreignBranchReader = new FakeReader((_, id) =>
    new SharedResourceReadResult(tenantA, otherBranch, id, "wrong-branch"));
await ExpectAsync<InvalidOperationException>(
    () => new ReadSharedTenantResource(new FakeResolver(scopeA), foreignBranchReader)
        .ExecuteAsync(item, CancellationToken.None),
    "Wrong branch row rejected after storage");
var foreignResourceReader = new FakeReader((_, _) =>
    new SharedResourceReadResult(tenantA, branch, onlyA, "wrong-resource"));
await ExpectAsync<InvalidOperationException>(
    () => new ReadSharedTenantResource(new FakeResolver(scopeA), foreignResourceReader)
        .ExecuteAsync(item, CancellationToken.None),
    "Wrong resource row rejected after storage");
Check(await new ReadSharedTenantResource(new FakeResolver(scopeA),
        new FakeReader((_, _) => null)).ExecuteAsync(item, CancellationToken.None)
        is null, "Missing row does not fall back to another tenant");

await ExpectAsync<InvalidOperationException>(
    () => useCase.ExecuteAsync(Guid.Empty, CancellationToken.None),
    "No read for zero resource ID");
Expect<ArgumentNullException>(() => new ReadSharedTenantResource(null!, authorizedReader),
    "Null authorizer forbidden");
Expect<ArgumentNullException>(() => new ReadSharedTenantResource(resolverA, null!),
    "Null reader forbidden");
using var cancelled = new CancellationTokenSource();
cancelled.Cancel();
var cancelledAuth = new FakeResolver(scopeA);
var cancelledReader = new FakeReader((_, _) => null);
await ExpectAsync<OperationCanceledException>(
    () => new ReadSharedTenantResource(cancelledAuth, cancelledReader)
        .ExecuteAsync(item, cancelled.Token),
    "Cancellation before auth stops immediately");
Check(cancelledAuth.Calls == 0 && cancelledReader.Calls == 0,
    "Cancelled request performs no authorization or SQL");
using var duringAuth = new CancellationTokenSource();
var authCancel = new FakeResolver(scopeA) { OnResolve = () => duringAuth.Cancel() };
var afterAuthReader = new FakeReader((_, _) => null);
await ExpectAsync<OperationCanceledException>(
    () => new ReadSharedTenantResource(authCancel, afterAuthReader)
        .ExecuteAsync(item, duringAuth.Token),
    "Cancelled auth cannot reach storage");
Check(afterAuthReader.Calls == 0, "Reader untouched on auth cancellation");
using var duringRead = new CancellationTokenSource();
var readerCancel = new FakeReader((_, _) => null) { OnRead = () => duringRead.Cancel() };
await ExpectAsync<OperationCanceledException>(
    () => new ReadSharedTenantResource(new FakeResolver(scopeA), readerCancel)
        .ExecuteAsync(item, duringRead.Token),
    "Cancelled storage result never escapes");
var brokenAuthReader = new FakeReader((_, _) => null);
await ExpectAsync<InvalidOperationException>(
    () => new ReadSharedTenantResource(
        new FakeResolver(scopeA) { Throw = true }, brokenAuthReader)
        .ExecuteAsync(item, CancellationToken.None),
    "Failed authorization cannot fail open");
Check(brokenAuthReader.Calls == 0, "Auth error never touches DB");
await ExpectAsync<InvalidOperationException>(
    () => new ReadSharedTenantResource(new FakeResolver(scopeA),
        new FakeReader((_, _) => null) { Throw = true })
        .ExecuteAsync(item, CancellationToken.None),
    "Failed storage never falls back to unscoped query");

await ExpectAsync<InvalidOperationException>(
    () => new MySqlSharedTenantResourceReader(null)
        .FindAsync(scopeA, item, CancellationToken.None),
    "No trusted DB configuration fails closed");
await ExpectAsync<InvalidOperationException>(
    () => new MySqlSharedTenantResourceReader(" ")
        .FindAsync(scopeA, item, CancellationToken.None),
    "Blank trusted DB configuration fails closed");

if (args.Length == 1 && args[0] == "db")
{
    var dsn = Environment.GetEnvironmentVariable("CRECISOL_C04_02_TEST_DSN");
    if (string.IsNullOrWhiteSpace(dsn))
        throw new InvalidOperationException("Disposable CI DSN required.");
    var real = new MySqlSharedTenantResourceReader(dsn);
    var a = await new ReadSharedTenantResource(new FakeResolver(scopeA), real)
        .ExecuteAsync(item, CancellationToken.None);
    Check(a?.ExternalKey == "a-shared" && a?.TenantId == tenantA,
        "SQL A scoped read returns A data");
    var b = await new ReadSharedTenantResource(new FakeResolver(scopeB), real)
        .ExecuteAsync(item, CancellationToken.None);
    Check(b?.ExternalKey == "b-shared" && b?.TenantId == tenantB,
        "SQL B scoped read returns B data for same branch/resource IDs");
    var noB = await new ReadSharedTenantResource(new FakeResolver(scopeB), real)
        .ExecuteAsync(onlyA, CancellationToken.None);
    Check(noB is null, "B cannot see A-only resource even with guessed UUID");
    var noBranch = await new ReadSharedTenantResource(new FakeResolver(scopeA), real)
        .ExecuteAsync(Guid.Parse("66666666-6666-6666-6666-666666666666"),
            CancellationToken.None);
    Check(noBranch is null, "A cannot access another branch with guessed UUID");
    var inOtherBranch = AuthorizedSharedReadScope.FromVerifiedBackendDecision(
        tenantA, otherBranch);
    var other = await new ReadSharedTenantResource(
        new FakeResolver(inOtherBranch), real)
        .ExecuteAsync(Guid.Parse("66666666-6666-6666-6666-666666666666"),
            CancellationToken.None);
    Check(other?.ExternalKey == "other-branch",
        "Authorized alternate A branch reads only its own row");
    // No actual actor membership resolver exists: cannot claim HTTP/BOLA
    // authorization until C04-05/06. Fake policy here tests fail-closed ordering.
}
else if (args.Length != 0)
    throw new InvalidOperationException("Usage: [db]");

Console.WriteLine($"PASS C04-02 SharedTenancy .NET 10: {checks} assertions; " +
    "authorization ordering, RFC4122, rejection and scoped read. " +
    "No API, real identity resolver or provisioning certified.");
return;

void Check(bool condition, string reason)
{
    if (!condition) throw new Exception("FAIL C04-02: " + reason);
    checks++;
}
void Expect<T>(Action action, string reason) where T : Exception
{
    try { action(); }
    catch (T) { Check(true, reason); return; }
    throw new Exception("FAIL C04-02: expected " + typeof(T).Name + ": " + reason);
}
async Task ExpectAsync<T>(Func<Task> action, string reason) where T : Exception
{
    try { await action(); }
    catch (T) { Check(true, reason); return; }
    throw new Exception("FAIL C04-02: expected " + typeof(T).Name + ": " + reason);
}
sealed class FakeResolver(AuthorizedSharedReadScope? scope)
    : IAuthorizedSharedReadScopeResolver
{
    public int Calls { get; private set; }
    public Action? OnResolve { get; init; }
    public bool Throw { get; init; }
    public ValueTask<AuthorizedSharedReadScope?> ResolveForCurrentActorAsync(
        CancellationToken cancellationToken)
    {
        Calls++;
        OnResolve?.Invoke();
        if (Throw) throw new InvalidOperationException("Policy unavailable");
        return ValueTask.FromResult(scope);
    }
}
sealed class FakeReader(
    Func<AuthorizedSharedReadScope, Guid, SharedResourceReadResult?> lookup)
    : ISharedTenantResourceReader
{
    public int Calls { get; private set; }
    public AuthorizedSharedReadScope? LastScope { get; private set; }
    public Action? OnRead { get; init; }
    public bool Throw { get; init; }
    public Task<SharedResourceReadResult?> FindAsync(
        AuthorizedSharedReadScope scope, Guid id, CancellationToken ct)
    {
        Calls++;
        LastScope = scope;
        OnRead?.Invoke();
        if (Throw) throw new InvalidOperationException("Storage unavailable");
        return Task.FromResult(lookup(scope,id));
    }
}
