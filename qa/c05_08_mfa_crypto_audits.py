#!/usr/bin/env python3
"""C05-08 cryptographic SUBGATE, never a full MFA certification."""
from __future__ import annotations
import json
import os
from pathlib import Path
import re
import subprocess
import sys
from c05_01_preparation_audits import ROOT

REPO="crecisol-platform/CRECISOL"
BASE="e648d7512897e2c6aaf5c42a75969f63e315c6a6"
BRANCH="feature/c05-08-mfa-stepup-passkeys-20261114"
FILES={
    "doc":"docs/c05/C05_08_ESPECIFICACION.md",
    "totp":"backend/src/Crecisol.CredentialSecurity/TotpSha1Verifier.cs",
    "webauthn":"backend/src/Crecisol.CredentialSecurity/SyntheticWebAuthnEs256Verifier.cs",
    "recovery":"backend/src/Crecisol.CredentialSecurity/MfaRecoveryCodeProtector.cs",
    "testproj":"backend/tests/Crecisol.MfaRisk.ContractsTests/Crecisol.MfaRisk.ContractsTests.csproj",
    "tests":"backend/tests/Crecisol.MfaRisk.ContractsTests/Program.cs",
    "qa":"qa/c05_08_mfa_crypto_audits.py",
    "ci":".github/workflows/c05-08-mfa-crypto.yml",
}
EXPECTED=set(FILES.values())
APPROVED_INTEGRATION={
    "backend/src/Crecisol.Application/Identity/DisposableMfaRiskUseCases.cs",
    "backend/src/Crecisol.CredentialSecurity/DisposableMfaChallengeProtector.cs",
    "backend/src/Crecisol.Infrastructure/Identity/MySqlDisposableMfaLedger.cs",
    "backend/tests/Crecisol.MfaRisk.MySqlTests/Crecisol.MfaRisk.MySqlTests.csproj",
    "backend/tests/Crecisol.MfaRisk.MySqlTests/Program.cs",
    "database/tests/fixtures/c05_08_disposable_mfa.sql",
    "database/tests/c05_08_mfa_http_mysql.sh",
    "qa/c05_08_mfa_integration_audits.py",
    ".github/workflows/c05-08-mfa-integration.yml",
}
ALLOWED_DIFF=EXPECTED|APPROVED_INTEGRATION
PROOF=ROOT/".c05-08-crypto-proof"
GUARDS={
    "doc":(
        "MFA y passkeys por riesgo",
        "Inscripción/recuperación y step-up testeados",
        "No bypass por restablecer factor",
        "NO CERTIFICADA",
        "C05-09 BLOQUEADA",
        "Documento Maestro Único",
        BASE,
        "C05-07",
        "Argon2id",
        "256 bits",
        "30 s",
        "HMAC-SHA256",
        "ES256",
        "crossOrigin",
        "signCount",
        "atestation de inscripción",
        "MySQL 8.4",
        "A/B/C",
        "NO-GO",
        "Rollback",
    ),
    "totp":(
        "RFC 6238",
        "IntervalSeconds = 30",
        "MaxPastSteps = 1",
        "RandomNumberGenerator.GetBytes(20)",
        "HMACSHA1.HashData(seed, counter)",
        "BinaryPrimitives.WriteInt64BigEndian",
        "CryptographicOperations.FixedTimeEquals",
        "step <= lastCommittedStep",
        "long now = StepAt(trustedUtc)",
        "Future steps are never accepted",
        "if (ch is < '0' or > '9')",
        "CryptographicOperations.ZeroMemory(mac)",
    ),
    "webauthn":(
        "Deliberately LIMITED",
        "test-only",
        "Credential enrollment/attestation",
        "public static bool VerifyTrustedFixtureAssertion",
        "expectedCredentialId",
        "presentedCredentialId",
        "CryptographicOperations.FixedTimeEquals",
        "string.Equals(\"https://\" + expectedRpId",
        "SHA256.HashData(Encoding.ASCII.GetBytes(expectedRpId))",
        "UserPresent | UserVerified",
        "AttestedCredentialData | ExtensionsIncluded",
        "BinaryPrimitives.ReadUInt32BigEndian",
        "count == 0 || count <= lastPersistedSignCount",
        "JsonDocument.Parse",
        "new HashSet<string>(StringComparer.Ordinal)",
        "type.GetString() != \"webauthn.get\"",
        "origin.GetString() != expectedHttpsOrigin",
        "challenge.GetString() != EncodeChallenge(expectedChallenge)",
        "property.Name == \"crossOrigin\"",
        "property.Name == \"topOrigin\"",
        "SHA256.HashData(clientDataJson.Span)",
        "verifier.ImportSubjectPublicKeyInfo",
        "verifier.KeySize != 256",
        "DSASignatureFormat.Rfc3279DerSequence",
        "CryptographicOperations.ZeroMemory(signedBytes)",
    ),
    "recovery":(
        "NOT authenticators for private access",
        "replacement PENDING",
        "MfaRecoveryCodeProtector : IDisposable",
        "private const string Prefix = \"cm8_\"",
        "RandomNumberGenerator.GetBytes(32)",
        "HMACSHA256.HashData(key, original)",
        "CryptographicOperations.ZeroMemory(key)",
        "CryptographicOperations.ZeroMemory(bytes)",
        "serverHeldKey.IndexOfAnyExcept((byte)0)",
        "MfaRecoveryCodeProtector[redacted]",
    ),
    "testproj":(
        "<TargetFramework>net10.0</TargetFramework>",
        "<TreatWarningsAsErrors>true</TreatWarningsAsErrors>",
        "Crecisol.CredentialSecurity/Crecisol.CredentialSecurity.csproj",
    ),
    "tests":(
        "RFC 6238 SHA1 59 seconds eight-digit published test vector",
        "TotpSha1Verifier.TryVerify",
        "Same code cannot be reused",
        "Future TOTP window is NOT accepted",
        "Zero secret cannot be used",
        "MfaRecoveryCodeProtector",
        "One-time code plus fresh password can open replacement PENDING",
        "Recovery does not grant step-up",
        "Factor replacement still requires a SEPARATE new step-up challenge",
        "public bool CanCompleteStepUp=>false",
        "A consumed backup code cannot be used twice",
        "Factor is never reset before replacement factor is cryptographically proven",
        "ECDsa.Create(ECCurve.NamedCurves.nistP256)",
        "SyntheticWebAuthnEs256Verifier.VerifyTrustedFixtureAssertion",
        "Real ES256 WebAuthn assertion signs authData",
        "Missing UV",
        "Missing UP",
        "RP ID hash of tenant B never authorizes A",
        "Signed tenant A origin cannot grant tenant B",
        "crossOrigin=true rejected",
        "Duplicate clientDataJSON",
        "Tampered ECDSA signature denied",
        "Replayed or non-increasing authenticator signCount denied",
        "16 concurrent MFA step-up challenge consumes exactly once",
        "No password-only, wrong tenant or wrong risk-operation step-up",
        "Password recovery by itself never satisfies an MFA step-up",
        "A tenant factor cannot bypass independent SuperAdmin private access policy",
        "NO attestation, durable MySQL ledger",
    ),
    "ci":(
        "permissions:",
        "contents: read",
        "persist-credentials: false",
        "fetch-depth: 2",
        "dotnet-version: '10.0.x'",
        "qa/c05_08_mfa_crypto_audits.py A",
        "qa/c05_08_mfa_crypto_audits.py B",
        "qa/c05_08_mfa_crypto_audits.py C",
        "Crecisol.MfaRisk.ContractsTests.csproj",
        "database/tests/c05_07_mailbox_http_mysql.sh",
        "Crecisol.ContextualLogin.ContractsTests.csproj",
        ".c05-08-crypto-proof",
        "GITHUB_SHA",
        "GITHUB_RUN_ID",
    ),
}

