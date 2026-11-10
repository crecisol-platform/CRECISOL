#!/usr/bin/env python3
"""C03-05 formal certification gate: real same-job A/B/C and historical integrity.

A GitHub Actions job only emits success after actual builds and runtime tests.
This gate does not attest remote PR statuses; the integrator must separately
verify PREMERGE and POSTMERGE completed/success on exact immutable SHAs.
"""
from copy import deepcopy
from hashlib import sha256
from pathlib import Path
import sys

import c03_05_api_audits as technical
import c03_05_status as state
import c03_04_closure_gate as fourth
import c03_03_closure_gate as third
import c03_02_closure_gate as second
import c03_01_closure_gate as first
import c02_certification_gate as c02

ROOT = Path(__file__).resolve().parents[1]
WORD = ROOT / "docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx"
WORD_SHA = "e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e"
TECH_HEAD = "3e444169b7beeacc6c891318588a582c893aa516"
TECH_SHA = "9d52ed9539b72611cc15bd71433ce7d7707dd13f"
OWNER = "OK, adelante con ello pues. termina la fase, la microfase 5 ya, ahora por favor."
PRE = ("42742553465", "42742553132", "42742553083")
POST = ("42743329418", "42743329202", "42743329425")
FILES = {
    "acta": ROOT / "docs/c03/C03_05_ACTA_MICROFASE.md",
    "spec": ROOT / "docs/c03/C03_05_ESPECIFICACION.md",
    "plan": ROOT / "docs/PLAN_37_CAPITULOS.md",
    "prep": ROOT / "docs/C03_PREPARACION.md",
    "master": ROOT / "docs/MASTER_REFERENCE.md",
    "future": ROOT / "docs/planificacion-futura/C03_MICROFASES_PROPUESTAS.md",
    "global": ROOT / "docs/PLAN_MICROFASES_37_CAPITULOS_PROPUESTO.md",
    "decisions": ROOT / "docs/DECISIONES_ABIERTAS.md",
    "agents": ROOT / "AGENTS.md",
    "fourth": ROOT / "docs/c03/C03_04_ACTA_MICROFASE.md",
}

def require(ok, reason):
    if not ok:
        raise AssertionError(reason)

def validate_content(files):
    acta, plan, prep = files["acta"], files["plan"], files["prep"]
    master, spec = files["master"], files["spec"]
    row = next((r for r in plan.splitlines() if r.startswith("C03 | ")), "")
    c04 = next((r for r in plan.splitlines() if r.startswith("C04 | ")), "")
    require(state.fifth_plan_safe(plan), "Fifth-phase status must be exact and C03/C04 locked")
    require(c04 == "C04 | Multitenancy, dominios y aprovisionamiento | PENDIENTE",
            "C04 cannot start until entire C03 chapter certified")
    require("2/37 capítulos certificados (C01 y C02)" in plan[:1900]
            and "2/37 capítulos certificados (C01 y C02)" in master,
            "Only C01/C02 chapters may be certified")
    require("ESTADO OPERATIVO VIGENTE DE C03" in master and
            "C03-05 CERRADA TÉCNICAMENTE" in master and
            "C03 NO CERTIFICADO" in master and "C04 BLOQUEADO" in master and
            WORD_SHA in master, "Canonical Word/status reference not preserved")
    for label in ("prep", "future", "global", "decisions", "agents"):
        document = files[label]
        require("C03-05 CERRADA TÉCNICAMENTE" in document and
                "5/10" in document and "C03-06 NO INICIADA" in document and
                "C03 NO CERTIFICADO" in document and "C04 BLOQUEADO" in document,
                "Fifth stage missing or downstream not locked in " + label)
    require("C03-05" in spec and "NO CERTIFICADA" in spec and
            "C03-06 NO INICIADA" in spec and "C03 NO CERTIFICADO" in spec and
            "C04 BLOQUEADO" in spec and TECH_SHA in spec,
            "Technical specification or lock changed")
    require("C03-04 CERRADA TÉCNICAMENTE" in files["fourth"] and
            "9 de noviembre de 2026" in files["fourth"] and
            "Adelante por favor con ello" in files["fourth"] and
            "C03 NO CERTIFICADO" in files["fourth"],
            "Historical C03-04 acta altered")
    for item in (
        "C03-05 CERRADA TÉCNICAMENTE Y ACEPTADA PARA CERTIFICACIÓN (5/10)",
        "FORMALIZACIÓN EN CURSO", "TODAVÍA NO CERTIFICADA",
        "antes de las pruebas", OWNER, "C03-04 CERTIFICADA",
        "4/10", "PR técnico #57", TECH_HEAD, TECH_SHA, WORD_SHA,
        "T01-R03/T01-R04", "P03/P04", "DIF-C03-01", "MAE-C03-01..08",
        "Auditoría A", "Auditoría B", "Auditoría C",
        "57/57 aserciones", "154 regresiones", "12/12 mutaciones adversariales",
        "27 HTTP", "83 Chromium", "37 casos HTTP", "MySQL8.4",
        "NO se certifican BOLA/IDOR/MFA", "Rollback",
        "3/3 completed/success", "push/completed/success",
        "CI propia", "skipped", "C03 NO CERTIFICADO", "C04 BLOQUEADO",
        "C03-06 NO INICIADA", "2/37 capítulos certificados",
    ):
        require(item.casefold() in acta.casefold(),
                "Acta C03-05 missing proof, explicit authorization or safety limit: " + item)
    require(all(ref in acta for ref in PRE + POST),
            "Acta must cite exact 3/3 PREMERGE and 3/3 POSTMERGE technical runs")
    require("C03 CERTIFICADO" not in acta and "C04 HABILITADO" not in acta,
            "Acta cannot certify chapter C03 or open C04")

