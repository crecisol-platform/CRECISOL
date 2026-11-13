using System.Security.Cryptography;
using Crecisol.Domain.Identity;
using Crecisol.Domain.Tenancy;
using Crecisol.Infrastructure.Identity;
using MySqlConnector;

// Actual .NET 10 / MySQL 8.4 disposable test, no HTTP, login, or membership grants.
// A C04-style trusted snapshot is reconstructed ONLY inside this test executable.
// This cannot substitute for cryptographic authentication in production.
if (args.Length != 1 || args[0] != "--integration")
    throw new InvalidOperationException("Only --integration is allowed.");
var adminDsn = Environment.GetEnvironmentVariable("CRECISOL_C05_02_BOUND_ADMIN_DSN")
    ?? throw new InvalidOperationException("Local setup DSN required.");
var workerDsn = Environment.GetEnvironmentVariable("CRECISOL_C05_02_BOUND_WORKER_DSN")
    ?? throw new InvalidOperationException("Local restricted worker DSN required.");
var options = new MySqlConnectionStringBuilder(adminDsn);
if (options.Database != "c05_02_disposable" ||
    options.Server is not ("127.0.0.1" or "localhost" or "::1"))
    throw new InvalidOperationException("Only disposable local setup is allowed.");

const string A = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
const string B = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
const string IA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaa0001";
const string IB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbb0001";
const string OA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaa0004";
const string RA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaa0003";
const string RA2 = "aaaaaaaaaaaaaaaaaaaaaaaaaaaa0006";
const string RB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbb0003";
const string LA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaa0009";
const string LB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbb0009";
const string BRA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaa0010";
const string BRB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbb0010";
const string RESA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaa0011";
const string RESB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbb0011";
var tenantA = TenantId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
var tenantB = TenantId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
var issuerA = TenantActorId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0001"));
var issuerB = TenantActorId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbb0001"));
var outsiderA = TenantActorId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0004"));
var branchA = TenantBranchId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0010"));
var branchB = TenantBranchId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbb0010"));
var resourceA = TenantResourceId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0011"));
var resourceB = TenantResourceId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbb0011"));
var recipientA = RecipientChannelBindingId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0003"));
var recipientA2 = RecipientChannelBindingId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0006"));
var recipientB = RecipientChannelBindingId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbb0003"));
var linkA = AccountTenantLinkId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0009"));
var linkB = AccountTenantLinkId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbb0009"));
var checks = 0;
void Pass(bool ok, string description)
{
    if (!ok) throw new InvalidOperationException("FAIL C05-02 issuer: " + description);
    ++checks;
    Console.WriteLine("PASS C05-02 issuer: " + description);
}
await using var admin = new MySqlConnection(adminDsn);
await admin.OpenAsync();
async Task SetupAsync(string sql)
{
    await using var command = new MySqlCommand(sql, admin);
    await command.ExecuteNonQueryAsync();
}
async Task<int> CountAsync(string sql)
{
    await using var command = new MySqlCommand(sql, admin);
    return Convert.ToInt32(await command.ExecuteScalarAsync());
}