def ensure(test:bool,what:str)->None:
    if not test: raise AssertionError(what)

def read()->dict[str,str]:
    return {k:(ROOT/p).read_text(encoding="utf-8") for k,p in FILES.items() if k!="qa"}

def check(data:dict[str,str])->None:
    for key,markers in GUARDS.items():
        for marker in markers:
            ensure(marker in data[key],"Missing C05-08 crypto barrier "+key+": "+marker)
    api=(ROOT/"backend/src/Crecisol.Api/Program.cs").read_text(encoding="utf-8")
    routes=re.findall(r'app\.Map(?:Get|Post|Put|Patch|Delete)\(\s*"([^"]+)"',api)
    ensure(routes==["/api/v1/public/branding/platform"] and
           "TotpSha1Verifier" not in api and
           "SyntheticWebAuthnEs256Verifier" not in api,
           "C05-08 crypto test MUST NOT expose product login/MFA API")
    ensure("workflow_dispatch:" not in data["ci"] and
           "id-token: write" not in data["ci"] and
           "deploy" not in data["ci"].lower(),
           "No deployment permission or privileged manual job")
    ensure("MySqlConnection" not in data["totp"] and
           "HttpContext" not in data["totp"] and
           "MySqlConnection" not in data["webauthn"],
           "Cryptography must not import HTTP/DB")

