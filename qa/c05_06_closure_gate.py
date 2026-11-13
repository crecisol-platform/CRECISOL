#!/usr/bin/env python3
"""CRECISOL C05-06 A/B/C: institutional approval ONLY isolated recovery."""
from __future__ import annotations

import json
import os
from pathlib import Path
import re
import subprocess
import sys

from c05_01_preparation_audits import ROOT

REPO = "crecisol-platform/CRECISOL"
BRANCH = "feature/c05-06-recovery-certification-20261113"
BASE = "2620d6fb274bd0fd6d8d6af24b1f9744fd08664b"
FILES = {
    "act": "docs/c05/ACTA_CERTIFICACION_ACOTADA_C05_06_2026_11_13.md",
    "qa": "qa/c05_06_closure_gate.py",
    "ci": ".github/workflows/c05-06-certification.yml",
}
ALLOWED = set(FILES.values())
PROOF = ROOT / ".c05-06-closure-proof"
GUARDS = {
    "act": (
        "C05-06 · Acta institucional CONDICIONADA",
        "NO EFICAZ",
        "C05-06 NO CERTIFICADA, C05-07 BLOQUEADA",
        "Documento Maestro Único",
        "MASTER_REFERENCE.md",
        "43364292640",
        "6864907945",
        "f34a751ee10dddc47c8f75fb49291a4741d5cca5",
        "0884837bb8ba75d8f73e47847075c2a8e485d912",
        BASE,
        "Tokens de un solo uso con expiración y revocación",
        "No reutilizar enlaces",
        "HMAC-SHA256",
        "256-bit CSPRNG",
        "MySqlConnector@2.6.2",
        "MySQL 8.4",
        "10 minutos UTC",
        "91 aserciones",
        "174 mutaciones estáticas",
        "16 consumos concurrentes",
        "exactamente 1 éxito y 15 rechazados",
        "C05-01",
        "C05-02",
        "C05-03",
        "C05-04",
        "C05-05",
        "43510570679",
        "43510570816",
        "43510570762",
        "43511437305",
        "43511437348",
        "43511437334",
        "43511437395",
        "43511437332",
        "PENDIENTE de conclusión AL REDACTAR",
        "NO-GO total para producción",
        "PREMERGE documental 2/2",
        "POSTMERGE documental 2/2",
        "SEIS puertas obligatorias",
        "CERTIFICADA EXCLUSIVAMENTE",
        "6/11 C05",
        "C05-07 habilitada",
        "C06 BLOQUEADO",
        "Rollback",
    ),
    "ci": (
        "permissions:",
        "contents: read",
        "persist-credentials: false",
        "fetch-depth: 2",
        "dotnet-version: '10.0.x'",
        "qa/c05_06_closure_gate.py A",
        "qa/c05_06_closure_gate.py B",
        "qa/c05_06_closure_gate.py C",
        "Crecisol.PasswordRecovery.MySqlTests.csproj",
        "database/tests/c05_06_recovery_http_mysql.sh",
        "Crecisol.SessionRevocation.MySqlTests.csproj",
        "database/tests/c05_05_session_http_mysql.sh",
        "Crecisol.ContextualLogin.ContractsTests.csproj",
        "Crecisol.PasswordHash.ContractsTests.csproj",
        "Crecisol.AccountIdentity.ContractsTests.csproj",
        "Crecisol.InvitationSecurity.ContractsTests.csproj",
        ".c05-06-closure-proof",
        "GITHUB_SHA",
        "GITHUB_RUN_ID",
    ),
}


def demand(ok: bool, reason: str) -> None:
    if not ok:
        raise AssertionError(reason)


def read() -> dict[str, str]:
    return {kind: (ROOT / path).read_text(encoding="utf-8")
            for kind, path in FILES.items() if kind != "qa"}


