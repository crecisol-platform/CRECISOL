-- CRECISOL C05 operational identity schema, version 001.
-- This is a product migration, separate from database/tests/fixtures.
-- Apply only to an isolated database owned by CRECISOL; never to a disposable
-- test schema or a tenant-selected DSN.
-- MySQL 8.4 / InnoDB / utf8mb4.

-- Shared platform catalog used by C04/C05. This is the only tenant and
-- verified-host authority; C05 identity tables do not create a second map.
CREATE TABLE c04_tenant_catalog (
    tenant_id BINARY(16) NOT NULL,
    internal_name VARCHAR(120) NOT NULL,
    display_name VARCHAR(120) NOT NULL,
    lifecycle_state TINYINT UNSIGNED NOT NULL,
    address_mode TINYINT UNSIGNED NOT NULL,
    catalog_revision BIGINT UNSIGNED NOT NULL,
    updated_at_utc DATETIME(6) NOT NULL,
    PRIMARY KEY (tenant_id),
    CONSTRAINT ck_c04_catalog_state CHECK (lifecycle_state IN (1,2,3,4,5)),
    CONSTRAINT ck_c04_catalog_address CHECK (address_mode IN (1,2,3)),
    CONSTRAINT ck_c04_catalog_revision CHECK (catalog_revision > 0)
) ENGINE=InnoDB;

CREATE TABLE c04_verified_tenant_host (
    canonical_host VARCHAR(253) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    tenant_id BINARY(16) NOT NULL,
    address_mode TINYINT UNSIGNED NOT NULL,
    verification_state TINYINT UNSIGNED NOT NULL,
    host_revision BIGINT UNSIGNED NOT NULL,
    changed_at_utc DATETIME(6) NOT NULL,
    verified_at_utc DATETIME(6) NULL,
    valid_until_utc DATETIME(6) NULL,
    PRIMARY KEY (canonical_host),
    KEY ix_c04_host_tenant (tenant_id, verification_state),
    CONSTRAINT fk_c04_verified_host_tenant FOREIGN KEY (tenant_id)
      REFERENCES c04_tenant_catalog (tenant_id),
    CONSTRAINT ck_c04_host_address CHECK (address_mode IN (2,3)),
    CONSTRAINT ck_c04_host_state CHECK (verification_state IN (1,2,3,4)),
    CONSTRAINT ck_c04_host_revision CHECK (host_revision > 0),
    CONSTRAINT ck_c04_host_verification_window CHECK
      ((verified_at_utc IS NULL AND valid_until_utc IS NULL) OR
       (verified_at_utc IS NOT NULL AND valid_until_utc > verified_at_utc))
) ENGINE=InnoDB;

CREATE TABLE c05_identity_registration_intent (
    tenant_id BINARY(16) NOT NULL,
    intent_id BINARY(16) NOT NULL,
    mailbox_hmac BINARY(32) NOT NULL,
    peer_hmac BINARY(32) NOT NULL,
    mailbox_ciphertext VARBINARY(512) NOT NULL,
    mailbox_nonce BINARY(12) NOT NULL,
    mailbox_tag BINARY(16) NOT NULL,
    key_version INT UNSIGNED NOT NULL,
    state TINYINT UNSIGNED NOT NULL,
    created_at_utc DATETIME(6) NOT NULL,
    expires_at_utc DATETIME(6) NOT NULL,
    PRIMARY KEY (tenant_id, intent_id),
    KEY ix_c05_intent_mailbox_state (tenant_id, mailbox_hmac, state),
    KEY ix_c05_intent_peer_time (tenant_id, peer_hmac, created_at_utc),
    CONSTRAINT fk_c05_intent_tenant FOREIGN KEY (tenant_id)
      REFERENCES c04_tenant_catalog (tenant_id),
    CONSTRAINT ck_c05_intent_state CHECK (state IN (0,1,2,3)),
    CONSTRAINT ck_c05_intent_expiry CHECK (expires_at_utc > created_at_utc)
) ENGINE=InnoDB;

