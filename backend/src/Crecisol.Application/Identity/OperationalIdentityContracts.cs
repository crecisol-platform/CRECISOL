namespace Crecisol.Application.Identity;

// Transport supplies a catalog-verified direct host and a socket-derived peer.
// Neither tenant IDs nor authorization claims are accepted from a form.
public sealed record OperationalIdentityRequest(string Host, string Peer, string Operation,
    IReadOnlyDictionary<string, string> Fields, string? Bearer);

public sealed record OperationalIdentityResult(bool Allowed, object? Value = null);
public sealed record OperationalSessionReceipt(string SessionToken, DateTimeOffset ExpiresAtUtc,
    IReadOnlyList<string>? RecoveryCodes = null);

public interface IOperationalIdentityFlow
{
    Task<OperationalIdentityResult> ExecuteAsync(OperationalIdentityRequest request, CancellationToken ct);
}

public interface IOperationalIdentityCrypto
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string phc);
    bool VerifyTotp(byte[] seed, string code, DateTimeOffset now, long previous, out long step);
}
