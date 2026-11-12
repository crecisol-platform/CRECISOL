#!/usr/bin/env bash
# CRECISOL C04-07: REAL isolated MySQL 8.4 InnoDB contention tests.
# Pure CI fixture. No production credentials, ports, external networks,
# schema migration or real provisioning; container and temp files cleaned.
set -euo pipefail
IMAGE="mysql:8.4@sha256:a81d043930e51745522bd5c430961cf8286f0a23f0a9a2a6e0108f7ea71b0902"
CONTAINER="crecisol-c04-07-ci-$RANDOM-$$"
DB="c04_07_disposable"
TMP="$(mktemp -d)"
PASS=0
DENIED=0
cleanup() { docker rm -f "$CONTAINER" >/dev/null 2>&1 || true; rm -rf "$TMP"; }
trap cleanup EXIT

A="11111111111111111111111111111111"
TEN_A="AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA"
ACT_A="BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB"
ACT_B="CCCCCCCCCCCCCCCCCCCCCCCCCCCCCCCC"
OP_B="22222222222222222222222222222222"
TEN_B="DDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDD"
DIGEST_A="$(printf 'a%.0s' $(seq 1 64))"
DIGEST_B="$(printf 'b%.0s' $(seq 1 64))"
CI_PASSWORD="$(openssl rand -hex 24)"
if [ -n "${GITHUB_ACTIONS:-}" ]; then
  printf '::add-mask::%s\n' "$CI_PASSWORD"
fi
root_sql() {
  docker exec "$CONTAINER" mysql -uroot --batch --skip-column-names -D "$DB" -e "$1"
}
runner_sql() {
  docker exec -e MYSQL_PWD="$CI_PASSWORD" "$CONTAINER" \
    mysql -uc04_07_ci_runner --batch --skip-column-names -D "$DB" -e "$1"
}
call_sql() {
  local operation="$1" tenant="$2" actor="$3" key="$4" mode="$5" digest="$6"
  printf "CALL c04_07_create_or_replay(X'%s',X'%s',X'%s','%s',%s,X'%s');" \
    "$operation" "$tenant" "$actor" "$key" "$mode" "$digest"
}
eq() {
  local label="$1" wanted="$2" got="$3"
  if [ "$wanted" != "$got" ]; then
    echo "FAIL C04-07: $label (want=$wanted got=$got)" >&2
    exit 1
  fi
  PASS=$((PASS+1))
  echo "PASS C04-07: $label"
}
denied() {
  local label="$1" statement="$2" output
  if output=$(runner_sql "$statement" 2>&1); then
    echo "FAIL C04-07 accepted: $label ($output)" >&2
    exit 1
  fi
  case "$output" in
    *"ERROR 1644"*) ;;
    *) echo "FAIL C04-07 wrong error for $label: $output" >&2; exit 1 ;;
  esac
  DENIED=$((DENIED+1))
  echo "PASS C04-07 rejected: $label"
}

docker run --rm -d --network none --name "$CONTAINER" \
  -e MYSQL_ALLOW_EMPTY_PASSWORD=yes -e MYSQL_DATABASE="$DB" "$IMAGE" >/dev/null
boot=0
ready=0
for _ in $(seq 1 120); do
  if [ "$boot" -eq 0 ] &&
     docker logs "$CONTAINER" 2>&1 | grep -Fq 'MySQL init process done. Ready for start up.'; then
    boot=1
  fi
  if [ "$boot" -eq 1 ] && root_sql 'SELECT 1' >/dev/null 2>&1; then
    sleep 1
    if root_sql 'SELECT 1' >/dev/null 2>&1; then ready=1; break; fi
  fi
  sleep 1
done
if [ "$ready" -ne 1 ]; then echo 'FAIL C04-07 MySQL bootstrap' >&2; exit 1; fi
case "$(root_sql 'SELECT VERSION()')" in 8.4.*) ;; *) echo 'FAIL requires MySQL 8.4' >&2; exit 1 ;; esac
eq "real MySQL transaction isolation enabled" "REPEATABLE-READ" \
  "$(root_sql 'SELECT @@transaction_isolation')"
docker exec -i "$CONTAINER" mysql -uroot -D "$DB" \
  < database/tests/c04_07_idempotent_fixture.sql

eq "two independent InnoDB tables" "2" "$(root_sql \
  "SELECT COUNT(*) FROM information_schema.tables
    WHERE table_schema=DATABASE() AND table_name IN
    ('c04_07_operations','c04_07_tenant_drafts') AND engine='InnoDB'")"
eq "unique keys op, tenant, business, draft links" "6" "$(root_sql \
  "SELECT COUNT(DISTINCT CONCAT(table_name, '.', index_name))
     FROM information_schema.statistics
    WHERE table_schema=DATABASE() AND table_name IN
     ('c04_07_operations','c04_07_tenant_drafts')
      AND non_unique=0")"
