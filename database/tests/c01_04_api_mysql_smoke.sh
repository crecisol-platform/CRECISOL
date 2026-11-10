#!/usr/bin/env bash
# C01-04 real API + MySQL 8.4 on one GitHub runner. No network, no VPS, no production.
set -euo pipefail

CONTAINER="crecisol-c01-04-$RANDOM"
DB="crecisol_c01_04_test"
SOCKET_DIR="$(mktemp -d)"
chmod 777 "$SOCKET_DIR"
API_PID=""
ZERO="00000000000000000000000000000000"
TENANT="22222222222222222222222222222222"

cleanup() {
  if [ -n "$API_PID" ]; then kill "$API_PID" 2>/dev/null || true; wait "$API_PID" 2>/dev/null || true; fi
  # MySQL entrypoint may chown its bind-mounted socket directory.
  # Restore runner ownership while container is still available.
  docker exec --user root "$CONTAINER" chown -R "$(id -u):$(id -g)" \
    /var/run/mysqld >/dev/null 2>&1 || true
  docker rm -f "$CONTAINER" >/dev/null 2>&1 || true
  rm -rf -- "$SOCKET_DIR"
}
trap cleanup EXIT

root_sql() {
  docker exec "$CONTAINER" mysql -u root --batch --skip-column-names -D "$DB" -e "$1"
}

docker run --rm -d --network none --name "$CONTAINER" \
  -v "$SOCKET_DIR:/var/run/mysqld" \
  -e MYSQL_ALLOW_EMPTY_PASSWORD=yes -e MYSQL_DATABASE="$DB" \
  mysql:8.4@sha256:a81d043930e51745522bd5c430961cf8286f0a23f0a9a2a6e0108f7ea71b0902 >/dev/null

ready=0
for i in $(seq 1 90); do
  if root_sql "SELECT 1" >/dev/null 2>&1; then ready=1; break; fi
  sleep 1
done
if [ "$ready" -ne 1 ]; then echo "FAIL MySQL startup" >&2; exit 1; fi
version="$(root_sql 'SELECT VERSION()')"
case "$version" in 8.4.*) echo "C01-04 isolated real MySQL $version" ;;
  *) echo "FAIL expected MySQL 8.4, found $version" >&2; exit 1 ;;
esac
[ -S "$SOCKET_DIR/mysqld.sock" ] || { echo "FAIL local-only socket missing" >&2; exit 1; }

# Same reviewed DDL as C01-03. Seed ONLY artificial platform/tenant records.
docker exec -i "$CONTAINER" mysql -u root -D "$DB" < database/c01_03_brand_schema.sql
root_sql "INSERT INTO brand_scopes (scope_kind,scope_id)
 VALUES (1,X'$ZERO'),(2,X'$TENANT')"
root_sql "INSERT INTO brand_revisions
 (scope_kind,scope_id,revision,publication_status,name,slogan,descriptor,
  primary_color,accent_color,logo_url,published_at_utc)
 VALUES
 (1,X'$ZERO',1,1,'Unpublished prototype','Draft slogan','Draft descriptor',
  '#112233','#445566','/assets/draft.png',NULL)"
root_sql "INSERT INTO brand_revisions
 (scope_kind,scope_id,revision,publication_status,name,slogan,descriptor,
  primary_color,accent_color,logo_url,website_url,favicon_url,typography_token,
  icon_set_token,seo_title,seo_description,copyright_notice,published_at_utc)
 VALUES
 (1,X'$ZERO',2,2,'CRECISOL','Tu negocio, a tu manera.',
  'Comercio y servicios','#062E65','#078FE8',
  '/assets/brand.png',NULL,'/assets/favicon.webp','system','outline',
  'CRECISOL institucional','Identidad institucional publicada.',NULL,UTC_TIMESTAMP(6))"
root_sql "INSERT INTO brand_revisions
 (scope_kind,scope_id,revision,publication_status,name,slogan,descriptor,
  primary_color,accent_color,logo_url,published_at_utc)
 VALUES
 (2,X'$TENANT',1,2,'PRIVATE_TENANT_B','Private tenant slogan',
  'Private tenant descriptor','#112233','#445566','/assets/tenant.png',UTC_TIMESTAMP(6))"
root_sql "INSERT INTO brand_publications(scope_kind,scope_id,published_revision)
 VALUES (1,X'$ZERO',2),(2,X'$TENANT',1)"

