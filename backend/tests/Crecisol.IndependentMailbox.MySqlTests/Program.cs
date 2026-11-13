using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Crecisol.Domain.Identity;
using Crecisol.Domain.Tenancy;
using Crecisol.Infrastructure.Identity;
using Crecisol.Infrastructure.Tenancy;
using MySqlConnector;

// Integration must exercise a separate receiving .NET process over a real
// 127.0.0.1 SMTP TCP socket and a real disposable MySQL8.4 instance.
// This does NOT prove delivery to, or possession of, any external mailbox.
if (args.Length != 1 || args[0] != "--integration")
    throw new InvalidOperationException("Only --integration is supported.");

var setupDsn = Environment.GetEnvironmentVariable("CRECISOL_C05_02_OUTBOX_SETUP_DSN")
    ?? throw new InvalidOperationException("Local setup DSN required.");
var ingestDsn = Environment.GetEnvironmentVariable("CRECISOL_C05_02_OUTBOX_INGEST_DSN")
    ?? throw new InvalidOperationException("Local intake DSN required.");
var workerDsn = Environment.GetEnvironmentVariable("CRECISOL_C05_02_OUTBOX_WORKER_DSN")
    ?? throw new InvalidOperationException("Local worker DSN required.");
var keyHex = Environment.GetEnvironmentVariable("CRECISOL_C05_02_OUTBOX_MASTER_HEX")
    ?? throw new InvalidOperationException("Disposable key required.");
var master = Convert.FromHexString(keyHex);
if (master.Length != 32)
    throw new InvalidOperationException("A 256-bit disposable key is required.");
var config = new MySqlConnectionStringBuilder(setupDsn);
if (config.Database != "c05_02_disposable" ||
    config.Server is not ("127.0.0.1" or "localhost" or "::1"))
    throw new InvalidOperationException("Test must not connect to remote MySQL.");

var receiverDll = Path.GetFullPath(
    "backend/tests/Crecisol.LoopbackSmtpReceiver/bin/Release/net10.0/" +
    "Crecisol.LoopbackSmtpReceiver.dll");
if (!File.Exists(receiverDll))
    throw new InvalidOperationException("Independent receiver must be built first.");
using var receiver = new Process
{
    StartInfo = new ProcessStartInfo("dotnet")
    {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
    }
};
receiver.StartInfo.ArgumentList.Add(receiverDll);
if (!receiver.Start())
    throw new InvalidOperationException("Independent SMTP test receiver not started.");
