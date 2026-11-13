using Crecisol.Application.Tenancy;
using Crecisol.Domain.Identity;
using Crecisol.Domain.Tenancy;

namespace Crecisol.Application.Identity;

/// <summary>
/// Global canonical lookup key only. There is deliberately no tenant selector,
/// branch, slug, role or ID in the login request. Internationalized mailbox
/// support requires a separate reviewed canonicalization contract.
/// </summary>
public sealed record CanonicalLoginHandle
{
    private CanonicalLoginHandle(string value) => Value = value;
    public string Value { get; }

    public static CanonicalLoginHandle Parse(string? raw)
    {
        if (string.IsNullOrEmpty(raw) || raw.Length > 254 ||
            raw != raw.Trim() || raw.Any(c => c < 33 || c > 126))
            throw new InvalidOperationException("Invalid canonical login handle.");

        var at = raw.IndexOf('@');
        if (at is < 1 or > 64 || at != raw.LastIndexOf('@') ||
            at >= raw.Length - 4)
            throw new InvalidOperationException("Invalid canonical login handle.");

        var local = raw[..at];
        var domain = raw[(at + 1)..];
        if (local[0] == '.' || local[^1] == '.' || local.Contains("..") ||
            !local.All(c => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z'
                or >= '0' and <= '9' or '.' or '_' or '%' or '+' or '-'))
            throw new InvalidOperationException("Invalid canonical login handle.");

        // Reuse C04-04 strict DNS syntax; this does NOT verify host ownership.
        _ = CanonicalTenantHost.Parse(domain);
        return new CanonicalLoginHandle(raw.ToLowerInvariant());
    }

    public override string ToString() => "CanonicalLoginHandle[redacted]";
}

public enum TrustedLoginRealm
{
    TenantAccount = 1,
    PlatformOperator = 2
}

/// <summary>
/// A backend-only directory snapshot, NEVER a public API response. A PHC
/// string is a one-way verifier, not plaintext, a token or an authorization.
/// The directory MUST return all matches, not an arbitrary first account.
/// </summary>
public sealed record TrustedLoginCredentialSnapshot
{
    private TrustedLoginCredentialSnapshot(
        IdentityAccountSnapshot account, AccountCredentialMetadata credential,
        TrustedLoginRealm realm, string phc)
    {
        Account = account;
        Credential = credential;
        Realm = realm;
        EncodedVerifier = phc;
    }

    public IdentityAccountSnapshot Account { get; }
    public AccountCredentialMetadata Credential { get; }
    public TrustedLoginRealm Realm { get; }
    private string EncodedVerifier { get; }

    public static TrustedLoginCredentialSnapshot RestoreFromTrustedDirectory(
        IdentityAccountSnapshot account, AccountCredentialMetadata credential,
        TrustedLoginRealm realm, string encodedVerifier)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(credential);
        if (!Enum.IsDefined(realm) || account.ActorId != credential.ActorId ||
            credential.Kind != AccountCredentialKind.PasswordVerifier ||
            string.IsNullOrEmpty(encodedVerifier) ||
            encodedVerifier.Length > 160)
            throw new InvalidOperationException("Invalid trusted credential snapshot.");
        return new TrustedLoginCredentialSnapshot(
            account, credential, realm, encodedVerifier);
    }

    internal string ExportForInternalVerifier() => EncodedVerifier;

    public bool SameVersionAs(TrustedLoginCredentialSnapshot? other) =>
        other is not null && Realm == other.Realm &&
        Account == other.Account && Credential == other.Credential &&
        string.Equals(EncodedVerifier, other.EncodedVerifier, StringComparison.Ordinal);

    public override string ToString() => "TrustedLoginCredentialSnapshot[redacted]";
}

/// <summary>All exact global login records (0/1/N), never first-match lookup.</summary>
public interface ITrustedLoginCredentialDirectory
{
    Task<IReadOnlyList<TrustedLoginCredentialSnapshot>?> FindExactAsync(
        CanonicalLoginHandle login, CancellationToken cancellationToken);
}

/// <summary>
/// Server-owned dummy PHC used for unknown login attempts. Must be a genuine
/// Argon2id verifier with reviewed resource limits; no default/fallback secret.
/// </summary>
public interface ITrustedUnknownLoginVerifier
{
    ValueTask<string?> ReadDummyVerifierAsync(CancellationToken cancellationToken);
}

/// <summary>Actual C05-03 Argon2id comparison, never a boolean from the browser.</summary>
public interface ITrustedLoginPasswordVerifier
{
    PasswordHashCheck Verify(ReadOnlyMemory<char> candidate, string storedPhc);
}

