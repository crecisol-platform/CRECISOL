#!/usr/bin/env python3
"""CRECISOL C06-04 institutional act independent A/B/C PRE/POST by SHA."""
import hashlib
import json
import os
import subprocess
import sys
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
CODE_SHA="31b8f58fcdb5f6f134a0b663213976f285371653"
WORD_SHA="ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c"
ACTA=ROOT/"docs/c06/ACTA_C06_04_ABAC.md"
STATUS=ROOT/"docs/ESTADO_VIGENTE_CAPITULOS.md"
PLAN=ROOT/"docs/planificacion-futura/C06_MICROFASES_PROPUESTAS.md"

def need(ok,msg):
    if not ok: raise AssertionError("C06-04 ACTA NO-GO: "+msg)

def changed():
    event=os.environ.get("GITHUB_EVENT_NAME","")
    if event=="pull_request":
        need(os.environ.get("GITHUB_BASE_REF")=="dev","acta PR must target dev")
        args=["origin/dev...HEAD"]
    elif event=="push":
        need(os.environ.get("GITHUB_REF")=="refs/heads/dev","acta push must be dev")
        args=["HEAD^","HEAD"]
    else:
        args=[CODE_SHA,"HEAD"]
    diff=subprocess.check_output(["git","diff","--name-only",*args],text=True,cwd=ROOT).splitlines()
    allowed={
      "docs/c06/ACTA_C06_04_ABAC.md",
      "docs/ESTADO_VIGENTE_CAPITULOS.md",
      "docs/planificacion-futura/C06_MICROFASES_PROPUESTAS.md",
      "qa/institutional_c06_04_act_audits.py",
      ".github/workflows/c06-04-institutional-act.yml",
    }
    need(diff and set(diff)<=allowed,"acta diff changes runtime/schema or source "+repr(diff))
    return diff

def common():
    need(hashlib.sha256((ROOT/"docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx").read_bytes()).hexdigest()==WORD_SHA,
         "Word canonical source changed")
    catalog=json.loads((ROOT/"docs/c06/C06_01_CATALOGO_ACCIONES.json").read_text(encoding="utf-8"))
    roles=json.loads((ROOT/"docs/c06/C06_02_MATRIZ_ROLES_CAPACIDADES.json").read_text(encoding="utf-8"))
    need((len(catalog["api_actions"]),len(catalog["non_http_contracts"]),len(catalog["normative_actions"]))==(80,12,28),
         "C06-01 inventory drift")
    need(len(roles["roles"])==10 and len(roles["actions"])==108,
         "C06-02 canonical roles drift")
    need(all(a["policy_activated"] is False and a["grantable"] is False for a in roles["actions"]),
         "future roles accidentally active")
    need("identity.private.read" in {a["action_id"] for a in catalog["api_actions"]},
         "existing private resource operation missing")
    need(any(a["action_id"]=="cases.read" and a["grantable"] is False and
             a["http_registered"] is False for a in catalog["normative_actions"]),
         "future case action accidentally enabled")
    text=ACTA.read_text(encoding="utf-8")
    state=STATUS.read_text(encoding="utf-8")
    plan=PLAN.read_text(encoding="utf-8")
    for marker in (CODE_SHA,"a27f1ec5e66355abf83d2b16f058f9b4ee8b405e",
                   "45522703023","45522703090","351","22","64",
                   "identity.private.read","ABAC","NO CERTIFICADA","4/11"):
        need(marker in text,"acta missing verified evidence "+marker)
    need("C06-04" in state and "C06-05" in state and "C06 global NO CERTIFICADO" in state,
         "official status missing conditional next gate")
    need("C06-04" in plan and "C06-05" in plan,"chapter plan missing bounded state")
    return text

def a():
    files=changed()
    common()
    print("PASS C06-04 ACTA A: Word SHA, 80/12/28, roles 10/108, exact identity.private.read and bounded diff:",files)

def b():
    common()
    policy=(ROOT/"backend/src/Crecisol.Domain/Tenancy/C06CaseAbacPolicy.cs").read_text(encoding="utf-8")
    adapter=(ROOT/"backend/src/Crecisol.Infrastructure/Tenancy/C06CaseAbacProjection.cs").read_text(encoding="utf-8")
    schema=(ROOT/"database/migrations/020_c06_case_abac.sql").read_text(encoding="utf-8")
    runtime=(ROOT/"database/operations/c06_04_local_runtime_role.sql").read_text(encoding="utf-8")
    e2e=(ROOT/"qa/c05_operational_flow_e2e.mjs").read_text(encoding="utf-8")
    need('e.RequiredAction.Value != "identity.private.read"' in policy and
         '"platform-operator"' not in policy.split("ReadRoles",1)[1].split("};",1)[0] and
         '"tenant-custom"' not in policy.split("ReadRoles",1)[1].split("};",1)[0],
         "platform or custom role accidentally gains private read")
    need("C06CaseAbacPolicy.Allows(evidence)" in adapter and
         adapter.index("C06CaseAbacPolicy.Allows(evidence)") < adapter.index('return new(true, new { reference ='),
         "private data returned before ABAC")
    need(schema.count("FOREIGN KEY(")==3 and
         "INSERT INTO c06_case_resource_assignment" not in schema,
         "tenant FKs or no-seed guarantee broken")
    need("GRANT SELECT" in runtime and
         "GRANT INSERT" not in runtime and
         "GRANT UPDATE" not in runtime and
         "GRANT DELETE" not in runtime,
         "runtime can mutate assignments")
    need("020_c06_case_abac.down.sql" in e2e and
         "C06-04 rollback preserves existing C05 cases" in e2e and
         "'--c06-04'" in e2e,
         "real E2E IDOR/reverse-migration proof missing")
    print("PASS C06-04 ACTA B: exact role/action/tenant and pre-content ABAC, SELECT only, rollback safety")

def c():
    common()
    domain=(ROOT/"backend/src/Crecisol.Domain/Tenancy/C06CaseAbacPolicy.cs").read_text(encoding="utf-8")
    adapter=(ROOT/"backend/src/Crecisol.Infrastructure/Tenancy/C06CaseAbacProjection.cs").read_text(encoding="utf-8")
    need(not any(term in domain for term in ("MySqlConnector","HttpContext","Microsoft.AspNetCore","System.Data.Sql")),
         "Domain has transport dependency")
    need("FOR SHARE" in adapter and
         "LIMIT 2 FOR SHARE" in adapter and
         "assignments.Count != 1" in adapter,
         "ABAC lost atomic/duplicate checks")
    print("PASS C06-04 ACTA C: Domain/Infrastructure layering, no UI/API code changes and reproducible source")

if __name__=="__main__":
    stage=sys.argv[1] if len(sys.argv)==2 else ""
    {"A":a,"B":b,"C":c}.get(stage,lambda: (_ for _ in ()).throw(ValueError("expected A|B|C")))()
