-- CRECISOL C05-07: disposable MySQL 8.4 extension to the C05-05/06
-- fixture only. NEVER deploy, migrate, or attach to production tenants.
-- All mailbox values and approval tokens are keyed HMAC digests.
CREATE TABLE c05_07_verifications (
  verification_id BINARY(16) NOT NULL PRIMARY KEY,
  tenant_id BINARY(16) NOT NULL,
  actor_id BINARY(16) NOT NULL,
  mailbox_digest BINARY(32) NOT NULL,
  mailbox_revision BIGINT UNSIGNED NOT NULL,
  subject_revision BIGINT UNSIGNED NOT NULL,
  token_digest BINARY(32) NOT NULL,
  issued_utc DATETIME(6) NOT NULL,
  expires_utc DATETIME(6) NOT NULL,
  confirmed_utc DATETIME(6) NULL,
  state VARCHAR(10) NOT NULL,
  UNIQUE KEY uq_c05_07_verify_token (token_digest),
  KEY ix_c05_07_verify_actor (tenant_id,actor_id,state),
  CONSTRAINT fk_c05_07_verify_actor FOREIGN KEY (tenant_id,actor_id)
    REFERENCES c05_05_subjects (tenant_id,actor_id)
    ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT ck_c05_07_verify_state CHECK (state IN ('PENDING','VERIFIED','REVOKED')),
  CONSTRAINT ck_c05_07_verify_window CHECK (expires_utc>issued_utc)
) ENGINE=InnoDB;

CREATE TABLE c05_07_changes (
  change_id BINARY(16) NOT NULL PRIMARY KEY,
  tenant_id BINARY(16) NOT NULL,
  actor_id BINARY(16) NOT NULL,
  previous_mailbox BINARY(32) NOT NULL,
  next_mailbox BINARY(32) NOT NULL,
  old_token_digest BINARY(32) NOT NULL,
  new_token_digest BINARY(32) NOT NULL,
  mailbox_revision BIGINT UNSIGNED NOT NULL,
  account_revision BIGINT UNSIGNED NOT NULL,
  credential_revision BIGINT UNSIGNED NOT NULL,
  membership_revision BIGINT UNSIGNED NOT NULL,
  tenant_revision BIGINT UNSIGNED NOT NULL,
  host_revision BIGINT UNSIGNED NOT NULL,
  subject_revision BIGINT UNSIGNED NOT NULL,
  issued_utc DATETIME(6) NOT NULL,
  expires_utc DATETIME(6) NOT NULL,
  old_confirmed_utc DATETIME(6) NULL,
  new_confirmed_utc DATETIME(6) NULL,
  state VARCHAR(10) NOT NULL,
  UNIQUE KEY uq_c05_07_old_token (old_token_digest),
  UNIQUE KEY uq_c05_07_new_token (new_token_digest),
  KEY ix_c05_07_change_actor (tenant_id,actor_id,state),
  CONSTRAINT fk_c05_07_change_actor FOREIGN KEY (tenant_id,actor_id)
    REFERENCES c05_05_subjects (tenant_id,actor_id)
    ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT ck_c05_07_change_state CHECK (state IN ('PENDING','APPLIED','REVOKED')),
  CONSTRAINT ck_c05_07_change_proofs CHECK (
    previous_mailbox<>next_mailbox AND old_token_digest<>new_token_digest),
  CONSTRAINT ck_c05_07_change_window CHECK (expires_utc>issued_utc)
) ENGINE=InnoDB;

-- Append-only, no emails, passwords, raw tokens, hashed tokens, IP or PII.
-- IDs allow reconstructing which security transitions occurred in a fixture.
CREATE TABLE c05_07_security_audit (
  event_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BINARY(16) NOT NULL,
  actor_id BINARY(16) NOT NULL,
  operation_id BINARY(16) NOT NULL,
  event_type VARCHAR(24) NOT NULL,
  occurred_utc DATETIME(6) NOT NULL,
  CONSTRAINT fk_c05_07_audit_actor FOREIGN KEY (tenant_id,actor_id)
    REFERENCES c05_05_subjects (tenant_id,actor_id)
    ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT ck_c05_07_audit_event CHECK (event_type IN (
    'VERIFY_ISSUED','MAILBOX_VERIFIED','CHANGE_REQUESTED',
    'PREVIOUS_CONFIRMED','REPLACEMENT_CONFIRMED','MAILBOX_CHANGED'))
) ENGINE=InnoDB;

