using System.Data;
using System.Security.Cryptography;
using Crecisol.Domain.Authorization;
using Crecisol.Domain.Tenancy;
using Crecisol.Infrastructure.Authorization;
using MySqlConnector;

// Synthetic disposable schema only. No actual bearer, customer data or MFA.
// This tests the real internal writer, not the still-unimplemented proof loader.
var cycleMode=Environment.GetEnvironmentVariable("C06_DELEGATION_CYCLE_WRITER_TESTS")=="1";
var dsn=Environment.GetEnvironmentVariable("C06_DELEGATION_TEST_DSN") ??
    throw new InvalidOperationException("Disposable test DSN required.");
var config=new MySqlConnectionStringBuilder(dsn);
if(config.Server!="127.0.0.1" || config.Database!="crecisol_c05" ||
   config.UserID!="c06_delegation_writer_test" || config.Port==3306)
    throw new InvalidOperationException("Only isolated random-port delegation fixture is allowed.");
await using var connection=new MySqlConnection(config.ConnectionString);
await connection.OpenAsync();
await using var tx=await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted);
await using var clock=new MySqlCommand("SELECT UTC_TIMESTAMP(6)",connection,tx);
var now=new DateTimeOffset(DateTime.SpecifyKind((DateTime)(await clock.ExecuteScalarAsync())!,DateTimeKind.Utc));
Guid Id(string hex)=>new(Convert.FromHexString(string.Concat(Enumerable.Repeat(hex,16))),bigEndian:true);
var tenant=TenantId.From(Id("11"));var otherTenant=TenantId.From(Id("22"));
var actor=TenantActorId.From(Id("31"));var subject=TenantActorId.From(Id("32"));
var scope=new C06DelegationScope(tenant,TenantBranchId.From(Id("71")),TenantResourceId.From(Id("73")),
    TenantRequiredAction.FromServerPolicy("identity.private.read"));
