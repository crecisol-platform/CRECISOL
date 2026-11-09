#!/usr/bin/env python3
"""C02-03 three independent audits: canonical source, safe public routing, UI gates.

Static checks + negative mutations complement (do not replace) browser E2E.
No API auth/tenant permissions are created or validated by these design gates.
"""
from collections import Counter
from copy import deepcopy
import json
from pathlib import Path
import re
import sys

import c02_01_source
import c02_02_audits

ROOT = Path(__file__).resolve().parents[1]
SITEMAP = ROOT / "docs/c02/C02_03_SITEMAP.json"
SPEC = ROOT / "docs/c02/C02_03_ESPECIFICACION.md"
PLAN = ROOT / "docs/C02_PREPARACION.md"
ACTA = ROOT / "docs/c02/C02_03_ACTA_MICROFASE.md"
ROUTES = ROOT / "frontend/src/app/app.routes.ts"
MODEL = ROOT / "frontend/src/app/pages/public-information.model.ts"
COMPONENT = ROOT / "frontend/src/app/pages/public-information.component.ts"
TEMPLATE = ROOT / "frontend/src/app/pages/public-information.component.html"
CSS = ROOT / "frontend/src/app/pages/public-information.component.css"
HOME = ROOT / "frontend/src/app/pages/platform-home.component.html"
INDEX = ROOT / "frontend/src/index.html"
BRAND_SERVICE = ROOT / "frontend/src/app/branding/public-brand.service.ts"
BRAND_PARSER = ROOT / "frontend/src/app/branding/public-brand.ts"
EXPECTED_PATHS = ["/", "/experiencias", "/demostraciones", "/alta-empresas"]
EXPECTED_PAGES = ["platform-home", "experiencias", "demostraciones", "alta-empresas"]
EXPECTED_REFS = {f"P{i:02d}" for i in range(1, 12)} | {
    f"T01-R{i:02d}" for i in range(1, 7)
}


def require(ok, message):
    if not ok:
        raise AssertionError(message)


def sitemap():
    return json.loads(SITEMAP.read_text(encoding="utf-8"))


def validate(data):
    require(data.get("artifact") == "CRECISOL-C02-03-PUBLIC-SITEMAP",
            "Unknown sitemap contract")
    require(data.get("schema_version") == 1, "Unknown sitemap schema")
    require(data.get("state") == "public-information-only-no-tenant-auth",
            "Sitemap must not claim a private/business app")
    source = data.get("source", {})
    require(source.get("sha256") == c02_01_source.EXPECTED_SHA,
            "Canonical Word source SHA mismatch")
    require(source.get("phase") == "C02-03", "Wrong microphase")
    require(source.get("parent") == "docs/c02/C02_02_MAPA_NECESIDADES.json",
            "Previous microphase map missing")
    routes = data.get("routes")
    require(isinstance(routes, list) and len(routes) == 4, "Four public routes only")
    require([r.get("id") for r in routes] == ["S01", "S02", "S03", "S04"],
            "Sitemap IDs missing/duplicated/out of order")
    require([r.get("path") for r in routes] == EXPECTED_PATHS,
            "A private, invented, repeated or missing public route was added")
    require([r.get("page") for r in routes] == EXPECTED_PAGES,
            "Page-to-route mapping changed")
    for i, route in enumerate(routes):
        require(route.get("visibility") == "platform-public",
                "Tenant visibility cannot be enabled")
        require(route.get("auth") == "none", "No private authorization here")
        require(route.get("backend_data") == "GET /api/v1/public/branding/platform",
                "Sitemap cannot fetch arbitrary or tenant data")
        require(route.get("operational") is (i == 0),
                "Information-only route must not claim operational services")
        require(route.get("mode") == ("published-identity" if i == 0 else "information-only"),
                "Public-page functional status changed")
        require(route.get("experience") in {"E01", "E02"},
                "Private experience cannot be made a public route")
        for key in ("source_refs", "need_refs"):
            refs = route.get(key)
            require(isinstance(refs, list) and refs and len(refs) == len(set(refs)),
                    "Empty or duplicated sitemap references")
        require(set(route["source_refs"]).issubset(EXPECTED_REFS),
                "Sitemap references a noncanonical paragraph/role")
        require(set(route["need_refs"]).issubset({f"N{i:02d}" for i in range(1, 19)}),
                "Sitemap references an undefined C02-02 need")
    controls = data.get("controls", {})
    for key in ("no_tenant_selector", "no_private_forms", "no_real_payment",
                "no_demo_customer_data", "noindex", "tenant_host_is_not_auth"):
        require(controls.get(key) is True, f"Public security control {key} downgraded")
    require(controls.get("unknown_route") == "not-found", "Unknown route must be denied")
    require(controls.get("scope") == "platform-only-no-tenant-public-pages",
            "Unexpected public tenant surface")
    require(controls.get("future_private_authorization") == "backend-deny-by-default",
            "Frontend cannot authorize private resources")
    require(controls.get("public_identity_endpoint") == "/api/v1/public/branding/platform",
            "Only published platform snapshot is allowed")
    require("WCAG 2.2 AA" in controls.get("accessibility", ""), "Accessibility baseline missing")
    deferred = data.get("deferred", [])
    require(isinstance(deferred, list) and len(deferred) == 6,
            "Six private/tenant/payment boundaries must remain deferred")
    require([d.get("id") for d in deferred] ==
            [f"D{i:02d}" for i in range(1, 7)], "Deferred boundaries changed")
    for d in deferred:
        require(d.get("experience") in {"E02", "E03", "E04", "E05", "E06"},
                "Deferred scope missing or points to a made-up experience")
        require(d.get("scope") != "platform-public",
                "Deferred private surface accidentally promoted to public")
        require(isinstance(d.get("condition"), str) and len(d["condition"]) > 25,
                "Authorization or host verification condition omitted")
        require(isinstance(d.get("chapters"), list) and d["chapters"] and
                all(re.fullmatch(r"C\d{2}", ch) for ch in d["chapters"]),
                "Deferred implementation dependency missing")
    require("NotFoundComponent" in data.get("deny_by_default", ""),
            "Unknown route deny-by-default contract missing")
    return routes


