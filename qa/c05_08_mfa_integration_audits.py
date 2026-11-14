#!/usr/bin/env python3
"""C05-08 independent runtime integration A/B/C: not a production approval."""
from __future__ import annotations
import json
import os
from pathlib import Path
import re
import subprocess
import sys

ROOT=Path(__file__).resolve().parent.parent
REPO="crecisol-platform/CRECISOL"
BASE="e648d7512897e2c6aaf5c42a75969f63e315c6a6"
BRANCH="feature/c05-08-mfa-stepup-passkeys-20261114"
ORIGINAL={
    ".github/workflows/c05-08-mfa-crypto.yml",
    "backend/src/Crecisol.CredentialSecurity/TotpSha256Verifier.cs",
    "backend/src/Crecisol.CredentialSecurity/SyntheticWebAuthnEs256Verifier.cs",
    "backend/src/Crecisol.CredentialSecurity/MfaRecoveryCodeProtector.cs",
    "backend/tests/Crecisol.MfaRisk.ContractsTests/Crecisol.MfaRisk.ContractsTests.csproj",
    "backend/tests/Crecisol.MfaRisk.ContractsTests/Program.cs",
    "docs/c05/C05_08_ESPECIFICACION.md",
    "qa/c05_08_mfa_crypto_audits.py",
}
NEW={
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
FILES=ORIGINAL|NEW
PROOF=ROOT/".c05-08-integration-proof"
GUARDS={
    "backend/src/Crecisol.Application/Identity/DisposableMfaRiskUseCases.cs":(
        "ITrustedImmediatePasswordReauthentication",
        "VerifyFreshAsync",
        "ContextualLoginPreparationStatus.Ready",
        "evidence.TenantId",
        "TryOpenEnrollmentAsync",
        "TryOpenReplacementAsync",
        "TryOpenStepUpAsync",
        "TryCommitCryptographicProofAsync",
        "TryVerifyTotp",
        "TryVerifyFixturePasskey",
        "TryDigest(backupCode)",
        "CryptographicOperations.ZeroMemory",
        "DisposableMfaRiskUseCases[redacted]",
    ),
    "backend/src/Crecisol.CredentialSecurity/DisposableMfaChallengeProtector.cs":(
        "RandomNumberGenerator.GetBytes(32)",
        "HMACSHA256.HashData(key,raw)",
        "Prefix = \"ma8_\"",
        "CryptographicOperations.ZeroMemory(key)",
        "DisposableMfaBackupAdapter",
        "CryptographicOperations.ZeroMemory(raw)",
    ),
    "backend/src/Crecisol.Infrastructure/Identity/MySqlDisposableMfaLedger.cs":(
        "Only fresh C05-04 revisions allowed.",
        "b.Server!=\"127.0.0.1\"",
        "b.Database!=\"c05_05_disposable\"",
        "CommandType=CommandType.StoredProcedure",
        "c05_08_open_enrollment",
        "c05_08_open_replacement",
        "c05_08_open_stepup",
        "c05_08_commit_proof",
        "c05_08_read_counter",
        "TryCommitCryptographicProofAsync",
        "MySqlDisposableMfaLedger[redacted]",
    ),
    "database/tests/fixtures/c05_08_disposable_mfa.sql":(
        "NEVER a migration",
        "c05_08_factors",
        "c05_08_challenges",
        "c05_08_backup_codes",
        "c05_08_security_audit",
        "FOREIGN KEY (tenant_id, actor_id)",
        "SQL SECURITY DEFINER",
        "FOR UPDATE",
        "START TRANSACTION",
        "ROLLBACK",
        "v_digest<>p_digest",
        "p_counter<=v_counter",
        "v_exp<=v_now",
        "v_cs<>v_sub",
        "v_realm<>'TENANT'",
        "v_verified<>1",
        "c05_08_open_replacement",
        "consumed_utc IS NULL",
        "state='PENDING'",
        "state='REVOKED'",
        "UPDATE c05_05_sessions SET revoked_utc=v_now",
        "subject_revision=subject_revision+1",
        "c05_08_audit_immutable_update",
        "c05_08_audit_immutable_delete",
    ),
    "backend/tests/Crecisol.MfaRisk.MySqlTests/Program.cs":(
        "Argon2idPasswordHasher",
        "FixtureImmediateReauthentication",
        "WebApplication.CreateBuilder",
        "http://127.0.0.1:0",
        "X-Tenant-Id",
        "X-Forwarded-Host",
        "UseProxy=false",
        "Dispose",
        "16 concurrent genuine HTTP/MySQL8.4",
        "Wrong password cannot",
        "Consumed backup cannot",
        "Pending replacement blocks fresh step-up",
        "Recovery invalidates earlier step-up",
        "New synthetic ES256 factor must prove",
        "Global actor",
        "sessionLedger.IsCurrentAsync",
        "Audit trigger forbids UPDATE",
        "Audit trigger forbids DELETE",
        "NO-GO production",
    ),
    "database/tests/c05_08_mfa_http_mysql.sh":(
        "mysql:8.4@sha256:",
        "127.0.0.1::3306",
        "c05_05_disposable_sessions.sql",
        "c05_06_disposable_recovery.sql",
        "c05_08_disposable_mfa.sql",
        "GRANT EXECUTE ON PROCEDURE",
        "CRECISOL_C05_08_RUNNER_DSN",
        "CRECISOL_C05_08_SETUP_DSN",
        "--integration",
    ),
    ".github/workflows/c05-08-mfa-integration.yml":(
        "contents: read",
        "persist-credentials: false",
        "dotnet-version: '10.0.x'",
        "c05_08_mfa_integration_audits.py A",
        "c05_08_mfa_integration_audits.py B",
        "c05_08_mfa_integration_audits.py C",
        "database/tests/c05_08_mfa_http_mysql.sh",
        "Crecisol.MfaRisk.MySqlTests.csproj",
        "c05_07_mailbox_http_mysql.sh",
        "GITHUB_SHA",
        "GITHUB_RUN_ID",
        ".c05-08-integration-proof",
    ),
}

def ensure(ok:bool,detail:str)->None:
    if not ok: raise AssertionError(detail)

def material()->dict[str,str]:
    return {p:(ROOT/p).read_text(encoding="utf-8") for p in FILES if p in GUARDS}

def check(data:dict[str,str])->None:
    for path,markers in GUARDS.items():
        for marker in markers:
            ensure(marker in data[path],"Missing "+path+" C05-08 security guard: "+marker)
    api=(ROOT/"backend/src/Crecisol.Api/Program.cs").read_text(encoding="utf-8")
    routes=re.findall(r'app\.Map(?:Get|Post|Put|Patch|Delete)\(\s*"([^"]+)"',api)
    ensure(routes==["/api/v1/public/branding/platform"],
           "C05-08 must NOT create product API routes")
    ci=data[".github/workflows/c05-08-mfa-integration.yml"]
    ensure("workflow_dispatch:" not in ci and "id-token: write" not in ci and
           "permissions:\n  contents: read" in ci,
           "No privileged token, manual dispatch or unreviewed deployment")

def a()->None:
    subprocess.run([sys.executable,"qa/master_source_guard.py","--self-test"],
                   cwd=ROOT,check=True)
    subprocess.run([sys.executable,"qa/c05_07_mailbox_audits.py","A"],
                   cwd=ROOT,check=True)
    plan=(ROOT/"docs/planificacion-futura/C05_MICROFASES_PROPUESTAS.md").read_text()
    ensure("C05-08 | MFA y passkeys por riesgo" in plan and
           "No bypass por restablecer factor" in plan,
           "Canonical microphase/negative case missing")
    check(material())
    print("PASS C05-08 integration A: Word, C05-07, risk requirement and isolated scope")

def b()->None:
    source=material()
    check(source)
    count=0
    for path,markers in GUARDS.items():
        for marker in markers:
            sample=dict(source)
            sample[path]=source[path].replace(marker,"[REMOVED_SECURITY_GUARD]")
            ensure(sample[path]!=source[path],"Ineffective mutation "+path)
            try: check(sample)
            except AssertionError: count+=1
            else: raise AssertionError("Undetected static guard removal: "+path)
    ensure(count>=70,"Insufficient negative security mutation guards")
    print(f"PASS C05-08 integration B: {count} static security guard deletions rejected")
    print("Static B MUST be complemented by REAL HTTP/MySQL negative runtime results")

def git(*args:str)->str:
    return subprocess.check_output(["git",*args],cwd=ROOT,text=True,
        stderr=subprocess.DEVNULL).strip()

def c()->None:
    ensure(os.getenv("GITHUB_ACTIONS")=="true" and
           os.getenv("GITHUB_REPOSITORY")==REPO,
           "C05-08 evidence requires exact CRECISOL GitHub Actions run")
    sha=os.getenv("GITHUB_SHA","")
    run=os.getenv("GITHUB_RUN_ID","")
    ensure(re.fullmatch(r"[0-9a-f]{40}",sha) is not None and
           re.fullmatch(r"[0-9]+",run) is not None and
           git("rev-parse","HEAD")==sha,
           "Immutable checkout and run identity")
    for stage in ("A","B","build","crypto","mfa","regressions"):
        stamp=PROOF/stage
        ensure(stamp.is_file() and stamp.read_text().strip()==
               f"PASS {stage} sha={sha} run={run}",
               "Missing genuine SAME-SHA/SAME-RUN proof "+stage)
    if os.getenv("GITHUB_EVENT_NAME")=="pull_request":
        eventPath=Path(os.getenv("GITHUB_EVENT_PATH",""))
        ensure(eventPath.is_file(),"Missing signed PR event")
        event=json.loads(eventPath.read_text())["pull_request"]
        base,head=event["base"],event["head"]
        ensure(base["ref"]=="dev" and base["sha"]==BASE and
               head["ref"]==BRANCH and
               base["repo"]["full_name"]==REPO and head["repo"]["full_name"]==REPO,
               "Untrusted C05-08 PR owner, baseline or branch")
        ensure(git("rev-list","--parents","-n","1","HEAD").split()==
               [sha,base["sha"],head["sha"]],
               "Synthetic checkout must be exact immutable base+head merge")
        changed=git("diff","--name-only",base["sha"],head["sha"]).splitlines()
    else:
        ensure(os.getenv("GITHUB_EVENT_NAME")=="push" and
               os.getenv("GITHUB_REF")=="refs/heads/dev",
               "Only dev POSTMERGE may attest")
        changed=git("show","--format=","--name-only",sha).splitlines()
    ensure(set(changed)==FILES and len(changed)==len(FILES),
           "Unexpected/missing C05-08 diff "+repr(changed))
    check(material())
    print(f"PASS C05-08 integration C sha={sha} run={run}: A/B/.NET10/HMAC/HTTP/MySQL8.4 + regressions")
    print("PASS is SYNTHETIC, not production MFA/passkeys authorization or whole C05 certification")

if __name__=="__main__":
    try:
        ensure(len(sys.argv)==2 and sys.argv[1] in ("A","B","C"),
               "Use C05-08 integration auditor A|B|C")
        {"A":a,"B":b,"C":c}[sys.argv[1]]()
    except (AssertionError,OSError,KeyError,ValueError,subprocess.SubprocessError) as e:
        print("FAIL C05-08 integration audit: "+str(e),file=sys.stderr)
        sys.exit(1)
