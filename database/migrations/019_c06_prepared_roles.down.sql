-- C06-02 rollback ONLY after an authorized backup/review.
-- Never run automatically against owner or production data.
DROP TRIGGER IF EXISTS trg_c06_audit_append_only_delete;
DROP TRIGGER IF EXISTS trg_c06_audit_append_only_update;
DROP TRIGGER IF EXISTS trg_c06_role_immutable_identity;
DROP TRIGGER IF EXISTS trg_c06_proposal_immutable_scope;
DROP TABLE IF EXISTS c06_role_proposal_audit;
DROP TABLE IF EXISTS c06_membership_role_proposal;
DROP TABLE IF EXISTS c06_role_capability_proposal;
DROP TABLE IF EXISTS c06_tenant_role;
DROP TABLE IF EXISTS c06_capability_registry;