try
{
    var ready = await receiver.StandardOutput.ReadLineAsync()
        .WaitAsync(TimeSpan.FromSeconds(15));
    if (ready is null || !ready.StartsWith("READY ", StringComparison.Ordinal) ||
        !int.TryParse(ready[6..], out var port) || port is < 1024 or > 65535)
        throw new InvalidOperationException("Private independent receiver unavailable.");

    var checks = 0;
    void Pass(bool predicate, string message)
    {
        if (!predicate)
            throw new InvalidOperationException("FAIL independent SMTP test: " + message);
        checks++;
        Console.WriteLine("PASS independent SMTP fixture: " + message);
    }

    async Task<byte[]> ReceiveAsync(string expectedMailbox)
    {
        var line = await receiver.StandardOutput.ReadLineAsync()
            .WaitAsync(TimeSpan.FromSeconds(10));
        const string marker = "MAIL ";
        if (line is null ||
            !line.StartsWith(marker + expectedMailbox + " ", StringComparison.Ordinal) ||
            line.Length != marker.Length + expectedMailbox.Length + 1 + 64)
            throw new InvalidOperationException("Independent receiver mailbox mismatch.");
        var secretHex = line[(marker.Length + expectedMailbox.Length + 1)..];
        return Convert.FromHexString(secretHex);
    }

    await using var admin = new MySqlConnection(config.ConnectionString);
    await admin.OpenAsync();
    async Task RunAsync(string sql)
    {
        await using var cmd = new MySqlCommand(sql, admin);
        await cmd.ExecuteNonQueryAsync();
    }
    async Task<int> CountAsync(string sql)
    {
        await using var cmd = new MySqlCommand(sql, admin);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    const string A = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    const string B = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    var tenantA = TenantId.From(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
    var tenantB = TenantId.From(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
    await RunAsync($"""
        INSERT INTO c05_02_tenants VALUES
          (X'{A}','ACTIVE',42),(X'{B}','ACTIVE',42)
        """);

    using var ingester = new MySqlDisposableNeutralRegistrationRecorder(
        ingestDsn, master, 1);
    using var sink = new LoopbackOnlySmtpProofSink(port);
    using var ledgerAndDelivery = new MySqlDisposableEmailPossessionAdapter(
        workerDsn, master, 1, sink);
    using var outbox = new MySqlDisposableProofDeliveryOutbox(
        workerDsn, master, 1, ledgerAndDelivery);

    async Task<Guid> InsertIntentAsync(TenantId tenant, string mailbox)
    {
        await ingester.RecordWithoutEnumerationAsync(
            tenant, mailbox,
            IPAddress.Loopback.MapToIPv6().GetAddressBytes(),
            DateTimeOffset.UtcNow, CancellationToken.None);
        var tenantRaw = MySqlRfc4122Guid.Encode(tenant.Value);
        var fingerprintKey = HMACSHA256.HashData(
            master, "CRECISOL/C05-02/MAILBOX-FINGERPRINT/V1"u8);
        var plain = Encoding.UTF8.GetBytes(mailbox);
        var scoped = new byte[tenantRaw.Length + plain.Length];
        byte[]? fingerprint = null;
        try
        {
            tenantRaw.CopyTo(scoped, 0);
            plain.CopyTo(scoped, tenantRaw.Length);
            fingerprint = HMACSHA256.HashData(fingerprintKey, scoped);
            await using var cmd = new MySqlCommand("""
                SELECT intent_id FROM c05_02_neutral_intents
                 WHERE tenant_id=@tenant AND mailbox_digest=@fingerprint
                """, admin);
            cmd.Parameters.Add("@tenant", MySqlDbType.Binary).Value = tenantRaw;
            cmd.Parameters.Add("@fingerprint", MySqlDbType.Binary).Value = fingerprint;
            var value = await cmd.ExecuteScalarAsync() as byte[];
            return value is null
                ? throw new InvalidOperationException("Stored synthetic intent missing.")
                : MySqlRfc4122Guid.Decode(value);
        }
        finally
        {
            foreach (var buffer in new[] { fingerprintKey, plain, scoped, fingerprint })
                if (buffer is not null) CryptographicOperations.ZeroMemory(buffer);
        }
    }

    var a1 = await InsertIntentAsync(tenantA, "one.a@example.test");
    var b1 = await InsertIntentAsync(tenantB, "one.b@example.test");
    var evil = await InsertIntentAsync(tenantB, "outsider@evil.test");
    var noReceiver = await InsertIntentAsync(tenantA, "undelivered.a@example.test");
    Pass(a1 != b1 && b1 != evil && evil != noReceiver,
        "All opaque intents remain distinct across two tenants");
    Pass(port >= 1024 && !receiver.HasExited,
        "Recipient is an independent process on unprivileged 127.0.0.1 TCP");

    try
    {
        using var _ = new LoopbackOnlySmtpProofSink(25);
        throw new InvalidOperationException("Privileged SMTP port accepted.");
    }
    catch (InvalidOperationException e) when
        (e.Message.StartsWith("Only an unprivileged", StringComparison.Ordinal))
    {
        Pass(true, "Production SMTP port 25 is rejected before connection");
    }

    try
    {
        await sink.DeliverInTestOnlyAsync(
            "other@outside.test", new byte[32], CancellationToken.None);
        throw new InvalidOperationException("External mailbox was accepted.");
    }
    catch (InvalidOperationException e) when
        (e.Message.StartsWith("Only an exact synthetic", StringComparison.Ordinal))
    {
        Pass(true, "External mailbox is denied before opening TCP");
    }
    try
    {
        await sink.DeliverInTestOnlyAsync(
            "one.a@example.test\r\nBcc:other@example.test",
            new byte[32], CancellationToken.None);
        throw new InvalidOperationException("Header injection was accepted.");
    }
    catch (InvalidOperationException e) when
        (e.Message.StartsWith("Only an exact synthetic", StringComparison.Ordinal))
    {
        Pass(true, "Header injection cannot change SMTP envelope");
    }

    Pass(await outbox.StageOnceAsync(tenantA,a1,DateTimeOffset.UtcNow,
        CancellationToken.None), "Tenant A stages encrypted one-time proof");
    Pass(!await outbox.StageOnceAsync(tenantB,a1,DateTimeOffset.UtcNow,
        CancellationToken.None), "Tenant B cannot re-stage A intent");
    Pass(await CountAsync($"""
        SELECT COUNT(*) FROM c05_02_mailbox_proofs
         WHERE tenant_id=X'{A}' AND lifecycle='PENDING'
        """)==1,
        "A proof remains unverified until recipient returns the correct code");

    Pass(await outbox.ProcessOnceAsync(tenantA,a1,CancellationToken.None),
        "MySQL-leased worker sends real SMTP DATA to separate receiver process");
    var aCode = await ReceiveAsync("one.a@example.test");
    try
    {
        Pass(aCode.Length==32,
            "Independent receiver obtained the 256-bit code through SMTP TCP");
        var wrong = RandomNumberGenerator.GetBytes(32);
        try
        {
            var wrongDigest = EmailPossessionDigest.FromToken(tenantA,a1,wrong);
            Pass(!await ledgerAndDelivery.TryVerifyOnceAsync(
                tenantA,a1,wrongDigest,DateTimeOffset.UtcNow,CancellationToken.None),
                "Possessing only the address does not verify the channel");
        }
        finally { CryptographicOperations.ZeroMemory(wrong); }
        var validA = EmailPossessionDigest.FromToken(tenantA,a1,aCode);
        Pass(!await ledgerAndDelivery.TryVerifyOnceAsync(
            tenantB,a1,EmailPossessionDigest.FromToken(tenantB,a1,aCode),
            DateTimeOffset.UtcNow,CancellationToken.None),
            "Same code cannot verify the wrong tenant");
        Pass(await ledgerAndDelivery.TryVerifyOnceAsync(
            tenantA,a1,validA,DateTimeOffset.UtcNow,CancellationToken.None),
            "Returned receiver challenge verifies exact tenant and intent once");
        Pass(!await ledgerAndDelivery.TryVerifyOnceAsync(
            tenantA,a1,validA,DateTimeOffset.UtcNow,CancellationToken.None),
            "An already-used challenge cannot be replayed");
        Pass(!await outbox.ProcessOnceAsync(tenantA,a1,CancellationToken.None),
            "A SENT outbox cannot dispatch a replay");
    }
    finally { CryptographicOperations.ZeroMemory(aCode); }

    Pass(await outbox.StageOnceAsync(tenantB,b1,DateTimeOffset.UtcNow,
        CancellationToken.None), "Tenant B independently stages its mailbox proof");
    Pass(await outbox.ProcessOnceAsync(tenantB,b1,CancellationToken.None),
        "Tenant B SMTP message reaches its own receiver inbox");
    var bCode = await ReceiveAsync("one.b@example.test");
    try
    {
        Pass(!await ledgerAndDelivery.TryVerifyOnceAsync(
            tenantA,b1,EmailPossessionDigest.FromToken(tenantA,b1,bCode),
            DateTimeOffset.UtcNow,CancellationToken.None),
            "Tenant B code cannot verify under tenant A");
        Pass(await ledgerAndDelivery.TryVerifyOnceAsync(
            tenantB,b1,EmailPossessionDigest.FromToken(tenantB,b1,bCode),
            DateTimeOffset.UtcNow,CancellationToken.None),
            "Tenant B verified only after presenting code from its receiver");
    }
    finally { CryptographicOperations.ZeroMemory(bCode); }

    Pass(await outbox.StageOnceAsync(tenantB,evil,DateTimeOffset.UtcNow,
        CancellationToken.None),
        "An otherwise valid outside-domain intent can stage neutrally");
    Pass(!await outbox.ProcessOnceAsync(tenantB,evil,CancellationToken.None),
        "Test-only sender rejects a stored address outside example.test");
    Pass(await CountAsync($"""
        SELECT COUNT(*) FROM c05_02_mailbox_proofs
         WHERE tenant_id=X'{B}' AND lifecycle='REVOKED'
        """)==1,
        "Rejected SMTP destination atomically revokes its pending challenge");

    Pass(await outbox.StageOnceAsync(tenantA,noReceiver,DateTimeOffset.UtcNow,
        CancellationToken.None),
        "Additional opaque intent is staged for unavailable receiver test");
    receiver.Kill(entireProcessTree: true);
    await receiver.WaitForExitAsync();
    Pass(!await outbox.ProcessOnceAsync(tenantA,noReceiver,CancellationToken.None),
        "Unavailable independent SMTP receiver fails delivery closed");
    Pass(await CountAsync($"""
        SELECT COUNT(*) FROM c05_02_mailbox_proofs
         WHERE tenant_id=X'{A}' AND lifecycle='REVOKED'
        """)==1,
        "A failed socket delivery cannot leave its challenge usable");

    Pass(await CountAsync($"""
        SELECT COUNT(*) FROM information_schema.TABLES
         WHERE TABLE_SCHEMA='c05_02_disposable'
           AND TABLE_NAME IN ('users','memberships','sessions')
        """)==0,
        "Neither SMTP receipt nor proof verification creates identity, role or session");
    Pass(await CountAsync("""
        SELECT COUNT(*) FROM information_schema.COLUMNS
         WHERE TABLE_SCHEMA='c05_02_disposable'
           AND TABLE_NAME='c05_02_delivery_outbox'
           AND COLUMN_NAME IN ('mailbox','email','raw_token','recipient_address')
        """)==0,
        "Delivery outbox has no plaintext recipient or bearer column");
    Console.WriteLine($"PASS C05-02 independent loopback SMTP + .NET10/MySQL8.4: " +
        $"{checks} real assertions, no external SMTP, membership or session");
}
finally
{
    if (!receiver.HasExited)
    {
        receiver.Kill(entireProcessTree: true);
        await receiver.WaitForExitAsync();
    }
    CryptographicOperations.ZeroMemory(master);
}
