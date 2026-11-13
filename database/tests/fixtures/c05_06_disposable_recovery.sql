-- CRECISOL C05-06: TEST-ONLY extension of C05-05 disposable MySQL8.4
-- schema. Prerequisite: c05_05_disposable_sessions.sql. NEVER apply to a
-- customer or production database. No plaintext email, token or password.
CREATE TABLE c05_06_credentials (
  actor_id BINARY(16) NOT NULL PRIMARY KEY,
  verifier_phc VARCHAR(160) NOT NULL,
  credential_revision BIGINT UNSIGNED NOT NULL,
  credential_active TINYINT(1) NOT NULL,
  realm VARCHAR(12) NOT NULL,
  CONSTRAINT ck_c05_06_credential_revision CHECK (credential_revision > 0),
  CONSTRAINT ck_c05_06_credential_realm CHECK (realm IN ('TENANT','PLATFORM'))
) ENGINE=InnoDB;

CREATE TABLE c05_06_mailboxes (
  tenant_id BINARY(16) NOT NULL,
  actor_id BINARY(16) NOT NULL,
  mailbox_digest BINARY(32) NOT NULL,
  mailbox_revision BIGINT UNSIGNED NOT NULL,
  verified TINYINT(1) NOT NULL,
  PRIMARY KEY (tenant_id,actor_id),
  UNIQUE KEY uq_c05_06_mailbox (tenant_id,mailbox_digest),
  CONSTRAINT fk_c05_06_mailbox_subject
    FOREIGN KEY (tenant_id,actor_id)
      REFERENCES c05_05_subjects (tenant_id,actor_id)
      ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT ck_c05_06_mailbox_revision CHECK (mailbox_revision > 0)
) ENGINE=InnoDB;

CREATE TABLE c05_06_challenges (
  challenge_id BINARY(16) NOT NULL PRIMARY KEY,
  tenant_id BINARY(16) NOT NULL,
  actor_id BINARY(16) NOT NULL,
  token_digest BINARY(32) NOT NULL,
  mailbox_digest BINARY(32) NOT NULL,
  mailbox_revision BIGINT UNSIGNED NOT NULL,
  account_revision BIGINT UNSIGNED NOT NULL,
  credential_revision BIGINT UNSIGNED NOT NULL,
  membership_revision BIGINT UNSIGNED NOT NULL,
  tenant_revision BIGINT UNSIGNED NOT NULL,
  host_revision BIGINT UNSIGNED NOT NULL,
  subject_revision BIGINT UNSIGNED NOT NULL,
  issued_utc DATETIME(6) NOT NULL,
  expires_utc DATETIME(6) NOT NULL,
  state VARCHAR(10) NOT NULL,
  consumed_utc DATETIME(6) NULL,
  UNIQUE KEY uq_c05_06_token_digest (token_digest),
  KEY ix_c05_06_actor_state (actor_id,state),
  CONSTRAINT fk_c05_06_challenge_subject
    FOREIGN KEY (tenant_id,actor_id)
      REFERENCES c05_05_subjects (tenant_id,actor_id)
      ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT ck_c05_06_challenge_state
    CHECK (state IN ('PENDING','CONSUMED','REVOKED')),
  CONSTRAINT ck_c05_06_challenge_expiry
    CHECK (expires_utc > issued_utc)
) ENGINE=InnoDB;

DELIMITER $

-- Security revisions MUST advance on protected state changes even when a
-- fixture operator mistakenly supplies the previous revision. Off+on is not
-- equivalent to uninterrupted verified ownership.
CREATE TRIGGER c05_06_mailbox_revision_bu BEFORE UPDATE ON c05_06_mailboxes
FOR EACH ROW
BEGIN
  IF NEW.verified<>OLD.verified OR NEW.mailbox_digest<>OLD.mailbox_digest THEN
    IF NEW.mailbox_revision<=OLD.mailbox_revision THEN
      SET NEW.mailbox_revision=OLD.mailbox_revision+1;
    END IF;
  END IF;
