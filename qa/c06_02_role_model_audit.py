#!/usr/bin/env python3
"""C06-02 independent A/B/C; prepared roles cannot authorize endpoints."""
import hashlib
import os
import json
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
BASE = "b2e436221bca684eb43d4ce72e7044080cfd6601"
UP = (ROOT / "database/migrations/019_c06_prepared_roles.sql").read_text(encoding="utf-8")
DOWN = (ROOT / "database/migrations/019_c06_prepared_roles.down.sql").read_text(encoding="utf-8")
DOMAIN = (ROOT / "backend/src/Crecisol.Domain/Authorization/C06PreparedRoleSnapshot.cs").read_text(encoding="utf-8")
APP = (ROOT / "backend/src/Crecisol.Application/Authorization/InspectPreparedTenantRoles.cs").read_text(encoding="utf-8")
WORD = ROOT / "docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx"
WORD_SHA = "ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c"
CATALOG = json.loads((ROOT / "docs/c06/C06_01_CATALOGO_ACCIONES.json").read_text(encoding="utf-8"))

def need(ok, reason):
    if not ok:
        raise AssertionError("C06-02 NO-GO " + reason)

def validate_schema(text):
    # Only executable SQL counts: security warnings in comments are NOT GRANT statements.
    effective = re.sub(r"(?m)^\s*--[^\n]*$", "", text)
    required = (
        "fk_c06_proposal_membership", "FOREIGN KEY (tenant_id, membership_id)",
        "fk_c06_proposal_role_assigned", "FOREIGN KEY (tenant_id, role_id)",
        "fk_c06_proposal_branch_assigned", "FOREIGN KEY (tenant_id, branch_id)",
        "fk_c06_proposal_capability", "FOREIGN KEY (action_id, realm)",
        "ck_c06_capability_not_grantable CHECK (grantable = FALSE)",
        "ck_c06_proposal_not_grant CHECK (proposal_only = TRUE)",
        "ck_c06_proposal_tenant_realm CHECK (realm = 1)",
        "uq_c06_proposal_membership_role_branch",
        "trg_c06_proposal_immutable_scope",
        "trg_c06_role_immutable_identity",
        "trg_c06_audit_append_only_update", "trg_c06_audit_append_only_delete",
        "OLD.proposal_state = 2 AND NEW.proposal_state <> 2",
        "OLD.role_state = 3 AND NEW.role_state <> 3",
        "NEW.revision <= OLD.revision",
        "ON DELETE RESTRICT ON UPDATE RESTRICT",
        "valid_until_utc > valid_from_utc",
    )
    for marker in required:
        need(marker in effective, "missing SQL safety invariant: " + marker)
    need(not re.search(r"\bGRANT\s+(?:ALL|SELECT|INSERT|UPDATE|DELETE)\b", effective, re.I),
         "migration is attempting runtime privilege GRANT")
    need(not re.search(r"\b(?:UPDATE|ALTER|DELETE\s+FROM)\s+c05_", effective, re.I),
         "C05 operational schema changed by C06")
    return required

def scope():
    # Never freeze a future chapter to C06-02's original baseline: compare
    # the ACTUAL PR delta or one push commit, then keep the exact path allowlist.
    event = os.environ.get("GITHUB_EVENT_NAME", "")
    if event == "pull_request":
        need(os.environ.get("GITHUB_BASE_REF") == "dev", "PR base must be dev")
        spec = ["origin/dev...HEAD"]
    elif event == "push":
        need(os.environ.get("GITHUB_REF") == "refs/heads/dev", "push must target dev")
        spec = ["HEAD^", "HEAD"]
    else:
        # Reproducible local/offline audit on the original C06-02 feature only.
        spec = [BASE, "HEAD"]
    changed = subprocess.check_output(
        ["git", "diff", "--name-only", *spec], cwd=ROOT, text=True).splitlines()
    # Explicit sequential successor: preserve every other historical audit.
    if "docs/c06/C06_07_ESPECIFICACION.md" in changed:
        from c06_07_privacy_scope import require_exact
        require_exact(changed)
        return changed
    if "docs/c06/C06_06_ESPECIFICACION.md" in changed:
        from c06_06_exceptional_scope import require_exact
        require_exact(changed)
        return changed
    allowed = (
        "backend/src/Crecisol.Domain/Authorization/",
        "backend/src/Crecisol.Application/Authorization/",
        "backend/src/Crecisol.Infrastructure/Authorization/",
        "backend/tests/Crecisol.PreparedRoles.MySqlTests/",
        "backend/tests/Crecisol.PreparedRoles.ContractsTests/",
        "database/migrations/019_c06_",
        "database/tests/c06_02_",
        "qa/c06_02_",
        ".github/workflows/c06-02-",
    )
    exact_docs = {
        "docs/c06/C06_02_ESPECIFICACION.md",
        "docs/c06/C06_02_ACTA_CIERRE.md",
        "docs/c06/C06_02_MATRIZ_ROLES_CAPACIDADES.json",
        "docs/c06/C06_02_MATRIZ_ROLES_CAPACIDADES.md",
        "docs/ESTADO_VIGENTE_CAPITULOS.md",
        "docs/planificacion-futura/C06_MICROFASES_PROPUESTAS.md",
    }
    # C06-05 changes shared runtime entrypoints, not the prepared-role registry.
    # Admit only its exact reviewed diff and retain all historical guard checks.
    c06_05_paths = {
        "backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.Authorized.cs",
        "backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.Platform.cs",
        "backend/src/Crecisol.Domain/Authorization/C06SensitiveOperationPolicy.cs",
        "backend/tests/Crecisol.C06StepUp.ContractsTests/Program.cs",
        "backend/tests/Crecisol.C06StepUp.ContractsTests/Crecisol.C06StepUp.ContractsTests.csproj",
        "docs/c06/C06_05_ESPECIFICACION.md",
        "qa/c06_05_stepup_audits.py",
        "qa/c05_operational_flow_e2e.mjs",
        ".github/workflows/c06-05-sensitive-step-up.yml",
        "qa/c06_03_context_audits.py",
        "qa/c06_04_abac_audits.py",
        "qa/c06_02_role_model_audit.py",
    }
    if "docs/c06/C06_05_ESPECIFICACION.md" in changed:
        need(set(changed) == c06_05_paths, "unexpected C06-05 regression scope")
        exact_docs |= c06_05_paths
    need(changed and all(p in exact_docs or any(p.startswith(a) for a in allowed)
                             for p in changed),
         "unexpected path in C06-02 diff: " + repr(changed))
    need(not any(p.startswith(("frontend/", "backend/src/Crecisol.Api/"))
                 for p in changed), "runtime/UI path touched")
    return changed

