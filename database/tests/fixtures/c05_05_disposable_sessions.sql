-- CRECISOL C05-05: disposable MySQL8.4 fixture ONLY. Not a production
-- migration, user directory, login session schema or deployment procedure.
-- This fixture deliberately stores ONLY keyed HMAC token digests, never bearer
-- tokens, passwords, PHC verifiers, email, Host, headers, roles or DSNs.
CREATE TABLE c05_05_subjects (
  tenant_id BINARY(16) NOT NULL,
  actor_id BINARY(16) NOT NULL,
  link_id BINARY(16) NOT NULL,
  account_revision BIGINT UNSIGNED NOT NULL,
  credential_revision BIGINT UNSIGNED NOT NULL,
  membership_revision BIGINT UNSIGNED NOT NULL,
  tenant_revision BIGINT UNSIGNED NOT NULL,
  host_revision BIGINT UNSIGNED NOT NULL,
  subject_revision BIGINT UNSIGNED NOT NULL,
  account_active TINYINT(1) NOT NULL,
  credential_active TINYINT(1) NOT NULL,
  membership_active TINYINT(1) NOT NULL,
  host_active TINYINT(1) NOT NULL,
  tenant_active TINYINT(1) NOT NULL,
  member_expires_utc DATETIME(6) NOT NULL,
  PRIMARY KEY (tenant_id, actor_id),
  UNIQUE KEY uq_c05_05_link (tenant_id, link_id),
  CONSTRAINT ck_c05_05_subject_revisions CHECK (
      account_revision > 0 AND credential_revision > 0 AND
      membership_revision > 0 AND tenant_revision > 0 AND
      host_revision > 0 AND subject_revision > 0)
) ENGINE=InnoDB;

CREATE TABLE c05_05_sessions (
  session_id BINARY(16) NOT NULL,
  tenant_id BINARY(16) NOT NULL,
  actor_id BINARY(16) NOT NULL,
  token_digest BINARY(32) NOT NULL,
  generation BIGINT UNSIGNED NOT NULL,
  subject_revision BIGINT UNSIGNED NOT NULL,
  issued_utc DATETIME(6) NOT NULL,
  expires_utc DATETIME(6) NOT NULL,
  absolute_expires_utc DATETIME(6) NOT NULL,
  revoked_utc DATETIME(6) NULL,
  PRIMARY KEY (session_id),
  UNIQUE KEY uq_c05_05_current_digest (token_digest),
  KEY ix_c05_05_tenant_actor (tenant_id, actor_id),
  CONSTRAINT fk_c05_05_subject FOREIGN KEY (tenant_id, actor_id)
    REFERENCES c05_05_subjects (tenant_id, actor_id)
    ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT ck_c05_05_session_window CHECK (
      generation > 0 AND subject_revision > 0 AND
      expires_utc > issued_utc AND
      absolute_expires_utc >= expires_utc)
) ENGINE=InnoDB;

DELIMITER $$

