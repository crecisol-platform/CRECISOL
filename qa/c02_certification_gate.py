#!/usr/bin/env python3
"""CRECISOL C02: real certification acceptance gate.

MUST be invoked in CI AFTER all C02-09 A/B/C, Angular, Chromium, .NET and
disposable MySQL integration steps have actually succeeded. This does not
grant backend-private security, C04 progress or production deployment.
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
            "**CERTIFICADO C02" in chapter_row and "9/9" in chapter_row,
            "C02 certification/2-of-37 was changed")
    # After C02 certification, opening *only* C03-01 is legitimate. Historical
    # C02 acta remains a snapshot; C04 must stay locked until C03 acceptance.
    c03_active = "**EN DESARROLLO C03-01 (0/10)**" in c03_row
    c03_closed_four = "**C03-01, C03-02 Y C03-03 CERTIFICADAS (3/10); C03-04 CERRADA TÉCNICAMENTE Y ACEPTADA PARA CERTIFICACIÓN (4/10)**" in c03_row
    c03_closed_three = ("**C03-01 Y C03-02 CERTIFICADAS (2/10); C03-03 CERRADA TÉCNICAMENTE Y ACEPTADA PARA CERTIFICACIÓN (3/10)**" in c03_row or c03_closed_four)
    c03_closed_two = "**C03-01 CERTIFICADA (1/10) Y C03-02 CERRADA TÉCNICAMENTE/APROBADA PARA CERTIFICACIÓN (2/10)**" in c03_row or c03_closed_three
    c03_closed = "**C03-01 CERRADA TÉCNICAMENTE (1/10)**" in c03_row or c03_closed_two
    require(not (c03_active and c03_closed), "Conflicting current C03 state")
    if c03_active or c03_closed:
        c03_file = ROOT / "docs/C03_PREPARACION.md"
        require(c03_file.is_file(), "C03 opened with no preparation/impact gate")
        c03_doc = c03_file.read_text(encoding="utf-8")
        require("C03 NO CERTIFICADO" in c03_doc and
                "C04 BLOQUEADO" in c03_doc and
                "C03 NO CERTIFICADO" in c03_row and
                "C04 BLOQUEADO" in c03_row and
                "C04 | Multitenancy, dominios y aprovisionamiento | PENDIENTE" in plan,
                "Unauthorized C03/C04 promotion after C02 certification")
        if c03_closed:
            c03_acta = ROOT / "docs/c03/C03_01_ACTA_MICROFASE.md"
            require(c03_acta.is_file(), "C03-01 closed with no acta")
            act = c03_acta.read_text(encoding="utf-8")
            require("C03-01 CERRADA TÉCNICAMENTE (1/10)" in act and
                    "CERTIFICA POR FAVOR LA MICROFASE 1 DEL BLOQUE 3" in act and
                    "7217fb1e720f16c71d5a654f605eca96d81deb58" in act and
                    "42691620500" in act and "7/7" in act and
                    "C03 NO CERTIFICADO" in act and "C04 BLOQUEADO" in act,
                    "C03-01 historical owner acceptance was lost")
            if c03_closed_two:
                next_acta_path = ROOT / "docs/c03/C03_02_ACTA_MICROFASE.md"
                require(next_acta_path.is_file(), "C03-02 status without formal acta")
                next_acta = next_acta_path.read_text(encoding="utf-8")
                require("adc975c7bcf4b6a772cc160a5b377d26af2b5fa6" in next_acta
                        and "adelante con ello por favor." in next_acta
                        and "C03 NO CERTIFICADO" in next_acta
                        and "C04 BLOQUEADO" in next_acta,
                        "C03-02 historical owner acceptance was lost")
                if c03_closed_three:
                    third_path = ROOT / "docs/c03/C03_03_ACTA_MICROFASE.md"
                    require(third_path.is_file(), "C03-03 approved state without acta")
                    third = third_path.read_text(encoding="utf-8")
                    require("c3bb9e4e314297c502b644ba1306a16364e7af2e" in third
                            and "Listo entonces adelante con el siguiente paso" in third
                            and "C03 NO CERTIFICADO" in third and "C04 BLOQUEADO" in third,
                            "C03-03 historical authorization was lost")
                    if c03_closed_four:
                        fourth_path = ROOT / "docs/c03/C03_04_ACTA_MICROFASE.md"
                        require(fourth_path.is_file(), "C03-04 acta required")
                        fourth = fourth_path.read_text(encoding="utf-8")
                        require("4/10" in c03_doc and "C03-04 CERRADA TÉCNICAMENTE" in c03_doc
                                and "C03-05 NO INICIADA" in c03_doc
                                and "d529f85f0bc8710940d195fceaea06bf6c5e674d" in fourth
                                and "Adelante por favor con ello" in fourth
                                and "C03 NO CERTIFICADO" in fourth and "C04 BLOQUEADO" in fourth,
                                "C03-04 progression lacks its specific later approval")
                    else:
                        require("3/10" in c03_doc and "C03-03 CERRADA TÉCNICAMENTE" in c03_doc
                                and "C03-04" in c03_doc and "NO INICIADA" in c03_doc,
                                "C03-03 may not start C03-04 automatically")
                else:
                    require("2/10" in c03_doc and "C03-02 CERRADA TÉCNICAMENTE" in c03_doc
                            and "C03-03" in c03_doc and "NO INICIADA" in c03_doc,
                            "C03-02 does not authorize C03-03 automatically")
            else:
                require("1/10" in c03_doc and "C03-01 CERRADA TÉCNICAMENTE" in c03_doc
                        and "C03-02" in c03_doc and "NO INICIADA" in c03_doc,
                        "Prior C03-01 closure must not automatically open C03-02")
        else:
            require("0/10" in c03_doc and "C03-01" in c03_doc,
                    "Active C03-01 missing zero-closed baseline")
    else:
        require("**HABILITABLE UNA VEZ CERTIFICADO C02**; NO INICIADO" in c03_row,
                "C03 started without a controlled C03-01 entry gate")
    if c03_closed_four:
        reference_ok = ("C03-01 **CERTIFICADA**" in reference and
                        "C03-02 **CERTIFICADA**" in reference and
                        "C03-03 **CERTIFICADA (3/10)**" in reference and
                        "C03-04 **CERRADA TÉCNICAMENTE" in reference and "4/10" in reference)
    elif c03_closed_three:
        reference_ok = ("C03-01 **CERTIFICADA**" in reference and
                        "C03-02 **CERTIFICADA**" in reference and
                        "C03-03 **CERRADA TÉCNICAMENTE" in reference and "3/10" in reference)
    elif c03_closed_two:
        reference_ok = ("C03-02 **CERRADA TÉCNICAMENTE" in reference and
                        "2/10" in reference and "C03-01 **CERTIFICADA**" in reference)
    elif c03_closed:
        reference_ok = ("C03-01 CERRADA TÉCNICAMENTE" in reference and "1/10" in reference)
    elif c03_active:
        reference_ok = "C03-01 activada" in reference
    else:
        reference_ok = "C03 será habilitable pero **no iniciado**" in reference
    require("2/37 capítulos certificados (C01 y C02)" in reference and
            "«APROBADO»" in reference and reference_ok,
            "Canonical master reference conflicts with C03 sequential state")
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
                   "CAPÍTULO 02 CERTIFICADO EN EL ALCANCE ACOTADO",
                   "NO INICIADO AQUÍ", "BOLA/IDOR/MFA", "NO** se certifican"):
        require(marker in acceptance, "Missing approval, evidence, or limit: " + marker)
    for run in ("42678910536", "42678910584",
                "42678910677", "42678910703", "42678910594"):
        require(run in acceptance, "Missing postmerge evidence run " + run)
    require("[#47]" in acceptance and "[#46]" in acceptance,
            "Technical and acta PR provenance missing")
    require("WCAG" in acceptance and "panel administrativo" in acceptance
            and "pagos" in acceptance and "producción" in acceptance,
            "Certification may not imply private/production feature readiness")
    require((c03_closed_two or ("C03-01 CERRADA TÉCNICAMENTE (1/10)" in c03_row)) if c03_closed
            else (("EN DESARROLLO C03-01 (0/10)" in c03_row) if c03_active
                  else ("HABILITABLE UNA VEZ CERTIFICADO C02" in c03_row
                        and "NO INICIADO" in c03_row)),
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
        ("C03 progressed beyond certified microphase", "plan",
         ("**C03-01, C03-02 Y C03-03 CERTIFICADAS (3/10); C03-04 CERRADA TÉCNICAMENTE Y ACEPTADA PARA CERTIFICACIÓN (4/10)**" if "**C03-01, C03-02 Y C03-03 CERTIFICADAS (3/10); C03-04 CERRADA TÉCNICAMENTE Y ACEPTADA PARA CERTIFICACIÓN (4/10)**" in docs["plan"] else
          ("**C03-01 Y C03-02 CERTIFICADAS (2/10); C03-03 CERRADA TÉCNICAMENTE Y ACEPTADA PARA CERTIFICACIÓN (3/10)**" if "**C03-01 Y C03-02 CERTIFICADAS (2/10); C03-03 CERRADA TÉCNICAMENTE Y ACEPTADA PARA CERTIFICACIÓN (3/10)**" in docs["plan"] else
          ("**C03-01 CERTIFICADA (1/10) Y C03-02 CERRADA TÉCNICAMENTE/APROBADA PARA CERTIFICACIÓN (2/10)**"
           if "**C03-01 CERTIFICADA (1/10) Y C03-02 CERRADA TÉCNICAMENTE/APROBADA PARA CERTIFICACIÓN (2/10)**" in docs["plan"]
           else ("**C03-01 CERRADA TÉCNICAMENTE (1/10)**"
                 if "**C03-01 CERRADA TÉCNICAMENTE (1/10)**" in docs["plan"]
                 else "**EN DESARROLLO C03-01 (0/10)**")))),
         "**C03 CERTIFICADO 10/10"),
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
          "explicit post-audit APROBADO, 2/37, C03-01/C03-02/C03-03/C03-04 at most 4/10, C04 blocked, "
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
