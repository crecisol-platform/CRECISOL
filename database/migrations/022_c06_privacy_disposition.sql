-- C06-07: isolated operational schema only; no automatic event or seed.
-- Logical disposition preserves keys, authorization metadata and evidence.
DELIMITER $$
CREATE PROCEDURE c06_dispose_closed_sensitive_payloads(IN p_limit INT)
SQL SECURITY DEFINER
MODIFIES SQL DATA
BEGIN
 DECLARE v_now DATETIME(6);
 DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN ROLLBACK; RESIGNAL; END;
 IF p_limit IS NULL OR p_limit<1 OR p_limit>100 THEN
   SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Invalid privacy batch';
 END IF;
 SET v_now=UTC_TIMESTAMP(6);
 START TRANSACTION;
 UPDATE c05_identity_recovery
 SET protected_payload=X''
 WHERE OCTET_LENGTH(protected_payload)>0
   AND (expires_at_utc<=v_now OR (consumed_at_utc IS NOT NULL AND consumed_at_utc<=v_now))
 ORDER BY expires_at_utc,account_id,recovery_id LIMIT p_limit;
 UPDATE c05_identity_sensitive_outbox
 SET protected_payload=X''
 WHERE OCTET_LENGTH(protected_payload)>0
   AND (expires_at_utc<=v_now OR (delivered_at_utc IS NOT NULL AND delivered_at_utc<=v_now))
 ORDER BY expires_at_utc,message_id LIMIT p_limit;
 COMMIT;
 SELECT 'ACK' AS result;
END$$
DELIMITER ;
