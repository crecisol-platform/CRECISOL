using System.Net;

namespace Crecisol.Domain.Tenancy;

/// <summary>
/// Canonical ASCII DNS host, without URL, wildcard, path, credentials, IP or
/// port. It is untrusted input until matched against a protected verified
/// domain catalogue. A host NEVER grants tenant membership or permissions.
/// </summary>
public sealed record CanonicalTenantHost
{
    private CanonicalTenantHost(string value) => Value = value;

    public string Value { get; }

    public static CanonicalTenantHost Parse(string? rawHost)
    {
        if (string.IsNullOrEmpty(rawHost) ||
            rawHost.Length is < 4 or > 253 ||
            !string.Equals(rawHost, rawHost.Trim(), StringComparison.Ordinal) ||
            rawHost.Any(c => c < 33 || c > 126) ||
            IPAddress.TryParse(rawHost, out _))
            throw new InvalidOperationException("Invalid tenant DNS host.");

        var labels = rawHost.Split('.');
        if (labels.Length < 2 || labels.Any(label => label.Length is < 1 or > 63 ||
            label[0] == '-' || label[^1] == '-' ||
            !label.All(c => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z'
                or >= '0' and <= '9' or '-')))
            throw new InvalidOperationException("Unsafe tenant DNS labels.");

        var tld = labels[^1];
        if (tld.Length < 2 || !tld.Any(c => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z') ||
            rawHost.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            rawHost.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Unsupported tenant DNS host.");

        // Lowercase is DNS comparison, never a domain ownership claim.
        return new CanonicalTenantHost(rawHost.ToLowerInvariant());
    }

    public override string ToString() => "CanonicalTenantHost[redacted]";
}

public enum HostVerificationState
{
    PendingVerification = 1,
    Verified = 2,
    Suspended = 3,
    Revoked = 4
}

/// <summary>
/// Server catalogue snapshot ONLY; this type cannot perform DNS ownership
/// checks or create a verified domain entry. No authentication or routing
/// decision may be inferred from this object alone.
/// </summary>
public sealed record VerifiedTenantHostBinding
{
    private VerifiedTenantHostBinding(
        TenantId tenantId,
        CanonicalTenantHost host,
        TenantAddressMode addressMode,
        HostVerificationState state,
        long revision,
        DateTimeOffset changedAtUtc,
        DateTimeOffset? verifiedAtUtc,
        DateTimeOffset? validUntilUtc)
    {
        TenantId = tenantId;
        Host = host;
        AddressMode = addressMode;
        State = state;
        Revision = revision;
        ChangedAtUtc = changedAtUtc;
        VerifiedAtUtc = verifiedAtUtc;
        ValidUntilUtc = validUntilUtc;
    }

    public TenantId TenantId { get; }
    public CanonicalTenantHost Host { get; }
    public TenantAddressMode AddressMode { get; }
    public HostVerificationState State { get; }
    public long Revision { get; }
    public DateTimeOffset ChangedAtUtc { get; }
    public DateTimeOffset? VerifiedAtUtc { get; }
    public DateTimeOffset? ValidUntilUtc { get; }

    public static VerifiedTenantHostBinding RestoreFromTrustedCatalog(
        TenantId tenantId,
        CanonicalTenantHost host,
        TenantAddressMode addressMode,
        HostVerificationState state,
        long revision,
        DateTimeOffset changedAtUtc,
        DateTimeOffset? verifiedAtUtc,
        DateTimeOffset? validUntilUtc)
    {
        ArgumentNullException.ThrowIfNull(tenantId);
        ArgumentNullException.ThrowIfNull(host);
        if (addressMode is not (TenantAddressMode.Subdomain or TenantAddressMode.CustomDomain))
            throw new InvalidOperationException("Only named verified hosts are supported.");
        if (!Enum.IsDefined(state) || revision <= 0)
            throw new InvalidOperationException("Unknown verification state or revision.");
        if (changedAtUtc == default || changedAtUtc.Offset != TimeSpan.Zero)
            throw new InvalidOperationException("Verification change must have UTC time.");
        if (verifiedAtUtc.HasValue &&
            (verifiedAtUtc.Value == default ||
             verifiedAtUtc.Value.Offset != TimeSpan.Zero ||
             verifiedAtUtc.Value > changedAtUtc))
            throw new InvalidOperationException("Invalid UTC verification evidence.");
        if (validUntilUtc.HasValue &&
            (validUntilUtc.Value == default ||
             validUntilUtc.Value.Offset != TimeSpan.Zero ||
             !verifiedAtUtc.HasValue ||
             validUntilUtc.Value <= verifiedAtUtc.Value))
            throw new InvalidOperationException("Invalid verification expiry.");
        if (verifiedAtUtc.HasValue != validUntilUtc.HasValue)
            throw new InvalidOperationException("Verification window must be complete.");
        if (state == HostVerificationState.Verified &&
            (!verifiedAtUtc.HasValue || !validUntilUtc.HasValue))
            throw new InvalidOperationException("A verified host requires trusted evidence.");
        if (state == HostVerificationState.PendingVerification &&
            (verifiedAtUtc.HasValue || validUntilUtc.HasValue))
            throw new InvalidOperationException("Pending host cannot claim verification.");

        return new VerifiedTenantHostBinding(
            tenantId, host, addressMode, state, revision, changedAtUtc,
            verifiedAtUtc, validUntilUtc);
    }

    public bool IsVerifiedAt(DateTimeOffset nowUtc)
    {
        if (nowUtc == default || nowUtc.Offset != TimeSpan.Zero)
            throw new InvalidOperationException("Backend time must be UTC.");
        return State == HostVerificationState.Verified &&
            VerifiedAtUtc.HasValue && ValidUntilUtc.HasValue &&
            ChangedAtUtc <= nowUtc &&
            VerifiedAtUtc.Value <= nowUtc && nowUtc < ValidUntilUtc.Value;
    }

    public override string ToString() => "VerifiedTenantHostBinding[redacted]";
}

/// <summary>
/// Internal contextual hint only. Never use it as authorization, credential,
/// tenant membership, cookie/claims assertion, or database placement decision.
/// </summary>
public sealed record VerifiedTenantHostHint(
    TenantId TenantId, TenantAddressMode AddressMode, long Revision)
{
    public override string ToString() => "VerifiedTenantHostHint[redacted]";
}
