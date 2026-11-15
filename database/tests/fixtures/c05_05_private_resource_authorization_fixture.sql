-- CRECISOL C05: disposable SQL 8.4 integration only. Load AFTER
-- C05-05 sessions, C05-06 recovery, C05-08 MFA and bound-session fixtures.
-- NOT a migration or operational private endpoint. All rows are synthetic.
-- SQL owns identity/tenant/branch/resource/owner authorization at READ time.
CREATE TABLE c05_05_private_branches (
  tenant_id BINARY(16) NOT NULL,
  branch_id BINARY(16) NOT NULL,
  lifecycle ENUM('ACTIVE','SUSPENDED','REVOKED') NOT NULL,
  revision BIGINT UNSIGNED NOT NULL,
  PRIMARY KEY (tenant_id,branch_id),
  CONSTRAINT ck_c05_priv_branch_revision CHECK (revision>0)
) ENGINE=InnoDB;

CREATE TABLE c05_05_private_resources (
  tenant_id BINARY(16) NOT NULL,
  resource_id BINARY(16) NOT NULL,
  branch_id BINARY(16) NOT NULL,
  owner_actor_id BINARY(16) NOT NULL,
  lifecycle ENUM('ACTIVE','SUSPENDED','DELETED') NOT NULL,
  revision BIGINT UNSIGNED NOT NULL,
  external_key VARCHAR(100) NOT NULL,
  PRIMARY KEY (tenant_id,resource_id),
  KEY ix_c05_priv_resource_branch (tenant_id,branch_id),
  CONSTRAINT fk_c05_priv_resource_branch FOREIGN KEY (tenant_id,branch_id)
    REFERENCES c05_05_private_branches (tenant_id,branch_id)
    ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT fk_c05_priv_resource_owner FOREIGN KEY (tenant_id,owner_actor_id)
    REFERENCES c05_05_subjects (tenant_id,actor_id)
    ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT ck_c05_priv_resource_revision CHECK (revision>0),
  CONSTRAINT ck_c05_priv_resource_reference CHECK (CHAR_LENGTH(external_key)>0)
) ENGINE=InnoDB;

CREATE TABLE c05_05_private_grants (
  tenant_id BINARY(16) NOT NULL,
  actor_id BINARY(16) NOT NULL,
  branch_id BINARY(16) NOT NULL,
  action_key VARCHAR(120) NOT NULL,
  access_scope ENUM('OWNER','BRANCH') NOT NULL,
  lifecycle ENUM('ACTIVE','SUSPENDED','REVOKED') NOT NULL,
  revision BIGINT UNSIGNED NOT NULL,
  issued_utc DATETIME(6) NOT NULL,
  expires_utc DATETIME(6) NOT NULL,
  PRIMARY KEY (tenant_id,actor_id,branch_id,action_key),
  CONSTRAINT fk_c05_priv_grant_actor FOREIGN KEY (tenant_id,actor_id)
    REFERENCES c05_05_subjects (tenant_id,actor_id)
    ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT fk_c05_priv_grant_branch FOREIGN KEY (tenant_id,branch_id)
    REFERENCES c05_05_private_branches (tenant_id,branch_id)
    ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT ck_c05_priv_grant_revision CHECK (revision>0),
  CONSTRAINT ck_c05_priv_grant_window CHECK (expires_utc>issued_utc)
) ENGINE=InnoDB;

