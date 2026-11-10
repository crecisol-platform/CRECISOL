#!/usr/bin/env python3
"""CRECISOL C03-06 final status audit: exact six-phase proof, 16 tamper negatives.

This test audits historical chain and DOCUMENTARY evidence only. It does not
invent CI statuses: exact PREMERGE/POSTMERGE conclusions are verified separately
via GitHub API by the integrator; real .NET/MySQL/Chromium C jobs run in Actions.
"""
from copy import deepcopy
from hashlib import sha256
from pathlib import Path
import sys

import c03_05_status as phase

ROOT = Path(__file__).resolve().parents[1]
WORD = ROOT / "docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx"
WORD_HASH = "e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e"
OWNER = "Sé full estricto, no omitas nada"
RUNS = (
    # Technical PR60 PREMERGE, POSTMERGE; acta PR61 PREMERGE, POSTMERGE.
    "42752596034", "42752596091", "42752596048", "42752596100", "42752596222",
    "42753303252", "42753303282", "42753303327", "42753303287", "42753303579",
    "42753672733", "42753672777", "42753672741", "42753672762", "42753672757",
    "42754431737", "42754431655", "42754431786", "42754431545", "42754431582",
)
SOURCES = {
    "plan": ROOT / "docs/PLAN_37_CAPITULOS.md",
    "master": ROOT / "docs/MASTER_REFERENCE.md",
    "prep": ROOT / "docs/C03_PREPARACION.md",
    "future": ROOT / "docs/planificacion-futura/C03_MICROFASES_PROPUESTAS.md",
    "global": ROOT / "docs/PLAN_MICROFASES_37_CAPITULOS_PROPUESTO.md",
    "decisions": ROOT / "docs/DECISIONES_ABIERTAS.md",
    "agents": ROOT / "AGENTS.md",
    "readme": ROOT / "README.md",
    "c01": ROOT / "docs/C01_PREPARACION.md",
    "c02": ROOT / "docs/C02_PREPARACION.md",
    "protocol": ROOT / "docs/PROTOCOLO_CONTINUIDAD_CRECISOL.md",
    "spec": ROOT / "docs/c03/C03_06_ESPECIFICACION.md",
    "acta": ROOT / "docs/c03/C03_06_ACTA_MICROFASE.md",
    "evidence": ROOT / "docs/c03/C03_06_CERTIFICACION_POSTMERGE.md",
    "prior": ROOT / "docs/c03/C03_05_CERTIFICACION_POSTMERGE.md",
}


def require(condition, reason):
    if not condition:
        raise AssertionError(reason)


def validate(docs):
    plan, evidence = docs["plan"], docs["evidence"]
    row = next((line for line in plan.splitlines() if line.startswith("C03 | ")), "")
    require(phase.sixth_certified(row) and phase.fifth_plan_safe(plan),
            "Only verified stage-six or its certified successor may be active")
    eighth = phase.eighth_certified(row)
    seventh = phase.seventh_certified(row)
    require("C04 | Multitenancy, dominios y aprovisionamiento | PENDIENTE" in plan,
            "C04 was started before chapter C03 certification")
    for name in ("master", "prep", "future", "global", "decisions",
                 "agents", "readme", "c01", "c02", "protocol", "spec"):
        doc = docs[name]
        next_marker = ("C03-07 NO INICIADA" if name == "spec" or not seventh
                       else "C03-09 NO INICIADA" if eighth
                       else "C03-08 NO INICIADA")
        for term in ("C03-05 CERTIFICADA (5/10)", "C03-06 CERTIFICADA (6/10)",
                     next_marker, "C03 NO CERTIFICADO", "C04 BLOQUEADO"):
            require(term in doc, "Stage-six historical provenance drift: " + name + ": " + term)
        if seventh and name != "spec":
            require("C03-07 CERTIFICADA (7/10)" in doc,
                    "Certified seventh stage omitted from current view: " + name)
        if eighth and name != "spec":
            require("C03-08 CERTIFICADA (8/10)" in doc,
                    "Certified eighth stage omitted from current view: " + name)
    require("2/37 capítulos certificados (C01 y C02)" in docs["master"] and
            WORD_HASH in docs["master"],
            "Master reference/source authority lost")
    require("C03-06 CERRADA TÉCNICAMENTE" in docs["acta"] and
            "NO CERTIFICADA" in docs["acta"] and OWNER in docs["acta"],
            "Historical acta must not be rewritten as a retrospective owner approval")
    require("11/11 CI POSTMERGE" in docs["prior"] and
            phase.CERTIFIED_MERGE in docs["prior"],
            "Earlier fifth-phase certification lost")
    for term in (phase.SIXTH_MERGE, "C03-06 CERTIFICADA (6/10)",
                 "C03-05 CERTIFICADA (5/10)", "PR #60", "PR #61",
                 "5/5 CI PREMERGE", "5/5 CI POSTMERGE",
                 "C03-07 NO INICIADA", "C03 NO CERTIFICADO", "C04 BLOQUEADO",
                 "2/37 capítulos certificados (C01 y C02)",
                 "25/25", "15/15", "12/12", "27/27", "83 Chromium",
                 "37 casos HTTP", "MySQL8.4", WORD_HASH, OWNER,
                 "No se certifican BOLA/IDOR/MFA", "Rollback"):
        require(term in evidence, "Verified postmerge proof incomplete: " + term)
    for rid in RUNS:
        require(rid in evidence, "Exact workflow run ID absent: " + rid)
    require(phase.SIXTH_PROOF in row and "C03 CERTIFICADO" not in row,
            "C03-06 certificate cannot advance the chapter")


