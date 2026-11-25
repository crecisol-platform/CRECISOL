#!/usr/bin/env python3
"""C06-09 independent five-audit source gate; runtime evidence is separate."""
import json
import os
import subprocess
import sys
from pathlib import Path
from c06_09_delivery_scope import require_exact
ROOT=Path(__file__).resolve().parents[1]
BASE="da67bfe38940edd25842b2f963090e3d1406ce45"
def need(ok,msg):
    if not ok: raise AssertionError("C06-09 NO-GO "+msg)
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
    for bad in ("frontend/unreviewed.ts","database/operations/extra-grant.sql","docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx"):
        try: require_exact(changed+[bad])
        except AssertionError: pass
        else: raise AssertionError("extra path accepted "+bad)
    try: require_exact(changed[1:])
    except AssertionError: pass
    else: raise AssertionError("missing path accepted")
def inventory():
    from c06_01_permissions_catalog_audit import verify_catalog
    catalog=json.loads(read("docs/c06/C06_01_CATALOGO_ACCIONES.json"))
    module=read("backend/src/Crecisol.Api/Modules/Identity/OperationalIdentityModule.cs")
    program=read("backend/src/Crecisol.Api/Program.cs")
    verify_catalog(catalog,module,program)
    for action in ("media.private.read","media.private.export","finance.report.export"):
        rows=[a for a in catalog["normative_actions"] if a["action_id"]==action]
        need(len(rows)==1 and rows[0]["http_registered"] is False and rows[0]["grantable"] is False,"future action exposed "+action)
    need(not git("diff","--name-only",BASE,"HEAD","--","frontend/","database/migrations/","database/operations/","backend/src/Crecisol.CredentialSecurity/","docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx","docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.pdf","docs/c06/C06_01_CATALOGO_ACCIONES.json","docs/c06/C06_02_MATRIZ_ROLES_CAPACIDADES.json"),"unapproved source or persistence change")
    need("NO CERTIFICADA" in read("docs/c06/C06_09_ESPECIFICACION.md"),"premature certificate")
def boundary():
    content=read("backend/src/Crecisol.Application/Identity/C06ProtectedContent.cs")
    projection=read("backend/src/Crecisol.Api/Contracts/C06IdentityResponseProjection.cs")
    profile=read("backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.Profile.cs")
    abac=read("backend/src/Crecisol.Infrastructure/Tenancy/C06CaseAbacProjection.cs")
    exception=read("backend/src/Crecisol.Infrastructure/Tenancy/C06ExceptionalAccess.cs")
    program=read("backend/src/Crecisol.Api/Program.cs")
    logger=read("backend/src/Crecisol.Api/Configuration/C06SafeRuntimeLoggerProvider.cs")
    for marker in ('MaxPng = 24576','ScanCount = 64 * (RowSize + 1)','"IHDR"u8','"IDAT"u8','"IEND"u8','position != png.Length','checksum != Checksum','zip.ReadExactly(scan)','zip.ReadByte() != -1','scan[offset] != 0','return EncodePhoto(pixels)','CryptographicOperations.ZeroMemory','char.IsAsciiLetterOrDigit','value.All'):
        need(marker in content,"closed media contract missing "+marker)
    for marker in ('F("png", ProtectedPhoto)','NormalizePhotoBase64','F("reference", DisplayReference)','F("action", AuditAction)','F("outcome", AuditOutcome)','Rules.ContainsKey(action)','"security/event"','n is 0 or 1'):
        need(marker in projection,"closed DTO missing "+marker)
    need("CanonicalProfilePng.Normalize" in profile and 'tenant_id=@t AND account_id=@a' in profile and 'operator_id=@a' in profile,"scoped media use case")
    for source in (abac,exception):
        need("C06ProtectedContent.IsDisplayReference(reference)" in source,"URL can escape authorized reference")
    need(exception.index("C06ExceptionalAccessPolicy.AllowsRead(evidence)")<exception.index("C06ProtectedContent.IsDisplayReference(reference)"),"exception delivery before gate")
    need("builder.Logging.ClearProviders();" in program and "new C06SafeRuntimeLoggerProvider(Console.Out)" in program and '["Cross-Origin-Resource-Policy"] = "same-origin"' in program and 'new EventId(context.Response.StatusCode)' in program,"safe diagnostics/headers missing")
    for prefix in ("identity","platform-identity","private","media","downloads","exports","logs"):
        need('StartsWithSegments("/api/v1/'+prefix+'", StringComparison.OrdinalIgnoreCase)' in program,"private ingress missing "+prefix)
    need('owner.Write(kind, logLevel, eventId.Id)' in logger and 'CRECISOL_RUNTIME source={kind} level={(int)level} event={eventCode}' in logger,"closed diagnostic record missing")
    for forbidden in ("formatter(state","exception.ToString","eventId.Name","output.WriteLine(categoryName","output.WriteLine(state"):
        need(forbidden not in logger,"unsafe diagnostic formatting "+forbidden)
    from c06_07_privacy_audits import boundary as previous_privacy
    from c06_08_query_audits import boundary as previous_queries
    previous_privacy();previous_queries()
def persistence():
    script=read("database/tests/c06_09_delivery_mysql.sh")
    for marker in ("SHOW GRANTS FOR","--protocol=SOCKET","app_sql","ERROR $3","tenant audit update","tenant audit deletion","raw platform audit read","platform audit update","platform audit deletion","profile without matching tenant membership","inconsistent photo state","1452","3819","PASS C06-09 D MYSQL"):
        need(marker in script,"MySQL boundary case missing "+marker)
    need(not git("diff","--name-only",BASE,"HEAD","--","database/migrations/","database/operations/"),"persistence privilege drift")
if __name__=="__main__":
    stage=sys.argv[1] if len(sys.argv)==2 else ""
    need(stage in ("A","B","C","D"),"stage")
    if stage=="A": scope();inventory()
    elif stage=="B": inventory();boundary()
    elif stage=="C":
        scope();inventory();boundary()
        e=read("qa/c05_operational_flow_e2e.mjs")
        need("verifyDeliveryBoundary(" in e and "verifyPrivateReference(" in e and "redacted HTTP diagnostics remain operational" in e,"real HTTP tests missing")
        need("c06Delivery" in read("qa/c05_profile_browser_checks.mjs") and "c06Delivery" in read("qa/c05_operator_recovery_checks.mjs"),"real scoped media browser regressions missing")
        need("contents: read" in read(".github/workflows/c06-09-protected-delivery.yml"),"workflow permission drift")
    else: persistence()
    print("PASS C06-09 "+stage+": source controls; real runtime evidence separately required")
