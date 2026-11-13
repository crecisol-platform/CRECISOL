-- CRECISOL C05-02, isolated local MySQL 8.4 test fixture only.
-- Load invitation, neutral-intent and mailbox-proof fixtures FIRST.
-- Not a migration. Never add to production, shared or dedicated tenant DB.
-- No plaintext bearer or mailbox, accounts, memberships, JWT or sessions.
CREATE TABLE c05_02_delivery_outbox (
    tenant_id BINARY(16) NOT NULL,
    intent_id BINARY(16) NOT NULL,
    challenge_digest BINARY(32) NOT NULL,
    nonce BINARY(12) NOT NULL,
    tag BINARY(16) NOT NULL,
    ciphertext BINARY(32) NOT NULL,
    key_version INT UNSIGNED NOT NULL,
    lifecycle ENUM('PENDING','LEASED','SENT','REVOKED') NOT NULL,
    attempts TINYINT UNSIGNED NOT NULL,
    lease_nonce BINARY(16) NULL,
    leased_until_utc DATETIME(6) NULL,
    issued_utc DATETIME(6) NOT NULL,
    expires_utc DATETIME(6) NOT NULL,
    sent_utc DATETIME(6) NULL,
    PRIMARY KEY (tenant_id,intent_id),
    KEY ix_c05_02_outbox_lease (tenant_id,lifecycle,leased_until_utc),
    CONSTRAINT fk_c05_02_outbox_proof
        FOREIGN KEY (tenant_id,intent_id)
        REFERENCES c05_02_mailbox_proofs(tenant_id,intent_id)
        ON UPDATE RESTRICT ON DELETE RESTRICT,
    CHECK (key_version > 0 AND attempts BETWEEN 0 AND 3),
    CHECK (expires_utc > issued_utc),
    CHECK (
        (lifecycle='LEASED' AND lease_nonce IS NOT NULL
            AND leased_until_utc IS NOT NULL)
        OR (lifecycle<>'LEASED' AND lease_nonce IS NULL
            AND leased_until_utc IS NULL)
    ),
    CHECK ((lifecycle='SENT' AND sent_utc IS NOT NULL)
        OR (lifecycle<>'SENT' AND sent_utc IS NULL))
) ENGINE=InnoDB;

DELIMITER //
-- Issue the proof AND durable AEAD envelope atomically. An issue with
-- a missing/expired intent or suspended tenant persists nothing.
CREATE PROCEDURE c05_02_outbox_issue(
    IN p_tenant BINARY(16), IN p_intent BINARY(16),
    IN p_digest BINARY(32), IN p_nonce BINARY(12),
    IN p_tag BINARY(16), IN p_ciphertext BINARY(32),
    IN p_key_version INT,
    IN p_issued DATETIME(6), IN p_expires DATETIME(6)
)
SQL SECURITY DEFINER
BEGIN
    DECLARE v_tenant_state VARCHAR(32) DEFAULT NULL;
    DECLARE v_intent_expiry DATETIME(6) DEFAULT NULL;
    DECLARE v_existing INT DEFAULT 0;
    DECLARE v_issued INT DEFAULT 0;
    DECLARE EXIT HANDLER FOR 1062
    BEGIN
        ROLLBACK;
        SELECT 'DENIED' AS result;
    END;
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    START TRANSACTION;
    -- Global lock order: tenant -> intent -> proof -> outbox.
    SELECT lifecycle INTO v_tenant_state FROM c05_02_tenants
      WHERE tenant_id=p_tenant FOR SHARE;
    SELECT expires_at_utc INTO v_intent_expiry FROM c05_02_neutral_intents
      WHERE tenant_id=p_tenant AND intent_id=p_intent FOR UPDATE;

    IF v_tenant_state='ACTIVE'
        AND v_intent_expiry>UTC_TIMESTAMP(6)
        AND p_key_version>0
        AND p_issued<=UTC_TIMESTAMP(6)
        AND p_issued>=DATE_SUB(UTC_TIMESTAMP(6),INTERVAL 5 MINUTE)
        AND p_expires>UTC_TIMESTAMP(6)
        AND p_expires<=v_intent_expiry
        AND TIMESTAMPDIFF(SECOND,p_issued,p_expires) BETWEEN 60 AND 600
    THEN
        SELECT COUNT(*) INTO v_existing FROM c05_02_mailbox_proofs
          WHERE tenant_id=p_tenant AND intent_id=p_intent;
        IF v_existing=0 THEN
            INSERT INTO c05_02_mailbox_proofs
                (tenant_id,intent_id,challenge_digest,lifecycle,attempts,
                 issued_utc,expires_utc,verified_utc)
            VALUES(p_tenant,p_intent,p_digest,'PENDING',1,
                   p_issued,p_expires,NULL);
            INSERT INTO c05_02_delivery_outbox
                (tenant_id,intent_id,challenge_digest,nonce,tag,ciphertext,
                 key_version,lifecycle,attempts,lease_nonce,leased_until_utc,
                 issued_utc,expires_utc,sent_utc)
            VALUES(p_tenant,p_intent,p_digest,p_nonce,p_tag,p_ciphertext,
                   p_key_version,'PENDING',0,NULL,NULL,p_issued,p_expires,NULL);
            SET v_issued=1;
        END IF;
    END IF;
    COMMIT;
    SELECT IF(v_issued=1,'ISSUED','DENIED') AS result;
