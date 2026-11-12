using Crecisol.Api.Configuration;
using Crecisol.Application.Tenancy;
using Crecisol.Domain.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System.Reflection;

var checks = 0;
var tenantA = TenantId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
var tenantB = TenantId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
var hostA = CanonicalTenantHost.Parse("SHOP.Example.Test");
var hostB = CanonicalTenantHost.Parse("custom-owner.example.test");
var verifiedAt = new DateTimeOffset(2026, 11, 11, 14, 0, 0, TimeSpan.Zero);
var changedAt = verifiedAt.AddMinutes(10);
var now = verifiedAt.AddDays(1);
var expiry = verifiedAt.AddDays(7);

Check(hostA.Value == "shop.example.test", "Host is canonical ASCII lowercase");
Check(hostA == CanonicalTenantHost.Parse("shop.example.test"), "Host equality is DNS-case-insensitive after normalization");
Check(hostA != hostB, "Distinct host labels remain distinct");
Check(CanonicalTenantHost.Parse("sub-domain.shop.example.test").Value == "sub-domain.shop.example.test",
    "Multi-label subdomain with hyphen accepted");
Check(CanonicalTenantHost.Parse("xn--bcher-kva.example").Value == "xn--bcher-kva.example",
    "Punycode ASCII A-label syntax is possible but does not prove ownership");
Check(hostA.ToString() == "CanonicalTenantHost[redacted]", "Host diagnostics redact sensitive routing hint");

foreach (var attack in new string?[] {
    null, "", " ", "foo", "localhost", "shop.localhost", "127.0.0.1",
    "::1", "[::1]", "shop.example.test.", ".shop.example.test",
    "shop..example.test", " shop.example.test", "shop.example.test ",
    "shop.example.test\r\nX-Forwarded-Host:evil.example",
    "shop.example.test/path", "shop.example.test\\admin", "shop.example.test?tenant=b",
    "https://shop.example.test", "http://shop.example.test",
    "shop.example.test:443", "user@shop.example.test", "*.example.test",
    "foo_bar.example.test", "-shop.example.test", "shop-.example.test",
    "shop.-example.test", "shop.example.-test", "bücher.example",
    "shop.example.test%00", "shop.example.test,evil.example",
    "shop.example.test#fragment", "abc.123", "a." + new string('a', 64) + ".example",
    new string('a', 250) + ".test", "shop.example.test\0", "shop.example.test\t"
})
    Expect<InvalidOperationException>(() => CanonicalTenantHost.Parse(attack),
        "Unsafe DNS, encoded, ambiguous or unverified host rejected");

var verifiedA = Binding(tenantA, hostA, TenantAddressMode.Subdomain,
    HostVerificationState.Verified, 3, changedAt, verifiedAt, expiry);
var verifiedB = Binding(tenantB, hostB, TenantAddressMode.CustomDomain,
    HostVerificationState.Verified, 2, changedAt, verifiedAt, expiry);
Check(verifiedA.AddressMode == TenantAddressMode.Subdomain && verifiedA.Revision == 3,
    "Subdomain metadata restored only from trusted catalogue");
Check(verifiedB.AddressMode == TenantAddressMode.CustomDomain && verifiedB.TenantId == tenantB,
    "Custom-domain binding belongs to correct tenant");
Check(verifiedA.IsVerifiedAt(now), "Verified host is current inside its UTC window");
Check(verifiedA.IsVerifiedAt(changedAt), "Verified host becomes active at trusted catalogue publication time");
Check(!verifiedA.IsVerifiedAt(expiry), "Verification invalid at exact expiry instant");
Check(verifiedA.ToString() == "VerifiedTenantHostBinding[redacted]",
    "Binding diagnostics redact tenant, hostname and verification dates");
Check(Enum.GetValues<HostVerificationState>().Length == 4,
    "Exactly pending/verified/suspended/revoked lifecycle states");

var pending = Binding(tenantA, hostA, TenantAddressMode.Subdomain,
    HostVerificationState.PendingVerification, 1, changedAt, null, null);
var suspended = Binding(tenantA, hostA, TenantAddressMode.Subdomain,
    HostVerificationState.Suspended, 4, changedAt, verifiedAt, expiry);
var revoked = Binding(tenantA, hostA, TenantAddressMode.Subdomain,
    HostVerificationState.Revoked, 5, changedAt, verifiedAt, expiry);
