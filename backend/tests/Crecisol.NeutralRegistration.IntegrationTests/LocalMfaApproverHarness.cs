using System.Security.Cryptography;
using Crecisol.Application.Identity;
using Crecisol.Domain.Tenancy;
using Crecisol.Infrastructure.Identity;
using Crecisol.Infrastructure.Tenancy;
using MySqlConnector;

namespace Crecisol.NeutralRegistration.IntegrationTests;

/// <summary>
/// Isolated-only C05: an approved actor FIRST proves Argon2id+TOTP and owns
/// an MFA-bound session, then a separate server-granted action is needed to
/// atomically approve another verified mailbox candidate in tenant B.
/// No real operator is bootstrapped by these fixture-only grants.
/// </summary>
internal static class LocalMfaApproverHarness
{
    public static async Task VerifyAsync(
        string approvalSetupDsn,string approvalWorkerDsn,
        ITrustedOpaqueSessionTokenProtector tokens,string bearer,
        TenantActorId actualApprover,VerifiedTenantHostHint trustedB,
        VerifiedTenantHostHint trustedA,Action<bool,string> pass,
        CancellationToken ct)
    {
        // Only this disposable setup connection can seed/remove synthetic
        // candidates; the operational approval worker stays EXECUTE-only.
        await using var protectedSetup=new MySqlConnection(approvalSetupDsn);
        await protectedSetup.OpenAsync(ct);
        var issuer=new ActivateMembershipWithMfaApprover(tokens,
            new MySqlDisposableMfaMembershipApprovalStore(approvalWorkerDsn));
        var candidate=await SeedPendingCandidateAsync(
            protectedSetup,trustedB,actualApprover,ct);
        try
        {
            pass(!await issuer.ExecuteAsync(bearer,trustedB,candidate.Actor,ct) &&
                 await CountAsync(protectedSetup,"c05_02_approved_memberships",
                     trustedB,candidate.Actor,ct)==0,
                "Valid MFA session without backend memberships.approve grant cannot activate candidate");
            pass(!await issuer.ExecuteAsync(null,trustedB,candidate.Actor,ct) &&
                 !await issuer.ExecuteAsync("cs5_"+new string('Z',43),
                     trustedB,candidate.Actor,ct) &&
                 !await issuer.ExecuteAsync(bearer,trustedA,candidate.Actor,ct) &&
                 !await issuer.ExecuteAsync(bearer,trustedB,actualApprover,ct),
                "Absent/forged MFA, foreign host and self-approval deny before membership writes");
            await AddGrantAsync(protectedSetup,trustedB,actualApprover,false,ct);
            pass(!await issuer.ExecuteAsync(bearer,trustedB,candidate.Actor,ct),
                "Explicitly suspended backend approver permission denies despite real MFA");
            await SetGrantAsync(protectedSetup,trustedB,actualApprover,true,ct);
            var results=await Task.WhenAll(Enumerable.Range(0,12).Select(_=>
                Task.Run(()=>issuer.ExecuteAsync(
                    bearer,trustedB,candidate.Actor,CancellationToken.None))));
            pass(results.Count(x=>x)==1 &&
                 await CountAsync(protectedSetup,"c05_02_approved_memberships",
                     trustedB,candidate.Actor,ct)==1 &&
                 await CountAsync(protectedSetup,"c05_02_member_approval_orders",
                     trustedB,candidate.Actor,ct)==1,
                "Twelve concurrent MFA-bound approver activations commit exactly one tenant membership");
            pass(await WasApprovedByAsync(protectedSetup,trustedB,candidate.Actor,
                     actualApprover,ct) &&
                 !await issuer.ExecuteAsync(bearer,trustedB,candidate.Actor,ct),
                "Committed order records the REAL MFA session actor, not a browser-provided approver");
            await SetGrantAsync(protectedSetup,trustedB,actualApprover,false,ct);
            var revokedCandidate=await SeedPendingCandidateAsync(
                protectedSetup,trustedB,actualApprover,ct);
            try
            {
                pass(!await issuer.ExecuteAsync(
                        bearer,trustedB,revokedCandidate.Actor,ct) &&
                     await CountAsync(protectedSetup,"c05_02_approved_memberships",
                         trustedB,revokedCandidate.Actor,ct)==0,
                    "Backend grant revocation immediately blocks further approvals with live session");
            }
            finally
            {
                await CleanupCandidateAsync(
                    protectedSetup,trustedB,revokedCandidate,ct);
            }
            var noDirectReads=false;
            await using(var limited=new MySqlConnection(approvalWorkerDsn))
            {
                await limited.OpenAsync(ct);
                await using var cmd=new MySqlCommand(
                    "SELECT token_digest FROM c05_05_disposable.c05_05_sessions",limited);
                try {_=await cmd.ExecuteScalarAsync(ct);}
                catch(MySqlException ex) when (ex.Number is 1142 or 1143 or 1044)
                {noDirectReads=true;}
            }
            pass(noDirectReads,
                "MFA approval EXECUTE-only worker cannot SELECT live session token digests");
        }
        finally
        {
            await CleanupCandidateAsync(protectedSetup,trustedB,candidate,ct);
            await using(var dropGrant=new MySqlCommand("""
                DELETE FROM c05_02_mfa_approval_grants
                 WHERE tenant_id=@t AND approver_actor_id=@a
                """,protectedSetup))
            {
                AddId(dropGrant,"@t",trustedB.TenantId.Value);
                AddId(dropGrant,"@a",actualApprover.Value);
                await dropGrant.ExecuteNonQueryAsync(ct);
            }
            await using(var dropAuthority=new MySqlCommand("""
                DELETE FROM c05_02_member_authorities
                 WHERE tenant_id=@t AND approver_actor_id=@a
                """,protectedSetup))
            {
                AddId(dropAuthority,"@t",trustedB.TenantId.Value);
                AddId(dropAuthority,"@a",actualApprover.Value);
                await dropAuthority.ExecuteNonQueryAsync(ct);
            }
        }
    }

