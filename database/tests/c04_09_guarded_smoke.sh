#!/usr/bin/env bash
# CRECISOL C04-09 D/E/F: real disposable MySQL 8.4 scope and recovery tests.
# No production connections, deployment, clients, ports or external network.
set -euo pipefail
IMAGE="mysql:8.4@sha256:a81d043930e51745522bd5c430961cf8286f0a23f0a9a2a6e0108f7ea71b0902"
CONTAINER="crecisol-c04-09-guard-$RANDOM-$$"
TMP="$(mktemp -d)"
PASSWORD="$(openssl rand -hex 24)"
PASS=0
DENIED=0
cleanup() { docker rm -f "$CONTAINER" >/dev/null 2>&1 || true; rm -rf "$TMP"; }
trap cleanup EXIT
root_sql() { docker exec "$CONTAINER" mysql -uroot -N -B -e "$1"; }
runner_sql() {
  docker exec -e MYSQL_PWD="$PASSWORD" "$CONTAINER" \
    mysql -uc0409_ci_runner -N -B -e "$1"
}
# Synthetic operator-only restoration: scope comes from protected ledger
# and is checked BEFORE any DROP/CREATE or backup import. Not a production
# restoration authorization or a DB-routing implementation.
restore_checked() {
  local op="$1" target_schema="$2" backup="$3" expected binding
  binding="$(root_sql "SELECT CONCAT(t.kind,':',t.tenant_id,':',t.slot_id,':',
      t.schema_name,':',l.fence)
    FROM c04_09_control.ledger l
    JOIN c04_09_control.targets t ON t.target_id=l.target_id
    JOIN c04_09_control.gates g ON g.target_id=l.target_id
    WHERE l.operation_id='$op' AND l.state='failed'
      AND g.inflight_operation=l.operation_id AND g.fence=l.fence")"
  expected="DEDICATED:B:slot_b:c04_09_ded_b:1"
  if [ "$binding" != "$expected" ] ||
     [ "$target_schema" != "c04_09_ded_b" ] ||
     [ ! -s "$backup" ]; then
    echo 'DENIED checked backup target or fencing mismatch' >&2
    return 1
  fi
  root_sql "DROP DATABASE c04_09_ded_b; CREATE DATABASE c04_09_ded_b"
  docker exec -i "$CONTAINER" mysql -uroot -D c04_09_ded_b < "$backup"
}
check() {
  local label="$1" expected="$2" actual="$3"
  if [ "$expected" != "$actual" ]; then
    echo "FAIL C04-09 guarded $label expected=$expected actual=$actual" >&2
    exit 1
  fi
  PASS=$((PASS+1)); echo "PASS guarded $label"
}
deny() {
  local label="$1" statement="$2" output
  if output="$(runner_sql "$statement" 2>&1)"; then
    echo "FAIL C04-09 guarded accepted $label: $output" >&2; exit 1
  fi
  case "$output" in
    *"ERROR 1644"*|*"ERROR 1142"*|*"ERROR 1143"*|*"ERROR 1044"*)
      ;;
    *)
      echo "FAIL C04-09 wrong denial $label: $output" >&2; exit 1
      ;;
  esac
  DENIED=$((DENIED+1)); echo "PASS guarded denied $label"
}
deny_root() {
  local label="$1" statement="$2" output
  if output="$(root_sql "$statement" 2>&1)"; then
    echo "FAIL C04-09 root adversary accepted $label: $output" >&2; exit 1
  fi
  case "$output" in *"ERROR 1644"*|*"ERROR 1062"*) ;; *)
    echo "FAIL wrong SQL rejection for $label: $output" >&2; exit 1 ;;
  esac
  DENIED=$((DENIED+1))
  echo "PASS guarded denied $label"
}
runner_sql_retry() {
  local statement="$1" output attempt pause
  for attempt in $(seq 1 8); do
    if output="$(runner_sql "$statement" 2>&1)"; then
      printf '%s\n' "$output"
      return 0
    fi
    case "$output" in
      *"ERROR 1213"*|*"ERROR 1205"*)
        pause="$(printf '0.%03d' "$((attempt*40 + RANDOM % 20))")"
        sleep "$pause"
        ;;
      *)
        printf '%s\n' "$output" >&2
        return 1
        ;;
    esac
  done
  echo "FAIL guarded transient retry budget exceeded" >&2
  return 1
}
OP() { printf '%032x' "$1"; }
HASH_A="$(printf 'a%.0s' {1..64})"
HASH_B="$(printf 'b%.0s' {1..64})"
decision() {
  local operation="$1" target="$2" kind="$3" tenant="$4" slot="$5"
  local rev="$6" epoch="$7" version="$8" checksum="$9"
  local approved="${10:-1}" expiry="${11:-1}"
  local t_sql="NULL" s_sql="NULL"
  if [ "$tenant" != "NULL" ]; then t_sql="'$tenant'"; fi
  if [ "$slot" != "NULL" ]; then s_sql="'$slot'"; fi
  root_sql "INSERT INTO c04_09_control.approvals
  (operation_id,target_id,expected_kind,expected_tenant,expected_slot,
   expected_revision,expected_epoch,migration_version,checksum,
   approved,approved_until_utc) VALUES (
   '$operation','$target','$kind',$t_sql,$s_sql,
   $rev,$epoch,'$version','$checksum',$approved,
   DATE_ADD(UTC_TIMESTAMP(6),INTERVAL $expiry DAY));"
}
if [ -n "${GITHUB_ACTIONS:-}" ]; then
  printf '::add-mask::%s\n' "$PASSWORD"