Check(!pending.IsVerifiedAt(now), "Pending cannot authorize a routing hint");
Check(!suspended.IsVerifiedAt(now), "Suspended cannot authorize a routing hint");
Check(!revoked.IsVerifiedAt(now), "Revoked cannot authorize a routing hint");
Check(!verifiedA.IsVerifiedAt(verifiedAt.AddMinutes(1)),
    "Verification evidence cannot route before catalogue publication");
var futureAt = verifiedAt.AddDays(4);
var future = Binding(tenantA, hostA, TenantAddressMode.Subdomain,
    HostVerificationState.Verified, 6, futureAt, futureAt, futureAt.AddDays(7));
Check(!future.IsVerifiedAt(now), "Future verification cannot be used early");

Expect<ArgumentNullException>(() => Binding(null!, hostA, TenantAddressMode.Subdomain,
    HostVerificationState.Verified, 1, changedAt, verifiedAt, expiry), "Null tenant denied");
Expect<ArgumentNullException>(() => Binding(tenantA, null!, TenantAddressMode.Subdomain,
    HostVerificationState.Verified, 1, changedAt, verifiedAt, expiry), "Null host denied");
Expect<InvalidOperationException>(() => Binding(tenantA, hostA, TenantAddressMode.PathSlug,
    HostVerificationState.Verified, 1, changedAt, verifiedAt, expiry),
    "PathSlug cannot resolve tenant from a host alone");
Expect<InvalidOperationException>(() => Binding(tenantA, hostA, (TenantAddressMode)100,
    HostVerificationState.Verified, 1, changedAt, verifiedAt, expiry),
    "Unknown address mode denied");
Expect<InvalidOperationException>(() => Binding(tenantA, hostA, TenantAddressMode.Subdomain,
    (HostVerificationState)0, 1, changedAt, verifiedAt, expiry), "Unknown state denied");
Expect<InvalidOperationException>(() => Binding(tenantA, hostA, TenantAddressMode.Subdomain,
    HostVerificationState.Verified, 0, changedAt, verifiedAt, expiry), "Zero revision denied");
Expect<InvalidOperationException>(() => Binding(tenantA, hostA, TenantAddressMode.Subdomain,
    HostVerificationState.Verified, -1, changedAt, verifiedAt, expiry), "Negative revision denied");
Expect<InvalidOperationException>(() => Binding(tenantA, hostA, TenantAddressMode.Subdomain,
    HostVerificationState.Verified, 1, default, verifiedAt, expiry), "Missing UTC change denied");
Expect<InvalidOperationException>(() => Binding(tenantA, hostA, TenantAddressMode.Subdomain,
    HostVerificationState.Verified, 1, changedAt.ToOffset(TimeSpan.FromHours(-6)), verifiedAt, expiry),
    "Non-UTC change denied");
Expect<InvalidOperationException>(() => Binding(tenantA, hostA, TenantAddressMode.Subdomain,
    HostVerificationState.Verified, 1, changedAt, verifiedAt.ToOffset(TimeSpan.FromHours(-6)), expiry),
    "Non-UTC verifiedAt denied");
Expect<InvalidOperationException>(() => Binding(tenantA, hostA, TenantAddressMode.Subdomain,
    HostVerificationState.Verified, 1, changedAt, verifiedAt, expiry.ToOffset(TimeSpan.FromHours(-6))),
    "Non-UTC expiry denied");
Expect<InvalidOperationException>(() => Binding(tenantA, hostA, TenantAddressMode.Subdomain,
    HostVerificationState.Verified, 1, changedAt, null, expiry),
    "Expiry without verification denied");
Expect<InvalidOperationException>(() => Binding(tenantA, hostA, TenantAddressMode.Subdomain,
    HostVerificationState.Verified, 1, changedAt, verifiedAt, null),
    "Verified state without expiry denied");
Expect<InvalidOperationException>(() => Binding(tenantA, hostA, TenantAddressMode.Subdomain,
    HostVerificationState.Verified, 1, changedAt, verifiedAt.AddHours(2), expiry),
    "VerifiedAt after change timestamp denied");
Expect<InvalidOperationException>(() => Binding(tenantA, hostA, TenantAddressMode.Subdomain,
    HostVerificationState.Verified, 1, changedAt, verifiedAt, verifiedAt),
    "Zero verification lifetime denied");
