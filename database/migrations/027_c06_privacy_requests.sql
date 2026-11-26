-- C06-11 isolated self-service intake. No automatic fulfilment, legal
-- decision, export, deletion, reviewer authority, policy seed or runtime grant.
CREATE TABLE c06_privacy_request_intent (
 tenant_id BINARY(16) NOT NULL,
 account_id BINARY(16) NOT NULL,
 membership_id BINARY(16) NOT NULL,
 session_id BINARY(16) NOT NULL,
 request_key BINARY(16) NOT NULL,
 challenge_hmac BINARY(32) NOT NULL,
 binding_hmac BINARY(32) NOT NULL,
 issued_at_utc DATETIME(6) NOT NULL,
 expires_at_utc DATETIME(6) NOT NULL,
 consumed_at_utc DATETIME(6) NULL,
 PRIMARY KEY(tenant_id,account_id,request_key),
 UNIQUE KEY uq_c06_pri_challenge(challenge_hmac),
 KEY ix_c06_pri_pending(tenant_id,account_id,session_id,consumed_at_utc,expires_at_utc),
 CONSTRAINT fk_c06_pri_member FOREIGN KEY(tenant_id,membership_id) REFERENCES c05_identity_membership(tenant_id,membership_id),
 CONSTRAINT fk_c06_pri_account FOREIGN KEY(account_id) REFERENCES c05_identity_account(account_id),
 CONSTRAINT fk_c06_pri_session FOREIGN KEY(tenant_id,session_id) REFERENCES c05_identity_session(tenant_id,session_id),
 CONSTRAINT ck_c06_pri_key CHECK(request_key<>X'00000000000000000000000000000000'),
 CONSTRAINT ck_c06_pri_hmac CHECK(challenge_hmac<>UNHEX(REPEAT('00',32)) AND binding_hmac<>UNHEX(REPEAT('00',32))),
 CONSTRAINT ck_c06_pri_window CHECK(expires_at_utc>issued_at_utc AND TIMESTAMPDIFF(MICROSECOND,issued_at_utc,expires_at_utc)<=300000000),
 CONSTRAINT ck_c06_pri_consumed CHECK(consumed_at_utc IS NULL OR (consumed_at_utc>=issued_at_utc AND consumed_at_utc<expires_at_utc))
) ENGINE=InnoDB;

