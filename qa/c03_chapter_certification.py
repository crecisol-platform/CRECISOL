#!/usr/bin/env python3
"""C03 chapter certification: independent prospective state ledger A/B/C gate.

Historical chapter gates are preserved and exercised independently. This gate
never invents builds, artifacts, GitHub statuses or private-tenant coverage.
"""
from pathlib import Path
import os
import re
import sys

from master_source_guard import canonical_active_word, CURRENT_WORD_SHA, CURRENT_PDF_SHA
from c03_01_source import actual_graph

ROOT = Path(__file__).resolve().parents[1]
PROOF = ROOT / ".c03-chapter-cert-proof"
BASE = "5b7c963c310d4947519ce4c8959568f8ad02143f"
TECH_SHA = "a8726d3ffd0ee08aae8bd25405b318ef262dea6c"
REPAIR_SHA = "a62764cbe78880ab76223b8dcefaf9e84e5c011a"
ACTA_PATH = "docs/c03/C03_CERTIFICACION_INTEGRAL_2026_11_11.md"
LEDGER_PATH = "docs/ESTADO_VIGENTE_CAPITULOS.md"


def need(ok, label):
    if not ok:
        raise AssertionError(label)


def read(path):
    location = ROOT / path
    need(location.is_file(), "Missing required evidence: " + path)
    return location.read_text(encoding="utf-8")


def contract(acta, ledger):
    need("CERTIFICADO (10/10)" in ledger and
         "3/37 capítulos certificados (C01, C02 y C03)" in ledger,
         "C03 prospective chapter status or count missing")
    need("C04 | HABILITADO PARA PREPARACIÓN DEL C04-01, NO CERTIFICADO" in ledger,
         "C04 must be enabled for preparation only; not certified")
    need("solo cuando" in ledger and
         "CI PREMERGE" in ledger and "CI POSTMERGE" in ledger,
         "Missing mandatory CI activation condition")
    need("«OK, VERIFICA SI YA ESTA TODO LISTO POR FAVOR Y CERTIFICA PARA CONTINUAR CON EL DESARROLLO»" in acta,
         "Explicit conditional owner approval not preserved")
    for sha in (BASE, TECH_SHA, REPAIR_SHA, CURRENT_WORD_SHA, CURRENT_PDF_SHA):
        need(sha in acta, "Evidence SHA missing from acta: " + sha[:12])
    for run in ("43086567997", "43087392709", "43092039462",
                "43092039404", "43092865466"):
        need(run in acta, "Referenced real GitHub run not preserved: " + run)
    need("DI C TAMEN" not in acta and
         "DICTAMEN FAVORABLE CONDICIONADO" in acta and
         "C03 permanece NO CERTIFICADO y C04 BLOQUEADO" in acta,
         "Acta must not assert certification before own CI")
    need("C04-01" in acta and "C04-02..12" in acta,
         "Sequential continuation boundary missing")


def audit_a():
    need(canonical_active_word(), "Canonical Word/PDF verification failed")
    actual_graph()
    acta, ledger = read(ACTA_PATH), read(LEDGER_PATH)
    contract(acta, ledger)
    plan = read("docs/PLAN_37_CAPITULOS.md")
    need("docs/ESTADO_VIGENTE_CAPITULOS.md" in plan,
         "Main plan must link new prospective state")
    need("C03 | Arquitectura técnica limpia y dependencias" in plan,
         "Original plan canonical chapter row removed")
    need("C04 | Multitenancy, dominios y aprovisionamiento | PENDIENTE" in plan,
         "C04 implementation state must remain pending")
    need("C03 NO CERTIFICADO" in plan and "C04 BLOQUEADO" in plan,
         "Historical audit references must be preserved verbatim")
    print("PASS C03 chapter A: Word/PDF, 10 microphases, explicit approval and historical provenance")


def audit_b():
    acta, ledger = read(ACTA_PATH), read(LEDGER_PATH)
    contract(acta, ledger)
    old_api = read("backend/src/Crecisol.Api/Program.cs")
    workflow = read(".github/workflows/c03-chapter-certification.yml")
    need("app.MapPost(" not in old_api and "AddHostedService" not in old_api,
         "Unexpected private API/active worker during C03 closure")
    need("permissions:\n  contents: read" in workflow and
         "pull_request_target:" not in workflow and
         "contents: write" not in workflow and
         "deploy" not in workflow.lower(),
         "Certification workflow permission or deployment boundary broken")
    mutations = (
        ("chapter count", acta, ledger.replace("3/37 capítulos certificados", "2/37 capítulos certificados", 1)),
        ("unearned C04", acta, ledger.replace("HABILITADO PARA PREPARACIÓN DEL C04-01, NO CERTIFICADO",
                                             "CERTIFICADO Y DESPLEGADO", 1)),
        ("missing owner acceptance", acta.replace("«OK, VERIFICA SI YA ESTA TODO LISTO POR FAVOR Y CERTIFICA PARA CONTINUAR CON EL DESARROLLO»", "NO", 1), ledger),
        ("forged base", acta.replace(BASE, "0" * 40, 1), ledger),
        ("missing latest regression", acta.replace("43092865466", "0" * 11, 1), ledger),
        ("lost CI gate", acta, ledger.replace("CI POSTMERGE", "SIN CI", 1)),
        ("no approval constraint", acta.replace("DICTAMEN FAVORABLE CONDICIONADO", "APROBADO SIN PRUEBAS", 1), ledger),
    )
    for label, mutated_acta, mutated_ledger in mutations:
        try:
            contract(mutated_acta, mutated_ledger)
        except AssertionError:
            continue
        raise AssertionError("Unsafe status mutation not rejected: " + label)
    print("PASS C03 chapter B: no private activation, read-only CI, seven forged-state mutations rejected")


def audit_c():
    need(os.environ.get("GITHUB_ACTIONS") == "true",
         "Chapter audit C requires real GitHub Actions runner")
    sha, run = os.getenv("GITHUB_SHA", ""), os.getenv("GITHUB_RUN_ID", "")
    need(re.fullmatch(r"[0-9a-f]{40}", sha) and re.fullmatch(r"[0-9]+", run),
         "Absent current CI SHA and run identifiers")
    for gate in ("A", "B"):
        p = PROOF / gate
        need(p.is_file() and
             p.read_text(encoding="utf-8").strip() == "PASS " + gate + " sha=" + sha + " run=" + run,
             "Missing same-run proof for earlier audit: " + gate)
    contract(read(ACTA_PATH), read(LEDGER_PATH))
    print("PASS C03 chapter C: real CI identity, same-run A/B markers, chapter continuity and rollback")
    print("LIMIT: prior .NET10/Angular22/MySQL8.4/Chromium runtime evidence is linked, not re-created by this documentary gate")


def main():
    need(len(sys.argv) == 2 and sys.argv[1] in ("A", "B", "C"),
         "Usage: c03_chapter_certification.py A|B|C")
    {"A": audit_a, "B": audit_b, "C": audit_c}[sys.argv[1]]()


if __name__ == "__main__":
    try:
        main()
    except (AssertionError, OSError, KeyError, ValueError) as exc:
        print("FAIL C03 certification:", exc, file=sys.stderr)
        sys.exit(1)