fi
docker run --rm -d --network none --name "$CONTAINER" \
  -e MYSQL_ALLOW_EMPTY_PASSWORD=yes "$IMAGE" >/dev/null
boot=0
ready=0
for _ in $(seq 1 120); do
  if [ "$boot" -eq 0 ] &&
     docker logs "$CONTAINER" 2>&1 | grep -Fq 'MySQL init process done. Ready for start up.'; then
    boot=1
  fi
  if [ "$boot" -eq 1 ] && root_sql 'SELECT 1' >/dev/null 2>&1; then
    ready=1; break
  fi
  sleep 1
done
if [ "$ready" -eq 0 ]; then echo "FAIL guarded MySQL bootstrap"; exit 1; fi
case "$(root_sql 'SELECT VERSION()')" in 8.4.*) ;; *)
  echo "FAIL guarded MySQL 8.4 required" >&2; exit 1 ;;
esac

root_sql "CREATE DATABASE c04_09_shared;
CREATE DATABASE c04_09_ded_a;
CREATE DATABASE c04_09_ded_b;
CREATE TABLE c04_09_shared.resources (
 tenant_id CHAR(1) NOT NULL, resource_id INT NOT NULL,
 payload VARCHAR(30) NOT NULL,
 PRIMARY KEY(tenant_id,resource_id)
) ENGINE=InnoDB;
CREATE TABLE c04_09_ded_a.resources (
 resource_id INT PRIMARY KEY, payload VARCHAR(30) NOT NULL
) ENGINE=InnoDB;
CREATE TABLE c04_09_ded_b.resources LIKE c04_09_ded_a.resources;
INSERT INTO c04_09_shared.resources VALUES('A',1,'ALPHA'),('A',2,'ALPHA-SECOND'),('B',1,'BETA');
INSERT INTO c04_09_ded_a.resources VALUES(1,'DED-ALPHA');
INSERT INTO c04_09_ded_b.resources VALUES(1,'DED-BETA');"
docker exec -i "$CONTAINER" mysql -uroot \
  < database/tests/c04_09_guarded_fixture.sql
root_sql "INSERT INTO c04_09_control.targets VALUES
 ('SHARED0','SHARED',NULL,NULL,'c04_09_shared',17,4),
 ('DED_A','DEDICATED','A','slot_a','c04_09_ded_a',3,3),
 ('DED_B','DEDICATED','B','slot_b','c04_09_ded_b',7,8);
 INSERT INTO c04_09_control.gates(target_id) VALUES
 ('SHARED0'),('DED_A'),('DED_B');
 CREATE USER 'c0409_ci_runner'@'localhost' IDENTIFIED BY '$PASSWORD';
 GRANT EXECUTE ON PROCEDURE c04_09_control.reserve TO 'c0409_ci_runner'@'localhost';
 GRANT EXECUTE ON PROCEDURE c04_09_control.finalize TO 'c0409_ci_runner'@'localhost';"
check "three protected target bindings" "3" "$(root_sql "
 SELECT COUNT(*) FROM c04_09_control.targets")"
check "globally shared schema has no tenant selector" "1" "$(root_sql "
 SELECT COUNT(*) FROM c04_09_control.targets
 WHERE target_id='SHARED0' AND kind='SHARED' AND tenant_id IS NULL AND slot_id IS NULL")"