    private sealed record CandidateFixture(TenantActorId Actor,Guid Intent);

    private static async Task<CandidateFixture> SeedPendingCandidateAsync(
        MySqlConnection setup,VerifiedTenantHostHint tenant,
        TenantActorId sourceActor,CancellationToken ct)
    {
        var id=Guid.NewGuid();
        var intent=Guid.NewGuid();
        string phc;
        await using(var read=new MySqlCommand("""
            SELECT verifier_phc FROM c05_02_candidate_accounts
             WHERE source_tenant_id=@t AND actor_id=@source AND lifecycle='ACTIVE'
            """,setup))
        {
            AddId(read,"@t",tenant.TenantId.Value);
            AddId(read,"@source",sourceActor.Value);
            phc=(string)(await read.ExecuteScalarAsync(ct) ??
                throw new InvalidOperationException("Approved fixture PHC unavailable."));
        }
        var mailboxDigest=RandomNumberGenerator.GetBytes(32);
        await using(var intentRow=new MySqlCommand("""
            INSERT INTO c05_02_neutral_intents
              (tenant_id,intent_id,peer_digest,mailbox_digest,nonce,tag,
               ciphertext,key_version,captured_at_utc,expires_at_utc)
            VALUES(@t,@i,@p,@m,@n,@tag,@cipher,1,UTC_TIMESTAMP(6),
                   DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 20 MINUTE))
            """,setup))
        {
            AddId(intentRow,"@t",tenant.TenantId.Value);
            AddId(intentRow,"@i",intent);
            intentRow.Parameters.Add("@p",MySqlDbType.Binary).Value=RandomNumberGenerator.GetBytes(32);
            intentRow.Parameters.Add("@m",MySqlDbType.Binary).Value=mailboxDigest;
            intentRow.Parameters.Add("@n",MySqlDbType.Binary).Value=RandomNumberGenerator.GetBytes(12);
            intentRow.Parameters.Add("@tag",MySqlDbType.Binary).Value=RandomNumberGenerator.GetBytes(16);
            intentRow.Parameters.Add("@cipher",MySqlDbType.VarBinary).Value=
                RandomNumberGenerator.GetBytes(32);
            await intentRow.ExecuteNonQueryAsync(ct);
        }
        await using(var proof=new MySqlCommand("""
            INSERT INTO c05_02_mailbox_proofs
              (tenant_id,intent_id,challenge_digest,lifecycle,attempts,
               issued_utc,expires_utc,verified_utc)
            VALUES(@t,@i,@digest,'VERIFIED',1,UTC_TIMESTAMP(6),
                   DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 8 MINUTE),UTC_TIMESTAMP(6))
            """,setup))
        {
            AddId(proof,"@t",tenant.TenantId.Value);
            AddId(proof,"@i",intent);
            proof.Parameters.Add("@digest",MySqlDbType.Binary).Value=
                RandomNumberGenerator.GetBytes(32);
            await proof.ExecuteNonQueryAsync(ct);
        }
        await using(var candidate=new MySqlCommand("""
            INSERT INTO c05_02_candidate_accounts
              (actor_id,login_digest,source_tenant_id,source_intent_id,
               mailbox_digest,verifier_phc,lifecycle,created_utc)
            VALUES(@a,@login,@t,@i,@mail,@phc,'PENDING_MEMBERSHIP',UTC_TIMESTAMP(6))
            """,setup))
        {
            AddId(candidate,"@a",id);
            AddId(candidate,"@t",tenant.TenantId.Value);
            AddId(candidate,"@i",intent);
            candidate.Parameters.Add("@login",MySqlDbType.Binary).Value=
                RandomNumberGenerator.GetBytes(32);
            candidate.Parameters.Add("@mail",MySqlDbType.Binary).Value=mailboxDigest;
            candidate.Parameters.Add("@phc",MySqlDbType.VarChar).Value=phc;
            await candidate.ExecuteNonQueryAsync(ct);
        }
        CryptographicOperations.ZeroMemory(mailboxDigest);
        return new CandidateFixture(TenantActorId.From(id),intent);
    }

