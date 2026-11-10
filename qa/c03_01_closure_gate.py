#!/usr/bin/env python3
"""CRECISOL C03-01: formal microphase closure gate AFTER real A/B/C tests.

Checks the explicit owner's C03-01 authorization, exact technical SHA and
GitHub postmerge run identities, limited 1/10 status, no C03/C04 promotion,
canonical source and real CI proof files. It does not certify whole C03.
"""
from copy import deepcopy
from pathlib import Path
import json
import sys

import c03_01_source as chapter
import c03_05_status as phase

ROOT = Path(__file__).resolve().parents[1]
TECHNICAL_SHA = "7217fb1e720f16c71d5a654f605eca96d81deb58"
WORD_SHA = "e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e"
OWNER_APPROVAL = "CERTIFICA POR FAVOR LA MICROFASE 1 DEL BLOQUE 3"
RUNS = ("42691620500", "42691620886", "42691620794",
        "42691620719", "42691621001", "42691620768", "42691620826")
FILES = {
    "acta": ROOT / "docs/c03/C03_01_ACTA_MICROFASE.md",
    "second": ROOT / "docs/c03/C03_02_ACTA_MICROFASE.md",
    "third": ROOT / "docs/c03/C03_03_ACTA_MICROFASE.md",
    "fourth": ROOT / "docs/c03/C03_04_ACTA_MICROFASE.md",
    "prep": ROOT / "docs/C03_PREPARACION.md",
    "plan": ROOT / "docs/PLAN_37_CAPITULOS.md",
    "master": ROOT / "docs/MASTER_REFERENCE.md",
    "spec": ROOT / "docs/c03/C03_01_ESPECIFICACION.md",
    "future": ROOT / "docs/planificacion-futura/C03_MICROFASES_PROPUESTAS.md",
    "global": ROOT / "docs/PLAN_MICROFASES_37_CAPITULOS_PROPUESTO.md",
    "decisions": ROOT / "docs/DECISIONES_ABIERTAS.md",
    "agents": ROOT / "AGENTS.md",
}


def require(flag, reason):
    if not flag:
        raise AssertionError(reason)


