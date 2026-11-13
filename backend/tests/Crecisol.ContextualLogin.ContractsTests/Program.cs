using System.Reflection;
using System.Security.Cryptography;
using Crecisol.Application.Identity;
using Crecisol.Application.Tenancy;
using Crecisol.CredentialSecurity;
using Crecisol.Domain.Identity;
using Crecisol.Domain.Tenancy;

// C05-04: real .NET10 Argon2id with fake trusted directories and two tenants.
// Never a session, browser login, private API, actual account or authorization.
var checks = 0;
void Check(bool ok, string title)
{
    if (!ok) throw new InvalidOperationException("FAIL C05-04: " + title);
    checks++;
}
void Deny<T>(Action action, string title) where T : Exception
{
    try { action(); }
    catch (T) { checks++; return; }
    throw new InvalidOperationException("FAIL C05-04 expected " + typeof(T).Name + ": " + title);
}
async Task DenyAsync<T>(Func<Task> action, string title) where T : Exception
{
    try { await action(); }
    catch (T) { checks++; return; }
    throw new InvalidOperationException("FAIL C05-04 expected " + typeof(T).Name + ": " + title);
}

var now = new DateTimeOffset(2026, 11, 13, 20, 0, 0, TimeSpan.Zero);
var changed = now.AddDays(-1);
var granted = now.AddDays(-2);
var expires = now.AddDays(2);
var actorA = TenantActorId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01"));
var actorB = TenantActorId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb02"));
var tenantA = TenantId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
var tenantB = TenantId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
var linkA = AccountTenantLinkId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa12"));
var linkB = AccountTenantLinkId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb12"));
var credentialId = AccountCredentialId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa34"));
var hostA = CanonicalTenantHost.Parse("business-a.example.test");
var hostB = CanonicalTenantHost.Parse("business-b.example.test");
var handle = CanonicalLoginHandle.Parse("User@Example.Test");
var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
var fakeBreach = new AllowOnlySyntheticScreen();
var crypto = new Argon2idPasswordHasher(PasswordHashingPolicy.Default, fakeBreach);
var verifier = new Argon2idLoginCandidateVerifier(crypto);
var phc = crypto.CreateVerifier(secret).ExportForTrustedCredentialStore();
var dummy = crypto.CreateVerifier(Convert.ToBase64String(
    RandomNumberGenerator.GetBytes(32))).ExportForTrustedCredentialStore();

IdentityAccountSnapshot Account(
    TenantActorId? actor = null, IdentityAccountState state = IdentityAccountState.Active,
    long revision = 7, DateTimeOffset? at = null) =>
    IdentityAccountSnapshot.RestoreFromTrustedIdentityDirectory(
        actor ?? actorA, state, revision, at ?? changed);

AccountCredentialMetadata Credential(
    TenantActorId? actor = null, AccountCredentialKind kind = AccountCredentialKind.PasswordVerifier,
    AccountCredentialState state = AccountCredentialState.Active,
    long revision = 3, DateTimeOffset? at = null) =>
    AccountCredentialMetadata.RestoreFromTrustedCredentialDirectory(
        credentialId, actor ?? actorA, kind, state, revision, at ?? changed);

TrustedLoginCredentialSnapshot LoginRecord(
    IdentityAccountState accountState = IdentityAccountState.Active,
    AccountCredentialState credState = AccountCredentialState.Active,
    TrustedLoginRealm realm = TrustedLoginRealm.TenantAccount,
    long accountRevision = 7, long credentialRevision = 3,
    string? verifierPhc = null, DateTimeOffset? accountAt = null,
    DateTimeOffset? credentialAt = null) =>
    TrustedLoginCredentialSnapshot.RestoreFromTrustedDirectory(
        Account(state: accountState, revision: accountRevision, at: accountAt),
        Credential(state: credState, revision: credentialRevision, at: credentialAt),
        realm, verifierPhc ?? phc);

AccountTenantLinkSnapshot Link(
    TenantId? tenant = null, AccountTenantLinkId? id = null,
    TenantActorId? actor = null, TenantMembershipState state = TenantMembershipState.Active,
    long revision = 5, DateTimeOffset? at = null,
    DateTimeOffset? from = null, DateTimeOffset? until = null) =>
    AccountTenantLinkSnapshot.RestoreFromTrustedMembershipDirectory(
        id ?? linkA, actor ?? actorA, tenant ?? tenantA, AccountTenantProfileKind.Employee,
        state, revision, at ?? changed, from ?? granted, until ?? expires);

