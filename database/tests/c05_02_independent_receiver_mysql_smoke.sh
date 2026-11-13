#!/usr/bin/env bash
# CRECISOL C05-02 ONLY. Local disposable MySQL8.4, separate .NET10 SMTP receiver, synthetic mail.
set -euo pipefail
IMAGE="mysql:8.4@sha256:a81d043930e51745522bd5c430961cf8286f0a23f0a9a2a6e0108f7ea71b0902"
NAME="crecisol-c0502-independent-receiver-$RANDOM-$"
DB="c05_02_disposable"
ROOT_PASSWORD="$(openssl rand -hex 24)"
INGEST_PASSWORD="$(openssl rand -hex 24)"
WORKER_PASSWORD="$(openssl rand -hex 24)"
SETUP_PASSWORD="$(openssl rand -hex 24)"
MASTER_HEX="$(openssl rand -hex 32)"
cleanup() { docker rm -f "$NAME" >/dev/null 2>&1 || true; }
trap cleanup EXIT
if [ -n "${GITHUB_ACTIONS:-}" ]; then
  for secret in "$ROOT_PASSWORD" "$INGEST_PASSWORD" "$WORKER_PASSWORD" "$SETUP_PASSWORD" "$MASTER_HEX"; do
    echo "::add-mask::$secret"
  done
fi
root_sql() {
  docker exec -e MYSQL_PWD="$ROOT_PASSWORD" "$NAME" mysql -uroot -N -B -D "$DB" -e "$1"
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
[ "$ready" -eq 1 ] || { echo 'FAIL C05-02 outbox isolated MySQL startup' >&2; exit 1; }
case "$(root_sql 'SELECT VERSION()')" in
  8.4.*) ;;
  *) echo 'FAIL C05-02 outbox: real MySQL8.4 required' >&2; exit 1 ;;
esac
for path in \
  database/tests/fixtures/c05_02_invitation_atomic_fixture.sql \
  database/tests/fixtures/c05_02_neutral_registration_fixture.sql \
  database/tests/fixtures/c05_02_mailbox_proof_atomic_fixture.sql \
  database/tests/fixtures/c05_02_delivery_outbox_atomic_fixture.sql; do
  docker exec -e MYSQL_PWD="$ROOT_PASSWORD" -i "$NAME" \
    mysql -uroot -D "$DB" < "$path"
done
root_sql "CREATE USER 'c05_02_outbox_ingest'@'%' IDENTIFIED BY '$INGEST_PASSWORD';
 CREATE USER 'c05_02_outbox_worker'@'%' IDENTIFIED BY '$WORKER_PASSWORD';
 CREATE USER 'c05_02_outbox_setup'@'%' IDENTIFIED BY '$SETUP_PASSWORD';
 GRANT EXECUTE ON PROCEDURE \`$DB\`.c05_02_record_registration_intent TO 'c05_02_outbox_ingest'@'%';
 GRANT SELECT ON \`$DB\`.c05_02_neutral_intents TO 'c05_02_outbox_worker'@'%';
 GRANT EXECUTE ON PROCEDURE \`$DB\`.c05_02_mailbox_proof_verify TO 'c05_02_outbox_worker'@'%';
 GRANT EXECUTE ON PROCEDURE \`$DB\`.c05_02_outbox_issue TO 'c05_02_outbox_worker'@'%';
 GRANT EXECUTE ON PROCEDURE \`$DB\`.c05_02_outbox_claim TO 'c05_02_outbox_worker'@'%';
 GRANT EXECUTE ON PROCEDURE \`$DB\`.c05_02_outbox_can_dispatch TO 'c05_02_outbox_worker'@'%';
 GRANT EXECUTE ON PROCEDURE \`$DB\`.c05_02_outbox_mark_sent TO 'c05_02_outbox_worker'@'%';
 GRANT EXECUTE ON PROCEDURE \`$DB\`.c05_02_outbox_mark_failed TO 'c05_02_outbox_worker'@'%';
 GRANT SELECT ON \`$DB\`.* TO 'c05_02_outbox_setup'@'%';
 GRANT INSERT,UPDATE ON \`$DB\`.c05_02_tenants TO 'c05_02_outbox_setup'@'%';
 GRANT UPDATE ON \`$DB\`.c05_02_delivery_outbox TO 'c05_02_outbox_setup'@'%';"
mapping="$(docker port "$NAME" '3306/tcp' | head -n 1)"
case "$mapping" in
  127.0.0.1:[0-9]*) port="${mapping##*:}" ;;
  *) echo 'FAIL C05-02 outbox: non-loopback port' >&2; exit 1 ;;
esac
case "$port" in *[!0-9]*|"") echo 'FAIL C05-02 outbox port' >&2; exit 1 ;; esac
export CRECISOL_C05_02_OUTBOX_SETUP_DSN="Server=127.0.0.1;Port=$port;Database=$DB;User ID=c05_02_outbox_setup;Password=$SETUP_PASSWORD;Pooling=false"
export CRECISOL_C05_02_OUTBOX_INGEST_DSN="Server=127.0.0.1;Port=$port;Database=$DB;User ID=c05_02_outbox_ingest;Password=$INGEST_PASSWORD;Pooling=false"
export CRECISOL_C05_02_OUTBOX_WORKER_DSN="Server=127.0.0.1;Port=$port;Database=$DB;User ID=c05_02_outbox_worker;Password=$WORKER_PASSWORD;Pooling=false"
export CRECISOL_C05_02_OUTBOX_MASTER_HEX="$MASTER_HEX"
dotnet run --no-build -c Release \
  --project backend/tests/Crecisol.IndependentMailbox.MySqlTests/Crecisol.IndependentMailbox.MySqlTests.csproj \
  -- --integration
echo 'PASS C05-02 real loopback SMTP receiver plus disposable MySQL8.4; NOT external inbox or grant'
