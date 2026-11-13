-- CRECISOL C05-02: disposable local-only neutral registration intake.
-- NO account existence table, email verification, login, tenant grants or users.
-- Requires c05_02_tenants from the earlier C05-02 disposable fixture.
-- NOT a production migration and NOT intended for installation outside CI.
CREATE TABLE c05_02_neutral_quota (
    tenant_id BINARY(16) NOT NULL,
    peer_digest BINARY(32) NOT NULL,
    window_bucket BIGINT UNSIGNED NOT NULL,
    attempts SMALLINT UNSIGNED NOT NULL,
    PRIMARY KEY (tenant_id,peer_digest,window_bucket),
    CONSTRAINT fk_c05_02_neutral_quota_tenant FOREIGN KEY (tenant_id)
        REFERENCES c05_02_tenants(tenant_id)
        ON UPDATE RESTRICT ON DELETE RESTRICT,
    CHECK (attempts BETWEEN 1 AND 9)
) ENGINE=InnoDB;

CREATE TABLE c05_02_neutral_intents (
    tenant_id BINARY(16) NOT NULL,
    intent_id BINARY(16) NOT NULL,
    peer_digest BINARY(32) NOT NULL,
    mailbox_digest BINARY(32) NOT NULL,
    nonce BINARY(12) NOT NULL,
    tag BINARY(16) NOT NULL,
    ciphertext VARBINARY(254) NOT NULL,
    key_version INT UNSIGNED NOT NULL,
    captured_at_utc DATETIME(6) NOT NULL,
    expires_at_utc DATETIME(6) NOT NULL,
    PRIMARY KEY (tenant_id,intent_id),
    KEY ix_c05_02_intent_retention (tenant_id,expires_at_utc),
    CONSTRAINT fk_c05_02_neutral_intent_tenant FOREIGN KEY (tenant_id)
        REFERENCES c05_02_tenants(tenant_id)
        ON UPDATE RESTRICT ON DELETE RESTRICT,
    CHECK (LENGTH(ciphertext) BETWEEN 6 AND 254),
    CHECK (key_version > 0),
    CHECK (expires_at_utc > captured_at_utc)
) ENGINE=InnoDB;

DELIMITER //
CREATE PROCEDURE c05_02_record_registration_intent(
    IN p_tenant BINARY(16), IN p_intent_id BINARY(16),
    IN p_client_digest BINARY(32), IN p_mailbox_digest BINARY(32),
    IN p_nonce BINARY(12), IN p_tag BINARY(16),
    IN p_ciphertext VARBINARY(254), IN p_key_version INT,
    IN p_created_utc DATETIME(6), IN p_expires_utc DATETIME(6)
)
SQL SECURITY DEFINER
BEGIN
    DECLARE v_state VARCHAR(32) DEFAULT NULL;
    DECLARE v_bucket BIGINT UNSIGNED;
    DECLARE v_attempts SMALLINT UNSIGNED DEFAULT 9;
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    START TRANSACTION;
    -- Shared record lock: protects against tenant lifecycle modification
    -- during admission without serializing readers with an exclusive lock.
    SELECT lifecycle INTO v_state FROM c05_02_tenants
     WHERE tenant_id=p_tenant FOR SHARE;

    IF v_state='ACTIVE'
       AND p_key_version > 0
       AND LENGTH(p_ciphertext) BETWEEN 6 AND 254
       AND p_created_utc >= DATE_SUB(UTC_TIMESTAMP(6), INTERVAL 5 MINUTE)
       AND p_created_utc <= DATE_ADD(UTC_TIMESTAMP(6), INTERVAL 1 MINUTE)
       AND TIMESTAMPDIFF(SECOND,p_created_utc,p_expires_utc) BETWEEN 60 AND 3600
    THEN
        SET v_bucket = FLOOR(UNIX_TIMESTAMP(UTC_TIMESTAMP(6)) / 900);
        -- Only an opaque server-keyed digest of the transport peer is stored.
        -- Every syntactically valid email, regardless of account existence,
        -- takes this same transaction path and receives the same receipt.
        INSERT INTO c05_02_neutral_quota
            (tenant_id,peer_digest,window_bucket,attempts)
        VALUES (p_tenant,p_client_digest,v_bucket,1)
        ON DUPLICATE KEY UPDATE attempts=LEAST(attempts+1,9);

        SELECT attempts INTO v_attempts FROM c05_02_neutral_quota
         WHERE tenant_id=p_tenant AND peer_digest=p_client_digest
           AND window_bucket=v_bucket FOR UPDATE;

        IF v_attempts <= 8 THEN
            INSERT INTO c05_02_neutral_intents
                (tenant_id,intent_id,peer_digest,mailbox_digest,
                 nonce,tag,ciphertext,key_version,captured_at_utc,expires_at_utc)
            VALUES (p_tenant,p_intent_id,p_client_digest,p_mailbox_digest,
                    p_nonce,p_tag,p_ciphertext,p_key_version,p_created_utc,p_expires_utc);
        END IF;
    END IF;

    COMMIT;
    -- Always one neutral result. No account lookup and no status/count shown.
    SELECT 'ACK' AS result;
END//
DELIMITER ;
