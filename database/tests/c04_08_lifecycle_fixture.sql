-- CRECISOL C04-08 disposable MySQL 8.4/InnoDB fixture.
-- NOT a migration, real tenant table, administrative authentication, payment,
-- activation endpoint, session revocation adapter or production deployment.
-- Never run against an existing database. The CI runner creates a fresh schema.
CREATE TABLE c04_08_tenants (
  tenant_id CHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL PRIMARY KEY,
  lifecycle_state ENUM('Draft','PendingVerification','Active','Suspended','Deactivated') NOT NULL,
  revision BIGINT UNSIGNED NOT NULL,
  access_epoch BIGINT UNSIGNED NOT NULL,
  retained_payload VARCHAR(80) NOT NULL,
  CONSTRAINT chk_c04_08_tenant_revision CHECK (revision > 0 AND access_epoch > 0)
) ENGINE=InnoDB;

CREATE TABLE c04_08_decisions (
  operation_id CHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL PRIMARY KEY,
  tenant_id CHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  actor_id CHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  action ENUM('Suspend','Reactivate','Deactivate') NOT NULL,
  expected_revision BIGINT UNSIGNED NOT NULL,
  expected_epoch BIGINT UNSIGNED NOT NULL,
  reason_code VARCHAR(48) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  intent_fingerprint CHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  approved TINYINT NOT NULL,
  approved_until_utc DATETIME(6) NOT NULL,
  CONSTRAINT fk_c04_08_decision_tenant FOREIGN KEY (tenant_id)
    REFERENCES c04_08_tenants(tenant_id) ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT chk_c04_08_decision_approved CHECK (approved IN (0,1)),
  CONSTRAINT chk_c04_08_decision_reason CHECK
    (REGEXP_LIKE(reason_code, '^[A-Z][A-Z0-9_]{2,47}$','c')),
  CONSTRAINT chk_c04_08_decision_hash CHECK
    (REGEXP_LIKE(intent_fingerprint, '^[0-9a-f]{64}$','c'))
) ENGINE=InnoDB;

CREATE TABLE c04_08_audit (
  operation_id CHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL PRIMARY KEY,
  tenant_id CHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  actor_id CHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  old_state VARCHAR(20) NOT NULL,
  new_state VARCHAR(20) NOT NULL,
  committed_revision BIGINT UNSIGNED NOT NULL,
  committed_epoch BIGINT UNSIGNED NOT NULL,
  reason_code VARCHAR(48) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  occurred_at_utc DATETIME(6) NOT NULL,
  CONSTRAINT fk_c04_08_audit_decision FOREIGN KEY (operation_id)
    REFERENCES c04_08_decisions(operation_id) ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT fk_c04_08_audit_tenant FOREIGN KEY (tenant_id)
    REFERENCES c04_08_tenants(tenant_id) ON DELETE RESTRICT ON UPDATE RESTRICT
) ENGINE=InnoDB;

