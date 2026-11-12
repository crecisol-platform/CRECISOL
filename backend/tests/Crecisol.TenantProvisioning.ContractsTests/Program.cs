using System.Reflection;
using Crecisol.Application.Tenancy;
using Crecisol.Domain.Tenancy;

var assertions = 0;
var now = new DateTimeOffset(2026, 11, 12, 20, 30, 0, TimeSpan.Zero);
var sponsorA = TenantActorId.From(Guid.Parse("11111111-1111-1111-1111-111111111111"));
var sponsorB = TenantActorId.From(Guid.Parse("22222222-2222-2222-2222-222222222222"));
var tenantA = TenantId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
var tenantB = TenantId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
var opA = TenantProvisioningOperationId.From(
    Guid.Parse("33333333-3333-3333-3333-333333333333"));
var opB = TenantProvisioningOperationId.From(
    Guid.Parse("44444444-4444-4444-4444-444444444444"));
var businessA = TenantProvisioningBusinessKey.FromTrustedCatalog("alpha-internal");
var businessB = TenantProvisioningBusinessKey.FromTrustedCatalog("beta-internal");
var approvedAt = now.AddHours(-4);
var expiresAt = now.AddDays(5);

TrustedTenantProvisioningPlanSnapshot Plan(
    TenantProvisioningOperationId? operation = null,
    TenantId? tenant = null, TenantActorId? actor = null,
    TenantProvisioningBusinessKey? business = null,
    TenantAddressMode mode = TenantAddressMode.Subdomain,
    TenantProvisioningPlanState state = TenantProvisioningPlanState.Approved,
    long revision = 3, DateTimeOffset? approved = null,
    DateTimeOffset? expires = null) =>
    TrustedTenantProvisioningPlanSnapshot.RestoreFromTrustedBackoffice(
        operation ?? opA, tenant ?? tenantA, actor ?? sponsorA, business ?? businessA,
        mode, state, revision, approved ?? approvedAt, expires ?? expiresAt);

TrustedTenantProvisioningReceipt Receipt(
    TrustedTenantProvisioningPlanSnapshot? plan = null,
    TenantProvisioningOperationId? operation = null,
    TenantId? tenant = null, TenantActorId? actor = null,
    TenantProvisioningBusinessKey? business = null,
    TenantAddressMode? mode = null, string? digest = null,
    TenantLifecycleState lifecycle = TenantLifecycleState.Draft,
    long revision = 1, DateTimeOffset? committed = null)
{
    var p = plan ?? Plan();
    return TrustedTenantProvisioningReceipt.RestoreFromTrustedAtomicStore(
        operation ?? p.OperationId, tenant ?? p.TenantId,
        actor ?? p.SponsoringActorId, business ?? p.BusinessKey,
        mode ?? p.AddressMode, digest ?? p.IntentFingerprint,
        lifecycle, revision, committed ?? now);
}

var approved = Plan();
Check(approved.IsApprovedAtUtc(now), "Approved backoffice plan is current");
Check(!Plan(state: TenantProvisioningPlanState.Suspended).IsApprovedAtUtc(now),
    "Suspended plan never authorizes provisioning");
Check(!Plan(state: TenantProvisioningPlanState.Revoked).IsApprovedAtUtc(now),
    "Revoked plan never authorizes provisioning");
Check(!approved.IsApprovedAtUtc(expiresAt),
    "Expired plan denied at precise UTC expiry");
Check(!approved.IsApprovedAtUtc(approvedAt.AddTicks(-1)),
    "Plan approval not valid before approval instant");
Check(approved.IntentFingerprint.Length == 64 &&
      approved.IntentFingerprint.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f'),
    "SHA256 intent fingerprint is lowercase canonical 64-hex");
Check(approved.IntentFingerprint == Plan().IntentFingerprint,
    "Identical canonical plan yields identical fingerprint");
Check(approved.IntentFingerprint != Plan(operation: opB).IntentFingerprint &&
      approved.IntentFingerprint != Plan(tenant: tenantB).IntentFingerprint &&
      approved.IntentFingerprint != Plan(actor: sponsorB).IntentFingerprint &&
      approved.IntentFingerprint != Plan(business: businessB).IntentFingerprint &&
      approved.IntentFingerprint != Plan(mode: TenantAddressMode.CustomDomain).IntentFingerprint &&
      approved.IntentFingerprint != Plan(revision: 4).IntentFingerprint,
    "Changing operation/tenant/sponsor/name/address mode/revision changes digest");
