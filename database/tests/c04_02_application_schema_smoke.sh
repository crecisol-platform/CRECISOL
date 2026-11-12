#!/usr/bin/env bash
# CRECISOL C04-02: proposed APPLICATION DDL tested on an isolated disposable database.
set -euo pipefail
IMAGE="mysql:8.4@sha256:a81d043930e51745522bd5c430961cf8286f0a23f0a9a2a6e0108f7ea71b0902"
CONTAINER="crecisol-c04-02-app-$RANDOM-$$"
DB="c04_02_app_disposable"
A="11111111111111111111111111111111"
B="22222222222222222222222222222222"
ABR="33333333333333333333333333333333"
BBR="44444444444444444444444444444444"
ITEM="55555555555555555555555555555555"
EV="66666666666666666666666666666666"
ZERO="00000000000000000000000000000000"
positive=0
negative=0
cleanup() { docker rm -f "$CONTAINER" >/dev/null 2>&1 || true; }
trap cleanup EXIT
sql() { docker exec "$CONTAINER" mysql -uroot --batch --skip-column-names -D "$DB" -e "$1"; }
eq() { local name="$1" want="$2" got="$3"; if [ "$want" != "$got" ]; then echo "FAIL $name: expected $want got $got" >&2; exit 1; fi; positive=$((positive+1)); echo "PASS $name"; }
reject() { local name="$1" statement="$2" result; if result=$(sql "$statement" 2>&1); then echo "FAIL accepted: $name" >&2; exit 1; fi; case "$result" in *"ERROR 1452"*|*"ERROR 1451"*|*"ERROR 1062"*|*"ERROR 3819"*|*"ERROR 1048"*) ;; *) echo "FAIL unexpected error $name: $result" >&2; exit 1 ;; esac; negative=$((negative+1)); echo "PASS rejected $name"; }
expect_immutable() {
  local label="$1" statement="$2" result
  if result=$(sql "$statement" 2>&1); then
    echo "FAIL C04-02 IMMUTABILITY: accepted $label" >&2; exit 1
  fi
  case "$result" in
    *"ERROR 1644"*) ;;
    *) echo "FAIL C04-02 wrong immutability denial ($label): $result" >&2; exit 1 ;;
  esac
  negative=$((negative+1))
  echo "PASS immutable ownership rejected: $label"
}
docker run --rm -d --network none --name "$CONTAINER" -e MYSQL_ALLOW_EMPTY_PASSWORD=yes -e MYSQL_DATABASE="$DB" "$IMAGE" >/dev/null
ready=0
boot=0
for _ in $(seq 1 120); do
  if [ "$boot" -eq 0 ] && docker logs "$CONTAINER" 2>&1 | grep -Fq 'MySQL init process done. Ready for start up.'; then boot=1; fi
  if [ "$boot" -eq 1 ] && sql 'SELECT 1' >/dev/null 2>&1; then sleep 1; if sql 'SELECT 1' >/dev/null 2>&1; then ready=1; break; fi; fi
  sleep 1
