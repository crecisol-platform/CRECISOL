using System.Security.Cryptography;
using Crecisol.Domain.Tenancy;

namespace Crecisol.Application.Identity;

/// <summary>C05-08 synthetic-only MFA protocol. No sessions, authorization,
/// real WebAuthn enrollment, tenant routing or public endpoints.</summary>
public enum DisposableMfaKind { Totp = 1, Passkey = 2 }
public enum DisposableMfaProofPurpose { Enroll = 1, StepUp = 2 }

public sealed class DisposableMfaChallenge
{
    internal DisposableMfaChallenge(Guid factor, Guid challenge, Guid operation,
        string raw, string? backupCode = null)
    {
        FactorId = factor;
        ChallengeId = challenge;
        OperationId = operation;
        TokenForTrustedFixtureOnly = raw;
        BackupCodeForTrustedFixtureOnly = backupCode;
    }
    public Guid FactorId { get; }
    public Guid ChallengeId { get; }
    public Guid OperationId { get; }
    // ONLY the isolated test harness may deliver these ephemeral synthetic secrets.
    public string TokenForTrustedFixtureOnly { get; }
    public string? BackupCodeForTrustedFixtureOnly { get; }
    public override string ToString() => "DisposableMfaChallenge[redacted]";
}

public sealed record DisposableMfaAssertion(
    byte[] CredentialId, byte[] ClientDataJson,
    byte[] AuthenticatorData, byte[] Signature)
{
    public override string ToString() => "DisposableMfaAssertion[redacted]";
}

public sealed record DisposableMfaCounter(DisposableMfaKind Kind,
    string State, long LastCommittedCounter);

public interface ITrustedDisposableMfaSecrets
{
    void RegisterPendingFixture(Guid factorId, TenantId tenant,
        TenantActorId actor, DisposableMfaKind kind);
    void DiscardPendingFixture(Guid factorId);
    bool TryVerifyTotp(Guid factorId, TenantId tenant, TenantActorId actor,
        string? candidate, long lastCounter, out long newCounter);
    bool TryVerifyFixturePasskey(Guid factorId, TenantId tenant, TenantActorId actor,
        ReadOnlySpan<byte> challenge, DisposableMfaAssertion assertion,
        long lastCounter, out long newCounter);
}

public interface ITrustedDisposableMfaChallengeProtector
{
    string GenerateForFixtureOnly();
    byte[]? TryDigest(string? token);
    byte[]? TryChallengeBytes(string? token);
}

public interface ITrustedDisposableMfaBackupProtector
{
    string GenerateForFixtureOnly();
    byte[]? TryDigest(string? code);
}

public interface ITrustedDisposableMfaLedger
{
    Task<bool> TryOpenEnrollmentAsync(PreparedContextualLoginEvidence proof,
        Guid factor, DisposableMfaKind kind, Guid challenge,
        byte[] challengeDigest, byte[] backupDigest, CancellationToken ct);
    Task<bool> TryOpenStepUpAsync(PreparedContextualLoginEvidence proof,
        Guid factor, Guid challenge, byte[] challengeDigest,
        Guid operation, CancellationToken ct);
    Task<bool> TryOpenReplacementAsync(PreparedContextualLoginEvidence proof,
        Guid oldFactor, byte[] backupDigest, Guid newFactor,
        DisposableMfaKind kind, Guid challenge, byte[] challengeDigest,
        CancellationToken ct);
    Task<DisposableMfaCounter?> ReadCounterAsync(TenantId tenant,
        TenantActorId actor, Guid factor, CancellationToken ct);
    Task<bool> TryCommitCryptographicProofAsync(TenantId tenant,
        TenantActorId actor, Guid factor, Guid challenge, byte[] challengeDigest,
        Guid operation, DisposableMfaProofPurpose purpose,
        long authenticatedCounter, CancellationToken ct);
}

