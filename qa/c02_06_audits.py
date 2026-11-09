#!/usr/bin/env python3
"""C02-06: Word-pinned real form/table UX patterns and CI-only Angular harness.

A/B/C are deterministic read-only contract audits plus adversarial mutations.
Real Chromium is executed separately in GitHub Actions, including Angular
test-harness components. No missing private endpoint is falsely certified.
"""
from copy import deepcopy
import json
from pathlib import Path
import re
import sys

import c02_01_source
import c02_02_audits
import c02_03_audits
import c02_04_audits
import c02_05_audits

ROOT = Path(__file__).resolve().parents[1]
DATA = ROOT / "docs/c02/C02_06_PATRONES_FORM_TABLA.json"
SPEC = ROOT / "docs/c02/C02_06_ESPECIFICACION.md"
PLAN = ROOT / "docs/C02_PREPARACION.md"
ACTA = ROOT / "docs/c02/C02_06_ACTA_MICROFASE.md"
CONTRACT = ROOT / "frontend/src/app/ui/patterns/pattern-contract.ts"
FORM = ROOT / "frontend/src/app/ui/patterns/draft-form-pattern.component.ts"
FORM_HTML = ROOT / "frontend/src/app/ui/patterns/draft-form-pattern.component.html"
FORM_CSS = ROOT / "frontend/src/app/ui/patterns/draft-form-pattern.component.css"
TABLE = ROOT / "frontend/src/app/ui/patterns/table-pattern.component.ts"
TABLE_HTML = ROOT / "frontend/src/app/ui/patterns/table-pattern.component.html"
TABLE_CSS = ROOT / "frontend/src/app/ui/patterns/table-pattern.component.css"
HARNESS = ROOT / "frontend/tests/pattern-harness/pattern-harness.component.ts"
HARNESS_MAIN = ROOT / "frontend/tests/pattern-harness/main.ts"
HARNESS_INDEX = ROOT / "frontend/tests/pattern-harness/index.html"
ANGULAR = ROOT / "frontend/angular.json"
PLAYWRIGHT = ROOT / "frontend/playwright.patterns.config.ts"
PATTERN_TESTS = ROOT / "frontend/tests/c02-06.form-table.spec.ts"
WF = ROOT / ".github/workflows/c02-06-form-table-patterns.yml"

