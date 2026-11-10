using System.Data;
using Crecisol.Application.Events;
using Crecisol.Domain.Events;
using MySqlConnector;

namespace Crecisol.Infrastructure.Events;

/// <summary>
/// Inactive, trusted-only PLATFORM outbox adapter; deliberately NOT composed
/// by the public API. This is a bounded local test foundation, not a tenant
/// queue, an authorization service, an external publisher or a product worker.
/// </summary>
public sealed class MySqlPlatformOutboxRelay(string? trustedServerDsn) : IPlatformOutboxRelayPort
{
    private static readonly byte[] PlatformScopeId = new byte[16];

    public async Task<MySqlConnection> OpenTrustedConnectionAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(trustedServerDsn))
            throw new InvalidOperationException("Trusted local outbox storage is not configured.");

        var config = new MySqlConnectionStringBuilder(trustedServerDsn)
        {
            ConnectionTimeout = 3,
            DefaultCommandTimeout = 6,
            Pooling = false
        };
        var db = new MySqlConnection(config.ConnectionString);
        try
        {
            await db.OpenAsync(cancellationToken);
            return db;
        }
        catch
        {
            await db.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Must be invoked INSIDE the exact same trusted InnoDB transaction that
    /// changed its aggregate. This API does not begin/commit a separate tx.
    /// No call is exposed from HTTP or a C03-07 production use case.
    /// </summary>
    public static async Task AppendWithinTransactionAsync(
        MySqlConnection db, MySqlTransaction transaction,
        PlatformOutboxEvent message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();
        if (!ReferenceEquals(transaction.Connection, db) || db.State != ConnectionState.Open)
            throw new InvalidOperationException("Outbox event requires its aggregate transaction.");

        const string sql = """
            INSERT INTO c03_07_outbox_events
              (event_id, scope_kind, scope_id, branch_id, event_type, schema_version,
               payload_json, correlation_id, causation_id, occurred_at_utc,
               status, available_at_utc, created_at_utc)
            VALUES
              (@event, 1, @scope, NULL, @type, @version,
               @payload, @correlation, NULL, @occurred,
               'pending', UTC_TIMESTAMP(6), UTC_TIMESTAMP(6))
            """;
        await using var command = new MySqlCommand(sql, db, transaction);
        command.Parameters.AddWithValue("@event", message.EventId.ToByteArray());
        command.Parameters.AddWithValue("@scope", PlatformScopeId);
        command.Parameters.AddWithValue("@type", message.EventType);
        command.Parameters.AddWithValue("@version", message.SchemaVersion);
        command.Parameters.AddWithValue("@payload", message.PayloadJson);
        command.Parameters.AddWithValue("@correlation", message.CorrelationId.ToByteArray());
        command.Parameters.AddWithValue("@occurred", message.OccurredAtUtc);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException("Outbox append was not atomic.");
    }

    public async Task<PlatformEventLease?> ClaimNextPlatformAsync(
        Guid workerId, int leaseSeconds, CancellationToken cancellationToken)
    {
        if (workerId == Guid.Empty) throw new ArgumentException("Worker identity required.", nameof(workerId));
        if (leaseSeconds is < 1 or > 120) throw new ArgumentOutOfRangeException(nameof(leaseSeconds));
        await using var db = await OpenTrustedConnectionAsync(cancellationToken);
        await using var tx = await db.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        // Crash after last allowed attempt is terminal; a lease cannot live forever.
        await using (var recover = new MySqlCommand("""
            UPDATE c03_07_outbox_events
               SET status='dead-letter', last_failure_code='lease-expired',
                   lease_owner=NULL, lease_expires_at_utc=NULL
             WHERE scope_kind=1 AND scope_id=@scope AND status='leased'
               AND lease_expires_at_utc<=UTC_TIMESTAMP(6) AND attempt_count>=3
            """, db, tx))
        {
            recover.Parameters.AddWithValue("@scope", PlatformScopeId);
            await recover.ExecuteNonQueryAsync(cancellationToken);
        }

        Guid eventId;
        string eventType;
        int schemaVersion;
        long generation;
        await using (var select = new MySqlCommand("""
            SELECT event_id, event_type, schema_version, lease_generation
              FROM c03_07_outbox_events
             WHERE scope_kind=1 AND scope_id=@scope AND attempt_count<3
               AND (
                    (status IN ('pending','retryable') AND available_at_utc<=UTC_TIMESTAMP(6))
                 OR (status='leased' AND lease_expires_at_utc<=UTC_TIMESTAMP(6))
               )
             ORDER BY available_at_utc, event_id
             LIMIT 1 FOR UPDATE SKIP LOCKED
            """, db, tx))
        {
            select.Parameters.AddWithValue("@scope", PlatformScopeId);
            await using var reader = await select.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                await reader.DisposeAsync();
                await tx.CommitAsync(cancellationToken);
                return null;
            }
            eventId = new Guid((byte[])reader.GetValue(0));
            eventType = reader.GetString(1);
            schemaVersion = reader.GetInt32(2);
            generation = checked(reader.GetInt64(3) + 1);
        }

        await using (var update = new MySqlCommand("""
            UPDATE c03_07_outbox_events
               SET status='leased', attempt_count=attempt_count+1,
                   lease_generation=@generation, lease_owner=@worker,
                   lease_expires_at_utc=DATE_ADD(UTC_TIMESTAMP(6), INTERVAL @seconds SECOND)
             WHERE event_id=@event AND scope_kind=1 AND scope_id=@scope
            """, db, tx))
        {
            update.Parameters.AddWithValue("@generation", generation);
            update.Parameters.AddWithValue("@worker", workerId.ToByteArray());
            update.Parameters.AddWithValue("@seconds", leaseSeconds);
            update.Parameters.AddWithValue("@event", eventId.ToByteArray());
            update.Parameters.AddWithValue("@scope", PlatformScopeId);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new InvalidOperationException("Atomic lease claim failed.");
        }
        await tx.CommitAsync(cancellationToken);
        return new PlatformEventLease(eventId, workerId, generation, eventType, schemaVersion);
    }

    public async Task<bool> AcknowledgeAsync(
        PlatformEventLease lease, string consumerName, CancellationToken cancellationToken)
    {
        ValidateLease(lease);
        if (consumerName != PlatformOutboxWorker.InternalConsumer)
            throw new InvalidOperationException("Unknown consumer must not receive a receipt.");
        await using var db = await OpenTrustedConnectionAsync(cancellationToken);
        await using var tx = await db.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        if (!await HoldsFreshLeaseAsync(db, tx, lease, cancellationToken))
        {
            await tx.RollbackAsync(cancellationToken);
            return false;
        }

        // Receipt is the ONLY processed effect in C03-07. It shares the same
        // transaction as delivery state; future business effects must NOT be
        // declared covered by this atomicity demonstration.
        await using (var receipt = new MySqlCommand("""
            INSERT INTO c03_07_inbox_receipts
              (scope_kind,scope_id,consumer_name,event_id,processed_at_utc)
            VALUES (1,@scope,@consumer,@event,UTC_TIMESTAMP(6))
            ON DUPLICATE KEY UPDATE event_id=event_id
            """, db, tx))
        {
            receipt.Parameters.AddWithValue("@scope", PlatformScopeId);
            receipt.Parameters.AddWithValue("@consumer", consumerName);
            receipt.Parameters.AddWithValue("@event", lease.EventId.ToByteArray());
            await receipt.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var update = new MySqlCommand("""
            UPDATE c03_07_outbox_events
               SET status='delivered', lease_owner=NULL,
                   lease_expires_at_utc=NULL, last_failure_code=NULL
             WHERE event_id=@event AND scope_kind=1 AND scope_id=@scope
               AND status='leased' AND lease_owner=@owner
               AND lease_generation=@generation AND lease_expires_at_utc>UTC_TIMESTAMP(6)
            """, db, tx))
        {
            AddLeaseParameters(update, lease);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new InvalidOperationException("Lease changed during atomic receipt.");
        }
        await tx.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<bool> FailAsync(
        PlatformEventLease lease, bool permanently, CancellationToken cancellationToken)
    {
        ValidateLease(lease);
        await using var db = await OpenTrustedConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand("""
            UPDATE c03_07_outbox_events
               SET status=IF(@permanent=1 OR attempt_count>=3,'dead-letter','retryable'),
                   available_at_utc=DATE_ADD(UTC_TIMESTAMP(6),
                       INTERVAL CASE WHEN attempt_count=1 THEN 1
                                     WHEN attempt_count=2 THEN 2 ELSE 4 END SECOND),
                   lease_owner=NULL, lease_expires_at_utc=NULL,
                   last_failure_code=IF(@permanent=1,'unsupported-version','retry')
             WHERE event_id=@event AND scope_kind=1 AND scope_id=@scope
               AND status='leased' AND lease_owner=@owner
               AND lease_generation=@generation AND lease_expires_at_utc>UTC_TIMESTAMP(6)
            """, db);
        AddLeaseParameters(command, lease);
        command.Parameters.AddWithValue("@permanent", permanently ? 1 : 0);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    private static async Task<bool> HoldsFreshLeaseAsync(
        MySqlConnection db, MySqlTransaction tx, PlatformEventLease lease, CancellationToken ct)
    {
        await using var command = new MySqlCommand("""
            SELECT 1 FROM c03_07_outbox_events
             WHERE event_id=@event AND scope_kind=1 AND scope_id=@scope
               AND status='leased' AND lease_owner=@owner
               AND lease_generation=@generation AND lease_expires_at_utc>UTC_TIMESTAMP(6)
             FOR UPDATE
            """, db, tx);
        AddLeaseParameters(command, lease);
        return await command.ExecuteScalarAsync(ct) is not null;
    }

    private static void AddLeaseParameters(MySqlCommand command, PlatformEventLease lease)
    {
        command.Parameters.AddWithValue("@event", lease.EventId.ToByteArray());
        command.Parameters.AddWithValue("@scope", PlatformScopeId);
        command.Parameters.AddWithValue("@owner", lease.WorkerId.ToByteArray());
        command.Parameters.AddWithValue("@generation", lease.Generation);
    }

    private static void ValidateLease(PlatformEventLease? lease)
    {
        ArgumentNullException.ThrowIfNull(lease);
        if (lease.EventId == Guid.Empty || lease.WorkerId == Guid.Empty || lease.Generation <= 0)
            throw new InvalidOperationException("An authenticated lease generation is required.");
    }
}
