namespace Crecisol.Domain.Events;

/// <summary>
/// Only a harmless, fixed, versioned PLATFORM probe is constructible in C03-07.
/// Tenant events cannot be created before verified identity, membership and resource
/// ownership exist in a later chapter. This is not an authorization primitive.
/// </summary>
public sealed class PlatformOutboxEvent
{
    public const string SupportedType = "platform.foundation-check.v1";
    public const int SupportedVersion = 1;
    public const string FixedPayload = "{\"kind\":\"foundation-check\"}";

    private PlatformOutboxEvent(Guid eventId, Guid correlationId, DateTime occurredAtUtc)
    {
        EventId = eventId;
        CorrelationId = correlationId;
        OccurredAtUtc = occurredAtUtc;
    }

    public Guid EventId { get; }
    public Guid CorrelationId { get; }
    public DateTime OccurredAtUtc { get; }
    // Explicit platform scope: never a browser-supplied tenant or branch.
    public byte ScopeKind => 1;
    public Guid ScopeId => Guid.Empty;
    public Guid? BranchId => null;
    public Guid? CausationId => null;
    public string EventType => SupportedType;
    public int SchemaVersion => SupportedVersion;
    public string PayloadJson => FixedPayload;

    public static PlatformOutboxEvent CreateFoundationProbe(
        Guid eventId, Guid correlationId, DateTime occurredAtUtc)
    {
        if (eventId == Guid.Empty || correlationId == Guid.Empty)
            throw new ArgumentException("A nonempty event and correlation identity are required.");
        if (occurredAtUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("An unambiguous UTC timestamp is required.");
        return new PlatformOutboxEvent(eventId, correlationId, occurredAtUtc);
    }

    public static bool SupportsConsumer(string eventType, int version) =>
        string.Equals(eventType, SupportedType, StringComparison.Ordinal) &&
        version == SupportedVersion;
}
