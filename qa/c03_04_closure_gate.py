#!/usr/bin/env python3
"""CRECISOL C03-04 certification: owner approval, A/B/C and adverse mutations.

Run AFTER successful .NET10 release builds, executable typed contracts and
disposable MySQL8.4 integration in THIS SAME CI job. These static records
do not attest future GitHub status: final PREMERGE and POSTMERGE runs must
be independently verified on their exact commit SHAs.
"""
from copy import deepcopy
from pathlib import Path
import sys

import c03_04_infrastructure_audits as infra
import c03_03_closure_gate as third
import c03_02_closure_gate as second
import c03_01_closure_gate as first
import c02_certification_gate as c02
import c03_05_status as phase

ROOT = Path(__file__).resolve().parents[1]
TECHNICAL_HEAD = "0bed4e82fee2459ef971140ff0d2de8a0eeb296a"
TECHNICAL_SHA = "d529f85f0bc8710940d195fceaea06bf6c5e674d"
WORD_SHA = "e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e"
OWNER_REPLY = "Adelante por favor con ello"
FOURTH_STATUS = ("**C03-01, C03-02 Y C03-03 CERTIFICADAS (3/10); "
                 "C03-04 CERRADA TÉCNICAMENTE Y ACEPTADA PARA CERTIFICACIÓN (4/10)**")
