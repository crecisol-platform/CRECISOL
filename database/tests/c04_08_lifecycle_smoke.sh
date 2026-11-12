#!/usr/bin/env bash
# CRECISOL C04-08 ONLY: synthetic MySQL 8.4 InnoDB test on an isolated
# disposable container. No production host, secrets, ports or migrations.
set -euo pipefail
IMAGE="mysql:8.4@sha256:a81d043930e51745522bd5c430961cf8286f0a23f0a9a2a6e0108f7ea71b0902"
CONTAINER="crecisol-c04-08-$$-$RANDOM"
DB="c04_08_disposable"
TMP="$(mktemp -d)"
PASS=0
DENIED=0
cleanup() { docker rm -f "$CONTAINER" >/dev/null 2>&1 || true; rm -rf "$TMP"; }
trap cleanup EXIT

root_sql() {
  docker exec "$CONTAINER" mysql -uroot --batch --skip-column-names -D "$DB" -e "$1"
}
check() {
  local label="$1" expected="$2" actual="$3"
  if [ "$expected" != "$actual" ]; then
    echo "FAIL C04-08 $label (expected=$expected actual=$actual)" >&2
    exit 1
  fi
  PASS=$((PASS+1))
  echo "PASS C04-08 $label"
}
deny() {
  local label="$1" sql="$2" output
  if output=$(root_sql "$sql" 2>&1); then
    echo "FAIL C04-08 unsafe operation accepted: $label ($output)" >&2
    exit 1
  fi
  case "$output" in *"ERROR 1644"*) ;; *)
    echo "FAIL C04-08 wrong SQL error: $label ($output)" >&2; exit 1 ;;
  esac
  DENIED=$((DENIED+1))
  echo "PASS C04-08 rejected: $label"
}
A="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
B="bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
C="cccccccccccccccccccccccccccccccc"
ACTOR="dddddddddddddddddddddddddddddddd"
SHA="$(printf 'a%.0s' {1..64})"
operation() { printf '%032x' "$1"; }
decision() {
  local op="$1" tenant="$2" action="$3" rev="$4" epoch="$5" approved="${6:-1}"
  root_sql "INSERT INTO c04_08_decisions
    (operation_id,tenant_id,actor_id,action,expected_revision,expected_epoch,
     reason_code,intent_fingerprint,approved,approved_until_utc)
    VALUES ('$op','$tenant','$ACTOR','$action',$rev,$epoch,
      'SECURITY_REVIEW','$SHA',$approved,DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 1 DAY));"
}
# Pulling the pinned public test image is permitted, but the instance itself
# has NO network, published ports or real credentials.
docker run --rm -d --network none --name "$CONTAINER" \
  -e MYSQL_ALLOW_EMPTY_PASSWORD=yes -e MYSQL_DATABASE="$DB" "$IMAGE" >/dev/null
ready=0
boot=0
for _ in $(seq 1 120); do
  if [ "$boot" -eq 0 ] &&
     docker logs "$CONTAINER" 2>&1 | grep -Fq 'MySQL init process done. Ready for start up.'; then
    boot=1
  fi
  if [ "$boot" -eq 1 ] && root_sql "SELECT 1" >/dev/null 2>&1; then
    ready=1; break
  fi
  sleep 1
done
if [ "$ready" -ne 1 ]; then echo "FAIL C04-08 MySQL boot" >&2; exit 1; fi
case "$(root_sql 'SELECT VERSION()')" in 8.4.*) ;; *)
  echo 'FAIL requires MySQL 8.4' >&2; exit 1 ;;
esac
docker exec -i "$CONTAINER" mysql -uroot -D "$DB" \
  < database/tests/c04_08_lifecycle_fixture.sql
check "four disposable InnoDB tables" "4" "$(root_sql "
  SELECT COUNT(*) FROM information_schema.tables
  WHERE table_schema=DATABASE() AND engine='InnoDB'
  AND table_name LIKE 'c04_08_%'")"
check "four immutable audit/receipt triggers" "4" "$(root_sql "
  SELECT COUNT(*) FROM information_schema.triggers
  WHERE trigger_schema=DATABASE() AND trigger_name LIKE 'trg_c04_08_%'")"
root_sql "INSERT INTO c04_08_tenants VALUES
 ('$A','Active',7,11,'RETAIN-A'),
 ('$B','Active',5,8,'RETAIN-B'),
 ('$C','Active',2,2,'RETAIN-C');"
O1="$(operation 101)"
decision "$O1" "$A" "Suspend" 7 11

# Twelve real concurrent clients targeting the exact SAME authorized
# operation must produce ONE APPLIED and eleven immutable REPLAY receipts.
pids=()
for i in $(seq 1 12); do
  (root_sql "CALL c04_08_transition('$O1');" > "$TMP/same-$i" 2>&1) &
  pids+=("$!")
done
for pid in "${pids[@]}"; do
  if ! wait "$pid"; then
    echo "FAIL same-operation concurrent request" >&2
    cat "$TMP"/same-* >&2
    exit 1
  fi
done
applied="$(grep -lx 'APPLIED' "$TMP"/same-* | wc -l)"
replays="$(grep -lx 'REPLAY' "$TMP"/same-* | wc -l)"
check "one applied under twelve real callers" "1" "$applied"
check "eleven exact replays" "11" "$replays"
check "tenant suspended, epoch and revision bumped once" "Suspended:8:12" "$(root_sql "
 SELECT CONCAT(lifecycle_state,':',revision,':',access_epoch)
 FROM c04_08_tenants WHERE tenant_id='$A'")"
