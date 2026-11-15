using System.Collections.Concurrent;
using System.Security.Cryptography;
using Crecisol.Application.Identity;
using Crecisol.Application.Tenancy;
using Crecisol.CredentialSecurity;
using Crecisol.Domain.Tenancy;
using Crecisol.Infrastructure.Identity;
using MySqlConnector;

namespace Crecisol.NeutralRegistration.IntegrationTests;

/// <summary>
/// C05-only test: uses a real cryptographic TOTP verifier and real .NET10
/// Argon2id login with a disposable SQL 8.4 MFA+session transaction.
/// Credential and membership records are copied by trusted TEST fixture
/// setup. No production identity, browser cookie or authorization is issued.
/// </summary>
internal static class LocalMfaBoundSessionHarness
{
    public static async Task VerifyAsync(
        MySqlConnection protectedCandidateSetup,
        string sessionSetupDsn,string sessionWorkerDsn,string approvalWorkerDsn,
        PrepareContextualPasswordLogin actualLogin,
        CanonicalLoginHandle login,string currentPassword,
        ContextualLoginPreparation approvedLogin,
        ResolveVerifiedTenantHost resolver,
        VerifiedTenantHostHint trustedB,VerifiedTenantHostHint trustedA,
        Action<bool,string> pass,CancellationToken ct)
    {
        if(approvedLogin.Status!=ContextualLoginPreparationStatus.Ready ||
           approvedLogin.Evidence is not { } approved ||
           approved.TenantId!=trustedB.TenantId ||
           approved.HostRevision!=trustedB.Revision)
            throw new InvalidOperationException("Approved synthetic identity required.");
        await using var db=new MySqlConnection(sessionSetupDsn);
        await db.OpenAsync(ct);
        string phc;
        await using(var read=new MySqlCommand("""
            SELECT verifier_phc
              FROM c05_02_candidate_accounts
             WHERE source_tenant_id=@tenant AND actor_id=@actor AND lifecycle='ACTIVE'
            """,protectedCandidateSetup))
        {
            read.Parameters.Add("@tenant",MySqlDbType.Binary).Value=
                approved.TenantId.Value.ToByteArray(bigEndian:true);
            read.Parameters.Add("@actor",MySqlDbType.Binary).Value=
                approved.ActorId.Value.ToByteArray(bigEndian:true);
            phc=(string)(await read.ExecuteScalarAsync(ct) ??
                 throw new InvalidOperationException("Active test credential missing."));
        }

        var expires=DateTime.UtcNow.AddHours(2);
        await using(var seed=new MySqlCommand("""
            INSERT INTO c05_05_subjects
              (tenant_id,actor_id,link_id,account_revision,credential_revision,
               membership_revision,tenant_revision,host_revision,subject_revision,
               account_active,credential_active,membership_active,
               host_active,tenant_active,member_expires_utc)
            VALUES(@tenant,@actor,@link,@a,@c,@m,@t,@h,1,1,1,1,1,1,@expires)
            """,db))
        {
            AddIdentity(seed,approved);
            seed.Parameters.Add("@expires",MySqlDbType.DateTime).Value=expires;
            pass(await seed.ExecuteNonQueryAsync(ct)==1,
                "Trusted fixture bridges approved B identity to disposable MFA/session SQL subject");
        }
        await using(var credential=new MySqlCommand("""
            INSERT INTO c05_06_credentials
                (actor_id,verifier_phc,credential_revision,credential_active,realm)
            VALUES(@actor,@phc,@revision,1,'TENANT')
            """,db))
        {
            credential.Parameters.Add("@actor",MySqlDbType.Binary).Value=
                approved.ActorId.Value.ToByteArray(bigEndian:true);
            credential.Parameters.Add("@phc",MySqlDbType.VarChar).Value=phc;
            credential.Parameters.Add("@revision",MySqlDbType.UInt64).Value=
                (ulong)approved.CredentialRevision;
            pass(await credential.ExecuteNonQueryAsync(ct)==1,
                "MFA fixture verifies same approved actor and Argon2id PHC from neutral registration");
        }
        byte[] digest=RandomNumberGenerator.GetBytes(32);
        try
        {
            await using var mailbox=new MySqlCommand("""
                INSERT INTO c05_06_mailboxes
                    (tenant_id,actor_id,mailbox_digest,mailbox_revision,verified)
                VALUES(@tenant,@actor,@digest,1,1)
                """,db);
            mailbox.Parameters.Add("@tenant",MySqlDbType.Binary).Value=
                approved.TenantId.Value.ToByteArray(bigEndian:true);
            mailbox.Parameters.Add("@actor",MySqlDbType.Binary).Value=
                approved.ActorId.Value.ToByteArray(bigEndian:true);
            mailbox.Parameters.Add("@digest",MySqlDbType.Binary).Value=digest;
            pass(await mailbox.ExecuteNonQueryAsync(ct)==1,
                 "Only HMAC-shaped test mailbox digest, not plaintext, bridges to MFA fixture");
        }
        finally {CryptographicOperations.ZeroMemory(digest);}

        var chalKey=RandomNumberGenerator.GetBytes(32);
        var backupKey=RandomNumberGenerator.GetBytes(32);
        var tokenKey=RandomNumberGenerator.GetBytes(32);
        using var challenge=new DisposableMfaChallengeProtector(chalKey);
        using var backup=new MfaRecoveryCodeProtector(backupKey);
        using var protector=new OpaqueSessionTokenProtector(tokenKey);
        CryptographicOperations.ZeroMemory(chalKey);
        CryptographicOperations.ZeroMemory(backupKey);
        CryptographicOperations.ZeroMemory(tokenKey);

        var mfaLedger=new MySqlDisposableMfaLedger(sessionWorkerDsn);
        using var secrets=new OnlyLocalTotpFactors();
        var reauth=new BackendPasswordRecheck(actualLogin,trustedB);
        var mfa=new DisposableMfaRiskUseCases(
            reauth,mfaLedger,secrets,challenge,new DisposableMfaBackupAdapter(backup));
        var boundLedger=new MySqlDisposableMfaBoundSessionLedger(sessionWorkerDsn);
        var issuance=new DisposableMfaBoundSessionIssuance(protector,boundLedger);
        var oldSessionLedger=new MySqlDisposableSessionLedger(sessionWorkerDsn);
        var scoped=new ScopedSessionLifecycle(protector,oldSessionLedger);

        pass(await issuance.TryIssueAsync(
                 approvedLogin,trustedB,null,ct) is null &&
             await ScalarAsync(db,"SELECT COUNT(*) FROM c05_05_sessions")==0,
             "Approved password without any MFA step-up cannot create a session");

        var legacyBypassDenied=false;
        try { _=await scoped.IssueAsync(approvedLogin,ct); }
        catch(MySqlException ex) when (ex.Number is 1142 or 1370 or 1044 or 1143)
        {legacyBypassDenied=true;}
        pass(legacyBypassDenied &&
             await ScalarAsync(db,"SELECT COUNT(*) FROM c05_05_sessions")==0,
             "Least-privilege worker cannot call legacy password-only session issuer");

        var enroll=await mfa.BeginEnrollmentAsync(
            login,currentPassword.AsMemory(),trustedB,DisposableMfaKind.Totp,ct);
        pass(enroll is not null &&
             enroll.ToString()=="DisposableMfaChallenge[redacted]",
             "Verified Argon2id and fresh B context open protected TOTP enrollment");
        var first=enroll ?? throw new InvalidOperationException("TOTP test enrollment refused.");
        pass(await mfa.ProveTotpAsync(trustedB,approved.ActorId,first,
                 DisposableMfaProofPurpose.Enroll,secrets.TotpCode(first.FactorId),ct),
             "Actual HMAC-SHA256 TOTP proof activates factor through SQL CAS");
        secrets.AdvanceBy(TimeSpan.FromSeconds(65));

        var operation=Guid.NewGuid();
        var step=await mfa.BeginStepUpAsync(
            login,currentPassword.AsMemory(),trustedB,first.FactorId,operation,ct);
        pass(step is not null,"Current B password and enrolled factor open one expiring MFA step-up");
        var granted=step ?? throw new InvalidOperationException("MFA step-up not created.");
        pass(await issuance.TryIssueAsync(approvedLogin,trustedB,granted,ct) is null &&
             await ScalarAsync(db,"SELECT COUNT(*) FROM c05_05_sessions")==0,
             "Unproved MFA step-up cannot mint any session even with correct password");
        pass(await issuance.TryIssueAsync(approvedLogin,trustedA,granted,ct) is null,
             "Foreign tenant A cannot consume B MFA to mint a session");
        pass(!await mfa.ProveTotpAsync(
                 trustedA,approved.ActorId,granted,DisposableMfaProofPurpose.StepUp,
                 secrets.TotpCode(first.FactorId),ct),
             "Valid B TOTP cannot be committed as an A step-up");

        var realCode=secrets.TotpCode(first.FactorId);
        pass(await mfa.ProveTotpAsync(trustedB,approved.ActorId,granted,
                 DisposableMfaProofPurpose.StepUp,realCode,ct),
             "Actual cryptographic TOTP step-up is bound to protected B actor and operation");
        pass(!await mfa.ProveTotpAsync(trustedB,approved.ActorId,granted,
                 DisposableMfaProofPurpose.StepUp,realCode,ct),
             "Consumed TOTP step-up cannot be verified twice");
        var raced=await Task.WhenAll(Enumerable.Range(0,12).Select(_=>
            Task.Run(()=>issuance.TryIssueAsync(
                approvedLogin,trustedB,granted,CancellationToken.None))));
        var winners=raced.Where(x=>x is not null).ToArray();
        pass(winners.Length==1 &&
             await ScalarAsync(db,"SELECT COUNT(*) FROM c05_05_sessions")==1 &&
             await ScalarAsync(db,"SELECT COUNT(*) FROM c05_05_mfa_session_bindings")==1,
             "Twelve racing MFA-bound session issues yield exactly one durable session and binding");
        var bearer=winners[0]!.RevealForTrustedTransport();
        pass(await issuance.TryIssueAsync(approvedLogin,trustedB,granted,ct) is null &&
             await scoped.ValidateAsync(bearer,trustedB,ct) &&
             !await scoped.ValidateAsync(bearer,trustedA,ct),
             "MFA session is one-use, valid only on trusted Host B, never on tenant A");

        await LocalMfaApproverHarness.VerifyAsync(
            protectedCandidateSetup,approvalWorkerDsn,protector,bearer,
            approved.ActorId,trustedB,trustedA,pass,ct);

        await using var privateAccess=await LocalMfaPrivateResourceHarness.VerifyAsync(
            db,sessionWorkerDsn,protector,bearer,approved.ActorId,
            resolver,trustedB,trustedA,pass,ct);
        var rotated=await Task.WhenAll(Enumerable.Range(0,12).Select(_=>
            Task.Run(()=>scoped.RotateAsync(bearer,trustedB,CancellationToken.None))));
        var newTokens=rotated.Where(x=>x is not null).ToArray();
        pass(newTokens.Length==1 &&
             !await scoped.ValidateAsync(bearer,trustedB,ct) &&
             await scoped.ValidateAsync(
                 newTokens[0]!.RevealForTrustedTransport(),trustedB,ct),
             "Twelve racing refreshes rotate only one opaque HMAC bearer; old token is invalid");
        var current=newTokens[0]!.RevealForTrustedTransport();
        pass(await privateAccess.DeniesAsync(bearer,trustedB,ct) &&
             (await privateAccess.Reader.ExecuteAsync(
                 current,trustedB,privateAccess.OwnResource,ct)).ExternalKey==
                 "synthetic-b-owner-case" &&
             await privateAccess.Http.AllowedAsync(
                 current,trustedB,privateAccess.OwnResource,ct),
             "Old rotated bearer denied by HTTP/SQL; new MFA bearer retains exact scope");
        pass(await scoped.LogoutAsync(current,trustedB,ct) &&
             !await scoped.ValidateAsync(current,trustedB,ct) &&
             !await scoped.LogoutAsync(current,trustedB,ct),
             "Protected logout revokes real MySQL session immediately and cannot replay");
        pass(await privateAccess.DeniesAsync(current,trustedB,ct),
             "Protected logout invalidates subsequent private SQL and HTTP resource reads");

        var inaccessible=false;
        await using(var runner=new MySqlConnection(sessionWorkerDsn))
        {
            await runner.OpenAsync(ct);
            await using var select=new MySqlCommand(
                "SELECT token_digest FROM c05_05_sessions",runner);
            try {_=await select.ExecuteScalarAsync(ct);}
            catch(MySqlException ex) when(ex.Number is 1142 or 1143)
            {inaccessible=true;}
        }
        pass(inaccessible,"EXECUTE-only MFA session worker cannot SELECT protected token digests");

        secrets.AdvanceBy(TimeSpan.FromSeconds(65));
        var step2=await mfa.BeginStepUpAsync(
            login,currentPassword.AsMemory(),trustedB,first.FactorId,Guid.NewGuid(),ct);
        pass(step2 is not null &&
             await mfa.ProveTotpAsync(trustedB,approved.ActorId,step2!,
                 DisposableMfaProofPurpose.StepUp,secrets.TotpCode(first.FactorId),ct),
             "Second valid MFA proof remains scoped to latest B revisions");
        await using(var revoke=new MySqlCommand("""
            UPDATE c05_05_subjects
               SET membership_active=0,membership_revision=membership_revision+1
             WHERE tenant_id=@tenant AND actor_id=@actor
            """,db))
        {
            revoke.Parameters.Add("@tenant",MySqlDbType.Binary).Value=
                approved.TenantId.Value.ToByteArray(bigEndian:true);
            revoke.Parameters.Add("@actor",MySqlDbType.Binary).Value=
                approved.ActorId.Value.ToByteArray(bigEndian:true);
            _=await revoke.ExecuteNonQueryAsync(ct);
        }
        pass(await issuance.TryIssueAsync(
                 approvedLogin,trustedB,step2,ct) is null &&
             await ScalarAsync(db,"SELECT COUNT(*) FROM c05_05_sessions")==1,
             "Revoked membership/revision change blocks previously verified MFA from issuing session");
    }

