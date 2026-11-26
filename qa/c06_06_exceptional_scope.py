"""Exact C06-06 successor scope; earlier audits still run every invariant."""
PATHS = frozenset([
  "backend/src/Crecisol.Api/Modules/Identity/OperationalIdentityModule.cs",
  "backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.Platform.cs",
  "backend/src/Crecisol.Domain/Authorization/C06ExceptionalAccessPolicy.cs",
  "backend/src/Crecisol.Infrastructure/Tenancy/C06ExceptionalAccess.cs",
  "database/migrations/021_c06_exceptional_access.sql",
  "database/migrations/021_c06_exceptional_access.down.sql",
  "database/operations/c06_06_local_runtime_role.sql",
  "backend/tests/Crecisol.C06ExceptionalAccess.ContractsTests/Crecisol.C06ExceptionalAccess.ContractsTests.csproj",
  "backend/tests/Crecisol.C06ExceptionalAccess.ContractsTests/Program.cs",
  "docs/c06/C06_06_ESPECIFICACION.md",
  "docs/c06/C06_06_CATALOGO_ACTIVACION.json",
  "qa/c05_operational_flow_e2e.mjs",
  "qa/c06_06_exceptional_access_checks.mjs",
  "qa/c06_06_exceptional_scope.py",
  "qa/c06_06_exceptional_audits.py",
  ".github/workflows/c06-06-exceptional-access.yml",
  "qa/c06_01_permissions_catalog_audit.py",
  "qa/c06_02_role_model_audit.py",
  "qa/c06_03_context_audits.py",
  "qa/c06_04_abac_audits.py",
  "qa/c06_05_stepup_audits.py",
  "database/tests/c06_06_exceptional_mysql.sh"
])
def require_exact(changed):
    if "docs/c06/C06_11_DELEGACION_EFECTIVA_DISENO_Y_POLITICA.md" in changed:
        from c06_11_delegated_role_scope import require_exact
        return require_exact(changed)
    if "docs/c06/C06_10_ESPECIFICACION.md" in changed:
        from c06_10_offensive_scope import require_exact
        require_exact(changed)
        return changed
    if "docs/c06/C06_09_ESPECIFICACION.md" in changed:
        from c06_09_delivery_scope import require_exact
        require_exact(changed)
        return changed
    if "docs/c06/C06_08_ESPECIFICACION.md" in changed:
        from c06_08_query_scope import require_exact
        require_exact(changed)
        return changed
    if "docs/c06/C06_07_ESPECIFICACION.md" in changed:
        from c06_07_privacy_scope import require_exact
        require_exact(changed)
        return changed
    if set(changed) != PATHS:
        raise AssertionError("C06-06 NO-GO unexpected successor diff " + repr(sorted(set(changed) ^ PATHS)))
