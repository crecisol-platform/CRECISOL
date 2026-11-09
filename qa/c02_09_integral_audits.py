#!/usr/bin/env python3
"""C02-09 integral technical A/B/C. Never certifies C02 or enables C03.

CI-only proof files are emitted AFTER real commands succeed; this script
independently validates source/contract risk gates and those execution markers.
"""
from pathlib import Path
from copy import deepcopy
import json
import re
import sys

import c02_01_source as source
import c02_08_audits as journeys

ROOT = Path(__file__).resolve().parents[1]
PROOF = ROOT / ".c02-09-ci-proof"
PREP = ROOT / "docs/C02_PREPARACION.md"
PLAN37 = ROOT / "docs/PLAN_37_CAPITULOS.md"
SPEC = ROOT / "docs/c02/C02_09_ESPECIFICACION.md"
ACTA = ROOT / "docs/c02/C02_09_AUDITORIA_Y_ACTA.md"
ROUTES = ROOT / "frontend/src/app/app.routes.ts"
ANGULAR = ROOT / "frontend/angular.json"
WORKFLOW = ROOT / ".github/workflows/c02-09-integral.yml"
PREVIOUS_HEAD = "cc1ec4a61afd50eaf680644db5ca94767f039f8e"


def require(ok, message):
    if not ok:
        raise AssertionError(message)


def contents(path):
    return path.read_text(encoding="utf-8")


def proof(name):
    path = PROOF / name
    require(path.is_file() and contents(path).strip() == "PASS: " + name,
            "Missing real CI evidence of command: " + name)


def originals():
    original, roles, _, _ = source.original_parts()
    needs, public, private, states = journeys.input_sources()
    contract = journeys.load(journeys.CONTRACT)
    journeys.validate_contract(contract, (needs, public, private, states))
    require(len(original) == 11 and len(roles) == 7 and
            len(needs["experiences"]) == 6, "C02 original chapter source shifted")
    require(len(contract["journeys"]) == 6 and
            len(contract["negative_cases"]) == 18 and
            len(contract["cross_cutting_negative_cases"]) == 8 and
            len(contract["navigation"]["private_proposed_paths"]) == 12,
            "Cross-phase journey or negative coverage changed")
    return contract, (needs, public, private, states)


def sequence():
    s, plan, spec = contents(PREP), contents(PLAN37), contents(SPEC)
    require("8/9" in s and "1/37" in s and
            "C02 **NO CERTIFICADO**" in s and "C03 bloqueado" in s,
            "C02 changed certification or next-chapter state prematurely")
    require("1/37 capítulos certificados" in plan and
            "C02 **NO CERTIFICADO**" in plan and
            "C03 bloqueado" in plan,
            "37-chapter plan illegally promotes C02/C03")
    for token in ("C02-09", "EN AUDITORÍA", "MAE-C02-01..08",
                  "58/58", "37 checks HTTP", "7 E2E", "P01",
                  "N01", "N18", "J01", "J06",
                  "NO CERTIFICADO", "ACEPTACIÓN HUMANA"):
        require(token.casefold() in spec.casefold(), "Missing C02 integral scope: " + token)
    require(PREVIOUS_HEAD in spec and source.EXPECTED_SHA in spec,
            "Previous close or canonical Word SHA missing")
    for index in range(1, 9):
        p = ROOT / f"docs/c02/C02_{index:02d}_ACTA_MICROFASE.md"
        require(p.is_file(), "Prior microphase acta missing: " + str(p))
        txt = contents(p)
        require(f"C02-{index:02d}" in txt and
                ("CERRAD" in txt.upper() or "CI POSTMERGE" in txt.upper()),
                "Prior microphase acta missing technical-close evidence: " + p.name)
    require("C02-09 | Auditorías integrales" in s,
            "C02-09 chapter gate not documented")
    if ACTA.is_file():
        acta = contents(ACTA)
        require("9/9" in s and "9/9" in acta and
                "PENDIENTE ACEPTACIÓN HUMANA" in acta and
                "C02 NO CERTIFICADO" in acta and "C03 BLOQUEADO" in acta and
                "POSTMERGE" in acta,
                "Formal acta may be integrated only with technical 9/9, "
                "express owner-acceptance pending and C03 denied")
    else:
        require("EN AUDITORÍA" in s and
                "C02-09" in spec and "8/9" in s,
                "No integral acta: prior 8/9 only, C02-09 must be in audit")


def mutate(data, source_inputs, change, title):
    test = deepcopy(data)
    change(test)
    try:
        journeys.validate_contract(test, source_inputs)
    except (AssertionError, IndexError, KeyError, ValueError, TypeError):
        return
    raise AssertionError("Unsafe mutation accepted: " + title)


def audit_a():
    contract, context = originals()
    sequence()
    for key in ("source", "c02-regressions", "domain", "application"):
        proof(key)
    # The original Word has 11 paragraphs P01..11 and 6 persona/demo rows.
    index = contents(ROOT / "docs/c02/C02_01_ESPECIFICACION.md")
    identifiers = source.matrix_ids(index)
    require(source.valid_ids(identifiers), "C02 17 source references untraceable")
    for id in [f"P{i:02d}" for i in range(1, 12)] + [
            f"T01-R{i:02d}" for i in range(1, 7)]:
        require(id in index, "Missing source ID " + id)
    for name in ("C02_02_MAPA_NECESIDADES.json", "C02_03_SITEMAP.json",
                 "C02_04_NAVEGACION_PRIVADA.json",
                 "C02_05_ESTADOS_INTERACCION.json",
                 "C02_06_PATRONES_FORM_TABLA.json",
                 "C02_07_MOBILE_ACCESIBILIDAD.json",
                 "C02_08_RECORRIDOS_CONTRATOS.json"):
        require((ROOT / "docs/c02" / name).is_file(), "Missing C02 input: " + name)
    mutate(contract, context, lambda m: m["source"].update(sha256="0"*64),
           "Word pin changed")
    mutate(contract, context, lambda m: m["journeys"][0]["needs"].pop(),
           "lost original requirement")
    mutate(contract, context, lambda m: m["negative_cases"].pop(),
           "lost source negative")
    print("AUDIT A INTEGRAL PASS C02-09: original Word and 17 source IDs, "
          "8 closed actas, 6 journeys/18 needs, 26 negative cases; "
          "3 extra mutations rejected, CI prior regression+Domain/Application evidence.")


