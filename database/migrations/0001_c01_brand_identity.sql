-- CRECISOL / C01: normalized BRAND identity schema sketch for MySQL 8.4 LTS.
-- NOT APPLIED to a database. Review on actual MySQL 8.4 before activation.
-- No tenant-facing write/API operations until identity, membership and RBAC exist.
-- The PLATFORM scope identifier is UUID ZERO. TENANT identifiers are verified by
-- the backend tenant registry, never by untrusted browser IDs or Host headers.

CREATE TABLE brand_scopes (
  scope_kind VARCHAR(8) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  scope_id BINARY(16) NOT NULL,
  created_at_utc DATETIME(6) NOT NULL,
  PRIMARY KEY (scope_kind, scope_id),
  CONSTRAINT ck_brand_scope_kind CHECK (scope_kind IN ('PLATFORM', 'TENANT')),
  CONSTRAINT ck_brand_scope_id CHECK (
    (scope_kind = 'PLATFORM' AND scope_id = X'00000000000000000000000000000000') OR
    (scope_kind = 'TENANT' AND scope_id <> X'00000000000000000000000000000000')
  )
) ENGINE=InnoDB;

CREATE TABLE brand_revisions (
  scope_kind VARCHAR(8) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  scope_id BINARY(16) NOT NULL,
  revision_number BIGINT UNSIGNED NOT NULL,
  name VARCHAR(100) NOT NULL,
  slogan VARCHAR(160) NOT NULL,
  descriptor VARCHAR(180) NOT NULL,
  primary_color CHAR(7) NOT NULL,
  accent_color CHAR(7) NOT NULL,
  logo_object_key VARCHAR(255) NOT NULL,
  website_url VARCHAR(512) NULL,
  created_at_utc DATETIME(6) NOT NULL,
  PRIMARY KEY (scope_kind, scope_id, revision_number),
  CONSTRAINT fk_brand_revision_scope FOREIGN KEY (scope_kind, scope_id)
    REFERENCES brand_scopes (scope_kind, scope_id),
  CONSTRAINT ck_brand_revision_number CHECK (revision_number > 0)
) ENGINE=InnoDB;

CREATE TABLE brand_publications (
  scope_kind VARCHAR(8) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  scope_id BINARY(16) NOT NULL,
  revision_number BIGINT UNSIGNED NOT NULL,
  published_at_utc DATETIME(6) NOT NULL,
  PRIMARY KEY (scope_kind, scope_id),
  CONSTRAINT fk_brand_publication_revision
    FOREIGN KEY (scope_kind, scope_id, revision_number)
    REFERENCES brand_revisions (scope_kind, scope_id, revision_number)
) ENGINE=InnoDB;

-- Important: no INSERT or UPDATE privileges, host/tenant selection, or publication
-- service are granted by this migration. Application auth, immutable audit/outbox,
-- referential rules for tenant registry, rollback and live tests remain OPEN.
