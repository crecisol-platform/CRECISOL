#!/usr/bin/env python3
"""C05 UX F06 — A/B/C static gates. Never equate static checks with runtime success."""
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
UI = ROOT / "frontend/src/app/ui/feedback"
PAGES = ROOT / "frontend/src/app/pages"
MODAL = ROOT / "frontend/src/app/ui/institutional-modal"
BASE = "b576d6ff64aea7d417c59aa851618dbf4dced68a"

ALLOWED = {
    "frontend/src/app/ui/feedback/operation-feedback.model.ts",
    "frontend/src/app/ui/feedback/operation-feedback.component.ts",
    "frontend/src/app/ui/feedback/operation-feedback.component.html",
    "frontend/src/app/ui/feedback/operation-feedback.component.css",
    "frontend/src/app/ui/feedback/secure-field-feedback.directive.ts",
    "frontend/src/app/ui/institutional-modal/institutional-modal.component.ts",
    "frontend/src/app/ui/institutional-modal/institutional-modal.component.html",
    "frontend/src/app/pages/identity-portal.component.ts",
    "frontend/src/app/pages/identity-portal.component.html",
    "frontend/src/app/pages/identity-portal.component.css",
    "frontend/src/app/pages/identity-flow-views.component.ts",
    "frontend/src/app/pages/platform-identity-panel.component.ts",
    "frontend/src/app/pages/platform-identity-panel.component.html",
    "frontend/src/app/pages/platform-operator-views.component.ts",
    "frontend/src/app/pages/identity-guest.component.html",
    "frontend/src/app/pages/identity-mfa.component.html",
    "frontend/src/app/pages/identity-recovery.component.html",
    "frontend/src/app/pages/identity-workspace.component.html",
    "frontend/src/app/pages/platform-operator-cases.component.html",
    "frontend/src/app/pages/platform-operator-profile.component.html",
    "frontend/src/app/pages/platform-operator-security.component.html",
    "frontend/src/app/pages/platform-operator-sessions.component.html",
    "frontend/src/styles.css",
    "frontend/tests/c05-f06-context-feedback.spec.ts",
    "frontend/playwright.c05-f06-feedback.config.ts",
    "qa/c05_ux_f05_modal_audits.py",
    "qa/c05_ux_f06_feedback_audits.py",
    "qa/c05_ux_f06_feedback_evidence.py",
    ".github/workflows/c05-ux-f06-feedback.yml",
    "docs/c05/ux/F06_MENSAJERIA_ESTADOS_MATRIZ_RIESGOS.md"
}

def text(path: str) -> str:
    file = ROOT / path
    assert file.is_file(), "F06 missing " + path
    return file.read_text("utf-8")

def need(condition: bool, reason: str) -> None:
    if not condition: raise AssertionError("F06 NO_GO: " + reason)

def scope() -> list[str]:
    changed = subprocess.check_output(["git", "diff", "--name-only", BASE + "...HEAD"],
                                      cwd=ROOT, text=True).strip().splitlines()
    need(bool(changed), "F06 diff absent")
    need(set(changed) <= ALLOWED, "out of scope: " + str(set(changed) - ALLOWED))
    for path in changed:
        # The validator itself contains malformed-token test literals by design.
        if path == "qa/c05_ux_f06_feedback_audits.py": continue
        if not path.endswith((".html",".css",".ts",".py",".md",".yml")): continue
        source = text(path)
        for malformed in ("\ufffd", "Ã¡", "Ã©", "Ã³", "Ã±", "Ãº", "Â¿", "Â¡", "â€¦", "â€”", "â€œ", "â€"):
            need(malformed not in source, "malformed Spanish/UTF-8 text in " + path)
    return changed

def audit_a() -> None:
    changes = scope()
    model = text("frontend/src/app/ui/feedback/operation-feedback.model.ts")
    portal = text("frontend/src/app/pages/identity-portal.component.ts")
    operator = text("frontend/src/app/pages/platform-identity-panel.component.ts")
    modal = text("frontend/src/app/ui/institutional-modal/institutional-modal.component.html")
    for key in ("contextualFeedback", "origin !== current", "activityLabel", "readonly kind"):
        need(key in model, "missing feedback contract: " + key)
    for code in (portal, operator):
        need("visibleFeedback = computed(" in code and "feedbackScope.set(origin)" in code,
             "not all realms have contextual feedback")
        need("feedbackGeneration" in code and "generation !== this.feedbackGeneration" in code,
             "stale asynchronous response not invalidated")
        need("IdentityApiService.publicError(caught)" in code,
             "raw API failure may appear in notifications")
    need("cs-operation-feedback" in modal, "dialogs are not using semantic feedback")
    need(len(changes) >= 15, "partial integration only")
    print("F06/A PASS: contextual async states, C05-only scope and Unicode text integrity")

