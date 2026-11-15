#!/usr/bin/env bash
# C05 integrated neutral registration and one-time mailbox proof in ONE loopback process; disposable MySQL8.4.
# No external email, customer data, production DB, VPS or DNS.
set -euo pipefail
IMAGE="mysql:8.4@sha256:a81d043930e51745522bd5c430961cf8286f0a23f0a9a2a6e0108f7ea71b0902"
NAME="crecisol-c0502-mail-proof-$RANDOM-$$"
DB="c05_02_disposable"
ROOT_PASSWORD="$(openssl rand -hex 24)"
INGEST_PASSWORD="$(openssl rand -hex 24)"
PROOF_PASSWORD="$(openssl rand -hex 24)"
CANDIDATE_PASSWORD="$(openssl rand -hex 24)"
DISPATCH_PASSWORD="$(openssl rand -hex 24)"
ACTIVATION_PASSWORD="$(openssl rand -hex 24)"
DIRECTORY_PASSWORD="$(openssl rand -hex 24)"
RATE_RUNNER_PASSWORD="$(openssl rand -hex 24)"
RATE_SETUP_PASSWORD="$(openssl rand -hex 24)"
RATE_DB="c05_10_disposable"
MFA_DB="c05_05_disposable"
MFA_SETUP_PASSWORD="$(openssl rand -hex 24)"
MFA_WORKER_PASSWORD="$(openssl rand -hex 24)"
ADMIN_PASSWORD="$(openssl rand -hex 24)"
MASTER_HEX="$(openssl rand -hex 32)"
cleanup() { docker rm -f "$NAME" >/dev/null 2>&1 || true; }
trap cleanup EXIT
if [ -n "${GITHUB_ACTIONS:-}" ]; then
  for value in "$ROOT_PASSWORD" "$INGEST_PASSWORD" "$PROOF_PASSWORD" "$CANDIDATE_PASSWORD" "$DISPATCH_PASSWORD" "$ACTIVATION_PASSWORD" "$DIRECTORY_PASSWORD" "$RATE_RUNNER_PASSWORD" "$RATE_SETUP_PASSWORD" "$MFA_SETUP_PASSWORD" "$MFA_WORKER_PASSWORD" "$ADMIN_PASSWORD" "$MASTER_HEX"; do
    echo "::add-mask::$value"
  done
fi
root_sql() {
  docker exec -e MYSQL_PWD="$ROOT_PASSWORD" "$NAME" \
    mysql -uroot -N -B -D "$DB" -e "$1"
}
docker run --rm -d --name "$NAME" --network bridge -p '127.0.0.1::3306' \
  -e MYSQL_ROOT_PASSWORD="$ROOT_PASSWORD" \
  -e MYSQL_DATABASE="$DB" "$IMAGE" >/dev/null
booted=0
ready=0
for _ in $(seq 1 120); do
  if [ "$booted" -eq 0 ] &&
     docker logs "$NAME" 2>&1 | grep -Fq 'MySQL init process done. Ready for start up.'; then
    booted=1
  fi
  if [ "$booted" -eq 1 ] && root_sql "SELECT 1" >/dev/null 2>&1; then
    sleep 1
    if root_sql "SELECT 1" >/dev/null 2>&1; then ready=1; break; fi
  fi
  sleep 1
done
[ "$ready" -eq 1 ] || { echo 'FAIL C05-02 proof: isolated MySQL startup' >&2; exit 1; }
case "$(root_sql 'SELECT VERSION()')" in
  8.4.*) ;; *) echo 'FAIL C05-02: actual MySQL 8.4 required' >&2; exit 1 ;;
esac
for path in \
  database/tests/fixtures/c05_02_invitation_atomic_fixture.sql \
  database/tests/fixtures/c05_02_neutral_registration_fixture.sql \
  database/tests/fixtures/c05_02_mailbox_proof_atomic_fixture.sql \
  database/tests/fixtures/c05_02_verified_candidate_fixture.sql \
  database/tests/fixtures/c05_02_neutral_dispatch_fixture.sql \
  database/tests/fixtures/c05_02_membership_approval_fixture.sql; do
  docker exec -e MYSQL_PWD="$ROOT_PASSWORD" -i "$NAME" \
    mysql -uroot -D "$DB" < "$path"
