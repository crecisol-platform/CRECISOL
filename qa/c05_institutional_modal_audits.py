#!/usr/bin/env python3
"""CRECISOL C05 institutional UI: bounded A/B/C documentary + source gates.

Does not substitute running Angular, Chromium, .NET or MySQL. Tests and
certification require the actual CI results on the exact head SHA.
"""
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
FRONT = ROOT / "frontend/src/app"
UI = FRONT / "ui/institutional-modal"
PAGES = FRONT / "pages"
OPERATOR = [
    "platform-identity-panel.component.html",
    *[f"platform-operator-{name}.component.html"
      for name in ("cases", "security", "sessions", "profile", "monitor")],
]


def read(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def require(condition: bool, reason: str) -> None:
    if not condition:
        raise AssertionError("C05 UX NO-GO: " + reason)


def guard_modal(source: str) -> None:
    required = (
        "dialog.showModal()", "if (this.busy()) return;",
        "if (this.busy()) event.preventDefault();",
        "this.previouslyFocused.focus()",
        "querySelectorAll('form').forEach(form => form.reset())",
        "readonly closeOnSuccess = input(false)",
        "this.submittedWhileOpen",
    )
    for fragment in required:
        require(fragment in source, "native-modal safety control missing: " + fragment)
    forbidden = ("innerHTML", "outerHTML", "insertAdjacentHTML",
                 "localStorage", "sessionStorage", "eval(", "window.open(")
    for fragment in forbidden:
        require(fragment not in source, "unsafe DOM or local persistence in modal: " + fragment)


def audit_a() -> None:
    operator = "\n".join(read(PAGES / path) for path in OPERATOR)
    workspace = read(PAGES / "identity-workspace.component.html")
    require(operator.count("<form ") == 18, "18 original operator forms not preserved")
    require(workspace.count("<form ") == 15, "15 original consumer account forms not preserved")
    for field in ("caseId", "verified", "evidenceHash", "reason",
                  "shareA", "shareB", "currentPassword", "newPassword",
                  "mailbox", "code", "token"):
        require(f'name="{field}"' in operator, "operator critical field lost: " + field)
    for field in ("membershipId", "factorId", "branchId", "resourceId",
                  "currentPassword", "newPassword", "mailbox", "code", "confirmed"):
        require(f'name="{field}"' in workspace, "account critical field lost: " + field)
    panel = read(PAGES / "platform-identity-panel.component.html")
    for tab in ("casos", "sesiones", "perfil", "seguridad", "monitor"):
        require(f"panel.set('{tab}')" in panel, "operator task tab missing: " + tab)
    require("panel-operator" not in workspace, "business account leaked platform view")
    require("Registrar decisión" in workspace, "private approval lost")
    require("Comprobar un acceso privado" in workspace and
            '(click)="privateAccessModal.open()"' in workspace and
            "action: 'private'" in workspace,
            "private resource action must have an accessible modal trigger")
    require("Revisar identidad del titular" in operator, "operator reviewer flow lost")
    require("Emitir parte de recuperación" in operator, "handoff flow lost")
    require("C06" not in panel, "unauthorized future chapter exposed in C05")
    print("PASS C05-UI-A: 18 operator forms, 15 account forms, task navigation and authority boundaries")


def audit_b() -> None:
    modal = read(UI / "institutional-modal.component.ts")
    guard_modal(modal)
    tpl = read(UI / "institutional-modal.component.html")
    require("<dialog" in tpl and "(cancel)=\"onCancel($event)\"" in tpl and
            "(close)=\"onClosed()\"" in tpl,
            "native dialog lifecycle not wired")
    require("role=\"alert\"" in tpl and "aria-live=\"polite\"" in tpl,
            "status or failure announcement missing")
    cases = read(PAGES / "platform-operator-cases.component.html")
    require("(dismissed)=\"platform.handoffShare.set('')\"" in cases,
            "operator recovery secret not cleared when its modal closes")
    share = cases.split('<cs-institutional-modal #opDialog22', 1)
    require(len(share) == 2 and 'aria-label="Parte de recuperación del operador"' in
            share[1].split('</cs-institutional-modal>', 1)[0],
            "operator share must stay in active dialog")
    require(cases.count('aria-label="Parte de recuperación del operador"') == 1,
            "operator share must not leak outside dialog")
    for src in (
        modal.replace("dialog.showModal()", "dialog.show()", 1),
        modal.replace("if (this.busy()) event.preventDefault();", "void event;", 1),
        modal.replace("querySelectorAll('form').forEach(form => form.reset())", "querySelectorAll('form')", 1),
        modal.replace("this.previouslyFocused.focus()", "this.previouslyFocused.blur()", 1),
        modal.replace("if (this.busy()) return;", "if (false) return;", 1),
        modal + "\nconst persisted = window.localStorage;\n",
    ):
        try:
            guard_modal(src)
        except AssertionError:
            continue
        raise AssertionError("C05 UX NO-GO: a deliberately unsafe modal mutation was accepted")
    print("PASS C05-UI-B: focus, busy state, private form cleanup and 6 negative mutations")


def audit_c() -> None:
    ts = read(UI / "institutional-modal.component.ts")
    css = read(UI / "institutional-modal.component.css")
    global_css = read(ROOT / "frontend/src/styles.css")
    view_src = read(PAGES / "identity-flow-views.component.ts")
    panel_src = read(PAGES / "platform-operator-views.component.ts")
    workspace = read(PAGES / "identity-workspace.component.html")
    require("InstitutionalModalComponent" in view_src and
            "InstitutionalModalComponent" in panel_src,
            "standalone modal not imported in business/operator views")
    require(workspace.count("<cs-institutional-modal") >= 12,
            "consumer forms still exposed as stacked technical controls")
    require("::backdrop" in css and "max-height" in css and
            "@media (max-width: 480px)" in css and
            "@media (prefers-reduced-motion: reduce)" in css and
            "@media (forced-colors: active)" in css,
            "responsive, keyboard, motion or contrast guard missing")
    require("body:has(dialog.cs-modal[open])" in global_css, "backdrop scrolling not bounded")
    require("https://" not in css and "@import" not in css, "remote CSS dependency forbidden")
    require("inert" not in ts or "showModal" in ts, "unbounded pseudo-modal")
    require("<cs-platform-operator-cases" in read(PAGES / "platform-identity-panel.component.html"),
            "operator case component missing")
    require("['frontend']" not in ts, "modal should not reference runtime configuration")
    print("PASS C05-UI-C: modular Angular 22, native modal, responsive style and no remote styling")


if __name__ == "__main__":
    try:
        require(len(sys.argv) == 2 and sys.argv[1] in ("A", "B", "C"),
                "expected a single audit A|B|C")
        {"A": audit_a, "B": audit_b, "C": audit_c}[sys.argv[1]]()
    except (AssertionError, OSError) as error:
        print("FAIL:", error, file=sys.stderr)
        sys.exit(1)