def validate_content(files):
    # C03-01's 1/10 acta is immutable historical evidence. The current state
    # can reach 2/10 only with a separate, explicitly accepted C03-02 acta.
    first = files["acta"]
    second = files["second"]
    third = files["third"]
    fourth = files["fourth"]
    prep, plan, master = files["prep"], files["plan"], files["master"]
    spec, future, global_plan = files["spec"], files["future"], files["global"]
    decisions, agents = files["decisions"], files["agents"]
    c03 = next((line for line in plan.splitlines() if line.startswith("C03 | ")), "")
    c04 = next((line for line in plan.splitlines() if line.startswith("C04 | ")), "")
    require("2/37 capítulos certificados (C01 y C02)" in plan[:2000] and
            "2/37 capítulos certificados (C01 y C02)" in master,
            "Only C01 and C02 may be certified chapters")
    require(c04 == "C04 | Multitenancy, dominios y aprovisionamiento | PENDIENTE"
            and "C03 NO CERTIFICADO" in c03 and "C04 BLOQUEADO" in c03,
            "C03 chapter or C04 illegally advanced")

    # Prior C03-01 provenance is checked directly, not overwritten to 2/10.
    require("## 5. Decisión de microfase y gate final" in first and
            "C03-01 CERRADA TÉCNICAMENTE (1/10)" in first and
            OWNER_APPROVAL in first and "9 de noviembre de 2026" in first and
            "formalización condicionada al SUCCESS" in first and
            TECHNICAL_SHA in first and WORD_SHA in first and
            all(run in first for run in RUNS) and "7/7" in first and
            "Auditoría A" in first and "Auditoría B" in first and
            "Auditoría C" in first and "10/10 mutaciones" in first and
            "83" in first and "MySQL8.4" in first and
            "BOLA/IDOR/MFA" in first and
            "NO APLICA A ESTA MICROFASE" in first and
            "C03 NO CERTIFICADO" in first and "C04 BLOQUEADO" in first,
            "Historic C03-01 authorization/proofs/limits altered")

    second_status = ("**C03-01 CERTIFICADA (1/10) Y "
                     "C03-02 CERRADA TÉCNICAMENTE/APROBADA PARA CERTIFICACIÓN (2/10)**")
    third_status = "**C03-01 Y C03-02 CERTIFICADAS (2/10); C03-03 CERRADA TÉCNICAMENTE Y ACEPTADA PARA CERTIFICACIÓN (3/10)**"
    fifth_active = phase.fifth_safe(c03)
    fourth_active = "**C03-01, C03-02 Y C03-03 CERTIFICADAS (3/10); C03-04 CERRADA TÉCNICAMENTE Y ACEPTADA PARA CERTIFICACIÓN (4/10)**" in c03 or fifth_active
    third_active = third_status in c03 or fourth_active
    require((second_status in c03 or third_active) and
            ("C03-06 NO INICIADA" in c03 if fifth_active else
             "C03-05 NO INICIADA" in c03 if fourth_active else
             "C03-04 NO INICIADA" in c03 if third_active else
             "C03-03 NO INICIADA" in c03),
            "Unauthorized progression beyond accepted microphase")
    if third_active:
        require("C03-03 CERRADA TÉCNICAMENTE" in third and
                "FORMALIZACIÓN EN CURSO" in third and
                "Listo entonces adelante con el siguiente paso" in third and
                "c3bb9e4e314297c502b644ba1306a16364e7af2e" in third and
                "C03 NO CERTIFICADO" in third and "C04 BLOQUEADO" in third,
                "C03-03 needs its own limited owner authorization")
    if fifth_active:
        require("5/10" in prep and "C03-06 NO INICIADA" in prep
                and "C03-05_ACTA_MICROFASE.md" in files["master"],
                "C03-05 lacks its new acta or next-phase lock")
    if fourth_active:
        prep_valid = ("4/10" in prep and "C03-04 CERRADA TÉCNICAMENTE" in prep
                      and "C03-05 NO INICIADA" in prep)
        require("C03-04 CERRADA TÉCNICAMENTE" in fourth and
                "FORMALIZACIÓN EN CURSO" in fourth and
                "Adelante por favor con ello" in fourth and
                "d529f85f0bc8710940d195fceaea06bf6c5e674d" in fourth and
                "C03 NO CERTIFICADO" in fourth and "C04 BLOQUEADO" in fourth,
                "Fourth microphase needs separate owner-approved acta")
    elif third_active:
        prep_valid = ("3/10" in prep and "C03-03 CERRADA TÉCNICAMENTE" in prep
                      and "C03-04" in prep and "NO INICIADA" in prep)
    else:
        prep_valid = ("2/10" in prep and "C03-03" in prep and
                      "NO INICIADA" in prep)
    require("C03-02" in prep and prep_valid and
            "C03 NO CERTIFICADO" in prep and "C04 BLOQUEADO" in prep,
            "C03 preparation not synchronized with approved scope")
    if fourth_active:
        master_valid = ("C03-02 **CERTIFICADA**" in master and
                        "C03-03 **CERTIFICADA (3/10)**" in master and
                        "C03-04 **CERRADA TÉCNICAMENTE" in master and
                        "4/10" in master)
    elif third_active:
        master_valid = ("C03-02 **CERTIFICADA**" in master and
                        "C03-03 **CERRADA TÉCNICAMENTE" in master and "3/10" in master)
    else:
        master_valid = ("C03-02 **CERRADA TÉCNICAMENTE" in master and "2/10" in master)
    require("C03-01 **CERTIFICADA**" in master and master_valid and
            WORD_SHA in master and
            "C03 NO CERTIFICADO" in master and "C04 BLOQUEADO" in master,
            "Canonical master reference was silently changed")
    # The C03-01 specification is historical, not the live C03-02 status.
    require("C03-01" in spec and "1/10" in spec and
            "C03 NO CERTIFICADO" in spec and "C04 BLOQUEADO" in spec,
            "Historical first-phase specification was altered")
    for label, document in (("future", future), ("global", global_plan),
                            ("decisions", decisions), ("agents", agents)):
        require("C03-02" in document and
                ("4/10" if fourth_active else "3/10" if third_active else "2/10") in document and
                "C03 NO CERTIFICADO" in document and "C04 BLOQUEADO" in document,
                "Missing authorized status in " + label)

    require("C03-02 CERRADA TÉCNICAMENTE" in second and
            "FORMALIZACIÓN EN CURSO" in second and
            "9 de noviembre de 2026" in second and
            "adelante con ello por favor." in second and
            "no es una aprobación anterior" in second and
            "adc975c7bcf4b6a772cc160a5b377d26af2b5fa6" in second and
            WORD_SHA in second and
            all(run in second for run in ("42701725556", "42701724980", "42701725339"))
            and "3/3 workflows" in second and "35/35" in second and
            "51/51" in second and "19/19" in second and
            "5/5 mutaciones adversariales" in second and
            "BOLA/IDOR/MFA" in second and "No se certifican" in second and
            "C03 NO CERTIFICADO" in second and
            "C03-03 NO INICIADA" in second and "C04 BLOQUEADO" in second,
            "C03-02 closure lacks explicit limited approval and verified CI evidence")

