CREATE TABLE c05_identity_abuse_signal (
 tenant_id BINARY(16) NOT NULL,
 bucket_hmac BINARY(32) NOT NULL,
 window_utc BIGINT NOT NULL,
 signal_kind VARCHAR(12) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 created_at_utc DATETIME(6) NOT NULL,
 PRIMARY KEY(tenant_id,bucket_hmac,window_utc),
 FOREIGN KEY(tenant_id) REFERENCES c04_tenant_catalog(tenant_id),
 CHECK(signal_kind IN ('PEER','TENANT','ROUTE','ACCOUNT'))
) ENGINE=InnoDB;
-- Operators also own a profile, but no tenant identity can address this realm.
CREATE TABLE c05_platform_scoped_profile (
 operator_id BINARY(16) NOT NULL PRIMARY KEY,
 protected_profile VARBINARY(2048) NOT NULL,
 photo_png VARBINARY(24576) NULL,
 photo_state TINYINT UNSIGNED NOT NULL DEFAULT 0,
 updated_at_utc DATETIME(6) NOT NULL,
 FOREIGN KEY(operator_id) REFERENCES c05_platform_operator(operator_id),
 CHECK(photo_state IN (0,1,2)), CHECK((photo_state=2 AND photo_png IS NOT NULL) OR (photo_state<>2 AND photo_png IS NULL))
) ENGINE=InnoDB;