END$

CREATE TRIGGER c05_06_subject_revision_bu BEFORE UPDATE ON c05_05_subjects
FOR EACH ROW
BEGIN
  IF NEW.account_active<>OLD.account_active OR
     NEW.credential_active<>OLD.credential_active OR
     NEW.membership_active<>OLD.membership_active OR
     NEW.host_active<>OLD.host_active OR
     NEW.tenant_active<>OLD.tenant_active OR
     NEW.member_expires_utc<>OLD.member_expires_utc OR
     NEW.account_revision<>OLD.account_revision OR
     NEW.credential_revision<>OLD.credential_revision OR
     NEW.membership_revision<>OLD.membership_revision OR
     NEW.host_revision<>OLD.host_revision OR
     NEW.tenant_revision<>OLD.tenant_revision THEN
    IF NEW.subject_revision<=OLD.subject_revision THEN
      SET NEW.subject_revision=OLD.subject_revision+1;
    END IF;
  END IF;
END$

CREATE TRIGGER c05_06_credential_revision_bu BEFORE UPDATE ON c05_06_credentials
FOR EACH ROW
BEGIN
  IF NEW.credential_active<>OLD.credential_active OR
     NEW.realm<>OLD.realm OR NEW.verifier_phc<>OLD.verifier_phc THEN
    IF NEW.credential_revision<=OLD.credential_revision THEN
      SET NEW.credential_revision=OLD.credential_revision+1;
    END IF;
  END IF;
END$

-- Host/tenant is resolved by the backend; mailbox is a separately keyed HMAC
-- of the canonical registered email. No caller-supplied actor/membership or
-- tenant routing is accepted. All unrecognized/disabled cases return DENIED.
CREATE PROCEDURE c05_06_issue_once (
  IN p_tenant BINARY(16), IN p_mailbox_digest BINARY(32),
  IN p_challenge BINARY(16), IN p_token_digest BINARY(32)
)
SQL SECURITY DEFINER
BEGIN
  DECLARE v_missing BOOLEAN DEFAULT FALSE;
  DECLARE v_actor BINARY(16);
  DECLARE v_verified TINYINT;
  DECLARE v_mail_revision BIGINT UNSIGNED;
  DECLARE v_account_rev BIGINT UNSIGNED;
  DECLARE v_credential_rev BIGINT UNSIGNED;
  DECLARE v_member_rev BIGINT UNSIGNED;
  DECLARE v_tenant_rev BIGINT UNSIGNED;
  DECLARE v_host_rev BIGINT UNSIGNED;
  DECLARE v_subject_rev BIGINT UNSIGNED;
  DECLARE v_account_active TINYINT;
  DECLARE v_credential_active TINYINT;
  DECLARE v_membership_active TINYINT;
  DECLARE v_host_active TINYINT;
  DECLARE v_tenant_active TINYINT;
  DECLARE v_until DATETIME(6);
  DECLARE v_global_cred_rev BIGINT UNSIGNED;
  DECLARE v_global_active TINYINT;
  DECLARE v_realm VARCHAR(12);
  DECLARE v_now DATETIME(6);
  DECLARE v_exp DATETIME(6);
  DECLARE v_result VARCHAR(8) DEFAULT 'DENIED';
  DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_missing=TRUE;
  DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;

  START TRANSACTION;
  SET v_now = UTC_TIMESTAMP(6);
  SELECT actor_id,verified,mailbox_revision
    INTO v_actor,v_verified,v_mail_revision
    FROM c05_06_mailboxes
   WHERE tenant_id=p_tenant AND mailbox_digest=p_mailbox_digest
   FOR UPDATE;

  IF NOT v_missing AND v_verified=1 THEN
    SELECT account_revision,credential_revision,membership_revision,
           tenant_revision,host_revision,subject_revision,account_active,
           credential_active,membership_active,host_active,tenant_active,
           member_expires_utc
      INTO v_account_rev,v_credential_rev,v_member_rev,
           v_tenant_rev,v_host_rev,v_subject_rev,v_account_active,
           v_credential_active,v_membership_active,v_host_active,
           v_tenant_active,v_until
      FROM c05_05_subjects
     WHERE tenant_id=p_tenant AND actor_id=v_actor FOR UPDATE;

    IF NOT v_missing AND v_account_active=1 AND v_credential_active=1
       AND v_membership_active=1 AND v_host_active=1 AND v_tenant_active=1
       AND v_until>v_now THEN
      SELECT credential_revision,credential_active,realm
        INTO v_global_cred_rev,v_global_active,v_realm
        FROM c05_06_credentials
       WHERE actor_id=v_actor FOR UPDATE;

      IF NOT v_missing AND v_realm='TENANT' AND v_global_active=1
         AND v_global_cred_rev=v_credential_rev THEN
        SET v_exp=LEAST(DATE_ADD(v_now,INTERVAL 10 MINUTE),v_until);
        IF v_exp>v_now THEN
          UPDATE c05_06_challenges SET state='REVOKED'
           WHERE actor_id=v_actor AND state='PENDING';
          INSERT INTO c05_06_challenges (
            challenge_id,tenant_id,actor_id,token_digest,mailbox_digest,
            mailbox_revision,account_revision,credential_revision,
            membership_revision,tenant_revision,host_revision,
            subject_revision,issued_utc,expires_utc,state
          ) VALUES (
            p_challenge,p_tenant,v_actor,p_token_digest,p_mailbox_digest,
            v_mail_revision,v_account_rev,v_credential_rev,
            v_member_rev,v_tenant_rev,v_host_rev,v_subject_rev,
            v_now,v_exp,'PENDING'
          );
          SET v_result='ISSUED';
        END IF;
      END IF;
    END IF;
  END IF;
  COMMIT;
  SELECT v_result AS result;
