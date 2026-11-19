using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Crecisol.Application.Identity;
using Crecisol.CredentialSecurity;

// Offline, synthetic-only provisioning BUNDLE generator. No connection, SQL,
// account write, default credential, public route, or production operator.
// stdin/stdout are private pipes in the harness; never copy this JSON to logs.
// Offline recovery ONLY for synthetic operators invalidated by C05-017.
// This tool does NOT write a database or grant access. A separate privileged,
// local one-time maintenance transaction must match BOTH ID and login digest.
if (args.Length == 1 && args[0] == "--isolated-existing-factor-bundle")
{
    byte[]? key = null, tokenKey = null, actor = null, seed = null;
    try
    {
        var raw = await Console.In.ReadToEndAsync();
        if (raw.Length > 2048) return 2;
        var input = JsonSerializer.Deserialize<ExistingFactorInput>(raw)!;
        var mailbox = CanonicalLoginHandle.Parse(input.Mailbox).Value;
        if (!mailbox.EndsWith("@example.test", StringComparison.Ordinal) ||
            input.OperatorId.Length != 32 ||
            !input.OperatorId.All(Uri.IsHexDigit)) return 2;
        actor = Convert.FromHexString(input.OperatorId);
        if (actor.Length != 16 || actor.All(x => x == 0)) return 2;
        var id = Convert.ToHexString(actor);
        tokenKey = Convert.FromHexString(Environment.GetEnvironmentVariable("CRECISOL_C05_TOKEN_HMAC_KEY") ?? "");
        key = Convert.FromHexString(Environment.GetEnvironmentVariable("CRECISOL_C05_FACTOR_ENCRYPTION_KEY") ?? "");
        if (tokenKey.Length != 32 || key.Length != 32) return 2;
        seed = RandomNumberGenerator.GetBytes(20);
        var envelope = new byte[seed.Length + 28];
        RandomNumberGenerator.Fill(envelope.AsSpan(0, 12));
        using (var aes = new AesGcm(key, 16))
            aes.Encrypt(envelope.AsSpan(0, 12), seed, envelope.AsSpan(28),
                envelope.AsSpan(12, 16),
                Encoding.UTF8.GetBytes("C05/operational/v1/platform-factor:" + id));
        var hash = Convert.ToHexString(HMACSHA256.HashData(tokenKey,
            Encoding.UTF8.GetBytes("C05/platform-login:" + mailbox)));
        var backups = Enumerable.Range(0, 10)
            .Select(_ => Convert.ToHexString(RandomNumberGenerator.GetBytes(16))).ToArray();
        var backupHashes = backups.Select(code => Convert.ToHexString(
            HMACSHA256.HashData(tokenKey, Encoding.UTF8.GetBytes(
                "C05/platform-backup:" + id + ":" + code)))).ToArray();
        await Console.Out.WriteAsync(JsonSerializer.Serialize(new {
            id, hash, envelope=Convert.ToHexString(envelope), seed=Base32(seed),
            backups, backupHashes, algorithm="SHA1"
        }));
        CryptographicOperations.ZeroMemory(envelope);
        return 0;
    }
    catch { await Console.Error.WriteLineAsync("Offline synthetic factor bundle declined."); return 2; }
    finally {
        foreach (var b in new[] { key, tokenKey, actor, seed })
            if (b is not null) CryptographicOperations.ZeroMemory(b);
    }
}
if (args.Length != 1 || args[0] != "--isolated-test-bundle") return 2;
try
{
    var raw = await Console.In.ReadToEndAsync();
    if (raw.Length > 2048) return 2;
    var input = JsonSerializer.Deserialize<BundleInput>(raw)!;
    var mailbox = CanonicalLoginHandle.Parse(input.Mailbox).Value;
    if (!mailbox.EndsWith("@example.test", StringComparison.Ordinal)) return 2;
    var tokenKey = Convert.FromHexString(Environment.GetEnvironmentVariable("CRECISOL_C05_TOKEN_HMAC_KEY") ?? "");
    var key = Convert.FromHexString(Environment.GetEnvironmentVariable("CRECISOL_C05_FACTOR_ENCRYPTION_KEY") ?? "");
    var loginKey = Convert.FromHexString(Environment.GetEnvironmentVariable("CRECISOL_C05_LOGIN_LOOKUP_HMAC_KEY") ?? "");
    if (tokenKey.Length != 32 || key.Length != 32 || loginKey.Length != 32) return 2;
    var actor = Guid.NewGuid().ToByteArray(true); var id = Convert.ToHexString(actor);
    var phc = new Argon2idPasswordHasher(PasswordHashingPolicy.Default, new KnownCompromisedPasswordScreen())
        .CreateVerifier(input.Password).ExportForTrustedCredentialStore();
    var seed = RandomNumberGenerator.GetBytes(20); var envelope = new byte[seed.Length + 28];
    RandomNumberGenerator.Fill(envelope.AsSpan(0, 12));
    using (var aes = new AesGcm(key, 16)) aes.Encrypt(envelope.AsSpan(0, 12), seed, envelope.AsSpan(28), envelope.AsSpan(12, 16),
        Encoding.UTF8.GetBytes("C05/operational/v1/platform-factor:" + id));
    var material = Encoding.UTF8.GetBytes("C05/platform-login:" + mailbox);
    var hash = Convert.ToHexString(HMACSHA256.HashData(tokenKey, material));
    var conflict = Convert.ToHexString(HMACSHA256.HashData(loginKey, Encoding.UTF8.GetBytes(mailbox)));
    var contactPlain = Encoding.UTF8.GetBytes(mailbox); var contact = new byte[contactPlain.Length + 28];
    RandomNumberGenerator.Fill(contact.AsSpan(0, 12));
    using (var aes = new AesGcm(key, 16)) aes.Encrypt(contact.AsSpan(0, 12), contactPlain, contact.AsSpan(28), contact.AsSpan(12, 16),
        Encoding.UTF8.GetBytes("C05/operational/v1/platform-contact:" + id));
    var backups = Enumerable.Range(0, 10).Select(_ => Convert.ToHexString(RandomNumberGenerator.GetBytes(16))).ToArray();
    var backupHashes = backups.Select(b => Convert.ToHexString(HMACSHA256.HashData(tokenKey, Encoding.UTF8.GetBytes("C05/platform-backup:" + id + ":" + b)))).ToArray();
    await Console.Out.WriteAsync(JsonSerializer.Serialize(new { id, hash, conflict, phc, envelope = Convert.ToHexString(envelope), contact = Convert.ToHexString(contact), seed = Base32(seed), backups, backupHashes }));
    foreach (var secret in new[] { key, tokenKey, loginKey, seed }) CryptographicOperations.ZeroMemory(secret);
    return 0;
}
catch { await Console.Error.WriteLineAsync("Isolated provisioning bundle declined."); return 2; }

static string Base32(byte[] bytes)
{
    const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
    var text = new StringBuilder(); int value = 0, bits = 0;
    foreach (var b in bytes) { value = (value << 8) | b; bits += 8; while (bits >= 5) { text.Append(chars[(value >> (bits - 5)) & 31]); bits -= 5; } }
    if (bits > 0) text.Append(chars[(value << (5 - bits)) & 31]);
    return text.ToString();
}
internal sealed record BundleInput(string Mailbox, string Password);

internal sealed record ExistingFactorInput(string Mailbox, string OperatorId);
