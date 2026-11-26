#!/usr/bin/env python3
"""C06-06 scoped source audits; runtime results require actual .NET/HTTP/MySQL."""
import json
import os
import subprocess
import sys
from pathlib import Path
from c06_06_exceptional_scope import require_exact

ROOT=Path(__file__).resolve().parents[1]
BASE="50d880ed5817a6b48cd112ad6f65f4ae581b8761"
def need(ok,msg):
    if not ok: raise AssertionError("C06-06 NO-GO "+msg)
def read(path): return (ROOT/path).read_text(encoding="utf-8")
def scope():
    event=os.environ.get("GITHUB_EVENT_NAME")
    if event=="pull_request":
        need(os.environ.get("GITHUB_BASE_REF")=="dev","PR must target dev")
        args=["origin/dev...HEAD"]
    elif event=="push":
        need(os.environ.get("GITHUB_REF")=="refs/heads/dev","push must be dev")
        args=["HEAD^","HEAD"]
    else: args=[BASE,"HEAD"]
    require_exact(subprocess.check_output(["git","diff","--name-only",*args],cwd=ROOT,text=True).splitlines())
def guard():
    api=read("backend/src/Crecisol.Api/Modules/Identity/OperationalIdentityModule.cs")
    p=read("backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.Platform.cs")
    infra=read("backend/src/Crecisol.Infrastructure/Tenancy/C06ExceptionalAccess.cs")
    schema=read("database/migrations/021_c06_exceptional_access.sql")
    role=read("database/operations/c06_06_local_runtime_role.sql")
    policy=read("backend/src/Crecisol.Domain/Authorization/C06ExceptionalAccessPolicy.cs")
    need('CRECISOL_C06_BREAKGLASS_ENABLED' in api and 'CRECISOL_C06_BREAKGLASS_ENABLED' in p,"missing independent opt-in")
    need(p.index("u.VerifiedActor = true;") < p.index("return await C06ExceptionalAccess") < p.index('if (r.Operation == "platform/sessions/keepalive")'),"platform auth before dispatch")
    for m in ("u.VerifiedActor","sessionId.SequenceEqual(requestSession)","IndependentReview",
              "C06ExceptionEntitlement","C06ExceptionResourceActive","C06ExceptionLiveSession",
              "CryptographicOperations.FixedTimeEquals","PlatformTotp(u, principal, code!)",
              '!GuidBytes(Input("tenantId")!).SequenceEqual(targetTenant)',
              '!GuidBytes(Input("branchId")!).SequenceEqual(targetBranch)',
              '!GuidBytes(Input("resourceId")!).SequenceEqual(targetResource)',
              "vs.revoked_at_utc IS NULL","rs.expires_at_utc>UTC_TIMESTAMP(6)",
              "LIMIT 2 FOR SHARE","C06ExceptionalAccessPolicy.AllowsRead(evidence)",
              "INSERT INTO c06_exception_audit","req.requester_id=@actor AND req.session_id=@session",
              "AND NOT EXISTS(SELECT 1 FROM c06_exception_close", "app.expires_at_utc>UTC_TIMESTAMP(6)"):
        need(m in infra,"missing "+m)
    need(infra.count("SELECT resource.display_reference")==1 and
         infra.index("C06ExceptionalAccessPolicy.AllowsRead(evidence)")<infra.index("SELECT resource.display_reference"),
         "private projection precedes gate")
    need("CacheControl = \"no-store, private\"" in api,"private response cache")
    need("e.Actor == e.Requester" in policy and "e.Session == e.RequestSession" in policy and
         "e.Now < e.ExpiresAt" in policy and "e.FreshFactorVerified" in policy,"domain fail-closed")
    need("FOREIGN KEY(tenant_id,branch_id,resource_id)" in schema and
         "req.requester_id<>NEW.reviewer_id" in schema and
         "OLD.revoked_at_utc IS NOT NULL" in schema and
         "expires_at_utc<=requested_at_utc+INTERVAL 10 MINUTE" in schema,"DB integrity")
    need("INSERT INTO c06_exception_entitlement" not in schema and
         "GRANT SELECT ON crecisol_c05.c06_exception_entitlement" in role and
         "GRANT INSERT ON crecisol_c05.c06_exception_audit" in role and
         not any(x in role for x in ("GRANT UPDATE","GRANT DELETE","GRANT ALL","GRANT SELECT,INSERT,UPDATE","GRANT SELECT,INSERT ON crecisol_c05.c06_exception_entitlement")),"runtime grant broadened")
    need("trg_c06_exception_audit_no_update" in schema and "trg_c06_exception_audit_no_delete" in schema and "trg_c06_exception_audit_guard" in schema,"audit integrity/immutability missing")
