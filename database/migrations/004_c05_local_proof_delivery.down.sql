DROP PROCEDURE IF EXISTS c05_operational_complete_mailbox_proof_delivery;
DROP PROCEDURE IF EXISTS c05_operational_claim_mailbox_proof_outbox;
DROP TRIGGER IF EXISTS c05_trg_proof_outbox_release_lease;

ALTER TABLE c05_identity_mailbox_outbox
  DROP CHECK ck_c05_proof_outbox_lease,
  DROP COLUMN lease_owner,
  ADD CONSTRAINT ck_c05_proof_outbox_lease CHECK
    ((delivery_state=1 AND leased_until_utc IS NOT NULL) OR
     (delivery_state<>1 AND leased_until_utc IS NULL));
