#!/usr/bin/env bash
# CRECISOL C04-10 isolated real MySQL 8.4 fixture: keys/jobs/media/exports.
# NEVER use a production DB, user, connection string, object store or worker.
set -euo pipefail
IMAGE="mysql:8.4@sha256:a81d043930e51745522bd5c430961cf8286f0a23f0a9a2a6e0108f7ea71b0902"
NAME="crecisol-c04-10-$RANDOM-$$"
DB="c04_10_disposable"
PASSWORD="$(openssl rand -hex 24)"
TMP="$(mktemp -d)"
PASS=0
NEGATIVE=0
cleanup() { docker rm -f "$NAME" >/dev/null 2>&1 || true; rm -rf "$TMP"; }
trap cleanup EXIT
root_sql() {
 docker exec "$NAME" mysql -uroot -N -B -D "$DB" -e "$1"
}
runner_sql() {
 local user="$1"; shift
 docker exec -e MYSQL_PWD="$PASSWORD" "$NAME" \
   mysql "-u$user" -N -B -D "$DB" -e "$1"
}
check() {
 local label="$1" expected="$2" actual="$3"
 if [ "$expected" != "$actual" ]; then
  echo "FAIL C04-10 $label expected=$expected actual=$actual" >&2
  exit 1
 fi
 PASS=$((PASS+1))
 echo "PASS C04-10 $label"
}
deny_runner() {
 local label="$1" user="$2" query="$3" output
 if output="$(runner_sql "$user" "$query" 2>&1)"; then
  echo "FAIL C04-10 unauthorized runner accepted $label ($output)" >&2
  exit 1
 fi
 case "$output" in
  *"ERROR 1644"*|*"ERROR 1142"*|*"ERROR 1143"*|*"ERROR 1044"*|*"ERROR 1370"*) ;;
  *) echo "FAIL C04-10 wrong denial $label: $output" >&2; exit 1 ;;
 esac
 NEGATIVE=$((NEGATIVE+1))
 echo "PASS C04-10 runner rejected $label"
}
deny_root() {
 local label="$1" query="$2" output
 if output="$(root_sql "$query" 2>&1)"; then
  echo "FAIL C04-10 integrity accepted $label ($output)" >&2; exit 1
 fi
 case "$output" in *"ERROR 1452"*|*"ERROR 1062"*|*"ERROR 3819"*|*"ERROR 1644"*) ;;
  *) echo "FAIL C04-10 unexpected DB denial $label: $output" >&2; exit 1 ;;
 esac
 NEGATIVE=$((NEGATIVE+1))
 echo "PASS C04-10 integrity rejected $label"
}
g32() { printf '%032x' "$1"; }
hex64() { printf '%064d' 0 | tr '0' "$1"; }
A="$(hex64 a)"; A="${A:0:32}"
B="$(hex64 b)"; B="${B:0:32}"
BA="$(hex64 c)"; BA="${BA:0:32}"
BB="$(hex64 d)"; BB="${BB:0:32}"
RESOURCE="$(hex64 e)"; RESOURCE="${RESOURCE:0:32}"
A_ACTOR="$(hex64 f)"; A_ACTOR="${A_ACTOR:0:32}"
B_ACTOR="$(hex64 1)"; B_ACTOR="${B_ACTOR:0:32}"
A_KEY="$(hex64 a)"
B_KEY="$(hex64 b)"
A_MEDIA_KEY="$(hex64 c)"
B_MEDIA_KEY="$(hex64 d)"
A_EXPORT_KEY="$(hex64 e)"
B_EXPORT_KEY="$(hex64 f)"
A_JOB="$(g32 101)"
B_JOB="$(g32 102)"
A_MEDIA="$(g32 201)"
B_MEDIA="$(g32 202)"
A_EXPORT="$(g32 301)"
B_EXPORT="$(g32 302)"
A_UNUSED_MEDIA="$(g32 203)"
B_UNUSED_EXPORT="$(g32 303)"
B_SECOND_JOB="$(g32 103)"

if [ -n "${GITHUB_ACTIONS:-}" ]; then printf '::add-mask::%s\n' "$PASSWORD"; fi
docker run --rm -d --network none --name "$NAME" \
  -e MYSQL_ALLOW_EMPTY_PASSWORD=yes "$IMAGE" >/dev/null
boot=0
ready=0
for _ in $(seq 1 120); do
 if [ "$boot" -eq 0 ] &&
    docker logs "$NAME" 2>&1 | grep -Fq 'MySQL init process done. Ready for start up.'; then
  boot=1
 fi
 if [ "$boot" -eq 1 ] &&
    docker exec "$NAME" mysql -uroot -N -B -e "SELECT 1" >/dev/null 2>&1; then
  ready=1; break
 fi
 sleep 1
