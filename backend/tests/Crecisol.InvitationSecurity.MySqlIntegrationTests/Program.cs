using System.Security.Cryptography;
using Crecisol.Domain.Identity;
using Crecisol.Domain.Tenancy;
using Crecisol.Infrastructure.Identity;
using MySqlConnector;

// Intentional opt-in: the old all-project .NET regression runs this without
// --integration, which prints SKIP rather than claiming an unexecuted DB test.
if (args.Length == 0)
{
    Console.WriteLine("SKIP C05-02 SQL adapter integration: --integration and local MySQL 8.4 required");
    return;
}
if (args.Length != 1 || args[0] != "--integration")
    throw new InvalidOperationException("Usage: --integration (isolated C05-02 local MySQL only)");
var testDsn = Environment.GetEnvironmentVariable("CRECISOL_C05_02_TEST_DSN");
var setupDsn = Environment.GetEnvironmentVariable("CRECISOL_C05_02_TEST_ADMIN_DSN");
if (string.IsNullOrWhiteSpace(testDsn) || string.IsNullOrWhiteSpace(setupDsn))
    throw new InvalidOperationException("C05-02 local isolated MySQL test DSNs missing.");
var setupSettings = new MySqlConnectionStringBuilder(setupDsn);
if (setupSettings.Database != "c05_02_disposable" ||
    setupSettings.Server is not ("127.0.0.1" or "localhost" or "::1"))
    throw new InvalidOperationException("C05-02 admin setup is forbidden outside isolated local fixture.");

var checks = 0;
void Pass(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException("FAIL C05-02 MySQL: " + message);
    checks++;
    Console.WriteLine("PASS C05-02 MySQL adapter: " + message);
}
void Deny<T>(Action action, string message) where T : Exception
{
    try { action(); }
    catch (T) { checks++; return; }
    throw new InvalidOperationException("FAIL C05-02 MySQL expected " + typeof(T).Name + ": " + message);
}

await using var setup = new MySqlConnection(setupSettings.ConnectionString);
await setup.OpenAsync();
async Task<int> ExecuteSetup(string sql)
{
    await using var command = new MySqlCommand(sql, setup);
    return await command.ExecuteNonQueryAsync();
}

const string A = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
const string B = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
const string IA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaa0001";
const string IB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbb0001";
const string AA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaa0002";
const string AB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbb0002";
const string RA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaa0003";
const string RB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbb0003";

await ExecuteSetup($"""
    INSERT INTO c05_02_tenants VALUES (X'{A}','ACTIVE',42),(X'{B}','ACTIVE',42);
    INSERT INTO c05_02_issuers VALUES
      (X'{A}',X'{IA}','INVITE_EMPLOYEE',1),(X'{B}',X'{IB}','INVITE_CONSUMER',1);
    INSERT INTO c05_02_recipients VALUES
      (X'{A}',X'{RA}',X'{AA}',1,DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 1 DAY)),
      (X'{B}',X'{RB}',X'{AB}',1,DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 1 DAY));
    INSERT INTO c05_02_consents VALUES
      (X'{A}',X'{RA}',42,UTC_TIMESTAMP(6)),(X'{B}',X'{RB}',42,UTC_TIMESTAMP(6));
    """);

Deny<InvalidOperationException>(() => new MySqlDisposableInvitationLedger(null),
    "No server-held DSN fails closed");
Deny<InvalidOperationException>(() => new MySqlDisposableInvitationLedger(
    "Server=another.example;Database=c05_02_disposable;User ID=fake;Password=fake"),
    "Untrusted off-host persistence prohibited");
Deny<InvalidOperationException>(() => new MySqlDisposableInvitationLedger(
    "Server=127.0.0.1;Database=crecisol_production;User ID=fake;Password=fake"),
    "Non-fixture database prohibited");
Deny<InvalidOperationException>(() =>
    InvitationTokenDigest.RestoreFromProtectedPersistence(new byte[31]),
    "Malformed 31-byte protected digest rejected");
Deny<InvalidOperationException>(() =>
    InvitationTokenDigest.RestoreFromProtectedPersistence(new byte[32]),
    "Missing all-zero protected digest rejected");
var ledger = new MySqlDisposableInvitationLedger(testDsn);
var tenantA = TenantId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
var tenantB = TenantId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
var issuerA = TenantActorId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0001"));
var issuerB = TenantActorId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbb0001"));
var actorA = TenantActorId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0002"));
var actorB = TenantActorId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbb0002"));
var recA = RecipientChannelBindingId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0003"));
var recB = RecipientChannelBindingId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbb0003"));
var idMain = InvitationId.From(Guid.Parse("11111111-1111-1111-1111-111111111111"));
var idSecond = InvitationId.From(Guid.Parse("22222222-2222-2222-2222-222222222222"));
var idThird = InvitationId.From(Guid.Parse("33333333-3333-3333-3333-333333333333"));
var idWrong = InvitationId.From(Guid.Parse("44444444-4444-4444-4444-444444444444"));
var now = DateTimeOffset.UtcNow;
var issuedAt = now.AddMinutes(-1);
var expiresAt = now.AddHours(2);

