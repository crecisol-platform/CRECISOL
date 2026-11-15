using System.Security.Cryptography;
using Crecisol.Application.Identity;
using Crecisol.Application.Tenancy;
using Crecisol.CredentialSecurity;
using Crecisol.Domain.Identity;
using Crecisol.Domain.Tenancy;
using Crecisol.Infrastructure.Identity;
using Crecisol.Infrastructure.Tenancy;
using MySqlConnector;

namespace Crecisol.NeutralRegistration.IntegrationTests;

/// <summary>
/// Test-only, isolated SQL authorization seed. Does NOT simulate production
/// MFA, an operator session or real approval; proves that mailbox possession
/// alone is insufficient, the separate SQL gate is one-use, and the existing
/// C05-04 contextual password contract refuses pending/revoked membership.
/// </summary>
internal static class LocalApprovedMembershipHarness
{
    public static async Task VerifyAsync(
        MySqlConnection setup, string activationDsn, string directoryDsn,
        string rateSetupDsn, string rateRunnerDsn,
        string mfaSetupDsn, string mfaWorkerDsn, byte[] trustedMaster, string knownPassword, Guid sourceIntent,
        ITrustedVerifiedHostCatalog hosts, ITrustedTenantCatalogReader tenants,
        ITrustedHostUtcClock clock, Action<bool,string> pass,
        CancellationToken ct)
    {
        var tenantB=TenantId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
        var tenantA=TenantId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
        var rawIntent = MySqlRfc4122Guid.Encode(sourceIntent);
        Guid actorId;
        await using (var cmd=new MySqlCommand("""
            SELECT actor_id FROM c05_02_candidate_accounts
             WHERE source_tenant_id=@tenant AND source_intent_id=@intent
            """,setup))
        {
            cmd.Parameters.Add("@tenant",MySqlDbType.Binary).Value=
                MySqlRfc4122Guid.Encode(tenantB.Value);
            cmd.Parameters.Add("@intent",MySqlDbType.Binary).Value=rawIntent;
            var result=(byte[])(await cmd.ExecuteScalarAsync(ct) ??
                throw new InvalidOperationException("Fixture candidate missing."));
            try { actorId=MySqlRfc4122Guid.Decode(result); }
            finally { CryptographicOperations.ZeroMemory(result); }
        }
        var actor=TenantActorId.From(actorId);
        var login=CanonicalLoginHandle.Parse("Integrated+One@example.test");
        var hasher=new Argon2idPasswordHasher(
            PasswordHashingPolicy.Default,new LocalOnlyFixturePasswordScreen());
        var unknown=hasher.CreateVerifier(Convert.ToBase64String(
            RandomNumberGenerator.GetBytes(32))).ExportForTrustedCredentialStore();
        using var directory=new MySqlDisposableApprovedLoginDirectory(
            directoryDsn,trustedMaster);
        var activation=new MySqlDisposableMembershipActivation(activationDsn);
        var resolver=new ResolveVerifiedTenantHost(hosts,clock);

        PrepareContextualPasswordLogin NewLogin(CanonicalTenantHost host) =>
            new(directory,new LocalOnlyDummyVerifier(unknown),
                new Argon2idLoginCandidateVerifier(hasher),
                new LocalOnlyLoginHost(host),directory,resolver,tenants,clock);

        var approvedHost=CanonicalTenantHost.Parse("b.crecisol.test");
        var foreignHost=CanonicalTenantHost.Parse("a.crecisol.test");
        var service=NewLogin(approvedHost);
        var noApproval=await service.ExecuteAsync(
            login,knownPassword.AsMemory(),ct);
        pass(noApproval.Status==ContextualLoginPreparationStatus.Denied &&
             noApproval.Evidence is null,
             "Verified mailbox+Argon2id alone cannot prepare contextual login without membership");
        var pending=await directory.FindExactAsync(login,ct);
        pass(pending is { Count: 1 } &&
             pending[0].Account.State==IdentityAccountState.PendingVerification &&
             pending[0].Credential.State==AccountCredentialState.PendingEnrollment &&
             (await directory.FindAllByActorAsync(actor,ct))?.Count==0,
             "Read-only global directory returns pending without an active tenant link");

        pass(!await activation.TryActivateAsync(
                 tenantB,actor,Guid.NewGuid(),ct) &&
             !await activation.TryActivateAsync(
                 tenantA,actor,Guid.NewGuid(),ct),
            "Unissued and cross-tenant approvals cannot activate any candidate");

        // Privileged setup is intentionally distinct from the EXECUTE-only
        // backend approval consumer. These values are synthetic fixtures,
        // not testimony of a real user session or MFA verification.
        var approver=Guid.NewGuid();
        var approval=Guid.NewGuid();
        await using (var cmd=new MySqlCommand("""
            INSERT INTO c05_02_member_authorities
              (tenant_id,approver_actor_id,lifecycle,session_revision,
               mfa_revision,authorized_utc,expires_utc)
            VALUES(@tenant,@approver,'REVOKED',5,9,UTC_TIMESTAMP(6),
                   DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 20 MINUTE))
            """,setup))
        {
            cmd.Parameters.Add("@tenant",MySqlDbType.Binary).Value=
                MySqlRfc4122Guid.Encode(tenantB.Value);
            cmd.Parameters.Add("@approver",MySqlDbType.Binary).Value=
                MySqlRfc4122Guid.Encode(approver);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await using (var cmd=new MySqlCommand("""
            INSERT INTO c05_02_member_approval_orders
              (tenant_id,approval_id,candidate_actor_id,approver_actor_id,
               expected_session_revision,expected_mfa_revision,lifecycle,
               issued_utc,expires_utc)
            VALUES(@tenant,@approval,@actor,@approver,5,9,'PENDING',
                   UTC_TIMESTAMP(6),DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 5 MINUTE))
            """,setup))
        {
            cmd.Parameters.Add("@tenant",MySqlDbType.Binary).Value=
                MySqlRfc4122Guid.Encode(tenantB.Value);
            cmd.Parameters.Add("@approval",MySqlDbType.Binary).Value=
                MySqlRfc4122Guid.Encode(approval);
            cmd.Parameters.Add("@actor",MySqlDbType.Binary).Value=
                MySqlRfc4122Guid.Encode(actorId);
            cmd.Parameters.Add("@approver",MySqlDbType.Binary).Value=
                MySqlRfc4122Guid.Encode(approver);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        pass(!await activation.TryActivateAsync(tenantB,actor,approval,ct),
            "Revoked synthetic approver cannot approve a verified mailbox");
        await ExecuteAsync(setup, """
            UPDATE c05_02_member_authorities SET lifecycle='ACTIVE',
                session_revision=6
             WHERE tenant_id=X'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb'
            """,ct);
        pass(!await activation.TryActivateAsync(tenantB,actor,approval,ct),
            "Mismatched approver session revision cannot approve membership");
        await ExecuteAsync(setup, """
            UPDATE c05_02_member_authorities SET session_revision=5,
                mfa_revision=10
             WHERE tenant_id=X'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb'
            """,ct);
        pass(!await activation.TryActivateAsync(tenantB,actor,approval,ct),
            "Mismatched MFA revision denies membership even with a valid password");
        await ExecuteAsync(setup, """
            UPDATE c05_02_member_authorities SET mfa_revision=9
             WHERE tenant_id=X'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb'
            """,ct);
        pass(!await activation.TryActivateAsync(tenantA,actor,approval,ct) &&
             !await activation.TryActivateAsync(
                 tenantB,TenantActorId.From(Guid.NewGuid()),approval,ct),
            "Foreign tenant and unrelated actor cannot consume another approval");

        var calls=await Task.WhenAll(Enumerable.Range(0,12)
            .Select(_=>Task.Run(()=>activation.TryActivateAsync(
                tenantB,actor,approval,CancellationToken.None))));
        pass(calls.Count(x=>x)==1 &&
             await ScalarAsync(setup,
                 "SELECT COUNT(*) FROM c05_02_approved_memberships") == 1 &&
             await ScalarAsync(setup,
                 "SELECT COUNT(*) FROM c05_02_member_approval_orders WHERE lifecycle='CONSUMED'") == 1,
             "Twelve racing independent approvals activate exactly one membership and consume one proof");
        pass(!await activation.TryActivateAsync(tenantB,actor,approval,ct),
            "Consumed approval cannot reactivate or duplicate tenant membership");

        var after=await directory.FindExactAsync(login,ct);
        var links=await directory.FindAllByActorAsync(actor,ct);
        pass(after is { Count: 1 } &&
             after[0].Account.State==IdentityAccountState.Active &&
             after[0].Credential.State==AccountCredentialState.Active &&
             links is { Count: 1 } &&
             links[0].TenantId==tenantB && links[0].State==TenantMembershipState.Active,
             "Only separately approved account can become ACTIVE with one B tenant link");

        var ready=await service.ExecuteAsync(login,knownPassword.AsMemory(),ct);
        pass(ready.Status==ContextualLoginPreparationStatus.Ready &&
             ready.Evidence?.TenantId==tenantB &&
             ready.Evidence?.ActorId==actor &&
             ready.Evidence?.MembershipRevision==1 &&
             ready.Evidence?.HostRevision==1,
             "Real MySQL directory + Argon2id + unique host B + active membership prepare a non-authorizing login");
        await LocalApprovedThrottledLoginHarness.VerifyAsync(
            rateSetupDsn,rateRunnerDsn,service,
            await resolver.ResolveAsync(approvedHost,ct),login,knownPassword,pass,ct);
        await LocalMfaBoundSessionHarness.VerifyAsync(
            setup,mfaSetupDsn,mfaWorkerDsn,service,login,knownPassword,
            ready,resolver,await resolver.ResolveAsync(approvedHost,ct),
            await resolver.ResolveAsync(foreignHost,ct),pass,ct);
        var wrongPassword=await service.ExecuteAsync(
            login,Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).AsMemory(),ct);
        pass(wrongPassword.Status==ContextualLoginPreparationStatus.Denied &&
             wrongPassword.Evidence is null,
             "Approved account still denies a wrong Argon2id password");
        var wrongHost=await NewLogin(foreignHost).ExecuteAsync(
            login,knownPassword.AsMemory(),ct);
        pass(wrongHost.Status==ContextualLoginPreparationStatus.Denied &&
             wrongHost.Evidence is null,
             "Verified tenant A Host cannot select a B-only approved membership");
        var unknownLogin=await service.ExecuteAsync(
            CanonicalLoginHandle.Parse("Unregistered@Example.test"),
            knownPassword.AsMemory(),ct);
        pass(unknownLogin.Status==ContextualLoginPreparationStatus.Denied &&
             unknownLogin.Evidence is null,
             "Unknown global login performs dummy Argon2id and receives no context");

        var directWriteDenied=false;
        await using (var readOnly=new MySqlConnection(directoryDsn))
        {
            await readOnly.OpenAsync(ct);
            await using var cmd=new MySqlCommand(
                "UPDATE c05_02_approved_memberships SET lifecycle='ACTIVE'",readOnly);
            try { _=await cmd.ExecuteNonQueryAsync(ct); }
            catch(MySqlException ex) when(ex.Number is 1142 or 1143)
            { directWriteDenied=true; }
        }
        pass(directWriteDenied, "Login directory SQL role cannot mutate membership or approval");
        var directReadDenied=false;
        await using (var noRead=new MySqlConnection(activationDsn))
        {
            await noRead.OpenAsync(ct);
            await using var cmd=new MySqlCommand(
                "SELECT verifier_phc FROM c05_02_candidate_accounts",noRead);
            try { _=await cmd.ExecuteScalarAsync(ct); }
            catch(MySqlException ex) when(ex.Number is 1142 or 1143)
            { directReadDenied=true; }
        }
        pass(directReadDenied, "Activation role cannot read PHC, approvals or tenant membership rows");

        // A trusted fixture revocation simulates a later independent security
        // decision; it is not a public/API revocation operation or full CAS.
        await ExecuteAsync(setup, """
            UPDATE c05_02_approved_memberships
               SET lifecycle='REVOKED',revision=revision+1,
                   changed_utc=UTC_TIMESTAMP(6)
             WHERE tenant_id=X'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb'
            """,ct);
        await ExecuteAsync(setup, """
            UPDATE c05_02_candidate_accounts
               SET lifecycle='REVOKED'
             WHERE source_tenant_id=X'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb'
               AND lifecycle='ACTIVE'
            """,ct);
        var revoked=await service.ExecuteAsync(login,knownPassword.AsMemory(),ct);
        pass(revoked.Status==ContextualLoginPreparationStatus.Denied &&
             revoked.Evidence is null,
             "Post-activation revocation immediately invalidates contextual login preparation");

        pass(ready.Evidence is not null &&
             ready.Evidence.ToString()=="PreparedContextualLoginEvidence[redacted]",
             "Prepared login evidence is redacted and cannot by itself grant an HTTP session");
    }

    private static async Task ExecuteAsync(MySqlConnection db, string sql, CancellationToken ct)
    {
        await using var cmd=new MySqlCommand(sql,db);
        await cmd.ExecuteNonQueryAsync(ct);
    }
    private static async Task<int> ScalarAsync(MySqlConnection db, string sql)
    {
        await using var cmd=new MySqlCommand(sql,db);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private sealed class LocalOnlyDummyVerifier(string protectedPhc) :
        ITrustedUnknownLoginVerifier
    {
        public ValueTask<string?> ReadDummyVerifierAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return ValueTask.FromResult<string?>(protectedPhc);
        }
    }

    private sealed class LocalOnlyLoginHost(CanonicalTenantHost trustedHost) :
        ITrustedLoginEntryReader
    {
        public ValueTask<TrustedLoginEntry?> ReadServerVerifiedEntryAsync(
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return ValueTask.FromResult<TrustedLoginEntry?>(
                TrustedLoginEntry.ForTenantHost(trustedHost));
        }
    }
}
