using System.Reflection;
using Crecisol.Application.Identity;
using Crecisol.Application.Tenancy;
using Crecisol.Domain.Identity;
using Crecisol.Domain.Tenancy;

// C05-01: real compiled Domain/Application .NET10 contracts, synthetic readers.
// These fixtures DO NOT perform password hashing, authenticate HTTP or use a DB.
var checks = 0;
var now = new DateTimeOffset(2026, 11, 12, 20, 0, 0, TimeSpan.Zero);
var changed = now.AddDays(-1);
var started = now.AddDays(-2);
var expiry = now.AddDays(3);
var actorA = TenantActorId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01"));
var actorB = TenantActorId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb02"));
var tenantA = TenantId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
var tenantB = TenantId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
var hostA = CanonicalTenantHost.Parse("business-a.example.test");
var hostB = CanonicalTenantHost.Parse("business-b.example.test");
var accountLinkA = AccountTenantLinkId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa22"));
var accountLinkB = AccountTenantLinkId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb22"));
var credentialId = AccountCredentialId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa33"));

void Check(bool ok, string label)
{
    if (!ok) throw new InvalidOperationException("FAIL C05-01: " + label);
    checks++;
}

void Deny<T>(Action action, string label) where T : Exception
{
    try { action(); }
    catch (T) { checks++; return; }
    throw new InvalidOperationException("FAIL expected " + typeof(T).Name + ": " + label);
}

async Task DenyAsync<T>(Func<Task> action, string label) where T : Exception
{
    try { await action(); }
    catch (T) { checks++; return; }
    throw new InvalidOperationException("FAIL expected " + typeof(T).Name + ": " + label);
}

IdentityAccountSnapshot Account(
    TenantActorId? actor = null, IdentityAccountState state = IdentityAccountState.Active,
    long rev = 7, DateTimeOffset? at = null) =>
    IdentityAccountSnapshot.RestoreFromTrustedIdentityDirectory(
        actor ?? actorA, state, rev, at ?? changed);

AccountCredentialMetadata Credential(
    TenantActorId? actor = null, AccountCredentialKind kind = AccountCredentialKind.PasswordVerifier,
    AccountCredentialState state = AccountCredentialState.Active, long rev = 1,
    DateTimeOffset? at = null) =>
    AccountCredentialMetadata.RestoreFromTrustedCredentialDirectory(
        credentialId, actor ?? actorA, kind, state, rev, at ?? changed);

AccountTenantLinkSnapshot Link(
    TenantActorId? actor = null, TenantId? tenant = null,
    AccountTenantLinkId? linkId = null,
    AccountTenantProfileKind profile = AccountTenantProfileKind.Owner,
    TenantMembershipState state = TenantMembershipState.Active, long rev = 5,
    DateTimeOffset? at = null, DateTimeOffset? from = null, DateTimeOffset? until = null) =>
    AccountTenantLinkSnapshot.RestoreFromTrustedMembershipDirectory(
        linkId ?? accountLinkA, actor ?? actorA, tenant ?? tenantA,
        profile, state, rev, at ?? changed, from ?? started, until ?? expiry);

PlatformOperatorDirectoryEntry Operator(
    TenantActorId? actor = null, PlatformOperatorState state = PlatformOperatorState.Active,
    long rev = 6, DateTimeOffset? at = null) =>
    PlatformOperatorDirectoryEntry.RestoreFromTrustedPlatformDirectory(
        actor ?? actorA, state, rev, at ?? changed);

TrustedActorSessionSnapshot Session(
    TenantActorId? actor = null, ActorSessionState state = ActorSessionState.Active,
    long rev = 4) => TrustedActorSessionSnapshot.RestoreFromTrustedIdentityProvider(
        actor ?? actorA, state, rev,
        now.AddDays(-4), changed, expiry.AddDays(2));

TenantActorMembershipSnapshot Membership(
    TenantActorId? actor = null, TenantId? tenant = null,
    TenantMembershipState state = TenantMembershipState.Active, long rev = 5) =>
    TenantActorMembershipSnapshot.RestoreFromTrustedMembershipCatalog(
        actor ?? actorA, tenant ?? tenantA, state, rev, changed, started, expiry);

