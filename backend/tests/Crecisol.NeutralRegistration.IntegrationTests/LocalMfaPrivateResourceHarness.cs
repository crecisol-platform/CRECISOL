using Crecisol.Application.Identity;
using Crecisol.Application.Tenancy;
using Crecisol.CredentialSecurity;
using Crecisol.Domain.Tenancy;
using Crecisol.Infrastructure.Tenancy;
using MySqlConnector;

namespace Crecisol.NeutralRegistration.IntegrationTests;

/// <summary>
/// Scoped resource authorization executes in the SAME isolated MySQL 8.4
/// process after actual Argon2id+TOTP/MFA-bound session. Fixtures are seeded
/// only by a separate privileged TEST account, never by the HTTP client.
/// </summary>
internal static class LocalMfaPrivateResourceHarness
{
    internal sealed record Context(
        ReadMfaAuthorizedPrivateResource Reader,TenantResourceId OwnResource,
        LocalMfaPrivateHttpHarness Http) : IAsyncDisposable
    {
        public async Task<bool> DeniesAsync(
            string? bearer, VerifiedTenantHostHint trustedHost, CancellationToken ct)
        {
            var denied = false;
            try { _=await Reader.ExecuteAsync(bearer,trustedHost,OwnResource,ct); }
            catch(UnauthorizedAccessException) {denied=true;}
            return denied && await Http.DeniedAsync(bearer,trustedHost,OwnResource,ct);
        }

        public ValueTask DisposeAsync() => Http.DisposeAsync();
    }

