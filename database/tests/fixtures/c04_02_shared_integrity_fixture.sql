-- CRECISOL C04-02: DISPOSABLE TEST FIXTURE ONLY (NOT A MIGRATION).
-- Reproduce shared relational tenant/branch boundaries on pinned MySQL 8.4.
-- This schema is NEVER loaded by the application or released to production.
-- No host, user identity, authorization, DSN or tenant provisioning is supplied.
-- BINARY(16) IDs represent opaque UUID bytes; adapter byte order is FUTURE work.
CREATE TABLE c04_02_fixture_tenants (
  tenant_id BINARY(16) NOT NULL,
  lifecycle_state TINYINT UNSIGNED NOT NULL,
  PRIMARY KEY (tenant_id),
  CONSTRAINT chk_c04_02_tenant_nonzero CHECK (tenant_id <> X'00000000000000000000000000000000'),
  CONSTRAINT chk_c04_02_tenant_state CHECK (lifecycle_state BETWEEN 1 AND 5)
) ENGINE=InnoDB;

CREATE TABLE c04_02_fixture_branches (
  tenant_id BINARY(16) NOT NULL,
  branch_id BINARY(16) NOT NULL,
  display_label VARCHAR(80) COLLATE utf8mb4_bin NOT NULL,
  PRIMARY KEY (tenant_id, branch_id),
  CONSTRAINT fk_c04_02_branch_tenant FOREIGN KEY (tenant_id)
    REFERENCES c04_02_fixture_tenants (tenant_id)
    ON UPDATE RESTRICT ON DELETE RESTRICT,
  CONSTRAINT chk_c04_02_branch_nonzero CHECK (branch_id <> X'00000000000000000000000000000000'),
  CONSTRAINT chk_c04_02_branch_label CHECK (CHAR_LENGTH(TRIM(display_label)) BETWEEN 2 AND 80)
) ENGINE=InnoDB;

CREATE TABLE c04_02_fixture_items (
  tenant_id BINARY(16) NOT NULL,
  branch_id BINARY(16) NOT NULL,
  item_id BINARY(16) NOT NULL,
  external_key VARCHAR(48) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  PRIMARY KEY (tenant_id, branch_id, item_id),
  UNIQUE KEY uq_c04_02_item_scope_key (tenant_id, branch_id, external_key),
  CONSTRAINT fk_c04_02_item_branch FOREIGN KEY (tenant_id, branch_id)
    REFERENCES c04_02_fixture_branches (tenant_id, branch_id)
    ON UPDATE RESTRICT ON DELETE RESTRICT,
  CONSTRAINT chk_c04_02_item_nonzero CHECK (item_id <> X'00000000000000000000000000000000'),
  CONSTRAINT chk_c04_02_item_key CHECK (
    CHAR_LENGTH(external_key) BETWEEN 1 AND 48 AND
    REGEXP_LIKE(external_key, '^[a-zA-Z0-9_-]+$', 'c')
  )
) ENGINE=InnoDB;

CREATE TABLE c04_02_fixture_notes (
  tenant_id BINARY(16) NOT NULL,
  branch_id BINARY(16) NOT NULL,
  item_id BINARY(16) NOT NULL,
  note_id BINARY(16) NOT NULL,
  note_kind TINYINT UNSIGNED NOT NULL,
  PRIMARY KEY (tenant_id, branch_id, item_id, note_id),
  CONSTRAINT fk_c04_02_note_item FOREIGN KEY (tenant_id, branch_id, item_id)
    REFERENCES c04_02_fixture_items (tenant_id, branch_id, item_id)
    ON UPDATE RESTRICT ON DELETE RESTRICT,
  CONSTRAINT chk_c04_02_note_nonzero CHECK (note_id <> X'00000000000000000000000000000000'),
  CONSTRAINT chk_c04_02_note_kind CHECK (note_kind BETWEEN 1 AND 3)
) ENGINE=InnoDB;

-- Architectural proof only: every child PK and FK includes tenant_id,
-- branch ownership is carried through all subordinate resource FKs, and
-- scoped uniqueness never relies on tenant-unspecific resource identity.
-- Future application queries STILL require verified membership, permissions,
-- tenant_id AND branch_id predicates. This fixture does not provide RLS.
