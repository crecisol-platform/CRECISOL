#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
NAME="crecisol-c06-delegation-$$"
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
  [[ "$actual" == "$2" ]] || { echo "FAIL C06-11 D $3 expected=$2 actual=$actual" >&2; exit 1; }
  echo "PASS C06-11 D $3"
}
rejected=0
reject() {
  local error
  if error="$(sql "$1" 2>&1)"; then
    echo "FAIL C06-11 D unexpectedly permitted: $2" >&2; exit 1
  fi
  if [[ "$error" != *"ERROR $3 "* ]]; then
    printf 'FAIL C06-11 D wrong SQL rejection: %s (expected %s, actual %s)\n' "$2" "$3" "$(printf '%s' "$error" | grep -oE 'ERROR [0-9]+' | head -n 1 || true)" >&2
    exit 1
  fi
  if [[ -n "${4:-}" && "$error" != *"\`$4\`"* ]]; then
    printf 'FAIL C06-11 D wrong constraint: %s (expected %s)\n' "$2" "$4" >&2
    exit 1
  fi
  rejected=$((rejected+1))
  echo "PASS C06-11 D rejected $2 (MySQL $3)"
}

# Exact migrations only; never use an installed or owner database.
for file in "$ROOT"/database/migrations/[0-9][0-9][0-9]_c05_*.sql; do
  [[ "$file" == *.down.sql ]] && continue
  apply "$file"
done
for file in "$ROOT"/database/migrations/0{19,20,21,22,23}_c06_*.sql; do
  [[ "$file" == *.down.sql ]] && continue
  apply "$file"
