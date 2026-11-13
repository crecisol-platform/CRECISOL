#!/usr/bin/env bash
# CRECISOL C05-05: isolated actual MySQL8.4 and .NET10 loopback HTTP.
# No production API, no real accounts or reusable secrets.
set -euo pipefail
IMAGE="mysql:8.4@sha256:a81d043930e51745522bd5c430961cf8286f0a23f0a9a2a6e0108f7ea71b0902"
CONTAINER="crecisol-c05-05-$RANDOM-$$"
DATABASE="c05_05_disposable"
ROOT_PASSWORD="$(openssl rand -hex 24)"
RUNNER_PASSWORD="$(openssl rand -hex 24)"
SETUP_PASSWORD="$(openssl rand -hex 24)"
cleanup() { docker rm -f "$CONTAINER" >/dev/null 2>&1 || true; }
trap cleanup EXIT
if [ -n "$(printenv GITHUB_ACTIONS || true)" ]; then
  for value in "$ROOT_PASSWORD" "$RUNNER_PASSWORD" "$SETUP_PASSWORD"; do
    echo "::add-mask::$value"
  done
fi
root_sql() {
  docker exec -e MYSQL_PWD="$ROOT_PASSWORD" "$CONTAINER" \
    mysql -uroot -N -B -D "$DATABASE" -e "$1"
}
docker run --rm -d --name "$CONTAINER" --network bridge -p '127.0.0.1::3306' \
  -e MYSQL_ROOT_PASSWORD="$ROOT_PASSWORD" \
  -e MYSQL_DATABASE="$DATABASE" "$IMAGE" >/dev/null
booted=0
ready=0
for _ in $(seq 1 120); do
  if [ "$booted" -eq 0 ] &&
     docker logs "$CONTAINER" 2>&1 | grep -Fq 'MySQL init process done. Ready for start up.'; then
    booted=1
  fi
  if [ "$booted" -eq 1 ] && root_sql 'SELECT 1' >/dev/null 2>&1; then
    sleep 1
    if root_sql 'SELECT 1' >/dev/null 2>&1; then ready=1; break; fi
  fi
  sleep 1
done
[ "$ready" -eq 1 ] || { echo "FAIL C05-05 disposable MySQL startup" >&2; exit 1; }
case "$(root_sql 'SELECT VERSION()')" in
  8.4.*) ;; *) echo 'FAIL C05-05 requires actual MySQL8.4' >&2; exit 1;;
esac
docker exec -e MYSQL_PWD="$ROOT_PASSWORD" -i "$CONTAINER" \
  mysql -uroot -D "$DATABASE" < database/tests/fixtures/c05_05_disposable_sessions.sql
same() {
  local label="$1" want="$2" actual="$3"
  [ "$want" = "$actual" ] || {
    echo "FAIL C05-05 SQL $label expected=$want actual=$actual" >&2; exit 1;
  }
  echo "PASS C05-05 SQL $label"
}
same "two InnoDB tables" 2 "$(root_sql "SELECT COUNT(*)
 FROM information_schema.tables
 WHERE table_schema=DATABASE() AND table_name LIKE 'c05_05_%' AND engine='InnoDB'")"
same "exactly four protected session procedures" 4 "$(root_sql "SELECT COUNT(*)
 FROM information_schema.routines
 WHERE routine_schema=DATABASE() AND routine_name LIKE 'c05_05_%'")"
same "scoped subject foreign key" 1 "$(root_sql "SELECT COUNT(*)
 FROM information_schema.referential_constraints
 WHERE constraint_schema=DATABASE() AND constraint_name='fk_c05_05_subject'
 AND update_rule='RESTRICT' AND delete_rule='RESTRICT'")"
same "no password/secret/raw token columns" 0 "$(root_sql "SELECT COUNT(*)
 FROM information_schema.columns WHERE table_schema=DATABASE()
 AND table_name='c05_05_sessions'
 AND (column_name LIKE '%token_raw%' OR column_name LIKE '%email%'
 OR column_name LIKE '%cookie%' OR column_name LIKE '%password%'
 OR column_name LIKE '%secret%' OR column_name LIKE '%jwt%')")"
root_sql "CREATE USER 'c05_05_runner'@'%' IDENTIFIED BY '$RUNNER_PASSWORD';
CREATE USER 'c05_05_fixture_setup'@'%' IDENTIFIED BY '$SETUP_PASSWORD';
GRANT EXECUTE ON PROCEDURE \`$DATABASE\`.c05_05_issue_once TO 'c05_05_runner'@'%';
GRANT EXECUTE ON PROCEDURE \`$DATABASE\`.c05_05_is_current TO 'c05_05_runner'@'%';
GRANT EXECUTE ON PROCEDURE \`$DATABASE\`.c05_05_rotate_once TO 'c05_05_runner'@'%';
GRANT EXECUTE ON PROCEDURE \`$DATABASE\`.c05_05_logout_once TO 'c05_05_runner'@'%';
GRANT SELECT, INSERT, UPDATE, DELETE ON \`$DATABASE\`.* TO 'c05_05_fixture_setup'@'%';"
port_mapping="$(docker port "$CONTAINER" '3306/tcp' | head -n 1)"
case "$port_mapping" in
  127.0.0.1:[0-9]*) port="$(printf '%s' "$port_mapping" | cut -d: -f2)";;
  *) echo "FAIL C05-05 MySQL not bound to host loopback" >&2; exit 1;;
esac
case "$port" in ""|*[!0-9]*) echo "FAIL invalid MySQL test port" >&2; exit 1;; esac
export CRECISOL_C05_05_TEST_DSN="Server=127.0.0.1;Port=$port;Database=$DATABASE;User ID=c05_05_runner;Password=$RUNNER_PASSWORD;SslMode=Preferred;Pooling=false"
export CRECISOL_C05_05_SETUP_DSN="Server=127.0.0.1;Port=$port;Database=$DATABASE;User ID=c05_05_fixture_setup;Password=$SETUP_PASSWORD;SslMode=Preferred;Pooling=false"
dotnet run --no-build -c Release \
  --project backend/tests/Crecisol.SessionRevocation.MySqlTests/Crecisol.SessionRevocation.MySqlTests.csproj \
  -- --integration
echo "PASS C05-05 REAL localhost .NET10 HTTP / MySQL8.4 CAS; no product login, real sessions or production accounts"