def main():
    # The canonical document and physical source inventory are read, not
    # guessed from the acta. These checks run after actual CI compile/smoke.
    chapter.original_parts()
    chapter.actual_graph()
    for key in ("angular", "dotnet", "domain", "application", "live-stack"):
        chapter.proof(key)
    graph = json.loads((ROOT / "docs/c03/C03_01_MODULOS_DEPENDENCIAS.json")
                       .read_text(encoding="utf-8"))
    chapter.validate_contract(graph)
    files = {key: p.read_text(encoding="utf-8") for key, p in FILES.items()}
    validate_content(files)

    fifth_active = phase.fifth_safe(next((x for x in files['plan'].splitlines() if x.startswith('C03 | ')), ''))
    fourth_active = "**C03-01, C03-02 Y C03-03 CERTIFICADAS (3/10); C03-04 CERRADA TÉCNICAMENTE Y ACEPTADA PARA CERTIFICACIÓN (4/10)**" in files["plan"] or fifth_active
    third_active = ("**C03-01 Y C03-02 CERTIFICADAS (2/10); C03-03 CERRADA TÉCNICAMENTE Y ACEPTADA PARA CERTIFICACIÓN (3/10)**" in files["plan"] or fourth_active)
    adversarial = [
        ("C03-02 owner approval removed", "second",
         "adelante con ello por favor.", "APROBACION AUSENTE"),
        ("C03-02 merge SHA falsified", "second",
         "adc975c7bcf4b6a772cc160a5b377d26af2b5fa6", "0" * 40),
        ("C03 microphase count advanced", "plan",
         "CERTIFICACIÓN (5/10)**" if fifth_active else "CERTIFICACIÓN (4/10)**" if fourth_active else "CERTIFICACIÓN (3/10)**" if third_active else "CERTIFICACIÓN (2/10)**",
         "CERTIFICACIÓN (6/10)**" if fifth_active else "CERTIFICACIÓN (5/10)**" if fourth_active else "CERTIFICACIÓN (4/10)**" if third_active else "CERTIFICACIÓN (3/10)**"),
        ("C04 started before C03", "plan",
         "C04 | Multitenancy, dominios y aprovisionamiento | PENDIENTE",
         "C04 | Multitenancy, dominios y aprovisionamiento | INICIADO"),
        ("C03 certification claimed", "second",
         "C03 NO CERTIFICADO", "C03 CERTIFICADO"),
    ]
    for label, target, before, after in adversarial:
        sample = deepcopy(files)
        require(before in sample[target],
                "Negative mutation setup missing: " + label)
        sample[target] = sample[target].replace(before, after)
        try:
            validate_content(sample)
        except (AssertionError, KeyError, TypeError, ValueError):
            continue
        raise AssertionError("Unsafe closure mutation accepted: " + label)
    print("C03-01 MICROPHASE CERTIFICATION GATE PASS: exact Word, "
          "physical project graph, real CI proof markers, owner's specific "
          "9 Nov 2026 approval, 7/7 technical postmerge references, "
          "C03-01 historical 1/10 + C03-02 2/10 + C03-03 3/10 + C03-04 4/10 accepted pending own CI, C03 uncertified, C04 blocked, "
          "5 adversarial closure mutations rejected.")
    print("LIMIT: public/documentary C03-01 microphase only; "
          "not C03 chapter, tenancy/auth private, payments or production.")


if __name__ == "__main__":
    try:
        main()
    except (AssertionError, KeyError, OSError, TypeError, ValueError) as ex:
        print("C03-01 CERTIFICATION GATE FAILED: " + str(ex),
              file=sys.stderr)
        sys.exit(1)
