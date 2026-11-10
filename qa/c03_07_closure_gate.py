#!/usr/bin/env python3
"""CRECISOL C03-07 documentary gate: historical 6/10 -> conditional 7/10.

GitHub PRE/POSTMERGE conclusions require independent API checks on the
exact HEAD/merge SHA; this gate validates immutable IDs, text and same-job
real compiler/MySQL proof emitted by the separate C03-07 workflow steps.
"""
from copy import deepcopy
from hashlib import sha256
from pathlib import Path
import sys

import c03_07_outbox_audits as technical
import c03_06_status_audits as previous
import c03_05_status as phase

from master_source_guard import canonical_active_word

ROOT = Path(__file__).resolve().parents[1]
WORD = ROOT / "docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx"
HASH = "e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e"
OWNER = "OK, ya adelante, continúa, por favor, hasta certificarla."
TECH_HEAD = "f7efc6d2b62db916bb429303887bcae3d6b0a4fd"
TECH_MERGE = "a256885afa5ee2f639bd4ed6f7d8595b709e6409"
PRE = ("42815359099", "42815359163")
POST = ("42815698115", "42815698155", "42815698446", "42815698259",
        "42815698005", "42815698160", "42815697923", "42815698031")
FILES = {
    "acta": ROOT / "docs/c03/C03_07_ACTA_MICROFASE.md",
    "spec": ROOT / "docs/c03/C03_07_ESPECIFICACION.md",
    "plan": ROOT / "docs/PLAN_37_CAPITULOS.md",
    "prep": ROOT / "docs/C03_PREPARACION.md",
    "master": ROOT / "docs/MASTER_REFERENCE.md",
    "prior": ROOT / "docs/c03/C03_06_CERTIFICACION_POSTMERGE.md",
}

def require(ok, why):
    if not ok:
        raise AssertionError(why)

def validate(docs):
    acta, plan, spec = docs["acta"], docs["plan"], docs["spec"]
    c03 = next((row for row in plan.splitlines() if row.startswith("C03 | ")), "")
    c04 = next((row for row in plan.splitlines() if row.startswith("C04 | ")), "")
    require(phase.fifth_plan_safe(plan), "Current chapter state must preserve canonical lock")
    require(c04 == "C04 | Multitenancy, dominios y aprovisionamiento | PENDIENTE",
            "C04 cannot begin before C03 completion")
    require("2/37 capítulos certificados (C01 y C02)" in plan and
            "C03 NO CERTIFICADO" in c03 and "C04 BLOQUEADO" in c03,
            "Only two chapters are certified")
    for term in (OWNER, TECH_HEAD, TECH_MERGE, HASH, "T01-R06",
                 "C03-07 CERRADA TÉCNICAMENTE", "NO CERTIFICADA",
                 "C03-01..C03-06", "8/8 workflows POSTMERGE SUCCESS",
                 "26 aserciones", "40 aserciones", "16/16 mutaciones adversariales",
                 "Auditoría A", "Auditoría B", "Auditoría C", "MySQL8.4",
                 "BOLA/IDOR/MFA", "C03-08 NO INICIADA", "C03 NO CERTIFICADO",
                 "C04 BLOQUEADO", "skipped", "Rollback", "PR técnico"):
        require(term.casefold() in acta.casefold(), "Acta evidence or limitation missing: " + term)
    require(all(rid in acta for rid in PRE+POST),
            "Exact technical PRE/POSTMERGE run IDs are missing")
    require(TECH_MERGE in docs["acta"] and
            HASH in spec and "C03-07" in spec and
            "C03-06 CERTIFICADA (6/10)" in docs["prior"] and
            phase.SIXTH_MERGE in docs["prior"],
            "Canonical technical source and predecessor proof missing")
    require("C03 CERTIFICADO" not in acta and "C04 HABILITADO" not in acta,
            "An acta must not certify the entire chapter or unlock C04")
    require(phase.next_locked(c03) in docs["prep"],
            "Preparation next-phase lock disagrees with current plan")
    if getattr(phase, "seventh_certified", lambda _: False)(c03):
        cert = ROOT / "docs/c03/C03_07_CERTIFICACION_POSTMERGE.md"
        require(cert.is_file(), "Stage 7 certified with no independent evidence")
        proof = cert.read_text(encoding="utf-8")
        for marker in (OWNER, TECH_HEAD, TECH_MERGE, HASH, "C03-07 CERTIFICADA (7/10)",
                       "C03-08 NO INICIADA", "C03 NO CERTIFICADO",
                       "C04 BLOQUEADO", "PR #63", "PR #64"):
            require(marker in proof, "Seventh-stage certification proof lacks: " + marker)
        require("C03-07 CERTIFICADA (7/10)" in c03 and
                (("C03-08 CERTIFICADA (8/10)" in c03 and
                  "C03-09 NO INICIADA" in c03) if phase.eighth_certified(c03)
                 else "C03-08 NO INICIADA" in c03) and
                "C03-07 CERTIFICADA (7/10)" in docs["master"],
                "Canonical seventh-stage promotion incomplete")
    else:
        require("C03-07 NO INICIADA" in c03 and
                "C03-08 NO INICIADA" in acta,
                "Seventh-stage technical closure has not passed documentary CI")

