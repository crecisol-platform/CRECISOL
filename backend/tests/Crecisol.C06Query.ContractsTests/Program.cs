using Crecisol.Infrastructure.Identity;
using System.Security.Cryptography;

var now = new DateTimeOffset(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);
var time = now.UtcDateTime.AddMinutes(-2).AddTicks(1234560);
var id = Guid.Parse("11111111-2222-4333-8444-555555555555");
var key = RandomNumberGenerator.GetBytes(32);
const string scope = "host/tenant/actor/member/session/grant/1/1/1/1/1/1";
var count = 0;
void Check(bool ok, string label)
{
    if (!ok) throw new InvalidOperationException("C06-08 contract failed: " + label);
    count++;
}
var cursor = C06PendingCursor.Encode(key, scope, time, id, now);
Check(cursor.Length <= 256, "closed HTTP length");
Check(C06PendingCursor.TryDecode(key, scope, cursor, now, out var t, out var i) && t == time && i == id, "roundtrip microseconds and RFC identifier");
Check(C06PendingCursor.TryDecode(key, scope, cursor, now.AddSeconds(599), out _, out _), "last valid second");
Check(!C06PendingCursor.TryDecode(key, scope, cursor, now.AddSeconds(600), out _, out _), "expiry");
Check(!C06PendingCursor.TryDecode(key, scope, cursor, now.AddSeconds(-1), out _, out _), "future-issued token");
var components = scope.Split('/');
for (var j = 0; j < components.Length; j++)
{
    var changed = components.ToArray();
    changed[j] += "X";
    Check(!C06PendingCursor.TryDecode(key, string.Join("/", changed), cursor, now, out _, out _), "scope component " + j);
}
var pieces = cursor.Split('/');
for (var j = 0; j < pieces.Length; j++)
{
    var changed = pieces.ToArray();
    changed[j] = (pieces[j][0] == '0' ? "1" : "0") + pieces[j][1..];
    Check(!C06PendingCursor.TryDecode(key, scope, string.Join("/", changed), now, out _, out _), "tampered component " + j);
}
foreach (var invalid in new[] { "", "start", "20261125120000000000/" + id.ToString("D"), cursor + "/", cursor[..^1], new string('A', 513), cursor.Replace("/", "//"), cursor[..^1] + "!" })
    Check(!C06PendingCursor.TryDecode(key, scope, invalid, now, out _, out _), "malformed cursor");
Check(!C06PendingCursor.TryDecode(RandomNumberGenerator.GetBytes(32), scope, cursor, now, out _, out _), "key rotation");
Check(!C06PendingCursor.TryDecode(new byte[31], scope, cursor, now, out _, out _), "bad key");
Check(!C06PendingCursor.TryDecode(key, "", cursor, now, out _, out _), "empty scope");
Console.WriteLine("PASS C06-08 continuation contracts=" + count);
