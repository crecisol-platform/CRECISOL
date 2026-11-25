-- Reverse only the C06-08 indexes. Existing rows and privileges remain.
ALTER TABLE c05_identity_abuse_signal DROP INDEX ix_c06_08_signal_window;
ALTER TABLE c05_identity_audit DROP INDEX ix_c06_08_subject_events;
ALTER TABLE c05_platform_session DROP INDEX ix_c06_08_operator_sessions;
ALTER TABLE c05_identity_session DROP INDEX ix_c06_08_tenant_sessions;
ALTER TABLE c05_identity_membership DROP INDEX ix_c06_08_pending;
