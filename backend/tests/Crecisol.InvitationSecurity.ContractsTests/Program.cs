using System.Reflection;
using System.Security.Cryptography;
using Crecisol.Application.Identity;
using Crecisol.Application.Tenancy;
using Crecisol.Domain.Identity;
using Crecisol.Domain.Tenancy;

// Actual compiled .NET10 Domain/Application tests with synthetic trusted adapters.
// NO registration HTTP, real email verification, MySQL identity or membership writes.
var checks = 0;
void Check(bool ok, string name)
{
    if (!ok) throw new InvalidOperationException("FAIL C05-02: " + name);
    checks++;
}
void Deny<T>(Action action, string name) where T : Exception
{
    try { action(); }
    catch (T) { checks++; return; }
    throw new InvalidOperationException("FAIL expected " + typeof(T).Name + ": " + name);
}
async Task DenyAsync<T>(Func<Task> action, string name) where T : Exception
{
    try { await action(); }
    catch (T) { checks++; return; }
    throw new InvalidOperationException("FAIL expected " + typeof(T).Name + ": " + name);
}

var baseTime = new DateTimeOffset(2026, 11, 12, 18, 0, 0, TimeSpan.Zero);
var a = new Fixture(baseTime);
Check(NeutralRegistrationReceipt.Acknowledged.Code == "REQUEST_ACCEPTED_IF_ELIGIBLE" &&
      ReferenceEquals(NeutralRegistrationReceipt.Acknowledged, NeutralRegistrationReceipt.Acknowledged),
      "Neutral acknowledgement carries neither account existence nor recipient email");
Check(!typeof(NeutralRegistrationReceipt).GetProperties()
    .Any(x => x.Name.Contains("Email", StringComparison.OrdinalIgnoreCase) ||
              x.Name.Contains("Exists", StringComparison.OrdinalIgnoreCase)),
    "Registration receipt has no enumeration-capable fields");
Check(InvitationId.From(a.InvitationId.Value) == a.InvitationId &&
      RecipientChannelBindingId.From(a.Recipient.Value) == a.Recipient,
      "Opaque invitation and channel IDs have stable identity");
Deny<InvalidOperationException>(() => InvitationId.From(Guid.Empty), "Reject empty invite ID");
Deny<InvalidOperationException>(() => RecipientChannelBindingId.From(Guid.Empty),
    "Reject empty recipient ID");
Deny<InvalidOperationException>(() => InvitationTokenDigest.FromToken(new byte[31]),
    "Reject weak or truncated token");
Deny<InvalidOperationException>(() => InvitationIssuePolicyDecision.FromTrustedPolicy(
    true, TimeSpan.FromSeconds(5), 1), "Reject dangerous policy TTL");
Deny<InvalidOperationException>(() => InvitationIssuePolicyDecision.FromTrustedPolicy(
    true, TimeSpan.FromDays(20), 1), "Reject excessively long policy TTL");
Deny<InvalidOperationException>(() => InvitationIssuePolicyDecision.FromTrustedPolicy(
    true, TimeSpan.FromHours(2), 0), "Reject absent terms version");
Deny<InvalidOperationException>(() => InvitationSnapshot.RestoreFromTrustedInvitationStore(
    a.InvitationId, a.TenantA, a.IssuerActor, a.Recipient,
    AccountTenantProfileKind.Owner, InvitationLifecycleState.Pending,
    InvitationTokenDigest.FromToken(new byte[32]), 1, 42,
    baseTime, baseTime, baseTime.AddHours(2)), "Owner invitation forbidden");
Deny<InvalidOperationException>(() => InvitationSnapshot.RestoreFromTrustedInvitationStore(
    a.InvitationId, a.TenantA, a.IssuerActor, a.Recipient,
    AccountTenantProfileKind.Employee, (InvitationLifecycleState)123,
    InvitationTokenDigest.FromToken(new byte[32]), 1, 42,
    baseTime, baseTime, baseTime.AddHours(2)), "Undefined invitation lifecycle");
Deny<InvalidOperationException>(() => InvitationSnapshot.RestoreFromTrustedInvitationStore(
    a.InvitationId, a.TenantA, a.IssuerActor, a.Recipient,
    AccountTenantProfileKind.Employee, InvitationLifecycleState.Pending,
    InvitationTokenDigest.FromToken(new byte[32]), 0, 42,
    baseTime, baseTime, baseTime.AddHours(2)), "Zero revision forbidden");
