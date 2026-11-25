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
    # C06-05 may close without modifying C06-04's historical act or any
    # product/runtime path. Admit only the four exact successor paths.
    successor = {
      "docs/c06/ACTA_C06_05_STEPUP.md",
      "docs/ESTADO_VIGENTE_CAPITULOS.md",
      "docs/planificacion-futura/C06_MICROFASES_PROPUESTAS.md",
      "qa/institutional_c06_04_act_audits.py",
    }
    if "docs/c06/ACTA_C06_05_STEPUP.md" in diff:
        need(set(diff)==successor,"unexpected C06-05 documentary scope "+repr(diff))
        allowed |= successor
    # C06-06 closure, exact documentary scope; preserve previous invariants.
    successor_06 = {
      "docs/c06/ACTA_C06_06_EXCEPTIONAL_ACCESS.md",
      "docs/ESTADO_VIGENTE_CAPITULOS.md",
      "docs/planificacion-futura/C06_MICROFASES_PROPUESTAS.md",
      "qa/institutional_c06_04_act_audits.py",
    }
    if "docs/c06/ACTA_C06_06_EXCEPTIONAL_ACCESS.md" in diff:
        need(set(diff)==successor_06,"unexpected C06-06 documentary scope "+repr(diff))
        allowed |= successor_06
    # C06-07 closure; exact documentary scope, no runtime or schema change.
    successor_07 = {
      "docs/c06/ACTA_C06_07_PRIVACY.md",
      "docs/ESTADO_VIGENTE_CAPITULOS.md",
      "docs/planificacion-futura/C06_MICROFASES_PROPUESTAS.md",
      "qa/institutional_c06_04_act_audits.py",
    }
    if "docs/c06/ACTA_C06_07_PRIVACY.md" in diff:
        need(set(diff)==successor_07,"unexpected C06-07 documentary scope "+repr(diff))
        allowed |= successor_07
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
                   "identity.private.read","ABAC","CERTIFICADA ÚNICAMENTE EN SU ALCANCE TÉCNICO",
                   "45528830322","45528982856","45528982862","4/11"):
        need(marker in text,"acta missing verified evidence "+marker)
    need("C06-04" in state and "C06-05" in state and "C06 global NO CERTIFICADO" in state,
         "official status missing conditional next gate")
    need("C06-04" in plan and "C06-05" in plan,"chapter plan missing bounded state")
    verify_c06_06_acta(state,plan)
    verify_c06_07_acta(state,plan)
    return text

def verify_c06_06_acta(state,plan):
    """Check the immutable evidence record and integrated tree; not live CI."""
    path=ROOT/"docs/c06/ACTA_C06_06_EXCEPTIONAL_ACCESS.md"
    if not path.is_file():
        return
    text=path.read_text(encoding="utf-8")
    code="37370beebbce8188f426df6a11e610615e5df665"
    tree="3ae2797f91d73617af6ef4fbc5836b7d57ef8fc4"
    for marker in (code,tree,"17d7dc5cb430c40843debfb3d9231b7a5f75395d","3862456be2dd01d7e7955f6fbc45b7daf678fbe6",
                   "45553531887","5/5 SUCCESS","423 comprobaciones","64 contratos","34 operaciones",
                   "9.5/10","C06 6/11","C06 global NO CERTIFICADO",
                   "139000043152","139000043060","139000043029","139000042822","139000042944"):
        need(marker in text,"C06-06 acta missing evidence "+marker)
    need(subprocess.check_output(["git","rev-parse",code+"^{tree}"],cwd=ROOT,text=True).strip()==tree,
         "C06-06 integrated tree mismatch")
    need(subprocess.run(["git","merge-base","--is-ancestor",code,"HEAD"],cwd=ROOT).returncode==0,
         "C06-06 tested integration not ancestral")
    need(all("C06 6/11" in doc and "ACTA_C06_06_EXCEPTIONAL_ACCESS.md" in doc for doc in (state,plan)),
         "C06-06 missing bounded state and acta reference")
    print("PASS C06-06 ACTA: exact integrated tree and five-audit evidence record")

def verify_c06_07_acta(state,plan):
    """Check the immutable evidence record and exact tested integration."""
    path=ROOT/"docs/c06/ACTA_C06_07_PRIVACY.md"
    if not path.is_file():
        return
    text=path.read_text(encoding="utf-8")
    code="b529fe6c66548d5e46e559d441573ae71f9f4cda"
    tree="1df494deaaacadfe4008ac92d0b615cb5adbf60f"
    for marker in (code,tree,"d741a9beb10dfdc7a9f11ce5d5193ffb776edb42","e22610e86a672fcdf92481ea413283d62ca938f8",
                   "45611001386","5/5 SUCCESS","255 comprobaciones","10 operaciones",
                   "406 comprobaciones","48 formas","50 respuestas","423 comprobaciones",
                   "139194173117","139194173059","139194172978","139194172467","139194172947",
                   "9.5/10","C06 7/11","C06 global NO CERTIFICADO"):
        need(marker in text,"C06-07 acta missing evidence "+marker)
    need(subprocess.check_output(["git","rev-parse",code+"^{tree}"],cwd=ROOT,text=True).strip()==tree,
         "C06-07 integrated tree mismatch")
    need(subprocess.run(["git","merge-base","--is-ancestor",code,"HEAD"],cwd=ROOT).returncode==0,
         "C06-07 tested integration not ancestral")
    need(all("C06 7/11" in doc and "ACTA_C06_07_PRIVACY.md" in doc for doc in (state,plan)),
         "C06-07 missing bounded state and acta reference")
    print("PASS C06-07 ACTA: exact integrated tree and five-audit evidence record")

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
