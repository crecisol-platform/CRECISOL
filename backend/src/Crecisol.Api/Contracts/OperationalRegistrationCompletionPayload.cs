using System.Text.Json;
using Crecisol.Application.Identity;

namespace Crecisol.Api.Contracts;

/// <summary>Bounded, exact JSON for mailbox proof plus pending account creation.</summary>
public sealed record OperationalRegistrationCompletionPayload(
    Guid ProofId, string Code, string Mailbox, string Password)
{
    public const int MaxBytes = 2048;

    public static bool TryParse(ReadOnlyMemory<byte> utf8Json,
        out OperationalRegistrationCompletionPayload? payload)
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
                codeProperty.Value.ValueKind != JsonValueKind.String || !iterator.MoveNext())
                return false;
            var mailboxProperty = iterator.Current;
            if (!mailboxProperty.NameEquals("mailbox") ||
                mailboxProperty.Value.ValueKind != JsonValueKind.String || !iterator.MoveNext())
                return false;
            var passwordProperty = iterator.Current;
            if (!passwordProperty.NameEquals("password") ||
                passwordProperty.Value.ValueKind != JsonValueKind.String || iterator.MoveNext())
                return false;

            var code = codeProperty.Value.GetString();
            var mailbox = mailboxProperty.Value.GetString();
            var password = passwordProperty.Value.GetString();
            var canonicalMailbox = RequestNeutralRegistrationIntent
                .CanonicalMailboxOrNull(mailbox);
            if (code is null || code.Length != 64 || !code.All(Uri.IsHexDigit) ||
                mailbox is null || canonicalMailbox != mailbox ||
                password is null || password.Length is < 15 or > 128 ||
                password.Any(char.IsControl))
                return false;

            payload = new OperationalRegistrationCompletionPayload(
                proofId, code, mailbox, password);
            return true;
        }
        catch (JsonException) { return false; }
    }
}