VerifiedTenantHostBinding Host(
    TenantId? tenant = null, CanonicalTenantHost? hostname = null,
    HostVerificationState state = HostVerificationState.Verified, long revision = 8) =>
    VerifiedTenantHostBinding.RestoreFromTrustedCatalog(
        tenant ?? tenantA, hostname ?? hostA,
        TenantAddressMode.Subdomain, state, revision,
        changed, granted, expires.AddDays(3));

TenantCatalogSnapshot Tenant(
    TenantId? tenant = null, TenantLifecycleState state = TenantLifecycleState.Active,
    long revision = 9, DateTimeOffset? at = null) =>
    TenantCatalogSnapshot.RestoreFromTrustedCatalog(
        tenant ?? tenantA, "internal-safe", "Test business",
        state, TenantAddressMode.Subdomain, revision, at ?? changed);

PrepareContextualPasswordLogin NewLogin(
    FakeCredentialDirectory? accounts = null,
    FakeUnknownVerifier? unknown = null,
    ITrustedLoginPasswordVerifier? checkPassword = null,
    FakeLoginEntry? entry = null,
    FakeLinkDirectory? links = null,
    FakeHostCatalog? hosts = null,
    FakeCatalog? tenants = null,
    FakeUtcClock? clock = null)
{
    var clockAdapter = clock ?? new FakeUtcClock(_ => now);
    return new PrepareContextualPasswordLogin(
        accounts ?? new FakeCredentialDirectory((_, _) => new[] { LoginRecord() }),
        unknown ?? new FakeUnknownVerifier(_ => dummy),
        checkPassword ?? verifier,
        entry ?? new FakeLoginEntry(_ => TrustedLoginEntry.ForTenantHost(hostA)),
        links ?? new FakeLinkDirectory((_, _) => new[] { Link() }),
        new ResolveVerifiedTenantHost(
            hosts ?? new FakeHostCatalog((host, _) => host == hostA
                ? new[] { Host() }
                : host == hostB
                    ? new[] { Host(tenantB, hostB) }
                    : Array.Empty<VerifiedTenantHostBinding>()),
            clockAdapter),
        tenants ?? new FakeCatalog((tenant, _) => Tenant(tenant)),
        clockAdapter);
}
Task<ContextualLoginPreparation> Run(
    PrepareContextualPasswordLogin? service = null, string? password = null,
    CancellationToken cancellation = default) =>
    (service ?? NewLogin()).ExecuteAsync(
        handle, (password ?? secret).AsMemory(), cancellation);
async Task Reject(
    string title, PrepareContextualPasswordLogin? service = null,
    string? password = null)
{
    var result = await Run(service, password);
    Check(result.Status == ContextualLoginPreparationStatus.Denied &&
          result.Evidence is null, title);
}

// Direct syntactic and trust-boundary checks.
Check(handle.Value == "user@example.test", "Canonical global login is deterministic ASCII");
Check(handle.ToString() == "CanonicalLoginHandle[redacted]",
    "Global account lookup key is redacted");
Check(CanonicalLoginHandle.Parse("USER@EXAMPLE.TEST") == handle,
    "Case-insensitive canonical lookup without tenant selector");
foreach (var invalid in new [] {
    "", "u", "u@localhost", " user@example.test", "user@example.test ",
    "user@@example.test", ".user@example.test", "user.@example.test",
    "u..ser@example.test", "user@127.0.0.1", "user@example..test",
    "u/ser@example.test", "ü@example.test", "user@sample..com",
    new string('x', 255) + "@example.test",
})
    Deny<InvalidOperationException>(() => CanonicalLoginHandle.Parse(invalid),
        "Malformed/ambiguous login handle rejected");
Deny<InvalidOperationException>(() => CanonicalLoginHandle.Parse(null),
    "Null handle rejected");
Deny<ArgumentNullException>(() => TrustedLoginEntry.ForTenantHost(null!),
    "Tenant host required for scoped entry");
Check(TrustedLoginEntry.ForPlatformLanding().Kind == TrustedLoginEntryKind.PlatformLanding &&
      TrustedLoginEntry.ForPlatformLanding().Host is null,
    "Central entry carries no browser tenant selection");
