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
    # Fail closed when a security guard appears only inside a TypeScript comment.
    # A previous red Chromium run exposed exactly this false-positive pattern.
    open_body = comp.split("  open(): void {", 1)[1].split("  close(): void {", 1)[0]
    executable = "\n".join(line.split("//", 1)[0] for line in open_body.splitlines())
    need("InstitutionalModalComponent.openInstances.size !== 0 ||" in executable and
         "document.querySelector('dialog:modal')" in executable,
         "other native modal guard missing or commented out")
    need("does not stack over a native session or security dialog" in test,
         "cross-component native modal regression absent")
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
    need('aria-live="polite"' in html and 'cs-modal__step-announcement' in html,
         "wizard step changes are not announced to assistive technology")
    need(".cs-modal__dismiss { width: 2.75rem; height: 2.75rem; }" in css and
         ".cs-modal__cancel { min-height: 2.75rem; }" in css,
         "minimum close targets were reduced")
    need("target.boundingBox()" in test and "toBeGreaterThanOrEqual(43.75)" in test,
         "F05 responsive touch target regression absent")
    for marker in ("F05 DEEP ", "deepViewports", "scroller.scrollHeight", "data-feedback-kind=\"pending\"",
                   "data-feedback-kind=\"error\"", "data-feedback-kind=\"success\"",
                   "closedByDesign", "lastElementChild", "scenario + ' final content"):
        need(marker in test, "full modal state/scroll regression removed: " + marker)
    print("F05/C PASS: 11 full modal states at mobile and desktop, scroll, keyboard and 5 original widths")


def deep_evidence(root: Path) -> None:
    """Fail closed: 11 scenarios x 3 widths x 5 states, exact runner SHA and PNG."""
    import json
    import os
    import struct
    import hashlib
    scenes = (
        "recuperacion-operador-solicitud", "recuperacion-operador-doble-control",
        "mfa-operador-verificacion", "mfa-operador-vinculacion",
        "passkey-registro", "passkey-revocacion", "cambio-contrasena",
        "aprobacion-membresia", "invitacion", "revocacion-sesion",
        "renovacion-sesion"
    )
    heights = {320: 720, 390: 844, 1440: 900}
    states = ("top", "bottom", "pending", "error", "result")
    expected = {(scene, width, state)
                for scene in scenes for width in heights for state in states}
    sha = os.environ.get("GITHUB_SHA", "")
    need(len(sha) == 40, "F05 deep evidence requires exact runner SHA")
    need(root.is_dir(), "F05 deep Playwright output missing")
    files = sorted(root.rglob("f05deep-*.json"))
    need(len(files) == len(expected),
         f"F05 deep 165 expected state files, found {len(files)}")
    covered = set()
    manifest = []
    for file in files:
        metadata = json.loads(file.read_text("utf-8"))
        scene, state = metadata["scenario"], metadata["state"]
        viewport = metadata["viewport"]
        width, height = viewport["width"], viewport["height"]
        item = (scene, width, state)
        need(item in expected and item not in covered,
             "F05 deep duplicate or unknown scenario/state: " + str(item))
        covered.add(item)
        key = f"f05deep-{scene}-{width}-{state}"
        need(metadata["key"] == key and metadata["png"] == key + ".png",
             "F05 deep manifest name mismatch")
        need(height == heights[width], "F05 deep wrong mobile/desktop viewport")
        need(metadata["synthetic"] is True and
             metadata["demonstratesBackendAuthorization"] is False and
             metadata["runnerSha"] == sha,
             "F05 deep synthetic/exact SHA/authorization provenance mismatch")
        need(metadata["metrics"]["overflow"] <= 2,
             "F05 deep hidden horizontal overflow")
        if state != "result":
            need(metadata["closedByDesign"] is False,
                 "F05 deep modal closed before its result")
        png = file.with_name(metadata["png"])
        need(png.exists() and png.stat().st_size > 1024,
             "F05 deep state PNG missing/empty")
        raw = png.read_bytes()
        need(raw[:8] == b"\x89PNG\r\n\x1a\n" and raw[12:16] == b"IHDR",
             "F05 deep invalid PNG bytes")
        x, y = struct.unpack(">II", raw[16:24])
        need((x,y) == (width,height), "F05 deep PNG viewport mismatch")
        manifest.append({"scenario":scene, "width":width, "state":state,
                         "png":str(png.relative_to(root)),
                         "sha256":hashlib.sha256(raw).hexdigest(),
                         "closedByDesign":metadata["closedByDesign"]})
    need(covered == expected, "F05 deep state matrix not complete")
    report = {"status":"PASS", "runnerSha":sha, "synthetic":True,
              "backendAuthorizationDemonstrated":False,
              "count":len(manifest), "scenarios":len(scenes),
              "widths":list(heights), "states":list(states), "files":manifest}
    (root / "F05_DEEP_STATES_MANIFEST.json").write_text(
        json.dumps(report, ensure_ascii=False, indent=2), "utf-8")
    print(f"F05 DEEP evidence PASS: 11 scenarios x 3 mobile/desktop widths "
          f"x 5 full states = {len(manifest)} original PNG with SHA-256, {sha[:12]}")


if __name__ == "__main__":
    try:
        if len(sys.argv) == 2 and sys.argv[1] in ("A", "B", "C"):
            {"A":audit_a,"B":audit_b,"C":audit_c}[sys.argv[1]]()
        elif len(sys.argv) == 3 and sys.argv[1] == "deep-evidence":
            deep_evidence(Path(sys.argv[2]))
        else:
            raise AssertionError("F05 expected A/B/C or deep-evidence <folder>")
    except (AssertionError, OSError, ValueError, TypeError, KeyError) as e:
        print(e, file=sys.stderr)
        raise SystemExit(1) from e