await SetupAsync($"""
  INSERT INTO c05_02_tenants VALUES
    (X'{A}','ACTIVE',42),(X'{B}','ACTIVE',42);
  INSERT INTO c05_02_issuers VALUES
    (X'{A}',X'{IA}','INVITE_EMPLOYEE',1),
    (X'{A}',X'{OA}','INVITE_EMPLOYEE',0),
    (X'{B}',X'{IB}','INVITE_CONSUMER',1);
  INSERT INTO c05_02_recipients VALUES
    (X'{A}',X'{RA}',X'{RA}',1,DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 3 HOUR)),
    (X'{A}',X'{RA2}',X'{RA2}',1,DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 3 HOUR)),
    (X'{B}',X'{RB}',X'{RB}',1,DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 3 HOUR));
  INSERT INTO c05_02_auth_accounts VALUES
    (X'{A}',X'{IA}','ACTIVE',7),(X'{B}',X'{IB}','ACTIVE',7);
  INSERT INTO c05_02_auth_sessions VALUES
    (X'{A}',X'{IA}','ACTIVE',11,DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 3 HOUR)),
    (X'{B}',X'{IB}','ACTIVE',11,DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 3 HOUR));
  INSERT INTO c05_02_auth_links VALUES
    (X'{A}',X'{IA}',X'{LA}','ACTIVE',12,DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 3 HOUR)),
    (X'{B}',X'{IB}',X'{LB}','ACTIVE',12,DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 3 HOUR));
  INSERT INTO c05_02_auth_hosts VALUES
    (X'{A}','VERIFIED',13,14),(X'{B}','VERIFIED',13,14);
  INSERT INTO c05_02_auth_branches VALUES
    (X'{A}',X'{BRA}','ACTIVE',15),(X'{B}',X'{BRB}','ACTIVE',15);
  INSERT INTO c05_02_auth_resources VALUES
    (X'{A}',X'{RESA}',X'{BRA}',X'{IA}','ACTIVE',16),
    (X'{B}',X'{RESB}',X'{BRB}',X'{IB}','ACTIVE',16);
  INSERT INTO c05_02_auth_grants VALUES
    (X'{A}',X'{IA}',X'{BRA}','identity.invitation.issue','OWNER_ONLY',
     'ACTIVE',17,DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 3 HOUR)),
    (X'{B}',X'{IB}',X'{BRB}','identity.invitation.issue','OWNER_ONLY',
     'ACTIVE',17,DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 3 HOUR));
  INSERT INTO c05_02_auth_issue_policy VALUES
    (X'{A}','EMPLOYEE','identity.invitation.issue','ACTIVE',7200),
    (X'{B}','CONSUMER','identity.invitation.issue','ACTIVE',7200);
  """);


(AccountTenantBindingEvidence, TenantBranchResourceScopeEvidence) Evidence(
    TenantId tenant, TenantActorId actor, AccountTenantLinkId link,
    TenantBranchId branch, TenantResourceId resource, TenantActorId owner,
    BranchGrantScope scopeMode = BranchGrantScope.OwnerOnly,
    long accountRevision = 7, long sessionRevision = 11,
    long membershipRevision = 12, long hostRevision = 13,
    long catalogRevision = 14, long branchRevision = 15,
    long resourceRevision = 16, long grantRevision = 17,
    string action = "identity.invitation.issue")
{
    var now = DateTimeOffset.UtcNow;
    var before = now.AddMinutes(-5);
    var until = now.AddHours(2);
    var session = TrustedActorSessionSnapshot.RestoreFromTrustedIdentityProvider(
        actor, ActorSessionState.Active, sessionRevision, before, before, until);
    var membership = TenantActorMembershipSnapshot.RestoreFromTrustedMembershipCatalog(
        actor, tenant, TenantMembershipState.Active, membershipRevision,
        before, before, until);
    var host = new VerifiedTenantHostHint(
        tenant, TenantAddressMode.Subdomain, hostRevision);
    var catalog = TenantCatalogSnapshot.RestoreFromTrustedCatalog(
        tenant, "tenant-local", "Tenant Local", TenantLifecycleState.Active,
        TenantAddressMode.Subdomain, catalogRevision, before);
    var context = TenantIdentityContextEvidence.FromVerifiedInternalEvidence(
        session, membership, host, catalog);
    var account = IdentityAccountSnapshot.RestoreFromTrustedIdentityDirectory(
        actor, IdentityAccountState.Active, accountRevision, before);
    var accountLink = AccountTenantLinkSnapshot.RestoreFromTrustedMembershipDirectory(
        link, actor, tenant, AccountTenantProfileKind.Employee,
        TenantMembershipState.Active, membershipRevision,
        before, before, until);
    var binding = AccountTenantBindingEvidence.FromTrustedInternalSnapshots(
        context, account, accountLink, now);
    var required = TenantRequiredAction.FromServerPolicy(action);
    var storedResource = TrustedBranchResourceSnapshot.RestoreFromTrustedResourceReader(
        tenant, branch, resource, owner, TrustedBranchState.Active,
        branchRevision, before, TrustedResourceState.Active,
        resourceRevision, before);
    var grant = TrustedActorBranchGrantSnapshot.RestoreFromTrustedGrantReader(
        actor, tenant, branch, required, scopeMode,
        BranchGrantState.Active, grantRevision, before, before, until);
    var scoped = TenantBranchResourceScopeEvidence.FromTrustedInternalSnapshots(
        context, storedResource, grant, required, now);
    return (binding, scoped);
}

var a = Evidence(tenantA,issuerA,linkA,branchA,resourceA,issuerA);
var b = Evidence(tenantB,issuerB,linkB,branchB,resourceB,issuerB);


