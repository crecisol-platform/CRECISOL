#!/usr/bin/env bash
# C01-03: isolated MySQL 8.4 integration and negative integrity tests.
# Never connect to Hostinger, a real database, or an external network.
set -euo pipefail

CONTAINER="crecisol-c01-03-mysql-$RANDOM"
DB="crecisol_c01_03_test"
ZERO="00000000000000000000000000000000"
A="11111111111111111111111111111111"
B_ID="22222222222222222222222222222222"
negatives=0
positives=0

cleanup() { docker rm -f "$CONTAINER" >/dev/null 2>&1 || true; }
trap cleanup EXIT

sql() {
  docker exec "$CONTAINER" mysql -u root --batch --skip-column-names -D "$DB" -e "$1"
}

pass_equal() {
  local label="$1" expected="$2" actual="$3"
  if [ "$actual" != "$expected" ]; then
    echo "FAIL: $label expected $expected, got $actual" >&2
    exit 1
  fi
  positives=$((positives + 1))
  echo "PASS positive: $label"
}

expect_denied() {
  local label="$1" query="$2" result
  if result=$(sql "$query" 2>&1); then
    echo "FAIL SECURITY: accepted $label" >&2
    exit 1
  fi
  # Reject ONLY on an actual database integrity error, not syntax/server failure.
  case "$result" in
    *"ERROR 3819"*|*"ERROR 1452"*|*"ERROR 1451"*|*"ERROR 1062"*|*"ERROR 1406"*|*"ERROR 1048"*) ;;
    *) echo "FAIL: unexpected error for $label: $result" >&2; exit 1 ;;
  esac
  negatives=$((negatives + 1))
  echo "PASS negative: $label"
}

docker run --rm -d --network none --name "$CONTAINER" \
  -e MYSQL_ALLOW_EMPTY_PASSWORD=yes -e MYSQL_DATABASE="$DB" mysql:8.4@sha256:a81d043930e51745522bd5c430961cf8286f0a23f0a9a2a6e0108f7ea71b0902 >/dev/null

ready=0
for i in $(seq 1 90); do
  if sql "SELECT 1" >/dev/null 2>&1; then ready=1; break; fi
  sleep 1
done
if [ "$ready" != 1 ]; then
  echo "FAIL: isolated MySQL did not become ready" >&2
  docker logs "$CONTAINER" | tail -30
  exit 1
fi
version=$(sql "SELECT VERSION()")
case "$version" in 8.4.*) echo "MySQL version verified: $version" ;; *)
  echo "FAIL: expected MySQL 8.4, got $version" >&2; exit 1 ;;
esac

# Import the exact reviewed schema. No live connection string, ports, or volume.
docker exec -i "$CONTAINER" mysql -u root -D "$DB" \
  < database/c01_03_brand_schema.sql

pass_equal "three InnoDB tables" "3" "$(sql "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE() AND table_name IN ('brand_scopes','brand_revisions','brand_publications') AND engine='InnoDB'")"
pass_equal "two composite foreign keys" "2" "$(sql "SELECT COUNT(*) FROM information_schema.referential_constraints WHERE constraint_schema=DATABASE() AND constraint_name IN ('fk_brand_revision_scope','fk_brand_publication_revision')")"
pass_equal "three composite primary keys" "3" "$(sql "SELECT COUNT(DISTINCT table_name) FROM information_schema.statistics WHERE table_schema=DATABASE() AND index_name='PRIMARY' AND table_name IN ('brand_scopes','brand_revisions','brand_publications')")"

sql "INSERT INTO brand_scopes (scope_kind,scope_id) VALUES (1,X'$ZERO'),(2,X'$A'),(2,X'$B_ID')"
sql "INSERT INTO brand_revisions
 (scope_kind,scope_id,revision,publication_status,name,slogan,descriptor,primary_color,accent_color,logo_url,published_at_utc)
 VALUES
 (1,X'$ZERO',1,1,'CRECISOL','Tu negocio, a tu manera.','Comercio y servicios','#112233','#445566','/assets/brand.png',NULL),
 (1,X'$ZERO',2,2,'CRECISOL','Tu negocio, a tu manera.','Comercio y servicios','#112233','#445566','/assets/brand.png',UTC_TIMESTAMP(6)),
 (2,X'$B_ID',1,1,'Empresa B','Empresa B lema','Empresa B descriptor','#112233','#445566','/assets/brand.png',NULL)"
