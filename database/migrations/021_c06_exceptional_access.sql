-- C06-06: isolated local database only. No seeded operator, permission or access.
-- Explicit eligibility is issued only by separate trusted offline DB authority.
CREATE TABLE c06_exception_entitlement (
 entitlement_id BINARY(16) PRIMARY KEY,
 operator_id BINARY(16) NOT NULL,
 issued_by BINARY(16) NOT NULL,
 security_revision BIGINT UNSIGNED NOT NULL,
 tenant_id BINARY(16) NOT NULL, branch_id BINARY(16) NOT NULL, resource_id BINARY(16) NOT NULL,
 capability TINYINT UNSIGNED NOT NULL,
 issued_at_utc DATETIME(6) NOT NULL, expires_at_utc DATETIME(6) NOT NULL, revoked_at_utc DATETIME(6) NULL,
 KEY ix_c06_exception_entitlement(operator_id,security_revision,tenant_id,branch_id,resource_id,capability,revoked_at_utc),
 UNIQUE KEY uq_c06_exception_grant_scope(entitlement_id,operator_id,security_revision,tenant_id,branch_id,resource_id),
 FOREIGN KEY(operator_id) REFERENCES c05_platform_operator(operator_id),
 FOREIGN KEY(issued_by) REFERENCES c05_platform_operator(operator_id),
 FOREIGN KEY(tenant_id,branch_id,resource_id) REFERENCES c05_identity_private_resource(tenant_id,branch_id,resource_id),
 CHECK(operator_id<>issued_by AND security_revision>0 AND capability IN (1,2)),
 CHECK(expires_at_utc>issued_at_utc AND expires_at_utc<=issued_at_utc+INTERVAL 30 DAY),
 CHECK(revoked_at_utc IS NULL OR revoked_at_utc>=issued_at_utc)
) ENGINE=InnoDB;

CREATE TABLE c06_exception_request (
 exception_id BINARY(16) PRIMARY KEY,
 requester_id BINARY(16) NOT NULL, session_id BINARY(16) NOT NULL, security_revision BIGINT UNSIGNED NOT NULL,
 entitlement_id BINARY(16) NOT NULL,
 tenant_id BINARY(16) NOT NULL, branch_id BINARY(16) NOT NULL, resource_id BINARY(16) NOT NULL,
 purpose_hmac BINARY(32) NOT NULL,
 requested_at_utc DATETIME(6) NOT NULL, expires_at_utc DATETIME(6) NOT NULL,
 UNIQUE KEY uq_c06_exception_request_scope(exception_id,tenant_id,branch_id,resource_id),
 FOREIGN KEY(entitlement_id,requester_id,security_revision,tenant_id,branch_id,resource_id)
 REFERENCES c06_exception_entitlement(entitlement_id,operator_id,security_revision,tenant_id,branch_id,resource_id),
 FOREIGN KEY(session_id) REFERENCES c05_platform_session(session_id),
 FOREIGN KEY(tenant_id,branch_id,resource_id) REFERENCES c05_identity_private_resource(tenant_id,branch_id,resource_id),
 CHECK(security_revision>0 AND expires_at_utc>requested_at_utc AND expires_at_utc<=requested_at_utc+INTERVAL 10 MINUTE)
) ENGINE=InnoDB;

CREATE TABLE c06_exception_approval (
 exception_id BINARY(16) PRIMARY KEY,
 reviewer_id BINARY(16) NOT NULL, review_session_id BINARY(16) NOT NULL, review_revision BIGINT UNSIGNED NOT NULL,
 entitlement_id BINARY(16) NOT NULL, review_hmac BINARY(32) NOT NULL,
 approved_at_utc DATETIME(6) NOT NULL, expires_at_utc DATETIME(6) NOT NULL,
 FOREIGN KEY(exception_id) REFERENCES c06_exception_request(exception_id),
 FOREIGN KEY(reviewer_id) REFERENCES c05_platform_operator(operator_id),
 FOREIGN KEY(review_session_id) REFERENCES c05_platform_session(session_id),
 FOREIGN KEY(entitlement_id) REFERENCES c06_exception_entitlement(entitlement_id),
 CHECK(review_revision>0 AND expires_at_utc>approved_at_utc AND expires_at_utc<=approved_at_utc+INTERVAL 10 MINUTE)
) ENGINE=InnoDB;

