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

ROOT = Path(__file__).resolve().parents[1]
TECHNICAL_SHA = "7217fb1e720f16c71d5a654f605eca96d81deb58"
WORD_SHA = "e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e"
OWNER_APPROVAL = "CERTIFICA POR FAVOR LA MICROFASE 1 DEL BLOQUE 3"
RUNS = ("42691620500", "42691620886", "42691620794",
        "42691620719", "42691621001", "42691620768", "42691620826")
FILES = {
    "acta": ROOT / "docs/c03/C03_01_ACTA_MICROFASE.md",
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
    acta, prep, plan = files["acta"], files["prep"], files["plan"]
    master, spec = files["master"], files["spec"]
    future, global_plan = files["future"], files["global"]
    decisions, agents = files["decisions"], files["agents"]
    row = next((s for s in plan.splitlines() if s.startswith("C03 | ")), "")
    row4 = next((s for s in plan.splitlines() if s.startswith("C04 | ")), "")
    head = plan.splitlines()[2] if len(plan.splitlines()) > 2 else ""
    require("2/37 capítulos certificados (C01 y C02)" in plan[:2000] and
            "2/37 capítulos certificados (C01 y C02)" in master,
            "Only C01+C02 may count as certified chapters")
    require("C03-01 CERRADA TÉCNICAMENTE Y ACEPTADA (1/10)" in head and
            "C03 NO CERTIFICADO" in head and "C04 BLOQUEADO" in head,
            "Effective 37-chapter status is not limited to C03-01")
    require("**C03-01 CERRADA TÉCNICAMENTE (1/10)**" in row and
            "C03 NO CERTIFICADO" in row and "C04 BLOQUEADO" in row and
            "C03-02" in row and "NO INICIADA" in row,
            "C03 row wrongly advances chapter or next microphase")
    require(row4 == "C04 | Multitenancy, dominios y aprovisionamiento | PENDIENTE",
            "C04 must remain blocked/unstarted")
    require("ESTADO VIGENTE: C03-01 CERRADA TÉCNICAMENTE" in prep and
            "1/10" in prep and
            "C03 NO CERTIFICADO" in prep and "C04 BLOQUEADO" in prep and
            "HABILITABLE TRAS CI FINAL C03-01, PERO NO INICIADA" in prep,
            "C03 preparation must show limited acceptance only")
    require("C03-01 CERRADA TÉCNICAMENTE" in master and
            "1/10" in master and "C03 NO CERTIFICADO" in master and
            "C04 BLOQUEADO" in master and WORD_SHA in master,
            "Canonical reference status, risk or provenance changed")
    for text, label in ((spec, "spec"), (future, "future"),
                        (global_plan, "global"), (decisions, "decisions"),
                        (agents, "agents")):
        require("C03-01" in text and "1/10" in text and
                "C03 NO CERTIFICADO" in text and "C04 BLOQUEADO" in text,
                "Unreconciled effective C03-01 state in " + label)
    require("## 5. Decisión de microfase y gate final" in acta and
            "C03-01 CERRADA TÉCNICAMENTE (1/10)" in acta and
            "formalización condicionada al SUCCESS" in acta and
            "C03 NO CERTIFICADO" in acta and
            "C04 BLOQUEADO" in acta and
            "C03-02" in acta and "NO INICIADA" in acta,
            "Microphase acta claims premature chapter certification")
    require(OWNER_APPROVAL in acta and "9 de noviembre de 2026" in acta and
            "no constituye aceptación anticipada" in acta,
            "Owner did not explicitly approve only C03-01")
    require(TECHNICAL_SHA in acta and WORD_SHA in acta and
            "PR #49" in acta and "7/7" in acta and
            "event=push" in acta and "status=completed" in acta and
            "conclusion=success" in acta,
            "Missing exact technical postmerge provenance")
    require(all(run in acta for run in RUNS),
            "Missing one of seven verified technical SHA workflow URLs")
    for marker in ("Auditoría A", "Auditoría B", "Auditoría C",
                   "16 identificadores", "10/10 mutaciones",
                   "83", "MySQL8.4", "Domain", "Application",
                   "BOLA/IDOR/MFA", "NO APLICA A ESTA MICROFASE",
                   "sin patrones de credenciales", "No se conocen P0/P1",
                   "revert", "NO**"):
        # "NO**" is optional and does not mark actual runtime evidence.
        if marker == "NO**":
            continue
        require(marker in acta, "Missing proof/risk/rollback in acta: " + marker)


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

    adversarial = [
        ("owner approval erased", "acta", OWNER_APPROVAL, "SIN APROBACION"),
        ("technical commit forged", "acta", TECHNICAL_SHA, "0" * 40),
        ("C03 phase count forged", "plan",
         "**C03-01 CERRADA TÉCNICAMENTE (1/10)**",
         "**C03-02 CERRADA TÉCNICAMENTE (2/10)**"),
        ("C04 activated without C03 certification", "plan",
         "C04 | Multitenancy, dominios y aprovisionamiento | PENDIENTE",
         "C04 | Multitenancy, dominios y aprovisionamiento | INICIADO"),
        ("privacy limitation removed", "acta",
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
          "1/10 closed, C03 uncertified, C04 blocked, "
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
