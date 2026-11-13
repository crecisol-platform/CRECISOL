-- CRECISOL C05-02 ONLY: test-only atomic issuance + issuer-bound claim.
-- Requires existing c05_02_invitation_atomic_fixture.sql AND
-- c05_02_issuer_scope_atomic_fixture.sql in a disposable MySQL 8.4 schema.
-- NOT a production migration; no account/session/role/membership creation.
CREATE TABLE c05_02_invite_scope_bindings (
  tenant_id BINARY(16) NOT NULL,
  invitation_id BINARY(16) NOT NULL,
  issuer_id BINARY(16) NOT NULL,
  issuer_link_id BINARY(16) NOT NULL,
  branch_id BINARY(16) NOT NULL,
  resource_id BINARY(16) NOT NULL,
  action_key VARCHAR(120) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  scope_mode ENUM('OWNER_ONLY','BRANCH_WIDE') NOT NULL,
  account_revision BIGINT UNSIGNED NOT NULL,
  session_revision BIGINT UNSIGNED NOT NULL,
  link_revision BIGINT UNSIGNED NOT NULL,
  host_revision BIGINT UNSIGNED NOT NULL,
  catalog_revision BIGINT UNSIGNED NOT NULL,
  branch_revision BIGINT UNSIGNED NOT NULL,
  resource_revision BIGINT UNSIGNED NOT NULL,
  grant_revision BIGINT UNSIGNED NOT NULL,
  PRIMARY KEY (tenant_id, invitation_id),
  CONSTRAINT fk_c05_02_bind_invite FOREIGN KEY (tenant_id, invitation_id)
    REFERENCES c05_02_invitations(tenant_id, invitation_id)
    ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT fk_c05_02_bind_issuer FOREIGN KEY (tenant_id, issuer_id)
    REFERENCES c05_02_issuers(tenant_id, actor_id)
    ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT fk_c05_02_bind_branch FOREIGN KEY (tenant_id, branch_id)
    REFERENCES c05_02_auth_branches(tenant_id, branch_id)
    ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT fk_c05_02_bind_resource FOREIGN KEY (tenant_id, resource_id)
    REFERENCES c05_02_auth_resources(tenant_id, resource_id)
    ON DELETE RESTRICT ON UPDATE RESTRICT,
  CHECK (account_revision > 0 AND session_revision > 0
     AND link_revision > 0 AND host_revision > 0 AND catalog_revision > 0
     AND branch_revision > 0 AND resource_revision > 0 AND grant_revision > 0)
) ENGINE=InnoDB;

