-- CRECISOL C06-02: additive, NON-AUTHORIZING role/capability model.
-- Apply to isolated C05 operational baseline only. Not auto-run on owner data.
-- No pre-existing users, roles, sessions, JWT, MFA or grants are changed.
-- All capabilities are fixed non-grantable until a separately reviewed gate.
CREATE TABLE c06_capability_registry (
  action_id VARCHAR(120) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  realm TINYINT UNSIGNED NOT NULL,
  grantable BOOLEAN NOT NULL DEFAULT FALSE,
  catalog_version SMALLINT UNSIGNED NOT NULL DEFAULT 1,
  PRIMARY KEY (action_id, realm),
  CONSTRAINT ck_c06_action_format CHECK (
    REGEXP_LIKE(action_id, '^[a-z][a-z0-9.-]{2,119}$', 'c')),
  CONSTRAINT ck_c06_capability_realm CHECK (realm IN (1,2)),
  CONSTRAINT ck_c06_capability_not_grantable CHECK (grantable = FALSE),
  CONSTRAINT ck_c06_catalog_version CHECK (catalog_version > 0)
) ENGINE=InnoDB;

CREATE TABLE c06_tenant_role (
  tenant_id BINARY(16) NOT NULL,
  role_id BINARY(16) NOT NULL,
  role_code VARCHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  role_state TINYINT UNSIGNED NOT NULL DEFAULT 1,
  role_revision BIGINT UNSIGNED NOT NULL DEFAULT 1,
  PRIMARY KEY (tenant_id, role_id),
  UNIQUE KEY uq_c06_role_code (tenant_id, role_code),
  CONSTRAINT fk_c06_role_tenant FOREIGN KEY (tenant_id)
    REFERENCES c04_tenant_catalog(tenant_id) ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT ck_c06_role_code CHECK (
    REGEXP_LIKE(role_code, '^[a-z][a-z0-9-]{2,63}$', 'c')),
  -- PLATFORM and SuperAdmin identities are separate from tenant role proposals.
  CONSTRAINT ck_c06_role_realm_separation CHECK (
    role_code NOT LIKE 'platform-%' AND
    role_code NOT IN ('superadmin','super-admin')),
  CONSTRAINT ck_c06_role_state CHECK (role_state IN (1,2,3)),
  CONSTRAINT ck_c06_role_revision CHECK (role_revision > 0),
  CONSTRAINT ck_c06_role_id CHECK (role_id <> X'00000000000000000000000000000000')
) ENGINE=InnoDB;

CREATE TABLE c06_role_capability_proposal (
  tenant_id BINARY(16) NOT NULL,
  role_id BINARY(16) NOT NULL,
  action_id VARCHAR(120) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  realm TINYINT UNSIGNED NOT NULL DEFAULT 1,
  proposal_only BOOLEAN NOT NULL DEFAULT TRUE,
  PRIMARY KEY (tenant_id, role_id, action_id),
  KEY ix_c06_proposal_catalog (action_id, realm),
  CONSTRAINT fk_c06_proposal_role FOREIGN KEY (tenant_id, role_id)
    REFERENCES c06_tenant_role(tenant_id, role_id) ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT fk_c06_proposal_capability FOREIGN KEY (action_id, realm)
    REFERENCES c06_capability_registry(action_id, realm) ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT ck_c06_proposal_tenant_realm CHECK (realm = 1),
  CONSTRAINT ck_c06_proposal_not_grant CHECK (proposal_only = TRUE)
) ENGINE=InnoDB;

