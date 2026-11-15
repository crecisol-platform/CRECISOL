using System.Security.Cryptography;
using Crecisol.Application.Identity;
using Crecisol.Domain.Identity;
using Crecisol.Domain.Tenancy;
using Crecisol.Infrastructure.Tenancy;
using MySqlConnector;

namespace Crecisol.Infrastructure.Identity;

/// <summary>
/// Operational proof consumer. Tenant scope is supplied by the verified
/// backend Host resolver; clients provide only an opaque proof selector and
/// 256-bit code. The SQL routine consumes once and rate-limits failures.
/// </summary>
public sealed class MySqlOperationalRegistrationMailboxProof(string? trustedDsn) :
    ITrustedRegistrationMailboxProofLedger
{
    private readonly string? dsn = trustedDsn;

    public async Task<bool> TryConfirmOnceAsync(TenantId verifiedTenant, Guid proofId,
        ReadOnlyMemory<byte> token, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(verifiedTenant);
        cancellationToken.ThrowIfCancellationRequested();
        if (proofId == Guid.Empty || token.Length != 32 || string.IsNullOrWhiteSpace(dsn))
            return false;

        var digest = EmailPossessionDigest.FromToken(verifiedTenant, proofId, token.Span)
            .CopyForProtectedDatabase();
        try
        {
            var options = new MySqlConnectionStringBuilder(dsn)
            {
                ConnectionTimeout = 3,
                DefaultCommandTimeout = 3,
                Pooling = true,
                MaximumPoolSize = 20
            };
            await using var db = new MySqlConnection(options.ConnectionString);
            await db.OpenAsync(cancellationToken);
            await using var command = new MySqlCommand(
                "c05_operational_consume_mailbox_proof", db)
            {
                CommandType = System.Data.CommandType.StoredProcedure
            };
            command.Parameters.Add("p_tenant", MySqlDbType.Binary).Value =
                MySqlRfc4122Guid.Encode(verifiedTenant.Value);
            command.Parameters.Add("p_proof", MySqlDbType.Binary).Value =
                MySqlRfc4122Guid.Encode(proofId);
            command.Parameters.Add("p_digest", MySqlDbType.Binary).Value = digest;
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result as string == "VERIFIED";
        }
        finally
        {
            CryptographicOperations.ZeroMemory(digest);
        }
    }

    public override string ToString() => "MySqlOperationalRegistrationMailboxProof[redacted]";
}
