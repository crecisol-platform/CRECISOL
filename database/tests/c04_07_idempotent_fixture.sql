-- CRECISOL C04-07 | STRICTLY SYNTHETIC disposable MySQL 8.4 fixture.
-- NOT A MIGRATION, NOT a production provisioning API/adapter or existing C04-02
-- tenant catalog. NEVER apply to any existing schema or production instance.
-- The trusted backoffice APPROVAL is not implemented by this fixture.
-- Business key is an INTERNAL global reservation, NOT a hostname/slug.
-- Operation id, tenant id, actor and SHA MUST originate from a trusted plan.
-- All values are binary RFC4122 UUID network order (see C04-02 codec).
CREATE TABLE c04_07_operations (
  operation_id BINARY(16) NOT NULL,
  tenant_id BINARY(16) NOT NULL,
  sponsor_actor_id BINARY(16) NOT NULL,
  business_key VARCHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  address_mode TINYINT UNSIGNED NOT NULL,
  intent_sha256 BINARY(32) NOT NULL,
  committed_at_utc DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  PRIMARY KEY (operation_id),
  UNIQUE KEY uq_c04_07_tenant (tenant_id),
  UNIQUE KEY uq_c04_07_business_key (business_key),
  CONSTRAINT chk_c04_07_operation CHECK
    (operation_id <> X'00000000000000000000000000000000'),
  CONSTRAINT chk_c04_07_tenant CHECK
    (tenant_id <> X'00000000000000000000000000000000'),
  CONSTRAINT chk_c04_07_sponsor CHECK
    (sponsor_actor_id <> X'00000000000000000000000000000000'),
  CONSTRAINT chk_c04_07_business CHECK
    (CHAR_LENGTH(business_key) BETWEEN 3 AND 64
     AND REGEXP_LIKE(business_key, '^[a-z][a-z0-9-]*[a-z0-9]$', 'c')),
  CONSTRAINT chk_c04_07_mode CHECK (address_mode IN (1,2,3))
) ENGINE=InnoDB;

CREATE TABLE c04_07_tenant_drafts (
  tenant_id BINARY(16) NOT NULL,
  operation_id BINARY(16) NOT NULL,
  business_key VARCHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  address_mode TINYINT UNSIGNED NOT NULL,
  lifecycle_state TINYINT UNSIGNED NOT NULL DEFAULT 1,
  created_at_utc DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  PRIMARY KEY (tenant_id),
  UNIQUE KEY uq_c04_07_draft_op (operation_id),
  UNIQUE KEY uq_c04_07_draft_business (business_key),
  CONSTRAINT fk_c04_07_draft_operation FOREIGN KEY (operation_id)
    REFERENCES c04_07_operations(operation_id)
    ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT chk_c04_07_draft_mode CHECK (address_mode IN (1,2,3)),
  CONSTRAINT chk_c04_07_draft_state CHECK (lifecycle_state = 1)
) ENGINE=InnoDB;

DELIMITER $$
CREATE TRIGGER trg_c04_07_op_update
BEFORE UPDATE ON c04_07_operations
FOR EACH ROW BEGIN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Provisioning operation immutable';
END$$
CREATE TRIGGER trg_c04_07_op_delete
BEFORE DELETE ON c04_07_operations
FOR EACH ROW BEGIN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Provisioning operation append only';
END$$
CREATE TRIGGER trg_c04_07_draft_update
BEFORE UPDATE ON c04_07_tenant_drafts
FOR EACH ROW BEGIN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Provisioning draft immutable';
END$$
CREATE TRIGGER trg_c04_07_draft_delete
BEFORE DELETE ON c04_07_tenant_drafts
FOR EACH ROW BEGIN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Provisioning draft append only';
END$$

