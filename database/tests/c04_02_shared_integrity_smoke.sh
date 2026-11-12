#!/usr/bin/env bash
# CRECISOL C04-02 PREPARATION: disposable MySQL8.4 fixture, not a migration.
# All SQL is synthetic: no network, VPS, DNS, production credentials or tenants.
set -euo pipefail

CONTAINER="crecisol-c04-02-fixture-$RANDOM-$$"
DB="crecisol_c04_02_disposable"
IMAGE="mysql:8.4@sha256:a81d043930e51745522bd5c430961cf8286f0a23f0a9a2a6e0108f7ea71b0902"
A="11111111111111111111111111111111"
B="22222222222222222222222222222222"
SHARED_BRANCH="33333333333333333333333333333333"
A_BRANCH="44444444444444444444444444444444"
B_BRANCH="99999999999999999999999999999999"
ITEM="55555555555555555555555555555555"
A_ITEM="88888888888888888888888888888888"
NOTE="77777777777777777777777777777777"
ZERO="00000000000000000000000000000000"
positive=0
negative=0

cleanup() {
  docker rm -f "$CONTAINER" >/dev/null 2>&1 || true
}
trap cleanup EXIT

sql() {
  docker exec "$CONTAINER" mysql -uroot --batch --skip-column-names -D "$DB" -e "$1"
}
pass_equal() {
  local label="$1" expected="$2" actual="$3"
  if [ "$actual" != "$expected" ]; then
    echo "FAIL C04-02 positive $label: expected $expected, got $actual" >&2
    exit 1
  fi
  positive=$((positive+1))
  echo "PASS C04-02 positive: $label"
}
expect_denied() {
  local label="$1" query="$2" result
  if result=$(sql "$query" 2>&1); then
    echo "FAIL C04-02 SECURITY: accepted $label" >&2
    exit 1
  fi
  # Only real integrity denials count: a Docker/socket/SQL syntax failure is FAIL.
  case "$result" in
    *"ERROR 1452"*|*"ERROR 1451"*|*"ERROR 1062"*|*"ERROR 3819"*|*"ERROR 1048"*) ;;
    *) echo "FAIL C04-02 unexpected denial for $label: $result" >&2; exit 1 ;;
  esac
  negative=$((negative+1))
  echo "PASS C04-02 negative: $label"
}
reader_denied() {
  local label="$1" query="$2" result
  if result=$(docker exec -e MYSQL_PWD="$READER_PASSWORD" "$CONTAINER" \
    mysql -u c04_fixture_reader --batch --skip-column-names -D "$DB" -e "$query" 2>&1); then
    echo "FAIL C04-02 READER: accepted $label" >&2
    exit 1
  fi
  case "$result" in
    *"ERROR 1142"*) ;;
    *) echo "FAIL C04-02 reader denial ($label): $result" >&2; exit 1 ;;
  esac
  negative=$((negative+1))
  echo "PASS C04-02 negative: $label"
}

docker run --rm -d --network none --name "$CONTAINER" \
  -e MYSQL_ALLOW_EMPTY_PASSWORD=yes -e MYSQL_DATABASE="$DB" "$IMAGE" >/dev/null

# MySQL 8.4 entrypoint first runs a temporary bootstrap server. Never
# interpret that server's SELECT 1 as readiness for the final daemon.
ready=0
init_done=0
for _ in $(seq 1 120); do
  if [ "$init_done" -eq 0 ] && docker logs "$CONTAINER" 2>&1 |
    grep -Fq 'MySQL init process done. Ready for start up.'; then
    init_done=1
  fi
  if [ "$init_done" -eq 1 ] && sql 'SELECT 1' >/dev/null 2>&1; then
    sleep 1
    if sql 'SELECT 1' >/dev/null 2>&1; then
      ready=1
      break
    fi
  fi
  sleep 1
done
if [ "$ready" -ne 1 ]; then
  echo "FAIL C04-02: final MySQL8.4 server unavailable" >&2
  exit 1
fi
case "$(sql 'SELECT VERSION()')" in
  8.4.*) echo "PASS C04-02 MySQL version: $(sql 'SELECT VERSION()')" ;;
  *) echo "FAIL C04-02 requires pinned MySQL8.4" >&2; exit 1 ;;
esac
pass_equal "foreign keys enforced" "1" "$(sql 'SELECT @@FOREIGN_KEY_CHECKS')"

