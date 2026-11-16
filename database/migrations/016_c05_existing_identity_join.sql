-- This narrow definer operation participates in the caller's transaction.
-- It MUST NOT START/COMMIT/ROLLBACK a transaction: proof consumption, verified
-- identity, terms and pending membership are one atomic application transition.
DELIMITER $$
CREATE PROCEDURE c05_operational_consume_join_proof(
 IN p_tenant BINARY(16), IN p_proof BINARY(16),
 IN p_digest BINARY(32), IN p_mailbox BINARY(32)
)
SQL SECURITY DEFINER
BEGIN
 DECLARE changed INT DEFAULT 0;
 UPDATE c05_identity_mailbox_proof p
 JOIN c05_identity_registration_intent i ON i.tenant_id=p.tenant_id AND i.intent_id=p.proof_id
 SET p.proof_state=1,p.consumed_at_utc=UTC_TIMESTAMP(6)
 WHERE p.tenant_id=p_tenant AND p.proof_id=p_proof AND p.proof_token_hmac=p_digest
 AND p.mailbox_hmac=p_mailbox AND i.mailbox_hmac=p_mailbox
 AND p.proof_state=0 AND p.failed_attempts<5 AND p.expires_at_utc>UTC_TIMESTAMP(6)
 AND i.state=0 AND i.expires_at_utc>UTC_TIMESTAMP(6);
 SET changed=ROW_COUNT();
 IF changed=1 THEN
  UPDATE c05_identity_registration_intent SET state=1 WHERE tenant_id=p_tenant AND intent_id=p_proof;
  UPDATE c05_identity_mailbox_outbox SET delivery_state=3,leased_until_utc=NULL
   WHERE tenant_id=p_tenant AND proof_id=p_proof AND delivery_state IN(0,1);
 ELSE
  UPDATE c05_identity_mailbox_proof
   SET proof_state=IF(failed_attempts>=4,2,proof_state),failed_attempts=LEAST(failed_attempts+1,5)
   WHERE tenant_id=p_tenant AND proof_id=p_proof AND mailbox_hmac=p_mailbox
    AND proof_state=0 AND failed_attempts<5 AND expires_at_utc>UTC_TIMESTAMP(6);
  UPDATE c05_identity_mailbox_outbox o JOIN c05_identity_mailbox_proof p USING(tenant_id,proof_id)
   SET o.delivery_state=3,o.leased_until_utc=NULL
   WHERE o.tenant_id=p_tenant AND o.proof_id=p_proof AND p.proof_state=2 AND o.delivery_state IN(0,1);
 END IF;
 SELECT changed AS consumed;
END$$
DELIMITER ;