InvitationSnapshot NewInvite(InvitationId id, TenantId tenant,
    TenantActorId issuer, RecipientChannelBindingId recipient,
    AccountTenantProfileKind profile, InvitationTokenDigest digest,
    DateTimeOffset? issued = null, DateTimeOffset? expires = null)
{
    var i = issued ?? issuedAt;
    return InvitationSnapshot.RestoreFromTrustedInvitationStore(
        id, tenant, issuer, recipient, profile, InvitationLifecycleState.Pending,
        digest, 1, 42, i, i, expires ?? expiresAt);
}

using var capA = EphemeralInvitationCode.Generate();
var digestA = capA.CreateDigest();
var primary = NewInvite(idMain, tenantA, issuerA, recA,
    AccountTenantProfileKind.Employee, digestA);
Pass(await ledger.TryInsertUniqueAsync(primary, CancellationToken.None),
    "Tenant A new hashed invitation inserted using verified issuer+recipient+terms");
Pass((await ledger.ReadExactAsync(tenantA, idMain, CancellationToken.None)) is { Count: 1 } readA &&
    readA[0].TenantId == tenantA && readA[0].Profile == AccountTenantProfileKind.Employee &&
    readA[0].State == InvitationLifecycleState.Pending &&
    readA[0].Digest.SameDigestAs(digestA),
    "Protected 32-byte digest restored without rehash and profile/tenant exact");
Pass((await ledger.ReadExactAsync(tenantB, idMain, CancellationToken.None)) is { Count: 0 },
    "Cross-tenant B cannot read tenant A invitation");

using var capDuplicate = EphemeralInvitationCode.Generate();
var duplicate = NewInvite(idSecond, tenantA, issuerA, recA,
    AccountTenantProfileKind.Employee, capDuplicate.CreateDigest());
Pass(!await ledger.TryInsertUniqueAsync(duplicate, CancellationToken.None),
    "Active invitation for same tenant+recipient denied even with fresh ID and digest");

// Multiple DB client connections race against the actual procedure.
var winners = await Task.WhenAll(Enumerable.Range(0, 16)
    .Select(_ => Task.Run(async () =>
        await ledger.TryClaimOnceAtomicallyAsync(
            primary, actorA, DateTimeOffset.UtcNow, CancellationToken.None))));
Pass(winners.Count(x => x) == 1, "Sixteen concurrent real .NET MySQL clients exactly ONE winner");
Pass(!await ledger.TryClaimOnceAtomicallyAsync(
    primary, actorA, DateTimeOffset.UtcNow, CancellationToken.None),
    "Replay of consumed token denied with same revision");
Pass((await ledger.ReadExactAsync(tenantA, idMain, CancellationToken.None)) is { Count: 1 } claimed &&
    claimed[0].State == InvitationLifecycleState.Claimed && claimed[0].Revision == 2,
    "Claimed row revision transitions once and can be read back accurately");

using var capB = EphemeralInvitationCode.Generate();
var inviteB = NewInvite(idMain, tenantB, issuerB, recB,
    AccountTenantProfileKind.Consumer, capB.CreateDigest());
Pass(await ledger.TryInsertUniqueAsync(inviteB, CancellationToken.None),
    "Tenant B can reuse same opaque invitation ID in isolated composite primary key");
Pass((await ledger.ReadExactAsync(tenantB, idMain, CancellationToken.None)) is { Count: 1 } readB &&
    readB[0].TenantId == tenantB && readB[0].Profile == AccountTenantProfileKind.Consumer,
    "B only sees its own invitation profile and digest");
Pass(!await ledger.TryClaimOnceAtomicallyAsync(
    inviteB, actorA, DateTimeOffset.UtcNow, CancellationToken.None),
    "Tenant A actor cannot consume B invitation");
Pass(await ledger.TryClaimOnceAtomicallyAsync(
    inviteB, actorB, DateTimeOffset.UtcNow, CancellationToken.None),
    "Tenant B recipient consumes B invitation without sharing A grant");

using var capSecond = EphemeralInvitationCode.Generate();
var second = NewInvite(idSecond, tenantA, issuerA, recA,
    AccountTenantProfileKind.Employee, capSecond.CreateDigest());
Pass(await ledger.TryInsertUniqueAsync(second, CancellationToken.None),
    "Consumed A invitation permits new A issuance via separate ID");
