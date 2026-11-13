#!/usr/bin/env bash
# CRECISOL C04-11 OFFENSIVE: isolated MySQL8.4 tenant A/B BOLA/IDOR.
# NEVER point this at product databases. The disposable container has NO network.
set -euo pipefail
IMAGE="mysql:8.4@sha256:a81d043930e51745522bd5c430961cf8286f0a23f0a9a2a6e0108f7ea71b0902"
NAME="crecisol-c04-11-$$-$RANDOM"
PASSWORD="$(openssl rand -hex 24)"
DB=c04_10_disposable
POS=0
NEG=0
cleanup() { docker rm -f "$NAME" >/dev/null 2>&1 || :; }
trap cleanup EXIT
root_sql() { docker exec "$NAME" mysql -uroot -N -B -D "$DB" -e "$1"; }
runner_sql() {
 local user="$1"; shift
 docker exec -e MYSQL_PWD="$PASSWORD" "$NAME" \
  mysql "-u$user" -N -B -D "$DB" -e "$1"
}
check() {
 local label="$1" want="$2" got="$3"
 if [ "$want" != "$got" ]; then
  printf 'FAIL O11 %s (expected=%s actual=%s)\n' "$label" "$want" "$got" >&2
  exit 1
 fi
 POS=$((POS+1))
 echo "PASS O11 positive: $label"
}
deny_runner() {
 local label="$1" user="$2" query="$3" out
 if out="$(runner_sql "$user" "$query" 2>&1)"; then
  echo "FAIL O11 SECURITY BYPASS: $label" >&2; exit 1
 fi
 case "$out" in
  *"ERROR 1644"*|*"ERROR 1142"*|*"ERROR 1143"*|*"ERROR 1044"*|*"ERROR 1370"*|*"ERROR 1227"*) ;;
  *"ERROR 1292"*)
    # MySQL 8.4 strict coercion rejects this hostile boolean expression
    # BEFORE the protected procedure can check the scoped lookup. Do not
    # permit 1292 for any other attack, where it might mask a bad fixture.
    [ "$label" = "O11-07 forged SQL boolean expression cannot bypass cache scope" ] || {
      echo "FAIL O11 unexpected cast error for $label: $out" >&2; exit 1;
    } ;;
  *) echo "FAIL O11 unexpected error for $label: $out" >&2; exit 1 ;;
 esac
 NEG=$((NEG+1))
 echo "PASS O11 blocked: $label"
}
deny_fk() {
 local label="$1" query="$2" out
 if out="$(root_sql "$query" 2>&1)"; then
  echo "FAIL O11 foreign-key bypass: $label" >&2; exit 1
 fi
 case "$out" in *"ERROR 1452"*) ;;
  *) echo "FAIL O11 not a genuine tenant FK rejection: $label: $out" >&2; exit 1 ;;
 esac
 NEG=$((NEG+1))
 echo "PASS O11 composite FK blocked: $label"
}
A=aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
B=bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb
BA=cccccccccccccccccccccccccccccccc
BB=dddddddddddddddddddddddddddddddd
RESOURCE=eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee
ACTA=11111111111111111111111111111111
ACTB=22222222222222222222222222222222
OPA=00000000000000000000000000000401
OPB=00000000000000000000000000000402
OMA=00000000000000000000000000000403
OMB=00000000000000000000000000000404
OEA=00000000000000000000000000000405
OEB=00000000000000000000000000000406
OMX=00000000000000000000000000000407
KA="$(printf '%064d' 0 | tr 0 a)"
KB="$(printf '%064d' 0 | tr 0 b)"
KM_A="$(printf '%064d' 0 | tr 0 c)"
KM_B="$(printf '%064d' 0 | tr 0 d)"
KE_A="$(printf '%064d' 0 | tr 0 e)"
KE_B="$(printf '%064d' 0 | tr 0 f)"
BAD_REF="$(printf '%064d' 0 | tr 0 3)"
if [ -n "${GITHUB_ACTIONS:-}" ]; then echo "::add-mask::$PASSWORD"; fi
docker run --rm -d --network none --name "$NAME" \
 -e MYSQL_ALLOW_EMPTY_PASSWORD=yes "$IMAGE" >/dev/null
ready=0
init=0
for _ in $(seq 1 120); do
 if [ "$init" -eq 0 ] && docker logs "$NAME" 2>&1 |
  grep -Fq 'MySQL init process done. Ready for start up.'; then init=1; fi
 if [ "$init" -eq 1 ] &&
    docker exec "$NAME" mysql -uroot -N -B -e "SELECT 1" >/dev/null 2>&1; then
  ready=1; break
 fi
 sleep 1
done
[ "$ready" -eq 1 ] || { echo 'FAIL O11 no disposable mysql readiness'; exit 1; }
case "$(docker exec "$NAME" mysql -uroot -N -B -e "SELECT VERSION()")" in
 8.4.*) ;; *) echo 'FAIL O11 not MySQL8.4'; exit 1 ;;
