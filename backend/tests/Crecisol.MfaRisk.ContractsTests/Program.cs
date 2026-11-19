using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Crecisol.CredentialSecurity;

int passed = 0;
void Check(bool ok, string name)
{
    if (!ok) throw new InvalidOperationException("FAIL C05-08: " + name);
    passed++;
}
void ExpectReject<T>(Action action, string name) where T : Exception
{
    try { action(); }
    catch (T) { passed++; return; }
    throw new InvalidOperationException("FAIL C05-08: expected " +
        typeof(T).Name + ": " + name);
}

// Authentic published RFC 6238 Appendix B HMAC-SHA1 eight-digit vector.
byte[] rfcSeed = Encoding.ASCII.GetBytes("12345678901234567890");
Check(TotpSha1Verifier.ComputeCode(rfcSeed, 1, 8) == "94287082",
    "RFC 6238 SHA1 59 seconds eight-digit published test vector");
Check(TotpSha1Verifier.ComputeCode(rfcSeed, 1) == "287082",
    "6-digit TOTP uses the same RFC SHA1 dynamic truncation");
Check(TotpSha1Verifier.StepAt(DateTimeOffset.FromUnixTimeSeconds(59)) == 1 &&
      TotpSha1Verifier.StepAt(DateTimeOffset.FromUnixTimeSeconds(60)) == 2,
    "Trusted 30-second time boundary is precise");
var clock59 = DateTimeOffset.FromUnixTimeSeconds(59);
string stepOne = TotpSha1Verifier.ComputeCode(rfcSeed, 1);
Check(TotpSha1Verifier.TryVerify(rfcSeed, stepOne, clock59, -1,
        out long acceptedStep) && acceptedStep == 1,
    "Correct TOTP from active step accepted");
Check(!TotpSha1Verifier.TryVerify(rfcSeed, stepOne, clock59, 1, out _),
    "Same code cannot be reused once counter is committed");
Check(TotpSha1Verifier.TryVerify(rfcSeed, stepOne,
        DateTimeOffset.FromUnixTimeSeconds(60), 0, out long pastStep) &&
      pastStep == 1, "Exactly one previous time step allowed");
Check(!TotpSha1Verifier.TryVerify(rfcSeed, stepOne,
        DateTimeOffset.FromUnixTimeSeconds(90), 0, out _),
    "Old TOTP beyond one step expires");
Check(!TotpSha1Verifier.TryVerify(rfcSeed,
        TotpSha1Verifier.ComputeCode(rfcSeed, 2), clock59, -1, out _),
    "Future TOTP window is NOT accepted");
Check(!TotpSha1Verifier.TryVerify(rfcSeed," 287082",clock59,-1,out _) &&
      !TotpSha1Verifier.TryVerify(rfcSeed,"287082 ",clock59,-1,out _) &&
      !TotpSha1Verifier.TryVerify(rfcSeed,"１１９２４６",clock59,-1,out _) &&
      !TotpSha1Verifier.TryVerify(rfcSeed,"11924a",clock59,-1,out _) &&
      !TotpSha1Verifier.TryVerify(rfcSeed,"287082\n",clock59,-1,out _),
    "Only exact 6-digit ASCII TOTP accepted; no Unicode or padding");
Check(!TotpSha1Verifier.TryVerify(new byte[20],stepOne,clock59,-1,out _),
    "Zero secret cannot be used as enrolled TOTP factor");
byte[] legacySha256Seed = Encoding.ASCII.GetBytes("12345678901234567890123456789012");
Check(!TotpSha1Verifier.TryVerify(legacySha256Seed, "119246", clock59, -1, out _),
    "Retired RFC6238 SHA256 credential of 32 bytes is rejected with no fallback");
Check(!TotpSha1Verifier.TryVerify(rfcSeed,stepOne,clock59,10,out _),
    "Counter rollback/replay impossible");
