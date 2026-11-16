-- Explicitly versioned contact envelope for identity security notifications.
-- Existing registration envelopes retain their original AAD and are untouched.
CREATE TABLE c05_identity_contact (
 account_id BINARY(16) NOT NULL PRIMARY KEY,
 protected_mailbox VARBINARY(512) NOT NULL,
 previous_mailbox VARBINARY(512) NULL,
 changed_at_utc DATETIME(6) NOT NULL,
 FOREIGN KEY(account_id) REFERENCES c05_identity_account(account_id)
) ENGINE=InnoDB;
CREATE TABLE c05_identity_incident (
 tenant_id BINARY(16) NOT NULL,
 token_hmac BINARY(32) NOT NULL,
 account_id BINARY(16) NOT NULL,
 event_code VARCHAR(40) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 created_at_utc DATETIME(6) NOT NULL,
 expires_at_utc DATETIME(6) NOT NULL,
 reported_at_utc DATETIME(6) NULL,
 PRIMARY KEY(tenant_id,token_hmac),
 FOREIGN KEY(tenant_id) REFERENCES c04_tenant_catalog(tenant_id),
 FOREIGN KEY(account_id) REFERENCES c05_identity_account(account_id)
) ENGINE=InnoDB;
ALTER TABLE c05_identity_account ADD COLUMN mailbox_crypto_scheme VARCHAR(24)
 CHARACTER SET ascii COLLATE ascii_bin NOT NULL DEFAULT 'REGISTRATION_V1',
 ADD CONSTRAINT ck_c05_mailbox_scheme CHECK(mailbox_crypto_scheme IN ('REGISTRATION_V1','OPERATIONAL_V1'));
-- In 003, creation and initial mailbox verification use the same p_created.
-- The only 006 transition advancing mailbox_verified_at_utc is the operational
-- mailbox-change envelope. Preserve that already-established scheme explicitly.
UPDATE c05_identity_account SET mailbox_crypto_scheme='OPERATIONAL_V1'
 WHERE mailbox_verified_at_utc>created_at_utc;
