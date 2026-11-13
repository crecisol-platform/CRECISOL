#!/usr/bin/env python3
"""C05-02 A/B/C: DOCUMENTARY preparation audits, not registration runtime tests."""
from __future__ import annotations
import json
import os
from pathlib import Path
import re
import subprocess
import sys
from c05_01_preparation_audits import ROOT, canonical_c05_source, fold
from master_source_guard import canonical_active_word, CURRENT_WORD_SHA, CURRENT_PDF_SHA

SPEC = ROOT / "docs/c05/C05_02_ESPECIFICACION.md"
ACTA = ROOT / "docs/c05/C05_02_ACTA_PREPARACION.md"
PLAN = ROOT / "docs/planificacion-futura/C05_MICROFASES_PROPUESTAS.md"
PRIOR = ROOT / "docs/c05/ACTA_C05_01_CONTRATOS.md"
WORKFLOW = ROOT / ".github/workflows/c05-02-preparation.yml"
API = ROOT / "backend/src/Crecisol.Api/Program.cs"
PROOF = ROOT / ".c05-02-prep-proof"
SOURCE_SHA = "bf7036849d6f1aad36a3a111b62121947aa12fae8c2a0551e63b0cc788d4a0aa"
PREDECESSOR = "39b0a322e6b8f0f8eb55db8668a959c16521f132"
COMMENT = "6844940005"
FEATURE = "feature/c05-02-preparacion-registro-invitaciones-20261112"
REPO = "crecisol-platform/CRECISOL"
ALLOWED = {
    "docs/c05/C05_02_ESPECIFICACION.md",
    "docs/c05/C05_02_ACTA_PREPARACION.md",
    "qa/c05_02_preparation_audits.py",
    ".github/workflows/c05-02-preparation.yml",
}
GUARDS = (
    "B01 Registro no enumera cuentas",
    "B02 Invitación no equivale a autorización",
    "B03 Invitación de un solo uso",
    "B04 Invitación expira y es revocable",
    "B05 Token CSPRNG y hash protegido",
    "B06 Tenant derivado del servidor",
    "B07 Actor invitante autorizado",
    "B08 No escalada a Owner/SuperAdmin",
    "B09 Destinatario exacto y canal verificado",
    "B10 Una identidad no hereda vínculos",
    "B11 Duplicados y carreras fail-closed",
    "B12 Condiciones legales versionadas",
    "B13 No activar sesión ni contraseña",
    "B14 Sin preguntas de seguridad",
    "B15 No PII/secretos en trazas",
    "B16 Suspensión/revocación de tenant prevalece",
    "B17 C05-03..11 y C06 bloqueadas",
    "B18 NO IMPLEMENTADA NI CERTIFICADA",
)

def need(ok: bool, msg: str) -> None:
    if not ok:
        raise AssertionError(msg)

def read(path: Path) -> str:
    need(path.is_file(), "Required file missing: " + str(path.relative_to(ROOT)))
    return path.read_text(encoding="utf-8")

def contract(spec: str) -> None:
    clean = spec.replace("**", "").replace(chr(96), "")
    for marker in GUARDS:
        need(marker in clean, "Missing security boundary: " + marker)
    ids = re.findall(r"^- \*\*B(\d{2}) ", spec, flags=re.MULTILINE)
    need(ids == [f"{n:02d}" for n in range(1, 19)], "B01..B18 incomplete or out of order")
    threat_ids = re.findall(r"^\| T(\d{2}) \|", spec, flags=re.MULTILINE)
    need(threat_ids == [f"{n:02d}" for n in range(1, 21)], "T01..T20 incomplete or out of order")
    for marker in (
        "NO IMPLEMENTADA / NO CERTIFICADA", "C05-01", "C05-02",
        "C05-03..11", "C06", "No enumeración de correos",
        "una sola consumición atómica", "tenant", "MFA",
        SOURCE_SHA, CURRENT_WORD_SHA, CURRENT_PDF_SHA, PREDECESSOR, COMMENT
    ):
        need(marker.casefold() in clean.casefold(), "Missing authority/limit: " + marker)

def audit_a() -> None:
    need(canonical_active_word(), "Canonical Word/PDF SHA has changed")
    digest, lines, start, stop = canonical_c05_source()
    need(digest == SOURCE_SHA and (len(lines), start, stop) == (93, 1807, 1900),
         "C05 source contents or canonical paragraph span drift")
    source = fold("\n".join(lines))
    for word in ("identidad", "invitaciones", "tenant", "autenticacion", "mfa"):
        need(word in source, "C05 Word lacks normative term: " + word)
    plan = read(PLAN)
    phase_ids = re.findall(r"^\| (C05-\d{2}) \|", plan, flags=re.MULTILINE)
    need(phase_ids == [f"C05-{n:02d}" for n in range(1, 12)],
         "The 11 original functional microphases were changed")
    need("| C05-02 | Registro e invitaciones seguras |" in plan and
         "No enumeración de correos" in plan, "Wrong C05-02 source contract")
    earlier = read(PRIOR)
    need("C05-01" in earlier and "CONTRATOS INTERNOS" in earlier.upper(),
         "C05-01 limited institutional acta missing")
    contract(read(SPEC))
    acta = read(ACTA)
    need("NO IMPLEMENTADA / NO CERTIFICADA" in acta and
         "NO EJECUTADAS" in acta and COMMENT in acta,
         "C05-02 acta misstates predecessor or runtime execution")
    print("PASS C05-02 A: canonical Word/PDF, C05-01 certified limited gate, 11 phases, 18/20 threat controls")

