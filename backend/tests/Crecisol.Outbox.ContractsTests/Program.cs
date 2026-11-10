using Crecisol.Application.Events;
using Crecisol.Domain.Events;
using Crecisol.Infrastructure.Events;
using MySqlConnector;

if (args.Length != 1 || args[0] is not ("contracts" or "mysql"))
    throw new InvalidOperationException("Use contracts or mysql, exclusively.");
var checks = 0;
void Check(bool ok, string why)
{
    if (!ok) throw new InvalidOperationException("FAIL C03-07: " + why);
    checks++;
}
async Task RejectAsync<T>(Func<Task> work, string why) where T : Exception
{
    try { await work(); }
    catch (T) { Check(true, why); return; }
    throw new InvalidOperationException("FAIL C03-07: expected rejection " + why);
}
async Task<int> EventCountAsync(MySqlPlatformOutboxRelay store, Guid id)
{
    await using var db = await store.OpenTrustedConnectionAsync(CancellationToken.None);
    await using var cmd = new MySqlCommand(
        "SELECT COUNT(*) FROM c03_07_outbox_events WHERE event_id=@id", db);
    cmd.Parameters.AddWithValue("@id", id.ToByteArray());
    return Convert.ToInt32(await cmd.ExecuteScalarAsync());
}
async Task<int> ReceiptCountAsync(MySqlPlatformOutboxRelay store, Guid id)
{
    await using var db = await store.OpenTrustedConnectionAsync(CancellationToken.None);
    await using var cmd = new MySqlCommand(
        "SELECT COUNT(*) FROM c03_07_inbox_receipts WHERE event_id=@id", db);
    cmd.Parameters.AddWithValue("@id", id.ToByteArray());
    return Convert.ToInt32(await cmd.ExecuteScalarAsync());
}
async Task<string> StatusAsync(MySqlPlatformOutboxRelay store, Guid id)
{
    await using var db = await store.OpenTrustedConnectionAsync(CancellationToken.None);
    await using var cmd = new MySqlCommand(
        "SELECT status FROM c03_07_outbox_events WHERE event_id=@id", db);
    cmd.Parameters.AddWithValue("@id", id.ToByteArray());
    return (string)(await cmd.ExecuteScalarAsync() ??
        throw new InvalidOperationException("Missing expected event."));
}
async Task AppendAndCommitAsync(MySqlPlatformOutboxRelay store, PlatformOutboxEvent message)
{
    await using var db = await store.OpenTrustedConnectionAsync(CancellationToken.None);
    await using var tx = await db.BeginTransactionAsync();
    await MySqlPlatformOutboxRelay.AppendWithinTransactionAsync(db, tx, message, CancellationToken.None);
    await tx.CommitAsync();
}
PlatformOutboxEvent NewProbe() =>
    PlatformOutboxEvent.CreateFoundationProbe(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);

