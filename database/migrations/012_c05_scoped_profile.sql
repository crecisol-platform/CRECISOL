-- Identity-only profile foundation; no general media/CMS subsystem or public
-- avatar URL. Both the owner and the verified tenant are mandatory keys.
CREATE TABLE c05_identity_scoped_profile (
 tenant_id BINARY(16) NOT NULL,
 account_id BINARY(16) NOT NULL,
 protected_profile VARBINARY(2048) NOT NULL,
 photo_png VARBINARY(24576) NULL,
 photo_state TINYINT UNSIGNED NOT NULL DEFAULT 0,
 updated_at_utc DATETIME(6) NOT NULL,
 PRIMARY KEY(tenant_id,account_id),
 FOREIGN KEY(tenant_id,account_id) REFERENCES c05_identity_membership(tenant_id,account_id),
 CHECK(photo_state IN (0,1,2)), CHECK((photo_state=2 AND photo_png IS NOT NULL) OR (photo_state<>2 AND photo_png IS NULL))
) ENGINE=InnoDB;
