using System.Buffers.Binary;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using Crecisol.Application.Identity;
using Crecisol.Domain.Identity;
using Crecisol.Domain.Tenancy;
using Crecisol.Infrastructure.Tenancy;
using MySqlConnector;

namespace Crecisol.Infrastructure.Identity;

/// <summary>
/// DISPOSABLE, LOCAL-ONLY cryptographically signed recipient-assertion demo.
/// The signing key is held by the backend, never in SQL or the caller.
/// Tokens are TEST-SIGNED by a dedicated test issuer; no real login exists.
/// The MySQL definer routine checks the current recipient session REVOCATION
/// and the original issuer scope in ONE atomic invitation CLAIMED update.
/// Neither the HMAC nor the signed assertion implies mailbox control.
/// </summary>
public sealed class MySqlDisposableSignedRecipientClaimLedger :
    ITrustedSignedRecipientClaimLedger, IDisposable
{
    private const int BodyLength = 93;
    private const int ProofLength = 125;
    private const byte FormatVersion = 1;
    private const string RequiredDatabase = "c05_02_disposable";
    private static readonly byte[] Purpose =
        Encoding.ASCII.GetBytes("CRECISOL/C05-02/RECIPIENT-ASSERTION/TENANT-CLAIM/V1");

    private readonly string trustedDsn;
    private readonly byte[] signingKey;
    private readonly int activeKeyEpoch;
    private bool disposed;

    public MySqlDisposableSignedRecipientClaimLedger(
        string? serverHeldLocalTestDsn, byte[] serverHeldSecret, int keyEpoch)
    {
        ArgumentNullException.ThrowIfNull(serverHeldSecret);
        if (serverHeldSecret.Length != 32 || keyEpoch < 1 ||
            string.IsNullOrWhiteSpace(serverHeldLocalTestDsn))
            throw new InvalidOperationException("Trusted local session proof configuration required.");

        var options = new MySqlConnectionStringBuilder(serverHeldLocalTestDsn)
        {
            Pooling = false,
            ConnectionTimeout = 5,
            DefaultCommandTimeout = 12
        };
        if (options.Database != RequiredDatabase ||
            options.Server is not ("127.0.0.1" or "localhost" or "::1") ||
            options.AllowLoadLocalInfile)
            throw new InvalidOperationException("Signed claim only supports disposable loopback SQL.");

        activeKeyEpoch = keyEpoch;
        trustedDsn = options.ConnectionString;
        signingKey = HMACSHA256.HashData(serverHeldSecret,
            "CRECISOL/C05-02/RECIPIENT-ASSERTION-KEY/V1"u8);
    }

    public async Task<bool> TryClaimWithVerifiedAssertionOnceAsync(
        TenantId verifiedTenant, InvitationId invitationId,
        ReadOnlyMemory<byte> rawInvitationCapability,
        ReadOnlyMemory<byte> signedRecipientAssertion,
        DateTimeOffset trustedNowUtc,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(verifiedTenant);
        ArgumentNullException.ThrowIfNull(invitationId);
        InvitationSnapshot.RequireUtc(trustedNowUtc);
        cancellationToken.ThrowIfCancellationRequested();

        if (rawInvitationCapability.Length != 32 ||
            !TryVerify(signedRecipientAssertion.Span, trustedNowUtc, out var proof) ||
            proof.Tenant != verifiedTenant.Value)
            return false;

        var digest = InvitationTokenDigest.FromToken(rawInvitationCapability.Span);
        var protectedDigest = digest.CopyForProtectedPersistenceAdapter();
        try
        {
            await using var db = new MySqlConnection(trustedDsn);
            await db.OpenAsync(cancellationToken);
            await using var cmd = new MySqlCommand("c05_02_claim_signed_once", db)
            {
                CommandType = CommandType.StoredProcedure
            };
            Id(cmd,"p_tenant",proof.Tenant);
            Id(cmd,"p_invitation",invitationId.Value);
            Id(cmd,"p_actor",proof.Actor);
            Id(cmd,"p_recipient",proof.Recipient);
            cmd.Parameters.Add("p_digest",MySqlDbType.Binary).Value=protectedDigest;
            Id(cmd,"p_signed_session",proof.Session);
            cmd.Parameters.Add("p_signed_revision",MySqlDbType.Int64).Value=proof.Revision;
            cmd.Parameters.Add("p_signed_key_epoch",MySqlDbType.Int32).Value=proof.KeyEpoch;
            cmd.Parameters.Add("p_signed_issued",MySqlDbType.DateTime).Value=proof.IssuedUtc;
            cmd.Parameters.Add("p_signed_expires",MySqlDbType.DateTime).Value=proof.ExpiresUtc;

            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            return result switch
            {
                "CLAIMED" => true,
                "DENIED" => false,
                _ => throw new InvalidOperationException("Unexpected signed claim SQL result.")
            };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(protectedDigest);
        }
    }

    private bool TryVerify(ReadOnlySpan<byte> input,
        DateTimeOffset now, out SessionProof result)
    {
        result = default;
        if (input.Length != ProofLength || input[0] != FormatVersion)
            return false;
        var keyEpoch = BinaryPrimitives.ReadInt32BigEndian(input.Slice(1,4));
        if (keyEpoch != activeKeyEpoch)
            return false;

        var data = new byte[Purpose.Length+BodyLength];
        var expected = Array.Empty<byte>();
        try
        {
            Purpose.CopyTo(data,0);
            input[..BodyLength].CopyTo(data.AsSpan(Purpose.Length));
            expected = HMACSHA256.HashData(signingKey,data);
            if (!CryptographicOperations.FixedTimeEquals(
                    expected,input.Slice(BodyLength,32)))
                return false;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(data);
            CryptographicOperations.ZeroMemory(expected);
        }

        var tenant = MySqlRfc4122Guid.Decode(input.Slice(5,16).ToArray());
        var actor = MySqlRfc4122Guid.Decode(input.Slice(21,16).ToArray());
        var recipient = MySqlRfc4122Guid.Decode(input.Slice(37,16).ToArray());
        var session = MySqlRfc4122Guid.Decode(input.Slice(53,16).ToArray());
        var revision = BinaryPrimitives.ReadInt64BigEndian(input.Slice(69,8));
        var issued = BinaryPrimitives.ReadInt64BigEndian(input.Slice(77,8));
        var expires = BinaryPrimitives.ReadInt64BigEndian(input.Slice(85,8));
        var nowSeconds = now.ToUnixTimeSeconds();

        // Audience and format are fixed by a purpose-separated MAC. SQL also
        // rechecks its own UTC clock and exact session issued/expires/revision.
        // Bound both timestamps before subtraction/conversion: even an
        // HMAC-valid test assertion with Int64 extremes must fail CLOSED,
        // not overflow or throw an out-of-range conversion exception.
        if (tenant==Guid.Empty || actor==Guid.Empty || recipient==Guid.Empty ||
            session==Guid.Empty || revision<1 ||
            issued>nowSeconds || issued<nowSeconds-300 ||
            expires<=nowSeconds || expires>nowSeconds+300 ||
            expires<=issued || expires-issued is <30 or >300)
            return false;

        result = new SessionProof(
            tenant,actor,recipient,session,revision,keyEpoch,
            DateTimeOffset.FromUnixTimeSeconds(issued).UtcDateTime,
            DateTimeOffset.FromUnixTimeSeconds(expires).UtcDateTime);
        return true;
    }

    private static void Id(MySqlCommand cmd,string name,Guid id) =>
        cmd.Parameters.Add(name,MySqlDbType.Binary).Value=MySqlRfc4122Guid.Encode(id);

    public void Dispose()
    {
        if (disposed) return;
        CryptographicOperations.ZeroMemory(signingKey);
        disposed=true;
        GC.SuppressFinalize(this);
    }

    private readonly record struct SessionProof(
        Guid Tenant,Guid Actor,Guid Recipient,Guid Session,
        long Revision,int KeyEpoch,DateTime IssuedUtc,DateTime ExpiresUtc);
}