/// <summary>
/// This service cannot accept a password-verification bool or a caller-supplied
/// prepared proof: it reauthenticates using the existing C05-04 Argon2id
/// interface INSIDE enrollment, replacement and step-up issuance. Factor
/// verification happens before the only SQL commit path. A backup code
/// can never authorize this commit or disable the old factor.
/// </summary>
public sealed class DisposableMfaRiskUseCases
{
    private readonly ITrustedImmediatePasswordReauthentication password;
    private readonly ITrustedDisposableMfaLedger ledger;
    private readonly ITrustedDisposableMfaSecrets factors;
    private readonly ITrustedDisposableMfaChallengeProtector challenges;
    private readonly ITrustedDisposableMfaBackupProtector backups;

    public DisposableMfaRiskUseCases(
        ITrustedImmediatePasswordReauthentication password,
        ITrustedDisposableMfaLedger ledger, ITrustedDisposableMfaSecrets factors,
        ITrustedDisposableMfaChallengeProtector challenges,
        ITrustedDisposableMfaBackupProtector backups)
    {
        this.password = password ?? throw new ArgumentNullException(nameof(password));
        this.ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        this.factors = factors ?? throw new ArgumentNullException(nameof(factors));
        this.challenges = challenges ?? throw new ArgumentNullException(nameof(challenges));
        this.backups = backups ?? throw new ArgumentNullException(nameof(backups));
    }

    private async Task<PreparedContextualLoginEvidence?> FreshAsync(
        CanonicalLoginHandle mailbox, ReadOnlyMemory<char> currentPassword,
        VerifiedTenantHostHint host, CancellationToken ct)
    {
        var result = await password.VerifyFreshAsync(mailbox,currentPassword,host,ct);
        return result.Status == ContextualLoginPreparationStatus.Ready &&
               result.Evidence is { } evidence &&
               evidence.TenantId == host.TenantId &&
               evidence.HostRevision == host.Revision &&
               evidence.HostRevision > 0
            ? evidence : null;
    }

    public async Task<DisposableMfaChallenge?> BeginEnrollmentAsync(
        CanonicalLoginHandle mailbox, ReadOnlyMemory<char> currentPassword,
        VerifiedTenantHostHint host, DisposableMfaKind kind, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(mailbox);
        ArgumentNullException.ThrowIfNull(host);
        if (!Enum.IsDefined(kind)) return null;
        var verified = await FreshAsync(mailbox,currentPassword,host,ct);
        if (verified is null) return null;
        var factor=Guid.NewGuid();
        var challenge=Guid.NewGuid();
        var token=challenges.GenerateForFixtureOnly();
        var backup=backups.GenerateForFixtureOnly();
        byte[]? digest=challenges.TryDigest(token);
        byte[]? backupDigest=backups.TryDigest(backup);
        if (digest is null || backupDigest is null)
            throw new InvalidOperationException("Server-only synthetic MFA crypto failed.");
        try
        {
            factors.RegisterPendingFixture(factor,verified.TenantId,verified.ActorId,kind);
            if (!await ledger.TryOpenEnrollmentAsync(
                    verified,factor,kind,challenge,digest,backupDigest,ct))
            {
                factors.DiscardPendingFixture(factor);
                return null;
            }
            return new DisposableMfaChallenge(factor,challenge,challenge,token,backup);
        }
        catch
        {
            factors.DiscardPendingFixture(factor);
            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(digest);
            CryptographicOperations.ZeroMemory(backupDigest);
        }
    }

    public async Task<DisposableMfaChallenge?> BeginStepUpAsync(
        CanonicalLoginHandle mailbox, ReadOnlyMemory<char> currentPassword,
        VerifiedTenantHostHint host, Guid factor, Guid operation, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(mailbox);
        ArgumentNullException.ThrowIfNull(host);
        if (factor==Guid.Empty || operation==Guid.Empty) return null;
        var verified = await FreshAsync(mailbox,currentPassword,host,ct);
        if (verified is null) return null;
        var challenge=Guid.NewGuid();
        var token=challenges.GenerateForFixtureOnly();
        byte[]? digest=challenges.TryDigest(token);
        if (digest is null) return null;
        try
        {
            return await ledger.TryOpenStepUpAsync(verified,factor,challenge,
                digest,operation,ct)
                    ? new DisposableMfaChallenge(factor,challenge,operation,token)
                    : null;
        }
        finally { CryptographicOperations.ZeroMemory(digest); }
    }