Deny<InvalidOperationException>(() => InvitationSnapshot.RestoreFromTrustedInvitationStore(
    a.InvitationId, a.TenantA, a.IssuerActor, a.Recipient,
    AccountTenantProfileKind.Employee, InvitationLifecycleState.Pending,
    InvitationTokenDigest.FromToken(new byte[32]), 1, 42,
    baseTime, baseTime, baseTime), "Zero-length invitation TTL");
Deny<InvalidOperationException>(() => InvitationSnapshot.RequireUtc(
    baseTime.ToOffset(TimeSpan.FromHours(-6))), "Non-UTC invitation time");
Deny<InvalidOperationException>(() => VerifiedRecipientChannel.FromTrustedBackendVerification(
    a.RecipientActor, a.Recipient, 0, baseTime, baseTime.AddHours(2)),
    "Unversioned channel verification");
Deny<InvalidOperationException>(() => VerifiedInvitationTermsAcceptance.FromTrustedConsentLedger(
    a.TenantA, a.Recipient, 0, baseTime), "Unversioned legal terms");

using (var token = EphemeralInvitationCode.Generate())
{
    var hash = token.CreateDigest();
    var transferred = token.TransferToTrustedSenderOnce();
    try
    {
        Check(transferred.Length == 32 && hash.Matches(transferred),
            "CSPRNG token generates 256-bit digest");
        Check(!hash.Matches(new byte[31]) && !hash.Matches(new byte[32]),
            "Short or wrong invitation code rejected");
        Check(hash.SameDigestAs(InvitationTokenDigest.FromToken(transferred)),
            "Stable purpose-separated SHA-256 verifier");
        Check(hash.CopyForProtectedPersistenceAdapter().Length == 32,
            "Only fixed-size hash can reach future protected persistence");
        Check(token.ToString() == "EphemeralInvitationCode[redacted]" &&
              hash.ToString() == "InvitationTokenDigest[redacted]",
            "No secrets appear in diagnostics");
        Deny<InvalidOperationException>(() => token.TransferToTrustedSenderOnce(),
            "Token transfer is one-time");
        Deny<InvalidOperationException>(() => token.CreateDigest(),
            "Consumed token cannot be re-derived");
    }
    finally { CryptographicOperations.ZeroMemory(transferred); }
}
using (var disposed = EphemeralInvitationCode.Generate())
{
    disposed.Dispose();
    Deny<InvalidOperationException>(() => disposed.TransferToTrustedSenderOnce(),
        "Disposed capability is unusable");
}

using (var issued = await a.NewIssuer().ExecuteAsync(
    a.Recipient, AccountTenantProfileKind.Employee, CancellationToken.None))
{
    var snapshot = a.Ledger.LastInserted ?? throw new InvalidOperationException("Missing ledger row");
    Check(snapshot.Id == issued.Id && snapshot.TenantId == a.TenantA &&
          snapshot.IssuedBy == a.IssuerActor && snapshot.Recipient == a.Recipient &&
          snapshot.State == InvitationLifecycleState.Pending &&
          snapshot.Profile == AccountTenantProfileKind.Employee &&
          snapshot.TermsVersion == 42 && snapshot.Revision == 1,
          "Issuer records exact trusted tenant, recipient, version and neutral profile");
    Check(snapshot.IsPendingAtUtc(a.Now) && !snapshot.IsPendingAtUtc(a.Now.AddHours(2)),
          "Pending invitation expires at exact exclusive UTC boundary");
    Check(snapshot.ToString() == "InvitationSnapshot[redacted]" &&
          issued.ToString() == "TrustedInvitationIssuance[redacted]",
          "Sensitive issue diagnostics are redacted");
    Check(a.PolicyCalls == 2 && a.TargetCalls == 2 &&
          a.Ledger.InsertCalls == 1, "Policy and recipient independently rechecked before insertion");

    var bytes = issued.DeliveryCapability.TransferToTrustedSenderOnce();
    try
    {
        Check(snapshot.Digest.Matches(bytes), "Stored hash matches transferred code");
        Check(!snapshot.Digest.Matches(new byte[32]), "Other tenant cannot guess a token");
        var receipt = await a.NewClaim().ExecuteAsync(
            snapshot.Id, bytes, CancellationToken.None);
        Check(receipt.Id == snapshot.Id &&
              receipt.ToString() == "InternalInvitationClaimReceipt[redacted]",
            "Claim receipt is non-authorizing and redacted");
        Check(a.Ledger.Claimed == 1, "Exactly one successful consumption");
        await DenyAsync<UnauthorizedAccessException>(() =>
            a.NewClaim().ExecuteAsync(snapshot.Id, bytes, CancellationToken.None),
            "Replay after consumption denied");
        Check(a.Ledger.Claimed == 1, "Replay cannot change state twice");
    }
    finally { CryptographicOperations.ZeroMemory(bytes); }
}

