-- C06-11 isolated candidate: explicit, non-activated delegated-role ledger.
-- No seed, production migration, API, runtime grants, or authority from proposals.
-- The future transactional adapter MUST revalidate the real session, commercial
-- condition, own ABAC, exact one-use factor proof and every revision under lock.
-- The ledger alone is NOT a private-resource authorization decision.
CREATE TABLE c06_delegation_role (
 tenant_id BINARY(16) NOT NULL,
 role_id BINARY(16) NOT NULL,
 role_code VARCHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 revision BIGINT NOT NULL DEFAULT 1,
 created_at_utc DATETIME(6) NOT NULL,
 retired_at_utc DATETIME(6) NULL,
 PRIMARY KEY(tenant_id,role_id),
 UNIQUE KEY uq_c06_dr_code(tenant_id,role_code),
 CONSTRAINT fk_c06_dr_tenant FOREIGN KEY(tenant_id) REFERENCES c04_tenant_catalog(tenant_id),
 CONSTRAINT ck_c06_dr_code CHECK(role_code IN ('tenant-staff','tenant-viewer','tenant-branch-manager')),
 CONSTRAINT ck_c06_dr_id CHECK(role_id<>X'00000000000000000000000000000000'),
 CONSTRAINT ck_c06_dr_lifecycle CHECK((revision=1 AND retired_at_utc IS NULL) OR
    (revision=2 AND retired_at_utc IS NOT NULL AND retired_at_utc>=created_at_utc))
) ENGINE=InnoDB;

CREATE TABLE c06_delegation_ceiling (
 tenant_id BINARY(16) NOT NULL,
 actor_membership_id BINARY(16) NOT NULL,
 branch_id BINARY(16) NOT NULL,
 resource_id BINARY(16) NOT NULL,
 action_id VARCHAR(120) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 revision BIGINT NOT NULL DEFAULT 1,
 may_assign BOOLEAN NOT NULL,
 may_revoke BOOLEAN NOT NULL,
 valid_from_utc DATETIME(6) NOT NULL,
 valid_until_utc DATETIME(6) NOT NULL,
 revoked_at_utc DATETIME(6) NULL,
 PRIMARY KEY(tenant_id,actor_membership_id,branch_id,resource_id,action_id),
 CONSTRAINT fk_c06_dc_actor FOREIGN KEY(tenant_id,actor_membership_id)
   REFERENCES c05_identity_membership(tenant_id,membership_id),
 CONSTRAINT fk_c06_dc_resource FOREIGN KEY(tenant_id,branch_id,resource_id)
   REFERENCES c05_identity_private_resource(tenant_id,branch_id,resource_id),
 CONSTRAINT ck_c06_dc_action CHECK(action_id='identity.private.read'),
 CONSTRAINT ck_c06_dc_flags CHECK(may_assign IN(0,1) AND may_revoke IN(0,1) AND (may_assign=1 OR may_revoke=1)),
 CONSTRAINT ck_c06_dc_window CHECK(valid_until_utc>valid_from_utc),
 CONSTRAINT ck_c06_dc_lifecycle CHECK((revision=1 AND revoked_at_utc IS NULL) OR
    (revision=2 AND revoked_at_utc IS NOT NULL AND revoked_at_utc>=valid_from_utc))
) ENGINE=InnoDB;