Check(approved.ToString() == "TrustedTenantProvisioningPlanSnapshot[redacted]",
    "Approved plan cannot log actor, tenant, key, mode or fingerprint");
Check(businessA.ToString() == "TenantProvisioningBusinessKey[redacted]" &&
      opA.ToString() == "TenantProvisioningOperationId[redacted]",
    "Internal key and id diagnostics redact real values");
Check(TenantProvisioningBusinessKey.FromTrustedCatalog("beta-internal") == businessB,
    "Business key equality is exact and ordinal");

Expect<InvalidOperationException>(() =>
    TenantProvisioningOperationId.From(Guid.Empty), "Empty operation ID rejected");
foreach (string? bad in new string?[] {
    null, "", " ", "ab", "a--", "-alpha", "alpha-", "ALPHA", "Alpha",
    "a b", " alpha", "alpha ", "alpha\nother", "alpha\0root",
    "alpha/host", "alpha.example.org", "alpha:admin", "alpha?tenant=other",
    "alpha%2fb", "álpha", "a_b", "0alpha", "αlpha", "a" + new string('b', 64),
    "a\tb", "a\\b", "<script>", "--", "abc-"
})
    Expect<InvalidOperationException>(() =>
        TenantProvisioningBusinessKey.FromTrustedCatalog(bad),
        "Untrusted/collision-prone name rejected");

Expect<ArgumentNullException>(() =>
    TrustedTenantProvisioningPlanSnapshot.RestoreFromTrustedBackoffice(
        null!, tenantA, sponsorA, businessA, TenantAddressMode.Subdomain,
        TenantProvisioningPlanState.Approved, 1, approvedAt, expiresAt),
    "Null operation rejected");
Expect<ArgumentNullException>(() =>
    TrustedTenantProvisioningPlanSnapshot.RestoreFromTrustedBackoffice(
        opA, null!, sponsorA, businessA, TenantAddressMode.Subdomain,
        TenantProvisioningPlanState.Approved, 1, approvedAt, expiresAt),
    "Null tenant rejected");
Expect<ArgumentNullException>(() =>
    TrustedTenantProvisioningPlanSnapshot.RestoreFromTrustedBackoffice(
        opA, tenantA, null!, businessA, TenantAddressMode.Subdomain,
        TenantProvisioningPlanState.Approved, 1, approvedAt, expiresAt),
    "Null sponsoring actor rejected");
Expect<ArgumentNullException>(() =>
    TrustedTenantProvisioningPlanSnapshot.RestoreFromTrustedBackoffice(
        opA, tenantA, sponsorA, null!, TenantAddressMode.Subdomain,
        TenantProvisioningPlanState.Approved, 1, approvedAt, expiresAt),
    "Null business key rejected");
Expect<InvalidOperationException>(() => Plan(revision: 0),
    "Zero approval revision denied");
Expect<InvalidOperationException>(() => Plan(revision: -12),
    "Negative approval revision denied");
Expect<InvalidOperationException>(() => Plan(mode: (TenantAddressMode)99),
    "Unknown commercial address modality denied");
Expect<InvalidOperationException>(() => Plan(state: (TenantProvisioningPlanState)99),
    "Unknown approval lifecycle denied");
Expect<InvalidOperationException>(() => Plan(approved: default(DateTimeOffset)),
    "Missing approval timestamp denied");
Expect<InvalidOperationException>(() =>
    Plan(approved: approvedAt.ToOffset(TimeSpan.FromHours(-6))),
    "Non-UTC approval timestamp denied");
Expect<InvalidOperationException>(() =>
    Plan(expires: expiresAt.ToOffset(TimeSpan.FromHours(-6))),
    "Non-UTC expiration timestamp denied");
Expect<InvalidOperationException>(() => Plan(expires: approvedAt),
    "Zero-length approval window denied");
Expect<InvalidOperationException>(() =>
    approved.IsApprovedAtUtc(now.ToOffset(TimeSpan.FromHours(-6))),
    "Non-UTC plan evaluation clock denied");

