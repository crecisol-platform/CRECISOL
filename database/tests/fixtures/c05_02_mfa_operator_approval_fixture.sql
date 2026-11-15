-- CRECISOL C05-P0-09: DISPOSABLE ONLY. Apply after c05_05_disposable schema.
-- The existing synthetic authority fixture remains a historical negative-test
-- path. This separate least-privilege path derives the approver from a REAL,
-- cryptographically proven MFA-bound session and a backend-granted action.
-- No password, raw bearer, factor seed, mail address or HTTP-supplied actor.
CREATE TABLE c05_02_mfa_approval_grants (
  tenant_id BINARY(16) NOT NULL,
  approver_actor_id BINARY(16) NOT NULL,
  action_key VARCHAR(48) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  active TINYINT(1) NOT NULL,
  grant_revision BIGINT UNSIGNED NOT NULL,
  expires_utc DATETIME(6) NOT NULL,
  PRIMARY KEY (tenant_id,approver_actor_id,action_key),
  CONSTRAINT fk_c05_mfa_grant_tenant FOREIGN KEY (tenant_id)
    REFERENCES c05_02_tenants(tenant_id)
    ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT ck_c05_mfa_grant_revision CHECK (grant_revision>0),
  CONSTRAINT ck_c05_mfa_grant_active CHECK (active IN (0,1)),
  CONSTRAINT ck_c05_mfa_grant_action CHECK (action_key='memberships.approve')
) ENGINE=InnoDB;

