#!/usr/bin/env bash
# C01-07: real MySQL 8.4 + ASP.NET Core 10 + Angular 22/Chromium regression.
# Every service and credential is disposable and restricted to this Actions runner.
set -euo pipefail
CONTAINER="crecisol-c01-07-$RANDOM"
DB="crecisol_c01_07_test"
SOCKET_DIR="$(mktemp -d)"
chmod 777 "$SOCKET_DIR"
API_PID=""
ZERO="00000000000000000000000000000000"
TENANT="22222222222222222222222222222222"
PROOFS="$PWD/.c01-07-ci-proof"
mkdir -p "$PROOFS"
cleanup() {
  if [ -n "$API_PID" ]; then kill "$API_PID" 2>/dev/null || true; wait "$API_PID" 2>/dev/null || true; fi
  docker exec --user root "$CONTAINER" chown -R "$(id -u):$(id -g)" /var/run/mysqld >/dev/null 2>&1 || true
  docker rm -f "$CONTAINER" >/dev/null 2>&1 || true
  rm -rf -- "$SOCKET_DIR"
}
trap cleanup EXIT
root_sql() {
  docker exec "$CONTAINER" mysql --default-character-set=utf8mb4 -u root -D "$DB" --batch --skip-column-names -e "$1"
}
proof() { printf 'PASS: %s\n' "$1" > "$PROOFS/$1"; }
browser() {
  local mode="$1"
  (cd frontend && npx playwright test --config=playwright.c01-07.config.ts --grep "$mode:")
  proof "browser-$mode"
}

docker run --rm -d --network none --name "$CONTAINER" -v "$SOCKET_DIR:/var/run/mysqld" \
  -e MYSQL_ALLOW_EMPTY_PASSWORD=yes -e MYSQL_DATABASE="$DB" \
  mysql:8.4@sha256:a81d043930e51745522bd5c430961cf8286f0a23f0a9a2a6e0108f7ea71b0902 >/dev/null
ready=0
for i in $(seq 1 90); do
  if root_sql 'SELECT 1' >/dev/null 2>&1; then ready=1; break; fi
  sleep 1
done
[ "$ready" -eq 1 ] || { echo 'FAIL MySQL startup' >&2; exit 1; }
version="$(root_sql 'SELECT VERSION()')"
case "$version" in 8.4.*) echo "PASS disposable MySQL $version";; *) echo "FAIL unsupported MySQL $version" >&2; exit 1;; esac
[ -S "$SOCKET_DIR/mysqld.sock" ] || { echo 'FAIL private Unix socket missing' >&2; exit 1; }
docker exec -i "$CONTAINER" mysql -u root -D "$DB" < database/c01_03_brand_schema.sql
root_sql "INSERT INTO brand_scopes (scope_kind,scope_id) VALUES (1,X'$ZERO'),(2,X'$TENANT')"
root_sql "INSERT INTO brand_revisions
 (scope_kind,scope_id,revision,publication_status,name,slogan,descriptor,primary_color,accent_color,logo_url,published_at_utc)
 VALUES (1,X'$ZERO',1,1,'UNPUBLISHED_DRAFT','Private draft','Do not show','#112233','#445566','/assets/draft.png',NULL)"
root_sql "INSERT INTO brand_revisions
 (scope_kind,scope_id,revision,publication_status,name,slogan,descriptor,primary_color,accent_color,
 logo_url,website_url,favicon_url,typography_token,icon_set_token,seo_title,seo_description,copyright_notice,published_at_utc)
 VALUES (1,X'$ZERO',2,2,'CRECISOL','Tu negocio, a tu manera.',
 'Comercio · Servicios · Membresías · Crecimiento','#062E65','#078FE8',
 '/assets/crecisol-oficial.png',NULL,'/assets/crecisol-emblem.png','system','outline',
 'CRECISOL · Tu negocio, a tu manera.','Identidad institucional publicada.',NULL,UTC_TIMESTAMP(6))"
root_sql "INSERT INTO brand_revisions
 (scope_kind,scope_id,revision,publication_status,name,slogan,descriptor,primary_color,accent_color,logo_url,published_at_utc)
 VALUES (2,X'$TENANT',1,2,'PRIVATE_TENANT_B','Tenant secret slogan','Tenant private descriptor',
 '#112233','#445566','/assets/tenant.png',UTC_TIMESTAMP(6))"
root_sql "INSERT INTO brand_publications (scope_kind,scope_id,published_revision)
 VALUES (1,X'$ZERO',2),(2,X'$TENANT',1)"
READER_PASSWORD="$(openssl rand -hex 18)"
docker exec -i "$CONTAINER" mysql -u root -D "$DB" >/dev/null <<SQL
CREATE USER 'brand_reader'@'localhost' IDENTIFIED BY '$READER_PASSWORD';
GRANT SELECT ON $DB.brand_publications TO 'brand_reader'@'localhost';
GRANT SELECT ON $DB.brand_revisions TO 'brand_reader'@'localhost';
SQL
docker exec -e MYSQL_PWD="$READER_PASSWORD" "$CONTAINER" \
  mysql -u brand_reader -D "$DB" -N -s -e 'SELECT COUNT(*) FROM brand_publications' >/dev/null
