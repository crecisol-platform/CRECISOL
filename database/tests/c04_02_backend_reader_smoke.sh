#!/usr/bin/env bash
# C04-02: disposable MySQL8.4 read-only .NET adapter integration, NEVER production.
# Only a random port on runner loopback is mapped to support host .NET SDK.
set -euo pipefail
IMAGE="mysql:8.4@sha256:a81d043930e51745522bd5c430961cf8286f0a23f0a9a2a6e0108f7ea71b0902"
CONTAINER="crecisol-c04-02-reader-$RANDOM-$$"
DB="c04_reader_ci"
A="11111111111111111111111111111111"
B="22222222222222222222222222222222"
SHARED_BRANCH="33333333333333333333333333333333"
OTHER_BRANCH="88888888888888888888888888888888"
SHARED_RESOURCE="55555555555555555555555555555555"
ONLY_A="77777777777777777777777777777777"
OTHER_RESOURCE="66666666666666666666666666666666"
ROOT_PASSWORD="$(openssl rand -hex 24)"
READER_PASSWORD="$(openssl rand -hex 24)"
if [ -n "${GITHUB_ACTIONS:-}" ]; then
  printf '::add-mask::%s\n' "$ROOT_PASSWORD"
  printf '::add-mask::%s\n' "$READER_PASSWORD"
fi
cleanup() { docker rm -f "$CONTAINER" >/dev/null 2>&1 || true; }
trap cleanup EXIT
root_sql() {
  docker exec -e MYSQL_PWD="$ROOT_PASSWORD" "$CONTAINER" \
    mysql -uroot --batch --skip-column-names -D "$DB" -e "$1"
}
reader_sql() {
  docker exec -e MYSQL_PWD="$READER_PASSWORD" "$CONTAINER" \
    mysql -uc04_ci_reader --batch --skip-column-names -D "$DB" -e "$1"
}
expect_privilege_denial() {
  local label="$1" statement="$2" result
  if result=$(reader_sql "$statement" 2>&1); then
    echo "FAIL C04-02 reader permission: $label succeeded" >&2; exit 1
  fi
  case "$result" in
    *"ERROR 1142"*|*"ERROR 1143"*) echo "PASS read-only privilege rejected: $label" ;;
    *) echo "FAIL C04-02 unexpected privilege error ($label): $result" >&2; exit 1 ;;
  esac
}
docker run --rm -d --network bridge -p 127.0.0.1::3306 --name "$CONTAINER" \
  -e MYSQL_ROOT_PASSWORD="$ROOT_PASSWORD" -e MYSQL_DATABASE="$DB" "$IMAGE" >/dev/null
ready=0
init_done=0
for _ in $(seq 1 120); do
  if [ "$init_done" -eq 0 ] && docker logs "$CONTAINER" 2>&1 |
     grep -Fq 'MySQL init process done. Ready for start up.'; then
    init_done=1
  fi
  if [ "$init_done" -eq 1 ] && root_sql 'SELECT 1' >/dev/null 2>&1; then
    sleep 1
    if root_sql 'SELECT 1' >/dev/null 2>&1; then ready=1; break; fi
  fi
  sleep 1
done
if [ "$ready" -ne 1 ]; then echo 'FAIL C04-02 final MySQL daemon unavailable' >&2; exit 1; fi
case "$(root_sql 'SELECT VERSION()')" in 8.4.*) ;; *) echo 'FAIL MySQL 8.4 required' >&2; exit 1 ;; esac
docker exec -i -e MYSQL_PWD="$ROOT_PASSWORD" "$CONTAINER" mysql -uroot -D "$DB" \
  < database/c04_02_shared_schema.sql
root_sql "INSERT INTO c04_tenants(tenant_id,lifecycle_state) VALUES (X'$A',3),(X'$B',3)"
root_sql "INSERT INTO c04_branches(tenant_id,branch_id,display_label) VALUES
  (X'$A',X'$SHARED_BRANCH','A branch'),(X'$B',X'$SHARED_BRANCH','B branch'),
  (X'$A',X'$OTHER_BRANCH','A other branch')"
root_sql "INSERT INTO c04_resources(tenant_id,branch_id,resource_id,external_key) VALUES
  (X'$A',X'$SHARED_BRANCH',X'$SHARED_RESOURCE','a-shared'),
  (X'$B',X'$SHARED_BRANCH',X'$SHARED_RESOURCE','b-shared'),
  (X'$A',X'$SHARED_BRANCH',X'$ONLY_A','a-only'),
  (X'$A',X'$OTHER_BRANCH',X'$OTHER_RESOURCE','other-branch')"
root_sql "CREATE USER 'c04_ci_reader'@'%' IDENTIFIED BY '$READER_PASSWORD'"
root_sql "GRANT SELECT ON \`$DB\`.c04_resources TO 'c04_ci_reader'@'%'"
test "$(reader_sql "SELECT COUNT(*) FROM c04_resources WHERE tenant_id=X'$A'")" = 3
test "$(reader_sql "SELECT COUNT(*) FROM c04_resources WHERE tenant_id=X'$B'")" = 1
expect_privilege_denial "UPDATE" "UPDATE c04_resources SET external_key='unsafe'"
expect_privilege_denial "DELETE" "DELETE FROM c04_resources WHERE 1=1"
expect_privilege_denial "tenant catalogue enumeration" "SELECT COUNT(*) FROM c04_tenants"
expect_privilege_denial "DROP" "DROP TABLE c04_resources"
PORT_MAPPING="$(docker port "$CONTAINER" 3306/tcp)"
case "$PORT_MAPPING" in 127.0.0.1:*) ;; *) echo 'FAIL: port is not loopback-only' >&2; exit 1 ;; esac
PORT="${PORT_MAPPING##*:}"
case "$PORT" in ''|*[!0-9]*) echo 'FAIL: missing numeric loopback port' >&2; exit 1 ;; esac
export CRECISOL_C04_02_TEST_DSN="Server=127.0.0.1;Port=$PORT;Database=$DB;User ID=c04_ci_reader;Password=$READER_PASSWORD;SslMode=None;AllowPublicKeyRetrieval=True;Connection Timeout=3;"
dotnet run --no-build -c Release --project \
  backend/tests/Crecisol.SharedTenancy.ContractsTests/Crecisol.SharedTenancy.ContractsTests.csproj -- db
unset CRECISOL_C04_02_TEST_DSN
echo 'PASS C04-02 scoped .NET10/MySqlConnector on real MySQL8.4, loopback only and ephemeral read-only grants.'
echo 'LIMIT: test fake resolver is NOT real authentication/authorization; no API, router or deployment.'
