-- CRECISOL C05-02: disposable authorization gate ONLY, MySQL 8.4.
-- Prerequisite: c05_02_invitation_atomic_fixture.sql. NEVER a migration.
-- Session rows here are REVOCABLE METADATA, NOT a login implementation.
-- Only a separately trusted backend verifier may originate the evidence.
-- No browser-selected tenant, action, branch, resource or DSN is authoritative.
CREATE TABLE c05_02_auth_accounts (
  tenant_id BINARY(16) NOT NULL, actor_id BINARY(16) NOT NULL,
  lifecycle ENUM('ACTIVE','SUSPENDED','REVOKED') NOT NULL,
  revision BIGINT UNSIGNED NOT NULL,
  PRIMARY KEY (tenant_id,actor_id),
  FOREIGN KEY (tenant_id,actor_id) REFERENCES c05_02_issuers(tenant_id,actor_id)
    ON DELETE RESTRICT ON UPDATE RESTRICT,
  CHECK (revision > 0)
) ENGINE=InnoDB;

CREATE TABLE c05_02_auth_sessions (
  tenant_id BINARY(16) NOT NULL, actor_id BINARY(16) NOT NULL,
  lifecycle ENUM('ACTIVE','SUSPENDED','REVOKED') NOT NULL,
  revision BIGINT UNSIGNED NOT NULL, expires_utc DATETIME(6) NOT NULL,
  PRIMARY KEY (tenant_id,actor_id),
  FOREIGN KEY (tenant_id,actor_id) REFERENCES c05_02_auth_accounts(tenant_id,actor_id)
    ON DELETE RESTRICT ON UPDATE RESTRICT,
  CHECK (revision > 0)
) ENGINE=InnoDB;

CREATE TABLE c05_02_auth_links (
  tenant_id BINARY(16) NOT NULL, actor_id BINARY(16) NOT NULL,
  link_id BINARY(16) NOT NULL,
  lifecycle ENUM('ACTIVE','PENDING','SUSPENDED','REVOKED') NOT NULL,
  revision BIGINT UNSIGNED NOT NULL, expires_utc DATETIME(6) NOT NULL,
  PRIMARY KEY (tenant_id,actor_id),
  UNIQUE KEY uq_auth_link (tenant_id,link_id),
  FOREIGN KEY (tenant_id,actor_id) REFERENCES c05_02_auth_accounts(tenant_id,actor_id)
    ON DELETE RESTRICT ON UPDATE RESTRICT,
  CHECK (revision > 0)
) ENGINE=InnoDB;

CREATE TABLE c05_02_auth_hosts (
  tenant_id BINARY(16) NOT NULL PRIMARY KEY,
  lifecycle ENUM('VERIFIED','REVOKED') NOT NULL,
  host_revision BIGINT UNSIGNED NOT NULL,
  catalog_revision BIGINT UNSIGNED NOT NULL,
  FOREIGN KEY (tenant_id) REFERENCES c05_02_tenants(tenant_id)
    ON DELETE RESTRICT ON UPDATE RESTRICT,
  CHECK (host_revision > 0 AND catalog_revision > 0)
) ENGINE=InnoDB;

CREATE TABLE c05_02_auth_branches (
  tenant_id BINARY(16) NOT NULL, branch_id BINARY(16) NOT NULL,
  lifecycle ENUM('ACTIVE','SUSPENDED','DEACTIVATED') NOT NULL,
  revision BIGINT UNSIGNED NOT NULL,
  PRIMARY KEY (tenant_id,branch_id),
  FOREIGN KEY (tenant_id) REFERENCES c05_02_tenants(tenant_id)
    ON DELETE RESTRICT ON UPDATE RESTRICT,
  CHECK (revision > 0)
) ENGINE=InnoDB;

CREATE TABLE c05_02_auth_resources (
  tenant_id BINARY(16) NOT NULL, resource_id BINARY(16) NOT NULL,
  branch_id BINARY(16) NOT NULL, owner_actor_id BINARY(16) NOT NULL,
  lifecycle ENUM('ACTIVE','SUSPENDED','DELETED') NOT NULL,
  revision BIGINT UNSIGNED NOT NULL,
  PRIMARY KEY (tenant_id,resource_id),
  FOREIGN KEY (tenant_id,branch_id) REFERENCES c05_02_auth_branches(tenant_id,branch_id)
    ON DELETE RESTRICT ON UPDATE RESTRICT,
  FOREIGN KEY (tenant_id,owner_actor_id) REFERENCES c05_02_issuers(tenant_id,actor_id)
    ON DELETE RESTRICT ON UPDATE RESTRICT,
  CHECK (revision > 0)
) ENGINE=InnoDB;