var goodReceipt = Receipt();
Check(goodReceipt.ToString() == "TrustedTenantProvisioningReceipt[redacted]",
    "Atomic receipt diagnostics redact business and identifiers");
Check(goodReceipt.TenantLifecycle == TenantLifecycleState.Draft,
    "Provisioning only creates Draft tenants, NEVER Active");
Check(Receipt().IntentFingerprint == approved.IntentFingerprint,
    "Receipt matches canonical fingerprint exactly");
var result = TenantProvisioningResult.FromAtomicStore(approved, goodReceipt, true, now);
Check(result.NewlyCreated && result.TenantId == tenantA &&
      result.OperationId == opA && result.StoreRevision == 1,
    "Created evidence carries only typed operation/tenant/revision");
Check(result.ToString() == "TenantProvisioningResult[redacted]",
    "Provisioning result never logs secret, tenant or business key");
Check(!TenantProvisioningResult.FromAtomicStore(
        approved, goodReceipt, false, now).NewlyCreated,
    "Replay is distinguishable from creation but not new authorization");

Expect<InvalidOperationException>(() => Receipt(lifecycle: TenantLifecycleState.Active),
    "Receipt must NOT activate tenant during provisioning");
Expect<InvalidOperationException>(() => Receipt(lifecycle: TenantLifecycleState.Suspended),
    "Receipt must NOT pretend suspended state is a Draft");
Expect<InvalidOperationException>(() => Receipt(revision: 0),
    "Store receipt revision must be positive");
Expect<InvalidOperationException>(() => Receipt(digest: new string('A', 64)),
    "Noncanonical uppercase digest rejected");
Expect<InvalidOperationException>(() => Receipt(digest: "abc"),
    "Short digest rejected");
Expect<InvalidOperationException>(() =>
    Receipt(committed: now.ToOffset(TimeSpan.FromHours(-6))),
    "Receipt timestamp must be UTC");
Expect<UnauthorizedAccessException>(() =>
    TenantProvisioningResult.FromAtomicStore(
        approved, Receipt(operation: opB), false, now),
    "Replayed receipt from different operation rejected");
Expect<UnauthorizedAccessException>(() =>
    TenantProvisioningResult.FromAtomicStore(
        approved, Receipt(tenant: tenantB), false, now),
    "Receipt from another tenant rejected");
Expect<UnauthorizedAccessException>(() =>
    TenantProvisioningResult.FromAtomicStore(
        approved, Receipt(actor: sponsorB), false, now),
    "Receipt from different sponsoring actor rejected");
Expect<UnauthorizedAccessException>(() =>
    TenantProvisioningResult.FromAtomicStore(
        approved, Receipt(business: businessB), false, now),
    "Receipt for another business key rejected");
Expect<UnauthorizedAccessException>(() =>
    TenantProvisioningResult.FromAtomicStore(
        approved, Receipt(mode: TenantAddressMode.CustomDomain), false, now),
    "Receipt for a different address modality rejected");
Expect<UnauthorizedAccessException>(() =>
    TenantProvisioningResult.FromAtomicStore(
        approved, Receipt(digest: new string('a', 64)), false, now),
    "Forged valid-format but wrong fingerprint rejected");
Expect<UnauthorizedAccessException>(() =>
    TenantProvisioningResult.FromAtomicStore(
        approved, Receipt(committed: now.AddMinutes(1)), false, now),
    "Future-dated commit receipt rejected");
Expect<UnauthorizedAccessException>(() =>
    TenantProvisioningResult.FromAtomicStore(
        approved, Receipt(committed: approvedAt.AddSeconds(-1)), false, now),
    "Commit before backoffice approval rejected");
Expect<UnauthorizedAccessException>(() =>
    TenantProvisioningResult.FromAtomicStore(
        Plan(state: TenantProvisioningPlanState.Revoked), goodReceipt, false, now),
    "Direct result creator cannot bypass revoked approval");

var normalReader = new FakePlanReader((_, _) => approved);
var normalStore = new SyntheticAtomicStore(now);
var normalClock = new FakeClock(_ => now);
var normal = new ProvisionApprovedTenantDraft(normalReader, normalStore, normalClock);
var created = await normal.ExecuteAsync(opA, CancellationToken.None);
var replayed = await normal.ExecuteAsync(opA, CancellationToken.None);
Check(created.NewlyCreated && !replayed.NewlyCreated &&
      created.TenantId == replayed.TenantId &&
      created.OperationId == replayed.OperationId,
    "Idempotent retry returns same Draft and distinguishes replay");