PATTERNS = {
    "FP01": ("local-draft", "frontend/src/app/ui/patterns/draft-form-pattern.component.ts",
             {"native-labelled-input", "required-and-length-validation", "allowlisted-rule",
              "inline-error-alert", "local-only-format-check", "no-server-submit"}),
    "FP02": ("unsaved-draft", "frontend/src/app/ui/patterns/draft-form-pattern.component.ts",
             {"native-modal-discard-confirmation", "cancel-and-escape",
              "beforeunload-hint", "clear-on-scope-change", "explicit-unverified-copy"}),
    "TP01": ("read-only-table", "frontend/src/app/ui/patterns/table-pattern.component.ts",
             {"caption", "semantic-th-td", "accessible-sort", "aria-sort",
              "client-only-filter", "keyboard", "horizontal-scroll"}),
    "TP02": ("scoped-table", "frontend/src/app/ui/patterns/table-pattern.component.ts",
             {"clears-filter-and-sort-on-scope-change", "fresh-host-supplied-rows",
              "non-authoritative-local-empty", "no-export-or-edit", "row-value-interpolation"})
}
SOURCE_EXPECTED = {
    "master": "docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx",
    "sha256": c02_01_source.EXPECTED_SHA,
    "chapter": "02",
    "process_gates": "MAE-C02-01..08",
    "operative_microphase": "C02-06",
    "needs": "docs/c02/C02_02_MAPA_NECESIDADES.json",
    "sitemap": "docs/c02/C02_03_SITEMAP.json",
    "private_design": "docs/c02/C02_04_NAVEGACION_PRIVADA.json",
    "interaction_states": "docs/c02/C02_05_ESTADOS_INTERACCION.json",
    "previous_acta": "docs/c02/C02_05_ACTA_MICROFASE.md",
    "previous_dev_sha": "cf751709f4cd5dac5e579dccc50c060bf20ae396",
}
BACKEND_GATES = {
    "authorization": "backend-deny-by-default",
    "identity": "verified-session-and-revocation",
    "tenancy": "host-verified-and-server-resolved-membership",
    "object_security": "tenant-branch-action-assignment-and-resource-ownership",
    "superadmin": "step-up-MFA-reason-time-window-audit-for-PII",
    "storage": "catalog-server-only-shared-or-dedicated",
    "output": "server-validated-DTO-no-private-browser-trust",
    "mutation": "CSRF-or-equivalent-protection-plus-versioned-authorization-transaction",
    "validation": "frontend-UX-only-backend-revalidates-before-write",
    "concurrency": "optimistic-version-or-explicit-conflict-policy-server-side",
    "changes": "unsaved-route-exit-confirmation-before-verified-scope-change",
    "log": "tenant-scope-no-raw-field-values-or-PII",
    "zero_tenant_selector": True,
    "no_live_private_route": True,
    "no_real_payment": True,
    "no_registration": True,
}
CONFIG_EXPECTED = {
    "state": "future-published-admin-managed-versioned",
    "scopes": ["platform", "tenant", "branch", "user"],
    "editable": ["field_label", "field_hint", "required", "max_length",
                 "rule_token", "table_caption", "table_column_label",
                 "table_column_order", "empty_copy", "locale"],
    "safe_fields": "bounded-allowlisted-text-and-tokens-no-arbitrary-HTML-JS-CSS-or-regex",
    "security_immutable": ["tenant-context", "backend-policy", "resource-ownership",
                           "database-routing", "authorization", "confirmation-proof"],
    "approval": "preview-approval-publish-audit-rollback",
    "runtime_admin_configuration_implemented": False,
    "examples": "CI-test-only-synthetic-literals-not-production-business-settings"
}
ISOLATION = {
    "production_entrypoint": "src/main.ts",
    "production_build": "crecisol-web:build:production",
    "production_routes": ["", "experiencias", "demostraciones", "alta-empresas", "**"],
    "harness_entrypoint": "tests/pattern-harness/main.ts",
    "harness_build": "crecisol-web:build:pattern-harness",
    "harness_served_port": 4201,
    "harness_only_tests": ["frontend/tests/c02-06.form-table.spec.ts"],
    "no_new_production_routes": True,
    "no_backend_changes": True,
    "no_database_changes": True,
    "no_harness_in_production_bundle": True
}


def require(condition, message):
    if not condition:
        raise AssertionError(message)


def parent_contexts():
    # Source gate reads the actual .docx bytes and extracts the chapter.
    c02_01_source.original_parts()
    previous = c02_05_audits.read_data()
    parents = c02_05_audits.parent_contracts()
    c02_05_audits.valid_contract(previous, parents)
    return previous["contexts"]


def read_data():
    return json.loads(DATA.read_text(encoding="utf-8"))


def valid_contract(data, parent):
    require(isinstance(data, dict) and data.get("schema_version") == 1,
            "Invalid C02-06 contract root")
    require(data.get("artifact") == "CRECISOL-C02-06-FORM-TABLE-UX-CONTRACT" and
            data.get("delivery_state") ==
            "reusable-angular-patterns-isolated-test-harness-no-live-business-form",
            "C02-06 must not claim a production/tenant form")
    require(data.get("source") == SOURCE_EXPECTED,
            "Canonical Word/C02-05 SHA, microphase or source omitted")
    contexts = data.get("contexts")
    require(isinstance(contexts, list) and len(contexts) == 6,
            "Six source experiences required")
    for i, ctx in enumerate(contexts):
        require(ctx.get("experience") == parent[i]["id"] and
                ctx.get("needs") == parent[i]["needs"] and
                ctx.get("usage") == "future-authorized-binding-only",
                "Wrong E01..06/N01..18, or elevated live context")
    patterns = data.get("patterns")
    require(isinstance(patterns, list) and len(patterns) == 4 and
            [p.get("id") for p in patterns] == list(PATTERNS),
            "Missing/duplicated four ordered form/table patterns")
    for p in patterns:
        kind, component, features = PATTERNS[p["id"]]
        require(p.get("kind") == kind and p.get("component") == component and
                isinstance(p.get("features"), list) and
                set(p["features"]) == features and len(p["features"]) == len(features),
                f"Pattern {p['id']} lost accessibility, UX or security features")
        require(p.get("no_business_operation") is True and
                p.get("live_public") is False and
                p.get("standalone_test_only") is True,
                "Pattern exposed as live business form/route")
    require(data.get("future_backend_gate") == BACKEND_GATES,
            "Future backend permission, ownership or validation boundary weakened")
    require(data.get("configuration") == CONFIG_EXPECTED,
            "Dynamic/admin copy or immutable security policy changed")
    require(data.get("build_isolation") == ISOLATION,
            "CI harness leaked into the production build or routes")
    cases = data.get("negative_cases")
    require(isinstance(cases, list) and len(cases) == 14 and
            [x.get("id") for x in cases] ==
            [f"NEG{i:02d}" for i in range(1, 15)],
            "Fourteen negative threat cases are required")
    for case in cases:
        for field in ("attack", "expected"):
            require(isinstance(case.get(field), str) and
                    len(case[field].strip()) > 23,
                    f"Missing detailed {case.get('id')} {field} scenario")
    return patterns


