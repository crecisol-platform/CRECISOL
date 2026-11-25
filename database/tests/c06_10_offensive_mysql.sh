#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
NAME="crecisol-c06-offensive-$$"
PASSWORD="$(openssl rand -hex 24)"
IMAGE='mysql:8.4@sha256:a81d043930e51745522bd5c430961cf8286f0a23f0a9a2a6e0108f7ea71b0902'
cleanup() { docker rm -f "$NAME" >/dev/null 2>&1 || true; }
trap cleanup EXIT
docker run --rm -d --name "$NAME" -p 127.0.0.1::3306 \
  -e "MYSQL_ROOT_PASSWORD=$PASSWORD" -e MYSQL_DATABASE=crecisol_c05 "$IMAGE" >/dev/null
ready=0
for _ in $(seq 1 120); do
  if docker exec -e "MYSQL_PWD=$PASSWORD" "$NAME" mysql --protocol=TCP -h127.0.0.1 -uroot -Nse 'SELECT 1' >/dev/null 2>&1; then
    ready=1; break
  fi
  sleep 1
done
[[ "$ready" == 1 ]] || { docker logs "$NAME"; exit 1; }
sql() {
  docker exec -e "MYSQL_PWD=$PASSWORD" "$NAME" \
    mysql -uroot -N -B -D crecisol_c05 -e "$1"
}
apply() {
  docker exec -i -e "MYSQL_PWD=$PASSWORD" "$NAME" mysql -uroot crecisol_c05 < "$1"
}
assert() {
  local actual
  actual="$(sql "$1")"
  [[ "$actual" == "$2" ]] || { echo "FAIL C06-10 D $3 expected=$2 actual=$actual" >&2; exit 1; }
  echo "PASS C06-10 D $3"
}
rejected=0
reject() {
  local error
  if error="$(sql "$1" 2>&1)"; then
    echo "FAIL C06-10 D unexpectedly permitted: $2" >&2; exit 1
  fi
  [[ "$error" == *"ERROR $3 "* ]] || { echo "FAIL C06-10 D wrong SQL rejection: $2 (expected $3)" >&2; exit 1; }
  rejected=$((rejected+1))
  echo "PASS C06-10 D rejected $2 (MySQL $3)"
}
for file in "$ROOT"/database/migrations/[0-9][0-9][0-9]_c05_*.sql; do
  [[ "$file" == *.down.sql ]] && continue
  apply "$file"
done

assert "SELECT VERSION() LIKE '8.4.%'" "1" "real MySQL 8.4"
for file in "$ROOT"/database/migrations/0{19,20,21,22}_c06_*.sql; do
  [[ "$file" == *.down.sql ]] && continue
  apply "$file"
done
apply "$ROOT/database/operations/c05_local_runtime_role.sql"
apply "$ROOT/database/migrations/023_c06_scoped_query_indexes.sql"
before_grants="$(sql "SHOW GRANTS FOR 'c05_identity_local_runtime'")"
sql "INSERT INTO c04_tenant_catalog VALUES(UNHEX(REPEAT('11',16)),'a','A',3,2,1,UTC_TIMESTAMP(6)),(UNHEX(REPEAT('22',16)),'b','B',3,2,1,UTC_TIMESTAMP(6));
INSERT INTO c05_identity_account(account_id,login_lookup_hmac,mailbox_ciphertext,mailbox_nonce,mailbox_tag,mailbox_key_version,password_phc,mailbox_verified_at_utc,account_state,created_at_utc,updated_at_utc) VALUES
(UNHEX(REPEAT('31',16)),UNHEX(REPEAT('31',32)),X'00',REPEAT(X'00',12),REPEAT(X'00',16),1,'synthetic',UTC_TIMESTAMP(6),0,UTC_TIMESTAMP(6),UTC_TIMESTAMP(6)),
(UNHEX(REPEAT('32',16)),UNHEX(REPEAT('32',32)),X'00',REPEAT(X'00',12),REPEAT(X'00',16),1,'synthetic',UTC_TIMESTAMP(6),0,UTC_TIMESTAMP(6),UTC_TIMESTAMP(6)),
(UNHEX(REPEAT('33',16)),UNHEX(REPEAT('33',32)),X'00',REPEAT(X'00',12),REPEAT(X'00',16),1,'synthetic',UTC_TIMESTAMP(6),0,UTC_TIMESTAMP(6),UTC_TIMESTAMP(6));
INSERT INTO c05_identity_membership(tenant_id,membership_id,account_id,membership_state,role_code,created_at_utc) VALUES
(UNHEX(REPEAT('11',16)),UNHEX(REPEAT('41',16)),UNHEX(REPEAT('31',16)),0,'MEMBER','2020-01-01 00:00:00.123456'),
(UNHEX(REPEAT('11',16)),UNHEX(REPEAT('42',16)),UNHEX(REPEAT('32',16)),0,'MEMBER','2020-01-01 00:00:00.123456'),
(UNHEX(REPEAT('22',16)),UNHEX(REPEAT('43',16)),UNHEX(REPEAT('33',16)),0,'MEMBER','2020-01-01 00:00:00.123456');"

