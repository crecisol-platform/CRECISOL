-- Local-only test dispatch for encrypted mailbox proofs.
-- The product schema stays usable with an independent provider integration;
-- this worker is restricted in API composition to loopback MySQL and
-- example.test recipients. It does not create accounts or permissions.

ALTER TABLE c05_identity_mailbox_outbox
  DROP CHECK ck_c05_proof_outbox_lease,
  ADD COLUMN lease_owner BINARY(16) NULL AFTER leased_until_utc,
  ADD CONSTRAINT ck_c05_proof_outbox_lease CHECK
    ((delivery_state=1 AND leased_until_utc IS NOT NULL AND lease_owner IS NOT NULL) OR
     (delivery_state<>1 AND leased_until_utc IS NULL AND lease_owner IS NULL));

DELIMITER $$
CREATE TRIGGER c05_trg_proof_outbox_release_lease
BEFORE UPDATE ON c05_identity_mailbox_outbox
FOR EACH ROW
BEGIN
    IF NEW.delivery_state<>1 THEN
        SET NEW.lease_owner=NULL;
    END IF;
END$$
DELIMITER ;

DELIMITER $$
CREATE PROCEDURE c05_operational_claim_mailbox_proof_outbox(IN p_owner BINARY(16))
SQL SECURITY DEFINER
BEGIN
    DECLARE v_tenant BINARY(16) DEFAULT NULL;
    DECLARE v_proof BINARY(16) DEFAULT NULL;
    DECLARE v_missing TINYINT DEFAULT 0;
    DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_missing=1;
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    IF p_owner IS NULL OR p_owner=UNHEX(REPEAT('00',16)) THEN
        SELECT 'DENIED' AS result;
    ELSE
        START TRANSACTION;
        -- Recover a process that stopped after claiming. SMTP delivery is
        -- at-least-once across this boundary, so the opaque proof is stable.
        UPDATE c05_identity_mailbox_outbox
           SET delivery_state=IF(attempts>=3 OR expires_at_utc<=UTC_TIMESTAMP(6),3,0),
               leased_until_utc=NULL,lease_owner=NULL
         WHERE delivery_state=1 AND leased_until_utc<=UTC_TIMESTAMP(6);
        UPDATE c05_identity_mailbox_outbox
           SET delivery_state=3
         WHERE delivery_state=0 AND expires_at_utc<=UTC_TIMESTAMP(6);
        UPDATE c05_identity_mailbox_proof p
        JOIN c05_identity_mailbox_outbox o
          ON o.tenant_id=p.tenant_id AND o.proof_id=p.proof_id
           SET p.proof_state=3
         WHERE o.delivery_state=3 AND p.proof_state=0
           AND (o.attempts>=3 OR o.expires_at_utc<=UTC_TIMESTAMP(6));

        SELECT o.tenant_id,o.proof_id INTO v_tenant,v_proof
          FROM c05_identity_mailbox_outbox o
          JOIN c05_identity_mailbox_proof p
            ON p.tenant_id=o.tenant_id AND p.proof_id=o.proof_id
          JOIN c05_identity_registration_intent i
            ON i.tenant_id=o.tenant_id AND i.intent_id=o.proof_id
          JOIN c04_tenant_catalog t ON t.tenant_id=o.tenant_id
         WHERE o.delivery_state=0 AND o.attempts<3
           AND o.expires_at_utc>UTC_TIMESTAMP(6)
           AND p.proof_state=0 AND p.expires_at_utc>UTC_TIMESTAMP(6)
           AND i.state=0 AND i.expires_at_utc>UTC_TIMESTAMP(6)
           AND t.lifecycle_state=3
         ORDER BY o.created_at_utc,o.tenant_id,o.proof_id
         LIMIT 1 FOR UPDATE SKIP LOCKED;

        IF v_missing=1 OR v_tenant IS NULL OR v_proof IS NULL THEN
            COMMIT;
            SELECT 'EMPTY' AS result;
        ELSE
            UPDATE c05_identity_mailbox_outbox
               SET delivery_state=1,attempts=attempts+1,
                   leased_until_utc=UTC_TIMESTAMP(6)+INTERVAL 2 MINUTE,
                   lease_owner=p_owner
             WHERE tenant_id=v_tenant AND proof_id=v_proof
               AND delivery_state=0 AND attempts<3;

            IF ROW_COUNT()<>1 THEN
                ROLLBACK;
                SELECT 'DENIED' AS result;
            ELSE
                COMMIT;
                SELECT 'CLAIMED' AS result;
                SELECT o.tenant_id,o.proof_id,p.mailbox_hmac,p.proof_token_hmac,
                       p.mailbox_ciphertext,p.mailbox_nonce,p.mailbox_tag,p.key_version,
                       o.payload_ciphertext,o.payload_nonce,o.payload_tag,o.key_version
                  FROM c05_identity_mailbox_outbox o
                  JOIN c05_identity_mailbox_proof p
                    ON p.tenant_id=o.tenant_id AND p.proof_id=o.proof_id
                 WHERE o.tenant_id=v_tenant AND o.proof_id=v_proof
                   AND o.delivery_state=1 AND o.lease_owner=p_owner;
            END IF;
        END IF;
    END IF;
