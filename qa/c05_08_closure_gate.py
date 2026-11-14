#!/usr/bin/env python3
"""C05-08 institutional A/B/C, TEST-only MFA. No production attestation."""
from __future__ import annotations

import json
import os
from pathlib import Path
import re
import subprocess
import sys
from c05_01_preparation_audits import ROOT

REPO = "crecisol-platform/CRECISOL"
BRANCH = "feature/c05-08-mfa-institutional-20261114"
BASE = "49e99411efa0b6639516eb703b1b057c87a7492e"
FILES = {
    "act": "docs/c05/ACTA_CERTIFICACION_ACOTADA_C05_08_2026_11_14.md",
    "qa": "qa/c05_08_closure_gate.py",
    "ci": ".github/workflows/c05-08-certification.yml",
}
ALLOWED = set(FILES.values())
PROOF = ROOT / ".c05-08-institutional-proof"
GUARDS = {
    "act": (
        "Acta institucional CONDICIONADA",
        "ACTA NO EFICAZ",
        "C05-08 NO CERTIFICADA",
        "C05-09 BLOQUEADA",
        "crecisol-platform/CRECISOL",
        "Documento Maestro Único",
        "MASTER_REFERENCE.md",
        "43364292640",
        "ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c",
        "0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77",
        "6882141034",
        "e648d7512897e2c6aaf5c42a75969f63e315c6a6",
        "MFA y passkeys por riesgo",
        "Inscripción/recuperación y step-up testeados",
        "No bypass por restablecer factor",
        "141",
        "ca8faf2c85e07435e085e25e7474efd0d50387f4",
        BASE,
        "EXACTAMENTE 17 archivos",
        "Argon2id C05-04 fresco",
        "TOTP RFC 6238",
        "HMAC-SHA256",
        "256 bits",
        "MySqlConnector@2.6.2",
        "SQL SECURITY DEFINER",
        "FOR UPDATE",
        "solo EXECUTE",
        "PENDING",
        "16 solicitudes concurrentes",
        "149 comprobaciones",
        "94 mutaciones",
        "112 mutaciones",
        "53 aserciones",
        "43637795358",
        "43637795464",
        "43637795474",
        "43637795403",
        "43638254515",
        "43638254480",
        "43638254422",
        "43638254469",
        "43638254452",
        "43638254419",
        "NO VERIFICADAS TODAS AL REDACTAR",
        "CS8321",
        "a4e0ded1fd02b846e59358c0f2af5b51628cf7f0",
        "C05-01",
        "C05-02",
        "C05-03",
        "C05-04",
        "C05-05",
        "C05-06",
        "C05-07",
        "NO-GO para producción",
        "ACTA SERÁ EFICAZ SOLO SI",
        "POSTMERGE técnico",
        "PREMERGE documental",
        "POSTMERGE documental",
        "EXACTAMENTE tres archivos",
        "Rollback controlado",
        "8/11",
        "C06 BLOQUEADO",
        "127.0.0.1",
        "c05_05_disposable",
    ),
    "ci": (
        "permissions:",
        "contents: read",
        "persist-credentials: false",
        "fetch-depth: 2",
        "dotnet-version: '10.0.x'",
        "qa/c05_08_closure_gate.py A",
        "qa/c05_08_closure_gate.py B",
        "qa/c05_08_closure_gate.py C",
        "Crecisol.MfaRisk.ContractsTests.csproj",
        "Crecisol.MfaRisk.MySqlTests.csproj",
        "database/tests/c05_08_mfa_http_mysql.sh",
        "database/tests/c05_07_mailbox_http_mysql.sh",
        "database/tests/c05_06_recovery_http_mysql.sh",
        "database/tests/c05_05_session_http_mysql.sh",
        "Crecisol.ContextualLogin.ContractsTests.csproj",
        "Crecisol.PasswordHash.ContractsTests.csproj",
        "Crecisol.AccountIdentity.ContractsTests.csproj",
        "Crecisol.InvitationSecurity.ContractsTests.csproj",
        ".c05-08-institutional-proof",
        "GITHUB_SHA",
        "GITHUB_RUN_ID",
    ),
}

def need(value: bool, reason: str) -> None:
    if not value:
        raise AssertionError(reason)

