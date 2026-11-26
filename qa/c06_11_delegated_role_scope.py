#!/usr/bin/env python3
"""C06-11 delegation policy and persistence slice: exact source scope, never a certificate."""
import os
import subprocess
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
BASE="076118ab5370e1fcbc7bfe0f1caefe07a664365b"
PATHS=frozenset(["backend/src/Crecisol.Domain/Authorization/C06ScopedRoleDelegationPolicy.cs","backend/tests/Crecisol.C06Delegation.ContractsTests/Program.cs","backend/tests/Crecisol.C06Delegation.ContractsTests/Crecisol.C06Delegation.ContractsTests.csproj","docs/c06/C06_11_DELEGACION_EFECTIVA_DISENO_Y_POLITICA.md",".github/workflows/c06-11-delegated-role-policy.yml","qa/c06_11_delegated_role_scope.py","qa/c06_02_role_model_audit.py","database/migrations/024_c06_delegated_role_ledger.sql","database/migrations/024_c06_delegated_role_ledger.down.sql","database/tests/c06_11_delegated_role_mysql.sh","qa/c06_07_privacy_scope.py","qa/c06_08_query_scope.py","qa/c06_09_delivery_scope.py","qa/c06_10_offensive_scope.py","qa/c06_07_privacy_audits.py","qa/c06_09_delivery_audits.py","qa/c06_10_offensive_audits.py"])
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

# Precisely reviewed historical source edits only.
HISTORICAL_EDITS={
    "qa/c06_07_privacy_scope.py": [
        [
            "def require_exact(changed):\n",
            "def require_exact(changed):\n    if \"docs/c06/C06_11_DELEGACION_EFECTIVA_DISENO_Y_POLITICA.md\" in changed:\n        from c06_11_delegated_role_scope import require_exact as require_delegation\n        return require_delegation(changed)\n"
        ]
    ],
    "qa/c06_08_query_scope.py": [
        [
            "def require_exact(changed):\n",
            "def require_exact(changed):\n    if \"docs/c06/C06_11_DELEGACION_EFECTIVA_DISENO_Y_POLITICA.md\" in changed:\n        from c06_11_delegated_role_scope import require_exact as require_delegation\n        return require_delegation(changed)\n"
        ]
    ],
    "qa/c06_09_delivery_scope.py": [
        [
            "def require_exact(changed):\n",
            "def require_exact(changed):\n    if \"docs/c06/C06_11_DELEGACION_EFECTIVA_DISENO_Y_POLITICA.md\" in changed:\n        from c06_11_delegated_role_scope import require_exact as require_delegation\n        return require_delegation(changed)\n"
        ]
    ],
    "qa/c06_10_offensive_scope.py": [
        [
            "def require_exact(changed):\n",
            "def require_exact(changed):\n    if \"docs/c06/C06_11_DELEGACION_EFECTIVA_DISENO_Y_POLITICA.md\" in changed:\n        from c06_11_delegated_role_scope import require_exact as require_delegation\n        return require_delegation(changed)\n"
        ]
    ],
    "qa/c06_07_privacy_audits.py": [
        [
            "    end= \"3789224e49f3183563a7f8491a3b3101f161a6a1\" if any(p in changed for p in (\"docs/c06/C06_08_ESPECIFICACION.md\",\"docs/c06/C06_09_ESPECIFICACION.md\",\"docs/c06/C06_10_ESPECIFICACION.md\")) else \"HEAD\"",
            "    end= \"3789224e49f3183563a7f8491a3b3101f161a6a1\" if any(p in changed for p in (\"docs/c06/C06_08_ESPECIFICACION.md\",\"docs/c06/C06_09_ESPECIFICACION.md\",\"docs/c06/C06_10_ESPECIFICACION.md\",\"docs/c06/C06_11_DELEGACION_EFECTIVA_DISENO_Y_POLITICA.md\")) else \"HEAD\""
        ]
    ],
    "qa/c06_09_delivery_audits.py": [
        [
            "def inventory():\n",
            "def audited_product_end():\n    # Preserve the prior implementation's no-product-change invariant.\n    # Exact successor scope is independently checked; runtime controls use HEAD.\n    changed=git(\"diff\",\"--name-only\",BASE,\"HEAD\").splitlines()\n    if \"docs/c06/C06_11_DELEGACION_EFECTIVA_DISENO_Y_POLITICA.md\" in changed:\n        from c06_11_delegated_role_scope import historical_product_end\n        return historical_product_end()\n    return \"HEAD\"\n\ndef inventory():\n"
        ],
        [
            "need(not git(\"diff\",\"--name-only\",BASE,\"HEAD\",\"--\",\"frontend/\",\"database/migrations/\",\"database/operations/\",\"backend/src/Crecisol.CredentialSecurity/\",\"docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx\",\"docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.pdf\",\"docs/c06/C06_01_CATALOGO_ACCIONES.json\",\"docs/c06/C06_02_MATRIZ_ROLES_CAPACIDADES.json\"),\"unapproved source or persistence change\")",
            "need(not git(\"diff\",\"--name-only\",BASE,audited_product_end(),\"--\",\"frontend/\",\"database/migrations/\",\"database/operations/\",\"backend/src/Crecisol.CredentialSecurity/\",\"docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx\",\"docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.pdf\",\"docs/c06/C06_01_CATALOGO_ACCIONES.json\",\"docs/c06/C06_02_MATRIZ_ROLES_CAPACIDADES.json\"),\"unapproved source or persistence change\")"
        ],
        [
            "need(not git(\"diff\",\"--name-only\",BASE,\"HEAD\",\"--\",\"database/migrations/\",\"database/operations/\"),\"persistence privilege drift\")",
            "need(not git(\"diff\",\"--name-only\",BASE,audited_product_end(),\"--\",\"database/migrations/\",\"database/operations/\"),\"persistence privilege drift\")"
        ]
    ],
    "qa/c06_10_offensive_audits.py": [
        [
            "def scope():\n",
            "def audited_product_end():\n    # Preserve the prior implementation's no-product-change invariant.\n    # Exact successor scope is independently checked; runtime controls use HEAD.\n    changed=git(\"diff\",\"--name-only\",BASE,\"HEAD\").splitlines()\n    if \"docs/c06/C06_11_DELEGACION_EFECTIVA_DISENO_Y_POLITICA.md\" in changed:\n        from c06_11_delegated_role_scope import historical_product_end\n        return historical_product_end()\n    return \"HEAD\"\n\ndef scope():\n"
        ],
        [
            "need(not git(\"diff\",\"--name-only\",BASE,\"HEAD\",\"--\",path),\"unapproved product/authority change \"+path)",
            "need(not git(\"diff\",\"--name-only\",BASE,audited_product_end(),\"--\",path),\"unapproved product/authority change \"+path)"
        ]
    ]
}
HISTORICAL_MODS=frozenset(HISTORICAL_EDITS) | {"qa/c06_02_role_model_audit.py"}