CREATE TABLE c06_delegation_grant (
 tenant_id BINARY(16) NOT NULL,
 grant_id BINARY(16) NOT NULL,
 branch_id BINARY(16) NOT NULL,
 resource_id BINARY(16) NOT NULL,
 action_id VARCHAR(120) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 subject_membership_id BINARY(16) NOT NULL,
 role_id BINARY(16) NOT NULL,
 issuer_membership_id BINARY(16) NOT NULL,
 role_revision BIGINT NOT NULL,
 subject_revision BIGINT NOT NULL,
 issuer_revision BIGINT NOT NULL,
 ceiling_revision BIGINT NOT NULL,
 revision BIGINT NOT NULL DEFAULT 1,
 issued_at_utc DATETIME(6) NOT NULL,
 expires_at_utc DATETIME(6) NOT NULL,
 purpose_hmac BINARY(32) NOT NULL,
 intent_hmac BINARY(32) NOT NULL,
 PRIMARY KEY(tenant_id,grant_id),
 UNIQUE KEY uq_c06_dg_scope(tenant_id,subject_membership_id,branch_id,resource_id,action_id),
 UNIQUE KEY uq_c06_dg_exact(tenant_id,grant_id,branch_id,resource_id,action_id),
 UNIQUE KEY uq_c06_dg_intent(tenant_id,intent_hmac),
 CONSTRAINT fk_c06_dg_subject FOREIGN KEY(tenant_id,subject_membership_id)
   REFERENCES c05_identity_membership(tenant_id,membership_id),
 CONSTRAINT fk_c06_dg_role FOREIGN KEY(tenant_id,role_id)
   REFERENCES c06_delegation_role(tenant_id,role_id),
 CONSTRAINT fk_c06_dg_issuer_scope FOREIGN KEY(tenant_id,issuer_membership_id,branch_id,resource_id,action_id)
   REFERENCES c06_delegation_ceiling(tenant_id,actor_membership_id,branch_id,resource_id,action_id),
 CONSTRAINT ck_c06_dg_action CHECK(action_id='identity.private.read'),
 CONSTRAINT ck_c06_dg_no_self CHECK(subject_membership_id<>issuer_membership_id),
 CONSTRAINT ck_c06_dg_revision CHECK(revision=1 AND role_revision>0 AND subject_revision>0 AND issuer_revision>0 AND ceiling_revision>0),
 CONSTRAINT ck_c06_dg_window CHECK(expires_at_utc>issued_at_utc),
 CONSTRAINT ck_c06_dg_id CHECK(grant_id<>X'00000000000000000000000000000000'),
 CONSTRAINT ck_c06_dg_hmac CHECK(purpose_hmac<>UNHEX(REPEAT('00',32)) AND intent_hmac<>UNHEX(REPEAT('00',32)))
) ENGINE=InnoDB;

-- One immutable revocation per grant: the unique PK is the second CAS barrier.
-- Read state is derived from presence of this event; no mutable active flag.
CREATE TABLE c06_delegation_revocation (
 tenant_id BINARY(16) NOT NULL,
 grant_id BINARY(16) NOT NULL,
 branch_id BINARY(16) NOT NULL,
 resource_id BINARY(16) NOT NULL,
 action_id VARCHAR(120) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 actor_membership_id BINARY(16) NOT NULL,
 actor_revision BIGINT NOT NULL,
 ceiling_revision BIGINT NOT NULL,
 expected_revision BIGINT NOT NULL,
 revision BIGINT NOT NULL,
 revoked_at_utc DATETIME(6) NOT NULL,
 purpose_hmac BINARY(32) NOT NULL,
 intent_hmac BINARY(32) NOT NULL,
 PRIMARY KEY(tenant_id,grant_id),
 UNIQUE KEY uq_c06_dv_intent(tenant_id,intent_hmac),
 CONSTRAINT fk_c06_dv_exact FOREIGN KEY(tenant_id,grant_id,branch_id,resource_id,action_id)
   REFERENCES c06_delegation_grant(tenant_id,grant_id,branch_id,resource_id,action_id),
 CONSTRAINT fk_c06_dv_actor_scope FOREIGN KEY(tenant_id,actor_membership_id,branch_id,resource_id,action_id)
   REFERENCES c06_delegation_ceiling(tenant_id,actor_membership_id,branch_id,resource_id,action_id),
 CONSTRAINT ck_c06_dv_revision CHECK(expected_revision=1 AND revision=2 AND actor_revision>0 AND ceiling_revision>0),
 CONSTRAINT ck_c06_dv_hmac CHECK(purpose_hmac<>UNHEX(REPEAT('00',32)) AND intent_hmac<>UNHEX(REPEAT('00',32)))
) ENGINE=InnoDB;

