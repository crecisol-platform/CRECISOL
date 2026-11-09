#!/usr/bin/env python3
"""C02-07: Word-pinned public/mobile and isolated Angular accessibility gates.

A/B/C static and 18 negative mutations; actual Chromium and axe run in CI.
NO private BOLA/IDOR/MFA or full manual WCAG certification is asserted.
"""
from copy import deepcopy
import json
from pathlib import Path
import re
import sys

import c02_01_source
import c02_04_audits
import c02_06_audits

ROOT = Path(__file__).resolve().parents[1]
DATA = ROOT / "docs/c02/C02_07_MOBILE_ACCESIBILIDAD.json"
SPEC = ROOT / "docs/c02/C02_07_ESPECIFICACION.md"
PLAN = ROOT / "docs/C02_PREPARACION.md"
ACTA = ROOT / "docs/c02/C02_07_ACTA_MICROFASE.md"
SRC = ROOT / "frontend/src/styles.css"
HOME_CSS = ROOT / "frontend/src/app/pages/platform-home.component.css"
HOME_TS = ROOT / "frontend/src/app/pages/platform-home.component.ts"
HOME_HTML = ROOT / "frontend/src/app/pages/platform-home.component.html"
INFO_CSS = ROOT / "frontend/src/app/pages/public-information.component.css"
INFO_TS = ROOT / "frontend/src/app/pages/public-information.component.ts"
INFO_HTML = ROOT / "frontend/src/app/pages/public-information.component.html"
NOTFOUND = ROOT / "frontend/src/app/pages/not-found.component.ts"
HARNESS = ROOT / "frontend/tests/pattern-harness/pattern-harness.component.ts"
PUBLIC_TESTS = ROOT / "frontend/tests/c02-07.mobile-accessibility.spec.ts"
HARNESS_TESTS = ROOT / "frontend/tests/c02-07.patterns-mobile.spec.ts"
HARNESS_RUNNER = ROOT / "frontend/playwright.patterns-a11y.config.ts"
APP_ROUTES = ROOT / "frontend/src/app/app.routes.ts"
ANGULAR = ROOT / "frontend/angular.json"
PACKAGE = ROOT / "frontend/package.json"
WORKFLOW = ROOT / ".github/workflows/c02-07-mobile-accessibility.yml"

