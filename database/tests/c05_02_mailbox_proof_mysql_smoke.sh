#!/usr/bin/env bash
# C05-02 disposable MySQL 8.4 + .NET10 independent email proof, local only.
# No external email, customer data, production DB, VPS or DNS.
set -euo pipefail
IMAGE="mysql:8.4@sha256:a81d043930e51745522bd5c430961cf8286f0a23f0a9a2a6e0108f7ea71b0902"
NAME="crecisol-c0502-mail-proof-$RANDOM-$$"
DB="c05_02_disposable"
ROOT_PASSWORD="$(openssl rand -hex 24)"
INGEST_PASSWORD="$(openssl rand -hex 24)"
PROOF_PASSWORD="$(openssl rand -hex 24)"
ADMIN_PASSWORD="$(openssl rand -hex 24)"
MASTER_HEX="$(openssl rand -hex 32)"
cleanup() { docker rm -f "$NAME" >/dev/null 2>&1 || true; }
trap cleanup EXIT
if [ -n "${GITHUB_ACTIONS:-}" ]; then
  for value in "$ROOT_PASSWORD" "$INGEST_PASSWORD" "$PROOF_PASSWORD" "$ADMIN_PASSWORD" "$MASTER_HEX"; do
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
  database/tests/fixtures/c05_02_mailbox_proof_atomic_fixture.sql; do
  docker exec -e MYSQL_PWD="$ROOT_PASSWORD" -i "$NAME" \
    mysql -uroot -D "$DB" < "$path"
done
root_sql "CREATE USER 'c05_02_proof_ingest'@'%' IDENTIFIED BY '$INGEST_PASSWORD';
 CREATE USER 'c05_02_proof_limited'@'%' IDENTIFIED BY '$PROOF_PASSWORD';
 CREATE USER 'c05_02_proof_setup'@'%' IDENTIFIED BY '$ADMIN_PASSWORD';
 GRANT EXECUTE ON PROCEDURE \`$DB\`.c05_02_record_registration_intent TO 'c05_02_proof_ingest'@'%';
 GRANT SELECT ON \`$DB\`.c05_02_neutral_intents TO 'c05_02_proof_limited'@'%';
 GRANT EXECUTE ON PROCEDURE \`$DB\`.c05_02_mailbox_proof_issue TO 'c05_02_proof_limited'@'%';
 GRANT EXECUTE ON PROCEDURE \`$DB\`.c05_02_mailbox_proof_verify TO 'c05_02_proof_limited'@'%';
 GRANT EXECUTE ON PROCEDURE \`$DB\`.c05_02_mailbox_proof_revoke TO 'c05_02_proof_limited'@'%';
 GRANT SELECT ON \`$DB\`.* TO 'c05_02_proof_setup'@'%';
 GRANT INSERT, UPDATE ON \`$DB\`.c05_02_tenants TO 'c05_02_proof_setup'@'%';
 GRANT UPDATE ON \`$DB\`.c05_02_mailbox_proofs TO 'c05_02_proof_setup'@'%';
 GRANT UPDATE ON \`$DB\`.c05_02_neutral_intents TO 'c05_02_proof_setup'@'%';"
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
dotnet run --no-build -c Release \
  --project backend/tests/Crecisol.EmailPossession.MySqlTests/Crecisol.EmailPossession.MySqlTests.csproj \
  -- --integration
echo 'PASS C05-02 REAL .NET10/MySQL8.4 disposable one-time email proof; NO real email, session or grant'