END//

-- At most one valid worker lease; all data returned is protected ciphertext.
-- Crash recovery can retry SAME ciphertext, never create a second proof.
CREATE PROCEDURE c05_02_outbox_claim(
    IN p_tenant BINARY(16), IN p_intent BINARY(16),
    IN p_lease BINARY(16)
)
SQL SECURITY DEFINER
BEGIN
    DECLARE v_tenant_state VARCHAR(32) DEFAULT NULL;
    DECLARE v_intent_expiry DATETIME(6) DEFAULT NULL;
    DECLARE v_proof_state VARCHAR(16) DEFAULT NULL;
    DECLARE v_proof_digest BINARY(32) DEFAULT NULL;
    DECLARE v_proof_expiry DATETIME(6) DEFAULT NULL;
    DECLARE v_state VARCHAR(16) DEFAULT NULL;
    DECLARE v_attempts INT DEFAULT 3;
    DECLARE v_lease_until DATETIME(6) DEFAULT NULL;
    DECLARE v_outbox_digest BINARY(32) DEFAULT NULL;
    DECLARE v_outbox_expiry DATETIME(6) DEFAULT NULL;
    DECLARE v_claimed INT DEFAULT 0;
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    START TRANSACTION;
    SELECT lifecycle INTO v_tenant_state FROM c05_02_tenants
      WHERE tenant_id=p_tenant FOR SHARE;
    SELECT expires_at_utc INTO v_intent_expiry FROM c05_02_neutral_intents
      WHERE tenant_id=p_tenant AND intent_id=p_intent FOR SHARE;
    SELECT lifecycle,challenge_digest,expires_utc
      INTO v_proof_state,v_proof_digest,v_proof_expiry
      FROM c05_02_mailbox_proofs
      WHERE tenant_id=p_tenant AND intent_id=p_intent FOR UPDATE;
    SELECT lifecycle,attempts,leased_until_utc,challenge_digest,expires_utc
      INTO v_state,v_attempts,v_lease_until,v_outbox_digest,v_outbox_expiry
      FROM c05_02_delivery_outbox
      WHERE tenant_id=p_tenant AND intent_id=p_intent FOR UPDATE;

    IF v_tenant_state='ACTIVE'
        AND v_intent_expiry>UTC_TIMESTAMP(6)
        AND v_proof_state='PENDING'
        AND v_proof_expiry>UTC_TIMESTAMP(6)
        AND v_outbox_expiry>UTC_TIMESTAMP(6)
        AND v_proof_digest=v_outbox_digest
        AND (v_state='PENDING' OR
            (v_state='LEASED' AND v_lease_until<=UTC_TIMESTAMP(6)))
    THEN
        IF v_attempts<3 THEN
            UPDATE c05_02_delivery_outbox SET
                lifecycle='LEASED',attempts=attempts+1,
                lease_nonce=p_lease,
                leased_until_utc=DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 120 SECOND)
            WHERE tenant_id=p_tenant AND intent_id=p_intent
              AND (lifecycle='PENDING' OR lifecycle='LEASED');
            SET v_claimed=ROW_COUNT();
        ELSE
            UPDATE c05_02_mailbox_proofs SET lifecycle='REVOKED'
             WHERE tenant_id=p_tenant AND intent_id=p_intent
               AND lifecycle='PENDING';
            UPDATE c05_02_delivery_outbox SET
                lifecycle='REVOKED',lease_nonce=NULL,leased_until_utc=NULL
             WHERE tenant_id=p_tenant AND intent_id=p_intent;
        END IF;
    END IF;
    COMMIT;

    IF v_claimed=1 THEN
        SELECT challenge_digest,nonce,tag,ciphertext,key_version
          FROM c05_02_delivery_outbox
         WHERE tenant_id=p_tenant AND intent_id=p_intent
           AND lifecycle='LEASED' AND lease_nonce=p_lease;
    END IF;
END//

