#!/usr/bin/env bash
# C05-02: real disposable MySQL8.4 retention/SQL ACL tests; synthetic bytes only.
set -euo pipefail
IMAGE="mysql:8.4@sha256:a81d043930e51745522bd5c430961cf8286f0a23f0a9a2a6e0108f7ea71b0902"
NAME="crecisol-retention-$(openssl rand -hex 8)"
DB="c05_02_disposable"
ROOT_PASSWORD="$(openssl rand -hex 24)"
WORKER_PASSWORD="$(openssl rand -hex 24)"
cleanup() { docker rm -f "$NAME" >/dev/null 2>&1 || true; }
trap cleanup EXIT
if [ -n "$(printenv GITHUB_ACTIONS || true)" ]; then
  echo "::add-mask::$ROOT_PASSWORD"
  echo "::add-mask::$WORKER_PASSWORD"
fi
root_sql() {
  docker exec -e MYSQL_PWD="$ROOT_PASSWORD" "$NAME" mysql -uroot -N -B -D "$DB" -e "$1"
}
worker_sql() {
  docker exec -e MYSQL_PWD="$WORKER_PASSWORD" "$NAME" mysql -uc05_02_privacy_worker -N -B -D "$DB" -e "$1"
}
check() {
  if [ "$1" != "$2" ]; then
    echo "FAIL C05-02 privacy: $3" >&2
    exit 1
  fi
  echo "PASS C05-02 privacy: $3"
}
docker run --rm -d --network bridge --name "$NAME" -p '127.0.0.1::3306' \
  -e MYSQL_ROOT_PASSWORD="$ROOT_PASSWORD" -e MYSQL_DATABASE="$DB" "$IMAGE" >/dev/null
booted=0
ready=0
for _ in $(seq 1 120); do
  if [ "$booted" -eq 0 ] && docker logs "$NAME" 2>&1 | grep -Fq 'MySQL init process done. Ready for start up.'; then
    booted=1
  fi
  if [ "$booted" -eq 1 ] && root_sql 'SELECT 1' >/dev/null 2>&1; then
    sleep 1
    if root_sql 'SELECT 1' >/dev/null 2>&1; then ready=1; break; fi
  fi
  sleep 1
done
[ "$ready" -eq 1 ] || { echo 'FAIL C05-02 MySQL8.4 startup' >&2; exit 1; }
case "$(root_sql 'SELECT VERSION()')" in
  8.4.*) ;;
  *) echo 'FAIL C05-02 genuine MySQL8.4 required' >&2; exit 1 ;;
esac
for file in \
  database/tests/fixtures/c05_02_invitation_atomic_fixture.sql \
  database/tests/fixtures/c05_02_neutral_registration_fixture.sql \
  database/tests/fixtures/c05_02_privacy_retention_fixture.sql; do
  docker exec -e MYSQL_PWD="$ROOT_PASSWORD" -i "$NAME" mysql -uroot -D "$DB" < "$file"
done
root_sql "CREATE USER 'c05_02_privacy_worker'@'%' IDENTIFIED BY '$WORKER_PASSWORD';
GRANT EXECUTE ON PROCEDURE c05_02_disposable.c05_02_dispose_expired_registration_pii
TO 'c05_02_privacy_worker'@'%';"
root_sql "
INSERT INTO c05_02_tenants (tenant_id,lifecycle,terms_version)
VALUES (X'AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA','ACTIVE',42),
       (X'BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB','SUSPENDED',42);
INSERT INTO c05_02_neutral_intents
(tenant_id,intent_id,peer_digest,mailbox_digest,nonce,tag,ciphertext,
 key_version,captured_at_utc,expires_at_utc)
VALUES
(X'AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA',X'00000000000000000000000000000001',
 UNHEX(REPEAT('11',32)),UNHEX(REPEAT('21',32)),
 UNHEX(REPEAT('01',12)),UNHEX(REPEAT('02',16)),X'112233445566778899AABBCC',
 1,DATE_SUB(UTC_TIMESTAMP(6),INTERVAL 120 MINUTE),
   DATE_SUB(UTC_TIMESTAMP(6),INTERVAL 60 MINUTE)),
(X'BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB',X'00000000000000000000000000000002',
 UNHEX(REPEAT('12',32)),UNHEX(REPEAT('22',32)),
 UNHEX(REPEAT('03',12)),UNHEX(REPEAT('04',16)),X'2233445566778899AABBCCDD',
 1,DATE_SUB(UTC_TIMESTAMP(6),INTERVAL 120 MINUTE),
   DATE_SUB(UTC_TIMESTAMP(6),INTERVAL 60 MINUTE)),
(X'AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA',X'00000000000000000000000000000003',
 UNHEX(REPEAT('13',32)),UNHEX(REPEAT('23',32)),
 UNHEX(REPEAT('05',12)),UNHEX(REPEAT('06',16)),X'0A0B0C0D0E0F101112131415',
 1,DATE_SUB(UTC_TIMESTAMP(6),INTERVAL 1 MINUTE),
   DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 25 MINUTE)),
