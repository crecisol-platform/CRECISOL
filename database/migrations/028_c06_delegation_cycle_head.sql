-- C06-11 candidate, NOT activated. Apply after 024..027 only in an isolated
-- schema, in exclusive maintenance, without application writers. MySQL DDL
-- commits per statement; take a verified baseline and stop on any error.
-- No historical migration is rewritten. No runtime privilege or permission
-- is granted. The head is derived metadata, NOT an authorization credential.
-- The future adapter MUST bind fresh MFA, locked authority and provenance,
-- and commit the grant/revocation, head transition and audit together.

-- A revision identifies one immutable issuance in the exact scope. The
-- longer unique key makes the head FK exact (including role and issuance ID).
ALTER TABLE c06_delegation_grant
 ADD UNIQUE KEY uq_c06_dg_generation
   (tenant_id,subject_membership_id,branch_id,resource_id,action_id,revision),
 ADD UNIQUE KEY uq_c06_dg_head_ref
   (tenant_id,subject_membership_id,branch_id,resource_id,action_id,revision,grant_id,role_id),
 DROP CHECK ck_c06_dg_revision,
 ADD CONSTRAINT ck_c06_dg_cycle_revision CHECK(
   revision BETWEEN 1 AND 9223372036854775805 AND MOD(revision,2)=1
   AND role_revision>0 AND subject_revision>0 AND issuer_revision>0 AND ceiling_revision>0);
ALTER TABLE c06_delegation_grant DROP INDEX uq_c06_dg_scope;

ALTER TABLE c06_delegation_revocation
 DROP CHECK ck_c06_dv_revision,
 ADD CONSTRAINT ck_c06_dv_cycle_revision CHECK(
   expected_revision BETWEEN 1 AND 9223372036854775805 AND MOD(expected_revision,2)=1
   AND revision=expected_revision+1 AND actor_revision>0 AND ceiling_revision>0);

CREATE TABLE c06_delegation_cycle_head (
 tenant_id BINARY(16) NOT NULL,
 subject_membership_id BINARY(16) NOT NULL,
 branch_id BINARY(16) NOT NULL,
 resource_id BINARY(16) NOT NULL,
 action_id VARCHAR(120) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 grant_id BINARY(16) NOT NULL,
 role_id BINARY(16) NOT NULL,
 issuance_revision BIGINT NOT NULL,
 revision BIGINT NOT NULL,
 terminal_at_utc DATETIME(6) NULL,
 PRIMARY KEY(tenant_id,subject_membership_id,branch_id,resource_id,action_id),
 UNIQUE KEY uq_c06_dch_grant(tenant_id,grant_id),
 CONSTRAINT fk_c06_dch_exact FOREIGN KEY(
   tenant_id,subject_membership_id,branch_id,resource_id,action_id,issuance_revision,grant_id,role_id)
 REFERENCES c06_delegation_grant(
   tenant_id,subject_membership_id,branch_id,resource_id,action_id,revision,grant_id,role_id),
 CONSTRAINT ck_c06_dch_revision CHECK(
   issuance_revision BETWEEN 1 AND 9223372036854775805 AND MOD(issuance_revision,2)=1
   AND ((revision=issuance_revision AND terminal_at_utc IS NULL)
     OR (revision=issuance_revision+1 AND terminal_at_utc IS NOT NULL)))
) ENGINE=InnoDB;

-- The old unique key guaranteed one issuance per scope before this migration.
-- Copy only its actual immutable evidence, including terminal revocations.
-- No grant, revocation, provenance, confirmation or authorization is created.
INSERT INTO c06_delegation_cycle_head
 (tenant_id,subject_membership_id,branch_id,resource_id,action_id,grant_id,role_id,
  issuance_revision,revision,terminal_at_utc)
SELECT g.tenant_id,g.subject_membership_id,g.branch_id,g.resource_id,g.action_id,g.grant_id,g.role_id,
       g.revision,COALESCE(v.revision,g.revision),v.revoked_at_utc
FROM c06_delegation_grant g
LEFT JOIN c06_delegation_revocation v ON v.tenant_id=g.tenant_id AND v.grant_id=g.grant_id;

DELIMITER $$
-- Keep all original BEFORE INSERT authorization guards and immutability
-- triggers from 024. These AFTER INSERT triggers add atomic lifecycle CAS;
-- SIGNAL rolls the entire failed statement back. The caller must roll back
-- its transaction, including any consumed MFA, on any error.
CREATE TRIGGER trg_c06_dg_cycle_head AFTER INSERT ON c06_delegation_grant FOR EACH ROW
BEGIN
 IF NEW.revision=1 THEN
   INSERT INTO c06_delegation_cycle_head
    (tenant_id,subject_membership_id,branch_id,resource_id,action_id,grant_id,role_id,
     issuance_revision,revision,terminal_at_utc)
   VALUES(NEW.tenant_id,NEW.subject_membership_id,NEW.branch_id,NEW.resource_id,NEW.action_id,
          NEW.grant_id,NEW.role_id,NEW.revision,NEW.revision,NULL);
 ELSE
   -- The first reviewed reissue contract supports the same exact role only.
   -- Never reissue from mere expiry, an active row or a different scope.
   UPDATE c06_delegation_cycle_head
      SET grant_id=NEW.grant_id,issuance_revision=NEW.revision,
          revision=NEW.revision,terminal_at_utc=NULL
    WHERE tenant_id=NEW.tenant_id AND subject_membership_id=NEW.subject_membership_id
      AND branch_id=NEW.branch_id AND resource_id=NEW.resource_id AND action_id=NEW.action_id
      AND role_id=NEW.role_id AND revision=NEW.revision-1
      AND issuance_revision=NEW.revision-2
      AND terminal_at_utc IS NOT NULL AND terminal_at_utc<=NEW.issued_at_utc;
   IF ROW_COUNT()<>1 THEN
     SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C06 lifecycle reissue compare-and-swap denied';
   END IF;
 END IF;
END$$
CREATE TRIGGER trg_c06_dv_cycle_head AFTER INSERT ON c06_delegation_revocation FOR EACH ROW
BEGIN
 UPDATE c06_delegation_cycle_head
    SET revision=NEW.revision,terminal_at_utc=NEW.revoked_at_utc
  WHERE tenant_id=NEW.tenant_id AND grant_id=NEW.grant_id
    AND branch_id=NEW.branch_id AND resource_id=NEW.resource_id AND action_id=NEW.action_id
    AND revision=NEW.expected_revision AND issuance_revision=NEW.expected_revision
    AND terminal_at_utc IS NULL;
 IF ROW_COUNT()<>1 THEN
   SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C06 lifecycle revocation compare-and-swap denied';
 END IF;
END$$
DELIMITER ;

-- No direct INSERT/UPDATE/DELETE privilege on the derived head is permitted
-- for application or fixture writer principals. Triggers maintain it under
-- their existing migration authority. The future adapter needs SELECT only.
-- Old runtime readers are not cycle-aware; do not activate 028 independently.