byte[] randomSeed=TotpSha1Verifier.GenerateSeed();
Check(randomSeed.Length == 20 &&
      randomSeed.Any(v=>v!=0) &&
      !CryptographicOperations.FixedTimeEquals(randomSeed,rfcSeed),
    "CSPRNG creates distinct 160-bit seed in memory");
CryptographicOperations.ZeroMemory(randomSeed);
ExpectReject<InvalidOperationException>(()=>
    TotpSha1Verifier.ComputeCode(new byte[20],1),
    "All-zero secret rejected");
ExpectReject<InvalidOperationException>(()=>
    TotpSha1Verifier.ComputeCode(rfcSeed,-1),
    "Negative TOTP moving factor rejected");

byte[] recoveryKey=RandomNumberGenerator.GetBytes(32);
using var recovery=new MfaRecoveryCodeProtector(recoveryKey);
string recoveryCode=recovery.GenerateForTrustedOneTimeDeliveryOnly();
byte[] backupDigest=recovery.TryDigest(recoveryCode) ??
    throw new InvalidOperationException("MFA recovery token must parse");
Check(recoveryCode.Length == 47 && recoveryCode.StartsWith("cm8_") &&
      backupDigest.Length == 32 &&
      recovery.ToString()=="MfaRecoveryCodeProtector[redacted]",
    "Purpose-specific 256-bit backup code is redacted and HMAC indexed");
Check(recovery.TryDigest(null) is null &&
      recovery.TryDigest("") is null &&
      recovery.TryDigest(recoveryCode.Replace("cm8_","cv7_")) is null &&
      recovery.TryDigest(recoveryCode + "=") is null &&
      recovery.TryDigest("cm8_" + new string('!',43)) is null,
    "Malformed, foreign-purpose, padded backup codes never authorize recovery");
byte[] badKey=RandomNumberGenerator.GetBytes(32);
using(var otherProtector=new MfaRecoveryCodeProtector(badKey))
{
    var otherDigest=otherProtector.TryDigest(recoveryCode) ??
        throw new InvalidOperationException("Wrong-key parsing failed");
    Check(!CryptographicOperations.FixedTimeEquals(
        backupDigest,otherDigest),
        "Independent server HMAC key blocks recovery-code transplant");
    CryptographicOperations.ZeroMemory(otherDigest);
}
CryptographicOperations.ZeroMemory(badKey);
var backupLedger=new SyntheticBackupCodeLedger(backupDigest);
Check(!backupLedger.TryStartReplacement(
        new byte[20], freshPassword:true),
    "Wrong HMAC backup code cannot start factor replacement");
Check(!backupLedger.TryStartReplacement(
        backupDigest, freshPassword:false),
    "Valid backup code alone cannot bypass fresh password reauthentication");
Check(backupLedger.TryStartReplacement(
        backupDigest, freshPassword:true),
    "One-time code plus fresh password can open replacement PENDING");
Check(!backupLedger.CanCompleteStepUp &&
      !backupLedger.FactorRetired &&
      backupLedger.ReplacementPending,
    "Recovery does not grant step-up, disable active MFA or revoke factors");
Check(!backupLedger.TryStartReplacement(
        backupDigest,freshPassword:true),
    "A consumed backup code cannot be used twice");
Check(!backupLedger.TryActivateReplacement(newFactorProven:false),
    "Factor is never reset before replacement factor is cryptographically proven");
Check(backupLedger.TryActivateReplacement(newFactorProven:true) &&
      backupLedger.FactorRetired && !backupLedger.ReplacementPending &&
      !backupLedger.CanCompleteStepUp,
    "Factor replacement still requires a SEPARATE new step-up challenge; recovery never grants access");
CryptographicOperations.ZeroMemory(backupDigest);
CryptographicOperations.ZeroMemory(recoveryKey);
ExpectReject<InvalidOperationException>(()=>
    _ = new MfaRecoveryCodeProtector(new byte[20]),
    "All-zero recovery HMAC key not accepted");