done
if [ "$ready" -ne 1 ]; then echo 'FAIL C04-10 disposable MySQL start'; exit 1; fi
case "$(docker exec "$NAME" mysql -uroot -N -B -e "SELECT VERSION()")" in
 8.4.*) ;; *) echo 'FAIL C04-10 MySQL 8.4 required' >&2; exit 1 ;;
esac
docker exec -i "$NAME" mysql -uroot < database/tests/c04_10_isolation_fixture.sql

root_sql "INSERT INTO tenants VALUES('$A','active',1),('$B','active',1);
INSERT INTO branches VALUES
('$A','$BA','active',1),('$B','$BB','active',1);
INSERT INTO resources VALUES
('$A','$BA','$RESOURCE','$A_ACTOR','active',1),
('$B','$BB','$RESOURCE','$B_ACTOR','active',1);
INSERT INTO test_account_binding VALUES
('c0410_a','$A','$A_ACTOR',1),('c0410_b','$B','$B_ACTOR',1);
CREATE USER 'c0410_a'@'localhost' IDENTIFIED BY '$PASSWORD';
CREATE USER 'c0410_b'@'localhost' IDENTIFIED BY '$PASSWORD';"
for user in c0410_a c0410_b; do
 for procedure in claim_job ack_job read_private_cache read_private_media read_private_export; do
  root_sql "GRANT EXECUTE ON PROCEDURE $DB.$procedure TO '$user'@'localhost';"
 done
done
check "two valid tenants with same resource ID" "2" "$(root_sql "
 SELECT COUNT(*) FROM resources WHERE resource_id='$RESOURCE'")"
check "four isolated artifact tables" "4" "$(root_sql "
 SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='$DB'
 AND table_name IN ('cache_entries','job_records','media_refs','export_manifests')")"

decision() {
 local op="$1" kind="$2" tenant="$3" branch="$4" actor="$5"
 root_sql "INSERT INTO decisions
 (operation_id,kind,tenant_id,branch_id,resource_id,actor_id,
 tenant_epoch,branch_revision,resource_revision,approved,expires_at_utc)
 VALUES('$op','$kind','$tenant','$branch','$RESOURCE','$actor',1,1,1,1,
 DATE_ADD(UTC_TIMESTAMP(6), INTERVAL 1 DAY));"
}
decision "$A_JOB" JOB "$A" "$BA" "$A_ACTOR"
decision "$B_JOB" JOB "$B" "$BB" "$B_ACTOR"
decision "$A_MEDIA" MEDIA "$A" "$BA" "$A_ACTOR"
decision "$B_MEDIA" MEDIA "$B" "$BB" "$B_ACTOR"
decision "$A_EXPORT" EXPORT "$A" "$BA" "$A_ACTOR"
decision "$B_EXPORT" EXPORT "$B" "$BB" "$B_ACTOR"
decision "$A_UNUSED_MEDIA" MEDIA "$A" "$BA" "$A_ACTOR"
decision "$B_UNUSED_EXPORT" EXPORT "$B" "$BB" "$B_ACTOR"
root_sql "INSERT INTO job_records
 (operation_id,tenant_id,branch_id,resource_id,actor_id,tenant_epoch,branch_revision,resource_revision)
 VALUES('$A_JOB','$A','$BA','$RESOURCE','$A_ACTOR',1,1,1),
       ('$B_JOB','$B','$BB','$RESOURCE','$B_ACTOR',1,1,1);
INSERT INTO media_refs
 (operation_id,tenant_id,branch_id,resource_id,actor_id,tenant_epoch,branch_revision,resource_revision,object_reference,version)
 VALUES('$A_MEDIA','$A','$BA','$RESOURCE','$A_ACTOR',1,1,1,'$A_MEDIA_KEY',1),
       ('$B_MEDIA','$B','$BB','$RESOURCE','$B_ACTOR',1,1,1,'$B_MEDIA_KEY',1);
INSERT INTO export_manifests
 (operation_id,tenant_id,branch_id,resource_id,actor_id,tenant_epoch,branch_revision,resource_revision,manifest_reference,version)
 VALUES('$A_EXPORT','$A','$BA','$RESOURCE','$A_ACTOR',1,1,1,'$A_EXPORT_KEY',1),
       ('$B_EXPORT','$B','$BB','$RESOURCE','$B_ACTOR',1,1,1,'$B_EXPORT_KEY',1);
