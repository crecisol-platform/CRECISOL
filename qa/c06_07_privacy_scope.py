"""Exact C06-07 successor scope, preserving previous invariants."""
PATHS = frozenset([
  "backend/src/Crecisol.Api/Modules/Identity/OperationalIdentityModule.cs",
  "backend/src/Crecisol.Api/Contracts/C06IdentityResponseProjection.cs",
  "backend/tests/Crecisol.C06Privacy.ContractsTests/Program.cs",
  "backend/tests/Crecisol.C06Privacy.ContractsTests/Crecisol.C06Privacy.ContractsTests.csproj",
  "docs/c06/C06_07_CONTRATOS_RESPUESTA.json",
  "database/migrations/022_c06_privacy_disposition.sql",
  "database/migrations/022_c06_privacy_disposition.down.sql",
  "database/operations/c06_07_local_privacy_worker.sql",
  "database/tests/c06_07_privacy_mysql.sh",
  "qa/c06_07_privacy_response_checks.mjs",
  "qa/c05_operational_flow_e2e.mjs",
  "docs/c06/C06_07_ESPECIFICACION.md",
  "qa/c06_07_privacy_scope.py",
  "qa/c06_07_privacy_audits.py",
  ".github/workflows/c06-07-privacy-minimization.yml",
  "qa/c06_06_exceptional_scope.py",
  "qa/c06_02_role_model_audit.py",
  "qa/c06_03_context_audits.py",
  "qa/c06_04_abac_audits.py",
  "qa/c06_05_stepup_audits.py"
])
REVALIDATION_PATHS = frozenset([
  "qa/c05_operational_flow_e2e.mjs",
  "qa/c06_07_privacy_response_checks.mjs",
  "qa/c06_07_privacy_oracle_tests.mjs",
  "qa/c06_07_privacy_scope.py",
  ".github/workflows/c06-07-privacy-minimization.yml",
  "docs/c06/C06_07_ESPECIFICACION.md",
])
# Exact extension for the confirmed ingress-cache defect. Preserve the
# original implementation and first revalidation sets for historical evidence.
INGRESS_REVALIDATION_PATHS = REVALIDATION_PATHS | frozenset([
  "backend/src/Crecisol.Api/Program.cs",
  "qa/c06_07_privacy_audits.py",
])
def require_exact(changed):
    actual = frozenset(changed)
    if actual not in (PATHS, REVALIDATION_PATHS, INGRESS_REVALIDATION_PATHS):
        raise AssertionError("C06-07 NO-GO unexpected successor diff " + repr(sorted(actual)))