const string rp = "alpha.example.test";
const string origin = "https://alpha.example.test";
byte[] challenge=RandomNumberGenerator.GetBytes(32);
byte[] credentialId=RandomNumberGenerator.GetBytes(32);
using var authenticator=ECDsa.Create(ECCurve.NamedCurves.nistP256);
byte[] spki=authenticator.ExportSubjectPublicKeyInfo();

byte[] ClientData(string type="webauthn.get",string? useOrigin=null,
    string? useChallenge=null,bool crossOrigin=false)
{
    return JsonSerializer.SerializeToUtf8Bytes(new {
        type,
        challenge=useChallenge??SyntheticWebAuthnEs256Verifier.EncodeChallenge(challenge),
        origin=useOrigin??origin,
        crossOrigin
    });
}
byte[] AuthData(string rpId=rp,byte flags=0x05,uint count=5)
{
    byte[] bytes=new byte[37];
    SHA256.HashData(Encoding.ASCII.GetBytes(rpId)).CopyTo(bytes,0);
    bytes[32]=flags;
    BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(33,4),count);
    return bytes;
}
byte[] Sign(ReadOnlySpan<byte> authData,ReadOnlySpan<byte> clientData)
{
    byte[] clientHash=SHA256.HashData(clientData);
    byte[] toSign=new byte[authData.Length+clientHash.Length];
    authData.CopyTo(toSign);
    clientHash.CopyTo(toSign,authData.Length);
    try
    {
        return authenticator.SignData(toSign,HashAlgorithmName.SHA256,
            DSASignatureFormat.Rfc3279DerSequence);
    }
    finally {
        CryptographicOperations.ZeroMemory(toSign);
        CryptographicOperations.ZeroMemory(clientHash);
    }
}
bool Verify(byte[] data,byte[] auth,byte[] signature,
    byte[]? id=null,byte[]? registeredId=null,string expectedRp=rp,
    string expectedOrigin=origin,uint priorCounter=4,
    byte[]? expectedChallenge=null)=>
    SyntheticWebAuthnEs256Verifier.VerifyTrustedFixtureAssertion(
        spki, registeredId??credentialId,id??credentialId,expectedRp,
        expectedOrigin,expectedChallenge??challenge,data,auth,signature,
        priorCounter,out _);

byte[] goodJson=ClientData();
byte[] goodAuth=AuthData();
byte[] goodSignature=Sign(goodAuth,goodJson);
Check(SyntheticWebAuthnEs256Verifier.VerifyTrustedFixtureAssertion(
    spki,credentialId,credentialId,rp,origin,challenge,goodJson,goodAuth,
    goodSignature,4,out uint newCounter) && newCounter == 5,
    "Real ES256 WebAuthn assertion signs authData plus SHA256(clientDataJSON)");
Check(!Verify(goodJson,goodAuth,goodSignature,priorCounter:5) &&
      !Verify(goodJson,goodAuth,goodSignature,priorCounter:9),
    "Replayed or non-increasing authenticator signCount denied");
Check(!Verify(goodJson,AuthData(count:0),Sign(AuthData(count:0),goodJson)),
    "Counterless authenticators explicitly unsupported by this isolated prototype");
Check(!Verify(goodJson,AuthData(flags:0x01),Sign(AuthData(flags:0x01),goodJson)),
    "Missing UV (user verification) denies high-risk step-up");
Check(!Verify(goodJson,AuthData(flags:0x04),Sign(AuthData(flags:0x04),goodJson)),
    "Missing UP (user presence) denies proof");
Check(!Verify(goodJson,AuthData(flags:0x45),Sign(AuthData(flags:0x45),goodJson)),
    "Attested-data flag unexpected in assertion denied");
Check(!Verify(goodJson,AuthData(flags:0x85),Sign(AuthData(flags:0x85),goodJson)),
    "Unsupported extension flag denied, no fallback parser");
Check(!Verify(goodJson,AuthData(rpId:"beta.example.test"),
              Sign(AuthData(rpId:"beta.example.test"),goodJson)),
    "RP ID hash of tenant B never authorizes A");
