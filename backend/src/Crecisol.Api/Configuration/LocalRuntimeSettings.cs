using System.Globalization;

namespace Crecisol.Api.Configuration;

/// <summary>
/// C03-08 technical configuration boundary. This is NOT a deployment manager,
/// tenant resolver, secret store, connection selector or worker activation.
/// Disabled by default; local/CI profiles never grant network privileges.
/// </summary>
public enum LocalRuntimeProfile : byte
{
    Disabled = 0,
    Local = 1,
    Ci = 2
}

/// <summary>
/// Typed, immutable local-only settings loaded exclusively at the server
/// composition root. Never read from request, Host, claims, tenant or headers.
/// Secrets and DB connection strings must NEVER be stored here or logged.
/// </summary>
public sealed class LocalRuntimeSettings
{
    private LocalRuntimeSettings(LocalRuntimeProfile profile, int maxWorkerBatch,
        string? testSocketPath)
    {
        Profile = profile;
        MaxWorkerBatch = maxWorkerBatch;
        TestSocketPath = testSocketPath;
    }

    public LocalRuntimeProfile Profile { get; }
    public int MaxWorkerBatch { get; }
    public string? TestSocketPath { get; }
    public bool CanAccessExternalNetwork => false;

    public static void ValidateHostAllowlist(IReadOnlyCollection<string> hosts)
    {
        ArgumentNullException.ThrowIfNull(hosts);
        if (hosts.Count is < 1 or > 16)
            throw new InvalidOperationException("A bounded explicit host allowlist is required.");

        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var host in hosts)
        {
            if (string.IsNullOrEmpty(host) || host.Length > 253 ||
                !string.Equals(host, host.Trim(), StringComparison.Ordinal) ||
                !unique.Add(host))
                throw new InvalidOperationException("Invalid or repeated host allowlist entry.");
            foreach (var label in host.Split('.'))
            {
                if (label.Length is < 1 or > 63 || label[0] == '-' ||
                    label[^1] == '-' || !label.All(c =>
                        c is >= 'a' and <= 'z' or >= 'A' and <= 'Z'
                            or >= '0' and <= '9' or '-'))
                    throw new InvalidOperationException("Unsafe host allowlist entry.");
            }
        }
    }

    public static LocalRuntimeSettings Parse(
        string? profile, string? maxWorkerBatch, string? allowExternalNetwork,
        string? testSocketPath, IReadOnlyCollection<string> allowedHosts)
    {
        ValidateHostAllowlist(allowedHosts);
        if (profile is null)
        {
            // Missing profile cannot partially activate any local configuration.
            if (maxWorkerBatch is not null || allowExternalNetwork is not null ||
                testSocketPath is not null)
                throw new InvalidOperationException("Local options require an explicit profile.");
            return new LocalRuntimeSettings(LocalRuntimeProfile.Disabled, 0, null);
        }

        LocalRuntimeProfile parsedProfile = profile switch
        {
            "local" => LocalRuntimeProfile.Local,
            "ci" => LocalRuntimeProfile.Ci,
            _ => throw new InvalidOperationException("Unsupported local runtime profile.")
        };

        if (allowedHosts.Any(host =>
            !string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Local runtime cannot trust non-loopback hosts.");

        if (allowExternalNetwork != "false")
            throw new InvalidOperationException("Network access must remain explicitly disabled.");

        if (maxWorkerBatch is null ||
            !int.TryParse(maxWorkerBatch, NumberStyles.None,
                CultureInfo.InvariantCulture, out var bound) ||
            bound is < 1 or > 32 ||
            maxWorkerBatch != bound.ToString(CultureInfo.InvariantCulture))
            throw new InvalidOperationException("Local batch must be canonical and bounded 1..32.");

        if (string.IsNullOrEmpty(testSocketPath) || testSocketPath.Length > 180 ||
            !testSocketPath.StartsWith("/tmp/", StringComparison.Ordinal) ||
            !testSocketPath.EndsWith("/mysqld.sock", StringComparison.Ordinal) ||
            testSocketPath.Contains("..", StringComparison.Ordinal) ||
            testSocketPath.Contains("//", StringComparison.Ordinal) ||
            !testSocketPath.All(c =>
                c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9'
                    or '/' or '.' or '_' or '-'))
            throw new InvalidOperationException("Only a bounded disposable Unix socket is permitted.");

        // The path is validated but deliberately not used to make a connection.
        // Local-only MySQL integration is exercised by an isolated CI harness.
        return new LocalRuntimeSettings(parsedProfile, bound, testSocketPath);
    }
}
