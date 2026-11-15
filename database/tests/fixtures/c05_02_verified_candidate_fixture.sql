-- C05 LOCAL FIXTURE ONLY. Verified mailbox creates PENDING credential, never a tenant grant.
CREATE TABLE c05_02_candidate_accounts (
  actor_id BINARY(16) NOT NULL,
  login_digest BINARY(32) NOT NULL,
  source_tenant_id BINARY(16) NOT NULL,
  source_intent_id BINARY(16) NOT NULL,
  mailbox_digest BINARY(32) NOT NULL,
  verifier_phc VARCHAR(160) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  lifecycle ENUM('PENDING_MEMBERSHIP') NOT NULL DEFAULT 'PENDING_MEMBERSHIP',
  created_utc DATETIME(6) NOT NULL,
  PRIMARY KEY(actor_id),
  UNIQUE KEY uq_candidate_global_login (login_digest),
  UNIQUE KEY uq_candidate_one_proof (source_tenant_id,source_intent_id),
  FOREIGN KEY (source_tenant_id,source_intent_id)
    REFERENCES c05_02_mailbox_proofs(tenant_id,intent_id)
    ON UPDATE RESTRICT ON DELETE RESTRICT,
  CHECK (LEFT(verifier_phc,10)='$argon2id$'),
  CHECK (LENGTH(verifier_phc) BETWEEN 80 AND 160)
) ENGINE=InnoDB;
DELIMITER //
CREATE PROCEDURE c05_02_create_pending_candidate(
  IN p_tenant BINARY(16), IN p_intent BINARY(16),
  IN p_actor BINARY(16), IN p_login_digest BINARY(32),
  IN p_mailbox_digest BINARY(32), IN p_phc VARCHAR(160)
) SQL SECURITY DEFINER
BEGIN
  DECLARE v_state VARCHAR(32) DEFAULT NULL;
  DECLARE v_intent_expiry DATETIME(6) DEFAULT NULL;
  DECLARE v_expected BINARY(32) DEFAULT NULL;
  DECLARE v_proof VARCHAR(16) DEFAULT NULL;
  DECLARE v_verified DATETIME(6) DEFAULT NULL;
  DECLARE v_affected INT DEFAULT 0;
  DECLARE v_duplicate INT DEFAULT 0;
  DECLARE CONTINUE HANDLER FOR 1062 SET v_duplicate=1;
  DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;
  START TRANSACTION;
  SELECT lifecycle INTO v_state FROM c05_02_tenants
    WHERE tenant_id=p_tenant FOR SHARE;
  SELECT expires_at_utc,mailbox_digest INTO v_intent_expiry,v_expected
    FROM c05_02_neutral_intents WHERE tenant_id=p_tenant
    AND intent_id=p_intent FOR UPDATE;
  SELECT lifecycle,verified_utc INTO v_proof,v_verified
    FROM c05_02_mailbox_proofs WHERE tenant_id=p_tenant
    AND intent_id=p_intent FOR UPDATE;
  IF v_state='ACTIVE' AND v_intent_expiry>UTC_TIMESTAMP(6)
    AND v_expected=p_mailbox_digest AND v_proof='VERIFIED'
    AND v_verified IS NOT NULL AND v_verified<=UTC_TIMESTAMP(6)
    AND LENGTH(p_phc) BETWEEN 80 AND 160
    AND LEFT(p_phc,10)='$argon2id$'
  THEN
    INSERT INTO c05_02_candidate_accounts
      (actor_id,login_digest,source_tenant_id,source_intent_id,
       mailbox_digest,verifier_phc,lifecycle,created_utc)
    VALUES(p_actor,p_login_digest,p_tenant,p_intent,
      p_mailbox_digest,p_phc,'PENDING_MEMBERSHIP',UTC_TIMESTAMP(6));
    IF v_duplicate=0 THEN SET v_affected=ROW_COUNT(); END IF;
  END IF;
  COMMIT;
  SELECT IF(v_affected=1,'PENDING','DENIED') AS result;
END//
DELIMITER ;