END$$

-- Atomic, exactly-once consumption. No password recovery can reset an MFA
-- factor, a platform-operator credential, or authorize a new session.
-- A globally shared actor's previous sessions and remaining challenges are
-- revoked across ALL tenants in the same transaction as Argon2id PHC update.
CREATE PROCEDURE c05_06_consume_once (
  IN p_tenant BINARY(16),IN p_token_digest BINARY(32),
  IN p_new_verifier VARCHAR(160)
)
SQL SECURITY DEFINER
BEGIN
  DECLARE v_missing BOOLEAN DEFAULT FALSE;
  DECLARE v_challenge BINARY(16);
  DECLARE v_actor BINARY(16);
  DECLARE v_mailbox BINARY(32);
  DECLARE v_mailbox_rev BIGINT UNSIGNED;
  DECLARE v_account_rev BIGINT UNSIGNED;
  DECLARE v_credential_rev BIGINT UNSIGNED;
  DECLARE v_member_rev BIGINT UNSIGNED;
  DECLARE v_tenant_rev BIGINT UNSIGNED;
  DECLARE v_host_rev BIGINT UNSIGNED;
  DECLARE v_subject_rev BIGINT UNSIGNED;
  DECLARE v_expires DATETIME(6);
  DECLARE v_state VARCHAR(10);
  DECLARE v_real_mailbox BINARY(32);
  DECLARE v_real_mail_rev BIGINT UNSIGNED;
  DECLARE v_verified TINYINT;
  DECLARE v_a BIGINT UNSIGNED;
  DECLARE v_c BIGINT UNSIGNED;
  DECLARE v_m BIGINT UNSIGNED;
  DECLARE v_t BIGINT UNSIGNED;
  DECLARE v_h BIGINT UNSIGNED;
  DECLARE v_s BIGINT UNSIGNED;
  DECLARE v_account TINYINT;
  DECLARE v_credential TINYINT;
  DECLARE v_membership TINYINT;
  DECLARE v_host TINYINT;
  DECLARE v_tenant TINYINT;
  DECLARE v_until DATETIME(6);
  DECLARE v_global_cred_rev BIGINT UNSIGNED;
  DECLARE v_global_active TINYINT;
  DECLARE v_realm VARCHAR(12);
  DECLARE v_now DATETIME(6);
  DECLARE v_result VARCHAR(8) DEFAULT 'DENIED';
  DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_missing=TRUE;
  DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;

  START TRANSACTION;
  SET v_now=UTC_TIMESTAMP(6);
  SELECT challenge_id,actor_id,mailbox_digest,mailbox_revision,
         account_revision,credential_revision,membership_revision,
         tenant_revision,host_revision,subject_revision,expires_utc,state
    INTO v_challenge,v_actor,v_mailbox,v_mailbox_rev,
         v_account_rev,v_credential_rev,v_member_rev,
         v_tenant_rev,v_host_rev,v_subject_rev,v_expires,v_state
    FROM c05_06_challenges
   WHERE tenant_id=p_tenant AND token_digest=p_token_digest FOR UPDATE;

  IF NOT v_missing AND v_state='PENDING' AND v_expires>v_now THEN
    SELECT mailbox_digest,mailbox_revision,verified
      INTO v_real_mailbox,v_real_mail_rev,v_verified
      FROM c05_06_mailboxes
     WHERE tenant_id=p_tenant AND actor_id=v_actor FOR UPDATE;

    IF NOT v_missing AND v_verified=1
       AND v_mailbox=v_real_mailbox AND v_mailbox_rev=v_real_mail_rev THEN
      SELECT account_revision,credential_revision,membership_revision,
             tenant_revision,host_revision,subject_revision,account_active,
             credential_active,membership_active,host_active,tenant_active,
             member_expires_utc
        INTO v_a,v_c,v_m,v_t,v_h,v_s,v_account,
             v_credential,v_membership,v_host,v_tenant,v_until
        FROM c05_05_subjects
       WHERE tenant_id=p_tenant AND actor_id=v_actor FOR UPDATE;

      IF NOT v_missing AND
         v_a=v_account_rev AND v_c=v_credential_rev AND
         v_m=v_member_rev AND v_t=v_tenant_rev AND v_h=v_host_rev AND
         v_s=v_subject_rev AND v_account=1 AND v_credential=1 AND
         v_membership=1 AND v_host=1 AND v_tenant=1 AND v_until>v_now THEN
        SELECT credential_revision,credential_active,realm
          INTO v_global_cred_rev,v_global_active,v_realm
          FROM c05_06_credentials WHERE actor_id=v_actor FOR UPDATE;

        IF NOT v_missing AND v_realm='TENANT' AND v_global_active=1
           AND v_global_cred_rev=v_c
           AND p_new_verifier LIKE '$argon2id$v=19$%'
           AND CHAR_LENGTH(p_new_verifier) BETWEEN 80 AND 160 THEN

          UPDATE c05_06_credentials
             SET verifier_phc=p_new_verifier,
                 credential_revision=credential_revision+1
           WHERE actor_id=v_actor AND credential_revision=v_c;
          IF ROW_COUNT()=1 THEN
            -- Invalidate current and already-rotated session tokens for this
            -- GLOBAL actor in both A and B; no token can be resurrected.
            UPDATE c05_05_sessions
               SET revoked_utc=COALESCE(revoked_utc,v_now)
             WHERE actor_id=v_actor AND revoked_utc IS NULL;
            UPDATE c05_05_subjects
               SET credential_revision=v_c+1,
                   subject_revision=subject_revision+1
             WHERE actor_id=v_actor;
            UPDATE c05_06_challenges
               SET state='REVOKED'
             WHERE actor_id=v_actor AND state='PENDING';
            UPDATE c05_06_challenges
               SET state='CONSUMED', consumed_utc=v_now
             WHERE challenge_id=v_challenge;
            SET v_result='RESET';
          END IF;
        END IF;
      END IF;
    END IF;
  END IF;
  COMMIT;
  SELECT v_result AS result;
END$$

DELIMITER ;
