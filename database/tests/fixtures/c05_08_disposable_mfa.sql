-- CRECISOL C05-08: exclusively synthetic MFA state machine.
-- Depends on C05-05 and C05-06 disposable fixtures; NEVER a migration.
-- No seed, private key, recovery code, password, bearer or email is persisted.
-- All APIs below are for a loopback-only test backend with EXECUTE privilege.
CREATE TABLE c05_08_factors (
  factor_id BINARY(16) NOT NULL PRIMARY KEY,
  tenant_id BINARY(16) NOT NULL,
  actor_id BINARY(16) NOT NULL,
  link_id BINARY(16) NOT NULL,
  kind VARCHAR(8) NOT NULL,
  state VARCHAR(8) NOT NULL,
  replaces_id BINARY(16) NULL,
  last_counter BIGINT NOT NULL DEFAULT -1,
  created_utc DATETIME(6) NOT NULL,
  activated_utc DATETIME(6) NULL,
  UNIQUE KEY uq_c05_08_factor_scoped (tenant_id, actor_id, factor_id),
  KEY ix_c05_08_factor_actor (tenant_id, actor_id, state),
  CONSTRAINT fk_c05_08_factor_subject FOREIGN KEY (tenant_id, actor_id)
    REFERENCES c05_05_subjects (tenant_id, actor_id)
    ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT fk_c05_08_replaces FOREIGN KEY (tenant_id, actor_id, replaces_id)
    REFERENCES c05_08_factors (tenant_id, actor_id, factor_id)
    ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT ck_c05_08_factor_kind CHECK (kind IN ('TOTP','PASSKEY')),
  CONSTRAINT ck_c05_08_factor_state CHECK (state IN ('PENDING','ACTIVE','REVOKED')),
  CONSTRAINT ck_c05_08_counter CHECK (last_counter >= -1),
  CONSTRAINT ck_c05_08_activation CHECK (
    (state='PENDING' AND activated_utc IS NULL) OR
    (state IN ('ACTIVE','REVOKED')))
) ENGINE=InnoDB;

CREATE TABLE c05_08_challenges (
  challenge_id BINARY(16) NOT NULL PRIMARY KEY,
  tenant_id BINARY(16) NOT NULL,
  actor_id BINARY(16) NOT NULL,
  factor_id BINARY(16) NOT NULL,
  operation_id BINARY(16) NOT NULL,
  challenge_kind VARCHAR(8) NOT NULL,
  risk_action VARCHAR(24) NOT NULL,
  digest BINARY(32) NOT NULL,
  link_id BINARY(16) NOT NULL,
  account_revision BIGINT UNSIGNED NOT NULL,
  credential_revision BIGINT UNSIGNED NOT NULL,
  membership_revision BIGINT UNSIGNED NOT NULL,
  tenant_revision BIGINT UNSIGNED NOT NULL,
  host_revision BIGINT UNSIGNED NOT NULL,
  subject_revision BIGINT UNSIGNED NOT NULL,
  issued_utc DATETIME(6) NOT NULL,
  expires_utc DATETIME(6) NOT NULL,
  consumed_utc DATETIME(6) NULL,
  state VARCHAR(8) NOT NULL,
  UNIQUE KEY uq_c05_08_digest (digest),
  KEY ix_c05_08_actor_challenges (actor_id, state),
  CONSTRAINT fk_c05_08_challenge_factor FOREIGN KEY
    (tenant_id, actor_id, factor_id)
    REFERENCES c05_08_factors (tenant_id, actor_id, factor_id)
    ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT ck_c05_08_challenge_kind CHECK (challenge_kind IN ('ENROLL','STEPUP')),
  CONSTRAINT ck_c05_08_challenge_risk CHECK
    (risk_action IN ('ENROLL','TENANT_SENSITIVE')),
  CONSTRAINT ck_c05_08_challenge_state CHECK (state IN ('PENDING','CONSUMED','REVOKED')),
  CONSTRAINT ck_c05_08_challenge_expiry CHECK (expires_utc > issued_utc)
) ENGINE=InnoDB;