    private static void AddIdentity(MySqlCommand cmd,PreparedContextualLoginEvidence proof)
    {
        cmd.Parameters.Add("@tenant",MySqlDbType.Binary).Value=
            proof.TenantId.Value.ToByteArray(bigEndian:true);
        cmd.Parameters.Add("@actor",MySqlDbType.Binary).Value=
            proof.ActorId.Value.ToByteArray(bigEndian:true);
        cmd.Parameters.Add("@link",MySqlDbType.Binary).Value=
            proof.LinkId.Value.ToByteArray(bigEndian:true);
        cmd.Parameters.Add("@a",MySqlDbType.UInt64).Value=(ulong)proof.AccountRevision;
        cmd.Parameters.Add("@c",MySqlDbType.UInt64).Value=(ulong)proof.CredentialRevision;
        cmd.Parameters.Add("@m",MySqlDbType.UInt64).Value=(ulong)proof.MembershipRevision;
        cmd.Parameters.Add("@t",MySqlDbType.UInt64).Value=(ulong)proof.TenantCatalogRevision;
        cmd.Parameters.Add("@h",MySqlDbType.UInt64).Value=(ulong)proof.HostRevision;
    }

    private static async Task<int> ScalarAsync(MySqlConnection db,string sql)
    {
        await using var cmd=new MySqlCommand(sql,db);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private sealed class BackendPasswordRecheck(
        PrepareContextualPasswordLogin login,VerifiedTenantHostHint trustedHost)
        : ITrustedImmediatePasswordReauthentication
    {
        public Task<ContextualLoginPreparation> VerifyFreshAsync(
            CanonicalLoginHandle candidate,ReadOnlyMemory<char> password,
            VerifiedTenantHostHint host,CancellationToken ct) =>
            host==trustedHost ? login.ExecuteAsync(candidate,password,ct) :
                Task.FromResult(ContextualLoginPreparation.Denied);
    }

    private sealed class OnlyLocalTotpFactors : ITrustedDisposableMfaSecrets,IDisposable
    {
        private sealed record Factor(TenantId Tenant,TenantActorId Actor,byte[] Seed);
        private readonly ConcurrentDictionary<Guid,Factor> factors=new();
        private long offsetSeconds;
        private DateTimeOffset Now() =>
            DateTimeOffset.UtcNow.AddSeconds(Interlocked.Read(ref offsetSeconds));
        public void AdvanceBy(TimeSpan value) =>
            Interlocked.Add(ref offsetSeconds,(long)value.TotalSeconds);
        public void RegisterPendingFixture(Guid factorId,TenantId tenant,
            TenantActorId actor,DisposableMfaKind kind)
        {
            if(kind!=DisposableMfaKind.Totp)
                throw new InvalidOperationException("Local TOTP-only fixture.");
            if(!factors.TryAdd(factorId,
                 new Factor(tenant,actor,TotpSha256Verifier.GenerateSeed())))
                throw new InvalidOperationException("Duplicate local TOTP factor.");
        }
        public void DiscardPendingFixture(Guid id)
        {
            if(factors.TryRemove(id,out var factor))
                CryptographicOperations.ZeroMemory(factor.Seed);
        }
        public string TotpCode(Guid id)
        {
            if(!factors.TryGetValue(id,out var factor))
                throw new InvalidOperationException("Local TOTP factor unavailable.");
            return TotpSha256Verifier.ComputeCode(
                factor.Seed,TotpSha256Verifier.StepAt(Now()));
        }
        public bool TryVerifyTotp(Guid id,TenantId tenant,TenantActorId actor,
            string? candidate,long lastCounter,out long nextCounter)
        {
            nextCounter=-1;
            return factors.TryGetValue(id,out var factor) &&
                factor.Tenant==tenant && factor.Actor==actor &&
                TotpSha256Verifier.TryVerify(
                    factor.Seed,candidate,Now(),lastCounter,out nextCounter);
        }
        public bool TryVerifyFixturePasskey(Guid id,TenantId tenant,TenantActorId actor,
            ReadOnlySpan<byte> challenge,DisposableMfaAssertion assertion,
            long lastCounter,out long newCounter)
        {
            newCounter=-1;
            return false;
        }
        public void Dispose()
        {
            foreach(var id in factors.Keys) DiscardPendingFixture(id);
        }
    }
}