VerifiedTenantHostBinding HostBinding(TenantId? tenant = null, CanonicalTenantHost? host = null) =>
    VerifiedTenantHostBinding.RestoreFromTrustedCatalog(
        tenant ?? tenantA, host ?? hostA, TenantAddressMode.Subdomain,
        HostVerificationState.Verified, 8, changed, started, expiry.AddDays(3));

TenantCatalogSnapshot Tenant(TenantId? tenant = null,
    TenantLifecycleState state = TenantLifecycleState.Active) =>
    TenantCatalogSnapshot.RestoreFromTrustedCatalog(
        tenant ?? tenantA, "internal-safe", "Safe business",
        state, TenantAddressMode.Subdomain, 9, changed);

var goodAccount = Account();
var goodLink = Link();
var goodCredential = Credential();
var goodOperator = Operator();
var clock = new FakeClock(_ => now);

ResolveCurrentAccountTenantLink NewResolver(
    FakeAccounts? accounts = null,
    FakeLinks? links = null,
    FakeSessions? sessions = null,
    FakeDirectHost? directHost = null,
    FakeHosts? hosts = null,
    FakeMemberships? memberships = null,
    FakeTenants? tenants = null,
    FakeClock? fixedClock = null)
{
    var utcClock = fixedClock ?? new FakeClock(_ => now);
    var tenantContext = new ResolveCurrentTenantIdentityContext(
        sessions ?? new FakeSessions(_ => Session()),
        directHost ?? new FakeDirectHost(_ => hostA),
        new ResolveVerifiedTenantHost(
            hosts ?? new FakeHosts((host, _) => host == hostA
                ? new[] { HostBinding() }
                : Array.Empty<VerifiedTenantHostBinding>()), utcClock),
        memberships ?? new FakeMemberships((_, _, _) => new[] { Membership() }),
        tenants ?? new FakeTenants((_, _) => Tenant()),
        utcClock);

    return new ResolveCurrentAccountTenantLink(
        tenantContext,
        accounts ?? new FakeAccounts((_, _) => goodAccount),
        links ?? new FakeLinks((_, _, _) => new[] { goodLink }),
        utcClock);
}

// Domain positive assertions and redacted diagnostics.
Check(accountLinkA != accountLinkB && actorA != actorB && tenantA != tenantB,
    "IDs of distinct businesses, actors and links remain independent");
Check(goodAccount.IsActiveAtUtc(now), "Active account has valid revision and UTC interval");
Check(goodLink.IsActiveAtUtc(now), "Exact tenant link valid inside activation window");
Check(goodLink.ProfileKind == AccountTenantProfileKind.Owner &&
      goodLink.TenantId == tenantA, "Profile is tenant-local metadata, not a global grant");
Check(!goodLink.IsActiveAtUtc(expiry), "Membership expiry is exclusive");
Check(!Account(state: IdentityAccountState.Suspended).IsActiveAtUtc(now),
    "Suspended account cannot be active");
Check(!Account(state: IdentityAccountState.Disabled).IsActiveAtUtc(now),
    "Disabled account cannot be active");
Check(!Account(state: IdentityAccountState.PendingVerification).IsActiveAtUtc(now),
    "Unverified account cannot be active");
Check(!Account(at: now.AddDays(1)).IsActiveAtUtc(now),
    "Future account change cannot be used to claim active state");
Check(!Link(state: TenantMembershipState.Suspended).IsActiveAtUtc(now),
    "Suspended tenant link rejected");
Check(!Link(state: TenantMembershipState.Revoked).IsActiveAtUtc(now),
    "Revoked tenant link rejected");
Check(!Link(from: now.AddHours(1), at: now.AddHours(2), until: now.AddHours(3))
    .IsActiveAtUtc(now), "Future tenant link is not yet valid");
Check(goodCredential.Kind == AccountCredentialKind.PasswordVerifier &&
      goodCredential.State == AccountCredentialState.Active,
    "Credential describes type/state only, not cryptographic verification");
Check(Credential(kind: AccountCredentialKind.PasskeyPublicKey).Kind ==
      AccountCredentialKind.PasskeyPublicKey, "Passkey metadata is distinct");
Check(Credential(kind: AccountCredentialKind.TotpVerifier).Kind ==
      AccountCredentialKind.TotpVerifier, "TOTP verifier metadata is distinct");