// Cross-tenant: a verified host B never reads/claims an A invitation.
var cross = new Fixture(baseTime);
using (var issued = await cross.NewIssuer().ExecuteAsync(
    cross.Recipient, AccountTenantProfileKind.Consumer, CancellationToken.None))
{
    var code = issued.DeliveryCapability.TransferToTrustedSenderOnce();
    try
    {
        cross.ActiveHost = cross.HostB;
        await DenyAsync<UnauthorizedAccessException>(() =>
            cross.NewClaim().ExecuteAsync(issued.Id, code, CancellationToken.None),
            "Host B cannot claim invitation issued to tenant A");
        Check(cross.Ledger.Claimed == 0,
            "Cross-tenant attempt cannot mutate A invitation");
        cross.ActiveHost = cross.HostA;
        Check((await cross.NewClaim().ExecuteAsync(issued.Id, code, CancellationToken.None))
            .Id == issued.Id, "Actual tenant A may claim with exact trusted proofs");
    }
    finally { CryptographicOperations.ZeroMemory(code); }
}

// Offensive matrix: fresh fixture for each mutation to avoid hidden state.
async Task RejectClaim(string name, Action<Fixture> mutate, bool wrongCode = false)
{
    var fx = new Fixture(baseTime);
    using var issued = await fx.NewIssuer().ExecuteAsync(
        fx.Recipient, AccountTenantProfileKind.Employee, CancellationToken.None);
    var code = issued.DeliveryCapability.TransferToTrustedSenderOnce();
    try
    {
        mutate(fx);
        await DenyAsync<UnauthorizedAccessException>(() =>
            fx.NewClaim().ExecuteAsync(issued.Id,
                wrongCode ? new byte[32] : code, CancellationToken.None), name);
        Check(fx.Ledger.Claimed == 0, "Rejected claim cannot consume invitation: " + name);
    }
    finally { CryptographicOperations.ZeroMemory(code); }
}

await RejectClaim("Wrong 256-bit secret rejected", _ => { }, wrongCode: true);
await RejectClaim("Missing recipient verification denied", f => f.HasRecipient = false);
await RejectClaim("Revoked channel proof denied", f => f.RecipientProofExpired = true);
await RejectClaim("Different recipient channel denied", f => f.UseOtherRecipient = true);
await RejectClaim("Self-invitation cannot claim", f => f.UseIssuerAsRecipient = true);
await RejectClaim("Missing consent denied", f => f.HasConsent = false);
await RejectClaim("Consent from tenant B denied", f => f.TermsTenant = f.TenantB);
await RejectClaim("Stale terms policy denied", f => f.TermsVersion = 41);
await RejectClaim("Published tenant policy changed denied", f => f.PublishedTermsVersion = 43);
await RejectClaim("Published tenant terms drift mid-flight denied",
    f => f.ChangePublishedTermsOnSecondRead = true);
await RejectClaim("Future consent denied", f => f.TermsInFuture = true);
await RejectClaim("Unverified direct host denied", f => f.ActiveHost = f.OtherHost);
await RejectClaim("Suspended tenant denied", f => f.ActiveTenant = false);
await RejectClaim("Invitation revoked denied", f => f.Ledger.ChangeState(InvitationLifecycleState.Revoked, f.Now));
await RejectClaim("Expired invite denied", f => f.Now = f.Now.AddHours(3));
await RejectClaim("Duplicate catalog rows denied", f => f.Ledger.ReturnDuplicates = true);
await RejectClaim("No invitation record denied", f => f.Ledger.ReturnMissing = true);
await RejectClaim("State revision changes between reads denied",
    f => f.Ledger.RevokeAfterFirstRead = true);
await RejectClaim("Tenant suspended only at atomic port denied",
    f => f.Ledger.AllowAtomicTenant = false);
await RejectClaim("Invitation issuer revoked before atomic update denied",
    f => f.Ledger.AllowAtomicIssuer = false);
await RejectClaim("Consent revoked before atomic update denied",
    f => f.Ledger.AllowAtomicConsent = false);
await RejectClaim("Second read from different recipient fails closed",
    f => f.ChangeRecipientOnSecondRead = true);
await RejectClaim("Terms change on second read fails closed",
    f => f.ChangeTermsOnSecondRead = true);
await RejectClaim("Tenant changes on second read fails closed",
    f => f.ChangeTenantOnSecondRead = true);

