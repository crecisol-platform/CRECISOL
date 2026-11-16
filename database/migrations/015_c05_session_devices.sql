ALTER TABLE c05_identity_session
 ADD COLUMN device_label VARCHAR(24) CHARACTER SET ascii COLLATE ascii_bin NOT NULL DEFAULT 'API',
 ADD COLUMN auth_method VARCHAR(12) CHARACTER SET ascii COLLATE ascii_bin NOT NULL DEFAULT 'LEGACY',
 ADD CONSTRAINT ck_c05_session_auth_method CHECK(auth_method IN ('LEGACY','TOTP','BACKUP','WEBAUTHN','PAIRING')),
 ADD COLUMN peer_hmac BINARY(32) NULL;
ALTER TABLE c05_platform_session
 ADD COLUMN session_id BINARY(16) NULL,
 ADD COLUMN created_at_utc DATETIME(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
 ADD COLUMN device_label VARCHAR(24) CHARACTER SET ascii COLLATE ascii_bin NOT NULL DEFAULT 'API',
 ADD COLUMN auth_method VARCHAR(12) CHARACTER SET ascii COLLATE ascii_bin NOT NULL DEFAULT 'LEGACY',
 ADD CONSTRAINT ck_c05_platform_auth_method CHECK(auth_method IN ('LEGACY','TOTP','BACKUP')),
 ADD COLUMN peer_hmac BINARY(32) NULL;
UPDATE c05_platform_session SET session_id=UUID_TO_BIN(UUID(),0),created_at_utc=mfa_at_utc;
ALTER TABLE c05_platform_session MODIFY COLUMN session_id BINARY(16) NOT NULL,
 ADD UNIQUE KEY uq_c05_platform_session_reference(session_id);
