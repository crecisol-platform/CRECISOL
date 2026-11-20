#!/usr/bin/env python3
"""C05 UX F03 A/B/C source and contract audits (not a backend authorization test)."""
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
PAGES = ROOT / "frontend/src/app/pages"
TEST = ROOT / "frontend/tests/c05-f03-navigation.spec.ts"


def check(value: bool, why: str) -> None:
    if not value:
        raise AssertionError("C05 UX F03 NO-GO: " + why)


def source(path: Path) -> str:
    check(path.is_file(), "missing " + str(path))
    return path.read_text(encoding="utf-8")


def audit_a() -> None:
    model = source(PAGES / "c05-navigation.model.ts")
    portal = source(PAGES / "identity-portal.component.ts")
    guest = source(PAGES / "identity-portal.component.html")
    operator_ts = source(PAGES / "platform-identity-panel.component.ts")
    operator_html = source(PAGES / "platform-identity-panel.component.html")
    for name in ("ingresar", "registro", "confirmar", "recuperar", "invitacion",
                 "acceso-existente", "resumen", "perfil", "llaves", "seguridad",
                 "administracion", "casos", "sesiones", "monitor"):
        check("'" + name + "'" in model, "navigation route missing: " + name)
    check("guestViewFor" in model and "workspaceViewFor" in model,
          "state-aware guest and workspace route resolvers missing")
    for name in ("guestView", "workspaceView", "sectionName", "publishedTheme"):
        check("readonly " + name in portal, "computed navigation context missing: " + name)
    for name in ("cs-identity-recovery", "cs-identity-guest",
                 "cs-identity-mfa", "cs-identity-workspace",
                 "cs-platform-identity-panel"):
        check(name in guest, "original identity view missing: " + name)
    for label in ("Opciones de identidad", "Otras opciones de cuenta",
                  "Mi cuenta", "Ruta de navegación"):
        check(label in guest, "accessible navigation missing: " + label)
    check("<cs-ui-navigation" in operator_html and "selectPanel($event)" in operator_html,
          "F02 operator navigation not integrated")
    check("void this.openSecurity()" in operator_ts and
          "this.platform.loadSessionPolicy()" in operator_ts,
          "operator security tab must preserve session policy API fetch")
    check("readonly navigationItems" in operator_ts and "this.busy()" in operator_ts,
          "operator navigation must block interactions during in-flight requests")
    check("operatorSectionFor(requested)" in operator_ts,
          "operator section names are not allowlisted")
    print("F03/A PASS: source route model, guest/account/operator navigation and existing actions")


def audit_b() -> None:
    model = source(PAGES / "c05-navigation.model.ts")
    portal = source(PAGES / "identity-portal.component.ts")
    html = source(PAGES / "identity-portal.component.html")
    operator = source(PAGES / "platform-identity-panel.component.ts")
    for name in ("guestViewFor", "workspaceViewFor", "operatorSectionFor"):
        check(name in model + portal + operator, "allowlist missing: " + name)
    for route in ("resumen", "perfil", "llaves", "seguridad", "administracion"):
        check("vista:'" + route + "'" in html,
              "route must remain a queryParam routerLink: " + route)
    for risky in ("innerHTML", "bypassSecurityTrust", "localStorage", "sessionStorage",
                  "document.cookie", "eval(", "XMLHttpRequest"):
        check(risky not in model and risky not in operator,
              "unsanctioned transport/storage in presentation routing: " + risky)
    check("this.identity.state() === 'authenticated'" in portal,
          "account section must depend on session state")
    check("this.view() === 'operadores'" in portal,
          "platform routing must stay distinct")
    check("if (!next || this.busy() || next === this.panel()) return;" in operator,
          "operator must deny invalid/repeated navigation and in-flight requests")
    check("publishedTheme" in portal and "InstitutionalThemeDirective" in portal,
          "theme must bind only validated published platform branding")
    check("MFA" in model and "backend" in model.lower(),
          "operator scope description and backend authority must remain explicit")
    print("F03/B PASS: allowlist, realm separation, no unsafe storage/HTML or auth bypass")


def audit_c() -> None:
    css = source(PAGES / "identity-portal.component.css")
    operator_css = source(PAGES / "identity-flow.component.css")
    tests = source(TEST)
    config = source(ROOT / "frontend/playwright.c05-f03.config.ts")
    for token in ("--cs-color-accent-soft", "--cs-color-border", "--cs-hit-target",
                  "--cs-color-focus", "--cs-brand-primary"):
        check(token in css, "F02 semantic design token not used: " + token)
    for item in ("account-breadcrumb", "account-workspace-intro", "account-more",
                 "account-nav--workspace", "forced-colors", "prefers-reduced-motion"):
        check(item in css, "missing responsive/a11y styling: " + item)
    for item in ("operator-context", "operator-navigation", "operator-exit"):
        check(item in operator_css, "missing operator style: " + item)
    for size in ("320", "390", "768", "1024", "1440"):
        check("width: " + size in tests, "missing viewport: " + size)
    for case in ("guest: deep links", "account: responsive",
                 "platform: separate", "200% text", "forcedColors",
                 "reducedMotion", "auditNavigation", "mockRealm"):
        check(case in tests, "missing browser QA: " + case)
    check("reuseExistingServer: false" in config and "4203" in config,
          "F03 must use an isolated loopback server")
    check("frontend" not in source(ROOT / "frontend/src/app/app.routes.ts")
          or "c05-f03" not in source(ROOT / "frontend/src/app/app.routes.ts"),
          "QA must not add production route")
    print("F03/C PASS: progressive navigation, 5 viewports, focus/zoom and QA contract")


if __name__ == "__main__":
    try:
        check(len(sys.argv) == 2 and sys.argv[1] in ("A", "B", "C"),
              "use: python3 qa/c05_ux_f03_navigation_audits.py A|B|C")
        {"A": audit_a, "B": audit_b, "C": audit_c}[sys.argv[1]]()
    except (AssertionError, OSError) as exc:
        print("FAIL:", exc, file=sys.stderr)
        raise SystemExit(1) from exc