def reject_mutation(data, mutate, label, parent):
    other = deepcopy(data)
    mutate(other)
    try:
        valid_contract(other, parent)
    except (AssertionError, KeyError, TypeError, ValueError, IndexError):
        return
    raise AssertionError(f"Adversarial mutation wrongly accepted: {label}")


def audit_a():
    parents = parent_contexts()
    data = read_data()
    valid_contract(data, parents)
    c02_04_audits.checked_routes()
    doc = SPEC.read_text(encoding="utf-8")
    for word in ("C02-06", "5/9", "NO CERTIFICADO", "MAE-C02-01..08",
                 "N01", "N18", "FP01", "FP02", "TP01", "TP02", "C02-07",
                 "T01-R01", "P01", "P11", "NEG01", "NEG14",
                 "formularios", "tablas", "rollback"):
        require(word.lower() in doc.lower(), f"Missing source/phase marker: {word}")
    reject_mutation(data, lambda d: d["patterns"].pop(), "missing pattern", parents)
    reject_mutation(data, lambda d: d["patterns"][1].update(id="FP01"),
                    "duplicate patterns", parents)
    reject_mutation(data, lambda d: d["source"].update(sha256="0" * 64),
                    "forged Word SHA", parents)
    reject_mutation(data, lambda d: d["contexts"][2]["needs"].pop(),
                    "missing C02-02 need", parents)
    reject_mutation(data, lambda d: d["patterns"][0]["features"].remove("no-server-submit"),
                    "missing form no-submit invariant", parents)
    reject_mutation(data, lambda d: d["negative_cases"].pop(),
                    "lost adversarial scenario", parents)
    print("AUDIT A PASS C02-06: Word SHA + six personas/N01..18, four patterns,")
    print("  fourteen negative cases and six corrupted contract mutations rejected.")


