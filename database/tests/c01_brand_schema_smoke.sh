#!/usr/bin/env bash
# C01 ephemeral MySQL 8.4 test: no network, credentials, persistent volume or production.
set -euo pipefail
CONTAINER="crecisol-c01-mysql-isolated"
DB="crecisol_c01_test"

cleanup() { docker rm -f "$CONTAINER" >/dev/null 2>&1 || true; }
trap cleanup EXIT

docker run --rm -d --network none --name "$CONTAINER" \
  -e MYSQL_ALLOW_EMPTY_PASSWORD=yes -e MYSQL_DATABASE="$DB" mysql:8.4 >/dev/null

ready=0
for i in $(seq 1 70); do
  if docker exec "$CONTAINER" mysql -u root -D "$DB" -N -s -e "SELECT 1" >/dev/null 2>&1; then
    ready=1
    break
  fi
  sleep 1
done
if [ "$ready" -ne 1 ]; then
  docker logs "$CONTAINER" | tail -30
  echo "C01 MySQL did not become ready" >&2
  exit 1
fi

version=$(docker exec "$CONTAINER" mysql -u root -N -s -e 'SELECT VERSION()')
case "$version" in
  8.4.*) echo "C01 isolated MySQL version: $version" ;;
  *) echo "Expected MySQL 8.4, found $version" >&2; exit 1 ;;
esac

docker exec -i "$CONTAINER" mysql -u root -D "$DB" \
  < database/migrations/0001_c01_brand_identity.sql
docker exec -i "$CONTAINER" mysql -u root -D "$DB" \
  < database/tests/c01_brand_schema_smoke.sql

publications=$(docker exec "$CONTAINER" mysql -u root -D "$DB" -N -s \
  -e "SELECT COUNT(*) FROM brand_publications")
[ "$publications" = "2" ] || { echo "C01 expected two publications" >&2; exit 1; }

expect_denied() {
  local label="$1"
  local query="$2"
  if docker exec "$CONTAINER" mysql -u root -D "$DB" -e "$query" >/dev/null 2>&1; then
    echo "SECURITY TEST FAILED: database accepted $label" >&2
    exit 1
  fi
  echo "Rejected as expected: $label"
}

expect_denied "tenant with platform zero scope" \
  "INSERT INTO brand_scopes VALUES ('TENANT', X'00000000000000000000000000000000', UTC_TIMESTAMP(6));"
expect_denied "platform with non-zero scope" \
  "INSERT INTO brand_scopes VALUES ('PLATFORM', X'99999999999999999999999999999999', UTC_TIMESTAMP(6));"
expect_denied "lowercase scope (binary collation)" \
  "INSERT INTO brand_scopes VALUES ('tenant', X'99999999999999999999999999999999', UTC_TIMESTAMP(6));"
expect_denied "foreign tenant publication of another tenant revision" \
  "INSERT INTO brand_publications VALUES ('TENANT', X'22222222222222222222222222222222', 1, UTC_TIMESTAMP(6));"
expect_denied "zero revision number" \
  "INSERT INTO brand_revisions VALUES ('TENANT', X'11111111111111111111111111111111', 0, 'A', 'B', 'C', '#112233', '#445566', 'logo', NULL, UTC_TIMESTAMP(6));"

echo "C01 MySQL 8.4 checks passed: schema, composite scope FK and five negative cases."
echo "Does NOT validate authenticated tenant storage, audit history or production migration."
