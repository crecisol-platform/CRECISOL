using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Crecisol.Domain.Tenancy;

/// <summary>
/// Separation of private, tenant-scoped artifact purposes. Never infer this
/// from a browser-controlled URL, host, job JSON, object key or cache key.
/// </summary>
public enum TenantArtifactKind
{
    Cache = 1,
    Job = 2,
    Media = 3,
    Export = 4
}

/// <summary>Backend-owned namespace selector, NOT a client parameter.</summary>
public enum TenantArtifactEnvironment
{
    Development = 1,
    ContinuousIntegration = 2,
    Staging = 3,
    Production = 4
}

/// <summary>
/// C04-10 internal, immutable intent snapshot from a FUTURE authorization
/// service. This DTO is NOT authorization, and no productive consumer or
/// queue/cache/media adapter exists in C04-10. A real adapter must revalidate
/// tenant, actor/session/membership, branch/resource ownership and epochs
/// atomically at each enqueue/dequeue/commit/download step.
/// </summary>
public sealed record TrustedTenantArtifactScope
{
    private TrustedTenantArtifactScope(
        Guid operationId, TenantArtifactKind kind, TenantArtifactEnvironment environment,
        TenantId tenantId, TenantBranchId branchId, TenantResourceId resourceId,
        TenantActorId actorId, TenantRequiredAction requiredAction,
        TenantPlacementMode placementMode, DedicatedStorageSlotId? dedicatedSlot,
        long tenantAccessEpoch, long sessionRevision, long membershipRevision,
        long branchRevision, long resourceRevision, long placementRevision,
        DateTimeOffset verifiedAtUtc, DateTimeOffset expiresAtUtc,
        string serverVariant, string isolationFingerprint, string intentFingerprint)
    {
        OperationId = operationId;
        Kind = kind;
        Environment = environment;
        TenantId = tenantId;
        BranchId = branchId;
        ResourceId = resourceId;
        ActorId = actorId;
        RequiredAction = requiredAction;
        PlacementMode = placementMode;
        DedicatedSlot = dedicatedSlot;
        TenantAccessEpoch = tenantAccessEpoch;
        SessionRevision = sessionRevision;
        MembershipRevision = membershipRevision;
        BranchRevision = branchRevision;
        ResourceRevision = resourceRevision;
        PlacementRevision = placementRevision;
        VerifiedAtUtc = verifiedAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        ServerVariant = serverVariant;
        IsolationFingerprint = isolationFingerprint;
        IntentFingerprint = intentFingerprint;
    }

    public Guid OperationId { get; }
    public TenantArtifactKind Kind { get; }
    public TenantArtifactEnvironment Environment { get; }
    public TenantId TenantId { get; }
    public TenantBranchId BranchId { get; }
    public TenantResourceId ResourceId { get; }
    public TenantActorId ActorId { get; }
    public TenantRequiredAction RequiredAction { get; }
    public TenantPlacementMode PlacementMode { get; }
    public DedicatedStorageSlotId? DedicatedSlot { get; }
    public long TenantAccessEpoch { get; }
    public long SessionRevision { get; }
    public long MembershipRevision { get; }
    public long BranchRevision { get; }
    public long ResourceRevision { get; }
    public long PlacementRevision { get; }
    public DateTimeOffset VerifiedAtUtc { get; }
    public DateTimeOffset ExpiresAtUtc { get; }
    /// <summary>
    /// Canonical variant/format/filter policy built by backend from approved
    /// server-side inputs; no URL, raw query, path or client-provided key.
    /// Every different response representation must use a different variant.
    /// </summary>
    public string ServerVariant { get; }
    // SHA-256 is for binding/invalidation, NOT a secret or access capability.
    public string IsolationFingerprint { get; }
    public string IntentFingerprint { get; }

