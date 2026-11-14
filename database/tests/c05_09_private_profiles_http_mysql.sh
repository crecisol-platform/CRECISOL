#!/usr/bin/env bash
# C05-09: PINNED disposable MySQL 8.4 + .NET 10 loopback HTTP self-profile.
# NO deployment, production DB, Cloudflare bucket, external API or real users.
set -euo pipefail
IMAGE="mysql:8.4@sha256:a81d043930e51745522bd5c430961cf8286f0a23f0a9a2a6e0108f7ea71b0902"
CONTAINER="crecisol-c05-09-$RANDOM-$$"
DATABASE="c05_05_disposable"
ROOT_PASSWORD="$(openssl rand -hex 24)"
RUNNER_PASSWORD="$(openssl rand -hex 24)"
SETUP_PASSWORD="$(openssl rand -hex 24)"
cleanup(){ docker rm -f "$CONTAINER" >/dev/null 2>&1 || true; }
trap cleanup EXIT
if [ "$(printenv GITHUB_ACTIONS || true)" = true ]; then
  for secret in "$ROOT_PASSWORD" "$RUNNER_PASSWORD" "$SETUP_PASSWORD"; do
    echo "::add-mask::$secret"
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
[ "$ready" -eq 1 ] || { echo "FAIL C05-09 local MySQL startup" >&2; exit 1; }
case "$(root_sql 'SELECT VERSION()')" in
  8.4.*) ;; *) echo 'FAIL C05-09 requires MySQL 8.4' >&2; exit 1;;
esac
for fixture in \
  database/tests/fixtures/c05_05_disposable_sessions.sql \
  database/tests/fixtures/c05_06_disposable_recovery.sql \
  database/tests/fixtures/c05_09_disposable_private_profiles.sql; do
  docker exec -e MYSQL_PWD="$ROOT_PASSWORD" -i "$CONTAINER" \
    mysql -uroot -D "$DATABASE" < "$fixture"
done
same() {
  local label="$1" expected="$2" actual="$3"
  [ "$expected" = "$actual" ] || {
    echo "FAIL C05-09 SQL $label expected=$expected actual=$actual" >&2;exit 1;
  }
  echo "PASS C05-09 SQL $label"
}
same "three private InnoDB tables" 3 "$(root_sql "SELECT COUNT(*) FROM information_schema.tables
WHERE table_schema=DATABASE() AND table_name LIKE 'c05_09_%' AND engine='InnoDB'")"
same "three protected procedures" 3 "$(root_sql "SELECT COUNT(*) FROM information_schema.routines
WHERE routine_schema=DATABASE() AND routine_name LIKE 'c05_09_%'
AND routine_type='PROCEDURE'")"
same "two immutable audit triggers" 2 "$(root_sql "SELECT COUNT(*) FROM information_schema.triggers
WHERE trigger_schema=DATABASE() AND trigger_name LIKE 'c05_09_%'")"
same "zero persistent object bytes or public URL columns" 0 "$(root_sql "SELECT COUNT(*)
FROM information_schema.columns WHERE table_schema=DATABASE() AND
table_name='c05_09_media_manifest' AND
(data_type IN ('blob','mediumblob','longblob','tinyblob') OR column_name LIKE '%url%')")"
root_sql "CREATE USER 'c05_09_runner'@'%' IDENTIFIED BY '$RUNNER_PASSWORD';
CREATE USER 'c05_09_fixture'@'%' IDENTIFIED BY '$SETUP_PASSWORD';
GRANT EXECUTE ON PROCEDURE \`$DATABASE\`.c05_09_read_self TO 'c05_09_runner'@'%';
GRANT EXECUTE ON PROCEDURE \`$DATABASE\`.c05_09_save_self TO 'c05_09_runner'@'%';
GRANT EXECUTE ON PROCEDURE \`$DATABASE\`.c05_09_erase_self TO 'c05_09_runner'@'%';
GRANT SELECT,INSERT,UPDATE,DELETE ON \`$DATABASE\`.* TO 'c05_09_fixture'@'%';"
mapped="$(docker port "$CONTAINER" '3306/tcp' | head -n1)"
case "$mapped" in
  127.0.0.1:[0-9]*) port="$(printf '%s' "$mapped" | cut -d: -f2)";;
  *) echo 'FAIL C05-09 DB not restricted to loopback' >&2;exit 1;;
esac
case "$port" in ""|*[!0-9]*) echo 'FAIL C05-09 invalid local DB port' >&2; exit 1;; esac
export CRECISOL_C05_09_RUNNER_DSN="Server=127.0.0.1;Port=$port;Database=$DATABASE;User ID=c05_09_runner;Password=$RUNNER_PASSWORD;SslMode=Preferred;Pooling=false"
export CRECISOL_C05_09_SETUP_DSN="Server=127.0.0.1;Port=$port;Database=$DATABASE;User ID=c05_09_fixture;Password=$SETUP_PASSWORD;SslMode=Preferred;Pooling=false"
dotnet run --no-build -c Release \
 --project backend/tests/Crecisol.PrivateProfiles.MySqlTests/Crecisol.PrivateProfiles.MySqlTests.csproj -- --integration
echo 'PASS C05-09 isolated HTTP ASP.NET10/MySQL8.4 ONLY; NO Cloudflare or production'
