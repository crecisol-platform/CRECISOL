-- CRECISOL C04-02 | proposed application schema, MySQL 8.4 LTS, InnoDB
-- NOT automatically executed. No connection, provisioning, identity, API or private route.
-- Fresh isolated schema only. Existing C01/C03 schemas require separately approved migration.
-- UUID bytes: RFC 4122/network order; conversion adapter and round-trip tests required before use.
-- Tenant ID is NOT a credential. No SQL FK can authorize a SELECT.
CREATE TABLE c04_tenants (
  tenant_id BINARY(16) NOT NULL,
  lifecycle_state TINYINT UNSIGNED NOT NULL,
  created_at_utc DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  PRIMARY KEY (tenant_id),
  CONSTRAINT chk_c04_tenant_id CHECK (tenant_id <> X'00000000000000000000000000000000'),
  CONSTRAINT chk_c04_tenant_state CHECK (lifecycle_state IN (1,2,3,4,5))
) ENGINE=InnoDB;

CREATE TABLE c04_branches (
  tenant_id BINARY(16) NOT NULL,
  branch_id BINARY(16) NOT NULL,
  display_label VARCHAR(80) COLLATE utf8mb4_bin NOT NULL,
  PRIMARY KEY (tenant_id, branch_id),
  CONSTRAINT fk_c04_branch_tenant FOREIGN KEY (tenant_id)
    REFERENCES c04_tenants(tenant_id) ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT chk_c04_branch_id CHECK (branch_id <> X'00000000000000000000000000000000'),
  CONSTRAINT chk_c04_branch_label CHECK (CHAR_LENGTH(TRIM(display_label)) BETWEEN 2 AND 80)
) ENGINE=InnoDB;

CREATE TABLE c04_resources (
  tenant_id BINARY(16) NOT NULL,
  branch_id BINARY(16) NOT NULL,
  resource_id BINARY(16) NOT NULL,
  external_key VARCHAR(48) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  created_at_utc DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  PRIMARY KEY (tenant_id, branch_id, resource_id),
  UNIQUE KEY uq_c04_resource_scope_key (tenant_id, branch_id, external_key),
  CONSTRAINT fk_c04_resource_branch FOREIGN KEY (tenant_id, branch_id)
    REFERENCES c04_branches(tenant_id, branch_id) ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT chk_c04_resource_id CHECK (resource_id <> X'00000000000000000000000000000000'),
  CONSTRAINT chk_c04_resource_key CHECK (
    CHAR_LENGTH(external_key) BETWEEN 1 AND 48
    AND REGEXP_LIKE(external_key, '^[a-zA-Z0-9_-]+$', 'c'))
) ENGINE=InnoDB;

CREATE TABLE c04_resource_events (
  tenant_id BINARY(16) NOT NULL,
  branch_id BINARY(16) NOT NULL,
  resource_id BINARY(16) NOT NULL,
  event_id BINARY(16) NOT NULL,
  event_kind TINYINT UNSIGNED NOT NULL,
  created_at_utc DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  PRIMARY KEY (tenant_id, branch_id, resource_id, event_id),
  CONSTRAINT fk_c04_event_resource FOREIGN KEY (tenant_id, branch_id, resource_id)
    REFERENCES c04_resources(tenant_id, branch_id, resource_id)
    ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT chk_c04_event_id CHECK (event_id <> X'00000000000000000000000000000000'),
  CONSTRAINT chk_c04_event_kind CHECK (event_kind BETWEEN 1 AND 3)
) ENGINE=InnoDB;

-- WARNING: these tables are intentionally NOT wired to EF, API, DI or tenant routing.
-- Domain-specific resources, authorization, branch membership, migration tracking,
-- retention, index performance and operational rollback remain NO-GO until tested.

-- A valid parent FK cannot by itself prevent an UPDATE that reparents a row
-- to ANOTHER EXISTING TENANT or branch. Freeze ownership identifiers even when
-- the target would satisfy all PK/FK/UNIQUE constraints. MySQL 8.4 trigger
-- errors are SQLSTATE 45000, code 1644; verified with valid-target attacks.
-- These triggers do NOT grant identity, membership, SELECT filtering, or roles.
DELIMITER $$
CREATE TRIGGER trg_c04_tenant_identity_immutable
BEFORE UPDATE ON c04_tenants
FOR EACH ROW
BEGIN
  IF NOT (OLD.tenant_id <=> NEW.tenant_id) THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'C04 tenant identity immutable';
  END IF;
END$$

CREATE TRIGGER trg_c04_branch_ownership_immutable
BEFORE UPDATE ON c04_branches
FOR EACH ROW
BEGIN
  IF NOT (OLD.tenant_id <=> NEW.tenant_id)
    OR NOT (OLD.branch_id <=> NEW.branch_id) THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'C04 branch ownership immutable';
  END IF;
END$$

CREATE TRIGGER trg_c04_resource_ownership_immutable
BEFORE UPDATE ON c04_resources
FOR EACH ROW
BEGIN
  IF NOT (OLD.tenant_id <=> NEW.tenant_id)
    OR NOT (OLD.branch_id <=> NEW.branch_id)
    OR NOT (OLD.resource_id <=> NEW.resource_id) THEN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'C04 resource ownership immutable';
  END IF;
END$$

CREATE TRIGGER trg_c04_event_append_only_update
BEFORE UPDATE ON c04_resource_events
FOR EACH ROW
BEGIN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'C04 event is append only';
END$$

CREATE TRIGGER trg_c04_event_append_only_delete
BEFORE DELETE ON c04_resource_events
FOR EACH ROW
BEGIN
  SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'C04 event cannot be removed';
END$$
DELIMITER ;
