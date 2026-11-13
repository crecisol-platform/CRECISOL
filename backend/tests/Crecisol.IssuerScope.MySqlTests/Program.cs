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
var adminDsn = Environment.GetEnvironmentVariable("CRECISOL_C05_02_ISSUER_ADMIN_DSN")
    ?? throw new InvalidOperationException("Local setup DSN required.");
var workerDsn = Environment.GetEnvironmentVariable("CRECISOL_C05_02_ISSUER_WORKER_DSN")
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
async Task DenyAsync(Func<Task> operation, string description)
{
    try { await operation(); }
    catch (UnauthorizedAccessException)
    {
        Pass(true, description);
        return;
    }
    throw new InvalidOperationException("FAIL expected authorization denial: " + description);
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

var ledger = new MySqlDisposableScopedInvitationLedger(workerDsn);
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

async Task<bool> IssueAsync(
    TenantId tenant, TenantActorId actor, RecipientChannelBindingId recipient,
    AccountTenantProfileKind profile,
    (AccountTenantBindingEvidence Binding, TenantBranchResourceScopeEvidence Scope) evidence,
    TimeSpan? lifetime = null)
{
    using var code = EphemeralInvitationCode.Generate();
    var now = DateTimeOffset.UtcNow;
    var snapshot = InvitationSnapshot.RestoreFromTrustedInvitationStore(
        InvitationId.From(Guid.NewGuid()),tenant,actor,recipient,profile,
        InvitationLifecycleState.Pending,code.CreateDigest(),1,42,
        now,now,now.Add(lifetime ?? TimeSpan.FromMinutes(20)));
    return await ledger.TryIssueAuthorizedOnceAsync(snapshot,evidence.Binding,
        evidence.Scope,CancellationToken.None);
}

async Task ResetPendingAsync()
{
    await SetupAsync($"""
       UPDATE c05_02_invitations SET lifecycle='REVOKED',revision=revision+1
         WHERE tenant_id=X'{A}' AND lifecycle='PENDING';
       """);
}
try
{
    _ = new MySqlDisposableScopedInvitationLedger(
        "Server=remote.example;Database=c05_02_disposable;User ID=invalid");
    throw new InvalidOperationException("Remote DSN accepted");
}
catch (InvalidOperationException e) when (e.Message.StartsWith(
    "Only disposable loopback",StringComparison.Ordinal))
{
    Pass(true,"Remote MySQL rejected before connecting");
}

Pass(await IssueAsync(tenantA,issuerA,recipientA,AccountTenantProfileKind.Employee,a),
    "Exact authenticated issuer metadata and OWNER_ONLY grant issue one pending invitation");
Pass(await CountAsync($"""
    SELECT COUNT(*) FROM c05_02_invitations
      WHERE tenant_id=X'{A}' AND issuer_id=X'{IA}' AND lifecycle='PENDING'
    """)==1, "Only one tenant-scoped digest is stored");
Pass(!await IssueAsync(tenantA,issuerA,recipientA,AccountTenantProfileKind.Employee,a),
    "Duplicate active recipient invitation denied atomically");
await ResetPendingAsync();

await DenyAsync(async () => { _ = await IssueAsync(
    tenantB,issuerA,recipientB,AccountTenantProfileKind.Employee,a); },
    "Tenant A trusted evidence cannot issue to B");
await DenyAsync(async () => { _ = await IssueAsync(
    tenantA,issuerA,recipientA,AccountTenantProfileKind.Employee,b); },
    "Foreign resource and actor scope cannot be passed as A");
Pass(!await IssueAsync(tenantA,issuerA,recipientA,AccountTenantProfileKind.Consumer,a),
    "An Employee issuer cannot forge Consumer/Owner profile privileges");
Pass(!await IssueAsync(tenantA,issuerA,recipientA,AccountTenantProfileKind.Employee,
    a,TimeSpan.FromHours(4)), "Published 2-hour server policy rejects longer lifetime");
await DenyAsync(async () => { _ = await IssueAsync(
    tenantA,issuerA,recipientA,AccountTenantProfileKind.Employee,
    Evidence(tenantA,issuerA,linkA,branchA,resourceA,issuerA,
        action:"identity.invitation.revoke")); },
    "An unauthorized alternate action is never acceptable");

var stale = new[]
{
    (Name:"account", Guard:Evidence(tenantA,issuerA,linkA,branchA,resourceA,issuerA,
        accountRevision:8)),
    (Name:"session", Guard:Evidence(tenantA,issuerA,linkA,branchA,resourceA,issuerA,
        sessionRevision:21)),
    (Name:"membership", Guard:Evidence(tenantA,issuerA,linkA,branchA,resourceA,issuerA,
        membershipRevision:22)),
    (Name:"host", Guard:Evidence(tenantA,issuerA,linkA,branchA,resourceA,issuerA,
        hostRevision:23)),
    (Name:"catalog", Guard:Evidence(tenantA,issuerA,linkA,branchA,resourceA,issuerA,
        catalogRevision:24)),
    (Name:"branch", Guard:Evidence(tenantA,issuerA,linkA,branchA,resourceA,issuerA,
        branchRevision:25)),
    (Name:"resource", Guard:Evidence(tenantA,issuerA,linkA,branchA,resourceA,issuerA,
        resourceRevision:26)),
    (Name:"grant", Guard:Evidence(tenantA,issuerA,linkA,branchA,resourceA,issuerA,
        grantRevision:27))
};
foreach (var item in stale)
    Pass(!await IssueAsync(tenantA,issuerA,recipientA,AccountTenantProfileKind.Employee,
        item.Guard), "Stale "+item.Name+" revision denied by SQL, not by cached C04 proof");
Pass(await IssueAsync(tenantA,issuerA,recipientA,AccountTenantProfileKind.Employee,a),
    "All exact revisions restore authorized atomic issuance");
await ResetPendingAsync();

async Task RevocationCase(string label, string mutation, string restore)
{
    await SetupAsync(mutation);
    Pass(!await IssueAsync(tenantA,issuerA,recipientA,AccountTenantProfileKind.Employee,a),
        "Denies "+label+" at SQL issue transaction");
    await SetupAsync(restore);
    Pass(await IssueAsync(tenantA,issuerA,recipientA,AccountTenantProfileKind.Employee,a),
        "Restored exact policy permits "+label+" case, proving denial was specific");
    await ResetPendingAsync();
}
await RevocationCase("tenant suspension",
    $"UPDATE c05_02_tenants SET lifecycle='SUSPENDED' WHERE tenant_id=X'{A}'",
    $"UPDATE c05_02_tenants SET lifecycle='ACTIVE' WHERE tenant_id=X'{A}'");
await RevocationCase("issuer disabled",
    $"UPDATE c05_02_issuers SET enabled=0 WHERE tenant_id=X'{A}' AND actor_id=X'{IA}'",
    $"UPDATE c05_02_issuers SET enabled=1 WHERE tenant_id=X'{A}' AND actor_id=X'{IA}'");
await RevocationCase("account suspension",
    $"UPDATE c05_02_auth_accounts SET lifecycle='SUSPENDED' WHERE tenant_id=X'{A}'",
    $"UPDATE c05_02_auth_accounts SET lifecycle='ACTIVE' WHERE tenant_id=X'{A}'");
await RevocationCase("session revocation",
    $"UPDATE c05_02_auth_sessions SET lifecycle='REVOKED' WHERE tenant_id=X'{A}'",
    $"UPDATE c05_02_auth_sessions SET lifecycle='ACTIVE' WHERE tenant_id=X'{A}'");
await RevocationCase("session expiry",
    $"UPDATE c05_02_auth_sessions SET expires_utc=DATE_SUB(UTC_TIMESTAMP(6),INTERVAL 1 SECOND) WHERE tenant_id=X'{A}'",
    $"UPDATE c05_02_auth_sessions SET expires_utc=DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 3 HOUR) WHERE tenant_id=X'{A}'");
await RevocationCase("membership link revocation",
    $"UPDATE c05_02_auth_links SET lifecycle='REVOKED' WHERE tenant_id=X'{A}'",
    $"UPDATE c05_02_auth_links SET lifecycle='ACTIVE' WHERE tenant_id=X'{A}'");
await RevocationCase("membership expiry",
    $"UPDATE c05_02_auth_links SET expires_utc=DATE_SUB(UTC_TIMESTAMP(6),INTERVAL 1 SECOND) WHERE tenant_id=X'{A}'",
    $"UPDATE c05_02_auth_links SET expires_utc=DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 3 HOUR) WHERE tenant_id=X'{A}'");
await RevocationCase("host verification revocation",
    $"UPDATE c05_02_auth_hosts SET lifecycle='REVOKED' WHERE tenant_id=X'{A}'",
    $"UPDATE c05_02_auth_hosts SET lifecycle='VERIFIED' WHERE tenant_id=X'{A}'");
await RevocationCase("branch suspension",
    $"UPDATE c05_02_auth_branches SET lifecycle='SUSPENDED' WHERE tenant_id=X'{A}'",
    $"UPDATE c05_02_auth_branches SET lifecycle='ACTIVE' WHERE tenant_id=X'{A}'");
await RevocationCase("resource deletion",
    $"UPDATE c05_02_auth_resources SET lifecycle='DELETED' WHERE tenant_id=X'{A}'",
    $"UPDATE c05_02_auth_resources SET lifecycle='ACTIVE' WHERE tenant_id=X'{A}'");
await RevocationCase("resource owner change",
    $"UPDATE c05_02_auth_resources SET owner_actor_id=X'{OA}' WHERE tenant_id=X'{A}'",
    $"UPDATE c05_02_auth_resources SET owner_actor_id=X'{IA}' WHERE tenant_id=X'{A}'");
await RevocationCase("grant revoked",
    $"UPDATE c05_02_auth_grants SET lifecycle='REVOKED' WHERE tenant_id=X'{A}'",
    $"UPDATE c05_02_auth_grants SET lifecycle='ACTIVE' WHERE tenant_id=X'{A}'");
await RevocationCase("grant expired",
    $"UPDATE c05_02_auth_grants SET expires_utc=DATE_SUB(UTC_TIMESTAMP(6),INTERVAL 1 SECOND) WHERE tenant_id=X'{A}'",
    $"UPDATE c05_02_auth_grants SET expires_utc=DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 3 HOUR) WHERE tenant_id=X'{A}'");
await RevocationCase("invitation policy disabled",
    $"UPDATE c05_02_auth_issue_policy SET lifecycle='DISABLED' WHERE tenant_id=X'{A}'",
    $"UPDATE c05_02_auth_issue_policy SET lifecycle='ACTIVE' WHERE tenant_id=X'{A}'");
await RevocationCase("terms reissued",
    $"UPDATE c05_02_tenants SET terms_version=43 WHERE tenant_id=X'{A}'",
    $"UPDATE c05_02_tenants SET terms_version=42 WHERE tenant_id=X'{A}'");
await RevocationCase("recipient channel revoked",
    $"UPDATE c05_02_recipients SET channel_verified=0 WHERE tenant_id=X'{A}'",
    $"UPDATE c05_02_recipients SET channel_verified=1 WHERE tenant_id=X'{A}'");

await SetupAsync($"""
    UPDATE c05_02_auth_grants SET scope='BRANCH_WIDE',revision=18 WHERE tenant_id=X'{A}';
    UPDATE c05_02_auth_resources SET owner_actor_id=X'{OA}' WHERE tenant_id=X'{A}';
    """);
var branchWide = Evidence(tenantA,issuerA,linkA,branchA,resourceA,outsiderA,
    BranchGrantScope.BranchWide,grantRevision:18);
Pass(await IssueAsync(tenantA,issuerA,recipientA,
    AccountTenantProfileKind.Employee,branchWide),
    "Explicit BRANCH_WIDE grant permits invitation for resource with different owner");
await ResetPendingAsync();
await SetupAsync($"""
    UPDATE c05_02_auth_grants SET scope='OWNER_ONLY',revision=17 WHERE tenant_id=X'{A}';
    UPDATE c05_02_auth_resources SET owner_actor_id=X'{IA}' WHERE tenant_id=X'{A}';
    """);

Pass(await IssueAsync(tenantB,issuerB,recipientB,
    AccountTenantProfileKind.Consumer,b),
    "Tenant B independently issues Consumer invitation with valid scope");
Pass(await CountAsync($"""
    SELECT COUNT(*) FROM c05_02_invitations WHERE tenant_id=X'{B}' AND lifecycle='PENDING'
    """)==1,"Tenant B invitation exists only under tenant B");

var simultaneous = await Task.WhenAll(Enumerable.Range(0,16).Select(_ =>
    IssueAsync(tenantA,issuerA,recipientA2,
        AccountTenantProfileKind.Employee,a)));
Pass(simultaneous.Count(v=>v)==1,
    "16 simultaneous authorized issuers create exactly one pending recipient invitation");
Pass(await CountAsync($"""
    SELECT COUNT(*) FROM c05_02_invitations
      WHERE tenant_id=X'{A}' AND recipient_id=X'{RA2}'
        AND lifecycle='PENDING' AND revision=1
    """)==1, "Single pending invitation row and revision after 16 SQL races");

await using var worker = new MySqlConnection(workerDsn);
await worker.OpenAsync();
async Task PermissionDeniedAsync(string sql, string message)
{
    await using var command = new MySqlCommand(sql,worker);
    try { await command.ExecuteNonQueryAsync(); }
    catch (MySqlException e) when (e.Number is 1142 or 1143)
    {
        Pass(true,message);
        return;
    }
    throw new InvalidOperationException("Restricted worker gained direct table permission.");
}
await PermissionDeniedAsync("SELECT * FROM c05_02_auth_sessions",
    "Restricted worker cannot inspect session metadata");
await PermissionDeniedAsync("SELECT * FROM c05_02_auth_links",
    "Restricted worker cannot inspect tenant link metadata");
await PermissionDeniedAsync("UPDATE c05_02_auth_grants SET lifecycle='ACTIVE'",
    "Restricted worker cannot fabricate branch permissions");
await PermissionDeniedAsync("INSERT INTO c05_02_invitations(tenant_id) VALUES (X'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa')",
    "Restricted worker cannot insert invitation outside definer procedure");
Pass(await CountAsync("""
    SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='c05_02_disposable'
      AND table_name IN ('memberships','sessions','users','account_credentials')
    """)==0, "No real identities, passwords, membership grants or login sessions created");
Console.WriteLine($"PASS C05-02 authenticated-scope fixture: {checks} real .NET10/MySQL8.4 assertions; " +
    "NO signed session, public route, real role grant or functional C05-02 certification");