public enum TrustedLoginEntryKind
{
    PlatformLanding = 1,
    VerifiedTenantHost = 2
}

/// <summary>
/// Only the protected backend transport adapter can decide entry type.
/// A tenant entry requires the existing C04 verified-host catalogue; the
/// platform entry never implies that an operator is privileged.
/// </summary>
public sealed record TrustedLoginEntry
{
    private TrustedLoginEntry(TrustedLoginEntryKind kind, CanonicalTenantHost? host)
    {
        Kind = kind;
        Host = host;
    }
    public TrustedLoginEntryKind Kind { get; }
    public CanonicalTenantHost? Host { get; }

    public static TrustedLoginEntry ForPlatformLanding() =>
        new(TrustedLoginEntryKind.PlatformLanding, null);

    public static TrustedLoginEntry ForTenantHost(CanonicalTenantHost host) =>
        new(TrustedLoginEntryKind.VerifiedTenantHost,
            host ?? throw new ArgumentNullException(nameof(host)));

    public override string ToString() => "TrustedLoginEntry[redacted]";
}

/// <summary>
/// Must reject forwarded or caller-controlled host, tenant, slug or header.
/// No public HTTP/DI implementation is registered at C05-04.
/// </summary>
public interface ITrustedLoginEntryReader
{
    ValueTask<TrustedLoginEntry?> ReadServerVerifiedEntryAsync(
        CancellationToken cancellationToken);
}

/// <summary>
/// Return ALL actor-linked records, including suspended/revoked, so duplicates
/// and cross-actor corruption are detected before selecting a context.
/// No client-supplied tenantId, list-first or storage-placement lookup.
/// </summary>
public interface ITrustedLoginTenantLinkDirectory
{
    Task<IReadOnlyList<AccountTenantLinkSnapshot>?> FindAllByActorAsync(
        TenantActorId verifiedActorId, CancellationToken cancellationToken);
}

public enum ContextualLoginPreparationStatus
{
    Denied = 1,
    Ambiguous = 2,
    Ready = 3
}

/// <summary>
/// Pre-session, NON-AUTHORIZING internal consistency evidence. It is not a
/// bearer, cookie, JWT, role, action grant, branch permission or session.
/// C05-05 MUST atomically revalidate before creating a real session.
/// </summary>
public sealed record PreparedContextualLoginEvidence
{
    internal PreparedContextualLoginEvidence(
        TenantActorId actorId, TenantId tenantId, AccountTenantLinkId linkId,
        long accountRevision, long credentialRevision, long membershipRevision,
        long tenantCatalogRevision, long hostRevision, bool needsRehash)
    {
        ActorId = actorId;
        TenantId = tenantId;
        LinkId = linkId;
        AccountRevision = accountRevision;
        CredentialRevision = credentialRevision;
        MembershipRevision = membershipRevision;
        TenantCatalogRevision = tenantCatalogRevision;
        HostRevision = hostRevision;
        NeedsRehash = needsRehash;
    }
    public TenantActorId ActorId { get; }
    public TenantId TenantId { get; }
    public AccountTenantLinkId LinkId { get; }
    public long AccountRevision { get; }
    public long CredentialRevision { get; }
    public long MembershipRevision { get; }
    public long TenantCatalogRevision { get; }
    public long HostRevision { get; }
    public bool NeedsRehash { get; }
    public override string ToString() => "PreparedContextualLoginEvidence[redacted]";
}

public sealed record ContextualLoginPreparation
{
    private ContextualLoginPreparation(
        ContextualLoginPreparationStatus status, PreparedContextualLoginEvidence? evidence)
    {
        Status = status;
        Evidence = evidence;
    }
    public ContextualLoginPreparationStatus Status { get; }
    public PreparedContextualLoginEvidence? Evidence { get; }
    public static ContextualLoginPreparation Denied { get; } =
        new(ContextualLoginPreparationStatus.Denied, null);
    public static ContextualLoginPreparation Ambiguous { get; } =
        new(ContextualLoginPreparationStatus.Ambiguous, null);
    internal static ContextualLoginPreparation Ready(PreparedContextualLoginEvidence evidence) =>
        new(ContextualLoginPreparationStatus.Ready, evidence);
    public override string ToString() => "ContextualLoginPreparation[redacted]";
}

