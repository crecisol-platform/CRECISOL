#!/usr/bin/env python3
"""C05 F04 premium: exact-scope A/B/C against authorized F01-F03 candidate."""
from pathlib import Path
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
BASE = "31ff32d348606e77d311b6250a0e341eb052485a"
P = "frontend/src/app/pages/"
VIEWS = ("cases", "sessions", "profile", "security", "monitor")
LABELS = ("casos", "sesiones", "perfil", "seguridad", "monitor")
FILES = tuple(P + "platform-operator-" + view + ".component.html" for view in VIEWS)
CSS = P + "platform-operator-f04.component.css"
CSS_PREMIUM = P + "platform-operator-f04-premium.component.css"
MODEL = P + "platform-operator-views.component.ts"
TEST = "frontend/tests/c05-f04-operators.spec.ts"
AUDIT = "qa/c05_ux_f04_premium_audits.py"
DOC = "docs/c05/ux/F04_PREMIUM_95_AUDITORIAS_2026_11_23.md"
WORKFLOW = ".github/workflows/c05-ux-f04-premium-9p5.yml"
ALLOWED = set(FILES) | {CSS, CSS_PREMIUM, MODEL, TEST, AUDIT, DOC, WORKFLOW}


def need(ok: bool, message: str) -> None:
    if not ok:
        raise AssertionError("F04 PREMIUM NO-GO: " + message)


def git(*args: str) -> str:
    p = subprocess.run(["git", *args], cwd=ROOT, text=True, capture_output=True)
    need(p.returncode == 0, "git: " + p.stderr[-200:])
    return p.stdout


def read(path: str) -> str:
    f = ROOT / path
    need(f.is_file(), "missing " + path)
    return f.read_text("utf-8")


def scope() -> None:
    need(git("rev-parse", BASE).strip() == BASE, "baseline unavailable")
    changed = set(git("diff", "--name-only", BASE + "...HEAD").splitlines())
    need(changed == ALLOWED, "diff mismatch " + str(sorted(changed ^ ALLOWED)))
    need(not any(s.startswith(("backend/", "database/", ".agents/", "docs/MASTER_REFERENCE",
                                "frontend/src/app/identity/", "frontend/src/styles.css"))
                 for s in changed), "security boundary crossed")


def A() -> None:
    scope()
    for view, label, path in zip(VIEWS, LABELS, FILES):
        before, after = git("show", BASE + ":" + path), read(path)
        tags = lambda t: re.findall(r"</?([a-z][a-z0-9-]*)\b", t)
        need(tags(before) == tags(after), "DOM structure changed: " + view)
        need("operator-workspace--" + label in after, "scoped area absent: " + view)
        need("operator-section-head" in after, "visible hierarchy absent: " + view)
    for mark in ("operator-card--lookup", "operator-case-summary",
                 "operator-task--review", "operator-task--sensitive"):
        need(mark in read(FILES[0]), "case task absent: " + mark)
    print("F04/A PASS: five view DOMs, exact-scope and task hierarchy")


def B() -> None:
    scope()
    for view, path in zip(VIEWS, FILES):
        before, after = git("show", BASE + ":" + path), read(path)
        for p in (r"<form\b", r"<input\b", r"<select\b", r"\brequired\b",
                  r"<cs-institutional-modal\b", r"\(click\)=", r"\(submit\)="):
            need(len(re.findall(p, before)) == len(re.findall(p, after)),
                 "protected controls changed: " + view + " " + p)
    cases, sessions, security, monitor = (read(FILES[i]) for i in (0, 1, 3, 4))
    for marker in ('name="decision" required', 'name="confirmed" type="checkbox" required',
                   'name="verified" type="checkbox" required', 'name="evidenceHash" required',
                   'name="reason" required', 'name="code" required'):
        need(marker in cases, "review confirmation/MFA missing: " + marker)
    need("revokeSessionRequested.emit(session.id)" in sessions,
         "selected session ID lost")
    need("action: 'revoke-all'" in sessions and 'name="code" required' in sessions,
         "session all-revoke MFA removed")
    need("policy.canManage" in security and "expectedRevision" in security and
         'name="code" type="password" required' in security,
         "session policy authorization/version/MFA removed")
    need("item.label" in monitor and "item.priority" in monitor and
         "operatorAbuseViews" in read(P + "platform-operator-views.component.ts"),
         "aggregated allowlist no longer used")
    need("getByRole('navigation', { name: 'Mi cuenta' })" in read(TEST),
         "separate business-realm negative missing")
    print("F04/B PASS: permissions, MFA, sessions, case review and privacy unchanged")


def C() -> None:
    scope()
    css, tests = read(CSS_PREMIUM), read(TEST)
    need("platform-operator-f04-premium.component.css" in read(MODEL),
         "five views do not load split CSS")
    for marker in ("operator-card--lookup", "operator-action--danger",
                   "operator-card--profile", "operator-security-actions",
                   "operator-stat--total", "max(44px", "forced-colors: active",
                   "prefers-reduced-motion: reduce"):
        need(marker in css, "visual pattern missing: " + marker)
    need("@import" not in css and "url(https:" not in css and
         "url(http:" not in css, "unsafe external resource")
    need("F04 PREMIUM" in tests and "operator tabs need a 44px tactile target" in tests,
         "5x5 tactile browser tests missing")
    need("0 P0/P1" in read(DOC), "risk threshold unspecified")
    print("F04/C PASS_source_only: scoped responsive design and 25 browser assertions")


if __name__ == "__main__":
    try:
        need(len(sys.argv) == 2 and sys.argv[1] in ("A", "B", "C"),
             "expected A|B|C")
        {"A": A, "B": B, "C": C}[sys.argv[1]]()
    except (AssertionError, OSError, KeyError, ValueError) as exc:
        print(str(exc), file=sys.stderr)
        raise SystemExit(1) from exc