    public static async Task<Context> VerifyAsync(
        MySqlConnection setup,string workerDsn,
        OpaqueSessionTokenProtector sessionProtector,
        string mfaBoundBearer,TenantActorId approvedActor,
        ResolveVerifiedTenantHost resolver,
        VerifiedTenantHostHint hostB,VerifiedTenantHostHint hostA,
        Action<bool,string> pass,CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(setup);
        var tenantB=hostB.TenantId;
        var tenantA=hostA.TenantId;
        var outsiderB=TenantActorId.From(Guid.NewGuid());
        var outsiderA=TenantActorId.From(Guid.NewGuid());
        await SeedSubjectAsync(setup,tenantB,outsiderB,ct);
        await SeedSubjectAsync(setup,tenantA,outsiderA,ct);

        var branchB=TenantBranchId.From(Guid.NewGuid());
        var branchB2=TenantBranchId.From(Guid.NewGuid());
        var branchA=TenantBranchId.From(Guid.NewGuid());
        var own=TenantResourceId.From(Guid.NewGuid());
        var foreignOwner=TenantResourceId.From(Guid.NewGuid());
        var foreignBranch=TenantResourceId.From(Guid.NewGuid());
        var foreignTenant=TenantResourceId.From(Guid.NewGuid());
        var actionRead=TenantRequiredAction.FromServerPolicy("cases.read");
        var actionWrite=TenantRequiredAction.FromServerPolicy("cases.update");

        await SeedBranchAsync(setup,tenantB,branchB,ct);
        await SeedBranchAsync(setup,tenantB,branchB2,ct);
        await SeedBranchAsync(setup,tenantA,branchA,ct);
        await SeedResourceAsync(setup,tenantB,branchB,own,approvedActor,
            "synthetic-b-owner-case",ct);
        await SeedResourceAsync(setup,tenantB,branchB,foreignOwner,outsiderB,
            "synthetic-b-outsider-case",ct);
        await SeedResourceAsync(setup,tenantB,branchB2,foreignBranch,approvedActor,
            "synthetic-b-other-branch",ct);
        await SeedResourceAsync(setup,tenantA,branchA,foreignTenant,outsiderA,
            "synthetic-a-private-case",ct);
        await using (var grant=new MySqlCommand("""
            INSERT INTO c05_05_private_grants
              (tenant_id,actor_id,branch_id,action_key,access_scope,lifecycle,
               revision,issued_utc,expires_utc)
            VALUES (@tenant,@actor,@branch,'cases.read','OWNER','ACTIVE',1,
                    DATE_SUB(UTC_TIMESTAMP(6),INTERVAL 1 MINUTE),
                    DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 30 MINUTE))
            """,setup))
        {
            AddId(grant,"@tenant",tenantB.Value);
            AddId(grant,"@actor",approvedActor.Value);
            AddId(grant,"@branch",branchB.Value);
            pass(await grant.ExecuteNonQueryAsync(ct)==1,
                "Backend fixture grants only B actor OWNER-only cases.read in branch B1");
        }

        var store=new MySqlDisposableMfaPrivateResourceStore(workerDsn);
        var reader=new ReadMfaAuthorizedPrivateResource(
            sessionProtector,store,actionRead);
        var writer=new ReadMfaAuthorizedPrivateResource(
            sessionProtector,store,actionWrite);
        var current=await reader.ExecuteAsync(mfaBoundBearer,hostB,own,ct);
        pass(current.ExternalKey=="synthetic-b-owner-case" &&
             current.ToString()=="AuthorizedPrivateResourceReference[redacted]" &&
             reader.ToString()=="ReadMfaAuthorizedPrivateResource[redacted]" &&
             store.ToString()=="MySqlDisposableMfaPrivateResourceStore[redacted]",
             "Active MFA-bound B session reads exactly one owned B1 case atomically");

        var http=await LocalMfaPrivateHttpHarness.StartAsync(reader,resolver,hostB,hostA,ct);
        await http.VerifyAdversarialAsync(mfaBoundBearer,own,foreignOwner,
            foreignBranch,foreignTenant,pass,ct);

        pass(await DeniedAsync(reader,mfaBoundBearer,hostB,foreignOwner,ct),
            "Owner-only grant cannot read a different actor's case in same B branch");
        pass(await DeniedAsync(reader,mfaBoundBearer,hostB,foreignBranch,ct),
            "Branch B1 grant does not authorize a case in B2 even with same owner");
        pass(await DeniedAsync(reader,mfaBoundBearer,hostB,foreignTenant,ct),
            "MFA-bound tenant B session cannot read known private resource ID in A");
        pass(await DeniedAsync(reader,mfaBoundBearer,hostA,own,ct),
            "Forged/fallback tenant A host cannot read B private resource using B bearer");
        pass(await DeniedAsync(writer,mfaBoundBearer,hostB,own,ct),
            "Valid cases.read permission never grants cases.update");
        pass(await DeniedAsync(reader,"cs5_"+new string('Z',43),hostB,own,ct) &&
             await DeniedAsync(reader,null,hostB,own,ct),
             "Random or missing bearer cannot read a private resource");

        await MutateAsync(setup,"UPDATE c05_08_factors SET state='REVOKED' WHERE state='ACTIVE'",ct);
        pass(await DeniedAsync(reader,mfaBoundBearer,hostB,own,ct) &&
             await http.DeniedAsync(mfaBoundBearer,hostB,own,ct),
            "Revoked MFA factor immediately denies SQL and HTTP private resource reads");
        await MutateAsync(setup,"UPDATE c05_08_factors SET state='ACTIVE' WHERE state='REVOKED'",ct);

        await MutateAsync(setup,"UPDATE c05_05_private_grants SET access_scope='BRANCH',revision=revision+1",ct);
        var branchWide=await reader.ExecuteAsync(mfaBoundBearer,hostB,foreignOwner,ct);
        pass(branchWide.ExternalKey=="synthetic-b-outsider-case",
            "Explicit backend BRANCH grant expands access only within exact B1 branch");
        await MutateAsync(setup,"UPDATE c05_05_private_grants SET access_scope='OWNER',revision=revision+1",ct);
        pass(await DeniedAsync(reader,mfaBoundBearer,hostB,foreignOwner,ct),
            "Owner-only grant restores strict owner authorization immediately");

        await MutateAsync(setup,"UPDATE c05_05_private_grants SET lifecycle='REVOKED',revision=revision+1",ct);
        var revoked=await Task.WhenAll(Enumerable.Range(0,12).Select(_=>
            Task.Run(()=>DeniedAsync(
                reader,mfaBoundBearer,hostB,own,CancellationToken.None))));
        pass(revoked.All(x=>x) &&
             await http.DeniedAsync(mfaBoundBearer,hostB,own,ct),
            "Twelve concurrent private reads deny after scoped grant revocation");
        await MutateAsync(setup,"UPDATE c05_05_private_grants SET lifecycle='ACTIVE',revision=revision+1",ct);
        pass((await reader.ExecuteAsync(mfaBoundBearer,hostB,own,ct)).ExternalKey==
                "synthetic-b-owner-case",
            "Reactivated exact grant permits only the same still-current B owner");

        await MutateAsync(setup,"UPDATE c05_05_private_branches SET lifecycle='SUSPENDED',revision=revision+1 WHERE branch_id IN (SELECT branch_id FROM c05_05_private_resources WHERE external_key='synthetic-b-owner-case')",ct);
        pass(await DeniedAsync(reader,mfaBoundBearer,hostB,own,ct),
            "Suspended B branch blocks private access despite live MFA session");
        await MutateAsync(setup,"UPDATE c05_05_private_branches SET lifecycle='ACTIVE',revision=revision+1 WHERE lifecycle='SUSPENDED'",ct);

        await MutateAsync(setup,"UPDATE c05_05_private_resources SET lifecycle='SUSPENDED',revision=revision+1 WHERE external_key='synthetic-b-owner-case'",ct);
        pass(await DeniedAsync(reader,mfaBoundBearer,hostB,own,ct),
            "Suspended resource is denied even with current grant and session");
        await MutateAsync(setup,"UPDATE c05_05_private_resources SET lifecycle='ACTIVE',revision=revision+1 WHERE lifecycle='SUSPENDED'",ct);

        await MutateAsync(setup,"UPDATE c05_05_private_grants SET expires_utc=DATE_SUB(UTC_TIMESTAMP(6),INTERVAL 1 SECOND),revision=revision+1",ct);
        pass(await DeniedAsync(reader,mfaBoundBearer,hostB,own,ct),
            "Expired backend permission is immediately denied under SQL clock");
        await MutateAsync(setup,"UPDATE c05_05_private_grants SET expires_utc=DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 30 MINUTE),revision=revision+1",ct);

        var directReadDenied=false;
        await using (var worker=new MySqlConnection(workerDsn))
        {
            await worker.OpenAsync(ct);
            await using var sql=new MySqlCommand(
                "SELECT external_key FROM c05_05_private_resources",worker);
            try {_=await sql.ExecuteScalarAsync(ct);}
            catch(MySqlException ex) when (ex.Number is 1142 or 1143)
            {directReadDenied=true;}
        }
        pass(directReadDenied,
            "EXECUTE-only MFA/resource worker cannot bypass authorization with table SELECT");
        pass((await reader.ExecuteAsync(mfaBoundBearer,hostB,own,ct)).ExternalKey==
                "synthetic-b-owner-case" &&
             await http.AllowedAsync(mfaBoundBearer,hostB,own,ct),
            "Protected SQL and HTTP read remain available after negative tampering tests");
        return new Context(reader,own,http);
    }

