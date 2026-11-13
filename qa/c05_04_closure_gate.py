#!/usr/bin/env python3
"""CRECISOL C05-04 scoped institutional A/B/C, never actual login certification."""
from __future__ import annotations

import json
import os
from pathlib import Path
import re
import subprocess
import sys

from c05_01_preparation_audits import ROOT

REPO = "crecisol-platform/CRECISOL"
FEATURE = "feature/c05-04-contextual-login-certification-20261113"
BASE = "be0a0fcee656ee849430a87f85ce6842afaa0261"
FILES = {
    "act": "docs/c05/ACTA_CERTIFICACION_ACOTADA_C05_04_2026_11_13.md",
    "qa": "qa/c05_04_closure_gate.py",
    "workflow": ".github/workflows/c05-04-certification.yml",
}
ALLOWED = set(FILES.values())
PROOF = ROOT / ".c05-04-closure-proof"
GUARDS = {
    "act": (
        "C05-04 · Acta institucional CONDICIONADA",
        "NO EFICAZ",
        "C05-04 NO CERTIFICADA Y C05-05 BLOQUEADA",
        "Documento Maestro Único",
        "C05-03 certificada",
        "6863110105",
        "70d68817269536b82e9e86bde0e7f448a5a48b6e",
        BASE,
        "568ab1a43bf1473284623ed89fb06abb9221ab65",
        "Vinculación de pertenencia sin selector de empresa",
        "No confundir sesión de tenant A y B",
        "Crecisol.CredentialSecurity",
        "Argon2id",
        "PHC almacenado no exportable",
        "95 aserciones reales",
        "112 mutaciones adversarias rechazadas",
        "POSTMERGE técnico",
        "PENDIENTES DE CONCLUSIÓN al redactar",
        "PREMERGE",
        "POSTMERGE",
        "CERTIFICADA EXCLUSIVAMENTE",
        "4/11 del C05",
        "C05-05 habilitada",
        "C05 completo NO CERTIFICADO",
        "C06 BLOQUEADO",
        "NO-GO",
        "Rollback",
    ),
    "workflow": (
        "permissions:",
        "contents: read",
        "persist-credentials: false",
        "fetch-depth: 2",
        "dotnet-version: '10.0.x'",
        "qa/c05_04_closure_gate.py A",
        "qa/c05_04_closure_gate.py B",
        "qa/c05_04_closure_gate.py C",
        "Crecisol.ContextualLogin.ContractsTests.csproj",
        "Crecisol.AccountIdentity.ContractsTests.csproj",
        "Crecisol.InvitationSecurity.ContractsTests.csproj",
        "Crecisol.PasswordHash.ContractsTests.csproj",
        ".c05-04-closure-proof",
        "GITHUB_SHA",
        "GITHUB_RUN_ID",
    ),
}


def require(ok: bool, reason: str) -> None:
    if not ok:
        raise AssertionError(reason)


def read() -> dict[str, str]:
    return {k: (ROOT / v).read_text(encoding="utf-8")
            for k, v in FILES.items() if k != "qa"}


def invariants(data: dict[str, str]) -> None:
    for kind, guards in GUARDS.items():
        for marker in guards:
            require(marker in data[kind],
                    f"Missing C05-04 closure {kind} safeguard: {marker}")
    require("id-token: write" not in data["workflow"] and
            "workflow_dispatch:" not in data["workflow"] and
            "deploy" not in data["workflow"].lower(),
            "CI may not gain privileges or activate production")
    api = (ROOT / "backend/src/Crecisol.Api/Program.cs").read_text(encoding="utf-8")
    routes = re.findall(r'app\.Map(?:Get|Post|Put|Patch|Delete)\(\s*"([^"]+)"', api)
    require(routes == ["/api/v1/public/branding/platform"],
            "No private login, cookies, sessions or authentication endpoints")
    require("PrepareContextualPasswordLogin" not in api,
            "No login composition in product")
    app = (ROOT / "backend/src/Crecisol.Application/Identity/PrepareContextualPasswordLogin.cs").read_text(
        encoding="utf-8")
    require("internal string ExportForInternalVerifier()" in app and
            "private string EncodedVerifier" in app and
            "ContextualLoginPreparation.Ambiguous" in app and
            "exact.Realm != TrustedLoginRealm.TenantAccount" in app and
            "firstLinks!.ToArray()" in app and
            "!firstFrozen.SequenceEqual(againLinks!)" in app and
            "return ContextualLoginPreparation.Ready(" in app and
            "hostResolver.ResolveAsync" in app,
            "C05-04 host/PHC/realm/tenant race safety must remain")
    crypto = (ROOT /
        "backend/src/Crecisol.CredentialSecurity/Argon2idLoginCandidateVerifier.cs"
        ).read_text(encoding="utf-8")
    require("hasher.Verify(candidate.Span, storedPhc)" in crypto,
            "Genuine C05-03 Argon2id adapter required")
    legacy = (ROOT / "backend/src/Crecisol.Infrastructure/Crecisol.Infrastructure.csproj").read_text(
        encoding="utf-8")
    require('PackageReference Include="MySqlConnector" Version="2.6.2"' in legacy and
            "Argon2" not in legacy,
            "C03-04 historic Infrastructure dependency must not change")