CREATE TABLE c05_08_backup_codes (
  digest BINARY(32) NOT NULL PRIMARY KEY,
  tenant_id BINARY(16) NOT NULL,
  actor_id BINARY(16) NOT NULL,
  factor_id BINARY(16) NOT NULL,
  issued_utc DATETIME(6) NOT NULL,
  expires_utc DATETIME(6) NOT NULL,
  consumed_utc DATETIME(6) NULL,
  CONSTRAINT fk_c05_08_backup_factor FOREIGN KEY
    (tenant_id, actor_id, factor_id)
    REFERENCES c05_08_factors (tenant_id, actor_id, factor_id)
    ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT ck_c05_08_backup_expiry CHECK (expires_utc > issued_utc)
) ENGINE=InnoDB;

CREATE TABLE c05_08_security_audit (
  event_id BIGINT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  tenant_id BINARY(16) NOT NULL,
  actor_id BINARY(16) NOT NULL,
  operation_id BINARY(16) NOT NULL,
  event_type VARCHAR(24) NOT NULL,
  occurred_utc DATETIME(6) NOT NULL,
  CONSTRAINT fk_c05_08_audit_subject FOREIGN KEY (tenant_id, actor_id)
    REFERENCES c05_05_subjects (tenant_id, actor_id)
    ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT ck_c05_08_event CHECK (event_type IN
    ('ENROLL_PENDING','FACTOR_ACTIVE','STEPUP_ISSUED',
     'STEPUP_PROVED','REPLACE_PENDING','FACTOR_REPLACED'))
) ENGINE=InnoDB;

DELIMITER $$

CREATE TRIGGER c05_08_audit_immutable_update
BEFORE UPDATE ON c05_08_security_audit FOR EACH ROW
BEGIN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C05-08 audit append-only';
END$$
CREATE TRIGGER c05_08_audit_immutable_delete
BEFORE DELETE ON c05_08_security_audit FOR EACH ROW
BEGIN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C05-08 audit append-only';
END$$

-- The backend supplies the current fresh C05-04 Argon2id preparation. SQL
-- independently checks snapshot revisions and C05-07 verified mailbox.
CREATE PROCEDURE c05_08_open_enrollment(
 IN p_tenant BINARY(16), IN p_actor BINARY(16), IN p_link BINARY(16),
 IN p_account BIGINT UNSIGNED, IN p_credential BIGINT UNSIGNED,
 IN p_member BIGINT UNSIGNED, IN p_tenant_rev BIGINT UNSIGNED,
 IN p_host BIGINT UNSIGNED, IN p_factor BINARY(16), IN p_kind VARCHAR(8),
 IN p_challenge BINARY(16), IN p_digest BINARY(32), IN p_backup BINARY(32)
)
SQL SECURITY DEFINER
proc:BEGIN
  DECLARE v_missing BOOL DEFAULT FALSE;
  DECLARE v_link BINARY(16);
  DECLARE v_subrev BIGINT UNSIGNED;
  DECLARE v_account BIGINT UNSIGNED;
  DECLARE v_credential BIGINT UNSIGNED;
  DECLARE v_member BIGINT UNSIGNED;
  DECLARE v_tenant_rev BIGINT UNSIGNED;
  DECLARE v_host BIGINT UNSIGNED;
  DECLARE v_a TINYINT; DECLARE v_c TINYINT; DECLARE v_m TINYINT;
  DECLARE v_h TINYINT; DECLARE v_t TINYINT;
  DECLARE v_until DATETIME(6);
  DECLARE v_global BIGINT UNSIGNED; DECLARE v_global_active TINYINT;
  DECLARE v_realm VARCHAR(12); DECLARE v_verified TINYINT;
  DECLARE v_now DATETIME(6);
  DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_missing=TRUE;
  DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;
  IF p_kind NOT IN ('TOTP','PASSKEY') THEN SELECT 'DENIED' AS result; LEAVE proc; END IF;
  START TRANSACTION;
  SET v_now=UTC_TIMESTAMP(6);
  SELECT link_id,account_revision,credential_revision,membership_revision,
      tenant_revision,host_revision,subject_revision,
      account_active,credential_active,membership_active,host_active,
      tenant_active,member_expires_utc
    INTO v_link,v_account,v_credential,v_member,v_tenant_rev,v_host,v_subrev,
      v_a,v_c,v_m,v_h,v_t,v_until
    FROM c05_05_subjects
   WHERE tenant_id=p_tenant AND actor_id=p_actor FOR UPDATE;
  IF v_missing OR v_link<>p_link OR v_account<>p_account OR
     v_credential<>p_credential OR v_member<>p_member OR
     v_tenant_rev<>p_tenant_rev OR v_host<>p_host OR
     v_a<>1 OR v_c<>1 OR v_m<>1 OR v_h<>1 OR v_t<>1 OR v_until<=v_now THEN
    ROLLBACK; SELECT 'DENIED' AS result; LEAVE proc;
  END IF;
  SELECT credential_revision,credential_active,realm
    INTO v_global,v_global_active,v_realm FROM c05_06_credentials
   WHERE actor_id=p_actor FOR UPDATE;
  IF v_missing OR v_global<>v_credential OR v_global_active<>1 OR v_realm<>'TENANT' THEN
    ROLLBACK; SELECT 'DENIED' AS result; LEAVE proc;
  END IF;
  SELECT verified INTO v_verified FROM c05_06_mailboxes
   WHERE tenant_id=p_tenant AND actor_id=p_actor FOR UPDATE;
  IF v_missing OR v_verified<>1 OR EXISTS(
     SELECT 1 FROM c05_08_factors
      WHERE tenant_id=p_tenant AND actor_id=p_actor AND state IN ('PENDING','ACTIVE')
  ) THEN ROLLBACK; SELECT 'DENIED' AS result; LEAVE proc; END IF;
  INSERT INTO c05_08_factors(factor_id,tenant_id,actor_id,link_id,kind,
      state,replaces_id,last_counter,created_utc)
    VALUES(p_factor,p_tenant,p_actor,p_link,p_kind,'PENDING',NULL,-1,v_now);
  INSERT INTO c05_08_challenges(challenge_id,tenant_id,actor_id,factor_id,
      operation_id,challenge_kind,risk_action,digest,link_id,account_revision,
      credential_revision,membership_revision,tenant_revision,host_revision,
      subject_revision,issued_utc,expires_utc,state)
    VALUES(p_challenge,p_tenant,p_actor,p_factor,p_challenge,'ENROLL','ENROLL',
      p_digest,p_link,v_account,v_credential,v_member,v_tenant_rev,v_host,
      v_subrev,v_now,DATE_ADD(v_now,INTERVAL 5 MINUTE),'PENDING');
  INSERT INTO c05_08_backup_codes(digest,tenant_id,actor_id,factor_id,
      issued_utc,expires_utc)
    VALUES(p_backup,p_tenant,p_actor,p_factor,v_now,DATE_ADD(v_now,INTERVAL 30 DAY));
  INSERT INTO c05_08_security_audit(tenant_id,actor_id,operation_id,event_type,occurred_utc)
    VALUES(p_tenant,p_actor,p_challenge,'ENROLL_PENDING',v_now);
  COMMIT;
  SELECT 'PENDING' AS result;