def source() -> dict[str, str]:
    return {k: (ROOT / v).read_text(encoding="utf-8")
            for k, v in FILES.items() if k != "qa"}

def invariants(data: dict[str, str]) -> None:
    for kind, markers in GUARDS.items():
        for marker in markers:
            need(marker in data[kind],
                 "Missing C05-08 institutional security/scope marker "+kind+": "+marker)
    api = (ROOT / "backend/src/Crecisol.Api/Program.cs").read_text(encoding="utf-8")
    routes = re.findall(r'app\.Map(?:Get|Post|Put|Patch|Delete)\(\s*"([^"]+)"', api)
    need(routes == ["/api/v1/public/branding/platform"]
         and "DisposableMfaRiskUseCases" not in api
         and "MySqlDisposableMfaLedger" not in api,
         "C05-08 test MFA must never be public product API")
    app = (ROOT / "backend/src/Crecisol.Application/Identity/DisposableMfaRiskUseCases.cs"
           ).read_text(encoding="utf-8")
    sql = (ROOT / "database/tests/fixtures/c05_08_disposable_mfa.sql"
           ).read_text(encoding="utf-8")
    infra = (ROOT / "backend/src/Crecisol.Infrastructure/Crecisol.Infrastructure.csproj"
             ).read_text(encoding="utf-8")
    tester = (ROOT / "backend/tests/Crecisol.MfaRisk.MySqlTests/Program.cs"
              ).read_text(encoding="utf-8")
    need("VerifyFreshAsync" in app and
         "TryCommitCryptographicProofAsync" in app and
         "TryVerifyTotp" in app and "TryVerifyFixturePasskey" in app and
         "TryOpenReplacementAsync" in app and
         "CryptographicOperations.ZeroMemory" in app,
         "Fresh Argon2id, genuine factor proof and isolated replacement required")
    need("CREATE TRIGGER c05_08_audit_immutable_update" in sql and
         "CREATE TRIGGER c05_08_audit_immutable_delete" in sql and
         "c05_08_open_replacement" in sql and
         "consumed_utc IS NULL" in sql and
         "p_counter<=v_counter" in sql and
         "v_challenge_mail_rev<>v_mail_rev" in sql and
         "v_realm<>'TENANT'" in sql and
         "UPDATE c05_05_sessions SET revoked_utc=v_now" in sql and
         "subject_revision=subject_revision+1" in sql and
         "FOR UPDATE" in sql,
         "Protected CAS, mailbox/tenant isolation, backup single-use and global session revocation required")
    need("16 concurrent genuine HTTP/MySQL8.4" in tester and
         "Pending replacement blocks fresh step-up" in tester and
         "Consumed backup cannot" in tester and
         "PLATFORM realm cannot" in tester and
         "Unrelated actor session remains valid" in tester and
         "Expired one-time backup cannot" in tester and
         "Reactivated membership cannot resurrect old challenge" in tester and
         "Password reset does not bypass or delete MFA" in tester,
         "Real negative SQL/HTTP tests for cross-tenant, recovery, expiry and one-winner required")
    need('PackageReference Include="MySqlConnector" Version="2.6.2"' in infra,
         "Connector pin differs from reviewed C05-08 contracts")
    ci = data["ci"]
    need("workflow_dispatch:" not in ci and
         "id-token: write" not in ci and
         "deploy" not in ci.lower() and
         "permissions:\n  contents: read" in ci,
         "Institutional checks cannot write, deploy or have ID token")

def a() -> None:
    subprocess.run([sys.executable, "qa/master_source_guard.py", "--self-test"],
                   cwd=ROOT, check=True)
    subprocess.run([sys.executable, "qa/c05_08_mfa_crypto_audits.py", "A"],
                   cwd=ROOT, check=True)
    subprocess.run([sys.executable, "qa/c05_08_mfa_integration_audits.py", "A"],
                   cwd=ROOT, check=True)
    plan = (ROOT / "docs/planificacion-futura/C05_MICROFASES_PROPUESTAS.md"
            ).read_text(encoding="utf-8")
    need("C05-08 | MFA y passkeys por riesgo" in plan and
         "Inscripción/recuperación y step-up testeados" in plan and
         "No bypass por restablecer factor" in plan,
         "Canonical C05-08 requirements must match source")
    invariants(source())
    print("PASS C05-08 institutional A: canonical Word/C05-07, exact technical SHA and scoped contract")