esac
docker exec -i "$NAME" mysql -uroot < database/tests/c04_10_isolation_fixture.sql
root_sql "INSERT INTO tenants VALUES('$A','active',1),('$B','active',1);
INSERT INTO branches VALUES('$A','$BA','active',1),('$B','$BB','active',1);
INSERT INTO resources VALUES
 ('$A','$BA','$RESOURCE','$ACTA','active',1),
 ('$B','$BB','$RESOURCE','$ACTB','active',1);
INSERT INTO test_account_binding VALUES
 ('c0411_a','$A','$ACTA',1),('c0411_b','$B','$ACTB',1);
CREATE USER 'c0411_a'@'localhost' IDENTIFIED BY '$PASSWORD';
CREATE USER 'c0411_b'@'localhost' IDENTIFIED BY '$PASSWORD';"
for user in c0411_a c0411_b; do
 for proc in claim_job ack_job read_private_cache read_private_media read_private_export; do
  root_sql "GRANT EXECUTE ON PROCEDURE $DB.$proc TO '$user'@'localhost';"
 done
done
decision() {
 local op="$1" kind="$2" tenant="$3" branch="$4" actor="$5"
 root_sql "INSERT INTO decisions(operation_id,kind,tenant_id,branch_id,resource_id,
 actor_id,tenant_epoch,branch_revision,resource_revision,approved,expires_at_utc)
 VALUES('$op','$kind','$tenant','$branch','$RESOURCE','$actor',1,1,1,1,
 DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 1 DAY));"
}
decision "$OPA" JOB "$A" "$BA" "$ACTA"
decision "$OPB" JOB "$B" "$BB" "$ACTB"
decision "$OMA" MEDIA "$A" "$BA" "$ACTA"
decision "$OMB" MEDIA "$B" "$BB" "$ACTB"
decision "$OEA" EXPORT "$A" "$BA" "$ACTA"
decision "$OEB" EXPORT "$B" "$BB" "$ACTB"
decision "$OMX" MEDIA "$A" "$BA" "$ACTA"
root_sql "INSERT INTO job_records(operation_id,tenant_id,branch_id,resource_id,
 actor_id,tenant_epoch,branch_revision,resource_revision)
 VALUES('$OPA','$A','$BA','$RESOURCE','$ACTA',1,1,1),
       ('$OPB','$B','$BB','$RESOURCE','$ACTB',1,1,1);
INSERT INTO cache_entries(tenant_id,branch_id,resource_id,actor_id,
 tenant_epoch,branch_revision,resource_revision,cache_key,marker)
 VALUES('$A','$BA','$RESOURCE','$ACTA',1,1,1,'$KA','ONLY_A'),
       ('$B','$BB','$RESOURCE','$ACTB',1,1,1,'$KB','ONLY_B');
INSERT INTO media_refs(operation_id,tenant_id,branch_id,resource_id,actor_id,
 tenant_epoch,branch_revision,resource_revision,object_reference,version)
 VALUES('$OMA','$A','$BA','$RESOURCE','$ACTA',1,1,1,'$KM_A',1),
       ('$OMB','$B','$BB','$RESOURCE','$ACTB',1,1,1,'$KM_B',1);
INSERT INTO export_manifests(operation_id,tenant_id,branch_id,resource_id,actor_id,
 tenant_epoch,branch_revision,resource_revision,manifest_reference,version)
 VALUES('$OEA','$A','$BA','$RESOURCE','$ACTA',1,1,1,'$KE_A',1),
       ('$OEB','$B','$BB','$RESOURCE','$ACTB',1,1,1,'$KE_B',1);"

check "two valid tenants same resource UUID" 2 "$(root_sql "
 SELECT COUNT(*) FROM resources WHERE resource_id='$RESOURCE'")"
check "A sees own cache only" ONLY_A "$(runner_sql c0411_a "
 CALL read_private_cache('$KA')")"
check "B sees own cache only" ONLY_B "$(runner_sql c0411_b "
 CALL read_private_cache('$KB')")"
check "A reads only own media handle" "$KM_A" "$(runner_sql c0411_a "
 CALL read_private_media('$OMA')")"
check "B reads only own media handle" "$KM_B" "$(runner_sql c0411_b "
 CALL read_private_media('$OMB')")"
check "A reads only own export handle" "$KE_A" "$(runner_sql c0411_a "
 CALL read_private_export('$OEA')")"
check "B reads only own export handle" "$KE_B" "$(runner_sql c0411_b "
 CALL read_private_export('$OEB')")"