// Deny wrong input and unsolicited/unauthorized issuance.
await DenyAsync<UnauthorizedAccessException>(() =>
    new Fixture(baseTime).NewIssuer().ExecuteAsync(
        a.Recipient, AccountTenantProfileKind.Owner, CancellationToken.None),
    "Owner invite cannot be requested via client profile");
await DenyAsync<UnauthorizedAccessException>(() =>
    new Fixture(baseTime).NewIssuer().ExecuteAsync(
        a.Recipient, (AccountTenantProfileKind)999, CancellationToken.None),
    "Unknown invite profile cannot grant membership");
var noPolicy = new Fixture(baseTime) { AuthorizedIssuer = false };
await DenyAsync<UnauthorizedAccessException>(() => noPolicy.NewIssuer().ExecuteAsync(
    noPolicy.Recipient, AccountTenantProfileKind.Employee, CancellationToken.None),
    "Unapproved issuer fails closed");
var noTarget = new Fixture(baseTime) { EligibleTarget = false };
await DenyAsync<UnauthorizedAccessException>(() => noTarget.NewIssuer().ExecuteAsync(
    noTarget.Recipient, AccountTenantProfileKind.Employee, CancellationToken.None),
    "Unknown recipient not permitted by trusted target directory");
var changingPolicy = new Fixture(baseTime) { RevokePolicyOnSecondRead = true };
await DenyAsync<UnauthorizedAccessException>(() => changingPolicy.NewIssuer().ExecuteAsync(
    changingPolicy.Recipient, AccountTenantProfileKind.Employee, CancellationToken.None),
    "Concurrent issuer policy revocation prevents insertion");
Check(changingPolicy.Ledger.InsertCalls == 0, "No token persisted after changing policy");
var revokedSession = new Fixture(baseTime) { SessionStatus = ActorSessionState.Revoked };
await DenyAsync<UnauthorizedAccessException>(() => revokedSession.NewIssuer().ExecuteAsync(
    revokedSession.Recipient, AccountTenantProfileKind.Employee, CancellationToken.None),
    "Revoked C04 verified session cannot issue");
var suspendedTenant = new Fixture(baseTime) { ActiveTenant = false };
await DenyAsync<UnauthorizedAccessException>(() => suspendedTenant.NewIssuer().ExecuteAsync(
    suspendedTenant.Recipient, AccountTenantProfileKind.Employee, CancellationToken.None),
    "Inactive verified tenant cannot issue");
var deniedLedger = new Fixture(baseTime);
deniedLedger.Ledger.AllowInsert = false;
await DenyAsync<UnauthorizedAccessException>(() => deniedLedger.NewIssuer().ExecuteAsync(
    deniedLedger.Recipient, AccountTenantProfileKind.Employee, CancellationToken.None),
    "Duplicate or failed insertion denied");
Check(deniedLedger.Ledger.Claimed == 0, "Failed insert cannot create claimed token");

// Verify 16 concurrent claims of one capability result in exactly one winner.
var parallel = new Fixture(baseTime);
using (var issued = await parallel.NewIssuer().ExecuteAsync(
    parallel.Recipient, AccountTenantProfileKind.Employee, CancellationToken.None))
{
    var code = issued.DeliveryCapability.TransferToTrustedSenderOnce();
    try
    {
        var tasks = Enumerable.Range(0, 16).Select(_ => Task.Run(async () =>
        {
            try
            {
                await parallel.NewClaim().ExecuteAsync(issued.Id, code, CancellationToken.None);
                return true;
            }
            catch (UnauthorizedAccessException) { return false; }
        })).ToArray();
        var result = await Task.WhenAll(tasks);
        Check(result.Count(x => x) == 1 && parallel.Ledger.Claimed == 1,
            "Sixteen parallel token consumptions have EXACTLY ONE winner");
    }
    finally { CryptographicOperations.ZeroMemory(code); }
}

using (var aborted = new CancellationTokenSource())
{
    aborted.Cancel();
    await DenyAsync<OperationCanceledException>(() =>
        new Fixture(baseTime).NewIssuer().ExecuteAsync(
            a.Recipient, AccountTenantProfileKind.Consumer, aborted.Token),
        "Cancelled issuance cannot persist a token");
    await DenyAsync<OperationCanceledException>(() =>
        new Fixture(baseTime).NewClaim().ExecuteAsync(
            a.InvitationId, new byte[32], aborted.Token),
        "Cancelled claim cannot mutate ledger");
}

var issueParameters = typeof(IssueInternalInvitation)
    .GetMethod(nameof(IssueInternalInvitation.ExecuteAsync))!.GetParameters();
