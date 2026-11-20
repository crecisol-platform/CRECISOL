#!/usr/bin/env python3
"""C05 UX F04 — deliberate A/B/C gates for the isolated platform operator UI."""
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
APP = ROOT / "frontend/src/app"
PAGES = APP / "pages"
IDENTITY = APP / "identity"
VIEWS = ("cases", "sessions", "profile", "security", "monitor")


def need(ok: bool, msg: str) -> None:
    if not ok:
        raise AssertionError("F04 NO-GO: " + msg)


def read(path: Path) -> str:
    need(path.is_file(), "missing " + str(path))
    return path.read_text("utf-8")


def a() -> None:
    model = read(PAGES / "platform-operator-views.component.ts")
    parent = read(PAGES / "platform-identity-panel.component.html")
    nav = read(PAGES / "c05-navigation.model.ts")
    service = read(IDENTITY / "platform-identity-api.service.ts")
    for section in ("casos", "sesiones", "perfil", "seguridad", "monitor"):
        need("'" + section + "'" in nav, "operator section not allowlisted: " + section)
        need("('" + section + "')" in parent or section == "casos",
             "operator section not bound: " + section)
    for view in VIEWS:
        template = read(PAGES / f"platform-operator-{view}.component.html")
        need("operator-workspace" in template and "operator-section-head" in template,
             "missing task-oriented hierarchy: " + view)
        need("class=\"operator" in template,
             "no platform-scoped presentation: " + view)
    for symbol in ("sessionsLoaded", "profileLoaded", "abuseLoaded", "caseLoaded",
                   "async lookup(", "async loadSessions(", "async loadProfile(",
                   "async loadAbuse(", "async review(", "async revokeSession("):
        need(symbol in service, "operator contract absent: " + symbol)
    need("platform-operator-f04.component.css" in model, "F04 scoped CSS not used")
    print("F04/A PASS: five administrative views, preserved API contracts and isolated component model")


def b() -> None:
    parent = read(PAGES / "platform-identity-panel.component.ts")
    form = read(PAGES / "platform-operator-cases.component.html")
    sessions = read(PAGES / "platform-operator-sessions.component.html")
    monitor = read(PAGES / "platform-operator-monitor.component.html")
    service = read(IDENTITY / "platform-identity-api.service.ts")
    present = read(IDENTITY / "platform-operator-presentation.ts")
    browser = read(ROOT / "frontend/tests/c05-f04-operators.spec.ts")
    need('value="">Selecciona una decisión</option>' in form and
         'name="decision" required' in form and 'name="confirmed" type="checkbox" required' in form,
         "decisions are implicit or not confirmed")
    need("field('confirmed') !== 'on'" in parent and
         "!['approve', 'reject'].includes(field('decision'))" in parent,
         "UI does not deny missing explicit decision/confirmation")
    need("{{ item.caseId }}" not in form and "{{ session.id }}" not in sessions and
         "{{ signal.kind }}" not in monitor, "raw operator identifiers or backend codes shown")
    need("identityReferenceLabel(session.id)" in read(PAGES / "platform-operator-views.component.ts"),
         "full session identifiers not masked")
    need("'Otras señales de protección'" in present,
         "unknown abuse codes could be displayed verbatim")
    for gate in ("sessionsRequestRevision", "abuseRequestRevision", "profileRequestRevision",
                 "caseRequestRevision", "this.sessions.set([]); this.sessionsLoaded.set(false);",
                 "this.abuse.set([]); this.abuseLoaded.set(false);"):
        need(gate in service, "unsafe stale read behavior: " + gate)
    need("/api/v1/platform-identity/" in service and
         "/api/v1/identity/browser/" not in service,
         "operator service crosses into business API")
    for code in ("innerHTML", "bypassSecurityTrust", "localStorage", "eval("):
        need(code not in form + sessions + monitor + present, "untrusted execution: " + code)
    need("platform realm never borrows a business session" in browser and
         "operator read denial clears" in browser,
         "negative isolation browser tests missing")
    print("F04/B PASS: explicit case decisions, no raw codes, denial clears stale operator data, no business credentials")


def c() -> None:
    css = read(PAGES / "platform-operator-f04.component.css")
    tests = read(ROOT / "frontend/tests/c05-f04-operators.spec.ts")
    config = read(ROOT / "frontend/playwright.c05-f04-operators.config.ts")
    for name in ("operator-section-head", "operator-card", "operator-filters", "operator-list",
                 "operator-empty", "operator-scope-note"):
        need("." + name in css, "missing visual component: " + name)
    need("font-size: 200%" in tests and "forcedColors: 'active'" in tests,
         "F04 200% and forced-colors test missing")
    need("prefers-reduced-motion: reduce" in css and "forced-colors: active" in css,
         "accessible visual modes missing")
    for width in ("320", "390", "768", "1024", "1440"):
        need("width: " + width in tests, "missing viewport " + width)
    for view in ("casos", "sesiones", "perfil", "seguridad", "monitor"):
        need("'" + view + "'" in tests, "missing operator screenshot area " + view)
    need("AxeBuilder" in tests and "noOverflow" in tests and
         "name: 'Áreas de la consola de identidad'" in tests,
         "WCAG, responsive or realm tests missing")
    need("127.0.0.1:4206" in config and "reuseExistingServer: false" in config,
         "F04 harness not isolated to loopback")
    print("F04/C PASS: five responsive operator screens, keyboard/axe tests and scoped styling")


if __name__ == "__main__":
    try:
        need(len(sys.argv) == 2 and sys.argv[1] in ("A", "B", "C"), "expected A, B or C")
        {"A": a, "B": b, "C": c}[sys.argv[1]]()
    except (AssertionError, OSError) as error:
        print("FAIL:", error, file=sys.stderr)
        raise SystemExit(1) from error