done
apply "$ROOT/database/operations/c05_local_runtime_role.sql"
before_grants="$(sql "SHOW GRANTS FOR 'c05_identity_local_runtime'")"
assert "SELECT VERSION() LIKE '8.4.%'" "1" "real MySQL 8.4"
UP="$ROOT/database/migrations/024_c06_delegated_role_ledger.sql"
DOWN="$ROOT/database/migrations/024_c06_delegated_role_ledger.down.sql"
apply "$UP"
assert "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='crecisol_c05' AND table_name IN('c06_delegation_role','c06_delegation_ceiling','c06_delegation_grant','c06_delegation_revocation')" "4" "four candidate tables"
apply "$DOWN"
assert "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='crecisol_c05' AND table_name IN('c06_delegation_role','c06_delegation_ceiling','c06_delegation_grant','c06_delegation_revocation')" "0" "empty isolated migration reversal"
apply "$UP"
T="UNHEX(REPEAT('11',16))"
U="UNHEX(REPEAT('22',16))"
A="UNHEX(REPEAT('31',16))"
B="UNHEX(REPEAT('32',16))"
C="UNHEX(REPEAT('33',16))"
D="UNHEX(REPEAT('34',16))"
MA="UNHEX(REPEAT('41',16))"
MB="UNHEX(REPEAT('42',16))"
MC="UNHEX(REPEAT('43',16))"
MD="UNHEX(REPEAT('44',16))"
BR="UNHEX(REPEAT('71',16))"
BR2="UNHEX(REPEAT('72',16))"
RES="UNHEX(REPEAT('73',16))"
R2="UNHEX(REPEAT('74',16))"
ROLE="UNHEX(REPEAT('61',16))"
G1="UNHEX(REPEAT('81',16))"
G2="UNHEX(REPEAT('82',16))"
G3="UNHEX(REPEAT('83',16))"
G4="UNHEX(REPEAT('84',16))"
ACTION="'identity.private.read'"
PAST="UTC_TIMESTAMP(6)-INTERVAL 5 MINUTE"
END="UTC_TIMESTAMP(6)+INTERVAL 1 HOUR"
H1="UNHEX(REPEAT('a1',32))"
H2="UNHEX(REPEAT('a2',32))"
H3="UNHEX(REPEAT('a3',32))"
H4="UNHEX(REPEAT('a4',32))"
H5="UNHEX(REPEAT('a5',32))"
H6="UNHEX(REPEAT('a6',32))"
sql "INSERT INTO c04_tenant_catalog VALUES($T,'a','A',3,2,1,UTC_TIMESTAMP(6)),($U,'b','B',3,2,1,UTC_TIMESTAMP(6));
INSERT INTO c05_identity_account(account_id,login_lookup_hmac,mailbox_ciphertext,mailbox_nonce,mailbox_tag,mailbox_key_version,password_phc,mailbox_verified_at_utc,account_state,created_at_utc,updated_at_utc) VALUES
($A,UNHEX(REPEAT('31',32)),X'00',REPEAT(X'00',12),REPEAT(X'00',16),1,'synthetic',UTC_TIMESTAMP(6),1,UTC_TIMESTAMP(6),UTC_TIMESTAMP(6)),
($B,UNHEX(REPEAT('32',32)),X'00',REPEAT(X'00',12),REPEAT(X'00',16),1,'synthetic',UTC_TIMESTAMP(6),1,UTC_TIMESTAMP(6),UTC_TIMESTAMP(6)),
($C,UNHEX(REPEAT('33',32)),X'00',REPEAT(X'00',12),REPEAT(X'00',16),1,'synthetic',UTC_TIMESTAMP(6),1,UTC_TIMESTAMP(6),UTC_TIMESTAMP(6)),
($D,UNHEX(REPEAT('34',32)),X'00',REPEAT(X'00',12),REPEAT(X'00',16),1,'synthetic',UTC_TIMESTAMP(6),1,UTC_TIMESTAMP(6),UTC_TIMESTAMP(6));
INSERT INTO c05_identity_membership(tenant_id,membership_id,account_id,membership_state,role_code,granted_at_utc,expires_at_utc,created_at_utc) VALUES
($T,$MA,$A,1,'OWNER',$PAST,UTC_TIMESTAMP(6)+INTERVAL 1 DAY,$PAST),
($T,$MB,$B,1,'MEMBER',$PAST,UTC_TIMESTAMP(6)+INTERVAL 1 DAY,$PAST),
($U,$MC,$C,1,'OWNER',$PAST,UTC_TIMESTAMP(6)+INTERVAL 1 DAY,$PAST),
($T,$MD,$D,1,'MEMBER',$PAST,UTC_TIMESTAMP(6)+INTERVAL 1 DAY,$PAST);
INSERT INTO c05_identity_branch VALUES($T,$BR,TRUE),($U,$BR,TRUE),($T,$BR2,TRUE);
INSERT INTO c05_identity_private_resource VALUES($T,$BR,$RES,$B,1,'SYNTHETIC-A'),($U,$BR,$RES,$C,1,'SYNTHETIC-B'),($T,$BR2,$R2,$D,1,'SYNTHETIC-C');
INSERT INTO c06_delegation_role VALUES($T,$ROLE,'tenant-staff',1,$PAST,NULL),($U,$ROLE,'tenant-staff',1,$PAST,NULL);
INSERT INTO c06_delegation_ceiling VALUES($T,$MA,$BR,$RES,$ACTION,1,1,1,$PAST,UTC_TIMESTAMP(6)+INTERVAL 2 HOUR,NULL),($U,$MC,$BR,$RES,$ACTION,1,1,1,$PAST,UTC_TIMESTAMP(6)+INTERVAL 2 HOUR,NULL);"
assert "SELECT COUNT(*) FROM c06_delegation_ceiling" "2" "colliding tenant scope remains separate"

