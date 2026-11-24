#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
NAME="crecisol-c06-role-$$"
PASSWORD="$(openssl rand -hex 24)"
IMAGE='mysql:8.4@sha256:a81d043930e51745522bd5c430961cf8286f0a23f0a9a2a6e0108f7ea71b0902'
cleanup() { docker rm -f "$NAME" >/dev/null 2>&1 || true; }
trap cleanup EXIT
docker run --rm -d --name "$NAME" -e "MYSQL_ROOT_PASSWORD=$PASSWORD" \
  -e MYSQL_DATABASE=crecisol_c06_prepared "$IMAGE" >/dev/null
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
    mysql -uroot -N -B -D crecisol_c06_prepared -e "$1"
}
apply() {
  docker exec -i -e "MYSQL_PWD=$PASSWORD" "$NAME" \
    mysql -uroot crecisol_c06_prepared < "$1"
}
assert() {
  local actual
  actual="$(sql "$1")"
  [[ "$actual" == "$2" ]] || { echo "FAIL C06-02 $3 expected='$2' got='$actual'" >&2; exit 1; }
  echo "PASS C06-02 $3"
}
rejected=0
reject() {
  if sql "$1" >/dev/null 2>&1; then
    echo "FAIL C06-02 unexpectedly accepted $2" >&2; exit 1
  fi
  rejected=$((rejected+1))
  echo "PASS C06-02 rejected: $2"
}
# Entire real C05 migration chain, then C06 in a disposable MySQL 8.4 only.
for file in "$ROOT"/database/migrations/[0-9][0-9][0-9]_c05_*.sql; do
  [[ "$file" == *.down.sql ]] && continue
  apply "$file"
done
assert "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE() AND table_name='c05_identity_membership'" "1" "C05 membership baseline present"
apply "$ROOT/database/migrations/019_c06_prepared_roles.sql"
assert "SELECT COUNT(*) FROM c06_capability_registry" "0" "no capability seeded"
assert "SELECT COUNT(*) FROM c06_membership_role_proposal" "0" "no role assigned by migration"
sql "INSERT INTO c04_tenant_catalog(tenant_id,internal_name,display_name,lifecycle_state,address_mode,catalog_revision,updated_at_utc) VALUES
(UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA'),'company-a','Company A',3,2,1,UTC_TIMESTAMP(6)),
(UNHEX('BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB'),'company-b','Company B',3,2,1,UTC_TIMESTAMP(6));"
sql "INSERT INTO c05_identity_account(account_id,login_lookup_hmac,mailbox_ciphertext,mailbox_nonce,mailbox_tag,mailbox_key_version,password_phc,account_state,created_at_utc,updated_at_utc) VALUES
(UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA01'),REPEAT(UNHEX('11'),32),X'11',REPEAT(UNHEX('11'),12),REPEAT(UNHEX('11'),16),1,'synthetic-hash-not-a-password',1,UTC_TIMESTAMP(6),UTC_TIMESTAMP(6)),
(UNHEX('BBBBBBBBBBBBBBBBBBBBBBBBBBBBBB02'),REPEAT(UNHEX('22'),32),X'22',REPEAT(UNHEX('22'),12),REPEAT(UNHEX('22'),16),1,'synthetic-hash-not-a-password',1,UTC_TIMESTAMP(6),UTC_TIMESTAMP(6));"
sql "INSERT INTO c05_identity_membership(tenant_id,membership_id,account_id,membership_state,role_code,approval_revision,created_at_utc,granted_at_utc,expires_at_utc) VALUES
(UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA'),UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA31'),UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA01'),1,'STAFF',1,UTC_TIMESTAMP(6)-INTERVAL 1 DAY,UTC_TIMESTAMP(6)-INTERVAL 1 DAY,UTC_TIMESTAMP(6)+INTERVAL 1 DAY),
(UNHEX('BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB'),UNHEX('BBBBBBBBBBBBBBBBBBBBBBBBBBBBBB32'),UNHEX('BBBBBBBBBBBBBBBBBBBBBBBBBBBBBB02'),1,'STAFF',1,UTC_TIMESTAMP(6)-INTERVAL 1 DAY,UTC_TIMESTAMP(6)-INTERVAL 1 DAY,UTC_TIMESTAMP(6)+INTERVAL 1 DAY);"
sql "INSERT INTO c05_identity_branch(tenant_id,branch_id,active) VALUES
(UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA'),UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA11'),1),
(UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA'),UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA12'),1),
(UNHEX('BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB'),UNHEX('BBBBBBBBBBBBBBBBBBBBBBBBBBBBBB13'),1);"
sql "INSERT INTO c06_capability_registry(action_id,realm) VALUES ('cases.read',1),('privacy.subject.erase',1),('platform.support.metadata.read',2);"
sql "INSERT INTO c06_tenant_role(tenant_id,role_id,role_code) VALUES
(UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA'),UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA21'),'tenant-viewer'),
(UNHEX('BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB'),UNHEX('BBBBBBBBBBBBBBBBBBBBBBBBBBBBBB22'),'tenant-viewer');"
sql "INSERT INTO c06_role_capability_proposal(tenant_id,role_id,action_id) VALUES
(UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA'),UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA21'),'cases.read');"
sql "START TRANSACTION;
INSERT INTO c06_membership_role_proposal
(tenant_id,proposal_id,membership_id,role_id,branch_id,valid_from_utc,valid_until_utc) VALUES
(UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA'),UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA41'),
 UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA31'),UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA21'),
 UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA11'),UTC_TIMESTAMP(6)-INTERVAL 1 HOUR,UTC_TIMESTAMP(6)+INTERVAL 1 DAY);