done
# Independent physical schema keeps the C05-10 preauth budget separated
# from the registration DB, while remaining on one disposable loopback server.
root_sql "CREATE DATABASE \`$RATE_DB\`; CREATE DATABASE \`$MFA_DB\`;"
docker exec -e MYSQL_PWD="$ROOT_PASSWORD" -i "$NAME" \
  mysql -uroot -D "$RATE_DB" < database/tests/fixtures/c05_10_disposable_rate_limits.sql
for path in \
  database/tests/fixtures/c05_05_disposable_sessions.sql \
  database/tests/fixtures/c05_06_disposable_recovery.sql \
  database/tests/fixtures/c05_08_disposable_mfa.sql \
  database/tests/fixtures/c05_05_mfa_bound_session_fixture.sql; do
  docker exec -e MYSQL_PWD="$ROOT_PASSWORD" -i "$NAME" \
    mysql -uroot -D "$MFA_DB" < "$path"
done
root_sql "CREATE USER 'c05_10_integrated_runner'@'%' IDENTIFIED BY '$RATE_RUNNER_PASSWORD';
 CREATE USER 'c05_10_integrated_setup'@'%' IDENTIFIED BY '$RATE_SETUP_PASSWORD';
 CREATE USER 'c05_05_mfa_setup'@'%' IDENTIFIED BY '$MFA_SETUP_PASSWORD';
 CREATE USER 'c05_05_mfa_worker'@'%' IDENTIFIED BY '$MFA_WORKER_PASSWORD';
 GRANT SELECT,INSERT,UPDATE ON \`$MFA_DB\`.*
   TO 'c05_05_mfa_setup'@'%';
 GRANT EXECUTE ON PROCEDURE \`$MFA_DB\`.c05_08_open_enrollment
   TO 'c05_05_mfa_worker'@'%';
 GRANT EXECUTE ON PROCEDURE \`$MFA_DB\`.c05_08_open_stepup
   TO 'c05_05_mfa_worker'@'%';
 GRANT EXECUTE ON PROCEDURE \`$MFA_DB\`.c05_08_commit_proof
   TO 'c05_05_mfa_worker'@'%';
 GRANT EXECUTE ON PROCEDURE \`$MFA_DB\`.c05_08_read_counter
   TO 'c05_05_mfa_worker'@'%';
 GRANT EXECUTE ON PROCEDURE \`$MFA_DB\`.c05_05_issue_after_mfa_once
   TO 'c05_05_mfa_worker'@'%';
 GRANT EXECUTE ON PROCEDURE \`$MFA_DB\`.c05_05_is_current
   TO 'c05_05_mfa_worker'@'%';
 GRANT EXECUTE ON PROCEDURE \`$MFA_DB\`.c05_05_rotate_once
   TO 'c05_05_mfa_worker'@'%';
 GRANT EXECUTE ON PROCEDURE \`$MFA_DB\`.c05_05_logout_once
   TO 'c05_05_mfa_worker'@'%';
 GRANT EXECUTE ON PROCEDURE \`$RATE_DB\`.c05_10_admit_once
   TO 'c05_10_integrated_runner'@'%';
 GRANT EXECUTE ON PROCEDURE \`$RATE_DB\`.c05_10_complete_once
   TO 'c05_10_integrated_runner'@'%';
 GRANT SELECT,INSERT,UPDATE ON \`$RATE_DB\`.*
   TO 'c05_10_integrated_setup'@'%';
 CREATE USER 'c05_02_proof_ingest'@'%' IDENTIFIED BY '$INGEST_PASSWORD';
 CREATE USER 'c05_02_proof_limited'@'%' IDENTIFIED BY '$PROOF_PASSWORD';
 CREATE USER 'c05_02_proof_setup'@'%' IDENTIFIED BY '$ADMIN_PASSWORD';
 CREATE USER 'c05_02_candidate_limited'@'%' IDENTIFIED BY '$CANDIDATE_PASSWORD';
 CREATE USER 'c05_02_dispatch_worker'@'%' IDENTIFIED BY '$DISPATCH_PASSWORD';
 CREATE USER 'c05_02_membership_worker'@'%' IDENTIFIED BY '$ACTIVATION_PASSWORD';
 CREATE USER 'c05_02_login_directory'@'%' IDENTIFIED BY '$DIRECTORY_PASSWORD';
 GRANT EXECUTE ON PROCEDURE \`$DB\`.c05_02_record_registration_intent TO 'c05_02_proof_ingest'@'%';
 GRANT SELECT ON \`$DB\`.c05_02_neutral_intents TO 'c05_02_proof_limited'@'%';
 GRANT EXECUTE ON PROCEDURE \`$DB\`.c05_02_mailbox_proof_issue TO 'c05_02_proof_limited'@'%';
 GRANT EXECUTE ON PROCEDURE \`$DB\`.c05_02_mailbox_proof_verify TO 'c05_02_proof_limited'@'%';
 GRANT EXECUTE ON PROCEDURE \`$DB\`.c05_02_mailbox_proof_revoke TO 'c05_02_proof_limited'@'%';
 GRANT SELECT (tenant_id,intent_id,nonce,tag,ciphertext,key_version,mailbox_digest,expires_at_utc)
   ON \`$DB\`.c05_02_neutral_intents TO 'c05_02_candidate_limited'@'%';
 GRANT EXECUTE ON PROCEDURE \`$DB\`.c05_02_create_pending_candidate
   TO 'c05_02_candidate_limited'@'%';
 GRANT EXECUTE ON PROCEDURE \`$DB\`.c05_02_dispatch_claim
   TO 'c05_02_dispatch_worker'@'%';
 GRANT EXECUTE ON PROCEDURE \`$DB\`.c05_02_dispatch_complete
   TO 'c05_02_dispatch_worker'@'%';
 GRANT EXECUTE ON PROCEDURE \`$DB\`.c05_02_activate_approved_candidate
   TO 'c05_02_membership_worker'@'%';
 GRANT SELECT (actor_id,lifecycle,verifier_phc,created_utc,source_tenant_id,login_digest)
   ON \`$DB\`.c05_02_candidate_accounts TO 'c05_02_login_directory'@'%';
 GRANT SELECT (tenant_id,actor_id,link_id,lifecycle,revision,changed_utc,granted_utc,expires_utc)
   ON \`$DB\`.c05_02_approved_memberships TO 'c05_02_login_directory'@'%';
 GRANT SELECT (tenant_id,lifecycle) ON \`$DB\`.c05_02_tenants
   TO 'c05_02_login_directory'@'%';
 GRANT SELECT ON \`$DB\`.* TO 'c05_02_proof_setup'@'%';
 GRANT INSERT, UPDATE ON \`$DB\`.c05_02_tenants TO 'c05_02_proof_setup'@'%';
 GRANT UPDATE ON \`$DB\`.c05_02_mailbox_proofs TO 'c05_02_proof_setup'@'%';
 GRANT UPDATE ON \`$DB\`.c05_02_neutral_intents TO 'c05_02_proof_setup'@'%';
 GRANT UPDATE ON \`$DB\`.c05_02_proof_dispatch TO 'c05_02_proof_setup'@'%';
 GRANT INSERT,UPDATE ON \`$DB\`.c05_02_member_authorities TO 'c05_02_proof_setup'@'%';
 GRANT INSERT,UPDATE ON \`$DB\`.c05_02_member_approval_orders TO 'c05_02_proof_setup'@'%';
 GRANT UPDATE ON \`$DB\`.c05_02_approved_memberships TO 'c05_02_proof_setup'@'%';
 GRANT UPDATE ON \`$DB\`.c05_02_candidate_accounts TO 'c05_02_proof_setup'@'%';"
port_mapping="$(docker port "$NAME" '3306/tcp' | head -n 1)"
case "$port_mapping" in
  127.0.0.1:[0-9]*) port="${port_mapping##*:}" ;;
  *) echo 'FAIL C05-02 proof: DB socket is not loopback-only' >&2; exit 1 ;;
esac
case "$port" in *[!0-9]*|"") echo 'FAIL C05-02 proof: invalid port' >&2; exit 1 ;; esac
export CRECISOL_C05_02_PROOF_SETUP_DSN="Server=127.0.0.1;Port=$port;Database=$DB;User ID=c05_02_proof_setup;Password=$ADMIN_PASSWORD;Pooling=false"
export CRECISOL_C05_02_PROOF_INGEST_DSN="Server=127.0.0.1;Port=$port;Database=$DB;User ID=c05_02_proof_ingest;Password=$INGEST_PASSWORD;Pooling=false"
export CRECISOL_C05_02_PROOF_TEST_DSN="Server=127.0.0.1;Port=$port;Database=$DB;User ID=c05_02_proof_limited;Password=$PROOF_PASSWORD;Pooling=false"
export CRECISOL_C05_02_PROOF_MASTER_HEX="$MASTER_HEX"
export CRECISOL_C05_02_NEUTRAL_TEST_DSN="$CRECISOL_C05_02_PROOF_INGEST_DSN"
export CRECISOL_C05_02_NEUTRAL_ADMIN_DSN="$CRECISOL_C05_02_PROOF_SETUP_DSN"
export CRECISOL_C05_02_NEUTRAL_MASTER_HEX="$CRECISOL_C05_02_PROOF_MASTER_HEX"
export CRECISOL_C05_PROOF_TEST_DSN="$CRECISOL_C05_02_PROOF_TEST_DSN"
export CRECISOL_C05_CANDIDATE_DSN="Server=127.0.0.1;Port=$port;Database=$DB;User ID=c05_02_candidate_limited;Password=$CANDIDATE_PASSWORD;Pooling=false"
export CRECISOL_C05_DISPATCH_DSN="Server=127.0.0.1;Port=$port;Database=$DB;User ID=c05_02_dispatch_worker;Password=$DISPATCH_PASSWORD;Pooling=false"
export CRECISOL_C05_ACTIVATION_DSN="Server=127.0.0.1;Port=$port;Database=$DB;User ID=c05_02_membership_worker;Password=$ACTIVATION_PASSWORD;Pooling=false"
export CRECISOL_C05_DIRECTORY_DSN="Server=127.0.0.1;Port=$port;Database=$DB;User ID=c05_02_login_directory;Password=$DIRECTORY_PASSWORD;Pooling=false"
export CRECISOL_C05_RATE_SETUP_DSN="Server=127.0.0.1;Port=$port;Database=$RATE_DB;User ID=c05_10_integrated_setup;Password=$RATE_SETUP_PASSWORD;Pooling=false"
export CRECISOL_C05_RATE_RUNNER_DSN="Server=127.0.0.1;Port=$port;Database=$RATE_DB;User ID=c05_10_integrated_runner;Password=$RATE_RUNNER_PASSWORD;Pooling=false"
export CRECISOL_C05_MFA_SETUP_DSN="Server=127.0.0.1;Port=$port;Database=$MFA_DB;User ID=c05_05_mfa_setup;Password=$MFA_SETUP_PASSWORD;Pooling=false"
export CRECISOL_C05_MFA_WORKER_DSN="Server=127.0.0.1;Port=$port;Database=$MFA_DB;User ID=c05_05_mfa_worker;Password=$MFA_WORKER_PASSWORD;Pooling=false"
dotnet run --no-build -c Release \
  --project backend/tests/Crecisol.NeutralRegistration.IntegrationTests/Crecisol.NeutralRegistration.IntegrationTests.csproj \
  -- --integration
echo 'PASS C05 disposable registration -> approval -> preauth -> TOTP step-up -> atomic revocable session; NO production auth/Angular'
