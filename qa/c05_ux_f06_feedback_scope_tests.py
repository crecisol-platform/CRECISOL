#!/usr/bin/env python3
"""F06 post-integration scope checks: negative and positive authorization paths.

Run independently from browser/DB tests. This test deliberately fails if the
scope validator starts trusting arbitrary future files or reverted commits.
"""
import unittest

from c05_ux_f06_feedback_audits import authorize_delta, ALLOWED

OWN = "qa/c05_ux_f06_feedback_audits.py"
OWN_DOC = "docs/c05/ux/F06_GATE_ALCANCE_POSTMERGE_2026_11_23.md"
F05_DOC = "docs/c05/ux/F05_PREMIUM_95_AUDITORIAS_2026_11_23.md"
F04_DOC = "docs/c05/ux/F04_PREMIUM_95_AUDITORIAS_2026_11_23.md"
FORBIDDEN_SQL = "database/migrations/999_unauthorized.sql"


class ScopeProvenanceTests(unittest.TestCase):
    def test_known_integrated_foreign_files_pass_only_when_unchanged(self):
        historic = {F05_DOC, F04_DOC, OWN, OWN_DOC}
        approved = {F05_DOC, F04_DOC}
        after = {OWN, OWN_DOC}
        self.assertTrue(after <= ALLOWED)
        self.assertEqual(
            authorize_delta(historic, approved, after, after),
            sorted(after))

    def test_new_backend_or_database_edit_fails_closed(self):
        with self.assertRaisesRegex(AssertionError, "new F06 out of scope"):
            authorize_delta({OWN, FORBIDDEN_SQL}, set(),
                            {OWN, FORBIDDEN_SQL}, {OWN, FORBIDDEN_SQL})

    def test_merged_f05_document_cannot_be_modified_again_by_f06(self):
        # Even a path from the approved snapshot is forbidden for NEW F06 commits.
        with self.assertRaisesRegex(AssertionError, "new F06 out of scope"):
            authorize_delta({OWN, F05_DOC}, {F05_DOC}, {OWN, F05_DOC},
                            {OWN, F05_DOC})

    def test_revert_in_same_branch_does_not_hide_foreign_changes(self):
        # Final diff looks authorized, but commit history touched F04 content.
        with self.assertRaisesRegex(AssertionError, "commit history touched"):
            authorize_delta({OWN, F04_DOC}, {F04_DOC}, {OWN},
                            {OWN, F04_DOC})

    def test_unknown_historical_file_is_not_grandfathered(self):
        with self.assertRaisesRegex(AssertionError, "unexpected inherited"):
            authorize_delta({OWN, FORBIDDEN_SQL}, {F05_DOC},
                            {OWN}, {OWN})

    def test_empty_original_or_new_diff_never_passes(self):
        with self.assertRaisesRegex(AssertionError, "original diff absent"):
            authorize_delta(set(), set(), {OWN}, {OWN})
        with self.assertRaisesRegex(AssertionError, "no F06 correction"):
            authorize_delta({OWN}, set(), set(), {OWN})

    def test_allowlist_never_includes_integrated_f04_f05_documents(self):
        self.assertNotIn(F04_DOC, ALLOWED)
        self.assertNotIn(F05_DOC, ALLOWED)
        self.assertIn(OWN, ALLOWED)
        self.assertIn(OWN_DOC, ALLOWED)


if __name__ == "__main__":
    unittest.main()
