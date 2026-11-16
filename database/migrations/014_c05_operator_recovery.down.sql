DROP TABLE c05_platform_identity_incident;
DROP TABLE c05_platform_recovery_share;
DROP TABLE c05_platform_recovery_review;
DROP TABLE c05_platform_recovery_case;
DROP TABLE c05_platform_factor_change;
DROP TABLE c05_platform_mailbox_change;
DROP TABLE c05_platform_mfa_backup;
ALTER TABLE c05_platform_operator DROP COLUMN recovery_pending,DROP COLUMN protected_contact,DROP COLUMN previous_contact,DROP COLUMN contact_changed_at_utc;
ALTER TABLE c05_platform_identity_audit DROP COLUMN purpose_hmac;