def invariants(data: dict[str, str]) -> None:
    for kind, markers in GUARDS.items():
        for marker in markers:
            demand(marker in data[kind],
                   "C05-06 institutional safeguard missing: "+kind+" "+marker)

    product_api = (ROOT / "backend/src/Crecisol.Api/Program.cs"
                   ).read_text(encoding="utf-8")
    routes = re.findall(r'app\.Map(?:Get|Post|Put|Patch|Delete)\(\s*"([^"]+)"',
                        product_api)
    demand(routes == ["/api/v1/public/branding/platform"] and
           "RecoverPasswordOnce" not in product_api and
           "MySqlDisposablePasswordRecoveryLedger" not in product_api,
           "C05-06 must not register any production recovery API")

    app = (ROOT / "backend/src/Crecisol.Application/Identity/PasswordRecoveryContracts.cs"
           ).read_text(encoding="utf-8")
    crypto = (ROOT / "backend/src/Crecisol.CredentialSecurity/PasswordRecoveryCrypto.cs"
              ).read_text(encoding="utf-8")
    sql = (ROOT / "database/tests/fixtures/c05_06_disposable_recovery.sql"
           ).read_text(encoding="utf-8")
    tests = (ROOT / "backend/tests/Crecisol.PasswordRecovery.MySqlTests/Program.cs"
             ).read_text(encoding="utf-8")
    demand("VerifiedTenantHostHint verifiedHost" in app and
           "ledger.TryConsumeAsync" in app and
           "ITrustedPasswordRecoveryDeliverySink" in app,
           "Backend-only neutral and host-verified recovery case required")
    demand("RandomNumberGenerator.GetBytes(32)" in crypto and
           "HMACSHA256.HashData(tokenKey, ascii)" in crypto and
           "HMACSHA256.HashData(mailboxKey, source)" in crypto and
           "hasher.CreateVerifier(candidate)" in crypto,
           "Real CSPRNG, domain-separated HMAC and Argon2id required")
    demand("c05_06_consume_once" in sql and
           "CREATE TRIGGER c05_06_mailbox_revision_bu" in sql and
           "CREATE TRIGGER c05_06_subject_revision_bu" in sql and
           "CREATE TRIGGER c05_06_credential_revision_bu" in sql and
           "SET revoked_utc=COALESCE(revoked_utc,v_now)" in sql and
           "SET state='CONSUMED', consumed_utc=v_now" in sql and
           "FOR UPDATE;" in sql,
           "Must retain SQL one-use CAS, revision and session revocation")
    demand("16 concurrent REAL HTTP/MySQL reset attempts: exactly ONE winner" in tests
           and "Reset revokes ALL actor sessions A/B" in tests
           and "Once-consumed password reset token can NEVER be reused" in tests
           and "Off-then-on mailbox re-verification cannot resurrect stale link" in tests,
           "Actual adversarial reset contracts must be retained")
    infra = (ROOT / "backend/src/Crecisol.Infrastructure/Crecisol.Infrastructure.csproj"
             ).read_text(encoding="utf-8")
    demand('PackageReference Include="MySqlConnector" Version="2.6.2"' in infra,
           "Previous Infrastructure dependency pin must be preserved")
    demand("workflow_dispatch:" not in data["ci"] and
           "id-token: write" not in data["ci"] and
           "deploy" not in data["ci"].lower(),
           "CI must not gain production permissions")


def audit_a() -> None:
    subprocess.run([sys.executable, "qa/master_source_guard.py", "--self-test"],
                   cwd=ROOT, check=True)
    subprocess.run([sys.executable, "qa/c05_06_recovery_audits.py", "A"],
                   cwd=ROOT, check=True)
    doc = read()["act"]
    for run_id in ("43510570679", "43510570816", "43510570762",
                   "43511437305", "43511437348", "43511437332"):
        demand(run_id in doc, "Missing exact technical provenance: "+run_id)
    invariants(read())
    print("PASS CRECISOL C05-06 closure A: canonical Word, prior C05-05, one-use recovery tests and limits")