check "two dedicated slots are disjoint" "2" "$(root_sql "
 SELECT COUNT(DISTINCT slot_id) FROM c04_09_control.targets WHERE kind='DEDICATED'")"
if runner_sql "SELECT * FROM c04_09_control.approvals" >/dev/null 2>&1; then
  echo 'FAIL unprivileged migration runner can enumerate protected approvals' >&2; exit 1
fi
PASS=$((PASS+1))
deny "runner cannot alter another dedicated schema" \
 "ALTER TABLE c04_09_ded_b.resources ADD COLUMN malicious INT"
deny "runner cannot perform destructive DDL downgrade" \
 "ALTER TABLE c04_09_ded_b.resources DROP COLUMN payload"
deny "runner cannot choose scope via update" \
 "UPDATE c04_09_control.targets SET slot_id='slot_a' WHERE target_id='DED_B'"
deny_root "valid tenant B cannot steal noncolliding tenant A resource" \
 "UPDATE c04_09_shared.resources SET tenant_id='B'
 WHERE tenant_id='A' AND resource_id=2"
deny_root "dedicated slot cannot be transferred to another valid tenant" \
 "UPDATE c04_09_control.targets SET tenant_id='A',slot_id='slot_a' WHERE target_id='DED_B'"
deny_root "different valid tenant cannot claim same dedicated slot" \
 "INSERT INTO c04_09_control.targets VALUES
 ('ATTACK','DEDICATED','B','slot_a','c04_09_ded_b2',1,1)"

# Valid B and valid slot B exist: the reject is for ownership mismatch,
# not for a missing/nonexistent tenant or slot.
O_BAD_TENANT="$(OP 101)"
O_BAD_SLOT="$(OP 102)"
O_STALE_REV="$(OP 103)"
O_STALE_EPOCH="$(OP 104)"
O_REVOKED="$(OP 105)"
O_EXPIRED="$(OP 106)"
O_BAD_SHARED="$(OP 107)"
decision "$O_BAD_TENANT" DED_A DEDICATED B slot_a 3 3 schema_bad_1 "$HASH_A"
decision "$O_BAD_SLOT" DED_A DEDICATED A slot_b 3 3 schema_bad_2 "$HASH_A"
decision "$O_STALE_REV" DED_A DEDICATED A slot_a 4 3 schema_bad_3 "$HASH_A"
decision "$O_STALE_EPOCH" DED_A DEDICATED A slot_a 3 4 schema_bad_4 "$HASH_A"
decision "$O_REVOKED" DED_A DEDICATED A slot_a 3 3 schema_bad_5 "$HASH_A" 0
decision "$O_EXPIRED" DED_A DEDICATED A slot_a 3 3 schema_bad_6 "$HASH_A" 1 -1
decision "$O_BAD_SHARED" SHARED0 SHARED B NULL 17 4 schema_bad_7 "$HASH_A"
deny "cross-tenant valid-B approval mismatches dedicated-A" \
 "CALL c04_09_control.reserve('$O_BAD_TENANT')"
deny "cross-slot valid-slot-B approval mismatches dedicated-A" \
 "CALL c04_09_control.reserve('$O_BAD_SLOT')"
deny "stale placement revision" \
 "CALL c04_09_control.reserve('$O_STALE_REV')"
deny "stale access epoch" \
 "CALL c04_09_control.reserve('$O_STALE_EPOCH')"
deny "revoked approval" \
 "CALL c04_09_control.reserve('$O_REVOKED')"
deny "expired approval" \
 "CALL c04_09_control.reserve('$O_EXPIRED')"
deny "shared global schema must not contain tenant selector" \
 "CALL c04_09_control.reserve('$O_BAD_SHARED')"
deny "unknown operation identifier" \
 "CALL c04_09_control.reserve('ffffffffffffffffffffffffffffffff')"
deny "malformed operation identifier" \
 "CALL c04_09_control.reserve('notanoperation')"
check "denied requests created zero ledger rows" "0" "$(root_sql "
 SELECT COUNT(*) FROM c04_09_control.ledger")"

# Global SHARED schema DDL affects BOTH A and B; separate approval gate
# cannot be confused with a tenant-specific dedicated approval.
O_SHARED="$(OP 201)"
decision "$O_SHARED" SHARED0 SHARED NULL NULL 17 4 schema_shared_1 "$HASH_A"
check "global shared migration reserved with fencing" "RESERVED:1" "$(runner_sql "
 CALL c04_09_control.reserve('$O_SHARED')")"
