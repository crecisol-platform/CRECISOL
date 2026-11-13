#!/usr/bin/env python3
"""CRECISOL C05-03 institutional closure A/B/C. Scoped contracts ONLY."""
from __future__ import annotations

import json
import os
from pathlib import Path
import re
import subprocess
import sys

from c05_01_preparation_audits import ROOT

REPO = "crecisol-platform/CRECISOL"
FEATURE = "feature/c05-03-argon2id-certification-20261113"
BASE = "06186413fd8974566640d66996762da9804fe017"
FILES = {
    "act": "docs/c05/ACTA_CERTIFICACION_ACOTADA_C05_03_2026_11_13.md",
    "qa": "qa/c05_03_closure_gate.py",
    "workflow": ".github/workflows/c05-03-certification.yml",
}
ALLOWED = set(FILES.values())
PROOF = ROOT / ".c05-03-cert-proof"
GUARDS = {
    "act": (
        "Acta institucional CONDICIONADA",
        "NO EFICAZ",
        "C05-04 BLOQUEADA",
        "Documento Maestro Único",
        "C05-02 certificada",
        "6862363822",
        BASE,
        "68a3955547474ddf933eeb90c17f1eb5f99ce1c1",
        "Crecisol.CredentialSecurity",
        "Argon2id v=19",
        "Konscious.Security.Cryptography.Argon2@1.3.1",
        "100 C05-01",
        "102 C05-02",
        "99 mutaciones",
        "68 aserciones",
        "POSTMERGE técnico",
        "PENDIENTE DE VERIFICACIÓN AL REDACTAR",
        "PREMERGE",
        "POSTMERGE",
        "C05-03 CERTIFICADA EXCLUSIVAMENTE",
        "3/11 microfases C05",
        "C05-04 habilitada",
        "C05 COMPLETO NO CERTIFICADO",
        "C06 BLOQUEADO",
        "NO IMPLEMENTADOS/NO PROBADOS",
        "Rollback",
    ),
    "workflow": (
        "permissions:",
        "contents: read",
        "persist-credentials: false",
        "fetch-depth: 2",
        "dotnet-version: '10.0.x'",
        "qa/c05_03_closure_gate.py A",
        "qa/c05_03_closure_gate.py B",
        "qa/c05_03_closure_gate.py C",
        "Crecisol.PasswordHash.ContractsTests.csproj",
        "Crecisol.AccountIdentity.ContractsTests.csproj",
        "Crecisol.InvitationSecurity.ContractsTests.csproj",
        "GITHUB_SHA",
        "GITHUB_RUN_ID",
        ".c05-03-cert-proof",
    ),
}


def need(ok: bool, description: str) -> None:
    if not ok:
        raise AssertionError(description)


def sources() -> dict[str, str]:
    return {k: (ROOT / p).read_text(encoding="utf-8")
            for k, p in FILES.items() if k != "qa"}


def invariants(data: dict[str, str]) -> None:
    for kind, guards in GUARDS.items():
        for guard in guards:
            need(guard in data[kind], "Missing strict C05-03 closure guard: " + kind + " " + guard)
    need("id-token: write" not in data["workflow"] and
         "workflow_dispatch:" not in data["workflow"] and
         "deploy" not in data["workflow"].lower(),
         "No privileged workflow or deployment")
    api = (ROOT / "backend/src/Crecisol.Api/Program.cs").read_text(encoding="utf-8")
    routes = re.findall(r'app\.Map(?:Get|Post|Put|Patch|Delete)\(\s*"([^"]+)"', api)
    need(routes == ["/api/v1/public/branding/platform"],
         "Credential/login/private API must not be activated")
    legacy = (ROOT / "backend/src/Crecisol.Infrastructure/Crecisol.Infrastructure.csproj").read_text(
        encoding="utf-8")
    need('PackageReference Include="MySqlConnector" Version="2.6.2"' in legacy and
         "Konscious.Security.Cryptography.Argon2" not in legacy,
         "C03-04 historic dependency protection unchanged")
    crypto = (ROOT / "backend/src/Crecisol.CredentialSecurity/Argon2idPasswordHasher.cs").read_text(
        encoding="utf-8")
    need("RandomNumberGenerator.GetBytes(16)" in crypto and
         "CryptographicOperations.FixedTimeEquals" in crypto and
         "CryptographicOperations.ZeroMemory" in crypto and
         "PasswordCompromiseVerdict.Allowed" in crypto and
         "Argon2id(password)" in crypto and
         'parts[1] != "argon2id"' in crypto and
         "StrictUtf8.GetByteCount(candidate)" in crypto,
         "Real Argon2id must remain in implementation and fail closed")
    policy = (ROOT / "backend/src/Crecisol.Application/Identity/PasswordHashingPolicy.cs").read_text(
        encoding="utf-8")
    need("MinAcceptedRunes = 15" in policy and
         "Argon2MinimumMemoryKib = 19456" in policy and
         "Argon2MinimumIterations = 2" in policy and
         "ITrustedCompromisedPasswordScreen" in policy,
         "Credential policy security floor missing")