INSERT INTO cache_entries
 (tenant_id,branch_id,resource_id,actor_id,tenant_epoch,branch_revision,resource_revision,cache_key,marker)
 VALUES('$A','$BA','$RESOURCE','$A_ACTOR',1,1,1,'$A_KEY','ALPHA'),
       ('$B','$BB','$RESOURCE','$B_ACTOR',1,1,1,'$B_KEY','BETA');"
check "A cache read own marker" "ALPHA" "$(runner_sql c0410_a "
 CALL read_private_cache('$A_KEY')")"
check "B cache read own marker" "BETA" "$(runner_sql c0410_b "
 CALL read_private_cache('$B_KEY')")"
check "A media only opaque object reference" "$A_MEDIA_KEY" "$(runner_sql c0410_a "
 CALL read_private_media('$A_MEDIA')")"
check "B media only opaque object reference" "$B_MEDIA_KEY" "$(runner_sql c0410_b "
 CALL read_private_media('$B_MEDIA')")"
check "A export only opaque manifest reference" "$A_EXPORT_KEY" "$(runner_sql c0410_a "
 CALL read_private_export('$A_EXPORT')")"
check "B export only opaque manifest reference" "$B_EXPORT_KEY" "$(runner_sql c0410_b "
 CALL read_private_export('$B_EXPORT')")"

deny_runner "B asks for A cached content" c0410_b "CALL read_private_cache('$A_KEY')"
deny_runner "A asks for B cache" c0410_a "CALL read_private_cache('$B_KEY')"
deny_runner "B asks for A object metadata" c0410_b "CALL read_private_media('$A_MEDIA')"
deny_runner "A asks for B object metadata" c0410_a "CALL read_private_media('$B_MEDIA')"
deny_runner "B asks for A export" c0410_b "CALL read_private_export('$A_EXPORT')"
deny_runner "A asks for B export" c0410_a "CALL read_private_export('$B_EXPORT')"
deny_runner "B claims A's job selector" c0410_b "CALL claim_job('$A_JOB')"
deny_runner "A claims B's job selector" c0410_a "CALL claim_job('$B_JOB')"
deny_runner "runner cannot read tenant ledger directly" c0410_a "SELECT * FROM tenants"
deny_runner "runner cannot enumerate protected jobs" c0410_b "SELECT * FROM job_records"
deny_runner "runner cannot inject cache rows" c0410_a "INSERT INTO cache_entries
 (tenant_id,branch_id,resource_id,actor_id,tenant_epoch,cache_key,marker)
 VALUES('$A','$BA','$RESOURCE','$A_ACTOR',1,'$B_KEY','ATTACK')"
deny_runner "runner cannot execute DDL" c0410_a "DROP TABLE job_records"

deny_root "valid B tenant with A branch is forbidden FK" "
 INSERT INTO cache_entries
 (tenant_id,branch_id,resource_id,actor_id,tenant_epoch,branch_revision,resource_revision,cache_key,marker)
 VALUES('$B','$BA','$RESOURCE','$B_ACTOR',1,1,1,'$(hex64 2)','INJECT')"
deny_root "A reuses globally unique B cache key" "
 INSERT INTO cache_entries
 (tenant_id,branch_id,resource_id,actor_id,tenant_epoch,branch_revision,resource_revision,cache_key,marker)
 VALUES('$A','$BA','$RESOURCE','$A_ACTOR',1,1,1,'$B_KEY','ALIAS')"
deny_root "B media mapped to valid A operation forbidden" "
 INSERT INTO media_refs
 (operation_id,tenant_id,branch_id,resource_id,actor_id,tenant_epoch,branch_revision,resource_revision,object_reference,version)
 VALUES('$A_UNUSED_MEDIA','$B','$BB','$RESOURCE','$B_ACTOR',1,1,1,'$(hex64 3)',1)"
deny_root "A export mapped to valid B operation forbidden" "
 INSERT INTO export_manifests
 (operation_id,tenant_id,branch_id,resource_id,actor_id,tenant_epoch,branch_revision,resource_revision,manifest_reference,version)
 VALUES('$B_UNUSED_EXPORT','$A','$BA','$RESOURCE','$A_ACTOR',1,1,1,'$(hex64 4)',1)"
deny_root "media reference cannot be a fake URL" "
 UPDATE media_refs SET object_reference='https://bad.host/a' WHERE operation_id='$A_MEDIA'"

# Genuine 12-way InnoDB contention on the same approved job. The account
# binding and row-level claim serialize attempts; only one gets execution.
pids=()
for i in $(seq 1 12); do
 (runner_sql c0410_a "CALL claim_job('$A_JOB')" > "$TMP/race-$i" 2>&1) &
 pids+=("$!")