deny "cannot finalize before verifying DDL" \
 "CALL c04_09_control.finalize('$O_SHARED',1)"
deny "stale fencing cannot finalize shared schema" \
 "CALL c04_09_control.finalize('$O_SHARED',0)"
root_sql "ALTER TABLE c04_09_shared.resources ADD COLUMN migration_tag VARCHAR(20) NULL"
check "verified shared migration" "VERIFIED" "$(runner_sql "
 CALL c04_09_control.finalize('$O_SHARED',1)")"
check "global Shared schema revision advanced once" "18" "$(root_sql "
 SELECT placement_revision FROM c04_09_control.targets WHERE target_id='SHARED0'")"
check "shared schema covers BOTH valid tenants" "2" "$(root_sql "
 SELECT COUNT(DISTINCT tenant_id) FROM c04_09_shared.resources WHERE resource_id=1")"
check "shared metadata confirms target column" "1" "$(root_sql "
 SELECT COUNT(*) FROM information_schema.columns WHERE table_schema='c04_09_shared'
 AND table_name='resources' AND column_name='migration_tag'")"
check "global shared exact operation replay" "REPLAY" "$(runner_sql "
 CALL c04_09_control.reserve('$O_SHARED')")"
deny_root "verified ledger may not rewrite checksum" \
 "UPDATE c04_09_control.ledger SET checksum='$HASH_B' WHERE operation_id='$O_SHARED'"
deny_root "verified ledger may not be downgraded to applying" \
 "UPDATE c04_09_control.ledger SET state='applying' WHERE operation_id='$O_SHARED'"
deny_root "verified ledger may not be deleted" \
 "DELETE FROM c04_09_control.ledger WHERE operation_id='$O_SHARED'"
# Synthetic metadata tampering must invalidate an old VERIFIED receipt;
# do not authorize replay merely because the ledger still says verified.
root_sql "ALTER TABLE c04_09_shared.resources DROP COLUMN migration_tag"
deny "verified shared replay detects schema drift" \
 "CALL c04_09_control.reserve('$O_SHARED')"
root_sql "ALTER TABLE c04_09_shared.resources ADD COLUMN migration_tag VARCHAR(20) NULL"
O_CONFLICT="$(OP 202)"
decision "$O_CONFLICT" SHARED0 SHARED NULL NULL 18 4 schema_shared_1 "$HASH_B"
deny "global same version different checksum" \
 "CALL c04_09_control.reserve('$O_CONFLICT')"
O_STALE_SHARED="$(OP 203)"
decision "$O_STALE_SHARED" SHARED0 SHARED NULL NULL 17 4 schema_shared_2 "$HASH_A"
deny "new Shared version cannot use old global schema revision" \
 "CALL c04_09_control.reserve('$O_STALE_SHARED')"
check "unrelated dedicated A stays unchanged" "0" "$(root_sql "
 SELECT COUNT(*) FROM information_schema.columns WHERE table_schema='c04_09_ded_a'
 AND table_name='resources' AND column_name='migration_tag'")"

# Dedicated A: isolated, verified, cross-slot and cross-tenant unchanged.
O_DED_A="$(OP 301)"
decision "$O_DED_A" DED_A DEDICATED A slot_a 3 3 schema_ded_1 "$HASH_A"
check "dedicated A migration reserved" "RESERVED:1" "$(runner_sql "
 CALL c04_09_control.reserve('$O_DED_A')")"
root_sql "ALTER TABLE c04_09_ded_a.resources ADD COLUMN migration_tag VARCHAR(20) NULL"
check "dedicated A verified" "VERIFIED" "$(runner_sql "
 CALL c04_09_control.finalize('$O_DED_A',1)")"
check "dedicated B not touched" "0" "$(root_sql "
 SELECT COUNT(*) FROM information_schema.columns WHERE table_schema='c04_09_ded_b'
 AND table_name='resources' AND column_name='migration_tag'")"
check "dedicated A receipt replay" "REPLAY" "$(runner_sql "
 CALL c04_09_control.reserve('$O_DED_A')")"
check "all original Shared/Dedicated payloads retained" "5" "$(root_sql "
 SELECT (SELECT COUNT(*) FROM c04_09_shared.resources) +
  (SELECT COUNT(*) FROM c04_09_ded_a.resources) +
  (SELECT COUNT(*) FROM c04_09_ded_b.resources)")"

# Dedicated B: real 12-client contention for same target, DIFFERENT
# independently approved operations. Exactly one may claim the gate; no
# logical or physical changes are allowed until explicit verification.
backup_hash="$(root_sql "SELECT SHA2(CONCAT(resource_id,':',payload),256)
 FROM c04_09_ded_b.resources WHERE resource_id=1")"