END$$

CREATE PROCEDURE c05_operational_complete_mailbox_proof_delivery(
    IN p_tenant BINARY(16), IN p_proof BINARY(16),
    IN p_owner BINARY(16), IN p_delivered BOOLEAN
)
SQL SECURITY DEFINER
BEGIN
    DECLARE v_attempts TINYINT UNSIGNED DEFAULT NULL;
    DECLARE v_expires DATETIME(6) DEFAULT NULL;
    DECLARE v_state TINYINT UNSIGNED DEFAULT NULL;
    DECLARE v_result VARCHAR(16) DEFAULT 'DENIED';
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    IF p_tenant IS NULL OR p_proof IS NULL OR p_owner IS NULL OR
       p_owner=UNHEX(REPEAT('00',16)) OR p_delivered IS NULL THEN
        SELECT 'DENIED' AS result;
    ELSE
        START TRANSACTION;
        SELECT o.attempts,o.expires_at_utc,p.proof_state
          INTO v_attempts,v_expires,v_state
          FROM c05_identity_mailbox_outbox o
          JOIN c05_identity_mailbox_proof p
            ON p.tenant_id=o.tenant_id AND p.proof_id=o.proof_id
         WHERE o.tenant_id=p_tenant AND o.proof_id=p_proof
           AND o.delivery_state=1 AND o.lease_owner=p_owner
           AND o.leased_until_utc>UTC_TIMESTAMP(6)
         FOR UPDATE;

        IF v_attempts IS NOT NULL AND v_state=0 AND v_expires>UTC_TIMESTAMP(6) THEN
            IF p_delivered=1 THEN
                UPDATE c05_identity_mailbox_outbox
                   SET delivery_state=2,delivered_at_utc=UTC_TIMESTAMP(6),
                       leased_until_utc=NULL,lease_owner=NULL
                 WHERE tenant_id=p_tenant AND proof_id=p_proof
                   AND delivery_state=1 AND lease_owner=p_owner;
                SET v_result='DELIVERED';
            ELSEIF v_attempts<3 THEN
                UPDATE c05_identity_mailbox_outbox
                   SET delivery_state=0,leased_until_utc=NULL,lease_owner=NULL
                 WHERE tenant_id=p_tenant AND proof_id=p_proof
                   AND delivery_state=1 AND lease_owner=p_owner;
                SET v_result='RETRY';
            ELSE
                UPDATE c05_identity_mailbox_outbox
                   SET delivery_state=3,leased_until_utc=NULL,lease_owner=NULL
                 WHERE tenant_id=p_tenant AND proof_id=p_proof
                   AND delivery_state=1 AND lease_owner=p_owner;
                UPDATE c05_identity_mailbox_proof
           SET proof_state=3
                 WHERE tenant_id=p_tenant AND proof_id=p_proof AND proof_state=0;
                SET v_result='REVOKED';
            END IF;
        END IF;
        COMMIT;
        SELECT v_result AS result;
    END IF;
END$$
DELIMITER ;