SHA_TECH_PREVIOUS = "d54147596984275d98cf7a313a9fdeb8e8b06e22"
PREVIOUS_RUNS = [
    42651313924, 42651314055, 42651314490, 42651314195,
    42651314457, 42651314175, 42651314408, 42651314339, 42651314496
]
SOURCE = {
    "master": "docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx",
    "sha256": c02_01_source.EXPECTED_SHA,
    "chapter": "02",
    "process_gates": "MAE-C02-01..08",
    "operative_microphase": "C02-07",
    "needs": "docs/c02/C02_02_MAPA_NECESIDADES.json",
    "public_sitemap": "docs/c02/C02_03_SITEMAP.json",
    "private_design": "docs/c02/C02_04_NAVEGACION_PRIVADA.json",
    "states": "docs/c02/C02_05_ESTADOS_INTERACCION.json",
    "patterns": "docs/c02/C02_06_PATRONES_FORM_TABLA.json",
    "previous_acta": "docs/c02/C02_06_ACTA_MICROFASE.md",
    "previous_dev_sha": SHA_TECH_PREVIOUS,
    "prior_postmerge_runs": PREVIOUS_RUNS,
}
KINDS = [
    ("UI01", "platform-home", "public-read-only"),
    ("UI02", "public-information", "public-read-only"),
    ("UI03", "public-unavailable", "public-fail-closed"),
    ("UI04", "unknown-private-route", "denied-by-default"),
    ("UI05", "form-synthetic-harness", "test-only"),
    ("UI06", "table-synthetic-harness", "test-only"),
]
BASELINE = {
    "viewport_css_pixels": [320, 360, 768, 1280],
    "text_resize_percent": 200,
    "min_touch_target_css_pixels": 44,
    "wcag_automated_tags": ["wcag2a", "wcag2aa", "wcag21aa", "wcag22aa"],
    "keyboard": ["skip-link-into-main", "visible-focus",
                 "native-modal-cancel-escape", "table-sort"],
    "reading": ["semantic-one-h1-per-route", "one-main-landmark",
                "screen-reader-live-region-on-error",
                "no-essential-mobile-hidden-control"],
    "motion": ["prefers-reduced-motion", "no-continuous-required-animation"],
    "contrast": ["forced-colors-visible-focus", "focus-outline",
                 "no-brand-color-auth"],
    "overflow_policy": "no-document-horizontal-scroll-except-local-table-scroll-region",
    "viewport_and_text_scope": "public-routes-and-synthetic-harness-only-not-future-private-modules",
    "manual_screen_reader_testing_completed": False,
    "physical_device_testing_completed": False,
    "full_wcag_conformance_certified": False,
}
SECURITY = {
    "verified_data": "public-platform-only-read-model-and-synthetic-harness",
    "authorization": "backend-only-no-client-role-grants",
    "scope": "tenant-identity-branch-resource-ownership-must-be-reverified-in-future-backend",
    "routing": "public-four-allowlisted-routes-plus-wildcard-denial",
    "no_private_api": True, "no_login_selector": True,
    "no_business_form": True, "no_real_payment": True,
    "no_database_changes": True, "no_backend_changes": True,
    "no_new_live_routes": True, "no_sensitive_storage": True,
    "stale_data": "clear-brand-and-meta-on-error-no-old-tenant-snapshot",
    "inject": "interpolated-text-no-user-html-evaluation",
    "harness": "separate-4201-build-not-in-production-bundle",
}
PARAMETERIZATION = {
    "status": "future-versioned-admin-panel-not-implemented-in-c02-07",
    "editable_future": [
        "platform-theme", "tenant-theme", "logo", "navigation-copy",
        "footer-copy", "focus-and-contrast-theme-tokens", "typography-tokens",
        "form-copy", "table-copy", "locale", "branch-variant"
    ],
    "scopes": ["platform", "tenant", "branch", "user"],
    "approval": "preview-publish-version-rollback-audit",
    "no_arbitrary_html_js_css": True,
    "security_invariants_immutable": [
        "backend-auth", "tenant-routing", "role-permission",
        "resource-ownership", "PII", "database-DSN"
    ],
    "current_public_brand": "same-origin-versioned-published-platform-api",
    "synthetic_labels": "test-only-never-commercial-runtime-defaults",
}
ARCH = {
    "production_entrypoint": "frontend/src/main.ts",
    "public_route_file": "frontend/src/app/app.routes.ts",
    "production_routes": ["", "experiencias", "demostraciones", "alta-empresas", "**"],
    "frontend_only": [
        "frontend/src/styles.css",
        "frontend/src/app/pages/platform-home.component.ts",
        "frontend/src/app/pages/platform-home.component.css",
        "frontend/src/app/pages/platform-home.component.html",
        "frontend/src/app/pages/public-information.component.ts",
        "frontend/src/app/pages/public-information.component.css",
        "frontend/src/app/pages/public-information.component.html",
        "frontend/src/app/pages/not-found.component.ts",
        "frontend/tests/pattern-harness/pattern-harness.component.ts"
    ],
    "public_tests": "frontend/tests/c02-07.mobile-accessibility.spec.ts",
    "public_new_test_count": 10,
    "harness_tests": "frontend/tests/c02-07.patterns-mobile.spec.ts",
    "harness_new_test_count": 5,
    "harness_runner": "frontend/playwright.patterns-a11y.config.ts",
    "inherited_chromium_tests": 27,
    "total_expected_chromium_tests": 42,
    "no_app_route_changes": True,
    "no_core_brand_service_changes": True,
    "no_external_deps": True,
    "no_deploy": True
}


def require(ok, message):
    if not ok:
        raise AssertionError(message)


def read_data():
    return json.loads(DATA.read_text(encoding="utf-8"))


def parent_contexts():
    # The source gate opens the real, SHA-pinned Word, not a markdown claim.
    c02_01_source.original_parts()
    old = c02_06_audits.read_data()
    parent = c02_06_audits.parent_contexts()
    c02_06_audits.valid_contract(old, parent)
    return old["contexts"]


