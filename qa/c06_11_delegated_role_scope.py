#!/usr/bin/env python3
"""C06-11 delegation policy and persistence slice: exact source scope, never a certificate."""
import os
import subprocess
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
BASE="076118ab5370e1fcbc7bfe0f1caefe07a664365b"
PATHS=frozenset(["backend/src/Crecisol.Domain/Authorization/C06ScopedRoleDelegationPolicy.cs","backend/tests/Crecisol.C06Delegation.ContractsTests/Program.cs","backend/tests/Crecisol.C06Delegation.ContractsTests/Crecisol.C06Delegation.ContractsTests.csproj","docs/c06/C06_11_DELEGACION_EFECTIVA_DISENO_Y_POLITICA.md",".github/workflows/c06-11-delegated-role-policy.yml","qa/c06_11_delegated_role_scope.py","qa/c06_02_role_model_audit.py","database/migrations/024_c06_delegated_role_ledger.sql","database/migrations/024_c06_delegated_role_ledger.down.sql","database/tests/c06_11_delegated_role_mysql.sh","qa/c06_07_privacy_scope.py","qa/c06_08_query_scope.py","qa/c06_09_delivery_scope.py","qa/c06_10_offensive_scope.py","qa/c06_07_privacy_audits.py","qa/c06_09_delivery_audits.py","qa/c06_10_offensive_audits.py","backend/src/Crecisol.Infrastructure/Authorization/MySqlC06DelegationLedger.cs","backend/tests/Crecisol.C06Delegation.MySqlTests/Crecisol.C06Delegation.MySqlTests.csproj","backend/tests/Crecisol.C06Delegation.MySqlTests/Program.cs","backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.Authorized.cs","backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.cs","backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.Delegation.cs","backend/src/Crecisol.Application/Identity/C06DelegationPreparation.cs","database/migrations/025_c06_delegation_confirmation.sql","database/migrations/025_c06_delegation_confirmation.down.sql","backend/tests/Crecisol.C06Delegation.FlowTests/Program.cs","backend/tests/Crecisol.C06Delegation.FlowTests/Crecisol.C06Delegation.FlowTests.csproj","qa/c06_03_context_audits.py","qa/c06_04_abac_audits.py","qa/c06_05_stepup_audits.py","backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.DelegatedRead.cs","database/migrations/026_c06_delegation_authority.sql","database/migrations/026_c06_delegation_authority.down.sql","qa/c05_sha1_exclusive_audits.py"])
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
    ],
    "backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.Authorized.cs": [
        [
            "        if (r.Operation == \"sessions/keepalive\")",
            "        // Candidate-only internal operations: the existing HTTP route inventory is unchanged.\n        if (r.Operation is \"delegation/prepare\" or \"delegation/confirm\")\n            return await C06DelegationTransition(u, tenant, actor, account, member, session, factor, idle, r, ct);\n        if (r.Operation == \"delegation/read\")\n            return await C06ReadDelegatedCase(u, tenant, actor, account, member, session, factor, idle, r);\n        if (r.Operation == \"sessions/keepalive\")"
        ]
    ],
    "backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.cs": [
        [
            "        public async Task<int> Run(string sql, params (string, object)[] parameters)",
            "        public Crecisol.Infrastructure.Authorization.MySqlC06DelegationLedger DelegationLedger() => new(db, tx);\n        public async Task<int> Run(string sql, params (string, object)[] parameters)"
        ]
    ],
    "qa/c06_03_context_audits.py": [
        [
            "    # Explicit sequential successor: preserve every other historical audit.",
            "    # Explicit sequential successor: preserve every other historical audit.\n    if \"docs/c06/C06_11_DELEGACION_EFECTIVA_DISENO_Y_POLITICA.md\" in changed:\n        from c06_11_delegated_role_scope import require_exact\n        require_exact(changed)\n        return changed"
        ]
    ],
    "qa/c06_04_abac_audits.py": [
        [
            "    # Explicit sequential successor: preserve every other historical audit.",
            "    # Explicit sequential successor: preserve every other historical audit.\n    if \"docs/c06/C06_11_DELEGACION_EFECTIVA_DISENO_Y_POLITICA.md\" in changed:\n        from c06_11_delegated_role_scope import require_exact\n        require_exact(changed)\n        return changed"
        ]
    ],
    "qa/c06_05_stepup_audits.py": [
        [
            "    # Explicit sequential successor: preserve every other historical audit.",
            "    # Explicit sequential successor: preserve every other historical audit.\n    if \"docs/c06/C06_11_DELEGACION_EFECTIVA_DISENO_Y_POLITICA.md\" in changed:\n        from c06_11_delegated_role_scope import require_exact\n        require_exact(changed)\n        return changed"
        ]
    ],
    "qa/c05_sha1_exclusive_audits.py": [
        [
            " ensure(bool(paths), \"empty current change scope\")",
            " ensure(bool(paths), \"empty current change scope\")\n if \"docs/c06/C06_11_DELEGACION_EFECTIVA_DISENO_Y_POLITICA.md\" in paths:\n  from c06_11_delegated_role_scope import require_exact\n  require_exact(paths)\n  return len(paths)"
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
    confirmation=(ROOT/"database/migrations/025_c06_delegation_confirmation.sql").read_text(encoding="utf-8")
    executable=re.sub(r"(?m)^\s*--[^\n]*$", "", confirmation)
    need(no_authority_change(executable),"confirmation migration expands legacy privileges/schema")
    need(git("rev-parse","b66f5bd4e746c23b79ed468f06ee5a9ce26cfb53:database/migrations/024_c06_delegated_role_ledger.sql")==git("rev-parse","HEAD:database/migrations/024_c06_delegated_role_ledger.sql"),"approved ledger changed")
    for invariant in ("uq_c06_dcf_token","fk_c06_dcf_member","fk_c06_dcf_session","trg_c06_dcf_consume","trg_c06_dcf_no_delete","TIMESTAMPDIFF(MICROSECOND"):
        need(invariant in executable,"missing confirmation invariant "+invariant)
    transport=read_text=(ROOT/"backend/src/Crecisol.Api/Modules/Identity/OperationalIdentityModule.cs").read_text(encoding="utf-8")
    need(all(op not in transport for op in ("delegation/prepare","delegation/confirm","delegation/read")),"candidate exposed via HTTP")
    candidate=(ROOT/"backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.Delegation.cs").read_text(encoding="utf-8")
    for invariant in ("CRECISOL_C06_DELEGATION_LAB_ENABLED", "HasCurrentTerms(u,tenant,actor)", "C06ScopedRoleDelegationPolicy.CanPrepare(e)", "Number(count,\"pending\")>=3", "CryptographicOperations.FixedTimeEquals(", "await AcceptTotp(u,actor,stepFactor,s.Code!,activate:false)", "await ledger.AppendAsync(proved"):
        need(invariant in candidate,"delegation confirmation missing "+invariant)
    need(candidate.index("CryptographicOperations.FixedTimeEquals(")<candidate.index("await AcceptTotp(")<candidate.index("UPDATE c06_delegation_confirmation SET consumed_at_utc=@now")<candidate.index("await ledger.AppendAsync(proved"),"confirmation proof/write sequence drift")
    provenance=(ROOT/"database/migrations/026_c06_delegation_authority.sql").read_text(encoding="utf-8")
    need(no_authority_change(re.sub(r"(?m)^\s*--[^\n]*$", "", provenance)),"provenance migration expands historical authority")
    need(git("rev-parse","4267ea67dbf69f5c37fae1a2fd368cc8fe21d44e:database/migrations/025_c06_delegation_confirmation.sql")==git("rev-parse","HEAD:database/migrations/025_c06_delegation_confirmation.sql"),"approved confirmation migration changed")
    reader=(ROOT/"backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.DelegatedRead.cs").read_text(encoding="utf-8")
    for invariant in ("JOIN c06_delegation_authority", "v.grant_id IS NULL","src.grant_revision=a.source_revision",
                      "ent.revision=a.entitlement_revision","x.owner_account_id=a.owner_account_id",
                      "sa.security_revision=a.subject_account_revision","ia.security_revision=a.issuer_account_revision",
                      "c.revision=g.ceiling_revision","role.revision=g.role_revision","sources.Count!=1"):
        need(invariant in reader,"missing delegated read invariant "+invariant)
    need(reader.index("C06CaseAbacPolicy.Allows(proof)")<reader.index("SELECT display_reference"),"private content selected before delegated ABAC")
    need(candidate.index("await ledger.AppendAsync(proved")<candidate.index("INSERT INTO c06_delegation_authority"),"provenance written outside confirmed issuance")
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
    print("PASS C06-11 DELEGATION thirty-five isolated paths, six rejected mutations; exact C06-02/07/08/09/10 successor routing, prior contracts and Master unchanged")

if __name__=="__main__":
    main()
