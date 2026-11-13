#!/usr/bin/env python3
"""C05-07 institutional A/B/C: only test-only .NET10 HTTP/MySQL 8.4."""
from __future__ import annotations

import json
import os
from pathlib import Path
import re
import subprocess
import sys

from c05_01_preparation_audits import ROOT

REPO = "crecisol-platform/CRECISOL"
BRANCH = "feature/c05-07-mailbox-certification-20261113"
BASE = "426c3e44cb0f1bd25ef09341dad68fbe0123e985"
FILES = {
    "act": "docs/c05/ACTA_CERTIFICACION_ACOTADA_C05_07_2026_11_13.md",
    "qa": "qa/c05_07_closure_gate.py",
    "ci": ".github/workflows/c05-07-certification.yml",
}
ALLOWED = set(FILES.values())
PROOF = ROOT / ".c05-07-closure-proof"
GUARDS = {
    "act": (
        "Acta institucional CONDICIONADA",
        "ACTA NO EFICAZ",
        "C05-07 NO CERTIFICADA",
        "C05-08 BLOQUEADA",
        "crecisol-platform/CRECISOL",
        "Documento Maestro Único",
        "MASTER_REFERENCE.md",
        "43364292640",
        "6865602159",
        "4a97dbd29b3acc93e9c20ae09e0e26945af98ea0",
        "09bf9acd5181635a22d776ef4d675f29378d6bd6",
        BASE,
        "Verificación de correo y cambios sensibles",
        "Reautenticación y confirmaciones auditable",
        "No cambio de correo sin comprobación",
        "Argon2id",
        "HMAC-SHA256",
        "256 bits",
        "MySqlConnector@2.6.2",
        "MySQL8.4",
        "79 aserciones",
        "229 mutaciones",
        "16 peticiones HTTP",
        "dos",
        "ITrustedImmediatePasswordReauthentication",
        "freshPassword.VerifyFreshAsync",
        "FOR UPDATE",
        "una única fila",
        "SMTP",
        "127.0.0.1",
        "43516381771",
        "43516381498",
        "43516381502",
        "43517108400",
        "43517108681",
        "43517108660",
        "43517108416",
        "43517108534",
        "NO VERIFICADAS AL REDACTAR",
        "CS0165",
        "43515462193",
        "43515961005",
        "C05-01",
        "C05-02",
        "C05-03",
        "C05-04",
        "C05-05",
        "C05-06",
        "NO-GO",
        "ACTA SERÁ EFICAZ SOLO SI",
        "PREMERGE documental",
        "POSTMERGE documental",
        "EXACTAMENTE tres archivos",
        "6.",
        "7/11 C05",
        "C05-08 habilitada",
        "C06 BLOQUEADO",
        "Rollback controlado",
    ),
    "ci": (
        "permissions:",
        "contents: read",
        "persist-credentials: false",
        "fetch-depth: 2",
        "dotnet-version: '10.0.x'",
        "qa/c05_07_closure_gate.py A",
        "qa/c05_07_closure_gate.py B",
        "qa/c05_07_closure_gate.py C",
        "Crecisol.MailboxApproval.MySqlTests.csproj",
        "database/tests/c05_07_mailbox_http_mysql.sh",
        "Crecisol.PasswordRecovery.MySqlTests.csproj",
        "database/tests/c05_06_recovery_http_mysql.sh",
        "Crecisol.SessionRevocation.MySqlTests.csproj",
        "database/tests/c05_05_session_http_mysql.sh",
        "Crecisol.ContextualLogin.ContractsTests.csproj",
        "Crecisol.PasswordHash.ContractsTests.csproj",
        "Crecisol.AccountIdentity.ContractsTests.csproj",
        "Crecisol.InvitationSecurity.ContractsTests.csproj",
        ".c05-07-closure-proof",
        "GITHUB_SHA",
        "GITHUB_RUN_ID",
    ),
}


def need(value: bool, reason: str) -> None:
    if not value:
        raise AssertionError(reason)


def sources() -> dict[str, str]:
    return {key: (ROOT / path).read_text(encoding="utf-8")
            for key, path in FILES.items() if key != "qa"}


