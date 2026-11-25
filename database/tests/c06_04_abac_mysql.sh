#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
NAME="crecisol-c06-abac-$$"
PASSWORD="$(openssl rand -hex 24)"
IMAGE='mysql:8.4@sha256:a81d043930e51745522bd5c430961cf8286f0a23f0a9a2a6e0108f7ea71b0902'
cleanup() { docker rm -f "$NAME" >/dev/null 2>&1 || true; }
trap cleanup EXIT
docker run --rm -d --name "$NAME" -p 127.0.0.1::3306 \
  -e "MYSQL_ROOT_PASSWORD=$PASSWORD" -e MYSQL_DATABASE=crecisol_c05 "$IMAGE" >/dev/null
ready=0
for _ in $(seq 1 120); do
  if docker exec -e "MYSQL_PWD=$PASSWORD" "$NAME" mysql -uroot -Nse 'SELECT 1' >/dev/null 2>&1; then
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
  [[ "$actual" == "$2" ]] || { echo "FAIL C06-04 $3 expected=$2 actual=$actual" >&2; exit 1; }
  echo "PASS C06-04 $3"
}
rejected=0
reject() {
  if sql "$1" >/dev/null 2>&1; then
    echo "FAIL C06-04 unexpectedly permitted: $2" >&2; exit 1
  fi
  rejected=$((rejected+1))
  echo "PASS C06-04 rejected $2"
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

sql "INSERT INTO c06_case_resource_assignment(tenant_id,assignment_id,branch_id,resource_id,account_id,approved_by_account_id,action_id,assignment_state,revision,issued_at_utc,expires_at_utc) VALUES
(UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA'),UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA51'),
 UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA21'),UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA31'),
 UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA11'),UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA12'),
 'cases.read',1,1,UTC_TIMESTAMP(6)-INTERVAL 1 HOUR,UTC_TIMESTAMP(6)+INTERVAL 1 DAY);"
assert "SELECT COUNT(*) FROM c06_case_resource_assignment WHERE tenant_id=UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA') AND branch_id=UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA21') AND resource_id=UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA31') AND account_id=UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA11') AND action_id='cases.read' AND assignment_state=1 AND revoked_at_utc IS NULL AND issued_at_utc<=UTC_TIMESTAMP(6) AND expires_at_utc>UTC_TIMESTAMP(6)" "1" "one exact current A actor A branch A resource assignment"
assert "SELECT COUNT(*) FROM c06_case_resource_assignment WHERE tenant_id=UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA') AND resource_id=UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA32')" "0" "same branch coworker resource has zero access by assignment"
assert "SELECT COUNT(*) FROM c06_case_resource_assignment WHERE tenant_id=UNHEX('BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB')" "0" "B tenant has no inherited A assignment"