DELIMITER //
-- Single transaction: bearer HMAC -> same tenant + valid MFA-bound session ->
-- fresh subject, realm, mailbox -> branch/resource metadata and exact grant.
-- Actor is derived FROM THE SESSION. Resource ID is an UNTRUSTED selector.
-- Action must be a backend-owned route policy; never a caller claim/header.
-- No TOCTOU between authorization and the private reference read.
CREATE PROCEDURE c05_05_read_private_resource(
 IN p_tenant BINARY(16),
 IN p_host_rev BIGINT UNSIGNED,
 IN p_token_digest BINARY(32),
 IN p_resource BINARY(16),
 IN p_action VARCHAR(120)
)
SQL SECURITY DEFINER
proc:BEGIN
 DECLARE v_missing BOOL DEFAULT FALSE;
 DECLARE v_now DATETIME(6);
 DECLARE v_sid BINARY(16);
 DECLARE v_actor BINARY(16);
 DECLARE v_session_rev BIGINT UNSIGNED;
 DECLARE v_session_exp DATETIME(6);
 DECLARE v_session_abs DATETIME(6);
 DECLARE v_session_revoke DATETIME(6);
 DECLARE v_link BINARY(16);
 DECLARE v_subject_rev BIGINT UNSIGNED;
 DECLARE v_account_rev BIGINT UNSIGNED;
 DECLARE v_cred_rev BIGINT UNSIGNED;
 DECLARE v_host_rev BIGINT UNSIGNED;
 DECLARE v_subject_a TINYINT;
 DECLARE v_subject_c TINYINT;
 DECLARE v_subject_m TINYINT;
 DECLARE v_subject_t TINYINT;
 DECLARE v_subject_h TINYINT;
 DECLARE v_subject_exp DATETIME(6);
 DECLARE v_global_rev BIGINT UNSIGNED;
 DECLARE v_global_active TINYINT;
 DECLARE v_realm VARCHAR(12);
 DECLARE v_mail_active TINYINT;
 DECLARE v_branch BINARY(16);
 DECLARE v_owner BINARY(16);
 DECLARE v_branch_state VARCHAR(12);
 DECLARE v_resource_state VARCHAR(12);
 DECLARE v_resource_revision BIGINT UNSIGNED;
 DECLARE v_reference VARCHAR(100);
 DECLARE v_scope VARCHAR(12);
 DECLARE v_grant_state VARCHAR(12);
 DECLARE v_grant_rev BIGINT UNSIGNED;
 DECLARE v_grant_issued DATETIME(6);
 DECLARE v_grant_expires DATETIME(6);
 DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_missing=TRUE;
 DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;

 IF p_host_rev IS NULL OR p_host_rev=0 OR p_action IS NULL OR
    CHAR_LENGTH(p_action)<3 OR CHAR_LENGTH(p_action)>120 OR
    p_resource IS NULL OR p_resource=REPEAT(X'00',16) OR
    p_token_digest IS NULL OR
    p_token_digest=REPEAT(X'00',32)
 THEN SELECT 'DENIED' AS result, CAST(NULL AS CHAR(100)) AS external_key;
      LEAVE proc; END IF;

 START TRANSACTION;
 SET v_now=UTC_TIMESTAMP(6);
 SELECT session_id,actor_id,subject_revision,expires_utc,
        absolute_expires_utc,revoked_utc
   INTO v_sid,v_actor,v_session_rev,v_session_exp,v_session_abs,v_session_revoke
   FROM c05_05_sessions
  WHERE tenant_id=p_tenant AND token_digest=p_token_digest FOR SHARE;
 IF v_missing OR v_session_revoke IS NOT NULL OR
    v_session_exp<=v_now OR v_session_abs<=v_now
 THEN ROLLBACK; SELECT 'DENIED' AS result,
      CAST(NULL AS CHAR(100)) AS external_key; LEAVE proc; END IF;

 SELECT link_id,subject_revision,account_revision,credential_revision,
        host_revision,account_active,credential_active,membership_active,
        tenant_active,host_active,member_expires_utc
   INTO v_link,v_subject_rev,v_account_rev,v_cred_rev,v_host_rev,
        v_subject_a,v_subject_c,v_subject_m,v_subject_t,v_subject_h,v_subject_exp
   FROM c05_05_subjects
  WHERE tenant_id=p_tenant AND actor_id=v_actor FOR SHARE;
 IF v_missing OR v_subject_rev<>v_session_rev OR v_host_rev<>p_host_rev OR
    v_subject_a<>1 OR v_subject_c<>1 OR v_subject_m<>1 OR
    v_subject_t<>1 OR v_subject_h<>1 OR v_subject_exp<=v_now
 THEN ROLLBACK; SELECT 'DENIED' AS result,
      CAST(NULL AS CHAR(100)) AS external_key; LEAVE proc; END IF;

 SELECT credential_revision,credential_active,realm
   INTO v_global_rev,v_global_active,v_realm
   FROM c05_06_credentials WHERE actor_id=v_actor FOR SHARE;
 IF v_missing OR v_global_active<>1 OR
    v_global_rev<>v_cred_rev OR v_realm<>'TENANT'
 THEN ROLLBACK; SELECT 'DENIED' AS result,
      CAST(NULL AS CHAR(100)) AS external_key; LEAVE proc; END IF;

 SELECT verified INTO v_mail_active FROM c05_06_mailboxes
   WHERE tenant_id=p_tenant AND actor_id=v_actor FOR SHARE;
 IF v_missing OR v_mail_active<>1 OR NOT EXISTS (
   SELECT 1 FROM c05_05_mfa_session_bindings bind
    JOIN c05_08_challenges challenge ON challenge.challenge_id=bind.challenge_id
    JOIN c05_08_factors factor ON factor.factor_id=challenge.factor_id
    WHERE bind.session_id=v_sid AND bind.tenant_id=p_tenant
      AND bind.actor_id=v_actor AND bind.operation_id=challenge.operation_id
      AND challenge.tenant_id=p_tenant AND challenge.actor_id=v_actor
      AND challenge.challenge_kind='STEPUP'
      AND challenge.risk_action='TENANT_SENSITIVE'
      AND challenge.state='CONSUMED'
      AND factor.tenant_id=p_tenant AND factor.actor_id=v_actor
      AND factor.state='ACTIVE'
 )
 THEN ROLLBACK; SELECT 'DENIED' AS result,
      CAST(NULL AS CHAR(100)) AS external_key; LEAVE proc; END IF;

 SELECT r.branch_id,r.owner_actor_id,r.lifecycle,r.revision,
        r.external_key,b.lifecycle
   INTO v_branch,v_owner,v_resource_state,v_resource_revision,
        v_reference,v_branch_state
   FROM c05_05_private_resources r
   INNER JOIN c05_05_private_branches b
     ON b.tenant_id=r.tenant_id AND b.branch_id=r.branch_id
  WHERE r.tenant_id=p_tenant AND r.resource_id=p_resource FOR SHARE;
 IF v_missing OR v_branch_state<>'ACTIVE' OR v_resource_state<>'ACTIVE'
    OR v_resource_revision=0
 THEN ROLLBACK; SELECT 'DENIED' AS result,
      CAST(NULL AS CHAR(100)) AS external_key; LEAVE proc; END IF;

 SELECT access_scope,lifecycle,revision,issued_utc,expires_utc
   INTO v_scope,v_grant_state,v_grant_rev,v_grant_issued,v_grant_expires
   FROM c05_05_private_grants
  WHERE tenant_id=p_tenant AND actor_id=v_actor AND branch_id=v_branch
    AND action_key=p_action FOR SHARE;
 IF v_missing OR v_grant_state<>'ACTIVE' OR v_grant_rev=0 OR
    v_grant_issued>v_now OR v_grant_expires<=v_now OR
    (v_scope='OWNER' AND v_owner<>v_actor) OR
    v_scope NOT IN ('OWNER','BRANCH')
 THEN ROLLBACK; SELECT 'DENIED' AS result,
      CAST(NULL AS CHAR(100)) AS external_key; LEAVE proc; END IF;

 COMMIT;
 SELECT 'ALLOWED' AS result,v_reference AS external_key;
END//
DELIMITER ;
