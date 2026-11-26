"""Exact C06-10 QA/documentation successor scope; no product files."""
PATHS=frozenset([
  ".github/workflows/c06-10-offensive-regression.yml",
  "database/tests/c06_10_offensive_mysql.sh",
  "docs/c06/C06_10_ESPECIFICACION.md",
  "docs/c06/C06_10_MATRIZ_REGRESION.json",
  "qa/c05_operational_flow_e2e.mjs",
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
  "qa/c06_09_delivery_scope.py",
  "qa/c06_10_offensive_audits.py",
  "qa/c06_10_offensive_checks.mjs",
  "qa/c06_10_offensive_scope.py"
])
def require_exact(changed):
    actual=frozenset(changed)
    if actual!=PATHS:
        raise AssertionError("C06-10 NO-GO unexpected scope "+repr(sorted(actual ^ PATHS)))
    return changed