def valid_contract(data, contexts):
    require(isinstance(data, dict) and data.get("schema_version") == 1 and
            data.get("artifact") == "CRECISOL-C02-07-MOBILE-ACCESSIBILITY-BASELINE" and
            data.get("delivery_state") ==
            "real-public-ui-mobile-and-synthetic-angular-patterns-only-no-private-products",
            "C02-07 contract root/version or scope altered")
    require(data.get("source") == SOURCE,
            "Word SHA, C02-06 technical SHA or nine previous CI runs missing")
    ctx = data.get("contexts")
    require(isinstance(ctx, list) and len(ctx) == 6,
            "All six experiences/N01..18 must remain traceable")
    for index, item in enumerate(ctx):
        require(item.get("experience") == contexts[index]["experience"] and
                item.get("needs") == contexts[index]["needs"] and
                item.get("usage") ==
                "shared-accessibility-principles-no-private-live-workflows",
                "Experience/need mismatch or premature private UX implementation")
    surfaces = data.get("surfaces")
    require(isinstance(surfaces, list) and len(surfaces) == 6 and
            [(s.get("id"), s.get("kind"), s.get("state")) for s in surfaces] == KINDS,
            "Six strictly ordered public/CI-only surfaces required")
    for surface in surfaces:
        checks = surface.get("checks")
        require(isinstance(checks, list) and len(checks) >= 3 and
                all(isinstance(x, str) and len(x) > 9 for x in checks),
                f"Surface {surface.get('id')} lost measurable constraints")
    require(surfaces[0].get("path") == "/" and
            surfaces[1].get("paths") ==
            ["/experiencias", "/demostraciones", "/alta-empresas"],
            "Published route allowlist mutated")
    require(data.get("baseline") == BASELINE, "Viewport, WCAG or manual-scope baseline weakened")
    require(data.get("security") == SECURITY,
            "Private access, tenant, XSS or test-harness boundary weakened")
    require(data.get("parameterization") == PARAMETERIZATION,
            "Future admin-controlled branding or immutable security changed")
    require(data.get("architecture") == ARCH, "Production route/build or test-count boundary altered")
    cases = data.get("negative_cases")
    require(isinstance(cases, list) and len(cases) == 16 and
            [case.get("id") for case in cases] ==
            [f"NEG{i:02d}" for i in range(1, 17)],
            "16 individually traceable negative cases required")
    for case in cases:
        require(isinstance(case.get("attack"), str) and
                len(case["attack"].strip()) >= 36 and
                isinstance(case.get("expected"), str) and
                len(case["expected"].strip()) >= 36,
                f"Negative case lacks specific threat/outcome {case.get('id')}")
    return surfaces


def reject(data, change, name, contexts):
    other = deepcopy(data)
    change(other)
    try:
        valid_contract(other, contexts)
    except (AssertionError, KeyError, ValueError, TypeError, IndexError):
        return
    raise AssertionError(f"Adversarial mutation accepted: {name}")


def audit_a():
    ctx = parent_contexts()
    data = read_data()
    valid_contract(data, ctx)
    c02_04_audits.checked_routes()
    spec = SPEC.read_text(encoding="utf-8")
    for marker in ("C02-07", "6/9", "NO CERTIFICADO", "MAE-C02-01..08",
                   "P01", "P11", "T01-R01", "N01", "N18", "UI01", "UI06",
                   "NEG01", "NEG16", "200 %", "320", "44", "C02-08",
                   "móvil", "accesibilidad", "rollback"):
        require(marker.lower() in spec.lower(), f"Spec loses {marker}")
    reject(data, lambda d: d.update(artifact="OTHER"), "forged artifact", ctx)
    reject(data, lambda d: d["source"].update(sha256="0" * 64),
           "forged original master", ctx)
    reject(data, lambda d: d["source"]["prior_postmerge_runs"].pop(),
           "removed prior successful CI", ctx)
    reject(data, lambda d: d["contexts"][3]["needs"].pop(),
           "forgot one C02-02 user need", ctx)
    reject(data, lambda d: d["surfaces"].pop(),
           "dropped a private-denial or accessible surface", ctx)
    reject(data, lambda d: d["negative_cases"].pop(),
           "lost a negative case", ctx)
    print("AUDIT A PASS C02-07: actual Word SHA, 6 experiences/N01..18,")
    print("  six mobile surfaces, 16 negative scenarios and six source mutations rejected.")


