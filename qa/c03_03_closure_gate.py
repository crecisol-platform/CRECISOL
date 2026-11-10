#!/usr/bin/env python3
"""CRECISOL C03-03: formal owner acceptance, real A/B/C and evidence gate.

This gate executes only after .NET10 Release and all NEW and historical
executable tests have passed in the SAME CI job. It cannot independently
attest remote workflow conclusions: PREMERGE/POSTMERGE GitHub results must
still be verified for the exact PR SHAs before declaring certification.
"""
from copy import deepcopy
from pathlib import Path
import sys

import c03_03_application_audits as application
import c03_02_closure_gate as previous
import c03_01_closure_gate as historical
import c02_certification_gate as c02
import c03_05_status as phase

ROOT = Path(__file__).resolve().parents[1]
TECHNICAL_HEAD = "4944348018bb3a1434e09eb7fdc802e269290b1b"
TECHNICAL_SHA = "c3bb9e4e314297c502b644ba1306a16364e7af2e"
WORD_SHA = "e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e"
OWNER_REPLY = "Listo entonces adelante con el siguiente paso por favor no te detengas hasta certificar Vale gracias amigo"
THIRD_STATUS = ("**C03-01 Y C03-02 CERTIFICADAS (2/10); "
                "C03-03 CERRADA TÉCNICAMENTE Y ACEPTADA PARA CERTIFICACIÓN (3/10)**")
POSTMERGE_RUNS = ("42714098896", "42714098908", "42714098950")
PREMERGE_RUNS = ("42713903148", "42713903003")
FILES = {
    "acta": ROOT / "docs/c03/C03_03_ACTA_MICROFASE.md",
    "spec": ROOT / "docs/c03/C03_03_ESPECIFICACION.md",
    "first": ROOT / "docs/c03/C03_01_ACTA_MICROFASE.md",
    "second": ROOT / "docs/c03/C03_02_ACTA_MICROFASE.md",
    "fourth": ROOT / "docs/c03/C03_04_ACTA_MICROFASE.md",
    "plan": ROOT / "docs/PLAN_37_CAPITULOS.md",
    "prep": ROOT / "docs/C03_PREPARACION.md",
    "master": ROOT / "docs/MASTER_REFERENCE.md",
    "future": ROOT / "docs/planificacion-futura/C03_MICROFASES_PROPUESTAS.md",
    "global": ROOT / "docs/PLAN_MICROFASES_37_CAPITULOS_PROPUESTO.md",
    "decisions": ROOT / "docs/DECISIONES_ABIERTAS.md",
    "agents": ROOT / "AGENTS.md",
}


def require(ok, reason):
    if not ok:
        raise AssertionError(reason)


