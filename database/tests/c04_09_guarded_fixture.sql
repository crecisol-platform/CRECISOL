-- CRECISOL C04-09: synthetic protected ledger and fencing contract.
-- Executable ONLY inside a fresh throwaway MySQL 8.4 --network none instance.
-- This is NOT a product migration manager, schema patch or an admin auth system.
CREATE DATABASE c04_09_control;
CREATE TABLE c04_09_control.targets (
 target_id VARCHAR(12) CHARACTER SET ascii COLLATE ascii_bin NOT NULL PRIMARY KEY,
 kind ENUM('SHARED','DEDICATED') NOT NULL,
 tenant_id CHAR(1) CHARACTER SET ascii COLLATE ascii_bin NULL,
 slot_id VARCHAR(12) CHARACTER SET ascii COLLATE ascii_bin NULL,
 schema_name VARCHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 placement_revision BIGINT UNSIGNED NOT NULL,
 access_epoch BIGINT UNSIGNED NOT NULL,
 UNIQUE KEY uq_c0409_target_slot (slot_id),
 UNIQUE KEY uq_c0409_target_schema (schema_name),
 CONSTRAINT chk_c0409_target_scope CHECK(
    (kind='SHARED' AND tenant_id IS NULL AND slot_id IS NULL)
    OR (kind='DEDICATED' AND tenant_id IS NOT NULL AND slot_id IS NOT NULL)),
 CONSTRAINT chk_c0409_target_revision CHECK(placement_revision > 0 AND access_epoch > 0)
) ENGINE=InnoDB;

CREATE TABLE c04_09_control.approvals (
 operation_id CHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL PRIMARY KEY,
 target_id VARCHAR(12) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 expected_kind ENUM('SHARED','DEDICATED') NOT NULL,
 expected_tenant CHAR(1) CHARACTER SET ascii COLLATE ascii_bin NULL,
 expected_slot VARCHAR(12) CHARACTER SET ascii COLLATE ascii_bin NULL,
 expected_revision BIGINT UNSIGNED NOT NULL,
 expected_epoch BIGINT UNSIGNED NOT NULL,
 migration_version VARCHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 checksum CHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 approved TINYINT NOT NULL,
 approved_until_utc DATETIME(6) NOT NULL,
 CONSTRAINT fk_c0409_approval_target FOREIGN KEY(target_id)
   REFERENCES c04_09_control.targets(target_id) ON UPDATE RESTRICT ON DELETE RESTRICT,
 CONSTRAINT chk_c0409_approval_bool CHECK(approved IN (0,1)),
 CONSTRAINT chk_c0409_approval_checksum CHECK
  (REGEXP_LIKE(checksum,'^[0-9a-f]{64}$','c')),
 CONSTRAINT chk_c0409_approval_version CHECK
  (REGEXP_LIKE(migration_version,'^[a-z][a-z0-9_-]{2,35}$','c')),
 CONSTRAINT chk_c0409_approval_operation CHECK
  (REGEXP_LIKE(operation_id,'^[0-9a-f]{32}$','c'))
) ENGINE=InnoDB;

CREATE TABLE c04_09_control.gates (
 target_id VARCHAR(12) CHARACTER SET ascii COLLATE ascii_bin NOT NULL PRIMARY KEY,
 inflight_operation CHAR(32) CHARACTER SET ascii COLLATE ascii_bin NULL,
 fence BIGINT UNSIGNED NOT NULL DEFAULT 0,
 CONSTRAINT fk_c0409_gate_target FOREIGN KEY(target_id)
   REFERENCES c04_09_control.targets(target_id) ON DELETE RESTRICT ON UPDATE RESTRICT
) ENGINE=InnoDB;

CREATE TABLE c04_09_control.ledger (
 target_id VARCHAR(12) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 migration_version VARCHAR(36) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 operation_id CHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 checksum CHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 state ENUM('applying','verified','failed','recovered') NOT NULL,
 fence BIGINT UNSIGNED NOT NULL,
 PRIMARY KEY (target_id,migration_version),
 UNIQUE KEY uq_c0409_ledger_op(operation_id),
 CONSTRAINT fk_c0409_ledger_approval FOREIGN KEY(operation_id)
   REFERENCES c04_09_control.approvals(operation_id) ON DELETE RESTRICT ON UPDATE RESTRICT,
 CONSTRAINT fk_c0409_ledger_target FOREIGN KEY(target_id)
   REFERENCES c04_09_control.targets(target_id) ON DELETE RESTRICT ON UPDATE RESTRICT
) ENGINE=InnoDB;