done
if [ "$ready" -ne 1 ]; then echo 'FAIL MySQL daemon not ready' >&2; exit 1; fi
case "$(sql 'SELECT VERSION()')" in 8.4.*) ;; *) echo 'FAIL MySQL version' >&2; exit 1 ;; esac
eq "enforced FK" "1" "$(sql 'SELECT @@FOREIGN_KEY_CHECKS')"
docker exec -i "$CONTAINER" mysql -uroot -D "$DB" < database/c04_02_shared_schema.sql
eq "four app tables" "4" "$(sql "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE() AND table_name IN ('c04_tenants','c04_branches','c04_resources','c04_resource_events') AND engine='InnoDB'")"
eq "3 restricted composite FKs" "3" "$(sql "SELECT COUNT(*) FROM information_schema.referential_constraints WHERE constraint_schema=DATABASE() AND constraint_name IN ('fk_c04_branch_tenant','fk_c04_resource_branch','fk_c04_event_resource') AND delete_rule='RESTRICT' AND update_rule='RESTRICT'")"
eq "four primary indexes tenant-prefixed" "4" "$(sql "SELECT COUNT(*) FROM information_schema.statistics WHERE table_schema=DATABASE() AND table_name IN ('c04_tenants','c04_branches','c04_resources','c04_resource_events') AND index_name='PRIMARY' AND seq_in_index=1 AND column_name='tenant_id'")"
eq "three FK tenant_id members" "3" "$(sql "SELECT COUNT(*) FROM information_schema.key_column_usage WHERE table_schema=DATABASE() AND constraint_name IN ('fk_c04_branch_tenant','fk_c04_resource_branch','fk_c04_event_resource') AND column_name='tenant_id' AND referenced_column_name='tenant_id'")"
sql "INSERT INTO c04_tenants (tenant_id,lifecycle_state) VALUES (X'$A',3),(X'$B',3)"
sql "INSERT INTO c04_branches (tenant_id,branch_id,display_label) VALUES (X'$A',X'$ABR','Branch A'),(X'$B',X'$BBR','Branch B')"
sql "INSERT INTO c04_resources (tenant_id,branch_id,resource_id,external_key) VALUES (X'$A',X'$ABR',X'$ITEM','shared'),(X'$B',X'$BBR',X'$ITEM','shared')"
sql "INSERT INTO c04_resource_events (tenant_id,branch_id,resource_id,event_id,event_kind) VALUES (X'$A',X'$ABR',X'$ITEM',X'$EV',1)"
eq "A isolated" "1" "$(sql "SELECT COUNT(*) FROM c04_resources WHERE tenant_id=X'$A' AND branch_id=X'$ABR'")"
eq "B isolated" "1" "$(sql "SELECT COUNT(*) FROM c04_resources WHERE tenant_id=X'$B' AND branch_id=X'$BBR'")"
eq "B predicate excludes A branch" "0" "$(sql "SELECT COUNT(*) FROM c04_resources WHERE tenant_id=X'$B' AND branch_id=X'$ABR'")"
eq "unscoped SELECT IS unsafe: returns both" "2" "$(sql "SELECT COUNT(*) FROM c04_resources")"
reject "A resource in B branch" "INSERT INTO c04_resources (tenant_id,branch_id,resource_id,external_key) VALUES (X'$A',X'$BBR',X'AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA','wrong')"
reject "B event bound to A resource" "INSERT INTO c04_resource_events (tenant_id,branch_id,resource_id,event_id,event_kind) VALUES (X'$B',X'$ABR',X'$ITEM',X'BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB',1)"
expect_immutable "cross-tenant resource reparent" "UPDATE c04_resources SET tenant_id=X'$B' WHERE tenant_id=X'$A' AND branch_id=X'$ABR' AND resource_id=X'$ITEM'"
reject "restrict delete event parent" "DELETE FROM c04_resources WHERE tenant_id=X'$A' AND branch_id=X'$ABR' AND resource_id=X'$ITEM'"
reject "same-scope external key duplicate" "INSERT INTO c04_resources (tenant_id,branch_id,resource_id,external_key) VALUES (X'$A',X'$ABR',X'CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC','shared')"
reject "unknown tenant" "INSERT INTO c04_branches (tenant_id,branch_id,display_label) VALUES (X'DDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDD',X'$ABR','Unknown')"
reject "zero tenant" "INSERT INTO c04_tenants (tenant_id,lifecycle_state) VALUES (X'$ZERO',3)"
reject "invalid state" "INSERT INTO c04_tenants (tenant_id,lifecycle_state) VALUES (X'EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE',9)"
reject "zero branch" "INSERT INTO c04_branches (tenant_id,branch_id,display_label) VALUES (X'$A',X'$ZERO','Zero branch')"
# A composite FK validates references but DOES NOT, by itself, forbid an
# UPDATE to another existing parent. These changes are valid without triggers.
eq "five ownership and append-only triggers" "5" "$(sql "SELECT COUNT(*) FROM information_schema.triggers WHERE trigger_schema=DATABASE() AND trigger_name LIKE 'trg_c04_%'")"
sql "INSERT INTO c04_branches (tenant_id,branch_id,display_label) VALUES (X'$A',X'77777777777777777777777777777777','A vacant branch')"
sql "INSERT INTO c04_resources (tenant_id,branch_id,resource_id,external_key) VALUES (X'$A',X'$ABR',X'99999999999999999999999999999999','a-transfer-target')"
sql "INSERT INTO c04_tenants (tenant_id,lifecycle_state) VALUES (X'AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA',3)"
expect_immutable "resource A to EXISTING B tenant and branch with no colliding ID" \
  "UPDATE c04_resources SET tenant_id=X'$B',branch_id=X'$BBR' WHERE tenant_id=X'$A' AND branch_id=X'$ABR' AND resource_id=X'99999999999999999999999999999999'"
expect_immutable "resource within tenant A to another VALID branch" \
  "UPDATE c04_resources SET branch_id=X'77777777777777777777777777777777' WHERE tenant_id=X'$A' AND branch_id=X'$ABR' AND resource_id=X'99999999999999999999999999999999'"
expect_immutable "empty A branch to EXISTING B tenant" \
  "UPDATE c04_branches SET tenant_id=X'$B' WHERE tenant_id=X'$A' AND branch_id=X'77777777777777777777777777777777'"
expect_immutable "tenant ID rekey without any dependent rows" \
  "UPDATE c04_tenants SET tenant_id=X'BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB' WHERE tenant_id=X'AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA'"
expect_immutable "event to existing foreign tenant/resource" \
  "UPDATE c04_resource_events SET tenant_id=X'$B',branch_id=X'$BBR' WHERE tenant_id=X'$A' AND branch_id=X'$ABR' AND resource_id=X'$ITEM' AND event_id=X'$EV'"
expect_immutable "append-only event content update" \
  "UPDATE c04_resource_events SET event_kind=2 WHERE tenant_id=X'$A' AND branch_id=X'$ABR' AND resource_id=X'$ITEM' AND event_id=X'$EV'"
expect_immutable "append-only event deletion" \
  "DELETE FROM c04_resource_events WHERE tenant_id=X'$A' AND branch_id=X'$ABR' AND resource_id=X'$ITEM' AND event_id=X'$EV'"
eq "A remains owner after denied valid-target reparent" "1" "$(sql "SELECT COUNT(*) FROM c04_resources WHERE tenant_id=X'$A' AND branch_id=X'$ABR' AND resource_id=X'99999999999999999999999999999999'")"
eq "event remains append-only after denied deletion" "1" "$(sql "SELECT COUNT(*) FROM c04_resource_events WHERE tenant_id=X'$A' AND branch_id=X'$ABR' AND resource_id=X'$ITEM' AND event_id=X'$EV'")"
echo "PASS C04-02 APPLICATION DDL: $positive positive and $negative negative checks; NO application auth certified."