await SetupAsync($"""
   INSERT INTO c05_02_consents VALUES
     (X'{A}',X'{RA}',42,UTC_TIMESTAMP(6)),
     (X'{A}',X'{RA2}',42,UTC_TIMESTAMP(6)),
     (X'{B}',X'{RB}',42,UTC_TIMESTAMP(6));
   """);

var ledger = new MySqlDisposableBoundInvitationLedger(workerDsn);
var actorA = TenantActorId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0003"));
var actorA2 = TenantActorId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0006"));
var actorB = TenantActorId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbb0003"));

async Task<(InvitationSnapshot Snapshot, byte[] Token)> IssueAsync(
    TenantId tenant, TenantActorId issuer,
    RecipientChannelBindingId recipient, AccountTenantProfileKind profile,
    (AccountTenantBindingEvidence Binding, TenantBranchResourceScopeEvidence Scope) scope)
{
    using var token = EphemeralInvitationCode.Generate();
    var now = DateTimeOffset.UtcNow;
    var invitation = InvitationSnapshot.RestoreFromTrustedInvitationStore(
        InvitationId.From(Guid.NewGuid()), tenant, issuer, recipient,
        profile, InvitationLifecycleState.Pending, token.CreateDigest(),
        1, 42, now, now, now.AddMinutes(20));
    if (!await ledger.TryIssueAuthorizedOnceAsync(
        invitation, scope.Binding, scope.Scope, CancellationToken.None))
        throw new InvalidOperationException("Scoped issuer could not create bound invitation.");
    return (invitation, token.TransferToTrustedSenderOnce());
}

async Task<bool> ClaimAsync(TenantId tenant, InvitationId invitation,
    RecipientChannelBindingId recipient, TenantActorId actor, byte[] bearer)
{
    return await ledger.TryClaimBoundOnceAtomicallyAsync(
        tenant, invitation, recipient, actor,
        InvitationTokenDigest.FromToken(bearer),
        DateTimeOffset.UtcNow, CancellationToken.None);
}

async Task DenyWhileAsync(string label, string change, string restore,
    InvitationId invite, byte[] bearer)
{
    await SetupAsync(change);
    try
    {
        Pass(!await ClaimAsync(tenantA, invite, recipientA, actorA, bearer),
            "Claim rejects current " + label + " in SQL");
    }
    finally { await SetupAsync(restore); }
    Pass(await CountAsync($"""
        SELECT COUNT(*) FROM c05_02_invitations
         WHERE tenant_id=X'{A}' AND invitation_id=@invitation_id AND lifecycle='PENDING'
        """.Replace("@invitation_id",
            "X'" + Convert.ToHexString(Crecisol.Infrastructure.Tenancy.MySqlRfc4122Guid.Encode(invite.Value)) + "'"))==1,
        "A denied " + label + " claim leaves invitation pending");
}

var (inviteA, rawA) = await IssueAsync(
    tenantA, issuerA, recipientA, AccountTenantProfileKind.Employee, a);
var (inviteB, rawB) = await IssueAsync(
    tenantB, issuerB, recipientB, AccountTenantProfileKind.Consumer, b);
var (inviteA2, rawA2) = await IssueAsync(
    tenantA, issuerA, recipientA2, AccountTenantProfileKind.Employee, a);
