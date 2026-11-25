-- C06-08 additive indexes only; apply after C05/C06 migrations in isolated schema.
-- No new privileges, tables, payloads, or grants.
ALTER TABLE c05_identity_membership ADD INDEX ix_c06_08_pending (tenant_id,membership_state,created_at_utc,membership_id);
ALTER TABLE c05_identity_session ADD INDEX ix_c06_08_tenant_sessions (tenant_id,account_id,membership_id,revoked_at_utc,last_seen_at_utc,session_id);
-- Both revision and operator are equality predicates. Start with revision so
-- InnoDB cannot replace the implicit operator_id foreign-key support index.
ALTER TABLE c05_platform_session ADD INDEX ix_c06_08_operator_sessions (security_revision,operator_id,revoked_at_utc,last_seen_at_utc,session_id);
ALTER TABLE c05_identity_audit ADD INDEX ix_c06_08_subject_events (tenant_id,subject_account_id,actor_account_id,created_at_utc,audit_id);
ALTER TABLE c05_identity_abuse_signal ADD INDEX ix_c06_08_signal_window (created_at_utc,signal_kind);
