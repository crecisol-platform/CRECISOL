-- C06-04 isolated authorization metadata for one real C05 case-read action.
-- This is NOT a role seed, API, permission grant or migration for production.
-- Apply only after C05 001..018 in disposable MySQL 8.4.
CREATE TABLE c06_case_resource_assignment (
  tenant_id BINARY(16) NOT NULL,
  assignment_id BINARY(16) NOT NULL,
  branch_id BINARY(16) NOT NULL,
  resource_id BINARY(16) NOT NULL,
  account_id BINARY(16) NOT NULL,
  approved_by_account_id BINARY(16) NOT NULL,
  action_id VARCHAR(120) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  assignment_state TINYINT UNSIGNED NOT NULL DEFAULT 1,
  revision BIGINT UNSIGNED NOT NULL DEFAULT 1,
  issued_at_utc DATETIME(6) NOT NULL,
  expires_at_utc DATETIME(6) NOT NULL,
  revoked_at_utc DATETIME(6) NULL,
  PRIMARY KEY (tenant_id, assignment_id),
  UNIQUE KEY uq_c06_case_actor_action
    (tenant_id,branch_id,resource_id,account_id,action_id),
  KEY ix_c06_case_exact_active
    (tenant_id,account_id,branch_id,resource_id,action_id,assignment_state,revoked_at_utc),
  CONSTRAINT fk_c06_case_resource
    FOREIGN KEY(tenant_id,branch_id,resource_id)
    REFERENCES c05_identity_private_resource(tenant_id,branch_id,resource_id)
    ON UPDATE RESTRICT ON DELETE RESTRICT,
  CONSTRAINT fk_c06_case_member
    FOREIGN KEY(tenant_id,account_id)
    REFERENCES c05_identity_membership(tenant_id,account_id)
    ON UPDATE RESTRICT ON DELETE RESTRICT,
  CONSTRAINT fk_c06_case_approver
    FOREIGN KEY(tenant_id,approved_by_account_id)
    REFERENCES c05_identity_membership(tenant_id,account_id)
    ON UPDATE RESTRICT ON DELETE RESTRICT,
  CONSTRAINT ck_c06_case_action_exact CHECK (action_id='identity.private.read'),
  CONSTRAINT ck_c06_case_state CHECK (assignment_state IN (1,2,3)),
  CONSTRAINT ck_c06_case_revision CHECK (revision > 0),
  CONSTRAINT ck_c06_case_time CHECK (
    expires_at_utc>issued_at_utc
    AND expires_at_utc<=issued_at_utc+INTERVAL 30 DAY),
  CONSTRAINT ck_c06_case_revocation CHECK (
    (assignment_state=3 AND revoked_at_utc IS NOT NULL)
    OR (assignment_state IN(1,2) AND revoked_at_utc IS NULL)),
  CONSTRAINT ck_c06_case_key CHECK (
    assignment_id <> X'00000000000000000000000000000000')
) ENGINE=InnoDB;

DELIMITER $$
CREATE TRIGGER trg_c06_case_assignment_immutable
BEFORE UPDATE ON c06_case_resource_assignment FOR EACH ROW
BEGIN
  IF NOT (OLD.tenant_id <=> NEW.tenant_id)
     OR NOT (OLD.assignment_id <=> NEW.assignment_id)
     OR NOT (OLD.branch_id <=> NEW.branch_id)
     OR NOT (OLD.resource_id <=> NEW.resource_id)
     OR NOT (OLD.account_id <=> NEW.account_id)
     OR NOT (OLD.approved_by_account_id <=> NEW.approved_by_account_id)
     OR NOT (OLD.action_id <=> NEW.action_id)
     OR NEW.revision <= OLD.revision
     OR (OLD.assignment_state=3 AND NEW.assignment_state<>3)
     OR (OLD.revoked_at_utc IS NOT NULL AND NEW.revoked_at_utc IS NULL)
  THEN
    SIGNAL SQLSTATE '45000'
      SET MESSAGE_TEXT='C06 case assignment identity or revocation immutable';
  END IF;
END$$
CREATE TRIGGER trg_c06_case_assignment_no_delete
BEFORE DELETE ON c06_case_resource_assignment FOR EACH ROW
BEGIN
  SIGNAL SQLSTATE '45000'
    SET MESSAGE_TEXT='C06 case assignments are not deleted, only revoked';
END$$
DELIMITER ;