Expect<InvalidOperationException>(() => Binding(tenantA, hostA, TenantAddressMode.Subdomain,
    HostVerificationState.PendingVerification, 1, changedAt, verifiedAt, expiry),
    "Pending state cannot silently carry verified evidence");
Expect<InvalidOperationException>(() => verifiedA.IsVerifiedAt(now.ToOffset(TimeSpan.FromHours(-6))),
    "Non-UTC runtime clock rejected");
Expect<InvalidOperationException>(() => verifiedA.IsVerifiedAt(default),
    "Missing runtime clock rejected");

// The application lookup is a strictly contextual HINT, never authorization.
var catalog = new FakeCatalog((_, _) => Task.FromResult<IReadOnlyList<VerifiedTenantHostBinding>?>(
    new[] { verifiedA }));
var clock = new FakeClock(now);
var resolver = new ResolveVerifiedTenantHost(catalog, clock);
var hintA = await resolver.ResolveAsync(hostA, CancellationToken.None);
Check(hintA.TenantId == tenantA && hintA.AddressMode == TenantAddressMode.Subdomain &&
      hintA.Revision == 3, "A valid host yields only tenant A contextual hint");
Check(hintA.ToString() == "VerifiedTenantHostHint[redacted]",
    "Hint never reveals tenant or host in logs");
Check(catalog.Calls == 1 && clock.Calls == 1 && catalog.LastHost == hostA,
    "One exact trusted lookup and one backend-owned UTC clock");
var hintB = await new ResolveVerifiedTenantHost(
    new FakeCatalog((_, _) => Task.FromResult<IReadOnlyList<VerifiedTenantHostBinding>?>(
        new[] { verifiedB })),
    new FakeClock(now)).ResolveAsync(hostB, CancellationToken.None);
Check(hintB.TenantId == tenantB && hintB.AddressMode == TenantAddressMode.CustomDomain,
    "Tenant B custom domain resolves only B metadata");

async Task DenyBindings(string label, IReadOnlyList<VerifiedTenantHostBinding>? bindings)
{
    await ExpectAsync<UnauthorizedAccessException>(() => new ResolveVerifiedTenantHost(
        new FakeCatalog((_, _) => Task.FromResult(bindings)), new FakeClock(now))
        .ResolveAsync(hostA, CancellationToken.None), label);
}
await DenyBindings("No matching host cannot fall back to platform tenant", Array.Empty<VerifiedTenantHostBinding>());
await DenyBindings("Null catalogue response denied", null);
await DenyBindings("Two tenants own same host: ambiguity denied", new[] {
    verifiedA,
    Binding(tenantB, hostA, TenantAddressMode.CustomDomain,
        HostVerificationState.Verified, 1, changedAt, verifiedAt, expiry)
});
await DenyBindings("Same-tenant duplicate host ownership also denied", new[] { verifiedA, verifiedA });
await DenyBindings("Pending host cannot resolve", new[] { pending });
await DenyBindings("Suspended host cannot resolve", new[] { suspended });
await DenyBindings("Revoked host cannot resolve", new[] { revoked });
await DenyBindings("Expired verified host cannot resolve",
    new[] { Binding(tenantA, hostA, TenantAddressMode.Subdomain,
        HostVerificationState.Verified, 4, changedAt, verifiedAt, now) });
await DenyBindings("Future verified host cannot resolve", new[] { future });

await ExpectAsync<InvalidOperationException>(() =>
    new ResolveVerifiedTenantHost(
        new FakeCatalog((_, _) => Task.FromResult<IReadOnlyList<VerifiedTenantHostBinding>?>(
            new[] { verifiedB })), new FakeClock(now))
        .ResolveAsync(hostA, CancellationToken.None),
    "Host A lookup never accepts catalog entry for host B");
await ExpectAsync<InvalidOperationException>(() =>
    new ResolveVerifiedTenantHost(
        new FakeCatalog((_, _) => Task.FromException<IReadOnlyList<VerifiedTenantHostBinding>?>(
            new InvalidOperationException("Catalog unavailable"))), new FakeClock(now))
        .ResolveAsync(hostA, CancellationToken.None),
    "Catalog outage must deny, not use unverified fallback");
