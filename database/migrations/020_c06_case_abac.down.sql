-- C06-04 rollback: ONLY in disposable MySQL with verified backup and
-- explicit assessment of active assignment records; NEVER on owner data.
DROP TRIGGER IF EXISTS trg_c06_case_assignment_no_delete;
DROP TRIGGER IF EXISTS trg_c06_case_assignment_immutable;
DROP TABLE IF EXISTS c06_case_resource_assignment;