    /// <summary>
    /// ONLY a trusted future backend policy/composite catalog reader should
    /// restore this. Public use of this factory NEVER proves a valid grant.
    /// Active states are necessary but NOT sufficient to authorize data.
    /// </summary>
    public static TrustedTenantArtifactScope FromVerifiedBackendDecision(
        Guid operationId, TenantArtifactKind kind, TenantArtifactEnvironment environment,
        TenantId tenantId, TenantBranchId branchId, TenantResourceId resourceId,
        TenantActorId actorId, TenantRequiredAction requiredAction,
        TenantPlacementMode placementMode, DedicatedStorageSlotId? dedicatedSlot,
        TenantLifecycleState tenantState, ActorSessionState sessionState,
        TenantMembershipState membershipState, TrustedBranchState branchState,
        TrustedResourceState resourceState,
        long tenantAccessEpoch, long sessionRevision, long membershipRevision,
        long branchRevision, long resourceRevision, long placementRevision,
        DateTimeOffset verifiedAtUtc, DateTimeOffset expiresAtUtc,
        string serverVariant)
    {
        ArgumentNullException.ThrowIfNull(tenantId);
        ArgumentNullException.ThrowIfNull(branchId);
        ArgumentNullException.ThrowIfNull(resourceId);
        ArgumentNullException.ThrowIfNull(actorId);
        ArgumentNullException.ThrowIfNull(requiredAction);

        if (operationId == Guid.Empty || !Enum.IsDefined(kind) ||
            !Enum.IsDefined(environment) ||
            tenantState != TenantLifecycleState.Active ||
            sessionState != ActorSessionState.Active ||
            membershipState != TenantMembershipState.Active ||
            branchState != TrustedBranchState.Active ||
            resourceState != TrustedResourceState.Active ||
            !Enum.IsDefined(placementMode) ||
            placementMode == TenantPlacementMode.Shared && dedicatedSlot is not null ||
            placementMode == TenantPlacementMode.Dedicated && dedicatedSlot is null ||
            tenantAccessEpoch is <= 0 or >= long.MaxValue ||
            sessionRevision is <= 0 or >= long.MaxValue ||
            membershipRevision is <= 0 or >= long.MaxValue ||
            branchRevision is <= 0 or >= long.MaxValue ||
            resourceRevision is <= 0 or >= long.MaxValue ||
            placementRevision is <= 0 or >= long.MaxValue ||
            verifiedAtUtc == default || verifiedAtUtc.Offset != TimeSpan.Zero ||
            expiresAtUtc == default || expiresAtUtc.Offset != TimeSpan.Zero ||
            expiresAtUtc <= verifiedAtUtc ||
            expiresAtUtc - verifiedAtUtc > TimeSpan.FromMinutes(15) ||
            string.IsNullOrEmpty(serverVariant) ||
            serverVariant.Length is < 3 or > 48 ||
            serverVariant[0] is < 'a' or > 'z' ||
            serverVariant.Contains("..", StringComparison.Ordinal) ||
            !serverVariant.All(c => c is >= 'a' and <= 'z' or
                >= '0' and <= '9' or '.' or '_' or '-'))
            throw new UnauthorizedAccessException("Invalid or inactive private artifact scope.");

        var expectedAction = kind switch
        {
            TenantArtifactKind.Cache => "tenant.cache.read",
            TenantArtifactKind.Job => "tenant.job.execute",
            TenantArtifactKind.Media => "tenant.media.read",
            TenantArtifactKind.Export => "tenant.export.create",
            _ => ""
        };
        if (!string.Equals(requiredAction.Value, expectedAction, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Artifact purpose and server action disagree.");

        static string N(long n) => n.ToString(CultureInfo.InvariantCulture);
        var isolationCanonical = string.Join("|",
            "crecisol.c04-10.private-isolation.v1",
            N((int)environment), N((int)kind),
            tenantId.Value.ToString("N"), branchId.Value.ToString("N"),
            resourceId.Value.ToString("N"), actorId.Value.ToString("N"),
            requiredAction.Value, serverVariant, N((int)placementMode),
            dedicatedSlot?.Value.ToString("N") ?? "shared",
            N(tenantAccessEpoch), N(sessionRevision), N(membershipRevision),
            N(branchRevision), N(resourceRevision), N(placementRevision));

        var isolationFingerprint = Convert.ToHexString(SHA256.HashData(
            Encoding.ASCII.GetBytes(isolationCanonical))).ToLowerInvariant();
        var intentCanonical = string.Join("|", "crecisol.c04-10.intent.v1",
            isolationFingerprint, operationId.ToString("N"),
            N(verifiedAtUtc.UtcTicks), N(expiresAtUtc.UtcTicks));
        var intentFingerprint = Convert.ToHexString(SHA256.HashData(
            Encoding.ASCII.GetBytes(intentCanonical))).ToLowerInvariant();

        return new TrustedTenantArtifactScope(
            operationId, kind, environment, tenantId, branchId, resourceId,
            actorId, requiredAction, placementMode, dedicatedSlot,
            tenantAccessEpoch, sessionRevision, membershipRevision,
            branchRevision, resourceRevision, placementRevision,
            verifiedAtUtc, expiresAtUtc, serverVariant,
            isolationFingerprint, intentFingerprint);
    }

    public bool IsFreshAtUtc(DateTimeOffset nowUtc) =>
        nowUtc != default && nowUtc.Offset == TimeSpan.Zero &&
        nowUtc >= VerifiedAtUtc && nowUtc < ExpiresAtUtc;

    public override string ToString() => "TrustedTenantArtifactScope[redacted]";
}

/// <summary>
/// The HMAC-derived namespace is opaque and names an artifact; possessing a
/// key grants NO authorization. Secret is provided only by the future
/// backend-controlled key provider; never include it in repository, logs, jobs
/// or clients. This pure helper cannot fetch or write a private artifact.
/// </summary>
public static class TenantPrivateArtifactKey
{
    public static string Derive(
        TrustedTenantArtifactScope scope, ReadOnlySpan<byte> protectedKeyMaterial)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (protectedKeyMaterial.Length < 32)
            throw new UnauthorizedAccessException("Strong protected key material required.");

        // Cache reuse across two separately authorized equivalent requests is
        // desirable; other effects must bind to their individual operation ID.
        var binding = scope.Kind == TenantArtifactKind.Cache
            ? scope.IsolationFingerprint : scope.IntentFingerprint;
        var purpose = ScopeTag(scope.Kind);
        var canonical = Encoding.ASCII.GetBytes(
            "crecisol.c04-10.key.v1|" +
            ((int)scope.Environment).ToString(CultureInfo.InvariantCulture) + "|" +
            purpose + "|" + binding);
        var digest = HMACSHA256.HashData(protectedKeyMaterial, canonical);
        return "c10:v1:" + purpose + ":" +
            Convert.ToHexString(digest).ToLowerInvariant();
    }

    public static bool IsCanonicalForKind(string? key, TenantArtifactKind kind)
    {
        if (!Enum.IsDefined(kind) || key is null)
            return false;
        var prefix = "c10:v1:" + ScopeTag(kind) + ":";
        return key.Length == prefix.Length + 64 &&
            key.StartsWith(prefix, StringComparison.Ordinal) &&
            key.AsSpan(prefix.Length).ToArray().All(c =>
                c is >= '0' and <= '9' or >= 'a' and <= 'f');
    }

    private static string ScopeTag(TenantArtifactKind kind) => kind switch
    {
        TenantArtifactKind.Cache => "cache",
        TenantArtifactKind.Job => "job",
        TenantArtifactKind.Media => "media",
        TenantArtifactKind.Export => "export",
        _ => throw new UnauthorizedAccessException("Unknown artifact kind.")
    };
}

public enum TenantArtifactReceiptState
{
    Applied = 1,
    Replayed = 2
}

/// <summary>
/// Synthetic future adapter's exact-scope/fence receipt. A verified receipt
/// is not an authentication token; physical effects still require protected
/// access and transaction/lease fencing in the FUTURE adapter.
/// </summary>
public sealed record TrustedTenantArtifactReceipt
{
    private TrustedTenantArtifactReceipt(Guid operationId, TenantArtifactKind kind,
        string intentFingerprint, string opaqueKey, long tenantAccessEpoch,
        long fenceGeneration, TenantArtifactReceiptState state,
        DateTimeOffset confirmedAtUtc)
    {
        OperationId = operationId;
        Kind = kind;
        IntentFingerprint = intentFingerprint;
        OpaqueKey = opaqueKey;
        TenantAccessEpoch = tenantAccessEpoch;
        FenceGeneration = fenceGeneration;
        State = state;
        ConfirmedAtUtc = confirmedAtUtc;
    }

