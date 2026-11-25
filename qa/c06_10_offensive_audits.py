#!/usr/bin/env python3
"""C06-10 source and independent inventory gates; runtime evidence separate."""
import json,os,subprocess,sys
from pathlib import Path
from c06_10_offensive_scope import require_exact
ROOT=Path(__file__).resolve().parents[1]
BASE="42e89226b3e53dfa8ec10da1ddf7961d4ce290f3"
def need(ok,msg):
    if not ok: raise AssertionError("C06-10 NO-GO "+msg)
def read(p): return (ROOT/p).read_text(encoding="utf-8")
def git(*args): return subprocess.check_output(["git",*args],cwd=ROOT,text=True).strip()
def scope():
    event=os.getenv("GITHUB_EVENT_NAME","")
    if event=="pull_request":
        need(os.getenv("GITHUB_BASE_REF")=="dev","PR outside dev")
        args=["origin/dev...HEAD"]
    elif event=="push":
        need(os.getenv("GITHUB_REF")=="refs/heads/dev","push outside dev")
        args=["HEAD^","HEAD"]
    else: args=[BASE,"HEAD"]
    changed=git("diff","--name-only",*args).splitlines()
    require_exact(changed)
    for bad in ("frontend/unreviewed.ts","backend/src/unreviewed.cs","database/operations/unreviewed.sql"):
        try: require_exact(changed+[bad])
        except AssertionError: pass
        else: raise AssertionError("extra path accepted")
    try: require_exact(changed[1:])
    except AssertionError: pass
    else: raise AssertionError("missing path accepted")
    for path in ("frontend/","backend/","database/migrations/","database/operations/","docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx","docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.pdf","docs/c06/C06_01_CATALOGO_ACCIONES.json","docs/c06/C06_02_MATRIZ_ROLES_CAPACIDADES.json"):
        need(not git("diff","--name-only",BASE,"HEAD","--",path),"unapproved product/authority change "+path)
def inventory():
    from c06_01_permissions_catalog_audit import verify_catalog
    cat=json.loads(read("docs/c06/C06_01_CATALOGO_ACCIONES.json"))
    overlay=json.loads(read("docs/c06/C06_06_CATALOGO_ACTIVACION.json"))
    verify_catalog(cat,read("backend/src/Crecisol.Api/Modules/Identity/OperationalIdentityModule.cs"),read("backend/src/Crecisol.Api/Program.cs"))
    m=json.loads(read("docs/c06/C06_10_MATRIZ_REGRESION.json"));ops=m["operations"]
    canonical={a["operation_code"]:a for a in cat["api_actions"]+overlay["actions"] if a.get("operation_code")}
    need(len(ops)==len(canonical)==79 and len({x["operation"] for x in ops})==79,"incomplete or duplicate inventory")
    need(set(canonical)=={x["operation"] for x in ops}==set(json.loads(read("docs/c06/C06_07_CONTRATOS_RESPUESTA.json"))["operations"]),"catalog/oracle mismatch")
    counts={}
    for row in ops:
        a=canonical[row["operation"]]
        need((row["realm"],row["boundary"],row["fields"])==(a["realm"],a["access_boundary"],a["required_fields"]),"matrix contract mismatch "+row["operation"])
        mode=("proof_regression" if a["access_boundary"] in ("tenant_preauth_or_proof","platform_recovery_or_challenge")
              else "browser_session" if row["operation"].endswith("/restore") else "bearer_session")
        need(row["mode"]==mode,"coverage classification drift "+row["operation"])
        counts[mode]=counts.get(mode,0)+1
    need(counts=={"bearer_session":55,"browser_session":2,"proof_regression":22},"matrix counts")
    need(m["session_scenarios"]==["anonymous","forged","wrong_realm","wrong_host"],"matrix scenarios")
    need(len(m["other_http"])==5 and set(x["action"] for x in m["other_http"])=={a["action_id"] for a in cat["api_actions"] if not a.get("operation_code")},"non-operational HTTP inventory")
    need("NO CERTIFICADA" in read("docs/c06/C06_10_ESPECIFICACION.md"),"premature certificate")
    print("PASS C06-10 inventory: 79 operational, 57 session, 22 independent proof, 228 required denial cells, five separate HTTP entries")
def harness():
    h=read("qa/c06_10_offensive_checks.mjs");e=read("qa/c05_operational_flow_e2e.mjs");q=read("qa/c06_08_query_checks.mjs")
    for s in ("done.size===228","stableSnapshot(sql)===beforeState","auditCounts(sql)","(after.get(key)||0)-(before.get(key)||0)===n","await sentinel(realm)","r.status===401","foreign pending applicant state and revision unchanged","coworker sessions remain active","forbidden approval leaves row unchanged","modal.getByRole('alert')","axe.violations","modal fits viewport","authorized retry succeeds"):
        need(s in h,"missing independently checked condition "+s)
    for s in ("verifyAuthorizationMatrix(","verifySameTenantObjects(","verifyBrowserDenial(","real unused A password challenge","if (c06Offensive && !c06Delivery)"):
        need(s in e,"missing runner gate "+s)
    need("verifyCrossTenantObjects(" in q and "nextProof" in q,"foreign concrete target regression missing")
    need("contents: read" in read(".github/workflows/c06-10-offensive-regression.yml"),"workflow permission drift")
def previous():
    from c06_04_abac_audits import static_guards,negative_gate
    from c06_09_delivery_audits import boundary
    static_guards();negative_gate();boundary()
def persistence():
    s=read("database/tests/c06_10_offensive_mysql.sh")
    for marker in ("--protocol=SOCKET","SHOW GRANTS FOR","same binary object IDs","1452","1142","ERROR $3","runtime cannot mint assignment","foreign assignment remains absent","PASS C06-10 D MYSQL"):
        need(marker in s,"missing exact SQL evidence "+marker)
    scope()
if __name__=="__main__":
    stage=sys.argv[1] if len(sys.argv)==2 else ""
    need(stage in ("A","B","C","D"),"stage")
    if stage=="A": scope();inventory()
    elif stage=="B": inventory();harness();previous()
    elif stage=="C": scope();inventory();harness();previous()
    else: persistence()
    print("PASS C06-10 "+stage+": source review; runtime evidence required separately")
