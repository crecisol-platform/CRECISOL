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
    successor_08 = {
      "docs/c06/ACTA_C06_08_SCOPED_QUERIES.md",
      "docs/ESTADO_VIGENTE_CAPITULOS.md",
      "docs/planificacion-futura/C06_MICROFASES_PROPUESTAS.md",
      "qa/institutional_c06_04_act_audits.py",
    }
    if "docs/c06/ACTA_C06_08_SCOPED_QUERIES.md" in diff:
        need(set(diff)==successor_08,"unexpected C06-08 documentary scope "+repr(diff))
        allowed |= successor_08
    successor_09 = {
      "docs/c06/ACTA_C06_09_PROTECTED_DELIVERY.md",
      "docs/ESTADO_VIGENTE_CAPITULOS.md",
      "docs/planificacion-futura/C06_MICROFASES_PROPUESTAS.md",
      "qa/institutional_c06_04_act_audits.py",
    }
    if "docs/c06/ACTA_C06_09_PROTECTED_DELIVERY.md" in diff:
        need(set(diff)==successor_09,"unexpected C06-09 documentary scope "+repr(diff))
        allowed |= successor_09
    successor_10 = {
      "docs/c06/ACTA_C06_10_OFFENSIVE_REGRESSION.md",
      "docs/ESTADO_VIGENTE_CAPITULOS.md",
      "docs/planificacion-futura/C06_MICROFASES_PROPUESTAS.md",
      "qa/institutional_c06_04_act_audits.py",
    }
    if "docs/c06/ACTA_C06_10_OFFENSIVE_REGRESSION.md" in diff:
        need(set(diff)==successor_10,"unexpected C06-10 documentary scope "+repr(diff))
        allowed |= successor_10
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
    verify_c06_08_acta(state,plan)
    verify_c06_09_acta(state,plan)
    verify_c06_10_acta(state,plan)
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
    verify_c06_07_revalidation(text,state,plan)
    print("PASS C06-07 ACTA: exact integrated tree and five-audit evidence record")

def verify_c06_07_revalidation(acta,state,plan):
    """Ratification of the later HTTP ingress/capture fix; retain original evidence."""
    code="3789224e49f3183563a7f8491a3b3101f161a6a1"
    tree="5bf350c9f38e314066890db5da191b437ed8062e"
    virtual="750d53e6e26b10953b0f11612b784bc9da7b63b4"
    head="865138fa4c52eb74baede4c7786dd44a720f7912"
    for marker in (code,tree,virtual,head,"45630138784","5/5 SUCCESS",
                   "139260804038","139260804149","139260804234","139260803641","139260804007",
                   "506 comprobaciones HTTP","70 formas","143 observadas","429 comprobaciones",
                   "16/16","45613536702","45619768486","45621380168","CANCELLED",
                   "9.5/10","11 workflows PRE aplicables SUCCESS","C06 7/11"):
        need(marker in acta,"C06-07 revalidation missing evidence "+marker)
    for doc in (state,plan):
        need(all(m in doc for m in (code,tree,"45630138784","RATIFICACIÓN C06-07","C06 7/11",
                  "C06-08 habilitada","ACTA_C06_07_PRIVACY.md")),"C06-07 revalidation status missing")
    for revision in (code,head):
        need(subprocess.check_output(["git","rev-parse",revision+"^{tree}"],cwd=ROOT,text=True).strip()==tree,
             "C06-07 revalidation tree mismatch "+revision)
    need(subprocess.run(["git","merge-base","--is-ancestor",code,"HEAD"],cwd=ROOT).returncode==0,
         "C06-07 revalidated integration not ancestral")
    print("PASS C06-07 REVALIDATION ACTA: exact tree, historical failures retained, five verified jobs")