    public Guid OperationId { get; }
    public TenantArtifactKind Kind { get; }
    public string IntentFingerprint { get; }
    public string OpaqueKey { get; }
    public long TenantAccessEpoch { get; }
    public long FenceGeneration { get; }
    public TenantArtifactReceiptState State { get; }
    public DateTimeOffset ConfirmedAtUtc { get; }

    public static TrustedTenantArtifactReceipt FromVerifiedProtectedGateway(
        Guid operationId, TenantArtifactKind kind, string intentFingerprint,
        string opaqueKey, long tenantAccessEpoch, long fenceGeneration,
        TenantArtifactReceiptState state, DateTimeOffset confirmedAtUtc)
    {
        if (operationId == Guid.Empty || !Enum.IsDefined(kind) ||
            !TenantPrivateArtifactKey.IsCanonicalForKind(opaqueKey, kind) ||
            intentFingerprint is null || intentFingerprint.Length != 64 ||
            !intentFingerprint.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f') ||
            tenantAccessEpoch <= 0 || fenceGeneration <= 0 ||
            !Enum.IsDefined(state) ||
            confirmedAtUtc == default || confirmedAtUtc.Offset != TimeSpan.Zero)
            throw new InvalidOperationException("Malformed private artifact receipt.");

        return new TrustedTenantArtifactReceipt(
            operationId, kind, intentFingerprint, opaqueKey, tenantAccessEpoch,
            fenceGeneration, state, confirmedAtUtc);
    }

    public static void RequireExactMatch(TrustedTenantArtifactScope scope,
        string derivedKey, TrustedTenantArtifactReceipt receipt,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(receipt);
        if (!scope.IsFreshAtUtc(nowUtc) ||
            !TenantPrivateArtifactKey.IsCanonicalForKind(derivedKey, scope.Kind) ||
            receipt.OperationId != scope.OperationId || receipt.Kind != scope.Kind ||
            receipt.TenantAccessEpoch != scope.TenantAccessEpoch ||
            !string.Equals(receipt.IntentFingerprint, scope.IntentFingerprint,
                StringComparison.Ordinal) ||
            receipt.ConfirmedAtUtc < scope.VerifiedAtUtc ||
            receipt.ConfirmedAtUtc > nowUtc ||
            !CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(receipt.OpaqueKey),
                Encoding.ASCII.GetBytes(derivedKey)))
            throw new UnauthorizedAccessException("Artifact receipt scope mismatch.");
    }

    public override string ToString() => "TrustedTenantArtifactReceipt[redacted]";
}
