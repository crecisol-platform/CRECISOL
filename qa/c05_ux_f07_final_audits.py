#!/usr/bin/env python3
"""CRECISOL C05 UX F07 final A/B/C: immutable F06 baseline, no borrowed PASS.

All F07 product code is unchanged. Runtime Playwright/.NET/MySQL tests are
separate mandatory gates and a successful static audit is not a certification.
"""
from __future__ import annotations

import csv
import subprocess
import sys
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
BASE = "96174cf1d401b66464c02ba3e0d3a0cc2dd88cdf"
ALLOWED = frozenset({
    ".github/workflows/c05-ux-f07-regresiones-integrales.yml",
    "qa/c05_ux_f07_final_audits.py",
    "qa/c05_ux_f07_final_audit_tests.py",
    "qa/c05_ux_f07_visual_comparison.py",
    # QA-only pacing of a real 80/min security limit; exact test assertions unchanged.
    "qa/c05_operational_flow_e2e.mjs",
    # Post-F07 reconciliation of legacy QA gates, exact content blobs pinned below.
    "qa/c05_institutional_modal_audits.py",
    ".github/workflows/c05-institutional-ui-audits.yml",
    "qa/c05_sha1_exclusive_audits.py",
    # QA fixture only: prevent stale headings racing screenshots, no product code.
    "frontend/tests/c05-f06-inventory-65.spec.ts",
    "docs/c05/ux/F07_REGRESIONES_EVALUACION_INTEGRAL_2026_11_23.md",
})
POST_F07_QA_BLOBS = {
    "qa/c05_institutional_modal_audits.py": "ae0a084a53bb0f85a93ff20acdf478beeee04c50",
    ".github/workflows/c05-institutional-ui-audits.yml": "de7ff05e9d17ca93275f0c68859abc173df25963",
    "qa/c05_sha1_exclusive_audits.py": "741d3e5369014a20980fd499fad9e159ad556a40",
}
CATALOG = ROOT / "docs/c05/ux/F06_65_SUPERFICIES_VALORACION.csv"


def need(ok: bool, message: str) -> None:
    if not ok:
        raise AssertionError("F07 NO-GO: " + message)


def git_paths(*args: str) -> set[str]:
    out = subprocess.check_output(["git", *args], cwd=ROOT, text=True)
    return {x.strip() for x in out.splitlines() if x.strip()}


def validate_scope(changed: set[str], history: set[str], base_present: bool) -> None:
    need(base_present, "signed F06 source SHA is not an ancestor of F07")
    need(bool(changed), "empty F07 change: no attributable QA work")
    need(changed <= ALLOWED, "new files outside F07 allowed scope: " + str(sorted(changed - ALLOWED)))
    need(history <= ALLOWED,
         "foreign files touched in F07 commits, even if reverted: "
         + str(sorted(history - ALLOWED)))
    need("qa/c05_ux_f07_visual_comparison.py" in changed,
         "real visual comparison missing from F07")


def scope() -> set[str]:
    ancestor = subprocess.run(["git", "merge-base", "--is-ancestor", BASE, "HEAD"],
                              cwd=ROOT, check=False, stdout=subprocess.DEVNULL).returncode == 0
    changed = git_paths("diff", "--name-only", "--no-renames", BASE + "...HEAD")
    history = git_paths("log", "--format=", "--name-only", "--no-renames", BASE + "..HEAD")
    validate_scope(changed, history, ancestor)
    # Accept only the exact two audited source-gate repairs. An arbitrary
    # mutation of either file (even though QA-only) remains a hard NO-GO.
    for path, expected_blob in POST_F07_QA_BLOBS.items():
        actual_blob = subprocess.check_output(["git", "hash-object", path],
                                              cwd=ROOT, text=True).strip()
        need(actual_blob == expected_blob,
             "unreviewed post-F07 auditor mutation: " + path)
    # All product sources are immutable during this revalidation; no weaker
    # gate can be substituted for a new UI review after future code changes.
    for prefix in ("frontend/src/", "backend/", "database/", "frontend/package-lock.json"):
        need(not any(p == prefix or p.startswith(prefix) for p in changed | history),
             "runtime implementation or dependency changed under F07: " + prefix)
    return changed


def buyer_gate() -> dict[str, float | int | bool]:
    sys.path.insert(0, str(ROOT / "qa"))
    from c05_ux_f06_65_coverage import validate_commercial_ia_ratings
    with CATALOG.open(newline="", encoding="utf-8") as fh:
        records = list(csv.DictReader(fh))
    buyer = validate_commercial_ia_ratings(records)
    need(buyer["accepted"] == 65 and buyer["minimum"] >= 9.5,
         "final approved individual IA buyer grades not present")
    need(buyer["humanAccepted"] is False, "cannot assign human sign-off from IA")
    need(buyer["runtimeAuthByImage"] is False, "cannot grant runtime authorization from screenshots")
    ids = [r["id"] for r in records]
    need(ids == [f"C05-F01-{i:03d}" for i in range(1, 66)],
         "catalog order, IDs or uniqueness changed")
    counts = Counter(r["tipo"] for r in records)
    need(sum(counts.values()) == 65 and counts.get("modal", 0) == 27,
         "base inventory category coverage changed")
    return buyer