    private static async Task AddGrantAsync(
        MySqlConnection db,VerifiedTenantHostHint host,TenantActorId approver,
        bool active,CancellationToken ct)
    {
        await using var cmd=new MySqlCommand("""
            INSERT INTO c05_02_mfa_approval_grants
              (tenant_id,approver_actor_id,action_key,active,grant_revision,expires_utc)
            VALUES(@t,@a,'memberships.approve',@enabled,1,
                   DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 8 MINUTE))
            """,db);
        AddId(cmd,"@t",host.TenantId.Value);
        AddId(cmd,"@a",approver.Value);
        cmd.Parameters.Add("@enabled",MySqlDbType.Byte).Value=active?1:0;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task SetGrantAsync(
        MySqlConnection db,VerifiedTenantHostHint host,TenantActorId approver,
        bool active,CancellationToken ct)
    {
        await using var cmd=new MySqlCommand("""
            UPDATE c05_02_mfa_approval_grants
               SET active=@enabled,grant_revision=grant_revision+1
             WHERE tenant_id=@t AND approver_actor_id=@a
            """,db);
        AddId(cmd,"@t",host.TenantId.Value);
        AddId(cmd,"@a",approver.Value);
        cmd.Parameters.Add("@enabled",MySqlDbType.Byte).Value=active?1:0;
        if(await cmd.ExecuteNonQueryAsync(ct)!=1)
            throw new InvalidOperationException("Fixture grant update failed.");
    }

    private static async Task<int> CountAsync(
        MySqlConnection db,string table,VerifiedTenantHostHint host,
        TenantActorId actor,CancellationToken ct)
    {
        // The table name originates from this file's fixed internal list.
        if(table is not ("c05_02_approved_memberships" or
                        "c05_02_member_approval_orders"))
            throw new InvalidOperationException("Unknown fixture assertion table.");
        var actorColumn=table=="c05_02_approved_memberships" ?
            "actor_id" : "candidate_actor_id";
        await using var cmd=new MySqlCommand(
            "SELECT COUNT(*) FROM "+table+" WHERE tenant_id=@t AND "+
            actorColumn+"=@a",db);
        AddId(cmd,"@t",host.TenantId.Value);
        AddId(cmd,"@a",actor.Value);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    private static async Task<bool> WasApprovedByAsync(
        MySqlConnection db,VerifiedTenantHostHint host,
        TenantActorId candidate,TenantActorId approver,CancellationToken ct)
    {
        await using var cmd=new MySqlCommand("""
            SELECT approved_by FROM c05_02_approved_memberships
             WHERE tenant_id=@t AND actor_id=@a
            """,db);
        AddId(cmd,"@t",host.TenantId.Value);
        AddId(cmd,"@a",candidate.Value);
        var raw=await cmd.ExecuteScalarAsync(ct) as byte[];
        if(raw is null) return false;
        try{return MySqlRfc4122Guid.Decode(raw)==approver.Value;}
        finally{CryptographicOperations.ZeroMemory(raw);}
    }

    private static async Task CleanupCandidateAsync(
        MySqlConnection db,VerifiedTenantHostHint host,CandidateFixture fixture,
        CancellationToken ct)
    {
        // Only disposable fixture rows; no production deletion APIs.
        foreach(var (table,idColumn) in new[] {
            ("c05_02_approved_memberships","actor_id"),
            ("c05_02_member_approval_orders","candidate_actor_id"),
            ("c05_02_candidate_accounts","actor_id")})
        {
            await using var cmd=new MySqlCommand(
                "DELETE FROM "+table+" WHERE tenant_id=@t AND "+idColumn+"=@a",
                db);
            // Candidate account uses source_tenant_id, not tenant_id.
            if(table=="c05_02_candidate_accounts")
                cmd.CommandText="DELETE FROM c05_02_candidate_accounts "+
                    "WHERE source_tenant_id=@t AND actor_id=@a";
            AddId(cmd,"@t",host.TenantId.Value);
            AddId(cmd,"@a",fixture.Actor.Value);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        foreach(var table in new[] {
            "c05_02_mailbox_proofs","c05_02_proof_dispatch",
            "c05_02_neutral_intents"})
        {
            await using var cmd=new MySqlCommand(
                "DELETE FROM "+table+" WHERE tenant_id=@t AND intent_id=@i",
                db);
            AddId(cmd,"@t",host.TenantId.Value);
            AddId(cmd,"@i",fixture.Intent);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    private static void AddId(MySqlCommand cmd,string name,Guid id) =>
        cmd.Parameters.Add(name,MySqlDbType.Binary).Value=
            MySqlRfc4122Guid.Encode(id);
}