def validate_content(files):
    acta, spec = files["acta"], files["spec"]
    plan, prep, master = files["plan"], files["prep"], files["master"]
    c03 = next((line for line in plan.splitlines() if line.startswith("C03 | ")), "")
    c04 = next((line for line in plan.splitlines() if line.startswith("C04 | ")), "")
    fifth_active = phase.fifth_plan_safe(plan)
    fourth_active = "**C03-01, C03-02 Y C03-03 CERTIFICADAS (3/10); C03-04 CERRADA TÉCNICAMENTE Y ACEPTADA PARA CERTIFICACIÓN (4/10)**" in c03 or fifth_active
    require((THIRD_STATUS in c03 or fourth_active) and
            ("C03-06 NO INICIADA" in c03 if fifth_active else
             "C03-05 NO INICIADA" in c03 if fourth_active else
             "C03-04 NO INICIADA" in c03) and
            "C03 NO CERTIFICADO" in c03 and "C04 BLOQUEADO" in c03 and
            "C03 CERTIFICADO" not in c03 and "6/10" not in c03,
            "C03 chapter and technical microphase sequence incorrectly promoted")
    require(c04 == "C04 | Multitenancy, dominios y aprovisionamiento | PENDIENTE",
            "C04 must remain blocked, not implemented")
    require("2/37 capítulos certificados (C01 y C02)" in plan[:1800] and
            "2/37 capítulos certificados (C01 y C02)" in master,
            "Only C01 and C02 are certified chapters")
    if fourth_active:
        master_ok = ("C03-03 **CERTIFICADA (3/10)**" in master and
                     "C03-04 **CERRADA TÉCNICAMENTE" in master and "4/10" in master)
    else:
        master_ok = "C03-03 **CERRADA TÉCNICAMENTE" in master and "3/10" in master
    require("C03-01 **CERTIFICADA**" in master and
            "C03-02 **CERTIFICADA**" in master and master_ok and
            WORD_SHA in master and
            "C03 NO CERTIFICADO" in master and "C04 BLOQUEADO" in master,
            "Canonical master reference or prior certification chain changed")

    for name in ("prep", "future", "global", "decisions", "agents"):
        doc = files[name]
        require("C03-03" in doc and
                ("4/10" if fourth_active else "3/10") in doc and
                "C03 NO CERTIFICADO" in doc and "C04 BLOQUEADO" in doc and
                ("C03-06" if fifth_active else "C03-05" if fourth_active else "C03-04") in doc and
                "NO INICIADA" in doc,
                "Current limited C03-03 status not synchronized: " + name)
    require("C03-03" in prep and
            ("CERTIFICADA (3/10)" if fourth_active else "C03-03 CERRADA TÉCNICAMENTE") in prep and
            "3/3 CI técnicas POSTMERGE SUCCESS" in prep,
            "C03-03 plan missing technical handoff evidence")
    require("C03-03 CERRADA TÉCNICAMENTE" in spec and
            "3/10" in spec and "FORMALIZACIÓN DOCUMENTAL" in spec and
            "C03 NO CERTIFICADO" in spec and "C04 BLOQUEADO" in spec and
            "C03-04 NO INICIADA" in spec,
            "C03-03 technical specification status/evidence changed")

    # Earlier historical acts remain immutable and separately authorized.
    require("C03-01 CERRADA TÉCNICAMENTE (1/10)" in files["first"] and
            "CERTIFICA POR FAVOR LA MICROFASE 1 DEL BLOQUE 3" in files["first"] and
            "7/7" in files["first"],
            "C03-01 historical evidence modified")
    require("C03-02 CERRADA TÉCNICAMENTE" in files["second"] and
            "adelante con ello por favor." in files["second"] and
            "3/3 workflows" in files["second"],
            "C03-02 historical acceptance modified")

    for marker in (
        "C03-03 CERRADA TÉCNICAMENTE Y ACEPTADA EXPRESAMENTE",
        "FORMALIZACIÓN EN CURSO",
        "9 de noviembre de 2026",
        OWNER_REPLY,
        "posterior a la integración técnica",
        TECHNICAL_HEAD, TECHNICAL_SHA,
        "PR técnico #53", "3/3 workflows",
        WORD_SHA,
        "T01-R03", "P03/P04", "DIF-C03-01",
        "Auditoría A", "Auditoría B", "Auditoría C",
        "37/37", "19/19", "35/35", "51/51",
        "7/7 mutaciones adversariales",
        "83 Chromium", "37 casos HTTP",
        "BOLA/IDOR/MFA", "No se certifican",
        "C03 NO CERTIFICADO", "C04 BLOQUEADO",
        "C03-04 NO INICIADA", "Rollback",
        "CI PREMERGE y CI POSTMERGE",
        "12/12 alteraciones adversariales"
    ):
        require(marker in acta, "C03-03 acta missing approval/proof/scope: " + marker)
    require(all(run in acta for run in POSTMERGE_RUNS + PREMERGE_RUNS),
            "C03-03 acta missing actual C03-03 technical CI run IDs")
    require("C03 CERTIFICADO" not in acta,
            "C03 chapter may not be certified from a partial microphase")
    if fourth_active:
        fourth = files["fourth"]
        require("C03-04 CERRADA TÉCNICAMENTE Y ACEPTADA EXPRESAMENTE" in fourth
                and "FORMALIZACIÓN EN CURSO" in fourth
                and "Adelante por favor con ello" in fourth
                and "d529f85f0bc8710940d195fceaea06bf6c5e674d" in fourth
                and "C03-05 NO INICIADA" in fourth
                and "C03 NO CERTIFICADO" in fourth and "C04 BLOQUEADO" in fourth,
                "Fourth microphase must have its own limited owner approval")