def audit_b() -> None:
    originals = read()
    invariants(originals)
    rejected = 0
    for kind, markers in GUARDS.items():
        for marker in markers:
            mutant = dict(originals)
            mutant[kind] = originals[kind].replace(marker, "[REMOVED_C05_06_CLOSURE_GUARD]")
            demand(mutant[kind] != originals[kind], "Mutation did not change source")
            try:
                invariants(mutant)
            except AssertionError:
                rejected += 1
            else:
                raise AssertionError("Adversarial institutional guard bypass: "+kind+" "+marker)
    demand(rejected >= 50, "Insufficient independent institutional negatives")
    print(f"PASS CRECISOL C05-06 closure B: {rejected} documentary security mutations denied")
    print("Static mutations alone do not certify real recovery; HTTP/MySQL8.4 runs separately")


def git(*args: str) -> str:
    return subprocess.check_output(["git", *args], cwd=ROOT,
                                   text=True, stderr=subprocess.DEVNULL).strip()


def audit_c() -> None:
    demand(os.getenv("GITHUB_ACTIONS") == "true" and
           os.getenv("GITHUB_REPOSITORY") == REPO,
           "Only CRECISOL GitHub CI can verify documentary closure")
    sha, run = os.getenv("GITHUB_SHA", ""), os.getenv("GITHUB_RUN_ID", "")
    demand(re.fullmatch("[0-9a-f]{40}", sha) is not None and
           re.fullmatch("[0-9]+", run) is not None and
           git("rev-parse", "HEAD") == sha, "Exact SHA/run required")
    for stage in ("A","B","build","mysql","regressions"):
        path = PROOF / stage
        demand(path.is_file() and path.read_text(encoding="utf-8").strip() ==
               f"PASS {stage} sha={sha} run={run}",
               "Missing real same SHA/run evidence: "+stage)
    event = os.getenv("GITHUB_EVENT_NAME")
    if event == "pull_request":
        event_path = Path(os.getenv("GITHUB_EVENT_PATH", ""))
        demand(event_path.is_file(), "Missing signed PR event payload")
        pr = json.loads(event_path.read_text(encoding="utf-8"))["pull_request"]
        base, head = pr["base"], pr["head"]
        demand(base["ref"] == "dev" and base["sha"] == BASE and
               head["ref"] == BRANCH and
               base["repo"]["full_name"] == REPO and
               head["repo"]["full_name"] == REPO,
               "Untrusted branch, base, or repository")
        demand(git("rev-list", "--parents", "-n", "1", "HEAD").split() ==
               [sha, base["sha"], head["sha"]],
               "Synthetic merge has unexpected lineage")
        changed = git("diff", "--name-only", base["sha"], head["sha"]).splitlines()
    else:
        demand(event == "push" and os.getenv("GITHUB_REF") == "refs/heads/dev",
               "Only dev POSTMERGE may attest")
        changed = git("show", "--format=", "--name-only", sha).splitlines()
    demand(set(changed) == ALLOWED and len(changed) == len(ALLOWED),
           "Institutional diff must consist of exactly 3 files: "+repr(changed))
    invariants(read())
    print(f"PASS CRECISOL C05-06 closure C: exact A/B/NET10/MySQL8.4/previous SHA={sha} run={run}")
    print("Certification scope remains isolated HTTP and synthetic mailbox, no real product")


if __name__ == "__main__":
    try:
        demand(len(sys.argv) == 2 and sys.argv[1] in ("A","B","C"),
               "Usage: qa/c05_06_closure_gate.py A|B|C")
        {"A": audit_a, "B": audit_b, "C": audit_c}[sys.argv[1]]()
    except (AssertionError, OSError, ValueError, KeyError,
            subprocess.SubprocessError) as exc:
        print("FAIL CRECISOL C05-06 institutional closure: "+str(exc),file=sys.stderr)
        sys.exit(1)
