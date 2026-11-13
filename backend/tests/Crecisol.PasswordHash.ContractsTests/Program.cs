using System.Reflection;
using System.Security.Cryptography;
using Crecisol.Application.Identity;
using Crecisol.Infrastructure.Identity;

// C05-03: REAL Argon2id .NET10 crypto with disposable synthetic candidates.
// NO login, actual user, database, email delivery, session, owner or tenant grant.
var assertions = 0;
void Check(bool condition, string label)
{
    if (!condition)
        throw new InvalidOperationException("FAIL C05-03 password test: " + label);
    assertions++;
}
void Deny<T>(Action action, string label) where T : Exception
{
    try { action(); }
    catch (T) { assertions++; return; }
    throw new InvalidOperationException(
        "FAIL C05-03 expected " + typeof(T).Name + ": " + label);
}

static string RandomCandidate() =>
    Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

var policy = PasswordHashingPolicy.Default;
var screen = new FakeScreen();
var hasher = new Argon2idPasswordHasher(policy, screen);
var candidate = RandomCandidate();
var otherCandidate = RandomCandidate();

Check(policy.MinimumRunes == 15 && policy.MaximumRunes == 128 &&
      policy.MemoryKib == 19456 && policy.Iterations == 2 &&
      policy.Parallelism == 1 && policy.PolicyRevision == 1,
    "Approved baseline and explicit version");
Check(!string.Equals(candidate, otherCandidate, StringComparison.Ordinal),
    "Independent synthetic candidates");
var wrapped = hasher.CreateVerifier(candidate);
var stored = wrapped.ExportForTrustedCredentialStore();
Check(stored.StartsWith("$argon2id$v=19$m=19456,t=2,p=1$", StringComparison.Ordinal),
    "Strong versioned Argon2id PHC parameters encoded");
Check(stored.Length is >= 85 and <= 160 && !stored.Contains(candidate, StringComparison.Ordinal),
    "Stored PHC never contains synthetic plaintext");
Check(hasher.Verify(candidate, stored) == new PasswordHashCheck(true, false),
    "Correct secret verifies cryptographically");
Check(hasher.Verify(otherCandidate, stored) == PasswordHashCheck.Denied,
    "Wrong candidate rejects without grant or rehash");
Check(hasher.Verify(candidate.AsSpan(0, candidate.Length - 1), stored) ==
      PasswordHashCheck.Denied, "Prefix-only mismatch never matches");
Check(hasher.Verify(candidate + "x", stored) == PasswordHashCheck.Denied,
    "Suffix-only mismatch never matches");
Check(hasher.Verify(candidate, null) == PasswordHashCheck.Denied &&
      hasher.Verify(candidate, "") == PasswordHashCheck.Denied,
    "Missing verifier fails closed");
Check(wrapped.ToString() == "PasswordVerifierEnvelope[redacted]" &&
      hasher.ToString() == "Argon2idPasswordHasher[redacted]" &&
      policy.ToString() == "PasswordHashingPolicy[redacted]",
    "No secret / parameters leak via diagnostic ToString");
Check(typeof(PasswordVerifierEnvelope).GetProperties(
      BindingFlags.Public | BindingFlags.Instance).Length == 0,
    "No accidental public serialized credential properties");

// Uniqueness of salts/digests, not deterministic HMAC/SHA256 or a fixed salt.
var another = hasher.CreateVerifier(candidate).ExportForTrustedCredentialStore();
Check(stored != another && stored.Split('$')[4] != another.Split('$')[4],
    "Independent CSPRNG salt for identical candidate");
Check(hasher.Verify(candidate, another).Matched,
    "Same candidate verifies with an independent salt");

// Unicode runes are counted as Unicode scalar values, no trimming/normalizing.
var emojiCandidate = string.Concat(Enumerable.Repeat("🙂", 15));
var emojiVerifier = hasher.CreateVerifier(emojiCandidate)
    .ExportForTrustedCredentialStore();
Check(emojiCandidate.Length == 30 && hasher.Verify(emojiCandidate, emojiVerifier).Matched,
    "15 emoji = 15 runes accepted, no UTF16 truncation");
var spaced = " " + RandomCandidate() + " ";
Check(hasher.Verify(spaced, hasher.CreateVerifier(spaced)
    .ExportForTrustedCredentialStore()).Matched,
    "No whitespace trimming, passphrase managers stay supported");
var maximumCandidate = new string('x', 128);
Check(hasher.Verify(maximumCandidate, hasher.CreateVerifier(maximumCandidate)
    .ExportForTrustedCredentialStore()).Matched,
    "Default 128-rune maximum is accepted");

