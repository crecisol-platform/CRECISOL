-- CRECISOL C05 local fixture: every durable anonymous intent enqueues a
-- backend-only proof dispatch in the SAME insert transaction.
-- Does not expose intent IDs, mailbox or authorization to public clients.
-- Apply after neutral_registration + mailbox_proof fixtures.
CREATE TABLE c05_02_proof_dispatch (
    tenant_id BINARY(16) NOT NULL,
    intent_id BINARY(16) NOT NULL,
    state ENUM('READY','CLAIMED','COMPLETED') NOT NULL DEFAULT 'READY',
    claim_owner BINARY(16) NULL,
    claimed_until_utc DATETIME(6) NULL,
    attempts SMALLINT UNSIGNED NOT NULL DEFAULT 0,
    created_utc DATETIME(6) NOT NULL,
    PRIMARY KEY (tenant_id,intent_id),
    KEY ix_c05_02_dispatch_queue (state,claimed_until_utc,created_utc),
    CONSTRAINT fk_c05_02_dispatch_intent FOREIGN KEY (tenant_id,intent_id)
      REFERENCES c05_02_neutral_intents (tenant_id,intent_id)
      ON UPDATE RESTRICT ON DELETE RESTRICT,
    CHECK (attempts BETWEEN 0 AND 3),
    CHECK (
      (state='READY' AND claim_owner IS NULL AND claimed_until_utc IS NULL)
      OR (state='CLAIMED' AND claim_owner IS NOT NULL AND claimed_until_utc IS NOT NULL)
      OR (state='COMPLETED' AND claim_owner IS NULL AND claimed_until_utc IS NULL)
    )
) ENGINE=InnoDB;

DELIMITER //
CREATE TRIGGER c05_02_enqueue_proof_on_intent_insert
AFTER INSERT ON c05_02_neutral_intents
FOR EACH ROW
BEGIN
    INSERT INTO c05_02_proof_dispatch
        (tenant_id,intent_id,state,claim_owner,claimed_until_utc,attempts,created_utc)
    VALUES (NEW.tenant_id,NEW.intent_id,'READY',NULL,NULL,0,UTC_TIMESTAMP(6));
END//

-- No caller-supplied tenant or intent. Only a service-owned lease key enters.
-- The claimant sees an opaque tenant/intent pair from verified DB ownership,
-- never a mailbox or bearer token. SKIP LOCKED permits parallel safe workers.
CREATE PROCEDURE c05_02_dispatch_claim(IN p_owner BINARY(16))
SQL SECURITY DEFINER
BEGIN
    DECLARE v_tenant BINARY(16) DEFAULT NULL;
    DECLARE v_intent BINARY(16) DEFAULT NULL;
    DECLARE v_none BOOL DEFAULT FALSE;
    DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_none=TRUE;
    DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;

    START TRANSACTION;
    IF p_owner IS NOT NULL AND p_owner<>REPEAT(X'00',16) THEN
        SELECT d.tenant_id,d.intent_id INTO v_tenant,v_intent
          FROM c05_02_proof_dispatch d
          JOIN c05_02_neutral_intents i
            ON i.tenant_id=d.tenant_id AND i.intent_id=d.intent_id
          JOIN c05_02_tenants t ON t.tenant_id=d.tenant_id
         WHERE t.lifecycle='ACTIVE'
           AND i.expires_at_utc>UTC_TIMESTAMP(6)
           AND d.attempts<3
           AND (d.state='READY' OR
                 (d.state='CLAIMED' AND d.claimed_until_utc<UTC_TIMESTAMP(6)))
         ORDER BY d.created_utc,d.intent_id
         LIMIT 1 FOR UPDATE SKIP LOCKED;
        IF NOT v_none AND v_tenant IS NOT NULL AND v_intent IS NOT NULL THEN
            UPDATE c05_02_proof_dispatch
               SET state='CLAIMED',claim_owner=p_owner,
                   claimed_until_utc=DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 2 MINUTE),
                   attempts=attempts+1
             WHERE tenant_id=v_tenant AND intent_id=v_intent
               AND attempts<3;
            IF ROW_COUNT()<>1 THEN SET v_tenant=NULL; SET v_intent=NULL; END IF;
        END IF;
    END IF;
    COMMIT;
    SELECT v_tenant AS tenant_id, v_intent AS intent_id;
END//

-- Delivery finalization is backed by a committed pending/verified proof.
-- Never acknowledge a row for the wrong owner/tenant or after lease expiry.
CREATE PROCEDURE c05_02_dispatch_complete(
    IN p_tenant BINARY(16), IN p_intent BINARY(16), IN p_owner BINARY(16)
)
SQL SECURITY DEFINER
BEGIN
    DECLARE v_done INT DEFAULT 0;
    DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;
    START TRANSACTION;
    UPDATE c05_02_proof_dispatch d
      JOIN c05_02_mailbox_proofs p
        ON p.tenant_id=d.tenant_id AND p.intent_id=d.intent_id
       SET d.state='COMPLETED',d.claim_owner=NULL,d.claimed_until_utc=NULL
     WHERE d.tenant_id=p_tenant AND d.intent_id=p_intent
       AND d.claim_owner=p_owner AND d.state='CLAIMED'
       AND d.claimed_until_utc>UTC_TIMESTAMP(6)
       AND p.lifecycle IN ('PENDING','VERIFIED');
    SET v_done=ROW_COUNT();
    COMMIT;
    SELECT IF(v_done=1,'COMPLETED','DENIED') AS result;
END//
DELIMITER ;