var member=Id("41");var target=Id("42");var role=Id("61");var factor=Guid.NewGuid();var session=Guid.NewGuid();
var identity=C06OperationalMembershipContext.VerifyLockedServerEvidence(new C06LockedIdentityEvidence
{
    VerifiedHostTenant=tenant,SessionTenant=tenant,MembershipTenant=tenant,SessionActor=actor,AccountActor=actor,
    MembershipActor=actor,FactorActor=actor,SessionMembershipId=member,MembershipId=member,
    SessionFactorId=factor,FactorId=factor,SessionRevision=1,AccountRevisionInSession=1,CurrentAccountRevision=1,
    MembershipRevisionInSession=1,CurrentMembershipRevision=1,FactorRevision=1,VerifiedHostRevision=1,TenantCatalogRevision=1,
    VerifiedHostAddressMode=TenantAddressMode.Subdomain,TenantCatalogAddressMode=TenantAddressMode.Subdomain,
    SessionNotRevoked=true,AccountActive=true,AccountMailboxVerified=true,MembershipActive=true,FactorActive=true,
    SessionIssuedAtUtc=now.AddMinutes(-4),SessionLastSeenAtUtc=now.AddMinutes(-1),
    SessionExpiresAtUtc=now.AddMinutes(30),MfaVerifiedAtUtc=now.AddMinutes(-4),
    MembershipGrantedAtUtc=now.AddMinutes(-5),MembershipExpiresAtUtc=now.AddHours(20),ServerNowUtc=now,ServerIdleMinutes=30
});
var intent=new C06DelegationIntent(C06DelegationCommand.Assign,scope,subject,target,role,1,1,1,0,now.AddMinutes(20));
C06DelegationStepUp Proof(C06DelegationIntent i)=>new(i,tenant,actor,member,1,1,1,now);
C06LockedDelegationEvidence Evidence(C06DelegationIntent i)=>new()
{
    Identity=identity,Intent=i,VerifiedResourceScope=scope,BranchActive=true,ResourceActive=true,
    CommercialAccessVerified=true,ActorRoleCode="OWNER",SubjectTenant=tenant,SubjectActor=subject,
    SubjectMembershipId=target,SubjectMembershipRevision=1,SubjectMembershipActive=true,
    SubjectMembershipUntilUtc=now.AddHours(20),VerifiedRoleId=role,RoleTenant=tenant,RoleRevision=1,
    TargetRoleCode="tenant-staff",RoleActive=true,Ceiling=new(scope,actor,member,1,true,true,now.AddMinutes(-5),now.AddHours(1),null),
    ActorEffectiveRead=new C06CaseAbacEvidence
    {
        Identity=identity,ResourceTenant=tenant,ResourceBranch=scope.Branch,ResourceId=scope.Resource,ResourceOwner=subject,
        SelectorBranch=scope.Branch,SelectorResource=scope.Resource,BranchActive=true,ResourceActive=true,RoleCode="OWNER",
        RequiredAction=scope.Action,GrantTenant=tenant,GrantActor=actor,GrantBranch=scope.Branch,GrantResource=null,
        GrantPermissionCode="RESOURCE_READ",GrantScope=2,GrantRevision=1,GrantIssuedAtUtc=now.AddMinutes(-5),
        GrantExpiresAtUtc=now.AddHours(1),GrantRevokedAtUtc=null,Assignment=null,CheckedAtUtc=now
    },StepUp=Proof(i),Existing=null,NowUtc=now
};
var e=Evidence(intent);var writer=new MySqlC06DelegationLedger(connection,tx);
var key=RandomNumberGenerator.GetBytes(32);
const string reason="Synthetic delegated read purpose";
int checks=0;
void Check(bool ok,string label)
{
    if(!ok)throw new InvalidOperationException("C06-11 WRITER FAIL "+label);
    checks++;Console.WriteLine("PASS C06-11 WRITER "+label);
}
async Task<long> Count(string table)
{
    if(table is not ("c06_delegation_grant" or "c06_delegation_revocation") &&
       !(cycleMode && table=="c06_delegation_cycle_head"))throw new InvalidOperationException();
    await using var cmd=new MySqlCommand("SELECT COUNT(*) FROM "+table,connection,tx);
    return Convert.ToInt64(await cmd.ExecuteScalarAsync());
}
Task<bool> Append(C06LockedDelegationEvidence x, string why=reason)=>writer.AppendAsync(x,session,key,why,CancellationToken.None);
Task<Guid?> AppendCycle(C06LockedDelegationEvidence x,C06DelegationCycleHead? head,string why=reason)=>
    writer.AppendCycleAsync(x,head,session,key,why,CancellationToken.None);
