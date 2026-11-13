-- CRECISOL C05-02 isolated CRYPTOGRAPHIC ASSERTION TEST (NOT login).
-- Requires prior invitation + issuer scope + bound claim disposable fixtures.
-- Server-held HMAC key is never stored in SQL or exposed to browser.
-- This table represents synthetic recipient authentication metadata ONLY.
-- A signed proof MUST be verified by the backend BEFORE this SQL procedure.
CREATE TABLE c05_02_signed_recipient_sessions (
  tenant_id BINARY(16) NOT NULL,
  session_id BINARY(16) NOT NULL,
  recipient_id BINARY(16) NOT NULL,
  actor_id BINARY(16) NOT NULL,
  lifecycle ENUM('ACTIVE','REVOKED','SUSPENDED') NOT NULL,
  revision BIGINT UNSIGNED NOT NULL,
  key_epoch INT UNSIGNED NOT NULL,
  issued_utc DATETIME(6) NOT NULL,
  expires_utc DATETIME(6) NOT NULL,
  PRIMARY KEY (tenant_id,session_id),
  INDEX ix_signed_recipient (tenant_id,recipient_id,actor_id),
  FOREIGN KEY (tenant_id,recipient_id)
    REFERENCES c05_02_recipients(tenant_id,recipient_id)
    ON DELETE RESTRICT ON UPDATE RESTRICT,
  CHECK (revision>0 AND key_epoch>0),
  CHECK (expires_utc>issued_utc)
) ENGINE=InnoDB;

-- Issued/claim session revision and expiry are checked within the very SAME
-- UPDATE that consumes the invitation; no time-of-check/time-of-use window.
-- No SELECT on token, PII, membership or session rows for the worker.
DELIMITER //
CREATE PROCEDURE c05_02_claim_signed_once(
  IN p_tenant BINARY(16), IN p_invitation BINARY(16),
  IN p_actor BINARY(16), IN p_recipient BINARY(16),
  IN p_digest BINARY(32),
  IN p_signed_session BINARY(16), IN p_signed_revision BIGINT UNSIGNED,
  IN p_signed_key_epoch INT UNSIGNED,
  IN p_signed_issued DATETIME(6), IN p_signed_expires DATETIME(6)
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
    JOIN c05_02_signed_recipient_sessions signed_session
      ON signed_session.tenant_id=recipient.tenant_id
      AND signed_session.recipient_id=recipient.recipient_id
      AND signed_session.actor_id=recipient.actor_id
      AND signed_session.session_id=p_signed_session
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
    AND signed_session.lifecycle='ACTIVE'
    AND signed_session.revision=p_signed_revision
    AND signed_session.key_epoch=p_signed_key_epoch
    AND signed_session.issued_utc=p_signed_issued
    AND signed_session.expires_utc=p_signed_expires
    AND signed_session.issued_utc<=UTC_TIMESTAMP(6)
    AND signed_session.expires_utc>UTC_TIMESTAMP(6)
    AND TIMESTAMPDIFF(SECOND,signed_session.issued_utc,signed_session.expires_utc)
          BETWEEN 30 AND 300
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