try
{
    Pass(await CountAsync("SELECT COUNT(*) FROM c05_02_invite_scope_bindings")==3,
        "Issue writes exact immutable scope binding for all three invitations");
    Pass(await CountAsync("SELECT COUNT(*) FROM c05_02_invitations WHERE lifecycle='PENDING'")==3,
        "Three pending invitations exist with the same number of bound scopes");
    Pass(!await ClaimAsync(tenantB, inviteA.Id, recipientA, actorA, rawA),
        "Wrong tenant cannot claim even when invitation ID and bearer are known");
    Pass(!await ClaimAsync(tenantA, inviteB.Id, recipientB, actorB, rawB),
        "Tenant B ID never resolves in tenant A");
    Pass(!await ClaimAsync(tenantA, inviteA.Id, recipientB, actorA, rawA),
        "Recipient channel B cannot consume A invitation");
    Pass(!await ClaimAsync(tenantA, inviteA.Id, recipientA, issuerA, rawA),
        "The issuer cannot claim its own invitation");
    Pass(!await ClaimAsync(tenantA, inviteA.Id, recipientA, actorA2, rawA),
        "Synthetic actor mismatch cannot pass protected recipient binding");
    var wrong = RandomNumberGenerator.GetBytes(32);
    try
    {
        Pass(!await ClaimAsync(tenantA, inviteA.Id, recipientA, actorA, wrong),
            "Incorrect 256-bit capability cannot claim");
    }
    finally { CryptographicOperations.ZeroMemory(wrong); }
    var missing = InvitationId.From(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    Pass(!await ClaimAsync(tenantA, missing, recipientA, actorA, rawA),
        "Unknown invitation never reveals issuer, channel or existence");

    // The older test-only issue routine does not record a bound scope.
    // Even a well-formed legacy row must be unusable by this new claim gate.
    var orphan = InvitationId.From(Guid.Parse("22222222-2222-2222-2222-222222222222"));
    var orphanRaw = RandomNumberGenerator.GetBytes(32);
    var orphanHash = InvitationTokenDigest.FromToken(orphanRaw).CopyForProtectedPersistenceAdapter();
    try
    {
        await SetupAsync($"""
          INSERT INTO c05_02_invitations
          (tenant_id,invitation_id,issuer_id,recipient_id,profile_scope,
           token_digest,lifecycle,revision,terms_version,
           issued_at_utc,expires_at_utc,claimed_at_utc)
          VALUES (X'{A}',X'22222222222222222222222222222222',
                  X'{IA}',X'{RA}','EMPLOYEE',X'{Convert.ToHexString(orphanHash)}',
                  'PENDING',1,42,DATE_SUB(UTC_TIMESTAMP(6),INTERVAL 1 MINUTE),
                  DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 20 MINUTE),NULL);
          """);
        Pass(!await ClaimAsync(tenantA, orphan, recipientA, actorA, orphanRaw),
            "Legacy invitation without atomically persisted scope cannot be claimed");
    }
    finally
    {
        CryptographicOperations.ZeroMemory(orphanRaw);
        CryptographicOperations.ZeroMemory(orphanHash);
    }

    // Each revocation is performed AFTER issuance. The stored binding is not
    // allowed to inherit some newer unrelated or broader entitlement.
    await DenyWhileAsync("tenant suspension",
        $"UPDATE c05_02_tenants SET lifecycle='SUSPENDED' WHERE tenant_id=X'{A}'",
        $"UPDATE c05_02_tenants SET lifecycle='ACTIVE' WHERE tenant_id=X'{A}'",
        inviteA.Id, rawA);
    await DenyWhileAsync("issuer disablement",
        $"UPDATE c05_02_issuers SET enabled=0 WHERE tenant_id=X'{A}' AND actor_id=X'{IA}'",
        $"UPDATE c05_02_issuers SET enabled=1 WHERE tenant_id=X'{A}' AND actor_id=X'{IA}'",
        inviteA.Id, rawA);
    await DenyWhileAsync("issuer account suspension",
        $"UPDATE c05_02_auth_accounts SET lifecycle='SUSPENDED' WHERE tenant_id=X'{A}'",
        $"UPDATE c05_02_auth_accounts SET lifecycle='ACTIVE' WHERE tenant_id=X'{A}'",
        inviteA.Id, rawA);
    await DenyWhileAsync("issuer account revision",
        $"UPDATE c05_02_auth_accounts SET revision=8 WHERE tenant_id=X'{A}'",
        $"UPDATE c05_02_auth_accounts SET revision=7 WHERE tenant_id=X'{A}'",
        inviteA.Id, rawA);
    await DenyWhileAsync("session revocation",
        $"UPDATE c05_02_auth_sessions SET lifecycle='REVOKED' WHERE tenant_id=X'{A}'",
        $"UPDATE c05_02_auth_sessions SET lifecycle='ACTIVE' WHERE tenant_id=X'{A}'",
        inviteA.Id, rawA);
    await DenyWhileAsync("session revision",
        $"UPDATE c05_02_auth_sessions SET revision=19 WHERE tenant_id=X'{A}'",
        $"UPDATE c05_02_auth_sessions SET revision=11 WHERE tenant_id=X'{A}'",
        inviteA.Id, rawA);
    await DenyWhileAsync("session expiry",
        $"UPDATE c05_02_auth_sessions SET expires_utc=DATE_SUB(UTC_TIMESTAMP(6),INTERVAL 1 SECOND) WHERE tenant_id=X'{A}'",
        $"UPDATE c05_02_auth_sessions SET expires_utc=DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 3 HOUR) WHERE tenant_id=X'{A}'",
        inviteA.Id, rawA);
    await DenyWhileAsync("link revocation",
        $"UPDATE c05_02_auth_links SET lifecycle='REVOKED' WHERE tenant_id=X'{A}'",
        $"UPDATE c05_02_auth_links SET lifecycle='ACTIVE' WHERE tenant_id=X'{A}'",
        inviteA.Id, rawA);
    await DenyWhileAsync("link revision",
        $"UPDATE c05_02_auth_links SET revision=31 WHERE tenant_id=X'{A}'",
        $"UPDATE c05_02_auth_links SET revision=12 WHERE tenant_id=X'{A}'",
        inviteA.Id, rawA);
    await DenyWhileAsync("link expiry",
        $"UPDATE c05_02_auth_links SET expires_utc=DATE_SUB(UTC_TIMESTAMP(6),INTERVAL 1 SECOND) WHERE tenant_id=X'{A}'",
        $"UPDATE c05_02_auth_links SET expires_utc=DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 3 HOUR) WHERE tenant_id=X'{A}'",
        inviteA.Id, rawA);
    await DenyWhileAsync("host revocation",
        $"UPDATE c05_02_auth_hosts SET lifecycle='REVOKED' WHERE tenant_id=X'{A}'",
        $"UPDATE c05_02_auth_hosts SET lifecycle='VERIFIED' WHERE tenant_id=X'{A}'",
        inviteA.Id, rawA);
    await DenyWhileAsync("host revision",
        $"UPDATE c05_02_auth_hosts SET host_revision=44 WHERE tenant_id=X'{A}'",
        $"UPDATE c05_02_auth_hosts SET host_revision=13 WHERE tenant_id=X'{A}'",
        inviteA.Id, rawA);
    await DenyWhileAsync("catalog revision",
        $"UPDATE c05_02_auth_hosts SET catalog_revision=45 WHERE tenant_id=X'{A}'",
        $"UPDATE c05_02_auth_hosts SET catalog_revision=14 WHERE tenant_id=X'{A}'",
        inviteA.Id, rawA);
    await DenyWhileAsync("branch suspension",
        $"UPDATE c05_02_auth_branches SET lifecycle='SUSPENDED' WHERE tenant_id=X'{A}'",
        $"UPDATE c05_02_auth_branches SET lifecycle='ACTIVE' WHERE tenant_id=X'{A}'",
        inviteA.Id, rawA);
    await DenyWhileAsync("branch revision",
        $"UPDATE c05_02_auth_branches SET revision=99 WHERE tenant_id=X'{A}'",
        $"UPDATE c05_02_auth_branches SET revision=15 WHERE tenant_id=X'{A}'",
        inviteA.Id, rawA);
    await DenyWhileAsync("resource deletion",
        $"UPDATE c05_02_auth_resources SET lifecycle='DELETED' WHERE tenant_id=X'{A}'",
        $"UPDATE c05_02_auth_resources SET lifecycle='ACTIVE' WHERE tenant_id=X'{A}'",
        inviteA.Id, rawA);
    await DenyWhileAsync("resource revision",
        $"UPDATE c05_02_auth_resources SET revision=99 WHERE tenant_id=X'{A}'",
        $"UPDATE c05_02_auth_resources SET revision=16 WHERE tenant_id=X'{A}'",
        inviteA.Id, rawA);
    await DenyWhileAsync("resource owner replaced",
        $"UPDATE c05_02_auth_resources SET owner_actor_id=X'{OA}' WHERE tenant_id=X'{A}'",
        $"UPDATE c05_02_auth_resources SET owner_actor_id=X'{IA}' WHERE tenant_id=X'{A}'",
        inviteA.Id, rawA);
    await DenyWhileAsync("grant revoked",
        $"UPDATE c05_02_auth_grants SET lifecycle='REVOKED' WHERE tenant_id=X'{A}'",
        $"UPDATE c05_02_auth_grants SET lifecycle='ACTIVE' WHERE tenant_id=X'{A}'",
        inviteA.Id, rawA);
    await DenyWhileAsync("grant revision",
        $"UPDATE c05_02_auth_grants SET revision=33 WHERE tenant_id=X'{A}'",
        $"UPDATE c05_02_auth_grants SET revision=17 WHERE tenant_id=X'{A}'",
        inviteA.Id, rawA);
    await DenyWhileAsync("grant expiry",
        $"UPDATE c05_02_auth_grants SET expires_utc=DATE_SUB(UTC_TIMESTAMP(6),INTERVAL 1 SECOND) WHERE tenant_id=X'{A}'",
        $"UPDATE c05_02_auth_grants SET expires_utc=DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 3 HOUR) WHERE tenant_id=X'{A}'",
        inviteA.Id, rawA);
    await DenyWhileAsync("scope broadened after issuance",
        $"UPDATE c05_02_auth_grants SET scope='BRANCH_WIDE' WHERE tenant_id=X'{A}'",
        $"UPDATE c05_02_auth_grants SET scope='OWNER_ONLY' WHERE tenant_id=X'{A}'",
        inviteA.Id, rawA);
    await DenyWhileAsync("original required action replaced",
        $"UPDATE c05_02_auth_grants SET action_key='identity.invitation.revoke' WHERE tenant_id=X'{A}'",
        $"UPDATE c05_02_auth_grants SET action_key='identity.invitation.issue' WHERE tenant_id=X'{A}'",
        inviteA.Id, rawA);
    await DenyWhileAsync("published invite policy disabled",
        $"UPDATE c05_02_auth_issue_policy SET lifecycle='DISABLED' WHERE tenant_id=X'{A}'",
        $"UPDATE c05_02_auth_issue_policy SET lifecycle='ACTIVE' WHERE tenant_id=X'{A}'",
        inviteA.Id, rawA);
    await DenyWhileAsync("published maximum TTL shortened",
        $"UPDATE c05_02_auth_issue_policy SET max_lifetime_seconds=60 WHERE tenant_id=X'{A}'",
        $"UPDATE c05_02_auth_issue_policy SET max_lifetime_seconds=7200 WHERE tenant_id=X'{A}'",
        inviteA.Id, rawA);
    await DenyWhileAsync("current terms changed",
        $"UPDATE c05_02_tenants SET terms_version=43 WHERE tenant_id=X'{A}'",
        $"UPDATE c05_02_tenants SET terms_version=42 WHERE tenant_id=X'{A}'",
        inviteA.Id, rawA);
    await DenyWhileAsync("recipient proof revoked",
        $"UPDATE c05_02_recipients SET channel_verified=0 WHERE tenant_id=X'{A}' AND recipient_id=X'{RA}'",
        $"UPDATE c05_02_recipients SET channel_verified=1 WHERE tenant_id=X'{A}' AND recipient_id=X'{RA}'",
        inviteA.Id, rawA);
    await DenyWhileAsync("recipient proof expired",
        $"UPDATE c05_02_recipients SET verified_until_utc=DATE_SUB(UTC_TIMESTAMP(6),INTERVAL 1 SECOND) WHERE tenant_id=X'{A}' AND recipient_id=X'{RA}'",
        $"UPDATE c05_02_recipients SET verified_until_utc=DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 3 HOUR) WHERE tenant_id=X'{A}' AND recipient_id=X'{RA}'",
        inviteA.Id, rawA);
    await DenyWhileAsync("consent removed",
        $"UPDATE c05_02_consents SET accepted_at_utc=DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 1 DAY) WHERE tenant_id=X'{A}' AND recipient_id=X'{RA}'",
        $"UPDATE c05_02_consents SET accepted_at_utc=UTC_TIMESTAMP(6) WHERE tenant_id=X'{A}' AND recipient_id=X'{RA}'",
        inviteA.Id, rawA);
    await DenyWhileAsync("invitation revision tampered",
        $"UPDATE c05_02_invitations SET revision=2 WHERE tenant_id=X'{A}' AND invitation_id=X'{Convert.ToHexString(Crecisol.Infrastructure.Tenancy.MySqlRfc4122Guid.Encode(inviteA.Id.Value))}'",
        $"UPDATE c05_02_invitations SET revision=1 WHERE tenant_id=X'{A}' AND invitation_id=X'{Convert.ToHexString(Crecisol.Infrastructure.Tenancy.MySqlRfc4122Guid.Encode(inviteA.Id.Value))}'",
        inviteA.Id, rawA);

    const string ALTBRANCH = "aaaaaaaaaaaaaaaaaaaaaaaaaaaa0088";
    await SetupAsync($"INSERT INTO c05_02_auth_branches VALUES (X'{A}',X'{ALTBRANCH}','ACTIVE',15)");
    await SetupAsync($"""
      INSERT INTO c05_02_auth_grants VALUES
        (X'{A}',X'{IA}',X'{ALTBRANCH}','identity.invitation.issue',
          'OWNER_ONLY','ACTIVE',17,DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 3 HOUR))
      """);
    await DenyWhileAsync("resource transferred to another authorized branch",
        $"UPDATE c05_02_auth_resources SET branch_id=X'{ALTBRANCH}' WHERE tenant_id=X'{A}'",
        $"UPDATE c05_02_auth_resources SET branch_id=X'{BRA}' WHERE tenant_id=X'{A}'",
        inviteA.Id, rawA);

    Pass(await ClaimAsync(tenantA, inviteA.Id, recipientA, actorA, rawA),
        "Exactly original actor, channel, tenant, scope and active issuer can consume once");
    Pass(!await ClaimAsync(tenantA, inviteA.Id, recipientA, actorA, rawA),
        "Replay of successfully consumed invitation is rejected");
    Pass(await CountAsync($"""
      SELECT COUNT(*) FROM c05_02_invitations WHERE tenant_id=X'{A}'
        AND lifecycle='CLAIMED' AND revision=2
      """)==1,"Claim CAS advances exactly one row to revision two");

    var raced = await Task.WhenAll(Enumerable.Range(0,16).Select(_ =>
       ClaimAsync(tenantA, inviteA2.Id, recipientA2, actorA2, rawA2)));
    Pass(raced.Count(v=>v)==1,"16 simultaneous MySQL claim attempts have exactly one winner");
    Pass(raced.Count(v=>!v)==15,"15 simultaneous claim replays fail closed");
    Pass(await CountAsync($"""
      SELECT COUNT(*) FROM c05_02_invitations
       WHERE tenant_id=X'{A}' AND recipient_id=X'{RA2}'
         AND lifecycle='CLAIMED' AND revision=2
      """)==1,"Concurrent claim updates only one invitation row");

    Pass(await ClaimAsync(tenantB, inviteB.Id, recipientB, actorB, rawB),
        "Tenant B independently claims its own scoped invitation");
    Pass(await CountAsync($"""
      SELECT COUNT(*) FROM c05_02_invitations
       WHERE tenant_id=X'{B}' AND lifecycle='CLAIMED' AND revision=2
      """)==1,"Tenant B remains independently scoped");

    await using var worker = new MySqlConnection(workerDsn);
    await worker.OpenAsync();
    async Task MustDenySqlAsync(string query, string label)
    {
        await using var command = new MySqlCommand(query,worker);
        try { await command.ExecuteNonQueryAsync(); }
        catch (MySqlException ex) when (ex.Number is 1142 or 1143)
        {
            Pass(true,label);
            return;
        }
        throw new InvalidOperationException("Unauthorized table access was accepted.");
    }
    await MustDenySqlAsync("SELECT * FROM c05_02_invite_scope_bindings",
        "Worker cannot read confidential issuer scope binding");
    await MustDenySqlAsync("UPDATE c05_02_invite_scope_bindings SET account_revision=1",
        "Worker cannot forge issuance-time revision binding");
    await MustDenySqlAsync("UPDATE c05_02_auth_sessions SET lifecycle='ACTIVE'",
        "Worker cannot revive revoked session metadata");
    await MustDenySqlAsync("INSERT INTO c05_02_invitations(tenant_id) VALUES(X'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa')",
        "Worker cannot insert unbound invitation");
    Pass(await CountAsync("""
      SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='c05_02_disposable'
       AND table_name IN ('users','memberships','sessions','account_credentials')
      """)==0,"No account, password, membership or session implemented");
    Pass(await CountAsync("""
      SELECT COUNT(*) FROM information_schema.columns WHERE table_schema='c05_02_disposable'
        AND table_name='c05_02_invite_scope_bindings'
        AND column_name IN ('email','mailbox','raw_token','password','session_cookie')
      """)==0,"Bound scope table has no raw token, email or credential fields");

    Console.WriteLine($"PASS C05-02 bound claim .NET10 + MySQL8.4: {checks} real assertions; " +
        "16 concurrent claims one winner; no real recipient auth or tenant grant");
}
finally
{
    CryptographicOperations.ZeroMemory(rawA);
    CryptographicOperations.ZeroMemory(rawB);
    CryptographicOperations.ZeroMemory(rawA2);
}