# Each of these must be rejected by MySQL 8.4 using real FK/CHECK/UNIQUE/triggers.
# A new assignment id is used so a duplicate key cannot mask the intended cause.
A="UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA')"
B="UNHEX('BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB')"
MEMBER="UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA11')"
OWNER="UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA12')"
B_MEMBER="UNHEX('BBBBBBBBBBBBBBBBBBBBBBBBBBBBBB11')"
BRANCH="UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA21')"
OTHER_BRANCH="UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA22')"
RESOURCE="UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA31')"
OTHER_RESOURCE="UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA32')"
B_BRANCH="UNHEX('BBBBBBBBBBBBBBBBBBBBBBBBBBBBBB21')"
B_RESOURCE="UNHEX('BBBBBBBBBBBBBBBBBBBBBBBBBBBBBB31')"
SEED="UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA61')"
insert="INSERT INTO c06_case_resource_assignment(tenant_id,assignment_id,branch_id,resource_id,account_id,approved_by_account_id,action_id,assignment_state,revision,issued_at_utc,expires_at_utc)"
window="1,1,UTC_TIMESTAMP(6)-INTERVAL 1 HOUR,UTC_TIMESTAMP(6)+INTERVAL 1 DAY"
reject "$insert VALUES($A,$SEED,$BRANCH,$RESOURCE,$B_MEMBER,$OWNER,'cases.read',$window)" "foreign company account cannot be assigned into tenant A"
reject "$insert VALUES($A,$SEED,$B_BRANCH,$B_RESOURCE,$MEMBER,$OWNER,'cases.read',$window)" "foreign company branch/resource cannot be assigned into tenant A"
reject "$insert VALUES($A,$SEED,$BRANCH,$RESOURCE,$MEMBER,$B_MEMBER,'cases.read',$window)" "foreign company approver cannot mint tenant A assignment"
reject "$insert VALUES($A,$SEED,$OTHER_BRANCH,$RESOURCE,$MEMBER,$OWNER,'cases.read',$window)" "branch spoofing with same-company case fails FK"
reject "$insert VALUES($A,$SEED,$BRANCH,$B_RESOURCE,$MEMBER,$OWNER,'cases.read',$window)" "foreign resource fails resource FK"
reject "$insert VALUES($A,$SEED,$BRANCH,$RESOURCE,$MEMBER,$OWNER,'cases.write',$window)" "unregistered action cannot be granted"
reject "$insert VALUES($A,$SEED,$BRANCH,$RESOURCE,$MEMBER,$OWNER,'*',$window)" "wildcard action never granted"
reject "$insert VALUES($A,$SEED,$BRANCH,$RESOURCE,$MEMBER,$OWNER,'cases.read',0,1,UTC_TIMESTAMP(6),UTC_TIMESTAMP(6)+INTERVAL 1 DAY)" "invalid assignment state"
reject "$insert VALUES($A,$SEED,$BRANCH,$RESOURCE,$MEMBER,$OWNER,'cases.read',1,0,UTC_TIMESTAMP(6),UTC_TIMESTAMP(6)+INTERVAL 1 DAY)" "zero revision rejected"
reject "$insert VALUES($A,$SEED,$BRANCH,$RESOURCE,$MEMBER,$OWNER,'cases.read',1,1,UTC_TIMESTAMP(6),UTC_TIMESTAMP(6)-INTERVAL 1 DAY)" "negative validity window rejected"
reject "$insert VALUES($A,$SEED,$BRANCH,$RESOURCE,$MEMBER,$OWNER,'cases.read',1,1,UTC_TIMESTAMP(6),UTC_TIMESTAMP(6)+INTERVAL 45 DAY)" "overlong validity window rejected"
reject "$insert VALUES($A,$SEED,$BRANCH,$RESOURCE,$MEMBER,$OWNER,'cases.read',3,1,UTC_TIMESTAMP(6),UTC_TIMESTAMP(6)+INTERVAL 1 DAY)" "revoked state without revoked timestamp rejected"
reject "$insert VALUES($A,$SEED,$BRANCH,$RESOURCE,$MEMBER,$OWNER,'cases.read',1,1,UTC_TIMESTAMP(6),UTC_TIMESTAMP(6)+INTERVAL 1 DAY)" "duplicate tenant/branch/resource/actor/action rejected"
reject "UPDATE c06_case_resource_assignment SET tenant_id=$B,revision=2 WHERE assignment_id=UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA51')" "cross-tenant reparenting rejected"
reject "UPDATE c06_case_resource_assignment SET branch_id=$OTHER_BRANCH,revision=2 WHERE assignment_id=UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA51')" "cross-branch reparenting rejected"
reject "UPDATE c06_case_resource_assignment SET resource_id=$OTHER_RESOURCE,revision=2 WHERE assignment_id=UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA51')" "same-branch cross-resource reparenting rejected"
reject "UPDATE c06_case_resource_assignment SET account_id=$OWNER,revision=2 WHERE assignment_id=UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA51')" "assignment cannot silently transfer to coworker"
reject "UPDATE c06_case_resource_assignment SET action_id='cases.write',revision=2 WHERE assignment_id=UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA51')" "action change rejected"
reject "UPDATE c06_case_resource_assignment SET revision=0 WHERE assignment_id=UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA51')" "revision rollback rejected"
reject "DELETE FROM c06_case_resource_assignment WHERE assignment_id=UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA51')" "assignment cannot be deleted instead of revoked"

sql "UPDATE c06_case_resource_assignment SET assignment_state=3,revision=revision+1,revoked_at_utc=UTC_TIMESTAMP(6) WHERE tenant_id=$A AND assignment_id=UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA51')"
assert "SELECT CONCAT(assignment_state,':',revision,':',revoked_at_utc IS NOT NULL) FROM c06_case_resource_assignment WHERE assignment_id=UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA51')" "3:2:1" "revocation is persisted with monotonic revision"
reject "UPDATE c06_case_resource_assignment SET assignment_state=1,revision=revision+1,revoked_at_utc=NULL WHERE assignment_id=UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA51')" "revocation cannot be resurrected"
reject "UPDATE c06_case_resource_assignment SET revision=revision+1,revoked_at_utc=NULL WHERE assignment_id=UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA51')" "revocation marker cannot be cleared"
assert "SELECT COUNT(*) FROM c06_case_resource_assignment WHERE assignment_state=1" "0" "no active assignments after revocation"

# Runtime API can SELECT assignments only, never issue, change or revoke them.
sql "CREATE ROLE IF NOT EXISTS 'c05_identity_local_runtime';"
apply "$ROOT/database/operations/c06_04_local_runtime_role.sql"
grants="$(sql "SHOW GRANTS FOR 'c05_identity_local_runtime'")"
[[ "$grants" == *"SELECT"*"c06_case_resource_assignment"* ]] || {
  echo "FAIL C06-04 missing limited SELECT runtime grant" >&2; exit 1;
}
[[ "$grants" != *"INSERT, UPDATE"*"c06_case_resource_assignment"* ]] || {
  echo "FAIL C06-04 unexpected DML grant" >&2; exit 1;
}
echo "PASS C06-04 runtime role has SELECT-only to C06 assignments"

apply "$ROOT/database/migrations/020_c06_case_abac.down.sql"
assert "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE() AND table_name='c06_case_resource_assignment'" "0" "rollback removes C06-04 table"
assert "SELECT COUNT(*) FROM c05_identity_private_resource" "4" "rollback preserves C05 tenant-private resources"
assert "SELECT COUNT(*) FROM c05_identity_membership" "3" "rollback preserves C05 tenant membership"
echo "C06-04 MYSQL PASS: $rejected rejected hostile SQL operations, exact tenant+branch+case assignment, revocation and rollback; no product data."