CREATE TABLE c05_identity_registration_quota (
    tenant_id BINARY(16) NOT NULL,
    peer_hmac BINARY(32) NOT NULL,
    window_start_utc DATETIME(0) NOT NULL,
    admitted_count SMALLINT UNSIGNED NOT NULL,
    PRIMARY KEY (tenant_id, peer_hmac, window_start_utc),
    CONSTRAINT fk_c05_registration_quota_tenant FOREIGN KEY (tenant_id)
      REFERENCES c04_tenant_catalog (tenant_id),
    CONSTRAINT ck_c05_registration_quota CHECK (admitted_count <= 8)
) ENGINE=InnoDB;

CREATE TABLE c05_identity_account (
    account_id BINARY(16) NOT NULL,
    login_lookup_hmac BINARY(32) NOT NULL,
    mailbox_ciphertext VARBINARY(512) NOT NULL,
    mailbox_nonce BINARY(12) NOT NULL,
    mailbox_tag BINARY(16) NOT NULL,
    mailbox_key_version INT UNSIGNED NOT NULL,
    mailbox_verified_at_utc DATETIME(6) NULL,
    password_phc VARCHAR(512) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    account_state TINYINT UNSIGNED NOT NULL,
    security_revision BIGINT UNSIGNED NOT NULL DEFAULT 1,
    created_at_utc DATETIME(6) NOT NULL,
    updated_at_utc DATETIME(6) NOT NULL,
    PRIMARY KEY (account_id),
    UNIQUE KEY uq_c05_identity_login (login_lookup_hmac),
    CONSTRAINT ck_c05_identity_state CHECK (account_state IN (0,1,2,3)),
    CONSTRAINT ck_c05_identity_revision CHECK (security_revision > 0)
) ENGINE=InnoDB;

CREATE TABLE c05_identity_membership (
    tenant_id BINARY(16) NOT NULL,
    membership_id BINARY(16) NOT NULL,
    account_id BINARY(16) NOT NULL,
    membership_state TINYINT UNSIGNED NOT NULL,
    role_code VARCHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    approval_revision BIGINT UNSIGNED NOT NULL DEFAULT 1,
    approved_by BINARY(16) NULL,
    approved_at_utc DATETIME(6) NULL,
    created_at_utc DATETIME(6) NOT NULL,
    PRIMARY KEY (tenant_id, membership_id),
    UNIQUE KEY uq_c05_membership_tenant_account (tenant_id, account_id),
    KEY ix_c05_membership_account_state (account_id, membership_state),
    CONSTRAINT fk_c05_membership_account FOREIGN KEY (account_id)
      REFERENCES c05_identity_account (account_id),
    CONSTRAINT ck_c05_membership_state CHECK (membership_state IN (0,1,2,3)),
    CONSTRAINT ck_c05_membership_revision CHECK (approval_revision > 0)
) ENGINE=InnoDB;

CREATE TABLE c05_identity_mailbox_proof (
    tenant_id BINARY(16) NOT NULL,
    proof_id BINARY(16) NOT NULL,
    mailbox_hmac BINARY(32) NOT NULL,
    proof_token_hmac BINARY(32) NOT NULL,
    mailbox_ciphertext VARBINARY(512) NOT NULL,
    mailbox_nonce BINARY(12) NOT NULL,
    mailbox_tag BINARY(16) NOT NULL,
    key_version INT UNSIGNED NOT NULL,
    proof_state TINYINT UNSIGNED NOT NULL,
    created_at_utc DATETIME(6) NOT NULL,
    expires_at_utc DATETIME(6) NOT NULL,
    consumed_at_utc DATETIME(6) NULL,
    PRIMARY KEY (tenant_id, proof_id),
    UNIQUE KEY uq_c05_proof_token (proof_token_hmac),
    KEY ix_c05_proof_mailbox_state (tenant_id, mailbox_hmac, proof_state),
    CONSTRAINT ck_c05_proof_state CHECK (proof_state IN (0,1,2,3)),
    CONSTRAINT ck_c05_proof_expiry CHECK (expires_at_utc > created_at_utc)
) ENGINE=InnoDB;

