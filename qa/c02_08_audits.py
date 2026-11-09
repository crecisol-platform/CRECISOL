#!/usr/bin/env python3
"""C02-08: immutable Word-pinned journey contracts, A/B/C + 18 mutations.

The public and synthetic Angular browser tests are executed separately by CI.
This script does NOT certify BOLA/IDOR/MFA or private backend functionality.
"""
from copy import deepcopy
import json
from pathlib import Path
import re
import sys

import c02_01_source
import c02_07_audits

ROOT = Path(__file__).resolve().parents[1]
CONTRACT = ROOT / "docs/c02/C02_08_RECORRIDOS_CONTRATOS.json"
SPEC = ROOT / "docs/c02/C02_08_ESPECIFICACION.md"
PREP = ROOT / "docs/C02_PREPARACION.md"
ACTA = ROOT / "docs/c02/C02_08_ACTA_MICROFASE.md"
NEEDS = ROOT / "docs/c02/C02_02_MAPA_NECESIDADES.json"
PUBLIC_MAP = ROOT / "docs/c02/C02_03_SITEMAP.json"
PRIVATE_MAP = ROOT / "docs/c02/C02_04_NAVEGACION_PRIVADA.json"
STATES = ROOT / "docs/c02/C02_05_ESTADOS_INTERACCION.json"
ROUTES = ROOT / "frontend/src/app/app.routes.ts"
ANGULAR = ROOT / "frontend/angular.json"
PUBLIC_TESTS = ROOT / "frontend/tests/c02-08.journeys-public.spec.ts"
PATTERN_TESTS = ROOT / "frontend/tests/c02-08.journeys-patterns.spec.ts"
HARNESS_CONFIG = ROOT / "frontend/playwright.journeys-patterns.config.ts"
WORKFLOW = ROOT / ".github/workflows/c02-08-journey-contracts.yml"


def require(test, message):
    if not test:
        raise AssertionError(message)


def load(path):
    return json.loads(path.read_text(encoding="utf-8"))


def input_sources():
    # Verify the actual pinned Word bytes via the upstream reader, not a markdown claim.
    c02_01_source.original_parts()
    old = c02_07_audits.read_data()
    c02_07_audits.valid_contract(old, c02_07_audits.parent_contexts())
    return load(NEEDS), load(PUBLIC_MAP), load(PRIVATE_MAP), load(STATES)