    public async Task<DisposableMfaChallenge?> BeginRecoveryReplacementAsync(
        CanonicalLoginHandle mailbox, ReadOnlyMemory<char> currentPassword,
        VerifiedTenantHostHint host, Guid activeFactor, string? backupCode,
        DisposableMfaKind nextKind, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(mailbox);
        ArgumentNullException.ThrowIfNull(host);
        if (activeFactor==Guid.Empty || !Enum.IsDefined(nextKind)) return null;
        var verified = await FreshAsync(mailbox,currentPassword,host,ct);
        if (verified is null) return null;
        byte[]? backup=backups.TryDigest(backupCode);
        if (backup is null) return null;
        var factor=Guid.NewGuid();
        var challenge=Guid.NewGuid();
        var token=challenges.GenerateForFixtureOnly();
        byte[]? digest=challenges.TryDigest(token);
        if (digest is null) return null;
        try
        {
            factors.RegisterPendingFixture(factor,verified.TenantId,verified.ActorId,nextKind);
            if (!await ledger.TryOpenReplacementAsync(verified,activeFactor,
                    backup,factor,nextKind,challenge,digest,ct))
            {
                factors.DiscardPendingFixture(factor);
                return null;
            }
            return new DisposableMfaChallenge(factor,challenge,challenge,token);
        }
        catch
        {
            factors.DiscardPendingFixture(factor);
            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(backup);
            CryptographicOperations.ZeroMemory(digest);
        }
    }

    public async Task<bool> ProveTotpAsync(VerifiedTenantHostHint host,
        TenantActorId backendVerifiedActor, DisposableMfaChallenge operation,
        DisposableMfaProofPurpose purpose, string? code, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(backendVerifiedActor);
        ArgumentNullException.ThrowIfNull(operation);
        var state = await ledger.ReadCounterAsync(host.TenantId,
            backendVerifiedActor,operation.FactorId,ct);
        if (state is null || state.Kind!=DisposableMfaKind.Totp ||
            state.State!=(purpose==DisposableMfaProofPurpose.Enroll ? "PENDING" : "ACTIVE") ||
            !factors.TryVerifyTotp(operation.FactorId,host.TenantId,
                backendVerifiedActor,code,state.LastCommittedCounter,out long counter))
            return false;
        byte[]? digest=challenges.TryDigest(operation.TokenForTrustedFixtureOnly);
        if (digest is null) return false;
        try
        {
            return await ledger.TryCommitCryptographicProofAsync(
                host.TenantId,backendVerifiedActor,operation.FactorId,
                operation.ChallengeId,digest,operation.OperationId,
                purpose,counter,ct);
        }
        finally { CryptographicOperations.ZeroMemory(digest); }
    }

    public async Task<bool> ProveFixturePasskeyAsync(VerifiedTenantHostHint host,
        TenantActorId backendVerifiedActor, DisposableMfaChallenge operation,
        DisposableMfaProofPurpose purpose,
        DisposableMfaAssertion assertion, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(backendVerifiedActor);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(assertion);
        var state = await ledger.ReadCounterAsync(host.TenantId,
            backendVerifiedActor,operation.FactorId,ct);
        if (state is null || state.Kind!=DisposableMfaKind.Passkey ||
            state.State!=(purpose==DisposableMfaProofPurpose.Enroll ? "PENDING" : "ACTIVE"))
            return false;
        byte[]? challengeBytes=challenges.TryChallengeBytes(
            operation.TokenForTrustedFixtureOnly);
        byte[]? digest=challenges.TryDigest(operation.TokenForTrustedFixtureOnly);
        if (challengeBytes is null || digest is null) return false;
        try
        {
            if (!factors.TryVerifyFixturePasskey(operation.FactorId,
                    host.TenantId,backendVerifiedActor,challengeBytes,assertion,
                    state.LastCommittedCounter,out long counter))
                return false;
            return await ledger.TryCommitCryptographicProofAsync(
                host.TenantId,backendVerifiedActor,operation.FactorId,
                operation.ChallengeId,digest,operation.OperationId,
                purpose,counter,ct);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(challengeBytes);
            CryptographicOperations.ZeroMemory(digest);
        }
    }

    public override string ToString() => "DisposableMfaRiskUseCases[redacted]";
}