-- Shrink the suspend/revoke race before touching the trusted local delivery.
-- This is a point-in-time check, NOT an authorization to send from production.
CREATE PROCEDURE c05_02_outbox_can_dispatch(
    IN p_tenant BINARY(16), IN p_intent BINARY(16),
    IN p_lease BINARY(16)
)
SQL SECURITY DEFINER
BEGIN
    SELECT IF(EXISTS(
        SELECT 1 FROM c05_02_delivery_outbox o
        JOIN c05_02_mailbox_proofs p
          ON p.tenant_id=o.tenant_id AND p.intent_id=o.intent_id
        JOIN c05_02_neutral_intents i
          ON i.tenant_id=o.tenant_id AND i.intent_id=o.intent_id
        JOIN c05_02_tenants t ON t.tenant_id=o.tenant_id
        WHERE o.tenant_id=p_tenant AND o.intent_id=p_intent
          AND o.lifecycle='LEASED' AND o.lease_nonce=p_lease
          AND o.leased_until_utc>UTC_TIMESTAMP(6)
          AND o.expires_utc>UTC_TIMESTAMP(6)
          AND i.expires_at_utc>UTC_TIMESTAMP(6)
          AND p.lifecycle='PENDING' AND p.challenge_digest=o.challenge_digest
          AND p.expires_utc>UTC_TIMESTAMP(6) AND t.lifecycle='ACTIVE'
    ),'ALLOW','DENIED') AS result;
END//

CREATE PROCEDURE c05_02_outbox_mark_sent(
    IN p_tenant BINARY(16), IN p_intent BINARY(16),
    IN p_lease BINARY(16)
)
SQL SECURITY DEFINER
BEGIN
    DECLARE v_tenant_state VARCHAR(32) DEFAULT NULL;
    DECLARE v_marked INT DEFAULT 0;
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;
    START TRANSACTION;
    SELECT lifecycle INTO v_tenant_state FROM c05_02_tenants
      WHERE tenant_id=p_tenant FOR SHARE;
    IF v_tenant_state='ACTIVE' THEN
        UPDATE c05_02_delivery_outbox SET
            lifecycle='SENT',lease_nonce=NULL,leased_until_utc=NULL,
            sent_utc=UTC_TIMESTAMP(6)
         WHERE tenant_id=p_tenant AND intent_id=p_intent
           AND lifecycle='LEASED' AND lease_nonce=p_lease
           AND leased_until_utc>UTC_TIMESTAMP(6)
           AND expires_utc>UTC_TIMESTAMP(6);
        SET v_marked=ROW_COUNT();
    END IF;
    COMMIT;
    SELECT IF(v_marked=1,'SENT','DENIED') AS result;
END//

-- Ambiguous delivery failures NEVER yield a new proof or grant.
-- Outbox and challenge are revoked atomically if challenge still pending.
CREATE PROCEDURE c05_02_outbox_mark_failed(
    IN p_tenant BINARY(16), IN p_intent BINARY(16),
    IN p_lease BINARY(16)
)
SQL SECURITY DEFINER
BEGIN
    DECLARE v_proof_state VARCHAR(16) DEFAULT NULL;
    DECLARE v_digest BINARY(32) DEFAULT NULL;
    DECLARE v_outbox_digest BINARY(32) DEFAULT NULL;
    DECLARE v_revoked INT DEFAULT 0;
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;
    START TRANSACTION;
    SELECT lifecycle INTO v_proof_state FROM c05_02_mailbox_proofs
      WHERE tenant_id=p_tenant AND intent_id=p_intent FOR UPDATE;
    SELECT challenge_digest INTO v_digest FROM c05_02_mailbox_proofs
      WHERE tenant_id=p_tenant AND intent_id=p_intent;
    SELECT challenge_digest INTO v_outbox_digest FROM c05_02_delivery_outbox
      WHERE tenant_id=p_tenant AND intent_id=p_intent FOR UPDATE;
    IF v_proof_state='PENDING' AND v_digest=v_outbox_digest THEN
        UPDATE c05_02_mailbox_proofs SET lifecycle='REVOKED'
         WHERE tenant_id=p_tenant AND intent_id=p_intent
           AND lifecycle='PENDING'
           AND EXISTS(SELECT 1 FROM c05_02_delivery_outbox
             WHERE tenant_id=p_tenant AND intent_id=p_intent
               AND lifecycle='LEASED' AND lease_nonce=p_lease);
        SET v_revoked=ROW_COUNT();
        IF v_revoked=1 THEN
            UPDATE c05_02_delivery_outbox SET
                lifecycle='REVOKED',lease_nonce=NULL,leased_until_utc=NULL
             WHERE tenant_id=p_tenant AND intent_id=p_intent
               AND lifecycle='LEASED' AND lease_nonce=p_lease;
        END IF;
    END IF;
    COMMIT;
    SELECT IF(v_revoked=1,'REVOKED','DENIED') AS result;
END//
DELIMITER ;
