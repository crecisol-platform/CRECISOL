#!/usr/bin/env python3
"""C06-11 first delegation slice: exact source scope, never a certificate."""
import os
import subprocess
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
BASE="076118ab5370e1fcbc7bfe0f1caefe07a664365b"
PATHS=frozenset(["backend/src/Crecisol.Domain/Authorization/C06ScopedRoleDelegationPolicy.cs","backend/tests/Crecisol.C06Delegation.ContractsTests/Program.cs","backend/tests/Crecisol.C06Delegation.ContractsTests/Crecisol.C06Delegation.ContractsTests.csproj","docs/c06/C06_11_DELEGACION_EFECTIVA_DISENO_Y_POLITICA.md",".github/workflows/c06-11-delegated-role-policy.yml","qa/c06_11_delegated_role_scope.py","qa/c06_02_role_model_audit.py"])
def git(*args):
    return subprocess.check_output(["git",*args],cwd=ROOT,text=True).strip()
def need(ok,why):
    if not ok:
        raise AssertionError("C06-11 DELEGATION NO-GO: "+why)
def require_exact(paths):
    actual=frozenset(paths)
    need(len(paths)==len(actual) and actual==PATHS,
         "scope drift: "+repr(sorted(actual ^ PATHS)))
    return paths

def main():
    need(subprocess.run(["git","merge-base","--is-ancestor",BASE,"HEAD"],cwd=ROOT).returncode==0,"missing verified base")
    need(os.environ.get("GITHUB_EVENT_NAME")=="pull_request" and os.environ.get("GITHUB_BASE_REF")=="dev","only draft candidate PR to dev")
    paths=git("diff","--name-only",BASE,"HEAD").splitlines()
    require_exact(paths)
    require_exact(git("diff","--name-only","origin/dev...HEAD").splitlines())
    need(set(git("log","--format=","--name-only","--no-renames",BASE+"..HEAD").splitlines())-{""} <= PATHS,"historic scope drift")
    for path in ("backend/src/Crecisol.Domain/Authorization/C06PreparedRoleSnapshot.cs",
                 "backend/src/Crecisol.Application/Authorization/InspectPreparedTenantRoles.cs",
                 "database/migrations/019_c06_prepared_roles.sql",
                 "backend/src/Crecisol.Domain/Tenancy/C06CaseAbacPolicy.cs",
                 "docs/c06/C06_01_CATALOGO_ACCIONES.json",
                 "docs/c06/C06_02_MATRIZ_ROLES_CAPACIDADES.json",
                 "docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx"):
        need(git("rev-parse",BASE+":"+path)==git("rev-parse","HEAD:"+path),"historical source changed: "+path)
    # The successor router itself must be the only permitted change to the
    # legacy auditor. Compare against the ratified version, not markers.
    old=git("show",BASE+":qa/c06_02_role_model_audit.py")
    current=(ROOT/"qa/c06_02_role_model_audit.py").read_text(encoding="utf-8").strip()
    marker='    # Explicit sequential successor: preserve every other historical audit.'
    insertion='    if "docs/c06/C06_11_DELEGACION_EFECTIVA_DISENO_Y_POLITICA.md" in changed:\n        from c06_11_delegated_role_scope import require_exact\n        require_exact(changed)\n        return changed\n'
    need(old.count(marker)==1 and current==old.replace(marker,marker+"\n"+insertion.rstrip("\n")),
         "historical auditor modified beyond exact successor routing")
    for attack in (paths[:-1],paths+[paths[0]],paths+["frontend/src/unreviewed.ts"],
                   paths+["backend/src/Crecisol.Api/Unreviewed.cs"],
                   paths+["database/migrations/unreviewed.sql"],
                   paths+["docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx"]):
        try:
            require_exact(attack)
        except AssertionError:
            continue
        raise AssertionError("scope mutation passed")
    print("PASS C06-11 DELEGATION seven isolated paths, six rejected mutations; exact C06-02 successor routing, prior contracts and Master unchanged")

if __name__=="__main__":
    main()