# Separate actual C05+MFA test mode. D and E-writer still execute every original SQL case.
if [[ "${C06_DELEGATION_FLOW_TESTS:-0}" == 1 ]]; then
  proof_up="$ROOT/database/migrations/025_c06_delegation_confirmation.sql"
  proof_down="$ROOT/database/migrations/025_c06_delegation_confirmation.down.sql"
  apply "$proof_up"
  apply "$proof_down"
  assert "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='crecisol_c05' AND table_name IN('c06_delegation_confirmation','c06_delegation_lab_entitlement')" "0" "empty confirmation migration reversal"
  apply "$proof_up"
  authority_up="$ROOT/database/migrations/026_c06_delegation_authority.sql"
  authority_down="$ROOT/database/migrations/026_c06_delegation_authority.down.sql"
  apply "$authority_up"
  apply "$authority_down"
  assert "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='crecisol_c05' AND table_name='c06_delegation_authority'" "0" "empty provenance migration reversal"
  apply "$authority_up"
  runtime_password="$(openssl rand -hex 24)"
  fixture_password="$(openssl rand -hex 24)"
  sql "CREATE USER 'c06_delegation_flow_test'@'%' IDENTIFIED BY '$runtime_password';
       CREATE USER 'c06_delegation_fixture_test'@'%' IDENTIFIED BY '$fixture_password';
       GRANT 'c05_identity_local_runtime' TO 'c06_delegation_flow_test'@'%';
       SET DEFAULT ROLE 'c05_identity_local_runtime' TO 'c06_delegation_flow_test'@'%';
       GRANT SELECT ON crecisol_c05.c06_delegation_role TO 'c06_delegation_flow_test'@'%';
       GRANT SELECT ON crecisol_c05.c06_delegation_ceiling TO 'c06_delegation_flow_test'@'%';
       GRANT SELECT ON crecisol_c05.c06_delegation_lab_entitlement TO 'c06_delegation_flow_test'@'%';
       GRANT SELECT,INSERT ON crecisol_c05.c06_delegation_grant TO 'c06_delegation_flow_test'@'%';
       GRANT SELECT,INSERT ON crecisol_c05.c06_delegation_revocation TO 'c06_delegation_flow_test'@'%';
       GRANT SELECT,INSERT ON crecisol_c05.c06_delegation_authority TO 'c06_delegation_flow_test'@'%';
       GRANT SELECT,INSERT ON crecisol_c05.c06_delegation_confirmation TO 'c06_delegation_flow_test'@'%';
       GRANT UPDATE(consumed_at_utc) ON crecisol_c05.c06_delegation_confirmation TO 'c06_delegation_flow_test'@'%';
       GRANT SELECT,INSERT ON crecisol_c05.c04_verified_tenant_host TO 'c06_delegation_fixture_test'@'%';
       GRANT SELECT,INSERT ON crecisol_c05.c05_identity_mfa_factor TO 'c06_delegation_fixture_test'@'%';
       GRANT SELECT,INSERT ON crecisol_c05.c05_identity_session TO 'c06_delegation_fixture_test'@'%';
       GRANT SELECT,INSERT ON crecisol_c05.c05_identity_terms TO 'c06_delegation_fixture_test'@'%';
       GRANT SELECT,INSERT ON crecisol_c05.c05_identity_terms_acceptance TO 'c06_delegation_fixture_test'@'%';
       GRANT SELECT,INSERT ON crecisol_c05.c05_identity_resource_grant TO 'c06_delegation_fixture_test'@'%';
       GRANT SELECT,INSERT,UPDATE ON crecisol_c05.c06_delegation_lab_entitlement TO 'c06_delegation_fixture_test'@'%';
       GRANT SELECT ON crecisol_c05.c06_delegation_grant TO 'c06_delegation_fixture_test'@'%';
       GRANT SELECT ON crecisol_c05.c06_delegation_revocation TO 'c06_delegation_fixture_test'@'%';
       GRANT SELECT ON crecisol_c05.c06_delegation_authority TO 'c06_delegation_fixture_test'@'%';
       GRANT SELECT ON crecisol_c05.c06_delegation_confirmation TO 'c06_delegation_fixture_test'@'%';
       GRANT SELECT ON crecisol_c05.c05_identity_private_resource TO 'c06_delegation_fixture_test'@'%';
       GRANT SELECT ON crecisol_c05.c05_identity_audit TO 'c06_delegation_fixture_test'@'%';"
  flow_port="$(docker port "$NAME" 3306/tcp | sed -n 's/^127\.0\.0\.1://p')"
  [[ "$flow_port" =~ ^[0-9]+$ && "$flow_port" != 3306 ]]
  C06_DELEGATION_FLOW_DSN="Server=127.0.0.1;Port=$flow_port;Database=crecisol_c05;User ID=c06_delegation_flow_test;Password=$runtime_password;SslMode=None;Pooling=False" \
  C06_DELEGATION_FIXTURE_DSN="Server=127.0.0.1;Port=$flow_port;Database=crecisol_c05;User ID=c06_delegation_fixture_test;Password=$fixture_password;SslMode=None;Pooling=False" \
    dotnet run -c Release --project "$ROOT/backend/tests/Crecisol.C06Delegation.FlowTests/Crecisol.C06Delegation.FlowTests.csproj"
  assert "SELECT COUNT(*) FROM c06_delegation_confirmation WHERE consumed_at_utc IS NOT NULL" "3" "three confirmations committed"
  assert "SELECT COUNT(*) FROM c06_delegation_authority" "2" "two immutable issuance provenance rows"
  reject "UPDATE c06_delegation_authority SET source_revision=source_revision+1" "provenance immutable" 1644
  reject "DELETE FROM c06_delegation_authority" "provenance history immutable" 1644
  if error="$(apply "$authority_down" 2>&1)"; then echo "FAIL C06-11 FLOW populated authority rollback succeeded" >&2; exit 1; fi
  [[ "$error" == *"ERROR 1644 "* ]]
  reject "UPDATE c06_delegation_confirmation SET consumed_at_utc=NULL WHERE consumed_at_utc IS NOT NULL" "confirmation cannot revive" 1644
  reject "UPDATE c06_delegation_confirmation SET binding_hmac=UNHEX(REPEAT('aa',32)) WHERE consumed_at_utc IS NULL" "confirmation binding immutable" 1644
  reject "DELETE FROM c06_delegation_confirmation" "confirmation history immutable" 1644
  if error="$(apply "$proof_down" 2>&1)"; then echo "FAIL C06-11 FLOW populated rollback succeeded" >&2; exit 1; fi
  [[ "$error" == *"ERROR 1644 "* ]]
  [[ "$(sql "SHOW GRANTS FOR 'c05_identity_local_runtime'")" == "$before_grants" ]]
  sql "DROP USER 'c06_delegation_flow_test'@'%','c06_delegation_fixture_test'@'%'"
  echo "PASS C06-11 FLOW real MFA, immutable confirmations and original runtime role preserved"
  exit 0
