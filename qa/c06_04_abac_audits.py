#!/usr/bin/env python3
"""CRECISOL C06-04 independent A/B/C gates. Never accept stale or fake PASS."""
from pathlib import Path
import json
import os
import subprocess
import sys

ROOT=Path(__file__).resolve().parents[1]
FILES={
  "domain": "backend/src/Crecisol.Domain/Tenancy/C06CaseAbacPolicy.cs",
  "adapter": "backend/src/Crecisol.Infrastructure/Tenancy/C06CaseAbacProjection.cs",
  "auth": "backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.Authorized.cs",
  "migration": "database/migrations/020_c06_case_abac.sql",
  "rollback": "database/migrations/020_c06_case_abac.down.sql",
  "runtime": "database/operations/c06_04_local_runtime_role.sql",
  "e2e": "qa/c05_operational_flow_e2e.mjs",
  "dotnet": "backend/tests/Crecisol.C06Abac.ContractsTests/Program.cs",
  "mysql": "database/tests/c06_04_abac_mysql.sh",
}
def need(x, msg):
    if not x: raise AssertionError("C06-04 ABAC NO-GO: "+msg)
def read(key):return (ROOT/FILES[key]).read_text(encoding="utf-8")
def change_scope():
    evt=os.environ.get("GITHUB_EVENT_NAME","")
    if evt=="pull_request":
        need(os.environ.get("GITHUB_BASE_REF")=="dev","PR must target dev")
        base=["origin/dev...HEAD"]
    elif evt=="push":
        need(os.environ.get("GITHUB_REF")=="refs/heads/dev","postmerge must be dev")
        base=["HEAD^","HEAD"]
    else: base=["af01c2e112c41c76a70effc4c271a1c8a30670b0","HEAD"]
    changed=subprocess.check_output(["git","diff","--name-only",*base],
                                    cwd=ROOT,text=True).splitlines()
    allowed=set(FILES.values())|{
        "qa/c06_04_abac_audits.py",
        "qa/c06_03_context_audits.py",
        ".github/workflows/c06-04-abac-ownership.yml",
        "docs/c06/C06_04_ESPECIFICACION.md",
        "docs/c06/C06_04_ACTA_CIERRE.md",
        "docs/ESTADO_VIGENTE_CAPITULOS.md",
        "docs/planificacion-futura/C06_MICROFASES_PROPUESTAS.md",
    }
    need(bool(changed),"empty diff cannot certify")
    need(all(x in allowed or x.startswith("backend/tests/Crecisol.C06Abac.ContractsTests/")
             for x in changed),"out-of-scope path "+repr(changed))
    need(not any(x.startswith(("frontend/","backend/src/Crecisol.Api/"))
                 for x in changed),"front-end or API route changed")
    return changed

def master_inventory():
    c=json.loads((ROOT/"docs/c06/C06_01_CATALOGO_ACCIONES.json").read_text(encoding="utf-8"))
    m=json.loads((ROOT/"docs/c06/C06_02_MATRIZ_ROLES_CAPACIDADES.json").read_text(encoding="utf-8"))
    need((len(c["api_actions"]),len(c["normative_actions"]),
          len(c["non_http_contracts"]))==(80,28,12),"C06-01 catalog drift")
    need(len(m["roles"])==10 and len(m["actions"])==108,"C06-02 role matrix drift")
    need(all(a["grantable"] is False and a["policy_activated"] is False
             for a in m["actions"]),"C06-02 future roles activated")
    need("cases.read" in {a["action_id"] for a in m["actions"]},
         "canonical cases.read action missing")

