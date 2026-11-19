-- Disposable QA rollback only. No rollback of MFA 017 is provided.
DROP TABLE IF EXISTS c05_session_idle_policy;
ALTER TABLE c05_platform_operator DROP COLUMN can_manage_session_policy;
