#!/usr/bin/env bash
# C05-02 REAL disposable MySQL8.4 InnoDB CAS fixture, NEVER production.
set -euo pipefail
IMAGE="mysql:8.4@sha256:a81d043930e51745522bd5c430961cf8286f0a23f0a9a2a6e0108f7ea71b0902"
NAME="crecisol-c05-02-$RANDOM-$$"
DB="c05_02_disposable"
PASSWORD="$(openssl rand -hex 24)"
TMP="$(mktemp -d)"
POS=0
NEG=0
cleanup() { docker rm -f "$NAME" >/dev/null 2>&1 || true; rm -rf "$TMP"; }
trap cleanup EXIT
if [ -n "$(printenv GITHUB_ACTIONS || true)" ]; then echo "::add-mask::$PASSWORD"; fi
root_sql() { docker exec "$NAME" mysql -uroot -N -B -D "$DB" -e "$1"; }
runner_sql() {
  docker exec -e MYSQL_PWD="$PASSWORD" "$NAME" \
    mysql -uc05_02_invite_runner -N -B -D "$DB" -e "$1"
}
same() {
  local label="$1" expected="$2" actual="$3"
  [ "$expected" = "$actual" ] || { echo "FAIL C05-02 SQL $label want=$expected got=$actual" >&2; exit 1; }
  POS=$((POS+1))
  echo "PASS C05-02 SQL $label"
}
deny() {
  local label="$1" result
  result="$(runner_sql "$2")"
  [ "$result" = "DENIED" ] || { echo "FAIL C05-02 SQL accepted $label: $result" >&2; exit 1; }
  NEG=$((NEG+1))
  echo "PASS C05-02 SQL denied $label"
}
deny_privilege() {
  local label="$1" result
  if result="$(runner_sql "$2" 2>&1)"; then
    echo "FAIL C05-02 SQL unauthorized direct access $label" >&2; exit 1
  fi
  case "$result" in *"ERROR 1142"*|*"ERROR 1143"*) ;; *)
    echo "FAIL C05-02 SQL wrong rejection $label: $result" >&2; exit 1 ;;
  esac
  NEG=$((NEG+1))
}
hex64() { printf '%064d' 0 | tr 0 "$1"; }
A="aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
B="bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
IA="aaaaaaaaaaaaaaaaaaaaaaaaaaaa0001"
IB="bbbbbbbbbbbbbbbbbbbbbbbbbbbb0001"
AA="aaaaaaaaaaaaaaaaaaaaaaaaaaaa0002"
AB="bbbbbbbbbbbbbbbbbbbbbbbbbbbb0002"
RA="aaaaaaaaaaaaaaaaaaaaaaaaaaaa0003"
RB="bbbbbbbbbbbbbbbbbbbbbbbbbbbb0003"
I1="11111111111111111111111111111111"
I2="22222222222222222222222222222222"
I3="33333333333333333333333333333333"
I4="44444444444444444444444444444444"
HA="$(hex64 a)"
HB="$(hex64 b)"
HR="$(hex64 c)"
HE="$(hex64 d)"
HV="$(hex64 e)"
call_claim() {
  printf "CALL c05_02_claim_once(X'%s',X'%s',X'%s',X'%s',X'%s',%s,%s);" \
    "$1" "$2" "$3" "$4" "$5" "$6" "$7"
}
docker run --rm -d --network none --name "$NAME" \
  -e MYSQL_ALLOW_EMPTY_PASSWORD=yes -e MYSQL_DATABASE="$DB" "$IMAGE" >/dev/null
booted=0
ready=0
for _ in $(seq 1 120); do
  if [ "$booted" -eq 0 ] &&
     docker logs "$NAME" 2>&1 | grep -Fq 'MySQL init process done. Ready for start up.'; then
    booted=1
  fi
  if [ "$booted" -eq 1 ] && root_sql 'SELECT 1' >/dev/null 2>&1; then
    sleep 1
    if root_sql 'SELECT 1' >/dev/null 2>&1; then ready=1; break; fi
  fi
  sleep 1
done
[ "$ready" -eq 1 ] || { echo "FAIL C05-02 real MySQL startup" >&2; exit 1; }
case "$(root_sql 'SELECT VERSION()')" in
  8.4.*) ;; *) echo 'FAIL C05-02 requires MySQL8.4' >&2; exit 1 ;;
esac
docker exec -i "$NAME" mysql -uroot -D "$DB" \
  < database/tests/fixtures/c05_02_invitation_atomic_fixture.sql