Check(Credential(kind: AccountCredentialKind.BackupCodeVerifier).Kind ==
      AccountCredentialKind.BackupCodeVerifier, "Backup-code verifier metadata is distinct");
Check(Credential(state: AccountCredentialState.Revoked).State ==
      AccountCredentialState.Revoked, "Revoked credential metadata remains non-authorizing");
Check(goodOperator.State == PlatformOperatorState.Active &&
      goodOperator.GetType().GetProperty("TenantId") is null,
    "Global operator directory is NOT a tenant membership or private access");
Check(Operator(state: PlatformOperatorState.Revoked).State == PlatformOperatorState.Revoked,
    "Revoked operator record cannot retain a tenant grant");
Check(AccountCredentialId.From(credentialId.Value) == credentialId,
    "Typed credential ID roundtrip");
Check(AccountTenantLinkId.From(accountLinkA.Value) == accountLinkA,
    "Typed link ID roundtrip");
Check(accountLinkA.ToString() == "AccountTenantLinkId[redacted]" &&
      credentialId.ToString() == "AccountCredentialId[redacted]" &&
      goodAccount.ToString() == "IdentityAccountSnapshot[redacted]" &&
      goodLink.ToString() == "AccountTenantLinkSnapshot[redacted]" &&
      goodCredential.ToString() == "AccountCredentialMetadata[redacted]" &&
      goodOperator.ToString() == "PlatformOperatorDirectoryEntry[redacted]",
    "No actor, account, tenant or credential identifier appears in ToString");
Check(!typeof(AccountCredentialMetadata).GetProperties(
        BindingFlags.Public | BindingFlags.Instance)
    .Any(x => x.PropertyType == typeof(string) || x.PropertyType == typeof(byte[])),
    "Credential metadata cannot carry a password, hash, token or raw secret");
Check(!typeof(AccountTenantLinkSnapshot).GetProperties(
        BindingFlags.Public | BindingFlags.Instance)
    .Any(x => x.Name.Contains("Role", StringComparison.OrdinalIgnoreCase) ||
              x.Name.Contains("Permission", StringComparison.OrdinalIgnoreCase)),
    "Profile categories never expose role/permission fields");

// Negative IDs, unknown enum values, dates, revisions, paired windows.
Deny<InvalidOperationException>(() => AccountCredentialId.From(Guid.Empty),
    "Empty credential ID");
Deny<InvalidOperationException>(() => AccountTenantLinkId.From(Guid.Empty),
    "Empty membership ID");
Deny<ArgumentNullException>(() =>
    IdentityAccountSnapshot.RestoreFromTrustedIdentityDirectory(
        null!, IdentityAccountState.Active, 1, changed), "Null actor");
Deny<InvalidOperationException>(() => Account(state: (IdentityAccountState)199),
    "Undefined account state");
Deny<InvalidOperationException>(() => Account(rev: 0), "Zero account revision");
Deny<InvalidOperationException>(() => Account(rev: -2), "Negative account revision");
Deny<InvalidOperationException>(() => Account(at: default(DateTimeOffset)),
    "Default account timestamp");
Deny<InvalidOperationException>(() =>
    Account(at: changed.ToOffset(TimeSpan.FromHours(-6))), "Non-UTC account change");
Deny<InvalidOperationException>(() =>
    goodAccount.IsActiveAtUtc(now.ToOffset(TimeSpan.FromHours(2))),
    "Non-UTC query time rejected");

Deny<ArgumentNullException>(() =>
    AccountCredentialMetadata.RestoreFromTrustedCredentialDirectory(
        null!, actorA, AccountCredentialKind.PasswordVerifier,
        AccountCredentialState.Active, 1, changed), "Null credential id");
Deny<ArgumentNullException>(() =>
    AccountCredentialMetadata.RestoreFromTrustedCredentialDirectory(
        credentialId, null!, AccountCredentialKind.PasswordVerifier,
        AccountCredentialState.Active, 1, changed), "Null credential actor");
Deny<InvalidOperationException>(() => Credential(kind: (AccountCredentialKind)100),
    "Unknown credential kind");
Deny<InvalidOperationException>(() => Credential(state: (AccountCredentialState)100),
    "Unknown credential state");
