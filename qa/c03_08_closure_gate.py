#!/usr/bin/env python3
"""C03-08 technical acta: exact GitHub evidence, 15 documentary negatives.

Remote Actions PRE/POST conclusions must still be checked independently for
the exact SHA. This program never invents runner-proof markers.
"""
from copy import deepcopy
from hashlib import sha256
from pathlib import Path
import sys

import c03_08_local_config_audits as technical
import c03_07_closure_gate as previous
import c03_05_status as phase

from master_source_guard import canonical_active_word

ROOT = Path(__file__).resolve().parents[1]
WORD = ROOT / "docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx"
HASH = "e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e"
OWNER = "Continúa por favor"
TECH_HEAD = "97ac8431ee4b1d3ab263dc54205b2bd22f07600a"
TECH_MERGE = "221afb6cc515fc79a3b6ca6e010af11f2e20b0a2"
PRE = ("42823974509", "42823974454")
POST = ("42824458247", "42824458457", "42824458283", "42824458016",
        "42824458308", "42824458319")
FILES = {
    "acta": ROOT / "docs/c03/C03_08_ACTA_MICROFASE.md",
    "spec": ROOT / "docs/c03/C03_08_ESPECIFICACION.md",
    "plan": ROOT / "docs/PLAN_37_CAPITULOS.md",
    "prep": ROOT / "docs/C03_PREPARACION.md",
    "master": ROOT / "docs/MASTER_REFERENCE.md",
    "prior": ROOT / "docs/c03/C03_07_CERTIFICACION_POSTMERGE.md",
}
def require(ok, why):
    if not ok:
        raise AssertionError(why)

def validate(docs):
    acta, plan = docs["acta"], docs["plan"]
    row = next((line for line in plan.splitlines() if line.startswith("C03 | ")), "")
    c04 = next((line for line in plan.splitlines() if line.startswith("C04 | ")), "")
    require(phase.seventh_certified(row) and phase.fifth_plan_safe(plan),
            "Predecessor C03-07 must remain the last formally certified microphase")
    certified = phase.eighth_certified(row)
    require((("C03-08 CERTIFICADA (8/10)" in row and "C03-09 NO INICIADA" in row)
             if certified else "C03-08 NO INICIADA" in row) and
            "C03 NO CERTIFICADO" in row and "C04 BLOQUEADO" in row,
            "Eighth-stage acta must preserve the current certificate and chapter locks")
    if certified:
        proof_file = ROOT / "docs/c03/C03_08_CERTIFICACION_POSTMERGE.md"
        require(proof_file.is_file(), "No independent eighth-stage certificate exists")
        proof = proof_file.read_text(encoding="utf-8")
        for term in ("C03-08 CERTIFICADA (8/10)", "C03-09 NO INICIADA",
                     TECH_MERGE, "b333e58378b6a5ea0438f1ee6b11de85d9ce04df",
                     "C03 NO CERTIFICADO", "C04 BLOQUEADO"):
            require(term in proof, "Independent eighth-stage certificate lacks: " + term)
    require(c04 == "C04 | Multitenancy, dominios y aprovisionamiento | PENDIENTE",
            "C04 cannot be unlocked by the eighth microphase")
    require("2/37 capítulos certificados (C01 y C02)" in plan and
            "C03-07 CERTIFICADA (7/10)" in docs["prep"] and
            "C03-07 CERTIFICADA (7/10)" in docs["master"],
            "Certified seven-stage provenance lost")
    require(HASH in docs["spec"] and "NO CERTIFICADA" in docs["spec"] and
            "C03 NO CERTIFICADO" in docs["spec"],
            "Historical C03-08 source and boundary altered")
    require("C03-07 CERTIFICADA (7/10)" in docs["prior"] and
            "C03 NO CERTIFICADO" in docs["prior"],
            "Historical prior certificate missing")
    for marker in (
        OWNER, TECH_HEAD, TECH_MERGE, HASH,
        "C03-08 CERRADA TÉCNICAMENTE", "NO CERTIFICADA",
        "6/6 CI POSTMERGE", "72 aserciones", "18/18",
        "Auditoría A", "Auditoría B", "Auditoría C", "MySQL8.4",
        "PR técnico", "C03-09 NO INICIADA", "C03 NO CERTIFICADO",
        "C04 BLOQUEADO", "2/37", "skipped", "Rollback",
        "BOLA/IDOR/MFA", "sin nuevos endpoints"
    ):
        require(marker.casefold() in acta.casefold(),
                "C03-08 acta evidence/limitations missing: " + marker)
    for run in PRE + POST:
        require(run in acta, "Exact technical workflow run absent: " + run)
    require("C03 CERTIFICADO" not in acta and "C04 HABILITADO" not in acta,
            "Eighth acta falsely certifies chapter or tenant routing")

