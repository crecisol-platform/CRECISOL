using Crecisol.Application.Authorization;
using Crecisol.Application.Tenancy;
using Crecisol.Domain.Authorization;
using Crecisol.Domain.Tenancy;

var checks = 0;
var now = new DateTimeOffset(2026, 11, 24, 18, 0, 0, TimeSpan.Zero);
var tenantA = TenantId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
var tenantB = TenantId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
var actorA = TenantActorId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01"));
var actorB = TenantActorId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb02"));
var branchA = TenantBranchId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa11"));
var branchB = TenantBranchId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb12"));
var branchAltA = TenantBranchId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa12"));
var actionRead = TenantRequiredAction.FromServerPolicy("cases.read");
var actionExport = TenantRequiredAction.FromServerPolicy("cases.export");
var roleId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa21");
var memberId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa31");

TenantIdentityContextEvidence Verified(TenantId? tenant = null, TenantActorId? actor = null)
{
    var t = tenant ?? tenantA;
    var a = actor ?? actorA;
    return TenantIdentityContextEvidence.FromVerifiedInternalEvidence(
        TrustedActorSessionSnapshot.RestoreFromTrustedIdentityProvider(
            a, ActorSessionState.Active, 7, now.AddDays(-2),
            now.AddDays(-1), now.AddDays(1)),
        TenantActorMembershipSnapshot.RestoreFromTrustedMembershipCatalog(
            a, t, TenantMembershipState.Active, 3,
            now.AddDays(-1), now.AddDays(-2), now.AddDays(4)),
        new VerifiedTenantHostHint(t, TenantAddressMode.Subdomain, 9),
        TenantCatalogSnapshot.RestoreFromTrustedCatalog(
            t, "tenant-internal", "Tenant display",
            TenantLifecycleState.Active, TenantAddressMode.Subdomain,
            12, now.AddDays(-1)));
}

C06PreparedRoleSnapshot Row(
    TenantId? tenant = null, TenantActorId? actor = null,
    TenantBranchId? branch = null, TenantRequiredAction? action = null,
    Guid? role = null, Guid? membership = null, string roleCode = "tenant-viewer",
    bool membershipActive = true, bool branchActive = true,
    bool roleActive = true, bool revoked = false,
    long roleRevision = 2, long assignmentRevision = 4,
    DateTimeOffset? from = null, DateTimeOffset? until = null,
    bool actionGrantable = false) =>
    C06PreparedRoleSnapshot.RestoreFromTrustedStore(
        tenant ?? tenantA, actor ?? actorA, branch ?? branchA,
        membership ?? memberId, role ?? roleId, roleCode, action ?? actionRead,
        membershipActive, branchActive, roleActive, revoked,
        roleRevision, assignmentRevision,
        from ?? now.AddMinutes(-20), until ?? now.AddHours(1),
        actionGrantable);

void Check(bool condition, string title)
{
    if (!condition) throw new Exception("C06-02 FAIL: " + title);
    checks++;
    Console.WriteLine("PASS C06-02 " + title);
}
void Reject(Action action, string title)
{
    try { action(); } catch (Exception e) when (e is InvalidOperationException or UnauthorizedAccessException or ArgumentNullException)
    { Check(true, title); return; }
    throw new Exception("C06-02 FAIL unexpected acceptance: " + title);
}
async Task RejectAsync(Func<Task> run, string title)
{
    try { await run(); } catch (Exception e) when (e is InvalidOperationException or UnauthorizedAccessException or OperationCanceledException)
    { Check(true, title); return; }
    throw new Exception("C06-02 FAIL unexpected acceptance: " + title);
}
async Task<C06PreparedRoleInspection> Inspect(
    IReadOnlyList<C06PreparedRoleSnapshot>? rows, bool errorClock = false,
    TenantIdentityContextEvidence? identity = null,
    TenantBranchId? branch = null, TenantRequiredAction? action = null)
{
    var reader = new MemoryReader(rows);
    var service = new InspectPreparedTenantRoles(reader,
        new Clock(errorClock ? now.ToOffset(TimeSpan.FromHours(-6)) : now));
    return await service.ExecuteAsync(identity ?? Verified(), branch ?? branchA,
        action ?? actionRead, CancellationToken.None);
}

var good = Row();
Check(good.IsCurrentCandidateFor(tenantA, actorA, branchA, actionRead, now),
    "candidate within exact tenant/actor/branch/action and UTC window");
Check(!good.IsCurrentCandidateFor(tenantB, actorA, branchA, actionRead, now),
    "tenant B cannot inherit tenant A role");
Check(!good.IsCurrentCandidateFor(tenantA, actorB, branchA, actionRead, now),
    "actor B cannot use actor A membership");
Check(!good.IsCurrentCandidateFor(tenantA, actorA, branchB, actionRead, now),
    "foreign branch not inherited");
var manager = Row(branch: branchAltA, roleCode: "tenant-branch-manager");
Check(manager.IsCurrentCandidateFor(tenantA, actorA, branchAltA, actionRead, now),
    "branch manager candidate valid only for its assigned branch");
Check(!manager.IsCurrentCandidateFor(tenantA, actorA, branchA, actionRead, now),
    "branch manager candidate not inherited by a second branch");