Deny<InvalidOperationException>(() => Credential(rev: 0),
    "Credential revision zero");
Deny<InvalidOperationException>(() =>
    Credential(at: changed.ToOffset(TimeSpan.FromHours(6))),
    "Credential timestamp must be UTC");

Deny<ArgumentNullException>(() =>
    AccountTenantLinkSnapshot.RestoreFromTrustedMembershipDirectory(
        null!, actorA, tenantA, AccountTenantProfileKind.Owner,
        TenantMembershipState.Active, 1, changed, started, expiry),
    "Null link ID");
Deny<ArgumentNullException>(() =>
    AccountTenantLinkSnapshot.RestoreFromTrustedMembershipDirectory(
        accountLinkA, null!, tenantA, AccountTenantProfileKind.Owner,
        TenantMembershipState.Active, 1, changed, started, expiry),
    "Null tenant actor");
Deny<ArgumentNullException>(() =>
    AccountTenantLinkSnapshot.RestoreFromTrustedMembershipDirectory(
        accountLinkA, actorA, null!, AccountTenantProfileKind.Owner,
        TenantMembershipState.Active, 1, changed, started, expiry),
    "Null tenant");
Deny<InvalidOperationException>(() => Link(profile: (AccountTenantProfileKind)999),
    "Unknown profile kind");
Deny<InvalidOperationException>(() => Link(state: (TenantMembershipState)999),
    "Unknown membership state");
Deny<InvalidOperationException>(() => Link(rev: 0), "Zero membership revision");
Deny<InvalidOperationException>(() => Link(at: changed.ToOffset(TimeSpan.FromHours(6))),
    "Non-UTC membership change");
Deny<InvalidOperationException>(() => Link(from: started.ToOffset(TimeSpan.FromHours(6))),
    "Non-UTC grant start");
Deny<InvalidOperationException>(() => Link(until: expiry.ToOffset(TimeSpan.FromHours(6))),
    "Non-UTC expiry");
Deny<InvalidOperationException>(() => Link(from: now.AddDays(1)),
    "Activation cannot follow revision time");
Deny<InvalidOperationException>(() => Link(until: started),
    "Zero-length membership window");
Deny<InvalidOperationException>(() =>
    AccountTenantLinkSnapshot.RestoreFromTrustedMembershipDirectory(
        accountLinkA, actorA, tenantA, AccountTenantProfileKind.Owner,
        TenantMembershipState.Active, 1, changed, null, null),
    "Active membership without dates");
Deny<InvalidOperationException>(() =>
    AccountTenantLinkSnapshot.RestoreFromTrustedMembershipDirectory(
        accountLinkA, actorA, tenantA, AccountTenantProfileKind.Owner,
        TenantMembershipState.Active, 1, changed, started, null),
    "Half-paired membership window");
Deny<InvalidOperationException>(() =>
    AccountTenantLinkSnapshot.RestoreFromTrustedMembershipDirectory(
        accountLinkA, actorA, tenantA, AccountTenantProfileKind.Owner,
        TenantMembershipState.Pending, 1, changed, started, expiry),
    "Pending link cannot claim active window");
Deny<InvalidOperationException>(() =>
    goodLink.IsActiveAtUtc(now.ToOffset(TimeSpan.FromHours(6))),
    "Non-UTC membership evaluation");

Deny<InvalidOperationException>(() => Operator(state: (PlatformOperatorState)100),
    "Invalid global operator status");
Deny<InvalidOperationException>(() => Operator(rev: 0),
    "Zero global operator revision");
Deny<InvalidOperationException>(() =>
    Operator(at: changed.ToOffset(TimeSpan.FromHours(6))),
    "Non-UTC global operator timestamp");

// Actual Application positive and cross-tenant negative tests.
var accountReader = new FakeAccounts((_, _) => goodAccount);
var linkReader = new FakeLinks((_, _, _) => new[] { goodLink });
var goodEvidence = await NewResolver(accounts: accountReader, links: linkReader)
    .ExecuteAsync(CancellationToken.None);
