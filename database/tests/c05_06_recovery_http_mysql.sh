#!/usr/bin/env bash
# C05-06 strictly disposable MySQL8.4 and ASP.NET10 loopback HTTP recovery.
# No real SMTP, external HTTP, production endpoints or persistent users.
set -euo pipefail
IMAGE="mysql:8.4@sha256:a81d043930e51745522bd5c430961cf8286f0a23f0a9a2a6e0108f7ea71b0902"
CONTAINER="crecisol-c05-06-$RANDOM-$$"
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
[ "$ready" -eq 1 ] || { echo "FAIL C05-06 isolated MySQL startup" >&2; exit 1; }
case "$(root_sql 'SELECT VERSION()')" in
  8.4.*) ;; *) echo "FAIL C05-06 requires MySQL 8.4" >&2; exit 1;;
esac
docker exec -e MYSQL_PWD="$ROOT_PASSWORD" -i "$CONTAINER" \
  mysql -uroot -D "$DATABASE" < database/tests/fixtures/c05_05_disposable_sessions.sql
docker exec -e MYSQL_PWD="$ROOT_PASSWORD" -i "$CONTAINER" \
  mysql -uroot -D "$DATABASE" < database/tests/fixtures/c05_06_disposable_recovery.sql

same() {
  local label="$1" want="$2" actual="$3"
  [ "$want" = "$actual" ] || {
    echo "FAIL C05-06 SQL $label expected=$want actual=$actual" >&2; exit 1;
  }
  echo "PASS C05-06 SQL $label"
}
same "five InnoDB tables for sessions + recovery" 5 "$(root_sql "SELECT COUNT(*)
 FROM information_schema.tables WHERE table_schema=DATABASE()
 AND table_name LIKE 'c05_0%' AND engine='InnoDB'")"
same "six stored procedures for sessions + reset" 6 "$(root_sql "SELECT COUNT(*)
 FROM information_schema.routines WHERE routine_schema=DATABASE()
 AND routine_name LIKE 'c05_0%' AND routine_type='PROCEDURE'")"
same "three security revision triggers" 3 "$(root_sql "SELECT COUNT(*)
 FROM information_schema.triggers WHERE trigger_schema=DATABASE()
 AND trigger_name LIKE 'c05_06_%'")"
same "two tenant-scoped recovery FK constraints" 2 "$(root_sql "SELECT COUNT(*)
 FROM information_schema.referential_constraints WHERE constraint_schema=DATABASE()
 AND constraint_name LIKE 'fk_c05_06_%'
 AND update_rule='RESTRICT' AND delete_rule='RESTRICT'")"
same "no plaintext email/token/password columns in challenges or mailboxes" 0 "$(root_sql "SELECT COUNT(*)
 FROM information_schema.columns WHERE table_schema=DATABASE()
 AND table_name IN ('c05_06_challenges','c05_06_mailboxes')
 AND (column_name LIKE '%token_raw%' OR column_name LIKE '%email%'
 OR column_name LIKE '%password%' OR column_name LIKE '%secret%'
 OR column_name LIKE '%cookie%' OR column_name LIKE '%jwt%')")"

root_sql "CREATE USER 'c05_06_runner'@'%' IDENTIFIED BY '$RUNNER_PASSWORD';
CREATE USER 'c05_06_fixture'@'%' IDENTIFIED BY '$SETUP_PASSWORD';
GRANT EXECUTE ON PROCEDURE \`$DATABASE\`.c05_06_issue_once TO 'c05_06_runner'@'%';
GRANT EXECUTE ON PROCEDURE \`$DATABASE\`.c05_06_consume_once TO 'c05_06_runner'@'%';
GRANT EXECUTE ON PROCEDURE \`$DATABASE\`.c05_05_is_current TO 'c05_06_runner'@'%';
GRANT SELECT, INSERT, UPDATE, DELETE ON \`$DATABASE\`.* TO 'c05_06_fixture'@'%';"
mapped="$(docker port "$CONTAINER" '3306/tcp' | head -n 1)"
case "$mapped" in
  127.0.0.1:[0-9]*) port="$(printf '%s' "$mapped" | cut -d: -f2)";;
  *) echo 'FAIL C05-06 bound to non-loopback MySQL' >&2; exit 1;;
esac
case "$port" in ""|*[!0-9]*) echo "FAIL invalid loopback port" >&2; exit 1;; esac
export CRECISOL_C05_06_RUNNER_DSN="Server=127.0.0.1;Port=$port;Database=$DATABASE;User ID=c05_06_runner;Password=$RUNNER_PASSWORD;SslMode=Preferred;Pooling=false"
export CRECISOL_C05_06_SETUP_DSN="Server=127.0.0.1;Port=$port;Database=$DATABASE;User ID=c05_06_fixture;Password=$SETUP_PASSWORD;SslMode=Preferred;Pooling=false"

dotnet run --no-build -c Release \
  --project backend/tests/Crecisol.PasswordRecovery.MySqlTests/Crecisol.PasswordRecovery.MySqlTests.csproj \
  -- --integration
echo 'PASS C05-06 REAL .NET10 ASP.NET HTTP + MySQL8.4 disposable recovery; NEVER production'
