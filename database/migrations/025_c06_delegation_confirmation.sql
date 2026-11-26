-- C06-11 isolated laboratory confirmation only. No seed, runtime grants,
-- production contract, or payment integration. Missing entitlement denies.
CREATE TABLE c06_delegation_lab_entitlement (
    tenant_id BINARY(16) NOT NULL,
    revision BIGINT NOT NULL,
    enabled BOOLEAN NOT NULL,
    valid_from_utc DATETIME(6) NOT NULL,
    valid_until_utc DATETIME(6) NOT NULL,
    PRIMARY KEY(tenant_id),
    CONSTRAINT fk_c06_dle_tenant FOREIGN KEY(tenant_id) REFERENCES c04_tenant_catalog(tenant_id),
    CONSTRAINT ck_c06_dle_revision CHECK(revision>0),
    CONSTRAINT ck_c06_dle_enabled CHECK(enabled IN(0,1)),
    CONSTRAINT ck_c06_dle_window CHECK(valid_until_utc>valid_from_utc)
) ENGINE=InnoDB;

CREATE TABLE c06_delegation_confirmation (
    tenant_id BINARY(16) NOT NULL,
    challenge_hmac BINARY(32) NOT NULL,
    actor_membership_id BINARY(16) NOT NULL,
    session_id BINARY(16) NOT NULL,
    binding_hmac BINARY(32) NOT NULL,
    issued_at_utc DATETIME(6) NOT NULL,
    expires_at_utc DATETIME(6) NOT NULL,
    consumed_at_utc DATETIME(6) NULL,
    PRIMARY KEY(tenant_id,challenge_hmac),
    UNIQUE KEY uq_c06_dcf_token(challenge_hmac),
    KEY ix_c06_dcf_pending(tenant_id,session_id,consumed_at_utc,expires_at_utc),
    CONSTRAINT fk_c06_dcf_member FOREIGN KEY(tenant_id,actor_membership_id)
       REFERENCES c05_identity_membership(tenant_id,membership_id),
    CONSTRAINT fk_c06_dcf_session FOREIGN KEY(tenant_id,session_id)
       REFERENCES c05_identity_session(tenant_id,session_id),
    CONSTRAINT ck_c06_dcf_window CHECK(expires_at_utc>issued_at_utc AND
       TIMESTAMPDIFF(MICROSECOND,issued_at_utc,expires_at_utc)<=300000000),
    CONSTRAINT ck_c06_dcf_consumed CHECK(consumed_at_utc IS NULL OR
       (consumed_at_utc>=issued_at_utc AND consumed_at_utc<expires_at_utc))
) ENGINE=InnoDB;
DELIMITER $$
CREATE TRIGGER trg_c06_dle_update BEFORE UPDATE ON c06_delegation_lab_entitlement FOR EACH ROW
BEGIN
    IF NEW.tenant_id<>OLD.tenant_id OR NEW.revision<>OLD.revision+1
    THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C06 lab entitlement revision required'; END IF;
END$$
CREATE TRIGGER trg_c06_dcf_insert BEFORE INSERT ON c06_delegation_confirmation FOR EACH ROW
BEGIN
    IF NEW.consumed_at_utc IS NOT NULL OR NEW.issued_at_utc>UTC_TIMESTAMP(6)
       OR NEW.expires_at_utc<=UTC_TIMESTAMP(6)
       OR NOT EXISTS(SELECT 1 FROM c05_identity_session s WHERE s.tenant_id=NEW.tenant_id
            AND s.session_id=NEW.session_id AND s.membership_id=NEW.actor_membership_id
            AND s.revoked_at_utc IS NULL AND s.expires_at_utc>UTC_TIMESTAMP(6))
    THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C06 confirmation invalid'; END IF;
END$$
CREATE TRIGGER trg_c06_dcf_consume BEFORE UPDATE ON c06_delegation_confirmation FOR EACH ROW
BEGIN
    IF NEW.tenant_id<>OLD.tenant_id OR NEW.challenge_hmac<>OLD.challenge_hmac
       OR NEW.actor_membership_id<>OLD.actor_membership_id OR NEW.session_id<>OLD.session_id
       OR NEW.binding_hmac<>OLD.binding_hmac OR NEW.issued_at_utc<>OLD.issued_at_utc
       OR NEW.expires_at_utc<>OLD.expires_at_utc OR OLD.consumed_at_utc IS NOT NULL
       OR NEW.consumed_at_utc IS NULL OR NEW.consumed_at_utc>UTC_TIMESTAMP(6)
       OR OLD.expires_at_utc<=UTC_TIMESTAMP(6)
    THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C06 confirmation immutable or expired'; END IF;
END$$
CREATE TRIGGER trg_c06_dcf_no_delete BEFORE DELETE ON c06_delegation_confirmation FOR EACH ROW
BEGIN
    SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C06 confirmation history requires reviewed disposition';
END$$
DELIMITER ;
-- No raw challenge, reason, code, bearer, private reference, permission seed or GRANT.