Check(TrustedLoginEntry.ForTenantHost(hostA).ToString() == "TrustedLoginEntry[redacted]",
    "Trusted backend host descriptor is redacted");
Deny<InvalidOperationException>(() =>
    TrustedLoginCredentialSnapshot.RestoreFromTrustedDirectory(
        Account(), Credential(actor: actorB), TrustedLoginRealm.TenantAccount, phc),
    "Forged credential of another actor rejected");
Deny<InvalidOperationException>(() =>
    TrustedLoginCredentialSnapshot.RestoreFromTrustedDirectory(
        Account(), Credential(kind: AccountCredentialKind.TotpVerifier),
        TrustedLoginRealm.TenantAccount, phc), "Cannot pass TOTP as password");
Deny<InvalidOperationException>(() =>
    TrustedLoginCredentialSnapshot.RestoreFromTrustedDirectory(
        Account(), Credential(), TrustedLoginRealm.TenantAccount, ""),
    "Missing hash rejected by directory snapshot");
Deny<InvalidOperationException>(() =>
    TrustedLoginCredentialSnapshot.RestoreFromTrustedDirectory(
        Account(), Credential(), (TrustedLoginRealm)777, phc),
    "Unknown identity realm rejected");
Check(LoginRecord().ToString() == "TrustedLoginCredentialSnapshot[redacted]" &&
      verifier.ToString() == "Argon2idLoginCandidateVerifier[redacted]",
    "PHC and credential identifier never appear in diagnostics");
Check(typeof(TrustedLoginCredentialSnapshot).GetMethods(
        BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance)
    .All(m => m.Name != "ExportForInternalVerifier"),
    "PHC export cannot be called from public API adapters");

// Real cryptographic positive cases.
var a = await Run();
Check(a.Status == ContextualLoginPreparationStatus.Ready &&
      a.Evidence is not null, "Argon2id password + host A + exact active A link yields preparation");
var evidenceA = a.Evidence!;
Check(evidenceA.ActorId == actorA && evidenceA.TenantId == tenantA &&
      evidenceA.LinkId == linkA && evidenceA.AccountRevision == 7 &&
      evidenceA.CredentialRevision == 3 && evidenceA.MembershipRevision == 5 &&
      evidenceA.HostRevision == 8 && evidenceA.TenantCatalogRevision == 9 &&
      !evidenceA.NeedsRehash, "Pre-session evidence is exact and versioned");
Check(evidenceA.ToString() == "PreparedContextualLoginEvidence[redacted]" &&
      a.ToString() == "ContextualLoginPreparation[redacted]",
    "No tenant/actor/password in ToString");
Check(!typeof(PreparedContextualLoginEvidence)
    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
    .Any(p => p.Name.Contains("Session", StringComparison.OrdinalIgnoreCase) ||
              p.Name.Contains("JWT", StringComparison.OrdinalIgnoreCase) ||
              p.Name.Contains("Role", StringComparison.OrdinalIgnoreCase) ||
              p.Name.Contains("Token", StringComparison.OrdinalIgnoreCase) ||
              p.Name.Contains("Permission", StringComparison.OrdinalIgnoreCase)),
    "Ready evidence cannot become bearer, role or session assertion");
Check(typeof(PrepareContextualPasswordLogin)
    .GetMethod(nameof(PrepareContextualPasswordLogin.ExecuteAsync))!
    .GetParameters().Select(p => p.ParameterType).SequenceEqual(
    new[] { typeof(CanonicalLoginHandle), typeof(ReadOnlyMemory<char>), typeof(CancellationToken) }),
    "Login accepts no tenant, slug, host, role, branch or user GUID parameters");

// Same global actor with A+B: trusted host is the ONLY context constraint.
var bothLinks = new FakeLinkDirectory((_, _) =>
    new[] { Link(), Link(tenantB, linkB) });
var hostAContext = await Run(NewLogin(links: bothLinks));
Check(hostAContext.Status == ContextualLoginPreparationStatus.Ready &&
      hostAContext.Evidence?.TenantId == tenantA,
    "On host A, actor with both memberships resolves only A");