# Runtime-only synthetic password, no secrets in repo or logs.
READER_PASSWORD="$(openssl rand -hex 18)"
docker exec -i "$CONTAINER" mysql -u root -D "$DB" >/dev/null <<SQL
CREATE USER 'brand_reader'@'localhost' IDENTIFIED BY '$READER_PASSWORD';
GRANT SELECT ON \`$DB\`.\`brand_publications\` TO 'brand_reader'@'localhost';
GRANT SELECT ON \`$DB\`.\`brand_revisions\` TO 'brand_reader'@'localhost';
SQL

docker exec -e MYSQL_PWD="$READER_PASSWORD" "$CONTAINER" \
  mysql -u brand_reader -D "$DB" -N -s -e "SELECT COUNT(*) FROM brand_publications" >/dev/null
if docker exec -e MYSQL_PWD="$READER_PASSWORD" "$CONTAINER" \
   mysql -u brand_reader -D "$DB" \
   -e "UPDATE brand_revisions SET name='illegal' WHERE scope_kind=1" >/dev/null 2>&1; then
  echo "FAIL: read-only SQL user may modify revision" >&2
  exit 1
fi
echo "PASS MySQL read-only SELECT grant and denied UPDATE"

# C03-08 opt-in profile: real disposable Unix socket, never production DSN.
if [ "${CRECISOL_C03_08_LOCAL_PROFILE_SMOKE:-0}" = "1" ]; then
  export CRECISOL_C03_08_PROFILE="ci"
  export CRECISOL_C03_08_MAX_WORKER_BATCH="8"
  export CRECISOL_C03_08_ALLOW_EXTERNAL_NETWORK="false"
  export CRECISOL_C03_08_TEST_SOCKET="$SOCKET_DIR/mysqld.sock"
fi

export CRECISOL_C01_ALLOWED_HOSTS="127.0.0.1"
export CRECISOL_C01_READONLY_DSN="Server=$SOCKET_DIR/mysqld.sock;Connection Protocol=Unix;User ID=brand_reader;Password=$READER_PASSWORD;Database=$DB;Pooling=True;"
export ASPNETCORE_ENVIRONMENT=Production
export ASPNETCORE_URLS="http://127.0.0.1:5184"

start_api() {
  dotnet run --no-build --no-launch-profile -c Release \
    --project backend/src/Crecisol.Api/Crecisol.Api.csproj >/dev/null 2>&1 &
  API_PID=$!
}
wait_http() {
  local port="$1" wanted="$2" response ready_http=0
  for i in $(seq 1 40); do
    response="$(curl --silent --max-time 1 --output /dev/null --write-out '%{http_code}' "http://127.0.0.1:$port/api/v1/public/branding/platform" || true)"
    if [ "$response" = "$wanted" ]; then ready_http=1; break; fi
    sleep 1
  done
  if [ "$ready_http" -ne 1 ]; then echo "FAIL API startup/status at local port $port" >&2; exit 1; fi
}
stop_api() {
  if [ -n "$API_PID" ]; then kill "$API_PID" 2>/dev/null || true; wait "$API_PID" 2>/dev/null || true; API_PID=""; fi
}
start_api
wait_http 5184 200
python3 qa/c01_04_http_smoke.py baseline 5184
if [ "${CRECISOL_C03_05_CONTRACT_SMOKE:-0}" = "1" ]; then python3 qa/c03_05_http_smoke.py baseline 5184; fi

# Platform publication absent must NEVER fall back to tenant, draft or cached brand.
root_sql "DELETE FROM brand_publications WHERE scope_kind=1 AND scope_id=X'$ZERO'"
python3 qa/c01_04_http_smoke.py absent 5184
if [ "${CRECISOL_C03_05_CONTRACT_SMOKE:-0}" = "1" ]; then python3 qa/c03_05_http_smoke.py absent 5184; fi
root_sql "INSERT INTO brand_publications(scope_kind,scope_id,published_revision) VALUES (1,X'$ZERO',2)"

# MySQL CHECK allows harmless-length HTML but DOMAIN must reject it.
root_sql "UPDATE brand_revisions SET name='<img src=x onerror=alert(1)>'
 WHERE scope_kind=1 AND scope_id=X'$ZERO' AND revision=2"
python3 qa/c01_04_http_smoke.py invalid 5184
if [ "${CRECISOL_C03_05_CONTRACT_SMOKE:-0}" = "1" ]; then python3 qa/c03_05_http_smoke.py invalid 5184; fi
root_sql "UPDATE brand_revisions SET name='CRECISOL'
 WHERE scope_kind=1 AND scope_id=X'$ZERO' AND revision=2"
python3 qa/c01_04_http_smoke.py baseline 5184
stop_api

# No DB config = 503, never static demo / accidental tenant response.
unset CRECISOL_C01_READONLY_DSN
export ASPNETCORE_URLS="http://127.0.0.1:5185"
start_api
wait_http 5185 503
python3 qa/c01_04_http_smoke.py unconfigured 5185
if [ "${CRECISOL_C03_05_CONTRACT_SMOKE:-0}" = "1" ]; then python3 qa/c03_05_http_smoke.py unconfigured 5185; fi
stop_api

echo "C01-04 PASS: real .NET 10 API + MySQL $version + read-only grants, \
published Platform only, tenant/draft isolation, malicious HTTP, bad data, no DB config."
echo "No administrative auth, publication, real hosts, user memberships, production, or VPS tested."