deny_runner "O11-01 A IDOR B cache" c0411_a "CALL read_private_cache('$KB')"
deny_runner "O11-01 B IDOR A cache" c0411_b "CALL read_private_cache('$KA')"
deny_runner "O11-06 A stolen B media operation" c0411_a "CALL read_private_media('$OMB')"
deny_runner "O11-06 B stolen A media operation" c0411_b "CALL read_private_media('$OMA')"
deny_runner "O11-06 A stolen B export manifest" c0411_a "CALL read_private_export('$OEB')"
deny_runner "O11-06 B stolen A export manifest" c0411_b "CALL read_private_export('$OEA')"
deny_runner "O11-08 A claims B job" c0411_a "CALL claim_job('$OPB')"
deny_runner "O11-08 B claims A job" c0411_b "CALL claim_job('$OPA')"
deny_runner "O11-08 A ack B job operation" c0411_a "CALL ack_job('$OPB',1)"
deny_runner "O11-08 B ack A job operation" c0411_b "CALL ack_job('$OPA',1)"
deny_runner "O11-07 A cannot SELECT other tenant base table" c0411_a "
 SELECT marker FROM cache_entries WHERE tenant_id='$B'"
deny_runner "O11-07 A cannot UPDATE other tenant base table" c0411_a "
 UPDATE cache_entries SET marker='COMPROMISED' WHERE tenant_id='$B'"
deny_runner "O11-07 A cannot DELETE other tenant base table" c0411_a "
 DELETE FROM cache_entries WHERE tenant_id='$B'"
deny_runner "O11-07 B cannot SELECT other tenants decisions" c0411_b "
 SELECT * FROM decisions WHERE tenant_id='$A'"
deny_runner "O11-07 B cannot UPDATE tenant lifecycle" c0411_b "
 UPDATE tenants SET state='active' WHERE tenant_id='$A'"
deny_runner "O11-07 B cannot DROP protected tenant table" c0411_b "
 DROP TABLE cache_entries"
deny_runner "O11-04 SET untrusted tenant variable cannot spoof SQL account" c0411_a "
 SET @tenant_id='$B'; CALL read_private_cache('$KB')"
deny_runner "O11-04 SET untrusted login variable cannot spoof SQL account" c0411_a "
 SET @sql_login='c0411_b'; CALL read_private_cache('$KB')"
deny_runner "O11-07 forged SQL boolean expression cannot bypass cache scope" c0411_a "
 CALL read_private_cache('$KB' OR '1'='1')"
deny_runner "O11-07 dynamic-looking operation string cannot select B media" c0411_a "
 CALL read_private_media('bad'' OR ''1''=''1')"
deny_fk "O11-01 valid A operation cannot bind B media record" "
 INSERT INTO media_refs(operation_id,tenant_id,branch_id,resource_id,actor_id,
 tenant_epoch,branch_revision,resource_revision,object_reference,version)
 VALUES('$OMX','$B','$BB','$RESOURCE','$ACTB',1,1,1,'$BAD_REF',1)"
deny_fk "O11-01 valid B tenant cannot reference branch A" "
 INSERT INTO cache_entries(tenant_id,branch_id,resource_id,actor_id,
 tenant_epoch,branch_revision,resource_revision,cache_key,marker)
 VALUES('$B','$BA','$RESOURCE','$ACTB',1,1,1,'$BAD_REF','ATTACK')"
check "A still intact after forged writes" ONLY_A "$(root_sql "
 SELECT marker FROM cache_entries WHERE tenant_id='$A'")"
check "B still intact after forged writes" ONLY_B "$(root_sql "
 SELECT marker FROM cache_entries WHERE tenant_id='$B'")"
check "B media metadata remains unchanged" "$KM_B" "$(root_sql "
 SELECT object_reference FROM media_refs WHERE operation_id='$OMB'")"
check "B job never claimed by A" pending "$(root_sql "
 SELECT status FROM job_records WHERE operation_id='$OPB'")"

# Revocation: a previously valid key/op must fail while B still succeeds.
root_sql "UPDATE tenants SET state='suspended',access_epoch=2 WHERE tenant_id='$A'"
deny_runner "O11-09 suspend A invalidates previous cache" c0411_a "
 CALL read_private_cache('$KA')"
deny_runner "O11-09 suspend A invalidates previous export" c0411_a "
 CALL read_private_export('$OEA')"
deny_runner "O11-09 suspend A prevents previous job claim" c0411_a "
 CALL claim_job('$OPA')"
check "B cache unaffected when A suspended" ONLY_B "$(runner_sql c0411_b "
 CALL read_private_cache('$KB')")"
root_sql "UPDATE tenants SET state='active' WHERE tenant_id='$A'"
deny_runner "O11-09 reactivated A cannot reuse old epoch cache" c0411_a "
 CALL read_private_cache('$KA')"
check "B tenant epoch preserved" 1 "$(root_sql "
 SELECT access_epoch FROM tenants WHERE tenant_id='$B'")"
check "no fake IDOR ever changed B data" ONLY_B "$(root_sql "
 SELECT marker FROM cache_entries WHERE tenant_id='$B'")"
echo "C04-11 OFFENSIVE MySQL8.4 SUCCESS: $POS positive + $NEG negative; " \
 "BOLA/IDOR/claims/DSN selectors blocked in disposable fixture, 0 B row mutations."
