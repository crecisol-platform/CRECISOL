-- Roll back only after registration traffic and any delivery worker are stopped.
DROP PROCEDURE IF EXISTS c05_operational_consume_mailbox_proof;
DROP TABLE IF EXISTS c05_identity_mailbox_outbox;
ALTER TABLE c05_identity_mailbox_proof
  DROP CHECK ck_c05_proof_failed_attempts,
  DROP COLUMN failed_attempts;
