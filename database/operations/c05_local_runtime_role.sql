-- Isolated schema only; apply after migrations 001..006 with a privileged
-- migration identity. Grant this role to a separately provisioned API user and
-- SET DEFAULT ROLE explicitly. This file creates no user or password.
CREATE ROLE IF NOT EXISTS 'c05_identity_local_runtime';
GRANT SELECT ON crecisol_c05.c04_tenant_catalog TO 'c05_identity_local_runtime';
GRANT SELECT ON crecisol_c05.c04_verified_tenant_host TO 'c05_identity_local_runtime';
GRANT SELECT ON crecisol_c05.c05_identity_branch TO 'c05_identity_local_runtime';
GRANT SELECT ON crecisol_c05.c05_identity_private_resource TO 'c05_identity_local_runtime';
GRANT SELECT ON crecisol_c05.c05_identity_resource_grant TO 'c05_identity_local_runtime';
GRANT SELECT ON crecisol_c05.c05_identity_account TO 'c05_identity_local_runtime';
GRANT UPDATE (login_lookup_hmac,mailbox_ciphertext,mailbox_nonce,mailbox_tag,mailbox_key_version,
 mailbox_verified_at_utc,password_phc,account_state,security_revision,updated_at_utc)
 ON crecisol_c05.c05_identity_account TO 'c05_identity_local_runtime';
GRANT SELECT ON crecisol_c05.c05_identity_membership TO 'c05_identity_local_runtime';
GRANT UPDATE (membership_state,role_code,approval_revision,approved_by,approved_at_utc,granted_at_utc,expires_at_utc)
 ON crecisol_c05.c05_identity_membership TO 'c05_identity_local_runtime';
GRANT SELECT,INSERT,UPDATE ON crecisol_c05.c05_identity_session TO 'c05_identity_local_runtime';
GRANT SELECT,INSERT,UPDATE ON crecisol_c05.c05_identity_mfa_factor TO 'c05_identity_local_runtime';
GRANT SELECT,INSERT,UPDATE ON crecisol_c05.c05_identity_mfa_backup TO 'c05_identity_local_runtime';
GRANT SELECT,INSERT,UPDATE ON crecisol_c05.c05_identity_challenge TO 'c05_identity_local_runtime';
GRANT SELECT,INSERT,UPDATE ON crecisol_c05.c05_identity_attempt_quota TO 'c05_identity_local_runtime';
GRANT SELECT,INSERT,UPDATE ON crecisol_c05.c05_identity_recovery TO 'c05_identity_local_runtime';
GRANT SELECT,INSERT,UPDATE ON crecisol_c05.c05_identity_sensitive_outbox TO 'c05_identity_local_runtime';
GRANT INSERT ON crecisol_c05.c05_identity_audit TO 'c05_identity_local_runtime';
GRANT INSERT ON crecisol_c05.c05_identity_membership_approval TO 'c05_identity_local_runtime';
GRANT INSERT ON crecisol_c05.c05_identity_registration_intent TO 'c05_identity_local_runtime';
GRANT SELECT,INSERT,UPDATE ON crecisol_c05.c05_identity_registration_quota TO 'c05_identity_local_runtime';
GRANT EXECUTE ON PROCEDURE crecisol_c05.c05_operational_stage_mailbox_proof TO 'c05_identity_local_runtime';
GRANT EXECUTE ON PROCEDURE crecisol_c05.c05_operational_consume_mailbox_proof TO 'c05_identity_local_runtime';
GRANT EXECUTE ON PROCEDURE crecisol_c05.c05_operational_reserve_registration_completion TO 'c05_identity_local_runtime';
GRANT EXECUTE ON PROCEDURE crecisol_c05.c05_operational_complete_pending_registration TO 'c05_identity_local_runtime';
GRANT EXECUTE ON PROCEDURE crecisol_c05.c05_operational_claim_mailbox_proof_outbox TO 'c05_identity_local_runtime';
GRANT EXECUTE ON PROCEDURE crecisol_c05.c05_operational_complete_mailbox_proof_delivery TO 'c05_identity_local_runtime';
