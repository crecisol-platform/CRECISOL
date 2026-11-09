#!/usr/bin/env python3
"""C01-05 independent read-only audits: requirements, browser boundary and UX architecture."""
from pathlib import Path
import json
import re
import sys
import c01_verify_source

ROOT = Path(__file__).resolve().parents[1]
FRONT = ROOT / "frontend"
PAGE = FRONT / "src/app/pages/platform-home.component.html"
PAGE_TS = FRONT / "src/app/pages/platform-home.component.ts"
PAGE_CSS = FRONT / "src/app/pages/platform-home.component.css"
SERVICE = FRONT / "src/app/branding/public-brand.service.ts"
MODEL = FRONT / "src/app/branding/public-brand.ts"
ROUTES = FRONT / "src/app/app.routes.ts"
TEST = FRONT / "tests/c01-05.public-brand.spec.ts"
WORKFLOW = ROOT / ".github/workflows/c01-05-check.yml"
SPEC = ROOT / "docs/c01/C01_05_INTERFAZ_ANGULAR.md"
MATRIX = ROOT / "docs/c01/C01_01_MATRIZ_REQUISITOS.md"
PLAN = ROOT / "docs/C01_PREPARACION.md"

def read(path: Path) -> str:
    return path.read_text(encoding="utf-8")

def require(ok: bool, message: str) -> None:
    if not ok:
        raise AssertionError(message)

def audit_a():
    c01_verify_source.main()
    spec, matrix = read(SPEC), read(MATRIX)
    ids = ("P01", "P04", "P06", "P07", "P14", "P15", "P24", "P25", "P31",
           "T01-R03", "T01-R04", "T02-R01", "T02-R02", "T02-R03",
           "T02-R07", "T03-R01", "T03-R02")
    for identifier in ids:
        require(identifier in matrix and identifier in spec, "Missing source ID " + identifier)
    for path in (PAGE, PAGE_TS, PAGE_CSS, SERVICE, MODEL, ROUTES, TEST, WORKFLOW):
        require(path.is_file(), "Missing implementation/test " + str(path))
    page, service, tests = read(PAGE), read(SERVICE), read(TEST)
    for token in ("<header", "<main", "<footer", "skip-link", "aria-labelledby",
                  "identity-details", "areas-section", "retry()", "model.kind"):
        require(token in page, "Missing C01-05 section or state " + token)
    require("'/api/v1/public/branding/platform'" in service,
            "Frontend must target existing C01-04 public endpoint")
    for label in ("public identity comes ONLY", "mobile 360", "WCAG 2.2 AA",
                  "fail closed on published tenant", "reject injected markup",
                  "HTTP 404, 503", "recovery after outage", "missing optional presentation"):
        require(label in tests, "Missing browser acceptance test: " + label)
    print("C01-05 AUDIT A PASS: verified Word and matrix, UI/SEO fields, required browser cases.")

def audit_b():
    service, model, page, page_ts = map(read, (SERVICE, MODEL, PAGE, PAGE_TS))
    routes, tests, css = map(read, (ROUTES, TEST, PAGE_CSS))
    for marker in ("parsePublishedPlatformBrand", "Number.isSafeInteger",
                   "safeText", "safeAsset", "safeColor", "optionalOrigin",
                   "platformKeys", "presentationKeys", "unsafeText"):
        require(marker in model, "Missing untrusted JSON gate: " + marker)
    for field in ("'revision'", "'publishedAt'", "'presentation'", "'logoUrl'"):
        require(field in model, "Public schema missing " + field)
    require("Object.keys(object).some(key => !allowed.has(key))" in model,
            "Private/unknown DTO properties must be denied")
    for marker in ("scopeId", "tenantId", "status", "preview"):
        require(marker not in service.lower(),
                "The browser service unexpectedly uses tenant/private context: " + marker)
    for forbidden in ("localStorage", "sessionStorage", "innerHTML", "DomSanitizer",
                      "bypassSecurityTrust", "eval(", "new Function(",
                      "document.cookie", "window.location", "X-Tenant-Id",
                      "Authorization", "CRECISOL_C01_READONLY_DSN"):
        for path, text in ((SERVICE, service), (PAGE_TS, page_ts), (PAGE, page)):
            require(forbidden not in text, "Unsafe browser capability " + forbidden + " in " + path.name)
    require("this.currentState.set({ kind: 'unavailable' })" in service,
            "Network failure must erase published identity from signals")
    require("link.setAttribute('href', faviconPath)" in page_ts and
            "safeAsset(obj['faviconUrl'])" in model,
            "Favicon must come from a validated local asset path")
    require("brand.presentation?.seoTitle" in page_ts and
            "this.meta.removeTag" in page_ts,
            "SEO must come from published metadata and reset on failure")
    require("style.--brand-primary" in page and
            "safeColor(obj['primaryColor'])" in model,
            "Only validated platform colors may reach CSS variables")
    require("loadComponent" in routes and "path: '**'" in routes and
            "admin" not in routes and "tenant" not in routes,
            "No private Angular pages or eager-only router")
    for marker in ("fail closed on published tenant", "reject injected markup",
                   "unsafe colors", "HTTP 404, 503", "localStorage", "sessionStorage"):
        require(marker in tests, "Negative browser coverage missing: " + marker)
    require("aria-hidden=\"true\"" in page and "@if (model.kind === 'ready')" in page,
            "Decorative content or untrusted render not guarded")
    print("C01-05 AUDIT B PASS: fixed same-origin endpoint, validated DTO and fail-closed browser.")