DELIMITER //
-- The only issuance route that CREATES a scope binding. It writes the invitation
-- and immutable bound issuer scope under the SAME InnoDB transaction.
-- The restricted worker has EXECUTE only. No direct INSERT on either table.
CREATE PROCEDURE c05_02_issue_bound_once(
  IN p_tenant BINARY(16), IN p_invitation BINARY(16),
  IN p_issuer BINARY(16), IN p_link BINARY(16),
  IN p_recipient BINARY(16), IN p_profile VARCHAR(16),
  IN p_digest BINARY(32), IN p_terms BIGINT UNSIGNED,
  IN p_issued DATETIME(6), IN p_expires DATETIME(6),
  IN p_account_rev BIGINT UNSIGNED, IN p_session_rev BIGINT UNSIGNED,
  IN p_link_rev BIGINT UNSIGNED, IN p_host_rev BIGINT UNSIGNED,
  IN p_catalog_rev BIGINT UNSIGNED,
  IN p_branch BINARY(16), IN p_resource BINARY(16),
  IN p_branch_rev BIGINT UNSIGNED, IN p_resource_rev BIGINT UNSIGNED,
  IN p_grant_rev BIGINT UNSIGNED,
  IN p_action VARCHAR(120), IN p_scope VARCHAR(20)
)
SQL SECURITY DEFINER
BEGIN
  DECLARE v_recipient BINARY(16) DEFAULT NULL;
  DECLARE v_pending INT DEFAULT 0;
  DECLARE EXIT HANDLER FOR 1062
  BEGIN
    ROLLBACK;
    SELECT 'DENIED' AS result;
  END;
  DECLARE EXIT HANDLER FOR SQLEXCEPTION
  BEGIN
    ROLLBACK;
    RESIGNAL;
  END;
  START TRANSACTION;
  SELECT recipient.recipient_id INTO v_recipient
    FROM c05_02_recipients recipient
    JOIN c05_02_tenants tenant ON tenant.tenant_id=recipient.tenant_id
    JOIN c05_02_issuers issuer ON issuer.tenant_id=tenant.tenant_id
      AND issuer.actor_id=p_issuer
    JOIN c05_02_auth_accounts account_row ON account_row.tenant_id=issuer.tenant_id
      AND account_row.actor_id=issuer.actor_id
    JOIN c05_02_auth_sessions session_row ON session_row.tenant_id=issuer.tenant_id
      AND session_row.actor_id=issuer.actor_id
    JOIN c05_02_auth_links link_row ON link_row.tenant_id=issuer.tenant_id
      AND link_row.actor_id=issuer.actor_id
    JOIN c05_02_auth_hosts host_row ON host_row.tenant_id=tenant.tenant_id
    JOIN c05_02_auth_branches branch_row ON branch_row.tenant_id=tenant.tenant_id
      AND branch_row.branch_id=p_branch
    JOIN c05_02_auth_resources resource_row ON resource_row.tenant_id=branch_row.tenant_id
      AND resource_row.branch_id=branch_row.branch_id AND resource_row.resource_id=p_resource
    JOIN c05_02_auth_grants grant_row ON grant_row.tenant_id=issuer.tenant_id
      AND grant_row.actor_id=issuer.actor_id AND grant_row.branch_id=branch_row.branch_id
      AND grant_row.action_key=p_action
    JOIN c05_02_auth_issue_policy policy_row ON policy_row.tenant_id=tenant.tenant_id
      AND policy_row.profile_scope=p_profile AND policy_row.action_key=p_action
   WHERE recipient.tenant_id=p_tenant AND recipient.recipient_id=p_recipient
     AND recipient.actor_id<>p_issuer AND recipient.channel_verified=1
     AND recipient.verified_until_utc>UTC_TIMESTAMP(6)
     AND tenant.lifecycle='ACTIVE' AND tenant.terms_version=p_terms
     AND issuer.enabled=1
     AND ((p_profile='EMPLOYEE' AND issuer.profile_scope='INVITE_EMPLOYEE')
       OR (p_profile='CONSUMER' AND issuer.profile_scope='INVITE_CONSUMER'))
     AND account_row.lifecycle='ACTIVE' AND account_row.revision=p_account_rev
     AND session_row.lifecycle='ACTIVE' AND session_row.revision=p_session_rev
     AND session_row.expires_utc>UTC_TIMESTAMP(6)
     AND link_row.lifecycle='ACTIVE' AND link_row.link_id=p_link
     AND link_row.revision=p_link_rev AND link_row.expires_utc>UTC_TIMESTAMP(6)
     AND host_row.lifecycle='VERIFIED'
     AND host_row.host_revision=p_host_rev AND host_row.catalog_revision=p_catalog_rev
     AND branch_row.lifecycle='ACTIVE' AND branch_row.revision=p_branch_rev
     AND resource_row.lifecycle='ACTIVE' AND resource_row.revision=p_resource_rev
     AND grant_row.lifecycle='ACTIVE' AND grant_row.revision=p_grant_rev
     AND grant_row.expires_utc>UTC_TIMESTAMP(6)
     AND grant_row.scope=p_scope
     AND (grant_row.scope='BRANCH_WIDE'
       OR (grant_row.scope='OWNER_ONLY' AND resource_row.owner_actor_id=p_issuer))
     AND p_action='identity.invitation.issue'
     AND policy_row.lifecycle='ACTIVE'
     AND p_issued<=UTC_TIMESTAMP(6)
     AND p_issued>=DATE_SUB(UTC_TIMESTAMP(6),INTERVAL 5 MINUTE)
     AND p_expires>UTC_TIMESTAMP(6)
     AND TIMESTAMPDIFF(SECOND,p_issued,p_expires) BETWEEN 60 AND 259200
     AND TIMESTAMPDIFF(SECOND,p_issued,p_expires)<=policy_row.max_lifetime_seconds
   FOR UPDATE;
  IF v_recipient IS NULL THEN
    ROLLBACK;
    SELECT 'DENIED' AS result;
  ELSE
    SELECT EXISTS(
      SELECT 1 FROM c05_02_invitations
       WHERE tenant_id=p_tenant AND recipient_id=p_recipient
         AND lifecycle='PENDING' AND expires_at_utc>UTC_TIMESTAMP(6)
    ) INTO v_pending;
    IF v_pending<>0 THEN
      ROLLBACK;
      SELECT 'DENIED' AS result;
    ELSE
      INSERT INTO c05_02_invitations
        (tenant_id,invitation_id,issuer_id,recipient_id,profile_scope,
         token_digest,lifecycle,revision,terms_version,
         issued_at_utc,expires_at_utc,claimed_at_utc)
      VALUES
        (p_tenant,p_invitation,p_issuer,p_recipient,p_profile,
         p_digest,'PENDING',1,p_terms,p_issued,p_expires,NULL);
      INSERT INTO c05_02_invite_scope_bindings
        (tenant_id,invitation_id,issuer_id,issuer_link_id,branch_id,resource_id,
         action_key,scope_mode,account_revision,session_revision,link_revision,
         host_revision,catalog_revision,branch_revision,resource_revision,grant_revision)
      VALUES
        (p_tenant,p_invitation,p_issuer,p_link,p_branch,p_resource,
         p_action,p_scope,p_account_rev,p_session_rev,p_link_rev,
         p_host_rev,p_catalog_rev,p_branch_rev,p_resource_rev,p_grant_rev);
      COMMIT;
      SELECT 'ISSUED' AS result;
    END IF;
  END IF;
