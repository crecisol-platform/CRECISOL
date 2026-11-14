using System.Text.Json;
using Crecisol.Application.Identity;

namespace Crecisol.Api.Contracts;

/// <summary>
/// Validates only the shape of the future anonymous registration-intent JSON.
/// This is not email ownership verification or a tenant/account lookup.
/// No HTTP endpoint currently invokes this parser.
/// </summary>
public static class NeutralRegistrationPayload
{
    public const int MaxBytes = 4096;

    public static bool TryReadCanonicalMailbox(ReadOnlyMemory<byte> utf8Json,
        out string? canonicalMailbox)
    {
        canonicalMailbox = null;
        if (utf8Json.IsEmpty || utf8Json.Length > MaxBytes)
            return false;

        try
        {
            using var document = JsonDocument.Parse(utf8Json,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 4
                });

            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return false;

            // A strict one-field envelope prevents hidden authority selectors,
            // duplicate JSON keys and case-insensitive property confusion.
            var properties = document.RootElement.EnumerateObject();
            using var iterator = properties.GetEnumerator();
            if (!iterator.MoveNext()) return false;
            var first = iterator.Current;
            if (!first.NameEquals("mailbox") ||
                first.Value.ValueKind != JsonValueKind.String ||
                iterator.MoveNext())
                return false;

            var mailbox = RequestNeutralRegistrationIntent.CanonicalMailboxOrNull(
                first.Value.GetString());
            if (mailbox is null) return false;
            canonicalMailbox = mailbox;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