Check(normalStore.OperationCount == 1 && normalStore.TenantCount == 1 &&
      normalStore.BusinessCount == 1,
    "Only one tenant/operation/business reservation exists after retry");
Check(normalReader.Calls == 4 && normalClock.Calls == 4 &&
      normalStore.Calls == 2,
    "Plan approval is reread and revalidated before publishing each receipt");
Check(normalReader.LastOperation == opA,
    "Plan reader accepts only opaque operation selector, not tenant or Host");

var independent = Plan(operation: opB, tenant: tenantB, actor: sponsorB,
    business: businessB, mode: TenantAddressMode.PathSlug);
var twoPlans = new Dictionary<TenantProvisioningOperationId,
    TrustedTenantProvisioningPlanSnapshot> {
    [opA] = approved, [opB] = independent
};
var multiStore = new SyntheticAtomicStore(now);
var multi = new ProvisionApprovedTenantDraft(
    new FakePlanReader((id, _) => twoPlans.GetValueOrDefault(id)),
    multiStore, new FakeClock(_ => now));
var aResult = await multi.ExecuteAsync(opA, CancellationToken.None);
var bResult = await multi.ExecuteAsync(opB, CancellationToken.None);
Check(aResult.TenantId == tenantA && bResult.TenantId == tenantB &&
      multiStore.OperationCount == 2 && multiStore.BusinessCount == 2,
    "Two distinct approved A/B business operations stay isolated and both Draft");
Check(independent.AddressMode == TenantAddressMode.PathSlug &&
      approved.AddressMode == TenantAddressMode.Subdomain &&
      Plan(mode: TenantAddressMode.CustomDomain).AddressMode ==
        TenantAddressMode.CustomDomain,
    "Three commercial address modes remain typed and no DNS/slug is reserved");

async Task Denied<T>(
    string label,
    FakePlanReader? reader = null, SyntheticAtomicStore? store = null,
    FakeClock? clock = null, TenantProvisioningOperationId? id = null)
    where T : Exception =>
    await ExpectAsync<T>(() => New(reader, store, clock)
        .ExecuteAsync(id ?? opA, CancellationToken.None), label);

await Denied<UnauthorizedAccessException>("Missing approved plan fails closed",
    reader: new FakePlanReader((_, _) => null));
await Denied<UnauthorizedAccessException>("Operation ID mismatch in reader fails closed",
    reader: new FakePlanReader((_, _) => Plan(operation: opB)));
await Denied<UnauthorizedAccessException>("Suspended plan denied before storage",
    reader: new FakePlanReader((_, _) =>
        Plan(state: TenantProvisioningPlanState.Suspended)));
await Denied<UnauthorizedAccessException>("Revoked plan denied before storage",
    reader: new FakePlanReader((_, _) =>
        Plan(state: TenantProvisioningPlanState.Revoked)));
await Denied<UnauthorizedAccessException>("Future approval cannot commit",
    reader: new FakePlanReader((_, _) =>
        Plan(approved: now.AddHours(2), expires: now.AddDays(6))));
await Denied<UnauthorizedAccessException>("Expired approval cannot commit",
    reader: new FakePlanReader((_, _) => Plan(expires: now)));
await Denied<UnauthorizedAccessException>("Approval revoked after DB write denies response",
    reader: new FakePlanReader((n, _) =>
        n == 1 ? approved : Plan(state: TenantProvisioningPlanState.Revoked)));
await Denied<UnauthorizedAccessException>("Approval revision changed during write denies",
    reader: new FakePlanReader((n, _) =>
        n == 1 ? approved : Plan(revision: 5)));
await Denied<UnauthorizedAccessException>("Approved tenant changed during write denies",
    reader: new FakePlanReader((n, _) =>
        n == 1 ? approved : Plan(tenant: tenantB)));
await Denied<UnauthorizedAccessException>("Approval disappeared after commit denies",
    reader: new FakePlanReader((n, _) => n == 1 ? approved : null));
