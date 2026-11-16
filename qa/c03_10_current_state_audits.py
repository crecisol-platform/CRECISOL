#!/usr/bin/env python3
"""C03 retrospective A/B/C: current state, architecture and C01/C02 UI boundaries.

This gate complements (never replaces) the real C03-10 .NET/Angular/MySQL/
Chromium gate. Historical snapshots may remain below the current README block.
"""
from pathlib import Path
import os
import re
import sys

import c03_01_source as architecture

ROOT = Path(__file__).resolve().parents[1]
BEGIN = "<!-- CRECISOL-ESTADO-C03-RETRO:INICIO -->"
END = "<!-- CRECISOL-ESTADO-C03-RETRO:FIN -->"


def need(ok, message):
    if not ok:
        raise AssertionError(message)


def read(path):
    location = ROOT / path
    need(location.is_file(), "Missing expected project file: " + path)
    return location.read_text(encoding="utf-8")


def active_summary(readme):
    need(readme.count(BEGIN) == 1 and readme.count(END) == 1,
         "README requires exactly one bounded current-status block")
    before, after = readme.split(BEGIN, 1)
    current, history = after.split(END, 1)
    need(before.startswith("# CRECISOL") and
         "## Registro histórico de C03" in history and
         "## Inventario histórico al inicio de C03" in history,
         "Historic C03 evidence was hidden or rewritten")
    return current


def state_contract(readme, ledger):
    present = active_summary(readme)
    authoritative = ledger.split("**INTEGRACIÓN OPERATIVA LOCAL C05", 1)[0]
    need("**CERTIFICACIÓN INTEGRAL OPERATIVA C05" in authoritative and
         "5/37" in authoritative and
         "C05 **CERTIFICADO en desarrollo local aislado**" in authoritative and
         "**C06 permanece BLOQUEADO**" in authoritative,
         "The authoritative institutional ledger changed; re-audit before proceeding")
    for value in (
        "5/37 capítulos certificados",
        "C03: 10/10",
        "C04: 12/12",
        "C05",
        "C06 permanece BLOQUEADO",
        "Angular 22",
        "ASP.NET Core 10",
        "MySQL 8.4",
        "16 migraciones/reversiones",
        "no demuestran preparación productiva",
        "Sin despliegues",
        "Documento Maestro Único",
    ):
        need(value in present, "Missing or unsafe active status contract: " + value)
    need("una sola ruta pública" not in present and
         "no existe identidad privada local" not in present,
         "Outdated C03-era runtime limitations repeated as current")
    return present


def audit_a():
    state_contract(read("README.md"), read("docs/ESTADO_VIGENTE_CAPITULOS.md"))
    reference = read("docs/MASTER_REFERENCE.md")
    need("CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx" in reference and
         "CRECISOL_DOCUMENTO_MAESTRO_UNICO.pdf" in reference,
         "Single master provenance is not linked")
    need("C03 CERTIFICADO" in read("docs/c03/C03_CERTIFICACION_INTEGRAL_2026_11_11.md"),
         "Historical C03 chapter certificate missing")
    print("PASS C03 retrospective A: active institutional state, canonical master and preserved history")


def audit_b():
    current = read("README.md")
    ledger = read("docs/ESTADO_VIGENTE_CAPITULOS.md")
    state_contract(current, ledger)
    changes = (
        ("wrong chapter count", current.replace("5/37 capítulos certificados",
                                                "3/37 capítulos certificados", 1)),
        ("unearned C06", current.replace("C06 permanece BLOQUEADO",
                                         "C06 HABILITADO", 1)),
        ("missing C03 gate", current.replace("C03: 10/10", "C03: 0/10", 1)),
        ("false production safety", current.replace(
            "no demuestran preparación productiva", "garantizan preparación productiva", 1)),
        ("missing deployment limit", current.replace("Sin despliegues",
                                                      "Con despliegues", 1)),
    )
    for label, altered in changes:
        need(altered != current, "Mutation was not applied: " + label)
        try:
            state_contract(altered, ledger)
        except AssertionError:
            continue
        raise AssertionError("Unsafe prospective-state mutation accepted: " + label)
    architecture.actual_graph()
    print("PASS C03 retrospective B: live clean-architecture graph and 5/5 forged-state mutations rejected")


def audit_c():
    need(os.getenv("GITHUB_ACTIONS") == "true" and
         re.fullmatch(r"[0-9a-f]{40}", os.getenv("GITHUB_SHA", "")),
         "C03 retrospective C requires a real same-SHA CI runner")
    state_contract(read("README.md"), read("docs/ESTADO_VIGENTE_CAPITULOS.md"))
    shared = read("frontend/src/styles.css")
    landing = read("frontend/src/app/pages/platform-home.component.css")
    public = read("frontend/src/app/pages/public-information.component.css")
    routes = read("frontend/src/app/app.routes.ts")
    need(":focus-visible" in shared and "prefers-reduced-motion" in shared,
         "Shared C01/C02 accessibility fallback is missing")
    need(all(".skip-link" in css and "var(--brand-primary)" in css
             for css in (landing, public)),
         "C01/C02 public editorial and brand boundaries changed")
    need("loadComponent" in routes and "path: 'experiencias'" in routes and
         "path: 'demostraciones'" in routes,
         "Approved lazy public navigation is no longer present")
    print("PASS C03 retrospective C: C01/C02 UI foundation and lazy route contracts")
    print("LIMIT: visual inspection, .NET/MySQL/Chromium runtime proof belong to separate CI gates")


def main():
    need(len(sys.argv) == 2 and sys.argv[1] in ("A", "B", "C"),
         "Usage: c03_10_current_state_audits.py A|B|C")
    {"A": audit_a, "B": audit_b, "C": audit_c}[sys.argv[1]]()


if __name__ == "__main__":
    try:
        main()
    except (AssertionError, OSError, ValueError, KeyError) as exc:
        print("FAIL C03 retrospective:", exc, file=sys.stderr)
        sys.exit(1)
