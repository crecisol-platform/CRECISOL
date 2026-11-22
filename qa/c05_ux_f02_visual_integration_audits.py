#!/usr/bin/env python3
"""CRECISOL C05 F02 visual integration audits (source/CI, no buyer rating).

Scope-locked to the last proven F06 branch head; fail-closed on any functional,
authorization, API, tenant, DTO or storage change. Browser/DB tests are separate.
"""
from __future__ import annotations
import csv
import json
import re
import subprocess
import sys
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
BASE = "d539b75d13cde64c8ea3279d33ab35b6349c1adf"
FRONT = "frontend/src/app/"
MODAL = FRONT + "ui/institutional-modal/institutional-modal.component.css"
FEEDBACK = FRONT + "ui/feedback/operation-feedback.component.css"
OPERATOR = FRONT + "pages/platform-operator-f04.component.css"
FORMS = FRONT + "pages/identity-flow.component.css"
TEST = "frontend/tests/c05-f06-context-feedback.spec.ts"
WORKFLOW = ".github/workflows/c05-ux-f02-paridad-visual.yml"
DOC = "docs/c05/ux/F02_PARIDAD_VISUAL_INTEGRACION_2026_11_22.md"
SCRIPT = "qa/c05_ux_f02_visual_integration_audits.py"
ALLOWED = {MODAL, FEEDBACK, OPERATOR, FORMS, TEST, WORKFLOW, DOC, SCRIPT}
EDITED_CSS = (MODAL, FEEDBACK, OPERATOR, FORMS)
EXPECTED = {f"C05-F01-{i:03d}" for i in range(1, 66)}


def require(ok: bool, reason: str) -> None:
    if not ok:
        raise AssertionError("F02 visual parity NO-GO: " + reason)


def read(path: str) -> str:
    target = ROOT / path
    require(target.is_file(), "missing required file " + path)
    return target.read_text(encoding="utf-8")


def git(*args: str) -> str:
    p = subprocess.run(["git", *args], cwd=ROOT, text=True,
                       encoding="utf-8", capture_output=True, check=False)
    require(p.returncode == 0, "git failed " + " ".join(args) + " " + p.stderr[-250:])
    return p.stdout


def scope() -> list[str]:
    require(git("rev-parse", BASE).strip() == BASE, "missing frozen F06 source commit")
    files = git("diff", "--name-only", BASE + "...HEAD").strip().splitlines()
    require(set(files) <= ALLOWED,
            "out-of-scope changes: " + str(sorted(set(files) - ALLOWED)))
    require(all(x in files for x in EDITED_CSS + (TEST, DOC, SCRIPT, WORKFLOW)),
            "incomplete implementation/QA/acta")
    return files


def audit_a() -> None:
    files = scope()
    with (ROOT / "docs/c05/ux/F06_65_SUPERFICIES_VALORACION.csv").open(
            encoding="utf-8", newline="") as src:
        surfaces = list(csv.DictReader(src))
    require(len(surfaces) == 65 and {x["id"] for x in surfaces} == EXPECTED,
            "catalogue lost an ID")
    require(Counter(x["tipo"] for x in surfaces) ==
            {"vista": 17, "estado": 18, "complemento": 3, "modal": 27},
            "surface type count drift")
    templates = {x["fuente_angular"] for x in surfaces}
    require(len(templates) == 11 and
            all((ROOT / x).is_file() for x in templates),
            "source HTML mapping incomplete")
    for path in EDITED_CSS:
        require(path in files and len(read(path)) > 500,
                "no concrete visual change in " + path)
    require("65" in read(DOC) and "9.5" in read(DOC),
            "deliverables must name full coverage and non-certified threshold")
    print(json.dumps({"A": "PASS", "surface_ids": len(surfaces),
                      "source_templates": len(templates), "changed_files": files}))


