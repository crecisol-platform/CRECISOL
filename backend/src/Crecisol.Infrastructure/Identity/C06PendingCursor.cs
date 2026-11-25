using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Crecisol.Infrastructure.Identity;

/// <summary>Closed continuation token for the existing pending-membership inbox.
/// Integrity, purpose and scope binding only: the cursor never grants access.
/// No personal data, authority claims or key material are serialized.</summary>
internal static class C06PendingCursor
{
    private const string TimeFormat = "yyyyMMddHHmmssffffff";
    private const long LifetimeSeconds = 600;
    private const string Purpose = "CRECISOL/C06-08/memberships/pending/v1\n";

    private static byte[] Mac(ReadOnlySpan<byte> key, string scope, string body) =>
        HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(Purpose + scope + "\n" + body));

    internal static string Encode(ReadOnlySpan<byte> key, string scope, DateTime time, Guid id, DateTimeOffset now)
    {
        if (key.Length != 32 || string.IsNullOrEmpty(scope) || time.Kind != DateTimeKind.Utc ||
            id == Guid.Empty || time < DateTime.UnixEpoch || time > now.UtcDateTime)
            throw new InvalidOperationException("Invalid continuation context.");
        var expiry = (now.ToUnixTimeSeconds() + LifetimeSeconds).ToString(CultureInfo.InvariantCulture);
        var body = "v1/" + time.ToString(TimeFormat, CultureInfo.InvariantCulture) + "/" + id.ToString("N") + "/" + expiry;
        return body + "/" + Convert.ToHexString(Mac(key, scope, body));
    }

    internal static bool TryDecode(ReadOnlySpan<byte> key, string scope, string cursor, DateTimeOffset now,
        out DateTime time, out Guid id)
    {
        time = default;
        id = default;
        if (key.Length != 32 || string.IsNullOrEmpty(scope) || cursor.Length is < 120 or > 160) return false;
        var parts = cursor.Split('/');
        if (parts.Length != 5 || parts[0] != "v1" || parts[1].Length != 20 ||
            parts[2].Length != 32 || parts[3].Length is < 1 or > 12 ||
            parts[4].Length != 64 || !parts[4].All(Uri.IsHexDigit)) return false;
        var body = cursor[..cursor.LastIndexOf('/')];
        if (!CryptographicOperations.FixedTimeEquals(Mac(key, scope, body), Convert.FromHexString(parts[4]))) return false;
        if (!long.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out var expiry) ||
            expiry <= now.ToUnixTimeSeconds() || expiry > now.ToUnixTimeSeconds() + LifetimeSeconds ||
            !DateTime.TryParseExact(parts[1], TimeFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed) ||
            parsed < DateTime.UnixEpoch || parsed > now.UtcDateTime ||
            !Guid.TryParseExact(parts[2], "N", out var parsedId) || parsedId == Guid.Empty) return false;
        time = parsed;
        id = parsedId;
        return true;
    }
}