def audit_b():
    parents = parent_contexts()
    data = read_data()
    valid_contract(data, parents)
    c02_04_audits.checked_routes()
    source = CONTRACT.read_text(encoding="utf-8")
    form = FORM.read_text(encoding="utf-8")
    form_html = FORM_HTML.read_text(encoding="utf-8")
    table = TABLE.read_text(encoding="utf-8")
    table_html = TABLE_HTML.read_text(encoding="utf-8")
    harness = HARNESS.read_text(encoding="utf-8")

    for marker in ("safeDisplayText", "safeKey", "validDraftConfig",
                   "validColumns", "draftIssue", "Number.isInteger",
                   "plain-text", "reference", "maxLength"):
        require(marker in source, f"Allowlisted field/column contract lacks {marker}")
    for marker in ("preventDefault()", "beforeunload", "removeEventListener",
                   "this.dirty()", "this.dirtyChange.emit", "ngOnChanges",
                   "this.draft.set('')", "askDiscard", "discard()"):
        require(marker in form, f"Draft safety action missing: {marker}")
    for marker in ("<form novalidate", "aria-describedby", "aria-invalid",
                   "role=\"alert\"", "role=\"status\"", "<dialog", "showModal"):
        if marker == "showModal":
            require(marker in form, "Modal confirmation must be native")
        else:
            require(marker in form_html, f"Accessible form safety missing: {marker}")
    for marker in ("[scopeKey]", "readonly", "this.rows()", "this.filter.set('')",
                   "this.sortKey.set(null)", "this.descending.set(false)"):
        if marker == "[scopeKey]":
            require("[scopeKey]" in harness, "Harness must bind verified-scope reset")
        else:
            require(marker in table, f"Table scope/readonly missing: {marker}")
    for marker in ("<table>", "<caption>", "scope=\"col\"", "aria-sort",
                   "row[column.key]", "role=\"status\""):
        require(marker in table_html, f"Table semantic/security element missing: {marker}")
    for marker in ("requestChange", "this.dirty()", "this.changeDialog().nativeElement.showModal()",
                   "cancelChange", "confirmChange", "this.dirty.set(false)"):
        require(marker in harness, f"Host must confirm dirty navigation before context change: {marker}")
    forbidden = ("innerHTML", "bypassSecurityTrust", "localStorage", "sessionStorage",
                 "HttpClient", "fetch(", "eval(", "new Function(", "document.cookie",
                 "X-Tenant-Id", "Authorization:", "router.navigate", "payment")
    code = source + form + form_html + table + table_html
    for token in forbidden:
        require(token not in code, f"Unsafe token/privileged runtime in components: {token}")
    require("CSRF-or-equivalent" in json.dumps(data) and
            "MFA" in json.dumps(data), "Backend, exceptional PII and writes not gated")
    reject_mutation(data, lambda d: d["future_backend_gate"].update(
        authorization="frontend-only"), "frontend grants access", parents)
    reject_mutation(data, lambda d: d["future_backend_gate"].update(
        tenancy="tenant_id-from-url"), "user chooses tenant", parents)
    reject_mutation(data, lambda d: d["future_backend_gate"].update(
        superadmin="global-PII-open"), "SuperAdmin no MFA", parents)
    reject_mutation(data, lambda d: d["configuration"].update(
        safe_fields="allow-HTML-or-user-regex"), "executable configuration", parents)
    reject_mutation(data, lambda d: d["patterns"][0].update(
        live_public=True), "premature live submit", parents)
    reject_mutation(data, lambda d: d["build_isolation"].update(
        no_harness_in_production_bundle=False), "test-only page leaked", parents)
    reject_mutation(data, lambda d: d["future_backend_gate"].update(
        mutation="client-side-save-only"), "unverified backend write", parents)
    reject_mutation(data, lambda d: d["configuration"].update(
        security_immutable=[]), "tenant trust user-editable", parents)
    print("AUDIT B PASS C02-06: local-only typed form, native discard,")
    print("  scoped table, safe data/config text and eight P0 security mutations rejected.")
    print("LIMIT: no live RBAC/BOLA/IDOR/MFA, payments or database writes were tested.")


