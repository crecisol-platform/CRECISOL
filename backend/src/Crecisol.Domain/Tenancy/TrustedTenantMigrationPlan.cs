using System.Security.Cryptography;
using System.Text;

namespace Crecisol.Domain.Tenancy;

/// <summary>
/// DEDICATED migration target comes ONLY from a protected backend catalog. A tenant or
/// slot GUID is never authorization; this model does not expose any DSN.
/// </summary>
public sealed record TrustedTenantMigrationPlan
{
    private TrustedTenantMigrationPlan(Guid operationId, TenantId tenantId,
        TenantPlacementMode mode, DedicatedStorageSlotId? slot, long placementRevision,
        string migrationVersion, string checksum, DateTimeOffset approvedUntilUtc,
        string intentFingerprint)
    {
        OperationId = operationId;
        TenantId = tenantId;
        Mode = mode;
        Slot = slot;
        PlacementRevision = placementRevision;
        MigrationVersion = migrationVersion;
        Checksum = checksum;
        ApprovedUntilUtc = approvedUntilUtc;
        IntentFingerprint = intentFingerprint;
    }

    public Guid OperationId { get; }
    public TenantId TenantId { get; }
    public TenantPlacementMode Mode { get; }
    public DedicatedStorageSlotId? Slot { get; }
    public long PlacementRevision { get; }
    public string MigrationVersion { get; }
    public string Checksum { get; }
    public DateTimeOffset ApprovedUntilUtc { get; }
    public string IntentFingerprint { get; }

    public static TrustedTenantMigrationPlan FromTrustedCatalog(
        Guid operationId, TenantStoragePlacementSnapshot placement,
        string migrationVersion, string checksum, DateTimeOffset approvedUntilUtc)
    {
        ArgumentNullException.ThrowIfNull(placement);
        if (operationId == Guid.Empty || placement.Revision <= 0 ||
            placement.Mode != TenantPlacementMode.Dedicated || placement.DedicatedSlot is null ||
            approvedUntilUtc == default || approvedUntilUtc.Offset != TimeSpan.Zero ||
            string.IsNullOrEmpty(migrationVersion) ||
            migrationVersion.Length is < 3 or > 48 ||
            !migrationVersion.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-') ||
            string.IsNullOrEmpty(checksum) || checksum.Length != 64 ||
            !checksum.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f'))
            throw new InvalidOperationException("Invalid trusted migration scope/version/checksum.");

        var canonical = string.Join("|", "crecisol.c04-09.v1",
            operationId.ToString("N"), placement.TenantId.Value.ToString("N"),
            ((int)placement.Mode).ToString(System.Globalization.CultureInfo.InvariantCulture),
            placement.DedicatedSlot.Value.ToString("N"),
            placement.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture),
            migrationVersion, checksum, approvedUntilUtc.UtcTicks.ToString(
                System.Globalization.CultureInfo.InvariantCulture));
        var fingerprint = Convert.ToHexString(SHA256.HashData(
            Encoding.ASCII.GetBytes(canonical))).ToLowerInvariant();
        return new TrustedTenantMigrationPlan(operationId, placement.TenantId,
            placement.Mode, placement.DedicatedSlot, placement.Revision,
            migrationVersion, checksum, approvedUntilUtc, fingerprint);
    }

    public bool IsApprovedAtUtc(DateTimeOffset utcNow) =>
        utcNow != default && utcNow.Offset == TimeSpan.Zero && utcNow < ApprovedUntilUtc;

    public override string ToString() => "TrustedTenantMigrationPlan[redacted]";
}

public enum TenantMigrationReceiptStatus
{
    Applied = 1,
    Replayed = 2
}

/// <summary>
/// Future DEDICATED durable gate receipt, NOT a database migration or authorization.
/// Migration DDL may implicitly commit in MySQL: a receipt alone cannot prove
/// physical rollback or successful restoration.
/// </summary>
public sealed record TrustedTenantMigrationReceipt
{
    private TrustedTenantMigrationReceipt(TenantId tenantId, Guid operationId,
        TenantPlacementMode mode, DedicatedStorageSlotId? slot, long placementRevision,
        string version, string checksum, string fingerprint, TenantMigrationReceiptStatus status,
        DateTimeOffset verifiedAtUtc)
    {
        TenantId = tenantId;
        OperationId = operationId;
        Mode = mode;
        Slot = slot;
        PlacementRevision = placementRevision;
        Version = version;
        Checksum = checksum;
        IntentFingerprint = fingerprint;
        Status = status;
        VerifiedAtUtc = verifiedAtUtc;
    }

    public TenantId TenantId { get; }
    public Guid OperationId { get; }
    public TenantPlacementMode Mode { get; }
    public DedicatedStorageSlotId? Slot { get; }
    public long PlacementRevision { get; }
    public string Version { get; }
    public string Checksum { get; }
    public string IntentFingerprint { get; }
    public TenantMigrationReceiptStatus Status { get; }
    public DateTimeOffset VerifiedAtUtc { get; }

    public static TrustedTenantMigrationReceipt FromTrustedVerifiedLedger(
        TenantId tenantId, Guid operationId, TenantPlacementMode mode,
        DedicatedStorageSlotId? slot, long placementRevision, string version,
        string checksum, string fingerprint, TenantMigrationReceiptStatus status,
        DateTimeOffset verifiedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(tenantId);
        if (operationId == Guid.Empty || mode != TenantPlacementMode.Dedicated || slot is null ||
            placementRevision <= 0 || string.IsNullOrWhiteSpace(version) ||
            checksum is null || checksum.Length != 64 ||
            !checksum.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f') ||
            fingerprint is null || fingerprint.Length != 64 ||
            !fingerprint.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f') ||
            !Enum.IsDefined(status) || verifiedAtUtc == default ||
            verifiedAtUtc.Offset != TimeSpan.Zero)
            throw new InvalidOperationException("Invalid migration ledger receipt.");
        return new TrustedTenantMigrationReceipt(tenantId, operationId, mode, slot,
            placementRevision, version, checksum, fingerprint, status, verifiedAtUtc);
    }

    public static void RequireExactMatch(TrustedTenantMigrationPlan plan,
        TrustedTenantMigrationReceipt receipt, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(receipt);
        if (!plan.IsApprovedAtUtc(nowUtc) || receipt.VerifiedAtUtc > nowUtc ||
            receipt.TenantId != plan.TenantId ||
            receipt.OperationId != plan.OperationId ||
            receipt.Mode != plan.Mode ||
            receipt.Slot != plan.Slot ||
            receipt.PlacementRevision != plan.PlacementRevision ||
            !string.Equals(receipt.Version, plan.MigrationVersion, StringComparison.Ordinal) ||
            !string.Equals(receipt.Checksum, plan.Checksum, StringComparison.Ordinal) ||
            !string.Equals(receipt.IntentFingerprint, plan.IntentFingerprint, StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Migration receipt is not bound to trusted scope.");
    }

    public override string ToString() => "TrustedTenantMigrationReceipt[redacted]";
}
