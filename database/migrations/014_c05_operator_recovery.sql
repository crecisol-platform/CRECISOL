-- Recovery of platform operators uses only other platform operators. No tenant
-- approval, account, mailbox change, or membership can authorize this realm.
ALTER TABLE c05_platform_operator
 ADD COLUMN protected_contact VARBINARY(512) NULL,
 ADD COLUMN previous_contact VARBINARY(512) NULL,
 ADD COLUMN contact_changed_at_utc DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
 ADD COLUMN recovery_pending BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE c05_platform_identity_audit ADD COLUMN purpose_hmac BINARY(32) NULL;
CREATE TABLE c05_platform_mfa_backup (
 operator_id BINARY(16) NOT NULL, token_hmac BINARY(32) NOT NULL,
 consumed_at_utc DATETIME(6) NULL,
 PRIMARY KEY(operator_id,token_hmac), FOREIGN KEY(operator_id) REFERENCES c05_platform_operator(operator_id)
) ENGINE=InnoDB;
CREATE TABLE c05_platform_factor_change (
 token_hmac BINARY(32) NOT NULL PRIMARY KEY,
 operator_id BINARY(16) NOT NULL, session_hmac BINARY(32) NOT NULL,
 operator_revision BIGINT UNSIGNED NOT NULL, protected_seed VARBINARY(128) NOT NULL,
 expires_at_utc DATETIME(6) NOT NULL, consumed_at_utc DATETIME(6) NULL,
 FOREIGN KEY(operator_id) REFERENCES c05_platform_operator(operator_id)
) ENGINE=InnoDB;
CREATE TABLE c05_platform_mailbox_change (
 token_hmac BINARY(32) NOT NULL PRIMARY KEY,
 operator_id BINARY(16) NOT NULL, session_hmac BINARY(32) NOT NULL,
 operator_revision BIGINT UNSIGNED NOT NULL,
 protected_contact VARBINARY(512) NOT NULL,
 login_hmac BINARY(32) NOT NULL, conflict_login_hmac BINARY(32) NOT NULL,
 expires_at_utc DATETIME(6) NOT NULL, consumed_at_utc DATETIME(6) NULL,
 FOREIGN KEY(operator_id) REFERENCES c05_platform_operator(operator_id)
) ENGINE=InnoDB;
CREATE TABLE c05_platform_recovery_case (
 case_id BINARY(16) NOT NULL PRIMARY KEY,
 operator_id BINARY(16) NOT NULL,
 operator_revision BIGINT UNSIGNED NOT NULL,
 created_at_utc DATETIME(6) NOT NULL,
 ready_at_utc DATETIME(6) NULL,
 expires_at_utc DATETIME(6) NOT NULL,
 blocked BOOLEAN NOT NULL DEFAULT FALSE,
 consumed_at_utc DATETIME(6) NULL,
 pending_password_phc VARCHAR(512) CHARACTER SET ascii COLLATE ascii_bin NULL,
 pending_seed VARBINARY(128) NULL,
 enrollment_hmac BINARY(32) NULL,
 enrollment_expires_at_utc DATETIME(6) NULL,
 active_operator BINARY(16) GENERATED ALWAYS AS(IF(NOT blocked AND consumed_at_utc IS NULL,operator_id,NULL)) STORED,
 UNIQUE KEY uq_c05_operator_recovery_active(active_operator),
 FOREIGN KEY(operator_id) REFERENCES c05_platform_operator(operator_id)
) ENGINE=InnoDB;
CREATE TABLE c05_platform_recovery_review (
 case_id BINARY(16) NOT NULL, reviewer_id BINARY(16) NOT NULL,
 reviewer_revision BIGINT UNSIGNED NOT NULL,
 approved BOOLEAN NOT NULL, evidence_hash BINARY(32) NOT NULL,
 reason_hmac BINARY(32) NOT NULL, reviewed_at_utc DATETIME(6) NOT NULL,
 PRIMARY KEY(case_id,reviewer_id),
 FOREIGN KEY(case_id) REFERENCES c05_platform_recovery_case(case_id),
 FOREIGN KEY(reviewer_id) REFERENCES c05_platform_operator(operator_id)
) ENGINE=InnoDB;
CREATE TABLE c05_platform_recovery_share (
 case_id BINARY(16) NOT NULL, reviewer_id BINARY(16) NOT NULL,
 share_hmac BINARY(32) NOT NULL, expires_at_utc DATETIME(6) NOT NULL,
 PRIMARY KEY(case_id,reviewer_id),
 FOREIGN KEY(case_id,reviewer_id) REFERENCES c05_platform_recovery_review(case_id,reviewer_id)
) ENGINE=InnoDB;
CREATE TABLE c05_platform_identity_incident (
 token_hmac BINARY(32) NOT NULL PRIMARY KEY,
 operator_id BINARY(16) NOT NULL,
 expires_at_utc DATETIME(6) NOT NULL,
 reported_at_utc DATETIME(6) NULL,
 FOREIGN KEY(operator_id) REFERENCES c05_platform_operator(operator_id)
) ENGINE=InnoDB;
DELIMITER $$
CREATE TRIGGER c05_operator_review_no_update BEFORE UPDATE ON c05_platform_recovery_review FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Operator identity reviews are append-only'; END$$
CREATE TRIGGER c05_operator_review_no_delete BEFORE DELETE ON c05_platform_recovery_review FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Operator identity reviews are append-only'; END$$
DELIMITER ;