INSERT INTO c06_role_proposal_audit
(tenant_id,audit_id,proposal_id,actor_account_id,event_kind,reason_hmac,at_utc) VALUES
(UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA'),UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA51'),
 UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA41'),UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA01'),
 1,REPEAT(UNHEX('33'),32),UTC_TIMESTAMP(6));
COMMIT;"
assert "SELECT COUNT(*) FROM c06_membership_role_proposal WHERE tenant_id=UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA') AND branch_id=UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA11')" "1" "A membership scoped to A branch"
assert "SELECT COUNT(*) FROM c06_membership_role_proposal WHERE tenant_id=UNHEX('BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB')" "0" "B has no inherited role"
assert "SELECT COUNT(*) FROM c06_role_capability_proposal p JOIN c06_capability_registry c ON c.action_id=p.action_id AND c.realm=p.realm WHERE c.grantable=1 OR p.proposal_only=0" "0" "zero grantable capabilities"
assert "SELECT COUNT(*) FROM c06_role_proposal_audit" "1" "audit event committed atomically"
reject "INSERT INTO c06_membership_role_proposal(tenant_id,proposal_id,membership_id,role_id,branch_id,valid_from_utc,valid_until_utc) VALUES (UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA'),UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA42'),UNHEX('BBBBBBBBBBBBBBBBBBBBBBBBBBBBBB32'),UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA21'),UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA11'),UTC_TIMESTAMP(6),UTC_TIMESTAMP(6)+INTERVAL 1 DAY)" "B membership cannot use A role"
reject "INSERT INTO c06_membership_role_proposal(tenant_id,proposal_id,membership_id,role_id,branch_id,valid_from_utc,valid_until_utc) VALUES (UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA'),UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA42'),UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA31'),UNHEX('BBBBBBBBBBBBBBBBBBBBBBBBBBBBBB22'),UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA11'),UTC_TIMESTAMP(6),UTC_TIMESTAMP(6)+INTERVAL 1 DAY)" "B role cannot use A membership"
reject "INSERT INTO c06_membership_role_proposal(tenant_id,proposal_id,membership_id,role_id,branch_id,valid_from_utc,valid_until_utc) VALUES (UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA'),UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA42'),UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA31'),UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA21'),UNHEX('BBBBBBBBBBBBBBBBBBBBBBBBBBBBBB13'),UTC_TIMESTAMP(6),UTC_TIMESTAMP(6)+INTERVAL 1 DAY)" "B branch cannot use A role"
reject "INSERT INTO c06_membership_role_proposal(tenant_id,proposal_id,membership_id,role_id,branch_id,valid_from_utc,valid_until_utc) VALUES (UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA'),UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA42'),UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA31'),UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA21'),UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA11'),UTC_TIMESTAMP(6),UTC_TIMESTAMP(6)+INTERVAL 1 DAY)" "duplicate membership-role-branch forbidden"
reject "INSERT INTO c06_role_capability_proposal(tenant_id,role_id,action_id) VALUES (UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA'),UNHEX('BBBBBBBBBBBBBBBBBBBBBBBBBBBBBB22'),'cases.read')" "B role capability cross-tenant forbidden"
reject "INSERT INTO c06_role_capability_proposal(tenant_id,role_id,action_id) VALUES (UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA'),UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA21'),'cases.write')" "uncatalogued capability forbidden"
reject "INSERT INTO c06_role_capability_proposal(tenant_id,role_id,action_id,realm) VALUES (UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA'),UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA21'),'platform.support.metadata.read',2)" "platform realm forbidden to tenant"
reject "UPDATE c06_capability_registry SET grantable=TRUE WHERE action_id='cases.read'" "cannot activate grants"
reject "INSERT INTO c06_capability_registry(action_id,realm) VALUES ('*',1)" "wildcard code rejected"
reject "UPDATE c06_role_capability_proposal SET proposal_only=FALSE WHERE action_id='cases.read'" "proposal cannot become permission"
reject "INSERT INTO c06_tenant_role(tenant_id,role_id,role_code) VALUES (UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA'),UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA23'),'TENANT-ADMIN')" "uppercase/privileged code forbidden"
reject "INSERT INTO c06_tenant_role(tenant_id,role_id,role_code) VALUES (UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA'),UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA24'),'tenant-viewer')" "duplicate tenant role code forbidden"
reject "UPDATE c06_membership_role_proposal SET tenant_id=UNHEX('BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB'),revision=2 WHERE proposal_id=UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA41')" "valid-target tenant reparent rejected"
reject "UPDATE c06_membership_role_proposal SET branch_id=UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA12'),revision=2 WHERE proposal_id=UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA41')" "same-tenant branch reparent rejected"
reject "UPDATE c06_membership_role_proposal SET revision=0 WHERE proposal_id=UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA41')" "revision rollback rejected"
reject "UPDATE c06_role_proposal_audit SET event_kind=2 WHERE audit_id=UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA51')" "audit update prohibited"
reject "DELETE FROM c06_role_proposal_audit WHERE audit_id=UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA51')" "audit delete prohibited"
reject "INSERT INTO c06_role_proposal_audit(tenant_id,audit_id,proposal_id,actor_account_id,event_kind,reason_hmac,at_utc) VALUES (UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA'),UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA52'),UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA41'),UNHEX('BBBBBBBBBBBBBBBBBBBBBBBBBBBBBB02'),1,REPEAT(UNHEX('33'),32),UTC_TIMESTAMP(6))" "foreign tenant audit actor forbidden"
sql "UPDATE c06_membership_role_proposal SET proposal_state=2,revision=revision+1,revoked_at_utc=UTC_TIMESTAMP(6) WHERE tenant_id=UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA') AND proposal_id=UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA41')"
assert "SELECT CONCAT(proposal_state,':',revision,':',revoked_at_utc IS NOT NULL) FROM c06_membership_role_proposal WHERE proposal_id=UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA41')" "2:2:1" "revocation persisted with revision"
reject "UPDATE c06_membership_role_proposal SET proposal_state=1,revision=revision+1,revoked_at_utc=NULL WHERE proposal_id=UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA41')" "revocation cannot revive"
reject "UPDATE c06_tenant_role SET tenant_id=UNHEX('BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB'),role_revision=role_revision+1 WHERE role_id=UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA21')" "role tenant immutable"
sql "UPDATE c06_tenant_role SET role_state=3,role_revision=role_revision+1 WHERE tenant_id=UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA') AND role_id=UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA21')"
reject "UPDATE c06_tenant_role SET role_state=1,role_revision=role_revision+1 WHERE role_id=UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAA21')" "retired role cannot reactivate"
assert "SELECT COUNT(*) FROM c06_membership_role_proposal WHERE tenant_id=UNHEX('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA') AND proposal_state=1" "0" "no prepared candidate after revocation"
apply "$ROOT/database/migrations/019_c06_prepared_roles.down.sql"
assert "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE() AND table_name LIKE 'c06_%'" "0" "rollback removes only C06 tables"
assert "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE() AND table_name='c05_identity_membership'" "1" "rollback preserves C05 membership"
assert "SELECT COUNT(*) FROM c05_identity_branch" "3" "rollback preserves C05 branches"
echo "C06-02 MYSQL 8.4 PASS: $rejected negative attacks rejected; scoped model and rollback."
