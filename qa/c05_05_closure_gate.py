#!/usr/bin/env python3
"""CRECISOL C05-05 institutional certification only for disposable HTTP/MySQL."""
from __future__ import annotations

import json
import os
from pathlib import Path
import re
import subprocess
import sys

from c05_01_preparation_audits import ROOT

REPO = "crecisol-platform/CRECISOL"
FEATURE = "feature/c05-05-session-certification-20261113"
BASE = "c30bafa20add32b6042283e0c9f853d177d603d3"
FILES = {
    "act": "docs/c05/ACTA_CERTIFICACION_ACOTADA_C05_05_2026_11_13.md",
    "qa": "qa/c05_05_closure_gate.py",
    "workflow": ".github/workflows/c05-05-certification.yml",
}
ALLOWED = set(FILES.values())
PROOF = ROOT / ".c05-05-closure-proof"
GUARDS = {
    "act": (
        "Acta institucional CONDICIONADA",
        "PROPUESTA NO EFICAZ",
        "C05-05 NO CERTIFICADA Y C05-06 BLOQUEADA",
        "Documento Maestro Único",
        "358df52d785cb4074790ce7841c5c8d0d4873908",
        "6863804285",
        BASE,
        "3b912a08b019a80c0cffbdfd1c1cf36aa0918d5f",
        "Rotación y logout verificables en API",
        "Token revocado no vuelve a funcionar",
        "HMAC-SHA256",
        "256 bits CSPRNG",
        "Crecisol.CredentialSecurity",
        "MySqlConnector@2.6.2",
        "MySQL 8.4",
        "52 aserciones",
        "165 mutaciones adversarias",
        "16 rotaciones concurrentes",
        "exactamente un ganador",
        "C05-01 100",
        "C05-02 102",
        "C05-03 68",
        "C05-04 95",
        "43505622170",
        "43505622204",
        "43505622324",
        "43506355739",
        "43506355760",
        "43506355742",
        "43506355741",
        "43506355907",
        "NO VERIFICADAS AL REDACTAR",
        "NO-GO productivo",
        "PREMERGE 2/2",
        "POSTMERGE 2/2",
        "EFICAZ SOLO SI",
        "CERTIFICADA EXCLUSIVAMENTE",
        "5/11 C05",
        "C05-06 habilitada",
        "C06 bloqueado",
        "Rollback",
    ),
    "workflow": (
        "permissions:",
        "contents: read",
        "persist-credentials: false",
        "fetch-depth: 2",
        "dotnet-version: '10.0.x'",
        "qa/c05_05_closure_gate.py A",
        "qa/c05_05_closure_gate.py B",
        "qa/c05_05_closure_gate.py C",
        "Crecisol.SessionRevocation.MySqlTests.csproj",
        "database/tests/c05_05_session_http_mysql.sh",
        "Crecisol.AccountIdentity.ContractsTests.csproj",
        "Crecisol.InvitationSecurity.ContractsTests.csproj",
        "Crecisol.PasswordHash.ContractsTests.csproj",
        "Crecisol.ContextualLogin.ContractsTests.csproj",
        ".c05-05-closure-proof",
        "GITHUB_SHA",
        "GITHUB_RUN_ID",
    ),
}


def need(ok: bool, why: str) -> None:
    if not ok:
        raise AssertionError(why)


def sources() -> dict[str, str]:
    return {k: (ROOT / p).read_text(encoding="utf-8")
            for k, p in FILES.items() if k != "qa"}


def invariants(data: dict[str, str]) -> None:
    for kind, markers in GUARDS.items():
        for marker in markers:
            need(marker in data[kind], "Missing C05-05 closure safeguard: " + kind + " " + marker)
    api = (ROOT / "backend/src/Crecisol.Api/Program.cs").read_text(encoding="utf-8")
    actual_routes = re.findall(
        r'app\.Map(?:Get|Post|Put|Patch|Delete)\(\s*"([^"]+)"', api)
    need(actual_routes == ["/api/v1/public/branding/platform"] and
         "ScopedSessionLifecycle" not in api and
         "MySqlDisposableSessionLedger" not in api,
         "C05-05 MUST remain a disposable test-only HTTP session module")
    app = (ROOT / "backend/src/Crecisol.Application/Identity/SessionLifecycleContracts.cs"
           ).read_text(encoding="utf-8")
    crypto = (ROOT / "backend/src/Crecisol.CredentialSecurity/OpaqueSessionTokenProtector.cs"
              ).read_text(encoding="utf-8")
    sql = (ROOT / "database/tests/fixtures/c05_05_disposable_sessions.sql"
           ).read_text(encoding="utf-8")
    need("ContextualLoginPreparationStatus.Ready" in app and
         "VerifiedTenantHostHint trustedHost" in app and
         "Task<bool> TryRotateAsync" in app and
         "Task<bool> TryRevokeAsync" in app,
         "Backend-only C05-04 and exact verified host gate required")
    need("RandomNumberGenerator.GetBytes(TokenBytes)" in crypto and
         "HMACSHA256.HashData(key, raw)" in crypto and
         "CryptographicOperations.ZeroMemory(key)" in crypto,
         "CSPRNG and keyed HMAC contract required")
    need("SQL SECURITY DEFINER" in sql and
         "FOR UPDATE;" in sql and
         "token_digest=p_new" in sql and
         "revoked_utc=v_now" in sql and
         "v_session_revision=v_current_revision" in sql and
         "WHERE tenant_id=p_tenant AND token_digest=p_digest" in sql,
         "Real SQL transactional rotate, tenant isolation and revoke required")
    legacy = (ROOT / "backend/src/Crecisol.Infrastructure/Crecisol.Infrastructure.csproj"
              ).read_text(encoding="utf-8")
    need('PackageReference Include="MySqlConnector" Version="2.6.2"' in legacy and
         "Argon2" not in legacy, "C03-04 Infrastructure pinned baseline unchanged")
    need("workflow_dispatch:" not in data["workflow"] and
         "id-token: write" not in data["workflow"] and
         "deploy" not in data["workflow"].lower() and
         "GRANT ALL" not in data["workflow"],
         "No privilege escalation or deployment")


