#!/usr/bin/env python3
"""C05 UX F06 — fail-closed tests for 65 independently reviewed buyer grades.

These tests are intentionally *not* a visual judge. Evidence review and the
acceptance decision are recorded per-ID in the CSV. The validator verifies
that no fabricated/improper blank grade can be certified by a green CI alone.
"""
from pathlib import Path
import csv
import copy
import unittest

from c05_ux_f06_65_coverage import validate_commercial_ia_ratings

ROOT = Path(__file__).resolve().parents[1]
CATALOG = ROOT / "docs/c05/ux/F06_65_SUPERFICIES_VALORACION.csv"


def real_rows():
    with CATALOG.open("r", encoding="utf-8", newline="") as source:
        return list(csv.DictReader(source))


class F06BuyerGradeTests(unittest.TestCase):
    def test_real_65_detailed_ia_reviews_are_consistent(self):
        rows = real_rows()
        summary = validate_commercial_ia_ratings(rows)
        self.assertEqual(summary["accepted"], 65)
        self.assertGreaterEqual(summary["minimum"], 9.5)
        self.assertIs(summary["humanAccepted"], False)
        self.assertIs(summary["runtimeAuthByImage"], False)

    def test_blank_score_denied(self):
        rows = real_rows()
        rows[0]["nota_comercial_0_10"] = ""
        with self.assertRaisesRegex(AssertionError, "missing buyer score"):
            validate_commercial_ia_ratings(rows)

    def test_under_9_5_denied_even_when_someone_marks_buy(self):
        rows = real_rows()
        rows[32]["nota_comercial_0_10"] = "9.4"
        rows[32]["dictamen_compraria"] = "COMPRARIA"
        with self.assertRaisesRegex(AssertionError, "under premium threshold"):
            validate_commercial_ia_ratings(rows)

    def test_missing_rationale_denied(self):
        rows = real_rows()
        rows[40]["correccion_verificada"] = "PASS"
        with self.assertRaisesRegex(AssertionError, "specific editorial rationale"):
            validate_commercial_ia_ratings(rows)

    def test_missing_specific_image_reference_denied(self):
        rows = real_rows()
        rows[11]["evidencia_png_por_viewport"] = "all views look good"
        with self.assertRaisesRegex(AssertionError, "source artifact"):
            validate_commercial_ia_ratings(rows)

    def test_false_human_signoff_denied(self):
        rows = real_rows()
        rows[54]["observaciones"] = "ACEPTACION_HUMANA_CERTIFICADA"
        with self.assertRaisesRegex(AssertionError, "IA-vs-human"):
            validate_commercial_ia_ratings(rows)

    def test_duplicate_id_denied(self):
        rows = real_rows()
        rows[5]["id"] = rows[4]["id"]
        with self.assertRaisesRegex(AssertionError, "unknown or duplicated"):
            validate_commercial_ia_ratings(rows)


if __name__ == "__main__":
    unittest.main()