def main():
    require(sha256(WORD.read_bytes()).hexdigest() == WORD_SHA,
            "Canonical master binary changed")
    technical.audit_a()
    technical.audit_b()
    technical.audit_c()
    docs = {k: p.read_text(encoding="utf-8") for k, p in FILES.items()}
    validate_content(docs)
    for module in (fourth, third, second, first, c02):
        historic = {k: p.read_text(encoding="utf-8")
                    for k, p in module.FILES.items()}
        module.validate_content(historic)
    mutants = (
        ("owner authorization removed", "acta", OWNER, "RECHAZADO"),
        ("technical premerge HEAD forged", "acta", TECH_HEAD, "0" * 40),
        ("technical merge SHA forged", "acta", TECH_SHA, "0" * 40),
        ("technical postmerge CI run forged", "acta", POST[1], "00000000000"),
        ("Word hash forged", "acta", WORD_SHA, "0" * 64),
        ("missing DTO tests", "acta", "57/57 aserciones", "0/57 aserciones"),
        ("missing security mutations", "acta", "12/12 mutaciones adversariales", "0/12 mutaciones adversariales"),
        ("private caveat stripped", "acta", "NO se certifican BOLA/IDOR/MFA",
         "SÍ se certifican BOLA/IDOR/MFA"),
        ("premature phase six", "plan", "C03-06 NO INICIADA", "C03-06 INICIADA"),
        ("premature chapter C03", "plan", "C03 NO CERTIFICADO", "C03 CERTIFICADO"),
        ("unearned C04", "plan", "C04 | Multitenancy, dominios y aprovisionamiento | PENDIENTE",
         "C04 | Multitenancy, dominios y aprovisionamiento | INICIADO"),
        ("forged microphase six", "plan",
         "CERTIFICACIÓN (5/10)**", "CERTIFICACIÓN (6/10)**"),
    )
    for name, key, before, after in mutants:
        copy = deepcopy(docs)
        require(before in copy[key], "Mutation fixture not found: " + name)
        copy[key] = copy[key].replace(before, after)  # mutate all repeated evidence copies
        try:
            validate_content(copy)
        except (AssertionError, KeyError, ValueError, TypeError):
            continue
        raise AssertionError("Unsafe C03-05 certificate mutation accepted: " + name)
    print("PASS C03-05 certification gate: canonical Word, prior C02/C03-01..04 gates, "
          "real DTO .NET10 and MySQL8.4 A/B/C, 12/12 documentation tamper attempts rejected; "
          "human instruction is before testing and certificate is conditional on external PR CI.")
    print("LIMIT: C03-05 only 5/10; C03 chapter NOT certified; C04 and C03-06 blocked.")

if __name__ == "__main__":
    try:
        main()
    except (AssertionError, KeyError, OSError, ValueError, TypeError) as exc:
        print("FAIL C03-05 certification gate: " + str(exc), file=sys.stderr)
        sys.exit(1)