Check(!manager.IsCurrentCandidateFor(tenantB, actorA, branchAltA, actionRead, now),
    "branch manager candidate not inherited by a second tenant");
Check(!good.IsCurrentCandidateFor(tenantA, actorA, branchA, actionExport, now),
    "separate export action not inherited");
Check(!good.IsCurrentCandidateFor(tenantA, actorA, branchA, actionRead, now.AddHours(1)),
    "expired exactly at boundary");
Check(good.ToString().Contains("redacted", StringComparison.Ordinal) &&
    !good.ToString().Contains("aaaaaaaa", StringComparison.Ordinal),
    "no identity leakage via ToString");
Reject(() => Row(actionGrantable: true), "future capability cannot become grantable");
Reject(() => Row(roleCode: "superadmin*"), "wildcard role rejected");
Reject(() => Row(roleCode: "PLATFORM"), "realm-confusing role rejected");
Reject(() => Row(role: Guid.Empty), "empty role id rejected");
Reject(() => Row(membership: Guid.Empty), "empty membership id rejected");
Reject(() => Row(roleRevision: 0), "missing role revision rejected");
Reject(() => Row(assignmentRevision: 0), "missing assignment revision rejected");
Reject(() => Row(until: now.AddMinutes(-20)), "zero-length window rejected");
Reject(() => Row(from: now.ToOffset(TimeSpan.FromHours(-6))), "non-UTC validity rejected");
Reject(() => good.IsCurrentCandidateFor(tenantA, actorA, branchA, actionRead,
    now.ToOffset(TimeSpan.FromHours(-6))), "untrusted clock rejected");

var ok = await Inspect(new[] { good });
Check(ok.CandidateCount == 1 && !ok.AuthorizesPrivateResource,
    "one scoped candidate is NOT an authorization grant");
Check(ok.ToString().Contains("not_authorization", StringComparison.Ordinal),
    "inspection is explicitly non-authorizing");
Check((await Inspect(Array.Empty<C06PreparedRoleSnapshot>())).CandidateCount == 0,
    "empty role catalog denies by default");
Check((await Inspect(new[] { Row(revoked: true) })).CandidateCount == 0,
    "revoked assignment is not current");
Check((await Inspect(new[] { Row(membershipActive: false) })).CandidateCount == 0,
    "inactive membership is not current");
Check((await Inspect(new[] { Row(roleActive: false) })).CandidateCount == 0,
    "inactive role is not current");
Check((await Inspect(new[] { Row(branchActive: false) })).CandidateCount == 0,
    "inactive branch is not current");
Check((await Inspect(new[] { Row(from: now.AddMinutes(1)) })).CandidateCount == 0,
    "future assignment is not current");
Check((await Inspect(new[] { Row(until: now) })).CandidateCount == 0,
    "expired assignment is not current");
Check((await Inspect(new[] {
    good, Row(role: Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa22"), roleCode: "tenant-admin")
})).CandidateCount == 2, "different explicit roles can coexist");
await RejectAsync(async () => { await Inspect(new[] { good, good }); },
    "duplicate role/membership rows fail closed");
await RejectAsync(async () => { await Inspect(null); },
    "unavailable role store fails closed");
await RejectAsync(async () => { await Inspect(new[] { Row(tenant: tenantB) }); },
    "foreign-tenant row fails closed even from a trusted port");
await RejectAsync(async () => { await Inspect(new[] { Row(actor: actorB) }); },
    "foreign-actor row fails closed");
await RejectAsync(async () => { await Inspect(new[] { Row(branch: branchB) }); },
    "foreign-branch row fails closed");
await RejectAsync(async () => { await Inspect(new[] { Row(action: actionExport) }); },
    "action substitution fails closed");
await RejectAsync(async () => { await Inspect(new[] { Row(revoked: true, tenant: tenantB) }); },
    "inactive foreign row must also fail closed");
await RejectAsync(async () => { await Inspect(new[] { good }, errorClock: true); },
    "non-UTC backend clock rejected");
await RejectAsync(async () => { await Inspect(Enumerable.Repeat(good, 129).ToArray()); },
    "unbounded candidate pages rejected");
var cancelled = new CancellationTokenSource();
cancelled.Cancel();
await RejectAsync(async () => {
    var service = new InspectPreparedTenantRoles(new MemoryReader(new[] { good }), new Clock(now));
    await service.ExecuteAsync(Verified(), branchA, actionRead, cancelled.Token);
}, "pre-cancelled evaluation rejected");

Console.WriteLine($"C06-02 CONTRACTS PASS: {checks} positive/negative checks; zero permissions granted.");

sealed class Clock(DateTimeOffset now) : ITrustedHostUtcClock
{
    public DateTimeOffset GetUtcNow() => now;
}
sealed class MemoryReader(IReadOnlyList<C06PreparedRoleSnapshot>? items) : ITrustedC06PreparedRoleReader
{
    public Task<IReadOnlyList<C06PreparedRoleSnapshot>?> ReadExactCandidatesAsync(
        TenantId tenant, TenantActorId actor, TenantBranchId branch, TenantRequiredAction action,
        CancellationToken ct) => Task.FromResult(items);
}