def must_reject(data, mutator, reason):
    variant = deepcopy(data)
    mutator(variant)
    try:
        validate(variant)
    except (AssertionError, KeyError, TypeError, ValueError):
        return
    raise AssertionError(f"Negative mutation was accepted: {reason}")


def actual_route_paths():
    contents = ROUTES.read_text(encoding="utf-8")
    return re.findall(r"^\s*path: '([^']*)'", contents, re.M), contents


def audit_a():
    original, roles, _, _ = c02_01_source.original_parts()
    c02_02_audits.valid_contract(c02_02_audits.read_data())
    require(len(original) == 11 and len(roles) == 7, "Master source changed")
    data = sitemap()
    validate(data)
    paths, _ = actual_route_paths()
    require(paths == ["", "experiencias", "demostraciones", "alta-empresas", "**"],
            f"Angular routes don't match allowlist and wildcard: {paths}")
    doc = SPEC.read_text(encoding="utf-8")
    require(all(token in doc for token in [
        "C02-03", "C02 NO CERTIFICADO", "11 párrafos", "seis",
        "P03", "P04", "P05", "P06", "P11", "T01-R06", "N01",
        "N06", "S01", "S02", "S03", "S04", "C02-04"
    ]), "Canonical source or phase mapping absent from C02-03 document")
    must_reject(data, lambda d: d["routes"].pop(), "deleted public route")
    must_reject(data, lambda d: d["routes"][1].update(path="/admin"), "private route")
    must_reject(data, lambda d: d["routes"][2].update(source_refs=["P99"]),
                "forged source ref")
    must_reject(data, lambda d: d["routes"][2].update(id="S02"),
                "duplicate sitemap ID")
    print("AUDIT A PASS C02-03: pinned Word/11 paragraphs, C02-02 contract,")
    print("  four exact public paths, canonical source mapping, 4 rejection mutations.")


def audit_b():
    c02_01_source.original_parts()
    data = sitemap()
    validate(data)
    paths, source = actual_route_paths()
    require(paths[-1] == "**", "Missing wildcard deny route")
    require("NotFoundComponent" in source, "Wildcard no longer uses NotFoundComponent")
    require(source.count("loadComponent:") == 5, "Unexpected route loader")
    for token in ("path: 'superadmin'", "path: 'login'", "path: 'panel'",
                  "path: 'negocio'", "path: 'empleado'", "path: 'cliente'",
                  "redirectTo:", "canActivate:"):
        require(token not in source, f"Private routing introduced: {token}")
    script = COMPONENT.read_text(encoding="utf-8")
    template = TEMPLATE.read_text(encoding="utf-8")
    model = MODEL.read_text(encoding="utf-8")
    api = BRAND_SERVICE.read_text(encoding="utf-8")
    parser = BRAND_PARSER.read_text(encoding="utf-8")
    require("parsePublishedPlatformBrand" in api and
            "/api/v1/public/branding/platform" in api,
            "Platform-only published identity contract not retained")
    require("Unexpected private branding property" in parser and
            "safeAsset" in parser and "safeText" in parser,
            "C01 strict untrusted public payload parser missing")
    require("PublicBrandService" in script and "state.kind !== 'ready'" in script,
            "Public routes must reject unavailable/invalid published snapshots")
    require("link[rel=\"icon\"][data-crecisol-managed=\"true\"]" in script,
            "Stale favicon cleaning missing")
    for token in ("localStorage", "sessionStorage", "innerHTML",
                  "bypassSecurityTrust", "document.cookie", "X-Tenant-Id",
                  "Authorization:", "eval(", "new Function("):
        require(token not in script + template + model,
                f"Private storage, unsafe HTML or fake auth introduced: {token}")
    require('routerLink="/"' in template and
            'href="#contenido"' in template and
            '[routerLink]="link.path"' in template,
            "Safe public navigation or keyboard skip link missing")
    for phrase in ("En preparación", "sin", "no", "No hay"):
        require(phrase.casefold() in model.casefold(), "Non-operational status text missing")
    must_reject(data, lambda d: d["routes"][2].update(backend_data="GET /api/v1/private/customers"),
                "private API as public sitemap data")
    must_reject(data, lambda d: d["routes"][3].update(operational=True),
                "fake operational contract/billing")
    must_reject(data, lambda d: d["controls"].update(no_tenant_selector=False),
                "selector of companies at login")
    must_reject(data, lambda d: d["controls"].update(no_demo_customer_data=False),
                "real customer data in demos")
    must_reject(data, lambda d: d["deferred"].pop(), "private boundary removed")
    print("AUDIT B PASS C02-03: explicit-only public routes, no tenant/private navigation,")
    print("  published platform brand only, strict fallback, five security mutations rejected.")
    print("LIMIT: No new private API, BOLA/IDOR, authentication or tenant-host router implemented.")


