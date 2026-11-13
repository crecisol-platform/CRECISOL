#!/usr/bin/env python3
"""CRECISOL C05-02 scope-bounded closure A/B/C; NEVER production certification."""
from __future__ import annotations

import json
import os
from pathlib import Path
import re
import subprocess
import sys

from c05_01_preparation_audits import ROOT

REPO = "crecisol-platform/CRECISOL"
FEATURE = "feature/c05-02-certificacion-acotada-20261113"
FILES = {
    "act": "docs/c05/C05_02_ACTA_CIERRE_ALCANCE_CONTRATOS.md",
    "state": "docs/ESTADO_VIGENTE_CAPITULOS.md",
    "qa": "qa/c05_02_scope_closure_audits.py",
    "workflow": ".github/workflows/c05-02-scope-closure.yml",
}
ALLOWED = set(FILES.values())
EVIDENCE = ROOT / ".c05-02-scope-closure-proof"
GUARDS = {
    "act": (
        "Acta de ratificación por alcance verificable (condicionada)",
        "NO EFICAZ",
        "Documento Maestro Único",
        "C05-02 «Registro e invitaciones seguras»",
        "Flujo tipado, expiración y pruebas de abuso",
        "No enumeración de correos",
        "C05-03",
        "C05-04",
        "C05-05",
        "C05-07",
        "C05-08",
        "C05-10",
        "NO elimina",
        "NO autoriza activar API",
        "PREMERGE",
        "POSTMERGE",
        "B01/T01",
        "B02–B05/T02–T05",
        "B06–B08/T06–T08",
        "B09–B11/T09–T11",
        "B12/T12–T14",
        "B13–B16/T15–T19",
        "B17–B18/T20",
        "2/11 microfases de C05",
        "C05-03 habilitada únicamente",
        "C05 como capítulo NO CERTIFICADO y C06 BLOQUEADO",
        "NO PROBADOS / NO AUTORIZADOS",
        "Rollback",
    ),
    "state": (
        "RATIFICACIÓN C05-02 CONDICIONADA",
        "C05_02_ACTA_CIERRE_ALCANCE_CONTRATOS.md",
        "C05-02 NO CERTIFICADA hasta",
        "C05-03 BLOQUEADA hasta",
        "C05-02 CERTIFICADA EXCLUSIVAMENTE",
        "C05-03 solo PARA PREPARACIÓN",
        "C05 NO CERTIFICADO",
        "C06 BLOQUEADO",
    ),
    "workflow": (
        "permissions:",
        "contents: read",
        "persist-credentials: false",
        "fetch-depth: 2",
        "dotnet-version: '10.0.x'",
        "qa/c05_02_scope_closure_audits.py A",
        "qa/c05_02_scope_closure_audits.py B",
        "qa/c05_02_scope_closure_audits.py C",
        "database/tests/c05_02_neutral_registration_http_mysql.sh",
        "database/tests/c05_02_mailbox_bound_mysql_smoke.sh",
        "database/tests/c05_02_privacy_retention_mysql.sh",
        "Crecisol.NeutralRegistration.IntegrationTests.csproj",
        "Crecisol.MailboxBound.MySqlTests.csproj",
        "Crecisol.InvitationSecurity.ContractsTests.csproj",
        "dotnet run --no-build -c Release",
        "GITHUB_SHA",
        "GITHUB_RUN_ID",
        "C05-02 CONTRACT/TEST ONLY",
    ),
}
RUNS = (
    "43372184968",  # preparation
    "43377586097",  # invitation core
    "43381681826",  # MySQL ledger
    "43386127775",  # HTTP neutral
    "43426225047",  # mailbox challenge
    "43446245494",  # outbox
    "43451338317",  # loopback SMTP
    "43456758070",  # issuer scope
    "43461600130",  # issuer recheck on claim
    "43470734839",  # signed recipient
    "43477170014",  # bound claim
    "43481465989",  # HMAC in backend
    "43485806970",  # retention
)


def need(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)


def read() -> dict[str, str]:
    return {k: (ROOT / v).read_text(encoding="utf-8")
            for k, v in FILES.items() if k != "qa"}


def invariants(data: dict[str, str]) -> None:
    for key, markers in GUARDS.items():
        for marker in markers:
            need(marker in data[key], "Missing C05-02 gate: " + key + " / " + marker)
    for number in RUNS:
        need(number in data["act"], "Missing prior exact CI run: " + number)
    plan = (ROOT / "docs/planificacion-futura/C05_MICROFASES_PROPUESTAS.md").read_text(
        encoding="utf-8")
    for row in ("C05-02 | Registro e invitaciones seguras",
                "C05-03 | Hash y política de credenciales",
                "C05-04 | Login contextual",
                "C05-05 | Sesiones y revocación",
                "C05-07 | Verificación de correo y cambios sensibles",
                "C05-10 | Rate limit y trazas de acceso"):
        need(row in plan, "Current C05 phase mapping differs: " + row)
    spec = (ROOT / "docs/c05/C05_02_ESPECIFICACION.md").read_text(
        encoding="utf-8")
    need("B01 Registro no enumera cuentas" in spec and
         "B03 Invitación de un solo uso" in spec and
         "B13 No activar sesión ni contraseña" in spec,
         "Cannot certify if mandatory C05-02 trust boundaries moved")
    api = (ROOT / "backend/src/Crecisol.Api/Program.cs").read_text(
        encoding="utf-8")
    routes = re.findall(r'app\.Map(?:Get|Post|Put|Patch|Delete)\(\s*"([^"]+)"',
                        api)
    need(routes == ["/api/v1/public/branding/platform"],
         "No real signup, login or privileged API is authorized")
    adapter = (ROOT /
        "backend/src/Crecisol.Infrastructure/Identity/MySqlDisposableMailboxBoundRecipientClaimLedger.cs"
        ).read_text(encoding="utf-8")
    need("CryptographicOperations.FixedTimeEquals" in adapter and
         "TryClaimWithVerifiedMailboxAndSignatureOnceAsync" in adapter and
         'new MySqlCommand("c05_02_claim_mailbox_bound_once", db)' in adapter,
         "Backend HMAC and atomic mailbox conjunction required")
    need("GRANT ALL" not in data["workflow"] and
         "id-token: write" not in data["workflow"] and
         "workflow_dispatch:" not in data["workflow"] and
         "deploy" not in data["workflow"].lower(),
         "No privileged CI or deployment actions")
    need("CERTIFICADA FUNCIONALMENTE EN PRODUCCIÓN" not in data["act"],
         "A scoped certification must not claim real production identity")


