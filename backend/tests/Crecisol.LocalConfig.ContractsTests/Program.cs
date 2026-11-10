using Crecisol.Api.Configuration;

int checks = 0;
void Check(bool valid, string message)
{
    if (!valid) throw new InvalidOperationException("FAIL C03-08: " + message);
    checks++;
}
void Reject(Action action, string message)
{
    try { action(); }
    catch (InvalidOperationException) { Check(true, message); return; }
    throw new InvalidOperationException("FAIL C03-08: accepted unsafe configuration " + message);
}

string[] local = ["127.0.0.1"];
string[] named = ["localhost"];
string socket = "/tmp/crecisol-ci-test/mysqld.sock";
LocalRuntimeSettings Parse(string? p = "ci", string? bound = "8",
    string? network = "false", string? path = "/tmp/crecisol-ci-test/mysqld.sock",
    string[]? hosts = null) =>
    LocalRuntimeSettings.Parse(p, bound, network, path, hosts ?? local);

var disabled = LocalRuntimeSettings.Parse(null, null, null, null, ["example.org"]);
Check(disabled.Profile == LocalRuntimeProfile.Disabled, "Profile absent is disabled");
Check(disabled.MaxWorkerBatch == 0, "No hidden batch default");
Check(disabled.TestSocketPath is null, "No implicit socket");
Check(!disabled.CanAccessExternalNetwork, "No external network is ever permitted");
var ci = Parse();
Check(ci.Profile == LocalRuntimeProfile.Ci, "CI profile exact allowlist");
Check(ci.MaxWorkerBatch == 8 && ci.TestSocketPath == socket,
    "Typed CI batch and disposable socket");
Check(!ci.CanAccessExternalNetwork, "CI profile cannot enable external traffic");
var dev = Parse(p: "local", bound: "1", hosts: named);
Check(dev.Profile == LocalRuntimeProfile.Local && dev.MaxWorkerBatch == 1,
    "Explicit local profile uses loopback only");
Check(Parse(bound: "32").MaxWorkerBatch == 32, "Maximum bounded batch accepted");
Check(Parse(hosts: ["127.0.0.1", "localhost"]).Profile == LocalRuntimeProfile.Ci,
    "Two explicit loopback host names are valid");
LocalRuntimeSettings.ValidateHostAllowlist(["service.example", "api.internal"]);
Check(true, "Server-only DNS allowlist permits valid institutional hostnames when profile off");

foreach (var (profile, label) in new (string?, string)[]
{
    ("", "empty profile"), ("CI", "wrong case"), ("production", "production profile"),
    ("staging", "staging profile"), ("dev", "unauthorized alias"),
    ("test", "unknown profile"), ("local ", "trailing whitespace"),
    (" ci", "leading whitespace"), ("true", "boolean injection")
})
    Reject(() => Parse(p: profile), label);

Reject(() => LocalRuntimeSettings.Parse(null, "8", null, null, local),
    "Orphaned batch must not enable local mode");
Reject(() => LocalRuntimeSettings.Parse(null, null, "false", null, local),
    "Orphaned network setting must not enable local mode");
Reject(() => LocalRuntimeSettings.Parse(null, null, null, socket, local),
    "Orphaned test socket must not enable local mode");

foreach (var (batch, label) in new (string?, string)[]
{
    (null, "missing batch"), ("", "empty batch"), ("0", "zero batch"),
    ("33", "too many items"), ("-1", "negative batch"), ("01", "leading zero"),
    ("1 ", "space after batch"), ("1.0", "decimal injection"),
    ("1e1", "scientific input"), ("2147483648", "integer overflow"),
    ("８", "full-width digit"), ("1;2", "expression injection")
})
    Reject(() => Parse(bound: batch), label);

foreach (var (network, label) in new (string?, string)[]
{
    (null, "implicit network policy"), ("", "empty network policy"),
    ("true", "external network enabled"), ("False", "case-dependent coercion"),
    ("0", "alternate boolean"), ("false ", "trailing whitespace"),
    ("localhost", "host confusable"), ("false;true", "option injection")
})
    Reject(() => Parse(network: network), label);

foreach (var (path, label) in new (string?, string)[]
{
    (null, "missing socket"), ("", "empty socket"), ("/var/run/mysqld/mysqld.sock", "non disposable socket"),
    ("/tmp/../prod/mysqld.sock", "traversal"), ("/tmp//mysql/mysqld.sock", "ambiguous slash"),
    ("/tmp/a/mysqld.sock?Password=secret", "DSN injection"),
    ("C:\\mysql\\mysqld.sock", "Windows host path"), ("mysql.example:3306", "remote DB"),
    ("/tmp/data/evil.sock", "unapproved socket"), ("/tmp/a/mysqld.sock;", "shell separator"),
    ("/tmp/a/mysqld.sock\n", "newline injection")
})
    Reject(() => Parse(path: path), label);
Reject(() => Parse(path: "/tmp/" + new string('a', 180) + "/mysqld.sock"),
    "Overlong socket path");

foreach (var (host, label) in new (string, string)[]
{
    ("example.com", "local profile with remote hostname"),
    ("0.0.0.0", "wildcard IPv4"), ("8.8.8.8", "external IP"),
    ("127.0.0.1:3306", "port injection"), ("127.0.0.1;example.com", "list injection"),
    ("localhost;evil", "injected host"), ("localhost/", "path injection"),
    ("localhost*", "wildcard host"), ("-localhost", "invalid leading dash"),
    ("localhost.", "trailing dot"), ("a..b", "empty DNS label"),
    ("localhost\\attacker", "slash injection"), ("localhost\nX-Test:1", "header injection")
})
    Reject(() => Parse(hosts: [host]), label);

Reject(() => Parse(hosts: ["localhost", "LOCALHOST"]), "Duplicate case-variant hosts");
Reject(() => Parse(hosts: []), "Empty host allowlist");
Reject(() => Parse(hosts: Enumerable.Range(1, 17)
    .Select(i => "test" + i).ToArray()), "Unbounded host count");
Reject(() => LocalRuntimeSettings.Parse(null, null, null, null,
    ["host..example"]), "Even disabled profiles reject invalid global allowlist");

Console.WriteLine($"PASS C03-08: {checks} executable typed local configuration assertions. " +
    "No secrets, remote DB, workers, tenant selection or productive connections.");
