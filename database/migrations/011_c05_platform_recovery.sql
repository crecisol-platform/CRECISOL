-- PLATFORM_IDENTITY is independent of tenant accounts/memberships/grants.
-- No public registration, default operator, credential or permission is seeded.
CREATE TABLE c05_platform_operator (
 operator_id BINARY(16) NOT NULL PRIMARY KEY,
 login_hmac BINARY(32) NOT NULL UNIQUE,
 conflict_login_hmac BINARY(32) NOT NULL,
 password_phc VARCHAR(512) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 protected_seed VARBINARY(128) NOT NULL,
 active BOOLEAN NOT NULL,
 security_revision BIGINT UNSIGNED NOT NULL,
 last_totp_step BIGINT UNSIGNED NULL,
 CHECK(security_revision>0)
) ENGINE=InnoDB;
CREATE TABLE c05_platform_challenge (
 token_hmac BINARY(32) NOT NULL PRIMARY KEY,
 operator_id BINARY(16) NOT NULL,
 security_revision BIGINT UNSIGNED NOT NULL,
 attempts TINYINT UNSIGNED NOT NULL DEFAULT 0,
 expires_at_utc DATETIME(6) NOT NULL,
 consumed_at_utc DATETIME(6) NULL,
 FOREIGN KEY(operator_id) REFERENCES c05_platform_operator(operator_id), CHECK(attempts<=5)
) ENGINE=InnoDB;
CREATE TABLE c05_platform_session (
 token_hmac BINARY(32) NOT NULL PRIMARY KEY,
 operator_id BINARY(16) NOT NULL,
 security_revision BIGINT UNSIGNED NOT NULL,
 mfa_at_utc DATETIME(6) NOT NULL,
 expires_at_utc DATETIME(6) NOT NULL,
 last_seen_at_utc DATETIME(6) NOT NULL,
 revoked_at_utc DATETIME(6) NULL,
 FOREIGN KEY(operator_id) REFERENCES c05_platform_operator(operator_id)
) ENGINE=InnoDB;
CREATE TABLE c05_platform_attempt_quota (
 bucket_hmac BINARY(32) NOT NULL, window_utc BIGINT NOT NULL,
 attempts SMALLINT UNSIGNED NOT NULL, PRIMARY KEY(bucket_hmac,window_utc)
) ENGINE=InnoDB;
CREATE TABLE c05_platform_identity_audit (
 audit_id BINARY(16) NOT NULL PRIMARY KEY,
 operator_id BINARY(16) NULL,
 case_id BINARY(16) NULL,
 action_code VARCHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 allowed BOOLEAN NOT NULL,
 created_at_utc DATETIME(6) NOT NULL
) ENGINE=InnoDB;
CREATE TABLE c05_identity_assisted_case (
 tenant_id BINARY(16) NOT NULL,
 case_id BINARY(16) NOT NULL,
 account_id BINARY(16) NOT NULL,
 account_revision BIGINT UNSIGNED NOT NULL,
 protected_anchor VARBINARY(512) NOT NULL,
 created_at_utc DATETIME(6) NOT NULL,
 ready_at_utc DATETIME(6) NULL,
 expires_at_utc DATETIME(6) NOT NULL,
 token_hmac BINARY(32) NULL,
 blocked BOOLEAN NOT NULL DEFAULT FALSE,
 consumed_at_utc DATETIME(6) NULL,
 active_account BINARY(16) GENERATED ALWAYS AS (IF(NOT blocked AND consumed_at_utc IS NULL,account_id,NULL)) STORED,
 PRIMARY KEY(tenant_id,case_id), UNIQUE KEY uq_c05_active_assisted_account(active_account),
 FOREIGN KEY(tenant_id) REFERENCES c04_tenant_catalog(tenant_id),
 FOREIGN KEY(account_id) REFERENCES c05_identity_account(account_id)
) ENGINE=InnoDB;
CREATE TABLE c05_identity_assisted_review (
 tenant_id BINARY(16) NOT NULL, case_id BINARY(16) NOT NULL,
 operator_id BINARY(16) NOT NULL, operator_revision BIGINT UNSIGNED NOT NULL,
 approved BOOLEAN NOT NULL,
 evidence_hash BINARY(32) NOT NULL, reason_hmac BINARY(32) NOT NULL,
 reviewed_at_utc DATETIME(6) NOT NULL,
 PRIMARY KEY(tenant_id,case_id,operator_id),
 FOREIGN KEY(tenant_id,case_id) REFERENCES c05_identity_assisted_case(tenant_id,case_id),
 FOREIGN KEY(operator_id) REFERENCES c05_platform_operator(operator_id)
) ENGINE=InnoDB;
CREATE TABLE c05_identity_mfa_reset_authorization (
 account_id BINARY(16) NOT NULL PRIMARY KEY,
 tenant_id BINARY(16) NOT NULL,
 account_revision BIGINT UNSIGNED NOT NULL,
 expires_at_utc DATETIME(6) NOT NULL,
 consumed_at_utc DATETIME(6) NULL,
 FOREIGN KEY(account_id) REFERENCES c05_identity_account(account_id),
 FOREIGN KEY(tenant_id) REFERENCES c04_tenant_catalog(tenant_id)
) ENGINE=InnoDB;
CREATE TABLE c05_identity_assisted_handoff (
 tenant_id BINARY(16) NOT NULL, case_id BINARY(16) NOT NULL,
 operator_id BINARY(16) NOT NULL, share_hmac BINARY(32) NOT NULL,
 expires_at_utc DATETIME(6) NOT NULL,
 PRIMARY KEY(tenant_id,case_id,operator_id),
 FOREIGN KEY(tenant_id,case_id,operator_id) REFERENCES c05_identity_assisted_review(tenant_id,case_id,operator_id)
) ENGINE=InnoDB;
ALTER TABLE c05_identity_challenge ADD COLUMN reset_required BOOLEAN NOT NULL DEFAULT FALSE;
DELIMITER $$
CREATE TRIGGER c05_platform_audit_no_update BEFORE UPDATE ON c05_platform_identity_audit FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Platform identity audit is append-only'; END$$
CREATE TRIGGER c05_platform_audit_no_delete BEFORE DELETE ON c05_platform_identity_audit FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Platform identity audit is append-only'; END$$
CREATE TRIGGER c05_assisted_review_no_update BEFORE UPDATE ON c05_identity_assisted_review FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Identity reviews are append-only'; END$$
CREATE TRIGGER c05_assisted_review_no_delete BEFORE DELETE ON c05_identity_assisted_review FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Identity reviews are append-only'; END$$
DELIMITER ;
