using System.Net;
using Crecisol.Application.Tenancy;
using Crecisol.Domain.Identity;
using Crecisol.Domain.Tenancy;

namespace Crecisol.Application.Identity;

/// <summary>
/// Only an authorized backend transport adapter may resolve the direct host
/// through C04-04. Neither query nor login request chooses tenant or realm.
/// </summary>
public interface ITrustedPreAuthTenantHostReader
{
    ValueTask<VerifiedTenantHostHint?> ReadServerVerifiedTenantAsync(
        CancellationToken cancellationToken);
}

/// <summary>
/// Returns actual server socket peer, NEVER Forwarded, X-Forwarded-For,
/// claims, cookies or body. A trusted proxy requires separate review.
/// </summary>
public interface ITrustedPreAuthNetworkReader
{
    ValueTask<IPAddress?> ReadServerSocketPeerAsync(
        CancellationToken cancellationToken);
}

public interface ITrustedPreAuthDigestProtector
{
    PreAuthKeyedDigest FingerprintSubject(TenantId tenant,
        PreAuthRateLimitPurpose purpose,CanonicalLoginHandle login);
    PreAuthKeyedDigest FingerprintSocketPeer(TenantId tenant,
        PreAuthRateLimitPurpose purpose,IPAddress actualRemoteIp);
}

/// <summary>
/// The same real C05-04 Argon2id + dummy PHC preparation as existing login.
/// Returned evidence NEVER becomes a session in this C05-10 test scope.
/// </summary>
public interface ITrustedThrottledPasswordVerifier
{
    Task<ContextualLoginPreparation> VerifyAsync(
        CanonicalLoginHandle login,ReadOnlyMemory<char> password,
        CancellationToken cancellationToken);
}

/// <summary>
/// SQL must enforce policy + BOTH source/subject CAS in one transaction,
/// and append a trace without any raw login, IP, password, or token.
/// </summary>
public interface ITrustedPreAuthRateLimitLedger
{
    Task<PreAuthAdmissionStatus> AdmitAsync(
        VerifiedTenantHostHint verifiedHost,PreAuthRateLimitPurpose purpose,
        Guid serverAttemptId,PreAuthKeyedDigest source,PreAuthKeyedDigest subject,
        CancellationToken cancellationToken);

    Task<bool> CompleteAsync(
        VerifiedTenantHostHint verifiedHost,PreAuthRateLimitPurpose purpose,
        Guid serverAttemptId,PreAuthKeyedDigest source,PreAuthKeyedDigest subject,
        PreAuthPrivateOutcome privateOutcome,CancellationToken cancellationToken);
}

/// <summary>
/// PRE-VERIFICATION atomic admission. Avoids the unsafe "check rate limit,
/// hash password, then increment" concurrency gap. All visible allowed
/// responses are neutral: no email enumeration, session, JWT or grant.
/// Fail closed on absent host/source/SQL/policy or audit write failure.
/// </summary>
public sealed class ThrottledContextualPasswordAttempt
{
    private readonly ITrustedPreAuthTenantHostReader hosts;
    private readonly ITrustedPreAuthNetworkReader network;
    private readonly ITrustedPreAuthDigestProtector protector;
    private readonly ITrustedPreAuthRateLimitLedger ledger;
    private readonly ITrustedThrottledPasswordVerifier passwordVerifier;

    public ThrottledContextualPasswordAttempt(
        ITrustedPreAuthTenantHostReader hosts,
        ITrustedPreAuthNetworkReader network,
        ITrustedPreAuthDigestProtector protector,
        ITrustedPreAuthRateLimitLedger ledger,
        ITrustedThrottledPasswordVerifier passwordVerifier)
    {
        this.hosts=hosts??throw new ArgumentNullException(nameof(hosts));
        this.network=network??throw new ArgumentNullException(nameof(network));
        this.protector=protector??throw new ArgumentNullException(nameof(protector));
        this.ledger=ledger??throw new ArgumentNullException(nameof(ledger));
        this.passwordVerifier=passwordVerifier??
            throw new ArgumentNullException(nameof(passwordVerifier));
    }

    public async Task<PreAuthPublicDisposition> AttemptAsync(
        CanonicalLoginHandle login,ReadOnlyMemory<char> candidatePassword,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(login);
        ct.ThrowIfCancellationRequested();
        VerifiedTenantHostHint? host;
        IPAddress? peer;
        try
        {
            host=await hosts.ReadServerVerifiedTenantAsync(ct);
            ct.ThrowIfCancellationRequested();
            peer=await network.ReadServerSocketPeerAsync(ct);
            ct.ThrowIfCancellationRequested();
        }
        catch(Exception e) when(e is not OperationCanceledException)
        {
            return PreAuthPublicDisposition.Unavailable;
        }

        if(host is null || host.Revision<1 || peer is null)
            return PreAuthPublicDisposition.Unavailable;

        PreAuthKeyedDigest source;
        PreAuthKeyedDigest subject;
        try
        {
            source=protector.FingerprintSocketPeer(host.TenantId,
                PreAuthRateLimitPurpose.PasswordLogin,peer);
            subject=protector.FingerprintSubject(host.TenantId,
                PreAuthRateLimitPurpose.PasswordLogin,login);
        }
        catch(Exception e) when(e is not OperationCanceledException)
        {
            return PreAuthPublicDisposition.Unavailable;
        }

        var id=Guid.NewGuid();
        PreAuthAdmissionStatus admitted;
        try
        {
            admitted=await ledger.AdmitAsync(host,PreAuthRateLimitPurpose.PasswordLogin,
                id,source,subject,ct);
            ct.ThrowIfCancellationRequested();
        }
        catch(Exception e) when(e is not OperationCanceledException)
        {
            // No "allow if Redis/MySQL unavailable" fallback.
            return PreAuthPublicDisposition.Unavailable;
        }

        if(admitted==PreAuthAdmissionStatus.Limited)
            return PreAuthPublicDisposition.RateLimited;
        if(admitted!=PreAuthAdmissionStatus.Admitted)
            return PreAuthPublicDisposition.Unavailable;

        bool verified=false;
        try
        {
            var prepared=await passwordVerifier.VerifyAsync(login,candidatePassword,ct);
            ct.ThrowIfCancellationRequested();
            var proof=prepared?.Evidence;
            verified=prepared?.Status==ContextualLoginPreparationStatus.Ready &&
                proof is not null && proof.TenantId==host.TenantId &&
                proof.HostRevision==host.Revision &&
                proof.AccountRevision>0 && proof.CredentialRevision>0 &&
                proof.MembershipRevision>0 && proof.TenantCatalogRevision>0;
        }
        catch(Exception e) when(e is not OperationCanceledException)
        {
            // The SQL budget has already been CONSUMED. A verifier crash never
            // mints a session or resets the counter and is not publicly exposed.
            return PreAuthPublicDisposition.Unavailable;
        }

        try
        {
            bool recorded=await ledger.CompleteAsync(
                host,PreAuthRateLimitPurpose.PasswordLogin,id,source,subject,
                verified?PreAuthPrivateOutcome.Verified:PreAuthPrivateOutcome.Rejected,ct);
            return recorded?PreAuthPublicDisposition.Accepted:
                PreAuthPublicDisposition.Unavailable;
        }
        catch(Exception e) when(e is not OperationCanceledException)
        {
            // Authentication evidence is not released if trace cannot commit.
            return PreAuthPublicDisposition.Unavailable;
        }
    }

    public override string ToString() => "ThrottledContextualPasswordAttempt[redacted]";
}
