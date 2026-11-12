namespace Crecisol.Domain.Tenancy;

/// <summary>
/// CRECISOL C04-03: placement kind, not a permission or data-source URL.
/// Host, slug, HTTP headers, cookies and claims NEVER decide this value.
/// </summary>
public enum TenantPlacementMode
{
    Shared = 1,
    Dedicated = 2
}

/// <summary>
/// Opaque reference to a future SERVER-OWNED dedicated storage catalogue entry.
/// Knowing this GUID is not a credential and cannot open a database.
/// </summary>
public sealed record DedicatedStorageSlotId
{
    private DedicatedStorageSlotId(Guid value) => Value = value;

    public Guid Value { get; }

    public static DedicatedStorageSlotId FromTrustedCatalog(Guid value)
    {
        if (value == Guid.Empty)
            throw new InvalidOperationException("A dedicated storage slot is required.");
        return new DedicatedStorageSlotId(value);
    }

    public override string ToString() => "DedicatedStorageSlotId[redacted]";
}

/// <summary>
/// Immutable placement metadata hydrated ONLY by a future protected backend
/// catalogue adapter. This type carries NO connection string, host, database
/// name, secret, tenant membership, lease or access authorization.
/// </summary>
public sealed record TenantStoragePlacementSnapshot
{
    private TenantStoragePlacementSnapshot(
        TenantId tenantId,
        TenantPlacementMode mode,
        DedicatedStorageSlotId? dedicatedSlot,
        long revision,
        DateTimeOffset updatedAtUtc)
    {
        TenantId = tenantId;
        Mode = mode;
        DedicatedSlot = dedicatedSlot;
        Revision = revision;
        UpdatedAtUtc = updatedAtUtc;
    }

    public TenantId TenantId { get; }
    public TenantPlacementMode Mode { get; }
    public DedicatedStorageSlotId? DedicatedSlot { get; }
    public long Revision { get; }
    public DateTimeOffset UpdatedAtUtc { get; }

    public static TenantStoragePlacementSnapshot RestoreFromTrustedCatalog(
        TenantId tenantId,
        TenantPlacementMode mode,
        DedicatedStorageSlotId? dedicatedSlot,
        long revision,
        DateTimeOffset updatedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(tenantId);
        if (!Enum.IsDefined(mode))
            throw new InvalidOperationException("Unknown tenant placement mode.");
        if (mode == TenantPlacementMode.Shared && dedicatedSlot is not null)
            throw new InvalidOperationException("Shared placement cannot reference dedicated storage.");
        if (mode == TenantPlacementMode.Dedicated && dedicatedSlot is null)
            throw new InvalidOperationException("Dedicated placement requires a trusted slot.");
        if (revision <= 0)
            throw new InvalidOperationException("Placement revision must be positive.");
        if (updatedAtUtc == default || updatedAtUtc.Offset != TimeSpan.Zero)
            throw new InvalidOperationException("Placement timestamp must be UTC.");

        return new TenantStoragePlacementSnapshot(
            tenantId, mode, dedicatedSlot, revision, updatedAtUtc);
    }

    public override string ToString() => "TenantStoragePlacementSnapshot[redacted]";
}