var wrongDigest = InvitationTokenDigest.FromToken(new byte[32]);
var forged = NewInvite(idSecond, tenantA, issuerA, recA,
    AccountTenantProfileKind.Employee, wrongDigest);
Pass(!await ledger.TryClaimOnceAtomicallyAsync(
    forged, actorA, DateTimeOffset.UtcNow, CancellationToken.None),
    "Forged 32-byte token digest cannot consume actual invitation");
Pass(!await ledger.TryClaimOnceAtomicallyAsync(
    second, actorB, DateTimeOffset.UtcNow, CancellationToken.None),
    "Wrong verified recipient actor fails closed");

await ExecuteSetup($"UPDATE c05_02_tenants SET lifecycle='SUSPENDED' WHERE tenant_id=X'{A}'");
Pass(!await ledger.TryClaimOnceAtomicallyAsync(
    second, actorA, DateTimeOffset.UtcNow, CancellationToken.None),
    "Suspended tenant cannot consume");
await ExecuteSetup($"UPDATE c05_02_tenants SET lifecycle='ACTIVE',terms_version=43 WHERE tenant_id=X'{A}'");
Pass(!await ledger.TryClaimOnceAtomicallyAsync(
    second, actorA, DateTimeOffset.UtcNow, CancellationToken.None),
    "New published terms version invalidates older invitation");
await ExecuteSetup($"UPDATE c05_02_tenants SET terms_version=42 WHERE tenant_id=X'{A}'");
await ExecuteSetup($"UPDATE c05_02_issuers SET enabled=0 WHERE tenant_id=X'{A}'");
Pass(!await ledger.TryClaimOnceAtomicallyAsync(
    second, actorA, DateTimeOffset.UtcNow, CancellationToken.None),
    "Issuer loses entitlement at atomic backend boundary");
await ExecuteSetup($"UPDATE c05_02_issuers SET enabled=1 WHERE tenant_id=X'{A}'");
await ExecuteSetup($"UPDATE c05_02_recipients SET channel_verified=0 WHERE tenant_id=X'{A}'");
Pass(!await ledger.TryClaimOnceAtomicallyAsync(
    second, actorA, DateTimeOffset.UtcNow, CancellationToken.None),
    "Recipient channel revoked at DB atomic boundary");
await ExecuteSetup($"UPDATE c05_02_recipients SET channel_verified=1 WHERE tenant_id=X'{A}'");
Pass(await ledger.TryClaimOnceAtomicallyAsync(
    second, actorA, DateTimeOffset.UtcNow, CancellationToken.None),
    "Exactly authorized actor and current tenant/channel/consent can claim");

// Independently deny issuance across policy changes and untrusted profiles.
using var capThird = EphemeralInvitationCode.Generate();
var third = NewInvite(idThird, tenantA, issuerA, recA,
    AccountTenantProfileKind.Employee, capThird.CreateDigest());
await ExecuteSetup($"UPDATE c05_02_issuers SET enabled=0 WHERE tenant_id=X'{A}'");
Pass(!await ledger.TryInsertUniqueAsync(third, CancellationToken.None),
    "Issuer revocation prevents NEW insertion and no token is delivered");
await ExecuteSetup($"UPDATE c05_02_issuers SET enabled=1 WHERE tenant_id=X'{A}'");
await ExecuteSetup($"UPDATE c05_02_recipients SET channel_verified=0 WHERE tenant_id=X'{A}'");
Pass(!await ledger.TryInsertUniqueAsync(third, CancellationToken.None),
    "Unverified recipient cannot be inserted");
await ExecuteSetup($"UPDATE c05_02_recipients SET channel_verified=1 WHERE tenant_id=X'{A}'");
await ExecuteSetup($"UPDATE c05_02_tenants SET lifecycle='SUSPENDED' WHERE tenant_id=X'{A}'");
Pass(!await ledger.TryInsertUniqueAsync(third, CancellationToken.None),
    "Suspended tenant cannot be invited");
await ExecuteSetup($"UPDATE c05_02_tenants SET lifecycle='ACTIVE',terms_version=43 WHERE tenant_id=X'{A}'");
Pass(!await ledger.TryInsertUniqueAsync(third, CancellationToken.None),
    "Outdated version of tenant terms cannot be issued");
await ExecuteSetup($"UPDATE c05_02_tenants SET terms_version=42 WHERE tenant_id=X'{A}'");
Pass(await ledger.TryInsertUniqueAsync(third, CancellationToken.None),
    "Reactivated issuer and current terms allow one pending invitation");
Pass(!await ledger.TryInsertUniqueAsync(third, CancellationToken.None),
    "Duplicate insertion with exact ID fails safely");

