CREATE TABLE c05_identity_pairing (
 tenant_id BINARY(16) NOT NULL,
 token_hmac BINARY(32) NOT NULL,
 public_key VARBINARY(256) NOT NULL,
 nonce BINARY(32) NOT NULL,
 expires_at_utc DATETIME(6) NOT NULL,
 account_id BINARY(16) NULL,
 membership_id BINARY(16) NULL,
 account_revision BIGINT UNSIGNED NULL,
 membership_revision BIGINT UNSIGNED NULL,
 factor_id BINARY(16) NULL,
 approved_at_utc DATETIME(6) NULL,
 consumed_at_utc DATETIME(6) NULL,
 PRIMARY KEY(tenant_id,token_hmac),
 FOREIGN KEY(tenant_id) REFERENCES c04_tenant_catalog(tenant_id),
 FOREIGN KEY(account_id,factor_id) REFERENCES c05_identity_mfa_factor(account_id,factor_id),
 FOREIGN KEY(tenant_id,membership_id) REFERENCES c05_identity_membership(tenant_id,membership_id)
) ENGINE=InnoDB;