if (args[0] == "contracts")
{
    var msg = NewProbe();
    Check(msg.EventType == "platform.foundation-check.v1", "Only fixed known type exists");
    Check(msg.ScopeKind == 1 && msg.ScopeId == Guid.Empty &&
          msg.BranchId is null && msg.CausationId is null,
          "Scope and causation remain explicit PLATFORM-only");
    Check(msg.SchemaVersion == 1, "Schema is versioned");
    Check(msg.PayloadJson == "{\"kind\":\"foundation-check\"}", "Only fixed safe payload");
    Check(msg.OccurredAtUtc.Kind == DateTimeKind.Utc, "UTC enforced");
    Check(msg.EventId != Guid.Empty && msg.CorrelationId != Guid.Empty, "Opaque correlation IDs");
    Check(PlatformOutboxEvent.SupportsConsumer(msg.EventType, 1), "Known version supported");
    Check(!PlatformOutboxEvent.SupportsConsumer(msg.EventType, 2), "Unknown schema denied");
    Check(!PlatformOutboxEvent.SupportsConsumer("tenant.private.v1", 1), "Tenant event denied");
    Check(!PlatformOutboxEvent.SupportsConsumer("Platform.foundation-check.v1", 1),
        "Case-variant event denied");
    await RejectAsync<ArgumentException>(
        () => Task.FromResult(PlatformOutboxEvent.CreateFoundationProbe(
            Guid.Empty, Guid.NewGuid(), DateTime.UtcNow)), "Empty event identity");
    await RejectAsync<ArgumentException>(
        () => Task.FromResult(PlatformOutboxEvent.CreateFoundationProbe(
            Guid.NewGuid(), Guid.Empty, DateTime.UtcNow)), "Empty correlation");
    await RejectAsync<ArgumentException>(
        () => Task.FromResult(PlatformOutboxEvent.CreateFoundationProbe(
            Guid.NewGuid(), Guid.NewGuid(), DateTime.Now)), "Local timestamp");
    await RejectAsync<ArgumentException>(
        () => Task.FromResult(PlatformOutboxEvent.CreateFoundationProbe(
            Guid.NewGuid(), Guid.NewGuid(), DateTime.SpecifyKind(DateTime.UtcNow,
                DateTimeKind.Unspecified))), "Ambiguous timestamp");

    var memory = new FakeRelayPort();
    var worker = new PlatformOutboxWorker(memory);
    await RejectAsync<ArgumentException>(
        () => worker.RunBatchAsync(Guid.Empty, 1, CancellationToken.None),
        "Empty worker denied");
    await RejectAsync<ArgumentOutOfRangeException>(
        () => worker.RunBatchAsync(Guid.NewGuid(), 0, CancellationToken.None),
        "Zero batch denied");
    await RejectAsync<ArgumentOutOfRangeException>(
        () => worker.RunBatchAsync(Guid.NewGuid(), 33, CancellationToken.None),
        "Unbounded batch denied");
    using var cancelled = new CancellationTokenSource();
    cancelled.Cancel();
    await RejectAsync<OperationCanceledException>(
        () => worker.RunBatchAsync(Guid.NewGuid(), 1, cancelled.Token),
        "Cancellation before claim");

    var owner = Guid.NewGuid();
    memory.Next = new PlatformEventLease(Guid.NewGuid(), owner, 1,
        PlatformOutboxEvent.SupportedType, 1);
    Check(await worker.RunBatchAsync(owner, 8, CancellationToken.None) == 1,
        "One known event consumed without repeat");
    Check(memory.AckCount == 1 && memory.FailCount == 0,
        "Known event creates only one receipt request");
    memory.Next = new PlatformEventLease(Guid.NewGuid(), owner, 1,
        "platform.unknown-v2", 2);
    Check(await worker.RunBatchAsync(owner, 8, CancellationToken.None) == 1,
        "Poison event handled once");
    Check(memory.FailCount == 1 && memory.AckCount == 1,
        "Unsupported version dead-lettered without handler execution");
    await RejectAsync<InvalidOperationException>(
        () => new MySqlPlatformOutboxRelay(null).ClaimNextPlatformAsync(
            owner, 1, CancellationToken.None), "Missing trusted DSN fails closed");
    await RejectAsync<ArgumentOutOfRangeException>(
        () => new MySqlPlatformOutboxRelay(null).ClaimNextPlatformAsync(
            owner, 0, CancellationToken.None), "Invalid lease time");
    Check(typeof(MySqlPlatformOutboxRelay).Assembly.GetName().Name ==
          "Crecisol.Infrastructure", "SQL remains only in Infrastructure");
    Check(typeof(IPlatformOutboxRelayPort).Assembly.GetName().Name ==
          "Crecisol.Application", "Port remains only in Application");
}
else
{
    var dsn = Environment.GetEnvironmentVariable("CRECISOL_C03_07_TEST_DSN");
    if (string.IsNullOrWhiteSpace(dsn))
        throw new InvalidOperationException("Disposable local MySQL test DSN required.");
    var store = new MySqlPlatformOutboxRelay(dsn);
    var discarded = NewProbe();
    await using (var db = await store.OpenTrustedConnectionAsync(CancellationToken.None))
    {
        await using var tx = await db.BeginTransactionAsync();
        await using (var marker = new MySqlCommand(
            "INSERT INTO c03_07_test_aggregate(event_id) VALUES (@id)", db, tx))
        {
            marker.Parameters.AddWithValue("@id", discarded.EventId.ToByteArray());
            Check(await marker.ExecuteNonQueryAsync() == 1,
                "Synthetic aggregate updated inside transaction");
        }
        await MySqlPlatformOutboxRelay.AppendWithinTransactionAsync(
            db, tx, discarded, CancellationToken.None);
        await tx.RollbackAsync();
    }
    Check(await EventCountAsync(store, discarded.EventId) == 0,
        "Rollback leaves no publishable event");
    await using (var db = await store.OpenTrustedConnectionAsync(CancellationToken.None))
    {
        await using var command = new MySqlCommand(
            "SELECT COUNT(*) FROM c03_07_test_aggregate WHERE event_id=@id", db);
        command.Parameters.AddWithValue("@id", discarded.EventId.ToByteArray());
        Check(Convert.ToInt32(await command.ExecuteScalarAsync()) == 0,
            "Rollback also cancels synthetic aggregate effect");
    }

    var delivered = NewProbe();
    await using (var db = await store.OpenTrustedConnectionAsync(CancellationToken.None))
    {
        await using var tx = await db.BeginTransactionAsync();
        await using (var marker = new MySqlCommand(
            "INSERT INTO c03_07_test_aggregate(event_id) VALUES (@id)", db, tx))
        {
            marker.Parameters.AddWithValue("@id", delivered.EventId.ToByteArray());
            await marker.ExecuteNonQueryAsync();
        }
        await MySqlPlatformOutboxRelay.AppendWithinTransactionAsync(
            db, tx, delivered, CancellationToken.None);
        await tx.CommitAsync();
    }
    Check(await EventCountAsync(store, delivered.EventId) == 1,
        "Commit atomically persists outbox");
    var owner1 = Guid.NewGuid();
    var first = await store.ClaimNextPlatformAsync(owner1, 2, CancellationToken.None);
    Check(first?.EventId == delivered.EventId, "Platform event claimed");
    Check(first is not null && first.Generation == 1, "First lease fenced");
    var active = first!;
    Check(!await store.AcknowledgeAsync(active with { WorkerId = Guid.NewGuid() },
          PlatformOutboxWorker.InternalConsumer, CancellationToken.None),
          "Spoofed worker cannot ACK");
    Check(!await store.AcknowledgeAsync(active with { Generation = 8 },
          PlatformOutboxWorker.InternalConsumer, CancellationToken.None),
          "Spoofed generation cannot ACK");
    await RejectAsync<InvalidOperationException>(
        () => store.AcknowledgeAsync(active, "untrusted.consumer", CancellationToken.None),
        "Unknown consumer denied");
    Check(await ReceiptCountAsync(store, delivered.EventId) == 0,
        "Rejected ACK has no receipt");
    Check(await store.AcknowledgeAsync(active, PlatformOutboxWorker.InternalConsumer,
          CancellationToken.None), "Correct lease ACK atomically succeeds");
    Check(await StatusAsync(store, delivered.EventId) == "delivered",
        "ACK marks delivered");
    Check(await ReceiptCountAsync(store, delivered.EventId) == 1,
        "Durable receipt inserted once");
    Check(!await store.AcknowledgeAsync(active, PlatformOutboxWorker.InternalConsumer,
          CancellationToken.None), "Replayed ACK is rejected");
    Check(await ReceiptCountAsync(store, delivered.EventId) == 1,
        "Replay does not duplicate durable receipt");

    var parallel = NewProbe();
    await AppendAndCommitAsync(store, parallel);
    var claims = await Task.WhenAll(Enumerable.Range(0, 8).Select(
        _ => store.ClaimNextPlatformAsync(Guid.NewGuid(), 3, CancellationToken.None)));
    Check(claims.Count(x => x is not null) == 1,
        "SKIP LOCKED prevents concurrent double claim");
    var winner = claims.Single(x => x is not null)!;
    Check(winner.EventId == parallel.EventId, "Concurrent winner owns expected event");
    Check(await store.AcknowledgeAsync(winner, PlatformOutboxWorker.InternalConsumer,
          CancellationToken.None), "Concurrent winner can ACK");

    var crash = NewProbe();
    await AppendAndCommitAsync(store, crash);
    var abandoned = (await store.ClaimNextPlatformAsync(
        Guid.NewGuid(), 1, CancellationToken.None))!;
    Check(abandoned.EventId == crash.EventId, "Crash scenario initially claimed");
    await Task.Delay(1250);
    var revived = (await store.ClaimNextPlatformAsync(
        Guid.NewGuid(), 2, CancellationToken.None))!;
    Check(revived.EventId == crash.EventId && revived.Generation == abandoned.Generation + 1,
        "Expired lease recovered with incremented fencing token");
    Check(!await store.AcknowledgeAsync(abandoned, PlatformOutboxWorker.InternalConsumer,
          CancellationToken.None), "Abandoned worker cannot ACK after lease recovery");
    Check(await store.AcknowledgeAsync(revived, PlatformOutboxWorker.InternalConsumer,
          CancellationToken.None), "Recovered worker can ACK");

    var retry = NewProbe();
    await AppendAndCommitAsync(store, retry);
    var attempt1 = (await store.ClaimNextPlatformAsync(
        Guid.NewGuid(), 2, CancellationToken.None))!;
    Check(await store.FailAsync(attempt1, false, CancellationToken.None),
        "Retryable error captured safely");
    Check(await StatusAsync(store, retry.EventId) == "retryable", "Retry state persisted");
    Check(await store.ClaimNextPlatformAsync(Guid.NewGuid(), 2, CancellationToken.None) is null,
        "Backoff blocks immediate reentry");
    await Task.Delay(1300);
    var attempt2 = (await store.ClaimNextPlatformAsync(
        Guid.NewGuid(), 2, CancellationToken.None))!;
    Check(attempt2.EventId == retry.EventId, "Retry resumes after backoff");
    Check(await store.FailAsync(attempt2, false, CancellationToken.None),
        "Second failure persisted");
    await Task.Delay(2300);
    var attempt3 = (await store.ClaimNextPlatformAsync(
        Guid.NewGuid(), 2, CancellationToken.None))!;
    Check(attempt3.EventId == retry.EventId, "Third attempt selected");
    Check(await store.FailAsync(attempt3, false, CancellationToken.None),
        "Bounded retries reach terminal state");
    Check(await StatusAsync(store, retry.EventId) == "dead-letter",
        "Three failures dead-letter without endless retry");

    var poison = Guid.NewGuid();
    await using (var db = await store.OpenTrustedConnectionAsync(CancellationToken.None))
    {
        await using var cmd = new MySqlCommand("""
            INSERT INTO c03_07_outbox_events
              (event_id,scope_kind,scope_id,event_type,schema_version,payload_json,
               correlation_id,occurred_at_utc,status,available_at_utc,created_at_utc)
            VALUES (@id,1,@scope,'platform.unknown-v2',2,'{}',
                    @corr,UTC_TIMESTAMP(6),'pending',UTC_TIMESTAMP(6),UTC_TIMESTAMP(6))
            """, db);
        cmd.Parameters.AddWithValue("@id", poison.ToByteArray());
        cmd.Parameters.AddWithValue("@scope", new byte[16]);
        cmd.Parameters.AddWithValue("@corr", Guid.NewGuid().ToByteArray());
        await cmd.ExecuteNonQueryAsync();
    }
    var worker = new PlatformOutboxWorker(store);
    Check(await worker.RunBatchAsync(Guid.NewGuid(), 8, CancellationToken.None) == 1,
        "Unknown schema handled only via fail-closed dead-letter");
    Check(await StatusAsync(store, poison) == "dead-letter", "Unknown type dead-lettered");
    Check(await ReceiptCountAsync(store, poison) == 0,
        "Unknown type never registered as processed");

    var tenantId = Guid.NewGuid();
    var tenantEvent = Guid.NewGuid();
    await using (var db = await store.OpenTrustedConnectionAsync(CancellationToken.None))
    {
        await using var cmd = new MySqlCommand("""
            INSERT INTO c03_07_outbox_events
              (event_id,scope_kind,scope_id,event_type,schema_version,payload_json,
               correlation_id,occurred_at_utc,status,available_at_utc,created_at_utc)
            VALUES (@id,2,@tenant,'tenant.synthetic.v1',1,'{}',
                    @corr,UTC_TIMESTAMP(6),'pending',UTC_TIMESTAMP(6),UTC_TIMESTAMP(6))
            """, db);
        cmd.Parameters.AddWithValue("@id", tenantEvent.ToByteArray());
        cmd.Parameters.AddWithValue("@tenant", tenantId.ToByteArray());
        cmd.Parameters.AddWithValue("@corr", Guid.NewGuid().ToByteArray());
        await cmd.ExecuteNonQueryAsync();
        await using var forged = new MySqlCommand("""
            INSERT INTO c03_07_inbox_receipts
              (scope_kind,scope_id,consumer_name,event_id,processed_at_utc)
            VALUES (1,@platform,@consumer,@tenantEvent,UTC_TIMESTAMP(6))
            """, db);
        forged.Parameters.AddWithValue("@platform", new byte[16]);
        forged.Parameters.AddWithValue("@consumer", PlatformOutboxWorker.InternalConsumer);
        forged.Parameters.AddWithValue("@tenantEvent", tenantEvent.ToByteArray());
        await RejectAsync<MySqlException>(
            async () => { await forged.ExecuteNonQueryAsync(); },
            "Composite FK denies cross-tenant receipt");
    }
    Check(await store.ClaimNextPlatformAsync(Guid.NewGuid(), 2, CancellationToken.None) is null,
        "PLATFORM worker never claims synthetic tenant event");
    Check(await StatusAsync(store, tenantEvent) == "pending",
        "Tenant event untouched by platform worker");
    Check(await ReceiptCountAsync(store, tenantEvent) == 0,
        "Tenant receives no cross-scope receipt");

    var known = NewProbe();
    await AppendAndCommitAsync(store, known);
    Check(await worker.RunBatchAsync(Guid.NewGuid(), 8, CancellationToken.None) == 1,
        "Opt-in worker processes one known platform event");
    Check(await ReceiptCountAsync(store, known.EventId) == 1,
        "Opt-in worker stores one scoped receipt");
    Check(await worker.RunBatchAsync(Guid.NewGuid(), 8, CancellationToken.None) == 0,
        "No work when all permitted platform events are consumed");
}
Console.WriteLine($"PASS C03-07 {args[0]}: {checks} executable assertions.");
return;

internal sealed class FakeRelayPort : IPlatformOutboxRelayPort
{
    public PlatformEventLease? Next { get; set; }
    public int AckCount { get; private set; }
    public int FailCount { get; private set; }
    public Task<PlatformEventLease?> ClaimNextPlatformAsync(
        Guid id, int seconds, CancellationToken ct)
    {
        var item = Next;
        Next = null;
        return Task.FromResult(item);
    }
    public Task<bool> AcknowledgeAsync(
        PlatformEventLease lease, string consumerName, CancellationToken ct)
    {
        AckCount++;
        return Task.FromResult(true);
    }
    public Task<bool> FailAsync(
        PlatformEventLease lease, bool permanently, CancellationToken ct)
    {
        FailCount++;
        return Task.FromResult(true);
    }
}