def audit_b() -> None:
    scope()
    # This microtask is presentational only. All security-critical components,
    # controllers, APIs, tests of tenant separation and session policy are untouched.
    for path in EDITED_CSS:
        css = read(path)
        for bad in ("@import", "expression(", "javascript:", "behavior:",
                    "url(http:", "url(https:", "data:text/html"):
            require(bad not in css.lower(),
                    "external source or executable content: " + path + "/" + bad)
        require("forced-colors: active" in css, "forced colors missing " + path)
        require("prefers-reduced-motion: reduce" in css or path == OPERATOR,
                "reduced-motion safeguard missing " + path)
    feedback = read(FEEDBACK)
    modal = read(MODAL)
    form = read(FORMS)
    for key in ("--cs-color-info", "--cs-color-info-bg", "--cs-color-success",
                "--cs-color-success-bg", "--cs-color-danger",
                "--cs-color-danger-bg", "--cs-color-warning"):
        require(key in feedback + modal,
                "required F02 semantic token " + key + " unused")
    require("aria-invalid='true'" in form and
            "var(--cs-color-danger" in form and
            "input:focus-visible" in form,
            "unsafe or invisible invalid/focus state")
    for piece in ("dialog", "scrollbar-gutter", "overflow: auto",
                  "max-height", "100dvh", ".cs-modal__footer",
                  ".cs-modal__scroll-cue", 'data-kind="destructive"'):
        require(piece in modal, "secure modal functionality removed " + piece)
    require("cs-operation-feedback" in read(TEST) and
            "aria-invalid" in read(TEST),
            "real semantic feedback states not tested")
    print(json.dumps({"B": "PASS", "authorization_backend_modified": False,
                      "tenant_and_mfa_contract_modified": False,
                      "sensitive_browser_storage_added": False}))


def audit_c() -> None:
    scope()
    css = {p: read(p) for p in EDITED_CSS}
    refs = {p: len(re.findall(r"var\(--cs-[a-z0-9-]+", text))
            for p, text in css.items()}
    thresholds = {MODAL: 40, FEEDBACK: 15, OPERATOR: 40, FORMS: 35}
    for path, threshold in thresholds.items():
        require(refs[path] >= threshold,
                path + " still detached from F02 (token usages: " + str(refs[path]) + ")")
    form = css[FORMS]
    feedback = css[FEEDBACK]
    modal = css[MODAL]
    operator = css[OPERATOR]
    require("var(--cs-color-action" in form and
            "var(--cs-color-text-strong" in form and
            "var(--cs-color-focus" in form,
            "forms not aligned with action, body and focus tokens")
    for name in ("success", "danger", "info"):
        require("var(--cs-color-" + name in feedback,
                "feedback semantic palette missing " + name)
    require("var(--cs-color-warning" in modal and
            "var(--cs-color-danger" in modal and
            "var(--cs-color-focus" in modal,
            "modal destructive/warning/focus states diverge")
    require("var(--cs-color-border" in operator and
            "var(--cs-color-surface" in operator and
            "var(--cs-hit-target" in operator,
            "platform UI not sharing color/target contract")
    for marker in ("async function assertF02VisualParity(",
                   "await assertF02VisualParity(page, selected);",
                   "getComputedStyle(", "feedback accent matches F02 token",
                   "secure modal uses F02 institutional surface",
                   "platform operator card uses shared F02 surface"):
        require(marker in read(TEST), "computed-style browser assertion missing " + marker)
    # The source scanner cannot substitute for real computed styles or visual
    # appraisal. These must run in Chromium and be evaluated individually.
    print(json.dumps({"C": "PASS_source_parity_only",
                      "token_refs_by_path": refs,
                      "chromium_computed_style_tests_executed_by_this_script": False,
                      "commercial_9_5_grades_issued": 0,
                      "visual_acceptance": "NO_GO"},
                     ensure_ascii=False))


if __name__ == "__main__":
    try:
        require(len(sys.argv) == 2 and sys.argv[1] in ("A", "B", "C"),
                "usage: A|B|C")
        {"A": audit_a, "B": audit_b, "C": audit_c}[sys.argv[1]]()
    except (AssertionError, OSError, ValueError, csv.Error, KeyError) as exc:
        print(str(exc), file=sys.stderr)
        raise SystemExit(1) from exc