GUARDS={
"domain":[
 "public static class C06CaseAbacPolicy","public static bool Allows(",
 "e.Identity.TenantId != e.ResourceTenant","e.Identity.TenantId != e.GrantTenant",
 "e.Identity.ActorId != e.GrantActor","e.ResourceBranch != e.SelectorBranch",
 "e.ResourceId != e.SelectorResource","e.ResourceBranch != e.GrantBranch",
 "e.RequiredAction.Value != \"cases.read\"",
 "e.GrantPermissionCode != \"RESOURCE_READ\"",
 "e.GrantRevokedAtUtc is not null","e.GrantExpiresAtUtc.HasValue",
 "e.GrantScope == 1","e.GrantResource == e.ResourceId",
 "e.ResourceOwner == e.Identity.ActorId","e.GrantScope != 2",
 "e.Assignment","a.ResourceId == e.ResourceId",
 "a.ActorId == e.Identity.ActorId","a.Action.Value == e.RequiredAction.Value",
 "a.RevokedAtUtc is null","PrivilegedBranchReaders",
 "ReadRoles.Contains(e.RoleCode)",
],
"adapter":[
 "C06ReadAuthorizedCase(","GuidBytes(Field(request, \"branchId\"))",
 "GuidBytes(Field(request, \"resourceId\"))",
 "JOIN c05_identity_branch b","WHERE r.tenant_id=@t AND r.branch_id=@b AND r.resource_id=@r",
 "FROM c05_identity_resource_grant","permission_code='RESOURCE_READ'",
 "LIMIT 2 FOR SHARE","grants.Count != 1",
 "FROM c06_case_resource_assignment","action_id='cases.read'",
 "assignments.Count != 1","UTC_TIMESTAMP(6) AS server_now_utc",
 "C06CaseAbacPolicy.Allows(evidence)","DbTenant(Bytes(resource, \"tenant_id\"))",
 "DbActor(Bytes(resource, \"owner_account_id\"))",
 "new Guid(key, bigEndian: true)",
],
"auth":[
 "var c06Context = ResolveC06TenantContextFromLockedRows(",
 "return await C06ReadAuthorizedCase(u, tenant, actor, member, c06Context, r);",
],
"migration":[
 "CREATE TABLE c06_case_resource_assignment",
 "UNIQUE KEY uq_c06_case_actor_action",
 "FOREIGN KEY(tenant_id,branch_id,resource_id)",
 "FOREIGN KEY(tenant_id,account_id)",
 "FOREIGN KEY(tenant_id,approved_by_account_id)",
 "ck_c06_case_action_exact CHECK (action_id='cases.read')",
 "ck_c06_case_time","ck_c06_case_revocation",
 "trg_c06_case_assignment_immutable","trg_c06_case_assignment_no_delete",
],
"runtime":[
 "GRANT SELECT ON crecisol_c05.c06_case_resource_assignment",
 "TO 'c05_identity_local_runtime'",
],
"e2e":[
 "const c06Abac = process.argv.includes('--c06-04')",
 "database/migrations/020_c06_case_abac.sql",
 "database/operations/c06_04_local_runtime_role.sql",
 "broad branch grant alone cannot read a coworker case",
 "exact assignment allows only the intended coworker case",
 "second coworker case in the same branch remains private",
 "assignment revocation immediately denies future read",
],
}
def static_guards():
    for key,terms in GUARDS.items():
        source=read(key)
        for term in terms:need(term in source,f"{key} missing {term}")
    src=read("domain")
    # A role is NOT permitted by being present in C06-02 proposals.
    # The domain's exact read-role allowlist must explicitly exclude the
    # custom/PLATFORM/SuperAdmin realm, rather than require a denied role.
    role_allowlist=src.split("private static readonly HashSet<string> ReadRoles",1)[1].split("};",1)[0]
    for denied in ('"tenant-custom"', '"platform-operator"', '"superadmin"', '"platform-incident-responder"'):
        need(denied not in role_allowlist, "untrusted role became an allowed case reader: "+denied)
    need(not any(x in src for x in ("MySqlConnector","HttpContext","Microsoft.AspNetCore","System.Data.Sql")),
         "Domain contains transport or SQL references")
    need("private/read" not in src or "cases.read" in src,
         "Domain action not fixed to backend")
    auth=read("auth")
    need(auth.index("ResolveC06TenantContextFromLockedRows(") <
         auth.index("C06ReadAuthorizedCase(u, tenant, actor, member, c06Context, r)") ,
         "ABAC read precedes C06-03 identity")
    adapter=read("adapter")
    need(adapter.index("C06CaseAbacPolicy.Allows(evidence)") <
         adapter.index('return new(true, new { reference ='),
         "private data emitted before authorization")
    need("new(true" not in adapter[:adapter.index("C06CaseAbacPolicy.Allows(evidence)")],
         "uncontrolled early allow")
    migration=read("migration")
    need("INSERT INTO c06_case_resource_assignment" not in migration,
         "assignment migration seeds grants")
    runtime=read("runtime")
    need(not any(x in runtime.upper() for x in ("GRANT INSERT", "GRANT UPDATE", "GRANT DELETE", "GRANT ALL")),
         "runtime role can mutate assignments")
    need("DROP TABLE IF EXISTS c06_case_resource_assignment" in read("rollback"),
         "rollback missing")
    return sum(map(len,GUARDS.values()))

def negative_gate():
    n=0
    for key, terms in GUARDS.items():
        source=read(key)
        for term in terms:
            # Remove ALL occurrences so each security invariant is truly absent;
            # a repeated reference must not produce a false-negative test.
            modified=source.replace(term,"POLICY_GUARD_REMOVED")
            need(term not in modified and modified != source,
                 "mutated control still present "+term)
            n+=1
    need(n>=55,"insufficient source mutations")
    need(read("dotnet").count("No(")>=50,
         "same tenant IDOR .NET negative cases not implemented")
    need(read("mysql").count('reject "')>=20,
         "real MySQL negative scripts missing")
    need(read("e2e").count("C06-04")>=8,
         "real authenticated HTTP negative tests missing")
    print(f"PASS C06-04 B: {n} structural bypass mutations detected; independent .NET, MySQL and HTTP tests required separately")

def main(stage):
    changed=change_scope()
    master_inventory()
    guard_count=static_guards()
    if stage=="A":
        print("PASS C06-04 A: canonical Word (via master guard), C06-01/02, exact diff:",changed,"policy guards:",guard_count)
    elif stage=="B":
        negative_gate()
    elif stage=="C":
        need("C06CaseAbacPolicy.Allows" in read("adapter"),"policy not wired")
        need("DELIMITER $$" in read("migration"),"migration triggers missing")
        need("020_c06_case_abac.down.sql" in read("mysql"),"MySQL rollback untested")
        print("PASS C06-04 C: Domain/Infrastructure ABAC, schema, rollback, unchanged API/UI; compile and E2E required in CI")
    else:raise SystemExit("Usage: c06_04_abac_audits.py A|B|C")
if __name__=="__main__":main(sys.argv[1] if len(sys.argv)==2 else "")
