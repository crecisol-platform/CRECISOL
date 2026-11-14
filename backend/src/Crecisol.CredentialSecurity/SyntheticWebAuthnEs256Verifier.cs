using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Crecisol.CredentialSecurity;

/// <summary>
/// Deliberately LIMITED, test-only WebAuthn Level 3 ES256 assertion verifier.
/// Credential enrollment/attestation, COSE keys, metadata, authenticator
/// backup/counterless devices and browser integration are NOT implemented.
/// Never register this component as a production WebAuthn/FIDO2 authority.
/// </summary>
public static class SyntheticWebAuthnEs256Verifier
{
    private const byte UserPresent = 0x01;
    private const byte UserVerified = 0x04;
    private const byte AttestedCredentialData = 0x40;
    private const byte ExtensionsIncluded = 0x80;

    public static string EncodeChallenge(ReadOnlySpan<byte> challenge)
    {
        if (challenge.Length != 32 || challenge.IndexOfAnyExcept((byte)0) < 0)
            throw new InvalidOperationException("Server-generated 256-bit challenge required.");
        return Convert.ToBase64String(challenge).TrimEnd('=')
            .Replace('+', '-').Replace('/', '_');
    }

    public static bool VerifyTrustedFixtureAssertion(
        ReadOnlySpan<byte> trustedPublicKeySpki,
        ReadOnlySpan<byte> expectedCredentialId,
        ReadOnlySpan<byte> presentedCredentialId,
        string? expectedRpId, string? expectedHttpsOrigin,
        ReadOnlySpan<byte> expectedChallenge,
        ReadOnlyMemory<byte> clientDataJson,
        ReadOnlySpan<byte> authenticatorData,
        ReadOnlySpan<byte> signature,
        uint lastPersistedSignCount,
        out uint newSignCount)
    {
        newSignCount = 0;
        if (trustedPublicKeySpki.Length is < 60 or > 200 ||
            expectedCredentialId.Length is < 16 or > 1024 ||
            presentedCredentialId.Length != expectedCredentialId.Length ||
            !CryptographicOperations.FixedTimeEquals(
                expectedCredentialId, presentedCredentialId) ||
            expectedChallenge.Length != 32 ||
            expectedChallenge.IndexOfAnyExcept((byte)0) < 0 ||
            clientDataJson.Length is < 32 or > 4096 ||
            authenticatorData.Length != 37 ||
            signature.Length is < 64 or > 80 ||
            string.IsNullOrWhiteSpace(expectedRpId) ||
            expectedRpId.Length > 253 ||
            !string.Equals(expectedRpId, expectedRpId.ToLowerInvariant(),
                StringComparison.Ordinal) ||
            string.IsNullOrEmpty(expectedHttpsOrigin) ||
            !string.Equals("https://" + expectedRpId,
                expectedHttpsOrigin, StringComparison.Ordinal))
            return false;

        // Strictly bind this synthetic fixture to one verified https host.
        // A deployed RP must implement origin/RP-ID policy and Public Suffix
        // rules using a complete, audited FIDO2 library.
        var host = Uri.CheckHostName(expectedRpId);
        if (host != UriHostNameType.Dns || expectedRpId.Contains("..") ||
            expectedRpId.StartsWith('.') || expectedRpId.EndsWith('.'))
            return false;

        byte[] rpHash = SHA256.HashData(Encoding.ASCII.GetBytes(expectedRpId));
        try
        {
            if (!CryptographicOperations.FixedTimeEquals(
                    rpHash, authenticatorData[..32]))
                return false;
        }
        finally { CryptographicOperations.ZeroMemory(rpHash); }

        byte flags = authenticatorData[32];
        if ((flags & (UserPresent | UserVerified)) !=
                (UserPresent | UserVerified) ||
            (flags & (AttestedCredentialData | ExtensionsIncluded)) != 0)
            return false;

        uint count = BinaryPrimitives.ReadUInt32BigEndian(authenticatorData[33..37]);
        if (count == 0 || count <= lastPersistedSignCount)
            return false; // reject zero/counterless and cloned/downgraded credentials

        try
        {
            using JsonDocument json = JsonDocument.Parse(clientDataJson,
                new JsonDocumentOptions {MaxDepth = 5, AllowTrailingCommas = false});
            JsonElement root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return false;

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in root.EnumerateObject())
            {
                if (!seen.Add(property.Name)) return false;
                if (property.Name is "type" or "origin" or "challenge")
                {
                    if (property.Value.ValueKind != JsonValueKind.String)
                        return false;
                }
                if (property.Name == "crossOrigin" &&
                    property.Value.ValueKind != JsonValueKind.False)
                    return false;
                if (property.Name == "topOrigin")
                    return false;
            }

            if (!root.TryGetProperty("type", out var type) ||
                type.GetString() != "webauthn.get" ||
                !root.TryGetProperty("origin", out var origin) ||
                origin.GetString() != expectedHttpsOrigin ||
                !root.TryGetProperty("challenge", out var challenge) ||
                challenge.GetString() != EncodeChallenge(expectedChallenge))
                return false;
        }
        catch (JsonException) { return false; }

        byte[] signedBytes = new byte[authenticatorData.Length + 32];
        authenticatorData.CopyTo(signedBytes);
        byte[] clientHash = SHA256.HashData(clientDataJson.Span);
        clientHash.CopyTo(signedBytes, authenticatorData.Length);
        CryptographicOperations.ZeroMemory(clientHash);

        try
        {
            using ECDsa verifier = ECDsa.Create();
            verifier.ImportSubjectPublicKeyInfo(trustedPublicKeySpki, out int used);
            if (used != trustedPublicKeySpki.Length ||
                verifier.KeySize != 256 ||
                !verifier.VerifyData(signedBytes, signature,
                    HashAlgorithmName.SHA256,
                    DSASignatureFormat.Rfc3279DerSequence))
                return false;
            newSignCount = count;
            return true;
        }
        catch (CryptographicException) { return false; }
        finally { CryptographicOperations.ZeroMemory(signedBytes); }
    }
}
