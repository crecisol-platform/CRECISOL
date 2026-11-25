#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
NAME="crecisol-c06-privacy-$$"
PASSWORD="$(openssl rand -hex 24)"
IMAGE='mysql:8.4@sha256:a81d043930e51745522bd5c430961cf8286f0a23f0a9a2a6e0108f7ea71b0902'
cleanup() { docker rm -f "$NAME" >/dev/null 2>&1 || true; }
trap cleanup EXIT
docker run --rm -d --name "$NAME" -p 127.0.0.1::3306 \
  -e "MYSQL_ROOT_PASSWORD=$PASSWORD" -e MYSQL_DATABASE=crecisol_c05 "$IMAGE" >/dev/null
ready=0
for _ in $(seq 1 120); do
  if docker exec -e "MYSQL_PWD=$PASSWORD" "$NAME" mysql -uroot -Nse 'SELECT 1' >/dev/null 2>&1; then
    if docker logs "$NAME" 2>&1 | grep -q 'MySQL init process done'; then ready=1; break; fi
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
  [[ "$actual" == "$2" ]] || { echo "FAIL C06-07 D $3 expected=$2 actual=$actual" >&2; exit 1; }
  echo "PASS C06-07 D $3"
}
rejected=0
reject() {
  local error
  if error="$(sql "$1" 2>&1)"; then
    echo "FAIL C06-07 D unexpectedly permitted: $2" >&2; exit 1
  fi
  [[ "$error" == *"ERROR $3 "* ]] || { echo "FAIL C06-07 D wrong SQL rejection: $2 (expected $3)" >&2; exit 1; }
  rejected=$((rejected+1))
  echo "PASS C06-07 D rejected $2 (MySQL $3)"
}
for file in "$ROOT"/database/migrations/[0-9][0-9][0-9]_c05_*.sql; do
  [[ "$file" == *.down.sql ]] && continue
  apply "$file"
done
assert "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE() AND table_name='c05_identity_private_resource'" "1" "C05 private resource baseline present"
apply "$ROOT/database/migrations/020_c06_case_abac.sql"
assert "SELECT COUNT(*) FROM c06_case_resource_assignment" "0" "migration creates NO assignments, privilege or user"
assert "SELECT COUNT(*) FROM information_schema.table_constraints WHERE table_schema=DATABASE() AND table_name='c06_case_resource_assignment' AND constraint_type='FOREIGN KEY'" "3" "three composite tenant-scoped foreign keys"

sql "INSERT INTO c04_tenant_catalog(tenant_id,internal_name,display_name,lifecycle_state,address_mode,catalog_revision,updated_at_utc) VALUES
(UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA'),'tenant-a','Tenant A',3,2,1,UTC_TIMESTAMP(6)),
(UNHEX('BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB'),'tenant-b','Tenant B',3,2,1,UTC_TIMESTAMP(6));"
sql "INSERT INTO c05_identity_account(account_id,login_lookup_hmac,mailbox_ciphertext,mailbox_nonce,mailbox_tag,mailbox_key_version,password_phc,account_state,created_at_utc,updated_at_utc) VALUES
(UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA11'),REPEAT(UNHEX('11'),32),X'11',REPEAT(UNHEX('11'),12),REPEAT(UNHEX('11'),16),1,'synthetic-not-a-login',1,UTC_TIMESTAMP(6),UTC_TIMESTAMP(6)),
(UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA12'),REPEAT(UNHEX('12'),32),X'12',REPEAT(UNHEX('12'),12),REPEAT(UNHEX('12'),16),1,'synthetic-not-a-login',1,UTC_TIMESTAMP(6),UTC_TIMESTAMP(6)),
(UNHEX('BBBBBBBBBBBBBBBBBBBBBBBBBBBBBB11'),REPEAT(UNHEX('21'),32),X'21',REPEAT(UNHEX('21'),12),REPEAT(UNHEX('21'),16),1,'synthetic-not-a-login',1,UTC_TIMESTAMP(6),UTC_TIMESTAMP(6));"
sql "INSERT INTO c05_identity_membership(tenant_id,membership_id,account_id,membership_state,role_code,approval_revision,created_at_utc,granted_at_utc,expires_at_utc) VALUES
(UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA'),UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA41'),UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA11'),1,'MEMBER',1,UTC_TIMESTAMP(6)-INTERVAL 1 DAY,UTC_TIMESTAMP(6)-INTERVAL 1 DAY,UTC_TIMESTAMP(6)+INTERVAL 1 DAY),
(UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA'),UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA42'),UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA12'),1,'MEMBER',1,UTC_TIMESTAMP(6)-INTERVAL 1 DAY,UTC_TIMESTAMP(6)-INTERVAL 1 DAY,UTC_TIMESTAMP(6)+INTERVAL 1 DAY),
(UNHEX('BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB'),UNHEX('BBBBBBBBBBBBBBBBBBBBBBBBBBBBBB41'),UNHEX('BBBBBBBBBBBBBBBBBBBBBBBBBBBBBB11'),1,'MEMBER',1,UTC_TIMESTAMP(6)-INTERVAL 1 DAY,UTC_TIMESTAMP(6)-INTERVAL 1 DAY,UTC_TIMESTAMP(6)+INTERVAL 1 DAY);"
sql "INSERT INTO c05_identity_branch(tenant_id,branch_id,active) VALUES
(UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA'),UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA21'),1),
(UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA'),UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA22'),1),
(UNHEX('BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB'),UNHEX('BBBBBBBBBBBBBBBBBBBBBBBBBBBBBB21'),1);"
sql "INSERT INTO c05_identity_private_resource(tenant_id,branch_id,resource_id,owner_account_id,resource_state,display_reference) VALUES
(UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA'),UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA21'),UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA31'),UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA12'),1,'CASE-A-ASSIGNABLE'),
(UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA'),UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA21'),UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA32'),UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA12'),1,'CASE-A-PRIVATE'),
(UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA'),UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA22'),UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA33'),UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA12'),1,'CASE-A-OTHER-BRANCH'),
(UNHEX('BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB'),UNHEX('BBBBBBBBBBBBBBBBBBBBBBBBBBBBBB21'),UNHEX('BBBBBBBBBBBBBBBBBBBBBBBBBBBBBB31'),UNHEX('BBBBBBBBBBBBBBBBBBBBBBBBBBBBBB11'),1,'CASE-B-PRIVATE');"