// Exact malformed PHC rejection, before expensive derivation.
var parts = stored.Split('$');
void RejectStored(string? malformed, string reason) =>
    Check(hasher.Verify(candidate, malformed) == PasswordHashCheck.Denied, reason);
RejectStored("not-a-hash", "Fast hash/plaintext never accepted");
RejectStored(stored.Replace("$argon2id$", "$argon2i$", StringComparison.Ordinal),
    "Argon2i downgrade rejected");
RejectStored(stored.Replace("$v=19$", "$v=16$", StringComparison.Ordinal),
    "Old/nonstandard version rejected");
RejectStored(stored.Replace("m=19456", "m=8192", StringComparison.Ordinal),
    "Weak cost downgraded below minimum rejected");
RejectStored(stored.Replace("m=19456", "m=99999999", StringComparison.Ordinal),
    "Excessive memory/DoS cost rejected");
RejectStored(stored.Replace("t=2", "t=1", StringComparison.Ordinal),
    "Weak iteration downgrade rejected");
RejectStored(stored.Replace("t=2", "t=999999", StringComparison.Ordinal),
    "Excessive iteration/DoS cost rejected");
RejectStored(stored.Replace("p=1", "p=32", StringComparison.Ordinal),
    "Parallelism expansion rejected");
RejectStored(stored.Replace("m=19456", "m=019456", StringComparison.Ordinal),
    "Noncanonical numeric representation rejected");
RejectStored(stored + "$extra", "Additional arbitrary PHC segment rejected");
RejectStored(stored + new string('Q', 200), "Oversized stored payload rejected");
RejectStored(stored.Replace(parts[4], parts[4] + "=", StringComparison.Ordinal),
    "Padded/noncanonical salt rejected");
RejectStored(stored.Replace(parts[5], parts[5] + "=", StringComparison.Ordinal),
    "Padded/noncanonical digest rejected");
RejectStored(stored.Replace(parts[4], "____________", StringComparison.Ordinal),
    "Illegal base64 salt rejected");
var alteredParts = (string[])parts.Clone();
alteredParts[5] = (alteredParts[5][0] == 'A' ? "B" : "A") +
                   alteredParts[5][1..];
RejectStored(string.Join('$', alteredParts), "Tampered derived value rejected");
alteredParts = (string[])parts.Clone();
alteredParts[4] = (alteredParts[4][0] == 'A' ? "B" : "A") +
                   alteredParts[4][1..];
RejectStored(string.Join('$', alteredParts), "Tampered random salt rejected");
RejectStored(stored.Replace("$argon2id$", "$sha256$", StringComparison.Ordinal),
    "No SHA256 fallback");
RejectStored("$argon2id$v=19$m=19456,t=2,p=1$$",
    "Blank hash/salt denied");

// Mandatory breach-screen verdict and refusal when signal absent.
Check(screen.Calls >= 5, "Approved screen invoked on all issued verifiers");
screen.Verdict = PasswordCompromiseVerdict.Compromised;
Deny<InvalidOperationException>(() => hasher.CreateVerifier(RandomCandidate()),
    "Known compromised candidate rejected");
screen.Verdict = PasswordCompromiseVerdict.Unavailable;
Deny<InvalidOperationException>(() => hasher.CreateVerifier(RandomCandidate()),
    "Breach source unavailable fails closed");
screen.Verdict = (PasswordCompromiseVerdict)999;
Deny<InvalidOperationException>(() => hasher.CreateVerifier(RandomCandidate()),
    "Unknown screener outcome fails closed");
screen.Throws = true;
Deny<InvalidOperationException>(() => hasher.CreateVerifier(RandomCandidate()),
    "Breach screen exception fails closed");
screen.Throws = false;
screen.Verdict = PasswordCompromiseVerdict.Allowed;
Deny<ArgumentNullException>(() =>
    new Argon2idPasswordHasher(policy, null!),
    "No missing injected trusted breach screen");
Deny<ArgumentNullException>(() =>
    new Argon2idPasswordHasher(null!, screen),
    "No missing policy");

// Candidate policy fails closed, does not trim, replace invalid UTF16 or log.
Deny<InvalidOperationException>(() => hasher.CreateVerifier(new string('a', 14)),
    "Under minimum 15 runes rejected");
Deny<InvalidOperationException>(() => hasher.CreateVerifier(new string('a', 129)),
    "Over configured max rejected");
Deny<InvalidOperationException>(() => hasher.CreateVerifier(""),
    "Empty password rejected");
Deny<InvalidOperationException>(() => hasher.CreateVerifier(
    RandomCandidate() + "\0"), "NUL injection rejected");
Deny<InvalidOperationException>(() => hasher.CreateVerifier(
    RandomCandidate() + "\n"), "Control injection rejected");
