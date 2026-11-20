#!/usr/bin/env python3
"""CRECISOL C05 UX F03 correction: six authenticated account-view static gates.

These audits complement real browser tests, not backend/DB authorization
certification or final independent visual 9/10 acceptance.
"""
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
APP = ROOT / "frontend/src/app"
PAGES = APP / "pages"
IDENTITY = APP / "identity"


def require(test: bool, statement: str) -> None:
    if not test:
        raise AssertionError("F03 ACCOUNT NO-GO: " + statement)


def read(path: Path) -> str:
    require(path.is_file(), "missing required file: " + str(path))
    return path.read_text(encoding="utf-8")


def a() -> None:
    nav = read(PAGES / "c05-navigation.model.ts")
    page = read(PAGES / "identity-portal.component.html")
    work = read(PAGES / "identity-workspace.component.html")
    model = read(PAGES / "identity-flow-views.component.ts")
    api = read(IDENTITY / "identity-api.service.ts")
    for route in ("resumen", "perfil", "llaves", "seguridad", "historial", "administracion"):
        require("'" + route + "'" in nav, "missing workspace route " + route)
    require('vista:\'historial\'' in page, "history link not navigable")
    require("view() === 'historial'" in work, "independent history panel absent")
    for handler in ("securityActionRequested.emit", "sessionRequested.emit",
                    "passkeyListRequested.emit", "loadProfileRequested.emit",
                    "revokeRequested.emit"):
        require(handler in work or handler in model,
                "existing account operation unavailable: " + handler)
    for action in ("async refreshPasskeys()", "async refreshSessions()",
                   "async loadSecurityEvents()", "async decideMembership(",
                   "async revokeSession("):
        require(action in api, "API contract altered or missing: " + action)
    require("cs-ui-card" in work and "cs-ui-state" in work,
            "F02 institutional card/state components not reused")
    print("F03 account/A PASS: 6 workspace views, operations, API compatibility, F02 components")


def b() -> None:
    work = read(PAGES / "identity-workspace.component.html")
    model = read(PAGES / "identity-flow-views.component.ts")
    present = read(IDENTITY / "identity-presentation.ts")
    portal = read(PAGES / "identity-portal.component.ts")
    require("{{ session.id }}" not in work, "raw session IDs visible")
    require("{{ key.id }}" not in work, "raw WebAuthn IDs visible")
    require("{{ event.action }}" not in work and "{{ event.atUtc }}" not in work,
            "raw security event codes or UTC strings visible")
    require('name="factorId"' in work and '<select id="passkey-id"' in work
            and 'value=""' in work, "passkey revoke must require explicit listed selection")
    require('name="decision" required' in work and
            '<option value="" selected disabled>' in work, "admin approval preselected")
    require('name="reason" required minlength="8"' in work and
            'name="code" autocomplete="one-time-code"' in work,
            "admin decision must require reason and MFA code")
    require("identityEventLabel" in model and "identityReferenceLabel" in model,
            "private formatting not used")
    require("return actionNames[action] ?? 'Otra actividad de seguridad';" in present,
            "unknown event reveals raw internal code")
    require("identityReferenceLabel" in present and "slice(-6)" in present,
            "masked IDs are not being generated")
    require("acknowledgements[action]" in portal, "generic action success messages remain")
    for risky in ("innerHTML", "bypassSecurityTrust", "eval(", "localStorage"):
        require(risky not in work and risky not in model,
                "unsafe client logic in account: " + risky)
    print("F03 account/B PASS: masked IDs, security history, explicit approvals and action receipts")


def c() -> None:
    css = read(PAGES / "identity-flow.component.css") + "\n" + read(PAGES / "identity-workspace.component.css")
    test = read(ROOT / "frontend/tests/c05-f03-account.spec.ts")
    config = read(ROOT / "frontend/playwright.c05-f03-account.config.ts")
    require("identity-workspace.component.css" in read(PAGES / "identity-flow-views.component.ts"),
            "workspace component must explicitly own the new scoped CSS")
    for cls in ("identity-workspace", "identity-profile", "identity-activity",
                "identity-device-region", "identity-session-overview",
                "identity-security-intro", "identity-approval-context",
                "identity-key-list"):
        require("." + cls in css, "missing visual treatment: " + cls)
    require("forced-colors: active" in css and "prefers-reduced-motion: reduce" in css,
            "accessible alternate media unavailable")
    for width in ("320", "390", "768", "1024", "1440"):
        require("width: " + width in test, "screenshot viewport missing: " + width)
    for route in ("resumen", "perfil", "llaves", "seguridad", "historial", "administracion"):
        require("'" + route + "'" in test, "missing account suite route " + route)
    require("AxeBuilder" in test and "noHorizontalOverflow" in test,
            "axe or responsive overflow verification missing")
    require("127.0.0.1:4204" in config and "reuseExistingServer: false" in config,
            "account QA not local and isolated")
    print("F03 account/C PASS: six view styling, responsive, accessibility and isolated tests")


if __name__ == "__main__":
    try:
        require(len(sys.argv) == 2 and sys.argv[1] in ("A", "B", "C"),
                "usage: ... A|B|C")
        {"A": a, "B": b, "C": c}[sys.argv[1]]()
    except (AssertionError, OSError) as exc:
        print("FAIL:", exc, file=sys.stderr)
        raise SystemExit(1) from exc