def audit_a() -> None:
    subprocess.run([sys.executable, "qa/master_source_guard.py", "--self-test"],
                   cwd=ROOT, check=True)
    subprocess.run([sys.executable, "qa/c05_03_password_audits.py", "A"],
                   cwd=ROOT, check=True)
    act = sources()["act"]
    for expected in (
        "43492886139", "43492886038", "43492885956",
        "43493725591", "43493725693", "43493725558",
        "43364292640", "Konscious.Security.Cryptography.Argon2",
    ):
        need(expected in act, "Missing documented technical evidence: " + expected)
    invariants(sources())
    print("PASS CRECISOL C05-03 closure A: Word, scope, implementation and pending postmerge proofs")


def audit_b() -> None:
    original = sources()
    invariants(original)
    rejected = 0
    for key, markers in GUARDS.items():
        for marker in markers:
            mutant = dict(original)
            mutant[key] = original[key].replace(marker, "[REMOVED_C05_03_CERT_GUARD]")
            need(mutant[key] != original[key], "Ineffective mutation")
            try:
                invariants(mutant)
            except AssertionError:
                rejected += 1
            else:
                raise AssertionError("Adversarial mutation accepted: " + key + marker)
    need(rejected >= 35, "Insufficient negative mutations")
    print(f"PASS CRECISOL C05-03 closure B: {rejected} security/documentation guard removals rejected")
    print("NO real user hash, external breach provider, login, session or membership certified")


def git(*args: str) -> str:
    return subprocess.check_output(
        ["git", *args], cwd=ROOT, text=True,
        stderr=subprocess.DEVNULL).strip()


def audit_c() -> None:
    need(os.getenv("GITHUB_ACTIONS") == "true" and
         os.getenv("GITHUB_REPOSITORY") == REPO,
         "C05-03 institutional CI must execute in CRECISOL only")
    sha, run = os.getenv("GITHUB_SHA", ""), os.getenv("GITHUB_RUN_ID", "")
    need(re.fullmatch(r"[0-9a-f]{40}", sha) is not None and
         re.fullmatch(r"[0-9]+", run) is not None and
         git("rev-parse", "HEAD") == sha,
         "Exact merged checkout/run identity required")
    for stage in ("A", "B", "crypto", "regression"):
        file = PROOF / stage
        need(file.is_file() and
             file.read_text(encoding="utf-8").strip() ==
             f"PASS {stage} sha={sha} run={run}",
             "Missing same SHA/run proof: " + stage)

    event = os.getenv("GITHUB_EVENT_NAME")
    if event == "pull_request":
        event_path = Path(os.getenv("GITHUB_EVENT_PATH", ""))
        need(event_path.is_file(), "PR payload missing")
        pr = json.loads(event_path.read_text(encoding="utf-8"))["pull_request"]
        base, head = pr["base"], pr["head"]
        need(base["ref"] == "dev" and head["ref"] == FEATURE and
             base["sha"] == BASE and
             base["repo"]["full_name"] == REPO and
             head["repo"]["full_name"] == REPO,
             "Unexpected base or branch for C05-03 certification")
        need(git("rev-list", "--parents", "-n", "1", "HEAD").split() ==
             [sha, base["sha"], head["sha"]],
             "Exact synthetic merge parents required")
        changed = git("diff", "--name-only", base["sha"], head["sha"]).splitlines()
    else:
        need(event == "push" and os.getenv("GITHUB_REF") == "refs/heads/dev",
             "Only dev integration may attest postmerge")
        changed = git("show", "--format=", "--name-only", sha).splitlines()
    need(set(changed) == ALLOWED and len(changed) == len(ALLOWED),
         "Unexpected/missing files: " + repr(changed))
    invariants(sources())
    print(f"PASS CRECISOL C05-03 closure C: same-run A/B/Argon2id/.NET10 sha={sha} run={run}")
    print("ONLY contracted password policy and cryptography; no login/session/productive identity")


if __name__ == "__main__":
    try:
        need(len(sys.argv) == 2 and sys.argv[1] in ("A", "B", "C"),
             "Usage: qa/c05_03_closure_gate.py A|B|C")
        {"A": audit_a, "B": audit_b, "C": audit_c}[sys.argv[1]]()
    except (AssertionError, OSError, ValueError, KeyError,
            subprocess.SubprocessError) as e:
        print("FAIL CRECISOL C05-03 closure gate: " + str(e), file=sys.stderr)
        sys.exit(1)