DELIMITER $$

CREATE TRIGGER c05_07_audit_no_update BEFORE UPDATE ON c05_07_security_audit
FOR EACH ROW
BEGIN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Security audit is append-only';
END$$

CREATE TRIGGER c05_07_audit_no_delete BEFORE DELETE ON c05_07_security_audit
FOR EACH ROW
BEGIN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Security audit is append-only';
END$$

-- A previously registered but UNVERIFIED mailbox can receive a proof.
-- Request/unknown outcomes are indistinguishable to public HTTP clients.
CREATE PROCEDURE c05_07_begin_verify(
  IN p_tenant BINARY(16), IN p_mailbox BINARY(32),
  IN p_verification BINARY(16), IN p_digest BINARY(32)
)
SQL SECURITY DEFINER
BEGIN
  DECLARE v_missing BOOLEAN DEFAULT FALSE;
  DECLARE v_actor BINARY(16);
  DECLARE v_mail_rev BIGINT UNSIGNED;
  DECLARE v_verified TINYINT;
  DECLARE v_subject_rev BIGINT UNSIGNED;
  DECLARE v_account TINYINT;
  DECLARE v_credential TINYINT;
  DECLARE v_membership TINYINT;
  DECLARE v_host TINYINT;
  DECLARE v_tenant TINYINT;
  DECLARE v_until DATETIME(6);
  DECLARE v_now DATETIME(6);
  DECLARE v_credential_rev BIGINT UNSIGNED;
  DECLARE v_global_rev BIGINT UNSIGNED;
  DECLARE v_global_active TINYINT;
  DECLARE v_realm VARCHAR(12);
  DECLARE v_result VARCHAR(8) DEFAULT 'DENIED';
  DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_missing=TRUE;
  DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;

  START TRANSACTION;
  SET v_now=UTC_TIMESTAMP(6);
  SELECT actor_id,mailbox_revision,verified
    INTO v_actor,v_mail_rev,v_verified FROM c05_06_mailboxes
   WHERE tenant_id=p_tenant AND mailbox_digest=p_mailbox FOR UPDATE;
  IF NOT v_missing AND v_verified=0 THEN
    SELECT subject_revision,credential_revision,account_active,
           credential_active,membership_active,host_active,tenant_active,
           member_expires_utc
      INTO v_subject_rev,v_credential_rev,v_account,
           v_credential,v_membership,v_host,v_tenant,v_until
      FROM c05_05_subjects
     WHERE tenant_id=p_tenant AND actor_id=v_actor FOR UPDATE;

    IF NOT v_missing AND v_account=1 AND v_credential=1 AND
       v_membership=1 AND v_host=1 AND v_tenant=1 AND v_until>v_now THEN
      SELECT credential_revision,credential_active,realm
        INTO v_global_rev,v_global_active,v_realm
        FROM c05_06_credentials WHERE actor_id=v_actor FOR UPDATE;
      IF NOT v_missing AND v_global_active=1 AND v_realm='TENANT'
         AND v_global_rev=v_credential_rev THEN
        UPDATE c05_07_verifications SET state='REVOKED'
         WHERE tenant_id=p_tenant AND actor_id=v_actor AND state='PENDING';
        INSERT INTO c05_07_verifications
          (verification_id,tenant_id,actor_id,mailbox_digest,mailbox_revision,
           subject_revision,token_digest,issued_utc,expires_utc,state)
        VALUES
          (p_verification,p_tenant,v_actor,p_mailbox,v_mail_rev,
           v_subject_rev,p_digest,v_now,DATE_ADD(v_now,INTERVAL 10 MINUTE),'PENDING');
        INSERT INTO c05_07_security_audit
          (tenant_id,actor_id,operation_id,event_type,occurred_utc)
        VALUES (p_tenant,v_actor,p_verification,'VERIFY_ISSUED',v_now);
        SET v_result='ISSUED';
      END IF;
    END IF;
  END IF;
  COMMIT;
  SELECT v_result AS result;
END$$