def mutate(docs):
    cases = (
        ("owner removed", "acta", OWNER, "AUTORIZACION AUSENTE"),
        ("forge canonical Word SHA", "acta", HASH, "0"*64),
        ("forge technical SHA", "acta", TECH_MERGE, "0"*40),
        ("forge technical HEAD", "acta", TECH_HEAD, "0"*40),
        ("forge PREMERGE run", "acta", PRE[0], "00000000000"),
        ("forge POSTMERGE run", "acta", POST[0], "00000000000"),
        ("erase CI count", "acta", "8/8 workflows POSTMERGE SUCCESS",
         "0/8 workflows POSTMERGE SUCCESS"),
        ("erase .NET assertions", "acta", "26 aserciones", "0 aserciones"),
        ("erase MySQL assertions", "acta", "40 aserciones", "0 aserciones"),
        ("erase adverse mutations", "acta", "16/16 mutaciones adversariales",
         "0/16 mutaciones adversariales"),
        ("erase authorization boundary", "acta", "NO CERTIFICADA", "CERTIFICADA"),
        ("unearned C03", "acta", "C03 NO CERTIFICADO", "C03 CERTIFICADO"),
        ("unearned C04", "acta", "C04 BLOQUEADO", "C04 HABILITADO"),
        ("missing next lock", "acta", "C03-08 NO INICIADA", "C03-08 CERTIFICADA"),
        ("forget technical antecedent", "prior", phase.SIXTH_MERGE, "0"*40),
    )
    for name, field, before, after in cases:
        require(before in docs[field], "Bad documentary mutation fixture: " + name)
        changed = deepcopy(docs)
        changed[field] = changed[field].replace(before, after)
        try:
            validate(changed)
        except (AssertionError, KeyError, TypeError, ValueError):
            continue
        raise AssertionError("Unsafe documentary mutation accepted: " + name)
    print("PASS C03-07 ACTA B: 15/15 documentary forgery/advancement mutations rejected.")

def run():
    require(canonical_active_word(HASH),
            "Word authority changed")
    technical.audit_a()
    technical.audit_b()
    technical.audit_c()
    previous.run()  # Preserve the historical sixth-stage 16/16 negatives.
    docs = {name: p.read_text(encoding="utf-8") for name, p in FILES.items()}
    validate(docs)
    mutate(docs)
    print("PASS C03-07 ACTA A/B/C: 8/8 technical POSTMERGE references, same-job runtime, "
          "15/15 documentary mutations and prior stage protected.")
    print("LIMIT: this program verifies text+local proof; only GitHub API may confirm "
          "independent documentary PREMERGE/POSTMERGE status and exact SHA.")

if __name__ == "__main__":
    try:
        run()
    except (AssertionError, OSError, KeyError, TypeError, ValueError) as error:
        print("FAIL C03-07 ACTA:", error, file=sys.stderr)
        sys.exit(1)
