using System.Buffers.Binary;
using System.Data;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Crecisol.Application.Identity;
using Crecisol.Domain.Identity;
using Crecisol.Domain.Tenancy;
using Crecisol.Infrastructure.Tenancy;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace Crecisol.Infrastructure.Identity;

/// <summary>
/// Loopback-only C05 proof dispatcher for the disposable local runtime.
/// It decrypts only claimed rows, sends only to example.test through an
/// independent loopback SMTP receiver and never logs or returns the code.
/// This is not a production mail provider.
/// </summary>
public sealed class MySqlLocalProofOutboxDispatcher : IDisposable
{
    private readonly string dsn;
    private readonly int activeKeyVersion;
    private readonly byte[] mailboxEncryptionKey;
    private readonly byte[] outboxEncryptionKey;
    private readonly LoopbackOnlySmtpProofSink sink;
    private readonly ILogger<MySqlLocalProofOutboxDispatcher> logger;
    private bool disposed;

    public MySqlLocalProofOutboxDispatcher(
        string? trustedLocalDsn, int activeKeyVersion,
        ReadOnlySpan<byte> mailboxEncryptionKey,
        ReadOnlySpan<byte> outboxEncryptionKey, int smtpPort,
        ILogger<MySqlLocalProofOutboxDispatcher> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        if (string.IsNullOrWhiteSpace(trustedLocalDsn) || activeKeyVersion < 1 ||
            mailboxEncryptionKey.Length != 32 || outboxEncryptionKey.Length != 32)
            throw new InvalidOperationException("Local mailbox dispatch configuration is invalid.");
        var options = new MySqlConnectionStringBuilder(trustedLocalDsn)
        {
            Pooling = false, ConnectionTimeout = 3, DefaultCommandTimeout = 8,
            AllowLoadLocalInfile = false
        };
        if (options.Server is not ("127.0.0.1" or "localhost" or "::1") ||
            options.Database != "crecisol_c05")
            throw new InvalidOperationException("Mailbox dispatcher requires isolated loopback MySQL.");
        dsn = options.ConnectionString;
        this.activeKeyVersion = activeKeyVersion;
        this.mailboxEncryptionKey = mailboxEncryptionKey.ToArray();
        this.outboxEncryptionKey = outboxEncryptionKey.ToArray();
        sink = new LoopbackOnlySmtpProofSink(smtpPort);
        this.logger = logger;
    }