Check(!Verify(goodJson,goodAuth,goodSignature,
    expectedRp:"beta.example.test",expectedOrigin:"https://beta.example.test"),
    "Signed tenant A origin cannot grant tenant B");
Check(!Verify(goodJson,goodAuth,goodSignature,
    expectedOrigin:"https://evil.example.test"),
    "Unexpected HTTPS origin fails even for otherwise genuine signature");
Check(!Verify(goodJson,goodAuth,goodSignature,
    expectedOrigin:"http://alpha.example.test"),
    "Non-HTTPS origins not allowed");
byte[] originJson=ClientData(useOrigin:"https://evil.example.test");
Check(!Verify(originJson,goodAuth,Sign(goodAuth,originJson)),
    "Cryptographically signed phishing origin still rejected");
byte[] wrongChallenge=RandomNumberGenerator.GetBytes(32);
byte[] wrongJson=ClientData(useChallenge:
    SyntheticWebAuthnEs256Verifier.EncodeChallenge(wrongChallenge));
Check(!Verify(wrongJson,goodAuth,Sign(goodAuth,wrongJson)),
    "Signed stale/foreign challenge rejected");
Check(!Verify(goodJson,goodAuth,goodSignature,expectedChallenge:wrongChallenge),
    "Server challenge reissue invalidates previous valid assertion");
byte[] crossJson=ClientData(crossOrigin:true);
Check(!Verify(crossJson,goodAuth,Sign(goodAuth,crossJson)),
    "crossOrigin=true rejected for strict tenant step-up");
byte[] topOriginJson=Encoding.UTF8.GetBytes(
    "{\"type\":\"webauthn.get\",\"challenge\":\"" +
    SyntheticWebAuthnEs256Verifier.EncodeChallenge(challenge) +
    "\",\"origin\":\"" + origin + "\",\"topOrigin\":\"" + origin + "\"}");
Check(!Verify(topOriginJson,goodAuth,Sign(goodAuth,topOriginJson)),
    "Embedded topOrigin unsupported for isolated assertion verifier");
byte[] duplicateJson=Encoding.UTF8.GetBytes(
    "{\"type\":\"webauthn.get\",\"type\":\"webauthn.get\",\"challenge\":\"" +
    SyntheticWebAuthnEs256Verifier.EncodeChallenge(challenge) +
    "\",\"origin\":\"" + origin + "\"}");
Check(!Verify(duplicateJson,goodAuth,Sign(goodAuth,duplicateJson)),
    "Duplicate clientDataJSON type rejected, no parsing ambiguity");
byte[] createJson=ClientData(type:"webauthn.create");
Check(!Verify(createJson,goodAuth,Sign(goodAuth,createJson)),
    "Registration assertion type cannot be used for step-up");
byte[] tamperedSignature=(byte[])goodSignature.Clone();
tamperedSignature[^8]^=0x80;
Check(!Verify(goodJson,goodAuth,tamperedSignature),
    "Tampered ECDSA signature denied");
byte[] stolenId=RandomNumberGenerator.GetBytes(32);
Check(!Verify(goodJson,goodAuth,goodSignature,id:stolenId),
    "Credential ID swap fails even with valid signature");
using(var wrongAuthenticator=ECDsa.Create(ECCurve.NamedCurves.nistP256))
{
    byte[] wrongPublic=wrongAuthenticator.ExportSubjectPublicKeyInfo();
    Check(!SyntheticWebAuthnEs256Verifier.VerifyTrustedFixtureAssertion(
        wrongPublic,credentialId,credentialId,rp,origin,challenge,
        goodJson,goodAuth,goodSignature,4,out _),
        "A valid signature with another authenticator key is rejected");
}
Check(!SyntheticWebAuthnEs256Verifier.VerifyTrustedFixtureAssertion(
    spki,credentialId,credentialId,rp,origin,challenge,
    Array.Empty<byte>(),goodAuth,goodSignature,4,out _),
    "Malformed empty clientDataJSON fails closed");