END$$

CREATE PROCEDURE c05_08_open_stepup(
 IN p_tenant BINARY(16), IN p_actor BINARY(16),
 IN p_factor BINARY(16), IN p_challenge BINARY(16),
 IN p_digest BINARY(32), IN p_operation BINARY(16),
 IN p_risk VARCHAR(24)
)
SQL SECURITY DEFINER
proc:BEGIN
  DECLARE v_missing BOOL DEFAULT FALSE;
  DECLARE v_link BINARY(16);
  DECLARE v_sub BIGINT UNSIGNED;
  DECLARE v_a BIGINT UNSIGNED; DECLARE v_c BIGINT UNSIGNED;
  DECLARE v_m BIGINT UNSIGNED; DECLARE v_t BIGINT UNSIGNED; DECLARE v_h BIGINT UNSIGNED;
  DECLARE v_account TINYINT; DECLARE v_cred TINYINT;
  DECLARE v_membership TINYINT; DECLARE v_host TINYINT; DECLARE v_tenant TINYINT;
  DECLARE v_until DATETIME(6); DECLARE v_kind VARCHAR(8);
  DECLARE v_state VARCHAR(8); DECLARE v_factor_link BINARY(16);
  DECLARE v_realm VARCHAR(12); DECLARE v_global_rev BIGINT UNSIGNED;
  DECLARE v_global_active TINYINT; DECLARE v_verified TINYINT;
  DECLARE v_now DATETIME(6);
  DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_missing=TRUE;
  DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;
  IF p_risk<>'TENANT_SENSITIVE' THEN SELECT 'DENIED' AS result; LEAVE proc; END IF;
  START TRANSACTION;
  SET v_now=UTC_TIMESTAMP(6);
  SELECT link_id,subject_revision,account_revision,credential_revision,
      membership_revision,tenant_revision,host_revision,account_active,
      credential_active,membership_active,host_active,tenant_active,member_expires_utc
    INTO v_link,v_sub,v_a,v_c,v_m,v_t,v_h,v_account,v_cred,
      v_membership,v_host,v_tenant,v_until
    FROM c05_05_subjects
   WHERE tenant_id=p_tenant AND actor_id=p_actor FOR UPDATE;
  IF v_missing OR v_account<>1 OR v_cred<>1 OR v_membership<>1 OR
     v_host<>1 OR v_tenant<>1 OR v_until<=v_now THEN
    ROLLBACK; SELECT 'DENIED' AS result; LEAVE proc; END IF;
  SELECT credential_revision,credential_active,realm
    INTO v_global_rev,v_global_active,v_realm FROM c05_06_credentials
   WHERE actor_id=p_actor FOR UPDATE;
  IF v_missing OR v_global_rev<>v_c OR v_global_active<>1 OR v_realm<>'TENANT' THEN
    ROLLBACK; SELECT 'DENIED' AS result; LEAVE proc; END IF;
  SELECT verified INTO v_verified FROM c05_06_mailboxes
   WHERE tenant_id=p_tenant AND actor_id=p_actor FOR UPDATE;
  IF v_missing OR v_verified<>1 THEN
    ROLLBACK; SELECT 'DENIED' AS result; LEAVE proc; END IF;
  SELECT kind,state,link_id INTO v_kind,v_state,v_factor_link
    FROM c05_08_factors
   WHERE factor_id=p_factor AND tenant_id=p_tenant AND actor_id=p_actor FOR UPDATE;
  IF v_missing OR v_state<>'ACTIVE' OR v_link<>v_factor_link OR EXISTS(
    SELECT 1 FROM c05_08_factors WHERE replaces_id=p_factor AND state='PENDING'
  ) THEN ROLLBACK; SELECT 'DENIED' AS result; LEAVE proc; END IF;
  INSERT INTO c05_08_challenges(challenge_id,tenant_id,actor_id,factor_id,
      operation_id,challenge_kind,risk_action,digest,link_id,account_revision,
      credential_revision,membership_revision,tenant_revision,host_revision,
      subject_revision,issued_utc,expires_utc,state)
    VALUES(p_challenge,p_tenant,p_actor,p_factor,p_operation,'STEPUP',p_risk,
      p_digest,v_link,v_a,v_c,v_m,v_t,v_h,v_sub,
      v_now,DATE_ADD(v_now,INTERVAL 3 MINUTE),'PENDING');
  INSERT INTO c05_08_security_audit(tenant_id,actor_id,operation_id,event_type,occurred_utc)
    VALUES(p_tenant,p_actor,p_operation,'STEPUP_ISSUED',v_now);
  COMMIT;
  SELECT 'ISSUED' AS result;