def b() -> None:
    original = source()
    invariants(original)
    killed = 0
    for kind, markers in GUARDS.items():
        for marker in markers:
            specimen = dict(original)
            specimen[kind] = original[kind].replace(marker, "[C05_08_REMOVED_GUARD]")
            need(specimen[kind] != original[kind], "Ineffective guard mutation")
            try:
                invariants(specimen)
            except AssertionError:
                killed += 1
            else:
                raise AssertionError("C05-08 institutional bypass: "+kind+": "+marker)
    need(killed >= 80, "Insufficient documentary/security negative mutations")
    print(f"PASS C05-08 institutional B: {killed} adverse static mutations rejected")
    print("MUST run real .NET10 HTTP/MySQL8.4 separately; static checks alone never certify")

def git(*args: str) -> str:
    return subprocess.check_output(["git", *args], cwd=ROOT,
                                   text=True, stderr=subprocess.DEVNULL).strip()

def c() -> None:
    need(os.getenv("GITHUB_ACTIONS") == "true" and
         os.getenv("GITHUB_REPOSITORY") == REPO,
         "Only actual CRECISOL GitHub CI can attest institutional closure")
    sha, run = os.getenv("GITHUB_SHA", ""), os.getenv("GITHUB_RUN_ID", "")
    need(re.fullmatch(r"[0-9a-f]{40}", sha) is not None and
         re.fullmatch(r"[0-9]+", run) is not None and
         git("rev-parse", "HEAD") == sha,
         "Exact SHA and run required")
    for stage in ("A", "B", "build", "crypto", "mysql", "regressions"):
        proof = PROOF / stage
        need(proof.is_file() and proof.read_text(encoding="utf-8").strip() ==
             f"PASS {stage} sha={sha} run={run}",
             "Missing genuine same-run C05-08 institutional proof: "+stage)
    if os.getenv("GITHUB_EVENT_NAME") == "pull_request":
        f = Path(os.getenv("GITHUB_EVENT_PATH", ""))
        need(f.is_file(), "Signed PR event required")
        pr = json.loads(f.read_text(encoding="utf-8"))["pull_request"]
        base, head = pr["base"], pr["head"]
        need(base["ref"] == "dev" and base["sha"] == BASE and
             head["ref"] == BRANCH and
             base["repo"]["full_name"] == REPO and
             head["repo"]["full_name"] == REPO,
             "Unreviewed repository, base SHA or PR branch")
        need(git("rev-list", "--parents", "-n", "1", "HEAD").split() ==
             [sha, base["sha"], head["sha"]],
             "PR checkout must be actual synthetic merge of exact base/head")
        changed = git("diff", "--name-only", base["sha"], head["sha"]).splitlines()
    else:
        need(os.getenv("GITHUB_EVENT_NAME") == "push" and
             os.getenv("GITHUB_REF") == "refs/heads/dev",
             "Only dev POSTMERGE can certify")
        changed = git("show", "--format=", "--name-only", sha).splitlines()
    need(set(changed) == ALLOWED and len(changed) == len(ALLOWED),
         "Institutional diff must be EXACTLY THREE approved files: "+repr(changed))
    invariants(source())
    print(f"PASS C05-08 institutional C: A/B .NET10 crypto, real HTTP/MySQL8.4, C05 regressions SHA={sha} run={run}")
    print("TEST ONLY: no authentic WebAuthn registration, KMS, privileged product access or deployment")

if __name__ == "__main__":
    try:
        need(len(sys.argv) == 2 and sys.argv[1] in ("A", "B", "C"),
             "Usage: qa/c05_08_closure_gate.py A|B|C")
        {"A": a, "B": b, "C": c}[sys.argv[1]]()
    except (AssertionError, OSError, KeyError, ValueError,
            subprocess.SubprocessError) as exc:
        print("FAIL C05-08 institutional gate: "+str(exc), file=sys.stderr)
        sys.exit(1)
