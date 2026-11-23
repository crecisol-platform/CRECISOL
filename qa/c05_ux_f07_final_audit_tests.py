#!/usr/bin/env python3
"""F07 A/B/C negative controls. The gate must never downgrade scope or scores."""
import csv
import unittest
from pathlib import Path

from c05_ux_f07_final_audits import ALLOWED, BASE, validate_scope, buyer_gate

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
