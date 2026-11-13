#!/usr/bin/env bash
# CRECISOL C05-02 ONLY. Real MySQL 8.4, localhost disposable issuer-scope tests.
set -euo pipefail
IMAGE="mysql:8.4@sha256:a81d043930e51745522bd5c430961cf8286f0a23f0a9a2a6e0108f7ea71b0902"
NAME="crecisol-c0502-signed-$(openssl rand -hex 8)"
DB="c05_02_disposable"
ROOT_PASSWORD="$(openssl rand -hex 24)"
ADMIN_PASSWORD="$(openssl rand -hex 24)"
WORKER_PASSWORD="$(openssl rand -hex 24)"
SIGNING_MASTER_HEX="$(openssl rand -hex 32)"
cleanup() { docker rm -f "$NAME" >/dev/null 2>&1 || true; }
trap cleanup EXIT
if [ -n "${GITHUB_ACTIONS:-}" ]; then
  for secret in "$ROOT_PASSWORD" "$ADMIN_PASSWORD" "$WORKER_PASSWORD" "$SIGNING_MASTER_HEX"; do
    echo "::add-mask::$secret"
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
[ "$ready" -eq 1 ] || { echo 'FAIL isolated issuer MySQL startup' >&2; exit 1; }
case "$(root_sql 'SELECT VERSION()')" in
  8.4.*) ;;
  *) echo 'FAIL issuer: real MySQL 8.4 required' >&2; exit 1 ;;
esac
for fixture in \
  database/tests/fixtures/c05_02_invitation_atomic_fixture.sql \
  database/tests/fixtures/c05_02_issuer_scope_atomic_fixture.sql \
  database/tests/fixtures/c05_02_issuer_bound_claim_fixture.sql \
  database/tests/fixtures/c05_02_signed_recipient_claim_fixture.sql; do
  docker exec -e MYSQL_PWD="$ROOT_PASSWORD" -i "$NAME" \
    mysql -uroot -D "$DB" < "$fixture"
done
root_sql "CREATE USER 'issuer_scope_setup'@'%' IDENTIFIED BY '$ADMIN_PASSWORD';
 CREATE USER 'issuer_scope_worker'@'%' IDENTIFIED BY '$WORKER_PASSWORD';
 GRANT SELECT,INSERT,UPDATE ON \`$DB\`.* TO 'issuer_scope_setup'@'%';
 GRANT EXECUTE ON PROCEDURE \`$DB\`.c05_02_claim_signed_once TO 'issuer_scope_worker'@'%';"
mapping="$(docker port "$NAME" '3306/tcp' | head -n 1)"
case "$mapping" in
  127.0.0.1:[0-9]*) port="${mapping##*:}" ;;
  *) echo 'FAIL issuer: Docker port not restricted to loopback' >&2; exit 1 ;;
esac
case "$port" in *[!0-9]*|"") echo 'FAIL issuer: invalid port' >&2; exit 1 ;; esac
export CRECISOL_C05_02_SIGNED_ADMIN_DSN="Server=127.0.0.1;Port=$port;Database=$DB;User ID=issuer_scope_setup;Password=$ADMIN_PASSWORD;Pooling=false"
export CRECISOL_C05_02_SIGNED_WORKER_DSN="Server=127.0.0.1;Port=$port;Database=$DB;User ID=issuer_scope_worker;Password=$WORKER_PASSWORD;Pooling=false"
export CRECISOL_C05_02_SIGNED_MASTER_HEX="$SIGNING_MASTER_HEX"
dotnet run --no-build -c Release \
  --project backend/tests/Crecisol.SignedRecipient.MySqlTests/Crecisol.SignedRecipient.MySqlTests.csproj \
  -- --integration
echo 'PASS C05-02 signed recipient assertion MySQL8.4 isolated; no real email/login'