# Exact reviewed fixture: NO live backend schema nor migrations.
docker exec -i "$CONTAINER" mysql -uroot -D "$DB" \
  < database/tests/fixtures/c04_02_shared_integrity_fixture.sql

pass_equal "four InnoDB fixture tables" "4" \
  "$(sql "SELECT COUNT(*) FROM information_schema.tables
    WHERE table_schema=DATABASE() AND table_name LIKE 'c04_02_fixture_%' AND engine='InnoDB'")"
pass_equal "three restrictive FK constraints" "3" \
  "$(sql "SELECT COUNT(*) FROM information_schema.referential_constraints
    WHERE constraint_schema=DATABASE()
      AND constraint_name IN ('fk_c04_02_branch_tenant','fk_c04_02_item_branch','fk_c04_02_note_item')
      AND update_rule='RESTRICT' AND delete_rule='RESTRICT'")"
pass_equal "four tenant-prefixed PKs" "4" \
  "$(sql "SELECT COUNT(*) FROM information_schema.statistics
    WHERE table_schema=DATABASE() AND index_name='PRIMARY' AND seq_in_index=1
      AND column_name='tenant_id' AND table_name LIKE 'c04_02_fixture_%'")"
pass_equal "no FK without tenant_id" "0" \
  "$(sql "SELECT COUNT(*) FROM information_schema.key_column_usage
    WHERE table_schema=DATABASE()
      AND constraint_name IN ('fk_c04_02_item_branch','fk_c04_02_note_item')
      AND column_name='tenant_id' AND referenced_column_name IS NULL")"

sql "INSERT INTO c04_02_fixture_tenants VALUES (X'$A',3),(X'$B',3)"
sql "INSERT INTO c04_02_fixture_branches VALUES
  (X'$A',X'$SHARED_BRANCH','A shared id'),
  (X'$A',X'$A_BRANCH','A only branch'),
  (X'$B',X'$SHARED_BRANCH','B shared id'),
  (X'$B',X'$B_BRANCH','B only branch')"
sql "INSERT INTO c04_02_fixture_items VALUES
  (X'$A',X'$SHARED_BRANCH',X'$ITEM','shared-key'),
  (X'$A',X'$A_BRANCH',X'$A_ITEM','a-only'),
  (X'$B',X'$SHARED_BRANCH',X'$ITEM','shared-key')"
sql "INSERT INTO c04_02_fixture_notes VALUES
  (X'$A',X'$SHARED_BRANCH',X'$ITEM',X'$NOTE',1),
  (X'$B',X'$SHARED_BRANCH',X'$ITEM',X'$NOTE',1)"

pass_equal "A has two isolated items" "2" \
  "$(sql "SELECT COUNT(*) FROM c04_02_fixture_items WHERE tenant_id=X'$A'")"
pass_equal "B has one isolated item" "1" \
  "$(sql "SELECT COUNT(*) FROM c04_02_fixture_items WHERE tenant_id=X'$B'")"
pass_equal "A and B may reuse branch/item/note IDs" "2" \
  "$(sql "SELECT COUNT(*) FROM c04_02_fixture_notes
    WHERE branch_id=X'$SHARED_BRANCH' AND item_id=X'$ITEM' AND note_id=X'$NOTE'")"
pass_equal "A scoped note read" "1" \
  "$(sql "SELECT COUNT(*) FROM c04_02_fixture_notes WHERE tenant_id=X'$A'
    AND branch_id=X'$SHARED_BRANCH' AND item_id=X'$ITEM'")"
pass_equal "B scoped note read" "1" \
  "$(sql "SELECT COUNT(*) FROM c04_02_fixture_notes WHERE tenant_id=X'$B'
    AND branch_id=X'$SHARED_BRANCH' AND item_id=X'$ITEM'")"
pass_equal "A-only resource hidden by B scope predicate" "0" \
  "$(sql "SELECT COUNT(*) FROM c04_02_fixture_items
    WHERE tenant_id=X'$B' AND branch_id=X'$A_BRANCH' AND item_id=X'$A_ITEM'")"
pass_equal "same external key is valid in another tenant" "2" \
  "$(sql "SELECT COUNT(*) FROM c04_02_fixture_items WHERE external_key='shared-key'")"
pass_equal "unscoped SQL DOES see both tenants (future backend authorization required)" "3" \
  "$(sql "SELECT COUNT(*) FROM c04_02_fixture_items")"

