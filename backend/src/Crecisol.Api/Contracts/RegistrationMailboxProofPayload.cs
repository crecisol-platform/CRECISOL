using System.Text.Json;

namespace Crecisol.Api.Contracts;

/// <summary>Strict bounded input for one-time registration proof confirmation.</summary>
public sealed record RegistrationMailboxProofPayload(Guid ProofId, string Code)
{
    public const int MaxBytes = 256;

    public static bool TryParse(ReadOnlyMemory<byte> utf8Json,
        out RegistrationMailboxProofPayload? payload)
    {
        payload = null;
        if (utf8Json.IsEmpty || utf8Json.Length > MaxBytes) return false;
        try
        {
            using var document = JsonDocument.Parse(utf8Json,
                new JsonDocumentOptions { AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow, MaxDepth = 3 });
            if (document.RootElement.ValueKind != JsonValueKind.Object) return false;
            var properties = document.RootElement.EnumerateObject();
            using var iterator = properties.GetEnumerator();
            if (!iterator.MoveNext() || !iterator.Current.NameEquals("proofId") ||
                iterator.Current.Value.ValueKind != JsonValueKind.String ||
                !Guid.TryParseExact(iterator.Current.Value.GetString(), "D", out var proofId) ||
                proofId == Guid.Empty || !iterator.MoveNext()) return false;
            var codeProperty = iterator.Current;
            if (!codeProperty.NameEquals("code") ||
                codeProperty.Value.ValueKind != JsonValueKind.String || iterator.MoveNext())
                return false;
            var code = codeProperty.Value.GetString();
            if (code is null || code.Length != 64 ||
                code.Any(character => !Uri.IsHexDigit(character))) return false;
            payload = new RegistrationMailboxProofPayload(proofId, code);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