DELIMITER $$
CREATE TRIGGER trg_c06_dr_insert BEFORE INSERT ON c06_delegation_role FOR EACH ROW
BEGIN
 IF NEW.revision<>1 OR NEW.retired_at_utc IS NOT NULL OR NEW.created_at_utc>UTC_TIMESTAMP(6)
 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C06 role must begin current'; END IF;
END$$
CREATE TRIGGER trg_c06_dr_retire BEFORE UPDATE ON c06_delegation_role FOR EACH ROW
BEGIN
 IF NOT (OLD.tenant_id<=>NEW.tenant_id) OR NOT (OLD.role_id<=>NEW.role_id)
 OR NOT (OLD.role_code<=>NEW.role_code) OR NOT (OLD.created_at_utc<=>NEW.created_at_utc)
 OR OLD.revision<>1 OR NEW.revision<>2 OR OLD.retired_at_utc IS NOT NULL
 OR NEW.retired_at_utc IS NULL OR NEW.retired_at_utc>UTC_TIMESTAMP(6)
 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C06 role immutable except first retirement'; END IF;
END$$
CREATE TRIGGER trg_c06_dr_no_delete BEFORE DELETE ON c06_delegation_role FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C06 role history immutable'; END$$
CREATE TRIGGER trg_c06_dc_insert BEFORE INSERT ON c06_delegation_ceiling FOR EACH ROW
BEGIN
 IF NEW.revision<>1 OR NEW.revoked_at_utc IS NOT NULL
 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C06 ceiling must begin unrevoked'; END IF;
END$$
CREATE TRIGGER trg_c06_dc_revoke BEFORE UPDATE ON c06_delegation_ceiling FOR EACH ROW
BEGIN
 IF NOT (OLD.tenant_id<=>NEW.tenant_id) OR NOT (OLD.actor_membership_id<=>NEW.actor_membership_id)
 OR NOT (OLD.branch_id<=>NEW.branch_id) OR NOT (OLD.resource_id<=>NEW.resource_id)
 OR NOT (OLD.action_id<=>NEW.action_id) OR NOT (OLD.may_assign<=>NEW.may_assign)
 OR NOT (OLD.may_revoke<=>NEW.may_revoke) OR NOT (OLD.valid_from_utc<=>NEW.valid_from_utc)
 OR NOT (OLD.valid_until_utc<=>NEW.valid_until_utc)
 OR OLD.revision<>1 OR NEW.revision<>2 OR OLD.revoked_at_utc IS NOT NULL
 OR NEW.revoked_at_utc IS NULL OR NEW.revoked_at_utc>UTC_TIMESTAMP(6)
 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C06 ceiling immutable except first revocation'; END IF;
END$$
CREATE TRIGGER trg_c06_dc_no_delete BEFORE DELETE ON c06_delegation_ceiling FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C06 ceiling history immutable'; END$$