    public async Task RunAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = await ProcessOneAsync(stoppingToken);
            if (!processed)
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            else
                await Task.Delay(TimeSpan.FromMilliseconds(100), stoppingToken);
        }
    }

    private async Task<bool> ProcessOneAsync(CancellationToken ct)
    {
        var leaseId = Guid.NewGuid();
        byte[]? owner = null;
        ClaimedEnvelope? envelope = null;
        byte[]? mailbox = null;
        byte[]? token = null;
        byte[]? mailboxAad = null;
        byte[]? outboxAad = null;
        byte[]? computedDigest = null;
        byte[]? completionTenant = null;
        byte[]? completionProof = null;
        byte[]? completionOwner = null;
        var sent = false;
        try
        {
            owner = MySqlRfc4122Guid.Encode(leaseId);
            envelope = await ClaimAsync(owner, ct);
            if (envelope is null) return false;
            completionTenant = envelope.Tenant.ToArray();
            completionProof = envelope.Proof.ToArray();
            completionOwner = owner.ToArray();

            if (envelope.MailboxKeyVersion != activeKeyVersion ||
                envelope.OutboxKeyVersion != activeKeyVersion ||
                envelope.Tenant.Length != 16 || envelope.Proof.Length != 16 ||
                envelope.MailboxDigest.Length != 32 || envelope.ProofDigest.Length != 32 ||
                envelope.MailboxNonce.Length != 12 || envelope.MailboxTag.Length != 16 ||
                envelope.MailboxCiphertext.Length is < 6 or > 254 ||
                envelope.OutboxNonce.Length != 12 || envelope.OutboxTag.Length != 16 ||
                envelope.OutboxCiphertext.Length != 32)
                throw new InvalidOperationException("Claimed mailbox envelope is invalid.");

            var tenantId = TenantId.From(MySqlRfc4122Guid.Decode(envelope.Tenant));
            var proofId = MySqlRfc4122Guid.Decode(envelope.Proof);
            mailbox = new byte[envelope.MailboxCiphertext.Length];
            mailboxAad = CreateMailboxAad(
                envelope.Tenant, envelope.MailboxDigest, activeKeyVersion);
            using (var aes = new AesGcm(mailboxEncryptionKey, 16))
                aes.Decrypt(envelope.MailboxNonce, envelope.MailboxCiphertext,
                    envelope.MailboxTag, mailbox, mailboxAad);
            var recipient = new UTF8Encoding(false, true).GetString(mailbox);
            if (!string.Equals(RequestNeutralRegistrationIntent.CanonicalMailboxOrNull(recipient),
                    recipient, StringComparison.Ordinal) ||
                !recipient.EndsWith("@example.test", StringComparison.Ordinal))
                throw new InvalidOperationException("Claimed mailbox is outside the local test domain.");

            token = new byte[32];
            outboxAad = CreateOutboxAad(
                envelope.Tenant, envelope.Proof, envelope.ProofDigest, activeKeyVersion);
            using (var aes = new AesGcm(outboxEncryptionKey, 16))
                aes.Decrypt(envelope.OutboxNonce, envelope.OutboxCiphertext,
                    envelope.OutboxTag, token, outboxAad);
            computedDigest = EmailPossessionDigest.FromToken(tenantId, proofId, token)
                .CopyForProtectedDatabase();
            if (!CryptographicOperations.FixedTimeEquals(computedDigest, envelope.ProofDigest))
                throw new InvalidOperationException("Claimed proof digest does not match its token.");

            await sink.DeliverInTestOnlyAsync(recipient, token, ct);
            sent = true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            if (envelope is not null && owner is not null)
                await BestEffortCompleteAsync(envelope.Tenant, envelope.Proof, owner, false);
            foreach (var bytes in new[] { completionTenant, completionProof, completionOwner })
                if (bytes is not null) CryptographicOperations.ZeroMemory(bytes);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning("Local mailbox dispatch attempt failed ({ErrorType}).", ex.GetType().Name);
        }
        finally
        {
            foreach (var bytes in new[] { owner, mailbox, token, mailboxAad,
                         outboxAad, computedDigest })
                if (bytes is not null) CryptographicOperations.ZeroMemory(bytes);
            envelope?.Clear();
        }

        try
        {
            if (completionTenant is not null && completionProof is not null && completionOwner is not null)
                await BestEffortCompleteAsync(completionTenant, completionProof, completionOwner, sent);
            return completionTenant is not null;
        }
        finally
        {
            foreach (var bytes in new[] { completionTenant, completionProof, completionOwner })
                if (bytes is not null) CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private async Task<ClaimedEnvelope?> ClaimAsync(byte[] owner, CancellationToken ct)
    {
        await using var db = await OpenAsync(ct);
        await using var command = new MySqlCommand(
            "c05_operational_claim_mailbox_proof_outbox", db)
        { CommandType = CommandType.StoredProcedure };
        command.Parameters.Add("p_owner", MySqlDbType.Binary).Value = owner;
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            throw new InvalidOperationException("Outbox claim result is missing.");
        var result = reader.GetString(0);
        if (result == "EMPTY") return null;
        if (result != "CLAIMED")
            throw new InvalidOperationException("Outbox claim was denied.");
        if (!await reader.NextResultAsync(ct) || !await reader.ReadAsync(ct))
            throw new InvalidOperationException("Claimed outbox envelope is missing.");
        var envelope = new ClaimedEnvelope(
            (byte[])reader.GetValue(0), (byte[])reader.GetValue(1),
            (byte[])reader.GetValue(2), (byte[])reader.GetValue(3),
            (byte[])reader.GetValue(4), (byte[])reader.GetValue(5),
            (byte[])reader.GetValue(6), reader.GetInt32(7),
            (byte[])reader.GetValue(8), (byte[])reader.GetValue(9),
            (byte[])reader.GetValue(10), reader.GetInt32(11));
        if (await reader.ReadAsync(ct))
        {
            envelope.Clear();
            throw new InvalidOperationException("Duplicate outbox claim.");
        }
        return envelope;
    }

    private async Task BestEffortCompleteAsync(
        byte[] tenant, byte[] proof, byte[] owner, bool delivered)
    {
        try
        {
            await using var db = await OpenAsync(CancellationToken.None);
            await using var command = new MySqlCommand(
                "c05_operational_complete_mailbox_proof_delivery", db)
            { CommandType = CommandType.StoredProcedure };
            command.Parameters.Add("p_tenant", MySqlDbType.Binary).Value = tenant;
            command.Parameters.Add("p_proof", MySqlDbType.Binary).Value = proof;
            command.Parameters.Add("p_owner", MySqlDbType.Binary).Value = owner;
            command.Parameters.Add("p_delivered", MySqlDbType.Bool).Value = delivered;
            var result = await command.ExecuteScalarAsync(CancellationToken.None) as string;
            if (result is not ("DELIVERED" or "RETRY" or "REVOKED" or "DENIED"))
                throw new InvalidOperationException("Outbox completion result is invalid.");
        }
        catch (Exception ex)
        {
            logger.LogWarning("Local mailbox lease completion failed ({ErrorType}).",
                ex.GetType().Name);
        }
    }

    private async Task<MySqlConnection> OpenAsync(CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var db = new MySqlConnection(dsn);
        try { await db.OpenAsync(ct); return db; }
        catch { await db.DisposeAsync(); throw; }
    }

    private static byte[] CreateMailboxAad(
        ReadOnlySpan<byte> tenant, ReadOnlySpan<byte> digest, int keyVersion)
    {
        var aad = new byte[52];
        tenant.CopyTo(aad);
        BinaryPrimitives.WriteInt32BigEndian(aad.AsSpan(16, 4), keyVersion);
        digest.CopyTo(aad.AsSpan(20));
        return aad;
    }

    private static byte[] CreateOutboxAad(ReadOnlySpan<byte> tenant,
        ReadOnlySpan<byte> proof, ReadOnlySpan<byte> digest, int keyVersion)
    {
        var purpose = "CRECISOL/C05/registration-proof-outbox/v1\0"u8;
        var aad = new byte[purpose.Length + 16 + 16 + 4 + 32];
        purpose.CopyTo(aad);
        tenant.CopyTo(aad.AsSpan(purpose.Length, 16));
        proof.CopyTo(aad.AsSpan(purpose.Length + 16, 16));
        BinaryPrimitives.WriteInt32BigEndian(aad.AsSpan(purpose.Length + 32, 4), keyVersion);
        digest.CopyTo(aad.AsSpan(purpose.Length + 36, 32));
        return aad;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        CryptographicOperations.ZeroMemory(mailboxEncryptionKey);
        CryptographicOperations.ZeroMemory(outboxEncryptionKey);
    }

    private sealed class ClaimedEnvelope(
        byte[] tenant, byte[] proof, byte[] mailboxDigest, byte[] proofDigest,
        byte[] mailboxCiphertext, byte[] mailboxNonce, byte[] mailboxTag,
        int mailboxKeyVersion, byte[] outboxCiphertext, byte[] outboxNonce,
        byte[] outboxTag, int outboxKeyVersion)
    {
        public byte[] Tenant { get; } = tenant;
        public byte[] Proof { get; } = proof;
        public byte[] MailboxDigest { get; } = mailboxDigest;
        public byte[] ProofDigest { get; } = proofDigest;
        public byte[] MailboxCiphertext { get; } = mailboxCiphertext;
        public byte[] MailboxNonce { get; } = mailboxNonce;
        public byte[] MailboxTag { get; } = mailboxTag;
        public int MailboxKeyVersion { get; } = mailboxKeyVersion;
        public byte[] OutboxCiphertext { get; } = outboxCiphertext;
        public byte[] OutboxNonce { get; } = outboxNonce;
        public byte[] OutboxTag { get; } = outboxTag;
        public int OutboxKeyVersion { get; } = outboxKeyVersion;

        public void Clear()
        {
            foreach (var bytes in new[] { Tenant, Proof, MailboxDigest, ProofDigest,
                         MailboxCiphertext, MailboxNonce, MailboxTag,
                         OutboxCiphertext, OutboxNonce, OutboxTag })
                CryptographicOperations.ZeroMemory(bytes);
        }
    }
}