Check(issueParameters.Length == 3 &&
      issueParameters.All(p => p.ParameterType != typeof(TenantId) &&
                               p.ParameterType != typeof(TenantActorId) &&
                               p.ParameterType != typeof(string)),
      "Issuer cannot accept user-controlled tenant, actor, host, email or raw string");
var claimParameters = typeof(ClaimInternalInvitation)
    .GetMethod(nameof(ClaimInternalInvitation.ExecuteAsync))!.GetParameters();
Check(claimParameters.Length == 3 &&
      claimParameters[0].ParameterType == typeof(InvitationId) &&
      claimParameters[1].ParameterType == typeof(ReadOnlyMemory<byte>) &&
      claimParameters[2].ParameterType == typeof(CancellationToken),
      "Claim input cannot select tenant, actor, role or backend DSN");
Check(!typeof(InvitationSnapshot).GetProperties(BindingFlags.Public | BindingFlags.Instance)
    .Any(p => p.PropertyType == typeof(string) || p.PropertyType == typeof(byte[])),
    "Snapshot exposes no raw token, email/PII, password or bearer field");
Check(typeof(InternalInvitationClaimReceipt).GetProperties().Length == 1,
    "Claim result contains no implicit membership, user role or JWT");

Console.WriteLine($"PASS C05-02 .NET10 invitation contracts: {checks} positive/negative assertions");
Console.WriteLine("PASS C05-02 cross-tenant, token expiry/replay/revocation, 16 concurrent claims ONE winner");
Console.WriteLine("LIMIT: synthetic trusted ports, no HTTP/email delivery/MySQL identity transactions or real registration");

sealed class Fixture
{
    private readonly DateTimeOffset initial;
    public Fixture(DateTimeOffset initial)
    {
        this.initial = initial;
        Now = initial;
        Ledger = new FakeInvitationLedger();
    }

    public DateTimeOffset Now { get; set; }
    public readonly TenantActorId IssuerActor =
        TenantActorId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01"));
    public readonly TenantActorId RecipientActor =
        TenantActorId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb02"));
    public readonly TenantId TenantA =
        TenantId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
    public readonly TenantId TenantB =
        TenantId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
    public readonly CanonicalTenantHost HostA = CanonicalTenantHost.Parse("tenant-a.example.test");
    public readonly CanonicalTenantHost HostB = CanonicalTenantHost.Parse("tenant-b.example.test");
    public readonly CanonicalTenantHost OtherHost = CanonicalTenantHost.Parse("unverified.example.test");
    public readonly InvitationId InvitationId =
        InvitationId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa12"));
    public readonly RecipientChannelBindingId Recipient =
        RecipientChannelBindingId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa34"));
    public readonly RecipientChannelBindingId OtherRecipient =
        RecipientChannelBindingId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb34"));
    public FakeInvitationLedger Ledger { get; }
    public CanonicalTenantHost? ActiveHost { get; set; }
    public bool ActiveTenant { get; set; } = true;
    public bool AuthorizedIssuer { get; set; } = true;
    public bool EligibleTarget { get; set; } = true;
    public bool RevokePolicyOnSecondRead { get; set; }
    public bool HasRecipient { get; set; } = true;
    public bool RecipientProofExpired { get; set; }
    public bool UseOtherRecipient { get; set; }
    public bool UseIssuerAsRecipient { get; set; }
    public bool HasConsent { get; set; } = true;
    public bool TermsInFuture { get; set; }
    public bool ChangeRecipientOnSecondRead { get; set; }
    public bool ChangeTermsOnSecondRead { get; set; }
    public bool ChangeTenantOnSecondRead { get; set; }
    public TenantId? TermsTenant { get; set; }
    public long TermsVersion { get; set; } = 42;
    public long PublishedTermsVersion { get; set; } = 42;
    public bool ChangePublishedTermsOnSecondRead { get; set; }
    public int PublishedTermsReads { get; private set; }
    public ActorSessionState SessionStatus { get; set; } = ActorSessionState.Active;
    public int PolicyCalls { get; private set; }
    public int TargetCalls { get; private set; }
    public int RecipientReads { get; private set; }
    public int TermsReads { get; private set; }
    public int TenantReads { get; private set; }

    public ITrustedHostUtcClock Clock => new ClockPort(() => Now);

    public ITrustedCurrentDirectHostReader DirectHost =>
        new HostReader(() => ActiveHost ?? HostA);

