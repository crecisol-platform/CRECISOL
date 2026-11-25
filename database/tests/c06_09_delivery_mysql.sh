#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
NAME="crecisol-c06-delivery-$$"
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
  [[ "$actual" == "$2" ]] || { echo "FAIL C06-09 D $3 expected=$2 actual=$actual" >&2; exit 1; }
  echo "PASS C06-09 D $3"
}
rejected=0
reject() {
  local error
  if error="$(sql "$1" 2>&1)"; then
    echo "FAIL C06-09 D unexpectedly permitted: $2" >&2; exit 1
  fi
  [[ "$error" == *"ERROR $3 "* ]] || { echo "FAIL C06-09 D wrong SQL rejection: $2 (expected $3)" >&2; exit 1; }
  rejected=$((rejected+1))
  echo "PASS C06-09 D rejected $2 (MySQL $3)"
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
  if error="$(app_sql "$1" 2>&1)"; then echo "FAIL C06-09 D unauthorized $2" >&2; exit 1; fi
  if [[ "$error" != *"ERROR $3 "* ]]; then
    printf 'FAIL C06-09 D unexpected SQL code for %s: %s\n' "$2" "$(printf '%s' "$error" | grep -oE 'ERROR [0-9]+' | head -n 1 || true)" >&2
    exit 1
  fi
  echo "PASS C06-09 D denied $2 (MySQL $3)"
}
[[ "$(app_sql "SELECT COUNT(*) FROM c05_identity_membership")" == "3" ]]
app_sql "INSERT INTO c05_identity_audit VALUES(UNHEX(REPEAT('11',16)),UNHEX(REPEAT('51',16)),UNHEX(REPEAT('31',16)),NULL,'profile/photo',1,UNHEX(REPEAT('61',32)),UTC_TIMESTAMP(6)); INSERT INTO c05_platform_identity_audit(audit_id,operator_id,case_id,action_code,allowed,created_at_utc) VALUES(UNHEX(REPEAT('52',16)),NULL,NULL,'platform/profile/photo',TRUE,UTC_TIMESTAMP(6));"
[[ "$(app_sql "SELECT COUNT(*) FROM c05_identity_audit WHERE tenant_id=UNHEX(REPEAT('11',16))")" == "1" ]]
echo "PASS C06-09 D authenticated runtime appends and reads intended tenant audit"
deny "UPDATE c05_identity_audit SET action_code='FORGED'" "tenant audit update" 1142
deny "DELETE FROM c05_identity_audit" "tenant audit deletion" 1142
deny "SELECT * FROM c05_platform_identity_audit" "raw platform audit read" 1142
deny "UPDATE c05_platform_identity_audit SET allowed=0" "platform audit update" 1142
deny "DELETE FROM c05_platform_identity_audit" "platform audit deletion" 1142
deny "DROP TABLE c05_identity_audit" "tenant audit DDL" 1142
app_sql "INSERT INTO c05_identity_scoped_profile(tenant_id,account_id,protected_profile,photo_state,updated_at_utc) VALUES(UNHEX(REPEAT('11',16)),UNHEX(REPEAT('31',16)),X'00',0,UTC_TIMESTAMP(6));"
deny "INSERT INTO c05_identity_scoped_profile(tenant_id,account_id,protected_profile,photo_state,updated_at_utc) VALUES(UNHEX(REPEAT('22',16)),UNHEX(REPEAT('31',16)),X'00',0,UTC_TIMESTAMP(6))" "profile without matching tenant membership" 1452
deny "UPDATE c05_identity_scoped_profile SET photo_state=2,photo_png=NULL" "inconsistent photo state" 3819
assert "SELECT COUNT(*) FROM c05_identity_audit WHERE action_code='profile/photo'" "1" "tenant audit evidence unchanged"
assert "SELECT COUNT(*) FROM c05_platform_identity_audit WHERE action_code='platform/profile/photo'" "1" "platform audit evidence unchanged"
assert "SELECT COUNT(*) FROM c05_identity_scoped_profile" "1" "profile FK and state guard preserve row"
[[ "$(sql "SHOW GRANTS FOR 'c05_identity_local_runtime'")" == "$before_grants" ]]
[[ "$(app_sql "SELECT COUNT(*) FROM c05_identity_membership")" == "3" ]]
echo "PASS C06-09 D MYSQL: append-only runtime evidence, isolated profile keys, unchanged grants"