var hostBContext = await Run(NewLogin(
    entry: new FakeLoginEntry(_ => TrustedLoginEntry.ForTenantHost(hostB)),
    links: new FakeLinkDirectory((_, _) =>
        new[] { Link(), Link(tenantB, linkB) })));
Check(hostBContext.Status == ContextualLoginPreparationStatus.Ready &&
      hostBContext.Evidence?.TenantId == tenantB &&
      hostBContext.Evidence?.LinkId == linkB &&
      hostAContext.Evidence?.TenantId != hostBContext.Evidence.TenantId,
    "On host B, exact actor resolves only B and never inherits A grants");
await Reject("Host B cannot get A-only membership", NewLogin(
    entry: new FakeLoginEntry(_ => TrustedLoginEntry.ForTenantHost(hostB))));
await Reject("Host A cannot get B-only membership", NewLogin(
    links: new FakeLinkDirectory((_, _) => new[] { Link(tenantB, linkB) })));
await Reject("Host spoof with unverified binding denied", NewLogin(
    entry: new FakeLoginEntry(_ => TrustedLoginEntry.ForTenantHost(hostB)),
    hosts: new FakeHostCatalog((_, _) => Array.Empty<VerifiedTenantHostBinding>())));
await Reject("Suspended host cannot route a login", NewLogin(
    hosts: new FakeHostCatalog((_, _) =>
        new[] { Host(state: HostVerificationState.Suspended) })));

// Central login: 0=deny, 1=auto, 2+=context ambiguous (no list/tenant).
var central = new FakeLoginEntry(_ => TrustedLoginEntry.ForPlatformLanding());
var unique = await Run(NewLogin(entry: central));
Check(unique.Status == ContextualLoginPreparationStatus.Ready &&
      unique.Evidence?.TenantId == tenantA && unique.Evidence.HostRevision == 0,
    "Central entry uniquely resolves one active membership without tenant chooser");
var ambiguous = await Run(NewLogin(
    entry: new FakeLoginEntry(_ => TrustedLoginEntry.ForPlatformLanding()),
    links: new FakeLinkDirectory((_, _) =>
        new[] { Link(), Link(tenantB, linkB) })));
Check(ambiguous.Status == ContextualLoginPreparationStatus.Ambiguous &&
      ambiguous.Evidence is null,
    "Two active contexts cannot be silently resolved or listed");
await Reject("Central entry without memberships is denied", NewLogin(
    entry: new FakeLoginEntry(_ => TrustedLoginEntry.ForPlatformLanding()),
    links: new FakeLinkDirectory((_, _) => Array.Empty<AccountTenantLinkSnapshot>())));
var revokedB = await Run(NewLogin(
    entry: new FakeLoginEntry(_ => TrustedLoginEntry.ForPlatformLanding()),
    links: new FakeLinkDirectory((_, _) => new[] {
        Link(), Link(tenantB, linkB, state: TenantMembershipState.Revoked)
    })));
Check(revokedB.Status == ContextualLoginPreparationStatus.Ready &&
      revokedB.Evidence?.TenantId == tenantA,
    "A revoked second membership never creates ambiguity or access");
await Reject("Duplicate links to same tenant rejected", NewLogin(
    links: new FakeLinkDirectory((_, _) =>
        new[] { Link(), Link(id: linkB) })));
await Reject("Cross-actor record in membership list rejected", NewLogin(
    links: new FakeLinkDirectory((_, _) =>
        new[] { Link(), Link(tenantB, linkB, actor: actorB) })));
await Reject("Tenant-suspended membership denied", NewLogin(
    links: new FakeLinkDirectory((_, _) =>
        new[] { Link(state: TenantMembershipState.Suspended) })));
await Reject("Tenant-revoked membership denied", NewLogin(
    links: new FakeLinkDirectory((_, _) =>
        new[] { Link(state: TenantMembershipState.Revoked) })));
await Reject("Expired membership denied", NewLogin(
    links: new FakeLinkDirectory((_, _) =>
        new[] { Link(at: changed, from: granted, until: now.AddSeconds(-1)) })));
await Reject("Future membership activation denied", NewLogin(
    links: new FakeLinkDirectory((_, _) =>
        new[] { Link(at: now.AddSeconds(2), from: now.AddSeconds(1), until: expires) })));
