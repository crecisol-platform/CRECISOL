-- Apply only to the existing isolated C05 runtime role after migration 021.
-- Eligibility cannot be issued, modified, or revoked by HTTP runtime.
GRANT SELECT ON crecisol_c05.c06_exception_entitlement TO 'c05_identity_local_runtime';
GRANT SELECT,INSERT ON crecisol_c05.c06_exception_request TO 'c05_identity_local_runtime';
GRANT SELECT,INSERT ON crecisol_c05.c06_exception_approval TO 'c05_identity_local_runtime';
GRANT SELECT,INSERT ON crecisol_c05.c06_exception_close TO 'c05_identity_local_runtime';
GRANT INSERT ON crecisol_c05.c06_exception_audit TO 'c05_identity_local_runtime';
