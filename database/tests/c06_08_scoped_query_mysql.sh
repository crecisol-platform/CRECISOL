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
for file in "$ROOT"/database/migrations/0{19,20,21,22}_c06_*.sql; do
  [[ "$file" == *.down.sql ]] && continue
  apply "$file"
done
apply "$ROOT/database/operations/c05_local_runtime_role.sql"
baseline_indexes() { sql "SELECT table_name,index_name,non_unique,seq_in_index,column_name,COALESCE(sub_part,0),COALESCE(collation,''),index_type,is_visible FROM information_schema.statistics WHERE table_schema=DATABASE() AND LEFT(index_name,10)<>'ix_c06_08_' ORDER BY table_name,index_name,seq_in_index"; }
baseline_fks() { sql "SELECT table_name,constraint_name,ordinal_position,column_name,referenced_table_name,referenced_column_name FROM information_schema.key_column_usage WHERE table_schema=DATABASE() AND referenced_table_name IS NOT NULL ORDER BY table_name,constraint_name,ordinal_position"; }
before_indexes="$(baseline_indexes)"
before_fks="$(baseline_fks)"
[[ -n "$before_indexes" && -n "$before_fks" ]]
before_grants="$(sql "SHOW GRANTS FOR 'c05_identity_local_runtime'")"
apply "$ROOT/database/migrations/023_c06_scoped_query_indexes.sql"
[[ "$(baseline_indexes)" == "$before_indexes" ]] || { echo "FAIL forward migration replaced a baseline index" >&2; exit 1; }
[[ "$(baseline_fks)" == "$before_fks" ]] || { echo "FAIL forward migration changed a foreign key" >&2; exit 1; }
echo "PASS C06-08 D forward migration preserves every baseline index and FK"
assert "SELECT GROUP_CONCAT(column_name ORDER BY seq_in_index SEPARATOR ',') FROM information_schema.statistics WHERE table_schema=DATABASE() AND table_name='c05_identity_membership' AND index_name='ix_c06_08_pending'" "tenant_id,membership_state,created_at_utc,membership_id" "ix_c06_08_pending exact index order"
assert "SELECT GROUP_CONCAT(column_name ORDER BY seq_in_index SEPARATOR ',') FROM information_schema.statistics WHERE table_schema=DATABASE() AND table_name='c05_identity_session' AND index_name='ix_c06_08_tenant_sessions'" "tenant_id,account_id,membership_id,revoked_at_utc,last_seen_at_utc,session_id" "ix_c06_08_tenant_sessions exact index order"
assert "SELECT GROUP_CONCAT(column_name ORDER BY seq_in_index SEPARATOR ',') FROM information_schema.statistics WHERE table_schema=DATABASE() AND table_name='c05_platform_session' AND index_name='ix_c06_08_operator_sessions'" "security_revision,operator_id,revoked_at_utc,last_seen_at_utc,session_id" "ix_c06_08_operator_sessions exact index order"
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
# Same authenticated socket connection for positive and negative commands.
# Never count a failed login, a missing index, or a failed FK as denied DDL.
app_sql() {
  docker exec -e "MYSQL_PWD=$app_password" "$NAME" mysql --protocol=SOCKET -uc06_query_test -N -B -D crecisol_c05 -e "$1"
}
[[ "$(app_sql "SELECT COUNT(*) FROM c05_identity_membership")" == "3" ]]
echo "PASS C06-08 D runtime account has the intended read privilege"
if ddl_error="$(app_sql "DROP INDEX ix_c06_08_pending ON c05_identity_membership" 2>&1)"; then
  echo "FAIL runtime gained INDEX privilege" >&2; exit 1
fi
# MySQL 1142 is table-command denial; 1044 is database-level privilege
# denial. Both are SQLSTATE 42000. The preceding SELECT proves authentication.
if [[ "$ddl_error" != *"ERROR 1142 (42000)"* && "$ddl_error" != *"ERROR 1044 (42000)"* ]]; then
  printf 'FAIL unexpected SQL rejection of runtime DDL; numeric code: %s\n' "$(printf '%s' "$ddl_error" | grep -oE 'ERROR [0-9]+' | head -n 1 || true)" >&2
  exit 1
fi
printf 'PASS C06-08 D runtime cannot drop query indexes (%s)\n' "$(printf '%s' "$ddl_error" | grep -oE 'ERROR [0-9]+' | head -n 1)"
[[ "$(app_sql "SELECT COUNT(*) FROM c05_identity_membership")" == "3" ]]
before_rows="$(sql "SELECT GROUP_CONCAT(HEX(membership_id) ORDER BY membership_id) FROM c05_identity_membership")"
apply "$ROOT/database/migrations/023_c06_scoped_query_indexes.down.sql"
assert "SELECT COUNT(*) FROM information_schema.statistics WHERE table_schema=DATABASE() AND index_name LIKE 'ix_c06_08_%'" "0" "rollback removes only new indexes"
[[ "$(sql "SELECT GROUP_CONCAT(HEX(membership_id) ORDER BY membership_id) FROM c05_identity_membership")" == "$before_rows" ]]
[[ "$(sql "SHOW GRANTS FOR 'c05_identity_local_runtime'")" == "$before_grants" ]]
[[ "$(baseline_indexes)" == "$before_indexes" ]] || { echo "FAIL rollback changed a baseline index" >&2; exit 1; }
[[ "$(baseline_fks)" == "$before_fks" ]] || { echo "FAIL rollback changed a foreign key" >&2; exit 1; }
echo "PASS C06-08 D rollback preserves every baseline index and FK"
# A second apply/rollback must be clean as well; no metadata drift.
apply "$ROOT/database/migrations/023_c06_scoped_query_indexes.sql"
apply "$ROOT/database/migrations/023_c06_scoped_query_indexes.down.sql"
[[ "$(baseline_indexes)" == "$before_indexes" && "$(baseline_fks)" == "$before_fks" ]]
assert "SELECT COUNT(*) FROM information_schema.statistics WHERE table_schema=DATABASE() AND LEFT(index_name,10)='ix_c06_08_'" "0" "second rollback no residual indexes"
[[ "$(sql "SELECT GROUP_CONCAT(HEX(membership_id) ORDER BY membership_id) FROM c05_identity_membership")" == "$before_rows" ]]
echo "PASS C06-08 D MYSQL: isolated counts, keyset index, unchanged rows and privileges, reversible DDL"