def audit_b() -> None:
    spec = read(SPEC)
    contract(spec)
    rejected = 0
    for item in GUARDS:
        mutant = spec.replace(item, "[REMOVED SECURITY BOUNDARY]")
        need(mutant != spec, "Cannot mutate invariant: " + item)
        try:
            contract(mutant)
        except AssertionError:
            rejected += 1
        else:
            raise AssertionError("Fail-open mutation not rejected: " + item)
    need(rejected == 18, "18 adversarial mutations required")
    api = read(API)
    paths = re.findall(r'app\.Map(?:Get|Post|Put|Patch|Delete)\(\s*"([^"]+)"', api)
    need(paths == ["/api/v1/public/branding/platform"],
         "Preparation unexpectedly enabled private registration/auth routes")
    wf = read(WORKFLOW)
    for word in ("contents: read", "persist-credentials: false", "fetch-depth: 2",
                 "qa/c05_02_preparation_audits.py A",
                 "qa/c05_02_preparation_audits.py B",
                 "qa/c05_02_preparation_audits.py C"):
        need(word in wf, "Read-only CI boundary missing: " + word)
    need("workflow_dispatch:" not in wf and "id-token: write" not in wf
         and "deploy" not in wf.casefold(), "Privileged or deployment workflow forbidden")
    print("PASS C05-02 B: 18 fail-closed documentary mutations; API remains public only")
    print("LIMIT: real email/token/HTTP registration/MySQL identity/MFA tests NOT EXECUTED")

def git(*args: str) -> str:
    return subprocess.check_output(["git", *args], cwd=ROOT,
                                   text=True, stderr=subprocess.DEVNULL).strip()

def audit_c() -> None:
    need(os.environ.get("GITHUB_ACTIONS") == "true", "Real CI runner required")
    sha, run = os.environ.get("GITHUB_SHA", ""), os.environ.get("GITHUB_RUN_ID", "")
    need(re.fullmatch(r"[a-f0-9]{40}", sha) is not None and
         re.fullmatch(r"\d+", run) is not None, "Runner SHA/run ID invalid")
    need(os.environ.get("GITHUB_REPOSITORY") == REPO and
         git("rev-parse", "HEAD") == sha, "Wrong source/repository SHA")
    for stage in ("A", "B"):
        marker = PROOF / stage
        need(marker.is_file() and marker.read_text(encoding="utf-8").strip() ==
             f"PASS {stage} sha={sha} run={run}", "Missing same-SHA/run proof " + stage)
    event = os.environ.get("GITHUB_EVENT_NAME")
    if event == "pull_request":
        source = Path(os.environ.get("GITHUB_EVENT_PATH", ""))
        need(source.is_file(), "Missing PR JSON")
        pr = json.loads(source.read_text(encoding="utf-8"))["pull_request"]
        base, head = pr["base"], pr["head"]
        need(base["ref"] == "dev" and head["ref"] == FEATURE
             and base["repo"]["full_name"] == REPO
             and head["repo"]["full_name"] == REPO,
             "Unexpected PR base/head/fork")
        parents = git("rev-list", "--parents", "-n", "1", "HEAD").split()
        need(parents == [sha, base["sha"], head["sha"]],
             "Synthetic merge is not exact base + PR HEAD")
        changed = git("diff", "--name-only", base["sha"], head["sha"]).splitlines()
        print(f"C05-02 provenance: PR HEAD={head['sha']}, BASE={base['sha']}, RUN={sha}")
    else:
        need(event == "push" and os.environ.get("GITHUB_REF") == "refs/heads/dev",
             "Only pull requests into dev or dev push allowed")
        changed = git("show", "--format=", "--name-only", sha).splitlines()
        print(f"C05-02 provenance: dev SHA={sha}")
    need(bool(changed) and set(changed).issubset(ALLOWED),
         "C05-02 altered out-of-scope files: " + repr(changed))
    contract(read(SPEC))
    need("NO IMPLEMENTADA / NO CERTIFICADA" in read(ACTA),
         "Documentary preparation cannot certify runtime")
    print(f"PASS C05-02 C: exact SHA/run={sha}/{run}, A+B evidence and docs-only diff")
    print("NOT CERTIFIED: C05-02 implementation and C05-03..11 remain blocked")

if __name__ == "__main__":
    try:
        need(len(sys.argv) == 2 and sys.argv[1] in ("A", "B", "C"),
             "Usage: c05_02_preparation_audits.py A|B|C")
        {"A": audit_a, "B": audit_b, "C": audit_c}[sys.argv[1]]()
    except (AssertionError, OSError, ValueError, KeyError, subprocess.SubprocessError) as exc:
        print("FAIL C05-02 preparation:", exc, file=sys.stderr)
        sys.exit(1)
