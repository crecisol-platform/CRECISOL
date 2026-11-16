ALTER TABLE c05_platform_session DROP INDEX uq_c05_platform_session_reference,DROP CHECK ck_c05_platform_auth_method,
 DROP COLUMN session_id,DROP COLUMN created_at_utc,DROP COLUMN device_label,DROP COLUMN auth_method,DROP COLUMN peer_hmac;
ALTER TABLE c05_identity_session DROP CHECK ck_c05_session_auth_method,DROP COLUMN device_label,DROP COLUMN auth_method,DROP COLUMN peer_hmac;
