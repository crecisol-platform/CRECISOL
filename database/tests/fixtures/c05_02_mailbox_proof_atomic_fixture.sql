-- C05-02 ONLY: disposable MySQL 8.4 email-possession challenge fixture.
-- Requires c05_02_invitation_atomic_fixture.sql followed by
-- c05_02_neutral_registration_fixture.sql. NOT a production migration.
-- No user, credential, email verification in a real inbox, tenant grant or session.
CREATE TABLE c05_02_mailbox_proofs (
    tenant_id BINARY(16) NOT NULL,
    intent_id BINARY(16) NOT NULL,
    challenge_digest BINARY(32) NOT NULL,
    lifecycle ENUM('PENDING','VERIFIED','REVOKED') NOT NULL,
    attempts TINYINT UNSIGNED NOT NULL,
    issued_utc DATETIME(6) NOT NULL,
    expires_utc DATETIME(6) NOT NULL,
    verified_utc DATETIME(6) NULL,
    PRIMARY KEY (tenant_id,intent_id),
    KEY ix_c05_02_proof_retention (tenant_id,expires_utc),
    CONSTRAINT fk_c05_02_proof_intent FOREIGN KEY (tenant_id,intent_id)
        REFERENCES c05_02_neutral_intents(tenant_id,intent_id)
        ON UPDATE RESTRICT ON DELETE RESTRICT,
    CHECK (attempts BETWEEN 1 AND 3),
    CHECK (expires_utc > issued_utc),
    CHECK ((lifecycle='VERIFIED' AND verified_utc IS NOT NULL)
        OR (lifecycle<>'VERIFIED' AND verified_utc IS NULL))
) ENGINE=InnoDB;

DELIMITER //
CREATE PROCEDURE c05_02_mailbox_proof_issue(
    IN p_tenant BINARY(16), IN p_intent BINARY(16),
    IN p_digest BINARY(32), IN p_issued DATETIME(6),
    IN p_expires DATETIME(6)
)
SQL SECURITY DEFINER
BEGIN
    DECLARE v_tenant_state VARCHAR(32) DEFAULT NULL;
    DECLARE v_intent_expiry DATETIME(6) DEFAULT NULL;
    DECLARE v_lifecycle VARCHAR(16) DEFAULT NULL;
    DECLARE v_attempts INT DEFAULT 0;
    DECLARE v_issued INT DEFAULT 0;
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    START TRANSACTION;
    -- Acquire locks in tenant -> intent -> proof order across all routines.
    SELECT lifecycle INTO v_tenant_state FROM c05_02_tenants
     WHERE tenant_id=p_tenant FOR SHARE;
    SELECT expires_at_utc INTO v_intent_expiry FROM c05_02_neutral_intents
     WHERE tenant_id=p_tenant AND intent_id=p_intent FOR UPDATE;

    IF v_tenant_state='ACTIVE'
       AND v_intent_expiry>UTC_TIMESTAMP(6)
       AND p_issued<=UTC_TIMESTAMP(6)
       AND p_issued>=DATE_SUB(UTC_TIMESTAMP(6),INTERVAL 5 MINUTE)
       AND p_expires>UTC_TIMESTAMP(6)
       AND p_expires<=v_intent_expiry
       AND TIMESTAMPDIFF(SECOND,p_issued,p_expires) BETWEEN 60 AND 600
    THEN
        SELECT lifecycle, attempts INTO v_lifecycle,v_attempts
          FROM c05_02_mailbox_proofs
         WHERE tenant_id=p_tenant AND intent_id=p_intent FOR UPDATE;
        IF v_attempts=0 THEN
            INSERT INTO c05_02_mailbox_proofs
                (tenant_id,intent_id,challenge_digest,lifecycle,
                 attempts,issued_utc,expires_utc,verified_utc)
            VALUES(p_tenant,p_intent,p_digest,'PENDING',
                   1,p_issued,p_expires,NULL);
            SET v_issued=1;
        ELSEIF v_lifecycle='REVOKED' AND v_attempts<3 THEN
            UPDATE c05_02_mailbox_proofs
               SET challenge_digest=p_digest,lifecycle='PENDING',
                   attempts=v_attempts+1,issued_utc=p_issued,
                   expires_utc=p_expires,verified_utc=NULL
             WHERE tenant_id=p_tenant AND intent_id=p_intent
               AND lifecycle='REVOKED';
            SET v_issued=ROW_COUNT();
        END IF;
    END IF;
    COMMIT;
    SELECT IF(v_issued=1,'ISSUED','DENIED') AS result;
END//

CREATE PROCEDURE c05_02_mailbox_proof_verify(
    IN p_tenant BINARY(16), IN p_intent BINARY(16),
    IN p_digest BINARY(32), IN p_now DATETIME(6)
)
SQL SECURITY DEFINER
BEGIN
    DECLARE v_tenant_state VARCHAR(32) DEFAULT NULL;
    DECLARE v_intent_expiry DATETIME(6) DEFAULT NULL;
    DECLARE v_verified INT DEFAULT 0;
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

    IF v_tenant_state='ACTIVE'
       AND v_intent_expiry>UTC_TIMESTAMP(6)
       AND p_now>=DATE_SUB(UTC_TIMESTAMP(6),INTERVAL 5 MINUTE)
       AND p_now<=DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 1 MINUTE)
    THEN
        UPDATE c05_02_mailbox_proofs
           SET lifecycle='VERIFIED',verified_utc=UTC_TIMESTAMP(6)
         WHERE tenant_id=p_tenant AND intent_id=p_intent
           AND lifecycle='PENDING'
           AND challenge_digest=p_digest
           AND issued_utc<=UTC_TIMESTAMP(6)
           AND expires_utc>UTC_TIMESTAMP(6);
        SET v_verified=ROW_COUNT();
    END IF;

    COMMIT;
    SELECT IF(v_verified=1,'VERIFIED','DENIED') AS result;
END//

CREATE PROCEDURE c05_02_mailbox_proof_revoke(
    IN p_tenant BINARY(16), IN p_intent BINARY(16),
    IN p_digest BINARY(32)
)
SQL SECURITY DEFINER
BEGIN
    DECLARE v_revoked INT DEFAULT 0;
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    START TRANSACTION;
    -- Bind exact recipient intent; never mutate a foreign tenant's row.
    UPDATE c05_02_mailbox_proofs
       SET lifecycle='REVOKED',verified_utc=NULL
     WHERE tenant_id=p_tenant AND intent_id=p_intent
       AND challenge_digest=p_digest AND lifecycle='PENDING';
    SET v_revoked=ROW_COUNT();
    COMMIT;
    SELECT IF(v_revoked=1,'REVOKED','DENIED') AS result;
END//
DELIMITER ;