app_password="$(openssl rand -hex 24)"
sql "CREATE USER 'c06_delivery_test'@'localhost' IDENTIFIED BY '$app_password'; GRANT 'c05_identity_local_runtime' TO 'c06_delivery_test'@'localhost'; SET DEFAULT ROLE 'c05_identity_local_runtime' TO 'c06_delivery_test'@'localhost';"
app_sql() {
  docker exec -e "MYSQL_PWD=$app_password" "$NAME" mysql --protocol=SOCKET -uc06_delivery_test -N -B -D crecisol_c05 -e "$1"
}
deny() {
  local error
  if error="$(app_sql "$1" 2>&1)"; then echo "FAIL C06-10 D unauthorized $2" >&2; exit 1; fi
  if [[ "$error" != *"ERROR $3 "* ]]; then
    printf 'FAIL C06-10 D unexpected SQL code for %s: %s\n' "$2" "$(printf '%s' "$error" | grep -oE 'ERROR [0-9]+' | head -n 1 || true)" >&2
    exit 1
  fi
  echo "PASS C06-10 D denied $2 (MySQL $3)"
}
[[ "$(app_sql "SELECT COUNT(*) FROM c05_identity_membership")" == "3" ]]
# Two tenants deliberately use the SAME branch and resource binary IDs.
# The authenticated runtime can read scoped rows but cannot mint authority.
T_A="UNHEX(REPEAT('11',16))"
T_B="UNHEX(REPEAT('22',16))"
A1="UNHEX(REPEAT('31',16))"
A2="UNHEX(REPEAT('32',16))"
B1="UNHEX(REPEAT('33',16))"
BR="UNHEX(REPEAT('71',16))"
RES="UNHEX(REPEAT('72',16))"
ASG="UNHEX(REPEAT('73',16))"
sql "INSERT INTO c05_identity_branch VALUES($T_A,$BR,TRUE),($T_B,$BR,TRUE);
INSERT INTO c05_identity_private_resource VALUES($T_A,$BR,$RES,$A2,1,'C06-10-A'),($T_B,$BR,$RES,$B1,1,'C06-10-B');"
assert "SELECT COUNT(*) FROM c05_identity_private_resource WHERE branch_id=$BR AND resource_id=$RES" "2" "same binary object IDs are legitimate across two tenants"
[[ "$(app_sql "SELECT display_reference FROM c05_identity_private_resource WHERE tenant_id=$T_A AND branch_id=$BR AND resource_id=$RES")" == "C06-10-A" ]]
[[ "$(app_sql "SELECT display_reference FROM c05_identity_private_resource WHERE tenant_id=$T_B AND branch_id=$BR AND resource_id=$RES")" == "C06-10-B" ]]
echo "PASS C06-10 D exact tenant lookup distinguishes deliberately colliding IDs"
insert="INSERT INTO c06_case_resource_assignment(tenant_id,assignment_id,branch_id,resource_id,account_id,approved_by_account_id,action_id,assignment_state,revision,issued_at_utc,expires_at_utc)"
suffix="'identity.private.read',1,1,UTC_TIMESTAMP(6)-INTERVAL 1 MINUTE,UTC_TIMESTAMP(6)+INTERVAL 1 HOUR"
sql "$insert VALUES($T_A,$ASG,$BR,$RES,$A1,$A2,$suffix)"
reject "$insert VALUES($T_B,UNHEX(REPEAT('74',16)),$BR,$RES,$A1,$B1,$suffix)" "cross-tenant actor despite colliding object IDs" 1452
reject "$insert VALUES($T_A,UNHEX(REPEAT('74',16)),$BR,$RES,$A1,$B1,$suffix)" "foreign approver despite colliding object IDs" 1452
reject "INSERT INTO c05_identity_private_resource VALUES($T_B,$BR,UNHEX(REPEAT('75',16)),$A2,1,'C06-10-FOREIGN')" "foreign resource owner" 1452
deny "$insert VALUES($T_B,UNHEX(REPEAT('74',16)),$BR,$RES,$B1,$B1,$suffix)" "runtime cannot mint assignment" 1142
deny "UPDATE c06_case_resource_assignment SET account_id=$A2 WHERE tenant_id=$T_A AND assignment_id=$ASG" "runtime cannot reassign ownership" 1142
deny "DELETE FROM c06_case_resource_assignment WHERE tenant_id=$T_A AND assignment_id=$ASG" "runtime cannot erase assignment" 1142
deny "UPDATE c05_identity_private_resource SET owner_account_id=$B1 WHERE tenant_id=$T_A" "runtime cannot transfer private resource" 1142
deny "UPDATE c05_identity_resource_grant SET permission_code='ADMIN'" "runtime cannot escalate grant" 1142
assert "SELECT COUNT(*) FROM c06_case_resource_assignment WHERE tenant_id=$T_A AND account_id=$A1 AND approved_by_account_id=$A2" "1" "original exact assignment preserved"
assert "SELECT COUNT(*) FROM c06_case_resource_assignment WHERE tenant_id=$T_B" "0" "foreign assignment remains absent"
assert "SELECT COUNT(*) FROM c05_identity_private_resource" "2" "original private rows remain unchanged"
[[ "$(sql "SHOW GRANTS FOR 'c05_identity_local_runtime'")" == "$before_grants" ]]
[[ "$(app_sql "SELECT COUNT(*) FROM c05_identity_membership")" == "3" ]]
echo "PASS C06-10 D MYSQL: three exact FK denials, five exact runtime privilege denials, collision isolation and intact grants"
