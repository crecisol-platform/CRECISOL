#!/usr/bin/env bash
# C03-04 Infrastructure adapter: verified MySQL 8.4 isolated UNIX-socket CI.
# Synthetic data only; no ports, network or external credentials. NO production.
set -euo pipefail

CONTAINER="crecisol-c03-04-$RANDOM"
DB="crecisol_c03_04_test"
SOCKET_DIR="$(mktemp -d)"
chmod 777 "$SOCKET_DIR"
ZERO="00000000000000000000000000000000"
TENANT="22222222222222222222222222222222"

cleanup() {
  docker exec --user root "$CONTAINER" chown -R "$(id -u):$(id -g)" \
    /var/run/mysqld >/dev/null 2>&1 || true
  docker rm -f "$CONTAINER" >/dev/null 2>&1 || true
  rm -rf -- "$SOCKET_DIR"
}
trap cleanup EXIT

sql() {
  docker exec "$CONTAINER" mysql -u root --batch --skip-column-names -D "$DB" -e "$1"
}
test_mode() {
  dotnet run --no-build -c Release \
    --project backend/tests/Crecisol.Infrastructure.ContractsTests/Crecisol.Infrastructure.ContractsTests.csproj \
    -- "$1"
}

docker run --rm -d --network none --name "$CONTAINER" \
  -v "$SOCKET_DIR:/var/run/mysqld" \
  -e MYSQL_ALLOW_EMPTY_PASSWORD=yes -e MYSQL_DATABASE="$DB" \
  mysql:8.4@sha256:a81d043930e51745522bd5c430961cf8286f0a23f0a9a2a6e0108f7ea71b0902 >/dev/null

ready=0
for i in $(seq 1 90); do
  if sql "SELECT 1" >/dev/null 2>&1; then ready=1; break; fi
  sleep 1
done
if [ "$ready" -ne 1 ]; then echo "FAIL C03-04: isolated MySQL startup" >&2; exit 1; fi
case "$(sql 'SELECT VERSION()')" in 8.4.*) : ;; *)
  echo "FAIL C03-04: MySQL must be 8.4" >&2; exit 1 ;; esac
[ -S "$SOCKET_DIR/mysqld.sock" ] || {
  echo "FAIL C03-04: local-only socket missing" >&2; exit 1;
}

docker exec -i "$CONTAINER" mysql -u root -D "$DB" < database/c01_03_brand_schema.sql

sql "INSERT INTO brand_scopes(scope_kind,scope_id) VALUES
 (1,X'$ZERO'),(2,X'$TENANT')"
sql "INSERT INTO brand_revisions
 (scope_kind,scope_id,revision,publication_status,name,slogan,descriptor,
  primary_color,accent_color,logo_url,published_at_utc)
 VALUES
 (1,X'$ZERO',1,1,'Platform draft','Draft slogan','Draft descriptor',
  '#112233','#445566','/assets/draft.png',NULL),
 (1,X'$ZERO',2,2,'Platform published','Published slogan','Published descriptor',
  '#112233','#445566','/assets/brand.png',UTC_TIMESTAMP(6)),
 (1,X'$ZERO',3,1,'Unpublished latest','Draft slogan','Draft descriptor',
  '#112233','#445566','/assets/draft.png',NULL),
 (2,X'$TENANT',99,2,'Tenant private','Private slogan','Private descriptor',
  '#112233','#445566','/assets/tenant.png',UTC_TIMESTAMP(6))"
sql "INSERT INTO brand_publications(scope_kind,scope_id,published_revision)
 VALUES (1,X'$ZERO',2),(2,X'$TENANT',99)"

# Build the least-privileged real database user; password is ephemeral, never logged.
READER_PASSWORD="$(openssl rand -hex 18)"
docker exec -i "$CONTAINER" mysql -u root -D "$DB" >/dev/null <<SQL
CREATE USER 'revision_reader'@'localhost' IDENTIFIED BY '$READER_PASSWORD';
GRANT SELECT ON \`$DB\`.\`brand_publications\` TO 'revision_reader'@'localhost';
GRANT SELECT ON \`$DB\`.\`brand_revisions\` TO 'revision_reader'@'localhost';
SQL
export CRECISOL_C03_04_TEST_DSN="Server=$SOCKET_DIR/mysqld.sock;Connection Protocol=Unix;User ID=revision_reader;Password=$READER_PASSWORD;Database=$DB;Pooling=False;"

test_mode published
# An extra draft must not appear as a new/current publication.
sql "INSERT INTO brand_revisions
 (scope_kind,scope_id,revision,publication_status,name,slogan,descriptor,
  primary_color,accent_color,logo_url,published_at_utc)
 VALUES (1,X'$ZERO',5,1,'Another draft','Draft slogan','Draft descriptor',
 '#112233','#445566','/assets/draft.png',NULL)"
test_mode published

# A legitimate new synthetic published revision changes the pointer atomically
# inside MySQL. Application preflight still does NOT write or reserve a version.
sql "INSERT INTO brand_revisions
 (scope_kind,scope_id,revision,publication_status,name,slogan,descriptor,
  primary_color,accent_color,logo_url,published_at_utc)
 VALUES (1,X'$ZERO',4,2,'New published','New slogan','New descriptor',
 '#112233','#445566','/assets/brand.png',UTC_TIMESTAMP(6))"
sql "UPDATE brand_publications SET published_revision=4
 WHERE scope_kind=1 AND scope_id=X'$ZERO'"
test_mode updated

# Deny UPDATE under the adapter's SQL account, not only via a string check.
if docker exec -e MYSQL_PWD="$READER_PASSWORD" "$CONTAINER" \
  mysql -u revision_reader -D "$DB" -e \
  "UPDATE brand_publications SET published_revision=2 WHERE scope_kind=1" >/dev/null 2>&1; then
  echo "FAIL C03-04: read-only user obtained UPDATE" >&2; exit 1
fi
echo "PASS C03-04: read-only SQL user denied UPDATE"

# Tenant remains published, but PLATFORM absent must be a null result.
sql "DELETE FROM brand_publications WHERE scope_kind=1 AND scope_id=X'$ZERO'"
test_mode absent

# Revoking SELECT on either joined table must never fall back to other data.
sql "REVOKE SELECT ON \`$DB\`.\`brand_revisions\` FROM 'revision_reader'@'localhost'"
test_mode denied

echo "C03-04 MYSQL8.4 PASS: platform-only pointer, draft/tenant denial, " \
     "current revision change, missing publication, least-privilege rejection."
echo "LIMIT: CI synthetic read-only adapter only; not private authorization, " \
     "publication writes, tenant routing, production or API integration."
