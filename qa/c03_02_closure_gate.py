#!/usr/bin/env python3
"""CRECISOL C03-02: owner acceptance and certificate documentary gate.

This must run only AFTER .NET10 Release, 35 C03 + 51 C01 + 19 Application
real assertions and three C03-02 audits A/B/C. The gate checks exact
documentary evidence; GitHub PREMERGE/POSTMERGE status must additionally
be verified externally for the exact PR merge SHA before claiming CERTIFIED.
"""
from copy import deepcopy
from pathlib import Path
import sys

import c03_01_closure_gate as prior
import c03_02_domain_audits as domain
import c03_05_status as phase

ROOT = Path(__file__).resolve().parents[1]
WORD_SHA = "e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e"
TECHNICAL_SHA = "adc975c7bcf4b6a772cc160a5b377d26af2b5fa6"
TECHNICAL_HEAD = "afff3ce06675d720b2332d6e03ebc9b7b8ac468c"
OWNER_REPLY = "adelante con ello por favor."
CI_RUNS = ("42701725556", "42701724980", "42701725339")
EXPECTED_C03_ROW = ("**C03-01 CERTIFICADA (1/10) Y "
                    "C03-02 CERRADA TÉCNICAMENTE/APROBADA PARA CERTIFICACIÓN (2/10)**")
FILES = {
    "acta": ROOT / "docs/c03/C03_02_ACTA_MICROFASE.md",
    "first": ROOT / "docs/c03/C03_01_ACTA_MICROFASE.md",
    "third": ROOT / "docs/c03/C03_03_ACTA_MICROFASE.md",
    "fourth": ROOT / "docs/c03/C03_04_ACTA_MICROFASE.md",
    "plan": ROOT / "docs/PLAN_37_CAPITULOS.md",
    "prep": ROOT / "docs/C03_PREPARACION.md",
    "master": ROOT / "docs/MASTER_REFERENCE.md",
    "spec": ROOT / "docs/c03/C03_02_ESPECIFICACION.md",
    "future": ROOT / "docs/planificacion-futura/C03_MICROFASES_PROPUESTAS.md",
    "global": ROOT / "docs/PLAN_MICROFASES_37_CAPITULOS_PROPUESTO.md",
    "decisions": ROOT / "docs/DECISIONES_ABIERTAS.md",
    "agents": ROOT / "AGENTS.md",
}


def require(flag, message):
    if not flag:
        raise AssertionError(message)