PREMERGE_RUNS = ("42722310989", "42722310053")
POSTMERGE_RUNS = ("42722550261", "42722550413", "42722550228")
FILES = {
    "acta": ROOT / "docs/c03/C03_04_ACTA_MICROFASE.md",
    "spec": ROOT / "docs/c03/C03_04_ESPECIFICACION.md",
    "first": ROOT / "docs/c03/C03_01_ACTA_MICROFASE.md",
    "second": ROOT / "docs/c03/C03_02_ACTA_MICROFASE.md",
    "third": ROOT / "docs/c03/C03_03_ACTA_MICROFASE.md",
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
    acta, spec, plan = files["acta"], files["spec"], files["plan"]
    prep, master = files["prep"], files["master"]
    c03 = next((row for row in plan.splitlines() if row.startswith("C03 | ")), "")
    c04 = next((row for row in plan.splitlines() if row.startswith("C04 | ")), "")
    fifth_active = phase.fifth_plan_safe(plan)
    require((FOURTH_STATUS in c03 or fifth_active) and
            (phase.next_locked(c03) in c03 if fifth_active else "C03-05 NO INICIADA" in c03) and
            "C03 NO CERTIFICADO" in c03 and "C04 BLOQUEADO" in c03 and
            "C03 CERTIFICADO" not in c03 and ("9/10" if phase.eighth_certified(c03) else "8/10" if phase.seventh_certified(c03) else "7/10" if phase.sixth_certified(c03) else "6/10") not in c03,
            "Unauthorized C03-04/C03-05 progression or C03 chapter certification")
    require(c04 == "C04 | Multitenancy, dominios y aprovisionamiento | PENDIENTE",
            "C04 was unlocked without C03 certification")
    require("2/37 capítulos certificados (C01 y C02)" in plan[:2000] and
            "2/37 capítulos certificados (C01 y C02)" in master,
            "Only C01 and C02 are certified chapters")
    require("C03-01 **CERTIFICADA**" in master and
            "C03-02 **CERTIFICADA**" in master and
            "C03-03 **CERTIFICADA (3/10)**" in master and
            "C03-04 **CERRADA TÉCNICAMENTE" in master and
            "4/10" in master and WORD_SHA in master and
            "C03 NO CERTIFICADO" in master and "C04 BLOQUEADO" in master,
            "Canonical document or prior certified chain changed")
    for name in ("prep", "future", "global", "decisions", "agents"):
        document = files[name]
        require("C03-04" in document and "4/10" in document and
                (phase.next_locked(c03) in document if fifth_active else
                 "C03-05 NO INICIADA" in document) and
                "C03 NO CERTIFICADO" in document and "C04 BLOQUEADO" in document,
                "Current approved phase not synchronized: " + name)
    require("C03-03" in prep and "CERTIFICADA (3/10)" in prep and
            "C03-04 CERRADA TÉCNICAMENTE" in prep and
            "3/3" in prep and "POSTMERGE SUCCESS" in prep and
            "CI PREMERGE/POSTMERGE" in prep,
            "Preparation lacks C03-03 certification or C03-04 evidence")
    require("C03-04" in spec and "NO CERTIFICADA" in spec and
            "FORMALIZACIÓN EN CURSO" in spec and
            "C03 NO CERTIFICADO" in spec and "C04 BLOQUEADO" in spec and
            "C03-05 NO INICIADA" in spec and
            TECHNICAL_SHA in spec,
            "Infrastructure specification was altered or prematurely certified")

    # Historical acts remain independent immutable snapshots, never rewritten
    # to reflect the current 4/10 state.
    require("C03-01 CERRADA TÉCNICAMENTE (1/10)" in files["first"] and
            "CERTIFICA POR FAVOR LA MICROFASE 1 DEL BLOQUE 3" in files["first"] and
            "7/7" in files["first"], "Historical C03-01 acta altered")
    require("C03-02 CERRADA TÉCNICAMENTE" in files["second"] and
            "adelante con ello por favor." in files["second"] and
            "3/3 workflows" in files["second"], "Historical C03-02 acta altered")
    require("C03-03 CERRADA TÉCNICAMENTE" in files["third"] and
            "Listo entonces adelante con el siguiente paso" in files["third"] and
            "c3bb9e4e314297c502b644ba1306a16364e7af2e" in files["third"] and
            "C03 NO CERTIFICADO" in files["third"] and
            "C04 BLOQUEADO" in files["third"],
            "Historical C03-03 acta altered")

    for marker in (
        "C03-04 CERRADA TÉCNICAMENTE Y ACEPTADA EXPRESAMENTE",
        "FORMALIZACIÓN EN CURSO", "9 de noviembre de 2026",
        OWNER_REPLY, "posterior", "solo para C03-04",
        TECHNICAL_HEAD, TECHNICAL_SHA, WORD_SHA,
        "PR técnico #55", "3/3 workflows",
        "T01-R04", "T01-R03", "P03/P04", "DIF-C03-01",
        "Auditoría A", "Auditoría B", "Auditoría C",
        "12 contratos ejecutables", "37/37", "19/19", "35/35", "51/51",
        "10/10 mutaciones adversariales",
        "83 Chromium", "37 casos HTTP",
        "BOLA/IDOR/MFA", "No se certifican",
        "C03 NO CERTIFICADO", "C04 BLOQUEADO",
        "C03-05 NO INICIADA", "Rollback",
        "PREMERGE y POSTMERGE",
        "12/12 alteraciones adversariales",
        "skipped",
    ):
        require(marker in acta, "Acta missing owner approval, scope or evidence: " + marker)
    require(all(run in acta for run in PREMERGE_RUNS + POSTMERGE_RUNS),
            "Acta lacks exact technical PREMERGE/POSTMERGE run IDs")
    require("C03 CERTIFICADO" not in acta,
            "C03 chapter may not be certified by fourth microphase")


def main():
    # A/B/C read the real canonical Word and project graph, validate SQL
    # scope, inject 10 negative mutations, and demand real same-job .NET
    # and MySQL test markers.
    infra.audit_a()
    infra.audit_b()
    infra.audit_c()
    docs = {key: path.read_text(encoding="utf-8") for key, path in FILES.items()}
    validate_content(docs)

    # Historical gates validate their own prior authorization, statuses
    # and document contracts. Run them again in their own workflows for
    # real proof markers; never fabricate C01/C02 executable test results.
    for module in (third, second, first, c02):
        historic = {key: path.read_text(encoding="utf-8")
                    for key, path in module.FILES.items()}
        module.validate_content(historic)

    mutations = [
        ("C03-04 owner approval forged", "acta", OWNER_REPLY, "NO APROBADO"),
        ("technical PREMERGE HEAD forged", "acta", TECHNICAL_HEAD, "0" * 40),
        ("technical merge SHA forged", "acta", TECHNICAL_SHA, "0" * 40),
        ("technical CI POSTMERGE fabricated", "acta", "42722550413", "00000000000"),
        ("C03-04 unearned next microphase", "plan",
         phase.fifth_count_before(docs["plan"]) if phase.fifth_active(next((x for x in docs["plan"].splitlines() if x.startswith("C03 | ")), "")) else "CERTIFICACIÓN (4/10)**",
         phase.fifth_count_after(docs["plan"]) if phase.fifth_active(next((x for x in docs["plan"].splitlines() if x.startswith("C03 | ")), "")) else "CERTIFICACIÓN (5/10)**"),
        ("C03-05 started prematurely", "plan",
         phase.next_locked(next((x for x in docs["plan"].splitlines() if x.startswith("C03 | ")), "")) if phase.fifth_active(next((x for x in docs["plan"].splitlines() if x.startswith("C03 | ")), "")) else "C03-05 NO INICIADA",
         phase.next_illicit(next((x for x in docs["plan"].splitlines() if x.startswith("C03 | ")), "")) if phase.fifth_active(next((x for x in docs["plan"].splitlines() if x.startswith("C03 | ")), "")) else "C03-05 INICIADA"),
        ("C04 prematurely unlocked", "plan",
         "C04 | Multitenancy, dominios y aprovisionamiento | PENDIENTE",
         "C04 | Multitenancy, dominios y aprovisionamiento | INICIADO"),
        ("C03 whole chapter forged", "plan", "C03 NO CERTIFICADO", "C03 CERTIFICADO"),
        ("canonical Word SHA forged", "acta", WORD_SHA, "0" * 64),
        ("Infrastructure real tests fabricated", "acta",
         "12 contratos ejecutables", "0 contratos ejecutables"),
        ("private authorization caveat removed", "acta",
         "No se certifican", "Sí se certifican"),
        ("technical POSTMERGE count forged", "acta",
         "3/3 workflows", "0/3 workflows"),
    ]
    for label, key, before, after in mutations:
        clone = deepcopy(docs)
        require(before in clone[key], "Missing mutation fixture: " + label)
        clone[key] = clone[key].replace(before, after)
        try:
            validate_content(clone)
        except (AssertionError, KeyError, ValueError, TypeError):
            continue
        raise AssertionError("Altered certificate accepted: " + label)
    print("C03-04 CERTIFICATION DOCUMENT GATE PASS: canonical Word and "
          "technical PR #55 evidence, real .NET10/MySQL8.4 A/B/C, prior "
          "C03-01/C03-02/C03-03/C02 gates, post-audit owner approval, "
          "C03-04 4/10 limited, C03-05 not started, C03 chapter uncertified, "
          "C04 blocked; 12/12 adverse acta mutations rejected.")
    print("LIMIT: Certified status requires separate GitHub PREMERGE and "
          "POSTMERGE verification of this acta PR's exact SHAs.")


if __name__ == "__main__":
    try:
        main()
    except (AssertionError, OSError, KeyError, ValueError, TypeError) as ex:
        print("C03-04 CERTIFICATION GATE FAILED: " + str(ex), file=sys.stderr)
        sys.exit(1)
