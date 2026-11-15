using System.Data;
using Crecisol.Domain.Tenancy;
using Crecisol.Infrastructure.Tenancy;
using MySqlConnector;

namespace Crecisol.Infrastructure.Identity;

/// <summary>
/// C05 fixture-only activation CAS. Only a protected backend approval already
/// persisted by a separate authenticated+MFA authority may be consumed.
/// This service CANNOT issue approvals, assign roles or open sessions.
/// It MUST NEVER be registered in the public product API.
/// </summary>
public sealed class MySqlDisposableMembershipActivation
{
    private readonly string dsn;

    public MySqlDisposableMembershipActivation(string? protectedLocalDsn)
    {
        if (string.IsNullOrWhiteSpace(protectedLocalDsn))
            throw new InvalidOperationException("Protected fixture activation store required.");
        var builder = new MySqlConnectionStringBuilder(protectedLocalDsn)
        {
            Pooling = false, ConnectionTimeout = 5, DefaultCommandTimeout = 15
        };
        if (builder.Server is not ("127.0.0.1" or "localhost" or "::1") ||
            builder.Database != "c05_02_disposable" ||
            builder.AllowLoadLocalInfile || builder.Port is < 1 or > 65535)
            throw new InvalidOperationException("Activation store must remain local and disposable.");
        dsn = builder.ConnectionString;
    }

    public async Task<bool> TryActivateAsync(
        TenantId backendVerifiedTenant, TenantActorId protectedCandidate,
        Guid backendApprovalId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(backendVerifiedTenant);
        ArgumentNullException.ThrowIfNull(protectedCandidate);
        if (backendApprovalId == Guid.Empty) return false;
        cancellationToken.ThrowIfCancellationRequested();

        // A membership link is a server-side internal identifier. The actor,
        // approval and tenant MUST NOT be obtained from anonymous HTTP.
        await using var db = new MySqlConnection(dsn);
        await db.OpenAsync(cancellationToken);
        await using var cmd = new MySqlCommand("c05_02_activate_approved_candidate", db)
        {
            CommandType = CommandType.StoredProcedure
        };
        cmd.Parameters.Add("p_tenant",MySqlDbType.Binary).Value =
            MySqlRfc4122Guid.Encode(backendVerifiedTenant.Value);
        cmd.Parameters.Add("p_actor",MySqlDbType.Binary).Value =
            MySqlRfc4122Guid.Encode(protectedCandidate.Value);
        cmd.Parameters.Add("p_approval",MySqlDbType.Binary).Value =
            MySqlRfc4122Guid.Encode(backendApprovalId);
        cmd.Parameters.Add("p_link",MySqlDbType.Binary).Value =
            MySqlRfc4122Guid.Encode(Guid.NewGuid());
        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result switch
        {
            "ACTIVATED" => true,
            "DENIED" => false,
            _ => throw new InvalidOperationException("Unknown protected approval outcome.")
        };
    }

    public override string ToString() => "MySqlDisposableMembershipActivation[redacted]";
}
