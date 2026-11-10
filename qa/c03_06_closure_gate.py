#!/usr/bin/env python3
"""CRECISOL C03-06: distinct technical acta gate, not premature certificate.

CI markers are produced only AFTER real .NET/MySQL work in the same Actions job.
GitHub PREMERGE/POSTMERGE runs must also be checked externally by exact SHA.
"""
from copy import deepcopy
from hashlib import sha256
from pathlib import Path
import sys

import c03_06_module_audits as technical
import c03_05_closure_gate as fifth
import c03_04_closure_gate as fourth
import c03_03_closure_gate as third
import c03_02_closure_gate as second
import c03_01_closure_gate as first
import c02_certification_gate as c02
import c03_05_status as state

ROOT = Path(__file__).resolve().parents[1]
WORD = ROOT / "docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx"
HASH = "e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e"
TECH_HEAD = "38a59262252bb4605e6f19eae490662da5f426a5"
TECH_MERGE = "7b96a2111f9f8483bca676b6019e3d07ec679989"
OWNER = "Sé full estricto, no omitas nada"
PRE = ("42752596034", "42752596091", "42752596048", "42752596100", "42752596222")
POST = ("42753303252", "42753303282", "42753303327", "42753303287", "42753303579")
FILES = {
    "acta": ROOT / "docs/c03/C03_06_ACTA_MICROFASE.md",
    "spec": ROOT / "docs/c03/C03_06_ESPECIFICACION.md",
    "plan": ROOT / "docs/PLAN_37_CAPITULOS.md",
    "master": ROOT / "docs/MASTER_REFERENCE.md",
    "prep": ROOT / "docs/C03_PREPARACION.md",
    "prior": ROOT / "docs/c03/C03_05_CERTIFICACION_POSTMERGE.md",
}

def require(ok, why):
    if not ok:
        raise AssertionError(why)

def validate_content(files):
    acta = files["acta"]
    current_row = next((line for line in files["plan"].splitlines() if line.startswith("C03 | ")), "")
    sixth = state.sixth_certified(current_row)
    require(state.fifth_plan_safe(files["plan"]),
            "C03-06 acta may not pre-certify the sixth microphase")
    require(state.next_locked(current_row) in files["prep"] and
            "C04 BLOQUEADO" in files["prep"],
            "Previous approved 5/10 plan must remain current until documentary gates")
    require("2/37 capítulos certificados (C01 y C02)" in files["master"]
            and HASH in files["master"], "Canonical reference/chapters changed")
    require("11/11 CI POSTMERGE" in files["prior"] and
            "12fb19b0c35038c297939e17b27770c4fd63f77e" in files["prior"],
            "Previous 5/10 certification cannot be inferred")
    require("C03-06" in files["spec"] and
            "Service Locator" in files["spec"] and HASH in files["spec"],
            "C03-06 spec absent/unauthorized")
    for token in (
        "C03-06 CERRADA TÉCNICAMENTE Y ACEPTADA PARA CERTIFICACIÓN (6/10)",
        "NO CERTIFICADA", "antes de ejecutar las pruebas", OWNER, HASH,
        "T01-R03", "T01-R04", "T01-R05", "MAE-C03-01..08",
        "C03-05 CERTIFICADA (5/10)", "19/19 CI POSTMERGE",
        TECH_HEAD, TECH_MERGE, "5/5 CI PREMERGE SUCCESS",
        "25/25 assertions", "15/15 mutaciones adversariales",
        "27/27 HTTP", "83 Chromium", "37 casos HTTP",
        "Auditoría", "C03-07 NO INICIADA",
        "C03 NO CERTIFICADO", "C04 BLOQUEADO", "2/37 capítulos",
        "Service Locator", "Rollback", "CI documental",
        "No se registran", "C01-04", "skipped",
    ):
        require(token.casefold() in acta.casefold(), "Acta missing proof or boundary: " + token)
    for rid in PRE + POST:
        require(rid in acta, "Acta run id missing: " + rid)
    require("C03 CERTIFICADO" not in acta and "C04 HABILITADO" not in acta,
            "Cannot certify C03 or enable C04")
    if sixth:
        final_doc = ROOT / "docs/c03/C03_06_CERTIFICACION_POSTMERGE.md"
        require(final_doc.is_file(), "Final sixth-stage proof absent")
        proof = final_doc.read_text(encoding="utf-8")
        for marker in (state.SIXTH_MERGE, "5/5 CI POSTMERGE",
                       "C03-06 CERTIFICADA (6/10)", "C03-07 NO INICIADA",
                       "C03 NO CERTIFICADO", "C04 BLOQUEADO", HASH, "PR #61"):
            require(marker in proof, "Sixth-stage proof missing: " + marker)
        require(state.SIXTH_MERGE in files["plan"],
                "Final acta SHA missing from canonical current plan")
    require("NO son afirmaciones anticipadas de SUCCESS" in acta,
            "POSTMERGE technical run IDs must not be pre-claimed as SUCCESS")

def document_mutations(docs):
    cases = (
        ("delete owner", "acta", OWNER, "AUTORIZACION AUSENTE"),
        ("forge Word SHA", "acta", HASH, "0" * 64),
        ("forge PR60 head", "acta", TECH_HEAD, "0" * 40),
        ("forge technical merge", "acta", TECH_MERGE, "0" * 40),
        ("forge PREMERGE run", "acta", PRE[0], "00000000000"),
        ("forge POSTMERGE run", "acta", POST[0], "00000000000"),
        ("erase module tests", "acta", "25/25 assertions", "0/25 assertions"),
        ("erase DI mutation proof", "acta", "15/15 mutaciones adversariales",
         "0/15 mutaciones adversariales"),
        ("unearned chapter", "acta", "C03 NO CERTIFICADO", "C03 CERTIFICADO"),
        ("unearned C04", "acta", "C04 BLOQUEADO", "C04 HABILITADO"),
        ("unearned next phase", "acta", "C03-07 NO INICIADA", "C03-07 INICIADA"),
        ("forge certification", "acta", "NO CERTIFICADA", "CERTIFICADA"),
    )
    for label, key, before, after in cases:
        require(before in docs[key], "Invalid negative mutation fixture: " + label)
        modified = deepcopy(docs)
        modified[key] = modified[key].replace(before, after)
        try:
            validate_content(modified)
        except (AssertionError, KeyError, ValueError, TypeError):
            continue
        raise AssertionError("Unsafe C03-06 documentary mutation accepted: " + label)
    print("PASS C03-06 ACTA B: 12/12 documentary tampering mutations rejected.")

def main():
    require(sha256(WORD.read_bytes()).hexdigest() == HASH, "Master Word altered")
    technical.audit_a()
    technical.audit_b()
    technical.audit_c()
    docs = {name: path.read_text(encoding="utf-8") for name, path in FILES.items()}
    validate_content(docs)
    for older in (fifth, fourth, third, second, first, c02):
        older.validate_content({name: p.read_text(encoding="utf-8")
                                for name, p in older.FILES.items()})
    document_mutations(docs)
    print("PASS C03-06 documentary gate: real previous A/B/C and 12/12 negative doc mutations, "
          "human authorization prior to tests; independent PR CI verification still required.")
    print("LIMIT: technical closure 6/10, NOT yet certified; C03-07 and C04 blocked.")

if __name__ == "__main__":
    try:
        main()
    except (AssertionError, KeyError, OSError, TypeError, ValueError) as error:
        print("FAIL C03-06 ACTA gate: " + str(error), file=sys.stderr)
        sys.exit(1)