Deny<InvalidOperationException>(() => hasher.CreateVerifier(
    RandomCandidate() + "\uD800"), "Unpaired UTF16 surrogate rejected");
Deny<InvalidOperationException>(() => hasher.CreateVerifier(
    new string('a', 1025)), "Oversized raw input rejected before KDF");

// Server policy cannot downgrade required guardrails.
foreach (var (min, max, memory, iterations, version) in
    new (int, int, int, int, long)[] {
        (14, 128, 19456, 2, 1),
        (15, 63, 19456, 2, 1),
        (70, 64, 19456, 2, 1),
        (15, 257, 19456, 2, 1),
        (15, 128, 18432, 2, 1),
        (15, 128, 20000, 2, 1),
        (15, 128, 66560, 2, 1),
        (15, 128, 19456, 1, 1),
        (15, 128, 19456, 5, 1),
        (15, 128, 19456, 2, 0),
    })
{
    Deny<InvalidOperationException>(() =>
        PasswordHashingPolicy.Create(min, max, memory, iterations, version),
        "Configuration downgrade or excess rejected");
}

// Stronger policy demands rehash upon next trusted credential rotation.
// Verification still accepts the old secret for controlled upgrade; it
// creates NO session and is NEVER enough to identify a tenant.
var newerPolicy = PasswordHashingPolicy.Create(20, 128, 32768, 3, 2);
var newer = new Argon2idPasswordHasher(newerPolicy, new FakeScreen());
Check(newer.Verify(candidate, stored) == new PasswordHashCheck(true, true),
    "Old cost is verifiable but requires stronger rehash");
var newerEnvelope = newer.CreateVerifier(candidate);
var upgraded = newerEnvelope.ExportForTrustedCredentialStore();
Check(upgraded.StartsWith("$argon2id$v=19$m=32768,t=3,p=1$", StringComparison.Ordinal),
    "Versioned policy increases Argon2id work factor");
Check(newer.Verify(candidate, upgraded) == new PasswordHashCheck(true, false),
    "Upgraded PHC verifies and is not marked stale");
Check(hasher.Verify(candidate, upgraded).Matched,
    "Previous policy also verifies stronger (non-downgraded) PHC");
Check(!hasher.Verify(otherCandidate, upgraded).Matched,
    "Wrong candidate still rejected after upgrade");
Check(!newer.Verify(new string('a', 1000), stored).Matched,
    "Invalid verification input always denied");

// Bounded concurrency: no shared salt, no session, no permission minted.
var produced = new string[3];
Parallel.For(0, produced.Length, i =>
{
    produced[i] = new Argon2idPasswordHasher(
        policy, new FakeScreen()).CreateVerifier(candidate)
        .ExportForTrustedCredentialStore();
});
Check(produced.Distinct(StringComparer.Ordinal).Count() == produced.Length,
    "Concurrent requests never reuse a salt");
Check(produced.All(p => hasher.Verify(candidate, p).Matched),
    "Independent concurrent hash outputs validate");

// C05-01 metadata stays non-secret; no account/role/tenant grant in adapter.
Check(typeof(Argon2idPasswordHasher).GetMethods(
        BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance)
    .All(m => !m.Name.Contains("Login", StringComparison.OrdinalIgnoreCase) &&
              !m.Name.Contains("Authorize", StringComparison.OrdinalIgnoreCase) &&
              !m.Name.Contains("Session", StringComparison.OrdinalIgnoreCase)),
    "Hasher exposes no identity/session/tenant authorization API");
Check(!typeof(PasswordVerifierEnvelope).GetProperties(
    BindingFlags.Public | BindingFlags.Instance).Any(
    p => p.Name.Contains("Secret", StringComparison.OrdinalIgnoreCase)),
    "Envelope exposes no original password property");

Console.WriteLine(
    $"PASS CRECISOL C05-03 .NET10 Argon2id: {assertions} real positive/negative assertions");
Console.WriteLine(
    "PASS no plaintext storage; fail-closed compromised password screen; bounded PHC and rehash");
Console.WriteLine(
    "LIMIT: synthetic candidates, no real compromised-password source, login, session or grant");

sealed class FakeScreen : ITrustedCompromisedPasswordScreen
{
    public PasswordCompromiseVerdict Verdict { get; set; } =
        PasswordCompromiseVerdict.Allowed;
    public bool Throws { get; set; }
    public int Calls { get; private set; }

    public PasswordCompromiseVerdict Check(ReadOnlySpan<char> candidate)
    {
        Calls++;
        if (Throws)
            throw new InvalidOperationException("Screen unavailable.");
        // Fake does NOT persist, compare, log or send the candidate anywhere.
        return Verdict;
    }
}
