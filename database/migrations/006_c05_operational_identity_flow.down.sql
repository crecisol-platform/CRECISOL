-- Stop the API first. Destructive rollback of local identity-flow state.
DROP TABLE c05_identity_private_resource;
DROP TABLE c05_identity_branch;
DROP TABLE c05_identity_sensitive_outbox;
ALTER TABLE c05_identity_recovery DROP FOREIGN KEY fk_c05_recovery_tenant,
 DROP CHECK ck_c05_recovery_purpose, DROP INDEX ix_c05_recovery_tenant,
 DROP COLUMN protected_payload, DROP COLUMN account_revision,
 DROP COLUMN purpose, DROP COLUMN tenant_id;
DROP TABLE c05_identity_attempt_quota;
DROP TABLE c05_identity_mfa_backup;
DROP TABLE c05_identity_challenge;
ALTER TABLE c05_identity_session DROP COLUMN last_seen_at_utc,
 DROP COLUMN membership_revision, DROP COLUMN account_revision;
ALTER TABLE c05_identity_mfa_factor DROP INDEX uq_c05_active_totp,
 DROP COLUMN active_totp_account;
