using System.Security.Cryptography;
using System.Text.Json;
using Crecisol.Api.Contracts;
using Crecisol.Infrastructure.Identity;
using Crecisol.Domain.Tenancy;
using Microsoft.AspNetCore.Http;

namespace Crecisol.NeutralRegistration.IntegrationTests;

/// <summary>Local test-only transport; never register in the CRECISOL product API.</summary>
internal sealed record LocalProofEnvelope(Guid Intent, string? Code);

internal static class LocalProofHttp
{
    public static void SetNeutralHeaders(HttpContext ctx)
    {
        ctx.Response.Headers.CacheControl = "no-store";
        ctx.Response.Headers.Pragma = "no-cache";
        ctx.Response.Headers["Referrer-Policy"] = "no-referrer";
        ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    }

    public static async Task<LocalProofEnvelope?> ReadAsync(
        HttpRequest request, bool confirm, CancellationToken token)
    {
        if (!NeutralRegistrationTransportPreflight.IsEligible(request) ||
            request.ContentLength > 512)
            return null;

        var bytes = new byte[checked((int)request.ContentLength!.Value)];
        try
        {
            await request.Body.ReadExactlyAsync(bytes.AsMemory(), token);
            if (await request.Body.ReadAsync(new byte[1].AsMemory(), token) != 0)
                return null;
            using var document = JsonDocument.Parse(bytes);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return null;
            var values = document.RootElement.EnumerateObject().ToArray();
            if (values.Length != (confirm ? 2 : 1) ||
                !values[0].NameEquals("intentId") ||
                values[0].Value.ValueKind != JsonValueKind.String ||
                !Guid.TryParseExact(values[0].Value.GetString(), "D", out var intent) ||
                intent == Guid.Empty)
                return null;
            if (!confirm)
                return new LocalProofEnvelope(intent, null);
            if (!values[1].NameEquals("code") ||
                values[1].Value.ValueKind != JsonValueKind.String)
                return null;
            var code = values[1].Value.GetString();
            if (code is null || code.Length != 64 || !code.All(Uri.IsHexDigit))
                return null;
            return new LocalProofEnvelope(intent, code);
        }
        catch (JsonException) { return null; }
        catch (EndOfStreamException) { return null; }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
}

internal sealed class LocalProofMailbox : ITrustedLocalProofLinkSink, IDisposable
{
    private readonly object gate = new();
    private readonly Dictionary<string, (TenantId? Tenant, Guid Intent, byte[] Token)> messages =
        new(StringComparer.Ordinal);
    private int delivered;

    public int Delivered { get { lock (gate) return delivered; } }

    public Task DeliverInTestOnlyAsync(
        string mailbox, ReadOnlyMemory<byte> secret, CancellationToken ct) =>
        StoreAsync(null, Guid.Empty, mailbox, secret, ct);

    public Task DeliverLinkInTestOnlyAsync(
        TenantId tenant, Guid intent, string mailbox,
        ReadOnlyMemory<byte> secret, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(tenant);
        if (intent == Guid.Empty)
            throw new InvalidOperationException("Trusted local link must have an opaque intent.");
        return StoreAsync(tenant, intent, mailbox, secret, ct);
    }

    private Task StoreAsync(
        TenantId? tenant, Guid intent, string mailbox,
        ReadOnlyMemory<byte> secret, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (secret.Length != 32 || string.IsNullOrEmpty(mailbox))
            throw new InvalidOperationException("Invalid disposable proof delivery.");
        var saved = secret.ToArray();
        try
        {
            lock (gate)
            {
                if (messages.ContainsKey(mailbox))
                    throw new InvalidOperationException("Duplicate local mailbox challenge.");
                messages.Add(mailbox, (tenant, intent, saved));
                delivered++;
            }
            return Task.CompletedTask;
        }
        catch
        {
            CryptographicOperations.ZeroMemory(saved);
            throw;
        }
    }

    public bool Has(string mailbox)
    {
        lock (gate) return messages.ContainsKey(mailbox);
    }

    public byte[] Take(string mailbox) => Remove(mailbox).Token;

    public (TenantId Tenant, Guid Intent, byte[] Token) TakeLink(string mailbox)
    {
        var item = Remove(mailbox);
        if (item.Tenant is null || item.Intent == Guid.Empty)
        {
            CryptographicOperations.ZeroMemory(item.Token);
            throw new InvalidOperationException("Local proof was not delivered as a trusted link.");
        }
        return (item.Tenant, item.Intent, item.Token);
    }

    private (TenantId? Tenant, Guid Intent, byte[] Token) Remove(string mailbox)
    {
        lock (gate)
        {
            if (!messages.Remove(mailbox, out var item))
                throw new InvalidOperationException("Proof unavailable in local mailbox.");
            return item;
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            foreach (var item in messages.Values)
                CryptographicOperations.ZeroMemory(item.Token);
            messages.Clear();
        }
    }
}