check "one immutable audit event" "1" "$(root_sql "
 SELECT COUNT(*) FROM c04_08_audit WHERE tenant_id='$A'")"
check "retained payload not deleted" "RETAIN-A" "$(root_sql "
 SELECT retained_payload FROM c04_08_tenants WHERE tenant_id='$A'")"
check "same operation after response loss replays" "REPLAY" "$(root_sql "
 CALL c04_08_transition('$O1')")"

O2="$(operation 102)"
decision "$O2" "$A" "Reactivate" 8 12
check "authorized reactivation" "APPLIED" "$(root_sql "CALL c04_08_transition('$O2')")"
check "reactivation advances revision and epoch" "Active:9:13" "$(root_sql "
 SELECT CONCAT(lifecycle_state,':',revision,':',access_epoch)
 FROM c04_08_tenants WHERE tenant_id='$A'")"
O3="$(operation 103)"
decision "$O3" "$A" "Deactivate" 9 13
check "logical deactivation" "APPLIED" "$(root_sql "CALL c04_08_transition('$O3')")"
check "logical deletion keeps row and data" "Deactivated:RETAIN-A" "$(root_sql "
 SELECT CONCAT(lifecycle_state,':',retained_payload) FROM c04_08_tenants WHERE tenant_id='$A'")"
O4="$(operation 104)"
decision "$O4" "$A" "Reactivate" 10 14
deny "deactivated cannot reactivate" "CALL c04_08_transition('$O4')"
check "denied reactivation has no audit" "0" "$(root_sql "
 SELECT COUNT(*) FROM c04_08_audit WHERE operation_id='$O4'")"

# Two DIFFERENT authorized decisions racing on same previous revision.
O5="$(operation 105)"
O6="$(operation 106)"
decision "$O5" "$B" "Suspend" 5 8
decision "$O6" "$B" "Suspend" 5 8
root_sql "CALL c04_08_transition('$O5');" > "$TMP/race-1" 2>&1 &
p1="$!"
root_sql "CALL c04_08_transition('$O6');" > "$TMP/race-2" 2>&1 &
p2="$!"
succeeded=0
failed=0
if wait "$p1"; then succeeded=$((succeeded+1)); else failed=$((failed+1)); fi
if wait "$p2"; then succeeded=$((succeeded+1)); else failed=$((failed+1)); fi
check "CAS race one transition succeeds" "1" "$succeeded"
check "CAS race other transition denied" "1" "$failed"
check "CAS race one audit" "1" "$(root_sql "
 SELECT COUNT(*) FROM c04_08_audit WHERE tenant_id='$B'")"
check "CAS race exactly one epoch bump" "Suspended:6:9" "$(root_sql "
 SELECT CONCAT(lifecycle_state,':',revision,':',access_epoch)
 FROM c04_08_tenants WHERE tenant_id='$B'")"
check "other tenant unchanged by CAS race" "Deactivated:10:14" "$(root_sql "
 SELECT CONCAT(lifecycle_state,':',revision,':',access_epoch)
 FROM c04_08_tenants WHERE tenant_id='$A'")"
if grep -q '^ERROR 1644' "$TMP/race-1" "$TMP/race-2"; then
  DENIED=$((DENIED+1))
else
  echo 'FAIL C04-08 losing caller did not return semantic conflict' >&2; exit 1
fi

# All state/audit writes must roll back on a synthetic failure before commit.
O7="$(operation 107)"
decision "$O7" "$C" "Suspend" 2 2
deny "precommit failure rolls back" "SET @c04_08_fail_before_commit=1;
 CALL c04_08_transition('$O7')"
check "rollback preserves state and epoch" "Active:2:2" "$(root_sql "
 SELECT CONCAT(lifecycle_state,':',revision,':',access_epoch)
 FROM c04_08_tenants WHERE tenant_id='$C'")"
check "rollback emitted no audit" "0" "$(root_sql "
 SELECT COUNT(*) FROM c04_08_audit WHERE operation_id='$O7'")"
check "retry after rollback applies once" "APPLIED" "$(root_sql "
 CALL c04_08_transition('$O7')")"

O8="$(operation 108)"
decision "$O8" "$C" "Reactivate" 3 3 0
deny "explicitly revoked decision" "CALL c04_08_transition('$O8')"
O9="$(operation 109)"
decision "$O9" "$C" "Reactivate" 3 3
root_sql "UPDATE c04_08_decisions SET approved_until_utc =
 DATE_SUB(UTC_TIMESTAMP(6),INTERVAL 1 SECOND) WHERE operation_id='$O9'"
deny "expired decision" "CALL c04_08_transition('$O9')"
deny "unknown operation" "CALL c04_08_transition('ffffffffffffffffffffffffffffffff')"
deny "malformed operation" "CALL c04_08_transition('not-a-uuid')"

deny "append-only audit blocks updates" "UPDATE c04_08_audit SET
 reason_code='TAMPER' WHERE operation_id='$O1'"
deny "append-only audit blocks deletion" "DELETE FROM c04_08_audit
 WHERE operation_id='$O1'"
deny "immutable receipt blocks deletion" "DELETE FROM c04_08_receipts
 WHERE operation_id='$O1'"
check "no physical deletion anywhere" "3" "$(root_sql "
 SELECT COUNT(*) FROM c04_08_tenants")"
echo "C04-08 disposable MySQL8.4: $PASS positive and $DENIED negative checks."
echo "LIMIT: no authenticated backoffice, real session/cache/worker invalidation, production tables or HTTP."