    public ITrustedVerifiedHostCatalog Hosts =>
        new HostCatalog(h =>
        {
            if (h == HostA) return new[] { HostBinding(TenantA, HostA) };
            if (h == HostB) return new[] { HostBinding(TenantB, HostB) };
            return Array.Empty<VerifiedTenantHostBinding>();
        });

    public ITrustedTenantCatalogReader Tenants =>
        new TenantCatalog((id) =>
        {
            TenantReads++;
            var tenantId = ChangeTenantOnSecondRead && TenantReads >= 2 ? TenantB : id;
            return TenantCatalogSnapshot.RestoreFromTrustedCatalog(
                tenantId, "internal-safe", "Safe business",
                ActiveTenant ? TenantLifecycleState.Active : TenantLifecycleState.Suspended,
                TenantAddressMode.Subdomain, 9, initial.AddDays(-1));
        });

    private VerifiedTenantHostBinding HostBinding(TenantId tenant, CanonicalTenantHost host) =>
        VerifiedTenantHostBinding.RestoreFromTrustedCatalog(
            tenant, host, TenantAddressMode.Subdomain, HostVerificationState.Verified,
            8, initial.AddDays(-1), initial.AddDays(-2), initial.AddDays(4));

    public ITrustedCurrentRecipientChannel Recipients =>
        new RecipientReader(() =>
        {
            RecipientReads++;
            if (!HasRecipient) return null;
            var binding = (UseOtherRecipient || (ChangeRecipientOnSecondRead && RecipientReads >= 2))
                ? OtherRecipient : Recipient;
            return VerifiedRecipientChannel.FromTrustedBackendVerification(
                UseIssuerAsRecipient ? IssuerActor : RecipientActor, binding, 8,
                initial.AddDays(-1),
                RecipientProofExpired ? initial.AddMinutes(-1) : initial.AddDays(4));
        });

    public ITrustedInvitationConsentLedger Terms =>
        new ConsentReader((tenant, recipient) =>
        {
            TermsReads++;
            if (!HasConsent) return null;
            return VerifiedInvitationTermsAcceptance.FromTrustedConsentLedger(
                TermsTenant ?? tenant, recipient,
                ChangeTermsOnSecondRead && TermsReads >= 2 ? 41 : TermsVersion,
                TermsInFuture ? Now.AddMinutes(1) : initial.AddHours(-2));
        });

    public IssueInternalInvitation NewIssuer()
    {
        var clock = Clock;
        var context = new ResolveCurrentTenantIdentityContext(
            new SessionReader(() => TrustedActorSessionSnapshot.RestoreFromTrustedIdentityProvider(
                IssuerActor, SessionStatus, 4, initial.AddDays(-3), initial.AddHours(-2),
                initial.AddDays(3))),
            DirectHost,
            new ResolveVerifiedTenantHost(Hosts, clock),
            new MembershipCatalog((actor, tenant) =>
                new[] { TenantActorMembershipSnapshot.RestoreFromTrustedMembershipCatalog(
                    actor, tenant, TenantMembershipState.Active, 5, initial.AddDays(-1),
                    initial.AddDays(-2), initial.AddDays(3)) }),
            Tenants, clock);

        var reader = new AccountReader(actor =>
            IdentityAccountSnapshot.RestoreFromTrustedIdentityDirectory(
                actor, IdentityAccountState.Active, 7, initial.AddDays(-1)));
        var links = new AccountLinks((actor, tenant) =>
            new[] { AccountTenantLinkSnapshot.RestoreFromTrustedMembershipDirectory(
                AccountTenantLinkId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa22")),
                actor, tenant, AccountTenantProfileKind.Employee,
                TenantMembershipState.Active, 5, initial.AddDays(-1),
                initial.AddDays(-2), initial.AddDays(3)) });
        return new IssueInternalInvitation(
            new ResolveCurrentAccountTenantLink(context, reader, links, clock),
            new PolicyPort((binding, target) =>
            {
                PolicyCalls++;
                var allowed = AuthorizedIssuer &&
                    !(RevokePolicyOnSecondRead && PolicyCalls >= 2) &&
                    binding.TenantId == TenantA &&
                    target is AccountTenantProfileKind.Employee or AccountTenantProfileKind.Consumer;
                return InvitationIssuePolicyDecision.FromTrustedPolicy(
                    allowed, TimeSpan.FromHours(2), 42);
            }),
            new TargetPort((tenant, recipient) =>
            {
                TargetCalls++;
                return EligibleTarget && tenant == TenantA && recipient == Recipient;
            }),
            Ledger, clock);
    }

