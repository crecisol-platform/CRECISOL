"""Exact C06-09 successor paths. No product grants or schema expansion."""
PATHS=frozenset([
  ".github/workflows/c06-09-protected-delivery.yml",
  "backend/src/Crecisol.Api/Configuration/C06SafeRuntimeLoggerProvider.cs",
  "backend/src/Crecisol.Api/Contracts/C06IdentityResponseProjection.cs",
  "backend/src/Crecisol.Api/Program.cs",
  "backend/src/Crecisol.Application/Identity/C06ProtectedContent.cs",
  "backend/src/Crecisol.Infrastructure/Identity/CanonicalProfilePng.cs",
  "backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.Profile.cs",
  "backend/src/Crecisol.Infrastructure/Tenancy/C06CaseAbacProjection.cs",
  "backend/src/Crecisol.Infrastructure/Tenancy/C06ExceptionalAccess.cs",
  "backend/tests/Crecisol.C06Delivery.ContractsTests/Crecisol.C06Delivery.ContractsTests.csproj",
  "backend/tests/Crecisol.C06Delivery.ContractsTests/Program.cs",
  "backend/tests/Crecisol.C06Privacy.ContractsTests/Program.cs",
  "database/tests/c06_09_delivery_mysql.sh",
  "docs/c06/C06_09_ESPECIFICACION.md",
  "qa/c05_operational_flow_e2e.mjs",
  "qa/c05_operator_recovery_checks.mjs",
  "qa/c05_profile_browser_checks.mjs",
  "qa/c05_sha1_exclusive_audits.py",
  "qa/c06_02_role_model_audit.py",
  "qa/c06_03_context_audits.py",
  "qa/c06_04_abac_audits.py",
  "qa/c06_05_stepup_audits.py",
  "qa/c06_06_exceptional_scope.py",
  "qa/c06_07_privacy_audits.py",
  "qa/c06_07_privacy_scope.py",
  "qa/c06_08_query_checks.mjs",
  "qa/c06_08_query_scope.py",
  "qa/c06_09_delivery_audits.py",
  "qa/c06_09_delivery_checks.mjs",
  "qa/c06_09_delivery_scope.py"
])
def require_exact(changed):
    actual=frozenset(changed)
    if actual!=PATHS:
        raise AssertionError("C06-09 NO-GO unexpected scope "+repr(sorted(actual ^ PATHS)))
    return changed