CREATE PROCEDURE c05_07_confirm_verify(
  IN p_tenant BINARY(16), IN p_digest BINARY(32)
)
SQL SECURITY DEFINER
BEGIN
  DECLARE v_missing BOOLEAN DEFAULT FALSE;
  DECLARE v_verification BINARY(16);
  DECLARE v_actor BINARY(16);
  DECLARE v_mailbox BINARY(32);
  DECLARE v_mailrev BIGINT UNSIGNED;
  DECLARE v_subrev BIGINT UNSIGNED;
  DECLARE v_real_mailbox BINARY(32);
  DECLARE v_real_mailrev BIGINT UNSIGNED;
  DECLARE v_verified TINYINT;
  DECLARE v_real_subrev BIGINT UNSIGNED;
  DECLARE v_credential_rev BIGINT UNSIGNED;
  DECLARE v_account TINYINT;
  DECLARE v_credential TINYINT;
  DECLARE v_membership TINYINT;
  DECLARE v_host TINYINT;
  DECLARE v_tenant TINYINT;
  DECLARE v_until DATETIME(6);
  DECLARE v_global_rev BIGINT UNSIGNED;
  DECLARE v_global_active TINYINT;
  DECLARE v_realm VARCHAR(12);
  DECLARE v_exp DATETIME(6);
  DECLARE v_state VARCHAR(10);
  DECLARE v_now DATETIME(6);
  DECLARE v_result VARCHAR(8) DEFAULT 'DENIED';
  DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_missing=TRUE;
  DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;

  START TRANSACTION;
  SET v_now=UTC_TIMESTAMP(6);
  SELECT verification_id,actor_id,mailbox_digest,mailbox_revision,
         subject_revision,expires_utc,state
    INTO v_verification,v_actor,v_mailbox,v_mailrev,v_subrev,v_exp,v_state
    FROM c05_07_verifications
   WHERE tenant_id=p_tenant AND token_digest=p_digest FOR UPDATE;

  IF NOT v_missing AND v_state='PENDING' AND v_exp>v_now THEN
    SELECT mailbox_digest,mailbox_revision,verified
      INTO v_real_mailbox,v_real_mailrev,v_verified
      FROM c05_06_mailboxes
     WHERE tenant_id=p_tenant AND actor_id=v_actor FOR UPDATE;
    IF NOT v_missing AND v_verified=0 AND
       v_mailbox=v_real_mailbox AND v_mailrev=v_real_mailrev THEN
      SELECT subject_revision,credential_revision,account_active,
             credential_active,membership_active,host_active,tenant_active,
             member_expires_utc
        INTO v_real_subrev,v_credential_rev,v_account,v_credential,
             v_membership,v_host,v_tenant,v_until
        FROM c05_05_subjects
       WHERE tenant_id=p_tenant AND actor_id=v_actor FOR UPDATE;
      IF NOT v_missing AND v_subrev=v_real_subrev AND v_account=1 AND
         v_credential=1 AND v_membership=1 AND v_host=1 AND v_tenant=1 AND
         v_until>v_now THEN
        SELECT credential_revision,credential_active,realm
          INTO v_global_rev,v_global_active,v_realm
          FROM c05_06_credentials WHERE actor_id=v_actor FOR UPDATE;
        IF NOT v_missing AND v_global_active=1 AND v_realm='TENANT'
           AND v_global_rev=v_credential_rev THEN
          UPDATE c05_06_mailboxes SET verified=1
           WHERE tenant_id=p_tenant AND actor_id=v_actor;
          UPDATE c05_05_sessions SET revoked_utc=COALESCE(revoked_utc,v_now)
           WHERE actor_id=v_actor AND revoked_utc IS NULL;
          UPDATE c05_05_subjects SET subject_revision=subject_revision+1
           WHERE actor_id=v_actor;
          UPDATE c05_06_challenges SET state='REVOKED'
           WHERE actor_id=v_actor AND state='PENDING';
          UPDATE c05_07_verifications SET state='REVOKED'
           WHERE tenant_id=p_tenant AND actor_id=v_actor AND state='PENDING';
          UPDATE c05_07_verifications SET state='VERIFIED',confirmed_utc=v_now
           WHERE verification_id=v_verification;
          INSERT INTO c05_07_security_audit
            (tenant_id,actor_id,operation_id,event_type,occurred_utc)
          VALUES (p_tenant,v_actor,v_verification,'MAILBOX_VERIFIED',v_now);
          SET v_result='VERIFIED';
        END IF;
      END IF;
    END IF;
  END IF;
  COMMIT;
  SELECT v_result AS result;
END$$