def negative_mutations(docs):
    row = next((line for line in docs["plan"].splitlines() if line.startswith("C03 | ")), "")
    next_phase = "C03-09" if phase.eighth_certified(row) else "C03-08"
    tests = (
        ("owner replaced", "acta", OWNER, "AUTORIZACION AUSENTE"),
        ("source SHA forged", "acta", HASH, "0"*64),
        ("technical head forged", "acta", TECH_HEAD, "0"*40),
        ("technical merge forged", "acta", TECH_MERGE, "0"*40),
        ("PREMERGE run forged", "acta", PRE[0], "00000000000"),
        ("POSTMERGE run forged", "acta", POST[0], "00000000000"),
        ("technical success forged", "acta", "6/6 CI POSTMERGE", "0/6 CI POSTMERGE"),
        ("contract proof removed", "acta", "72 aserciones", "0 aserciones"),
        ("mutation proof removed", "acta", "18/18", "0/18"),
        ("source scope removed", "spec", "NO CERTIFICADA", "CERTIFICADA"),
        ("chapter promotion", "plan", "C03 NO CERTIFICADO", "C03 CERTIFICADO"),
        ("C04 unblocked", "plan", "C04 | Multitenancy, dominios y aprovisionamiento | PENDIENTE",
         "C04 | Multitenancy, dominios y aprovisionamiento | INICIADO"),
        ("premature next stage", "plan", next_phase + " NO INICIADA", next_phase + " INICIADA"),
        ("next stage started", "acta", "C03-09 NO INICIADA", "C03-09 INICIADA"),
        ("previous certificate forged", "prior", "C03-07 CERTIFICADA (7/10)",
         "C03-07 NO CERTIFICADA (7/10)"),
    )
    for label, kind, before, after in tests:
        require(before in docs[kind], "Bad negative fixture: " + label)
        changed = deepcopy(docs)
        changed[kind] = changed[kind].replace(before, after)
        try:
            validate(changed)
        except (AssertionError, KeyError, TypeError, ValueError):
            continue
        raise AssertionError("Unsafe documentary mutation accepted: " + label)
    print("PASS C03-08 acta B: 15/15 document forgery and premature advancement mutations rejected.")

def run():
    require(canonical_active_word(HASH),
            "Canonical Word authority modified")
    technical.audit_a()
    technical.audit_b()
    technical.audit_c()  # demands same-job Release/typed/MySQL/old regressions
    previous.run()  # demands prior same-job C03-07 runtime proof, 15 negatives
    docs = {name: path.read_text(encoding="utf-8") for name, path in FILES.items()}
    validate(docs)
    negative_mutations(docs)
    print("PASS C03-08 acta A/B/C: 6/6 technical POSTMERGE references, 72 typed tests, "
          "18 security + 15 documentary negatives; C03/C04 locked.")
    print("LIMIT: only GitHub API can certify independent acta PREMERGE/POSTMERGE SHA.")

if __name__ == "__main__":
    try:
        run()
    except (AssertionError, OSError, KeyError, ValueError, TypeError) as error:
        print("FAIL C03-08 acta:", error, file=sys.stderr)
        sys.exit(1)