apply "$ROOT/database/migrations/021_c06_exceptional_access.sql"
apply "$ROOT/database/migrations/022_c06_privacy_disposition.sql"
assert "SELECT VERSION() LIKE '8.4.%'" "1" "real MySQL 8.4"
A="UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA')"
B="UNHEX('BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB')"
AA="UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA11')"
BB="UNHEX('BBBBBBBBBBBBBBBBBBBBBBBBBBBBBB11')"
T="UTC_TIMESTAMP(6)"
# Synthetic payloads and retained keys. No actual secrets.
sql "INSERT INTO c05_identity_recovery(account_id,recovery_id,token_hmac,created_at_utc,expires_at_utc,consumed_at_utc,tenant_id,purpose,account_revision,protected_payload) VALUES
($AA,UNHEX(REPEAT('01',16)),UNHEX(REPEAT('01',32)),$T-INTERVAL 20 MINUTE,$T-INTERVAL 10 MINUTE,NULL,$A,'RECOVERY',1,X'1122'),
($BB,UNHEX(REPEAT('02',16)),UNHEX(REPEAT('02',32)),$T-INTERVAL 5 MINUTE,$T+INTERVAL 5 MINUTE,$T-INTERVAL 1 MINUTE,$B,'MAILBOX',1,X'3344'),
($AA,UNHEX(REPEAT('03',16)),UNHEX(REPEAT('03',32)),$T-INTERVAL 1 MINUTE,$T+INTERVAL 9 MINUTE,NULL,$A,'RECOVERY',1,X'5566'),
($BB,UNHEX(REPEAT('04',16)),UNHEX(REPEAT('04',32)),$T-INTERVAL 1 MINUTE,$T+INTERVAL 9 MINUTE,NULL,$B,'MAILBOX',1,X'7788');
INSERT INTO c05_identity_sensitive_outbox VALUES
(UNHEX(REPEAT('11',16)),X'0102',$T-INTERVAL 1 MINUTE,NULL),
(UNHEX(REPEAT('12',16)),X'0304',$T+INTERVAL 9 MINUTE,$T-INTERVAL 1 SECOND),
(UNHEX(REPEAT('13',16)),X'0506',$T+INTERVAL 9 MINUTE,NULL);"
APP_PASSWORD="$(openssl rand -hex 24)"
apply "$ROOT/database/operations/c06_07_local_privacy_worker.sql"
sql "CREATE USER 'c06_privacy_test'@'localhost' IDENTIFIED BY '$APP_PASSWORD'; GRANT 'c06_privacy_local_worker' TO 'c06_privacy_test'@'localhost'; SET DEFAULT ROLE 'c06_privacy_local_worker' TO 'c06_privacy_test'@'localhost';"
worker_sql() {
 docker exec -e "MYSQL_PWD=$APP_PASSWORD" "$NAME" mysql -uc06_privacy_test -N -B -D crecisol_c05 -e "$1"
}
deny_worker() {
 local err
 if err="$(worker_sql "$1" 2>&1)"; then echo "FAIL C06-07 D unexpected worker permission: $2" >&2; exit 1; fi
 [[ "$err" == *"ERROR $3 "* ]] || { echo "FAIL C06-07 D wrong error for $2" >&2; exit 1; }
 rejected=$((rejected+1))
 echo "PASS C06-07 D rejected $2 (MySQL $3)"
}
for n in 0 101 NULL; do deny_worker "CALL c06_dispose_closed_sensitive_payloads($n)" "invalid batch $n" 1644; done
[[ "$(worker_sql 'CALL c06_dispose_closed_sensitive_payloads(1)')" == "ACK" ]]
assert "SELECT COUNT(*) FROM c05_identity_recovery WHERE OCTET_LENGTH(protected_payload)=0" "1" "bounded recovery batch"
assert "SELECT COUNT(*) FROM c05_identity_sensitive_outbox WHERE OCTET_LENGTH(protected_payload)=0" "1" "bounded outbox batch"
[[ "$(worker_sql 'CALL c06_dispose_closed_sensitive_payloads(1)')" == "ACK" ]]
assert "SELECT COUNT(*) FROM c05_identity_recovery WHERE OCTET_LENGTH(protected_payload)=0" "2" "expired and consumed recovery payloads disposed"
assert "SELECT COUNT(*) FROM c05_identity_sensitive_outbox WHERE OCTET_LENGTH(protected_payload)=0" "2" "expired and delivered outbox payloads disposed"
[[ "$(worker_sql 'CALL c06_dispose_closed_sensitive_payloads(100)')" == "ACK" ]]
assert "SELECT COUNT(*) FROM c05_identity_recovery WHERE HEX(protected_payload) IN ('5566','7788') AND consumed_at_utc IS NULL AND expires_at_utc>UTC_TIMESTAMP(6)" "2" "live payloads in both tenants remain intact"
assert "SELECT HEX(protected_payload) FROM c05_identity_sensitive_outbox WHERE message_id=UNHEX(REPEAT('13',16))" "0506" "live undelivered payload intact"
assert "SELECT COUNT(*) FROM c05_identity_recovery WHERE OCTET_LENGTH(protected_payload)=0 AND ((recovery_id=UNHEX(REPEAT('01',16)) AND token_hmac=UNHEX(REPEAT('01',32)) AND tenant_id=$A) OR (recovery_id=UNHEX(REPEAT('02',16)) AND token_hmac=UNHEX(REPEAT('02',32)) AND tenant_id=$B AND consumed_at_utc IS NOT NULL))" "2" "retained keys, tenant and consumption evidence"
assert "SELECT COUNT(*) FROM c05_identity_sensitive_outbox" "3" "outbox evidence retained"
deny_worker "SELECT protected_payload FROM c05_identity_recovery" "read recovery payload" 1142
deny_worker "SELECT protected_payload FROM c05_identity_sensitive_outbox" "read outbox payload" 1142
deny_worker "UPDATE c05_identity_recovery SET consumed_at_utc=UTC_TIMESTAMP(6)" "modify proof state" 1142
deny_worker "DELETE FROM c05_identity_recovery" "delete evidence" 1142
deny_worker "UPDATE c05_identity_sensitive_outbox SET protected_payload=X'FF'" "write outbox payload" 1142
deny_worker "SELECT * FROM c05_identity_account" "read accounts" 1142
# No product/runtime privilege is created by the maintenance role.
assert "SELECT COUNT(*) FROM mysql.procs_priv WHERE Db=DATABASE() AND Routine_name='c06_dispose_closed_sensitive_payloads' AND User='c05_identity_local_runtime'" "0" "no runtime execution grant"
apply "$ROOT/database/migrations/022_c06_privacy_disposition.down.sql"
assert "SELECT COUNT(*) FROM information_schema.routines WHERE routine_schema=DATABASE() AND routine_name='c06_dispose_closed_sensitive_payloads'" "0" "rollback removes routine only"
assert "SELECT COUNT(*) FROM c05_identity_recovery" "4" "rollback preserves proof rows"
assert "SELECT COUNT(*) FROM c05_identity_private_resource" "4" "rollback preserves unrelated resources"
assert "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE() AND table_name LIKE 'c06_exception_%'" "5" "rollback preserves exceptional audit schema"
echo "PASS C06-07 D MYSQL: $rejected denied operations; bounded disposal, neutral ACK, idempotence and preservation"
