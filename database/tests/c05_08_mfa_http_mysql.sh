#!/usr/bin/env bash
# C05-08 strict real MySQL8.4/.NET10 loopback HTTP-only MFA tests.
# NEVER a database migration, identity API, login, production job or deployment.
set -euo pipefail
IMAGE="mysql:8.4@sha256:a81d043930e51745522bd5c430961cf8286f0a23f0a9a2a6e0108f7ea71b0902"
CONTAINER="crecisol-c05-08-$RANDOM-$$"
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
      docker logs "$CONTAINER" 2>&1 |
      grep -Fq 'MySQL init process done. Ready for start up.'; then
    booted=1
  fi
  if [ "$booted" -eq 1 ] && root_sql 'SELECT 1' >/dev/null 2>&1; then
    sleep 1
    if root_sql 'SELECT 1' >/dev/null 2>&1; then ready=1; break; fi
  fi
  sleep 1
done
[ "$ready" -eq 1 ] || { echo 'FAIL C05-08 disposable MySQL startup' >&2; exit 1; }
case "$(root_sql 'SELECT VERSION()')" in
  8.4.*) ;; *) echo 'FAIL C05-08 requires MySQL8.4' >&2; exit 1;;
esac
for fixture in \
    database/tests/fixtures/c05_05_disposable_sessions.sql \
    database/tests/fixtures/c05_06_disposable_recovery.sql \
    database/tests/fixtures/c05_08_disposable_mfa.sql; do
  docker exec -e MYSQL_PWD="$ROOT_PASSWORD" -i "$CONTAINER" \
    mysql -uroot -D "$DATABASE" < "$fixture"
done
same() {
  local label="$1" expected="$2" actual="$3"
  [ "$expected" = "$actual" ] || {
    echo "FAIL C05-08 SQL $label expected=$expected actual=$actual" >&2; exit 1;
  }
  echo "PASS C05-08 SQL $label"
}
same "four own InnoDB tables" 4 "$(root_sql "SELECT COUNT(*)
 FROM information_schema.tables WHERE table_schema=DATABASE()
 AND table_name LIKE 'c05_08_%' AND engine='InnoDB'")"
same "five protected SQL routines" 5 "$(root_sql "SELECT COUNT(*)
 FROM information_schema.routines WHERE routine_schema=DATABASE()
 AND routine_name LIKE 'c05_08_%' AND routine_type='PROCEDURE'")"
same "two immutable audit triggers" 2 "$(root_sql "SELECT COUNT(*)
 FROM information_schema.triggers WHERE trigger_schema=DATABASE()
 AND trigger_name LIKE 'c05_08_%'")"
same "zero secret columns in MFA factor table" 0 "$(root_sql "SELECT COUNT(*)
 FROM information_schema.columns WHERE table_schema=DATABASE()
 AND table_name='c05_08_factors' AND
 (column_name LIKE '%seed%' OR column_name LIKE '%private%'
  OR column_name LIKE '%secret%' OR column_name LIKE '%email%')")"
root_sql "CREATE USER 'c05_08_runner'@'%' IDENTIFIED BY '$RUNNER_PASSWORD';
CREATE USER 'c05_08_fixture'@'%' IDENTIFIED BY '$SETUP_PASSWORD';
GRANT EXECUTE ON PROCEDURE \`$DATABASE\`.c05_08_open_enrollment TO 'c05_08_runner'@'%';
GRANT EXECUTE ON PROCEDURE \`$DATABASE\`.c05_08_open_stepup TO 'c05_08_runner'@'%';
GRANT EXECUTE ON PROCEDURE \`$DATABASE\`.c05_08_commit_proof TO 'c05_08_runner'@'%';
GRANT EXECUTE ON PROCEDURE \`$DATABASE\`.c05_08_open_replacement TO 'c05_08_runner'@'%';
GRANT EXECUTE ON PROCEDURE \`$DATABASE\`.c05_08_read_counter TO 'c05_08_runner'@'%';
GRANT EXECUTE ON PROCEDURE \`$DATABASE\`.c05_05_issue_once TO 'c05_08_runner'@'%';
GRANT EXECUTE ON PROCEDURE \`$DATABASE\`.c05_05_is_current TO 'c05_08_runner'@'%';
GRANT SELECT,INSERT,UPDATE,DELETE ON \`$DATABASE\`.* TO 'c05_08_fixture'@'%';"
mapped="$(docker port "$CONTAINER" '3306/tcp' | head -n 1)"
case "$mapped" in
  127.0.0.1:[0-9]*) port="$(printf '%s' "$mapped" | cut -d: -f2)";;
  *) echo 'FAIL C05-08 MySQL is not loopback only' >&2; exit 1;;
esac
case "$port" in ""|*[!0-9]*) echo 'FAIL invalid local fixture port' >&2; exit 1;; esac
export CRECISOL_C05_08_RUNNER_DSN="Server=127.0.0.1;Port=$port;Database=$DATABASE;User ID=c05_08_runner;Password=$RUNNER_PASSWORD;SslMode=Preferred;Pooling=false"
export CRECISOL_C05_08_SETUP_DSN="Server=127.0.0.1;Port=$port;Database=$DATABASE;User ID=c05_08_fixture;Password=$SETUP_PASSWORD;SslMode=Preferred;Pooling=false"
dotnet run --no-build -c Release \
  --project backend/tests/Crecisol.MfaRisk.MySqlTests/Crecisol.MfaRisk.MySqlTests.csproj \
  -- --integration
echo 'PASS C05-08 real local .NET10 ASP.NET loopback + disposable MySQL8.4; SYNTHETIC ONLY'