done
wins=0
busy=0
for i in $(seq 1 12); do
 wait "${pids[$((i-1))]}" || {
   echo "FAIL C04-10 concurrency runner-$i" >&2
   cat "$TMP/race-$i" >&2; exit 1;
 }
 case "$(cat "$TMP/race-$i")" in
  CLAIMED:1) wins=$((wins+1)) ;;
  BUSY) busy=$((busy+1)) ;;
  *) echo "FAIL C04-10 unexpected result race $i" >&2
     cat "$TMP/race-$i" >&2; exit 1 ;;
 esac
done
check "one real claimant of A job" "1" "$wins"
check "eleven other workers cannot execute same A job" "11" "$busy"
check "fenced generation exactly once" "1" "$(root_sql "
 SELECT fence FROM job_records WHERE operation_id='$A_JOB'")"
check "job running remains tenant A only" "running" "$(root_sql "
 SELECT status FROM job_records WHERE operation_id='$A_JOB'")"
deny_runner "stale lease generation cannot ack" c0410_a "
 CALL ack_job('$A_JOB',0)"
deny_runner "tenant B cannot ack A's fence" c0410_b "
 CALL ack_job('$A_JOB',1)"

# Tenant A suspension increments epoch, blocks cache, media/export and jobs
# without affecting B. Reactivation with new epoch never restores stale work.
root_sql "UPDATE tenants SET state='suspended', access_epoch=2 WHERE tenant_id='$A'"
deny_runner "suspended A cannot finish previously claimed job" c0410_a "
 CALL ack_job('$A_JOB',1)"
deny_runner "suspended A cannot see stale cache" c0410_a "
 CALL read_private_cache('$A_KEY')"
deny_runner "suspended A cannot get media" c0410_a "
 CALL read_private_media('$A_MEDIA')"
deny_runner "suspended A cannot get export" c0410_a "
 CALL read_private_export('$A_EXPORT')"
check "B still reads its own cache during A suspension" "BETA" "$(runner_sql c0410_b "
 CALL read_private_cache('$B_KEY')")"
check "B still reads its media during A suspension" "$B_MEDIA_KEY" "$(runner_sql c0410_b "
 CALL read_private_media('$B_MEDIA')")"
check "B still reads its export during A suspension" "$B_EXPORT_KEY" "$(runner_sql c0410_b "
 CALL read_private_export('$B_EXPORT')")"
root_sql "UPDATE tenants SET state='active' WHERE tenant_id='$A'"
deny_runner "reactivated A epoch rejects old cache" c0410_a "
 CALL read_private_cache('$A_KEY')"
deny_runner "reactivated A epoch rejects old job ack" c0410_a "
 CALL ack_job('$A_JOB',1)"
deny_runner "reactivated A epoch rejects old media" c0410_a "
 CALL read_private_media('$A_MEDIA')"
deny_runner "reactivated A epoch rejects old export" c0410_a "
 CALL read_private_export('$A_EXPORT')"
check "B unaffected epoch after A reactivation" "1" "$(root_sql "
 SELECT access_epoch FROM tenants WHERE tenant_id='$B'")"

check "B job can still claim" "CLAIMED:1" "$(runner_sql c0410_b "
 CALL claim_job('$B_JOB')")"
deny_runner "wrong B job fence cannot ack" c0410_b "CALL ack_job('$B_JOB',2)"
check "B job can ack exactly once" "ACKED" "$(runner_sql c0410_b "
 CALL ack_job('$B_JOB',1)")"
deny_runner "double ack B cannot execute twice" c0410_b "CALL ack_job('$B_JOB',1)"
check "replayed B claim is busy, not execution" "BUSY" "$(runner_sql c0410_b "
 CALL claim_job('$B_JOB')")"

# Isolate revocation and expiry negatives while branch/resource revisions
# still match, so these tests cannot pass accidentally via a stale revision.
root_sql "UPDATE test_account_binding SET enabled=0 WHERE sql_login='c0410_b'"
deny_runner "current B identity revoked before cache read" c0410_b "
 CALL read_private_cache('$B_KEY')"
root_sql "UPDATE test_account_binding SET enabled=1 WHERE sql_login='c0410_b'"
check "B regained own cache only after identity enabled" "BETA" "$(runner_sql c0410_b "
 CALL read_private_cache('$B_KEY')")"
root_sql "UPDATE decisions SET approved=0 WHERE operation_id='$B_MEDIA'"
deny_runner "B media permission revoked while tenant active" c0410_b "
 CALL read_private_media('$B_MEDIA')"
root_sql "UPDATE decisions SET approved=1 WHERE operation_id='$B_MEDIA'"
check "B media permission approval restored" "$B_MEDIA_KEY" "$(runner_sql c0410_b "
 CALL read_private_media('$B_MEDIA')")"