await Denied<UnauthorizedAccessException>("Plan expires during operation",
    clock: new FakeClock(n => n == 1 ? now : expiresAt));
await Denied<UnauthorizedAccessException>("Clock rollback detected",
    clock: new FakeClock(n => n == 1 ? now : now.AddDays(-6)));
await Denied<InvalidOperationException>("Non-UTC provisioning clock denied",
    clock: new FakeClock(_ => now.ToOffset(TimeSpan.FromHours(-6))));
await Denied<InvalidOperationException>("Storage exceptions propagate, no fallback",
    store: new SyntheticAtomicStore(now) { AlwaysFail = true });
await Denied<UnauthorizedAccessException>("Storage altered receipt tenant denied",
    store: new SyntheticAtomicStore(now) {
        OverrideReceipt = p => Receipt(p, tenant: tenantB)
    });
await Denied<UnauthorizedAccessException>("Storage altered receipt actor denied",
    store: new SyntheticAtomicStore(now) {
        OverrideReceipt = p => Receipt(p, actor: sponsorB)
    });
await Denied<UnauthorizedAccessException>("Storage altered receipt hash denied",
    store: new SyntheticAtomicStore(now) {
        OverrideReceipt = p => Receipt(p, digest: new string('a', 64))
    });
await Denied<UnauthorizedAccessException>("Storage reported no durable receipt denied",
    store: new SyntheticAtomicStore(now) { ReturnNull = true });

var conflictingStore = new SyntheticAtomicStore(now);
await New(store: conflictingStore).ExecuteAsync(opA, CancellationToken.None);
await Denied<UnauthorizedAccessException>(
    "Same operation key with altered payload cannot replay",
    reader: new FakePlanReader((_, _) => Plan(mode: TenantAddressMode.CustomDomain)),
    store: conflictingStore);
Check(conflictingStore.OperationCount == 1 &&
      conflictingStore.TenantCount == 1,
    "Conflicting operation did not create another tenant");
await Denied<UnauthorizedAccessException>(
    "Different operation same internal business identity denied",
    reader: new FakePlanReader((_, _) =>
        Plan(operation: opB, tenant: tenantB)),
    store: conflictingStore, id: opB);
await Denied<UnauthorizedAccessException>(
    "Different operation same tenant ID with different business identity denied",
    reader: new FakePlanReader((_, _) =>
        Plan(operation: opB, business: businessB)),
    store: conflictingStore, id: opB);
Check(conflictingStore.OperationCount == 1 &&
      conflictingStore.TenantCount == 1,
    "Both unique identity constraints prevent duplicate tenant rows");

var crashStore = new SyntheticAtomicStore(now) { FailBeforeNextCommit = true };
await ExpectAsync<InvalidOperationException>(() =>
    New(store: crashStore).ExecuteAsync(opA, CancellationToken.None),
    "Synthetic crash before commit fails without a partial receipt");
Check(crashStore.OperationCount == 0 && crashStore.TenantCount == 0,
    "Crash before commit leaves zero operations and tenants");
var recovered = await New(store: crashStore).ExecuteAsync(
    opA, CancellationToken.None);
Check(recovered.NewlyCreated && crashStore.TenantCount == 1,
    "After crash, retry commits Draft exactly once");

var lostResponseStore = new SyntheticAtomicStore(now) {
    FailAfterNextCommit = true
};
await ExpectAsync<InvalidOperationException>(() =>
    New(store: lostResponseStore).ExecuteAsync(opA, CancellationToken.None),
    "Lost response AFTER durable commit bubbles up safely");
Check(lostResponseStore.TenantCount == 1,
    "Lost response does not roll back already committed Draft");
var recoveredReply = await New(store: lostResponseStore)
    .ExecuteAsync(opA, CancellationToken.None);
Check(!recoveredReply.NewlyCreated &&
      recoveredReply.TenantId == tenantA &&
      lostResponseStore.TenantCount == 1,
    "Retry after uncertain result replays original committed identity");

var concurrentStore = new SyntheticAtomicStore(now);
var sameCall = New(store: concurrentStore);
var concurrent = await Task.WhenAll(Enumerable.Range(0, 48)
    .Select(_ => Task.Run(async () =>
        await sameCall.ExecuteAsync(opA, CancellationToken.None))));
