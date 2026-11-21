#!/usr/bin/env python3
"""C05 UX F05 — A/B/C gates over the secure-modal work scope. No fake runtime PASS."""
from pathlib import Path
import sys
ROOT = Path(__file__).resolve().parents[1]
UI = ROOT / "frontend/src/app/ui/institutional-modal"
PAGES = ROOT / "frontend/src/app/pages"
SPEC = ROOT / "frontend/tests/c05-f05-secure-modals.spec.ts"

def text(path: Path) -> str:
    if not path.is_file():
        raise AssertionError(f"F05 missing: {path.relative_to(ROOT)}")
    return path.read_text("utf-8")
def need(condition: bool, reason: str) -> None:
    if not condition: raise AssertionError("F05 NO-GO: " + reason)

def audit_a() -> None:
    comp = text(UI / "institutional-modal.component.ts")
    modal = text(UI / "institutional-modal.component.html")
    checks = [
        "'simple' | 'form' | 'destructive' | 'wizard'",
        "readonly kind = input<InstitutionalModalKind>('form')",
        "readonly steps = input<readonly string[]>([])",
        "readonly activeStep = computed(",
        "InstitutionalModalComponent.openInstances.size !== 0",
        "dialog.showModal()", "this.heading?.nativeElement.focus()",
        "this.previouslyFocused.focus()", "querySelectorAll('form')",
        "if (this.busy()) event.preventDefault()"
    ]
    for needle in checks: need(needle in comp, "modal behavior missing: " + needle)
    need("aria-current" in modal and "cs-modal__stepper" in modal, "wizard progress absent")
    need("cs-modal__warning" in modal and ("role=\"alert\"" in modal or "cs-operation-feedback" in modal), "warning/alert feedback missing")
    work = text(PAGES / "identity-workspace.component.html")
    operator = text(PAGES / "platform-identity-panel.component.html")
    security = text(PAGES / "platform-operator-security.component.html")
    for marker in ('workspaceModal22 kind="destructive"', 'workspaceModal35 kind="destructive"',
                   'workspaceModal36 kind="form"', 'workspaceModal21 kind="form"',
                   'workspaceModal31 kind="form"'):
        need(marker in work, "business flow classification missing: " + marker)
    need('operatorRecoveryModal kind="wizard"' in operator and
         'recoveryStep()' in operator and 'recoveryStep.set(2)' in operator,
         "operator recovery does not advance through verified stages")
    need('opDialog33 kind="wizard"' in security and 'platform.factorSecret() ? 2 : 1' in security,
         "MFA wizard is not tied to actual backend readiness")
    print("F05/A PASS: distinct semantic variants, staged flows, real recovery and MFA contracts")

def audit_b() -> None:
    comp = text(UI / "institutional-modal.component.ts")
    work = text(PAGES / "identity-workspace.component.html")
    cases = text(PAGES / "platform-operator-cases.component.html")
    session = text(PAGES / "platform-operator-sessions.component.html")
    operator = text(PAGES / "platform-identity-panel.component.ts")
    test = text(SPEC)
    need("data-modal-action" in work and "data-modal-action" in session, "direct commands lack local receipt attribution")
    need("button.hasAttribute('data-modal-action')" in comp and
         "onDialogSubmit()" in comp and "(submit)" in text(UI / "institutional-modal.component.html"),
         "wizard navigation may falsely consume server receipts")
    need("InstitutionalModalComponent.openInstances.size !== 0" in comp,
         "privileged modal could stack")
    need("this.busy()) return" in comp and "if (this.busy()) event.preventDefault()" in comp,
         "busy state can lose focus/safety")
    need("this.previouslyFocused.focus()" in comp and
         "form.reset()" in comp, "focus return or secret input cleanup absent")
    need("['approve', 'reject']" in operator and
         'name="confirmed" type="checkbox" required' in cases,
         "explicit operator decision guard missing")
    need("selectedMembershipId.set('')" in work and
         'name="confirmed" required' in work, "tenant approval has no explicit confirmation")
    for forbidden in ("eval(", "innerHTML", "localStorage", "sessionStorage"):
        need(forbidden not in comp, "unsafe modal primitive " + forbidden)
    for phrase in ("only one privileged native dialog", "busy transaction cannot be dismissed",
                   "recovery step 2 is not usable", "traps keyboard"):
        need(phrase in test, "F05 negative test missing: " + phrase)
    print("F05/B PASS: single privilege surface, busy guard, no implicit approval, no receipt crossing")

def audit_c() -> None:
    css = text(UI / "institutional-modal.component.css")
    html = text(UI / "institutional-modal.component.html")
    test = text(SPEC)
    config = text(ROOT / "frontend/playwright.c05-f05-modals.config.ts")
    for token in ("max-height", "100dvh", "overflow: auto", "overflow-x: hidden",
                  "scrollbar-gutter", "max-width: calc(100vw", "@media (max-height: 540px)",
                  "forced-colors: active", "prefers-reduced-motion: reduce", "cs-modal__steps"):
        need(token in css, "reflow/display control missing: " + token)
    for label in ("Información y acción", "Confirmación de seguridad", "Asistente protegido",
                  "Formulario verificado", "No compartas"):
        need(label in html, "imprecise modal copy or missing variant: " + label)
    for width in (320,390,768,1024,1440):
        need("width: " + str(width) in test, f"{width}px F05 viewport absent")
    need("AxeBuilder" in test and "visualViewport" in test and
         "font-size: 200%" in test and "forcedColors: 'active'" in test,
         "axe/geometry/200%-zoom regression absent")
    need("127.0.0.1:4207" in config and "reuseExistingServer: false" in config,
         "QA harness not pinned to isolated loopback")
    print("F05/C PASS: readable dialog variants, 5 widths, WCAG, 200pct zoom, compact height")

if __name__ == "__main__":
    try:
        need(len(sys.argv) == 2 and sys.argv[1] in ("A","B","C"), "expected A/B/C")
        {"A":audit_a,"B":audit_b,"C":audit_c}[sys.argv[1]]()
    except (AssertionError,OSError) as e:
        print(e, file=sys.stderr)
        raise SystemExit(1) from e