    public ClaimInternalInvitation NewClaim()
    {
        var clock = Clock;
        return new ClaimInternalInvitation(
            DirectHost, new ResolveVerifiedTenantHost(Hosts, clock),
            Tenants, Recipients, Terms,
            new CurrentTermsPort(tenant =>
            {
                PublishedTermsReads++;
                return ChangePublishedTermsOnSecondRead && PublishedTermsReads >= 2
                    ? PublishedTermsVersion + 1 : PublishedTermsVersion;
            }),
            Ledger, clock);
    }
}

sealed class ClockPort(Func<DateTimeOffset> now) : ITrustedHostUtcClock
{
    public DateTimeOffset GetUtcNow() => now();
}
sealed class HostReader(Func<CanonicalTenantHost?> read) : ITrustedCurrentDirectHostReader
{
    public ValueTask<CanonicalTenantHost?> ReadCurrentDirectHostAsync(CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); return ValueTask.FromResult(read()); }
}
sealed class HostCatalog(Func<CanonicalTenantHost, IReadOnlyList<VerifiedTenantHostBinding>> read)
    : ITrustedVerifiedHostCatalog
{
    public Task<IReadOnlyList<VerifiedTenantHostBinding>?> FindExactBindingsAsync(
        CanonicalTenantHost host, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); return Task.FromResult<IReadOnlyList<VerifiedTenantHostBinding>?>(read(host)); }
}
sealed class TenantCatalog(Func<TenantId, TenantCatalogSnapshot?> read) : ITrustedTenantCatalogReader
{
    public Task<TenantCatalogSnapshot?> FindByIdAsync(TenantId id, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); return Task.FromResult(read(id)); }
}
sealed class SessionReader(Func<TrustedActorSessionSnapshot?> read) : ITrustedCurrentActorSessionReader
{
    public ValueTask<TrustedActorSessionSnapshot?> ReadCurrentVerifiedSessionAsync(CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); return ValueTask.FromResult(read()); }
}
sealed class MembershipCatalog(
    Func<TenantActorId, TenantId, IReadOnlyList<TenantActorMembershipSnapshot>?> read)
    : ITrustedActorTenantMembershipReader
{
    public Task<IReadOnlyList<TenantActorMembershipSnapshot>?> FindExactMembershipsAsync(
        TenantActorId actor, TenantId tenant, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); return Task.FromResult(read(actor, tenant)); }
}
sealed class AccountReader(Func<TenantActorId, IdentityAccountSnapshot?> read)
    : ITrustedIdentityAccountReader
{
    public Task<IdentityAccountSnapshot?> FindByVerifiedActorAsync(
        TenantActorId actor, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); return Task.FromResult(read(actor)); }
}
sealed class AccountLinks(
    Func<TenantActorId, TenantId, IReadOnlyList<AccountTenantLinkSnapshot>?> read)
    : ITrustedAccountTenantLinkReader
{
    public Task<IReadOnlyList<AccountTenantLinkSnapshot>?> FindExactLinksAsync(
        TenantActorId actor, TenantId tenant, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); return Task.FromResult(read(actor, tenant)); }
}
sealed class PolicyPort(
    Func<AccountTenantBindingEvidence, AccountTenantProfileKind, InvitationIssuePolicyDecision?> policy)
    : ITrustedInvitationIssuerPolicy
{
    public Task<InvitationIssuePolicyDecision?> EvaluateAsync(
        AccountTenantBindingEvidence actor, AccountTenantProfileKind profile, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); return Task.FromResult(policy(actor, profile)); }
}
sealed class TargetPort(Func<TenantId, RecipientChannelBindingId, bool> eligible)
    : ITrustedInvitationTargetDirectory
{
    public Task<bool> IsEligibleAsync(
        TenantId tenant, RecipientChannelBindingId recipient, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); return Task.FromResult(eligible(tenant, recipient)); }
}
sealed class CurrentTermsPort(Func<TenantId, long?> current)
    : ITrustedCurrentInvitationTermsPolicy
{
    public Task<long?> ReadCurrentTermsVersionAsync(TenantId tenant, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); return Task.FromResult(current(tenant)); }
}
sealed class RecipientReader(Func<VerifiedRecipientChannel?> proof) : ITrustedCurrentRecipientChannel
{
    public Task<VerifiedRecipientChannel?> ReadVerifiedAsync(CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); return Task.FromResult(proof()); }
}
sealed class ConsentReader(
    Func<TenantId, RecipientChannelBindingId, VerifiedInvitationTermsAcceptance?> reader)
    : ITrustedInvitationConsentLedger
{
    public Task<VerifiedInvitationTermsAcceptance?> ReadAcceptedAsync(
        TenantId tenant, RecipientChannelBindingId recipient, CancellationToken ct)
    { ct.ThrowIfCancellationRequested(); return Task.FromResult(reader(tenant, recipient)); }
}