def negative_mutations(docs):
    row = next((line for line in docs["plan"].splitlines() if line.startswith("C03 | ")), "")
    eighth = phase.eighth_certified(row)
    seventh = phase.seventh_certified(row)
    from_marker = phase.EIGHTH_STATUS if eighth else phase.SEVENTH_STATUS if seventh else phase.SIXTH_STATUS
    to_marker = ("**C03-01 A C03-09 CERTIFICADAS (9/10)**" if eighth else
                 phase.EIGHTH_STATUS if seventh else phase.SEVENTH_STATUS)
    next_marker = phase.next_locked(row)
    illicit_next = phase.next_illicit(row)
    cases = (
        ("unearned next certificate", "plan", from_marker, to_marker),
        ("premature next", "plan", next_marker, illicit_next),
        ("unearned chapter", "plan", "C03 NO CERTIFICADO", "C03 CERTIFICADO"),
        ("unearned C04", "plan", "C04 | Multitenancy, dominios y aprovisionamiento | PENDIENTE",
         "C04 | Multitenancy, dominios y aprovisionamiento | INICIADO"),
        ("forged SHA", "plan", phase.SIXTH_MERGE, "0" * 40),
        ("forged CI count", "plan", "5/5 CI POSTMERGE SUCCESS", "0/5 CI POSTMERGE SUCCESS"),
        ("forged PR", "plan", "PR #61", "PR #99"),
        ("forged Word", "evidence", WORD_HASH, "0" * 64),
        ("forged Actions run", "evidence", RUNS[-1], "00000000000"),
        ("missing explicit owner", "evidence", OWNER, "SIN APROBACION"),
        ("unsafe private claim", "evidence", "No se certifican BOLA/IDOR/MFA",
         "Sí se certifican BOLA/IDOR/MFA"),
        ("unlocked C04 in proof", "evidence", "C04 BLOQUEADO", "C04 HABILITADO"),
        ("changed current master next", "master", next_marker, illicit_next),
        ("false preparation status", "prep", "C03-06 CERTIFICADA (6/10)",
         "C03-06 NO CERTIFICADA (6/10)"),
        ("unlocked AGENTS next", "agents", next_marker, illicit_next),
        ("README advanced illegally", "readme",
         "C03-08 CERTIFICADA (8/10)" if eighth else "C03-07 CERTIFICADA (7/10)" if seventh else "C03-06 CERTIFICADA (6/10)",
         "C03-09 CERTIFICADA (9/10)" if eighth else "C03-08 CERTIFICADA (8/10)" if seventh else "C03-07 CERTIFICADA (7/10)"),
    )
    for label, key, old, forged in cases:
        require(old in docs[key], "Invalid mutation fixture: " + label)
        wrong = deepcopy(docs)
        wrong[key] = wrong[key].replace(old, forged)
        try:
            validate(wrong)
        except (AssertionError, ValueError, KeyError, TypeError):
            continue
        raise AssertionError("Unsafe stage-six documentary mutation accepted: " + label)
    print("PASS C03-06 status B: 16/16 adversarial advancement/forged-evidence attempts rejected.")


def run():
    require(sha256(WORD.read_bytes()).hexdigest() == WORD_HASH,
            "Canonical Word SHA differs from approved authority")
    docs = {name: path.read_text(encoding="utf-8") for name, path in SOURCES.items()}
    validate(docs)
    negative_mutations(docs)
    print("PASS C03-06 status A: certified sixth microphase documented with exact runs and locked seventh/C03/C04.")
    print("LIMIT: this verifies text+SHA only; runtime and GitHub job statuses require independent real CI.")


if __name__ == "__main__":
    try:
        run()
    except (AssertionError, OSError, KeyError, ValueError, TypeError) as exc:
        print("FAIL C03-06 STATUS:", str(exc), file=sys.stderr)
        sys.exit(1)
