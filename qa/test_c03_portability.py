#!/usr/bin/env python3
"""Minimal cross-OS negative assertions; never create CI proof markers."""
from pathlib import Path, PureWindowsPath
from tempfile import TemporaryDirectory
from unittest import TestCase, main, mock

import c03_02_domain_audits as domain
import c03_03_application_audits as app
import c03_05_api_audits as api
import c03_05_status as state


class C03PortabilityTests(TestCase):
    def test_windows_relative_keys_use_posix(self):
        root = PureWindowsPath("C:/Proyectos/FullStack/Crecisol/backend/src/Crecisol.Domain")
        source = root / "Common" / "DomainRevision.cs"
        self.assertEqual(source.relative_to(root).as_posix(), "Common/DomainRevision.cs")
        self.assertIn("Common/DomainRevision.cs", domain.sources())
        self.assertIn(app.CASE_PATH, app.authored_sources())
        self.assertNotIn("\\", "".join(domain.sources().keys()))
        self.assertNotIn("\\", "".join(app.authored_sources().keys()))

    def test_only_fifth_stage_current_and_sixth_locked(self):
        plan = (state_plan := Path(__file__).resolve().parents[1] /
                "docs/PLAN_37_CAPITULOS.md").read_text(encoding="utf-8")
        self.assertTrue(state.fifth_plan_safe(plan))
        for bad, changed in (
            ("C03-06 NO INICIADA", "C03-06 INICIADA"),
            ("C03 NO CERTIFICADO", "C03 CERTIFICADO"),
            ("C04 BLOQUEADO", "C04 HABILITADO"),
        ):
            with self.subTest(bad=bad), self.assertRaises(AssertionError):
                state.fifth_plan_safe(plan.replace(bad, changed))
        self.assertTrue(state_plan.is_file())

    def test_ci_audit_c_fails_closed_without_real_proof(self):
        with TemporaryDirectory() as temp, mock.patch.object(api, "PROOF", Path(temp)):
            with self.assertRaisesRegex(AssertionError, "Missing real Actions step evidence"):
                api.audit_c()

    def test_preflight_does_not_mint_proof_or_claim_certification(self):
        content = (Path(__file__).resolve().parent / "c03_local_preflight.py").read_text(encoding="utf-8")
        self.assertNotIn("mkdir(", content)
        self.assertNotIn(".write_text(", content)
        self.assertNotIn("module.audit_c()", content)
        self.assertIn("NOT EXECUTED:", content)


if __name__ == "__main__":
    main()