sql "INSERT INTO brand_revisions
 (scope_kind,scope_id,revision,publication_status,name,slogan,descriptor,primary_color,accent_color,logo_url,website_url,favicon_url,typography_token,icon_set_token,seo_title,seo_description,copyright_notice,published_at_utc)
 VALUES
 (2,X'$A',1,2,'Empresa A','Empresa A lema','Empresa A descriptor','#112233','#445566','/assets/brand.png','https://example.test','/assets/favicon.webp','system','outline','Empresa A inicio','Descripcion segura de empresa A','Copyright Empresa A',UTC_TIMESTAMP(6))"
sql "INSERT INTO brand_publications (scope_kind,scope_id,published_revision)
 VALUES (1,X'$ZERO',2),(2,X'$A',1)"
pass_equal "two independent publications" "2" "$(sql "SELECT COUNT(*) FROM brand_publications")"
pass_equal "platform published pointer" "2" "$(sql "SELECT published_revision FROM brand_publications WHERE scope_kind=1 AND scope_id=X'$ZERO'")"
pass_equal "tenant A published pointer" "1" "$(sql "SELECT published_revision FROM brand_publications WHERE scope_kind=2 AND scope_id=X'$A'")"
pass_equal "tenant B not published" "0" "$(sql "SELECT COUNT(*) FROM brand_publications WHERE scope_kind=2 AND scope_id=X'$B_ID'")"

expect_denied "unknown scope kind" \
 "INSERT INTO brand_scopes(scope_kind,scope_id) VALUES (3,X'$A')"
expect_denied "platform with tenant id" \
 "INSERT INTO brand_scopes(scope_kind,scope_id) VALUES (1,X'$A')"
expect_denied "tenant with empty id" \
 "INSERT INTO brand_scopes(scope_kind,scope_id) VALUES (2,X'$ZERO')"
expect_denied "orphan revision" \
 "INSERT INTO brand_revisions(scope_kind,scope_id,revision,publication_status,name,slogan,descriptor,primary_color,accent_color,logo_url) VALUES (2,X'99999999999999999999999999999999',1,1,'Valid brand','Valid slogan','Valid descriptor','#112233','#445566','/assets/brand.png')"
expect_denied "zero revision" \
 "INSERT INTO brand_revisions(scope_kind,scope_id,revision,publication_status,name,slogan,descriptor,primary_color,accent_color,logo_url) VALUES (2,X'$A',0,1,'Valid brand','Valid slogan','Valid descriptor','#112233','#445566','/assets/brand.png')"
expect_denied "revision greater than .NET signed long" \
 "INSERT INTO brand_revisions(scope_kind,scope_id,revision,publication_status,name,slogan,descriptor,primary_color,accent_color,logo_url) VALUES (2,X'$A',9223372036854775808,1,'Valid brand','Valid slogan','Valid descriptor','#112233','#445566','/assets/brand.png')"
expect_denied "unknown publication status" \
 "INSERT INTO brand_revisions(scope_kind,scope_id,revision,publication_status,name,slogan,descriptor,primary_color,accent_color,logo_url) VALUES (2,X'$A',3,3,'Valid brand','Valid slogan','Valid descriptor','#112233','#445566','/assets/brand.png')"
expect_denied "draft with published timestamp" \
 "INSERT INTO brand_revisions(scope_kind,scope_id,revision,publication_status,name,slogan,descriptor,primary_color,accent_color,logo_url,published_at_utc) VALUES (2,X'$A',3,1,'Valid brand','Valid slogan','Valid descriptor','#112233','#445566','/assets/brand.png',UTC_TIMESTAMP(6))"
expect_denied "published without UTC timestamp" \
 "INSERT INTO brand_revisions(scope_kind,scope_id,revision,publication_status,name,slogan,descriptor,primary_color,accent_color,logo_url) VALUES (2,X'$A',3,2,'Valid brand','Valid slogan','Valid descriptor','#112233','#445566','/assets/brand.png')"
expect_denied "invalid color syntax" \
 "INSERT INTO brand_revisions(scope_kind,scope_id,revision,publication_status,name,slogan,descriptor,primary_color,accent_color,logo_url) VALUES (2,X'$A',3,1,'Valid brand','Valid slogan','Valid descriptor','#zzzzzz','#445566','/assets/brand.png')"