docker exec "$CONTAINER" mysqldump -uroot --no-tablespaces --single-transaction \
 c04_09_ded_b > "$TMP/ded_b_backup.sql"
test -s "$TMP/ded_b_backup.sql" || { echo 'FAIL empty disposable backup'; exit 1; }
check "pre-DDL dedicated backup contains fixture" "1" "$(grep -c "DED-BETA" "$TMP/ded_b_backup.sql")"
pids=()
for i in $(seq 1 12); do
  op="$(OP "$((400+i))")"
  decision "$op" DED_B DEDICATED B slot_b 7 8 schema_race_1 "$HASH_A"
done
for i in $(seq 1 12); do
  op="$(OP "$((400+i))")"
  (runner_sql_retry "CALL c04_09_control.reserve('$op')" \
   > "$TMP/race-$i" 2>&1) &
  pids+=("$!")
done
winner=0
loser=0
winning_op=""
for i in $(seq 1 12); do
  if wait "${pids[$((i-1))]}"; then
    winner=$((winner+1))
    winning_op="$(OP "$((400+i))")"
    check "winner holds fence one" "RESERVED:1" "$(cat "$TMP/race-$i")"
  else
    loser=$((loser+1))
    case "$(cat "$TMP/race-$i")" in *"ERROR 1644"*) ;; *)
       echo "FAIL unexpected concurrency error in race-$i" >&2
       cat "$TMP/race-$i" >&2; exit 1 ;;
    esac
  fi
done
check "twelve independent decisions: one winner" "1" "$winner"
check "eleven conflicting contenders fail closed" "11" "$loser"
check "exactly one B ledger row before DDL" "1" "$(root_sql "
 SELECT COUNT(*) FROM c04_09_control.ledger WHERE target_id='DED_B'")"
check "B remains unchanged before DDL" "0" "$(root_sql "
 SELECT COUNT(*) FROM information_schema.columns WHERE table_schema='c04_09_ded_b'
 AND table_name='resources' AND column_name='migration_tag'")"
deny "second version cannot enter while target locked" \
 "CALL c04_09_control.reserve('$(OP 402)')"
deny "stale fencing cannot finalize dedicated B" \
 "CALL c04_09_control.finalize('$winning_op',0)"
deny "finalization before DDL cannot certify" \
 "CALL c04_09_control.finalize('$winning_op',1)"

# This deliberately proves MySQL implicit DDL commits: ROLLBACK does NOT
# remove the added column. Mark operation failed and require real restoration
# from a disposable backup, not just a misleading SQL ROLLBACK.
root_sql "START TRANSACTION;
 ALTER TABLE c04_09_ded_b.resources ADD COLUMN migration_tag VARCHAR(20) NULL;
 ROLLBACK;"
check "DDL persists after ROLLBACK (implicit commit)" "1" "$(root_sql "
 SELECT COUNT(*) FROM information_schema.columns WHERE table_schema='c04_09_ded_b'
 AND table_name='resources' AND column_name='migration_tag'")"
root_sql "UPDATE c04_09_control.approvals SET approved=0 WHERE operation_id='$winning_op'"
deny "approval revoked midflight cannot finalize committed DDL" \
 "CALL c04_09_control.finalize('$winning_op',1)"
check "mark interrupted migration failed" "FAILED_LOCKED" "$(root_sql "
 CALL c04_09_control.mark_failed('$winning_op',1)")"
# A failed ledger must still deny replay even after approval is restored;
# otherwise the negative would test only the revoked approval guard.
root_sql "UPDATE c04_09_control.approvals SET approved=1 WHERE operation_id='$winning_op'"
deny "failed migration can never replay as success" \
 "CALL c04_09_control.reserve('$winning_op')"
if root_sql "CALL c04_09_control.verify_restored_and_release('$winning_op',1)" \
 >/dev/null 2>&1; then
  echo 'FAIL restoration acknowledged while partial DDL remains' >&2; exit 1
fi
DENIED=$((DENIED+1))
check "failed gate remains locked" "$winning_op" "$(root_sql "
 SELECT inflight_operation FROM c04_09_control.gates WHERE target_id='DED_B'")"

if restore_checked "$winning_op" "c04_09_ded_a" "$TMP/ded_b_backup.sql" \
 > "$TMP/wrong-restore" 2>&1; then
  echo 'FAIL wrong dedicated restore destination accepted' >&2; exit 1