def audit_b():
    contract, context = originals()
    sequence()
    for key in ("c02-regressions", "c02-browser", "live-stack"):
        proof(key)
    production_routes = contents(ROUTES)
    paths = re.findall(r"^\s*path:\s*'([^']*)'", production_routes, re.M)
    require(paths == ["", "experiencias", "demostraciones",
                      "alta-empresas", "**"], "Production route allowlist changed")
    require(contract["navigation"]["public_brand_api"] ==
            "GET /api/v1/public/branding/platform" and
            contract["navigation"]["no_private_backend"] is True,
            "Public read-model was used as private backend")
    require(not any(p in paths for p in ["consola", "negocio", "trabajo",
                                        "mi-cuenta", "login"]),
            "Private route activated")
    # Regression tests are real Angular/Playwright; private backend does NOT exist.
    public_tests = contents(ROOT / "frontend/tests/c02-08.journeys-public.spec.ts")
    for marker in ("PRIVATE_TENANT_B", "/consola/resumen",
                   "/mi-cuenta/historial", "tenant_id=OTHER",
                   "data-interaction-state", "login company selector"):
        require(marker.lower() in public_tests.lower(),
                "Private/routing negative test is missing: " + marker)
    for p in (ROOT / "docs/c02").glob("C02_0[1-8]_ACTA_MICROFASE.md"):
        require("SOLQARYN" in contents(p).upper() or "C02" in contents(p),
                "Missing scope provenance " + p.name)
    mutate(contract, context, lambda m: m["navigation"].update(no_private_backend=False),
           "future private API activated")
    mutate(contract, context, lambda m: m["journeys"][2].update(future_only=False),
           "SuperAdmin private claim activated")
    mutate(contract, context, lambda m: m["parameterization"].update(
        arbitrary_executable_config_allowed=True), "untrusted executable configuration")
    mutate(contract, context, lambda m: m["constraints"].update(real_payment_enabled=True),
           "real payments granted")
    print("AUDIT B INTEGRAL PASS C02-09: four public routes only; "
          "12 private paths denied, 4 extra security mutations rejected, "
          "disposable public-only MySQL/API/Angular evidence.")
    print("LIMIT: private MFA/BOLA/IDOR/PII/branch/backup authorization remains UNTESTED "
          "until private backend modules exist.")


def audit_c():
    contract, context = originals()
    sequence()
    for key in ("build", "harness", "c02-browser", "c01-browser",
                "live-stack", "domain", "application"):
        proof(key)
    angular = json.loads(contents(ANGULAR))
    build = angular["projects"]["crecisol-web"]["architect"]["build"]
    require(build["options"]["browser"] == "src/main.ts" and
            build["configurations"]["pattern-harness"]["browser"] ==
            "tests/pattern-harness/main.ts" and
            build["defaultConfiguration"] == "production",
            "Production bootstrap replaced by synthetic harness")
    wf = contents(WORKFLOW)
    for marker in ("permissions:\n  contents: read", "qa/c02_09_integral_audits.py A",
                   "qa/c02_09_integral_audits.py B",
                   "qa/c02_09_integral_audits.py C",
                   "bash qa/c01_07_real_stack.sh", "npm audit --omit=dev",
                   "dotnet run -c Release",
                   "playwright.journeys-patterns.config.ts",
                   "playwright.patterns-a11y.config.ts",
                   "ng build --configuration pattern-harness",
                   "c01-08.brand-revision.spec.ts"):
        require(marker in wf, "Missing real regression or audit step: " + marker)
    for fn, expected in (("c02-08.journeys-public.spec.ts", 11),
                         ("c02-08.journeys-patterns.spec.ts", 5)):
        s = contents(ROOT / "frontend/tests" / fn)
        require(len(re.findall(r"\btest\('", s)) == expected,
                "Missing real Chromium cases " + fn)
    mutate(contract, context, lambda m: m["tests"].update(expected_total_chromium=1),
           "C02 regression lost")
    mutate(contract, context, lambda m: m["tests"].update(manual_wcag_certified=True),
           "false WCAG manual certification")
    mutate(contract, context, lambda m: m["navigation"].update(no_new_routes=False),
           "production routes widened")
    print("AUDIT C INTEGRAL PASS C02-09: Angular22 production/harness, .NET10, "
          "real MySQL8.4 public-read-model, 58 C02 Chromium plus C01/UI and "
          "C01-stack regressions, 3 extra architecture mutations rejected.")
    print("LIMIT: accessibility automated only; no live private auth, "
          "admin panel, production performance benchmark or paid features.")


def main():
    require(len(sys.argv) == 2 and sys.argv[1] in ("A", "B", "C"),
            "Usage: python3 qa/c02_09_integral_audits.py A|B|C")
    {"A": audit_a, "B": audit_b, "C": audit_c}[sys.argv[1]]()


if __name__ == "__main__":
    try:
        main()
    except (AssertionError, IndexError, KeyError, ValueError, TypeError, OSError) as error:
        print("AUDIT INTEGRAL FAILED C02-09: " + str(error), file=sys.stderr)
        sys.exit(1)