Check(goodEvidence.ActorId == actorA && goodEvidence.TenantId == tenantA &&
      goodEvidence.LinkId == accountLinkA && goodEvidence.AccountRevision == 7 &&
      goodEvidence.LinkRevision == 5 && goodEvidence.SessionRevision == 4 &&
      goodEvidence.HostRevision == 8 && goodEvidence.TenantCatalogRevision == 9,
    "Only verified actor/host/tenant and exact account+membership yield evidence");
Check(goodEvidence.ToString() == "AccountTenantBindingEvidence[redacted]",
    "Evidence cannot leak tenant, actor or link IDs in diagnostics");
Check(accountReader.Calls == 2 && accountReader.LastActor == actorA,
    "Account fetched only by verified actor, twice for race detection");
Check(linkReader.Calls == 2 && linkReader.LastActor == actorA &&
      linkReader.LastTenant == tenantA,
    "Link looked up twice by verified actor AND verified tenant");

// One actor may be a member of A and B, but each entry remains separate.
var linkB = Link(tenant: tenantB, linkId: accountLinkB,
    profile: AccountTenantProfileKind.Consumer);
var goodB = await NewResolver(
    directHost: new FakeDirectHost(_ => hostB),
    hosts: new FakeHosts((_, _) => new[] { HostBinding(tenantB, hostB) }),
    memberships: new FakeMemberships((_, _, _) => new[] { Membership(tenant: tenantB) }),
    tenants: new FakeTenants((_, _) => Tenant(tenantB)),
    links: new FakeLinks((_, _, _) => new[] { linkB }))
    .ExecuteAsync(CancellationToken.None);
Check(goodB.TenantId == tenantB && goodB.LinkId == accountLinkB &&
      goodB.ProfileKind == AccountTenantProfileKind.Consumer &&
      goodEvidence.TenantId != goodB.TenantId,
    "Same actor is independently linked to B and never inherits A scope");
Check(Link(profile: AccountTenantProfileKind.Employee).ProfileKind ==
      AccountTenantProfileKind.Employee, "Employee is a profile not a privilege");

// Negative permission attempts: actor, tenant, state and revision.
async Task Reject<T>(string label, FakeAccounts? accounts = null,
    FakeLinks? links = null, FakeSessions? sessions = null,
    FakeDirectHost? directHost = null, FakeHosts? hosts = null,
    FakeMemberships? memberships = null, FakeTenants? tenants = null,
    FakeClock? utcClock = null) where T : Exception =>
    await DenyAsync<T>(() => NewResolver(
        accounts, links, sessions, directHost, hosts, memberships, tenants, utcClock)
            .ExecuteAsync(CancellationToken.None), label);

await Reject<UnauthorizedAccessException>("A actor cannot use B tenant link",
    links: new FakeLinks((_, _, _) => new[] { linkB }));
await Reject<UnauthorizedAccessException>("Different actor credential account rejected",
    accounts: new FakeAccounts((_, _) => Account(actor: actorB)));
await Reject<UnauthorizedAccessException>("Different actor membership link rejected",
    links: new FakeLinks((_, _, _) => new[] { Link(actor: actorB) }));
await Reject<UnauthorizedAccessException>("B tenant link rejected under verified A host",
    links: new FakeLinks((_, _, _) => new[] { Link(tenant: tenantB) }));
await Reject<UnauthorizedAccessException>("Missing account rejected",
    accounts: new FakeAccounts((_, _) => null));
await Reject<UnauthorizedAccessException>("Suspended account rejected",
    accounts: new FakeAccounts((_, _) => Account(state: IdentityAccountState.Suspended)));
await Reject<UnauthorizedAccessException>("Disabled account rejected",
    accounts: new FakeAccounts((_, _) => Account(state: IdentityAccountState.Disabled)));
await Reject<UnauthorizedAccessException>("Pending account rejected",
    accounts: new FakeAccounts((_, _) => Account(state: IdentityAccountState.PendingVerification)));
await Reject<UnauthorizedAccessException>("Future account revision rejected",
    accounts: new FakeAccounts((_, _) => Account(at: now.AddSeconds(1))));
await Reject<UnauthorizedAccessException>("Missing tenant-account membership rejected",
    links: new FakeLinks((_, _, _) => null));
await Reject<UnauthorizedAccessException>("Empty membership list denied",
    links: new FakeLinks((_, _, _) => Array.Empty<AccountTenantLinkSnapshot>()));