-- The procedure is a SQL TEST HARNESS for one atomic commit, not wired into
-- Infrastructure/DI. A real adapter must authenticate an approved plan, check
-- its current approval/revocation within its DB transaction, and ensure
-- idempotency across all side effects and provisioning steps before activation.
CREATE PROCEDURE c04_07_create_or_replay(
  IN p_operation BINARY(16), IN p_tenant BINARY(16),
  IN p_actor BINARY(16), IN p_business VARCHAR(64),
  IN p_address_mode TINYINT UNSIGNED, IN p_fingerprint BINARY(32)
)
SQL SECURITY DEFINER
BEGIN
  DECLARE v_duplicate BOOLEAN DEFAULT FALSE;
  DECLARE v_operation BINARY(16) DEFAULT NULL;
  DECLARE v_tenant BINARY(16) DEFAULT NULL;
  DECLARE v_actor BINARY(16) DEFAULT NULL;
  DECLARE v_business VARCHAR(64) CHARACTER SET ascii COLLATE ascii_bin DEFAULT NULL;
  DECLARE v_mode TINYINT UNSIGNED DEFAULT NULL;
  DECLARE v_fingerprint BINARY(32) DEFAULT NULL;
  DECLARE v_draft_count BIGINT DEFAULT 0;
  DECLARE EXIT HANDLER FOR SQLEXCEPTION
  BEGIN
    ROLLBACK;
    RESIGNAL;
  END;

  IF p_operation IS NULL OR p_tenant IS NULL OR p_actor IS NULL OR
     p_business IS NULL OR p_fingerprint IS NULL OR
     p_operation = X'00000000000000000000000000000000' OR
     p_tenant = X'00000000000000000000000000000000' OR
     p_actor = X'00000000000000000000000000000000' OR
     p_address_mode NOT IN (1,2,3) OR
     p_business NOT REGEXP '^[a-z][a-z0-9-]{1,62}[a-z0-9]$' THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Invalid trusted provisioning intent';
  END IF;

  START TRANSACTION;
  BEGIN
    -- Expected InnoDB UNIQUE duplicate only. Do NOT use INSERT IGNORE or
    -- ON DUPLICATE UPDATE: neither may silently rewrite another tenant.
    DECLARE CONTINUE HANDLER FOR 1062 SET v_duplicate = TRUE;
    INSERT INTO c04_07_operations
      (operation_id, tenant_id, sponsor_actor_id,
       business_key, address_mode, intent_sha256)
    VALUES
      (p_operation, p_tenant, p_actor,
       p_business, p_address_mode, p_fingerprint);
  END;

  IF v_duplicate THEN
    -- The duplicate may be on OP, TENANT or BUSINESS key. Return REPLAY ONLY
    -- if the SAME operation and FULL intent match an existing complete draft.
    SELECT operation_id, tenant_id, sponsor_actor_id,
           business_key, address_mode, intent_sha256
      INTO v_operation, v_tenant, v_actor,
           v_business, v_mode, v_fingerprint
      FROM c04_07_operations
     WHERE operation_id = p_operation FOR UPDATE;

    IF v_operation IS NULL OR
       NOT (v_tenant <=> p_tenant) OR
       NOT (v_actor <=> p_actor) OR
       NOT (v_business <=> p_business) OR
       NOT (v_mode <=> p_address_mode) OR
       NOT (v_fingerprint <=> p_fingerprint) THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Idempotency conflict: different tenant or intent';
    END IF;

    SELECT COUNT(*) INTO v_draft_count
      FROM c04_07_tenant_drafts
     WHERE operation_id = p_operation
       AND tenant_id = p_tenant
       AND business_key = p_business
       AND address_mode = p_address_mode
       AND lifecycle_state = 1
     FOR UPDATE;
    IF v_draft_count != 1 THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Partial or altered provisioning receipt';
    END IF;
    COMMIT;
    SELECT 'REPLAY' AS outcome;
  ELSE
    -- Simulated crash BEFORE commit is confined to disposable CI databases.
    IF COALESCE(@c04_07_ci_fail_before_commit, 0) = 1 THEN
      SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'CI synthetic crash before tenant commit';
    END IF;
    IF COALESCE(@c04_07_ci_hold_lock, 0) = 1 THEN
      DO SLEEP(0.7);
    END IF;

    INSERT INTO c04_07_tenant_drafts
      (tenant_id, operation_id, business_key, address_mode, lifecycle_state)
    VALUES (p_tenant, p_operation, p_business, p_address_mode, 1);

    COMMIT;
    SELECT 'CREATED' AS outcome;
  END IF;
END$$
DELIMITER ;