def audit_b() -> None:
    scope()
    directive = text("frontend/src/app/ui/feedback/secure-field-feedback.directive.ts")
    modal = text("frontend/src/app/ui/institutional-modal/institutional-modal.component.ts")
    portal = text("frontend/src/app/pages/identity-portal.component.ts")
    operator = text("frontend/src/app/pages/platform-identity-panel.component.ts")
    for token in ("input.type !== 'password'", "one-time-code", "input.value = ''",
                  "invalid", "aria-invalid", "aria-describedby", "fieldValidationCopy",
                  "ngOnDestroy", "previousDescription", "this.form.noValidate = true",
                  "stopImmediatePropagation"):
        need(token in directive, "field privacy or accessibility missing: " + token)
    for code in (portal, operator):
        need("clearSensitiveInputValues(form);" in code and "this.feedbackScope.set('');" in code,
             "field cleanup or stale-message revocation missing")
    need("this.identity.recoveryCodes.set([])" in portal and
         "this.platform.recoveryCodes.set([])" in operator, "backup visibility persists across sections")
    for path in ["frontend/src/app/ui/feedback/operation-feedback.model.ts",
                 "frontend/src/app/ui/feedback/operation-feedback.component.ts",
                 "frontend/src/app/ui/feedback/secure-field-feedback.directive.ts"]:
        source = text(path)
        for forbidden in ("localStorage", "sessionStorage", "innerHTML", "eval(", "fetch("):
            need(forbidden not in source, "unsafe feedback primitive: " + forbidden)
    need("this.busy()) return" in modal and "actionStartedHere()" in modal,
         "modal bypass or false progress")
    print("F06/B PASS: no raw server errors, form secret scrubbing, stale/realm guards")

def audit_c() -> None:
    scope()
    css = text("frontend/src/app/ui/feedback/operation-feedback.component.css")
    html = text("frontend/src/app/ui/feedback/operation-feedback.component.html")
    global_css = text("frontend/src/styles.css")
    test = text("frontend/tests/c05-f06-context-feedback.spec.ts")
    for needle in ("prefers-reduced-motion: reduce", "forced-colors: active",
                   "grid-template-columns", "@keyframes feedback-enter", "overflow-wrap"):
        need(needle in css, "missing UI/motion system: " + needle)
    for needle in ("aria-live", "aria-busy", "aria-atomic", "kind() === 'error'"):
        need(needle in html, "missing live-region contract: " + needle)
    need("cs-field-feedback" in global_css and "aria-invalid" in global_css, "field validation not styled")
    need("clip-path: inset(50%)" in text("frontend/src/app/pages/identity-portal.component.css"),
         "skip link must be visually clipped when not focused without losing keyboard access")
    for width in (320,390,768,1024,1440):
        need("width: " + str(width) in test, "visual viewport missing " + str(width))
    for need_test in ("AxeBuilder", "reducedMotion: 'reduce'", "forcedColors: 'active'",
                      "font-size: 200%", "noHorizontalClip", "late response",
                      "scrubs password", "backup-code secrets"):
        need(need_test in test, "missing F06 regression: " + need_test)
    need("reuseExistingServer: false" in text("frontend/playwright.c05-f06-feedback.config.ts"),
         "test harness not isolated")
    print("F06/C PASS: accessible validation, editorial consistency and reduced motion")

if __name__ == "__main__":
    try:
        need(len(sys.argv) == 2 and sys.argv[1] in ("A","B","C"), "select audit A/B/C")
        {"A": audit_a, "B": audit_b, "C": audit_c}[sys.argv[1]]()
    except (OSError, AssertionError, subprocess.CalledProcessError) as error:
        print(error, file=sys.stderr)
        raise SystemExit(1) from error
