#!/usr/bin/env python3
"""C06-11 first delegation slice: exact source scope, never a certificate."""
import os
import subprocess
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
BASE="076118ab5370e1fcbc7bfe0f1caefe07a664365b"
PATHS=frozenset(["backend/src/Crecisol.Domain/Authorization/C06ScopedRoleDelegationPolicy.cs","backend/tests/Crecisol.C06Delegation.ContractsTests/Program.cs","backend/tests/Crecisol.C06Delegation.ContractsTests/Crecisol.C06Delegation.ContractsTests.csproj","docs/c06/C06_11_DELEGACION_EFECTIVA_DISENO_Y_POLITICA.md",".github/workflows/c06-11-delegated-role-policy.yml","qa/c06_11_delegated_role_scope.py"])
def git(*args):
    return subprocess.check_output(["git",*args],cwd=ROOT,text=True).strip()
def need(ok,why):
    if not ok:
        raise AssertionError("C06-11 DELEGATION NO-GO: "+why)
need(subprocess.run(["git","merge-base","--is-ancestor",BASE,"HEAD"],cwd=ROOT).returncode==0,"missing verified base")
need(os.environ.get("GITHUB_EVENT_NAME")=="pull_request" and os.environ.get("GITHUB_BASE_REF")=="dev","only draft candidate PR to dev")
need(set(git("diff","--name-only",BASE,"HEAD").splitlines())==PATHS,"scope drift")
need(set(git("log","--format=","--name-only","--no-renames",BASE+"..HEAD").splitlines())-{""} <= PATHS,"historic scope drift")
for path in ("backend/src/Crecisol.Domain/Authorization/C06PreparedRoleSnapshot.cs",
             "backend/src/Crecisol.Application/Authorization/InspectPreparedTenantRoles.cs",
             "database/migrations/019_c06_prepared_roles.sql",
             "backend/src/Crecisol.Domain/Tenancy/C06CaseAbacPolicy.cs",
             "docs/c06/C06_01_CATALOGO_ACCIONES.json",
             "docs/c06/C06_02_MATRIZ_ROLES_CAPACIDADES.json",
             "docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx"):
    need(git("rev-parse",BASE+":"+path)==git("rev-parse","HEAD:"+path),"historical source changed: "+path)
print("PASS C06-11 DELEGATION six isolated files; prior contracts and Master unchanged")