CREATE TABLE c04_08_receipts (
  operation_id CHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL PRIMARY KEY,
  tenant_id CHAR(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  intent_fingerprint CHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  new_state VARCHAR(20) NOT NULL,
  committed_revision BIGINT UNSIGNED NOT NULL,
  committed_epoch BIGINT UNSIGNED NOT NULL,
  committed_at_utc DATETIME(6) NOT NULL,
  CONSTRAINT fk_c04_08_receipt_audit FOREIGN KEY (operation_id)
    REFERENCES c04_08_audit(operation_id) ON DELETE RESTRICT ON UPDATE RESTRICT
) ENGINE=InnoDB;

DELIMITER $$
CREATE TRIGGER trg_c04_08_audit_update BEFORE UPDATE ON c04_08_audit
FOR EACH ROW BEGIN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Append-only lifecycle audit';
END$$
CREATE TRIGGER trg_c04_08_audit_delete BEFORE DELETE ON c04_08_audit
FOR EACH ROW BEGIN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Cannot erase lifecycle audit';
END$$
CREATE TRIGGER trg_c04_08_receipt_update BEFORE UPDATE ON c04_08_receipts
FOR EACH ROW BEGIN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Immutable lifecycle receipt';
END$$
CREATE TRIGGER trg_c04_08_receipt_delete BEFORE DELETE ON c04_08_receipts
FOR EACH ROW BEGIN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Cannot erase lifecycle receipt';
END$$

-- Synthetic operation ID is read from pre-populated protected decisions;
-- possessing it is NOT authorization in real applications. Real backend
-- principal authentication/MFA/approval must be checked transactionally.
-- No caller-supplied tenant/actor/action/DSN/host parameters are accepted.
CREATE PROCEDURE c04_08_transition(IN p_operation CHAR(32))
SQL SECURITY DEFINER
BEGIN
  DECLARE v_tenant CHAR(32) DEFAULT NULL;
  DECLARE v_actor CHAR(32) DEFAULT NULL;
  DECLARE v_action VARCHAR(12) DEFAULT NULL;
  DECLARE v_reason VARCHAR(48) DEFAULT NULL;
  DECLARE v_fingerprint CHAR(64) DEFAULT NULL;
  DECLARE v_expected BIGINT UNSIGNED DEFAULT NULL;
  DECLARE v_expected_epoch BIGINT UNSIGNED DEFAULT NULL;
  DECLARE v_approved TINYINT DEFAULT NULL;
  DECLARE v_until DATETIME(6) DEFAULT NULL;
  DECLARE v_state VARCHAR(20) DEFAULT NULL;
  DECLARE v_revision BIGINT UNSIGNED DEFAULT NULL;
  DECLARE v_epoch BIGINT UNSIGNED DEFAULT NULL;
  DECLARE v_next VARCHAR(20) DEFAULT NULL;
  DECLARE v_existing CHAR(64) DEFAULT NULL;
  DECLARE v_existing_tenant CHAR(32) DEFAULT NULL;
  DECLARE v_audit_count INTEGER DEFAULT 0;
  DECLARE v_committed DATETIME(6);
  DECLARE EXIT HANDLER FOR SQLEXCEPTION
  BEGIN
    ROLLBACK;
    RESIGNAL;
  END;

  IF p_operation IS NULL OR NOT REGEXP_LIKE(p_operation,'^[0-9a-f]{32}$','c') THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Invalid lifecycle operation';
  END IF;

  START TRANSACTION;
  SELECT tenant_id,actor_id,action,reason_code,intent_fingerprint,
         expected_revision,expected_epoch,approved,approved_until_utc
    INTO v_tenant,v_actor,v_action,v_reason,v_fingerprint,
         v_expected,v_expected_epoch,v_approved,v_until
    FROM c04_08_decisions WHERE operation_id=p_operation FOR UPDATE;

  IF v_tenant IS NULL OR v_actor IS NULL OR v_approved <> 1
     OR v_until IS NULL OR v_until <= UTC_TIMESTAMP(6) THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='No current authorized decision';
  END IF;

  SELECT tenant_id,intent_fingerprint INTO v_existing_tenant,v_existing
    FROM c04_08_receipts WHERE operation_id=p_operation FOR SHARE;
  IF v_existing IS NOT NULL THEN
    SELECT COUNT(*) INTO v_audit_count FROM c04_08_audit
      WHERE operation_id=p_operation AND tenant_id=v_tenant;
    IF NOT (v_existing <=> v_fingerprint) OR
       NOT (v_existing_tenant <=> v_tenant) OR v_audit_count <> 1 THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Conflicting replay or missing audit';
    END IF;
    COMMIT;
    SELECT 'REPLAY' AS outcome;
  ELSE
    SELECT lifecycle_state,revision,access_epoch
      INTO v_state,v_revision,v_epoch
      FROM c04_08_tenants WHERE tenant_id=v_tenant FOR UPDATE;
    IF v_state IS NULL OR v_revision <> v_expected OR v_epoch <> v_expected_epoch
       OR v_revision >= 18446744073709551614 OR
          v_epoch >= 18446744073709551614 THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='State/version conflict';
    END IF;
    IF v_state='Active' AND v_action='Suspend' THEN
      SET v_next='Suspended';
    ELSEIF v_state='Suspended' AND v_action='Reactivate' THEN
      SET v_next='Active';
    ELSEIF v_state IN ('Active','Suspended') AND v_action='Deactivate' THEN
      SET v_next='Deactivated';
    ELSE
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Lifecycle transition denied';
    END IF;

    UPDATE c04_08_tenants SET lifecycle_state=v_next,
      revision=v_expected+1,access_epoch=v_expected_epoch+1
      WHERE tenant_id=v_tenant AND revision=v_expected
        AND access_epoch=v_expected_epoch;
    IF ROW_COUNT() <> 1 THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='CAS lost';
    END IF;
    IF COALESCE(@c04_08_fail_before_commit,0)=1 THEN
      SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Synthetic crash before commit';
    END IF;
    IF COALESCE(@c04_08_hold_transaction,0)=1 THEN
      DO SLEEP(0.4);
    END IF;
    SET v_committed=UTC_TIMESTAMP(6);
    INSERT INTO c04_08_audit
      (operation_id,tenant_id,actor_id,old_state,new_state,
       committed_revision,committed_epoch,reason_code,occurred_at_utc)
      VALUES(p_operation,v_tenant,v_actor,v_state,v_next,
             v_expected+1,v_expected_epoch+1,v_reason,v_committed);
    INSERT INTO c04_08_receipts
      (operation_id,tenant_id,intent_fingerprint,new_state,
       committed_revision,committed_epoch,committed_at_utc)
      VALUES(p_operation,v_tenant,v_fingerprint,v_next,
             v_expected+1,v_expected_epoch+1,v_committed);
    COMMIT;
    SELECT 'APPLIED' AS outcome;
  END IF;
END$$
DELIMITER ;
