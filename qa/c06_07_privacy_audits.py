#!/usr/bin/env python3
import json
import os
import re
import subprocess
import sys
from pathlib import Path
from c06_07_privacy_scope import require_exact
ROOT=Path(__file__).resolve().parents[1]
BASE="e60c8a7642589f511b8beaa977f773524e963ece"
def need(ok,msg):
    if not ok: raise AssertionError("C06-07 NO-GO "+msg)
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
    require_exact(git("diff","--name-only",*args).splitlines())
def catalog():
    from c06_01_permissions_catalog_audit import verify_catalog
    module=read("backend/src/Crecisol.Api/Modules/Identity/OperationalIdentityModule.cs")
    verify_catalog(json.loads(read("docs/c06/C06_01_CATALOGO_ACCIONES.json")),module,read("backend/src/Crecisol.Api/Program.cs"))
    routes=set(re.findall(r'\["([^"]+)"\]\s*=\s*\[',module))
    rules=set(re.findall(r'\["([^"]+)"\]\s*=\s*\w+,',read("backend/src/Crecisol.Api/Contracts/C06IdentityResponseProjection.cs")))
    doc=json.loads(read("docs/c06/C06_07_CONTRATOS_RESPUESTA.json"))
    need(len(routes)==79 and routes==rules==set(doc["operations"]),"response inventory drift")
    need(doc["authority"]=="HTTP_RESPONSE_CONTRACT_NOT_AUTHORIZATION","privacy grants authority")
    need("NO CERTIFICADA" in read("docs/c06/C06_07_ESPECIFICACION.md"),"premature certificate")
def ingress_privacy():
    program=read("backend/src/Crecisol.Api/Program.cs")
    ingress=program.index('context.Request.Path.StartsWithSegments("/api/v1/identity", StringComparison.OrdinalIgnoreCase)')
    platform=program.index('context.Request.Path.StartsWithSegments("/api/v1/platform-identity", StringComparison.OrdinalIgnoreCase)')
    callback=program.index("context.Response.OnStarting(",platform)
    cache=program.index('context.Response.Headers.CacheControl = "no-store, private"',callback)
    exception=program.index("app.UseExceptionHandler(")
    host=program.index("if (!trustedHosts.Contains(")
    need(ingress<platform<callback<cache<exception<host,"identity ingress cache must survive early rejection and downstream error resets")
    need('context.Response.Headers.CacheControl = "no-store";' in program,"public cache baseline removed")
    need("C06-07 ingress " in read("qa/c05_operational_flow_e2e.mjs"),"real ingress regression matrix missing")
def boundary():
    ingress_privacy()
    module=read("backend/src/Crecisol.Api/Modules/Identity/OperationalIdentityModule.cs")
    code=read("backend/src/Crecisol.Api/Contracts/C06IdentityResponseProjection.cs")
    need(module.index("var result = await flow.ExecuteAsync")<module.index("C06IdentityResponseProjection.TryApply(operation, result, out var safe)")<module.index("browserTransport.Receipt(context, operation, safe, browserSession)")<module.index("Results.Ok(safe.Value)"),"raw response or bypass")
    need("Results.Ok(result.Value" not in module and "Results.Accepted(value: result.Value" not in module,"unprojected result")
    need('CacheControl = "no-store, private"' in module and "NeutralAssisted(result)" in module,"cache or neutral response")
    for mark in ("if (!Rules.TryGetValue(operation","if (!result.Allowed) return true","members.Count != fields.Length",
                 "type.IsDefined(typeof(CompilerGeneratedAttribute)","!properties.TryAdd(p.Name, p.Value)",
                 "if (result.Count >= maximum)","if (receipt.RecoveryCodes is not null && !mayIncludeBackups)",
                 'F("mailboxHint", MaskedMailbox)','Protocol(F("challenge", Base64Url)'):
        need(mark in code,"missing "+mark)
    need("JsonSerializer.Serialize" not in code and "GetValue(value)" in code and "output.Add(field.Name, field.Read" in code,
         "projection must build allowed fields without serializing raw result")
    e2e=read("qa/c05_operational_flow_e2e.mjs")
    relay=read("qa/c06_07_privacy_relay.mjs")
    oracle=read("qa/c06_07_privacy_response_checks.mjs")
    need("inspectPrivacyResponse(operation,response.statusCode" in e2e and
         "startPrivacyRelay(apiPort, inspectPrivacyResponse)" in e2e and
         e2e.count("target: `http://127.0.0.1:${proxyPort}`")==2 and
         "await privacyRelay.finish(); reportPrivacyResponses();" in e2e,"wire oracle not connected")
    for marker in ("hostname:'127.0.0.1'", "server.listen(0,'127.0.0.1'", "incoming.pipe(upstream)",
                   "response.complete", "response.rawHeaders", "outgoing.end(body)", "setTimeout(fail,deadlineMs)",
                   "size>maxBytes", "fatal:true", "inspect(op,response.statusCode,raw"):
        need(marker in relay,"privacy relay invariant missing "+marker)
    need("response.finished()" not in oracle and "response.body()" not in oracle and
         "relayBrowserChecked>=10" in oracle and "relayEnrollments>=2" in oracle,"capture/coverage regression")
    need("node --test qa/c06_07_privacy_oracle_tests.mjs qa/c06_07_privacy_relay_tests.mjs" in
         read(".github/workflows/c06-07-privacy-minimization.yml"),"relay tests not connected")
def architecture():
    scope(); boundary(); catalog()
    for p in ("frontend/","backend/src/Crecisol.Domain/","backend/src/Crecisol.Infrastructure/","backend/src/Crecisol.CredentialSecurity/"):
        need(not git("diff","--name-only",BASE,"HEAD","--",p),"unapproved existing layer change "+p)
    need("contents: read" in read(".github/workflows/c06-07-privacy-minimization.yml"),"workflow write authority")
def persistence():
    sql=read("database/migrations/022_c06_privacy_disposition.sql"); role=read("database/operations/c06_07_local_privacy_worker.sql")
    for mark in ("SQL SECURITY DEFINER","p_limit IS NULL OR p_limit<1 OR p_limit>100","SET v_now=UTC_TIMESTAMP(6)",
                 "START TRANSACTION","ROLLBACK; RESIGNAL","expires_at_utc<=v_now","consumed_at_utc<=v_now","delivered_at_utc<=v_now","OCTET_LENGTH(protected_payload)>0","LIMIT p_limit","SELECT 'ACK'"):
        need(mark in sql,"missing persistence "+mark)
    need(sql.count("SET protected_payload=X''")==2 and sql.count("LIMIT p_limit")==2 and "DELETE FROM" not in sql and "CREATE EVENT" not in sql,"disposition scope")
    need(role.count("GRANT EXECUTE ON PROCEDURE")==1 and "GRANT SELECT" not in role and "GRANT UPDATE" not in role and "c05_identity_local_runtime" not in role,"worker privilege expansion")
    need(read("database/migrations/022_c06_privacy_disposition.down.sql").count("DROP PROCEDURE")==1 and "DROP TABLE" not in read("database/migrations/022_c06_privacy_disposition.down.sql"),"rollback scope")
if __name__=="__main__":
    stage=sys.argv[1] if len(sys.argv)==2 else ""
    need(stage in ("A","B","C","D"),"stage")
    if stage=="A": scope(); catalog()
    elif stage=="B": boundary(); catalog()
    elif stage=="C": architecture()
    else: persistence()
    print("PASS C06-07 "+stage+": source audit only; execution separately required")