def audit_c():
    c02_01_source.original_parts()
    data = sitemap()
    validate(data)
    html = TEMPLATE.read_text(encoding="utf-8")
    css = CSS.read_text(encoding="utf-8")
    model = MODEL.read_text(encoding="utf-8")
    plan = PLAN.read_text(encoding="utf-8")
    require("1/37" in plan and "C03 bloqueado" in plan and "NO CERTIFICADO" in plan,
            "Chapter gate prematurely certified or advanced")
    if ACTA.exists():
        acta = ACTA.read_text(encoding="utf-8")
        require("3/9" in plan and "C02-03" in plan and
                "C02-03 CERRADA TÉCNICAMENTE" in acta and "POSTMERGE" in acta and
                "6ccd98960f36dce3407392fd26406366b4cd0650" in acta and
                all(run in acta for run in (
                    "42624454291", "42624454313", "42624454480",
                    "42624454179", "42624454085", "42624454342")),
                "C02-03 technical closure requires exact SHA and all six postmerge runs")
    else:
        require("2/9" in plan, "C02-03 cannot count as closed without an acta")
    require(len(re.findall(r"\{ path: '/[^']+', label:", model)) == 3,
            "Three public info navigation links expected")
    require('href="/experiencias"' in HOME.read_text(encoding="utf-8"),
            "No reachable public entry point from C01 platform home")
    require(all(token in html for token in (
        'class="skip-link"', 'aria-current', '<h1>', '<h2>', 'id="contenido"',
        'aria-label="Navegación pública"', 'model.kind === \'ready\'',
        'role="alert"', 'role="status"', 'routerLink'
    )), "Public information UI lacks accessible navigation or state handling")
    require(all(token in css for token in (
        "max-width: 650px", "grid-template-columns: 1fr",
        "prefers-reduced-motion", "overflow-wrap", "min-height: 44px"
    )), "Responsive, focus or reduced-motion CSS missing")
    require('content="noindex, nofollow"' in INDEX.read_text(encoding="utf-8"),
            "New public pages must not claim production SEO publication")
    require("styleUrl: './public-information.component.css'" in COMPONENT.read_text(encoding="utf-8"),
            "Public component must load bounded local styles")
    for md in (SPEC, PLAN):
        for target in re.findall(r"\[[^\]]+\]\(([^)]+)\)", md.read_text(encoding="utf-8")):
            if target.startswith(("https://", "http://", "mailto:", "#")):
                continue
            dest = (md.parent / target.split("#", 1)[0]).resolve()
            require(dest.is_relative_to(ROOT.resolve()) and dest.is_file(),
                    f"Broken or escaping Markdown link: {target}")
    must_reject(data, lambda d: d["controls"].update(noindex=False),
                "premature SEO publication")
    must_reject(data, lambda d: d["routes"][1].update(path="/empresa/tenant-x"),
                "tenant route added to platform")
    print("AUDIT C PASS C02-03: lazy sitemap, responsive/a11y and metadata gates,")
    print("  existing build budgets, no premature chapter certification, two negative mutations.")
    print("LIMIT: browser WCAG and runtime route/negative tests must pass separately in CI.")


def main():
    require(len(sys.argv) == 2 and sys.argv[1] in ("A", "B", "C"),
            "Usage: python3 qa/c02_03_audits.py A|B|C")
    {"A": audit_a, "B": audit_b, "C": audit_c}[sys.argv[1]]()


if __name__ == "__main__":
    try:
        main()
    except (AssertionError, KeyError, ValueError, TypeError, OSError) as error:
        print(f"FAIL C02-03: {error}", file=sys.stderr)
        sys.exit(1)
