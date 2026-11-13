-- C05-02 DISPOSABLE MySQL 8.4 test fixture ONLY. Not a production migration.
-- No credentials, email addresses, raw invitation codes, roles or grant changes.
CREATE TABLE c05_02_tenants (
    tenant_id BINARY(16) NOT NULL PRIMARY KEY,
    lifecycle ENUM('ACTIVE','SUSPENDED') NOT NULL,
    terms_version BIGINT UNSIGNED NOT NULL,
    CHECK (terms_version > 0)
) ENGINE=InnoDB;

CREATE TABLE c05_02_issuers (
    tenant_id BINARY(16) NOT NULL,
    actor_id BINARY(16) NOT NULL,
    profile_scope ENUM('INVITE_EMPLOYEE','INVITE_CONSUMER') NOT NULL,
    enabled TINYINT(1) NOT NULL,
    PRIMARY KEY (tenant_id, actor_id),
    CONSTRAINT fk_c05_02_issuers_tenant FOREIGN KEY (tenant_id)
      REFERENCES c05_02_tenants(tenant_id) ON UPDATE RESTRICT ON DELETE RESTRICT,
    CHECK (enabled IN (0,1))
) ENGINE=InnoDB;

CREATE TABLE c05_02_recipients (
    tenant_id BINARY(16) NOT NULL,
    recipient_id BINARY(16) NOT NULL,
    actor_id BINARY(16) NOT NULL,
    channel_verified TINYINT(1) NOT NULL,
    verified_until_utc DATETIME(6) NOT NULL,
    PRIMARY KEY (tenant_id, recipient_id),
    CONSTRAINT fk_c05_02_recipient_tenant FOREIGN KEY (tenant_id)
      REFERENCES c05_02_tenants(tenant_id) ON UPDATE RESTRICT ON DELETE RESTRICT,
    CHECK (channel_verified IN (0,1))
) ENGINE=InnoDB;

CREATE TABLE c05_02_consents (
    tenant_id BINARY(16) NOT NULL,
    recipient_id BINARY(16) NOT NULL,
    terms_version BIGINT UNSIGNED NOT NULL,
    accepted_at_utc DATETIME(6) NOT NULL,
    PRIMARY KEY (tenant_id, recipient_id, terms_version),
    CONSTRAINT fk_c05_02_consent_recipient FOREIGN KEY (tenant_id, recipient_id)
      REFERENCES c05_02_recipients(tenant_id, recipient_id)
      ON UPDATE RESTRICT ON DELETE RESTRICT,
    CHECK (terms_version > 0)
) ENGINE=InnoDB;

CREATE TABLE c05_02_invitations (
    tenant_id BINARY(16) NOT NULL,
    invitation_id BINARY(16) NOT NULL,
    issuer_id BINARY(16) NOT NULL,
    recipient_id BINARY(16) NOT NULL,
    profile_scope ENUM('EMPLOYEE','CONSUMER') NOT NULL,
    token_digest BINARY(32) NOT NULL,
    lifecycle ENUM('PENDING','CLAIMED','REVOKED') NOT NULL,
    revision BIGINT UNSIGNED NOT NULL,
    terms_version BIGINT UNSIGNED NOT NULL,
    issued_at_utc DATETIME(6) NOT NULL,
    expires_at_utc DATETIME(6) NOT NULL,
    claimed_at_utc DATETIME(6) NULL,
    PRIMARY KEY (tenant_id, invitation_id),
    UNIQUE KEY uq_c05_02_digest_tenant (tenant_id, token_digest),
    CONSTRAINT fk_c05_02_invitation_tenant FOREIGN KEY (tenant_id)
      REFERENCES c05_02_tenants(tenant_id) ON UPDATE RESTRICT ON DELETE RESTRICT,
    CONSTRAINT fk_c05_02_invitation_issuer FOREIGN KEY (tenant_id, issuer_id)
      REFERENCES c05_02_issuers(tenant_id, actor_id)
      ON UPDATE RESTRICT ON DELETE RESTRICT,
    CONSTRAINT fk_c05_02_invitation_recipient FOREIGN KEY (tenant_id, recipient_id)
      REFERENCES c05_02_recipients(tenant_id, recipient_id)
      ON UPDATE RESTRICT ON DELETE RESTRICT,
    CHECK (revision > 0 AND terms_version > 0),
    CHECK (expires_at_utc > issued_at_utc),
    CHECK (
        (lifecycle = 'CLAIMED' AND claimed_at_utc IS NOT NULL)
        OR (lifecycle <> 'CLAIMED' AND claimed_at_utc IS NULL)
    )
) ENGINE=InnoDB;

DELIMITER //
-- The caller must already have obtained p_tenant from verified direct host.
-- Test runner may call this procedure but has zero SELECT/INSERT/UPDATE rights.
-- One InnoDB UPDATE is the linearization point; 0 rows = neutral denial.
CREATE PROCEDURE c05_02_claim_once(
    IN p_tenant BINARY(16), IN p_invitation BINARY(16),
    IN p_actor BINARY(16), IN p_recipient BINARY(16),
    IN p_digest BINARY(32),
    IN p_terms BIGINT UNSIGNED, IN p_revision BIGINT UNSIGNED
)
SQL SECURITY DEFINER
BEGIN
    UPDATE c05_02_invitations i
      JOIN c05_02_tenants t ON t.tenant_id = i.tenant_id
      JOIN c05_02_issuers s
        ON s.tenant_id = i.tenant_id AND s.actor_id = i.issuer_id
      JOIN c05_02_recipients r
        ON r.tenant_id = i.tenant_id AND r.recipient_id = i.recipient_id
      JOIN c05_02_consents c
        ON c.tenant_id = r.tenant_id AND c.recipient_id = r.recipient_id
        AND c.terms_version = i.terms_version
    SET i.lifecycle = 'CLAIMED',
        i.revision = i.revision + 1,
        i.claimed_at_utc = UTC_TIMESTAMP(6)
    WHERE i.tenant_id = p_tenant
      AND i.invitation_id = p_invitation
      AND i.recipient_id = p_recipient
      AND i.token_digest = p_digest
      AND i.terms_version = p_terms
      AND i.revision = p_revision
      AND i.lifecycle = 'PENDING'
      AND i.issued_at_utc <= UTC_TIMESTAMP(6)
      AND UTC_TIMESTAMP(6) < i.expires_at_utc
      AND i.issuer_id <> p_actor
      AND t.lifecycle = 'ACTIVE'
      AND t.terms_version = i.terms_version
      AND s.enabled = 1
      AND (
          (i.profile_scope = 'EMPLOYEE' AND s.profile_scope = 'INVITE_EMPLOYEE')
          OR (i.profile_scope = 'CONSUMER' AND s.profile_scope = 'INVITE_CONSUMER')
      )
      AND r.actor_id = p_actor
      AND r.channel_verified = 1
      AND UTC_TIMESTAMP(6) < r.verified_until_utc
      AND c.accepted_at_utc <= UTC_TIMESTAMP(6);
    SELECT IF(ROW_COUNT() = 1, 'CLAIMED', 'DENIED') AS result;
END//
DELIMITER ;