await Reject<UnauthorizedAccessException>("Duplicate membership records denied",
    links: new FakeLinks((_, _, _) => new[] { goodLink, goodLink }));
await Reject<UnauthorizedAccessException>("Wrong C04 membership revision rejected",
    links: new FakeLinks((_, _, _) => new[] { Link(rev: 11) }));
await Reject<UnauthorizedAccessException>("Revoked membership denied",
    links: new FakeLinks((_, _, _) => new[] { Link(state: TenantMembershipState.Revoked) }));
await Reject<UnauthorizedAccessException>("Suspended membership denied",
    links: new FakeLinks((_, _, _) => new[] { Link(state: TenantMembershipState.Suspended) }));
await Reject<UnauthorizedAccessException>("Expiry boundary prevents stale membership",
    links: new FakeLinks((_, _, _) => new[] { Link(until: now) }));
await Reject<UnauthorizedAccessException>("Changing account revision mid-flight rejected",
    accounts: new FakeAccounts((call, _) => Account(rev: call == 1 ? 7 : 8)));
await Reject<UnauthorizedAccessException>("Changing membership link ID mid-flight rejected",
    links: new FakeLinks((call, _, _) =>
        new[] { Link(linkId: call == 1 ? accountLinkA : accountLinkB) }));
await Reject<UnauthorizedAccessException>("Changing link profile mid-flight rejected",
    links: new FakeLinks((call, _, _) =>
        new[] { Link(profile: call == 1 ? AccountTenantProfileKind.Owner
                                    : AccountTenantProfileKind.Employee) }));
await Reject<UnauthorizedAccessException>("Changing link revision mid-flight rejected",
    links: new FakeLinks((call, _, _) => new[] { Link(rev: call == 1 ? 5 : 6) }));
await Reject<UnauthorizedAccessException>("Session revision switch detected on refresh",
    sessions: new FakeSessions(call => Session(rev: call == 1 ? 4 : 6)));
await Reject<UnauthorizedAccessException>("Revoked C04 session cannot be a source of identity",
    sessions: new FakeSessions(_ => Session(state: ActorSessionState.Revoked)));
await Reject<InvalidOperationException>("Actor from another account cannot match C04 membership",
    sessions: new FakeSessions(_ => Session(actor: actorB)));
await Reject<UnauthorizedAccessException>("Membership of tenant B cannot be used for host A",
    memberships: new FakeMemberships((_, _, _) => new[] { Membership(tenant: tenantB) }));
await Reject<UnauthorizedAccessException>("Suspended tenant cannot authorize account",
    tenants: new FakeTenants((_, _) => Tenant(state: TenantLifecycleState.Suspended)));
await Reject<UnauthorizedAccessException>("Unverified tenant host cannot authorize account",
    hosts: new FakeHosts((_, _) => Array.Empty<VerifiedTenantHostBinding>()));
await Reject<UnauthorizedAccessException>("Host B cannot inherit A membership",
    directHost: new FakeDirectHost(_ => hostB));
await Reject<UnauthorizedAccessException>("Session absent cannot fall through to account lookup",
    sessions: new FakeSessions(_ => null));
await Reject<UnauthorizedAccessException>("Account expiry between start and final checks denied",
    utcClock: new FakeClock(call => call >= 10 ? now.AddDays(4) : now));
await Reject<InvalidOperationException>("Non-UTC clock fails closed",
    utcClock: new FakeClock(_ => now.ToOffset(TimeSpan.FromHours(-6))));
await Reject<InvalidOperationException>("Clock moving backwards denied",
    utcClock: new FakeClock(call => call >= 10 ? now.AddSeconds(-5) : now));
await Reject<InvalidOperationException>("Account backend exception propagated (no fallback)",
    accounts: new FakeAccounts((_, _) => throw new InvalidOperationException("backend unavailable")));
await Reject<InvalidOperationException>("Tenant link backend exception propagated (no fallback)",
    links: new FakeLinks((_, _, _) => throw new InvalidOperationException("backend unavailable")));

