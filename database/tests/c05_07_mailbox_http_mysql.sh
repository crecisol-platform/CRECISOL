#!/usr/bin/env bash
# C05-07 disposable MySQL8.4 and .NET10 HTTP sensitive-mailbox checks ONLY.
# No mail provider, customers, account API, deployment, VPS or billing.
set -euo pipefail
IMAGE="mysql:8.4@sha256:a81d043930e51745522bd5c430961cf8286f0a23f0a9a2a6e0108f7ea71b0902"
CONTAINER="crecisol-c05-07-$RANDOM-$$"
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
[ "$ready" -eq 1 ] || { echo 'FAIL C05-07 disposable MySQL startup' >&2; exit 1; }
case "$(root_sql 'SELECT VERSION()')" in
  8.4.*) ;; *) echo 'FAIL C05-07 requires MySQL8.4' >&2; exit 1;;
esac
for fixture in \
  database/tests/fixtures/c05_05_disposable_sessions.sql \
  database/tests/fixtures/c05_06_disposable_recovery.sql \
  database/tests/fixtures/c05_07_disposable_mailbox.sql; do
  docker exec -e MYSQL_PWD="$ROOT_PASSWORD" -i "$CONTAINER" \
    mysql -uroot -D "$DATABASE" < "$fixture"
done
same() {
  local label="$1" expected="$2" actual="$3"
  [ "$expected" = "$actual" ] || {
    echo "FAIL C05-07 SQL $label expected=$expected actual=$actual" >&2; exit 1;
  }
  echo "PASS C05-07 SQL $label"
}
same "8 InnoDB fixture tables" 8 "$(root_sql "SELECT COUNT(*)
  FROM information_schema.tables WHERE table_schema=DATABASE()
  AND table_name LIKE 'c05_0%' AND engine='InnoDB'")"
same "10 protected stored procedures" 10 "$(root_sql "SELECT COUNT(*)
  FROM information_schema.routines WHERE routine_schema=DATABASE()
  AND routine_name LIKE 'c05_0%' AND routine_type='PROCEDURE'")"
same "5 revision/append-only audit triggers" 5 "$(root_sql "SELECT COUNT(*)
  FROM information_schema.triggers WHERE trigger_schema=DATABASE()
  AND trigger_name LIKE 'c05_0%'")"
same "5 composite tenant/actor FK controls" 5 "$(root_sql "SELECT COUNT(*)
  FROM information_schema.referential_constraints
  WHERE constraint_schema=DATABASE() AND
   (constraint_name LIKE 'fk_c05_06_%' OR constraint_name LIKE 'fk_c05_07_%')")"
same "no audit email, password, plaintext token or digest columns" 0 "$(root_sql "SELECT COUNT(*)
  FROM information_schema.columns
  WHERE table_schema=DATABASE() AND table_name='c05_07_security_audit'
  AND (column_name LIKE '%mailbox%' OR column_name LIKE '%email%'
    OR column_name LIKE '%password%' OR column_name LIKE '%token%'
    OR column_name LIKE '%secret%' OR column_name LIKE '%digest%'
    OR column_name LIKE '%cookie%' OR column_name LIKE '%jwt%')")"

root_sql "CREATE USER 'c05_07_runner'@'%' IDENTIFIED BY '$RUNNER_PASSWORD';
CREATE USER 'c05_07_fixture'@'%' IDENTIFIED BY '$SETUP_PASSWORD';
GRANT EXECUTE ON PROCEDURE \`$DATABASE\`.c05_07_begin_verify TO 'c05_07_runner'@'%';
GRANT EXECUTE ON PROCEDURE \`$DATABASE\`.c05_07_confirm_verify TO 'c05_07_runner'@'%';
GRANT EXECUTE ON PROCEDURE \`$DATABASE\`.c05_07_begin_change TO 'c05_07_runner'@'%';
GRANT EXECUTE ON PROCEDURE \`$DATABASE\`.c05_07_confirm_change TO 'c05_07_runner'@'%';
GRANT EXECUTE ON PROCEDURE \`$DATABASE\`.c05_05_is_current TO 'c05_07_runner'@'%';
GRANT SELECT,INSERT,UPDATE,DELETE ON \`$DATABASE\`.* TO 'c05_07_fixture'@'%';"
mapped="$(docker port "$CONTAINER" '3306/tcp' | head -n 1)"
case "$mapped" in
  127.0.0.1:[0-9]*) port="$(printf '%s' "$mapped" | cut -d: -f2)";;
  *) echo 'FAIL C05-07 database exposed beyond loopback' >&2; exit 1;;
esac
case "$port" in ""|*[!0-9]*) echo 'FAIL C05-07 invalid test DB port' >&2; exit 1;; esac

export CRECISOL_C05_07_RUNNER_DSN="Server=127.0.0.1;Port=$port;Database=$DATABASE;User ID=c05_07_runner;Password=$RUNNER_PASSWORD;SslMode=Preferred;Pooling=false"
export CRECISOL_C05_07_SETUP_DSN="Server=127.0.0.1;Port=$port;Database=$DATABASE;User ID=c05_07_fixture;Password=$SETUP_PASSWORD;SslMode=Preferred;Pooling=false"

dotnet run --no-build -c Release \
  --project backend/tests/Crecisol.MailboxApproval.MySqlTests/Crecisol.MailboxApproval.MySqlTests.csproj \
  -- --integration

echo 'PASS C05-07 .NET10 real HTTP/MySQL8.4 verification + dual approval; TEST-ONLY'