-- A fresh REAL C05-04 Argon2id proof is REQUIRED. The trusted Application
-- supplies it immediately after checking the current password, and SQL
-- revalidates all account/credential/membership/host/tenant revisions.
-- The verified old mailbox MUST already be registered to this actor.
CREATE PROCEDURE c05_07_begin_change(
  IN p_tenant BINARY(16),IN p_actor BINARY(16),IN p_link BINARY(16),
  IN p_account_rev BIGINT UNSIGNED,IN p_credential_rev BIGINT UNSIGNED,
  IN p_membership_rev BIGINT UNSIGNED,IN p_tenant_rev BIGINT UNSIGNED,
  IN p_host_rev BIGINT UNSIGNED,IN p_old BINARY(32),IN p_new BINARY(32),
  IN p_change BINARY(16),IN p_old_token BINARY(32),IN p_new_token BINARY(32)
)
SQL SECURITY DEFINER
BEGIN
  DECLARE v_missing BOOLEAN DEFAULT FALSE;
  DECLARE v_old BINARY(32);
  DECLARE v_mailrev BIGINT UNSIGNED;
  DECLARE v_verified TINYINT;
  DECLARE v_link BINARY(16);
  DECLARE v_account_rev BIGINT UNSIGNED;
  DECLARE v_credential_rev BIGINT UNSIGNED;
  DECLARE v_membership_rev BIGINT UNSIGNED;
  DECLARE v_tenant_rev BIGINT UNSIGNED;
  DECLARE v_host_rev BIGINT UNSIGNED;
  DECLARE v_subrev BIGINT UNSIGNED;
  DECLARE v_account TINYINT;
  DECLARE v_credential TINYINT;
  DECLARE v_membership TINYINT;
  DECLARE v_host TINYINT;
  DECLARE v_tenant TINYINT;
  DECLARE v_until DATETIME(6);
  DECLARE v_global_rev BIGINT UNSIGNED;
  DECLARE v_global_active TINYINT;
  DECLARE v_realm VARCHAR(12);
  DECLARE v_collisions BIGINT DEFAULT 0;
  DECLARE v_now DATETIME(6);
  DECLARE v_result VARCHAR(8) DEFAULT 'DENIED';
  DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_missing=TRUE;
  DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;

  START TRANSACTION;
  SET v_now=UTC_TIMESTAMP(6);
  SELECT mailbox_digest,mailbox_revision,verified
    INTO v_old,v_mailrev,v_verified
    FROM c05_06_mailboxes
   WHERE tenant_id=p_tenant AND actor_id=p_actor FOR UPDATE;

  IF NOT v_missing AND v_verified=1 AND v_old=p_old AND p_old<>p_new
     AND p_old_token<>p_new_token THEN
    SELECT link_id,account_revision,credential_revision,membership_revision,
           tenant_revision,host_revision,subject_revision,
           account_active,credential_active,membership_active,
           host_active,tenant_active,member_expires_utc
      INTO v_link,v_account_rev,v_credential_rev,v_membership_rev,
           v_tenant_rev,v_host_rev,v_subrev,
           v_account,v_credential,v_membership,v_host,v_tenant,v_until
      FROM c05_05_subjects
     WHERE tenant_id=p_tenant AND actor_id=p_actor FOR UPDATE;
    IF NOT v_missing AND v_link=p_link AND
       v_account_rev=p_account_rev AND v_credential_rev=p_credential_rev AND
       v_membership_rev=p_membership_rev AND v_tenant_rev=p_tenant_rev AND
       v_host_rev=p_host_rev AND v_account=1 AND v_credential=1 AND
       v_membership=1 AND v_host=1 AND v_tenant=1 AND v_until>v_now THEN
      SELECT credential_revision,credential_active,realm
        INTO v_global_rev,v_global_active,v_realm
        FROM c05_06_credentials WHERE actor_id=p_actor FOR UPDATE;
      SELECT COUNT(*) INTO v_collisions FROM c05_06_mailboxes
       WHERE tenant_id=p_tenant AND mailbox_digest=p_new
         AND actor_id<>p_actor;
      IF NOT v_missing AND v_global_active=1 AND v_realm='TENANT' AND
         v_global_rev=v_credential_rev AND v_collisions=0 THEN
        UPDATE c05_07_changes SET state='REVOKED'
         WHERE tenant_id=p_tenant AND actor_id=p_actor AND state='PENDING';
        INSERT INTO c05_07_changes
          (change_id,tenant_id,actor_id,previous_mailbox,next_mailbox,
           old_token_digest,new_token_digest,mailbox_revision,
           account_revision,credential_revision,membership_revision,
           tenant_revision,host_revision,subject_revision,
           issued_utc,expires_utc,state)
        VALUES
          (p_change,p_tenant,p_actor,p_old,p_new,p_old_token,p_new_token,v_mailrev,
           v_account_rev,v_credential_rev,v_membership_rev,
           v_tenant_rev,v_host_rev,v_subrev,
           v_now,DATE_ADD(v_now,INTERVAL 5 MINUTE),'PENDING');
        INSERT INTO c05_07_security_audit
          (tenant_id,actor_id,operation_id,event_type,occurred_utc)
        VALUES (p_tenant,p_actor,p_change,'CHANGE_REQUESTED',v_now);
        SET v_result='ISSUED';
      END IF;
    END IF;
  END IF;
  COMMIT;
  SELECT v_result AS result;