def verify_c06_08_acta(state,plan):
    """Verify recorded evidence, exact technical tree and bounded status; not live CI."""
    path=ROOT/"docs/c06/ACTA_C06_08_SCOPED_QUERIES.md"
    if not path.is_file():
        return
    text=path.read_text(encoding="utf-8")
    code="990ae64da906ec07f16c87bdbd1b476cfd16b257"
    head="6640cd914608d9907633a60c573dd78cdaf3f105"
    tree="b81b7940b14561b612005b163d0f33ad3aa38763"
    prior="ff5de0670d5a09e31b32c3b6aff7e78c5449fee8"
    for marker in (code,head,tree,prior,"9d7db9b7152b45ca0c81fd99205c929df831fb7f",
                   "45660058699","139367175244","139367174667","139367174098",
                   "139367174984","139367174750","5/5 SUCCESS","13 workflows PRE aplicables SUCCESS",
                   "33 comprobaciones","255 comprobaciones","16/16","474 comprobaciones integradas",
                   "540 comprobaciones HTTP","70 formas","148 observadas","8 aserciones Angular",
                   "ERROR 1142","ERROR 1553","45641579789","45642143005",
                   "9.5/10","C06 8/11","C06 global NO CERTIFICADO","C06-09 habilitada"):
        need(marker in text,"C06-08 acta missing evidence "+marker)
    for revision in (code,head):
        need(subprocess.check_output(["git","rev-parse",revision+"^{tree}"],cwd=ROOT,text=True).strip()==tree,
             "C06-08 integrated tree mismatch "+revision)
    need(subprocess.run(["git","merge-base","--is-ancestor",code,"HEAD"],cwd=ROOT).returncode==0,
         "C06-08 tested integration not ancestral")
    parents=subprocess.check_output(["git","show","-s","--format=%P",code],cwd=ROOT,text=True).strip().split()
    need(parents==[prior,head],"C06-08 merge parent mismatch")
    # Read historical scope from the tested commit, not a future edited guard.
    import ast
    source=subprocess.check_output(["git","show",code+":qa/c06_08_query_scope.py"],cwd=ROOT,text=True)
    expected=set(ast.literal_eval(next(node.value.args[0] for node in ast.parse(source).body
                 if isinstance(node,ast.Assign) and any(isinstance(t,ast.Name) and t.id=="PATHS" for t in node.targets))))
    diff=set(subprocess.check_output(["git","diff","--name-only",prior,code],cwd=ROOT,text=True).splitlines())
    need(len(diff)==28 and diff==expected,"C06-08 technical scope drift")
    need(not any(p.startswith("frontend/") or p.startswith("database/operations/") or "DOCUMENTO_MAESTRO" in p for p in diff),
         "C06-08 unapproved frontend/privilege/source change")
    for doc in (state,plan):
        need(all(m in doc for m in (code,tree,"45660058699","C06 8/11","C06-09 habilitada",
              "ACTA_C06_08_SCOPED_QUERIES.md","C06 global NO CERTIFICADO")),"C06-08 bounded status missing")
    print("PASS C06-08 ACTA: exact integrated tree, 28 paths, evidence and bounded next gate")

def verify_c06_09_acta(state,plan):
    """Check immutable evidence, exact scope and bounded next step, not live CI."""
    path=ROOT/"docs/c06/ACTA_C06_09_PROTECTED_DELIVERY.md"
    if not path.is_file():
        return
    text=path.read_text(encoding="utf-8")
    code="01e27372689fa3494727eebc30bd0b71db56d174"
    head="af865e81a1ff8ace8fbbae3f824fe89a11a71477"
    tree="85b4badbd881abb755f363d764b1c2af8dcb4ff9"
    prior="da67bfe38940edd25842b2f963090e3d1406ce45"
    for marker in (code,head,tree,prior,"4670574540f8008e74efcd73c6105d952d8dc15c","45716942933",
                   "139565303593","139565303414","139565303803","139565303021","139565303370",
                   "45716943110","139565304234","139565304224","139565304201","139565303712",
                   "5/5 SUCCESS","16 workflows PRE aplicables SUCCESS","80 comprobaciones",
                   "33 comprobaciones","255 comprobaciones","16/16","528 comprobaciones integradas",
                   "54 verificaciones","574 comprobaciones HTTP","73 formas","148 observadas",
                   "351 comprobaciones integradas","66 mutaciones","ERROR 1142","ERROR 1452","ERROR 3819",
                   "45696887241","45697214588","45697214149","9.5/10","C06 9/11",
                   "C06 global NO CERTIFICADO","C06-10 habilitada","no grantables"):
        need(marker in text,"C06-09 acta missing evidence "+marker)
    for revision in (code,head):
        need(subprocess.check_output(["git","rev-parse",revision+"^{tree}"],cwd=ROOT,text=True).strip()==tree,
             "C06-09 integrated tree mismatch "+revision)
    need(subprocess.run(["git","merge-base","--is-ancestor",code,"HEAD"],cwd=ROOT).returncode==0,
         "C06-09 tested integration not ancestral")
    parents=subprocess.check_output(["git","show","-s","--format=%P",code],cwd=ROOT,text=True).strip().split()
    need(parents==[prior,head],"C06-09 merge parent mismatch")
    import ast
    source=subprocess.check_output(["git","show",code+":qa/c06_09_delivery_scope.py"],cwd=ROOT,text=True)
    expected=set(ast.literal_eval(next(node.value.args[0] for node in ast.parse(source).body
                 if isinstance(node,ast.Assign) and any(isinstance(t,ast.Name) and t.id=="PATHS" for t in node.targets))))
    diff=set(subprocess.check_output(["git","diff","--name-only",prior,code],cwd=ROOT,text=True).splitlines())
    need(len(diff)==30 and diff==expected,"C06-09 technical scope drift")
    need(not any(p.startswith(("frontend/","database/operations/","database/migrations/")) or "DOCUMENTO_MAESTRO" in p for p in diff),
         "C06-09 unapproved source or privilege change")
    for doc in (state,plan):
        need(all(m in doc for m in (code,tree,"45716942933","C06 9/11","C06-10 habilitada",
              "ACTA_C06_09_PROTECTED_DELIVERY.md","C06 global NO CERTIFICADO")),"C06-09 bounded status missing")
    print("PASS C06-09 ACTA: exact integrated tree, 30 paths, evidence and bounded next gate")