try
{
    if(cycleMode)
    {
        Check(await Count("c06_delegation_grant")==0 && await Count("c06_delegation_cycle_head")==0,
            "cycle fixture empty");
        var absence=await writer.ReadCycleEvidenceAsync(intent,CancellationToken.None);
        Check(absence.Grant is null && absence.Head is null,"independent absence of head and grant");
        Check(await AppendCycle(e with { CommercialAccessVerified=false },null) is null,"cycle cannot bypass commercial authority");
        Check(await AppendCycle(e with { SubjectTenant=otherTenant },null) is null,"cycle cannot cross tenant");
        var firstId=await AppendCycle(e,null) ?? throw new InvalidOperationException("First cycle issuance denied.");
        var active=await writer.ReadCycleEvidenceAsync(intent,CancellationToken.None);
        var firstGrant=active.Grant ?? throw new InvalidOperationException("Cycle grant missing.");
        var firstHead=active.Head ?? throw new InvalidOperationException("Cycle head missing.");
        Check(firstId!=Guid.Empty && firstGrant.GrantId==firstId && firstHead.GrantId==firstId &&
            firstGrant.Revision==1 && firstHead.Revision==1 && firstGrant.RevokedAtUtc is null,
            "first issuance and head share immutable ID and revision");
        Check(await AppendCycle(e,null) is null,"stale absence denied after first issuance");
        var cycleRevoke=intent with { Command=C06DelegationCommand.Revoke,ExpectedGrantRevision=1,UntilUtc=null };
        var cycleRevEvidence=Evidence(cycleRevoke) with { Existing=firstGrant,ActorEffectiveRead=null };
        Check(await AppendCycle(cycleRevEvidence,firstHead with { GrantId=Guid.NewGuid() }) is null,
            "forged head rejected by actual independent read");
        Check(await AppendCycle(cycleRevEvidence,firstHead with { Revision=3 }) is null,"stale head revision denied");
        Check(await AppendCycle(cycleRevEvidence,firstHead)==firstId,"actual first revocation through cycle writer");
        var terminal=await writer.ReadCycleEvidenceAsync(intent,CancellationToken.None);
        var terminalGrant=terminal.Grant ?? throw new InvalidOperationException("Terminal grant missing.");
        var terminalHead=terminal.Head ?? throw new InvalidOperationException("Terminal head missing.");
        Check(terminalGrant.Revision==2 && terminalHead.Revision==2 && terminalGrant.RevokedAtUtc==now &&
            terminalGrant.GrantId==firstId,"revocation retains immutable issuance identity");
        var nextIntent=intent with { ExpectedGrantRevision=2 };
        var nextEvidence=Evidence(nextIntent) with { Existing=terminalGrant };
        Check(await AppendCycle(nextEvidence with { StepUp=Proof(intent) },terminalHead) is null,
            "old intent proof cannot reissue");
        Check(await AppendCycle(nextEvidence with { ActorEffectiveRead=null },terminalHead) is null,
            "reissue still requires current own ABAC");
        Check(await AppendCycle(nextEvidence,firstHead) is null,"old active head cannot authorize reissue");
        Check(await AppendCycle(nextEvidence,terminalHead,"short") is null,"reissue reason is validated");
        var legacyDigest=MySqlC06DelegationLedger.Digests(nextEvidence,session,key,reason);
        var cycleDigest=MySqlC06DelegationLedger.Digests(nextEvidence,session,key,reason,true,terminalHead);
        var foreignDigest=MySqlC06DelegationLedger.Digests(nextEvidence,session,key,reason,true,
            terminalHead with { GrantId=Guid.NewGuid() });
        try
        {
            Check(!legacyDigest.Intent.SequenceEqual(cycleDigest.Intent),"cycle metadata domain separated from legacy");
            Check(!cycleDigest.Intent.SequenceEqual(foreignDigest.Intent),"cycle metadata bound to predecessor ID");
        }
        finally
        {
            foreach(var digestPair in new[]{legacyDigest,cycleDigest,foreignDigest})
            {
                CryptographicOperations.ZeroMemory(digestPair.Purpose);
                CryptographicOperations.ZeroMemory(digestPair.Intent);
            }
        }
        var secondId=await AppendCycle(nextEvidence,terminalHead) ?? throw new InvalidOperationException("Reissue denied.");
        var second=await writer.ReadCycleEvidenceAsync(nextIntent,CancellationToken.None);
        var secondGrant=second.Grant ?? throw new InvalidOperationException("Reissue missing.");
        var secondHead=second.Head ?? throw new InvalidOperationException("Reissue head missing.");
        Check(secondId!=firstId && secondGrant.GrantId==secondId && secondHead.GrantId==secondId &&
            secondGrant.Revision==3 && secondHead.Revision==3 && secondGrant.RevokedAtUtc is null,
            "new issuance replaces only derived head, not immutable history");
        Check(await AppendCycle(nextEvidence,terminalHead) is null,"terminal reissue replay denied");
        Check(await AppendCycle(cycleRevEvidence,firstHead) is null,"old revoke cannot target later generation");
        Check(await Count("c06_delegation_grant")==2 && await Count("c06_delegation_revocation")==1 &&
            await Count("c06_delegation_cycle_head")==1,"old and new issuances retained");
        var revokeSecond=cycleRevoke with { ExpectedGrantRevision=3 };
        Check(await AppendCycle(Evidence(revokeSecond) with { Existing=secondGrant,ActorEffectiveRead=null },secondHead)==secondId,
            "later issuance revocable by its own revision");
        var last=await writer.ReadCycleEvidenceAsync(revokeSecond,CancellationToken.None);
        Check(last.Head?.Revision==4 && last.Grant?.RevokedAtUtc==now,"second revocation advances head");
        await tx.RollbackAsync();
        await using var cycleVerify=new MySqlCommand("SELECT (SELECT COUNT(*) FROM c06_delegation_grant)+(SELECT COUNT(*) FROM c06_delegation_revocation)+(SELECT COUNT(*) FROM c06_delegation_cycle_head)",connection);
        Check(Convert.ToInt64(await cycleVerify.ExecuteScalarAsync())==0,"caller rollback includes derived head");
        Console.WriteLine($"PASS C06-11 CYCLE WRITER MYSQL checks={checks}; synthetic proof only, no HTTP or effective grant claim");
        return;
    }
    Check(await Count("c06_delegation_grant")==0,"empty fixture");
    Check(!await Append(e with { CommercialAccessVerified=false }),"commercial unknown denied");
    Check(!await Append(e with { SubjectTenant=otherTenant }),"foreign subject tenant denied");
    Check(!await Append(e with { StepUp=Proof(intent with { RoleId=Guid.NewGuid() }) }),"mismatched proof intent denied");
    Check(!await Append(e,"short"),"short purpose denied");
    Check(!await Append(e,"synthetic\ncontrol"),"control purpose denied");
    Check(!await Append(e,"synthetic malformed \uD800"),"invalid Unicode denied");
    Check(await Count("c06_delegation_grant")==0,"denials preserve rows");
    var digest=MySqlC06DelegationLedger.Digests(e,session,key,reason);
    var same=MySqlC06DelegationLedger.Digests(e,session,key," "+reason+" ");
    Check(digest.Purpose.SequenceEqual(same.Purpose)&&digest.Intent.SequenceEqual(same.Intent),"canonical reason framing");
    var differentSession=MySqlC06DelegationLedger.Digests(e,Guid.NewGuid(),key,reason);
    Check(digest.Purpose.SequenceEqual(differentSession.Purpose)&&!digest.Intent.SequenceEqual(differentSession.Intent),"intent bound to verified session");
    var differentRole=MySqlC06DelegationLedger.Digests(Evidence(intent with { RoleId=Guid.NewGuid() }),session,key,reason);
    Check(!digest.Intent.SequenceEqual(differentRole.Intent),"intent bound to role");
    var differentPurpose=MySqlC06DelegationLedger.Digests(e,session,key,"Another synthetic purpose");
    Check(!digest.Purpose.SequenceEqual(differentPurpose.Purpose)&&!digest.Intent.SequenceEqual(differentPurpose.Intent),"purpose bound to intent");
    Check(await Append(e),"actual parameterized issuance");
    Check(await Count("c06_delegation_grant")==1,"one issuance inside transaction");
    Check(!await Append(e),"stale absence cannot repeat issuance");
    var grant=new C06DelegationGrant(scope,subject,target,role,1,now,intent.UntilUtc!.Value,null);
    var revoke=intent with { Command=C06DelegationCommand.Revoke,ExpectedGrantRevision=1,UntilUtc=null };
    var revEvidence=Evidence(revoke) with { Existing=grant,ActorEffectiveRead=null };
    Check(!await Append(revEvidence with { Existing=grant with { FromUtc=now.AddSeconds(-1) } }),"stale existing grant denied");
    Check(!await Append(revEvidence with { Existing=grant with { RoleId=Guid.NewGuid() } }),"different stored role denied");
    Check(await Count("c06_delegation_revocation")==0,"revocation denials preserve rows");
    Check(await Append(revEvidence),"actual parameterized revocation");
    Check(await Count("c06_delegation_revocation")==1,"one revocation in transaction");
    Check(!await Append(revEvidence),"stale revoked state cannot append again");
    Check(!await Append(e),"cannot reinstate revoked scope");
    Check(await Count("c06_delegation_grant")==1 && await Count("c06_delegation_revocation")==1,"immutable event counts");
    await tx.RollbackAsync();
    await using var verify=new MySqlCommand("SELECT (SELECT COUNT(*) FROM c06_delegation_grant)+(SELECT COUNT(*) FROM c06_delegation_revocation)",connection);
    Check(Convert.ToInt64(await verify.ExecuteScalarAsync())==0,"caller rollback removes both uncommitted events");
    Console.WriteLine($"PASS C06-11 WRITER MYSQL checks={checks}; synthetic proof fixture only, no HTTP or operational authority");
}
finally { CryptographicOperations.ZeroMemory(key); }