def invariants(data: dict[str, str]) -> None:
    for kind, markers in GUARDS.items():
        for marker in markers:
            need(marker in data[kind],
                 "Missing C05-07 institutional marker: " + kind + ": " + marker)

    api = (ROOT / "backend/src/Crecisol.Api/Program.cs").read_text(encoding="utf-8")
    routes = re.findall(r'app\.Map(?:Get|Post|Put|Patch|Delete)\(\s*"([^"]+)"',
                        api)
    need(routes == ["/api/v1/public/branding/platform"] and
         "VerifyAndChangeSensitiveMailbox" not in api and
         "MySqlDisposableSensitiveMailboxLedger" not in api,
         "Product API must NOT expose sensitive mailbox changes")

    app = (ROOT / "backend/src/Crecisol.Application/Identity/SensitiveMailboxVerification.cs"
           ).read_text(encoding="utf-8")
    crypto = (ROOT / "backend/src/Crecisol.CredentialSecurity/MailboxApprovalTokenProtector.cs"
              ).read_text(encoding="utf-8")
    sql = (ROOT / "database/tests/fixtures/c05_07_disposable_mailbox.sql"
           ).read_text(encoding="utf-8")
    tests = (ROOT / "backend/tests/Crecisol.MailboxApproval.MySqlTests/Program.cs"
             ).read_text(encoding="utf-8")

    need("freshPassword.VerifyFreshAsync(" in app and
         "ContextualLoginPreparationStatus.Ready" in app and
         "proof.HostRevision != verifiedHost.Revision" in app and
         "ConfirmPreviousMailbox" in app and
         "ConfirmReplacementMailbox" in app and
         "ledger.TryConfirmChangeAsync" in app,
         "Fresh C05-04 Argon2id and independent approvals are mandatory")
    need("RandomNumberGenerator.GetBytes(32)" in crypto and
         "HMACSHA256.HashData(secret, ascii)" in crypto and
         "CryptographicOperations.ZeroMemory(secret)" in crypto,
         "Secure separate 256-bit token and HMAC required")
    need("CREATE TRIGGER c05_07_audit_no_update" in sql and
         "CREATE TRIGGER c05_07_audit_no_delete" in sql and
         "UNION ALL" in sql and
         "WHERE change_id=v_lookup AND tenant_id=p_tenant FOR UPDATE" in sql and
         "p_token=v_oldtoken OR p_token=v_newtoken" in sql and
         "old_confirmed_utc" in sql and
         "new_confirmed_utc" in sql and
         "WHERE actor_id=v_actor AND revoked_utc IS NULL" in sql and
         "SET state='APPLIED'" in sql,
         "Preserve one-row MySQL CAS, audit immutability and session revocation")
    need("16 concurrent real HTTP/MySQL second confirmations: exactly 1 winner" in tests and
         "New address alone cannot replace old verified address" in tests and
         "freshReauthentication.Calls>=2" in tests and
         "Append-only database audit cannot be deleted" in tests,
         "Real negative tests for double approval and current reauth required")
    infra = (ROOT / "backend/src/Crecisol.Infrastructure/Crecisol.Infrastructure.csproj"
             ).read_text(encoding="utf-8")
    need('PackageReference Include="MySqlConnector" Version="2.6.2"' in infra,
         "Historic Infrastructure MySqlConnector pin must be unchanged")
    need("workflow_dispatch:" not in data["ci"] and
         "id-token: write" not in data["ci"] and
         "deploy" not in data["ci"].lower(),
         "Institutional CI read-only and no deployments")


def a() -> None:
    subprocess.run([sys.executable, "qa/master_source_guard.py", "--self-test"],
                   cwd=ROOT, check=True)
    subprocess.run([sys.executable, "qa/c05_07_mailbox_audits.py", "A"],
                   cwd=ROOT, check=True)
    plan = (ROOT / "docs/planificacion-futura/C05_MICROFASES_PROPUESTAS.md"
            ).read_text(encoding="utf-8")
    need("C05-07 | Verificación de correo y cambios sensibles" in plan and
         "No cambio de correo sin comprobación" in plan,
         "Canonical C05-07 scope missing")
    invariants(sources())
    print("PASS CRECISOL C05-07 institutional A: Word/C05-06, reauth and double-mailbox proof")


