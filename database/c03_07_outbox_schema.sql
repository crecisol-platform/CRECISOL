-- CRECISOL C03-07: additive test-only outbox foundation, MySQL 8.4 LTS.
-- NEVER applied to production in this chapter. No API/writes are enabled.
-- Business updates and AppendWithinTransactionAsync MUST use one InnoDB tx.
-- scope_kind: 1=platform (zero scope_id); 2=tenant (nonzero scope_id).
-- Tenant event creation/dispatch remains disabled until verified auth/tenancy.
CREATE TABLE IF NOT EXISTS c03_07_outbox_events (
  event_id BINARY(16) NOT NULL,
  scope_kind TINYINT UNSIGNED NOT NULL,
  scope_id BINARY(16) NOT NULL,
  branch_id BINARY(16) NULL,
  event_type VARCHAR(96) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  schema_version INT UNSIGNED NOT NULL,
  payload_json JSON NOT NULL,
  correlation_id BINARY(16) NOT NULL,
  causation_id BINARY(16) NULL,
  occurred_at_utc DATETIME(6) NOT NULL,
  status VARCHAR(16) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  available_at_utc DATETIME(6) NOT NULL,
  created_at_utc DATETIME(6) NOT NULL,
  attempt_count INT NOT NULL DEFAULT 0,
  lease_generation BIGINT NOT NULL DEFAULT 0,
  lease_owner BINARY(16) NULL,
  lease_expires_at_utc DATETIME(6) NULL,
  last_failure_code VARCHAR(32) CHARACTER SET ascii COLLATE ascii_bin NULL,
  PRIMARY KEY (event_id),
  UNIQUE KEY uq_c03_07_scope_event (scope_kind, scope_id, event_id),
  KEY ix_c03_07_claim (scope_kind, scope_id, status, available_at_utc, lease_expires_at_utc, event_id),
  CONSTRAINT chk_c03_07_scope_kind CHECK (scope_kind IN (1,2)),
  CONSTRAINT chk_c03_07_scope_identity CHECK (
    (scope_kind=1 AND scope_id=X'00000000000000000000000000000000' AND branch_id IS NULL)
    OR (scope_kind=2 AND scope_id<>X'00000000000000000000000000000000')
  ),
  CONSTRAINT chk_c03_07_version CHECK (schema_version>0),
  CONSTRAINT chk_c03_07_status CHECK (status IN ('pending','leased','delivered','retryable','dead-letter')),
  CONSTRAINT chk_c03_07_attempt CHECK (attempt_count>=0 AND attempt_count<=3),
  CONSTRAINT chk_c03_07_lease CHECK (
    (status='leased' AND lease_owner IS NOT NULL AND lease_expires_at_utc IS NOT NULL)
    OR (status<>'leased' AND lease_owner IS NULL AND lease_expires_at_utc IS NULL)
  )
) ENGINE=InnoDB;

-- Durable receipt unique within the SAME scope and specific consumer.
-- No global consumer cross-tenant receipt can satisfy another tenant event.
CREATE TABLE IF NOT EXISTS c03_07_inbox_receipts (
  scope_kind TINYINT UNSIGNED NOT NULL,
  scope_id BINARY(16) NOT NULL,
  consumer_name VARCHAR(96) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  event_id BINARY(16) NOT NULL,
  processed_at_utc DATETIME(6) NOT NULL,
  PRIMARY KEY (scope_kind, scope_id, consumer_name, event_id),
  CONSTRAINT fk_c03_07_receipt_event FOREIGN KEY (scope_kind, scope_id, event_id)
    REFERENCES c03_07_outbox_events (scope_kind, scope_id, event_id)
    ON UPDATE RESTRICT ON DELETE RESTRICT
) ENGINE=InnoDB;