await ExpectAsync<InvalidOperationException>(() =>
    new ResolveVerifiedTenantHost(
        new FakeCatalog((_, _) => Task.FromResult<IReadOnlyList<VerifiedTenantHostBinding>?>(
            new[] { verifiedA })), new FakeClock(now.ToOffset(TimeSpan.FromHours(-6))))
        .ResolveAsync(hostA, CancellationToken.None),
    "Backend clock cannot run in non-UTC mode");
Expect<ArgumentNullException>(() => new ResolveVerifiedTenantHost(null!, clock),
    "No permissive null catalogue");
Expect<ArgumentNullException>(() => new ResolveVerifiedTenantHost(catalog, null!),
    "No permissive null clock");
await ExpectAsync<ArgumentNullException>(() =>
    resolver.ResolveAsync(null!, CancellationToken.None), "Null lookup host rejected");

using (var cancelled = new CancellationTokenSource())
{
    cancelled.Cancel();
    var untouchedCatalog = new FakeCatalog((_, _) =>
        Task.FromResult<IReadOnlyList<VerifiedTenantHostBinding>?>(new[] { verifiedA }));
    await ExpectAsync<OperationCanceledException>(() => new ResolveVerifiedTenantHost(
        untouchedCatalog, new FakeClock(now)).ResolveAsync(hostA, cancelled.Token),
        "Cancelled request rejected before catalog");
    Check(untouchedCatalog.Calls == 0, "Cancelled request never calls catalogue");
}
using (var cancelledDuring = new CancellationTokenSource())
{
    var cancelCatalog = new FakeCatalog((_, _) =>
        Task.FromResult<IReadOnlyList<VerifiedTenantHostBinding>?>(new[] { verifiedA }))
        { OnLookup = () => cancelledDuring.Cancel() };
    var postCancelClock = new FakeClock(now);
    await ExpectAsync<OperationCanceledException>(() =>
        new ResolveVerifiedTenantHost(cancelCatalog, postCancelClock)
            .ResolveAsync(hostA, cancelledDuring.Token),
        "Cancellation during catalogue lookup rejects hint");
    Check(postCancelClock.Calls == 0, "Cancelled catalog never reads clock");
}
using (var cancelledClock = new CancellationTokenSource())
{
    var clockCancels = new FakeClock(now) { OnClock = () => cancelledClock.Cancel() };
    await ExpectAsync<OperationCanceledException>(() =>
        new ResolveVerifiedTenantHost(
            new FakeCatalog((_, _) => Task.FromResult<IReadOnlyList<VerifiedTenantHostBinding>?>(
                new[] { verifiedA })), clockCancels).ResolveAsync(hostA, cancelledClock.Token),
        "Cancellation after UTC clock read never returns a hint");
}

HttpRequest RequestWithHost(string? raw)
{
    var context = new DefaultHttpContext();
    if (raw is not null) context.Request.Headers.Host = new StringValues(raw);
    return context.Request;
}

bool AcceptDirect(string host, string expected)
{
    var ok = StrictVerifiedHostTransport.TryReadDirectHost(
        RequestWithHost(host), out var parsed);
    return ok && parsed is not null && parsed.Value == expected;
}

Check(AcceptDirect("Shop.Example.Test", "shop.example.test"),
    "ASP.NET direct Host header resolves canonical ASCII name");
Check(AcceptDirect("custom-owner.example.test", "custom-owner.example.test"),
    "Direct custom domain without forwarding headers accepted");

foreach (var attack in new string?[] {
    null, "", "shop.example.test:443", "https://shop.example.test",
    "shop.example.test/", "shop.example.test?tenant=B", "127.0.0.1",
    "shop.example.test,evil.example", "evil.example\r\nX-Forwarded-Host:shop.example.test",
    "shop..example.test", "shop.example.test.", "bücher.example"
})
{
    var ok = StrictVerifiedHostTransport.TryReadDirectHost(
        RequestWithHost(attack), out var returned);
    Check(!ok && returned is null, "Malformed direct Host rejected with no context hint");
}
var multiHost = RequestWithHost(null);
multiHost.Headers.Host = new StringValues(new[] { "shop.example.test", "evil.example.test" });
Check(!StrictVerifiedHostTransport.TryReadDirectHost(multiHost, out var duplicate) &&
      duplicate is null, "Duplicate Host headers denied even if first is allowed");

