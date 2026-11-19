#!/usr/bin/env bash
# Destructive SHA256->SHA1 retirement tested ONLY on an isolated disposable MySQL.
# Never run against local persistent volumes, C01/C02, or production.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
NAME="crecisol-c05-sha1-cutover-$$"
PASS="$(openssl rand -hex 24)"
IMAGE='mysql:8.4@sha256:a81d043930e51745522bd5c430961cf8286f0a23f0a9a2a6e0108f7ea71b0902'
cleanup() { docker rm -f "$NAME" >/dev/null 2>&1 || true; }
trap cleanup EXIT
docker run --rm -d --name "$NAME" -p 127.0.0.1::3306 \
  -e "MYSQL_ROOT_PASSWORD=$PASS" -e MYSQL_DATABASE=crecisol_c05 "$IMAGE" >/dev/null
db() { docker exec -i -e "MYSQL_PWD=$PASS" "$NAME" mysql -uroot -N -B crecisol_c05 -e "$1"; }
apply() { docker exec -i -e "MYSQL_PWD=$PASS" "$NAME" mysql -uroot crecisol_c05 < "$1"; }
ready=0
for _ in $(seq 1 120); do
  if db "SELECT 1" >/dev/null 2>&1; then ready=1; break; fi
  sleep 1
done
test "$ready" -eq 1
names=(
  001_c05_identity_operational 002_c05_registration_proof_outbox
  003_c05_pending_account_completion 004_c05_local_proof_delivery
  005_c05_operational_login_directory 006_c05_operational_identity_flow
  007_c05_passkeys 008_c05_terms_invitations
  009_c05_pairing 010_c05_security_notices
  011_c05_platform_recovery 012_c05_scoped_profile
  013_c05_abuse_signals 014_c05_operator_recovery
  015_c05_session_devices 016_c05_existing_identity_join
)
for item in "${names[@]}"; do apply "$ROOT/database/migrations/$item.sql"; done
# Synthetic credentials intentionally NOT functional; this exercises data cutover,
# preservation of unrelated identities and revocation, never fixture login.
db "
INSERT INTO c04_tenant_catalog VALUES(
 UNHEX(REPEAT('ab',16)),'sha1-qa','SHA1 QA',3,2,1,UTC_TIMESTAMP(6));
INSERT INTO c05_identity_account(account_id,login_lookup_hmac,
 mailbox_ciphertext,mailbox_nonce,mailbox_tag,mailbox_key_version,
 mailbox_verified_at_utc,password_phc,account_state,security_revision,
 created_at_utc,updated_at_utc)
VALUES(UNHEX(REPEAT('a1',16)),UNHEX(REPEAT('a2',32)),X'01',
 UNHEX(REPEAT('a3',12)),UNHEX(REPEAT('a4',16)),1,
 UTC_TIMESTAMP(6),'\$argon2id\$v=19\$m=65536,t=3,p=1\$test\$test',
 1,1,UTC_TIMESTAMP(6),UTC_TIMESTAMP(6));
INSERT INTO c05_identity_mfa_factor(account_id,factor_id,factor_kind,
 protected_secret,secret_nonce,secret_tag,key_version,factor_state,
 factor_revision,last_accepted_step,enrolled_at_utc,revoked_at_utc)
VALUES(UNHEX(REPEAT('a1',16)),UNHEX(REPEAT('a5',16)),1,
 UNHEX(REPEAT('a6',32)),UNHEX(REPEAT('a7',12)),UNHEX(REPEAT('a8',16)),
 1,1,1,120,UTC_TIMESTAMP(6),NULL);
INSERT INTO c05_identity_mfa_factor(account_id,factor_id,factor_kind,
 protected_secret,secret_nonce,secret_tag,key_version,factor_state,
 factor_revision,last_accepted_step,enrolled_at_utc,revoked_at_utc)
VALUES(UNHEX(REPEAT('a1',16)),UNHEX(REPEAT('b5',16)),2,
 UNHEX(REPEAT('b6',64)),UNHEX(REPEAT('b7',12)),UNHEX(REPEAT('b8',16)),
 1,1,1,NULL,UTC_TIMESTAMP(6),NULL);
INSERT INTO c05_platform_operator(operator_id,login_hmac,conflict_login_hmac,
 password_phc,protected_seed,active,security_revision,last_totp_step,protected_contact)