def validate_contract(data, original):
    needs, pub, priv, states = original
    require(isinstance(data, dict) and data.get("artifact") ==
            "CRECISOL-C02-08-JOURNEY-CONTRACTS" and data.get("schema_version") == 1
            and data.get("delivery_state") ==
            "real-public-journeys-and-isolated-synthetic-ux-tests-only",
            "C02-08 root/version/scope changed")
    source = data.get("source", {})
    require(source.get("master") == "docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx"
            and source.get("sha256") == c02_01_source.EXPECTED_SHA
            and source.get("chapter") == "02" and source.get("process_gates") ==
            "MAE-C02-01..08" and source.get("previous_acta") ==
            "docs/c02/C02_07_ACTA_MICROFASE.md" and source.get("previous_dev_sha") ==
            "10c14166a333cc0509c06eedaaad420b409d86c9",
            "Canonical Word or prior microphase SHA was forged")
    require(source.get("previous_postmerge_runs") == [
            42661826797, 42661826836, 42661826763, 42661826921,
            42661826765, 42661826790, 42661826764, 42661826837,
            42661826784, 42661826751],
            "All ten verified C02-07 POSTMERGE runs must remain traceable")
    require(source.get("dependencies") == [
            "docs/c02/C02_02_MAPA_NECESIDADES.json",
            "docs/c02/C02_03_SITEMAP.json",
            "docs/c02/C02_04_NAVEGACION_PRIVADA.json",
            "docs/c02/C02_05_ESTADOS_INTERACCION.json",
            "docs/c02/C02_06_PATRONES_FORM_TABLA.json",
            "docs/c02/C02_07_MOBILE_ACCESIBILIDAD.json"],
            "Journey chain no longer links to all six previous contracts")

    nav = data.get("navigation", {})
    paths = [route["path"] for route in pub["routes"]]
    require(paths == ["/", "/experiencias", "/demostraciones", "/alta-empresas"]
            and nav.get("public_allowlist") == paths and nav.get("wildcard") == "**"
            and nav.get("public_brand_api") ==
            "GET /api/v1/public/branding/platform",
            "Public platform route/API allowlist widened")
    future = [n["proposed_path"] for c in priv["contexts"] for n in c["navigation"]]
    require(len(future) == 12 and nav.get("private_proposed_paths") == future
            and all(nav.get(key) is True for key in
                    ("no_new_routes", "no_private_backend",
                     "no_database_changes", "no_deployment")),
            "Private routes were activated, dropped, or server/database scope changed")

    expected_states = [s["kind"] for s in states["states"]]
    status = data.get("universal_states", {})
    require(status.get("kinds") == expected_states and
            status.get("active_public") == ["loading", "error", "offline"] and
            status.get("synthetic_only") == ["empty", "confirmation"] and
            status.get("stale_publication_reuse") is False and
            status.get("automatic_retry_on_online") is False,
            "Universal state boundary or fresh-only retry weakened")

    journeys = data.get("journeys", [])
    require(isinstance(journeys, list) and len(journeys) == 6,
            "Missing any of six product journeys")
    for i, (journey, original_exp) in enumerate(zip(journeys, needs["experiences"])):
        private = i >= 2
        private_exp = next((c for c in priv["contexts"]
                            if c["experience"] == original_exp["id"]), None)
        require(journey.get("id") == f"J{i+1:02d}" and
                journey.get("experience") == original_exp["id"] and
                journey.get("actor") == original_exp["actor"] and
                journey.get("scope") == original_exp["scope"] and
                journey.get("future_only") is private and
                journey.get("delivery_state") ==
                ("design-only-private-denied" if private else "real-public-read-only"),
                "Journey actor/scope/reality mismatched")
        require(journey.get("entry_path") ==
                (private_exp["proposed_root"] if private else
                 ("/" if i == 0 else "/alta-empresas")),
                "Journey entry URL changed")
        require(journey.get("public_path_sequence") ==
                ([] if private else
                 (["/", "/experiencias", "/demostraciones"] if i == 0
                  else ["/alta-empresas"])),
                "Unexpected public navigation sequence")
        require(journey.get("denied_proposed_paths") ==
                ([n["proposed_path"] for n in private_exp["navigation"]]
                 if private else []),
                "Private proposed routes were lost or published")
        require(journey.get("expected_boundary") ==
                ("404-no-brand-fetch-and-no-private-data" if private else
                 "published-platform-read-only"),
                "Journey authorization/readonly boundary changed")
        require(journey.get("future_backend_checks") ==
                (["verified-host-and-tenant", "identity", "membership",
                  "action", "branch-when-applicable", "resource-ownership",
                  "audit", "revocation"] if private else []),
                "Private backend authorization prerequisites weakened")
        actual_needs = journey.get("needs", [])
        require(len(actual_needs) == 3, "Each experience must preserve three needs")
        for n, raw in zip(actual_needs, original_exp["needs"]):
            require(n.get("id") == raw["id"] and n.get("risk") == raw["risk"]
                    and n.get("source_refs") == raw["source_refs"]
                    and n.get("depends_on") == raw["dependencies"]
                    and n.get("acceptance") == raw["acceptance"]
                    and n.get("negative_case") == raw["negative_case"]
                    and n.get("implementation_state") ==
                    ("future-backend-required" if private
                     else "information-only-no-business-action"),
                    "Original need, security negative or evidence changed: " +
                    raw["id"])

    expected_negs = [(e["id"], n["id"], n["negative_case"])
                     for e in needs["experiences"] for n in e["needs"]]
    negatives = data.get("negative_cases", [])
    require(len(negatives) == 18 and
            [(x.get("experience"), x.get("need"), x.get("threat"))
             for x in negatives] == expected_negs and
            [n.get("id") for n in negatives] ==
            [f"NEG-{n[1]}" for n in expected_negs] and
            all(len(n.get("expected", "")) >= 45 for n in negatives),
            "18 source-derived negative scenarios incomplete")
    extras = data.get("cross_cutting_negative_cases", [])
    require(len(extras) == 8 and
            [n.get("id") for n in extras] ==
            [f"NEG-X{i:02d}" for i in range(1, 9)] and
            all(len(n.get("attack", "")) > 40 and
                len(n.get("expected", "")) > 40 for n in extras),
            "Eight cross-cutting negative scenarios missing")

    tests = data.get("tests", {})
    require(tests.get("inherited_chromium") == 42 and
            tests.get("public_real_angular") == 11 and
            tests.get("isolated_synthetic_angular") == 5 and
            tests.get("expected_total_chromium") == 58 and
            tests.get("public_spec") ==
            "frontend/tests/c02-08.journeys-public.spec.ts" and
            tests.get("isolated_spec") ==
            "frontend/tests/c02-08.journeys-patterns.spec.ts" and
            tests.get("isolated_config") ==
            "frontend/playwright.journeys-patterns.config.ts" and
            tests.get("fake_api_data_only") ==
            "validated-public-platform-brand-response" and
            tests.get("synthetic_fixture_only") is True and
            tests.get("manual_wcag_certified") is False and
            tests.get("private_backend_bola_idor_tested") is False,
            "Real test counts, isolation, or manual security claims fabricated")

    params = data.get("parameterization", {})
    require(params.get("future_scopes") == ["platform", "tenant", "branch", "user"] and
            len(params.get("future_admin_editable", [])) == 10 and
            params.get("future_controls") ==
            ["preview", "version", "approval", "audit", "rollback"] and
            params.get("current_platform_brand") ==
            "same-origin-versioned-published-platform-read-model" and
            params.get("future_admin_panel_implemented") is False and
            params.get("immutable_security") ==
            ["tenant-resolution", "membership", "backend-auth", "roles",
             "resource-ownership", "PII", "DSN"] and
            params.get("arbitrary_executable_config_allowed") is False,
            "Admin-config security invariant or status weakened")
    constraints = data.get("constraints", {})
    require(all(constraints.get(k) is False for k in (
            "chapter_02_certified", "chapter_03_open",
            "real_company_signup_enabled", "real_demo_enabled",
            "real_payment_enabled", "login_company_picker_allowed",
            "superadmin_private_access_implemented")),
            "Premature chapter certification, signup, demo, login or private privileges")
    return journeys