DELIMITER $
-- Valid-target ownership attacks must fail even when the new tenant exists
-- and no PK/unique collision would prevent the reparenting.
CREATE TRIGGER c0409_shared_owner_immutable
BEFORE UPDATE ON c04_09_shared.resources FOR EACH ROW
BEGIN
 IF NOT (OLD.tenant_id <=> NEW.tenant_id) OR
    NOT (OLD.resource_id <=> NEW.resource_id) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Shared resource ownership immutable';
 END IF;
END$
CREATE TRIGGER c0409_target_binding_immutable
BEFORE UPDATE ON c04_09_control.targets FOR EACH ROW
BEGIN
 IF NOT (OLD.tenant_id <=> NEW.tenant_id) OR
    NOT (OLD.slot_id <=> NEW.slot_id) OR
    NOT (OLD.kind <=> NEW.kind) OR
    NOT (OLD.schema_name <=> NEW.schema_name) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Migration target ownership immutable';
 END IF;
END$$
CREATE TRIGGER c0409_ledger_identity_immutable
BEFORE UPDATE ON c04_09_control.ledger FOR EACH ROW
BEGIN
 IF NOT (OLD.target_id <=> NEW.target_id) OR
    NOT (OLD.operation_id <=> NEW.operation_id) OR
    NOT (OLD.migration_version <=> NEW.migration_version) OR
    NOT (OLD.checksum <=> NEW.checksum) OR
    NOT (OLD.fence <=> NEW.fence) OR
    NOT (
       OLD.state <=> NEW.state
       OR (OLD.state='applying' AND NEW.state IN ('verified','failed'))
       OR (OLD.state='failed' AND NEW.state='recovered')
    ) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Migration ledger identity immutable';
 END IF;
END$$
CREATE TRIGGER c0409_ledger_delete_immutable
BEFORE DELETE ON c04_09_control.ledger FOR EACH ROW
BEGIN
 SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Migration ledger cannot be erased';
END$$

-- The caller supplies ONLY an operation ID. Scope, mode, tenant and schema
-- are loaded from protected server-side approval and catalog tables.
-- This fixture does not implement real authenticated backoffice approval.
CREATE PROCEDURE c04_09_control.reserve(IN p_op CHAR(32))
SQL SECURITY DEFINER
BEGIN
 DECLARE v_target VARCHAR(12) DEFAULT NULL;
 DECLARE v_version VARCHAR(36) DEFAULT NULL;
 DECLARE v_hash CHAR(64) DEFAULT NULL;
 DECLARE v_kind VARCHAR(12) DEFAULT NULL;
 DECLARE v_tenant CHAR(1) DEFAULT NULL;
 DECLARE v_slot VARCHAR(12) DEFAULT NULL;
 DECLARE v_rev BIGINT UNSIGNED DEFAULT NULL;
 DECLARE v_epoch BIGINT UNSIGNED DEFAULT NULL;
 DECLARE v_approved TINYINT DEFAULT NULL;
 DECLARE v_expires DATETIME(6) DEFAULT NULL;
 DECLARE v_actual_kind VARCHAR(12) DEFAULT NULL;
 DECLARE v_actual_tenant CHAR(1) DEFAULT NULL;
 DECLARE v_actual_slot VARCHAR(12) DEFAULT NULL;
 DECLARE v_actual_schema VARCHAR(32) DEFAULT NULL;
 DECLARE v_actual_rev BIGINT UNSIGNED DEFAULT NULL;
 DECLARE v_actual_epoch BIGINT UNSIGNED DEFAULT NULL;
 DECLARE v_inflight CHAR(32) DEFAULT NULL;
 DECLARE v_fence BIGINT UNSIGNED DEFAULT 0;
 DECLARE v_existing_op CHAR(32) DEFAULT NULL;
 DECLARE v_existing_hash CHAR(64) DEFAULT NULL;
 DECLARE v_existing_state VARCHAR(12) DEFAULT NULL;
 DECLARE EXIT HANDLER FOR SQLEXCEPTION
 BEGIN
   ROLLBACK;
   RESIGNAL;
 END;

 IF p_op IS NULL OR NOT REGEXP_LIKE(p_op,'^[0-9a-f]{32}$','c') THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Invalid migration operation selector';
 END IF;
 START TRANSACTION;
 SELECT target_id,migration_version,checksum,expected_kind,expected_tenant,
        expected_slot,expected_revision,expected_epoch,approved,approved_until_utc
 INTO v_target,v_version,v_hash,v_kind,v_tenant,v_slot,
      v_rev,v_epoch,v_approved,v_expires
 FROM c04_09_control.approvals WHERE operation_id=p_op FOR UPDATE;
 IF v_target IS NULL OR v_approved <> 1 OR v_expires <= UTC_TIMESTAMP(6) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Missing or inactive approval';
 END IF;
 SELECT kind,tenant_id,slot_id,schema_name,placement_revision,access_epoch
 INTO v_actual_kind,v_actual_tenant,v_actual_slot,v_actual_schema,
      v_actual_rev,v_actual_epoch
 FROM c04_09_control.targets WHERE target_id=v_target FOR UPDATE;
 IF v_actual_kind IS NULL OR
    NOT (v_kind <=> v_actual_kind) OR
    NOT (v_tenant <=> v_actual_tenant) OR
    NOT (v_slot <=> v_actual_slot) OR
    NOT (v_rev <=> v_actual_rev) OR
    NOT (v_epoch <=> v_actual_epoch) OR
    (v_actual_kind='SHARED' AND
       (v_actual_schema <> 'c04_09_shared' OR v_actual_tenant IS NOT NULL
        OR v_actual_slot IS NOT NULL)) OR
    (v_actual_kind='DEDICATED' AND
       (v_actual_tenant IS NULL OR v_actual_slot IS NULL OR
        v_actual_schema NOT IN ('c04_09_ded_a','c04_09_ded_b'))) THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Migration target binding mismatch';
 END IF;
 SELECT inflight_operation,fence INTO v_inflight,v_fence
 FROM c04_09_control.gates WHERE target_id=v_target FOR UPDATE;
 IF v_fence IS NULL OR v_fence >= 18446744073709551614 THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Migration fence invalid';
 END IF;

 SELECT operation_id,checksum,state INTO
   v_existing_op,v_existing_hash,v_existing_state
 FROM c04_09_control.ledger
 WHERE target_id=v_target AND migration_version=v_version FOR UPDATE;
 IF v_existing_op IS NOT NULL THEN
  IF v_existing_op <> p_op OR v_existing_hash <> v_hash
     OR v_existing_state <> 'verified' THEN
   SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Version/checksum/operation conflict or incomplete DDL';
  END IF;
  COMMIT;
  SELECT 'REPLAY' AS outcome;
 ELSE
  IF v_inflight IS NOT NULL THEN
   SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Another migration is still in flight';
  END IF;
  SET v_fence=v_fence+1;
  UPDATE c04_09_control.gates
   SET inflight_operation=p_op,fence=v_fence
   WHERE target_id=v_target AND inflight_operation IS NULL;
  IF ROW_COUNT() <> 1 THEN
   SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Migration reservation CAS lost';
  END IF;
  INSERT INTO c04_09_control.ledger
   (target_id,migration_version,operation_id,checksum,state,fence)
  VALUES(v_target,v_version,p_op,v_hash,'applying',v_fence);
  COMMIT;
  SELECT CONCAT('RESERVED:',v_fence) AS outcome;
 END IF;