same "five InnoDB tables" "5" "$(root_sql "SELECT COUNT(*) FROM information_schema.tables
  WHERE table_schema=DATABASE() AND table_name LIKE 'c05_02_%' AND engine='InnoDB'")"
same "six restricted tenant foreign keys" "6" "$(root_sql "SELECT COUNT(*) FROM information_schema.referential_constraints
  WHERE constraint_schema=DATABASE() AND constraint_name LIKE 'fk_c05_02_%'
    AND update_rule='RESTRICT' AND delete_rule='RESTRICT'")"
same "invitation scoped primary/unique keys" "2" "$(root_sql "SELECT COUNT(*) FROM information_schema.statistics
  WHERE table_schema=DATABASE() AND table_name='c05_02_invitations'
    AND index_name IN ('PRIMARY','uq_c05_02_digest_tenant') AND seq_in_index=1")"
same "no plaintext token/email/password column" "0" "$(root_sql "SELECT COUNT(*)
  FROM information_schema.columns WHERE table_schema=DATABASE()
  AND table_name='c05_02_invitations'
  AND (column_name LIKE '%token_raw%' OR column_name LIKE '%email%'
       OR column_name LIKE '%password%' OR column_name LIKE '%secret%')")"
root_sql "INSERT INTO c05_02_tenants VALUES (X'$A','ACTIVE',42),(X'$B','ACTIVE',42);
 INSERT INTO c05_02_issuers VALUES
 (X'$A',X'$IA','INVITE_EMPLOYEE',1),(X'$B',X'$IB','INVITE_CONSUMER',1);
 INSERT INTO c05_02_recipients VALUES
 (X'$A',X'$RA',X'$AA',1,DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 1 DAY)),
 (X'$B',X'$RB',X'$AB',1,DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 1 DAY));
 INSERT INTO c05_02_consents VALUES
 (X'$A',X'$RA',42,UTC_TIMESTAMP(6)),(X'$B',X'$RB',42,UTC_TIMESTAMP(6));
 INSERT INTO c05_02_invitations VALUES
 (X'$A',X'$I1',X'$IA',X'$RA','EMPLOYEE',X'$HA','PENDING',1,42,
  DATE_SUB(UTC_TIMESTAMP(6),INTERVAL 1 HOUR),DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 2 HOUR),NULL),
 (X'$B',X'$I1',X'$IB',X'$RB','CONSUMER',X'$HB','PENDING',1,42,
  DATE_SUB(UTC_TIMESTAMP(6),INTERVAL 1 HOUR),DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 2 HOUR),NULL),
 (X'$A',X'$I2',X'$IA',X'$RA','EMPLOYEE',X'$HR','PENDING',1,42,
  DATE_SUB(UTC_TIMESTAMP(6),INTERVAL 1 HOUR),DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 2 HOUR),NULL),
 (X'$A',X'$I3',X'$IA',X'$RA','EMPLOYEE',X'$HE','PENDING',1,42,
  DATE_SUB(UTC_TIMESTAMP(6),INTERVAL 3 HOUR),DATE_SUB(UTC_TIMESTAMP(6),INTERVAL 1 HOUR),NULL),
 (X'$A',X'$I4',X'$IA',X'$RA','EMPLOYEE',X'$HV','REVOKED',2,42,
  DATE_SUB(UTC_TIMESTAMP(6),INTERVAL 3 HOUR),DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 1 HOUR),NULL);
 CREATE USER 'c05_02_invite_runner'@'localhost' IDENTIFIED BY '$PASSWORD';
 GRANT EXECUTE ON PROCEDURE \`$DB\`.c05_02_claim_once TO 'c05_02_invite_runner'@'localhost';"
deny_privilege "SELECT invitations" "SELECT * FROM c05_02_invitations"
deny_privilege "UPDATE invitations" "UPDATE c05_02_invitations SET lifecycle='CLAIMED'"
deny "A invitation ID with B tenant and A digest" \
  "$(call_claim "$B" "$I1" "$AB" "$RB" "$HA" 42 1)"
deny "wrong recipient" "$(call_claim "$A" "$I1" "$AA" "$RB" "$HA" 42 1)"
deny "wrong actor" "$(call_claim "$A" "$I1" "$AB" "$RA" "$HA" 42 1)"
deny "wrong digest" "$(call_claim "$A" "$I1" "$AA" "$RA" "$HB" 42 1)"
deny "wrong revision" "$(call_claim "$A" "$I1" "$AA" "$RA" "$HA" 42 2)"
deny "wrong terms" "$(call_claim "$A" "$I1" "$AA" "$RA" "$HA" 41 1)"
deny "expired token" "$(call_claim "$A" "$I3" "$AA" "$RA" "$HE" 42 1)"
deny "revoked token" "$(call_claim "$A" "$I4" "$AA" "$RA" "$HV" 42 2)"
root_sql "UPDATE c05_02_tenants SET lifecycle='SUSPENDED' WHERE tenant_id=X'$A'"
deny "suspended tenant" "$(call_claim "$A" "$I1" "$AA" "$RA" "$HA" 42 1)"
root_sql "UPDATE c05_02_tenants SET lifecycle='ACTIVE',terms_version=43 WHERE tenant_id=X'$A'"
deny "terms upgraded without consent" "$(call_claim "$A" "$I1" "$AA" "$RA" "$HA" 42 1)"
root_sql "UPDATE c05_02_tenants SET terms_version=42 WHERE tenant_id=X'$A'"
root_sql "UPDATE c05_02_issuers SET enabled=0 WHERE tenant_id=X'$A'"
deny "revoked issuer" "$(call_claim "$A" "$I1" "$AA" "$RA" "$HA" 42 1)"
root_sql "UPDATE c05_02_issuers SET enabled=1 WHERE tenant_id=X'$A'"
root_sql "UPDATE c05_02_recipients SET channel_verified=0 WHERE tenant_id=X'$A'"
deny "revoked recipient channel" "$(call_claim "$A" "$I1" "$AA" "$RA" "$HA" 42 1)"
root_sql "UPDATE c05_02_recipients SET channel_verified=1 WHERE tenant_id=X'$A'"
same "valid A claim" "CLAIMED" \
  "$(runner_sql "$(call_claim "$A" "$I1" "$AA" "$RA" "$HA" 42 1)")"
deny "replay of claimed A token" "$(call_claim "$A" "$I1" "$AA" "$RA" "$HA" 42 1)"
same "valid independent B with same invitation ID" "CLAIMED" \
  "$(runner_sql "$(call_claim "$B" "$I1" "$AB" "$RB" "$HB" 42 1)")"
same "A exactly one claimed revision 2" "1" "$(root_sql "SELECT COUNT(*)
  FROM c05_02_invitations WHERE tenant_id=X'$A' AND invitation_id=X'$I1'
  AND lifecycle='CLAIMED' AND revision=2")"
same "B independent claimed revision 2" "1" "$(root_sql "SELECT COUNT(*)
  FROM c05_02_invitations WHERE tenant_id=X'$B' AND invitation_id=X'$I1'
  AND lifecycle='CLAIMED' AND revision=2")"
race="$(call_claim "$A" "$I2" "$AA" "$RA" "$HR" 42 1)"
for i in $(seq 1 16); do
  (
    if runner_sql "$race" > "$TMP/out-$i" 2> "$TMP/err-$i"; then
      echo 0 > "$TMP/rc-$i"
    else
      echo 1 > "$TMP/rc-$i"
    fi
  ) &
done
wait
won=0
lost=0
for i in $(seq 1 16); do
  [ "$(cat "$TMP/rc-$i")" = 0 ] || {
    echo "FAIL C05-02 MySQL concurrency client error $(cat "$TMP/err-$i")" >&2
    exit 1
  }
  case "$(cat "$TMP/out-$i")" in
    CLAIMED) won=$((won+1));;
    DENIED) lost=$((lost+1));;
    *) echo "FAIL C05-02 MySQL unknown concurrent response" >&2; exit 1;;
  esac
done
same "16 real MySQL clients exactly ONE winner" "1" "$won"
same "15 losing claims denied" "15" "$lost"
same "race row changed once to revision 2" "1" "$(root_sql "SELECT COUNT(*)
  FROM c05_02_invitations WHERE tenant_id=X'$A' AND invitation_id=X'$I2'
  AND lifecycle='CLAIMED' AND revision=2")"
same "B unaffected by A race" "1" "$(root_sql "SELECT COUNT(*)
  FROM c05_02_invitations WHERE tenant_id=X'$B' AND invitation_id=X'$I1'
  AND lifecycle='CLAIMED'")"
same "no membership grants created" "0" "$(root_sql "SELECT COUNT(*)
  FROM information_schema.tables WHERE table_schema=DATABASE()
    AND table_name='c05_02_memberships'")"
echo "PASS C05-02 REAL MySQL8.4 disposable invitation CAS $POS positive, $NEG denied; 16 competing clients one winner"
echo "LIMIT: disposable SQL fixture only; no production identity adapter, HTTP signup, email or tenant membership grants"
