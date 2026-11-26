-- C06-11 candidate only. A ledger issuance alone never authorizes a read.
-- Provenance must be appended in the SAME transaction as real confirmation.
-- No existing row is backfilled; no historical role or grant is activated.
CREATE TABLE c06_delegation_authority (
    tenant_id BINARY(16) NOT NULL,
    grant_id BINARY(16) NOT NULL,
    source_grant_id BINARY(16) NOT NULL,
    source_revision BIGINT NOT NULL,
    issuer_account_revision BIGINT NOT NULL,
    subject_account_revision BIGINT NOT NULL,
    entitlement_revision BIGINT NOT NULL,
    owner_account_id BINARY(16) NOT NULL,
    challenge_hmac BINARY(32) NOT NULL,
    PRIMARY KEY(tenant_id,grant_id),
    UNIQUE KEY uq_c06_da_confirmation(tenant_id,challenge_hmac),
    CONSTRAINT fk_c06_da_grant FOREIGN KEY(tenant_id,grant_id)
      REFERENCES c06_delegation_grant(tenant_id,grant_id),
    CONSTRAINT fk_c06_da_source FOREIGN KEY(tenant_id,source_grant_id)
      REFERENCES c05_identity_resource_grant(tenant_id,grant_id),
    CONSTRAINT fk_c06_da_confirmation FOREIGN KEY(tenant_id,challenge_hmac)
      REFERENCES c06_delegation_confirmation(tenant_id,challenge_hmac),
    CONSTRAINT fk_c06_da_owner FOREIGN KEY(owner_account_id)
      REFERENCES c05_identity_account(account_id),
    CONSTRAINT ck_c06_da_revisions CHECK(source_revision>0 AND issuer_account_revision>0
      AND subject_account_revision>0 AND entitlement_revision>0)
) ENGINE=InnoDB;
DELIMITER $$
CREATE TRIGGER trg_c06_da_insert BEFORE INSERT ON c06_delegation_authority FOR EACH ROW
BEGIN
    IF NOT EXISTS(
        SELECT 1 FROM c06_delegation_grant g
        JOIN c06_delegation_confirmation cf ON cf.tenant_id=g.tenant_id AND cf.challenge_hmac=NEW.challenge_hmac
        JOIN c05_identity_membership im ON im.tenant_id=g.tenant_id AND im.membership_id=g.issuer_membership_id
        JOIN c05_identity_membership sm ON sm.tenant_id=g.tenant_id AND sm.membership_id=g.subject_membership_id
        JOIN c05_identity_account ia ON ia.account_id=im.account_id
        JOIN c05_identity_account sa ON sa.account_id=sm.account_id
        JOIN c05_identity_private_resource x ON x.tenant_id=g.tenant_id AND x.branch_id=g.branch_id AND x.resource_id=g.resource_id
        JOIN c05_identity_resource_grant src ON src.tenant_id=g.tenant_id AND src.grant_id=NEW.source_grant_id
        JOIN c06_delegation_lab_entitlement ent ON ent.tenant_id=g.tenant_id
        WHERE g.tenant_id=NEW.tenant_id AND g.grant_id=NEW.grant_id
          AND cf.actor_membership_id=g.issuer_membership_id AND cf.consumed_at_utc=g.issued_at_utc
          AND im.approval_revision=g.issuer_revision AND sm.approval_revision=g.subject_revision
          AND ia.security_revision=NEW.issuer_account_revision AND sa.security_revision=NEW.subject_account_revision
          AND ia.account_state=1 AND sa.account_state=1
          AND ia.mailbox_verified_at_utc IS NOT NULL AND sa.mailbox_verified_at_utc IS NOT NULL
          AND x.owner_account_id=NEW.owner_account_id
          AND src.account_id=im.account_id AND src.branch_id=g.branch_id
          AND src.permission_code='RESOURCE_READ' AND src.grant_revision=NEW.source_revision
          AND src.revoked_at_utc IS NULL AND src.granted_at_utc<=g.issued_at_utc
          AND src.expires_at_utc>=g.expires_at_utc
          AND ((src.scope_kind=2 AND src.resource_id IS NULL) OR
               (src.scope_kind=1 AND src.resource_id=g.resource_id AND x.owner_account_id=im.account_id))
          AND ent.revision=NEW.entitlement_revision AND ent.enabled=1
          AND ent.valid_from_utc<=g.issued_at_utc AND ent.valid_until_utc>=g.expires_at_utc
    ) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C06 issuance provenance denied'; END IF;
END$$
CREATE TRIGGER trg_c06_da_no_update BEFORE UPDATE ON c06_delegation_authority FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C06 issuance provenance immutable'; END$$
CREATE TRIGGER trg_c06_da_no_delete BEFORE DELETE ON c06_delegation_authority FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C06 issuance provenance immutable'; END$$
DELIMITER ;
-- No GRANT, seed, production payment or credential material.
