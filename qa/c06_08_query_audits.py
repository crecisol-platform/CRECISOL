#!/usr/bin/env python3
"""C06-08 five-audit source controls; CI runtime results are separate."""
import json
import os
import subprocess
import sys
from pathlib import Path
from c06_08_query_scope import require_exact, PATHS
ROOT=Path(__file__).resolve().parents[1]
BASE="ff5de0670d5a09e31b32c3b6aff7e78c5449fee8"
def need(ok,msg):
    if not ok: raise AssertionError("C06-08 NO-GO "+msg)
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
    for bad in ("docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx","frontend/unreviewed.ts","database/operations/extra-grant.sql"):
        try: require_exact(changed+[bad])
        except AssertionError: pass
        else: raise AssertionError("extra path accepted "+bad)
    try: require_exact(changed[1:])
    except AssertionError: pass
    else: raise AssertionError("missing path accepted")
def audited_product_end():
    # Preserve the prior implementation's no-product-change invariant.
    # Exact successor scope is independently checked; runtime controls use HEAD.
    changed=git("diff","--name-only",BASE,"HEAD").splitlines()
    if "docs/c06/C06_11_DELEGACION_EFECTIVA_DISENO_Y_POLITICA.md" in changed:
        from c06_11_delegated_role_scope import historical_product_end
        return historical_product_end()
    return "HEAD"

def inventory():
    from c06_01_permissions_catalog_audit import verify_catalog
    verify_catalog(json.loads(read("docs/c06/C06_01_CATALOGO_ACCIONES.json")),read("backend/src/Crecisol.Api/Modules/Identity/OperationalIdentityModule.cs"),read("backend/src/Crecisol.Api/Program.cs"))
    need("NO CERTIFICADA" in read("docs/c06/C06_08_ESPECIFICACION.md"),"premature certificate")
    need(not git("diff","--name-only",BASE,audited_product_end(),"--","docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx","docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.pdf","frontend/","backend/src/Crecisol.CredentialSecurity/"),"unapproved layer/source change")
def boundary():
    a=read("backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.Authorized.cs");p=read("backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.Platform.cs");q=read("backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.PendingApprovals.cs");c=read("backend/src/Crecisol.Infrastructure/Identity/C06PendingCursor.cs")
    need(a.index("ResolveC06TenantContextFromLockedRows(")<a.index("return await PendingMemberships("),"page before authorization")
    for m in ("s.tenant_id=@t AND s.account_id=@a AND s.membership_id=@mid","ORDER BY s.last_seen_at_utc DESC,s.session_id DESC","LIMIT 4097","if (rows.Count > 4096) return new(false)","ORDER BY created_at_utc DESC,audit_id DESC LIMIT 50","Array.Empty<OperationalSessionSummary>()"):
        need(m in a,"tenant inventory "+m)
    for m in ("operator_id=@a AND security_revision=@v","ORDER BY last_seen_at_utc DESC,session_id DESC LIMIT 4097","if (rows.Count > 4096) return new(false)","GROUP BY signal_kind ORDER BY signal_kind LIMIT 4"):
        need(m in p,"platform inventory "+m)
    for m in ("LIMIT 2 FOR SHARE","WHERE m.tenant_id=@t AND m.membership_state=0","m.created_at_utc<=@now","ORDER BY m.created_at_utc ASC,m.membership_id ASC","LIMIT 13","C06PendingCursor.TryDecode(tokenKey, scope","grant_revision","approval_revision","session_revision","account_revision","host_revision","catalog_revision","C06PendingCursor.Encode(tokenKey, scope"):
        need(m in q,"pending query "+m)
    need(q.index("if (authority is null)")<q.index("C06PendingCursor.TryDecode")<q.index("SELECT m.membership_id"),"cursor/authority before data")
    for m in ("LifetimeSeconds = 600","HMACSHA256.HashData","CryptographicOperations.FixedTimeEquals","expiry <= now.ToUnixTimeSeconds()","parsed > now.UtcDateTime","parts.Length != 5"):
        need(m in c,"cursor "+m)
    need("operation, fields," in read("backend/src/Crecisol.Api/Modules/Identity/OperationalIdentityModule.cs") and 'operation == "browser/restore" ? "sessions"' not in read("backend/src/Crecisol.Api/Modules/Identity/OperationalIdentityModule.cs"),"restore still loads inventory")
    for name in ("Assisted","OperatorRecovery"):
        need(read("backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow."+name+".cs").count("LIMIT 3 FOR ")==2,"unbounded two-person proof read")
    passkeys=read("backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.Passkeys.cs")
    need("LIMIT 9" in passkeys and "rows.Count > 8" in passkeys and passkeys.count("LIMIT 8")==2,"passkey materialization bound")
def persistence():
    up=read("database/migrations/023_c06_scoped_query_indexes.sql");down=read("database/migrations/023_c06_scoped_query_indexes.down.sql")
    need(up.count(" ADD INDEX ")==5 and down.count(" DROP INDEX ")==5,"index count")
    need(not any(m in up.upper()+down.upper() for m in ("GRANT ","CREATE TABLE","DROP TABLE","DELETE FROM","UPDATE ","INSERT INTO","CREATE USER")),"DDL outside index-only scope")
    need("ix_c06_08_operator_sessions (security_revision,operator_id,revoked_at_utc,last_seen_at_utc,session_id)" in up,"operator index must not replace FK support index")
    test=read("database/tests/c06_08_scoped_query_mysql.sh")
    for mark in ("SHOW GRANTS FOR","baseline_indexes","baseline_fks","DROP INDEX ix_c06_08_pending ON c05_identity_membership","ERROR 1142 (42000)","ERROR 1044 (42000)","second rollback no residual indexes","PASS C06-08 D MYSQL"):
        need(mark in test,"independent persistence audit missing "+mark)
    need("queryIndexBaseline() === queryIndexesBefore" in read("qa/c05_operational_flow_e2e.mjs"),"end-to-end metadata preservation absent")
if __name__=="__main__":
    stage=sys.argv[1] if len(sys.argv)==2 else ""
    need(stage in ("A","B","C","D"),"stage")
    if stage=="A": scope();inventory()
    elif stage=="B": boundary();inventory()
    elif stage=="C":
        scope();boundary();inventory()
        need("contents: read" in read(".github/workflows/c06-08-scoped-queries.yml"),"workflow permissions")
        need("verifyScopedQueries(" in read("qa/c05_operational_flow_e2e.mjs") and "verifyBrowserScopedPages(" in read("qa/c05_operational_flow_e2e.mjs"),"HTTP/browser integration missing")
    else: persistence()
    print("PASS C06-08 "+stage+": source review; real execution separately required")