def b() -> None:
    original = sources()
    invariants(original)
    count = 0
    for kind, markers in GUARDS.items():
        for marker in markers:
            mutant = dict(original)
            mutant[kind] = original[kind].replace(
                marker, "[REMOVED_C05_07_CLOSURE_GUARD]")
            need(mutant[kind] != original[kind], "Ineffective adversarial mutation")
            try:
                invariants(mutant)
            except AssertionError:
                count += 1
            else:
                raise AssertionError("Documentary scope bypass: "+kind+": "+marker)
    need(count >= 65, "Insufficient independent documentary/security mutations")
    print(f"PASS CRECISOL C05-07 institutional B: {count} documentary guard mutations denied")
    print("These are static mutations; real MySQL/HTTP and prior regressions run separately")


def git(*args: str) -> str:
    return subprocess.check_output(["git", *args], cwd=ROOT,
                                   text=True, stderr=subprocess.DEVNULL).strip()


def c() -> None:
    need(os.getenv("GITHUB_ACTIONS") == "true" and
         os.getenv("GITHUB_REPOSITORY") == REPO,
         "Only CRECISOL GitHub CI can attest")
    sha, run = os.getenv("GITHUB_SHA", ""), os.getenv("GITHUB_RUN_ID", "")
    need(re.fullmatch(r"[0-9a-f]{40}", sha) is not None and
         re.fullmatch(r"[0-9]+", run) is not None and
         git("rev-parse", "HEAD") == sha,
         "Exact current SHA/run required")
    for stage in ("A", "B", "build", "mysql", "regressions"):
        proof = PROOF / stage
        need(proof.is_file() and proof.read_text(encoding="utf-8").strip() ==
             f"PASS {stage} sha={sha} run={run}",
             "Missing genuine same-run proof: " + stage)
    event = os.getenv("GITHUB_EVENT_NAME")
    if event == "pull_request":
        event_file = Path(os.getenv("GITHUB_EVENT_PATH", ""))
        need(event_file.is_file(), "Missing PR payload")
        pr = json.loads(event_file.read_text(encoding="utf-8"))["pull_request"]
        base, head = pr["base"], pr["head"]
        need(base["ref"] == "dev" and base["sha"] == BASE and
             head["ref"] == BRANCH and
             base["repo"]["full_name"] == REPO and
             head["repo"]["full_name"] == REPO,
             "Untrusted PR base SHA, owner or branch")
        need(git("rev-list", "--parents", "-n", "1", "HEAD").split() ==
             [sha, base["sha"], head["sha"]],
             "Unexpected synthetic PR merge ancestry")
        changed = git("diff", "--name-only", base["sha"], head["sha"]).splitlines()
    else:
        need(event == "push" and os.getenv("GITHUB_REF") == "refs/heads/dev",
             "Only dev POSTMERGE certifies")
        changed = git("show", "--format=", "--name-only", sha).splitlines()

    need(set(changed) == ALLOWED and len(changed) == len(ALLOWED),
         "Institutional diff must contain EXACTLY 3 files: " + repr(changed))
    invariants(sources())
    print(f"PASS CRECISOL C05-07 institutional C: A/B real NET10 HTTP MySQL8.4 and regressions sha={sha} run={run}")
    print("Scope synthetic only. No production mailbox ownership, delivery or sessions")


if __name__ == "__main__":
    try:
        need(len(sys.argv) == 2 and sys.argv[1] in ("A", "B", "C"),
             "Usage: qa/c05_07_closure_gate.py A|B|C")
        {"A": a, "B": b, "C": c}[sys.argv[1]]()
    except (AssertionError, OSError, ValueError, KeyError,
            subprocess.SubprocessError) as exc:
        print("FAIL CRECISOL C05-07 institutional gate: " + str(exc),
              file=sys.stderr)
        sys.exit(1)
