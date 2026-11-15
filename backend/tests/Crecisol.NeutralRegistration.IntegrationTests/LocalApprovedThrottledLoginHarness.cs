using System.Net;
using System.Security.Cryptography;
using Crecisol.Application.Identity;
using Crecisol.Application.Tenancy;
using Crecisol.Domain.Identity;
using Crecisol.Domain.Tenancy;
using Crecisol.Infrastructure.Identity;
using MySqlConnector;

namespace Crecisol.NeutralRegistration.IntegrationTests;

/// <summary>
/// Isolated C05-04/C05-10 composition over an ACTIVATED synthetic directory
/// account. SQL admission must consume source+subject budget BEFORE real
/// Argon2id. Every accepted HTTP-equivalent outcome is neutral, not a session
/// or authorization. No public route, operator or MFA is created here.
/// </summary>
internal static class LocalApprovedThrottledLoginHarness
{
    public static async Task VerifyAsync(
        string rateSetupDsn,string rateRunnerDsn,PrepareContextualPasswordLogin login,
        VerifiedTenantHostHint trustedHost,CanonicalLoginHandle canonical,
        string correctPassword, Action<bool,string> pass,CancellationToken ct)
    {
        await using var admin=new MySqlConnection(rateSetupDsn);
        await admin.OpenAsync(ct);
        await using (var seed=new MySqlCommand("""
            INSERT INTO c05_10_policies
              (tenant_id,purpose,subject_limit,source_limit,window_seconds,
               cooldown_seconds,enabled,host_revision,revision,changed_utc)
            VALUES (@tenant,'LOGIN',3,20,900,900,1,@host,1,UTC_TIMESTAMP(6))
            """,admin))
        {
            seed.Parameters.Add("@tenant",MySqlDbType.Binary).Value=
                trustedHost.TenantId.Value.ToByteArray(bigEndian:true);
            seed.Parameters.Add("@host",MySqlDbType.UInt64).Value=(ulong)trustedHost.Revision;
            if(await seed.ExecuteNonQueryAsync(ct)!=1)
                throw new InvalidOperationException("Private preauth policy not seeded.");
        }
        var secret=RandomNumberGenerator.GetBytes(32);
        using var protector=new SyntheticPreAuthDigestProtector(secret);
        CryptographicOperations.ZeroMemory(secret);
        var protectedLedger=new MySqlDisposablePreAuthRateLimitLedger(rateRunnerDsn);
        var measured=new MeasuredPasswordVerifier(login);
        ThrottledContextualPasswordAttempt NewAttempt(
            VerifiedTenantHostHint? host,IPAddress? peer,MeasuredPasswordVerifier verifier) =>
            new(new HostReader(host),new PeerReader(peer),protector,protectedLedger,verifier);
        var attempt=NewAttempt(trustedHost,IPAddress.Loopback,measured);
        var wrong=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var first=await attempt.AttemptAsync(
            canonical,correctPassword.AsMemory(),ct);
        var second=await attempt.AttemptAsync(canonical,wrong.AsMemory(),ct);
        var third=await attempt.AttemptAsync(
            canonical,correctPassword.AsMemory(),ct);
        var fourth=await attempt.AttemptAsync(
            canonical,correctPassword.AsMemory(),ct);
        pass(first==PreAuthPublicDisposition.Accepted &&
             second==PreAuthPublicDisposition.Accepted &&
             third==PreAuthPublicDisposition.Accepted &&
             fourth==PreAuthPublicDisposition.RateLimited &&
             measured.Calls==3,
            "Real MySQL HMAC preauth budget consumes three attempts BEFORE Argon2id and denies fourth");
        pass(first==second && measured.Calls==3,
            "Correct and incorrect passwords are indistinguishable public receipts");

        var before=measured.Calls;
        var withoutHost=await NewAttempt(null,IPAddress.Loopback,measured)
            .AttemptAsync(canonical,correctPassword.AsMemory(),ct);
        var withoutSocket=await NewAttempt(trustedHost,null,measured)
            .AttemptAsync(canonical,correctPassword.AsMemory(),ct);
        pass(withoutHost==PreAuthPublicDisposition.Unavailable &&
             withoutSocket==PreAuthPublicDisposition.Unavailable &&
             measured.Calls==before,
            "Missing verified host or actual network socket fails before password calculation");

        var unknown=CanonicalLoginHandle.Parse("Unknown+Burst@Example.test");
        var raced=await Task.WhenAll(Enumerable.Range(0,12).Select(_=>
            Task.Run(()=>attempt.AttemptAsync(
                unknown,correctPassword.AsMemory(),CancellationToken.None))));
        pass(raced.Count(x=>x==PreAuthPublicDisposition.Accepted)==3 &&
             raced.Count(x=>x==PreAuthPublicDisposition.RateLimited)==9 &&
             measured.Calls==before+3,
             "Twelve simultaneous unknown logins enter Argon2id exactly three times under MySQL CAS");
        var outstanding=await ScalarAsync(admin,
            "SELECT COUNT(*) FROM c05_10_access_traces");
        pass(outstanding>=6, "Protected SQL stores server-keyed access traces for admitted password attempts");
        var tracePrivacy=await ScalarAsync(admin, """
            SELECT COUNT(*) FROM information_schema.columns
             WHERE table_schema='c05_10_disposable'
               AND table_name IN ('c05_10_access_traces','c05_10_access_outcomes')
               AND column_name IN ('email','password','ip','raw_login','cookie','bearer','phc','token')
            """);
        pass(tracePrivacy==0, "Preauth audit schema has no raw account, password, token or IP columns");

        var deniedDirectRead=false;
        await using (var runner=new MySqlConnection(rateRunnerDsn))
        {
            await runner.OpenAsync(ct);
            await using var cmd=new MySqlCommand(
                "SELECT subject_digest FROM c05_10_access_traces",runner);
            try{_ = await cmd.ExecuteScalarAsync(ct);}
            catch(MySqlException ex) when(ex.Number is 1142 or 1143)
            {deniedDirectRead=true;}
        }
        pass(deniedDirectRead,
            "EXECUTE-only rate-limit role cannot SELECT protected access traces");

        await using (var disable=new MySqlCommand("""
            UPDATE c05_10_policies SET enabled=0,revision=revision+1
             WHERE purpose='LOGIN'
            """,admin))
            _=await disable.ExecuteNonQueryAsync(ct);
        var callsBeforeFailure=measured.Calls;
        var denied=await attempt.AttemptAsync(
            CanonicalLoginHandle.Parse("Another+Unknown@Example.test"),
            correctPassword.AsMemory(),ct);
        pass(denied==PreAuthPublicDisposition.Unavailable &&
             measured.Calls==callsBeforeFailure,
             "Disabled or unavailable SQL security policy always denies before Argon2id");
    }

    private static async Task<int> ScalarAsync(MySqlConnection db,string sql)
    {
        await using var cmd=new MySqlCommand(sql,db);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    private sealed class HostReader(VerifiedTenantHostHint? host) :
        ITrustedPreAuthTenantHostReader
    {
        public ValueTask<VerifiedTenantHostHint?> ReadServerVerifiedTenantAsync(
            CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return ValueTask.FromResult(host);
        }
    }

    private sealed class PeerReader(IPAddress? socketAddress) :
        ITrustedPreAuthNetworkReader
    {
        public ValueTask<IPAddress?> ReadServerSocketPeerAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return ValueTask.FromResult(socketAddress);
        }
    }

    private sealed class MeasuredPasswordVerifier(PrepareContextualPasswordLogin login) :
        ITrustedThrottledPasswordVerifier
    {
        private int calls;
        public int Calls => Volatile.Read(ref calls);
        public Task<ContextualLoginPreparation> VerifyAsync(
            CanonicalLoginHandle handle,ReadOnlyMemory<char> password,
            CancellationToken ct)
        {
            Interlocked.Increment(ref calls);
            return login.ExecuteAsync(handle,password,ct);
        }
    }
}