if docker exec -e MYSQL_PWD="$READER_PASSWORD" "$CONTAINER" mysql -u brand_reader -D "$DB" \
  -e "UPDATE brand_revisions SET name='unauthorized' WHERE scope_kind=1" >/dev/null 2>&1; then
  echo 'FAIL SQL reader obtained write privilege' >&2; exit 1
fi
proof read-only
export CRECISOL_C01_ALLOWED_HOSTS="127.0.0.1"
export CRECISOL_C01_READONLY_DSN="Server=$SOCKET_DIR/mysqld.sock;Connection Protocol=Unix;User ID=brand_reader;Password=$READER_PASSWORD;Database=$DB;Pooling=True;"
export ASPNETCORE_ENVIRONMENT=Production
export ASPNETCORE_URLS="http://127.0.0.1:5184"
dotnet run --no-build --no-launch-profile -c Release \
  --project backend/src/Crecisol.Api/Crecisol.Api.csproj >/dev/null 2>&1 &
API_PID=$!
ready_http=0
for i in $(seq 1 45); do
  status="$(curl --silent --max-time 1 --output /dev/null --write-out '%{http_code}' \
    http://127.0.0.1:5184/api/v1/public/branding/platform || true)"
  if [ "$status" = 200 ]; then ready_http=1; break; fi
  sleep 1
done
[ "$ready_http" -eq 1 ] || { echo 'FAIL local API did not start' >&2; exit 1; }
python3 qa/c01_04_http_smoke.py baseline 5184
proof api-baseline
browser published

root_sql "DELETE FROM brand_publications WHERE scope_kind=1 AND scope_id=X'$ZERO'"
python3 qa/c01_04_http_smoke.py absent 5184
proof api-absent
browser absent

root_sql "INSERT INTO brand_publications (scope_kind,scope_id,published_revision)
 VALUES (1,X'$ZERO',2)"
root_sql "UPDATE brand_revisions SET name='<img src=x onerror=alert(1)>'
 WHERE scope_kind=1 AND scope_id=X'$ZERO' AND revision=2"
python3 qa/c01_04_http_smoke.py invalid 5184
proof api-invalid
browser invalid

root_sql "UPDATE brand_revisions SET name='CRECISOL'
 WHERE scope_kind=1 AND scope_id=X'$ZERO' AND revision=2"
root_sql "INSERT INTO brand_revisions
 (scope_kind,scope_id,revision,publication_status,name,slogan,descriptor,primary_color,accent_color,
 logo_url,website_url,favicon_url,typography_token,icon_set_token,seo_title,seo_description,copyright_notice,published_at_utc)
 VALUES (1,X'$ZERO',3,2,'CRECISOL','Tu negocio, a tu manera.',
 'Comercio · Servicios · Membresías · Crecimiento','#062E65','#078FE8',
 '/assets/crecisol-oficial.png',NULL,'/assets/crecisol-emblem.png','system','outline',
 'CRECISOL revisión 3','Identidad revisada sin cache antiguo.',NULL,UTC_TIMESTAMP(6))"
root_sql "UPDATE brand_publications SET published_revision=3 WHERE scope_kind=1 AND scope_id=X'$ZERO'"
browser updated
root_sql "UPDATE brand_publications SET published_revision=2 WHERE scope_kind=1 AND scope_id=X'$ZERO'"
python3 qa/c01_04_http_smoke.py baseline 5184
proof api-restored
browser published
# No server DSN must deny with HTTP 503; never fall back to public demo.
kill "$API_PID" 2>/dev/null || true
wait "$API_PID" 2>/dev/null || true
API_PID=""
unset CRECISOL_C01_READONLY_DSN
export ASPNETCORE_URLS="http://127.0.0.1:5185"
dotnet run --no-build --no-launch-profile -c Release \
  --project backend/src/Crecisol.Api/Crecisol.Api.csproj >/dev/null 2>&1 &
API_PID=$!
ready_http=0
for i in $(seq 1 45); do
  status="$(curl --silent --max-time 1 --output /dev/null --write-out '%{http_code}' \
    http://127.0.0.1:5185/api/v1/public/branding/platform || true)"
  if [ "$status" = 503 ]; then ready_http=1; break; fi
  sleep 1
done
[ "$ready_http" -eq 1 ] || { echo 'FAIL unconfigured API startup' >&2; exit 1; }
python3 qa/c01_04_http_smoke.py unconfigured 5185
proof api-unconfigured
echo 'PASS C01-07: real MySQL 8.4 + .NET 10 HTTP + Angular 22 Chromium with no mocks, tenant/draft/XSS/host/cache checks.'