await Reject("Too many memberships rejected", NewLogin(
    links: new FakeLinkDirectory((_, _) =>
        Enumerable.Range(0, 33).Select(i =>
            Link(tenant: TenantId.From(Guid.NewGuid()), id: AccountTenantLinkId.From(Guid.NewGuid())))
            .ToArray())));

// Account/credential and denial privacy: no membership lookup before Argon2id.
await Reject("Incorrect password denied with no tenant evidence",
    NewLogin(), Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
await Reject("Prefix password denied", NewLogin(), secret[..^1]);
await Reject("Trailing password denied", NewLogin(), secret + "x");
var wrongPasswordLinks = new FakeLinkDirectory((_, _) => new[] { Link() });
await Reject("Wrong password never performs tenant lookup", NewLogin(
    links: wrongPasswordLinks), "A different long password!");
Check(wrongPasswordLinks.Calls == 0, "Directory remains private before crypto proof");
await Reject("Suspended account denied", NewLogin(
    accounts: new FakeCredentialDirectory((_, _) =>
        new[] { LoginRecord(accountState: IdentityAccountState.Suspended) })));
await Reject("Pending verification account denied", NewLogin(
    accounts: new FakeCredentialDirectory((_, _) =>
        new[] { LoginRecord(accountState: IdentityAccountState.PendingVerification) })));
await Reject("Disabled account denied", NewLogin(
    accounts: new FakeCredentialDirectory((_, _) =>
        new[] { LoginRecord(accountState: IdentityAccountState.Disabled) })));
await Reject("Revoked credential denied", NewLogin(
    accounts: new FakeCredentialDirectory((_, _) =>
        new[] { LoginRecord(credState: AccountCredentialState.Revoked) })));
await Reject("Unenrolled credential denied", NewLogin(
    accounts: new FakeCredentialDirectory((_, _) =>
        new[] { LoginRecord(credState: AccountCredentialState.PendingEnrollment) })));
await Reject("Future changed account denied", NewLogin(
    accounts: new FakeCredentialDirectory((_, _) =>
        new[] { LoginRecord(accountAt: now.AddSeconds(1)) })));
await Reject("Future changed credential denied", NewLogin(
    accounts: new FakeCredentialDirectory((_, _) =>
        new[] { LoginRecord(credentialAt: now.AddSeconds(1)) })));
await Reject("Platform operator cannot enter tenant realm", NewLogin(
    accounts: new FakeCredentialDirectory((_, _) =>
        new[] { LoginRecord(realm: TrustedLoginRealm.PlatformOperator) })));
await Reject("Invalid/modified PHC denied", NewLogin(
    accounts: new FakeCredentialDirectory((_, _) =>
        new[] { LoginRecord(verifierPhc: phc.Replace("$argon2id$", "$sha256$",
            StringComparison.Ordinal)) })));
await Reject("Failed uniqueness with duplicate accounts denied", NewLogin(
    accounts: new FakeCredentialDirectory((_, _) =>
        new[] { LoginRecord(), LoginRecord() })));
var counted = new FakeCountingVerifier();
var missing = new FakeCredentialDirectory((_, _) =>
    Array.Empty<TrustedLoginCredentialSnapshot>());
await Reject("Unknown account returns neutral Denied", NewLogin(
    accounts: missing, checkPassword: counted));
Check(counted.Calls == 1 && counted.LastPhc == dummy,
    "Unknown account exercises server-owned Argon2id dummy PHC once");
await Reject("Null credentials returns neutral Denied", NewLogin(
    accounts: new FakeCredentialDirectory((_, _) => null)));
var absentDummy = new FakeCredentialDirectory((_, _) => new[] { LoginRecord() });
await Reject("Unavailable unknown-account verifier fails closed", NewLogin(
    unknown: new FakeUnknownVerifier(_ => null), accounts: absentDummy));
Check(absentDummy.Calls == 0, "No lookup when dummy verifier absent");
await Reject("Oversized dummy verifier fails closed", NewLogin(
    unknown: new FakeUnknownVerifier(_ => new string('x', 161))));
await Reject("Corrupt directory actor/credential mismatch denied", NewLogin(
    accounts: new FakeCredentialDirectory((_, _) => null)));

// Races: credentials, memberships, tenant revision/availability, host/entry.
await Reject("Account revision change detected after password verification", NewLogin(
    accounts: new FakeCredentialDirectory((_, n) => new[] {
        LoginRecord(accountRevision: n == 1 ? 7 : 8)
    })));
await Reject("Credential revoked during resolution", NewLogin(
    accounts: new FakeCredentialDirectory((_, n) => new[] {
        LoginRecord(credState: n == 1
            ? AccountCredentialState.Active : AccountCredentialState.Revoked)
    })));
await Reject("Credential digest rotated during resolution", NewLogin(
    accounts: new FakeCredentialDirectory((_, n) => new[] {
        LoginRecord(verifierPhc: n == 1 ? phc : dummy)
    })));
await Reject("Membership revoked during resolution", NewLogin(
    links: new FakeLinkDirectory((_, n) => new[] {
        Link(state: n == 1 ? TenantMembershipState.Active : TenantMembershipState.Revoked)
    })));
await Reject("Membership version changed during resolution", NewLogin(
    links: new FakeLinkDirectory((_, n) => new[] {
        Link(revision: n == 1 ? 5 : 6)
    })));
await Reject("Duplicated membership appears before completion", NewLogin(
    links: new FakeLinkDirectory((_, n) => n == 1
        ? new[] { Link() }
        : new[] { Link(), Link(id: linkB) })));
await Reject("Backend host changes after credential check", NewLogin(
    entry: new FakeLoginEntry(n => n == 1
        ? TrustedLoginEntry.ForTenantHost(hostA)
        : TrustedLoginEntry.ForTenantHost(hostB))));
await Reject("Host catalogue rebind changes tenant", NewLogin(
    hosts: new FakeHostCatalog((_, n) => new[] {
        Host(tenant: n == 1 ? tenantA : tenantB)
    })));
await Reject("Tenant suspension between reads rejected", NewLogin(
    tenants: new FakeCatalog((tenant, n) =>
        Tenant(tenant, state: n == 1
            ? TenantLifecycleState.Active : TenantLifecycleState.Suspended))));
await Reject("Tenant catalog version change rejected", NewLogin(
    tenants: new FakeCatalog((tenant, n) =>
        Tenant(tenant, revision: n == 1 ? 9 : 10))));
await Reject("Tenant catalog mismatch rejected", NewLogin(
    tenants: new FakeCatalog((_, _) => Tenant(tenantB))));
await Reject("Tenant suspended before first lookup rejected", NewLogin(
    tenants: new FakeCatalog((tenant, _) =>
        Tenant(tenant, TenantLifecycleState.Suspended))));
await Reject("Final account expired before preparation", NewLogin(
    clock: new FakeUtcClock(n => n >= 4 ? expires : now)));
await Reject("Backend clock rollback denied", NewLogin(
    clock: new FakeUtcClock(n => n >= 4 ? now.AddHours(-1) : now)));
await DenyAsync<InvalidOperationException>(
    async () => { _ = await Run(NewLogin(
        clock: new FakeUtcClock(n => n == 1
            ? now.ToOffset(TimeSpan.FromHours(-6)) : now))); },
    "Non-UTC server clock must be rejected");

// Cancellation must never produce a Ready result.
using (var cancelled = new CancellationTokenSource())
{
    cancelled.Cancel();
    await DenyAsync<OperationCanceledException>(
        async () => { _ = await Run(cancellation: cancelled.Token); },
        "Cancelled requests cannot use cached credential to mint context");
}

// Stronger C05-03 policy marks rehash but does not create a session.
var stronger = new Argon2idLoginCandidateVerifier(
    new Argon2idPasswordHasher(
        PasswordHashingPolicy.Create(20, 128, 32768, 3, 2), fakeBreach));
var requiresRehash = await Run(NewLogin(checkPassword: stronger));
Check(requiresRehash.Status == ContextualLoginPreparationStatus.Ready &&
      requiresRehash.Evidence?.NeedsRehash == true,
    "Strong policy recommends rehash without role/session issuance");

// Boundary reflection checks.
Check(typeof(ContextualLoginPreparation).GetConstructors().Length == 0,
    "Caller cannot construct unverified Ready result");
Check(typeof(PreparedContextualLoginEvidence).GetConstructors().Length == 0,
    "Caller cannot construct tenant context evidence");
Check(!typeof(PrepareContextualPasswordLogin).GetMethods(
    BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
    .Any(m => m.Name.Contains("Issue", StringComparison.OrdinalIgnoreCase) ||
              m.Name.Contains("Session", StringComparison.OrdinalIgnoreCase) ||
              m.Name.Contains("JWT", StringComparison.OrdinalIgnoreCase)),
    "No session issuance or user JWT method exposed");
Check(fakeBreach.Calls == 2,
    "Only fixture creation used breach screen, not verification or login");
Check(!typeof(ContextualLoginPreparation).GetProperties()
    .Any(p => p.Name.Contains("Cookie", StringComparison.OrdinalIgnoreCase)),
    "No bearer or cookie returned");

// Bounded concurrency: independent lookups never share hidden tenant selection.
var results = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Run()));
Check(results.All(r => r.Status == ContextualLoginPreparationStatus.Ready &&
      r.Evidence?.TenantId == tenantA),
    "Concurrent synthetic logins keep exact tenant context");