DELIMITER //
CREATE PROCEDURE c05_02_activate_by_mfa_approver(
  IN p_tenant BINARY(16),
  IN p_host_revision BIGINT UNSIGNED,
  IN p_candidate BINARY(16),
  IN p_approval BINARY(16),
  IN p_link BINARY(16),
  IN p_token_digest BINARY(32)
)
SQL SECURITY DEFINER
proc:BEGIN
  DECLARE v_missing BOOLEAN DEFAULT FALSE;
  DECLARE v_now DATETIME(6);
  DECLARE v_tenant_state VARCHAR(32) DEFAULT NULL;
  DECLARE v_candidate_state VARCHAR(32) DEFAULT NULL;
  DECLARE v_intent BINARY(16) DEFAULT NULL;
  DECLARE v_candidate_mail BINARY(32) DEFAULT NULL;
  DECLARE v_intent_mail BINARY(32) DEFAULT NULL;
  DECLARE v_intent_until DATETIME(6) DEFAULT NULL;
  DECLARE v_proof_state VARCHAR(16) DEFAULT NULL;
  DECLARE v_proved DATETIME(6) DEFAULT NULL;
  DECLARE v_session BINARY(16) DEFAULT NULL;
  DECLARE v_approver BINARY(16) DEFAULT NULL;
  DECLARE v_generation BIGINT UNSIGNED DEFAULT 0;
  DECLARE v_session_revision BIGINT UNSIGNED DEFAULT 0;
  DECLARE v_session_issued DATETIME(6) DEFAULT NULL;
  DECLARE v_session_until DATETIME(6) DEFAULT NULL;
  DECLARE v_revoked DATETIME(6) DEFAULT NULL;
  DECLARE v_subject_revision BIGINT UNSIGNED DEFAULT 0;
  DECLARE v_subject_host BIGINT UNSIGNED DEFAULT 0;
  DECLARE v_member_until DATETIME(6) DEFAULT NULL;
  DECLARE v_link BINARY(16) DEFAULT NULL;
  DECLARE v_account_active TINYINT DEFAULT 0;
  DECLARE v_credential_active TINYINT DEFAULT 0;
  DECLARE v_member_active TINYINT DEFAULT 0;
  DECLARE v_tenant_active TINYINT DEFAULT 0;
  DECLARE v_host_active TINYINT DEFAULT 0;
  DECLARE v_credential_revision BIGINT UNSIGNED DEFAULT 0;
  DECLARE v_global_revision BIGINT UNSIGNED DEFAULT 0;
  DECLARE v_global_active TINYINT DEFAULT 0;
  DECLARE v_realm VARCHAR(12) DEFAULT NULL;
  DECLARE v_mail_active TINYINT DEFAULT 0;
  DECLARE v_mail_revision BIGINT UNSIGNED DEFAULT 0;
  DECLARE v_challenge BINARY(16) DEFAULT NULL;
  DECLARE v_operation BINARY(16) DEFAULT NULL;
  DECLARE v_factor BINARY(16) DEFAULT NULL;
  DECLARE v_challenge_state VARCHAR(8) DEFAULT NULL;
  DECLARE v_challenge_kind VARCHAR(8) DEFAULT NULL;
  DECLARE v_challenge_risk VARCHAR(24) DEFAULT NULL;
  DECLARE v_challenge_operation BINARY(16) DEFAULT NULL;
  DECLARE v_challenge_link BINARY(16) DEFAULT NULL;
  DECLARE v_challenge_subject BIGINT UNSIGNED DEFAULT 0;
  DECLARE v_challenge_mail BIGINT UNSIGNED DEFAULT 0;
  DECLARE v_challenge_until DATETIME(6) DEFAULT NULL;
  DECLARE v_challenge_consumed DATETIME(6) DEFAULT NULL;
  DECLARE v_factor_state VARCHAR(8) DEFAULT NULL;
  DECLARE v_factor_link BINARY(16) DEFAULT NULL;
  DECLARE v_factor_counter BIGINT DEFAULT -1;
  DECLARE v_grant_active TINYINT DEFAULT 0;
  DECLARE v_grant_until DATETIME(6) DEFAULT NULL;
  DECLARE v_grant_rev BIGINT UNSIGNED DEFAULT 0;
  DECLARE v_approval_until DATETIME(6) DEFAULT NULL;
  DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_missing=TRUE;
  DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;

  START TRANSACTION;
  SET v_now=UTC_TIMESTAMP(6);
  SELECT lifecycle INTO v_tenant_state
    FROM c05_02_tenants WHERE tenant_id=p_tenant FOR SHARE;
  -- Serialize competing approvals for this candidate before checking MFA.
  SELECT lifecycle,source_intent_id,mailbox_digest
    INTO v_candidate_state,v_intent,v_candidate_mail
    FROM c05_02_candidate_accounts
    WHERE source_tenant_id=p_tenant AND actor_id=p_candidate FOR UPDATE;
  SELECT mailbox_digest,expires_at_utc INTO v_intent_mail,v_intent_until
    FROM c05_02_neutral_intents
    WHERE tenant_id=p_tenant AND intent_id=v_intent FOR SHARE;
  SELECT lifecycle,verified_utc INTO v_proof_state,v_proved
    FROM c05_02_mailbox_proofs
    WHERE tenant_id=p_tenant AND intent_id=v_intent FOR SHARE;

  -- Derive the approver from a keyed HMAC digest, NEVER an actor supplied by
  -- the browser. A current session has a unique digest per generation.
  SELECT session_id,actor_id,generation,subject_revision,issued_utc,
         expires_utc,revoked_utc
    INTO v_session,v_approver,v_generation,v_session_revision,
         v_session_issued,v_session_until,v_revoked
    FROM c05_05_disposable.c05_05_sessions
    WHERE tenant_id=p_tenant AND token_digest=p_token_digest FOR UPDATE;
  SELECT link_id,subject_revision,host_revision,member_expires_utc,
         credential_revision,account_active,credential_active,
         membership_active,host_active,tenant_active
    INTO v_link,v_subject_revision,v_subject_host,v_member_until,
         v_credential_revision,v_account_active,v_credential_active,
         v_member_active,v_host_active,v_tenant_active
    FROM c05_05_disposable.c05_05_subjects
    WHERE tenant_id=p_tenant AND actor_id=v_approver FOR SHARE;
  SELECT credential_revision,credential_active,realm
    INTO v_global_revision,v_global_active,v_realm
    FROM c05_05_disposable.c05_06_credentials
    WHERE actor_id=v_approver FOR SHARE;
  SELECT verified,mailbox_revision INTO v_mail_active,v_mail_revision
    FROM c05_05_disposable.c05_06_mailboxes
    WHERE tenant_id=p_tenant AND actor_id=v_approver FOR SHARE;
  SELECT challenge_id,operation_id INTO v_challenge,v_operation
    FROM c05_05_disposable.c05_05_mfa_session_bindings
    WHERE tenant_id=p_tenant AND actor_id=v_approver
      AND session_id=v_session FOR SHARE;
  SELECT factor_id,state,challenge_kind,risk_action,operation_id,link_id,
         subject_revision,mailbox_revision,expires_utc,consumed_utc
    INTO v_factor,v_challenge_state,v_challenge_kind,v_challenge_risk,
         v_challenge_operation,v_challenge_link,v_challenge_subject,
         v_challenge_mail,v_challenge_until,v_challenge_consumed
    FROM c05_05_disposable.c05_08_challenges
    WHERE tenant_id=p_tenant AND actor_id=v_approver
      AND challenge_id=v_challenge FOR SHARE;
  SELECT state,link_id,last_counter
    INTO v_factor_state,v_factor_link,v_factor_counter
    FROM c05_05_disposable.c05_08_factors
    WHERE tenant_id=p_tenant AND actor_id=v_approver
      AND factor_id=v_factor FOR SHARE;

  SELECT active,grant_revision,expires_utc
    INTO v_grant_active,v_grant_rev,v_grant_until
    FROM c05_02_mfa_approval_grants
    WHERE tenant_id=p_tenant AND approver_actor_id=v_approver
      AND action_key='memberships.approve' FOR SHARE;

  IF v_missing OR p_tenant IS NULL OR p_candidate IS NULL OR
     p_approval IS NULL OR p_link IS NULL OR
     p_approval=REPEAT(X'00',16) OR p_link=REPEAT(X'00',16) OR
     p_candidate=v_approver OR p_host_revision<1 OR
     v_tenant_state<>'ACTIVE' OR v_candidate_state<>'PENDING_MEMBERSHIP' OR
     v_intent_until<=v_now OR v_intent_mail<>v_candidate_mail OR
     v_proof_state<>'VERIFIED' OR v_proved IS NULL OR
     v_proved>v_now OR v_proved<=DATE_SUB(v_now,INTERVAL 30 MINUTE) OR
     v_session IS NULL OR v_revoked IS NOT NULL OR v_generation<1 OR
     v_session_until<=v_now OR v_session_issued>v_now OR
     v_session_issued<=DATE_SUB(v_now,INTERVAL 5 MINUTE) OR
     v_session_revision<>v_subject_revision OR
     v_subject_host<>p_host_revision OR v_member_until<=v_now OR
     v_account_active<>1 OR v_credential_active<>1 OR
     v_member_active<>1 OR v_host_active<>1 OR v_tenant_active<>1 OR
     v_global_active<>1 OR v_global_revision<>v_credential_revision OR
     v_realm<>'TENANT' OR v_mail_active<>1 OR
     v_challenge IS NULL OR v_factor IS NULL OR
     v_challenge_state<>'CONSUMED' OR v_challenge_kind<>'STEPUP' OR
     v_challenge_risk<>'TENANT_SENSITIVE' OR
     v_challenge_operation<>v_operation OR v_challenge_link<>v_link OR
     v_challenge_subject<>v_subject_revision OR
     v_challenge_mail<>v_mail_revision OR
     v_challenge_until<=v_now OR v_challenge_consumed IS NULL OR
     v_challenge_consumed>v_now OR
     v_challenge_consumed<=DATE_SUB(v_now,INTERVAL 60 SECOND) OR
     v_factor_state<>'ACTIVE' OR v_factor_link<>v_link OR
     v_factor_counter<0 OR
     v_grant_active<>1 OR v_grant_rev<1 OR v_grant_until<=v_now OR
     EXISTS(SELECT 1 FROM c05_05_disposable.c05_08_factors
            WHERE tenant_id=p_tenant AND actor_id=v_approver
              AND replaces_id=v_factor AND state='PENDING')
  THEN
    ROLLBACK;
    SELECT 'DENIED' AS result;
    LEAVE proc;
  END IF;

  SET v_approval_until=LEAST(
    DATE_ADD(v_now,INTERVAL 5 MINUTE),v_grant_until,
    v_session_until,v_member_until);
  IF v_approval_until<=v_now THEN
    ROLLBACK;
    SELECT 'DENIED' AS result;
    LEAVE proc;
  END IF;

  -- Approval/order/activation commit together under the same session, factor,
  -- tenant and candidate locks. An external mutation waits or causes denial.
  INSERT INTO c05_02_member_authorities
    (tenant_id,approver_actor_id,lifecycle,session_revision,mfa_revision,
     authorized_utc,expires_utc)
  VALUES(p_tenant,v_approver,'ACTIVE',v_generation,v_factor_counter+1,
         v_now,v_approval_until)
  ON DUPLICATE KEY UPDATE
    lifecycle='ACTIVE',session_revision=v_generation,
    mfa_revision=v_factor_counter+1,
    authorized_utc=v_now,expires_utc=v_approval_until;
  INSERT INTO c05_02_member_approval_orders
    (tenant_id,approval_id,candidate_actor_id,approver_actor_id,
     expected_session_revision,expected_mfa_revision,lifecycle,
     issued_utc,expires_utc,consumed_utc)
  VALUES(p_tenant,p_approval,p_candidate,v_approver,
         v_generation,v_factor_counter+1,'CONSUMED',
         v_now,v_approval_until,v_now);
  INSERT INTO c05_02_approved_memberships
    (tenant_id,actor_id,link_id,lifecycle,revision,approved_by,approval_id,
     granted_utc,changed_utc,expires_utc)
  VALUES(p_tenant,p_candidate,p_link,'ACTIVE',1,v_approver,p_approval,
         v_now,v_now,v_approval_until);
  UPDATE c05_02_candidate_accounts SET lifecycle='ACTIVE'
    WHERE source_tenant_id=p_tenant AND actor_id=p_candidate
      AND lifecycle='PENDING_MEMBERSHIP';
  IF ROW_COUNT()<>1 THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C05 approval CAS mismatch';
  END IF;
  COMMIT;
  SELECT 'ACTIVATED' AS result;
END//
DELIMITER ;
