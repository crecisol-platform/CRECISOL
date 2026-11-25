#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
NAME="crecisol-c06-query-$$"
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
  [[ "$actual" == "$2" ]] || { echo "FAIL C06-08 D $3 expected=$2 actual=$actual" >&2; exit 1; }
  echo "PASS C06-08 D $3"
}
rejected=0
reject() {
  local error
  if error="$(sql "$1" 2>&1)"; then
    echo "FAIL C06-08 D unexpectedly permitted: $2" >&2; exit 1
  fi
  [[ "$error" == *"ERROR $3 "* ]] || { echo "FAIL C06-08 D wrong SQL rejection: $2 (expected $3)" >&2; exit 1; }
  rejected=$((rejected+1))
  echo "PASS C06-08 D rejected $2 (MySQL $3)"
}
for file in "$ROOT"/database/migrations/[0-9][0-9][0-9]_c05_*.sql; do
  [[ "$file" == *.down.sql ]] && continue
  apply "$file"
done

assert "SELECT VERSION() LIKE '8.4.%'" "1" "real MySQL 8.4"
apply "$ROOT/database/operations/c05_local_runtime_role.sql"
before_grants="$(sql "SHOW GRANTS FOR 'c05_identity_local_runtime'")"
apply "$ROOT/database/migrations/023_c06_scoped_query_indexes.sql"
assert "SELECT GROUP_CONCAT(column_name ORDER BY seq_in_index SEPARATOR ',') FROM information_schema.statistics WHERE table_schema=DATABASE() AND table_name='c05_identity_membership' AND index_name='ix_c06_08_pending'" "tenant_id,membership_state,created_at_utc,membership_id" "ix_c06_08_pending exact index order"
assert "SELECT GROUP_CONCAT(column_name ORDER BY seq_in_index SEPARATOR ',') FROM information_schema.statistics WHERE table_schema=DATABASE() AND table_name='c05_identity_session' AND index_name='ix_c06_08_tenant_sessions'" "tenant_id,account_id,membership_id,revoked_at_utc,last_seen_at_utc,session_id" "ix_c06_08_tenant_sessions exact index order"
assert "SELECT GROUP_CONCAT(column_name ORDER BY seq_in_index SEPARATOR ',') FROM information_schema.statistics WHERE table_schema=DATABASE() AND table_name='c05_platform_session' AND index_name='ix_c06_08_operator_sessions'" "operator_id,security_revision,revoked_at_utc,last_seen_at_utc,session_id" "ix_c06_08_operator_sessions exact index order"
assert "SELECT GROUP_CONCAT(column_name ORDER BY seq_in_index SEPARATOR ',') FROM information_schema.statistics WHERE table_schema=DATABASE() AND table_name='c05_identity_audit' AND index_name='ix_c06_08_subject_events'" "tenant_id,subject_account_id,actor_account_id,created_at_utc,audit_id" "ix_c06_08_subject_events exact index order"
assert "SELECT GROUP_CONCAT(column_name ORDER BY seq_in_index SEPARATOR ',') FROM information_schema.statistics WHERE table_schema=DATABASE() AND table_name='c05_identity_abuse_signal' AND index_name='ix_c06_08_signal_window'" "created_at_utc,signal_kind" "ix_c06_08_signal_window exact index order"
[[ "$(sql "SHOW GRANTS FOR 'c05_identity_local_runtime'")" == "$before_grants" ]]
sql "INSERT INTO c04_tenant_catalog VALUES(UNHEX(REPEAT('11',16)),'a','A',3,2,1,UTC_TIMESTAMP(6)),(UNHEX(REPEAT('22',16)),'b','B',3,2,1,UTC_TIMESTAMP(6));
INSERT INTO c05_identity_account(account_id,login_lookup_hmac,mailbox_ciphertext,mailbox_nonce,mailbox_tag,mailbox_key_version,password_phc,mailbox_verified_at_utc,account_state,created_at_utc,updated_at_utc) VALUES
(UNHEX(REPEAT('31',16)),UNHEX(REPEAT('31',32)),X'00',REPEAT(X'00',12),REPEAT(X'00',16),1,'synthetic',UTC_TIMESTAMP(6),0,UTC_TIMESTAMP(6),UTC_TIMESTAMP(6)),
(UNHEX(REPEAT('32',16)),UNHEX(REPEAT('32',32)),X'00',REPEAT(X'00',12),REPEAT(X'00',16),1,'synthetic',UTC_TIMESTAMP(6),0,UTC_TIMESTAMP(6),UTC_TIMESTAMP(6)),
(UNHEX(REPEAT('33',16)),UNHEX(REPEAT('33',32)),X'00',REPEAT(X'00',12),REPEAT(X'00',16),1,'synthetic',UTC_TIMESTAMP(6),0,UTC_TIMESTAMP(6),UTC_TIMESTAMP(6));
INSERT INTO c05_identity_membership(tenant_id,membership_id,account_id,membership_state,role_code,created_at_utc) VALUES
(UNHEX(REPEAT('11',16)),UNHEX(REPEAT('41',16)),UNHEX(REPEAT('31',16)),0,'MEMBER','2020-01-01 00:00:00.123456'),
(UNHEX(REPEAT('11',16)),UNHEX(REPEAT('42',16)),UNHEX(REPEAT('32',16)),0,'MEMBER','2020-01-01 00:00:00.123456'),
(UNHEX(REPEAT('22',16)),UNHEX(REPEAT('43',16)),UNHEX(REPEAT('33',16)),0,'MEMBER','2020-01-01 00:00:00.123456');"
assert "SELECT COUNT(*) FROM c05_identity_membership WHERE tenant_id=UNHEX(REPEAT('11',16)) AND membership_state=0" "2" "tenant A scoped count"
assert "SELECT COUNT(*) FROM c05_identity_membership WHERE tenant_id=UNHEX(REPEAT('22',16)) AND membership_state=0" "1" "tenant B scoped count"
assert "SELECT HEX(membership_id) FROM c05_identity_membership WHERE tenant_id=UNHEX(REPEAT('11',16)) AND membership_state=0 AND (created_at_utc>'2020-01-01 00:00:00.123456' OR (created_at_utc='2020-01-01 00:00:00.123456' AND membership_id>UNHEX(REPEAT('41',16)))) ORDER BY created_at_utc,membership_id LIMIT 13" "42424242424242424242424242424242" "binary keyset tie-breaker remains scoped"
# EXPLAIN verifies the reviewed index is usable, not a production latency SLA.
plan="$(sql "EXPLAIN SELECT membership_id FROM c05_identity_membership FORCE INDEX(ix_c06_08_pending) WHERE tenant_id=UNHEX(REPEAT('11',16)) AND membership_state=0 ORDER BY created_at_utc,membership_id LIMIT 13")"
[[ "$plan" == *ix_c06_08_pending* ]]
echo "PASS C06-08 D pending keyset index usable"
app_password="$(openssl rand -hex 24)"
sql "CREATE USER 'c06_query_test'@'localhost' IDENTIFIED BY '$app_password'; GRANT 'c05_identity_local_runtime' TO 'c06_query_test'@'localhost'; SET DEFAULT ROLE 'c05_identity_local_runtime' TO 'c06_query_test'@'localhost';"
if ddl_error="$(docker exec -e "MYSQL_PWD=$app_password" "$NAME" mysql --protocol=TCP -h127.0.0.1 -uc06_query_test -N -B -D crecisol_c05 -e "ALTER TABLE c05_identity_membership DROP INDEX ix_c06_08_pending" 2>&1)"; then
  echo "FAIL runtime gained DDL" >&2; exit 1
fi
[[ "$ddl_error" == *"ERROR 1142 "* ]] || { echo "FAIL unexpected SQL rejection of runtime DDL" >&2; exit 1; }
echo "PASS C06-08 D runtime cannot alter query indexes"
before_rows="$(sql "SELECT GROUP_CONCAT(HEX(membership_id) ORDER BY membership_id) FROM c05_identity_membership")"
apply "$ROOT/database/migrations/023_c06_scoped_query_indexes.down.sql"
assert "SELECT COUNT(*) FROM information_schema.statistics WHERE table_schema=DATABASE() AND index_name LIKE 'ix_c06_08_%'" "0" "rollback removes only new indexes"
[[ "$(sql "SELECT GROUP_CONCAT(HEX(membership_id) ORDER BY membership_id) FROM c05_identity_membership")" == "$before_rows" ]]
[[ "$(sql "SHOW GRANTS FOR 'c05_identity_local_runtime'")" == "$before_grants" ]]
echo "PASS C06-08 D MYSQL: isolated counts, keyset index, unchanged rows and privileges, reversible DDL"
