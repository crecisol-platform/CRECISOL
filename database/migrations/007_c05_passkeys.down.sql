-- Stop API first. Removing this schema disables passkey login; do not silently
-- turn existing passkey factors into password-only credentials.
DROP TABLE c05_identity_passkey_ceremony;
DROP TABLE c05_identity_passkey;
