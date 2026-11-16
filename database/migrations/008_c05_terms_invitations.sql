-- Terms are published by the migration/operator identity, never arbitrary HTML
-- or code from a browser. Acceptance records are append-only for the API role.
CREATE TABLE c05_identity_terms (
 tenant_id BINARY(16) NOT NULL,
 terms_version VARCHAR(40) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 content_text TEXT NOT NULL,
 content_hash BINARY(32) NOT NULL,
 published_at_utc DATETIME(6) NOT NULL,
 active BOOLEAN NOT NULL,
 active_tenant BINARY(16) GENERATED ALWAYS AS (IF(active,tenant_id,NULL)) STORED,
 PRIMARY KEY(tenant_id,terms_version), UNIQUE KEY uq_c05_current_terms(active_tenant),
 FOREIGN KEY(tenant_id) REFERENCES c04_tenant_catalog(tenant_id)
) ENGINE=InnoDB;
CREATE TABLE c05_identity_terms_acceptance (
 tenant_id BINARY(16) NOT NULL,
 account_id BINARY(16) NOT NULL,
 terms_version VARCHAR(40) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 content_hash BINARY(32) NOT NULL,
 accepted_at_utc DATETIME(6) NOT NULL,
 PRIMARY KEY(tenant_id,account_id,terms_version),
 FOREIGN KEY(tenant_id,terms_version) REFERENCES c05_identity_terms(tenant_id,terms_version),
 FOREIGN KEY(account_id) REFERENCES c05_identity_account(account_id)
) ENGINE=InnoDB;
CREATE TABLE c05_identity_invitation (
 tenant_id BINARY(16) NOT NULL,
 token_hmac BINARY(32) NOT NULL,
 target_login_hmac BINARY(32) NOT NULL,
 inviter_account_id BINARY(16) NOT NULL,
 inviter_membership_revision BIGINT UNSIGNED NOT NULL,
 expires_at_utc DATETIME(6) NOT NULL,
 consumed_at_utc DATETIME(6) NULL,
 PRIMARY KEY(tenant_id,token_hmac),
 FOREIGN KEY(tenant_id) REFERENCES c04_tenant_catalog(tenant_id),
 FOREIGN KEY(inviter_account_id) REFERENCES c05_identity_account(account_id)
) ENGINE=InnoDB;