def run(stage):
    validate_schema(UP)
    changed = scope()
    need("DROP TABLE IF EXISTS c06_capability_registry" in DOWN and
         "DROP TABLE IF EXISTS c06_role_proposal_audit" in DOWN and
         not re.search(r"\b(?:DROP|ALTER)\s+TABLE\s+(?:IF EXISTS\s+)?c05_", DOWN, re.I),
         "C06 rollback modifies historical tenant/account tables")
    need("public bool AuthorizesPrivateResource => false;" in APP and
         "if (actionGrantable)" in DOMAIN and "ReadExactCandidatesAsync" in APP,
         "candidate metadata presented as runtime authority")
    need(len(CATALOG["api_actions"]) == 80 and
         len(CATALOG["non_http_contracts"]) == 12 and
         len(CATALOG["normative_actions"]) == 28 and
         all(x["grantable"] is False and x["http_registered"] is False
             for x in CATALOG["normative_actions"]) and
         all(x["c06_policy_bound"] is False for x in CATALOG["api_actions"]),
         "C06-01 inventory status silently changed")
    if stage == "A":
        acta = ROOT / "docs/c06/C06_02_ACTA_CIERRE.md"
        if acta.is_file():
            contents = acta.read_text(encoding="utf-8")
            state = (ROOT / "docs/ESTADO_VIGENTE_CAPITULOS.md").read_text(encoding="utf-8")
            need("5022328e499f1e3f039a3d308e2b1d754231215c" in contents and
                 "45469970937" in contents and "45469970986" in contents and
                 "C06-02" in state and "C06-03" in state and
                 "NO CERTIFICADO" in state,
                 "institutional act missing SHA/evidence/next gate")
        need(WORD.is_file() and hashlib.sha256(WORD.read_bytes()).hexdigest() == WORD_SHA,
             "approved C06 Word SHA mismatch")
        print("PASS C06-02 A: approved Word, C06-01 canonical inventory, exact diff:", changed)
    elif stage == "B":
        count = 0
        for marker in (
            "fk_c06_proposal_membership", "fk_c06_proposal_role_assigned",
            "fk_c06_proposal_branch_assigned", "fk_c06_proposal_capability",
            "ck_c06_capability_not_grantable CHECK (grantable = FALSE)",
            "ck_c06_proposal_not_grant CHECK (proposal_only = TRUE)",
            "ck_c06_proposal_tenant_realm CHECK (realm = 1)",
            "trg_c06_proposal_immutable_scope", "trg_c06_audit_append_only_delete",
            "OLD.proposal_state = 2 AND NEW.proposal_state <> 2",
        ):
            forged = UP.replace(marker, "REMOVED_BY_ATTACK")
            try:
                validate_schema(forged)
            except AssertionError:
                count += 1
            else:
                raise AssertionError("C06-02 NO-GO hostile SQL omission accepted")
        need(count == 10, "hostile mutations unverified")
        print("PASS C06-02 B: 10 malicious schema mutations rejected; zero new grants")
    elif stage == "C":
        need("namespace Crecisol.Domain.Authorization;" in DOMAIN and
             "namespace Crecisol.Application.Authorization;" in APP and
             not any(s in DOMAIN for s in ("MySqlConnector", "Microsoft.AspNetCore", "EntityFramework")),
             "Domain/application architecture violated")
        adapter = (ROOT / "backend/src/Crecisol.Infrastructure/Authorization/MySqlPreparedRoleReader.cs").read_text(encoding="utf-8")
        need("MySqlRfc4122Guid.Encode" in adapter and
             "JOIN c05_identity_membership" in adapter and
             "JOIN c05_identity_branch" in adapter and
             "ReadExactCandidatesAsync" in adapter and
             "new MySqlConnectionStringBuilder" in adapter,
             "MySQL read adapter lacks tenant-bound parameterized joins")
        print("PASS C06-02 C: Domain/Application/Infrastructure scoped, no HTTP/UI activation")
    else:
        raise AssertionError("expected A|B|C")

if __name__ == "__main__":
    run(sys.argv[1] if len(sys.argv) == 2 else "")
