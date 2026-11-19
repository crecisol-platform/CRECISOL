-- Isolated schema only; apply after migrations 001..006 with a privileged
-- migration identity. Grant this role to a separately provisioned API user and
-- SET DEFAULT ROLE explicitly. This file creates no user or password.
CREATE ROLE IF NOT EXISTS 'c05_identity_local_runtime';
-- C05-017: read-only SHA1 cutover policy; absence/blocked status denies login.
GRANT SELECT ON crecisol_c05.c05_totp_sha1_policy TO 'c05_identity_local_runtime';
GRANT SELECT ON crecisol_c05.c04_tenant_catalog TO 'c05_identity_local_runtime';
GRANT SELECT ON crecisol_c05.c04_verified_tenant_host TO 'c05_identity_local_runtime';
GRANT SELECT ON crecisol_c05.c05_identity_branch TO 'c05_identity_local_runtime';
GRANT SELECT ON crecisol_c05.c05_identity_private_resource TO 'c05_identity_local_runtime';
GRANT SELECT ON crecisol_c05.c05_identity_resource_grant TO 'c05_identity_local_runtime';
GRANT SELECT ON crecisol_c05.c05_identity_account TO 'c05_identity_local_runtime';
GRANT UPDATE (login_lookup_hmac,mailbox_ciphertext,mailbox_nonce,mailbox_tag,mailbox_key_version,
 mailbox_verified_at_utc,password_phc,account_state,security_revision,updated_at_utc,mailbox_crypto_scheme)
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
GRANT SELECT,INSERT,UPDATE ON crecisol_c05.c05_identity_passkey TO 'c05_identity_local_runtime';
GRANT SELECT,INSERT,UPDATE ON crecisol_c05.c05_identity_passkey_ceremony TO 'c05_identity_local_runtime';
GRANT SELECT ON crecisol_c05.c05_identity_terms TO 'c05_identity_local_runtime';
GRANT SELECT,INSERT ON crecisol_c05.c05_identity_terms_acceptance TO 'c05_identity_local_runtime';
GRANT SELECT,INSERT,UPDATE ON crecisol_c05.c05_identity_invitation TO 'c05_identity_local_runtime';
GRANT SELECT,INSERT,UPDATE ON crecisol_c05.c05_identity_pairing TO 'c05_identity_local_runtime';
GRANT SELECT,INSERT,UPDATE ON crecisol_c05.c05_identity_contact TO 'c05_identity_local_runtime';
GRANT SELECT,INSERT,UPDATE ON crecisol_c05.c05_identity_incident TO 'c05_identity_local_runtime';
GRANT SELECT ON crecisol_c05.c05_identity_audit TO 'c05_identity_local_runtime';
GRANT SELECT ON crecisol_c05.c05_platform_operator TO 'c05_identity_local_runtime';
GRANT UPDATE(last_totp_step,password_phc,protected_seed,security_revision,recovery_pending,login_hmac,conflict_login_hmac,protected_contact,previous_contact,contact_changed_at_utc) ON crecisol_c05.c05_platform_operator TO 'c05_identity_local_runtime';
GRANT SELECT,INSERT,UPDATE ON crecisol_c05.c05_platform_session TO 'c05_identity_local_runtime';
GRANT SELECT,INSERT,UPDATE ON crecisol_c05.c05_platform_challenge TO 'c05_identity_local_runtime';
GRANT SELECT,INSERT,UPDATE ON crecisol_c05.c05_platform_attempt_quota TO 'c05_identity_local_runtime';
GRANT INSERT ON crecisol_c05.c05_platform_identity_audit TO 'c05_identity_local_runtime';
GRANT SELECT,INSERT,UPDATE ON crecisol_c05.c05_identity_assisted_case TO 'c05_identity_local_runtime';
GRANT SELECT,INSERT ON crecisol_c05.c05_identity_assisted_review TO 'c05_identity_local_runtime';
GRANT SELECT,INSERT,UPDATE ON crecisol_c05.c05_identity_assisted_handoff TO 'c05_identity_local_runtime';
GRANT SELECT,INSERT,UPDATE ON crecisol_c05.c05_identity_mfa_reset_authorization TO 'c05_identity_local_runtime';
GRANT SELECT,INSERT,UPDATE ON crecisol_c05.c05_identity_scoped_profile TO 'c05_identity_local_runtime';
GRANT SELECT,INSERT ON crecisol_c05.c05_identity_abuse_signal TO 'c05_identity_local_runtime';
GRANT SELECT,INSERT,UPDATE ON crecisol_c05.c05_platform_scoped_profile TO 'c05_identity_local_runtime';
GRANT SELECT,INSERT,UPDATE ON crecisol_c05.c05_platform_recovery_case TO 'c05_identity_local_runtime';
GRANT SELECT,INSERT ON crecisol_c05.c05_platform_recovery_review TO 'c05_identity_local_runtime';
GRANT SELECT,INSERT,UPDATE ON crecisol_c05.c05_platform_recovery_share TO 'c05_identity_local_runtime';
GRANT SELECT,INSERT,UPDATE ON crecisol_c05.c05_platform_identity_incident TO 'c05_identity_local_runtime';
GRANT SELECT,INSERT,UPDATE ON crecisol_c05.c05_platform_mfa_backup TO 'c05_identity_local_runtime';
GRANT SELECT,INSERT,UPDATE ON crecisol_c05.c05_platform_factor_change TO 'c05_identity_local_runtime';
GRANT SELECT,INSERT,UPDATE ON crecisol_c05.c05_platform_mailbox_change TO 'c05_identity_local_runtime';
GRANT INSERT ON crecisol_c05.c05_identity_membership TO 'c05_identity_local_runtime';
GRANT INSERT ON crecisol_c05.c05_identity_audit TO 'c05_identity_local_runtime';
GRANT INSERT ON crecisol_c05.c05_identity_membership_approval TO 'c05_identity_local_runtime';
GRANT INSERT ON crecisol_c05.c05_identity_registration_intent TO 'c05_identity_local_runtime';
GRANT SELECT,INSERT,UPDATE ON crecisol_c05.c05_identity_registration_quota TO 'c05_identity_local_runtime';
GRANT EXECUTE ON PROCEDURE crecisol_c05.c05_operational_stage_mailbox_proof TO 'c05_identity_local_runtime';
GRANT EXECUTE ON PROCEDURE crecisol_c05.c05_operational_consume_mailbox_proof TO 'c05_identity_local_runtime';
GRANT EXECUTE ON PROCEDURE crecisol_c05.c05_operational_reserve_registration_completion TO 'c05_identity_local_runtime';
GRANT EXECUTE ON PROCEDURE crecisol_c05.c05_operational_complete_pending_registration TO 'c05_identity_local_runtime';
GRANT EXECUTE ON PROCEDURE crecisol_c05.c05_operational_consume_join_proof TO 'c05_identity_local_runtime';
GRANT EXECUTE ON PROCEDURE crecisol_c05.c05_operational_claim_mailbox_proof_outbox TO 'c05_identity_local_runtime';
GRANT EXECUTE ON PROCEDURE crecisol_c05.c05_operational_complete_mailbox_proof_delivery TO 'c05_identity_local_runtime';

-- C05-018 server-side, MFA-authorized session policy edits only.
GRANT SELECT ON crecisol_c05.c05_session_idle_policy TO 'c05_identity_local_runtime';
GRANT UPDATE(idle_minutes,revision,changed_at_utc,changed_by)
 ON crecisol_c05.c05_session_idle_policy TO 'c05_identity_local_runtime';