-- SQL-side atomic account/membership/host/catalog revision recheck from
-- C05-04. A privileged fixture seed is NOT a real identity directory.
CREATE PROCEDURE c05_05_issue_once(
  IN p_session BINARY(16), IN p_tenant BINARY(16),
  IN p_actor BINARY(16), IN p_link BINARY(16),
  IN p_account_revision BIGINT UNSIGNED,
  IN p_credential_revision BIGINT UNSIGNED,
  IN p_membership_revision BIGINT UNSIGNED,
  IN p_tenant_revision BIGINT UNSIGNED,
  IN p_host_revision BIGINT UNSIGNED,
  IN p_digest BINARY(32)
)
SQL SECURITY DEFINER
BEGIN
  DECLARE v_missing BOOLEAN DEFAULT FALSE;
  DECLARE v_found_link BINARY(16);
  DECLARE v_a BIGINT UNSIGNED;
  DECLARE v_c BIGINT UNSIGNED;
  DECLARE v_m BIGINT UNSIGNED;
  DECLARE v_t BIGINT UNSIGNED;
  DECLARE v_h BIGINT UNSIGNED;
  DECLARE v_subject BIGINT UNSIGNED;
  DECLARE v_account TINYINT;
  DECLARE v_credential TINYINT;
  DECLARE v_membership TINYINT;
  DECLARE v_host TINYINT;
  DECLARE v_tenant TINYINT;
  DECLARE v_member_until DATETIME(6);
  DECLARE v_now DATETIME(6);
  DECLARE v_expire DATETIME(6);
  DECLARE v_absolute DATETIME(6);
  DECLARE v_result VARCHAR(8) DEFAULT 'DENIED';
  DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_missing=TRUE;
  DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;

  START TRANSACTION;
  SET v_now = UTC_TIMESTAMP(6);
  SELECT link_id, account_revision, credential_revision, membership_revision,
         tenant_revision, host_revision, subject_revision,
         account_active, credential_active, membership_active,
         host_active, tenant_active, member_expires_utc
    INTO v_found_link, v_a, v_c, v_m, v_t, v_h, v_subject,
         v_account, v_credential, v_membership, v_host, v_tenant,
         v_member_until
    FROM c05_05_subjects
   WHERE tenant_id=p_tenant AND actor_id=p_actor FOR UPDATE;

  IF NOT v_missing AND v_found_link=p_link AND
     v_a=p_account_revision AND v_c=p_credential_revision AND
     v_m=p_membership_revision AND v_t=p_tenant_revision AND
     (p_host_revision=0 OR v_h=p_host_revision) AND
     v_account=1 AND v_credential=1 AND v_membership=1 AND
     v_host=1 AND v_tenant=1 AND v_member_until>v_now THEN

    SET v_expire = LEAST(DATE_ADD(v_now, INTERVAL 15 MINUTE),
                         v_member_until);
    SET v_absolute = LEAST(DATE_ADD(v_now, INTERVAL 8 HOUR),
                           v_member_until);
    IF v_expire>v_now THEN
      INSERT INTO c05_05_sessions (
        session_id, tenant_id, actor_id, token_digest,
        generation, subject_revision, issued_utc,
        expires_utc, absolute_expires_utc, revoked_utc
      ) VALUES (
        p_session, p_tenant, p_actor, p_digest,
        1, v_subject, v_now, v_expire, v_absolute, NULL
      );
      SET v_result='ISSUED';
    END IF;
  END IF;
  COMMIT;
  SELECT v_result AS result;
END$$

-- Only current token digest and exact server-verified tenant can validate.
-- Authorization changes permanently revoke the session rather than merely
-- denying while a fixture is suspended.
CREATE PROCEDURE c05_05_is_current(
  IN p_tenant BINARY(16), IN p_digest BINARY(32)
)
SQL SECURITY DEFINER
BEGIN
  DECLARE v_missing BOOLEAN DEFAULT FALSE;
  DECLARE v_id BINARY(16);
  DECLARE v_actor BINARY(16);
  DECLARE v_session_revision BIGINT UNSIGNED;
  DECLARE v_current_revision BIGINT UNSIGNED;
  DECLARE v_expire DATETIME(6);
  DECLARE v_absolute DATETIME(6);
  DECLARE v_revoked DATETIME(6);
  DECLARE v_a TINYINT;
  DECLARE v_c TINYINT;
  DECLARE v_m TINYINT;
  DECLARE v_h TINYINT;
  DECLARE v_t TINYINT;
  DECLARE v_member_until DATETIME(6);
  DECLARE v_now DATETIME(6);
  DECLARE v_result VARCHAR(8) DEFAULT 'DENIED';
  DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_missing=TRUE;
  DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;

  START TRANSACTION;
  SET v_now = UTC_TIMESTAMP(6);
  SELECT session_id, actor_id, subject_revision,
         expires_utc, absolute_expires_utc, revoked_utc
    INTO v_id, v_actor, v_session_revision,
         v_expire, v_absolute, v_revoked
    FROM c05_05_sessions
   WHERE tenant_id=p_tenant AND token_digest=p_digest FOR UPDATE;
  IF NOT v_missing THEN
    SELECT subject_revision, account_active, credential_active,
           membership_active, host_active, tenant_active,
           member_expires_utc
      INTO v_current_revision, v_a, v_c, v_m, v_h, v_t, v_member_until
      FROM c05_05_subjects
     WHERE tenant_id=p_tenant AND actor_id=v_actor FOR UPDATE;
    IF NOT v_missing AND v_revoked IS NULL AND
       v_now<v_expire AND v_now<v_absolute AND v_now<v_member_until AND
       v_session_revision=v_current_revision AND v_a=1 AND v_c=1 AND
       v_m=1 AND v_h=1 AND v_t=1 THEN
      SET v_result='CURRENT';
    ELSE
      UPDATE c05_05_sessions SET revoked_utc=COALESCE(revoked_utc,v_now)
       WHERE session_id=v_id;
    END IF;
  END IF;
  COMMIT;
  SELECT v_result AS result;