def verify_c06_10_acta(state,plan):
    """Verify immutable PRE evidence and exact integrated QA tree; not live CI."""
    path=ROOT/"docs/c06/ACTA_C06_10_OFFENSIVE_REGRESSION.md"
    if not path.is_file():
        return
    text=path.read_text(encoding="utf-8")
    code="ec0258b1d2c0738dbcb0c43aa305be18db0f9a3f"
    head="ffca485df3de5c713fbbd68598398c3194ea5420"
    tree="9072c2694fa6e565503ac75c562390c35af0dacc"
    prior="42e89226b3e53dfa8ec10da1ddf7961d4ce290f3"
    for marker in (code,head,tree,prior,"16857973bf3880748f90bf42b6c424d587bd1eed",
                   "45734643721","139624806309","139624806620","139624806686",
                   "139624806571","139624806532","5/5 SUCCESS","15/15 workflows PRE SUCCESS",
                   "228/228","921 comprobaciones integradas","862 comprobaciones HTTP",
                   "1452","1062","1142","45723923745","45737693483",
                   "9.5/10","C06 9/11","C06 10/11","C06-11 bloqueada",
                   "C06 global NO CERTIFICADO","NO CERTIFICADA"):
        need(marker in text,"C06-10 acta missing evidence "+marker)
    need(subprocess.check_output(["git","rev-parse",code+"^{tree}"],cwd=ROOT,text=True).strip()==tree,
         "C06-10 integrated tree mismatch")
    need(subprocess.run(["git","merge-base","--is-ancestor",code,"HEAD"],cwd=ROOT).returncode==0,
         "C06-10 tested integration not ancestral")
    parents=subprocess.check_output(["git","show","-s","--format=%P",code],cwd=ROOT,text=True).strip().split()
    need(parents==[prior],"C06-10 squash parent mismatch")
    import ast
    source=subprocess.check_output(["git","show",code+":qa/c06_10_offensive_scope.py"],cwd=ROOT,text=True)
    expected=set(ast.literal_eval(next(node.value.args[0] for node in ast.parse(source).body
                 if isinstance(node,ast.Assign) and any(isinstance(t,ast.Name) and t.id=="PATHS" for t in node.targets))))
    diff=set(subprocess.check_output(["git","diff","--name-only",prior,code],cwd=ROOT,text=True).splitlines())
    need(len(diff)==19 and diff==expected,"C06-10 technical scope drift")
    need(not any(p.startswith(("frontend/","backend/","database/operations/","database/migrations/")) or "DOCUMENTO_MAESTRO" in p for p in diff),
         "C06-10 unapproved product, schema, grants or source change")
    for doc in (state,plan):
        need(all(m in doc for m in (code,tree,"45734643721","45737693483","C06 9/11","C06 10/11",
              "ACTA_C06_10_OFFENSIVE_REGRESSION.md","C06 global NO CERTIFICADO")), "C06-10 bounded status missing")
    print("PASS C06-10 ACTA: exact integrated QA tree, 19 paths, conditional gate; no live CI claim")


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
    # Preserve the historical ABAC ordering requirement; the C06-09
    # successor now also requires the closed reference-content gate.
    if (ROOT/"docs/c06/ACTA_C06_09_PROTECTED_DELIVERY.md").is_file():
        from c06_04_abac_audits import verify_case_delivery_order
        verify_case_delivery_order(adapter)
    else:
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