def audit_a() -> None:
    subprocess.run([sys.executable, "qa/master_source_guard.py", "--self-test"],
                   cwd=ROOT, check=True)
    subprocess.run([sys.executable, "qa/c05_04_contextual_login_audits.py", "A"],
                   cwd=ROOT, check=True)
    data = read()
    for run in ("43498076795", "43498076759", "43498076955",
                "43498783259", "43498783230", "43498783272",
                "43498783092", "43364292640"):
        require(run in data["act"], "Document lacks exact source evidence "+run)
    invariants(data)
    print("PASS CRECISOL C05-04 closure A: Word, trusted no-selector context, predecessor and CI provenance")


def audit_b() -> None:
    original = read()
    invariants(original)
    rejected = 0
    for kind, markers in GUARDS.items():
        for marker in markers:
            mutant = dict(original)
            mutant[kind] = original[kind].replace(marker, "[REMOVED_C05_04_CERT_GUARD]")
            require(mutant[kind] != original[kind], "Ineffective negative mutation")
            try:
                invariants(mutant)
            except AssertionError:
                rejected += 1
            else:
                raise AssertionError("Adversarial closure mutation bypassed: " +
                                     kind + ":" + marker)
    require(rejected >= 35, "Insufficient adversarial coverage")
    print(f"PASS CRECISOL C05-04 closure B: {rejected} scope/safety guard removals rejected")
    print("STATIC MUTATIONS DO NOT PROVE PRODUCTIVE LOGIN OR SESSION SECURITY")


def git(*args: str) -> str:
    return subprocess.check_output(
        ["git", *args], cwd=ROOT, text=True, stderr=subprocess.DEVNULL).strip()


def audit_c() -> None:
    require(os.getenv("GITHUB_ACTIONS") == "true" and
            os.getenv("GITHUB_REPOSITORY") == REPO,
            "Institutional evidence requires exact CRECISOL GitHub CI")
    sha, run = os.getenv("GITHUB_SHA", ""), os.getenv("GITHUB_RUN_ID", "")
    require(re.fullmatch("[0-9a-f]{40}", sha) is not None and
            re.fullmatch("[0-9]+", run) is not None and
            git("rev-parse", "HEAD") == sha,
            "Exact SHA and run ID required")
    for stage in ("A", "B", "context", "predecessors"):
        path = PROOF / stage
        require(path.is_file() and path.read_text(encoding="utf-8").strip() ==
                f"PASS {stage} sha={sha} run={run}",
                "Missing real same-SHA evidence: "+stage)

    event = os.getenv("GITHUB_EVENT_NAME")
    if event == "pull_request":
        event_path = Path(os.getenv("GITHUB_EVENT_PATH", ""))
        require(event_path.is_file(), "Missing PR event payload")
        pr = json.loads(event_path.read_text(encoding="utf-8"))["pull_request"]
        base, head = pr["base"], pr["head"]
        require(base["ref"] == "dev" and head["ref"] == FEATURE and
                base["sha"] == BASE and
                base["repo"]["full_name"] == REPO and
                head["repo"]["full_name"] == REPO,
                "Wrong target, source repo or unreviewed base")
        require(git("rev-list", "--parents", "-n", "1", "HEAD").split() ==
                [sha, base["sha"], head["sha"]],
                "Synthetic merge parents not exact")
        changed = git("diff", "--name-only", base["sha"], head["sha"]).splitlines()
    else:
        require(event == "push" and os.getenv("GITHUB_REF") == "refs/heads/dev",
                "POSTMERGE certification only on dev")
        changed = git("show", "--format=", "--name-only", sha).splitlines()
    require(set(changed) == ALLOWED and len(changed) == len(ALLOWED),
            "Unreviewed or missing files: " + repr(changed))
    invariants(read())
    print(f"PASS CRECISOL C05-04 closure C: real A/B/NET10/context/regressions sha={sha} run={run}")
    print("CERTIFICATION SCOPE ONLY pre-session contracts; NO user login, sessions or tenant grants")


if __name__ == "__main__":
    try:
        require(len(sys.argv) == 2 and sys.argv[1] in ("A", "B", "C"),
                "Usage: qa/c05_04_closure_gate.py A|B|C")
        {"A": audit_a, "B": audit_b, "C": audit_c}[sys.argv[1]]()
    except (AssertionError, OSError, ValueError, KeyError,
            subprocess.SubprocessError) as exc:
        print("FAIL CRECISOL C05-04 closure: " + str(exc), file=sys.stderr)
        sys.exit(1)
