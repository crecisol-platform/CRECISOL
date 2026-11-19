namespace Crecisol.Application.Identity;

// Transport supplies a catalog-verified direct host and a socket-derived peer.
// Neither tenant IDs nor authorization claims are accepted from a form.
public sealed record OperationalIdentityRequest(string Host, string Peer, string Operation,
    IReadOnlyDictionary<string, string> Fields, string? Bearer, string DeviceLabel = "API");

public sealed record OperationalIdentityResult(bool Allowed, object? Value = null)
{
    // Browser transport renews a session cookie only after backend validation.
    public DateTimeOffset? SessionExpiresAtUtc { get; init; }
    public int? SessionIdleMinutes { get; init; }
}
public sealed record OperationalSessionReceipt(string SessionToken, DateTimeOffset ExpiresAtUtc,
    IReadOnlyList<string>? RecoveryCodes = null);
public sealed record OperationalAssistedReceipt(Guid Reference, bool Accepted = true);
public interface IOperationalSessionWindow { DateTimeOffset ExpiresAtUtc { get; } }
public sealed record OperationalSessionSummary(Guid Id, DateTimeOffset IssuedAtUtc, DateTimeOffset ExpiresAtUtc,
    DateTimeOffset LastSeenAtUtc, string Device, string Factor);
public sealed record OperationalSessionInventory(DateTimeOffset ExpiresAtUtc, IReadOnlyList<OperationalSessionSummary> Sessions) : IOperationalSessionWindow;
public sealed record OperationalPlatformValidation(DateTimeOffset ExpiresAtUtc, string Realm = "PLATFORM_IDENTITY") : IOperationalSessionWindow;

public interface IOperationalIdentityFlow
{
    Task<OperationalIdentityResult> ExecuteAsync(OperationalIdentityRequest request, CancellationToken ct);
}

public interface IOperationalIdentityCrypto
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string phc);
    bool VerifyTotp(byte[] seed, string code, DateTimeOffset now, long previous, out long step);
    bool VerifyPairing(byte[] publicKey, string message, byte[] signature);
}

// WebAuthn models stay behind the credential-security port. Only public keys,
// opaque user handles and server-generated options cross this boundary.
public sealed record OperationalPasskey(byte[] CredentialId, byte[] PublicKey, uint Counter);
public interface IOperationalPasskeyCrypto
{
    string RegistrationOptions(byte[] account, IReadOnlyList<byte[]> excluded);
    string AssertionOptions();
    byte[] ReadCredentialId(string response);
    Task<OperationalPasskey> RegisterAsync(string options, string response, Func<byte[], Task<bool>> unique, CancellationToken ct);
    Task<uint> VerifyAsync(string options, string response, OperationalPasskey credential, byte[] account, CancellationToken ct);
}
