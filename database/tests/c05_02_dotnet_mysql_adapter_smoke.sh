#!/usr/bin/env bash
# C05-02: REAL .NET10 MySqlConnector to MySQL8.4 on 127.0.0.1 only.
# No external service or deployment. Entire database and users are disposable.
set -euo pipefail
IMAGE="mysql:8.4@sha256:a81d043930e51745522bd5c430961cf8286f0a23f0a9a2a6e0108f7ea71b0902"
NAME="crecisol-c05-02-adapter-$RANDOM-$$"
DATABASE="c05_02_disposable"
ROOT_PASSWORD="$(openssl rand -hex 24)"
ADAPTER_PASSWORD="$(openssl rand -hex 24)"
SETUP_PASSWORD="$(openssl rand -hex 24)"
cleanup() { docker rm -f "$NAME" >/dev/null 2>&1 || true; }
trap cleanup EXIT
if [ -n "${GITHUB_ACTIONS:-}" ]; then
  for password in "$ROOT_PASSWORD" "$ADAPTER_PASSWORD" "$SETUP_PASSWORD"; do
    echo "::add-mask::$password"
  done
fi
root_sql() {
  docker exec -e MYSQL_PWD="$ROOT_PASSWORD" "$NAME" \
    mysql -uroot -N -B -D "$DATABASE" -e "$1"
}
docker run --rm -d --name "$NAME" --network bridge -p '127.0.0.1::3306' \
  -e MYSQL_ROOT_PASSWORD="$ROOT_PASSWORD" \
  -e MYSQL_DATABASE="$DATABASE" \
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
[ "$ready" -eq 1 ] || { echo "FAIL C05-02 integration: isolated MySQL startup" >&2; exit 1; }
case "$(root_sql 'SELECT VERSION()')" in
  8.4.*) ;; *) echo "FAIL C05-02 requires actual MySQL 8.4" >&2; exit 1 ;;
esac
# No privileged credentials are retained in Git, logs or API.
docker exec -e MYSQL_PWD="$ROOT_PASSWORD" -i "$NAME" \
  mysql -uroot -D "$DATABASE" \
  < database/tests/fixtures/c05_02_invitation_atomic_fixture.sql

root_sql "CREATE USER 'c05_02_adapter'@'%' IDENTIFIED BY '$ADAPTER_PASSWORD';
 CREATE USER 'c05_02_fixture_setup'@'%' IDENTIFIED BY '$SETUP_PASSWORD';
 GRANT SELECT ON \`$DATABASE\`.c05_02_tenants TO 'c05_02_adapter'@'%';
 GRANT SELECT ON \`$DATABASE\`.c05_02_issuers TO 'c05_02_adapter'@'%';
 GRANT SELECT ON \`$DATABASE\`.c05_02_recipients TO 'c05_02_adapter'@'%';
 GRANT SELECT, INSERT ON \`$DATABASE\`.c05_02_invitations TO 'c05_02_adapter'@'%';
 GRANT EXECUTE ON PROCEDURE \`$DATABASE\`.c05_02_claim_once TO 'c05_02_adapter'@'%';
 GRANT SELECT, INSERT, UPDATE, DELETE ON \`$DATABASE\`.* TO 'c05_02_fixture_setup'@'%';"

port_mapping="$(docker port "$NAME" '3306/tcp' | head -n 1)"
case "$port_mapping" in
  127.0.0.1:[0-9]*) host_port="${port_mapping##*:}" ;;
  *) echo "FAIL C05-02 test port not bound only to loopback" >&2; exit 1 ;;
esac
case "$host_port" in *[!0-9]*|"") echo "FAIL invalid local MySQL port" >&2; exit 1 ;; esac
export CRECISOL_C05_02_TEST_DSN="Server=127.0.0.1;Port=$host_port;Database=$DATABASE;User ID=c05_02_adapter;Password=$ADAPTER_PASSWORD;SslMode=Preferred;Pooling=false"
export CRECISOL_C05_02_TEST_ADMIN_DSN="Server=127.0.0.1;Port=$host_port;Database=$DATABASE;User ID=c05_02_fixture_setup;Password=$SETUP_PASSWORD;SslMode=Preferred;Pooling=false"

dotnet run --no-build -c Release \
  --project backend/tests/Crecisol.InvitationSecurity.MySqlIntegrationTests/Crecisol.InvitationSecurity.MySqlIntegrationTests.csproj \
  -- --integration
echo "PASS C05-02 .NET10 MySqlConnector -> actual MySQL8.4 isolated adapter; no production DB, email or API"