using var capWrong = EphemeralInvitationCode.Generate();
var wrongProfile = NewInvite(idWrong, tenantA, issuerA, recA,
    AccountTenantProfileKind.Consumer, capWrong.CreateDigest());
Pass(!await ledger.TryInsertUniqueAsync(wrongProfile, CancellationToken.None),
    "Employee-only issuer cannot issue consumer or owner credentials");
Pass(!await ledger.TryInsertUniqueAsync(
    NewInvite(idWrong, tenantB, issuerA, recB,
        AccountTenantProfileKind.Consumer, capWrong.CreateDigest()), CancellationToken.None),
    "Foreign-tenant issuer not permitted even if recipient exists");

var stale = NewInvite(idWrong, tenantA, issuerA, recA,
    AccountTenantProfileKind.Employee, capWrong.CreateDigest(),
    issuedAt.AddHours(-3), issuedAt.AddMinutes(-2));
Pass(!await ledger.TryInsertUniqueAsync(stale, CancellationToken.None),
    "Expired invitation creation fails closed in SQL adapter");
Pass((await ledger.ReadExactAsync(tenantA, idWrong, CancellationToken.None)) is { Count: 0 },
    "Forbidden invitations are not written to the database");

// Hostile privileges: the app role must NOT write tenant/recipient/issuer
// state nor bypass SQL SECURITY DEFINER for invitation metadata.
async Task RequirePermissionDenied(string sql, string description)
{
    await using var restricted = new MySqlConnection(testDsn);
    await restricted.OpenAsync();
    await using var cmd = new MySqlCommand(sql, restricted);
    try
    {
        await cmd.ExecuteNonQueryAsync();
    }
    catch (MySqlException error) when (error.Number is 1142 or 1143)
    {
        Pass(true, description);
        return;
    }
    throw new InvalidOperationException("FAIL C05-02 MySQL application role had direct privilege: " + description);
}
await RequirePermissionDenied(
    "UPDATE c05_02_tenants SET lifecycle='SUSPENDED' WHERE 1=0",
    "Adapter cannot UPDATE tenant lifecycle directly");
await RequirePermissionDenied(
    "UPDATE c05_02_recipients SET channel_verified=0 WHERE 1=0",
    "Adapter cannot UPDATE recipient verification directly");
await RequirePermissionDenied(
    "UPDATE c05_02_invitations SET lifecycle='CLAIMED' WHERE 1=0",
    "Adapter cannot UPDATE invitation lifecycle directly");
await RequirePermissionDenied(
    "INSERT INTO c05_02_invitations (tenant_id) VALUES (X'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa')",
    "Adapter cannot INSERT invitation metadata outside definer procedure");
await RequirePermissionDenied(
    "DELETE FROM c05_02_invitations WHERE 1=0",
    "Adapter cannot DELETE invitation rows");

// Simulate 16 genuinely concurrent distinct invitation ISSUANCES for one
// recipient, not just 16 token claims. The SQL recipient lock must allow
// exactly ONE live pending invitation even if all GUIDs and digests differ.
Pass(await ledger.TryClaimOnceAtomicallyAsync(
    third, actorA, DateTimeOffset.UtcNow, CancellationToken.None),
    "Close previous A pending invitation before concurrent new issuance");
var capabilities = Enumerable.Range(0, 16)
    .Select(_ => EphemeralInvitationCode.Generate()).ToArray();
try
{
    var contenders = capabilities.Select(capability => NewInvite(
        InvitationId.From(Guid.NewGuid()), tenantA, issuerA, recA,
        AccountTenantProfileKind.Employee, capability.CreateDigest())).ToArray();
    var issuedResults = await Task.WhenAll(contenders.Select(row =>
        Task.Run(() => ledger.TryInsertUniqueAsync(row, CancellationToken.None))));
    Pass(issuedResults.Count(x => x) == 1,
        "Sixteen concurrent real SQL ISSUANCES exactly ONE pending invitation");
    await using var query = new MySqlCommand($"""
        SELECT COUNT(*) FROM c05_02_invitations
         WHERE tenant_id=X'{A}' AND recipient_id=X'{RA}'
           AND lifecycle='PENDING' AND expires_at_utc>UTC_TIMESTAMP(6)
        """, setup);
    var count = Convert.ToInt32(await query.ExecuteScalarAsync());
    Pass(count == 1, "SQL has exactly one active pending A recipient after issuance race");
}
finally
{
    foreach (var capability in capabilities)
        capability.Dispose();
}

// The canonical public API has no registration route, no session and no grant.
// This integration test proves only the trusted local storage port, not signup.
Console.WriteLine($"PASS C05-02 actual .NET10 + MySQL8.4 adapter: {checks} verified checks");
Console.WriteLine("LIMIT: isolated local fixture only; HTTP/email/credential verification, tenant grant and production DSN NOT IMPLEMENTED");