CREATE TABLE c05_02_auth_grants (
  tenant_id BINARY(16) NOT NULL, actor_id BINARY(16) NOT NULL,
  branch_id BINARY(16) NOT NULL, action_key VARCHAR(120) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  scope ENUM('OWNER_ONLY','BRANCH_WIDE') NOT NULL,
  lifecycle ENUM('ACTIVE','SUSPENDED','REVOKED') NOT NULL,
  revision BIGINT UNSIGNED NOT NULL, expires_utc DATETIME(6) NOT NULL,
  PRIMARY KEY (tenant_id,actor_id,branch_id,action_key),
  FOREIGN KEY (tenant_id,actor_id) REFERENCES c05_02_auth_accounts(tenant_id,actor_id)
    ON DELETE RESTRICT ON UPDATE RESTRICT,
  FOREIGN KEY (tenant_id,branch_id) REFERENCES c05_02_auth_branches(tenant_id,branch_id)
    ON DELETE RESTRICT ON UPDATE RESTRICT,
  CHECK (revision > 0)
) ENGINE=InnoDB;

CREATE TABLE c05_02_auth_issue_policy (
  tenant_id BINARY(16) NOT NULL,
  profile_scope ENUM('EMPLOYEE','CONSUMER') NOT NULL,
  action_key VARCHAR(120) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  lifecycle ENUM('ACTIVE','DISABLED') NOT NULL,
  max_lifetime_seconds INT UNSIGNED NOT NULL,
  PRIMARY KEY (tenant_id,profile_scope,action_key),
  FOREIGN KEY (tenant_id) REFERENCES c05_02_tenants(tenant_id)
    ON DELETE RESTRICT ON UPDATE RESTRICT,
  CHECK (max_lifetime_seconds BETWEEN 60 AND 259200)
) ENGINE=InnoDB;

DELIMITER //
-- One tenant-scoped transaction for evidence, policy and unique issuance.
-- Backend-only scope evidence must originate from C04 resolver, not HTTP.
-- The restricted application identity may EXECUTE but not inspect/mutate rows.
-- Revalidation FOR UPDATE is the linearization point with state revocations.
CREATE PROCEDURE c05_02_issue_scoped_once(
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
  -- Order recipient -> tenant -> issuer -> account/session/link -> branch/resource/grant.
  -- Restrict *all* joins by tenant. No role/profile inference.
  SELECT r.recipient_id INTO v_recipient
    FROM c05_02_recipients r
    JOIN c05_02_tenants t ON t.tenant_id=r.tenant_id
    JOIN c05_02_issuers issuer ON issuer.tenant_id=t.tenant_id
      AND issuer.actor_id=p_issuer
    JOIN c05_02_auth_accounts acc ON acc.tenant_id=issuer.tenant_id
      AND acc.actor_id=issuer.actor_id
    JOIN c05_02_auth_sessions sess ON sess.tenant_id=acc.tenant_id
      AND sess.actor_id=acc.actor_id
    JOIN c05_02_auth_links link ON link.tenant_id=acc.tenant_id
      AND link.actor_id=acc.actor_id
    JOIN c05_02_auth_hosts host ON host.tenant_id=t.tenant_id
    JOIN c05_02_auth_branches br ON br.tenant_id=t.tenant_id
      AND br.branch_id=p_branch
    JOIN c05_02_auth_resources res ON res.tenant_id=br.tenant_id
      AND res.branch_id=br.branch_id AND res.resource_id=p_resource
    JOIN c05_02_auth_grants grant_row ON grant_row.tenant_id=acc.tenant_id
      AND grant_row.actor_id=acc.actor_id AND grant_row.branch_id=br.branch_id
      AND grant_row.action_key=p_action
    JOIN c05_02_auth_issue_policy policy_row ON policy_row.tenant_id=t.tenant_id
      AND policy_row.profile_scope=p_profile AND policy_row.action_key=p_action
   WHERE r.tenant_id=p_tenant AND r.recipient_id=p_recipient
     AND r.actor_id<>p_issuer AND r.channel_verified=1
     AND r.verified_until_utc>UTC_TIMESTAMP(6)
     AND t.lifecycle='ACTIVE' AND t.terms_version=p_terms
     AND issuer.enabled=1
     AND ((p_profile='EMPLOYEE' AND issuer.profile_scope='INVITE_EMPLOYEE')
       OR (p_profile='CONSUMER' AND issuer.profile_scope='INVITE_CONSUMER'))
     AND acc.lifecycle='ACTIVE' AND acc.revision=p_account_rev
     AND sess.lifecycle='ACTIVE' AND sess.revision=p_session_rev
     AND sess.expires_utc>UTC_TIMESTAMP(6)
     AND link.lifecycle='ACTIVE' AND link.link_id=p_link
     AND link.revision=p_link_rev AND link.expires_utc>UTC_TIMESTAMP(6)
     AND host.lifecycle='VERIFIED'
     AND host.host_revision=p_host_rev AND host.catalog_revision=p_catalog_rev
     AND br.lifecycle='ACTIVE' AND br.revision=p_branch_rev
     AND res.lifecycle='ACTIVE' AND res.revision=p_resource_rev
     AND grant_row.lifecycle='ACTIVE' AND grant_row.revision=p_grant_rev
     AND grant_row.expires_utc>UTC_TIMESTAMP(6)
     AND grant_row.scope=p_scope
     AND (grant_row.scope='BRANCH_WIDE' OR
       (grant_row.scope='OWNER_ONLY' AND res.owner_actor_id=p_issuer))
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
      COMMIT;
      SELECT 'ISSUED' AS result;
    END IF;
  END IF;
END//
DELIMITER ;