Check(concurrent.Count(x => x.NewlyCreated) == 1 &&
      concurrent.Count(x => !x.NewlyCreated) == 47,
    "48 actual concurrent same-operation attempts: 1 CREATED, 47 REPLAY");
Check(concurrent.All(x => x.TenantId == tenantA &&
      x.OperationId == opA) &&
      concurrentStore.OperationCount == 1 &&
      concurrentStore.TenantCount == 1 &&
      concurrentStore.BusinessCount == 1,
    "No duplicate tenant, operation or name under .NET concurrency");

var businessCollisionStore = new SyntheticAtomicStore(now);
var collisions = Enumerable.Range(0, 24).Select(i =>
{
    var op = TenantProvisioningOperationId.From(Guid.NewGuid());
    var tenant = TenantId.From(Guid.NewGuid());
    return Plan(operation: op, tenant: tenant);
}).ToArray();
var outcomes = await Task.WhenAll(collisions.Select(p =>
    Task.Run(async () =>
    {
        try
        {
            var r = await New(
                reader: new FakePlanReader((_, _) => p),
                store: businessCollisionStore)
                .ExecuteAsync(p.OperationId, CancellationToken.None);
            return r.NewlyCreated ? "CREATED" : "REPLAY";
        }
        catch (UnauthorizedAccessException)
        {
            return "DENIED";
        }
    })));
Check(outcomes.Count(x => x == "CREATED") == 1 &&
      outcomes.Count(x => x == "DENIED") == 23,
    "24 concurrent DIFFERENT operations requesting same global business key: 1 winner");
Check(businessCollisionStore.OperationCount == 1 &&
      businessCollisionStore.TenantCount == 1 &&
      businessCollisionStore.BusinessCount == 1,
    "Global business identity cannot duplicate even with distinct operation IDs");

using (var pre = new CancellationTokenSource())
{
    pre.Cancel();
    var never = new FakePlanReader((_, _) => approved);
    await ExpectAsync<OperationCanceledException>(() =>
        New(reader: never).ExecuteAsync(opA, pre.Token),
        "Pre-cancel request cannot read approval or store");
    Check(never.Calls == 0, "Pre-cancel stops before any plan read");
}
using (var afterPlan = new CancellationTokenSource())
{
    var neverStore = new SyntheticAtomicStore(now);
    var reader = new FakePlanReader((_, _) => {
        afterPlan.Cancel();
        return approved;
    });
    await ExpectAsync<OperationCanceledException>(() =>
        New(reader: reader, store: neverStore)
            .ExecuteAsync(opA, afterPlan.Token),
        "Cancellation after plan read stops before atomic storage");
    Check(neverStore.Calls == 0, "No writes after cancellation of plan read");
}
using (var afterStore = new CancellationTokenSource())
{
    var reader = new FakePlanReader((_, _) => approved);
    var store = new SyntheticAtomicStore(now) {
        OnCommitted = () => afterStore.Cancel()
    };
    await ExpectAsync<OperationCanceledException>(() =>
        New(reader: reader, store: store)
            .ExecuteAsync(opA, afterStore.Token),
        "Cancel after atomic store prevents publishing result");
    Check(reader.Calls == 1 && store.TenantCount == 1,
        "Cancellation cannot undo committed transaction; retry must recover");
    Check(!((await New(store: store).ExecuteAsync(opA, CancellationToken.None)).NewlyCreated),
        "Retry recovers committed Draft after caller cancellation");
}
using (var afterSecondApproval = new CancellationTokenSource())
{
    var reader = new FakePlanReader((n, _) => {
        if (n == 2) afterSecondApproval.Cancel();
        return approved;
    });
    await ExpectAsync<OperationCanceledException>(() =>
        New(reader: reader).ExecuteAsync(opA, afterSecondApproval.Token),
        "Cancel after re-reading approval prevents evidence");
}
using (var finalClock = new CancellationTokenSource())
{
    var clock = new FakeClock(n => {
        if (n == 2) finalClock.Cancel();
        return now;
    });
    await ExpectAsync<OperationCanceledException>(() =>
        New(clock: clock).ExecuteAsync(opA, finalClock.Token),
        "Cancel during final UTC check prevents response");
}

Expect<ArgumentNullException>(() => New(nullPlanReader: true),
    "Missing backoffice catalog dependency denied");
