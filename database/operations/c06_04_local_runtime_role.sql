-- Apply with an isolated privileged migrator AFTER 020. This role existed in
-- the C05 local harness; the API principal gets SELECT ONLY on C06 assignments.
-- No DML, assignment issuance, platform grants or production configuration.
GRANT SELECT ON crecisol_c05.c06_case_resource_assignment
  TO 'c05_identity_local_runtime';
