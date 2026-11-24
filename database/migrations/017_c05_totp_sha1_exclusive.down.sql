-- C05-017 is IRREVERSIBLE for historical TOTP-SHA256 material.
-- DOWN does not restore any seed, backup code, session or authorization.
-- Legacy restoration requires a consistent pre-cutover database+key backup.
-- For disposable full-schema rollback tests ONLY; never active local QA.
DROP TABLE IF EXISTS c05_totp_sha1_policy;