Check(!SyntheticWebAuthnEs256Verifier.VerifyTrustedFixtureAssertion(
    spki,credentialId,credentialId,rp,origin,challenge,
    goodJson,Array.Empty<byte>(),goodSignature,4,out _),
    "Short authenticatorData cannot be authenticated");
Check(!SyntheticWebAuthnEs256Verifier.VerifyTrustedFixtureAssertion(
    spki,credentialId,credentialId,rp,origin,challenge,
    goodJson,goodAuth,Array.Empty<byte>(),4,out _),
    "No signature is never accepted");

var oneTimeChallenge=new SyntheticSingleUseChallenge();
int winners=0;
await Task.WhenAll(Enumerable.Range(0,16).Select(_=>Task.Run(()=>
{
    if(oneTimeChallenge.TryConsume(
        validFactor:Verify(goodJson,goodAuth,goodSignature),
        trustedTenantMatches:true,exactOperationMatches:true))
        Interlocked.Increment(ref winners);
})));
Check(winners==1 && !oneTimeChallenge.TryConsume(
        validFactor:true,trustedTenantMatches:true,exactOperationMatches:true),
    "16 concurrent MFA step-up challenge consumes exactly once in memory fixture");
Check(!new SyntheticSingleUseChallenge().TryConsume(
        validFactor:false,trustedTenantMatches:true,exactOperationMatches:true) &&
      !new SyntheticSingleUseChallenge().TryConsume(
        validFactor:true,trustedTenantMatches:false,exactOperationMatches:true) &&
      !new SyntheticSingleUseChallenge().TryConsume(
        validFactor:true,trustedTenantMatches:true,exactOperationMatches:false),
    "No password-only, wrong tenant or wrong risk-operation step-up");
Check(!new SyntheticSingleUseChallenge().TryConsume(
        validFactor:false,trustedTenantMatches:false,exactOperationMatches:false),
    "Password recovery by itself never satisfies an MFA step-up");
Check(!new SyntheticSingleUseChallenge().TryConsume(
        validFactor:true,trustedTenantMatches:true,exactOperationMatches:true,
        platformPrivateAccess:true),
    "A tenant factor cannot bypass independent SuperAdmin private access policy");

Console.WriteLine($"PASS CRECISOL C05-08 .NET10 real TOTP/WebAuthn crypto: {passed} positive and adversarial assertions");
Console.WriteLine("LIMIT: real RFC6238 and ES256 crypto, but synthetic test-only credential with NO attestation, durable MySQL ledger, browser or production MFA; C05-08 remains UN-CERTIFIED.");

sealed class SyntheticBackupCodeLedger
{
    private readonly byte[] digest;
    private int used;
    private int state; // 0 active old; 1 replacement pending; 2 replacement proven
    public SyntheticBackupCodeLedger(byte[] digest) => this.digest=(byte[])digest.Clone();
    public bool ReplacementPending=>Volatile.Read(ref state)==1;
    public bool FactorRetired=>Volatile.Read(ref state)==2;
    // Neither using a backup code nor proving the replacement authenticates
    // a sensitive operation. A separate operation-bound challenge is required.
    public bool CanCompleteStepUp=>false;
    public bool TryStartReplacement(byte[] supplied,bool freshPassword)
    {
        if(!freshPassword || supplied.Length!=digest.Length ||
           !CryptographicOperations.FixedTimeEquals(supplied,digest))
            return false;
        if(Interlocked.CompareExchange(ref used,1,0)!=0) return false;
        Volatile.Write(ref state,1);
        return true;
    }
    public bool TryActivateReplacement(bool newFactorProven)=>
        newFactorProven && Interlocked.CompareExchange(ref state,2,1)==1;
}
sealed class SyntheticSingleUseChallenge
{
    private int used;
    public bool TryConsume(bool validFactor,bool trustedTenantMatches,
        bool exactOperationMatches,bool platformPrivateAccess=false)=>
        validFactor && trustedTenantMatches && exactOperationMatches &&
        !platformPrivateAccess &&
        Interlocked.CompareExchange(ref used,1,0)==0;
}