CREATE TABLE c06_membership_role_proposal (
  tenant_id BINARY(16) NOT NULL,
  proposal_id BINARY(16) NOT NULL,
  membership_id BINARY(16) NOT NULL,
  role_id BINARY(16) NOT NULL,
  branch_id BINARY(16) NOT NULL,
  proposal_state TINYINT UNSIGNED NOT NULL DEFAULT 1,
  revision BIGINT UNSIGNED NOT NULL DEFAULT 1,
  valid_from_utc DATETIME(6) NOT NULL,
  valid_until_utc DATETIME(6) NOT NULL,
  revoked_at_utc DATETIME(6) NULL,
  PRIMARY KEY (tenant_id, proposal_id),
  UNIQUE KEY uq_c06_proposal_membership_role_branch
    (tenant_id, membership_id, role_id, branch_id),
  KEY ix_c06_proposal_lookup (tenant_id, membership_id, branch_id, proposal_state, valid_until_utc),
  KEY ix_c06_proposal_role (tenant_id, role_id),
  KEY ix_c06_proposal_branch (tenant_id, branch_id),
  CONSTRAINT fk_c06_proposal_membership FOREIGN KEY (tenant_id, membership_id)
    REFERENCES c05_identity_membership(tenant_id, membership_id)
    ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT fk_c06_proposal_role_assigned FOREIGN KEY (tenant_id, role_id)
    REFERENCES c06_tenant_role(tenant_id, role_id)
    ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT fk_c06_proposal_branch_assigned FOREIGN KEY (tenant_id, branch_id)
    REFERENCES c05_identity_branch(tenant_id, branch_id)
    ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT ck_c06_proposal_id CHECK (proposal_id <> X'00000000000000000000000000000000'),
  CONSTRAINT ck_c06_proposal_state CHECK (proposal_state IN (1,2)),
  CONSTRAINT ck_c06_proposal_revision CHECK (revision > 0),
  CONSTRAINT ck_c06_proposal_window CHECK (valid_until_utc > valid_from_utc),
  CONSTRAINT ck_c06_proposal_revocation CHECK (
    (proposal_state = 1 AND revoked_at_utc IS NULL) OR
    (proposal_state = 2 AND revoked_at_utc IS NOT NULL))
) ENGINE=InnoDB;

CREATE TABLE c06_role_proposal_audit (
  tenant_id BINARY(16) NOT NULL,
  audit_id BINARY(16) NOT NULL,
  proposal_id BINARY(16) NOT NULL,
  actor_account_id BINARY(16) NOT NULL,
  event_kind TINYINT UNSIGNED NOT NULL,
  reason_hmac BINARY(32) NOT NULL,
  at_utc DATETIME(6) NOT NULL,
  PRIMARY KEY (tenant_id, audit_id),
  KEY ix_c06_audit_proposal (tenant_id, proposal_id, at_utc),
  CONSTRAINT fk_c06_audit_proposal FOREIGN KEY (tenant_id, proposal_id)
    REFERENCES c06_membership_role_proposal(tenant_id, proposal_id)
    ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT fk_c06_audit_actor FOREIGN KEY (tenant_id, actor_account_id)
    REFERENCES c05_identity_membership(tenant_id, account_id)
    ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT ck_c06_audit_kind CHECK (event_kind IN (1,2))
) ENGINE=InnoDB;

DELIMITER $$
CREATE TRIGGER trg_c06_proposal_immutable_scope
BEFORE UPDATE ON c06_membership_role_proposal FOR EACH ROW
BEGIN
  IF NOT (OLD.tenant_id <=> NEW.tenant_id)
    OR NOT (OLD.proposal_id <=> NEW.proposal_id)
    OR NOT (OLD.membership_id <=> NEW.membership_id)
    OR NOT (OLD.role_id <=> NEW.role_id)
    OR NOT (OLD.branch_id <=> NEW.branch_id)
    OR NOT (OLD.valid_from_utc <=> NEW.valid_from_utc)
    OR NOT (OLD.valid_until_utc <=> NEW.valid_until_utc)
    OR NEW.revision <= OLD.revision
    OR (OLD.proposal_state = 2 AND NEW.proposal_state <> 2)
    OR (OLD.revoked_at_utc IS NOT NULL
        AND NOT (OLD.revoked_at_utc <=> NEW.revoked_at_utc)) THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C06 prepared scope immutable or version invalid';
  END IF;
END$$
CREATE TRIGGER trg_c06_role_immutable_identity
BEFORE UPDATE ON c06_tenant_role FOR EACH ROW
BEGIN
  IF NOT (OLD.tenant_id <=> NEW.tenant_id)
    OR NOT (OLD.role_id <=> NEW.role_id)
    OR NOT (OLD.role_code <=> NEW.role_code)
    OR NEW.role_revision <= OLD.role_revision
    OR (OLD.role_state = 3 AND NEW.role_state <> 3) THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C06 role identity immutable or revoked';
  END IF;
END$$
CREATE TRIGGER trg_c06_audit_append_only_update
BEFORE UPDATE ON c06_role_proposal_audit FOR EACH ROW
BEGIN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C06 role audit is append only';
END$$
CREATE TRIGGER trg_c06_audit_append_only_delete
BEFORE DELETE ON c06_role_proposal_audit FOR EACH ROW
BEGIN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C06 role audit cannot be removed';
END$$
DELIMITER ;

-- Never grant SELECT/INSERT/UPDATE on these tables to a public transport
-- identity by this migration. No HTTP routes or default role seeds.