END$$

-- Post-DDL finalization checks the exact protected target and fencing token;
-- no automatic DDL rollback is ever asserted.
CREATE PROCEDURE c04_09_control.finalize(IN p_op CHAR(32), IN p_fence BIGINT UNSIGNED)
SQL SECURITY DEFINER
BEGIN
 DECLARE v_target VARCHAR(12) DEFAULT NULL;
 DECLARE v_schema VARCHAR(32) DEFAULT NULL;
 DECLARE v_state VARCHAR(12) DEFAULT NULL;
 DECLARE v_ledger_fence BIGINT UNSIGNED DEFAULT NULL;
 DECLARE v_inflight CHAR(32) DEFAULT NULL;
 DECLARE v_gate_fence BIGINT UNSIGNED DEFAULT NULL;
 DECLARE v_approved TINYINT DEFAULT NULL;
 DECLARE v_expires DATETIME(6) DEFAULT NULL;
 DECLARE v_expected_rev BIGINT UNSIGNED DEFAULT NULL;
 DECLARE v_expected_epoch BIGINT UNSIGNED DEFAULT NULL;
 DECLARE v_current_rev BIGINT UNSIGNED DEFAULT NULL;
 DECLARE v_current_epoch BIGINT UNSIGNED DEFAULT NULL;
 DECLARE v_columns BIGINT DEFAULT 0;
 DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;
 START TRANSACTION;
 SELECT target_id,state,fence INTO v_target,v_state,v_ledger_fence
 FROM c04_09_control.ledger WHERE operation_id=p_op FOR UPDATE;
 IF v_target IS NULL OR v_state <> 'applying' OR v_ledger_fence <> p_fence THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Missing, stale or non-applying migration fence';
 END IF;
 SELECT schema_name,placement_revision,access_epoch
 INTO v_schema,v_current_rev,v_current_epoch
 FROM c04_09_control.targets WHERE target_id=v_target FOR UPDATE;
 SELECT inflight_operation,fence INTO v_inflight,v_gate_fence
 FROM c04_09_control.gates WHERE target_id=v_target FOR UPDATE;
 SELECT approved,approved_until_utc,expected_revision,expected_epoch
 INTO v_approved,v_expires,v_expected_rev,v_expected_epoch
 FROM c04_09_control.approvals WHERE operation_id=p_op FOR UPDATE;
 IF NOT (v_inflight <=> p_op) OR v_gate_fence <> p_fence
    OR v_approved <> 1 OR v_expires <= UTC_TIMESTAMP(6)
    OR v_expected_rev <> v_current_rev OR v_expected_epoch <> v_current_epoch THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Migration approval/revision/fence revoked';
 END IF;
 SELECT COUNT(*) INTO v_columns FROM information_schema.columns
 WHERE table_schema=v_schema AND table_name='resources' AND column_name='migration_tag';
 IF v_columns <> 1 THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='DDL verification failed';
 END IF;
 UPDATE c04_09_control.ledger SET state='verified'
 WHERE operation_id=p_op AND state='applying';
 IF ROW_COUNT() <> 1 THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Ledger CAS lost';
 END IF;
 UPDATE c04_09_control.gates SET inflight_operation=NULL
 WHERE target_id=v_target AND inflight_operation=p_op AND fence=p_fence;
 IF ROW_COUNT() <> 1 THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Gate CAS lost';
 END IF;
 COMMIT;
 SELECT 'VERIFIED' AS outcome;
