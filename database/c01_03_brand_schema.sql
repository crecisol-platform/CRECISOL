-- CRECISOL C01-03 | MySQL 8.4 LTS | schema-only, no connection or deployment
-- Apply ONLY to a disposable, isolated test database. Application migrations remain pending review.
-- All identifiers are BINARY(16) RFC4122 byte-order; application adapter must define conversion.
CREATE TABLE brand_scopes (
  scope_kind TINYINT UNSIGNED NOT NULL,
  scope_id BINARY(16) NOT NULL,
  created_at_utc DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  PRIMARY KEY (scope_kind, scope_id),
  CONSTRAINT chk_brand_scope_kind CHECK (scope_kind IN (1, 2)),
  CONSTRAINT chk_brand_scope_identity CHECK (
    (scope_kind = 1 AND scope_id = X'00000000000000000000000000000000')
    OR (scope_kind = 2 AND scope_id <> X'00000000000000000000000000000000')
  )
) ENGINE=InnoDB;

CREATE TABLE brand_revisions (
  scope_kind TINYINT UNSIGNED NOT NULL,
  scope_id BINARY(16) NOT NULL,
  revision BIGINT UNSIGNED NOT NULL,
  publication_status TINYINT UNSIGNED NOT NULL,
  name VARCHAR(100) COLLATE utf8mb4_bin NOT NULL,
  slogan VARCHAR(160) COLLATE utf8mb4_bin NOT NULL,
  descriptor VARCHAR(180) COLLATE utf8mb4_bin NOT NULL,
  primary_color CHAR(7) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  accent_color CHAR(7) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  logo_url VARCHAR(512) COLLATE utf8mb4_bin NOT NULL,
  website_url VARCHAR(512) COLLATE utf8mb4_bin NULL,
  favicon_url VARCHAR(512) COLLATE utf8mb4_bin NULL,
  typography_token VARCHAR(24) COLLATE utf8mb4_bin NULL,
  icon_set_token VARCHAR(24) COLLATE utf8mb4_bin NULL,
  seo_title VARCHAR(160) COLLATE utf8mb4_bin NULL,
  seo_description VARCHAR(300) COLLATE utf8mb4_bin NULL,
  copyright_notice VARCHAR(200) COLLATE utf8mb4_bin NULL,
  published_at_utc DATETIME(6) NULL,
  created_at_utc DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  PRIMARY KEY (scope_kind, scope_id, revision),
  UNIQUE KEY uq_brand_revision_status (scope_kind, scope_id, revision, publication_status),
  CONSTRAINT fk_brand_revision_scope FOREIGN KEY (scope_kind, scope_id)
    REFERENCES brand_scopes(scope_kind, scope_id) ON DELETE RESTRICT,
  CONSTRAINT chk_brand_revision_positive CHECK (revision >= 1),
  CONSTRAINT chk_brand_publication_status CHECK (publication_status IN (1, 2)),
  CONSTRAINT chk_brand_publication_time CHECK (
    (publication_status = 1 AND published_at_utc IS NULL) OR
    (publication_status = 2 AND published_at_utc IS NOT NULL)
  ),
  CONSTRAINT chk_brand_colors CHECK (
    REGEXP_LIKE(primary_color, '^#[0-9A-Fa-f]{6}$', 'c') AND
    REGEXP_LIKE(accent_color, '^#[0-9A-Fa-f]{6}$', 'c')
  ),
  CONSTRAINT chk_brand_names_not_blank CHECK (
    CHAR_LENGTH(TRIM(name)) BETWEEN 2 AND 100 AND
    CHAR_LENGTH(TRIM(slogan)) BETWEEN 2 AND 160 AND
    CHAR_LENGTH(TRIM(descriptor)) BETWEEN 2 AND 180
  )
) ENGINE=InnoDB;

-- An explicit published pointer, restricted by FK to status=2 on the SAME scope.
-- Does NOT authorize writes or infer which tenant the caller may access.
CREATE TABLE brand_publications (
  scope_kind TINYINT UNSIGNED NOT NULL,
  scope_id BINARY(16) NOT NULL,
  published_revision BIGINT UNSIGNED NOT NULL,
  publication_status TINYINT UNSIGNED NOT NULL DEFAULT 2,
  updated_at_utc DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
  PRIMARY KEY (scope_kind, scope_id),
  CONSTRAINT chk_publication_status_fixed CHECK (publication_status = 2),
  CONSTRAINT fk_brand_publication_revision FOREIGN KEY
    (scope_kind, scope_id, published_revision, publication_status)
    REFERENCES brand_revisions(scope_kind, scope_id, revision, publication_status)
    ON DELETE RESTRICT
) ENGINE=InnoDB;

-- No tenant-scoped public API, publication endpoint, secrets, or real seeds are introduced.
-- Database constraints protect structural relations; immutability, access control,
-- domain validation, publication concurrency and audit remain server-side obligations.