def audit_a() -> None:
    subprocess.run([sys.executable, "qa/master_source_guard.py", "--self-test"],
                   cwd=ROOT, check=True)
    subprocess.run([sys.executable, "qa/c05_05_session_audits.py", "A"],
                   cwd=ROOT, check=True)
    act = sources()["act"]
    for marker in ("43364292640", "43505622170", "43506355739",
                   "POSTMERGE", "MySQL 8.4", "A/B/C"):
        need(marker in act, "Missing technical CI proof: " + marker)
    invariants(sources())
    print("PASS CRECISOL C05-05 closure A: Word, predecessor, functional HTTP/MySQL and scope traced")


def audit_b() -> None:
    original = sources()
    invariants(original)
    count = 0
    for kind, markers in GUARDS.items():
        for marker in markers:
            mutant = dict(original)
            mutant[kind] = original[kind].replace(marker, "[REMOVED_C05_05_CERT_GUARD]")
            need(mutant[kind] != original[kind], "Ineffective adversarial mutation")
            try:
                invariants(mutant)
            except AssertionError:
                count += 1
            else:
                raise AssertionError("Scope/safety mutation bypassed: "+kind+" "+marker)
    need(count >= 45, "Too few negative institutional mutations")
    print(f"PASS CRECISOL C05-05 closure B: {count} documentary/security guard removals rejected")
    print("STATIC documentation mutations; the real MySQL and HTTP tests run separately")


def git(*args: str) -> str:
    return subprocess.check_output(
        ["git", *args], cwd=ROOT, text=True, stderr=subprocess.DEVNULL).strip()


def audit_c() -> None:
    need(os.getenv("GITHUB_ACTIONS") == "true" and
         os.getenv("GITHUB_REPOSITORY") == REPO,
         "Only CRECISOL CI can attest institutional closure")
    sha, run = os.getenv("GITHUB_SHA", ""), os.getenv("GITHUB_RUN_ID", "")
    need(re.fullmatch(r"[0-9a-f]{40}", sha) is not None and
         re.fullmatch(r"[0-9]+", run) is not None and
         git("rev-parse", "HEAD") == sha,
         "Exact current SHA and run identity required")
    for stage in ("A", "B", "build", "mysql", "regressions"):
        path = PROOF / stage
        need(path.is_file() and path.read_text(encoding="utf-8").strip() ==
             f"PASS {stage} sha={sha} run={run}",
             "Missing same-SHA actual proof: "+stage)

    event = os.getenv("GITHUB_EVENT_NAME")
    if event == "pull_request":
        event_path = Path(os.getenv("GITHUB_EVENT_PATH", ""))
        need(event_path.is_file(), "PR payload missing")
        pr = json.loads(event_path.read_text(encoding="utf-8"))["pull_request"]
        base, head = pr["base"], pr["head"]
        need(base["ref"] == "dev" and base["sha"] == BASE and
             head["ref"] == FEATURE and
             base["repo"]["full_name"] == REPO and
             head["repo"]["full_name"] == REPO,
             "Unexpected source repo/base SHA/feature branch")
        need(git("rev-list", "--parents", "-n", "1", "HEAD").split() ==
             [sha, base["sha"], head["sha"]],
             "PR synthetic merge ancestry mismatch")
        changed = git("diff", "--name-only", base["sha"], head["sha"]).splitlines()
    else:
        need(event == "push" and os.getenv("GITHUB_REF") == "refs/heads/dev",
             "Only dev POSTMERGE may certify")
        changed = git("show", "--format=", "--name-only", sha).splitlines()
    need(set(changed) == ALLOWED and len(changed) == len(ALLOWED),
         "Institutional PR modified unreviewed files: " + repr(changed))
    invariants(sources())
    print(f"PASS CRECISOL C05-05 closure C: real A/B/HTTP/MySQL/regressions sha={sha} run={run}")
    print("Scope limited to disposable session lifecycle; NO production sign-in, tokens or tenant grants")


if __name__ == "__main__":
    try:
        need(len(sys.argv) == 2 and sys.argv[1] in ("A", "B", "C"),
             "Usage: qa/c05_05_closure_gate.py A|B|C")
        {"A": audit_a, "B": audit_b, "C": audit_c}[sys.argv[1]]()
    except (AssertionError, OSError, KeyError, ValueError,
            subprocess.SubprocessError) as exc:
        print("FAIL CRECISOL C05-05 closure: " + str(exc), file=sys.stderr)
        sys.exit(1)
