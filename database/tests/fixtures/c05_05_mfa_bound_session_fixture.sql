-- CRECISOL C05 integration only. Apply AFTER isolated C05-05, C05-06,
-- C05-08 fixtures in c05_05_disposable, never to a production schema.
-- No raw bearer, TOTP code/seed, passkey private key, IP or mailbox stored.
-- The old C05-05 issue_once MUST NOT be granted to this limited worker.
CREATE TABLE c05_05_mfa_session_bindings (
  tenant_id BINARY(16) NOT NULL,
  actor_id BINARY(16) NOT NULL,
  challenge_id BINARY(16) NOT NULL,
  operation_id BINARY(16) NOT NULL,
  session_id BINARY(16) NOT NULL,
  bound_utc DATETIME(6) NOT NULL,
  PRIMARY KEY (challenge_id),
  UNIQUE KEY uq_c05_05_mfa_op (tenant_id,actor_id,operation_id),
  UNIQUE KEY uq_c05_05_mfa_session (session_id),
  CONSTRAINT fk_c05_05_mfa_challenge FOREIGN KEY (challenge_id)
    REFERENCES c05_08_challenges(challenge_id)
    ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT fk_c05_05_mfa_session FOREIGN KEY (session_id)
    REFERENCES c05_05_sessions(session_id)
    ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT fk_c05_05_mfa_actor FOREIGN KEY (tenant_id,actor_id)
    REFERENCES c05_05_subjects(tenant_id,actor_id)
    ON DELETE RESTRICT ON UPDATE RESTRICT
) ENGINE=InnoDB;