expect_denied "nonlocal brand asset" \
 "INSERT INTO brand_revisions(scope_kind,scope_id,revision,publication_status,name,slogan,descriptor,primary_color,accent_color,logo_url) VALUES (2,X'$A',3,1,'Valid brand','Valid slogan','Valid descriptor','#112233','#445566','https://evil.invalid/brand.png')"
expect_denied "invalid presentation token" \
 "INSERT INTO brand_revisions(scope_kind,scope_id,revision,publication_status,name,slogan,descriptor,primary_color,accent_color,logo_url,favicon_url,typography_token,icon_set_token,seo_title,seo_description) VALUES (2,X'$A',3,1,'Valid brand','Valid slogan','Valid descriptor','#112233','#445566','/assets/brand.png','/assets/favicon.png','untrusted-css','outline','Valid SEO title','Valid SEO description')"
expect_denied "partial presentation" \
 "INSERT INTO brand_revisions(scope_kind,scope_id,revision,publication_status,name,slogan,descriptor,primary_color,accent_color,logo_url,seo_title) VALUES (2,X'$A',3,1,'Valid brand','Valid slogan','Valid descriptor','#112233','#445566','/assets/brand.png','Incomplete SEO')"
expect_denied "blank name" \
 "INSERT INTO brand_revisions(scope_kind,scope_id,revision,publication_status,name,slogan,descriptor,primary_color,accent_color,logo_url) VALUES (2,X'$A',3,1,'  ','Valid slogan','Valid descriptor','#112233','#445566','/assets/brand.png')"
expect_denied "draft cannot be publication pointer" \
 "INSERT INTO brand_publications(scope_kind,scope_id,published_revision,publication_status) VALUES (2,X'$B_ID',1,2)"
expect_denied "cross-tenant revision reference" \
 "INSERT INTO brand_publications(scope_kind,scope_id,published_revision,publication_status) VALUES (2,X'$B_ID',2,2)"
expect_denied "nonexistent revision" \
 "INSERT INTO brand_publications(scope_kind,scope_id,published_revision,publication_status) VALUES (2,X'$B_ID',999,2)"
expect_denied "publication status must be published" \
 "INSERT INTO brand_publications(scope_kind,scope_id,published_revision,publication_status) VALUES (2,X'$B_ID',1,1)"
expect_denied "alter referenced published status" \
 "UPDATE brand_revisions SET publication_status=1,published_at_utc=NULL WHERE scope_kind=1 AND scope_id=X'$ZERO' AND revision=2"
expect_denied "duplicate revision per scope" \
 "INSERT INTO brand_revisions(scope_kind,scope_id,revision,publication_status,name,slogan,descriptor,primary_color,accent_color,logo_url,published_at_utc) VALUES (2,X'$A',1,2,'Valid brand','Valid slogan','Valid descriptor','#112233','#445566','/assets/brand.png',UTC_TIMESTAMP(6))"
expect_denied "duplicate publication per scope" \
 "INSERT INTO brand_publications(scope_kind,scope_id,published_revision) VALUES (1,X'$ZERO',2)"
expect_denied "parent scope delete with revisions" \
 "DELETE FROM brand_scopes WHERE scope_kind=2 AND scope_id=X'$A'"

# Valid late publication of B uses a NEW revision; draft remains unpublished.
sql "INSERT INTO brand_revisions(scope_kind,scope_id,revision,publication_status,name,slogan,descriptor,primary_color,accent_color,logo_url,published_at_utc) VALUES (2,X'$B_ID',2,2,'Empresa B','Empresa B lema','Empresa B descriptor','#112233','#445566','/assets/brand.png',UTC_TIMESTAMP(6))"
sql "INSERT INTO brand_publications(scope_kind,scope_id,published_revision) VALUES (2,X'$B_ID',2)"
pass_equal "B points only to own published revision" "2" "$(sql "SELECT published_revision FROM brand_publications WHERE scope_kind=2 AND scope_id=X'$B_ID'")"

# Rollback safety on this SYNTHETIC database only.
sql "DROP TABLE brand_publications"
sql "DROP TABLE brand_revisions"
sql "DROP TABLE brand_scopes"
pass_equal "reversible DDL in disposable database" "0" "$(sql "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE() AND table_name LIKE 'brand_%'")"

echo "C01-03 MYSQL 8.4 PASS: $positives positive, $negatives rejected integrity cases"
echo "Boundary: SQL FK/CHECK tests do NOT prove actor authorization, tenant resolution, audit, or production migration."
