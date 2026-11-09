#!/usr/bin/env python3
"""C02-05: pinned canonical UX-state contracts and independently audited UI.

This proves public browser presentation gates and fail-closed source contracts.
It DOES NOT prove missing private-API RBAC/BOLA, MFA, tenant writes or payments.
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

ROOT = Path(__file__).resolve().parents[1]
DATA = ROOT / "docs/c02/C02_05_ESTADOS_INTERACCION.json"
SPEC = ROOT / "docs/c02/C02_05_ESPECIFICACION.md"
ACTA = ROOT / "docs/c02/C02_05_ACTA_MICROFASE.md"
PLAN = ROOT / "docs/C02_PREPARACION.md"
ROUTES = ROOT / "frontend/src/app/app.routes.ts"
SOURCE = ROOT / "frontend/src/app/branding/public-brand.service.ts"
COMPONENT = ROOT / "frontend/src/app/ui/interaction-state.component.ts"
TEMPLATE = ROOT / "frontend/src/app/ui/interaction-state.component.html"
STYLE = ROOT / "frontend/src/app/ui/interaction-state.component.css"
MODEL = ROOT / "frontend/src/app/ui/interaction-state.model.ts"
HOST = ROOT / "frontend/src/app/pages/public-information.component.ts"
HOST_HTML = ROOT / "frontend/src/app/pages/public-information.component.html"
TESTS = ROOT / "frontend/tests/c02-05.interaction-states.spec.ts"
KIND_ORDER = ["loading", "empty", "error", "offline", "confirmation"]
RUNTIME_STATES = ["loading", "error", "offline"]
FUTURE_STATES = ["empty", "confirmation"]
EXPECTED_TRANSITIONS = {
    "request_start": "clear-previous-view-and-identity-metadata-then-loading",
    "authorized_success_nonempty": "render-scoped-ready-using-only-fresh-published-or-authorized-response",
    "authorized_success_empty": "empty-after-success-not-after-403",
    "response_error": "error-and-clear-current-context-data",
    "browser_offline_hint": "offline-display-only-after-request-unavailable-never-bypass-security",
    "online_hint": "remain-denied-until-fresh-request-succeeds",
    "confirmed_write": "confirmation-only-after-backend-acknowledgement-and-versioned-consent",
    "context_switch": "invalidate-private-content-and-all-previous-scope-projections",
    "retry": "explicit-single-inflight-idempotent-read-with-fresh-validation",
}


def require(ok, message):
    if not ok:
        raise AssertionError(message)


def read_data():
    return json.loads(DATA.read_text(encoding="utf-8"))


def parent_contracts():
    # Real Word-byte and chapter extraction, not a summary of the Word.
    c02_01_source.original_parts()
    old = c02_02_audits.read_data()
    c02_02_audits.valid_contract(old)
    c02_03_audits.validate(c02_03_audits.sitemap())
    c02_04_audits.valid_contract(c02_04_audits.read_data())
    return {e["id"]: e for e in old["experiences"]}


def valid_contract(data, parents):
    require(isinstance(data, dict), "State contract must be an object")
    require(data.get("artifact") == "CRECISOL-C02-05-INTERACTION-STATE-CONTRACT",
            "Wrong state artifact")
    require(data.get("schema_version") == 1, "Unsupported state contract schema")
    require(data.get("delivery_state") ==
            "shared-frontend-pattern-public-only-private-future-design",
            "State pattern must not claim operational private modules")
    source = data.get("source", {})
    expected = {
        "master": "docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx",
        "sha256": c02_01_source.EXPECTED_SHA,
        "chapter": "02",
        "process_gates": "MAE-C02-01..08",
        "microphase": "C02-05",
        "needs": "docs/c02/C02_02_MAPA_NECESIDADES.json",
        "public_sitemap": "docs/c02/C02_03_SITEMAP.json",
        "private_design": "docs/c02/C02_04_NAVEGACION_PRIVADA.json",
        "previous_acta": "docs/c02/C02_04_ACTA_MICROFASE.md",
        "previous_dev_sha": "a57dcb2ae7fb533f10beae8afb59656b301318d3",
    }
    require(all(source.get(k) == v for k, v in expected.items()),
            "Unpinned Word/previous-phase source, chapter or process gates")

    states = data.get("states")
    require(isinstance(states, list) and len(states) == 5,
            "Five universal interaction states required")
    require([s.get("id") for s in states] ==
            [f"ST{i:02d}" for i in range(1, 6)], "Five state IDs must be unique")
    require([s.get("kind") for s in states] == KIND_ORDER,
            "Missing, duplicate or unauthorized state")
    for item in states:
        kind = item["kind"]
        require(item.get("aria_role") == ("alert" if kind in ("error", "offline") else "status"),
                f"ARIA state announcement incorrect: {kind}")
        require(item.get("aria_busy") is (kind == "loading"),
                f"ARIA busy state incorrect: {kind}")
        require(isinstance(item.get("requires"), str) and
                len(item["requires"]) > 22 and
                isinstance(item.get("recovery"), str) and
                len(item["recovery"]) > 16,
                f"State trigger and recovery missing: {kind}")
        require(isinstance(item.get("allows"), list) and item["allows"] and
                isinstance(item.get("forbids"), list) and len(item["forbids"]) >= 3,
                f"Negative/positive state conditions absent: {kind}")
        require(item.get("live") == (
            "future-only" if kind in FUTURE_STATES else
            "public-identity-connectivity-advisory" if kind == "offline" else
            "public-identity"
        ), "Future state falsely marked live")

    contexts = data.get("contexts")
    require(isinstance(contexts, list) and len(contexts) == 6,
            "All six canonical experiences must have a state scope")
    require([c.get("id") for c in contexts] ==
            [f"E{i:02d}" for i in range(1, 7)], "Experience duplicated/missing")
    refs = []
    for context in contexts:
        exp = context["id"]
        need_ids = [n["id"] for n in parents[exp]["needs"]]
        require(context.get("needs") == need_ids,
                f"Missing exact N01-N18 source mapping for {exp}")
        refs.extend(need_ids)
        require(isinstance(context.get("state_usage"), str) and
                len(context["state_usage"]) >= 18,
                f"State usage needs future-auth boundary in {exp}")
    require(refs == [f"N{i:02d}" for i in range(1, 19)],
            "One or more needs from C02-02 untraced")

    require(data.get("transitions") == EXPECTED_TRANSITIONS,
            "Unsafe or altered interaction transitions")
    security = data.get("security", {})
    for field, value in {
        "authorization": "backend-deny-by-default",
        "login": "automatic-verified-context-no-tenant-picker",
        "cross_tenant": "fail-closed-no-stale-projection-and-no-private-cache",
        "branch_resource": "backend-validates-membership-action-branch-owner-and-revocation",
        "superadmin": "MFA-step-up-reason-expiry-audit-before-private-PII",
        "shared_dedicated": "backend-catalog-only-never-browser-DSN",
        "client_escapes": "Angular-interpolation-only-no-innerHTML-bypass",
        "offline_hint": "navigator.onLine-not-authorization",
        "confirmation": "never-before-authorized-backend-ack",
        "no_client_perms": True,
        "no_demo_real_data": True,
        "no_real_charge": True,
        "no_private_routes": True,
    }.items():
        require(security.get(field) == value,
                f"Security state invariant downgraded: {field}")

    config = data.get("parameterization", {})
    require(config.get("future_scope") == ["platform", "tenant", "branch", "user"],
            "State copy must be future-admin configurable by safe scope")
    require(config.get("versioned_publication") ==
            "preview-approve-publish-audit-rollback", "Missing versioned publication")
    require(config.get("editable_system_copy") ==
            ["eyebrow", "title", "description", "action_label", "locale"],
            "Unapproved/fixed UX copy configuration")
    require(config.get("safe_content") ==
            "bounded-validated-text-no-HTML-CSS-JS-or-code",
            "Unsafe executable customization proposed")
    require(config.get("allowed_tokens") == KIND_ORDER,
            "Unknown interaction state token allowed")
    require(config.get("immutable") == [
        "authorization", "tenant-context", "resource-ownership",
        "server-proof-of-confirmation", "route-policy"],
        "User-supplied copy cannot grant access or fake confirmation")
    require(config.get("configuration_runtime_implemented") is False,
            "No live configuration/admin system is delivered here")

    runtime = data.get("runtime", {})
    for k, v in {
        "shared_component": "frontend/src/app/ui/interaction-state.component.ts",
        "shared_model": "frontend/src/app/ui/interaction-state.model.ts",
        "integrated_public_page": "frontend/src/app/pages/public-information.component.html",
        "backend_changes": [],
        "database_changes": [],
        "private_routes": [],
        "live_states": RUNTIME_STATES,
        "defined_not_live": FUTURE_STATES,
        "no_emulated_private_session": True
    }.items():
        require(runtime.get(k) == v, f"Unexpected execution scope: {k}")

    cases = data.get("negative_cases")
    require(isinstance(cases, list) and len(cases) == 12 and
            [c.get("id") for c in cases] ==
            [f"NEG{i:02d}" for i in range(1, 13)],
            "12 negative cases required")
    for case in cases:
        for field in ("attack", "expected"):
            require(isinstance(case.get(field), str) and
                    len(case[field]) >= 16,
                    f"Missing adverse scenario detail for {case.get('id')}")
    return states


def reject_mutation(data, mutation, title, parents):
    v = deepcopy(data)
    mutation(v)
    try:
        valid_contract(v, parents)
    except (AssertionError, KeyError, ValueError, TypeError):
        return
    raise AssertionError(f"Adversarial contract mutation accepted: {title}")


def audit_a():
    parents = parent_contracts()
    data = read_data()
    valid_contract(data, parents)
    doc = SPEC.read_text(encoding="utf-8")
    for marker in ("C02-05", "4/9", "NO CERTIFICADO", "MAE-C02-01..08",
                   "N01", "N18", "ST01", "ST02", "ST03", "ST04", "ST05",
                   "loading", "empty", "error", "offline", "confirmation",
                   "C02-06", "P01", "P11", "T01-R01"):
        require(marker in doc, f"Document/source marker missing: {marker}")
    reject_mutation(data, lambda d: d["states"].pop(), "missing state", parents)
    reject_mutation(data, lambda d: d["states"][0].update(id="ST02"),
                    "duplicate state ID", parents)
    reject_mutation(data, lambda d: d["source"].update(sha256="0" * 64),
                    "forged Word master", parents)
    reject_mutation(data, lambda d: d["contexts"][4]["needs"].pop(),
                    "missing C02-02 canonical need", parents)
    reject_mutation(data, lambda d: d["states"][1].update(
        requires="an HTTP 403 is an empty list"), "fake empty on HTTP forbidden", parents)
    print("AUDIT A PASS C02-05: exact Word, E01-E06 / N01-N18, five state kinds,")
    print("  parent chapter gates and five adversarial source/state mutations rejected.")


def audit_b():
    parents = parent_contracts()
    data = read_data()
    valid_contract(data, parents)
    c02_04_audits.checked_routes()
    service = SOURCE.read_text(encoding="utf-8")
    host = HOST.read_text(encoding="utf-8")
    template = TEMPLATE.read_text(encoding="utf-8")
    html = HOST_HTML.read_text(encoding="utf-8")
    model = MODEL.read_text(encoding="utf-8")
    require("parsePublishedPlatformBrand" in service and
            "/api/v1/public/branding/platform" in service and
            "if (this.pending) return" in service,
            "Invalid or overlapping public publication reads cannot be accepted")
    require("this.currentState.set({ kind: 'loading' })" in service and
            "this.currentState.set({ kind: 'unavailable' })" in service,
            "Retry must clear previous data and fail closed")
    require("PublicBrandService" in host and
            "publicBrandFallback" in host and
            "InteractionStateComponent" in host and
            "online" in host and "offline" in host and
            "removeEventListener" in host,
            "Host must use safe public brand, clean up offline listeners")
    require("cs-interaction-state" in html and
            'role="status"' in html and 'role="alert"' in html and
            '[view]="loadingView"' in html and
            '[view]="failedView()"' in html,
            "Missing accessible fail-closed public interaction integration")
    require("{{ view().title }}" in template and
            "{{ view().description }}" in template and
            'type="button"' in template and
            'view().kind === \'error\'' in template and
            'view().kind === \'offline\'' in template,
            "Unsafe or missing state templates/explicit retry")
    require("publicBrandFallback" in model and
            "return browserOnline ? PUBLIC_ERROR : PUBLIC_OFFLINE" in model and
            "if (load === 'loading')" in model,
            "Browser online hint must not produce a validated ready state")
    forbidden = ("innerHTML", "bypassSecurityTrust", "localStorage",
                 "sessionStorage", "document.cookie", "X-Tenant-Id",
                 "Authorization:", "eval(", "new Function(", "fetch(",
                 "HttpClient", "router.navigate", "window.location")
    code = template + model + COMPONENT.read_text(encoding="utf-8") + host
    for unsafe in forbidden:
        require(unsafe not in code, f"State renderer must not access private data: {unsafe}")
    reject_mutation(data, lambda d: d["transitions"].update(
        response_error="show-previous-tenant-result"), "stale on error", parents)
    reject_mutation(data, lambda d: d["transitions"].update(
        authorized_success_empty="allow-403-as-empty"), "HTTP forbidden as empty", parents)
    reject_mutation(data, lambda d: d["transitions"].update(
        confirmed_write="optimistic-client-success"), "success without ACK", parents)
    reject_mutation(data, lambda d: d["security"].update(
        offline_hint="offline-grants-access"), "browser hint authorizes", parents)
    reject_mutation(data, lambda d: d["security"].update(
        cross_tenant="share-cached-customer"), "cross-tenant stale cache", parents)
    reject_mutation(data, lambda d: d["security"].update(
        no_private_routes=False), "new private panel without auth", parents)
    reject_mutation(data, lambda d: d["runtime"].update(
        live_states=["loading", "confirmation"]), "future success falsely live", parents)
    reject_mutation(data, lambda d: d["security"].update(
        superadmin="global-access-private-PII"), "SuperAdmin no MFA", parents)
    reject_mutation(data, lambda d: d["parameterization"].update(
        safe_content="allow-admin-injected-html-js"), "unsafe admin-managed copy", parents)
    print("AUDIT B PASS C02-05: strict platform-only API, data clearing, online hint")
    print("  is not authorization, nine security/PII/confirmation mutations rejected.")
    print("LIMIT: browser regressions cover public/404, not private backend MFA/BOLA.")


def audit_c():
    parents = parent_contracts()
    data = read_data()
    valid_contract(data, parents)
    c02_04_audits.checked_routes()
    plan = PLAN.read_text(encoding="utf-8")
    if ACTA.is_file():
        acta = ACTA.read_text(encoding="utf-8")
        require("5/9" in plan and
                "C02-05 CERRADA TÉCNICAMENTE" in acta and
                "POSTMERGE" in acta and
                "C02-06" in acta,
                "C02-05 closure requires 5/9 and formal postmerge acta")
    else:
        require("4/9" in plan and "C02-05" in plan,
                "Cannot advance until C02-05 audited acta exists")
    require("NO CERTIFICADO" in plan and "1/37" in plan and "C03 bloqueado" in plan,
            "No chapter 02 certification or 03 development")
    c = COMPONENT.read_text(encoding="utf-8")
    template = TEMPLATE.read_text(encoding="utf-8")
    css = STYLE.read_text(encoding="utf-8")
    model = MODEL.read_text(encoding="utf-8")
    require("ChangeDetectionStrategy.OnPush" in c and
            "input.required<InteractionView>()" in c and
            "output<void>()" in c and
            "this.retry.emit()" in c,
            "State component must be standalone, OnPush and output-only")
    require("INTERACTION_KINDS" in model and
            all(f"'{kind}'" in model for kind in KIND_ORDER),
            "All five states must be typed")
    require("cs-interaction-state" in HOST_HTML.read_text(encoding="utf-8"),
            "Shared component isn't integrated in the current public page")
    require("min-height: 48px" in css and
            "prefers-reduced-motion" in css and
            ":focus-visible" in css and
            "overflow-wrap" in css and
            "clamp(" in css,
            "Touch, focus, mobile, overflow and reduced-motion treatment missing")
    require('data-interaction-state' in template and
            'aria-hidden="true"' in template and
            "{{ view().title }}" in template,
            "State component must be semantic and safely interpolated")
    t = TESTS.read_text(encoding="utf-8")
    for check in ("loading is announced", "HTTP error fails closed",
                  "offline/online browser hints", "malicious tenant-scoped publication",
                  "retry is single-flight", "mobile failure and offline alerts",
                  "unknown and proposed private paths"):
        require(check in t, f"Browser test absent: {check}")
    # Ensure local links cannot escape this repo / resolve to nowhere.
    for p in (SPEC, PLAN):
        for ref in re.findall(r"\[[^\]]+\]\(([^)]+)\)", p.read_text(encoding="utf-8")):
            if ref.startswith(("https://", "http://", "mailto:", "#")):
                continue
            target = (p.parent / ref.split("#", 1)[0]).resolve()
            require(target.is_relative_to(ROOT.resolve()) and target.is_file(),
                    f"Broken relative document link: {ref}")
    reject_mutation(data, lambda d: d["parameterization"].update(
        configuration_runtime_implemented=True), "fake live copy-admin", parents)
    reject_mutation(data, lambda d: d["runtime"].update(
        database_changes=["migrate user profiles"]), "premature DB", parents)
    reject_mutation(data, lambda d: d["parameterization"].update(
        immutable=["authorization"]), "missing noneditable trust boundaries", parents)
    reject_mutation(data, lambda d: d["negative_cases"].pop(), "lost negative case", parents)
    print("AUDIT C PASS C02-05: Angular standalone state renderer, keyboard/mobile,")
    print("  configurable future copy, four architecture mutations rejected.")
    print("LIMIT: Angular/.NET builds, real Chromium and Lighthouse/WCAG checks are separate CI.")


def main():
    require(len(sys.argv) == 2 and sys.argv[1] in ("A", "B", "C"),
            "Usage: python3 qa/c02_05_audits.py A|B|C")
    {"A": audit_a, "B": audit_b, "C": audit_c}[sys.argv[1]]()


if __name__ == "__main__":
    try:
        main()
    except (AssertionError, KeyError, ValueError, TypeError, OSError) as exc:
        print(f"FAIL C02-05: {exc}", file=sys.stderr)
        sys.exit(1)