Expect<ArgumentNullException>(() => New(nullStore: true),
    "Missing atomic store dependency denied");
Expect<ArgumentNullException>(() => New(nullClock: true),
    "Missing backend clock dependency denied");
await ExpectAsync<ArgumentNullException>(() =>
    normal.ExecuteAsync(null!, CancellationToken.None),
    "Null operation selector denied");

var method = typeof(ProvisionApprovedTenantDraft)
    .GetMethod(nameof(ProvisionApprovedTenantDraft.ExecuteAsync))!;
Check(method.GetParameters().Select(x => x.ParameterType)
    .SequenceEqual(new[] {
        typeof(TenantProvisioningOperationId), typeof(CancellationToken)
    }),
    "Use case accepts ONLY operation selector + cancellation, not tenant, Host or payload");
Check(typeof(ITrustedTenantProvisioningPlanReader).GetMethods().Single()
    .GetParameters().Select(x => x.ParameterType)
    .SequenceEqual(new[] {
        typeof(TenantProvisioningOperationId), typeof(CancellationToken)
    }),
    "Trusted plan reader accepts no untrusted actor/tenant/address/DSN");
Check(typeof(IAtomicTenantProvisioningStore).GetMethods().Single()
    .GetParameters().Select(x => x.ParameterType)
    .SequenceEqual(new[] {
        typeof(TrustedTenantProvisioningPlanSnapshot), typeof(CancellationToken)
    }),
    "Atomic store only receives immutable trusted approved plan");
Check(typeof(TenantProvisioningResult)
    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
    .All(p => !p.CanWrite && p.PropertyType != typeof(string)),
    "Published result contains immutable typed keys only, no prices/DSN/authorization");
Check(typeof(ProvisionApprovedTenantDraft).Assembly.GetName().Name ==
      "Crecisol.Application" &&
      typeof(TrustedTenantProvisioningPlanSnapshot).Assembly.GetName().Name ==
      "Crecisol.Domain",
    "Provisioning stays in clean Domain/Application boundaries");

Console.WriteLine($"PASS C04-07 .NET10 synthetic idempotent Draft: {assertions} assertions; " +
    "48 same-operation races (1 created/47 replay) and 24 different-operation " +
    "business collisions (1 created/23 denied), rollback/replay after lost reply, " +
    "payload/tenant/actor/mode mismatches, approval revocation, expiry and cancel. " +
    "NO real production writer, approved admin identity, HTTP, billing, contract " +
    "activation, verified host or dedicated routing certified.");

ProvisionApprovedTenantDraft New(
    FakePlanReader? reader = null,
    SyntheticAtomicStore? store = null,
    FakeClock? clock = null,
    bool nullPlanReader = false, bool nullStore = false,
    bool nullClock = false) =>
    new(
        nullPlanReader ? null! : reader ?? new FakePlanReader((_, _) => approved),
        nullStore ? null! : store ?? new SyntheticAtomicStore(now),
        nullClock ? null! : clock ?? new FakeClock(_ => now));

void Check(bool condition, string label)
{
    if (!condition)
        throw new Exception("FAIL C04-07: " + label);
    assertions++;
}

void Expect<T>(Action fn, string label) where T : Exception
{
    try { fn(); }
    catch (T) { Check(true, label); return; }
    throw new Exception("FAIL C04-07 expected " + typeof(T).Name + ": " + label);
}

async Task ExpectAsync<T>(Func<Task> fn, string label) where T : Exception
{
    try { await fn(); }
    catch (T) { Check(true, label); return; }
    throw new Exception("FAIL C04-07 expected " + typeof(T).Name + ": " + label);
}

sealed class FakePlanReader(
    Func<int, TenantProvisioningOperationId, TrustedTenantProvisioningPlanSnapshot?> read)
    : ITrustedTenantProvisioningPlanReader
{
    private int calls;
    public int Calls => Volatile.Read(ref calls);
    public TenantProvisioningOperationId? LastOperation { get; private set; }

    public Task<TrustedTenantProvisioningPlanSnapshot?> ReadByOperationIdAsync(
        TenantProvisioningOperationId id, CancellationToken ct)
    {
        LastOperation = id;
        return Task.FromResult(read(Interlocked.Increment(ref calls), id));
    }
}

