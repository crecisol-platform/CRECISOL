using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Crecisol.Domain.Tenancy;

/// <summary>
/// GLOBAL SHARED DDL changes one database schema for ALL of its tenants.
/// It must never be presented as one tenant's isolated migration.
/// </summary>
public enum ProtectedSharedSchemaFamily
{
    TenantResources = 1
}

/// <summary>
/// Platform-wide decision for SHARED schema change. Deliberately has no
/// TenantId, DedicatedStorageSlotId, DSN, hostname or request-selected target.
/// Protected backend approval, maintenance window, impact inventory and
/// compatible deployment are additional future requirements.
/// </summary>
public sealed record TrustedSharedSchemaMigrationPlan
{
    private TrustedSharedSchemaMigrationPlan(
        Guid operationId, ProtectedSharedSchemaFamily family, long schemaRevision,
        string version, string checksum, DateTimeOffset approvedUntilUtc,
        string fingerprint)
    {
        OperationId = operationId;
        SchemaFamily = family;
        ExpectedSchemaRevision = schemaRevision;
        MigrationVersion = version;
        Checksum = checksum;
        ApprovedUntilUtc = approvedUntilUtc;
        IntentFingerprint = fingerprint;
    }

    public Guid OperationId { get; }
    public ProtectedSharedSchemaFamily SchemaFamily { get; }
    public long ExpectedSchemaRevision { get; }
    public string MigrationVersion { get; }
    public string Checksum { get; }
    public DateTimeOffset ApprovedUntilUtc { get; }
    public string IntentFingerprint { get; }

    public static TrustedSharedSchemaMigrationPlan FromTrustedPlatformApproval(
        Guid operationId, ProtectedSharedSchemaFamily family,
        long schemaRevision, string version, string checksum,
        DateTimeOffset approvedUntilUtc)
    {
        if (operationId == Guid.Empty ||
            family != ProtectedSharedSchemaFamily.TenantResources ||
            schemaRevision <= 0 || schemaRevision == long.MaxValue ||
            version is null || version.Length is < 3 or > 48 ||
            version[0] is < 'a' or > 'z' ||
            !version.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-') ||
            checksum is null || checksum.Length != 64 ||
            !checksum.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f') ||
            approvedUntilUtc == default || approvedUntilUtc.Offset != TimeSpan.Zero)
            throw new InvalidOperationException("Invalid platform-wide shared migration approval.");

        var canonical = string.Join("|",
            "crecisol.c04-09.shared.v1",
            operationId.ToString("N"),
            ((int)family).ToString(CultureInfo.InvariantCulture),
            schemaRevision.ToString(CultureInfo.InvariantCulture),
            version, checksum, approvedUntilUtc.UtcTicks.ToString(CultureInfo.InvariantCulture));
        var fingerprint = Convert.ToHexString(SHA256.HashData(
            Encoding.ASCII.GetBytes(canonical))).ToLowerInvariant();
        return new TrustedSharedSchemaMigrationPlan(operationId, family,
            schemaRevision, version, checksum, approvedUntilUtc, fingerprint);
    }

    public bool IsApprovedAtUtc(DateTimeOffset utcNow) =>
        utcNow != default && utcNow.Offset == TimeSpan.Zero && utcNow < ApprovedUntilUtc;

    public override string ToString() => "TrustedSharedSchemaMigrationPlan[redacted]";
}

/// <summary>
/// Verified ledger receipt for the entire SHARED schema; never a per-tenant
/// consent or access grant. No implicit DDL rollback is implied.
/// </summary>
public sealed record TrustedSharedSchemaMigrationReceipt
{
    private TrustedSharedSchemaMigrationReceipt(
        Guid operationId, ProtectedSharedSchemaFamily family,
        long schemaRevision, string version, string checksum,
        string fingerprint, TenantMigrationReceiptStatus status,
        DateTimeOffset verifiedAtUtc)
    {
        OperationId = operationId;
        SchemaFamily = family;
        CommittedSchemaRevision = schemaRevision;
        Version = version;
        Checksum = checksum;
        IntentFingerprint = fingerprint;
        Status = status;
        VerifiedAtUtc = verifiedAtUtc;
    }

    public Guid OperationId { get; }
    public ProtectedSharedSchemaFamily SchemaFamily { get; }
    public long CommittedSchemaRevision { get; }
    public string Version { get; }
    public string Checksum { get; }
    public string IntentFingerprint { get; }
    public TenantMigrationReceiptStatus Status { get; }
    public DateTimeOffset VerifiedAtUtc { get; }

    public static TrustedSharedSchemaMigrationReceipt FromTrustedVerifiedLedger(
        Guid operationId, ProtectedSharedSchemaFamily family,
        long committedSchemaRevision, string version, string checksum,
        string fingerprint, TenantMigrationReceiptStatus status,
        DateTimeOffset verifiedAtUtc)
    {
        if (operationId == Guid.Empty || family != ProtectedSharedSchemaFamily.TenantResources ||
            committedSchemaRevision <= 1 || version is null ||
            version.Length is < 3 or > 48 ||
            checksum is null || checksum.Length != 64 ||
            !checksum.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f') ||
            fingerprint is null || fingerprint.Length != 64 ||
            !fingerprint.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f') ||
            !Enum.IsDefined(status) || verifiedAtUtc == default ||
            verifiedAtUtc.Offset != TimeSpan.Zero)
            throw new InvalidOperationException("Invalid shared migration receipt.");
        return new TrustedSharedSchemaMigrationReceipt(operationId, family,
            committedSchemaRevision, version, checksum, fingerprint, status,
            verifiedAtUtc);
    }

    public static void RequireExactMatch(TrustedSharedSchemaMigrationPlan plan,
        TrustedSharedSchemaMigrationReceipt receipt, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(receipt);
        if (!plan.IsApprovedAtUtc(nowUtc) ||
            receipt.VerifiedAtUtc > nowUtc ||
            receipt.OperationId != plan.OperationId ||
            receipt.SchemaFamily != plan.SchemaFamily ||
            receipt.CommittedSchemaRevision != checked(plan.ExpectedSchemaRevision + 1) ||
            !string.Equals(receipt.Version, plan.MigrationVersion, StringComparison.Ordinal) ||
            !string.Equals(receipt.Checksum, plan.Checksum, StringComparison.Ordinal) ||
            !string.Equals(receipt.IntentFingerprint, plan.IntentFingerprint, StringComparison.Ordinal))
            throw new UnauthorizedAccessException(
                "Shared schema receipt does not match platform-wide approval.");
    }

    public override string ToString() => "TrustedSharedSchemaMigrationReceipt[redacted]";
}