END$$

-- p_counter has ALREADY been cryptographically verified by test-only backend
-- against protected seed/public key. SQL accepts only strictly increasing
-- persisted counters; cannot turn backup/password into factor evidence.
CREATE PROCEDURE c05_08_commit_proof(
 IN p_tenant BINARY(16), IN p_actor BINARY(16),
 IN p_factor BINARY(16), IN p_challenge BINARY(16),
 IN p_digest BINARY(32), IN p_operation BINARY(16),
 IN p_kind VARCHAR(8), IN p_counter BIGINT
)
SQL SECURITY DEFINER
proc:BEGIN
  DECLARE v_missing BOOL DEFAULT FALSE;
  DECLARE v_link BINARY(16); DECLARE v_sub BIGINT UNSIGNED;
  DECLARE v_a BIGINT UNSIGNED; DECLARE v_c BIGINT UNSIGNED;
  DECLARE v_m BIGINT UNSIGNED; DECLARE v_t BIGINT UNSIGNED; DECLARE v_h BIGINT UNSIGNED;
  DECLARE v_account TINYINT; DECLARE v_cred TINYINT;
  DECLARE v_member TINYINT; DECLARE v_host TINYINT; DECLARE v_tenant TINYINT;
  DECLARE v_until DATETIME(6); DECLARE v_factor_link BINARY(16);
  DECLARE v_state VARCHAR(8); DECLARE v_factor_kind VARCHAR(8);
  DECLARE v_counter BIGINT; DECLARE v_replaces BINARY(16);
  DECLARE v_digest BINARY(32); DECLARE v_kind VARCHAR(8);
  DECLARE v_risk VARCHAR(24); DECLARE v_operation BINARY(16);
  DECLARE v_challenge_state VARCHAR(8); DECLARE v_exp DATETIME(6);
  DECLARE v_ca BIGINT UNSIGNED; DECLARE v_cc BIGINT UNSIGNED;
  DECLARE v_cm BIGINT UNSIGNED; DECLARE v_ct BIGINT UNSIGNED;
  DECLARE v_ch BIGINT UNSIGNED; DECLARE v_cs BIGINT UNSIGNED;
  DECLARE v_challenge_link BINARY(16);
  DECLARE v_global_rev BIGINT UNSIGNED; DECLARE v_global_active TINYINT;
  DECLARE v_realm VARCHAR(12); DECLARE v_verified TINYINT;
  DECLARE v_now DATETIME(6);
  DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_missing=TRUE;
  DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;
  IF p_counter<0 OR p_kind NOT IN ('ENROLL','STEPUP') THEN
    SELECT 'DENIED' AS result; LEAVE proc; END IF;
  START TRANSACTION;
  SET v_now=UTC_TIMESTAMP(6);
  SELECT link_id,subject_revision,account_revision,credential_revision,
      membership_revision,tenant_revision,host_revision,
      account_active,credential_active,membership_active,host_active,tenant_active,
      member_expires_utc
    INTO v_link,v_sub,v_a,v_c,v_m,v_t,v_h,v_account,v_cred,v_member,
      v_host,v_tenant,v_until
    FROM c05_05_subjects
   WHERE tenant_id=p_tenant AND actor_id=p_actor FOR UPDATE;
  IF v_missing OR v_account<>1 OR v_cred<>1 OR v_member<>1 OR
     v_host<>1 OR v_tenant<>1 OR v_until<=v_now THEN
    ROLLBACK; SELECT 'DENIED' AS result; LEAVE proc; END IF;
  SELECT credential_revision,credential_active,realm INTO
    v_global_rev,v_global_active,v_realm FROM c05_06_credentials
   WHERE actor_id=p_actor FOR UPDATE;
  IF v_missing OR v_global_rev<>v_c OR v_global_active<>1 OR v_realm<>'TENANT' THEN
    ROLLBACK; SELECT 'DENIED' AS result; LEAVE proc; END IF;
  SELECT verified INTO v_verified FROM c05_06_mailboxes
   WHERE tenant_id=p_tenant AND actor_id=p_actor FOR UPDATE;
  IF v_missing OR v_verified<>1 THEN
    ROLLBACK; SELECT 'DENIED' AS result; LEAVE proc; END IF;
  SELECT state,kind,link_id,last_counter,replaces_id
    INTO v_state,v_factor_kind,v_factor_link,v_counter,v_replaces
    FROM c05_08_factors
   WHERE factor_id=p_factor AND tenant_id=p_tenant AND actor_id=p_actor FOR UPDATE;
  IF v_missing OR v_link<>v_factor_link OR
     (p_kind='ENROLL' AND v_state<>'PENDING') OR
     (p_kind='STEPUP' AND v_state<>'ACTIVE') OR
     (p_kind='STEPUP' AND EXISTS (
       SELECT 1 FROM c05_08_factors WHERE replaces_id=p_factor AND state='PENDING'
     )) OR p_counter<=v_counter THEN
    ROLLBACK; SELECT 'DENIED' AS result; LEAVE proc; END IF;
  SELECT digest,challenge_kind,risk_action,operation_id,state,expires_utc,
      link_id,account_revision,credential_revision,membership_revision,
      tenant_revision,host_revision,subject_revision
    INTO v_digest,v_kind,v_risk,v_operation,v_challenge_state,v_exp,
      v_challenge_link,v_ca,v_cc,v_cm,v_ct,v_ch,v_cs
    FROM c05_08_challenges
   WHERE challenge_id=p_challenge AND factor_id=p_factor
     AND tenant_id=p_tenant AND actor_id=p_actor FOR UPDATE;
  IF v_missing OR v_kind<>p_kind OR v_challenge_state<>'PENDING' OR
     v_exp<=v_now OR v_digest<>p_digest OR v_operation<>p_operation OR
     v_challenge_link<>v_link OR v_ca<>v_a OR v_cc<>v_c OR
     v_cm<>v_m OR v_ct<>v_t OR v_ch<>v_h OR v_cs<>v_sub OR
     (p_kind='ENROLL' AND v_risk<>'ENROLL') OR
     (p_kind='STEPUP' AND v_risk<>'TENANT_SENSITIVE') THEN
    ROLLBACK; SELECT 'DENIED' AS result; LEAVE proc; END IF;
  UPDATE c05_08_factors SET last_counter=p_counter,
      state=IF(p_kind='ENROLL','ACTIVE',state),
      activated_utc=IF(p_kind='ENROLL',v_now,activated_utc)
   WHERE factor_id=p_factor AND last_counter<p_counter;
  UPDATE c05_08_challenges SET state='CONSUMED',consumed_utc=v_now
   WHERE challenge_id=p_challenge AND state='PENDING';
  IF p_kind='ENROLL' THEN
    IF v_replaces IS NOT NULL THEN
      UPDATE c05_08_factors SET state='REVOKED' WHERE
        factor_id=v_replaces AND tenant_id=p_tenant AND actor_id=p_actor
        AND state='ACTIVE';
      UPDATE c05_08_challenges SET state='REVOKED'
        WHERE actor_id=p_actor AND state='PENDING';
      UPDATE c05_05_sessions SET revoked_utc=v_now
        WHERE actor_id=p_actor AND revoked_utc IS NULL;
      UPDATE c05_05_subjects SET subject_revision=subject_revision+1
        WHERE actor_id=p_actor;
      INSERT INTO c05_08_security_audit
        (tenant_id,actor_id,operation_id,event_type,occurred_utc)
        VALUES(p_tenant,p_actor,p_operation,'FACTOR_REPLACED',v_now);
    ELSE
      INSERT INTO c05_08_security_audit
        (tenant_id,actor_id,operation_id,event_type,occurred_utc)
        VALUES(p_tenant,p_actor,p_operation,'FACTOR_ACTIVE',v_now);
    END IF;
    COMMIT; SELECT 'ACTIVE' AS result; LEAVE proc;
  END IF;
  INSERT INTO c05_08_security_audit
      (tenant_id,actor_id,operation_id,event_type,occurred_utc)
    VALUES(p_tenant,p_actor,p_operation,'STEPUP_PROVED',v_now);
  COMMIT;
  SELECT 'PROVED' AS result;