CREATE TABLE c05_identity_mfa_factor (
    account_id BINARY(16) NOT NULL,
    factor_id BINARY(16) NOT NULL,
    factor_kind TINYINT UNSIGNED NOT NULL,
    protected_secret VARBINARY(1024) NOT NULL,
    secret_nonce BINARY(12) NOT NULL,
    secret_tag BINARY(16) NOT NULL,
    key_version INT UNSIGNED NOT NULL,
    factor_state TINYINT UNSIGNED NOT NULL,
    factor_revision BIGINT UNSIGNED NOT NULL DEFAULT 1,
    last_accepted_step BIGINT UNSIGNED NULL,
    enrolled_at_utc DATETIME(6) NOT NULL,
    revoked_at_utc DATETIME(6) NULL,
    PRIMARY KEY (account_id, factor_id),
    CONSTRAINT fk_c05_mfa_account FOREIGN KEY (account_id)
      REFERENCES c05_identity_account (account_id),
    CONSTRAINT ck_c05_mfa_kind CHECK (factor_kind IN (1,2)),
    CONSTRAINT ck_c05_mfa_state CHECK (factor_state IN (0,1,2)),
    CONSTRAINT ck_c05_mfa_revision CHECK (factor_revision > 0)
) ENGINE=InnoDB;

CREATE TABLE c05_identity_session (
    tenant_id BINARY(16) NOT NULL,
    session_id BINARY(16) NOT NULL,
    account_id BINARY(16) NOT NULL,
    membership_id BINARY(16) NOT NULL,
    token_hmac BINARY(32) NOT NULL,
    session_family BINARY(16) NOT NULL,
    mfa_factor_id BINARY(16) NOT NULL,
    mfa_verified_at_utc DATETIME(6) NOT NULL,
    session_revision BIGINT UNSIGNED NOT NULL DEFAULT 1,
    issued_at_utc DATETIME(6) NOT NULL,
    expires_at_utc DATETIME(6) NOT NULL,
    revoked_at_utc DATETIME(6) NULL,
    replaced_by BINARY(16) NULL,
    PRIMARY KEY (tenant_id, session_id),
    UNIQUE KEY uq_c05_session_token (token_hmac),
    KEY ix_c05_session_family (tenant_id, session_family, revoked_at_utc),
    CONSTRAINT fk_c05_session_membership FOREIGN KEY (tenant_id, membership_id)
      REFERENCES c05_identity_membership (tenant_id, membership_id),
    CONSTRAINT fk_c05_session_factor FOREIGN KEY (account_id, mfa_factor_id)
      REFERENCES c05_identity_mfa_factor (account_id, factor_id),
    CONSTRAINT ck_c05_session_revision CHECK (session_revision > 0),
    CONSTRAINT ck_c05_session_expiry CHECK (expires_at_utc > issued_at_utc)
) ENGINE=InnoDB;

CREATE TABLE c05_identity_membership_approval (
    tenant_id BINARY(16) NOT NULL,
    membership_id BINARY(16) NOT NULL,
    approver_account_id BINARY(16) NOT NULL,
    approver_session_id BINARY(16) NOT NULL,
    decision TINYINT UNSIGNED NOT NULL,
    reason_hmac BINARY(32) NOT NULL,
    decided_at_utc DATETIME(6) NOT NULL,
    PRIMARY KEY (tenant_id, membership_id),
    KEY ix_c05_approval_actor_time (tenant_id, approver_account_id, decided_at_utc),
    CONSTRAINT fk_c05_approval_membership FOREIGN KEY (tenant_id, membership_id)
      REFERENCES c05_identity_membership (tenant_id, membership_id),
    CONSTRAINT fk_c05_approval_actor FOREIGN KEY (approver_account_id)
      REFERENCES c05_identity_account (account_id),
    CONSTRAINT fk_c05_approval_session FOREIGN KEY (tenant_id, approver_session_id)
      REFERENCES c05_identity_session (tenant_id, session_id),
    CONSTRAINT ck_c05_approval_decision CHECK (decision IN (1,2))
) ENGINE=InnoDB;