def validate_content(files):
    acta, plan, prep = files["acta"], files["plan"], files["prep"]
    master, spec = files["master"], files["spec"]
    first = files["first"]
    c03 = next((line for line in plan.splitlines() if line.startswith("C03 | ")), "")
    c04 = next((line for line in plan.splitlines() if line.startswith("C04 | ")), "")
    require("2/37 capítulos certificados (C01 y C02)" in plan[:1800]
            and "2/37 capítulos certificados (C01 y C02)" in master,
            "Only C01 and C02 may count as certified chapters")
    third_status = "**C03-01 Y C03-02 CERTIFICADAS (2/10); C03-03 CERRADA TÉCNICAMENTE Y ACEPTADA PARA CERTIFICACIÓN (3/10)**"
    fifth_active = phase.fifth_plan_safe(plan)
    fourth_active = "**C03-01, C03-02 Y C03-03 CERTIFICADAS (3/10); C03-04 CERRADA TÉCNICAMENTE Y ACEPTADA PARA CERTIFICACIÓN (4/10)**" in c03 or fifth_active
    third_active = third_status in c03 or fourth_active
    require((EXPECTED_C03_ROW in c03 or third_active) and
            ("C03-06 NO INICIADA" in c03 if fifth_active else
             "C03-05 NO INICIADA" in c03 if fourth_active else
             "C03-04 NO INICIADA" in c03 if third_active else "C03-03 NO INICIADA" in c03)
            and "C03 NO CERTIFICADO" in c03 and "C04 BLOQUEADO" in c03,
            "C03 microphase count or downstream phase was changed")
    if third_active:
        third = files["third"]
        require("C03-03 CERRADA TÉCNICAMENTE" in third and
                "FORMALIZACIÓN EN CURSO" in third and
                "Listo entonces adelante con el siguiente paso" in third and
                "c3bb9e4e314297c502b644ba1306a16364e7af2e" in third and
                "C03 NO CERTIFICADO" in third and "C04 BLOQUEADO" in third,
                "C03-03 requires separate owner-approved acta")
    if fourth_active:
        fourth = files["fourth"]
        require("C03-04 CERRADA TÉCNICAMENTE" in fourth and
                "FORMALIZACIÓN EN CURSO" in fourth and
                "Adelante por favor con ello" in fourth and
                "d529f85f0bc8710940d195fceaea06bf6c5e674d" in fourth and
                "C03 NO CERTIFICADO" in fourth and "C04 BLOQUEADO" in fourth,
                "C03-04 requires distinct approved acta")
    require(c04 == "C04 | Multitenancy, dominios y aprovisionamiento | PENDIENTE",
            "C04 must remain blocked")
    require("C03-01 CERRADA TÉCNICAMENTE (1/10)" in first and
            "CERTIFICA POR FAVOR LA MICROFASE 1 DEL BLOQUE 3" in first and
            "7/7" in first and "C03 NO CERTIFICADO" in first,
            "Historical C03-01 acta altered")
    if fourth_active:
        prep_valid = ("4/10" in prep and "C03-04 CERRADA TÉCNICAMENTE" in prep
                      and "C03-05 NO INICIADA" in prep)
    elif third_active:
        prep_valid = ("3/10" in prep and "C03-03 CERRADA TÉCNICAMENTE" in prep
                      and "C03-04" in prep and "NO INICIADA" in prep)
    else:
        prep_valid = ("2/10" in prep and "C03-03" in prep and "NO INICIADA" in prep)
    require("C03-02" in prep and prep_valid and
            "C03 NO CERTIFICADO" in prep and "C04 BLOQUEADO" in prep,
            "C03 preparation does not match limited acceptance")
    if fourth_active:
        master_valid = ("C03-02 **CERTIFICADA**" in master and
                        "C03-03 **CERTIFICADA (3/10)**" in master and
                        "C03-04 **CERRADA TÉCNICAMENTE" in master and "4/10" in master)
    elif third_active:
        master_valid = ("C03-02 **CERTIFICADA**" in master and
                        "C03-03 **CERRADA TÉCNICAMENTE" in master and "3/10" in master)
    else:
        master_valid = ("C03-02 **CERRADA TÉCNICAMENTE" in master and "2/10" in master)
    require("C03-01 **CERTIFICADA**" in master and master_valid and
            "C03 NO CERTIFICADO" in master and "C04 BLOQUEADO" in master
            and WORD_SHA in master,
            "Canonical master reference drift or premature C03 promotion")
    require("C03-02 CERRADA TÉCNICAMENTE" in spec and
            "2/10" in spec and "C03 NO CERTIFICADO" in spec and
            "C04 BLOQUEADO" in spec and "C03-03 NO INICIADA" in spec,
            "Technical spec status not reconciled")
    for key in ("future", "global", "decisions", "agents"):
        value = files[key]
        require("C03-02" in value and
                ("4/10" if fourth_active else "3/10" if third_active else "2/10") in value and
                "C03 NO CERTIFICADO" in value and "C04 BLOQUEADO" in value,
                "Future/global/governance status not synchronized: " + key)

    require("C03-02 CERRADA TÉCNICAMENTE Y ACEPTADA EXPRESAMENTE" in acta and
            "FORMALIZACIÓN EN CURSO" in acta and
            "9 de noviembre de 2026" in acta and
            OWNER_REPLY in acta and
            "no es una aprobación anterior" in acta and
            "solo" in acta.casefold() and
            "C03 NO CERTIFICADO" in acta and
            "C04 BLOQUEADO" in acta and "C03-03 NO INICIADA" in acta,
            "Owner approval absent, ambiguous, or extends beyond C03-02")
    require(TECHNICAL_HEAD in acta and TECHNICAL_SHA in acta and
            "PR técnico #51" in acta and WORD_SHA in acta and
            "3/3 workflows" in acta and
            all(value in acta for value in CI_RUNS),
            "Exact technical SHA/Word/CI postmerge provenance missing")
    for key in ("Auditoría A", "Auditoría B", "Auditoría C",
                "35/35", "51/51", "19/19", "5/5 mutaciones adversariales",
                "83 Chromium", "MySQL8.4", "BOLA/IDOR/MFA",
                "No se certifican", "Rollback",
                "CI PREMERGE y CI POSTMERGE", "skipped"):
        require(key in acta, "Missing evidence/risk/final CI condition: " + key)


