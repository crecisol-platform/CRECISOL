#!/usr/bin/env python3
"""CRECISOL C05 F03 premium six-account-view scope, security and UX audits.

These static gates do not replace Angular/Chromium/.NET/MySQL execution or
an independent visual rating. Runs against a fixed F01/F02/F06 candidate base.
"""
from __future__ import annotations

import csv
import json
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
BASE = "42409d0bce6c70f4fb6f25679340063a43b61a44"
PORTAL = "frontend/src/app/pages/identity-portal.component.html"
PORTAL_CSS = "frontend/src/app/pages/identity-portal.component.css"
WORKSPACE = "frontend/src/app/pages/identity-workspace.component.html"
WORKSPACE_CSS = "frontend/src/app/pages/identity-workspace.component.css"
TEST = "frontend/tests/c05-f03-account.spec.ts"
SCRIPT = "qa/c05_ux_f03_premium_audits.py"
DOC = "docs/c05/ux/F03_PREMIUM_95_AUDITORIAS_2026_11_22.md"
WORKFLOW = ".github/workflows/c05-ux-f03-premium-9p5.yml"
ALLOWED = {PORTAL, PORTAL_CSS, WORKSPACE, WORKSPACE_CSS,
           TEST, SCRIPT, DOC, WORKFLOW}
VIEWS = ("resumen", "perfil", "llaves", "seguridad", "historial",
         "administracion")


def require(check: bool, why: str) -> None:
    if not check:
        raise AssertionError("F03 premium NO-GO: " + why)


def git(*args: str) -> str:
    p = subprocess.run(["git", *args], cwd=ROOT, text=True, encoding="utf-8",
                       capture_output=True, check=False)
    require(p.returncode == 0, "git " + " ".join(args) + " " + p.stderr[-200:])
    return p.stdout


def read(path: str) -> str:
    item = ROOT / path
    require(item.is_file(), "missing " + path)
    return item.read_text(encoding="utf-8")


def prior(path: str) -> str:
    return git("show", BASE + ":" + path)


def scope() -> list[str]:
    require(git("rev-parse", BASE).strip() == BASE, "base SHA not available")
    changed = git("diff", "--name-only", BASE + "...HEAD").strip().splitlines()
    require(set(changed) == ALLOWED,
            "scope mismatch: " + str(sorted(set(changed) ^ ALLOWED)))
    for forbidden in ("backend/", "database/", ".agents/", "docs/MASTER",
                      "frontend/src/styles.css", "frontend/src/app/identity/"):
        require(not any(p.startswith(forbidden) for p in changed),
                "forbidden cross-boundary change " + forbidden)
    return changed


def tags(markup: str) -> list[str]:
    return re.findall(r"</?([a-z][a-z0-9-]*)\b", markup)


def audit_a() -> None:
    changed = scope()
    original = prior(WORKSPACE)
    current = read(WORKSPACE)
    require(tags(original) == tags(current),
            "account HTML element structure changed")
    require(tags(prior(PORTAL)) == tags(read(PORTAL)),
            "portal HTML element structure changed")
    require(current.count('<cs-institutional-modal') == 15,
            "secure modal count changed")
    require('[attr.data-view]="view()"' in current,
            "six-view CSS scoping marker absent")
    for name in VIEWS[1:]:
        require("@case ('" + name + "')" in current,
                "missing workspace branch " + name)
    portal = read(PORTAL)
    require(portal.count("routerLink=\"/cuenta\"") >= 6 and
            'aria-label="Mi cuenta"' in portal,
            "six real navigation links unavailable")
    with (ROOT / "docs/c05/ux/F06_65_SUPERFICIES_VALORACION.csv").open(
        encoding="utf-8", newline="") as source:
        ids = [row["id"] for row in csv.DictReader(source)]
    expected = {f"C05-F01-{i:03d}" for i in range(1, 66)}
    require(len(ids) == 65 and set(ids) == expected, "65 inventory IDs changed")
    print(json.dumps({"A": "PASS", "files": changed, "views": len(VIEWS),
                      "inventoried_surfaces": len(ids)}))


def audit_b() -> None:
    scope()
    before = prior(WORKSPACE)
    after = read(WORKSPACE)
    # Markup may change wording only, never forms, fields, MFA or handlers.
    for pattern in (r"<form\b", r"<input\b", r"<select\b",
                    r"\brequired\b", r"<cs-institutional-modal\b",
                    r"\[disabled\]=", r"\(submit\)=", r"\(click\)=",
                    r"\(change\)="):
        require(len(re.findall(pattern, before)) ==
                len(re.findall(pattern, after)),
                "security-related markup count changed: " + pattern)
    for marker in (
        "name=\"confirmed\" required", "name=\"membershipId\" required",
        "name=\"decision\" required", "name=\"reason\" required",
        "name=\"code\" autocomplete=\"one-time-code\"",
        "pendingRequested.emit(false)", "pendingRequested.emit(true)",
        "selectedMembershipId.set(request.id)", "revokeRequested.emit(session.id)",
        "logoutRequested.emit()", "identity.recoveryCodes()",
        "passkeyRows()", "identity.pendingDenied()",
    ):
        require(marker in after, "protected action removed: " + marker)
    for path in (PORTAL_CSS, WORKSPACE_CSS):
        css = read(path).lower()
        require(not any(x in css for x in (
            "@import", "expression(", "javascript:", "url(http:",
            "url(https:", "data:text/html")),
            "unsafe external CSS resource in " + path)
        require("forced-colors: active" in css,
                "forced-colors safeguard missing " + path)
    print(json.dumps({"B": "PASS", "backend_or_database_modified": False,
                      "mfa_and_membership_markup_preserved": True}))


def audit_c() -> None:
    scope()
    portal = read(PORTAL_CSS)
    workspace = read(WORKSPACE_CSS)
    tests = read(TEST)
    require("Seis áreas · Desliza o usa las flechas para ver más." in read(PORTAL),
            "mobile navigation discovery copy missing")
    for view in VIEWS:
        require("data-view='" + view + "'" in workspace,
                "CSS treatment missing for " + view)
    for marker in ("scroll-padding-inline", "account-nav--workspace",
                   "var(--cs-hit-target", "forced-colors: active"):
        require(marker in portal, "responsive navigation contract missing " + marker)
    for marker in ("identity-key-revoke", "identity-activity-item",
                   "identity-pending-card", "identity-device-card",
                   "identity-panel-heading", "identity-task-caption"):
        require(marker in workspace, "premium view layout missing " + marker)
    require("F03 PREMIUM six-view geometry" in tests and
            "F03 PREMIUM membership review preserves MFA" in tests,
            "premium browser assertions missing")
    require("9.5" in read(DOC) and "65" in read(DOC),
            "technical and editorial gates not documented")
    print(json.dumps({"C": "PASS_source_only",
                      "responsive_sizes_to_test": [320, 390, 768, 1024, 1440],
                      "premium_browser_scenarios": 13,
                      "institutional_9_5_score_issued": False}))


if __name__ == "__main__":
    try:
        require(len(sys.argv) == 2 and sys.argv[1] in ("A", "B", "C"),
                "usage: A|B|C")
        {"A": audit_a, "B": audit_b, "C": audit_c}[sys.argv[1]]()
    except (AssertionError, OSError, ValueError, KeyError, csv.Error) as error:
        print(str(error), file=sys.stderr)
        raise SystemExit(1) from error
