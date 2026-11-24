-- C05-017: ONE-WAY SECURITY CUTOVER. Apply with C05 API stopped, only after
-- verified backup/restore of the isolated database and encryption keys.
-- Historical 001-016 migrations remain untouched. NEVER use on C01/C02.
-- The policy first enters BLOCKED (0): interrupted scripts fail closed.
CREATE TABLE IF NOT EXISTS c05_totp_sha1_policy (
    policy_id TINYINT UNSIGNED NOT NULL PRIMARY KEY,
    status TINYINT UNSIGNED NOT NULL,
    changed_at_utc DATETIME(6) NOT NULL,
    CONSTRAINT ck_c05_totp_sha1_policy CHECK (policy_id=1 AND status IN(0,1))
) ENGINE=InnoDB;
-- A completed status=1 cannot be applied twice: fail instead of retiring
-- newly enrolled SHA1 factors. A status=0 interrupted attempt can resume.
INSERT INTO c05_totp_sha1_policy(policy_id,status,changed_at_utc)
VALUES(1,0,UTC_TIMESTAMP(6))
ON DUPLICATE KEY UPDATE status=IF(status=0,0,2);

UPDATE c05_identity_account a
SET a.security_revision=a.security_revision+1,a.updated_at_utc=UTC_TIMESTAMP(6)
WHERE EXISTS (
 SELECT 1 FROM c05_identity_mfa_factor f WHERE f.account_id=a.account_id
 AND f.factor_kind=1 AND f.factor_state<>2
);

UPDATE c05_identity_session SET revoked_at_utc=UTC_TIMESTAMP(6) WHERE revoked_at_utc IS NULL;
UPDATE c05_identity_challenge SET consumed_at_utc=UTC_TIMESTAMP(6) WHERE consumed_at_utc IS NULL;
UPDATE c05_identity_mfa_backup SET consumed_at_utc=UTC_TIMESTAMP(6) WHERE consumed_at_utc IS NULL;
UPDATE c05_identity_mfa_reset_authorization SET consumed_at_utc=UTC_TIMESTAMP(6) WHERE consumed_at_utc IS NULL;
UPDATE c05_identity_recovery SET consumed_at_utc=UTC_TIMESTAMP(6) WHERE consumed_at_utc IS NULL;
UPDATE c05_identity_pairing SET consumed_at_utc=UTC_TIMESTAMP(6) WHERE consumed_at_utc IS NULL;
UPDATE c05_identity_assisted_case SET blocked=1 WHERE consumed_at_utc IS NULL AND blocked=0;

-- Cryptoshred ALL old TOTP seeds (active, pending, previously revoked).
-- WebAuthn public-key factors, passwords, tenants and immutable audit stay.
UPDATE c05_identity_mfa_factor
SET factor_state=2,revoked_at_utc=COALESCE(revoked_at_utc,UTC_TIMESTAMP(6)),
 factor_revision=factor_revision+1,protected_secret=X'',
 secret_nonce=UNHEX(REPEAT('00',12)),secret_tag=UNHEX(REPEAT('00',16)),
 last_accepted_step=NULL
WHERE factor_kind=1 AND (factor_state<>2 OR OCTET_LENGTH(protected_secret)>0
 OR last_accepted_step IS NOT NULL);

-- Every existing operator needs independent OFFLINE synthetic re-provisioning
-- before any platform identity/recovery operation may resume.
UPDATE c05_platform_session SET revoked_at_utc=UTC_TIMESTAMP(6) WHERE revoked_at_utc IS NULL;
UPDATE c05_platform_challenge SET consumed_at_utc=UTC_TIMESTAMP(6) WHERE consumed_at_utc IS NULL;
UPDATE c05_platform_mfa_backup SET consumed_at_utc=UTC_TIMESTAMP(6) WHERE consumed_at_utc IS NULL;
UPDATE c05_platform_factor_change SET consumed_at_utc=UTC_TIMESTAMP(6),protected_seed=X''
 WHERE consumed_at_utc IS NULL OR OCTET_LENGTH(protected_seed)>0;
UPDATE c05_platform_mailbox_change SET consumed_at_utc=UTC_TIMESTAMP(6) WHERE consumed_at_utc IS NULL;
UPDATE c05_platform_recovery_case
 SET blocked=1,pending_seed=NULL,pending_password_phc=NULL,enrollment_hmac=NULL
 WHERE consumed_at_utc IS NULL;
UPDATE c05_platform_operator SET security_revision=security_revision+1,
 recovery_pending=1,protected_seed=X'',last_totp_step=NULL
 WHERE recovery_pending=0 OR OCTET_LENGTH(protected_seed)>0;

-- Only activate SHA1 once all legacy credentials are unusable.
-- No password-only reset authorizations are minted by this migration.
UPDATE c05_totp_sha1_policy SET status=1,changed_at_utc=UTC_TIMESTAMP(6)
WHERE policy_id=1
 AND NOT EXISTS(SELECT 1 FROM c05_identity_mfa_factor
  WHERE factor_kind=1 AND (factor_state<>2 OR OCTET_LENGTH(protected_secret)>0))
 AND NOT EXISTS(SELECT 1 FROM c05_platform_operator
  WHERE recovery_pending=0 OR OCTET_LENGTH(protected_seed)>0)
 AND NOT EXISTS(SELECT 1 FROM c05_identity_session WHERE revoked_at_utc IS NULL)
 AND NOT EXISTS(SELECT 1 FROM c05_platform_session WHERE revoked_at_utc IS NULL);
