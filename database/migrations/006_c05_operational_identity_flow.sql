-- Operational C05 flow. Apply after 001..005, with the API stopped.
ALTER TABLE c05_identity_mfa_factor
 ADD COLUMN active_totp_account BINARY(16) GENERATED ALWAYS AS
 (IF(factor_state=1 AND factor_kind=1,account_id,NULL)) STORED,
 ADD UNIQUE KEY uq_c05_active_totp(active_totp_account);

ALTER TABLE c05_identity_session
 ADD COLUMN account_revision BIGINT UNSIGNED NOT NULL DEFAULT 1,
 ADD COLUMN membership_revision BIGINT UNSIGNED NOT NULL DEFAULT 1,
 ADD COLUMN last_seen_at_utc DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6);

CREATE TABLE c05_identity_challenge (
 tenant_id BINARY(16) NOT NULL,
 token_hmac BINARY(32) NOT NULL,
 account_id BINARY(16) NOT NULL,
 membership_id BINARY(16) NOT NULL,
 account_revision BIGINT UNSIGNED NOT NULL,
 membership_revision BIGINT UNSIGNED NOT NULL,
 factor_id BINARY(16) NULL,
 replace_factor_id BINARY(16) NULL,
 attempts TINYINT UNSIGNED NOT NULL DEFAULT 0,
 expires_at_utc DATETIME(6) NOT NULL,
 consumed_at_utc DATETIME(6) NULL,
 PRIMARY KEY (tenant_id,token_hmac),
 FOREIGN KEY (tenant_id,membership_id) REFERENCES c05_identity_membership(tenant_id,membership_id),
 FOREIGN KEY (account_id) REFERENCES c05_identity_account(account_id),
 CHECK (attempts<=5)
) ENGINE=InnoDB;

CREATE TABLE c05_identity_attempt_quota (
 tenant_id BINARY(16) NOT NULL,
 bucket_hmac BINARY(32) NOT NULL,
 window_utc BIGINT NOT NULL,
 attempts SMALLINT UNSIGNED NOT NULL,
 PRIMARY KEY(tenant_id,bucket_hmac,window_utc)
) ENGINE=InnoDB;

CREATE TABLE c05_identity_mfa_backup (
 account_id BINARY(16) NOT NULL,
 token_hmac BINARY(32) NOT NULL,
 consumed_at_utc DATETIME(6) NULL,
 PRIMARY KEY(account_id,token_hmac),
 FOREIGN KEY(account_id) REFERENCES c05_identity_account(account_id)
) ENGINE=InnoDB;

-- Evolve the original recovery ledger instead of creating a competing store.
-- No operational recovery rows existed before this version. A legacy row
-- without a trustworthy tenant cannot be guessed/migrated into an authority.
ALTER TABLE c05_identity_recovery
 ADD COLUMN tenant_id BINARY(16) NOT NULL,
 ADD COLUMN purpose VARCHAR(24) CHARACTER SET ascii COLLATE ascii_bin NOT NULL DEFAULT 'RECOVERY',
 ADD COLUMN account_revision BIGINT UNSIGNED NOT NULL DEFAULT 1,
 ADD COLUMN protected_payload VARBINARY(2048) NOT NULL,
 ADD KEY ix_c05_recovery_tenant(tenant_id,token_hmac),
 ADD CONSTRAINT fk_c05_recovery_tenant FOREIGN KEY(tenant_id) REFERENCES c04_tenant_catalog(tenant_id),
 ADD CONSTRAINT ck_c05_recovery_purpose CHECK(purpose IN ('RECOVERY','MAILBOX'));

-- Local delivery follows the same durable outbox contract; only a loopback
-- dispatcher in the isolated composition decrypts it. No public read route.
CREATE TABLE c05_identity_sensitive_outbox (
 message_id BINARY(16) NOT NULL PRIMARY KEY,
 protected_payload VARBINARY(4096) NOT NULL,
 expires_at_utc DATETIME(6) NOT NULL,
 delivered_at_utc DATETIME(6) NULL
) ENGINE=InnoDB;

CREATE TABLE c05_identity_branch (
 tenant_id BINARY(16) NOT NULL,
 branch_id BINARY(16) NOT NULL,
 active BOOLEAN NOT NULL,
 PRIMARY KEY(tenant_id,branch_id),
 FOREIGN KEY(tenant_id) REFERENCES c04_tenant_catalog(tenant_id)
) ENGINE=InnoDB;

CREATE TABLE c05_identity_private_resource (
 tenant_id BINARY(16) NOT NULL,
 branch_id BINARY(16) NOT NULL,
 resource_id BINARY(16) NOT NULL,
 owner_account_id BINARY(16) NOT NULL,
 resource_state TINYINT UNSIGNED NOT NULL,
 display_reference VARCHAR(120) NOT NULL,
 PRIMARY KEY(tenant_id,branch_id,resource_id),
 FOREIGN KEY(tenant_id,branch_id) REFERENCES c05_identity_branch(tenant_id,branch_id),
 FOREIGN KEY(tenant_id,owner_account_id) REFERENCES c05_identity_membership(tenant_id,account_id),
 CHECK(resource_state IN(0,1))
) ENGINE=InnoDB;