def audit_b():
    ctx = parent_contexts()
    data = read_data()
    valid_contract(data, ctx)
    c02_04_audits.checked_routes()
    styles = SRC.read_text(encoding="utf-8")
    home = HOME_CSS.read_text(encoding="utf-8")
    info = INFO_CSS.read_text(encoding="utf-8")
    html = HOME_HTML.read_text(encoding="utf-8") + INFO_HTML.read_text(encoding="utf-8")
    source = NOTFOUND.read_text(encoding="utf-8")
    harness = HARNESS.read_text(encoding="utf-8")
    require(all('focusMainContent(event: Event)' in p.read_text(encoding="utf-8")
                for p in (HOME_TS, INFO_TS)), "Skip handlers must focus main explicitly")
    require(all('(click)="focusMainContent($event)"' in p.read_text(encoding="utf-8")
                for p in (HOME_HTML, INFO_HTML)), "Both skip links need a real focus action")
    require(all('event.preventDefault()' in p.read_text(encoding="utf-8") and
                "history.replaceState(" in p.read_text(encoding="utf-8")
                for p in (HOME_TS, INFO_TS)),
            "Skip links must not route-reload and lose their focus target")
    for text in ('tabindex="-1"', 'main', 'Saltar al contenido principal'):
        require(text in html, f"Skip link cannot focus real main: {text}")
    require('id="contenido" tabindex="-1"' in html and
            'id="contenido" class="public-container" tabindex="-1"' in html and
            'id="contenido" tabindex="-1"' in source,
            "Non-focusable or invisible main skip target")
    require("44px" in home and "44px" in info and
            "display: inline-flex" in home and "min-width: 44px" in home,
            "Public touch targets not sized as rendered boxes")
    require("overflow-wrap: anywhere" in styles and
            "overflow-wrap: anywhere" in home and
            "overflow-wrap: anywhere" in info,
            "Validated long published platform copy could clip")
    require("forced-colors: active" in styles and "forced-colors: active" in info and
            "forced-colors: active" in home,
            "System focus/forced-color path lost")
    require("prefers-reduced-motion" in styles and
            "showModal()" in harness and
            "this.dirty()" in harness,
            "Motion or dirty navigation guard omitted")
    require("dialog button" in harness and "min-height:44px" in harness and
            "max-width:100%" in harness,
            "CI-only modal hidden or too small for touch")
    for code in (styles, home, info, html, harness):
        require("overflow-x:hidden" not in code and
                "overflow-x: hidden" not in code,
                "Do not conceal essential content by clipping viewport")
    for code in (html, harness):
        for unsafe in ("innerHTML", "bypassSecurityTrust", "document.cookie",
                       "localStorage", "sessionStorage", "HttpClient", "fetch("):
            require(unsafe not in code, f"Unsafe UI execution/scope token {unsafe}")
    # Verify negative contract changes are actually rejected, not only described.
    reject(data, lambda d: d["security"].update(authorization="frontend-allow"),
           "frontend grants backend privilege", ctx)
    reject(data, lambda d: d["security"].update(no_new_live_routes=False),
           "unapproved mobile private routes", ctx)
    reject(data, lambda d: d["security"].update(stale_data="reuse-old-brand"),
           "stale data in failed publication", ctx)
    reject(data, lambda d: d["security"].update(inject="allow-live-HTML"),
           "unescaped published payload", ctx)
    reject(data, lambda d: d["security"].update(no_sensitive_storage=False),
           "private local storage", ctx)
    reject(data, lambda d: d["security"].update(harness="live-prod-harness"),
           "harness exposed to public", ctx)
    reject(data, lambda d: d["parameterization"].update(
        no_arbitrary_html_js_css=False),
           "executable admin-defined code", ctx)
    reject(data, lambda d: d["parameterization"].update(
        security_invariants_immutable=[]),
           "tenant and resource permissions editable", ctx)
    print("AUDIT B PASS C02-07: fail-closed public/mobile UI, stable focus,")
    print("  no private auth or arbitrary HTML, eight P0 mutations rejected.")
    print("LIMIT: no private backend BOLA/IDOR/MFA endpoints exist in this phase.")


