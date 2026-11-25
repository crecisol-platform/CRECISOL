#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
NAME="crecisol-c06-exception-$$"
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
  [[ "$actual" == "$2" ]] || { echo "FAIL C06-06 D $3 expected=$2 actual=$actual" >&2; exit 1; }
  echo "PASS C06-06 D $3"
}
rejected=0
reject() {
  local error
  if error="$(sql "$1" 2>&1)"; then
    echo "FAIL C06-06 D unexpectedly permitted: $2" >&2; exit 1
  fi
  [[ "$error" == *"ERROR $3 "* ]] || { echo "FAIL C06-06 D wrong SQL rejection: $2 (expected $3)" >&2; exit 1; }
  rejected=$((rejected+1))
  echo "PASS C06-06 D rejected $2 (MySQL $3)"
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
assert "SELECT VERSION() LIKE '8.4.%'" "1" "real MySQL 8.4"
assert "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE() AND table_name LIKE 'c06_exception_%'" "5" "five isolated exception tables"
assert "SELECT COUNT(*) FROM c06_exception_entitlement" "0" "no entitlement seeds"
assert "SELECT COUNT(*) FROM c05_platform_operator" "0" "no operator seeds"
# Fixed synthetics; not usable authentication material. Only DB integrity is tested.
R="UNHEX('11111111111111111111111111111111')"
V="UNHEX('22222222222222222222222222222222')"
I="UNHEX('33333333333333333333333333333333')"
RS="UNHEX('11111111111111111111111111111112')"
VS="UNHEX('22222222222222222222222222222223')"
A="UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA')"
B="UNHEX('BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB')"
BR="UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA21')"
RES="UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA31')"
BRB="UNHEX('BBBBBBBBBBBBBBBBBBBBBBBBBBBBBB21')"
RESB="UNHEX('BBBBBBBBBBBBBBBBBBBBBBBBBBBBBB31')"
GR="UNHEX('11111111111111111111111111111113')"
GV="UNHEX('22222222222222222222222222222224')"
GS="UNHEX('11111111111111111111111111111114')"
GB="UNHEX('22222222222222222222222222222225')"
Q="UNHEX('44444444444444444444444444444444')"
Q2="UNHEX('55555555555555555555555555555555')"
Q3="UNHEX('66666666666666666666666666666666')"
X="UNHEX('77777777777777777777777777777777')"
STAMP="$(sql 'SELECT UTC_TIMESTAMP(6)')"
T="TIMESTAMP('$STAMP')"
ISS="$T-INTERVAL 1 HOUR"
EXP="$T+INTERVAL 1 HOUR"
REQ="$T-INTERVAL 30 SECOND"
APP="$T-INTERVAL 20 SECOND"
UNTIL="$T+INTERVAL 5 MINUTE"
DIG="UNHEX(REPEAT('ab',32))"
sql "INSERT INTO c05_platform_operator(operator_id,login_hmac,conflict_login_hmac,password_phc,protected_seed,active,security_revision,last_totp_step) VALUES
($R,UNHEX(REPEAT('11',32)),UNHEX(REPEAT('21',32)),'synthetic-not-a-login',X'11',1,1,NULL),
($V,UNHEX(REPEAT('12',32)),UNHEX(REPEAT('22',32)),'synthetic-not-a-login',X'12',1,1,NULL),
($I,UNHEX(REPEAT('13',32)),UNHEX(REPEAT('23',32)),'synthetic-not-a-login',X'13',1,1,NULL);
INSERT INTO c05_platform_session(token_hmac,operator_id,security_revision,mfa_at_utc,expires_at_utc,last_seen_at_utc,session_id,created_at_utc,auth_method) VALUES
(UNHEX(REPEAT('31',32)),$R,1,$ISS,$EXP,$T,$RS,$ISS,'TOTP'),
(UNHEX(REPEAT('32',32)),$V,1,$ISS,$EXP,$T,$VS,$ISS,'TOTP');"
grant="INSERT INTO c06_exception_entitlement(entitlement_id,operator_id,issued_by,security_revision,tenant_id,branch_id,resource_id,capability,issued_at_utc,expires_at_utc)"
sql "$grant VALUES($GR,$R,$I,1,$A,$BR,$RES,1,$ISS,$EXP),($GV,$V,$I,1,$A,$BR,$RES,2,$ISS,$EXP),($GS,$R,$I,1,$A,$BR,$RES,2,$ISS,$EXP),($GB,$V,$I,1,$B,$BRB,$RESB,2,$ISS,$EXP);"
reject "$grant VALUES($X,$R,$R,1,$A,$BR,$RES,1,$ISS,$EXP)" "self-issued entitlement" 3819
reject "$grant VALUES($X,$R,$I,1,$A,$BRB,$RESB,1,$ISS,$EXP)" "cross-tenant branch/resource entitlement" 1452
reject "$grant VALUES($X,$R,$I,1,$A,$BR,$RES,3,$ISS,$EXP)" "unregistered capability" 3819
reject "$grant VALUES($X,$R,$I,1,$A,$BR,$RES,1,$ISS,$T+INTERVAL 31 DAY)" "overlong entitlement" 3819
request="INSERT INTO c06_exception_request VALUES"
approve="INSERT INTO c06_exception_approval VALUES"
close="INSERT INTO c06_exception_close VALUES"
ledger="INSERT INTO c06_exception_audit VALUES"
sql "$request($Q,$R,$RS,1,$GR,$A,$BR,$RES,$DIG,$REQ,$UNTIL),($Q2,$R,$RS,1,$GR,$A,$BR,$RES,$DIG,$REQ,$UNTIL),($Q3,$R,$RS,1,$GR,$A,$BR,$RES,$DIG,$REQ,$UNTIL)"
reject "$request($X,$R,$VS,1,$GR,$A,$BR,$RES,$DIG,$REQ,$UNTIL)" "session belongs to different operator" 1644
reject "$request($X,$R,$RS,1,$GS,$A,$BR,$RES,$DIG,$REQ,$UNTIL)" "review entitlement cannot request" 1644
reject "$request($X,$R,$RS,1,$GR,$A,$BR,$RES,$DIG,$REQ,$T+INTERVAL 11 MINUTE)" "request exceeds ten-minute limit" 3819
reject "$approve($Q,$R,$RS,1,$GS,$DIG,$APP,$UNTIL)" "self-review even with review eligibility" 1644
reject "$approve($Q,$V,$VS,1,$GB,$DIG,$APP,$UNTIL)" "review entitlement from another tenant" 1644
reject "$approve($Q,$V,$RS,1,$GV,$DIG,$APP,$UNTIL)" "reviewer using requester's session" 1644
reject "$approve($Q,$V,$VS,1,$GV,$DIG,$APP,$UNTIL+INTERVAL 1 MICROSECOND)" "approval extends request by one microsecond" 1644
reject "$ledger($X,$Q,$R,$VS,'platform/breakglass/open',1,$T)" "audit operator/session mismatch" 1644
reject "$ledger($X,$Q,$R,$RS,'platform/breakglass/read',1,$T)" "positive read audit without approval" 1644
reject "$ledger($X,$Q,$R,$RS,'platform/breakglass/close',1,$T)" "positive close audit without closure" 1644
reject "$close($Q,$V,$T)" "another operator cannot close" 1644
sql "$approve($Q,$V,$VS,1,$GV,$DIG,$APP,$UNTIL)"
reject "$approve($Q,$V,$VS,1,$GV,$DIG,$APP,$UNTIL)" "duplicate review" 1062
reject "$ledger($X,$Q,$R,$RS,'platform/breakglass/approve',1,$T)" "forged positive approval actor" 1644
sql "$ledger(UNHEX('88888888888888888888888888888881'),$Q,$R,$RS,'platform/breakglass/open',1,$T),
(UNHEX('88888888888888888888888888888882'),$Q,$V,$VS,'platform/breakglass/approve',1,$T),
(UNHEX('88888888888888888888888888888883'),$Q,$R,$RS,'platform/breakglass/read',1,$T),
(UNHEX('88888888888888888888888888888884'),NULL,$V,$VS,'platform/breakglass/read',0,$T)"
sql "$close($Q,$R,$T); $close($Q2,$R,$T); $ledger(UNHEX('88888888888888888888888888888885'),$Q,$R,$RS,'platform/breakglass/close',1,$T)"
reject "$approve($Q2,$V,$VS,1,$GV,$DIG,$APP,$UNTIL)" "approval after close" 1644
reject "$ledger($X,$Q,$R,$RS,'platform/breakglass/read',1,$T)" "positive read audit after close" 1644
reject "UPDATE c06_exception_request SET expires_at_utc=expires_at_utc+INTERVAL 1 SECOND WHERE exception_id=$Q" "request cannot be extended" 1644
reject "DELETE FROM c06_exception_approval WHERE exception_id=$Q" "approval cannot be erased" 1644
reject "DELETE FROM c06_exception_close WHERE exception_id=$Q" "closed state cannot be reopened" 1644
reject "UPDATE c06_exception_audit SET allowed=0 WHERE exception_id=$Q" "audit cannot be modified" 1644
reject "DELETE FROM c06_exception_audit WHERE exception_id=$Q" "audit cannot be deleted" 1644
reject "UPDATE c06_exception_entitlement SET capability=1 WHERE entitlement_id=$GV" "eligibility cannot change" 1644
sql "UPDATE c06_exception_entitlement SET revoked_at_utc=$T WHERE entitlement_id=$GV"
reject "UPDATE c06_exception_entitlement SET revoked_at_utc=NULL WHERE entitlement_id=$GV" "revocation irreversible" 1644
reject "$approve($Q3,$V,$VS,1,$GV,$DIG,$APP,$UNTIL)" "revoked eligibility cannot approve" 1644
assert "SELECT COUNT(*) FROM c06_exception_audit WHERE allowed=1 AND exception_id=$Q" "4" "four attributable positive transitions"
assert "SELECT COUNT(*) FROM c06_exception_audit WHERE allowed=0 AND exception_id IS NULL" "1" "unlinked denied attempt logged"
# Use actual runtime principal; a denied SQL must be a privilege failure, not a
# trigger/syntax failure. The role script is the production-boundary contract.
APP_PASSWORD="$(openssl rand -hex 24)"
sql "CREATE ROLE IF NOT EXISTS 'c05_identity_local_runtime'; CREATE USER 'c06_exception_test'@'localhost' IDENTIFIED BY '$APP_PASSWORD'; GRANT 'c05_identity_local_runtime' TO 'c06_exception_test'@'localhost'; SET DEFAULT ROLE 'c05_identity_local_runtime' TO 'c06_exception_test'@'localhost';"
apply "$ROOT/database/operations/c06_06_local_runtime_role.sql"
runtime_sql() {
  docker exec -e "MYSQL_PWD=$APP_PASSWORD" "$NAME" mysql -uc06_exception_test -N -B -D crecisol_c05 -e "$1"
}
runtime_reject() {
  local error
  if error="$(runtime_sql "$1" 2>&1)"; then
    echo "FAIL C06-06 D unexpected runtime permission: $2" >&2; exit 1
  fi
  [[ "$error" == *"ERROR 1142 "* ]] || { echo "FAIL C06-06 D wrong runtime rejection: $2" >&2; exit 1; }
  rejected=$((rejected+1))
  echo "PASS C06-06 D runtime rejected $2"
}
[[ "$(runtime_sql 'SELECT COUNT(*) FROM c06_exception_entitlement')" == "4" ]]
runtime_sql "$ledger($X,NULL,$V,$VS,'platform/breakglass/read',0,$T)"
runtime_reject "$grant VALUES(UNHEX('99999999999999999999999999999999'),$R,$I,1,$A,$BR,$RES,1,$ISS,$EXP)" "issue eligibility"
runtime_reject "UPDATE c06_exception_entitlement SET revoked_at_utc=$T WHERE entitlement_id=$GR" "change eligibility"
runtime_reject "DELETE FROM c06_exception_request WHERE exception_id=$Q" "erase request"
runtime_reject "UPDATE c06_exception_approval SET expires_at_utc=$T WHERE exception_id=$Q" "modify approval"
runtime_reject "DELETE FROM c06_exception_close WHERE exception_id=$Q" "erase closure"
runtime_reject "SELECT * FROM c06_exception_audit" "enumerate private audit"
runtime_reject "DELETE FROM c06_exception_audit WHERE event_id=$X" "erase audit"
assert "SELECT COUNT(*) FROM c06_exception_audit" "6" "runtime may append attributable denial, never read or erase ledger"
apply "$ROOT/database/migrations/021_c06_exceptional_access.down.sql"
assert "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE() AND table_name LIKE 'c06_exception_%'" "0" "rollback removes only exception tables"
assert "SELECT COUNT(*) FROM c05_identity_private_resource" "4" "rollback preserves existing private resources"
assert "SELECT COUNT(*) FROM c05_platform_operator" "3" "rollback preserves operators"
assert "SELECT COUNT(*) FROM c05_identity_membership" "3" "rollback preserves memberships"
echo "PASS C06-06 D MYSQL: $rejected independently rejected hostile operations; exact scope, immutable audit and rollback"
