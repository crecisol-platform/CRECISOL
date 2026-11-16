-- Apply after 006 with the API stopped. RP and origin are server configuration.
CREATE TABLE c05_identity_passkey (
 credential_id VARBINARY(1024) NOT NULL PRIMARY KEY,
 account_id BINARY(16) NOT NULL,
 factor_id BINARY(16) NOT NULL,
 public_key VARBINARY(2048) NOT NULL,
 signature_count BIGINT UNSIGNED NOT NULL,
 created_at_utc DATETIME(6) NOT NULL,
 UNIQUE KEY uq_c05_passkey_factor(account_id,factor_id),
 FOREIGN KEY(account_id,factor_id) REFERENCES c05_identity_mfa_factor(account_id,factor_id),
 CHECK(signature_count<=4294967295)
) ENGINE=InnoDB;

CREATE TABLE c05_identity_passkey_ceremony (
 tenant_id BINARY(16) NOT NULL,
 token_hmac BINARY(32) NOT NULL,
 ceremony_kind VARCHAR(12) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 account_id BINARY(16) NULL,
 session_id BINARY(16) NULL,
 account_revision BIGINT UNSIGNED NULL,
 options_json TEXT NOT NULL,
 expires_at_utc DATETIME(6) NOT NULL,
 consumed_at_utc DATETIME(6) NULL,
 PRIMARY KEY(tenant_id,token_hmac),
 FOREIGN KEY(tenant_id) REFERENCES c04_tenant_catalog(tenant_id),
 FOREIGN KEY(account_id) REFERENCES c05_identity_account(account_id),
 CHECK(ceremony_kind IN ('REGISTER','LOGIN')),
 CHECK((ceremony_kind='REGISTER' AND account_id IS NOT NULL AND session_id IS NOT NULL AND account_revision IS NOT NULL)
 OR (ceremony_kind='LOGIN' AND account_id IS NULL AND session_id IS NULL AND account_revision IS NULL))
) ENGINE=InnoDB;