def audit_c():
    ctx = parent_contexts()
    data = read_data()
    valid_contract(data, ctx)
    c02_04_audits.checked_routes()
    plan = PLAN.read_text(encoding="utf-8")
    require("NO CERTIFICADO" in plan and "1/37" in plan and
            "C03 bloqueado" in plan,
            "Chapter 02 must remain uncertified; chapter 03 must remain blocked")
    if ACTA.exists():
        acta = ACTA.read_text(encoding="utf-8")
        require("7/9" in plan and
                "C02-07 CERRADA TÉCNICAMENTE" in acta and
                "POSTMERGE" in acta and "C02-08" in acta,
                "Cannot advance to 7/9 without formal C02-07 acta")
    else:
        require("6/9" in plan, "C02-07 unclosed must remain 6/9")
    angular = json.loads(ANGULAR.read_text(encoding="utf-8"))
    build = angular["projects"]["crecisol-web"]["architect"]["build"]
    require(build["defaultConfiguration"] == "production" and
            build["options"]["browser"] == "src/main.ts" and
            build["configurations"]["pattern-harness"]["browser"] ==
            "tests/pattern-harness/main.ts",
            "Mobile audit cannot replace production bootstrap with fixture harness")
    package = json.loads(PACKAGE.read_text(encoding="utf-8"))
    require(package["dependencies"]["@angular/core"] == "22.2.4" and
            package["devDependencies"]["@playwright/test"] == "1.56.1" and
            package["devDependencies"]["@axe-core/playwright"] == "4.10.2",
            "Pinned UI runtime, browser and a11y test versions changed")
    public = PUBLIC_TESTS.read_text(encoding="utf-8")
    harness = HARNESS_TESTS.read_text(encoding="utf-8")
    runner = HARNESS_RUNNER.read_text(encoding="utf-8")
    require(public.count("test('") == 10 and
            harness.count("test('") == 5,
            "15 actual C02-07 browser cases missing")
    for text in ("320, 360, 768, 1280", "200%", "touchTargets", "noDocumentOverflow",
                 "AxeBuilder", "forcedColors", "reducedMotion", "PRIVATE_TENANT_B"):
        require(text in public, f"Public a11y test omitted: {text}")
    for text in ("AxeBuilder", "320, 360, 768, 1280", "200%", "Conservar borrador",
                 "Escape", "noDocumentOverflow", "forcedColors"):
        require(text in harness, f"Real Angular harness missing: {text}")
    require("At least one relevant control must exist" in harness and
            "cs-draft-form-pattern form button:not([disabled])" in harness and
            "cs-pattern-harness dialog button" in harness,
            "Touch test must target real visible controls and reject empty selectors")
    require("test-results-c02-07-harness" in runner and
            "pattern-harness" in runner and "4201" in runner and
            "c02-07.patterns-mobile.spec.ts" in runner,
            "Harness runner cannot be production or a mocked HTML page")
    for p in [SRC, HOME_CSS, INFO_CSS]:
        require(p.read_text(encoding="utf-8").count("min-width: 320px") <= 1,
                "Mobile min-width cannot force horizontal overflow")
    wf = WORKFLOW.read_text(encoding="utf-8")
    for text in ("qa/c02_07_audits.py A", "qa/c02_07_audits.py B",
                 "qa/c02_07_audits.py C", "c02-07.mobile-accessibility.spec.ts",
                 "playwright.patterns-a11y.config.ts", "dotnet build", "npm audit",
                 "check:types", "build --configuration pattern-harness"):
        require(text in wf, f"CI missing relevant gate: {text}")
    for p in (SPEC, PLAN):
        for link in re.findall(r"\[[^\]]+\]\(([^)]+)\)", p.read_text(encoding="utf-8")):
            if link.startswith(("http:", "https:", "mailto:", "#")):
                continue
            target = (p.parent / link.split("#", 1)[0]).resolve()
            require(target.is_relative_to(ROOT.resolve()) and target.is_file(),
                    f"Broken local Markdown link: {link}")
    reject(data, lambda d: d["baseline"].update(
        min_touch_target_css_pixels=24), "smaller tapping controls", ctx)
    reject(data, lambda d: d["baseline"].update(
        full_wcag_conformance_certified=True), "fabricated manual certification", ctx)
    reject(data, lambda d: d["architecture"].update(
        total_expected_chromium_tests=0), "browser tests removed", ctx)
    reject(data, lambda d: d["architecture"].update(
        no_app_route_changes=False), "unauthorized route addition", ctx)
    print("AUDIT C PASS C02-07: reflow/focus/zoom/contrast, separate Angular")
    print("  harness, 15 new browser scenarios and four architecture mutations rejected.")
    print("LIMIT: full WCAG manual/physical devices and private backend aren't tested.")


def main():
    require(len(sys.argv) == 2 and sys.argv[1] in ("A", "B", "C"),
            "Usage: python3 qa/c02_07_audits.py A|B|C")
    {"A": audit_a, "B": audit_b, "C": audit_c}[sys.argv[1]]()


if __name__ == "__main__":
    try:
        main()
    except (AssertionError, KeyError, TypeError, ValueError, IndexError, OSError) as exc:
        print(f"FAIL C02-07: {exc}", file=sys.stderr)
        sys.exit(1)
