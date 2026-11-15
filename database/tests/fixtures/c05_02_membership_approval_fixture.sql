-- CRECISOL C05 integration-only disposable MySQL 8.4 approval boundary.
-- NOT a production migration. Only fixture setup may seed proof of an
-- independently authenticated approver with current MFA and session.
-- Verified email or PHC alone NEVER grants a membership, role or session.
ALTER TABLE c05_02_candidate_accounts
    MODIFY lifecycle ENUM('PENDING_MEMBERSHIP','ACTIVE','REVOKED')
      NOT NULL DEFAULT 'PENDING_MEMBERSHIP',
    ADD UNIQUE KEY uq_c05_02_candidate_actor_source (actor_id,source_tenant_id);

CREATE TABLE c05_02_member_authorities (
    tenant_id BINARY(16) NOT NULL,
    approver_actor_id BINARY(16) NOT NULL,
    lifecycle ENUM('ACTIVE','REVOKED') NOT NULL,
    session_revision BIGINT UNSIGNED NOT NULL,
    mfa_revision BIGINT UNSIGNED NOT NULL,
    authorized_utc DATETIME(6) NOT NULL,
    expires_utc DATETIME(6) NOT NULL,
    PRIMARY KEY (tenant_id,approver_actor_id),
    CONSTRAINT fk_c05_02_authority_tenant FOREIGN KEY (tenant_id)
      REFERENCES c05_02_tenants (tenant_id)
      ON DELETE RESTRICT ON UPDATE RESTRICT,
    CHECK (session_revision>0 AND mfa_revision>0),
    CHECK (expires_utc>authorized_utc)
) ENGINE=InnoDB;

CREATE TABLE c05_02_member_approval_orders (
    tenant_id BINARY(16) NOT NULL,
    approval_id BINARY(16) NOT NULL,
    candidate_actor_id BINARY(16) NOT NULL,
    approver_actor_id BINARY(16) NOT NULL,
    expected_session_revision BIGINT UNSIGNED NOT NULL,
    expected_mfa_revision BIGINT UNSIGNED NOT NULL,
    lifecycle ENUM('PENDING','CONSUMED','REVOKED') NOT NULL DEFAULT 'PENDING',
    issued_utc DATETIME(6) NOT NULL,
    expires_utc DATETIME(6) NOT NULL,
    consumed_utc DATETIME(6) NULL,
    PRIMARY KEY (tenant_id,approval_id),
    KEY ix_c05_02_pending_actor (tenant_id,candidate_actor_id,lifecycle),
    CONSTRAINT fk_c05_02_order_candidate FOREIGN KEY (candidate_actor_id,tenant_id)
      REFERENCES c05_02_candidate_accounts (actor_id,source_tenant_id)
      ON DELETE RESTRICT ON UPDATE RESTRICT,
    CONSTRAINT fk_c05_02_order_authorizer FOREIGN KEY (tenant_id,approver_actor_id)
      REFERENCES c05_02_member_authorities (tenant_id,approver_actor_id)
      ON DELETE RESTRICT ON UPDATE RESTRICT,
    CHECK (candidate_actor_id<>approver_actor_id),
    CHECK (expected_session_revision>0 AND expected_mfa_revision>0),
    CHECK (expires_utc>issued_utc),
    CHECK ((lifecycle='CONSUMED' AND consumed_utc IS NOT NULL)
        OR (lifecycle<>'CONSUMED' AND consumed_utc IS NULL))
) ENGINE=InnoDB;

CREATE TABLE c05_02_approved_memberships (
    tenant_id BINARY(16) NOT NULL,
    actor_id BINARY(16) NOT NULL,
    link_id BINARY(16) NOT NULL,
    lifecycle ENUM('ACTIVE','REVOKED') NOT NULL,
    revision BIGINT UNSIGNED NOT NULL,
    approved_by BINARY(16) NOT NULL,
    approval_id BINARY(16) NOT NULL,
    granted_utc DATETIME(6) NOT NULL,
    changed_utc DATETIME(6) NOT NULL,
    expires_utc DATETIME(6) NOT NULL,
    PRIMARY KEY (tenant_id,actor_id),
    UNIQUE KEY uq_c05_02_membership_link (tenant_id,link_id),
    UNIQUE KEY uq_c05_02_membership_approval (tenant_id,approval_id),
    CONSTRAINT fk_c05_02_membership_candidate FOREIGN KEY (actor_id,tenant_id)
      REFERENCES c05_02_candidate_accounts (actor_id,source_tenant_id)
      ON DELETE RESTRICT ON UPDATE RESTRICT,
    CONSTRAINT fk_c05_02_membership_order FOREIGN KEY (tenant_id,approval_id)
      REFERENCES c05_02_member_approval_orders (tenant_id,approval_id)
      ON DELETE RESTRICT ON UPDATE RESTRICT,
    CONSTRAINT fk_c05_02_membership_authority FOREIGN KEY (tenant_id,approved_by)
      REFERENCES c05_02_member_authorities (tenant_id,approver_actor_id)
      ON DELETE RESTRICT ON UPDATE RESTRICT,
    CHECK (revision>0 AND expires_utc>granted_utc AND changed_utc>=granted_utc)
) ENGINE=InnoDB;