-- Redundant database guard, NOT a replacement for the locked application policy.
CREATE TRIGGER trg_c06_dg_scope BEFORE INSERT ON c06_delegation_grant FOR EACH ROW
BEGIN
 IF NEW.issued_at_utc>UTC_TIMESTAMP(6) OR NEW.expires_at_utc<=UTC_TIMESTAMP(6)
 OR NOT EXISTS(
   SELECT 1 FROM c06_delegation_ceiling c
   JOIN c06_delegation_role r ON r.tenant_id=c.tenant_id AND r.role_id=NEW.role_id
   JOIN c05_identity_membership a ON a.tenant_id=c.tenant_id AND a.membership_id=c.actor_membership_id
   JOIN c05_identity_membership s ON s.tenant_id=c.tenant_id AND s.membership_id=NEW.subject_membership_id
   JOIN c05_identity_branch b ON b.tenant_id=c.tenant_id AND b.branch_id=c.branch_id
   JOIN c05_identity_private_resource x ON x.tenant_id=c.tenant_id AND x.branch_id=c.branch_id AND x.resource_id=c.resource_id
   WHERE c.tenant_id=NEW.tenant_id AND c.actor_membership_id=NEW.issuer_membership_id
     AND c.branch_id=NEW.branch_id AND c.resource_id=NEW.resource_id AND c.action_id=NEW.action_id
     AND c.revision=NEW.ceiling_revision AND c.may_assign=1 AND c.revoked_at_utc IS NULL
     AND c.valid_from_utc<=NEW.issued_at_utc AND NEW.expires_at_utc<=c.valid_until_utc
     AND r.revision=NEW.role_revision AND r.retired_at_utc IS NULL AND r.created_at_utc<=NEW.issued_at_utc
     AND a.membership_state=1 AND a.approval_revision=NEW.issuer_revision
     AND a.role_code IN('OWNER','ADMIN','tenant-owner','tenant-admin')
     AND a.granted_at_utc<=NEW.issued_at_utc AND NEW.expires_at_utc<=a.expires_at_utc
     AND s.membership_state=1 AND s.approval_revision=NEW.subject_revision
     AND s.granted_at_utc<=NEW.issued_at_utc AND NEW.expires_at_utc<=s.expires_at_utc
     AND b.active=1 AND x.resource_state=1
 )
 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C06 delegation issuance scope denied'; END IF;
END$$
CREATE TRIGGER trg_c06_dg_no_update BEFORE UPDATE ON c06_delegation_grant FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C06 grant issuance immutable'; END$$
CREATE TRIGGER trg_c06_dg_no_delete BEFORE DELETE ON c06_delegation_grant FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C06 grant issuance immutable'; END$$
CREATE TRIGGER trg_c06_dv_scope BEFORE INSERT ON c06_delegation_revocation FOR EACH ROW
BEGIN
 IF NEW.revoked_at_utc>UTC_TIMESTAMP(6)
 OR NOT EXISTS(
   SELECT 1 FROM c06_delegation_grant g
   JOIN c06_delegation_ceiling c ON c.tenant_id=g.tenant_id AND c.actor_membership_id=NEW.actor_membership_id
     AND c.branch_id=g.branch_id AND c.resource_id=g.resource_id AND c.action_id=g.action_id
   JOIN c05_identity_membership a ON a.tenant_id=c.tenant_id AND a.membership_id=c.actor_membership_id
   WHERE g.tenant_id=NEW.tenant_id AND g.grant_id=NEW.grant_id
     AND g.branch_id=NEW.branch_id AND g.resource_id=NEW.resource_id AND g.action_id=NEW.action_id
     AND g.subject_membership_id<>NEW.actor_membership_id AND g.revision=NEW.expected_revision
     AND g.issued_at_utc<=NEW.revoked_at_utc
     AND c.revision=NEW.ceiling_revision AND c.may_revoke=1 AND c.revoked_at_utc IS NULL
     AND c.valid_from_utc<=NEW.revoked_at_utc AND c.valid_until_utc>UTC_TIMESTAMP(6)
     AND a.membership_state=1 AND a.approval_revision=NEW.actor_revision
     AND a.role_code IN('OWNER','ADMIN','tenant-owner','tenant-admin')
     AND a.granted_at_utc<=NEW.revoked_at_utc AND a.expires_at_utc>UTC_TIMESTAMP(6)
 )
 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C06 delegation revocation scope denied'; END IF;
END$$
CREATE TRIGGER trg_c06_dv_no_update BEFORE UPDATE ON c06_delegation_revocation FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C06 grant revocation immutable'; END$$
CREATE TRIGGER trg_c06_dv_no_delete BEFORE DELETE ON c06_delegation_revocation FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C06 grant revocation immutable'; END$$
DELIMITER ;
-- Deliberately no GRANT, routine callable by the API, view exposing PII or role seed.
