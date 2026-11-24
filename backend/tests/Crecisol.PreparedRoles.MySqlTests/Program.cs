using Crecisol.Application.Authorization;
using Crecisol.Application.Tenancy;
using Crecisol.Domain.Tenancy;
using Crecisol.Infrastructure.Authorization;

var dsn = Environment.GetEnvironmentVariable("CRECISOL_C06_TEST_DSN");
if (string.IsNullOrWhiteSpace(dsn))
    throw new InvalidOperationException("Disposable MySQL test DSN required; no default.");
if (args.Length != 1 || args[0] is not ("before" or "after"))
    throw new InvalidOperationException("Test phase before|after required.");
var revoked = args[0] == "after";
var now = DateTimeOffset.UtcNow;
var tenantA = TenantId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
var tenantB = TenantId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
var actorA = TenantActorId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01"));
var actorB = TenantActorId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb02"));
var branchA = TenantBranchId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa11"));
var branchAlt = TenantBranchId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa12"));
var branchB = TenantBranchId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb13"));
var read = TenantRequiredAction.FromServerPolicy("cases.read");
var export = TenantRequiredAction.FromServerPolicy("cases.export");
var checks = 0;
void Check(bool valid, string name)
{
    if (!valid) throw new Exception("C06-02 MYSQL ADAPTER FAIL: " + name);
    checks++;
    Console.WriteLine("PASS C06-02 MYSQL adapter " + name);
}

TenantIdentityContextEvidence Identity(TenantId tenant, TenantActorId actor) =>
    TenantIdentityContextEvidence.FromVerifiedInternalEvidence(
        TrustedActorSessionSnapshot.RestoreFromTrustedIdentityProvider(
            actor, ActorSessionState.Active, 4, now.AddHours(-2),
            now.AddMinutes(-1), now.AddMinutes(30)),
        TenantActorMembershipSnapshot.RestoreFromTrustedMembershipCatalog(
            actor, tenant, TenantMembershipState.Active, 5,
            now.AddDays(-1), now.AddDays(-2), now.AddDays(1)),
        new VerifiedTenantHostHint(tenant, TenantAddressMode.Subdomain, 3),
        TenantCatalogSnapshot.RestoreFromTrustedCatalog(tenant,
            "company-synthetic", "Synthetic tenant",
            TenantLifecycleState.Active, TenantAddressMode.Subdomain, 8,
            now.AddDays(-1)));

var adapter = new MySqlPreparedRoleReader(dsn);
var rows = await adapter.ReadExactCandidatesAsync(
    tenantA, actorA, branchA, read, CancellationToken.None);
Check(rows is { Count: 1 }, "real SQL returned one exact tenant+branch role row");
Check(rows![0].TenantId == tenantA && rows[0].ActorId == actorA &&
      rows[0].BranchId == branchA, "RFC4122 GUID roundtrip and scoped identity");
Check(rows[0].RoleCode == "tenant-viewer" && rows[0].RoleRevision > 0,
    "role metadata and revisions loaded");
Check(!rows[0].IsCurrentCandidateFor(tenantA, actorA, branchA, read, now) == revoked,
    "revocation changes candidate state under real MySQL");
var inspector = new InspectPreparedTenantRoles(adapter, new Clock(now));
var evaluation = await inspector.ExecuteAsync(
    Identity(tenantA, actorA), branchA, read, CancellationToken.None);
Check(evaluation.CandidateCount == (revoked ? 0 : 1) &&
      !evaluation.AuthorizesPrivateResource,
    "Application inspection never grants HTTP rights");
Check((await adapter.ReadExactCandidatesAsync(
    tenantA, actorA, branchAlt, read, CancellationToken.None))?.Count == 0,
    "same-tenant other branch yields zero");
Check((await adapter.ReadExactCandidatesAsync(
    tenantA, actorB, branchA, read, CancellationToken.None))?.Count == 0,
    "other account yields zero");
Check((await adapter.ReadExactCandidatesAsync(
    tenantB, actorA, branchB, read, CancellationToken.None))?.Count == 0,
    "other tenant yields zero");
Check((await adapter.ReadExactCandidatesAsync(
    tenantA, actorA, branchA, export, CancellationToken.None))?.Count == 0,
    "unlisted export action yields zero");
Check(!adapter.ToString().Contains("Password=", StringComparison.OrdinalIgnoreCase),
    "adapter description never leaks DSN");
Console.WriteLine($"C06-02 MYSQL ADAPTER {args[0]} PASS: {checks} real integration checks.");

sealed class Clock(DateTimeOffset value) : ITrustedHostUtcClock
{
    public DateTimeOffset GetUtcNow() => value;
}