Console.WriteLine($"PASS CRECISOL C05-04 .NET10 contextual Argon2id: {checks} positive/negative assertions");
Console.WriteLine("PASS global ambiguous denied without chooser; verified host A/B segregation; races and revocation rejected");
Console.WriteLine("LIMIT: pre-session in-memory trusted ports only; NO real login, JWT, cookie, membership grant, MFA or production email");

sealed class AllowOnlySyntheticScreen : ITrustedCompromisedPasswordScreen
{
    public int Calls { get; private set; }
    public PasswordCompromiseVerdict Check(ReadOnlySpan<char> candidate)
    {
        Calls++;
        return PasswordCompromiseVerdict.Allowed;
    }
}
sealed class FakeCredentialDirectory(
    Func<CanonicalLoginHandle, int, IReadOnlyList<TrustedLoginCredentialSnapshot>?> next)
    : ITrustedLoginCredentialDirectory
{
    public int Calls { get; private set; }
    public Task<IReadOnlyList<TrustedLoginCredentialSnapshot>?> FindExactAsync(
        CanonicalLoginHandle login, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(next(login, ++Calls));
    }
}
sealed class FakeUnknownVerifier(Func<int, string?> next)
    : ITrustedUnknownLoginVerifier
{
    public int Calls { get; private set; }
    public ValueTask<string?> ReadDummyVerifierAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return ValueTask.FromResult(next(++Calls));
    }
}
sealed class FakeCountingVerifier : ITrustedLoginPasswordVerifier
{
    public int Calls { get; private set; }
    public string? LastPhc { get; private set; }
    public PasswordHashCheck Verify(ReadOnlyMemory<char> candidate, string storedPhc)
    {
        Calls++;
        LastPhc = storedPhc;
        return PasswordHashCheck.Denied;
    }
}
sealed class FakeLoginEntry(Func<int, TrustedLoginEntry?> next)
    : ITrustedLoginEntryReader
{
    public int Calls { get; private set; }
    public ValueTask<TrustedLoginEntry?> ReadServerVerifiedEntryAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return ValueTask.FromResult(next(++Calls));
    }
}
sealed class FakeLinkDirectory(
    Func<TenantActorId, int, IReadOnlyList<AccountTenantLinkSnapshot>?> next)
    : ITrustedLoginTenantLinkDirectory
{
    public int Calls { get; private set; }
    public Task<IReadOnlyList<AccountTenantLinkSnapshot>?> FindAllByActorAsync(
        TenantActorId actor, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(next(actor, ++Calls));
    }
}
sealed class FakeHostCatalog(
    Func<CanonicalTenantHost, int, IReadOnlyList<VerifiedTenantHostBinding>?> next)
    : ITrustedVerifiedHostCatalog
{
    public int Calls { get; private set; }
    public Task<IReadOnlyList<VerifiedTenantHostBinding>?> FindExactBindingsAsync(
        CanonicalTenantHost host, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(next(host, ++Calls));
    }
}
sealed class FakeCatalog(Func<TenantId, int, TenantCatalogSnapshot?> next)
    : ITrustedTenantCatalogReader
{
    public int Calls { get; private set; }
    public Task<TenantCatalogSnapshot?> FindByIdAsync(TenantId id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(next(id, ++Calls));
    }
}
sealed class FakeUtcClock(Func<int, DateTimeOffset> next) : ITrustedHostUtcClock
{
    public int Calls { get; private set; }
    public DateTimeOffset GetUtcNow() => next(++Calls);
}