def reject_mutation(data, mutate, name, original):
    temp = deepcopy(data)
    mutate(temp)
    try:
        validate_contract(temp, original)
    except (AssertionError, IndexError, ValueError, KeyError, TypeError):
        return
    raise AssertionError("Adversarial mutation was accepted: " + name)


def audit_a():
    original = input_sources()
    data = load(CONTRACT)
    validate_contract(data, original)
    specification = SPEC.read_text(encoding="utf-8")
    for marker in ("C02-08", "7/9", "MAE-C02-01..08", "E01", "E06",
                   "N01", "N18", "J01", "J06", "NEG-X01", "NEG-X08",
                   "58 pruebas", "NO CERTIFICADO", "C02-09", "rollback"):
        require(marker.lower() in specification.lower(), "Missing specification " + marker)
    reject_mutation(data, lambda d: d.update(artifact="OTHER"),
                    "forged contract", original)
    reject_mutation(data, lambda d: d["source"].update(sha256="0"*64),
                    "forged master SHA", original)
    reject_mutation(data, lambda d: d["source"]["previous_postmerge_runs"].pop(),
                    "lost required previous CI", original)
    reject_mutation(data, lambda d: d["journeys"].pop(),
                    "lost one experience", original)
    reject_mutation(data, lambda d: d["journeys"][4]["needs"].pop(),
                    "lost employee need", original)
    reject_mutation(data, lambda d: d["negative_cases"].pop(),
                    "lost canonical negative case", original)
    print("AUDIT A PASS C02-08: exact Word and prior 10 CI, 6 journeys/18 needs,"
          " 26 scenarios; 6 adversarial source mutations rejected.")


def audit_b():
    original = input_sources()
    data = load(CONTRACT)
    validate_contract(data, original)
    app_routes = ROUTES.read_text(encoding="utf-8")
    require(all(token in app_routes for token in (
            "path: 'experiencias'", "path: 'demostraciones'",
            "path: 'alta-empresas'", "path: '**'")),
            "Production public allowlist modified")
    require(not any(token in app_routes for token in (
            "path: 'consola'", "path: 'negocio'", "path: 'trabajo'",
            "path: 'mi-cuenta'", "path: 'login'")),
            "Private routes leaked into production")
    for p in (PUBLIC_TESTS, PATTERN_TESTS):
        body = p.read_text(encoding="utf-8")
        require("PRIVATE_TENANT_B" in body or p is PATTERN_TESTS,
                "Public stale tenant data must be explicitly denied")
        require("Authorization: Bearer" not in body,
                "Do not forge private session in public journey")
    reject_mutation(data, lambda d: d["navigation"].update(no_private_backend=False),
                    "added unverified private API", original)
    reject_mutation(data, lambda d: d["navigation"]["private_proposed_paths"].pop(),
                    "lost 404 negative route", original)
    reject_mutation(data, lambda d: d["journeys"][2].update(future_only=False),
                    "SuperAdmin became real", original)
    reject_mutation(data, lambda d: d["journeys"][5].update(
                    delivery_state="real-private"), "consumer private navigation", original)
    reject_mutation(data, lambda d: d["universal_states"].update(
                    stale_publication_reuse=True), "stale brand reuse", original)
    reject_mutation(data, lambda d: d["constraints"].update(real_payment_enabled=True),
                    "real payment activation", original)
    reject_mutation(data, lambda d: d["parameterization"].update(
                    arbitrary_executable_config_allowed=True), "admin code execution", original)
    reject_mutation(data, lambda d: d["parameterization"].update(
                    immutable_security=[]), "RBAC/tenant security configurable", original)
    print("AUDIT B PASS C02-08: public-only routes, 12 denied private paths,"
          " fail-closed states, 8 P0/security mutations rejected.")
    print("LIMIT: no private BOLA/IDOR/MFA endpoint exists in this phase.")


