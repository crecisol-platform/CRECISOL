"""Exact sequential scope for C06-08; preserve historical audits."""
PATHS=frozenset([
  ".github/workflows/c06-08-scoped-queries.yml",
  "backend/src/Crecisol.Api/Modules/Identity/OperationalIdentityModule.cs",
  "backend/src/Crecisol.Infrastructure/Identity/C06PendingCursor.cs",
  "backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.Assisted.cs",
  "backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.Authorized.cs",
  "backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.OperatorRecovery.cs",
  "backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.Passkeys.cs",
  "backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.PendingApprovals.cs",
  "backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.Platform.cs",
  "backend/tests/Crecisol.C06Query.ContractsTests/Crecisol.C06Query.ContractsTests.csproj",
  "backend/tests/Crecisol.C06Query.ContractsTests/Program.cs",
  "database/migrations/023_c06_scoped_query_indexes.down.sql",
  "database/migrations/023_c06_scoped_query_indexes.sql",
  "database/tests/c06_02_prepared_roles_mysql.sh",
  "database/tests/c06_08_scoped_query_mysql.sh",
  "docs/c06/C06_08_ESPECIFICACION.md",
  "qa/c05_operational_flow_e2e.mjs",
  "qa/c05_sha1_exclusive_audits.py",
  "qa/c06_02_role_model_audit.py",
  "qa/c06_03_context_audits.py",
  "qa/c06_04_abac_audits.py",
  "qa/c06_05_stepup_audits.py",
  "qa/c06_06_exceptional_scope.py",
  "qa/c06_07_privacy_audits.py",
  "qa/c06_07_privacy_scope.py",
  "qa/c06_08_query_audits.py",
  "qa/c06_08_query_checks.mjs",
  "qa/c06_08_query_scope.py"
])
def require_exact(changed):
    if "docs/c06/C06_10_ESPECIFICACION.md" in changed:
        from c06_10_offensive_scope import require_exact as require_offensive
        return require_offensive(changed)
    if "docs/c06/C06_09_ESPECIFICACION.md" in changed:
        from c06_09_delivery_scope import require_exact as require_delivery
        return require_delivery(changed)
    actual=frozenset(changed)
    if actual!=PATHS:
        raise AssertionError("C06-08 NO-GO unexpected scope "+repr(sorted(actual ^ PATHS)))
    return changed