END//
DELIMITER ;

DELIMITER //
-- Single UPDATE is the claim linearization point and checks the exact
-- previously recorded branch/resource/action plus EVERY CURRENT issuer guard.
-- A new grant on a DIFFERENT branch/resource cannot resurrect an old invite.
-- A revoked/expired/changed issuer session, grant or membership prevents claim.
-- Recipient channel and current-version consent are independent mandatory gates.
-- No account/membership/session/role is ever inserted by this procedure.
CREATE PROCEDURE c05_02_claim_bound_once(
  IN p_tenant BINARY(16), IN p_invitation BINARY(16),
  IN p_actor BINARY(16), IN p_recipient BINARY(16),
  IN p_digest BINARY(32)
)
SQL SECURITY DEFINER
BEGIN
  UPDATE c05_02_invitations invitation
    JOIN c05_02_invite_scope_bindings binding_row
      ON binding_row.tenant_id=invitation.tenant_id
      AND binding_row.invitation_id=invitation.invitation_id
      AND binding_row.issuer_id=invitation.issuer_id
    JOIN c05_02_tenants tenant ON tenant.tenant_id=invitation.tenant_id
    JOIN c05_02_issuers issuer ON issuer.tenant_id=binding_row.tenant_id
      AND issuer.actor_id=binding_row.issuer_id
    JOIN c05_02_auth_accounts account_row ON account_row.tenant_id=issuer.tenant_id
      AND account_row.actor_id=issuer.actor_id
    JOIN c05_02_auth_sessions session_row ON session_row.tenant_id=issuer.tenant_id
      AND session_row.actor_id=issuer.actor_id
    JOIN c05_02_auth_links link_row ON link_row.tenant_id=issuer.tenant_id
      AND link_row.actor_id=issuer.actor_id
      AND link_row.link_id=binding_row.issuer_link_id
    JOIN c05_02_auth_hosts host_row ON host_row.tenant_id=tenant.tenant_id
    JOIN c05_02_auth_branches branch_row ON branch_row.tenant_id=tenant.tenant_id
      AND branch_row.branch_id=binding_row.branch_id
    JOIN c05_02_auth_resources resource_row ON resource_row.tenant_id=branch_row.tenant_id
      AND resource_row.branch_id=branch_row.branch_id
      AND resource_row.resource_id=binding_row.resource_id
    JOIN c05_02_auth_grants grant_row ON grant_row.tenant_id=issuer.tenant_id
      AND grant_row.actor_id=issuer.actor_id
      AND grant_row.branch_id=binding_row.branch_id
      AND grant_row.action_key=binding_row.action_key
    JOIN c05_02_auth_issue_policy policy_row ON policy_row.tenant_id=tenant.tenant_id
      AND policy_row.profile_scope=invitation.profile_scope
      AND policy_row.action_key=binding_row.action_key
    JOIN c05_02_recipients recipient ON recipient.tenant_id=invitation.tenant_id
      AND recipient.recipient_id=invitation.recipient_id
    JOIN c05_02_consents consent ON consent.tenant_id=recipient.tenant_id
      AND consent.recipient_id=recipient.recipient_id
      AND consent.terms_version=invitation.terms_version
  SET invitation.lifecycle='CLAIMED',
      invitation.revision=invitation.revision+1,
      invitation.claimed_at_utc=UTC_TIMESTAMP(6)
  WHERE invitation.tenant_id=p_tenant
    AND invitation.invitation_id=p_invitation
    AND invitation.recipient_id=p_recipient
    AND invitation.token_digest=p_digest
    AND invitation.revision=1
    AND invitation.lifecycle='PENDING'
    AND invitation.issued_at_utc<=UTC_TIMESTAMP(6)
    AND UTC_TIMESTAMP(6)<invitation.expires_at_utc
    AND invitation.issuer_id<>p_actor
    AND recipient.actor_id=p_actor
    AND recipient.channel_verified=1
    AND recipient.verified_until_utc>UTC_TIMESTAMP(6)
    AND consent.accepted_at_utc<=UTC_TIMESTAMP(6)
    AND tenant.lifecycle='ACTIVE'
    AND tenant.terms_version=invitation.terms_version
    AND issuer.enabled=1
    AND ((invitation.profile_scope='EMPLOYEE' AND issuer.profile_scope='INVITE_EMPLOYEE')
      OR (invitation.profile_scope='CONSUMER' AND issuer.profile_scope='INVITE_CONSUMER'))
    AND account_row.lifecycle='ACTIVE'
    AND account_row.revision=binding_row.account_revision
    AND session_row.lifecycle='ACTIVE'
    AND session_row.revision=binding_row.session_revision
    AND session_row.expires_utc>UTC_TIMESTAMP(6)
    AND link_row.lifecycle='ACTIVE'
    AND link_row.revision=binding_row.link_revision
    AND link_row.expires_utc>UTC_TIMESTAMP(6)
    AND host_row.lifecycle='VERIFIED'
    AND host_row.host_revision=binding_row.host_revision
    AND host_row.catalog_revision=binding_row.catalog_revision
    AND branch_row.lifecycle='ACTIVE'
    AND branch_row.revision=binding_row.branch_revision
    AND resource_row.lifecycle='ACTIVE'
    AND resource_row.revision=binding_row.resource_revision
    AND grant_row.lifecycle='ACTIVE'
    AND grant_row.revision=binding_row.grant_revision
    AND grant_row.scope=binding_row.scope_mode
    AND grant_row.expires_utc>UTC_TIMESTAMP(6)
    AND (grant_row.scope='BRANCH_WIDE'
      OR (grant_row.scope='OWNER_ONLY' AND resource_row.owner_actor_id=issuer.actor_id))
    AND binding_row.action_key='identity.invitation.issue'
    AND policy_row.lifecycle='ACTIVE'
    AND TIMESTAMPDIFF(SECOND,invitation.issued_at_utc,invitation.expires_at_utc)
      <=policy_row.max_lifetime_seconds;
  SELECT IF(ROW_COUNT()=1, 'CLAIMED', 'DENIED') AS result;
END//
DELIMITER ;