sealed class FakeClock(Func<int, DateTimeOffset> read) : ITrustedHostUtcClock
{
    private int calls;
    public int Calls => Volatile.Read(ref calls);
    public DateTimeOffset GetUtcNow() => read(Interlocked.Increment(ref calls));
}

/// <summary>
/// Test-only atomic store. Its lock protects THREE uniqueness indexes.
/// NOT a database adapter, distributed lock or production implementation.
/// </summary>
sealed class SyntheticAtomicStore(DateTimeOffset currentUtc)
    : IAtomicTenantProvisioningStore
{
    private readonly object mutex = new();
    private readonly Dictionary<TenantProvisioningOperationId,
        TrustedTenantProvisioningReceipt> byOperation = new();
    private readonly HashSet<TenantId> tenantIds = new();
    private readonly HashSet<TenantProvisioningBusinessKey> businessNames = new();
    private int calls;

    public int Calls => Volatile.Read(ref calls);
    public int OperationCount { get { lock (mutex) return byOperation.Count; } }
    public int TenantCount { get { lock (mutex) return tenantIds.Count; } }
    public int BusinessCount { get { lock (mutex) return businessNames.Count; } }
    public bool FailBeforeNextCommit { get; set; }
    public bool FailAfterNextCommit { get; set; }
    public bool AlwaysFail { get; set; }
    public bool ReturnNull { get; set; }
    public Func<TrustedTenantProvisioningPlanSnapshot,
        TrustedTenantProvisioningReceipt>? OverrideReceipt { get; set; }
    public Action? OnCommitted { get; set; }

    public async Task<AtomicTenantProvisioningCommit> CommitOrReplayAsync(
        TrustedTenantProvisioningPlanSnapshot plan, CancellationToken ct)
    {
        Interlocked.Increment(ref calls);
        await Task.Yield(); // Force overlapping calls for deterministic contention checks.
        ct.ThrowIfCancellationRequested();
        lock (mutex)
        {
            if (AlwaysFail)
                throw new InvalidOperationException("Synthetic store inaccessible.");
            if (ReturnNull)
                return null!;
            if (!plan.IsApprovedAtUtc(currentUtc))
                throw new UnauthorizedAccessException("Synthetic approval inactive.");

            if (byOperation.TryGetValue(plan.OperationId, out var existing))
            {
                if (existing.TenantId != plan.TenantId ||
                    existing.SponsoringActorId != plan.SponsoringActorId ||
                    existing.BusinessKey != plan.BusinessKey ||
                    existing.AddressMode != plan.AddressMode ||
                    existing.IntentFingerprint != plan.IntentFingerprint)
                    throw new UnauthorizedAccessException(
                        "Same idempotency operation has a different intent.");
                return new AtomicTenantProvisioningCommit(
                    OverrideReceipt is null ? existing : OverrideReceipt(plan), false);
            }

            // Business identity MUST be globally unique even with different
            // idempotency keys; tenant allocation must not be reused either.
            if (tenantIds.Contains(plan.TenantId) ||
                businessNames.Contains(plan.BusinessKey))
                throw new UnauthorizedAccessException(
                    "Conflicting tenant/business reservation.");

            if (FailBeforeNextCommit)
            {
                FailBeforeNextCommit = false;
                throw new InvalidOperationException("Synthetic crash before COMMIT.");
            }

            var receipt = TrustedTenantProvisioningReceipt.RestoreFromTrustedAtomicStore(
                plan.OperationId, plan.TenantId, plan.SponsoringActorId,
                plan.BusinessKey, plan.AddressMode, plan.IntentFingerprint,
                TenantLifecycleState.Draft, 1, currentUtc);
            // The three updates below represent ONE fake atomic transaction.
            // Real durability is validated separately with MySQL8.4.
            byOperation.Add(plan.OperationId, receipt);
            tenantIds.Add(plan.TenantId);
            businessNames.Add(plan.BusinessKey);
            OnCommitted?.Invoke();

            if (FailAfterNextCommit)
            {
                FailAfterNextCommit = false;
                throw new InvalidOperationException("Reply lost after commit.");
            }

            return new AtomicTenantProvisioningCommit(
                OverrideReceipt is null ? receipt : OverrideReceipt(plan), true);
        }
    }
}