eq "two-table strict FK on operation" "1" "$(root_sql \
  "SELECT COUNT(*) FROM information_schema.referential_constraints
    WHERE constraint_schema=DATABASE() AND constraint_name='fk_c04_07_draft_operation'
      AND update_rule='RESTRICT' AND delete_rule='RESTRICT'")"
eq "four append-only triggers" "4" "$(root_sql \
  "SELECT COUNT(*) FROM information_schema.triggers WHERE trigger_schema=DATABASE()
      AND trigger_name LIKE 'trg_c04_07_%'")"

root_sql "CREATE USER 'c04_07_ci_runner'@'localhost' IDENTIFIED BY '$CI_PASSWORD'"
root_sql "GRANT EXECUTE ON PROCEDURE \`$DB\`.c04_07_create_or_replay TO 'c04_07_ci_runner'@'localhost'"
# The fixture runner cannot directly insert/read/reparent draft tenant rows.
if runner_sql "SELECT COUNT(*) FROM c04_07_operations" >/dev/null 2>&1; then
  echo 'FAIL C04-07 ci user can enumerate approvals' >&2; exit 1
fi
PASS=$((PASS+1))
if runner_sql "INSERT INTO c04_07_operations(operation_id) VALUES (X'$A')" >/dev/null 2>&1; then
  echo 'FAIL C04-07 direct table write allowed' >&2; exit 1
fi
PASS=$((PASS+1))

CALL_A="$(call_sql "$A" "$TEN_A" "$ACT_A" 'alpha-internal' 2 "$DIGEST_A")"
eq "first atomic tenant draft" "CREATED" "$(runner_sql "$CALL_A")"
eq "identical retry returns same operation" "REPLAY" "$(runner_sql "$CALL_A")"
eq "one operation row" "1" "$(root_sql 'SELECT COUNT(*) FROM c04_07_operations')"
eq "one draft tenant row" "1" "$(root_sql 'SELECT COUNT(*) FROM c04_07_tenant_drafts')"
eq "draft never becomes Active" "1" "$(root_sql \
  'SELECT lifecycle_state FROM c04_07_tenant_drafts LIMIT 1')"
denied "same operation with changed fingerprint" \
  "$(call_sql "$A" "$TEN_A" "$ACT_A" 'alpha-internal' 2 "$DIGEST_B")"
denied "same operation with another tenant" \
  "$(call_sql "$A" "$TEN_B" "$ACT_A" 'alpha-internal' 2 "$DIGEST_A")"
denied "same operation with different sponsor" \
  "$(call_sql "$A" "$TEN_A" "$ACT_B" 'alpha-internal' 2 "$DIGEST_A")"
denied "same operation with different address mode" \
  "$(call_sql "$A" "$TEN_A" "$ACT_A" 'alpha-internal' 3 "$DIGEST_A")"
denied "same operation with another internal business key" \
  "$(call_sql "$A" "$TEN_A" "$ACT_A" 'attacker-key' 2 "$DIGEST_A")"
denied "new operation may not clone business identity" \
  "$(call_sql "$OP_B" "$TEN_B" "$ACT_A" 'alpha-internal' 2 "$DIGEST_B")"
denied "new operation may not reassign tenant id" \
  "$(call_sql "$OP_B" "$TEN_A" "$ACT_A" 'another-business' 2 "$DIGEST_B")"
denied "zero operation id rejected" \
  "$(call_sql "00000000000000000000000000000000" "$TEN_B" "$ACT_A" 'invalid-key' 2 "$DIGEST_B")"
denied "invalid address mode rejected" \
  "$(call_sql "$OP_B" "$TEN_B" "$ACT_A" 'invalid-key' 9 "$DIGEST_B")"
denied "uppercase noncanonical business key rejected" \
  "$(call_sql "$OP_B" "$TEN_B" "$ACT_A" 'Bad-Business' 2 "$DIGEST_B")"

# Simulated crash occurs AFTER operation INSERT but BEFORE draft INSERT:
# it must ROLLBACK both rows; retry can commit normally.
CRASH_OP="33333333333333333333333333333333"
CRASH_TENANT="44444444444444444444444444444444"
CALL_CRASH="$(call_sql "$CRASH_OP" "$CRASH_TENANT" "$ACT_A" 'retry-after-crash' 1 "$DIGEST_A")"
denied "synthetic mid-transaction crash forces rollback" \
  "SET @c04_07_ci_fail_before_commit=1; $CALL_CRASH"
eq "crashed transaction left no operation" "0" "$(root_sql \
  "SELECT COUNT(*) FROM c04_07_operations WHERE operation_id=X'$CRASH_OP'")"
eq "crashed transaction left no tenant" "0" "$(root_sql \
  "SELECT COUNT(*) FROM c04_07_tenant_drafts WHERE tenant_id=X'$CRASH_TENANT'")"