def audit_c():
    package = json.loads(read(FRONT/"package.json"))
    build = json.loads(read(FRONT/"angular.json"))
    tsconfig = json.loads(read(FRONT/"tsconfig.json"))
    for dependency in ("@angular/core", "@angular/common", "@angular/platform-browser", "@angular/router"):
        require(package["dependencies"].get(dependency) == "22.2.4",
                "Angular 22.2.4 must be pinned: " + dependency)
    for dependency in ("@angular/build", "@angular/cli", "@angular/compiler-cli"):
        require(package["devDependencies"].get(dependency) == "22.2.4",
                "Angular CLI/build must match framework version: " + dependency)
    require(package["devDependencies"].get("typescript") == "6.0.3",
            "Angular 22 needs TypeScript 6.0.x")
    require(package["engines"]["node"] == ">=24.15.0 <25",
            "Compatible Node 24.15.0 floor required")
    require(package["devDependencies"].get("@playwright/test") and
            package["devDependencies"].get("@axe-core/playwright"),
            "Chromium and accessibility test engines must be declared")
    require(tsconfig["compilerOptions"]["strict"] and
            tsconfig["angularCompilerOptions"]["strictTemplates"],
            "TS and Angular templates must be strict")
    require(build["projects"]["crecisol-web"]["architect"]["build"]["options"]["browser"] == "src/main.ts",
            "Angular application builder must reference its standalone bootstrap")
    for marker in ("@media (max-width: 700px)", "@media (max-width: 380px)",
                   "min-height: 44px", "focus", "prefers-reduced-motion"):
        styles = read(PAGE_CSS) + read(FRONT/"src/styles.css")
        require(marker in styles, "Responsive/accessibility CSS missing: " + marker)
    require("loadComponent: () => import(" in read(ROUTES),
            "Main route must load standalone component lazily")
    require("provideHttpClient(withFetch())" in read(FRONT/"src/main.ts"),
            "Angular v22 HTTP fetch provider required")
    require("computed(" in read(PAGE_TS) and "signal<BrandLoadState>" in read(SERVICE),
            "Signals and derived state required")
    require("noindex, nofollow" in read(FRONT/"src/index.html"),
            "Prelaunch page must not claim discoverable production SEO")
    require("<img" not in read(PAGE) and "official-logo-c01-06" in read(PAGE),
            "C01-06 must own approval and insertion of official logo binary")
    require("0/37" in read(SPEC) and "C01-06" in read(SPEC),
            "Sequence/limits missing")
    require("Reversión" in read(SPEC), "Rollback not documented")
    workflow = read(WORKFLOW)
    for token in ("contents: read", "node-version: '24.15.0'",
                  "c01-05-audits", "c01-05-build", "c01-05-browser"):
        require(token in workflow, "Missing reproducible CI gate: " + token)
    print("C01-05 AUDIT C PASS: Angular 22/Node24/TS6, lazy routes, Signals, mobile/WCAG.")

if __name__ == "__main__":
    try:
        require(len(sys.argv) == 2 and sys.argv[1] in ("A", "B", "C"),
                "Usage: c01_05_three_audits.py A|B|C")
        {"A": audit_a, "B": audit_b, "C": audit_c}[sys.argv[1]]()
    except (AssertionError, OSError, ValueError, KeyError, TypeError) as exc:
        print("C01-05 AUDIT FAIL: " + str(exc), file=sys.stderr)
        sys.exit(1)
