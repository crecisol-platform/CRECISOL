#!/usr/bin/env python3
"""C03 local static A/B and portability preflight; deliberately NOT certification.

Cross-platform: CPython 3.11+, no Docker, MySQL, .NET, Angular or network.
C (runtime evidence) is intentionally excluded: only real CI can create
the .c03-*-ci-proof files after compiling/executing the respective tests.
"""
from pathlib import Path, PureWindowsPath
import sys

import c03_01_source as c01
import c03_02_domain_audits as c02
import c03_03_application_audits as c03
import c03_04_infrastructure_audits as c04
import c03_05_api_audits as c05
import c03_05_status as state
import c03_05_closure_gate as closure
import c03_04_closure_gate as historical_fourth
import c03_03_closure_gate as historical_third
import c03_02_closure_gate as historical_second
import c03_01_closure_gate as historical_first
import c02_certification_gate as historical_c02

ROOT = Path(__file__).resolve().parents[1]
STAGES = (
    ("C03-01", c01),
    ("C03-02", c02),
    ("C03-03", c03),
    ("C03-04", c04),
    ("C03-05", c05),
)


def check_portable_paths():
    """Exercise the *real* C03 source keys and a synthetic Windows path."""
    domain_keys = c02.sources()
    application_keys = c03.authored_sources()
    assert "Common/DomainRevision.cs" in domain_keys, "Domain path keys are not portable"
    assert c03.CASE_PATH in application_keys, "Application path keys are not portable"
    assert not any("\\" in key for key in (*domain_keys, *application_keys)), (
        "Windows path separator leaked into C03 file-key contract"
    )
    winroot = PureWindowsPath("C:/crecisol/backend/src/Crecisol.Domain")
    winfile = winroot / "Common" / "DomainRevision.cs"
    assert winfile.relative_to(winroot).as_posix() == "Common/DomainRevision.cs"
    assert (ROOT / "docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx").is_file()
    assert state.fifth_plan_safe((ROOT / "docs/PLAN_37_CAPITULOS.md").read_text(encoding="utf-8"))


def main():
    if len(sys.argv) != 1:
        raise ValueError("Usage: python qa/c03_local_preflight.py (no fake C/CI flags)")
    check_portable_paths()
    documentary = {name: location.read_text(encoding="utf-8")
                   for name, location in closure.FILES.items()}
    closure.validate_content(documentary)  # STATIC archive check, no C/CI markers
    for old in (historical_fourth, historical_third, historical_second,
                historical_first, historical_c02):
        old_contents = {name: location.read_text(encoding="utf-8")
                        for name, location in old.FILES.items()}
        old.validate_content(old_contents)
    print("PASS LOCAL DOCUMENTARY: C02 + C03-01..05 static historical gates; not remote CI")
    for name, module in STAGES:
        module.audit_a()
        module.audit_b()
        print("PASS LOCAL SOURCE ONLY:", name, "A/B; no CI-only proof inferred")
    print("PASS LOCAL PATHS: native and PureWindowsPath match canonical POSIX file keys")
    print("NOT EXECUTED: C audits, .NET/Angular build, MySQL, real HTTP, Chromium,")
    print("  POSTMERGE; these require real toolchains and separate GitHub Actions evidence.")
    print("RESULT: local static preflight PASS, NOT a technical or formal certification.")


if __name__ == "__main__":
    try:
        main()
    except (AssertionError, OSError, KeyError, TypeError, ValueError) as error:
        print("FAIL LOCAL C03 PREFLIGHT:", error, file=sys.stderr)
        sys.exit(1)