END$$

-- Each old/new proof can be recorded at most once. When BOTH are confirmed,
-- the exact mailbox, revision, subject state and credential are checked AGAIN
-- under InnoDB row locks. Only then update the mailbox and atomically revoke
-- all sessions and outstanding recovery challenges for the global actor.
CREATE PROCEDURE c05_07_confirm_change(
  IN p_tenant BINARY(16),IN p_token BINARY(32)
)
SQL SECURITY DEFINER
BEGIN
  DECLARE v_missing BOOLEAN DEFAULT FALSE;
  DECLARE v_change BINARY(16);
  DECLARE v_actor BINARY(16);
  DECLARE v_old BINARY(32);
  DECLARE v_new BINARY(32);
  DECLARE v_oldtoken BINARY(32);
  DECLARE v_newtoken BINARY(32);
  DECLARE v_mailrev BIGINT UNSIGNED;
  DECLARE v_accountrev BIGINT UNSIGNED;
  DECLARE v_credrev BIGINT UNSIGNED;
  DECLARE v_memberrev BIGINT UNSIGNED;
  DECLARE v_tenantrev BIGINT UNSIGNED;
  DECLARE v_hostrev BIGINT UNSIGNED;
  DECLARE v_subrev BIGINT UNSIGNED;
  DECLARE v_exp DATETIME(6);
  DECLARE v_state VARCHAR(10);
  DECLARE v_old_utc DATETIME(6);
  DECLARE v_new_utc DATETIME(6);
  DECLARE v_mailbox BINARY(32);
  DECLARE v_current_mailrev BIGINT UNSIGNED;
  DECLARE v_verified TINYINT;
  DECLARE v_a BIGINT UNSIGNED;
  DECLARE v_c BIGINT UNSIGNED;
  DECLARE v_m BIGINT UNSIGNED;
  DECLARE v_t BIGINT UNSIGNED;
  DECLARE v_h BIGINT UNSIGNED;
  DECLARE v_s BIGINT UNSIGNED;
  DECLARE v_account TINYINT;
  DECLARE v_credential TINYINT;
  DECLARE v_member TINYINT;
  DECLARE v_host TINYINT;
  DECLARE v_tenant TINYINT;
  DECLARE v_until DATETIME(6);
  DECLARE v_global_rev BIGINT UNSIGNED;
  DECLARE v_global_active TINYINT;
  DECLARE v_realm VARCHAR(12);
  DECLARE v_collision BIGINT DEFAULT 0;
  DECLARE v_now DATETIME(6);
  DECLARE v_result VARCHAR(10) DEFAULT 'DENIED';
  DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_missing=TRUE;
  DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;

  START TRANSACTION;
  SET v_now=UTC_TIMESTAMP(6);
  SELECT change_id,actor_id,previous_mailbox,next_mailbox,
         old_token_digest,new_token_digest,mailbox_revision,
         account_revision,credential_revision,membership_revision,
         tenant_revision,host_revision,subject_revision,
         expires_utc,state,old_confirmed_utc,new_confirmed_utc
    INTO v_change,v_actor,v_old,v_new,v_oldtoken,v_newtoken,v_mailrev,
         v_accountrev,v_credrev,v_memberrev,v_tenantrev,v_hostrev,v_subrev,
         v_exp,v_state,v_old_utc,v_new_utc
    FROM c05_07_changes
   WHERE tenant_id=p_tenant AND
        (old_token_digest=p_token OR new_token_digest=p_token)
   FOR UPDATE;

  IF NOT v_missing AND v_state='PENDING' AND v_exp>v_now THEN
    SELECT mailbox_digest,mailbox_revision,verified
      INTO v_mailbox,v_current_mailrev,v_verified
      FROM c05_06_mailboxes
     WHERE tenant_id=p_tenant AND actor_id=v_actor FOR UPDATE;

    IF NOT v_missing AND v_verified=1 AND v_mailbox=v_old AND
       v_current_mailrev=v_mailrev THEN
      SELECT account_revision,credential_revision,membership_revision,
             tenant_revision,host_revision,subject_revision,account_active,
             credential_active,membership_active,host_active,tenant_active,
             member_expires_utc
        INTO v_a,v_c,v_m,v_t,v_h,v_s,v_account,
             v_credential,v_member,v_host,v_tenant,v_until
        FROM c05_05_subjects
       WHERE tenant_id=p_tenant AND actor_id=v_actor FOR UPDATE;

      IF NOT v_missing AND v_a=v_accountrev AND v_c=v_credrev AND
         v_m=v_memberrev AND v_t=v_tenantrev AND v_h=v_hostrev AND
         v_s=v_subrev AND v_account=1 AND v_credential=1 AND
         v_member=1 AND v_host=1 AND v_tenant=1 AND v_until>v_now THEN
        SELECT credential_revision,credential_active,realm
          INTO v_global_rev,v_global_active,v_realm
          FROM c05_06_credentials WHERE actor_id=v_actor FOR UPDATE;

        IF NOT v_missing AND v_global_active=1 AND v_realm='TENANT'
           AND v_global_rev=v_c THEN
          IF p_token=v_oldtoken AND v_old_utc IS NULL THEN
            SET v_old_utc=v_now;
            UPDATE c05_07_changes SET old_confirmed_utc=v_now
             WHERE change_id=v_change;
            INSERT INTO c05_07_security_audit
              (tenant_id,actor_id,operation_id,event_type,occurred_utc)
            VALUES (p_tenant,v_actor,v_change,'PREVIOUS_CONFIRMED',v_now);
            SET v_result='RECORDED';
          ELSEIF p_token=v_newtoken AND v_new_utc IS NULL THEN
            SET v_new_utc=v_now;
            UPDATE c05_07_changes SET new_confirmed_utc=v_now
             WHERE change_id=v_change;
            INSERT INTO c05_07_security_audit
              (tenant_id,actor_id,operation_id,event_type,occurred_utc)
            VALUES (p_tenant,v_actor,v_change,'REPLACEMENT_CONFIRMED',v_now);
            SET v_result='RECORDED';
          END IF;

          IF v_result='RECORDED' AND v_old_utc IS NOT NULL AND
             v_new_utc IS NOT NULL THEN
            SELECT COUNT(*) INTO v_collision FROM c05_06_mailboxes
             WHERE tenant_id=p_tenant AND mailbox_digest=v_new
               AND actor_id<>v_actor;
            IF v_collision=0 THEN
              UPDATE c05_06_mailboxes SET mailbox_digest=v_new
               WHERE tenant_id=p_tenant AND actor_id=v_actor
                 AND mailbox_digest=v_old AND mailbox_revision=v_mailrev;
              IF ROW_COUNT()=1 THEN
                UPDATE c05_05_sessions
                   SET revoked_utc=COALESCE(revoked_utc,v_now)
                 WHERE actor_id=v_actor AND revoked_utc IS NULL;
                UPDATE c05_05_subjects
                   SET subject_revision=subject_revision+1
                 WHERE actor_id=v_actor;
                UPDATE c05_06_challenges SET state='REVOKED'
                 WHERE actor_id=v_actor AND state='PENDING';
                UPDATE c05_07_verifications SET state='REVOKED'
                 WHERE actor_id=v_actor AND state='PENDING';
                UPDATE c05_07_changes SET state='REVOKED'
                 WHERE actor_id=v_actor AND state='PENDING';
                UPDATE c05_07_changes SET state='APPLIED'
                 WHERE change_id=v_change;
                INSERT INTO c05_07_security_audit
                  (tenant_id,actor_id,operation_id,event_type,occurred_utc)
                VALUES (p_tenant,v_actor,v_change,'MAILBOX_CHANGED',v_now);
                SET v_result='APPLIED';
              ELSE
                SIGNAL SQLSTATE '45000'
                  SET MESSAGE_TEXT='Mail change CAS revision conflict';
              END IF;
            ELSE
              SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT='Conflicting mailbox binding';
            END IF;
          END IF;
        END IF;
      END IF;
    END IF;
  END IF;
  COMMIT;
  SELECT v_result AS result;
END$$

DELIMITER ;
