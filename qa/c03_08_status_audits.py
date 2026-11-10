#!/usr/bin/env python3
"""C03-08 certified status: independent Word evidence and falsification gates.

Historical C03-08 acta remains a time-stamped technical closure, not rewritten.
GitHub Actions conclusions require external exact-SHA API verification; source
strings never substitute for that check. No private tenancy is certified.
"""
from copy import deepcopy
from hashlib import sha256
from pathlib import Path
import sys

import c03_05_status as status
import c03_08_closure_gate as acta_gate
import c03_08_local_config_audits as technical

from master_source_guard import canonical_active_word

ROOT = Path(__file__).resolve().parents[1]
WORD = ROOT / "docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx"
WORD_HASH = "e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e"
OWNER = "cierra la fase número 8 con su acta y todo"
TECH_SHA = "221afb6cc515fc79a3b6ca6e010af11f2e20b0a2"
ACTA_SHA = "b333e58378b6a5ea0438f1ee6b11de85d9ce04df"
RUNS_PRE = ("42825656795", "42825656755")
RUNS_POST = ("42826621425", "42826620877", "42826620733",
             "42826620758", "42826620681")
FILES = {
    "certificate": ROOT / "docs/c03/C03_08_CERTIFICACION_POSTMERGE.md",
    "acta": ROOT / "docs/c03/C03_08_ACTA_MICROFASE.md",
    "prior": ROOT / "docs/c03/C03_07_CERTIFICACION_POSTMERGE.md",
    "plan": ROOT / "docs/PLAN_37_CAPITULOS.md",
    "prep": ROOT / "docs/C03_PREPARACION.md",
    "master": ROOT / "docs/MASTER_REFERENCE.md",
    "agents": ROOT / "AGENTS.md",
    "future": ROOT / "docs/planificacion-futura/C03_MICROFASES_PROPUESTAS.md",
    "readme": ROOT / "README.md",
}

def require(flag, why):
    if not flag:
        raise AssertionError(why)

def validate(docs):
    plan, cert = docs["plan"], docs["certificate"]
    row = next((line for line in plan.splitlines() if line.startswith("C03 | ")), "")
    require(status.eighth_certified(row) and status.fifth_plan_safe(plan),
            "Eighth-stage certification requires validated active row and heading")
    require("C04 | Multitenancy, dominios y aprovisionamiento | PENDIENTE" in plan,
            "C04 cannot be unlocked by a microphase")
    for name in ("plan", "prep", "master", "agents", "future", "readme"):
        doc = docs[name]
        require("C03-07 CERTIFICADA (7/10)" in doc and
                "C03-08 CERTIFICADA (8/10)" in doc and
                "C03-09 NO INICIADA" in doc and
                "C03 NO CERTIFICADO" in doc and "C04 BLOQUEADO" in doc,
                "Missing sequential certification or C03/C04 lock: " + name)
    for token in (WORD_HASH, OWNER, TECH_SHA, ACTA_SHA, "PR #70", "PR #71",
                  "C03-08 CERTIFICADA (8/10)", "C03-09 NO INICIADA",
                  "6/6 CI POSTMERGE SUCCESS", "5/5 CI POSTMERGE SUCCESS",
                  "72 aserciones", "18/18", "15/15",
                  "Auditoría A", "Auditoría B", "Auditoría C",
                  "MySQL8.4", "BOLA/IDOR/MFA", "C03 NO CERTIFICADO",
                  "C04 BLOQUEADO", "Rollback"):
        require(token.casefold() in cert.casefold(), "Missing evidence/limit: " + token)
    for run in RUNS_PRE + RUNS_POST:
        require(run in cert, "Exact acta run missing: " + run)
    require("C03-08 CERRADA TÉCNICAMENTE" in docs["acta"] and
            "NO CERTIFICADA" in docs["acta"],
            "Historical acta was retrospectively rewritten")
    require("C03-07 CERTIFICADA (7/10)" in docs["prior"] and
            "C03 NO CERTIFICADO" in docs["prior"],
            "Previous seventh-stage certificate changed")
    require("C03 CERTIFICADO" not in row and "C04 HABILITADO" not in cert,
            "Premature chapter promotion")

def negative_mutations(docs):
    tests = (
        ("owner forged", "certificate", OWNER, "AUTORIZACION AUSENTE"),
        ("Word SHA forged", "certificate", WORD_HASH, "0" * 64),
        ("technical SHA forged", "certificate", TECH_SHA, "0" * 40),
        ("acta SHA forged", "certificate", ACTA_SHA, "0" * 40),
        ("acta PREMERGE forged", "certificate", RUNS_PRE[0], "00000000000"),
        ("acta POSTMERGE forged", "certificate", RUNS_POST[0], "00000000000"),
        ("A audit erased", "certificate", "Auditoría A", "AUDITORIA A AUSENTE"),
        ("B mutations erased", "certificate", "18/18", "0/18"),
        ("C runtime erased", "certificate", "72 aserciones", "0 aserciones"),
        ("document mutations erased", "certificate", "15/15", "0/15"),
        ("certification status forged", "plan", status.EIGHTH_STATUS,
         "**C03-01 A C03-09 CERTIFICADAS (9/10)**"),
        ("ninth illegally initiated", "plan", "C03-09 NO INICIADA", "C03-09 INICIADA"),
        ("C03 chapter promotion", "plan", "C03 NO CERTIFICADO", "C03 CERTIFICADO"),
        ("C04 unlocked", "plan", "C04 | Multitenancy, dominios y aprovisionamiento | PENDIENTE",
         "C04 | Multitenancy, dominios y aprovisionamiento | INICIADO"),
    )
    for label, name, original, fake in tests:
        require(original in docs[name], "Invalid negative fixture: " + label)
        broken = deepcopy(docs)
        broken[name] = broken[name].replace(original, fake)
        try:
            validate(broken)
        except (AssertionError, KeyError, ValueError, TypeError):
            continue
        raise AssertionError("Unsafe C03-08 certificate forgery accepted: " + label)
    print("PASS C03-08 CERT B: 14/14 documentary forgery and premature-promotion mutations rejected.")

def run():
    require(canonical_active_word(WORD_HASH),
            "Canonical Word SHA was modified")
    technical.audit_a()
    technical.audit_b()
    technical.audit_c()
    acta_gate.run()
    docs = {name: path.read_text(encoding="utf-8") for name, path in FILES.items()}
    validate(docs)
    negative_mutations(docs)
    print("PASS C03-08 CERT A/B/C: Word and exact #70/#71 evidence, 72 typed contracts, "
          "18 configuration and 15 acta mutations, 14 status mutations, C03/C04 locked.")
    print("LIMIT: PR certification PRE/POSTMERGE exact SHA is verified independently via GitHub API.")

if __name__ == "__main__":
    try:
        run()
    except (AssertionError, OSError, KeyError, ValueError, TypeError) as exc:
        print("FAIL C03-08 certification:", exc, file=sys.stderr)
        sys.exit(1)