VALUES(UNHEX(REPEAT('c1',16)),UNHEX(REPEAT('c2',32)),UNHEX(REPEAT('c3',32)),
 '\$argon2id\$v=19\$m=65536,t=3,p=1\$test\$test',
 UNHEX(REPEAT('c4',60)),TRUE,1,120,UNHEX(REPEAT('c5',40)));
"
test "$(db "SELECT COUNT(*) FROM c05_identity_mfa_factor WHERE factor_kind=1 AND factor_state=1")" = 1
test "$(db "SELECT COUNT(*) FROM c05_platform_operator WHERE recovery_pending=0")" = 1
apply "$ROOT/database/migrations/017_c05_totp_sha1_exclusive.sql"
test "$(db "SELECT status FROM c05_totp_sha1_policy WHERE policy_id=1")" = 1
test "$(db "SELECT CONCAT(factor_state,':',LENGTH(protected_secret),':',last_accepted_step IS NULL) FROM c05_identity_mfa_factor WHERE factor_kind=1")" = '2:0:1'
test "$(db "SELECT CONCAT(factor_state,':',LENGTH(protected_secret)) FROM c05_identity_mfa_factor WHERE factor_kind=2")" = '1:64'
test "$(db "SELECT CONCAT(recovery_pending,':',LENGTH(protected_seed),':',last_totp_step IS NULL) FROM c05_platform_operator")" = '1:0:1'
test "$(db "SELECT security_revision FROM c05_identity_account")" = 2
test "$(db "SELECT COUNT(*) FROM c04_tenant_catalog")" = 1
test "$(db "SELECT COUNT(*) FROM c05_identity_account")" = 1
# Re-running the irreversible cutover must FAIL, never delete new SHA1 factors.
if apply "$ROOT/database/migrations/017_c05_totp_sha1_exclusive.sql" >/dev/null 2>&1; then
  echo 'FAIL SHA1 cutover unexpectedly allowed second application' >&2
  exit 1
fi
test "$(db "SELECT status FROM c05_totp_sha1_policy WHERE policy_id=1")" = 1
test "$(db "SELECT COUNT(*) FROM c05_identity_account")" = 1

echo 'PASS C05-017 MySQL8.4: legacy factor cryptoshred, operator lock, WebAuthn preserved, blocked rerun'
# A separate ADDITIVE C05-018 policy must preserve all credentials/data while
# rejecting unsafe minute values and defaulting to 30 idle minutes.
apply "$ROOT/database/migrations/018_c05_session_idle_policy.sql"
test "$(db "SELECT idle_minutes FROM c05_session_idle_policy WHERE policy_id=1")" = 30
test "$(db "SELECT revision FROM c05_session_idle_policy WHERE policy_id=1")" = 1
test "$(db "SELECT can_manage_session_policy FROM c05_platform_operator")" = 0
test "$(db "SELECT COUNT(*) FROM c05_identity_account")" = 1
test "$(db "SELECT COUNT(*) FROM c05_platform_operator")" = 1
db "UPDATE c05_session_idle_policy SET idle_minutes=5,revision=revision+1 WHERE policy_id=1"
test "$(db "SELECT idle_minutes FROM c05_session_idle_policy WHERE policy_id=1")" = 5
db "UPDATE c05_session_idle_policy SET idle_minutes=120,revision=revision+1 WHERE policy_id=1"
test "$(db "SELECT idle_minutes FROM c05_session_idle_policy WHERE policy_id=1")" = 120
for invalid in 0 4 121 65535; do
  if db "UPDATE c05_session_idle_policy SET idle_minutes=$invalid WHERE policy_id=1" >/dev/null 2>&1; then
    echo 'FAIL: invalid inactivity minutes accepted by MySQL constraint' >&2
    exit 1
  fi
done
db "UPDATE c05_session_idle_policy SET idle_minutes=30,revision=revision+1 WHERE policy_id=1"
if apply "$ROOT/database/migrations/018_c05_session_idle_policy.sql" >/dev/null 2>&1; then
  echo 'FAIL C05-018 unexpected second ALTER TABLE or table overwrite' >&2
  exit 1
fi
test "$(db "SELECT idle_minutes FROM c05_session_idle_policy WHERE policy_id=1")" = 30
echo 'PASS C05-018 MySQL8.4: initial 30m, bounds 5..120, default no-admin, preserved credentials, no migration replay'

