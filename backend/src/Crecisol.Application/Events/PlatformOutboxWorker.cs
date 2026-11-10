using Crecisol.Domain.Events;

namespace Crecisol.Application.Events;

/// <summary>
/// A lease is fenced by (event, worker, generation); its existence grants no
/// user, tenant, business or database permission.
/// </summary>
public sealed record PlatformEventLease(
    Guid EventId, Guid WorkerId, long Generation, string EventType, int SchemaVersion);

public interface IPlatformOutboxRelayPort
{
    Task<PlatformEventLease?> ClaimNextPlatformAsync(
        Guid workerId, int leaseSeconds, CancellationToken cancellationToken);
    Task<bool> AcknowledgeAsync(
        PlatformEventLease lease, string consumerName, CancellationToken cancellationToken);
    Task<bool> FailAsync(
        PlatformEventLease lease, bool permanently, CancellationToken cancellationToken);
}

/// <summary>
/// Explicit opt-in, bounded, cancellation-aware local relay. Never registered
/// as an API hosted service. C03-07 only persists its own idempotency receipt;
/// it does NOT execute customer-defined handlers, business effects or HTTP.
/// </summary>
public sealed class PlatformOutboxWorker(IPlatformOutboxRelayPort port)
{
    public const string InternalConsumer = "crecisol.platform.foundation-check.v1";

    public async Task<int> RunBatchAsync(
        Guid workerId, int maxItems, CancellationToken cancellationToken)
    {
        if (workerId == Guid.Empty)
            throw new ArgumentException("A nonempty worker identity is required.", nameof(workerId));
        if (maxItems is < 1 or > 32)
            throw new ArgumentOutOfRangeException(nameof(maxItems), "Batch bound is 1..32.");

        int handled = 0;
        for (int i = 0; i < maxItems; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var lease = await port.ClaimNextPlatformAsync(workerId, 15, cancellationToken);
            if (lease is null) break;
            if (!PlatformOutboxEvent.SupportsConsumer(lease.EventType, lease.SchemaVersion))
            {
                if (!await port.FailAsync(lease, true, cancellationToken))
                    throw new InvalidOperationException("Outbox lease lost before fail-closed rejection.");
            }
            else if (!await port.AcknowledgeAsync(lease, InternalConsumer, cancellationToken))
            {
                throw new InvalidOperationException("Outbox lease lost before idempotent acknowledgement.");
            }
            handled++;
        }
        return handled;
    }
}