fi
reject "INSERT INTO c06_delegation_ceiling VALUES($T,$MC,$BR,$RES,$ACTION,1,1,1,$PAST,$END,NULL)" "foreign membership ceiling" 1452 fk_c06_dc_actor
reject "INSERT INTO c06_delegation_ceiling VALUES($U,$MC,$BR2,$R2,$ACTION,1,1,1,$PAST,$END,NULL)" "foreign resource ceiling" 1452 fk_c06_dc_resource
reject "INSERT INTO c06_delegation_role VALUES($T,UNHEX(REPEAT('62',16)),'tenant-admin',1,$PAST,NULL)" "privileged recipient role" 3819
reject "UPDATE c06_delegation_ceiling SET may_assign=0 WHERE tenant_id=$T" "ceiling cannot change flags" 1644
reject "UPDATE c06_delegation_role SET role_code='tenant-viewer' WHERE tenant_id=$T" "role cannot change identity" 1644
grant_sql() {
  # exact typed fixture: tenant grant branch resource subject issuer expiry digest [subject revision]
  local rev="${9:-1}"
  printf "INSERT INTO c06_delegation_grant VALUES(%s,%s,%s,%s,%s,%s,%s,%s,1,%s,1,1,1,UTC_TIMESTAMP(6),%s,%s,%s)" \
    "$1" "$2" "$3" "$4" "$ACTION" "$5" "$ROLE" "$6" "$rev" "$7" "$H1" "$8"
}
revoke_sql() {
  printf "INSERT INTO c06_delegation_revocation VALUES(%s,%s,%s,%s,%s,%s,1,1,1,2,UTC_TIMESTAMP(6),%s,%s)" \
    "$1" "$2" "$3" "$4" "$ACTION" "$5" "$H1" "$6"
}
reject "$(grant_sql "$T" "$G1" "$BR" "$RES" "$MC" "$MA" "$END" "$H2")" "foreign subject" 1644
reject "$(grant_sql "$T" "$G1" "$BR" "$RES" "$MB" "$MC" "$END" "$H2")" "foreign issuer" 1644
reject "$(grant_sql "$T" "$G1" "$BR2" "$R2" "$MB" "$MA" "$END" "$H2")" "ungranted branch" 1644
reject "$(grant_sql "$T" "$G1" "$BR" "$RES" "$MB" "$MA" "UTC_TIMESTAMP(6)+INTERVAL 3 HOUR" "$H2")" "beyond ceiling expiry" 1644
reject "$(grant_sql "$T" "$G1" "$BR" "$RES" "$MB" "$MA" "$END" "$H2" 2)" "stale subject revision" 1644
reject "$(grant_sql "$T" "$G1" "$BR" "$RES" "$MA" "$MA" "$END" "$H2")" "self grant" 3819
assert "SELECT COUNT(*) FROM c06_delegation_grant" "0" "denials create no grant"