    private static async Task<bool> DeniedAsync(
        ReadMfaAuthorizedPrivateResource reader,string? bearer,
        VerifiedTenantHostHint host,TenantResourceId resource,CancellationToken ct)
    {
        try { _=await reader.ExecuteAsync(bearer,host,resource,ct); }
        catch(UnauthorizedAccessException) {return true;}
        return false;
    }

    private static async Task SeedSubjectAsync(
        MySqlConnection db,TenantId tenant,TenantActorId actor,CancellationToken ct)
    {
        await using var cmd=new MySqlCommand("""
            INSERT INTO c05_05_subjects
             (tenant_id,actor_id,link_id,account_revision,credential_revision,
              membership_revision,tenant_revision,host_revision,subject_revision,
              account_active,credential_active,membership_active,host_active,
              tenant_active,member_expires_utc)
            VALUES (@tenant,@actor,@link,1,1,1,1,1,1,1,1,1,1,1,
                    DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 2 HOUR))
            """,db);
        AddId(cmd,"@tenant",tenant.Value);
        AddId(cmd,"@actor",actor.Value);
        AddId(cmd,"@link",Guid.NewGuid());
        if (await cmd.ExecuteNonQueryAsync(ct)!=1)
            throw new InvalidOperationException("Fixture outsider setup failed.");
    }

    private static async Task SeedBranchAsync(
        MySqlConnection db,TenantId tenant,TenantBranchId branch,CancellationToken ct)
    {
        await using var cmd=new MySqlCommand("""
            INSERT INTO c05_05_private_branches
              (tenant_id,branch_id,lifecycle,revision)
            VALUES(@tenant,@branch,'ACTIVE',1)
            """,db);
        AddId(cmd,"@tenant",tenant.Value);
        AddId(cmd,"@branch",branch.Value);
        if (await cmd.ExecuteNonQueryAsync(ct)!=1)
            throw new InvalidOperationException("Fixture branch setup failed.");
    }

    private static async Task SeedResourceAsync(
        MySqlConnection db,TenantId tenant,TenantBranchId branch,
        TenantResourceId resource,TenantActorId owner,string reference,
        CancellationToken ct)
    {
        await using var cmd=new MySqlCommand("""
            INSERT INTO c05_05_private_resources
              (tenant_id,resource_id,branch_id,owner_actor_id,
               lifecycle,revision,external_key)
            VALUES(@tenant,@resource,@branch,@owner,'ACTIVE',1,@reference)
            """,db);
        AddId(cmd,"@tenant",tenant.Value);
        AddId(cmd,"@resource",resource.Value);
        AddId(cmd,"@branch",branch.Value);
        AddId(cmd,"@owner",owner.Value);
        cmd.Parameters.Add("@reference",MySqlDbType.VarChar).Value=reference;
        if(await cmd.ExecuteNonQueryAsync(ct)!=1)
            throw new InvalidOperationException("Fixture resource setup failed.");
    }

    private static async Task MutateAsync(
        MySqlConnection db,string trustedFixtureOnlySql,CancellationToken ct)
    {
        await using var cmd=new MySqlCommand(trustedFixtureOnlySql,db);
        if (await cmd.ExecuteNonQueryAsync(ct)<1)
            throw new InvalidOperationException("Fixture mutation missing.");
    }

    private static void AddId(MySqlCommand cmd,string name,Guid id)=>
        cmd.Parameters.Add(name,MySqlDbType.Binary).Value=
            id.ToByteArray(bigEndian:true);
}
