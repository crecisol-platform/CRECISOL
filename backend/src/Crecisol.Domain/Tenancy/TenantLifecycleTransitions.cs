using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Crecisol.Domain.Tenancy;

/// <summary>Operations are internal selectors, never proof of administrative authority.</summary>
public enum TenantLifecycleAction
{
    Suspend = 1,
    Reactivate = 2,
    Deactivate = 3
}

/// <summary>
/// Read-only intent for a FUTURE authenticated backend administrative workflow.
/// No public HTTP endpoint, identity adapter or catalog writer exists in C04-08.
/// A reason or operation GUID is not authorization.
/// </summary>
public sealed record TrustedTenantLifecycleIntent
{
    private TrustedTenantLifecycleIntent(
        TenantId tenantId, TenantActorId actorId, Guid operationId,
        TenantLifecycleAction action, long expectedRevision, long expectedAccessEpoch,
        string reasonCode, DateTimeOffset requestedAtUtc, string fingerprint)
    {
        TenantId = tenantId;
        ActorId = actorId;
        OperationId = operationId;
        Action = action;
        ExpectedRevision = expectedRevision;
        ExpectedAccessEpoch = expectedAccessEpoch;
        ReasonCode = reasonCode;
        RequestedAtUtc = requestedAtUtc;
        IntentFingerprint = fingerprint;
    }

    public TenantId TenantId { get; }
    public TenantActorId ActorId { get; }
    public Guid OperationId { get; }
    public TenantLifecycleAction Action { get; }
    public long ExpectedRevision { get; }
    public long ExpectedAccessEpoch { get; }
    public string ReasonCode { get; }
    public DateTimeOffset RequestedAtUtc { get; }
    public string IntentFingerprint { get; }

    public static TrustedTenantLifecycleIntent FromTrustedBackoffice(
        TenantId tenantId, TenantActorId actorId, Guid operationId,
        TenantLifecycleAction action, long expectedRevision, long expectedAccessEpoch,
        string reasonCode, DateTimeOffset requestedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(tenantId);
        ArgumentNullException.ThrowIfNull(actorId);
        if (operationId == Guid.Empty || !Enum.IsDefined(action) ||
            expectedRevision <= 0 || expectedAccessEpoch <= 0 ||
            string.IsNullOrEmpty(reasonCode) || reasonCode.Length is < 3 or > 48 ||
            reasonCode[0] is < 'A' or > 'Z' ||
            !reasonCode.All(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_') ||
            requestedAtUtc == default || requestedAtUtc.Offset != TimeSpan.Zero)
            throw new InvalidOperationException("Invalid internal lifecycle intent.");

        var canonical = "crecisol.c04-08.v1|" +
            tenantId.Value.ToString("N") + "|" + actorId.Value.ToString("N") + "|" +
            operationId.ToString("N") + "|" +
            ((int)action).ToString(CultureInfo.InvariantCulture) + "|" +
            expectedRevision.ToString(CultureInfo.InvariantCulture) + "|" +
            expectedAccessEpoch.ToString(CultureInfo.InvariantCulture) + "|" +
            reasonCode + "|" + requestedAtUtc.UtcTicks.ToString(CultureInfo.InvariantCulture);
        var fingerprint = Convert.ToHexString(SHA256.HashData(
            Encoding.ASCII.GetBytes(canonical))).ToLowerInvariant();
        return new TrustedTenantLifecycleIntent(
            tenantId, actorId, operationId, action, expectedRevision,
            expectedAccessEpoch, reasonCode, requestedAtUtc, fingerprint);
    }

    public override string ToString() => "TrustedTenantLifecycleIntent[redacted]";
}

/// <summary>
/// Strict, side-effect-free policy. Deactivated is the EXISTING catalog's
/// logical-delete state. There is no Deleted enum and no physical delete.
/// Draft/PendingVerification activation is outside this microphase.
/// </summary>
public static class TenantLifecycleTransitionPolicy
{
    public static TenantLifecycleState NextState(
        TenantLifecycleState current, TenantLifecycleAction action)
    {
        if (!Enum.IsDefined(current) || !Enum.IsDefined(action))
            throw new UnauthorizedAccessException("Unknown lifecycle state/action.");

        return (current, action) switch
        {
            (TenantLifecycleState.Active, TenantLifecycleAction.Suspend)
                => TenantLifecycleState.Suspended,
            (TenantLifecycleState.Suspended, TenantLifecycleAction.Reactivate)
                => TenantLifecycleState.Active,
            (TenantLifecycleState.Active, TenantLifecycleAction.Deactivate)
                => TenantLifecycleState.Deactivated,
            (TenantLifecycleState.Suspended, TenantLifecycleAction.Deactivate)
                => TenantLifecycleState.Deactivated,
            _ => throw new UnauthorizedAccessException("Lifecycle transition denied.")
        };
    }

    /// <summary>
    /// A fresh active catalog is REQUIRED for further authorization checks,
    /// but this predicate NEVER grants permissions, sessions or resource access.
    /// </summary>
    public static bool IsEligibleForAdditionalAuthorization(
        TenantLifecycleState state) => state == TenantLifecycleState.Active;
}

/// <summary>
/// Receipt from a future CAS+audit+epoch atomic store. It is not a permission.
/// The store must commit state, epoch, immutable audit and operation ID together.
/// </summary>
public sealed record TrustedTenantLifecycleReceipt
{
    private TrustedTenantLifecycleReceipt(
        TenantId tenantId, Guid operationId, string fingerprint,
        TenantLifecycleState state, long revision, long accessEpoch, DateTimeOffset committedAtUtc)
    {
        TenantId = tenantId;
        OperationId = operationId;
        IntentFingerprint = fingerprint;
        State = state;
        Revision = revision;
        AccessEpoch = accessEpoch;
        CommittedAtUtc = committedAtUtc;
    }

    public TenantId TenantId { get; }
    public Guid OperationId { get; }
    public string IntentFingerprint { get; }
    public TenantLifecycleState State { get; }
    public long Revision { get; }
    public long AccessEpoch { get; }
    public DateTimeOffset CommittedAtUtc { get; }

    public static TrustedTenantLifecycleReceipt FromTrustedAtomicStore(
        TenantId tenantId, Guid operationId, string fingerprint,
        TenantLifecycleState state, long revision, long accessEpoch, DateTimeOffset committedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(tenantId);
        if (operationId == Guid.Empty || !Enum.IsDefined(state) ||
            revision <= 0 || accessEpoch <= 0 ||
            fingerprint is null || fingerprint.Length != 64 ||
            !fingerprint.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f') ||
            committedAtUtc == default || committedAtUtc.Offset != TimeSpan.Zero)
            throw new InvalidOperationException("Invalid lifecycle store receipt.");
        return new TrustedTenantLifecycleReceipt(
            tenantId, operationId, fingerprint, state, revision, accessEpoch, committedAtUtc);
    }

    public override string ToString() => "TrustedTenantLifecycleReceipt[redacted]";
}