DELIMITER //
-- Real test-only session HMAC issuance: the MFA proof MUST have been verified
-- cryptographically and COMMITTED by C05-08. This routine rechecks the exact
-- freshly consumed challenge and identity revisions under the SAME InnoDB
-- transaction that inserts the session and unique MFA operation binding.
CREATE PROCEDURE c05_05_issue_after_mfa_once(
 IN p_tenant BINARY(16), IN p_actor BINARY(16), IN p_link BINARY(16),
 IN p_account BIGINT UNSIGNED, IN p_credential BIGINT UNSIGNED,
 IN p_member BIGINT UNSIGNED, IN p_tenant_rev BIGINT UNSIGNED,
 IN p_host BIGINT UNSIGNED, IN p_factor BINARY(16),
 IN p_challenge BINARY(16), IN p_operation BINARY(16),
 IN p_session BINARY(16), IN p_token_digest BINARY(32)
)
SQL SECURITY DEFINER
proc:BEGIN
 DECLARE v_missing BOOL DEFAULT FALSE;
 DECLARE v_link BINARY(16);
 DECLARE v_a BIGINT UNSIGNED;
 DECLARE v_c BIGINT UNSIGNED;
 DECLARE v_m BIGINT UNSIGNED;
 DECLARE v_t BIGINT UNSIGNED;
 DECLARE v_h BIGINT UNSIGNED;
 DECLARE v_sub BIGINT UNSIGNED;
 DECLARE v_account TINYINT;
 DECLARE v_credential TINYINT;
 DECLARE v_member TINYINT;
 DECLARE v_tenant TINYINT;
 DECLARE v_host TINYINT;
 DECLARE v_until DATETIME(6);
 DECLARE v_global BIGINT UNSIGNED;
 DECLARE v_global_active TINYINT;
 DECLARE v_realm VARCHAR(12);
 DECLARE v_mail_active TINYINT;
 DECLARE v_mail_rev BIGINT UNSIGNED;
 DECLARE v_factor_state VARCHAR(8);
 DECLARE v_factor_counter BIGINT;
 DECLARE v_challenge_state VARCHAR(8);
 DECLARE v_kind VARCHAR(8);
 DECLARE v_risk VARCHAR(24);
 DECLARE v_op BINARY(16);
 DECLARE v_challenge_factor BINARY(16);
 DECLARE v_challenge_link BINARY(16);
 DECLARE v_ca BIGINT UNSIGNED;
 DECLARE v_cc BIGINT UNSIGNED;
 DECLARE v_cm BIGINT UNSIGNED;
 DECLARE v_ct BIGINT UNSIGNED;
 DECLARE v_ch BIGINT UNSIGNED;
 DECLARE v_cs BIGINT UNSIGNED;
 DECLARE v_mail_ch BIGINT UNSIGNED;
 DECLARE v_expires DATETIME(6);
 DECLARE v_consumed DATETIME(6);
 DECLARE v_now DATETIME(6);
 DECLARE v_session_until DATETIME(6);
 DECLARE v_absolute_until DATETIME(6);
 DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_missing=TRUE;
 DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;

 IF p_session IS NULL OR p_factor IS NULL OR p_operation IS NULL OR
    p_challenge IS NULL OR p_host=0 OR
    p_session=REPEAT(X'00',16) OR p_factor=REPEAT(X'00',16)
 THEN SELECT 'DENIED' AS result; LEAVE proc; END IF;

 START TRANSACTION;
 SET v_now=UTC_TIMESTAMP(6);
 SELECT link_id,account_revision,credential_revision,
        membership_revision,tenant_revision,host_revision,
        subject_revision,account_active,credential_active,
        membership_active,host_active,tenant_active,member_expires_utc
   INTO v_link,v_a,v_c,v_m,v_t,v_h,v_sub,
        v_account,v_credential,v_member,v_host,v_tenant,v_until
   FROM c05_05_subjects
  WHERE tenant_id=p_tenant AND actor_id=p_actor FOR UPDATE;
 IF v_missing OR v_link<>p_link OR v_a<>p_account OR
    v_c<>p_credential OR v_m<>p_member OR v_t<>p_tenant_rev OR
    v_h<>p_host OR v_account<>1 OR v_credential<>1 OR
    v_member<>1 OR v_host<>1 OR v_tenant<>1 OR v_until<=v_now
 THEN ROLLBACK; SELECT 'DENIED' AS result; LEAVE proc; END IF;

 SELECT credential_revision,credential_active,realm
   INTO v_global,v_global_active,v_realm
   FROM c05_06_credentials WHERE actor_id=p_actor FOR SHARE;
 IF v_missing OR v_global<>v_c OR v_global_active<>1 OR
    v_realm<>'TENANT'
 THEN ROLLBACK; SELECT 'DENIED' AS result; LEAVE proc; END IF;

 SELECT verified,mailbox_revision INTO v_mail_active,v_mail_rev
   FROM c05_06_mailboxes
  WHERE tenant_id=p_tenant AND actor_id=p_actor FOR SHARE;
 IF v_missing OR v_mail_active<>1
 THEN ROLLBACK; SELECT 'DENIED' AS result; LEAVE proc; END IF;

 SELECT state,last_counter INTO v_factor_state,v_factor_counter
   FROM c05_08_factors WHERE factor_id=p_factor
    AND tenant_id=p_tenant AND actor_id=p_actor FOR SHARE;
 IF v_missing OR v_factor_state<>'ACTIVE' OR v_factor_counter<0 OR
    EXISTS(SELECT 1 FROM c05_08_factors
           WHERE replaces_id=p_factor AND state='PENDING')
 THEN ROLLBACK; SELECT 'DENIED' AS result; LEAVE proc; END IF;

 SELECT state,challenge_kind,risk_action,operation_id,factor_id,link_id,
        account_revision,credential_revision,membership_revision,
        tenant_revision,host_revision,subject_revision,mailbox_revision,
        expires_utc,consumed_utc
   INTO v_challenge_state,v_kind,v_risk,v_op,v_challenge_factor,
        v_challenge_link,v_ca,v_cc,v_cm,v_ct,v_ch,v_cs,v_mail_ch,
        v_expires,v_consumed
   FROM c05_08_challenges
  WHERE challenge_id=p_challenge AND tenant_id=p_tenant
    AND actor_id=p_actor AND factor_id=p_factor FOR UPDATE;
 IF v_missing OR v_challenge_state<>'CONSUMED' OR
    v_kind<>'STEPUP' OR v_risk<>'TENANT_SENSITIVE' OR
    v_op<>p_operation OR v_challenge_factor<>p_factor OR
    v_challenge_link<>p_link OR
    v_ca<>v_a OR v_cc<>v_c OR v_cm<>v_m OR v_ct<>v_t OR
    v_ch<>v_h OR v_cs<>v_sub OR v_mail_ch<>v_mail_rev OR
    v_expires<=v_now OR v_consumed IS NULL OR
    v_consumed>v_now OR v_consumed<DATE_SUB(v_now,INTERVAL 60 SECOND) OR
    EXISTS(SELECT 1 FROM c05_05_mfa_session_bindings
            WHERE challenge_id=p_challenge OR
                  (tenant_id=p_tenant AND actor_id=p_actor
                   AND operation_id=p_operation))
 THEN ROLLBACK; SELECT 'DENIED' AS result; LEAVE proc; END IF;

 SET v_session_until=LEAST(DATE_ADD(v_now,INTERVAL 15 MINUTE),v_until);
 SET v_absolute_until=LEAST(DATE_ADD(v_now,INTERVAL 8 HOUR),v_until);
 IF v_session_until<=v_now OR v_absolute_until<v_session_until
 THEN ROLLBACK; SELECT 'DENIED' AS result; LEAVE proc; END IF;

 INSERT INTO c05_05_sessions
   (session_id,tenant_id,actor_id,token_digest,generation,
    subject_revision,issued_utc,expires_utc,absolute_expires_utc,revoked_utc)
 VALUES(p_session,p_tenant,p_actor,p_token_digest,1,
        v_sub,v_now,v_session_until,v_absolute_until,NULL);
 INSERT INTO c05_05_mfa_session_bindings
   (tenant_id,actor_id,challenge_id,operation_id,session_id,bound_utc)
 VALUES(p_tenant,p_actor,p_challenge,p_operation,p_session,v_now);
 COMMIT;
 SELECT 'ISSUED' AS result;
END//
DELIMITER ;