(X'BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB',X'00000000000000000000000000000004',
 UNHEX(REPEAT('14',32)),UNHEX(REPEAT('24',32)),
 UNHEX(REPEAT('07',12)),UNHEX(REPEAT('08',16)),X'1A1B1C1D1E1F212223242526',
 1,DATE_SUB(UTC_TIMESTAMP(6),INTERVAL 1 MINUTE),
   DATE_ADD(UTC_TIMESTAMP(6),INTERVAL 25 MINUTE));
INSERT INTO c05_02_neutral_quota (tenant_id,peer_digest,window_bucket,attempts)
VALUES
(X'AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA',UNHEX(REPEAT('11',32)),
 FLOOR(UNIX_TIMESTAMP(UTC_TIMESTAMP(6))/900)-3,1),
(X'BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB',UNHEX(REPEAT('12',32)),
 FLOOR(UNIX_TIMESTAMP(UTC_TIMESTAMP(6))/900)-3,1),
(X'AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA',UNHEX(REPEAT('13',32)),
 FLOOR(UNIX_TIMESTAMP(UTC_TIMESTAMP(6))/900)-1,1);
"
check "$(root_sql 'SELECT COUNT(*) FROM c05_02_neutral_intents')" "4" "Four synthetic intents across two tenants"
check "$(root_sql 'SELECT COUNT(*) FROM c05_02_privacy_dispositions')" "0" "No premature logical erasure"
check "$(worker_sql 'CALL c05_02_dispose_expired_registration_pii(1)')" "ACK" "Worker receives fixed neutral response"
check "$(root_sql 'SELECT COUNT(*) FROM c05_02_privacy_dispositions')" "1" "First batch is bounded"
check "$(worker_sql 'CALL c05_02_dispose_expired_registration_pii(1)')" "ACK" "Second batch succeeds"
check "$(root_sql 'SELECT COUNT(*) FROM c05_02_privacy_dispositions')" "2" "Both expired intents erased"
check "$(worker_sql 'CALL c05_02_dispose_expired_registration_pii(1)')" "ACK" "Idempotent replay succeeds"
check "$(root_sql 'SELECT COUNT(*) FROM c05_02_privacy_dispositions')" "2" "Replay does not erase live rows"
check "$(root_sql 'SELECT COUNT(*) FROM c05_02_neutral_intents WHERE expires_at_utc>UTC_TIMESTAMP(6)')" "2" "Both active tenant records remain valid"
check "$(root_sql "SELECT HEX(ciphertext) FROM c05_02_neutral_intents WHERE intent_id=X'00000000000000000000000000000003'")" "0A0B0C0D0E0F101112131415" "Active A ciphertext unchanged"
check "$(root_sql "SELECT HEX(ciphertext) FROM c05_02_neutral_intents WHERE intent_id=X'00000000000000000000000000000004'")" "1A1B1C1D1E1F212223242526" "Active B ciphertext unchanged"
check "$(root_sql "SELECT COUNT(*) FROM c05_02_neutral_intents WHERE expires_at_utc<=UTC_TIMESTAMP(6) AND (ciphertext IN (X'112233445566778899AABBCC',X'2233445566778899AABBCCDD') OR mailbox_digest IN (UNHEX(REPEAT('21',32)),UNHEX(REPEAT('22',32))))")" "0" "Expired encrypted data and mailbox digests overwritten"
check "$(root_sql "SELECT COUNT(*) FROM c05_02_neutral_intents i JOIN c05_02_privacy_dispositions d ON d.tenant_id=i.tenant_id AND d.intent_id=i.intent_id")" "2" "Tenant-scoped foreign keys remain valid"
check "$(root_sql 'SELECT COUNT(*) FROM c05_02_neutral_intents')" "4" "Referenced intent IDs remain present"
check "$(root_sql 'SELECT COUNT(*) FROM c05_02_neutral_quota')" "1" "Only prior valid quota window remains"
for value in 0 101 NULL; do
  if worker_sql "CALL c05_02_dispose_expired_registration_pii($value)" >/dev/null 2>&1; then
    echo "FAIL C05-02 privacy: accepted invalid batch size" >&2
    exit 1
  fi
done
echo 'PASS C05-02 privacy: invalid batch sizes rejected'
if worker_sql "SELECT ciphertext FROM c05_02_neutral_intents" >/dev/null 2>&1; then
  echo 'FAIL C05-02 privacy: SQL worker can read PII' >&2; exit 1
fi
if worker_sql "UPDATE c05_02_neutral_intents SET key_version=2" >/dev/null 2>&1; then
  echo 'FAIL C05-02 privacy: SQL worker can mutate PII directly' >&2; exit 1
fi
echo 'PASS C05-02 privacy: EXECUTE-only database role'
echo 'PASS REAL C05-02 MySQL8.4 logical erasure; NO signup or functional certification'