# Real .NET writer with a dedicated, non-runtime synthetic SQL account.
# No new privilege file or default role; the whole container is disposable.
if [[ "${C06_DELEGATION_WRITER_TESTS:-0}" == 1 ]]; then
  writer_password="$(openssl rand -hex 24)"
  sql "CREATE USER 'c06_delegation_writer_test'@'%' IDENTIFIED BY '$writer_password';
       GRANT SELECT ON crecisol_c05.c05_identity_membership TO 'c06_delegation_writer_test'@'%';
       GRANT SELECT,INSERT ON crecisol_c05.c06_delegation_grant TO 'c06_delegation_writer_test'@'%';
       GRANT SELECT,INSERT ON crecisol_c05.c06_delegation_revocation TO 'c06_delegation_writer_test'@'%';"
  writer_port="$(docker port "$NAME" 3306/tcp | sed -n 's/^127\.0\.0\.1://p')"
  [[ "$writer_port" =~ ^[0-9]+$ && "$writer_port" != 3306 ]]
  C06_DELEGATION_TEST_DSN="Server=127.0.0.1;Port=$writer_port;Database=crecisol_c05;User ID=c06_delegation_writer_test;Password=$writer_password;SslMode=None;Pooling=False" \
    dotnet run -c Release --project "$ROOT/backend/tests/Crecisol.C06Delegation.MySqlTests/Crecisol.C06Delegation.MySqlTests.csproj"
  sql "DROP USER 'c06_delegation_writer_test'@'%'"
  assert "SELECT CONCAT((SELECT COUNT(*) FROM c06_delegation_grant),':',(SELECT COUNT(*) FROM c06_delegation_revocation))" "0:0" "writer tests rolled back all data"
