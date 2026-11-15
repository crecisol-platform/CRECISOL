-- C05 verified-mailbox registration completion.
-- Adds only a PENDING account and PENDING membership after mailbox proof;
-- it never grants login, approval, role, MFA, session or private access.

CREATE TABLE c05_identity_registration_completion_quota (
    tenant_id BINARY(16) NOT NULL,
    peer_hmac BINARY(32) NOT NULL,
    window_start_utc DATETIME(0) NOT NULL,
    admitted_count TINYINT UNSIGNED NOT NULL,
    PRIMARY KEY (tenant_id, peer_hmac, window_start_utc),
    CONSTRAINT fk_c05_completion_quota_tenant FOREIGN KEY (tenant_id)
      REFERENCES c04_tenant_catalog (tenant_id),
    CONSTRAINT ck_c05_completion_quota_count CHECK (admitted_count <= 5)
) ENGINE=InnoDB;

DELIMITER $$
CREATE PROCEDURE c05_operational_reserve_registration_completion(
    IN p_tenant BINARY(16), IN p_peer BINARY(32), IN p_window DATETIME(0)
)
SQL SECURITY DEFINER
BEGIN
    DECLARE v_count TINYINT UNSIGNED DEFAULT 0;
    DECLARE v_result VARCHAR(16) DEFAULT 'LIMITED';
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    IF p_window <> TIMESTAMP(DATE_FORMAT(UTC_TIMESTAMP(0), '%Y-%m-%d %H:%i:00')) THEN
        SELECT 'DENIED' AS result;
    ELSE
        START TRANSACTION;
        INSERT INTO c05_identity_registration_completion_quota
          (tenant_id, peer_hmac, window_start_utc, admitted_count)
        VALUES (p_tenant, p_peer, p_window, 0)
        ON DUPLICATE KEY UPDATE admitted_count=admitted_count;
        SELECT admitted_count INTO v_count
          FROM c05_identity_registration_completion_quota
         WHERE tenant_id=p_tenant AND peer_hmac=p_peer
           AND window_start_utc=p_window FOR UPDATE;
        IF v_count < 5 THEN
            UPDATE c05_identity_registration_completion_quota
               SET admitted_count=admitted_count+1
             WHERE tenant_id=p_tenant AND peer_hmac=p_peer
               AND window_start_utc=p_window AND admitted_count<5;
            SET v_result='ADMITTED';
        END IF;
        COMMIT;
        SELECT v_result AS result;
    END IF;
END$$