fi
DENIED=$((DENIED+1))
check "rejected wrong restoration did not modify A" "DED-ALPHA" "$(root_sql "
 SELECT payload FROM c04_09_ded_a.resources WHERE resource_id=1")"
restore_checked "$winning_op" "c04_09_ded_b" "$TMP/ded_b_backup.sql"
restored_hash="$(root_sql "SELECT SHA2(CONCAT(resource_id,':',payload),256)
 FROM c04_09_ded_b.resources WHERE resource_id=1")"
check "physical backup restoration data hash" "$backup_hash" "$restored_hash"
check "physical restoration removes partial DDL" "0" "$(root_sql "
 SELECT COUNT(*) FROM information_schema.columns WHERE table_schema='c04_09_ded_b'
 AND table_name='resources' AND column_name='migration_tag'")"
# The protected recovery procedure (not just this shell) MUST verify the
# original snapshot digest. Corrupt the restored payload and demand a hard deny.
root_sql "UPDATE c04_09_ded_b.resources SET payload='TAMPERED' WHERE resource_id=1"
deny_root "protected recovery rejects altered restored payload" \
 "CALL c04_09_control.verify_restored_and_release('$winning_op',1)"
restore_checked "$winning_op" "c04_09_ded_b" "$TMP/ded_b_backup.sql"
check "second physical restoration recovers snapshot hash" "$backup_hash" "$(root_sql "
 SELECT SHA2(CONCAT(resource_id,':',payload),256)
 FROM c04_09_ded_b.resources WHERE resource_id=1")"
check "recovered gate releases only after restoration" "RESTORED" "$(root_sql "
 CALL c04_09_control.verify_restored_and_release('$winning_op',1)")"
check "immutable ledger records historical failed-recovered operation" "recovered" "$(root_sql "
 SELECT state FROM c04_09_control.ledger WHERE operation_id='$winning_op'")"
deny "historical failed-recovered operation cannot blindly replay" \
 "CALL c04_09_control.reserve('$winning_op')"
check "other dedicated A still intact after B restoration" "DED-ALPHA" "$(root_sql "
 SELECT payload FROM c04_09_ded_a.resources WHERE resource_id=1")"
check "shared A/B unchanged after B restoration" "ALPHA:BETA" "$(root_sql "
 SELECT GROUP_CONCAT(payload ORDER BY tenant_id SEPARATOR ':')
 FROM c04_09_shared.resources WHERE resource_id=1")"

# A fresh decision/version on restored B can now succeed; old fencing
# cannot override the new generation.
O_FRESH="$(OP 501)"
decision "$O_FRESH" DED_B DEDICATED B slot_b 7 8 schema_recovered_2 "$HASH_B"
check "new operation increments immutable fence" "RESERVED:2" "$(runner_sql "
 CALL c04_09_control.reserve('$O_FRESH')")"
root_sql "ALTER TABLE c04_09_ded_b.resources ADD COLUMN migration_tag VARCHAR(20) NULL"
deny "stale first fence fails after recovery" \
 "CALL c04_09_control.finalize('$O_FRESH',1)"
check "restored B new migration verified" "VERIFIED" "$(runner_sql "
 CALL c04_09_control.finalize('$O_FRESH',2)")"
check "one verified B ledger and one recovered" "1:1" "$(root_sql "
 SELECT CONCAT(SUM(state='verified'),':',SUM(state='recovered'))
 FROM c04_09_control.ledger WHERE target_id='DED_B'")"
check "all three schemas have independent migrations only" "1:1:1" "$(root_sql "
 SELECT CONCAT(
 (SELECT COUNT(*) FROM information_schema.columns WHERE table_schema='c04_09_shared'
 AND table_name='resources' AND column_name='migration_tag'),':',
 (SELECT COUNT(*) FROM information_schema.columns WHERE table_schema='c04_09_ded_a'
 AND table_name='resources' AND column_name='migration_tag'),':',
 (SELECT COUNT(*) FROM information_schema.columns WHERE table_schema='c04_09_ded_b'
 AND table_name='resources' AND column_name='migration_tag'))")"

echo "C04-09 GUARDED MySQL 8.4: $PASS positive + $DENIED negative; 12 real concurrent approvals, 1 winner/11 denials; genuine mysqldump/restore; scope+fencing verified."
echo "LIMIT: synthetic approvals and root-only DDL test harness, NOT production authentication, tenant router, backup scheduling or deployment."