def a()->None:
    subprocess.run([sys.executable,"qa/master_source_guard.py","--self-test"],
                   cwd=ROOT,check=True)
    subprocess.run([sys.executable,"qa/c05_07_mailbox_audits.py","A"],
                   cwd=ROOT,check=True)
    plan=(ROOT/"docs/planificacion-futura/C05_MICROFASES_PROPUESTAS.md").read_text(encoding="utf-8")
    ensure("C05-08 | MFA y passkeys por riesgo" in plan and
           "No bypass por restablecer factor" in plan,
           "Missing exact C05-08 canonical requirement")
    check(read())
    print("PASS C05-08 crypto A: canonical source, previous C05-07, exact scope and negative bypass")

def b()->None:
    original=read()
    check(original)
    mutated=0
    for key,markers in GUARDS.items():
        for marker in markers:
            changed=dict(original)
            changed[key]=original[key].replace(marker,"[REMOVED_C05_08_GUARD]")
            ensure(changed[key]!=original[key],"Ineffective mutation")
            try: check(changed)
            except AssertionError: mutated+=1
            else: raise AssertionError("Bypass of crypto guard "+key+": "+marker)
    ensure(mutated>=60,"Insufficient negative crypto guard coverage")
    print(f"PASS C05-08 crypto B: {mutated} static adversarial guard deletions denied")
    print("Static assertions do NOT certify WebAuthn enrollment, recovery or durable MFA")

def git(*args:str)->str:
    return subprocess.check_output(["git",*args],cwd=ROOT,
                                   text=True,stderr=subprocess.DEVNULL).strip()

def c()->None:
    ensure(os.getenv("GITHUB_ACTIONS")=="true" and
           os.getenv("GITHUB_REPOSITORY")==REPO,
           "Only CRECISOL GitHub Actions can attest")
    sha=os.getenv("GITHUB_SHA","")
    run=os.getenv("GITHUB_RUN_ID","")
    ensure(re.fullmatch(r"[0-9a-f]{40}",sha) is not None and
           re.fullmatch(r"[0-9]+",run) is not None and
           git("rev-parse","HEAD")==sha,
           "Exact checkout SHA/run required")
    for stage in ("A","B","build","crypto","regressions"):
        p=PROOF/stage
        ensure(p.is_file() and p.read_text(encoding="utf-8").strip()==
               f"PASS {stage} sha={sha} run={run}",
               "Missing actual same-run proof "+stage)
    if os.getenv("GITHUB_EVENT_NAME")=="pull_request":
        path=Path(os.getenv("GITHUB_EVENT_PATH",""))
        ensure(path.is_file(),"Missing signed PR event")
        event=json.loads(path.read_text(encoding="utf-8"))["pull_request"]
        base,head=event["base"],event["head"]
        ensure(base["ref"]=="dev" and base["sha"]==BASE and
               head["ref"]==BRANCH and
               base["repo"]["full_name"]==REPO and head["repo"]["full_name"]==REPO,
               "Untrusted owner/base/branch")
        ensure(git("rev-list","--parents","-n","1","HEAD").split()==
               [sha,base["sha"],head["sha"]],
               "Synthetic PR ancestry mismatch")
        changed=git("diff","--name-only",base["sha"],head["sha"]).splitlines()
    else:
        ensure(os.getenv("GITHUB_EVENT_NAME")=="push" and
               os.getenv("GITHUB_REF")=="refs/heads/dev",
               "Only dev POSTMERGE may attest")
        changed=git("show","--format=","--name-only",sha).splitlines()
    ensure(set(changed)==ALLOWED_DIFF and len(changed)==len(ALLOWED_DIFF),
           "Unexpected diff or missing files: "+repr(changed))
    check(read())
    print(f"PASS C05-08 crypto C sha={sha} run={run}: A/B/.NET10/crypto/previous scope")
    print("C05-08 NOT CERTIFIED: lacks durable factor ledger and WebAuthn registration attestation")

if __name__=="__main__":
    try:
        ensure(len(sys.argv)==2 and sys.argv[1] in ("A","B","C"),
               "Usage: qa/c05_08_mfa_crypto_audits.py A|B|C")
        {"A":a,"B":b,"C":c}[sys.argv[1]]()
    except (AssertionError,OSError,ValueError,KeyError,
            subprocess.SubprocessError) as exc:
        print("FAIL C05-08 cryptographic subgate: "+str(exc),file=sys.stderr)
        sys.exit(1)