CREATE TABLE c05_identity_recovery (
    account_id BINARY(16) NOT NULL,
    recovery_id BINARY(16) NOT NULL,
    token_hmac BINARY(32) NOT NULL,
    created_at_utc DATETIME(6) NOT NULL,
    expires_at_utc DATETIME(6) NOT NULL,
    consumed_at_utc DATETIME(6) NULL,
    PRIMARY KEY (account_id, recovery_id),
    UNIQUE KEY uq_c05_recovery_token (token_hmac),
    CONSTRAINT fk_c05_recovery_account FOREIGN KEY (account_id)
      REFERENCES c05_identity_account (account_id),
    CONSTRAINT ck_c05_recovery_expiry CHECK (expires_at_utc > created_at_utc)
) ENGINE=InnoDB;

CREATE TABLE c05_identity_resource_grant (
    tenant_id BINARY(16) NOT NULL,
    grant_id BINARY(16) NOT NULL,
    account_id BINARY(16) NOT NULL,
    branch_id BINARY(16) NOT NULL,
    resource_id BINARY(16) NULL,
    scope_kind TINYINT UNSIGNED NOT NULL,
    permission_code VARCHAR(96) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    granted_by BINARY(16) NOT NULL,
    granted_at_utc DATETIME(6) NOT NULL,
    expires_at_utc DATETIME(6) NULL,
    revoked_at_utc DATETIME(6) NULL,
    grant_revision BIGINT UNSIGNED NOT NULL DEFAULT 1,
    PRIMARY KEY (tenant_id, grant_id),
    KEY ix_c05_grant_policy (tenant_id, account_id, branch_id, permission_code, revoked_at_utc),
    CONSTRAINT fk_c05_grant_account FOREIGN KEY (account_id)
      REFERENCES c05_identity_account (account_id),
    CONSTRAINT fk_c05_grant_grantor FOREIGN KEY (granted_by)
      REFERENCES c05_identity_account (account_id),
    CONSTRAINT ck_c05_grant_scope CHECK (scope_kind IN (1,2)),
    CONSTRAINT ck_c05_grant_expiry CHECK (expires_at_utc IS NULL OR expires_at_utc > granted_at_utc),
    CONSTRAINT ck_c05_grant_revision CHECK (grant_revision > 0)
) ENGINE=InnoDB;

CREATE TABLE c05_identity_audit (
    tenant_id BINARY(16) NOT NULL,
    audit_id BINARY(16) NOT NULL,
    actor_account_id BINARY(16) NULL,
    subject_account_id BINARY(16) NULL,
    action_code VARCHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    outcome_code TINYINT UNSIGNED NOT NULL,
    context_hmac BINARY(32) NOT NULL,
    created_at_utc DATETIME(6) NOT NULL,
    PRIMARY KEY (tenant_id, audit_id),
    KEY ix_c05_audit_actor_time (tenant_id, actor_account_id, created_at_utc),
    KEY ix_c05_audit_subject_time (tenant_id, subject_account_id, created_at_utc)
) ENGINE=InnoDB;

DELIMITER $$
CREATE TRIGGER c05_identity_audit_no_update
BEFORE UPDATE ON c05_identity_audit FOR EACH ROW
BEGIN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'C05 audit rows are append-only';
END$$
CREATE TRIGGER c05_identity_audit_no_delete
BEFORE DELETE ON c05_identity_audit FOR EACH ROW
BEGIN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'C05 audit rows are append-only';
END$$
DELIMITER ;



