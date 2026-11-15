-- Rollback for 001_c05_identity_operational.sql.
-- Destructive: only run against the isolated environment after exporting any
-- required audit evidence and confirming all identity traffic is disabled.
DROP TRIGGER IF EXISTS c05_identity_audit_no_delete;
DROP TRIGGER IF EXISTS c05_identity_audit_no_update;
DROP TABLE IF EXISTS c05_identity_audit;
DROP TABLE IF EXISTS c05_identity_registration_quota;
DROP TABLE IF EXISTS c05_identity_registration_intent;
DROP TABLE IF EXISTS c05_identity_resource_grant;
DROP TABLE IF EXISTS c05_identity_recovery;
DROP TABLE IF EXISTS c05_identity_membership_approval;
DROP TABLE IF EXISTS c05_identity_session;
DROP TABLE IF EXISTS c05_identity_mfa_factor;
DROP TABLE IF EXISTS c05_identity_membership;
DROP TABLE IF EXISTS c05_identity_mailbox_proof;
DROP TABLE IF EXISTS c05_identity_account;
DROP TABLE IF EXISTS c04_verified_tenant_host;
DROP TABLE IF EXISTS c04_tenant_catalog;