END$$

-- Operator-only recovery functions. The unprivileged CI caller is NOT
-- granted these capabilities. Failed DDL must first be restored explicitly.
CREATE PROCEDURE c04_09_control.mark_failed(
 IN p_op CHAR(32), IN p_fence BIGINT UNSIGNED)
SQL SECURITY DEFINER
BEGIN
 DECLARE v_target VARCHAR(12) DEFAULT NULL;
 DECLARE v_gate CHAR(32) DEFAULT NULL;
 DECLARE v_fence BIGINT UNSIGNED DEFAULT NULL;
 DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;
 START TRANSACTION;
 SELECT target_id INTO v_target FROM c04_09_control.ledger
 WHERE operation_id=p_op AND fence=p_fence AND state='applying' FOR UPDATE;
 IF v_target IS NULL THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Cannot mark non-applying operation failed';
 END IF;
 SELECT inflight_operation,fence INTO v_gate,v_fence FROM c04_09_control.gates
 WHERE target_id=v_target FOR UPDATE;
 IF NOT (v_gate <=> p_op) OR v_fence <> p_fence THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Failure recovery fencing mismatch';
 END IF;
 UPDATE c04_09_control.ledger SET state='failed' WHERE operation_id=p_op;
 COMMIT;
 SELECT 'FAILED_LOCKED' AS outcome;
END$$

CREATE PROCEDURE c04_09_control.verify_restored_and_release(
 IN p_op CHAR(32), IN p_fence BIGINT UNSIGNED)
SQL SECURITY DEFINER
BEGIN
 DECLARE v_target VARCHAR(12) DEFAULT NULL;
 DECLARE v_schema VARCHAR(32) DEFAULT NULL;
 DECLARE v_gate CHAR(32) DEFAULT NULL;
 DECLARE v_fence BIGINT UNSIGNED DEFAULT NULL;
 DECLARE v_columns BIGINT DEFAULT 0;
 DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;
 START TRANSACTION;
 SELECT target_id INTO v_target FROM c04_09_control.ledger
 WHERE operation_id=p_op AND fence=p_fence AND state='failed' FOR UPDATE;
 IF v_target IS NULL THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='No failed migration to recover';
 END IF;
 SELECT schema_name INTO v_schema FROM c04_09_control.targets
 WHERE target_id=v_target FOR UPDATE;
 SELECT inflight_operation,fence INTO v_gate,v_fence FROM c04_09_control.gates
 WHERE target_id=v_target FOR UPDATE;
 IF NOT (v_gate <=> p_op) OR v_fence <> p_fence THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Recovery fence changed';
 END IF;
 SELECT COUNT(*) INTO v_columns FROM information_schema.columns
 WHERE table_schema=v_schema AND table_name='resources'
 AND column_name='migration_tag';
 IF v_columns <> 0 THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='DDL is still applied: verify backup restoration';
 END IF;
 UPDATE c04_09_control.ledger SET state='recovered'
 WHERE operation_id=p_op AND state='failed';
 UPDATE c04_09_control.gates SET inflight_operation=NULL
 WHERE target_id=v_target AND inflight_operation=p_op AND fence=p_fence;
 IF ROW_COUNT() <> 1 THEN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Recovery CAS lost';
 END IF;
 COMMIT;
 SELECT 'RESTORED' AS outcome;
END$$
DELIMITER ;