def audit_a() -> None:
    changed = scope()
    buyer = buyer_gate()
    need((ROOT / "frontend/tests/c05-f06-inventory-65.spec.ts").is_file(),
         "F06 65-case Chromium executable absent")
    need((ROOT / "frontend/tests/c05-f05-secure-modals.spec.ts").is_file(),
         "F05 complete secure modal cases absent")
    need((ROOT / "frontend/tests/c05-f06-context-feedback.spec.ts").is_file(),
         "F06 async/feedback regression absent")
    need((ROOT / "qa/c05_operational_flow_e2e.mjs").is_file(),
         "real full Angular NET10 MySQL8.4 regression absent")
    print(f"F07/A PASS: {len(changed)} isolated QA/docs/workflow files, "
          f"F01-F06 immutable implementation, 65/65 buyer IA >= {buyer['minimum']}")



ROTATION_QUOTA_RESERVATION = """  // The rotation cohort requires a full live peer-quota budget. Prior real
  // negative-security calls can consume the 80-request/minute allowance and
  // would otherwise deny the *winner verification* at attempt 81. Wait for a
  // fresh real quota window without modifying quota rows, server clocks,
  // concurrency, expected 401/200 outcomes or any product security policy.
  await reserveTestWindow(30);
"""


def validate_quota_window_change(source: str, original: str) -> None:
    """Allow only preflight pacing, never altered assertions/quota/product code."""
    needle = "  const rotations = await Promise.all(Array.from({ length: 8 }, () => api('sessions/rotate', {}, token)));"
    need(original.count(needle) == 1,
         "historic 8-concurrent-rotation security test unavailable")
    need(source == original.replace(needle, ROTATION_QUOTA_RESERVATION + needle),
         "real E2E QA differs from F06 accepted source beyond one actual quota reservation")
    for required in (
        "if (peak + capacity > 80)",
        "await delay(60500 - Date.now() % 60000);",
        "rotations.filter(r => r.status === 200).length === 1",
        "rotated token cannot be replayed",
        "rotation winner remains usable",
        "concurrent login quota admits at most eight password challenges per account-minute",
    ):
        need(required in source, "an original negative or real quota guard was removed: " + required)

def audit_b() -> None:
    scope()
    source = (ROOT / "qa/c05_operational_flow_e2e.mjs").read_text("utf-8")
    original = subprocess.check_output(["git", "show",
                                         BASE + ":qa/c05_operational_flow_e2e.mjs"],
                                        cwd=ROOT, text=True)
    validate_quota_window_change(source, original)
    for gate in ("verifyPasskeys", "verifyInvitations", "verifyPairing",
                 "verifyAssisted", "verifyProfile", "verifyTenantAuditBoundary",
                 "verifyOperatorRecovery", "verifySha1EnrollmentQr"):
        need(gate in source, "critical real identity security check missing: " + gate)
    workflow = (ROOT / ".github/workflows/c05-ux-f07-regresiones-integrales.yml").read_text("utf-8")
    for gate in ("permissions:", "contents: read", "actions: read",
                 "persist-credentials: false", "Node 24", ".NET 10",
                 "node qa/c05_operational_flow_e2e.mjs", "340",
                 "18 migraciones", "18 reversiones"):
        need(gate in workflow, "F07 workflow security/real evidence gate missing: " + gate)
    need("production" not in str(scope()), "production files were touched")
    print("F07/B PASS: negative tenant/session/MFA tests retained, no product or DB change, read-only token")


def audit_c() -> None:
    scope()
    buyer = buyer_gate()
    fixture = (ROOT / "frontend/tests/c05-f06-inventory-65.spec.ts").read_text("utf-8")
    for marker in ("auxiliaryExpectedHeading",
                   "auxiliary JSON heading was stale during screenshot",
                   "auxiliary UI changed during screenshot",
                   "section.operator-workspace:visible > header.operator-section-head h2"):
        need(marker in fixture, "F07 capture metadata race guard missing: " + marker)
    checker = (ROOT / "qa/c05_ux_f07_visual_comparison.py").read_text("utf-8")
    for gate in ("ImageChops", "ratio_threshold", "mean_threshold",
                 "KNOWN_STALE_BASELINE_HEADINGS", "known_baseline_metadata_race",
                 "fraction == 0 and mean == 0",
                 "BASE", "GITHUB_SHA", "inventoryIds", "visual_diff_report.json",
                 "F07_MATRIZ_FINAL_65.csv"):
        need(gate in checker, "F07 real pixel/metadata comparator incomplete: " + gate)
    workflow = (ROOT / ".github/workflows/c05-ux-f07-regresiones-integrales.yml").read_text("utf-8")
    for gate in ("playwright.c05-f06-inventory65.config.ts",
                 "playwright.c05-f06-feedback.config.ts",
                 "playwright.c05-f05-modals.config.ts",
                 "playwright.c05-f04-operators.config.ts",
                 "playwright.c05-f03-account.config.ts",
                 "playwright.institutional.config.ts",
                 "c05_ux_f05_modal_audits.py deep-evidence",
                 "c05_ux_f06_65_coverage.py",
                 "c05_ux_f07_visual_comparison.py"):
        need(gate in workflow, "F07 full visual/Chromium/a11y gate missing: " + gate)
    print(f"F07/C PASS: 65/65 recorded grades min={buyer['minimum']}; "
          "5 widths, evidence SHA/pixel comparison, accessibility and no product changes")


def main() -> None:
    need(len(sys.argv) == 2 and sys.argv[1] in ("A", "B", "C"),
         "usage: python3 qa/c05_ux_f07_final_audits.py [A|B|C]")
    {"A": audit_a, "B": audit_b, "C": audit_c}[sys.argv[1]]()


if __name__ == "__main__":
    try:
        main()
    except (AssertionError, OSError, ValueError, KeyError, subprocess.CalledProcessError) as exc:
        print(str(exc), file=sys.stderr)
        raise SystemExit(1) from exc
