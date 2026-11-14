using System.Security.Cryptography;
using System.Text.Json;
using Crecisol.Api.Contracts;
using Crecisol.Infrastructure.Identity;
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

internal sealed class LocalProofMailbox : ITrustedLocalProofMailboxSink, IDisposable
{
    private readonly Dictionary<string, byte[]> messages = new(StringComparer.Ordinal);
    public int Delivered { get; private set; }

    public Task DeliverInTestOnlyAsync(
        string mailbox, ReadOnlyMemory<byte> secret, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (secret.Length != 32 || messages.ContainsKey(mailbox))
            throw new InvalidOperationException("Duplicate or invalid test proof.");
        messages.Add(mailbox, secret.ToArray());
        Delivered++;
        return Task.CompletedTask;
    }

    public bool Has(string mailbox) => messages.ContainsKey(mailbox);

    public byte[] Take(string mailbox)
    {
        if (!messages.Remove(mailbox, out var bytes))
            throw new InvalidOperationException("Local test proof not available.");
        return bytes;
    }

    public void Dispose()
    {
        foreach (var value in messages.Values)
            CryptographicOperations.ZeroMemory(value);
        messages.Clear();
    }
}
