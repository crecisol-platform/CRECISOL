#!/usr/bin/env python3
"""C03-09 regression: C01-06 approved logo must come from CURRENT canonical Word.

Read-only against the real repository. No npm, Docker, MySQL or network.
Historical C01-06 approval and SHA must remain immutable.
"""
from hashlib import sha256
from pathlib import Path
from tempfile import TemporaryDirectory
from unittest import TestCase, main, mock

import c01_06_logo_candidate as historical
import c01_06_official_assets as official
import master_source_guard as master


class WordLogoCurrentEditionTests(TestCase):
    def test_historical_and_current_word_pins_remain_distinct(self):
        self.assertEqual(historical.EXPECTED, master.HISTORICAL_WORD_SHA)
        self.assertEqual(official.CURRENT_WORD_SHA, master.CURRENT_WORD_SHA)
        self.assertNotEqual(official.CURRENT_WORD_SHA, historical.EXPECTED)
        self.assertEqual(official.APPROVED_SHA, master.HISTORICAL_LOGO_SHA)

    def test_current_sha_is_used_after_full_canonical_guard(self):
        image = b"synthetic immutable approved image"
        with mock.patch.object(official, "canonical_active_word", return_value=True) as guard, \
             mock.patch.object(official, "candidate",
                               return_value=(image, "png", "word/media/image1.png", 1298, 859)) as candidate, \
             mock.patch.object(official, "APPROVED_SHA", sha256(image).hexdigest()), \
             mock.patch.object(official, "crop_emblem", return_value=b"derived emblem") as crop:
            assets = official.output_assets()
        guard.assert_called_once_with()
        candidate.assert_called_once_with(official.WORD, master.CURRENT_WORD_SHA)
        crop.assert_called_once_with(image)
        self.assertEqual(assets, ((official.FULL, image),
                                  (official.EMBLEM, b"derived emblem")))

    def test_failing_canonical_guard_stops_before_image_extraction(self):
        with mock.patch.object(official, "canonical_active_word",
                               side_effect=AssertionError("Word or PDF altered")), \
             mock.patch.object(official, "candidate") as candidate:
            with self.assertRaisesRegex(AssertionError, "altered"):
                official.output_assets()
        candidate.assert_not_called()

    def test_owner_approved_logo_hash_remains_strict(self):
        with mock.patch.object(official, "canonical_active_word", return_value=True), \
             mock.patch.object(official, "candidate",
                               return_value=(b"forged-logo", "png",
                                             "word/media/image1.png", 1298, 859)), \
             mock.patch.object(official, "crop_emblem") as crop:
            with self.assertRaisesRegex(ValueError, "Owner-approved image hash mismatch"):
                official.output_assets()
        crop.assert_not_called()

    def test_unapproved_image_relationship_is_rejected(self):
        image = b"synthetic image"
        with mock.patch.object(official, "canonical_active_word", return_value=True), \
             mock.patch.object(official, "candidate",
                               return_value=(image, "png",
                                             "word/media/other.png", 1298, 859)), \
             mock.patch.object(official, "APPROVED_SHA", sha256(image).hexdigest()), \
             mock.patch.object(official, "crop_emblem") as crop:
            with self.assertRaisesRegex(ValueError, "relationship changed"):
                official.output_assets()
        crop.assert_not_called()

    def test_candidate_extraction_rejects_unapproved_word_hash(self):
        with TemporaryDirectory(prefix="crecisol-c03-09-word-") as folder:
            path = Path(folder) / "fake.docx"
            image = historical.fake_png()
            historical.fake_word(path, image)
            self.assertEqual(historical.candidate(path, sha256(path.read_bytes()).hexdigest())[0], image)
            with self.assertRaisesRegex(ValueError, "Canonical Word SHA mismatch"):
                historical.candidate(path, master.CURRENT_WORD_SHA)

    def test_candidate_extraction_rejects_external_image_reference(self):
        with TemporaryDirectory(prefix="crecisol-c03-09-image-") as folder:
            path = Path(folder) / "fake.docx"
            historical.fake_word(path, historical.fake_png(), external=True)
            with self.assertRaisesRegex(ValueError, "External image rejected"):
                historical.candidate(path, sha256(path.read_bytes()).hexdigest())

    def test_real_current_docx_pdf_and_logo_can_be_extracted_read_only(self):
        """A positive integration guard against the ACTUAL Git-tracked binaries."""
        self.assertTrue(master.canonical_active_word())
        assets = dict(official.output_assets())
        self.assertEqual(sha256(assets[official.FULL]).hexdigest(), official.APPROVED_SHA)
        self.assertEqual(historical.check_png(assets[official.FULL]), (1298, 859))
        self.assertEqual(historical.check_png(assets[official.EMBLEM]), (558, 537))


if __name__ == "__main__":
    main()