CREATE PROCEDURE c05_operational_complete_pending_registration(
    IN p_tenant BINARY(16), IN p_proof BINARY(16),
    IN p_proof_digest BINARY(32), IN p_mailbox_digest BINARY(32),
    IN p_login_digest BINARY(32), IN p_password_phc VARCHAR(512),
    IN p_account BINARY(16), IN p_membership BINARY(16),
    IN p_created DATETIME(6)
)
SQL SECURITY DEFINER
BEGIN
    DECLARE v_state TINYINT UNSIGNED DEFAULT NULL;
    DECLARE v_failed TINYINT UNSIGNED DEFAULT NULL;
    DECLARE v_expires DATETIME(6) DEFAULT NULL;
    DECLARE v_mailbox BINARY(32) DEFAULT NULL;
    DECLARE v_digest BINARY(32) DEFAULT NULL;
    DECLARE v_result VARCHAR(16) DEFAULT 'DENIED';
    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    IF p_proof=UNHEX(REPEAT('00',16)) OR
       p_account=UNHEX(REPEAT('00',16)) OR
       p_membership=UNHEX(REPEAT('00',16)) OR
       p_created > UTC_TIMESTAMP(6) OR
       CHAR_LENGTH(p_password_phc) NOT BETWEEN 80 AND 512 OR
       LEFT(p_password_phc, 15) <> '$argon2id$v=19$' OR
       p_password_phc NOT REGEXP '^[ -~]+$' THEN
        SELECT 'DENIED' AS result;
    ELSE
        START TRANSACTION;
        SELECT proof_state, failed_attempts, expires_at_utc,
               mailbox_hmac, proof_token_hmac
          INTO v_state, v_failed, v_expires, v_mailbox, v_digest
          FROM c05_identity_mailbox_proof
         WHERE tenant_id=p_tenant AND proof_id=p_proof FOR UPDATE;

        IF v_state=0 AND v_failed<5 AND v_expires>p_created AND
           v_mailbox=p_mailbox_digest AND v_digest=p_proof_digest AND
           EXISTS (
             SELECT 1 FROM c05_identity_registration_intent i
             JOIN c04_tenant_catalog t ON t.tenant_id=i.tenant_id
             WHERE i.tenant_id=p_tenant AND i.intent_id=p_proof
               AND i.mailbox_hmac=p_mailbox_digest AND i.state=0
               AND i.expires_at_utc>p_created AND t.lifecycle_state=3
           ) THEN
            IF NOT EXISTS (
                SELECT 1 FROM c05_identity_account
                 WHERE login_lookup_hmac=p_login_digest
            ) THEN
                INSERT INTO c05_identity_account
                  (account_id,login_lookup_hmac,mailbox_ciphertext,mailbox_nonce,
                   mailbox_tag,mailbox_key_version,mailbox_verified_at_utc,
                   password_phc,account_state,security_revision,created_at_utc,updated_at_utc)
                SELECT p_account,p_login_digest,i.mailbox_ciphertext,i.mailbox_nonce,
                       i.mailbox_tag,i.key_version,p_created,p_password_phc,0,1,p_created,p_created
                  FROM c05_identity_registration_intent i
                 WHERE i.tenant_id=p_tenant AND i.intent_id=p_proof
                   AND i.mailbox_hmac=p_mailbox_digest AND i.state=0;

                INSERT INTO c05_identity_membership
                  (tenant_id,membership_id,account_id,membership_state,role_code,
                   approval_revision,approved_by,approved_at_utc,created_at_utc)
                VALUES (p_tenant,p_membership,p_account,0,'PENDING',1,NULL,NULL,p_created);

                SET v_result='PENDING';
            ELSE
                SET v_result='DUPLICATE_LOGIN';
            END IF;

            UPDATE c05_identity_mailbox_proof
               SET proof_state=1,consumed_at_utc=p_created
             WHERE tenant_id=p_tenant AND proof_id=p_proof AND proof_state=0;
            UPDATE c05_identity_registration_intent
               SET state=IF(v_result='PENDING',1,2)
             WHERE tenant_id=p_tenant AND intent_id=p_proof AND state=0;
            UPDATE c05_identity_mailbox_outbox
               SET delivery_state=3,leased_until_utc=NULL
             WHERE tenant_id=p_tenant AND proof_id=p_proof AND delivery_state IN (0,1);
            INSERT INTO c05_identity_audit
              (tenant_id,audit_id,actor_account_id,subject_account_id,action_code,
               outcome_code,context_hmac,created_at_utc)
            VALUES (p_tenant,UUID_TO_BIN(UUID(),0),NULL,
                    IF(v_result='PENDING',p_account,NULL),
                    'REGISTRATION_PENDING',1,p_proof_digest,p_created);
        ELSEIF v_state=0 AND v_expires>p_created AND v_failed<5 THEN
            IF v_mailbox<>p_mailbox_digest THEN
                SET v_result='MAILBOX_MISMATCH';
            ELSEIF v_digest<>p_proof_digest THEN
                SET v_result='TOKEN_MISMATCH';
            ELSE
                SET v_result='INTENT_MISMATCH';
            END IF;
            UPDATE c05_identity_mailbox_proof
               SET failed_attempts=v_failed+1,
                   proof_state=IF(v_failed+1>=5,2,proof_state)
             WHERE tenant_id=p_tenant AND proof_id=p_proof AND proof_state=0;
            IF v_failed+1>=5 THEN
                UPDATE c05_identity_mailbox_outbox
                   SET delivery_state=3,leased_until_utc=NULL
                 WHERE tenant_id=p_tenant AND proof_id=p_proof AND delivery_state IN (0,1);
            END IF;
        ELSEIF v_state=0 AND v_expires<=p_created THEN
            SET v_result='EXPIRED';
            UPDATE c05_identity_mailbox_proof
               SET proof_state=3
             WHERE tenant_id=p_tenant AND proof_id=p_proof AND proof_state=0;
            UPDATE c05_identity_mailbox_outbox
               SET delivery_state=3,leased_until_utc=NULL
             WHERE tenant_id=p_tenant AND proof_id=p_proof AND delivery_state IN (0,1);
        ELSEIF v_state=0 AND v_failed>=5 THEN
            SET v_result='LOCKED';
        END IF;
        COMMIT;
        SELECT v_result AS result;
    END IF;
END$$
DELIMITER ;