END$$

-- A valid backup code AND a fresh password may create a pending replacement.
-- The former ACTIVE factor is NOT disabled, step-up is NOT granted, and its
-- outstanding challenges are revoked. Activation requires separate crypto proof.
CREATE PROCEDURE c05_08_open_replacement(
 IN p_tenant BINARY(16), IN p_actor BINARY(16), IN p_old BINARY(16),
 IN p_backup BINARY(32), IN p_new BINARY(16), IN p_new_kind VARCHAR(8),
 IN p_challenge BINARY(16), IN p_digest BINARY(32)
)
SQL SECURITY DEFINER
proc:BEGIN
  DECLARE v_missing BOOL DEFAULT FALSE;
  DECLARE v_link BINARY(16); DECLARE v_sub BIGINT UNSIGNED;
  DECLARE v_a BIGINT UNSIGNED; DECLARE v_c BIGINT UNSIGNED;
  DECLARE v_m BIGINT UNSIGNED; DECLARE v_t BIGINT UNSIGNED; DECLARE v_h BIGINT UNSIGNED;
  DECLARE v_account TINYINT; DECLARE v_cred TINYINT;
  DECLARE v_member TINYINT; DECLARE v_host TINYINT; DECLARE v_tenant TINYINT;
  DECLARE v_until DATETIME(6); DECLARE v_state VARCHAR(8);
  DECLARE v_factor_link BINARY(16); DECLARE v_backup_factor BINARY(16);
  DECLARE v_expires DATETIME(6); DECLARE v_consumed DATETIME(6);
  DECLARE v_global_rev BIGINT UNSIGNED; DECLARE v_global_active TINYINT;
  DECLARE v_realm VARCHAR(12); DECLARE v_verified TINYINT;
  DECLARE v_now DATETIME(6);
  DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_missing=TRUE;
  DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;
  IF p_new_kind NOT IN ('TOTP','PASSKEY') THEN SELECT 'DENIED' AS result; LEAVE proc; END IF;
  START TRANSACTION;
  SET v_now=UTC_TIMESTAMP(6);
  SELECT link_id,subject_revision,account_revision,credential_revision,
      membership_revision,tenant_revision,host_revision,
      account_active,credential_active,membership_active,host_active,tenant_active,
      member_expires_utc
    INTO v_link,v_sub,v_a,v_c,v_m,v_t,v_h,v_account,v_cred,v_member,
      v_host,v_tenant,v_until FROM c05_05_subjects
   WHERE tenant_id=p_tenant AND actor_id=p_actor FOR UPDATE;
  IF v_missing OR v_account<>1 OR v_cred<>1 OR v_member<>1 OR
     v_host<>1 OR v_tenant<>1 OR v_until<=v_now THEN
    ROLLBACK; SELECT 'DENIED' AS result; LEAVE proc; END IF;
  SELECT credential_revision,credential_active,realm INTO
    v_global_rev,v_global_active,v_realm FROM c05_06_credentials
   WHERE actor_id=p_actor FOR UPDATE;
  IF v_missing OR v_global_rev<>v_c OR v_global_active<>1 OR v_realm<>'TENANT' THEN
    ROLLBACK; SELECT 'DENIED' AS result; LEAVE proc; END IF;
  SELECT verified INTO v_verified FROM c05_06_mailboxes
   WHERE tenant_id=p_tenant AND actor_id=p_actor FOR UPDATE;
  IF v_missing OR v_verified<>1 THEN
    ROLLBACK; SELECT 'DENIED' AS result; LEAVE proc; END IF;
  SELECT state,link_id INTO v_state,v_factor_link FROM c05_08_factors
   WHERE factor_id=p_old AND tenant_id=p_tenant AND actor_id=p_actor FOR UPDATE;
  IF v_missing OR v_state<>'ACTIVE' OR v_factor_link<>v_link OR EXISTS(
     SELECT 1 FROM c05_08_factors WHERE replaces_id=p_old AND state='PENDING'
  ) THEN ROLLBACK; SELECT 'DENIED' AS result; LEAVE proc; END IF;
  SELECT factor_id,expires_utc,consumed_utc
    INTO v_backup_factor,v_expires,v_consumed FROM c05_08_backup_codes
   WHERE digest=p_backup AND tenant_id=p_tenant AND actor_id=p_actor FOR UPDATE;
  IF v_missing OR v_backup_factor<>p_old OR v_consumed IS NOT NULL OR
     v_expires<=v_now THEN ROLLBACK; SELECT 'DENIED' AS result; LEAVE proc; END IF;
  UPDATE c05_08_backup_codes SET consumed_utc=v_now WHERE digest=p_backup
    AND consumed_utc IS NULL;
  UPDATE c05_08_challenges SET state='REVOKED'
    WHERE actor_id=p_actor AND state='PENDING';
  INSERT INTO c05_08_factors(factor_id,tenant_id,actor_id,link_id,kind,
      state,replaces_id,last_counter,created_utc)
    VALUES(p_new,p_tenant,p_actor,v_link,p_new_kind,'PENDING',p_old,-1,v_now);
  INSERT INTO c05_08_challenges(challenge_id,tenant_id,actor_id,factor_id,
      operation_id,challenge_kind,risk_action,digest,link_id,account_revision,
      credential_revision,membership_revision,tenant_revision,host_revision,
      subject_revision,issued_utc,expires_utc,state)
    VALUES(p_challenge,p_tenant,p_actor,p_new,p_challenge,'ENROLL','ENROLL',
      p_digest,v_link,v_a,v_c,v_m,v_t,v_h,v_sub,
      v_now,DATE_ADD(v_now,INTERVAL 5 MINUTE),'PENDING');
  INSERT INTO c05_08_security_audit(tenant_id,actor_id,operation_id,event_type,occurred_utc)
    VALUES(p_tenant,p_actor,p_challenge,'REPLACE_PENDING',v_now);
  COMMIT;
  SELECT 'PENDING' AS result;
END$$

CREATE PROCEDURE c05_08_read_counter(
 IN p_tenant BINARY(16), IN p_actor BINARY(16), IN p_factor BINARY(16)
)
SQL SECURITY DEFINER
BEGIN
  SELECT last_counter,kind,state FROM c05_08_factors
   WHERE tenant_id=p_tenant AND actor_id=p_actor AND factor_id=p_factor;
END$$

DELIMITER ;