CREATE TABLE c06_exception_close (
 exception_id BINARY(16) PRIMARY KEY, closed_by BINARY(16) NOT NULL, closed_at_utc DATETIME(6) NOT NULL,
 FOREIGN KEY(exception_id) REFERENCES c06_exception_request(exception_id),
 FOREIGN KEY(closed_by) REFERENCES c05_platform_operator(operator_id)
) ENGINE=InnoDB;

CREATE TABLE c06_exception_audit (
 event_id BINARY(16) PRIMARY KEY, exception_id BINARY(16) NULL,
 operator_id BINARY(16) NOT NULL, session_id BINARY(16) NOT NULL,
 action_code VARCHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
 allowed BOOLEAN NOT NULL, event_at_utc DATETIME(6) NOT NULL,
 KEY ix_c06_exception_audit_request(exception_id,event_at_utc),
 FOREIGN KEY(exception_id) REFERENCES c06_exception_request(exception_id),
 FOREIGN KEY(operator_id) REFERENCES c05_platform_operator(operator_id),
 FOREIGN KEY(session_id) REFERENCES c05_platform_session(session_id),
 CHECK(allowed IN (0,1)),
 CHECK(allowed=0 OR exception_id IS NOT NULL),
 CHECK(action_code IN ('platform/breakglass/open','platform/breakglass/approve','platform/breakglass/read','platform/breakglass/close'))
) ENGINE=InnoDB;

DELIMITER $$
CREATE TRIGGER trg_c06_exception_entitlement_update BEFORE UPDATE ON c06_exception_entitlement FOR EACH ROW
BEGIN
 IF NOT (OLD.entitlement_id<=>NEW.entitlement_id) OR NOT (OLD.operator_id<=>NEW.operator_id)
 OR NOT (OLD.issued_by<=>NEW.issued_by) OR NOT (OLD.security_revision<=>NEW.security_revision)
 OR NOT (OLD.tenant_id<=>NEW.tenant_id) OR NOT (OLD.branch_id<=>NEW.branch_id)
 OR NOT (OLD.resource_id<=>NEW.resource_id) OR NOT (OLD.capability<=>NEW.capability)
 OR NOT (OLD.issued_at_utc<=>NEW.issued_at_utc) OR NOT (OLD.expires_at_utc<=>NEW.expires_at_utc)
 OR OLD.revoked_at_utc IS NOT NULL OR NEW.revoked_at_utc IS NULL
 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C06 exception entitlement immutable except first revocation'; END IF;
END$$
CREATE TRIGGER trg_c06_exception_entitlement_no_delete BEFORE DELETE ON c06_exception_entitlement FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C06 exception entitlement is historical'; END$$

-- Defense in depth: an application defect cannot insert an approval for
-- itself or another scope, or silently extend a pending request's deadline.
CREATE TRIGGER trg_c06_exception_approval_guard BEFORE INSERT ON c06_exception_approval FOR EACH ROW
BEGIN
 IF NOT EXISTS (
   SELECT 1 FROM c06_exception_request req
   JOIN c06_exception_entitlement g ON g.entitlement_id=NEW.entitlement_id
   JOIN c05_platform_session s ON s.session_id=NEW.review_session_id
   WHERE req.exception_id=NEW.exception_id AND req.requester_id<>NEW.reviewer_id
     AND g.operator_id=NEW.reviewer_id AND g.security_revision=NEW.review_revision AND g.capability=2
     AND g.tenant_id=req.tenant_id AND g.branch_id=req.branch_id AND g.resource_id=req.resource_id
     AND s.operator_id=NEW.reviewer_id AND s.security_revision=NEW.review_revision
     AND g.issued_at_utc<=NEW.approved_at_utc AND g.revoked_at_utc IS NULL
     AND NEW.approved_at_utc>=req.requested_at_utc
     AND NEW.expires_at_utc<=req.expires_at_utc AND NEW.expires_at_utc<=g.expires_at_utc
     AND NOT EXISTS (SELECT 1 FROM c06_exception_close c WHERE c.exception_id=req.exception_id)
 ) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C06 exception review scope denied'; END IF;