def historical_product_end():
    need(subprocess.run(["git","merge-base","--is-ancestor",BASE,"HEAD"],cwd=ROOT).returncode==0,
         "missing ratified historical baseline")
    paths=git("diff","--name-only",BASE,"HEAD").splitlines()
    require_exact(paths)
    status=git("diff","--name-status","--no-renames",BASE,"HEAD").splitlines()
    expected={"M\t"+p if p in HISTORICAL_MODS else "A\t"+p for p in PATHS}
    need(len(status)==len(expected) and set(status)==expected,"unapproved path status")
    for path,pairs in HISTORICAL_EDITS.items():
        expected_text=git("show",BASE+":"+path)
        for old,new in pairs:
            need(expected_text.count(old)==1,"historical marker drift "+path)
            expected_text=expected_text.replace(old,new)
        need((ROOT/path).read_text(encoding="utf-8").strip()==expected_text,
             "historical audit modified beyond exact successor change: "+path)
    return BASE

def main():
    historical_product_end()
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
    up=(ROOT/"database/migrations/024_c06_delegated_role_ledger.sql").read_text(encoding="utf-8")
    down=(ROOT/"database/migrations/024_c06_delegated_role_ledger.down.sql").read_text(encoding="utf-8")
    import re
    sql=re.sub(r"(?m)^\s*--[^\n]*$", "", up)
    # Quoted error messages may contain the word grant; inspect executable SQL.
    def no_authority_change(source):
        unquoted=re.sub(r"'(?:''|[^'])*'", "''", source)
        return re.search(r"\b(?:GRANT|ALTER\s+TABLE|DROP\s+TABLE)\b",unquoted,re.I) is None
    need(no_authority_change(sql),"new ledger changes old privileges/schema")
    for forbidden in ("GRANT SELECT ON sample TO sample_role",
                      "ALTER TABLE sample ADD COLUMN unsafe INT",
                      "DROP TABLE sample"):
        need(not no_authority_change(sql+"\n"+forbidden+";"),"dangerous DDL mutation accepted")
    need(no_authority_change("SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C06 grant immutable';"),
         "quoted error message incorrectly treated as a privilege grant")
    for invariant in ("uq_c06_dg_scope","fk_c06_dc_actor","fk_c06_dc_resource","fk_c06_dg_issuer_scope",
                      "fk_c06_dv_exact","trg_c06_dg_no_update","trg_c06_dv_no_delete","ck_c06_dg_no_self",
                      "NEW.expires_at_utc<=c.valid_until_utc","NEW.expires_at_utc<=s.expires_at_utc"):
        need(invariant in sql,"missing ledger invariant "+invariant)
    need("IF EXISTS(SELECT 1 FROM c06_delegation_revocation)" in down and
         "ledger not empty; preserve history" in down,"unsafe down migration")
    for path in ("database/operations/c05_local_runtime_role.sql","database/operations/c06_04_local_runtime_role.sql"):
        need(git("rev-parse",BASE+":"+path)==git("rev-parse","HEAD:"+path),"runtime grant source changed")
    print("PASS C06-11 DELEGATION seventeen isolated paths, six rejected mutations; exact C06-02/07/08/09/10 successor routing, prior contracts and Master unchanged")

if __name__=="__main__":
    main()