root_sql "UPDATE decisions SET expires_at_utc=DATE_SUB(UTC_TIMESTAMP(6), INTERVAL 1 SECOND)
 WHERE operation_id='$B_EXPORT'"
deny_runner "expired B export cannot be reused" c0410_b "
 CALL read_private_export('$B_EXPORT')"
root_sql "UPDATE decisions SET expires_at_utc=DATE_ADD(UTC_TIMESTAMP(6), INTERVAL 1 DAY)
 WHERE operation_id='$B_EXPORT'"

# Identity changes and resource/branch suspension revoke reads independently
# of tenant lifecycle, and ownership remains checked at each read.
root_sql "UPDATE resources SET owner_actor='$A_ACTOR', revision=2
 WHERE tenant_id='$B' AND branch_id='$BB' AND resource_id='$RESOURCE'"
deny_runner "resource ownership changed B cache invalid" c0410_b "
 CALL read_private_cache('$B_KEY')"
deny_runner "resource ownership changed B media invalid" c0410_b "
 CALL read_private_media('$B_MEDIA')"
root_sql "UPDATE resources SET owner_actor='$B_ACTOR', revision=3
 WHERE tenant_id='$B' AND branch_id='$BB' AND resource_id='$RESOURCE'"
deny_runner "restored resource owner cannot replay old revision cache" c0410_b "
 CALL read_private_cache('$B_KEY')"
deny_runner "restored resource owner cannot replay old revision media" c0410_b "
 CALL read_private_media('$B_MEDIA')"
# Independently test a job already claimed against a change to the branch
# revision BEFORE acknowledgement. Returning branch to active never revives it.
root_sql "INSERT INTO decisions
 (operation_id,kind,tenant_id,branch_id,resource_id,actor_id,tenant_epoch,
  branch_revision,resource_revision,approved,expires_at_utc)
 VALUES('$B_SECOND_JOB','JOB','$B','$BB','$RESOURCE','$B_ACTOR',1,1,3,1,
 DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 1 DAY));
INSERT INTO job_records
 (operation_id,tenant_id,branch_id,resource_id,actor_id,tenant_epoch,
  branch_revision,resource_revision)
 VALUES('$B_SECOND_JOB','$B','$BB','$RESOURCE','$B_ACTOR',1,1,3);"
check "new B job with fresh resource revision can claim" "CLAIMED:1" "$(runner_sql c0410_b "
 CALL claim_job('$B_SECOND_JOB')")"
root_sql "UPDATE branches SET state='suspended',revision=2
 WHERE tenant_id='$B' AND branch_id='$BB'"
deny_runner "suspended B branch blocks already leased job ack" c0410_b "
 CALL ack_job('$B_SECOND_JOB',1)"
deny_runner "suspended B branch cannot read cache" c0410_b "CALL read_private_cache('$B_KEY')"
deny_runner "suspended B branch cannot read export" c0410_b "CALL read_private_export('$B_EXPORT')"
root_sql "UPDATE branches SET state='active' WHERE tenant_id='$B' AND branch_id='$BB'"
deny_runner "B reactivated branch cannot ack old lease revision" c0410_b "
 CALL ack_job('$B_SECOND_JOB',1)"
deny_runner "B reactivated branch cannot read stale export" c0410_b "
 CALL read_private_export('$B_EXPORT')"
root_sql "UPDATE test_account_binding SET enabled=0 WHERE sql_login='c0410_b'"
deny_runner "revoked DB runner cannot read B cache" c0410_b "CALL read_private_cache('$B_KEY')"
deny_runner "revoked DB runner cannot claim B job" c0410_b "CALL claim_job('$B_JOB')"

check "A and B isolated cache rows retained" "2" "$(root_sql "
 SELECT COUNT(*) FROM cache_entries")"
check "A and B media references retained without payloads" "2" "$(root_sql "
 SELECT COUNT(*) FROM media_refs")"
check "A and B export manifests retained without files" "2" "$(root_sql "
 SELECT COUNT(*) FROM export_manifests")"
check "A job never committed after suspension" "running" "$(root_sql "
 SELECT status FROM job_records WHERE operation_id='$A_JOB'")"
check "B job completed exactly once" "done" "$(root_sql "
 SELECT status FROM job_records WHERE operation_id='$B_JOB'")"

echo "C04-10 disposable MySQL 8.4 SUCCESS: $PASS positive + $NEGATIVE negative; 12 real job claims (1 claimed / 11 busy), tenant A/B protected; no real worker/Redis/Cloudflare/media/export."