expect_denied "unknown tenant branch" \
  "INSERT INTO c04_02_fixture_branches VALUES (X'AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA',X'$A_BRANCH','Orphan branch')"
expect_denied "zero tenant identifier" \
  "INSERT INTO c04_02_fixture_tenants VALUES (X'$ZERO',3)"
expect_denied "invalid tenant lifecycle status" \
  "INSERT INTO c04_02_fixture_tenants VALUES (X'BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB',9)"
expect_denied "NULL tenant on branch" \
  "INSERT INTO c04_02_fixture_branches VALUES (NULL,X'$B_BRANCH','Null tenant')"
expect_denied "B item references A-only branch" \
  "INSERT INTO c04_02_fixture_items VALUES (X'$B',X'$A_BRANCH',X'$A_ITEM','foreign-branch')"
expect_denied "A item references B-only branch" \
  "INSERT INTO c04_02_fixture_items VALUES (X'$A',X'$B_BRANCH',X'$A_ITEM','foreign-branch')"
expect_denied "B note references A-only resource" \
  "INSERT INTO c04_02_fixture_notes VALUES (X'$B',X'$A_BRANCH',X'$A_ITEM',X'$NOTE',2)"
expect_denied "A note references B-only resource" \
  "INSERT INTO c04_02_fixture_notes VALUES (X'$A',X'$B_BRANCH',X'$ITEM',X'$NOTE',2)"
expect_denied "A-only item tenant reparented into B" \
  "UPDATE c04_02_fixture_items SET tenant_id=X'$B'
    WHERE tenant_id=X'$A' AND branch_id=X'$A_BRANCH' AND item_id=X'$A_ITEM'"
expect_denied "deleting A branch with referenced item" \
  "DELETE FROM c04_02_fixture_branches
    WHERE tenant_id=X'$A' AND branch_id=X'$A_BRANCH'"
expect_denied "repeating scoped external key" \
  "INSERT INTO c04_02_fixture_items VALUES
    (X'$A',X'$SHARED_BRANCH',X'CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC','shared-key')"
expect_denied "unknown item in same branch" \
  "INSERT INTO c04_02_fixture_notes VALUES
    (X'$A',X'$SHARED_BRANCH',X'AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA',X'$NOTE',1)"
expect_denied "item ID cannot be zero" \
  "INSERT INTO c04_02_fixture_items VALUES (X'$A',X'$A_BRANCH',X'$ZERO','zero-item')"
expect_denied "invalid note kind" \
  "INSERT INTO c04_02_fixture_notes VALUES
    (X'$A',X'$A_BRANCH',X'$A_ITEM',X'AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA',7)"

# Synthetic reader can SELECT scoped rows but must not modify/DDL.
READER_PASSWORD="$(openssl rand -hex 18)"
docker exec -i "$CONTAINER" mysql -uroot -D "$DB" >/dev/null <<SQL
CREATE USER 'c04_fixture_reader'@'localhost' IDENTIFIED BY '$READER_PASSWORD';
GRANT SELECT ON \`$DB\`.\`c04_02_fixture_items\` TO 'c04_fixture_reader'@'localhost';
SQL
pass_equal "reader can execute tenant-scoped SELECT" "2" \
  "$(docker exec -e MYSQL_PWD="$READER_PASSWORD" "$CONTAINER" \
    mysql -u c04_fixture_reader --batch --skip-column-names -D "$DB" \
    -e "SELECT COUNT(*) FROM c04_02_fixture_items WHERE tenant_id=X'$A'")"
reader_denied "read-only cannot UPDATE" \
  "UPDATE c04_02_fixture_items SET external_key='evil' WHERE tenant_id=X'$A'"
reader_denied "read-only cannot DELETE" \
  "DELETE FROM c04_02_fixture_items WHERE tenant_id=X'$A'"
reader_denied "read-only cannot DROP" \
  "DROP TABLE c04_02_fixture_items"
reader_denied "read-only cannot enumerate tenant registry" \
  "SELECT COUNT(*) FROM c04_02_fixture_tenants"

echo "PASS C04-02 MySQL8.4 preparation: $positive positives, $negative negatives;"
echo "PASS composite same-tenant/branch FK, scoped uniqueness, restrictive delete, no cross-tenant reparent, restricted reader."
echo "LIMIT: synthetic disposable SQL fixture only. Shared persistence, authenticated backend filtering, RLS, dedicated routing, migrations and production are NOT implemented/certified."