foreach (var forwarded in new [] {
    "Forwarded", "X-Forwarded-Host", "X-Forwarded-Proto",
    "X-Forwarded-Port", "X-Forwarded-For", "X-Forwarded-Server",
    "X-Forwarded-Prefix", "X-Original-Host", "X-Original-URL",
    "X-Rewrite-URL", "x-forwarded-anything"
})
{
    var request = RequestWithHost("shop.example.test");
    request.Headers[forwarded] = "evil.example.test";
    Check(!StrictVerifiedHostTransport.TryReadDirectHost(request, out var rejected) &&
          rejected is null, "Forged proxy and rewrite header rejected: " + forwarded);
}
Expect<ArgumentNullException>(() =>
    StrictVerifiedHostTransport.TryReadDirectHost(null!, out _),
    "Null HTTP request rejected");

var resolverSignature = typeof(ResolveVerifiedTenantHost)
    .GetMethod(nameof(ResolveVerifiedTenantHost.ResolveAsync))!;
Check(resolverSignature.GetParameters().Select(p => p.ParameterType).SequenceEqual(
        new[] { typeof(CanonicalTenantHost), typeof(CancellationToken) }),
    "Resolver accepts ONLY canonical host hint + cancellation, no DSN or client role");
Check(typeof(ITrustedVerifiedHostCatalog).GetMethods().Single()
      .GetParameters().Select(p => p.ParameterType)
      .SequenceEqual(new[] { typeof(CanonicalTenantHost), typeof(CancellationToken) }),
    "Trusted catalogue reads only typed host; no client headers/DSN");
Check(typeof(VerifiedTenantHostHint).GetProperties(BindingFlags.Instance |
      BindingFlags.Public).All(p => p.PropertyType != typeof(string)),
    "Hint has no host string, URL, DSN or secret field");
Check(typeof(VerifiedTenantHostBinding).Assembly.GetName().Name == "Crecisol.Domain" &&
      typeof(ResolveVerifiedTenantHost).Assembly.GetName().Name == "Crecisol.Application" &&
      typeof(StrictVerifiedHostTransport).Assembly.GetName().Name == "Crecisol.Api",
    "Domain, Application and API adapter remain separate layers");

Console.WriteLine($"PASS C04-04 .NET 10 verified-host boundary: {checks} positive/negative checks; " +
    "exact DNS, lifecycle, expiry, ambiguity, forged Forwarded, cancellation. " +
    "NO real DNS ownership verification, private auth, DI, routing or deployment certified.");

VerifiedTenantHostBinding Binding(
    TenantId id, CanonicalTenantHost host, TenantAddressMode mode,
    HostVerificationState state, long revision, DateTimeOffset changed,
    DateTimeOffset? at, DateTimeOffset? until) =>
    VerifiedTenantHostBinding.RestoreFromTrustedCatalog(
        id, host, mode, state, revision, changed, at, until);

void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL C04-04: " + name);
    checks++;
}
void Expect<T>(Action action, string name) where T : Exception
{
    try { action(); }
    catch (T) { Check(true, name); return; }
    throw new Exception("FAIL C04-04 expected " + typeof(T).Name + ": " + name);
}
async Task ExpectAsync<T>(Func<Task> action, string name) where T : Exception
{
    try { await action(); }
    catch (T) { Check(true, name); return; }
    throw new Exception("FAIL C04-04 expected " + typeof(T).Name + ": " + name);
}

sealed class FakeCatalog(
    Func<CanonicalTenantHost, CancellationToken,
        Task<IReadOnlyList<VerifiedTenantHostBinding>?>> find) : ITrustedVerifiedHostCatalog
{
    public int Calls { get; private set; }
    public CanonicalTenantHost? LastHost { get; private set; }
    public Action? OnLookup { get; init; }
    public Task<IReadOnlyList<VerifiedTenantHostBinding>?> FindExactBindingsAsync(
        CanonicalTenantHost host, CancellationToken token)
    {
        Calls++;
        LastHost = host;
        OnLookup?.Invoke();
        return find(host, token);
    }
}
sealed class FakeClock(DateTimeOffset value) : ITrustedHostUtcClock
{
    public int Calls { get; private set; }
    public Action? OnClock { get; init; }
    public DateTimeOffset GetUtcNow()
    {
        Calls++;
        OnClock?.Invoke();
        return value;
    }
}
