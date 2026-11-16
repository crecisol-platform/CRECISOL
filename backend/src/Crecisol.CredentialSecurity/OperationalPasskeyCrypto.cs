using System.Security.Cryptography;
using System.Text.Json;
using Crecisol.Application.Identity;
using Fido2NetLib;
using Fido2NetLib.Objects;

namespace Crecisol.CredentialSecurity;

/// <summary>Explicit localhost RP for the isolated composition, never inferred
/// from a client header. No production RP or cross-origin ceremony is enabled.</summary>
public sealed class OperationalPasskeyCrypto : IOperationalPasskeyCrypto
{
    private readonly Fido2 fido = new(new Fido2Configuration
    {
        RPID = "localhost", RPName = "CRECISOL local",
        Origins = new HashSet<string> { "http://localhost:4200", "http://localhost:4201" },
        TimestampDriftTolerance = 0
    });

    public string RegistrationOptions(byte[] account, IReadOnlyList<byte[]> excluded) =>
        fido.RequestNewCredential(new RequestNewCredentialParams
        {
            User = new Fido2User { Id = account, Name = Convert.ToHexString(account), DisplayName = "Cuenta CRECISOL" },
            ExcludeCredentials = excluded.Select(id => new PublicKeyCredentialDescriptor(id)).ToArray(),
            AuthenticatorSelection = new AuthenticatorSelection
            { ResidentKey = ResidentKeyRequirement.Required, UserVerification = UserVerificationRequirement.Required },
            AttestationPreference = AttestationConveyancePreference.None
        }).ToJson();

    public string AssertionOptions() => fido.GetAssertionOptions(new GetAssertionOptionsParams
    { UserVerification = UserVerificationRequirement.Required }).ToJson();

    public byte[] ReadCredentialId(string response) =>
        JsonSerializer.Deserialize<AuthenticatorAssertionRawResponse>(response)?.RawId
        ?? throw new InvalidOperationException("Invalid authenticator response.");

    public async Task<OperationalPasskey> RegisterAsync(string options, string response,
        Func<byte[], Task<bool>> unique, CancellationToken ct)
    {
        var result = await fido.MakeNewCredentialAsync(new MakeNewCredentialParams
        {
            OriginalOptions = CredentialCreateOptions.FromJson(options),
            AttestationResponse = JsonSerializer.Deserialize<AuthenticatorAttestationRawResponse>(response)
                ?? throw new InvalidOperationException("Invalid authenticator response."),
            IsCredentialIdUniqueToUserCallback = (args, _) => unique(args.CredentialId)
        }, ct);
        return new(result.Id, result.PublicKey, result.SignCount);
    }

    public async Task<uint> VerifyAsync(string options, string response, OperationalPasskey credential,
        byte[] account, CancellationToken ct)
    {
        var result = await fido.MakeAssertionAsync(new MakeAssertionParams
        {
            OriginalOptions = Fido2NetLib.AssertionOptions.FromJson(options),
            AssertionResponse = JsonSerializer.Deserialize<AuthenticatorAssertionRawResponse>(response)
                ?? throw new InvalidOperationException("Invalid authenticator response."),
            StoredPublicKey = credential.PublicKey, StoredSignatureCounter = credential.Counter,
            IsUserHandleOwnerOfCredentialIdCallback = (args, _) => Task.FromResult(
                args.UserHandle is not null && CryptographicOperations.FixedTimeEquals(args.UserHandle, account) &&
                CryptographicOperations.FixedTimeEquals(args.CredentialId, credential.CredentialId))
        }, ct);
        return result.SignCount;
    }
}