def audit_a() -> None:
    subprocess.run([sys.executable, "qa/master_source_guard.py", "--self-test"],
                   cwd=ROOT, check=True)
    subprocess.run([sys.executable, "qa/c05_01_preparation_audits.py", "source"],
                   cwd=ROOT, check=True)
    invariants(read())
    print("PASS C05-02 scoped A: canonical Word/PDF, C05 sequence, 13 historical CI, limited gate")


def audit_b() -> None:
    original = read()
    invariants(original)
    rejected = 0
    for key, markers in GUARDS.items():
        for marker in markers:
            candidate = dict(original)
            candidate[key] = original[key].replace(marker, "[REMOVED_C05_SCOPE_GUARD]")
            need(candidate[key] != original[key], "Ineffective audit mutation")
            try:
                invariants(candidate)
            except AssertionError:
                rejected += 1
            else:
                raise AssertionError("Unrejected mutation: " + key + " " + marker)
    need(rejected >= 42, "Insufficient safeguard mutation coverage")
    print(f"PASS C05-02 scoped B: {rejected} removals rejected")
    print("STATIC MUTATIONS CANNOT CERTIFY REAL AUTHENTICATION OR EXTERNAL EMAIL")


def git(*args: str) -> str:
    return subprocess.check_output(
        ["git", *args], cwd=ROOT, text=True, stderr=subprocess.DEVNULL).strip()


def audit_c() -> None:
    need(os.getenv("GITHUB_ACTIONS") == "true" and
         os.getenv("GITHUB_REPOSITORY") == REPO, "Only repository CI can attest")
    sha = os.getenv("GITHUB_SHA", "")
    run = os.getenv("GITHUB_RUN_ID", "")
    need(re.fullmatch("[0-9a-f]{40}", sha) is not None and
         re.fullmatch("[0-9]+", run) is not None and
         git("rev-parse", "HEAD") == sha, "Exact SHA and run ID required")
    for stage in ("A", "B", "dotnet", "neutral", "mailbox", "retention"):
        file = EVIDENCE / stage
        need(file.is_file() and file.read_text(encoding="utf-8").strip() ==
             f"PASS {stage} sha={sha} run={run}",
             "Missing same-run real evidence: " + stage)
    event = os.getenv("GITHUB_EVENT_NAME")
    if event == "pull_request":
        ep = Path(os.getenv("GITHUB_EVENT_PATH", ""))
        need(ep.is_file(), "PR payload missing")
        pr = json.loads(ep.read_text(encoding="utf-8"))["pull_request"]
        base, head = pr["base"], pr["head"]
        need(base["ref"] == "dev" and head["ref"] == FEATURE and
             base["repo"]["full_name"] == REPO and
             head["repo"]["full_name"] == REPO,
             "Incorrect repo/head/base for certification")
        need(git("rev-list", "--parents", "-n", "1", "HEAD").split() ==
             [sha, base["sha"], head["sha"]],
             "PR merge SHA must have exact parent pair")
        changed = git("diff", "--name-only",
                      base["sha"], head["sha"]).splitlines()
    else:
        need(event == "push" and os.getenv("GITHUB_REF") == "refs/heads/dev",
             "Postmerge certification only on dev")
        changed = git("show", "--format=", "--name-only", sha).splitlines()
    need(set(changed) == ALLOWED and len(changed) == len(ALLOWED),
         "Unauthorized or missing changed file(s): " + repr(changed))
    invariants(read())
    print(f"PASS C05-02 scoped C: same A/B/.NET10/HTTP+MySQL/mailbox+MySQL/"
          f"retention sha={sha} run={run}")
    print("DO NOT claim human identity, external mailbox, actual membership or production")


if __name__ == "__main__":
    try:
        need(len(sys.argv) == 2 and sys.argv[1] in ("A", "B", "C"),
             "Usage: C05-02 scoped audit A|B|C")
        {"A": audit_a, "B": audit_b, "C": audit_c}[sys.argv[1]]()
    except (AssertionError, OSError, ValueError, KeyError,
            subprocess.SubprocessError) as exc:
        print("FAIL C05-02 scoped closure: " + str(exc), file=sys.stderr)
        sys.exit(1)