def audit_c():
    original = input_sources()
    data = load(CONTRACT)
    validate_contract(data, original)
    prep = PREP.read_text(encoding="utf-8")
    require("1/37" in prep and "C03 bloqueado" in prep and
            "C02 **NO CERTIFICADO**" in prep,
            "C02 was prematurely certified or C03 opened")
    if ACTA.is_file():
        acta = ACTA.read_text(encoding="utf-8")
        require("8/9" in prep and "C02-08 CERRADA TÉCNICAMENTE" in acta and
                "POSTMERGE" in acta and "C02-09" in acta,
                "Cannot advance C02-08 to 8/9 without formal acta and postmerge")
    else:
        require("7/9" in prep and "C02-08" in prep and
                "EN DESARROLLO" in prep,
                "Unclosed C02-08 must remain 7/9")
    angular = load(ANGULAR)
    opts = angular["projects"]["crecisol-web"]["architect"]["build"]
    require(opts["options"]["browser"] == "src/main.ts" and
            opts["defaultConfiguration"] == "production" and
            opts["configurations"]["pattern-harness"]["browser"] ==
            "tests/pattern-harness/main.ts",
            "Pattern harness replaced production entrypoint")
    public = PUBLIC_TESTS.read_text(encoding="utf-8")
    isolated = PATTERN_TESTS.read_text(encoding="utf-8")
    conf = HARNESS_CONFIG.read_text(encoding="utf-8")
    require(len(re.findall(r"\btest\('", public)) == 11 and
            len(re.findall(r"\btest\('", isolated)) == 5,
            "16 new real Angular browser cases missing")
    require(all(x in public for x in ("page.goto", "PRIVATE_TENANT_B",
            "/consola/resumen", "/mi-cuenta/historial",
            "AxeBuilder", "data-interaction-state", "role=superadmin")),
            "Public browser journeys missing high-risk controls")
    require(all(x in isolated for x in ("AxeBuilder",
            "Conservar borrador", "Descartar y cambiar", "Escape",
            "localStorage", "Tabla sintética de lectura")),
            "Synthetic Angular patterns did not cover state/keyboard/XSS")
    require(all(x in conf for x in ("4201", "pattern-harness",
            "c02-08.journeys-patterns.spec.ts", "test-results-c02-08-harness")),
            "Angular synthetic runner must remain isolated")
    workflow = WORKFLOW.read_text(encoding="utf-8")
    require(all(x in workflow for x in (
            "qa/c02_08_audits.py A", "qa/c02_08_audits.py B",
            "qa/c02_08_audits.py C", "c02-08.journeys-public.spec.ts",
            "playwright.journeys-patterns.config.ts", "npm audit",
            "dotnet build", "ng build --configuration pattern-harness")),
            "Missing C02-08 CI execution gate")
    for path in (SPEC, PREP):
        for link in re.findall(r"\[[^\]]+\]\(([^)]+)\)", path.read_text(encoding="utf-8")):
            if link.startswith(("http:", "https:", "mailto:", "#")):
                continue
            relative = link.split("#", 1)[0]
            target = (path.parent / relative).resolve()
            require(target.is_relative_to(ROOT) and target.is_file(),
                    "Broken Markdown relative link: " + link)
    reject_mutation(data, lambda d: d["tests"].update(expected_total_chromium=0),
                    "browser regression count removed", original)
    reject_mutation(data, lambda d: d["tests"].update(manual_wcag_certified=True),
                    "manual a11y certification invented", original)
    reject_mutation(data, lambda d: d["navigation"].update(no_new_routes=False),
                    "new live routes leaked", original)
    reject_mutation(data, lambda d: d["tests"].update(
                    isolated_config="frontend/playwright.config.ts"),
                    "isolated test runner replaced", original)
    print("AUDIT C PASS C02-08: Angular production vs synthetic isolation,"
          " 11+5 real browser scenarios planned and 4 mutations rejected.")
    print("LIMIT: 58 Chromium and Angular/.NET builds must pass real CI,"
          " not inferred from these source checks.")


def main():
    require(len(sys.argv) == 2 and sys.argv[1] in ("A", "B", "C"),
            "Usage: python3 qa/c02_08_audits.py A|B|C")
    {"A": audit_a, "B": audit_b, "C": audit_c}[sys.argv[1]]()


if __name__ == "__main__":
    try:
        main()
    except (AssertionError, KeyError, IndexError, TypeError, ValueError, OSError) as exc:
        print(f"AUDIT FAILED C02-08: {exc}", file=sys.stderr)
        sys.exit(1)
