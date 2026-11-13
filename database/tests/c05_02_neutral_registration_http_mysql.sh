#!/usr/bin/env bash
# C05-02 ONLY: disposable loopback HTTP/Kestrel and MySQL8.4, no production.
set -euo pipefail
IMAGE="mysql:8.4@sha256:a81d043930e51745522bd5c430961cf8286f0a23f0a9a2a6e0108f7ea71b0902"
NAME="crecisol-neutral-c0502-$RANDOM-$$"
DB="c05_02_disposable"
ROOT_PASSWORD="$(openssl rand -hex 24)"
INGEST_PASSWORD="$(openssl rand -hex 24)"
SETUP_PASSWORD="$(openssl rand -hex 24)"
MASTER_HEX="$(openssl rand -hex 32)"
cleanup() { docker rm -f "$NAME" >/dev/null 2>&1 || true; }
trap cleanup EXIT
if [ -n "$(printenv GITHUB_ACTIONS || true)" ]; then
  for key in "$ROOT_PASSWORD" "$INGEST_PASSWORD" "$SETUP_PASSWORD" "$MASTER_HEX"; do
    echo "::add-mask::$key"
  done
fi
root_sql() {
  docker exec -e MYSQL_PWD="$ROOT_PASSWORD" "$NAME" \
    mysql -uroot -N -B -D "$DB" -e "$1"
}
docker run --rm -d --network bridge --name "$NAME" -p '127.0.0.1::3306' \
  -e MYSQL_ROOT_PASSWORD="$ROOT_PASSWORD" -e MYSQL_DATABASE="$DB" \
  "$IMAGE" >/dev/null
booted=0
ready=0
for _ in $(seq 1 120); do
  if [ "$booted" -eq 0 ] &&
     docker logs "$NAME" 2>&1 | grep -Fq 'MySQL init process done. Ready for start up.'; then
    booted=1
  fi
  if [ "$booted" -eq 1 ] && root_sql 'SELECT 1' >/dev/null 2>&1; then
    sleep 1
    if root_sql 'SELECT 1' >/dev/null 2>&1; then ready=1; break; fi
  fi
  sleep 1
done
[ "$ready" -eq 1 ] || { echo 'FAIL C05-02 isolated MySQL startup' >&2; exit 1; }
case "$(root_sql 'SELECT VERSION()')" in
  8.4.*) ;; *) echo 'FAIL C05-02: actual MySQL 8.4 required' >&2; exit 1 ;;
esac
docker exec -e MYSQL_PWD="$ROOT_PASSWORD" -i "$NAME" mysql -uroot -D "$DB" \
  < database/tests/fixtures/c05_02_invitation_atomic_fixture.sql
docker exec -e MYSQL_PWD="$ROOT_PASSWORD" -i "$NAME" mysql -uroot -D "$DB" \
  < database/tests/fixtures/c05_02_neutral_registration_fixture.sql

# Grant only EXECUTE to HTTP app, NEVER SELECT/INSERT/UPDATE/DELETE on PII.
root_sql "CREATE USER 'c05_02_neutral_ingest'@'%' IDENTIFIED BY '$INGEST_PASSWORD';
CREATE USER 'c05_02_neutral_setup'@'%' IDENTIFIED BY '$SETUP_PASSWORD';
GRANT EXECUTE ON PROCEDURE \`$DB\`.c05_02_record_registration_intent TO 'c05_02_neutral_ingest'@'%';
GRANT SELECT ON \`$DB\`.* TO 'c05_02_neutral_setup'@'%';
GRANT INSERT, UPDATE ON \`$DB\`.c05_02_tenants TO 'c05_02_neutral_setup'@'%';"
port_mapping="$(docker port "$NAME" '3306/tcp' | head -n 1)"
case "$port_mapping" in
  127.0.0.1:[0-9]*) host_port="$(printf '%s' "$port_mapping" | cut -d: -f2)" ;;
  *) echo 'FAIL MySQL exposed to non-loopback network' >&2; exit 1 ;;
esac
case "$host_port" in *[!0-9]*|"") echo 'FAIL invalid local port' >&2; exit 1 ;; esac
export CRECISOL_C05_02_NEUTRAL_TEST_DSN="Server=127.0.0.1;Port=$host_port;Database=$DB;User ID=c05_02_neutral_ingest;Password=$INGEST_PASSWORD;Pooling=false;SslMode=Preferred"
export CRECISOL_C05_02_NEUTRAL_ADMIN_DSN="Server=127.0.0.1;Port=$host_port;Database=$DB;User ID=c05_02_neutral_setup;Password=$SETUP_PASSWORD;Pooling=false;SslMode=Preferred"
export CRECISOL_C05_02_NEUTRAL_MASTER_HEX="$MASTER_HEX"

dotnet run --no-build -c Release \
  --project backend/tests/Crecisol.NeutralRegistration.IntegrationTests/Crecisol.NeutralRegistration.IntegrationTests.csproj \
  -- --integration
echo "PASS C05-02 REAL .NET10 loopback HTTP + MySQL8.4 neutral registration; NOT user registration/email delivery"