// Fake CAS is a synchronized simulation only; it is NOT MySQL implementation.
sealed class FakeInvitationLedger : ITrustedInvitationLedger
{
    private readonly object sync = new();
    private readonly Dictionary<(Guid tenant, Guid invitation), InvitationSnapshot> rows = new();
    public bool AllowInsert { get; set; } = true;
    public bool ReturnMissing { get; set; }
    public bool ReturnDuplicates { get; set; }
    public bool RevokeAfterFirstRead { get; set; }
    public bool AllowAtomicTenant { get; set; } = true;
    public bool AllowAtomicIssuer { get; set; } = true;
    public bool AllowAtomicConsent { get; set; } = true;
    public int InsertCalls { get; private set; }
    public int ReadCalls { get; private set; }
    public int Claimed { get; private set; }
    public InvitationSnapshot? LastInserted { get; private set; }

    public Task<bool> TryInsertUniqueAsync(InvitationSnapshot row, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (sync)
        {
            InsertCalls++;
            var key = (row.TenantId.Value, row.Id.Value);
            if (!AllowInsert || rows.ContainsKey(key))
                return Task.FromResult(false);
            rows.Add(key, row);
            LastInserted = row;
            return Task.FromResult(true);
        }
    }

    public Task<IReadOnlyList<InvitationSnapshot>?> ReadExactAsync(
        TenantId tenant, InvitationId id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (sync)
        {
            ReadCalls++;
            var key = (tenant.Value, id.Value);
            if (ReturnMissing || !rows.TryGetValue(key, out var row))
                return Task.FromResult<IReadOnlyList<InvitationSnapshot>?>(
                    Array.Empty<InvitationSnapshot>());
            if (RevokeAfterFirstRead && ReadCalls >= 2 &&
                row.State == InvitationLifecycleState.Pending)
            {
                row = InvitationSnapshot.RestoreFromTrustedInvitationStore(
                    row.Id, row.TenantId, row.IssuedBy, row.Recipient,
                    row.Profile, InvitationLifecycleState.Revoked, row.Digest,
                    row.Revision + 1, row.TermsVersion,
                    row.IssuedUtc, row.ChangedUtc, row.ExpiresUtc);
                rows[key] = row;
            }
            IReadOnlyList<InvitationSnapshot> result =
                ReturnDuplicates ? new[] { row, row } : new[] { row };
            return Task.FromResult<IReadOnlyList<InvitationSnapshot>?>(result);
        }
    }

    public Task<bool> TryClaimOnceAtomicallyAsync(
        InvitationSnapshot expected, TenantActorId recipientActor,
        DateTimeOffset now, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (sync)
        {
            var key = (expected.TenantId.Value, expected.Id.Value);
            if (!AllowAtomicTenant || !AllowAtomicIssuer || !AllowAtomicConsent ||
                !rows.TryGetValue(key, out var current) ||
                current.State != InvitationLifecycleState.Pending ||
                current.Revision != expected.Revision ||
                current.Recipient != expected.Recipient ||
                current.IssuedBy == recipientActor ||
                current.TermsVersion != expected.TermsVersion ||
                !current.IsPendingAtUtc(now) ||
                !current.Digest.SameDigestAs(expected.Digest))
                return Task.FromResult(false);
            rows[key] = InvitationSnapshot.RestoreFromTrustedInvitationStore(
                current.Id, current.TenantId, current.IssuedBy, current.Recipient,
                current.Profile, InvitationLifecycleState.Claimed, current.Digest,
                current.Revision + 1, current.TermsVersion,
                current.IssuedUtc, now, current.ExpiresUtc);
            Claimed++;
            return Task.FromResult(true);
        }
    }

    public void ChangeState(InvitationLifecycleState state, DateTimeOffset now)
    {
        lock (sync)
        {
            if (LastInserted is null)
                throw new InvalidOperationException("No invitation to mutate");
            var old = LastInserted;
            var newRow = InvitationSnapshot.RestoreFromTrustedInvitationStore(
                old.Id, old.TenantId, old.IssuedBy, old.Recipient, old.Profile,
                state, old.Digest, old.Revision + 1, old.TermsVersion,
                old.IssuedUtc, now, old.ExpiresUtc);
            rows[(old.TenantId.Value, old.Id.Value)] = newRow;
        }
    }
}
