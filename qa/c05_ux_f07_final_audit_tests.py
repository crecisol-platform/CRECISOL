#!/usr/bin/env python3
"""F07 A/B/C negative controls. The gate must never downgrade scope or scores."""
import csv
import subprocess
import unittest
from pathlib import Path

from c05_ux_f07_final_audits import ALLOWED, BASE, validate_scope, buyer_gate, validate_quota_window_change, ROTATION_QUOTA_RESERVATION

ROOT = Path(__file__).resolve().parents[1]
F07_SOURCE = "qa/c05_ux_f07_visual_comparison.py"
F07_GATE = "qa/c05_ux_f07_final_audits.py"
BACKEND = "backend/src/Crecisol.Api/Program.cs"
SQL = "database/migrations/unreviewed.sql"
F06_UI = "frontend/src/app/ui/feedback/operation-feedback.component.ts"


class F07FinalGateTests(unittest.TestCase):
    def test_approved_scope_only(self):
        validate_scope({F07_SOURCE, F07_GATE}, {F07_SOURCE, F07_GATE}, True)
        self.assertIn(F07_SOURCE, ALLOWED)

    def test_exact_qa_capture_fixture_is_allowed_but_product_components_are_not(self):
        self.assertIn("frontend/tests/c05-f06-inventory-65.spec.ts", ALLOWED)
        self.assertNotIn(F06_UI, ALLOWED)
        self.assertNotIn("frontend/src/app/ui/feedback/operation-feedback.component.html", ALLOWED)

    def test_only_original_e2e_plus_live_quota_window_is_allowed(self):
        baseline = subprocess.check_output(
            ["git", "show", BASE + ":qa/c05_operational_flow_e2e.mjs"],
            cwd=ROOT, text=True)
        revised = (ROOT / "qa/c05_operational_flow_e2e.mjs").read_text("utf-8")
        validate_quota_window_change(revised, baseline)
        self.assertIn("qa/c05_operational_flow_e2e.mjs", ALLOWED)
        self.assertEqual(revised.count(ROTATION_QUOTA_RESERVATION), 1)

    def test_original_eight_concurrent_session_rotations_cannot_be_reduced(self):
        baseline = subprocess.check_output(
            ["git", "show", BASE + ":qa/c05_operational_flow_e2e.mjs"],
            cwd=ROOT, text=True)
        revised = (ROOT / "qa/c05_operational_flow_e2e.mjs").read_text("utf-8")
        with self.assertRaisesRegex(AssertionError, "beyond one actual quota reservation"):
            validate_quota_window_change(
                revised.replace("length: 8 }, () => api('sessions/rotate'",
                                "length: 1 }, () => api('sessions/rotate'"), baseline)

    def test_real_security_quota_is_not_relaxed(self):
        baseline = subprocess.check_output(
            ["git", "show", BASE + ":qa/c05_operational_flow_e2e.mjs"],
            cwd=ROOT, text=True)
        revised = (ROOT / "qa/c05_operational_flow_e2e.mjs").read_text("utf-8")
        with self.assertRaisesRegex(AssertionError, "beyond one actual quota reservation"):
            validate_quota_window_change(revised.replace(
                "if (peak + capacity > 80)", "if (peak + capacity > 800)"), baseline)

    def test_no_baseline_ancestor_denied(self):
        with self.assertRaisesRegex(AssertionError, "ancestor"):
            validate_scope({F07_SOURCE}, {F07_SOURCE}, False)

    def test_backend_change_denied(self):
        with self.assertRaisesRegex(AssertionError, "outside F07"):
            validate_scope({F07_SOURCE, BACKEND}, {F07_SOURCE, BACKEND}, True)

    def test_mysql_migration_change_denied(self):
        with self.assertRaisesRegex(AssertionError, "outside F07"):
            validate_scope({F07_SOURCE, SQL}, {F07_SOURCE, SQL}, True)

    def test_changed_ui_after_visual_acceptance_denied(self):
        with self.assertRaisesRegex(AssertionError, "outside F07"):
            validate_scope({F07_SOURCE, F06_UI}, {F07_SOURCE, F06_UI}, True)

    def test_transient_reverted_foreign_edit_denied(self):
        with self.assertRaisesRegex(AssertionError, "even if reverted"):
            validate_scope({F07_SOURCE}, {F07_SOURCE, SQL}, True)

    def test_empty_review_denied(self):
        with self.assertRaisesRegex(AssertionError, "empty F07"):
            validate_scope(set(), set(), True)

    def test_visual_regression_script_cannot_be_missing(self):
        with self.assertRaisesRegex(AssertionError, "visual comparison missing"):
            validate_scope({F07_GATE}, {F07_GATE}, True)

    def test_individual_buyer_scores_are_above_minimum_without_human_signoff(self):
        result = buyer_gate()
        self.assertEqual(result["accepted"], 65)
        self.assertGreaterEqual(result["minimum"], 9.5)
        self.assertFalse(result["humanAccepted"])
        self.assertFalse(result["runtimeAuthByImage"])
        self.assertEqual(len(BASE), 40)


if __name__ == "__main__":
    unittest.main()