fi
sql "START TRANSACTION; $(grant_sql "$T" "$G1" "$BR" "$RES" "$MB" "$MA" "$END" "$H2"); $(revoke_sql "$T" "$G1" "$BR" "$RES" "$MA" "$H3"); ROLLBACK;"
assert "SELECT CONCAT((SELECT COUNT(*) FROM c06_delegation_grant),':',(SELECT COUNT(*) FROM c06_delegation_revocation))" "0:0" "issue and revoke roll back together"
sql "$(grant_sql "$T" "$G1" "$BR" "$RES" "$MB" "$MA" "$END" "$H2")"
reject "$(grant_sql "$T" "$G2" "$BR" "$RES" "$MB" "$MA" "$END" "$H3")" "duplicate exact grant" 1062
reject "UPDATE c06_delegation_grant SET expires_at_utc=UTC_TIMESTAMP(6)+INTERVAL 5 HOUR WHERE tenant_id=$T" "grant cannot be extended" 1644
reject "DELETE FROM c06_delegation_grant WHERE tenant_id=$T" "grant history cannot be erased" 1644
reject "$(revoke_sql "$U" "$G1" "$BR" "$RES" "$MC" "$H3")" "foreign grant revocation" 1644
reject "$(revoke_sql "$T" "$G1" "$BR2" "$R2" "$MA" "$H3")" "foreign branch revocation" 1644
sql "$(revoke_sql "$T" "$G1" "$BR" "$RES" "$MA" "$H3")"
reject "$(revoke_sql "$T" "$G1" "$BR" "$RES" "$MA" "$H4")" "duplicate revoke CAS" 1062
reject "UPDATE c06_delegation_revocation SET revoked_at_utc=UTC_TIMESTAMP(6) WHERE tenant_id=$T" "revocation immutable" 1644
reject "DELETE FROM c06_delegation_revocation WHERE tenant_id=$T" "revocation cannot be erased" 1644
reject "$(grant_sql "$T" "$G2" "$BR" "$RES" "$MB" "$MA" "$END" "$H4")" "no reactivation by reinsertion" 1062
# Two independently connected writers race for the same subject/scope.
logs="$(mktemp -d)"
trap 'rm -rf "$logs"; cleanup' EXIT
(sql "START TRANSACTION; $(grant_sql "$T" "$G3" "$BR" "$RES" "$MD" "$MA" "$END" "$H4"); SELECT SLEEP(2); COMMIT;" >"$logs/a" 2>&1) &
pid_a=$!
(sql "START TRANSACTION; $(grant_sql "$T" "$G4" "$BR" "$RES" "$MD" "$MA" "$END" "$H5"); SELECT SLEEP(2); COMMIT;" >"$logs/b" 2>&1) &
pid_b=$!
wins=0
losses=0
if wait "$pid_a"; then wins=$((wins+1)); else grep -q 'ERROR 1062 ' "$logs/a"; losses=$((losses+1)); fi
if wait "$pid_b"; then wins=$((wins+1)); else grep -q 'ERROR 1062 ' "$logs/b"; losses=$((losses+1)); fi
[[ "$wins:$losses" == "1:1" ]]
assert "SELECT COUNT(*) FROM c06_delegation_grant WHERE tenant_id=$T AND subject_membership_id=$MD" "1" "concurrent same-scope issue has one winner"
winner="$(sql "SELECT HEX(grant_id) FROM c06_delegation_grant WHERE tenant_id=$T AND subject_membership_id=$MD")"
[[ "$winner" =~ ^[0-9A-F]{32}$ ]]
win="UNHEX('$winner')"
(sql "START TRANSACTION; $(revoke_sql "$T" "$win" "$BR" "$RES" "$MA" "$H4"); SELECT SLEEP(2); COMMIT;" >"$logs/a" 2>&1) &
pid_a=$!
(sql "START TRANSACTION; $(revoke_sql "$T" "$win" "$BR" "$RES" "$MA" "$H5"); SELECT SLEEP(2); COMMIT;" >"$logs/b" 2>&1) &
pid_b=$!
wins=0
losses=0
if wait "$pid_a"; then wins=$((wins+1)); else grep -q 'ERROR 1062 ' "$logs/a"; losses=$((losses+1)); fi
if wait "$pid_b"; then wins=$((wins+1)); else grep -q 'ERROR 1062 ' "$logs/b"; losses=$((losses+1)); fi
[[ "$wins:$losses" == "1:1" ]]
assert "SELECT COUNT(*) FROM c06_delegation_revocation WHERE tenant_id=$T AND grant_id=$win" "1" "concurrent revocation has one winner"
sql "UPDATE c06_delegation_ceiling SET revision=2,revoked_at_utc=UTC_TIMESTAMP(6) WHERE tenant_id=$T"
reject "UPDATE c06_delegation_ceiling SET revision=1,revoked_at_utc=NULL WHERE tenant_id=$T" "ceiling cannot revive" 1644
reject "$(grant_sql "$T" "$G2" "$BR" "$RES" "$MB" "$MA" "$END" "$H6")" "revoked ceiling denies issuance" 1644
sql "UPDATE c06_delegation_role SET revision=2,retired_at_utc=UTC_TIMESTAMP(6) WHERE tenant_id=$T"
reject "UPDATE c06_delegation_role SET revision=1,retired_at_utc=NULL WHERE tenant_id=$T" "retired role cannot revive" 1644
# Nothing in the migration gives the existing runtime visibility or writes.
app_password="$(openssl rand -hex 24)"
sql "CREATE USER 'c06_delegation_test'@'localhost' IDENTIFIED BY '$app_password'; GRANT 'c05_identity_local_runtime' TO 'c06_delegation_test'@'localhost'; SET DEFAULT ROLE 'c05_identity_local_runtime' TO 'c06_delegation_test'@'localhost';"
app_sql() { docker exec -e "MYSQL_PWD=$app_password" "$NAME" mysql --protocol=SOCKET -uc06_delegation_test -N -B -D crecisol_c05 -e "$1"; }
deny_runtime() {
  local error
  if error="$(app_sql "$1" 2>&1)"; then echo "FAIL C06-11 D runtime permitted $2" >&2; exit 1; fi
  [[ "$error" == *"ERROR 1142 "* ]] || { echo "FAIL C06-11 D unexpected runtime error $2" >&2; exit 1; }
  echo "PASS C06-11 D runtime denied $2"
}
for table in c06_delegation_role c06_delegation_ceiling c06_delegation_grant c06_delegation_revocation; do
  deny_runtime "SELECT COUNT(*) FROM $table" "$table read"
  deny_runtime "DELETE FROM $table WHERE tenant_id=$T" "$table delete"