// Explicit cancellation and constructors: no caller-supplied identity or tenant.
using (var cancelled = new CancellationTokenSource())
{
    cancelled.Cancel();
    await DenyAsync<OperationCanceledException>(
        () => NewResolver().ExecuteAsync(cancelled.Token),
        "Pre-cancelled flow never grants account/tenant link");
}
var parameters = typeof(ResolveCurrentAccountTenantLink)
    .GetMethod(nameof(ResolveCurrentAccountTenantLink.ExecuteAsync))!
    .GetParameters();
Check(parameters.Length == 1 && parameters[0].ParameterType == typeof(CancellationToken),
    "Use case accepts cancellation ONLY, never caller-supplied tenant_id/email/claims/host");
Check(typeof(AccountTenantBindingEvidence).GetProperties()
    .All(p => p.Name is not ("Role" or "Permission" or "ConnectionString" or "Token")),
    "Evidence never carries tenant action grants or bearer material");
Check(clock.GetUtcNow() == now, "UTC clock is backend fixture, not request input");

Console.WriteLine($"PASS C05-01 .NET10 internal account/credential/membership: {checks} assertions");
Console.WriteLine("PASS A/B positive tenant A/B and negative revocation, mismatch, expiry, races");
Console.WriteLine("LIMIT: synthetic trusted ports; no password auth, HTTP, MySQL identity or MFA executed");

sealed class FakeClock(Func<int, DateTimeOffset> next) : ITrustedHostUtcClock
{
    public int Calls { get; private set; }
    public DateTimeOffset GetUtcNow() => next(++Calls);
}

sealed class FakeSessions(Func<int, TrustedActorSessionSnapshot?> read)
    : ITrustedCurrentActorSessionReader
{
    public int Calls { get; private set; }
    public ValueTask<TrustedActorSessionSnapshot?> ReadCurrentVerifiedSessionAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(read(++Calls));
    }
}

sealed class FakeDirectHost(Func<int, CanonicalTenantHost?> read)
    : ITrustedCurrentDirectHostReader
{
    public int Calls { get; private set; }
    public ValueTask<CanonicalTenantHost?> ReadCurrentDirectHostAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(read(++Calls));
    }
}

sealed class FakeHosts(Func<CanonicalTenantHost, int, IReadOnlyList<VerifiedTenantHostBinding>?> read)
    : ITrustedVerifiedHostCatalog
{
    public int Calls { get; private set; }
    public Task<IReadOnlyList<VerifiedTenantHostBinding>?> FindExactBindingsAsync(
        CanonicalTenantHost host, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(read(host, ++Calls));
    }
}

sealed class FakeMemberships(
    Func<TenantActorId, TenantId, int, IReadOnlyList<TenantActorMembershipSnapshot>?> read)
    : ITrustedActorTenantMembershipReader
{
    public int Calls { get; private set; }
    public Task<IReadOnlyList<TenantActorMembershipSnapshot>?> FindExactMembershipsAsync(
        TenantActorId actorId, TenantId tenantId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(read(actorId, tenantId, ++Calls));
    }
}

sealed class FakeTenants(Func<TenantId, int, TenantCatalogSnapshot?> read)
    : ITrustedTenantCatalogReader
{
    public int Calls { get; private set; }
    public Task<TenantCatalogSnapshot?> FindByIdAsync(TenantId tenantId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(read(tenantId, ++Calls));
    }
}

sealed class FakeAccounts(Func<int, TenantActorId, IdentityAccountSnapshot?> read)
    : ITrustedIdentityAccountReader
{
    public int Calls { get; private set; }
    public TenantActorId? LastActor { get; private set; }
    public Task<IdentityAccountSnapshot?> FindByVerifiedActorAsync(
        TenantActorId actorId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastActor = actorId;
        return Task.FromResult(read(++Calls, actorId));
    }
}

sealed class FakeLinks(
    Func<int, TenantActorId, TenantId, IReadOnlyList<AccountTenantLinkSnapshot>?> read)
    : ITrustedAccountTenantLinkReader
{
    public int Calls { get; private set; }
    public TenantActorId? LastActor { get; private set; }
    public TenantId? LastTenant { get; private set; }
    public Task<IReadOnlyList<AccountTenantLinkSnapshot>?> FindExactLinksAsync(
        TenantActorId actorId, TenantId tenantId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastActor = actorId;
        LastTenant = tenantId;
        return Task.FromResult(read(++Calls, actorId, tenantId));
    }
}