def audit_c():
    parents = parent_contexts()
    data = read_data()
    valid_contract(data, parents)
    c02_04_audits.checked_routes()
    plan = PLAN.read_text(encoding="utf-8")
    if ACTA.is_file():
        acta = ACTA.read_text(encoding="utf-8")
        require("6/9" in plan and
                "C02-06 CERRADA TÉCNICAMENTE" in acta and
                "POSTMERGE" in acta and "C02-07" in acta and
                "d82cfc0a57561f21266a1f3e2e8c328dd3c4aeb4" in acta and
                all(run in acta for run in (
                    "42649577691", "42649577850", "42649577851",
                    "42649577513", "42649577478", "42649577726",
                    "42649577757", "42649577427")),
                "6/9 needs exact C02-06 SHA, eight CI postmerge and a formal acta")
    else:
        require("5/9" in plan and "C02-06" in plan,
                "Cannot advance without audited C02-06 acta")
    require("C02 **NO CERTIFICADO**" in plan and "1/37" in plan and
            "C03 bloqueado" in plan, "Chapter sequence advanced without certification")

    angular = json.loads(ANGULAR.read_text(encoding="utf-8"))
    project = angular["projects"]["crecisol-web"]["architect"]
    build = project["build"]
    serve = project["serve"]
    require(build["options"]["browser"] == "src/main.ts" and
            build["options"]["index"] == "src/index.html" and
            build["defaultConfiguration"] == "production" and
            serve["defaultConfiguration"] == "development" and
            "pattern-harness" in build["configurations"] and
            build["configurations"]["pattern-harness"]["browser"] ==
            "tests/pattern-harness/main.ts" and
            build["configurations"]["pattern-harness"]["index"] ==
            "tests/pattern-harness/index.html" and
            build["configurations"]["pattern-harness"]["outputPath"] ==
            "dist/crecisol-pattern-harness" and
            build["configurations"]["pattern-harness"]["assets"] == [] and
            serve["configurations"]["pattern-harness"]["buildTarget"] ==
            "crecisol-web:build:pattern-harness",
            "Test harness cannot replace or leak into the production entrypoint")
    for p in (ROOT / "frontend/src/main.ts",
              ROOT / "frontend/src/app/app.routes.ts"):
        code = p.read_text(encoding="utf-8")
        require("pattern-harness" not in code and
                "PatternHarness" not in code and
                "DraftFormPattern" not in code and
                "TablePattern" not in code,
                "Production entrypoint/routes must never import synthetic harness")
    require("bootstrapApplication(PatternHarnessComponent)" in
            HARNESS_MAIN.read_text(encoding="utf-8") and
            "cs-pattern-harness" in HARNESS_INDEX.read_text(encoding="utf-8"),
            "Harness cannot be a mocked HTML instead of live Angular components")
    require("DraftFormPatternComponent" in HARNESS.read_text(encoding="utf-8") and
            "TablePatternComponent" in HARNESS.read_text(encoding="utf-8"),
            "CI must mount the real Angular standalone components")
    require("pattern-harness" in PLAYWRIGHT.read_text(encoding="utf-8") and
            "4201" in PLAYWRIGHT.read_text(encoding="utf-8") and
            "test-results-patterns" in PLAYWRIGHT.read_text(encoding="utf-8"),
            "Real Chromium harness runner not configured")
    for css in (FORM_CSS, TABLE_CSS):
        value = css.read_text(encoding="utf-8")
        require("prefers-reduced-motion" in value and
                "focus-visible" in value and
                "min-width:0" in value and
                "min-height:4" in value,
                "Keyboard, mobile, reduced-motion or touch sizes missing")
    tests = PATTERN_TESTS.read_text(encoding="utf-8")
    for text in ("isolated preview renders", "local validation requires",
                 "HTML injection", "discard is never silent",
                 "table filtering and keyboard", "scope changes clear",
                 "synthetic table content", "320px layout",
                 "AxeBuilder", "Conservar borrador", "Descartar y cambiar"):
        require(text in tests, f"Missing real Chromium UX/security test {text}")
    w = WF.read_text(encoding="utf-8")
    for name in ("qa/c02_06_audits.py A", "qa/c02_06_audits.py B",
                 "qa/c02_06_audits.py C", "playwright.patterns.config.ts",
                 "test:c02-06", "Angular22", "dotnet build", "npm audit"):
        require(name in w, f"Workflow missing test/architecture gate: {name}")
    for p in (SPEC, PLAN):
        for link in re.findall(r"\[[^\]]+\]\(([^)]+)\)", p.read_text(encoding="utf-8")):
            if link.startswith(("https:", "http:", "mailto:", "#")):
                continue
            target = (p.parent / link.split("#", 1)[0]).resolve()
            require(target.is_relative_to(ROOT.resolve()) and target.is_file(),
                    f"Broken reference {p}: {link}")
    reject_mutation(data, lambda d: d["build_isolation"].update(
        production_entrypoint="tests/pattern-harness/main.ts"),
        "harness promoted to production", parents)
    reject_mutation(data, lambda d: d["configuration"].update(
        runtime_admin_configuration_implemented=True),
        "fake live configuration runtime", parents)
    reject_mutation(data, lambda d: d["build_isolation"].update(
        no_new_production_routes=False),
        "new public or private URL", parents)
    reject_mutation(data, lambda d: d["configuration"]["editable"].pop(),
        "lost admin-configurable copy", parents)
    print("AUDIT C PASS C02-06: real isolated Angular test runtime, production")
    print("  route/build independence, keyboard/320px/WCAG gates and four mutations rejected.")
    print("LIMIT: Angular/.NET builds and 27 Chromium tests execute separately in CI.")


def main():
    require(len(sys.argv) == 2 and sys.argv[1] in ("A", "B", "C"),
            "Usage: python3 qa/c02_06_audits.py A|B|C")
    {"A": audit_a, "B": audit_b, "C": audit_c}[sys.argv[1]]()


if __name__ == "__main__":
    try:
        main()
    except (AssertionError, KeyError, ValueError, TypeError, IndexError, OSError) as exc:
        print(f"FAIL C02-06: {exc}", file=sys.stderr)
        sys.exit(1)