def audited_product_end():
    # Preserve historical no-frontend/crypto edits while independently
    # checking the exact authorized successor; live guards still use HEAD.
    changed=subprocess.check_output(["git","diff","--name-only",BASE,"HEAD"],cwd=ROOT,text=True).splitlines()
    if "docs/c06/C06_11_DELEGACION_EFECTIVA_DISENO_Y_POLITICA.md" in changed:
        from c06_11_delegated_role_scope import historical_product_end
        return historical_product_end()
    return "HEAD"

def architecture():
    scope()
    policy=read("backend/src/Crecisol.Domain/Authorization/C06ExceptionalAccessPolicy.cs")
    need(not any(s in policy for s in ("MySqlConnector","HttpContext","Microsoft.AspNetCore","Crecisol.Infrastructure")),"domain crosses layers")
    need("NO CERTIFICADA" in read("docs/c06/C06_06_ESPECIFICACION.md"),"premature certification")
    need("contents: read" in read(".github/workflows/c06-06-exceptional-access.yml"),"workflow write permission")
    for p in ("frontend/","backend/src/Crecisol.CredentialSecurity/"):
        diff=subprocess.check_output(["git","diff","--name-only",BASE,audited_product_end(),"--",p],cwd=ROOT,text=True).strip()
        need(not diff,"unapproved frontend or crypto change")
    need("DROP TABLE IF EXISTS c06_exception_audit;" in read("database/migrations/021_c06_exceptional_access.down.sql") and
         "DROP TABLE IF EXISTS c05_" not in read("database/migrations/021_c06_exceptional_access.down.sql"),"unsafe rollback")
def persistence():
    schema=read("database/migrations/021_c06_exceptional_access.sql")
    test=read("database/tests/c06_06_exceptional_mysql.sh")
    need(schema.count("CREATE TRIGGER ")==schema.count("END$$") and "\nEND$\n" not in schema,"trigger delimiter mismatch")
    need("CHECK(allowed IN (0,1))" in schema and "CHECK(allowed=0 OR exception_id IS NOT NULL)" in schema,"invalid ledger result")
    for marker in ("s.session_id=NEW.session_id AND s.operator_id=NEW.operator_id",
        "app.reviewer_id=NEW.operator_id AND app.review_session_id=NEW.session_id",
        "req.requester_id=NEW.operator_id AND req.session_id=NEW.session_id",
        "cls.exception_id IS NULL","cls.closed_by=NEW.operator_id",
        "NEW.expires_at_utc<=req.expires_at_utc"):
        need(marker in schema,"ledger/scope guard missing: "+marker)
    need("INTERVAL 1 MICROSECOND" in test and "ERROR 1142" in test and "PASS C06-06 D MYSQL" in test,
         "independent executable SQL/privilege audit missing")
def catalog():
    from c06_01_permissions_catalog_audit import verify_catalog
    data=json.loads(read("docs/c06/C06_01_CATALOGO_ACCIONES.json"))
    verify_catalog(data,read("backend/src/Crecisol.Api/Modules/Identity/OperationalIdentityModule.cs"),read("backend/src/Crecisol.Api/Program.cs"))
    overlay=json.loads(read("docs/c06/C06_06_CATALOGO_ACTIVACION.json"))
    need(len(overlay["actions"])==4 and len(overlay["activated_normative"])==3,"activation scope")
    need(all(x["grantable"] is False for x in data["normative_actions"]),"historic generic grant altered")
if __name__=="__main__":
    need(len(sys.argv)==2 and sys.argv[1] in ("A","B","C","D"),"stage")
    if sys.argv[1]=="A": scope(); catalog()
    elif sys.argv[1]=="B": guard(); catalog()
    elif sys.argv[1]=="C": architecture(); guard()
    else: persistence(); guard()
    print("PASS C06-06 "+sys.argv[1]+": source review only; real runtime evidence separately required")