CREATE TABLE c06_privacy_request (
 tenant_id BINARY(16) NOT NULL,
 account_id BINARY(16) NOT NULL,
 request_id BINARY(16) NOT NULL,
 request_key BINARY(16) NOT NULL,
 membership_id BINARY(16) NOT NULL,
 right_code VARCHAR(12) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 category_code VARCHAR(20) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 protected_detail VARBINARY(1024) NOT NULL,
 submitted_at_utc DATETIME(6) NOT NULL,
 -- These are explicit pending assessment gates, NOT legal findings.
 identity_state VARCHAR(16) CHARACTER SET ascii COLLATE ascii_bin NOT NULL DEFAULT 'mfa-verified',
 purpose_state VARCHAR(12) CHARACTER SET ascii COLLATE ascii_bin NOT NULL DEFAULT 'pending',
 legal_basis_state VARCHAR(12) CHARACTER SET ascii COLLATE ascii_bin NOT NULL DEFAULT 'pending',
 retention_state VARCHAR(12) CHARACTER SET ascii COLLATE ascii_bin NOT NULL DEFAULT 'pending',
 legal_hold_state VARCHAR(12) CHARACTER SET ascii COLLATE ascii_bin NOT NULL DEFAULT 'pending',
 disposition_state VARCHAR(12) CHARACTER SET ascii COLLATE ascii_bin NOT NULL DEFAULT 'pending',
 PRIMARY KEY(tenant_id,request_id),
 UNIQUE KEY uq_c06_pr_key(tenant_id,account_id,request_key),
 KEY ix_c06_pr_own_page(tenant_id,account_id,submitted_at_utc,request_id),
 CONSTRAINT fk_c06_pr_intent FOREIGN KEY(tenant_id,account_id,request_key)
   REFERENCES c06_privacy_request_intent(tenant_id,account_id,request_key),
 CONSTRAINT fk_c06_pr_member FOREIGN KEY(tenant_id,membership_id) REFERENCES c05_identity_membership(tenant_id,membership_id),
 CONSTRAINT ck_c06_pr_id CHECK(request_id<>X'00000000000000000000000000000000'),
 CONSTRAINT ck_c06_pr_right CHECK(right_code IN('access','rectify','erase','object')),
 CONSTRAINT ck_c06_pr_category CHECK(category_code IN('profile','identity','security','case-records')),
 CONSTRAINT ck_c06_pr_cipher CHECK(OCTET_LENGTH(protected_detail) BETWEEN 36 AND 1024),
 CONSTRAINT ck_c06_pr_initial CHECK(identity_state='mfa-verified' AND purpose_state='pending' AND
   legal_basis_state='pending' AND retention_state='pending' AND legal_hold_state='pending' AND disposition_state='pending')
) ENGINE=InnoDB;
DELIMITER $$
CREATE TRIGGER trg_c06_pri_insert BEFORE INSERT ON c06_privacy_request_intent FOR EACH ROW
BEGIN
 IF NEW.consumed_at_utc IS NOT NULL OR NEW.issued_at_utc>UTC_TIMESTAMP(6) OR NEW.expires_at_utc<=UTC_TIMESTAMP(6)
 OR NOT EXISTS(SELECT 1 FROM c05_identity_session s JOIN c05_identity_membership m
   ON m.tenant_id=s.tenant_id AND m.membership_id=s.membership_id AND m.account_id=s.account_id
   WHERE s.tenant_id=NEW.tenant_id AND s.session_id=NEW.session_id AND s.account_id=NEW.account_id
    AND s.membership_id=NEW.membership_id AND m.membership_state=1
    AND m.expires_at_utc>UTC_TIMESTAMP(6) AND s.revoked_at_utc IS NULL AND s.expires_at_utc>UTC_TIMESTAMP(6))
 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C06 privacy preparation denied'; END IF;
END$$
CREATE TRIGGER trg_c06_pri_consume BEFORE UPDATE ON c06_privacy_request_intent FOR EACH ROW
BEGIN
 IF NEW.tenant_id<>OLD.tenant_id OR NEW.account_id<>OLD.account_id OR NEW.membership_id<>OLD.membership_id
 OR NEW.session_id<>OLD.session_id OR NEW.request_key<>OLD.request_key OR NEW.challenge_hmac<>OLD.challenge_hmac
 OR NEW.binding_hmac<>OLD.binding_hmac OR NEW.issued_at_utc<>OLD.issued_at_utc OR NEW.expires_at_utc<>OLD.expires_at_utc
 OR OLD.consumed_at_utc IS NOT NULL OR NEW.consumed_at_utc IS NULL OR NEW.consumed_at_utc>UTC_TIMESTAMP(6)
 OR OLD.expires_at_utc<=UTC_TIMESTAMP(6)
 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C06 privacy preparation immutable or expired'; END IF;
END$$
CREATE TRIGGER trg_c06_pri_no_delete BEFORE DELETE ON c06_privacy_request_intent FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C06 privacy intent requires reviewed retention disposition'; END$$
CREATE TRIGGER trg_c06_pr_insert BEFORE INSERT ON c06_privacy_request FOR EACH ROW
BEGIN
 IF NEW.submitted_at_utc>UTC_TIMESTAMP(6) OR NOT EXISTS(
   SELECT 1 FROM c06_privacy_request_intent i
    WHERE i.tenant_id=NEW.tenant_id AND i.account_id=NEW.account_id AND i.request_key=NEW.request_key
      AND i.membership_id=NEW.membership_id AND i.consumed_at_utc=NEW.submitted_at_utc
 ) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C06 privacy confirmed intent required'; END IF;
END$$
CREATE TRIGGER trg_c06_pr_no_update BEFORE UPDATE ON c06_privacy_request FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C06 privacy intake immutable; assessment is separate'; END$$
CREATE TRIGGER trg_c06_pr_no_delete BEFORE DELETE ON c06_privacy_request FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C06 privacy request requires reviewed retention disposition'; END$$
DELIMITER ;
-- Review, legal hold, response and disposition are independent future states;
-- no blanket erasure and no direct authority from receipt or requested right.