def main():
    # Verify actual canonical Word, source graph and this workflow's real
    # test-proof files, not only an assertion inside a Markdown acta.
    domain.audit_a()
    domain.audit_b()
    domain.audit_c()
    docs = {k: p.read_text(encoding="utf-8") for k, p in FILES.items()}
    validate_content(docs)
    historic = {key: path.read_text(encoding="utf-8")
                for key, path in prior.FILES.items()}
    historic["second"] = docs["acta"]
    prior.validate_content(historic)
    fifth_active = phase.fifth_safe(next((x for x in docs['plan'].splitlines() if x.startswith('C03 | ')), ''))
    fourth_active = "**C03-01, C03-02 Y C03-03 CERTIFICADAS (3/10); C03-04 CERRADA TÉCNICAMENTE Y ACEPTADA PARA CERTIFICACIÓN (4/10)**" in docs["plan"] or fifth_active
    third_active = ("**C03-01 Y C03-02 CERTIFICADAS (2/10); C03-03 CERRADA TÉCNICAMENTE Y ACEPTADA PARA CERTIFICACIÓN (3/10)**" in docs["plan"] or fourth_active)
    variants = [
        ("owner approval forged", "acta", OWNER_REPLY, "DENEGADO"),
        ("technical SHA forged", "acta", TECHNICAL_SHA, "0" * 40),
        ("wrong microphase count", "plan",
         "CERTIFICACIÓN (5/10)**" if fifth_active else "CERTIFICACIÓN (4/10)**" if fourth_active else "CERTIFICACIÓN (3/10)**" if third_active else "CERTIFICACIÓN (2/10)**",
         "CERTIFICACIÓN (6/10)**" if fifth_active else "CERTIFICACIÓN (5/10)**" if fourth_active else "CERTIFICACIÓN (4/10)**" if third_active else "CERTIFICACIÓN (3/10)**"),
        ("unearned C04", "plan",
         "C04 | Multitenancy, dominios y aprovisionamiento | PENDIENTE",
         "C04 | Multitenancy, dominios y aprovisionamiento | INICIADO"),
        ("unearned C03 chapter certificate", "plan",
         "C03 NO CERTIFICADO", "C03 CERTIFICADO"),
        ("premature next microphase", "plan",
         "C03-06 NO INICIADA" if fifth_active else "C03-05 NO INICIADA" if fourth_active else "C03-04 NO INICIADA" if third_active else "C03-03 NO INICIADA",
         "C03-06 INICIADA" if fifth_active else "C03-05 INICIADA" if fourth_active else "C03-04 INICIADA" if third_active else "C03-03 INICIADA"),
        ("missing CI success count", "acta", "3/3 workflows", "0/3 workflows"),
        ("privacy disclaimer removed", "acta",
         "No se certifican", "Si se certifican"),
        ("Domain assertions fabricated", "acta", "35/35", "0/35"),
        ("Word hash forged", "acta", WORD_SHA, "0" * 64),
    ]
    for label, key, before, after in variants:
        sample = deepcopy(docs)
        require(before in sample[key], "Negative test setup missing: " + label)
        sample[key] = sample[key].replace(before, after)
        try:
            validate_content(sample)
        except (AssertionError, ValueError, TypeError, KeyError):
            continue
        raise AssertionError("Adversarial acta mutation accepted: " + label)
    print("C03-02 CERTIFICATION DOCUMENT GATE PASS: approved Word, real "
          "A/B/C + Domain/Application proof, accepted C03-02 only, PR #51 "
          "3/3 technical POSTMERGE references, 2/10, C03 uncertified, "
          "C03-04 4/10 only after its own CI, C03-05 not started, C04 blocked, 10/10 mutations rejected.")
    print("IMPORTANT: Not final certification until PR acta PREMERGE and "
          "POSTMERGE workflows are verified completed/success on exact SHAs.")


if __name__ == "__main__":
    try:
        main()
    except (AssertionError, OSError, ValueError, TypeError, KeyError) as exc:
        print("C03-02 CERTIFICATION GATE FAILED: " + str(exc), file=sys.stderr)
        sys.exit(1)