def main():
    # Execute A/B/C with canonical Word SHA, physical project refs and
    # markers deposited only by successful executable compile/tests.
    application.audit_a()
    application.audit_b()
    application.audit_c()
    docs = {name: path.read_text(encoding="utf-8") for name, path in FILES.items()}
    validate_content(docs)

    # Preserve historical gates. Their own negative mutations run in
    # C03-01/C03-02 and C02 workflows; here we revalidate their content.
    old2 = {key: path.read_text(encoding="utf-8")
            for key, path in previous.FILES.items()}
    previous.validate_content(old2)
    old1 = {key: path.read_text(encoding="utf-8")
            for key, path in historical.FILES.items()}
    historical.validate_content(old1)
    c02_docs = {key: path.read_text(encoding="utf-8")
                for key, path in c02.FILES.items()}
    c02.validate_content(c02_docs)

    fifth_active = phase.fifth_safe(next((x for x in docs['plan'].splitlines() if x.startswith('C03 | ')), ''))
    fourth_active = "**C03-01, C03-02 Y C03-03 CERTIFICADAS (3/10); C03-04 CERRADA TÉCNICAMENTE Y ACEPTADA PARA CERTIFICACIÓN (4/10)**" in docs["plan"] or fifth_active
    mutations = [
        ("owner authorization forged", "acta", OWNER_REPLY, "NO APROBADO"),
        ("technical merge SHA forged", "acta", TECHNICAL_SHA, "0" * 40),
        ("technical PREMERGE HEAD forged", "acta", TECHNICAL_HEAD, "0" * 40),
        ("technical POSTMERGE CI falsified", "acta", "42714098950", "00000000000"),
        ("C03 progression falsified", "plan",
         phase.fifth_count_before(docs["plan"]) if fifth_active else "CERTIFICACIÓN (4/10)**" if fourth_active else "CERTIFICACIÓN (3/10)**",
         phase.fifth_count_after(docs["plan"]) if fifth_active else "CERTIFICACIÓN (5/10)**" if fourth_active else "CERTIFICACIÓN (4/10)**"),
        ("next microphase started prematurely", "plan",
         "C03-06 NO INICIADA" if fifth_active else "C03-05 NO INICIADA" if fourth_active else "C03-04 NO INICIADA",
         "C03-06 INICIADA" if fifth_active else "C03-05 INICIADA" if fourth_active else "C03-04 INICIADA"),
        ("C04 unlocked", "plan",
         "C04 | Multitenancy, dominios y aprovisionamiento | PENDIENTE",
         "C04 | Multitenancy, dominios y aprovisionamiento | INICIADO"),
        ("chapter C03 prematurely certified", "plan",
         "C03 NO CERTIFICADO", "C03 CERTIFICADO"),
        ("Word hash falsified", "acta", WORD_SHA, "0" * 64),
        ("Application tests falsified", "acta", "37/37", "0/37"),
        ("private authorization caveat stripped", "acta",
         "No se certifican", "Sí se certifican"),
        ("technical CI count falsified", "acta", "3/3 workflows", "0/3 workflows"),
    ]
    for label, target, before, after in mutations:
        altered = deepcopy(docs)
        require(before in altered[target],
                "Adversarial test fixture absent: " + label)
        altered[target] = altered[target].replace(before, after)
        try:
            validate_content(altered)
        except (AssertionError, KeyError, ValueError, TypeError):
            continue
        raise AssertionError("Tampered certificate accepted: " + label)
    print("C03-03 CERTIFICATION DOCUMENT GATE PASS: actual canonical Word, "
          "real A/B/C and .NET10 tests, PR #53 3/3 postmerge, owner approval, "
          "C03-01/C03-02 historical gates, 3/10 prior and 4/10 fourth stage limited state, C03 chapter "
          "uncertified, C03-04 not started, C04 blocked, 12/12 mutations rejected.")
    print("IMPORTANT: Formal certification remains CONDITIONAL until the "
          "acta PR PREMERGE and POSTMERGE workflows succeed on exact SHAs.")


if __name__ == "__main__":
    try:
        main()
    except (AssertionError, OSError, KeyError, ValueError, TypeError) as exc:
        print("C03-03 CERTIFICATION GATE FAILED: " + str(exc), file=sys.stderr)
        sys.exit(1)