END$$

-- On successful rotation the old HMAC digest is REPLACED atomically. No old
-- token can validate, rotate or log out; competing rotations have one winner.
-- A historical replay is denied (no family-replay enforcement in this scope).
CREATE PROCEDURE c05_05_rotate_once(
  IN p_tenant BINARY(16), IN p_old BINARY(32), IN p_new BINARY(32)
)
SQL SECURITY DEFINER
BEGIN
  DECLARE v_missing BOOLEAN DEFAULT FALSE;
  DECLARE v_id BINARY(16);
  DECLARE v_actor BINARY(16);
  DECLARE v_session_revision BIGINT UNSIGNED;
  DECLARE v_current_revision BIGINT UNSIGNED;
  DECLARE v_expire DATETIME(6);
  DECLARE v_absolute DATETIME(6);
  DECLARE v_revoked DATETIME(6);
  DECLARE v_a TINYINT;
  DECLARE v_c TINYINT;
  DECLARE v_m TINYINT;
  DECLARE v_h TINYINT;
  DECLARE v_t TINYINT;
  DECLARE v_member_until DATETIME(6);
  DECLARE v_now DATETIME(6);
  DECLARE v_result VARCHAR(8) DEFAULT 'DENIED';
  DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_missing=TRUE;
  DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;

  START TRANSACTION;
  SET v_now = UTC_TIMESTAMP(6);
  IF p_old<>p_new THEN
    SELECT session_id, actor_id, subject_revision,
           expires_utc, absolute_expires_utc, revoked_utc
      INTO v_id, v_actor, v_session_revision,
           v_expire, v_absolute, v_revoked
      FROM c05_05_sessions
     WHERE tenant_id=p_tenant AND token_digest=p_old FOR UPDATE;
    IF NOT v_missing THEN
      SELECT subject_revision, account_active, credential_active,
             membership_active, host_active, tenant_active, member_expires_utc
        INTO v_current_revision, v_a, v_c, v_m, v_h, v_t, v_member_until
        FROM c05_05_subjects
       WHERE tenant_id=p_tenant AND actor_id=v_actor FOR UPDATE;
      IF NOT v_missing AND v_revoked IS NULL AND
         v_now<v_expire AND v_now<v_absolute AND v_now<v_member_until AND
         v_session_revision=v_current_revision AND v_a=1 AND v_c=1 AND
         v_m=1 AND v_h=1 AND v_t=1 THEN
        UPDATE c05_05_sessions
           SET token_digest=p_new,
               generation=generation+1,
               expires_utc=LEAST(DATE_ADD(v_now, INTERVAL 15 MINUTE),
                                 v_absolute,v_member_until)
         WHERE session_id=v_id;
        SET v_result='ROTATED';
      ELSE
        UPDATE c05_05_sessions SET revoked_utc=COALESCE(revoked_utc,v_now)
         WHERE session_id=v_id;
      END IF;
    END IF;
  END IF;
  COMMIT;
  SELECT v_result AS result;
END$$

-- Logout is idempotent at the HTTP boundary, but reports REVOKED only for a
-- current token belonging to this trusted tenant; no resurrection of a token.
CREATE PROCEDURE c05_05_logout_once(
  IN p_tenant BINARY(16), IN p_digest BINARY(32)
)
SQL SECURITY DEFINER
BEGIN
  DECLARE v_now DATETIME(6) DEFAULT UTC_TIMESTAMP(6);
  DECLARE v_result VARCHAR(8) DEFAULT 'DENIED';
  DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;
  START TRANSACTION;
  UPDATE c05_05_sessions
     SET revoked_utc=v_now
   WHERE tenant_id=p_tenant AND token_digest=p_digest
     AND revoked_utc IS NULL AND expires_utc>v_now
     AND absolute_expires_utc>v_now;
  IF ROW_COUNT()=1 THEN SET v_result='REVOKED'; END IF;
  COMMIT;
  SELECT v_result AS result;
END$$

DELIMITER ;