END$$
CREATE TRIGGER trg_c06_exception_request_guard BEFORE INSERT ON c06_exception_request FOR EACH ROW
BEGIN
 IF NOT EXISTS (
   SELECT 1 FROM c06_exception_entitlement g
   JOIN c05_platform_session s ON s.session_id=NEW.session_id
   WHERE g.entitlement_id=NEW.entitlement_id AND g.operator_id=NEW.requester_id AND g.security_revision=NEW.security_revision
     AND g.capability=1 AND g.revoked_at_utc IS NULL
     AND s.operator_id=NEW.requester_id AND s.security_revision=NEW.security_revision
     AND g.issued_at_utc<=NEW.requested_at_utc AND NEW.expires_at_utc<=g.expires_at_utc
 ) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C06 exception request scope denied'; END IF;
END$$
CREATE TRIGGER trg_c06_exception_close_guard BEFORE INSERT ON c06_exception_close FOR EACH ROW
BEGIN
 IF NOT EXISTS(SELECT 1 FROM c06_exception_request req WHERE req.exception_id=NEW.exception_id AND req.requester_id=NEW.closed_by AND NEW.closed_at_utc>=req.requested_at_utc)
 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C06 exception close scope denied'; END IF;
END$$
CREATE TRIGGER trg_c06_exception_request_no_update BEFORE UPDATE ON c06_exception_request FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C06 exception request immutable'; END$$
CREATE TRIGGER trg_c06_exception_request_no_delete BEFORE DELETE ON c06_exception_request FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C06 exception request immutable'; END$$
CREATE TRIGGER trg_c06_exception_approval_no_update BEFORE UPDATE ON c06_exception_approval FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C06 exception approval immutable'; END$$
CREATE TRIGGER trg_c06_exception_approval_no_delete BEFORE DELETE ON c06_exception_approval FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C06 exception approval immutable'; END$$
CREATE TRIGGER trg_c06_exception_close_no_update BEFORE UPDATE ON c06_exception_close FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C06 exception close immutable'; END$$
CREATE TRIGGER trg_c06_exception_close_no_delete BEFORE DELETE ON c06_exception_close FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C06 exception close immutable'; END$$
-- The ledger is append-only AND positive records are attributable to the
-- exact actor/session/transition. A mismatched operator and session, or an
-- allowed record without its underlying transition, cannot be inserted.
CREATE TRIGGER trg_c06_exception_audit_guard BEFORE INSERT ON c06_exception_audit FOR EACH ROW
BEGIN
 IF NOT EXISTS(SELECT 1 FROM c05_platform_session s
    WHERE s.session_id=NEW.session_id AND s.operator_id=NEW.operator_id)
 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C06 exception audit actor/session mismatch'; END IF;
 IF NEW.allowed=1 AND NOT EXISTS (
    SELECT 1 FROM c06_exception_request req
    LEFT JOIN c06_exception_approval app ON app.exception_id=req.exception_id
    LEFT JOIN c06_exception_close cls ON cls.exception_id=req.exception_id
    WHERE req.exception_id=NEW.exception_id AND NEW.event_at_utc>=req.requested_at_utc
      AND (
        (NEW.action_code='platform/breakglass/approve'
          AND app.reviewer_id=NEW.operator_id AND app.review_session_id=NEW.session_id
          AND NEW.event_at_utc>=app.approved_at_utc)
        OR (req.requester_id=NEW.operator_id AND req.session_id=NEW.session_id
          AND (NEW.action_code='platform/breakglass/open'
            OR (NEW.action_code='platform/breakglass/read' AND app.exception_id IS NOT NULL
                AND cls.exception_id IS NULL AND NEW.event_at_utc>=app.approved_at_utc)
            OR (NEW.action_code='platform/breakglass/close' AND cls.closed_by=NEW.operator_id
                AND NEW.event_at_utc>=cls.closed_at_utc)))
      )
 ) THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C06 exception audit transition mismatch'; END IF;
END$$
CREATE TRIGGER trg_c06_exception_audit_no_update BEFORE UPDATE ON c06_exception_audit FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C06 exception audit immutable'; END$$
CREATE TRIGGER trg_c06_exception_audit_no_delete BEFORE DELETE ON c06_exception_audit FOR EACH ROW
BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='C06 exception audit immutable'; END$$
DELIMITER ;