done
deny_runtime "$(grant_sql "$T" "$G2" "$BR" "$RES" "$MB" "$MA" "$END" "$H6")" "grant insert"
deny_runtime "UPDATE c06_delegation_ceiling SET may_assign=1 WHERE tenant_id=$T" "ceiling update"
[[ "$(sql "SHOW GRANTS FOR 'c05_identity_local_runtime'")" == "$before_grants" ]]
assert "SELECT COUNT(*) FROM c06_delegation_grant WHERE tenant_id=$U" "0" "foreign ledger unchanged"
assert "SELECT COUNT(*) FROM c06_delegation_grant" "2" "two immutable issuances preserved"
assert "SELECT COUNT(*) FROM c06_delegation_revocation" "2" "two immutable revocations preserved"
assert "SELECT COUNT(*) FROM c05_identity_private_resource WHERE display_reference IN('SYNTHETIC-A','SYNTHETIC-B','SYNTHETIC-C')" "3" "original private resources unchanged"
assert "SELECT COUNT(*) FROM c05_identity_resource_grant" "0" "no existing operational authority minted"
if error="$(apply "$DOWN" 2>&1)"; then echo "FAIL C06-11 D populated ledger dropped" >&2; exit 1; fi
[[ "$error" == *"ERROR 1644 "* ]] || { echo "FAIL C06-11 D incorrect rollback refusal" >&2; exit 1; }
assert "SELECT COUNT(*) FROM c06_delegation_revocation" "2" "populated down migration refused without erasing history"
echo "PASS C06-11 DELEGATION MYSQL rejected=$rejected; two real concurrent CAS races; no runtime privileges or operational authority"