eq "retry after crash commits once" "CREATED" "$(runner_sql "$CALL_CRASH")"
# The application may lose the reply AFTER COMMIT; replay must return same row.
runner_sql "$CALL_CRASH" >/dev/null
eq "lost response after commit returns replay" "REPLAY" "$(runner_sql "$CALL_CRASH")"

# Force actual overlapping transactions on SAME key, not just serial retries.
RACE_OP="55555555555555555555555555555555"
RACE_TENANT="66666666666666666666666666666666"
RACE_CALL="$(call_sql "$RACE_OP" "$RACE_TENANT" "$ACT_A" 'concurrent-tenant' 3 "$DIGEST_B")"
runner_sql "SET @c04_07_ci_hold_lock=1; $RACE_CALL" > "$TMP/same-0" 2>&1 &
pids=("$!")
sleep 0.1
for n in $(seq 1 11); do
  runner_sql "$RACE_CALL" > "$TMP/same-$n" 2>&1 &
  pids+=("$!")
done
for pid in "${pids[@]}"; do
  if ! wait "$pid"; then echo 'FAIL C04-07 same-operation concurrent caller' >&2; exit 1; fi
done
created=0
replay=0
for n in $(seq 0 11); do
  case "$(cat "$TMP/same-$n")" in
    CREATED) created=$((created+1));;
    REPLAY) replay=$((replay+1));;
    *) echo "FAIL C04-07 unexpected same-key race result" >&2; exit 1;;
  esac
done
eq "12 same-key calls yielded exactly one CREATED" "1" "$created"
eq "11 duplicate calls yielded REPLAY" "11" "$replay"
eq "same-key race one committed operation" "1" "$(root_sql \
  "SELECT COUNT(*) FROM c04_07_operations WHERE operation_id=X'$RACE_OP'")"
eq "same-key race one committed draft" "1" "$(root_sql \
  "SELECT COUNT(*) FROM c04_07_tenant_drafts WHERE tenant_id=X'$RACE_TENANT'")"

# Different operation IDs concurrently reserving SAME canonical business key
# must yield ONE tenant and eleven 1644 conflicts. The business UNIQUE index,
# not the idempotency operation, prevents duplicate global identities.
pids=()
for n in $(seq 1 12); do
  op="$(printf '%032x' "$((500+n))")"
  ten="$(printf '%032x' "$((700+n))")"
  runner_sql "$(call_sql "$op" "$ten" "$ACT_A" 'collision-business' 1 "$DIGEST_A")" \
    > "$TMP/diff-$n" 2>&1 &
  pids+=("$!")
done
succeeded=0
failed=0
for pid in "${pids[@]}"; do
  if wait "$pid"; then succeeded=$((succeeded+1)); else failed=$((failed+1)); fi
done
eq "different keys same business: one winner" "1" "$succeeded"
eq "different keys same business: 11 conflicts" "11" "$failed"
for n in $(seq 1 12); do
  case "$(cat "$TMP/diff-$n")" in
    CREATED) ;;
    *"ERROR 1644"*) ;;
    *) echo "FAIL C04-07 unexpected different-key concurrency result" >&2; exit 1;;
  esac
done
eq "no duplicate business identity created" "1" "$(root_sql \
  "SELECT COUNT(*) FROM c04_07_tenant_drafts WHERE business_key='collision-business'")"

# Post-commit identity, tenant and business identity are append-only even if
# an attacker can satisfy a different existing unique target.
immutability() {
  local label="$1" statement="$2" output
  if output=$(root_sql "$statement" 2>&1); then
    echo "FAIL C04-07 immutable attack accepted: $label" >&2; exit 1
  fi
  case "$output" in *"ERROR 1644"*) ;; *)
    echo "FAIL C04-07 wrong immutability error: $label ($output)" >&2; exit 1;;
  esac
  DENIED=$((DENIED+1))
}
immutability "operation tenant rekey" \
  "UPDATE c04_07_operations SET tenant_id=X'$TEN_B' WHERE operation_id=X'$A'"
immutability "tenant draft business rekey" \
  "UPDATE c04_07_tenant_drafts SET business_key='danger' WHERE tenant_id=X'$TEN_A'"
immutability "operation deletion" \
  "DELETE FROM c04_07_operations WHERE operation_id=X'$A'"
immutability "tenant deletion" \
  "DELETE FROM c04_07_tenant_drafts WHERE tenant_id=X'$TEN_A'"
eq "original row survived all forbidden mutations" "1" "$(root_sql \
  "SELECT COUNT(*) FROM c04_07_tenant_drafts
    WHERE tenant_id=X'$TEN_A' AND business_key='alpha-internal' AND address_mode=2")"

echo "PASS C04-07 MySQL8.4: $PASS positive and $DENIED rejected cases;"
echo "PASS C04-07 real InnoDB contention: same-key 1 created/11 replay; distinct keys one business 1 created/11 rejected; crash rollback and retry."
echo "LIMIT: disposable synthetic SQL fixture, no production admin auth, contracts, billing, host/DNS reservation, DB adapter, HTTP or deployment."
