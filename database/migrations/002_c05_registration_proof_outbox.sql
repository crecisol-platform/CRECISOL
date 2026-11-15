-- C05 operational one-time mailbox proof and encrypted delivery outbox.
-- Apply after 001_c05_identity_operational.sql on an isolated CRECISOL DB.

ALTER TABLE c05_identity_mailbox_proof
  ADD COLUMN failed_attempts TINYINT UNSIGNED NOT NULL DEFAULT 0,
  ADD CONSTRAINT ck_c05_proof_failed_attempts CHECK (failed_attempts <= 5);

CREATE TABLE c05_identity_mailbox_outbox (
    tenant_id BINARY(16) NOT NULL,
    proof_id BINARY(16) NOT NULL,
    payload_ciphertext BINARY(32) NOT NULL,
    payload_nonce BINARY(12) NOT NULL,
    payload_tag BINARY(16) NOT NULL,
    key_version INT UNSIGNED NOT NULL,
    delivery_state TINYINT UNSIGNED NOT NULL DEFAULT 0,
    attempts TINYINT UNSIGNED NOT NULL DEFAULT 0,
    created_at_utc DATETIME(6) NOT NULL,
    expires_at_utc DATETIME(6) NOT NULL,
    leased_until_utc DATETIME(6) NULL,
    delivered_at_utc DATETIME(6) NULL,
    PRIMARY KEY (tenant_id, proof_id),
    KEY ix_c05_proof_outbox_pending (delivery_state, created_at_utc),
    CONSTRAINT fk_c05_proof_outbox_proof FOREIGN KEY (tenant_id, proof_id)
      REFERENCES c05_identity_mailbox_proof (tenant_id, proof_id),
    CONSTRAINT ck_c05_proof_outbox_state CHECK (delivery_state IN (0,1,2,3)),
    CONSTRAINT ck_c05_proof_outbox_attempts CHECK (attempts <= 5),
    CONSTRAINT ck_c05_proof_outbox_expiry CHECK (expires_at_utc > created_at_utc),
    CONSTRAINT ck_c05_proof_outbox_lease CHECK
      ((delivery_state=1 AND leased_until_utc IS NOT NULL) OR
       (delivery_state<>1 AND leased_until_utc IS NULL)),
    CONSTRAINT ck_c05_proof_outbox_delivery CHECK
      ((delivery_state=2 AND delivered_at_utc IS NOT NULL) OR
       (delivery_state<>2 AND delivered_at_utc IS NULL))
) ENGINE=InnoDB;

DELIMITER $$
CREATE PROCEDURE c05_operational_consume_mailbox_proof(
    IN p_tenant BINARY(16), IN p_proof BINARY(16), IN p_digest BINARY(32)
)
SQL SECURITY DEFINER
BEGIN
    DECLARE v_state TINYINT UNSIGNED DEFAULT NULL;
    DECLARE v_attempts TINYINT UNSIGNED DEFAULT NULL;
    DECLARE v_expires DATETIME(6) DEFAULT NULL;
    DECLARE v_result VARCHAR(16) DEFAULT 'DENIED';
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    START TRANSACTION;
    SELECT proof_state, failed_attempts, expires_at_utc
      INTO v_state, v_attempts, v_expires
      FROM c05_identity_mailbox_proof
     WHERE tenant_id=p_tenant AND proof_id=p_proof
     FOR UPDATE;

    IF v_state=0 AND v_expires>UTC_TIMESTAMP(6) AND v_attempts<5 THEN
        IF EXISTS (
            SELECT 1 FROM c05_identity_mailbox_proof
             WHERE tenant_id=p_tenant AND proof_id=p_proof
               AND proof_token_hmac=p_digest AND proof_state=0
        ) THEN
            UPDATE c05_identity_mailbox_proof
               SET proof_state=1, consumed_at_utc=UTC_TIMESTAMP(6)
             WHERE tenant_id=p_tenant AND proof_id=p_proof AND proof_state=0;
            UPDATE c05_identity_mailbox_outbox
               SET delivery_state=3, leased_until_utc=NULL
             WHERE tenant_id=p_tenant AND proof_id=p_proof AND delivery_state IN (0,1);
            SET v_result='VERIFIED';
        ELSE
            UPDATE c05_identity_mailbox_proof
               SET failed_attempts=failed_attempts+1,
                   proof_state=IF(failed_attempts+1>=5,2,proof_state)
             WHERE tenant_id=p_tenant AND proof_id=p_proof AND proof_state=0;
        END IF;
    ELSEIF v_state=0 AND v_expires<=UTC_TIMESTAMP(6) THEN
        UPDATE c05_identity_mailbox_proof
           SET proof_state=3
         WHERE tenant_id=p_tenant AND proof_id=p_proof AND proof_state=0;
        UPDATE c05_identity_mailbox_outbox
           SET delivery_state=3, leased_until_utc=NULL
         WHERE tenant_id=p_tenant AND proof_id=p_proof AND delivery_state IN (0,1);
    END IF;

    COMMIT;
    SELECT v_result AS result;
END$$
DELIMITER ;
