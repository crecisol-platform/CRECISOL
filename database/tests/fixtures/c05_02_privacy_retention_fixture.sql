-- CRECISOL C05-02: disposable MySQL 8.4 PRIVACY test extension only.
-- Requires c05_02_invitation_atomic_fixture.sql and
-- c05_02_neutral_registration_fixture.sql. NOT a production migration.
-- No account, role, email delivery, signup, credentials, memberships or session.
-- Purpose: fail-closed logical crypto-erasure after a neutral intent expires,
-- even if a downstream test proof still references its tenant+intent PK.
CREATE TABLE c05_02_privacy_dispositions (
  tenant_id BINARY(16) NOT NULL,
  intent_id BINARY(16) NOT NULL,
  job_id BINARY(16) NOT NULL,
  disposed_at_utc DATETIME(6) NOT NULL,
  PRIMARY KEY (tenant_id,intent_id),
  KEY ix_c05_02_privacy_job (job_id),
  CONSTRAINT fk_c05_02_privacy_intent
    FOREIGN KEY (tenant_id,intent_id)
    REFERENCES c05_02_neutral_intents(tenant_id,intent_id)
    ON DELETE CASCADE ON UPDATE RESTRICT
) ENGINE=InnoDB;

DELIMITER //
CREATE PROCEDURE c05_02_dispose_expired_registration_pii(
  IN p_batch_size INT
)
SQL SECURITY DEFINER
BEGIN
  DECLARE v_job BINARY(16);
  DECLARE v_now DATETIME(6);
  DECLARE v_bucket BIGINT UNSIGNED;
  DECLARE EXIT HANDLER FOR SQLEXCEPTION
  BEGIN
    ROLLBACK;
    RESIGNAL;
  END;

  IF p_batch_size IS NULL OR p_batch_size < 1 OR p_batch_size > 100 THEN
    SIGNAL SQLSTATE '45000'
      SET MESSAGE_TEXT='Invalid disposal batch size';
  END IF;
  SET v_job = UUID_TO_BIN(UUID(),1);
  SET v_now = UTC_TIMESTAMP(6);
  SET v_bucket = FLOOR(UNIX_TIMESTAMP(v_now) / 900);
  START TRANSACTION;

  -- A unique disposition marker makes replay idempotent. A concurrent
  -- collision fails closed and rolls back; retry is safe, never double-grants.
  -- No tenant_id or row contents are returned to the maintenance caller.
  INSERT INTO c05_02_privacy_dispositions
    (tenant_id,intent_id,job_id,disposed_at_utc)
  SELECT i.tenant_id,i.intent_id,v_job,v_now
    FROM c05_02_neutral_intents AS i
    LEFT JOIN c05_02_privacy_dispositions AS d
      ON d.tenant_id=i.tenant_id AND d.intent_id=i.intent_id
   WHERE i.expires_at_utc <= v_now
     AND d.intent_id IS NULL
   ORDER BY i.tenant_id,i.expires_at_utc,i.intent_id
   LIMIT p_batch_size;

  -- Logical erasure, not a claim of physical disk/backup secure deletion.
  -- Only expired intents selected by THIS atomic transaction are erased.
  -- Keep tenant+intent PK and FK, so existing references remain valid and
  -- cannot bypass the expiry condition in the one-use claim procedure.
  UPDATE c05_02_neutral_intents AS i
    JOIN c05_02_privacy_dispositions AS d
      ON d.tenant_id=i.tenant_id AND d.intent_id=i.intent_id
     AND d.job_id=v_job
     SET i.ciphertext=RANDOM_BYTES(OCTET_LENGTH(i.ciphertext)),
         i.nonce=RANDOM_BYTES(12),
         i.tag=RANDOM_BYTES(16),
         i.mailbox_digest=RANDOM_BYTES(32),
         i.peer_digest=RANDOM_BYTES(32)
   WHERE i.expires_at_utc <= v_now;

  -- Expired quota windows are unnecessary after the current and previous
  -- windows. Fixed local-test retention; NOT an administrative policy value.
  DELETE FROM c05_02_neutral_quota
   WHERE window_bucket < v_bucket - 2
   LIMIT p_batch_size;

  COMMIT;
  SELECT 'ACK' AS result;
END//
DELIMITER ;