DELIMITER //
-- An EXECUTE-only backend worker may consume a pre-existing authorization.
-- NO operation issues approval evidence or inserts the signing authority.
-- SQL independently rechecks tenant/candidate/mailbox/approver/expiry under
-- locks. An approval from A cannot activate B; candidate cannot self-approve.
CREATE PROCEDURE c05_02_activate_approved_candidate(
    IN p_tenant BINARY(16), IN p_actor BINARY(16),
    IN p_approval BINARY(16), IN p_link BINARY(16)
)
SQL SECURITY DEFINER
BEGIN
    DECLARE v_tenant_state VARCHAR(32) DEFAULT NULL;
    DECLARE v_candidate_state VARCHAR(32) DEFAULT NULL;
    DECLARE v_source_intent BINARY(16) DEFAULT NULL;
    DECLARE v_verified_state VARCHAR(16) DEFAULT NULL;
    DECLARE v_verified_utc DATETIME(6) DEFAULT NULL;
    DECLARE v_authorizer BINARY(16) DEFAULT NULL;
    DECLARE v_order_state VARCHAR(16) DEFAULT NULL;
    DECLARE v_order_issued DATETIME(6) DEFAULT NULL;
    DECLARE v_order_expiry DATETIME(6) DEFAULT NULL;
    DECLARE v_expected_session BIGINT UNSIGNED DEFAULT 0;
    DECLARE v_expected_mfa BIGINT UNSIGNED DEFAULT 0;
    DECLARE v_authority_state VARCHAR(16) DEFAULT NULL;
    DECLARE v_session BIGINT UNSIGNED DEFAULT 0;
    DECLARE v_mfa BIGINT UNSIGNED DEFAULT 0;
    DECLARE v_authority_expiry DATETIME(6) DEFAULT NULL;
    DECLARE v_now DATETIME(6);
    DECLARE v_admitted INT DEFAULT 0;
    DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;

    START TRANSACTION;
    SET v_now=UTC_TIMESTAMP(6);
    SELECT lifecycle INTO v_tenant_state FROM c05_02_tenants
      WHERE tenant_id=p_tenant FOR SHARE;
    SELECT lifecycle,source_intent_id INTO v_candidate_state,v_source_intent
      FROM c05_02_candidate_accounts
      WHERE actor_id=p_actor AND source_tenant_id=p_tenant FOR UPDATE;
    IF v_source_intent IS NOT NULL THEN
        SELECT lifecycle,verified_utc INTO v_verified_state,v_verified_utc
          FROM c05_02_mailbox_proofs
          WHERE tenant_id=p_tenant AND intent_id=v_source_intent FOR SHARE;
    END IF;
    SELECT approver_actor_id,lifecycle,expected_session_revision,
           expected_mfa_revision,issued_utc,expires_utc
      INTO v_authorizer,v_order_state,v_expected_session,
           v_expected_mfa,v_order_issued,v_order_expiry
      FROM c05_02_member_approval_orders
      WHERE tenant_id=p_tenant AND candidate_actor_id=p_actor
        AND approval_id=p_approval FOR UPDATE;
    IF v_authorizer IS NOT NULL THEN
        SELECT lifecycle,session_revision,mfa_revision,expires_utc
          INTO v_authority_state,v_session,v_mfa,v_authority_expiry
          FROM c05_02_member_authorities
         WHERE tenant_id=p_tenant AND approver_actor_id=v_authorizer FOR SHARE;
    END IF;

    IF p_actor IS NOT NULL AND p_actor<>REPEAT(X'00',16)
      AND p_link IS NOT NULL AND p_link<>REPEAT(X'00',16)
      AND v_tenant_state='ACTIVE'
      AND v_candidate_state='PENDING_MEMBERSHIP'
      AND v_verified_state='VERIFIED'
      AND v_verified_utc IS NOT NULL
      AND v_verified_utc<=v_now
      AND v_verified_utc>DATE_SUB(v_now,INTERVAL 30 MINUTE)
      AND v_authorizer IS NOT NULL AND v_authorizer<>p_actor
      AND v_order_state='PENDING'
      AND v_order_issued<=v_now AND v_order_issued>DATE_SUB(v_now,INTERVAL 10 MINUTE)
      AND v_order_expiry>v_now AND v_order_expiry<=DATE_ADD(v_now,INTERVAL 10 MINUTE)
      AND v_authority_state='ACTIVE' AND v_authority_expiry>v_now
      AND v_session>0 AND v_mfa>0
      AND v_session=v_expected_session AND v_mfa=v_expected_mfa
    THEN
        INSERT INTO c05_02_approved_memberships
          (tenant_id,actor_id,link_id,lifecycle,revision,approved_by,approval_id,
           granted_utc,changed_utc,expires_utc)
        VALUES(p_tenant,p_actor,p_link,'ACTIVE',1,v_authorizer,p_approval,
               v_now,v_now,LEAST(DATE_ADD(v_now,INTERVAL 1 DAY),v_authority_expiry));
        UPDATE c05_02_candidate_accounts SET lifecycle='ACTIVE'
         WHERE actor_id=p_actor AND source_tenant_id=p_tenant
           AND lifecycle='PENDING_MEMBERSHIP';
        UPDATE c05_02_member_approval_orders
           SET lifecycle='CONSUMED',consumed_utc=v_now
         WHERE tenant_id=p_tenant AND approval_id=p_approval
           AND lifecycle='PENDING';
        SET v_admitted=1;
    END IF;
    COMMIT;
    SELECT IF(v_admitted=1,'ACTIVATED','DENIED') AS result;
END//
DELIMITER ;
