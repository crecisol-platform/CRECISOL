#!/usr/bin/env python3
"""CRECISOL C02: real certification acceptance gate.

MUST be invoked in CI AFTER all C02-09 A/B/C, Angular, Chromium, .NET and
disposable MySQL integration steps have actually succeeded. This does not
grant backend-private security or permit production/C03 development.
"""
from copy import deepcopy
from pathlib import Path
import sys

import c02_01_source as source
import c02_09_integral_audits as integral

ROOT = Path(__file__).resolve().parents[1]
HEAD_PRECERT = "d8ba77dd65f87325ca8dbf2f38455943af2f8d8e"
WORD_SHA = source.EXPECTED_SHA
FILES = {
    "acta": ROOT / "docs/c02/C02_09_AUDITORIA_Y_ACTA.md",
    "prep": ROOT / "docs/C02_PREPARACION.md",
    "plan": ROOT / "docs/PLAN_37_CAPITULOS.md",
    "reference": ROOT / "docs/MASTER_REFERENCE.md",
    "timeline": ROOT / "docs/planificacion-futura/C02_MICROFASES_PROPUESTAS.md",
}


def require(ok, reason):
    if not ok:
        raise AssertionError(reason)


def validate_content(files):
    acta, prep, plan = files["acta"], files["prep"], files["plan"]
    reference, timeline = files["reference"], files["timeline"]
    chapter_row = next((s for s in plan.splitlines() if s.startswith("C02 | ")), "")
    c03_row = next((s for s in plan.splitlines() if s.startswith("C03 | ")), "")
    section = acta.split("## 6. CERTIFICACIÓN FORMAL C02", 1)
    require(len(section) == 2, "Missing separate post-audit owner certification act")
    acceptance = section[1]

    # Only the new, effective status is authoritative, never a historical quote.
    require("C02 CERTIFICADO" in acta[:500] and
            "NO CERTIFICADO" not in acta[:500],
            "Acta effective status is not certified")
    require("CAPÍTULO C02 CERTIFICADO" in prep[:350] and
            "2/37" in prep[:1600] and "9/9" in prep[:1600] and
            "C03 **HABILITABLE PERO NO INICIADO**" in prep[:1700],
            "Effective C02 and C03 status not updated")
    require("2/37 capítulos certificados (C01 y C02)" in plan[:2000] and
            "**CERTIFICADO C02" in chapter_row and
            "9/9" in chapter_row and
            "**HABILITABLE UNA VEZ CERTIFICADO C02**; NO INICIADO" in c03_row,
            "37-chapter plan promotes next chapter prematurely or loses C02")
    require("2/37 capítulos certificados (C01 y C02)" in reference and
            "«APROBADO»" in reference and
            "C03 será habilitable pero **no iniciado**" in reference,
            "Canonical master reference conflicts with certified state")
    require("C02 CERTIFICADO POR ACEPTACIÓN EXPRESA «APROBADO»" in timeline[:600] and
            "9/9" in timeline[:600] and
            "**NO INICIADO**" in timeline[:1500],
            "Provisional timeline still claims pending acceptance")

    # Acceptance is from the real conversation after the C02 technical acta
    # and must not retroactively count as approval of future capabilities.
    for marker in ("**ACEPTACIÓN HUMANA EXPRESA:**",
                   "**«APROBADO»**", "9 de noviembre de 2026",
                   HEAD_PRECERT, WORD_SHA,
                   "12/12 CI SUCCESS", "58/58", "25/25",
                   "**Auditoría A PASS**", "**B PASS**", "**C PASS**",
                   "C02 CERTIFICADO EN EL ALCANCE ACOTADO",
                   "NO INICIADO AQUÍ", "BOLA/IDOR/MFA", "NO** se certifican"):
        require(marker in acceptance, "Missing approval, evidence, or limit: " + marker)
    for run in ("42678910536", "42678910584",
                "42678910677", "42678910703", "42678910594"):
        require(run in acceptance, "Missing postmerge evidence run " + run)
    require("PR #47" in acceptance and "PR #46" in acceptance,
            "Technical and acta PR provenance missing")
    require("WCAG" in acceptance and "panel administrativo" in acceptance
            and "pagos" in acceptance and "producción" in acceptance,
            "Certification may not imply private/production feature readiness")
    require("C03 habilitable" in plan.lower() or "C03 |" in plan,
            "C03 planning boundary missing")
    # Old audit statutes remain clearly labelled historical, not current.
    require("Antecedente histórico" in plan and
            "NO es el estado vigente" in prep and
            "1/37" in plan and "C02 **NO CERTIFICADO**" in plan,
            "Historical evidence improperly rewritten as active status")


def main():
    # The original master Word is read and hashed directly by our canonical
    # extractor, not trusted from an assistant narrative or a CSV.
    source.original_parts()
    for key in ("source", "c02-regressions", "c02-browser", "c01-browser",
                "build", "harness", "domain", "application", "live-stack"):
        integral.proof(key)

    docs = {key: path.read_text(encoding="utf-8") for key, path in FILES.items()}
    validate_content(docs)
    adversarial = [
        ("owner approval", "acta", "«APROBADO»", "«NO APROBADO»"),
        ("falsified chapter count", "plan",
         "2/37 capítulos certificados (C01 y C02)",
         "3/37 capítulos certificados (C01 y C02)"),
        ("C03 started without scope", "plan",
         "**HABILITABLE UNA VEZ CERTIFICADO C02**; NO INICIADO",
         "**YA INICIADO**"),
        ("forged technical evidence SHA", "acta", HEAD_PRECERT, "0" * 40),
        ("missing privacy caveat", "acta", "NO** se certifican", "SI** se certifican"),
    ]
    for label, target, before, after in adversarial:
        clone = deepcopy(docs)
        require(before in clone[target], "Mutation precondition not found: " + label)
        clone[target] = clone[target].replace(before, after)
        try:
            validate_content(clone)
        except (AssertionError, KeyError, TypeError):
            continue
        raise AssertionError("Unsafe certification mutation was accepted: " + label)
    print("C02 CERTIFICATION GATE PASS: Word verified, 9/9, "
          "explicit post-audit APROBADO, 2/37, C03 not started, "
          "all real A/B/C and integration proofs present, "
          "5 adversarial certification mutations rejected.")
    print("LIMIT: private authorization, admin panel, manual WCAG, "
          "payments and production are NOT certified.")


if __name__ == "__main__":
    try:
        main()
    except (AssertionError, KeyError, OSError, TypeError, ValueError) as error:
        print("C02 CERTIFICATION GATE FAILED: " + str(error), file=sys.stderr)
        sys.exit(1)