/// <summary>
/// C05-04. Password + unique global identity -> verified backend context,
/// with NO tenant selector, no session issuance, no MFA bypass, no role grant.
/// Active account, credential, host, membership and tenant are rechecked.
/// </summary>
public sealed class PrepareContextualPasswordLogin
{
    private const int MaxLinkSnapshots = 32;
    private readonly ITrustedLoginCredentialDirectory credentials;
    private readonly ITrustedUnknownLoginVerifier unknown;
    private readonly ITrustedLoginPasswordVerifier verifier;
    private readonly ITrustedLoginEntryReader entryReader;
    private readonly ITrustedLoginTenantLinkDirectory links;
    private readonly ResolveVerifiedTenantHost hostResolver;
    private readonly ITrustedTenantCatalogReader tenants;
    private readonly ITrustedHostUtcClock clock;

    public PrepareContextualPasswordLogin(
        ITrustedLoginCredentialDirectory credentials,
        ITrustedUnknownLoginVerifier unknown,
        ITrustedLoginPasswordVerifier verifier,
        ITrustedLoginEntryReader entryReader,
        ITrustedLoginTenantLinkDirectory links,
        ResolveVerifiedTenantHost hostResolver,
        ITrustedTenantCatalogReader tenants,
        ITrustedHostUtcClock clock)
    {
        this.credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
        this.unknown = unknown ?? throw new ArgumentNullException(nameof(unknown));
        this.verifier = verifier ?? throw new ArgumentNullException(nameof(verifier));
        this.entryReader = entryReader ?? throw new ArgumentNullException(nameof(entryReader));
        this.links = links ?? throw new ArgumentNullException(nameof(links));
        this.hostResolver = hostResolver ?? throw new ArgumentNullException(nameof(hostResolver));
        this.tenants = tenants ?? throw new ArgumentNullException(nameof(tenants));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<ContextualLoginPreparation> ExecuteAsync(
        CanonicalLoginHandle login, ReadOnlyMemory<char> candidate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(login);
        cancellationToken.ThrowIfCancellationRequested();

        // Fail closed without a backend-supplied dummy verifier. Unknown and
        // duplicate accounts still perform a real bounded Argon2id attempt.
        var dummyPhc = await unknown.ReadDummyVerifierAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrEmpty(dummyPhc) || dummyPhc.Length > 160)
            return ContextualLoginPreparation.Denied;

        var allCredentials = await credentials.FindExactAsync(login, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var exact = allCredentials is { Count: 1 } ? allCredentials[0] : null;
        var phc = exact?.ExportForInternalVerifier() ?? dummyPhc;

        var checkedPassword = verifier.Verify(candidate, phc);
        cancellationToken.ThrowIfCancellationRequested();
        if (exact is null || !checkedPassword.Matched ||
            exact.Realm != TrustedLoginRealm.TenantAccount)
            return ContextualLoginPreparation.Denied;

        var firstNow = clock.GetUtcNow();
        RequireUtc(firstNow);
        if (!IsCurrent(exact, firstNow))
            return ContextualLoginPreparation.Denied;

        // Only AFTER a real password check may we inspect tenant memberships
        // and verified host. Never use this code to list tenant affiliations.
        var entry = await entryReader.ReadServerVerifiedEntryAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (entry is null) return ContextualLoginPreparation.Denied;

        var hostHint = await VerifiedHostHintOrNull(entry, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (entry.Kind == TrustedLoginEntryKind.VerifiedTenantHost && hostHint is null)
            return ContextualLoginPreparation.Denied;
        if (entry.Kind != TrustedLoginEntryKind.PlatformLanding &&
            entry.Kind != TrustedLoginEntryKind.VerifiedTenantHost)
            return ContextualLoginPreparation.Denied;

        var firstLinks = await links.FindAllByActorAsync(
            exact.Account.ActorId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (!ValidLinkSet(firstLinks, exact.Account.ActorId))
            return ContextualLoginPreparation.Denied;

        // Snapshot the first read so a mutable adapter-owned collection cannot
        // mutate both the first and second comparisons in place.
        var firstFrozen = firstLinks!.ToArray();
        var active = firstFrozen.Where(x => x.IsActiveAtUtc(firstNow)).ToArray();
        AccountTenantLinkSnapshot selected;
        if (hostHint is not null)
        {
            var exactLinks = active.Where(x => x.TenantId == hostHint.TenantId).ToArray();
            if (exactLinks.Length != 1) return ContextualLoginPreparation.Denied;
            selected = exactLinks[0];
        }
        else
        {
            if (active.Length > 1) return ContextualLoginPreparation.Ambiguous;
            if (active.Length != 1) return ContextualLoginPreparation.Denied;
            selected = active[0];
        }

        var catalog = await tenants.FindByIdAsync(
            selected.TenantId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (!ValidTenant(catalog, selected.TenantId, hostHint, firstNow))
            return ContextualLoginPreparation.Denied;

        // Recheck ALL mutable inputs. The resolver is preparation evidence
        // only; a real session store in C05-05 must enforce atomic CAS again.
        var againCredentials = await credentials.FindExactAsync(login, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (againCredentials is not { Count: 1 } ||
            !exact.SameVersionAs(againCredentials[0]))
            return ContextualLoginPreparation.Denied;

        var againLinks = await links.FindAllByActorAsync(
            exact.Account.ActorId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (!ValidLinkSet(againLinks, exact.Account.ActorId) ||
            !firstFrozen.SequenceEqual(againLinks!))
            return ContextualLoginPreparation.Denied;

        var againEntry = await entryReader.ReadServerVerifiedEntryAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (againEntry != entry) return ContextualLoginPreparation.Denied;

        var againHostHint = await VerifiedHostHintOrNull(againEntry, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (againHostHint != hostHint)
            return ContextualLoginPreparation.Denied;

        var againCatalog = await tenants.FindByIdAsync(
            selected.TenantId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (againCatalog != catalog)
            return ContextualLoginPreparation.Denied;

        var finalNow = clock.GetUtcNow();
        RequireUtc(finalNow);
        cancellationToken.ThrowIfCancellationRequested();
        if (finalNow < firstNow)
            return ContextualLoginPreparation.Denied;
        if (!IsCurrent(exact, finalNow) ||
            !selected.IsActiveAtUtc(finalNow) ||
            !ValidTenant(againCatalog, selected.TenantId, hostHint, finalNow))
            return ContextualLoginPreparation.Denied;

        return ContextualLoginPreparation.Ready(
            new PreparedContextualLoginEvidence(
                exact.Account.ActorId, selected.TenantId, selected.LinkId,
                exact.Account.Revision, exact.Credential.Revision, selected.Revision,
                catalog!.Revision, hostHint?.Revision ?? 0,
                checkedPassword.NeedsRehash));
    }

    private async Task<VerifiedTenantHostHint?> VerifiedHostHintOrNull(
        TrustedLoginEntry entry, CancellationToken cancellationToken)
    {
        if (entry.Kind == TrustedLoginEntryKind.PlatformLanding && entry.Host is null)
            return null;
        if (entry.Kind != TrustedLoginEntryKind.VerifiedTenantHost ||
            entry.Host is null)
            return null;
        try
        {
            return await hostResolver.ResolveAsync(entry.Host, cancellationToken);
        }
        catch (UnauthorizedAccessException)
        {
            // No host existence, ownership or tenant data in a login outcome.
            return null;
        }
        catch (InvalidOperationException)
        {
            // Corrupt or mismatched host metadata never grants context.
            return null;
        }
    }

    private static bool ValidLinkSet(
        IReadOnlyList<AccountTenantLinkSnapshot>? rows, TenantActorId actor)
    {
        if (rows is null || rows.Count > MaxLinkSnapshots ||
            rows.Any(x => x is null || x.ActorId != actor))
            return false;
        return rows.Select(x => x.TenantId).Distinct().Count() == rows.Count;
    }

    private static bool IsCurrent(
        TrustedLoginCredentialSnapshot snapshot, DateTimeOffset nowUtc) =>
        snapshot.Account.IsActiveAtUtc(nowUtc) &&
        snapshot.Credential.ActorId == snapshot.Account.ActorId &&
        snapshot.Credential.Kind == AccountCredentialKind.PasswordVerifier &&
        snapshot.Credential.State == AccountCredentialState.Active &&
        snapshot.Credential.ChangedAtUtc <= nowUtc;

    private static bool ValidTenant(
        TenantCatalogSnapshot? tenant, TenantId id,
        VerifiedTenantHostHint? host, DateTimeOffset nowUtc) =>
        tenant is not null && tenant.TenantId == id &&
        tenant.State == TenantLifecycleState.Active &&
        tenant.UpdatedAtUtc <= nowUtc &&
        (host is null || (host.TenantId == id &&
                          host.AddressMode == tenant.AddressMode &&
                          host.Revision > 0));

    private static void RequireUtc(DateTimeOffset time)
    {
        if (time == default || time.Offset != TimeSpan.Zero)
            throw new InvalidOperationException("Contextual login clock must be UTC.");
    }
}
