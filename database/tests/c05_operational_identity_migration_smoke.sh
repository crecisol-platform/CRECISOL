#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
NAME="crecisol-c05-operational-migration-${RANDOM}-${RANDOM}"
PASSWORD="$(openssl rand -hex 24)"
IMAGE='mysql:8.4@sha256:a81d043930e51745522bd5c430961cf8286f0a23f0a9a2a6e0108f7ea71b0902'
cleanup() { docker rm -f "$NAME" >/dev/null 2>&1 || true; }
trap cleanup EXIT

docker run --rm -d --name "$NAME" \
  -e "MYSQL_ROOT_PASSWORD=$PASSWORD" \
  -e MYSQL_DATABASE=crecisol_c05_migration \
  "$IMAGE" >/dev/null

ready=0
for _ in $(seq 1 120); do
  if docker logs "$NAME" 2>&1 | grep -Fq 'MySQL init process done. Ready for start up.' &&
     docker exec -e "MYSQL_PWD=$PASSWORD" "$NAME" \
       mysql -uroot -Nse 'SELECT 1' >/dev/null 2>&1; then
    sleep 1
    docker exec -e "MYSQL_PWD=$PASSWORD" "$NAME" \
      mysql -uroot -Nse 'SELECT 1' >/dev/null 2>&1 && { ready=1; break; }
  fi
  sleep 1
done
[[ "$ready" == 1 ]] || { docker logs "$NAME"; exit 1; }

root_sql() {
  docker exec -e "MYSQL_PWD=$PASSWORD" "$NAME" \
    mysql -uroot -N -B -D crecisol_c05_migration -e "$1"
}

docker exec -i -e "MYSQL_PWD=$PASSWORD" "$NAME" \
  mysql -uroot crecisol_c05_migration \
  < "$ROOT/database/migrations/001_c05_identity_operational.sql"
docker exec -i -e "MYSQL_PWD=$PASSWORD" "$NAME" \
  mysql -uroot crecisol_c05_migration \
  < "$ROOT/database/migrations/002_c05_registration_proof_outbox.sql"
docker exec -i -e "MYSQL_PWD=$PASSWORD" "$NAME" \
  mysql -uroot crecisol_c05_migration \
  < "$ROOT/database/migrations/003_c05_pending_account_completion.sql"
docker exec -i -e "MYSQL_PWD=$PASSWORD" "$NAME" \
  mysql -uroot crecisol_c05_migration \
  < "$ROOT/database/migrations/004_c05_local_proof_delivery.sql"
docker exec -i -e "MYSQL_PWD=$PASSWORD" "$NAME" \
  mysql -uroot crecisol_c05_migration \
  < "$ROOT/database/migrations/005_c05_operational_login_directory.sql"

actual="$(docker exec -e "MYSQL_PWD=$PASSWORD" "$NAME" \
  mysql -uroot -Nse 'SHOW TABLES' crecisol_c05_migration | sort)"
expected="$(printf '%s\n' c05_identity_account c05_identity_audit \
  c05_identity_registration_intent c05_identity_registration_quota \
  c05_identity_mailbox_proof c05_identity_mailbox_outbox c05_identity_membership \
  c05_identity_registration_completion_quota \
  c05_identity_membership_approval c05_identity_mfa_factor \
  c05_identity_recovery c05_identity_resource_grant \
  c05_identity_session c04_tenant_catalog c04_verified_tenant_host | sort)"
[[ "$actual" == "$expected" ]]

# Tenant-scoped membership FK rejects a session whose membership belongs to a
# different tenant; the account/membership fixture is intentionally synthetic.
docker exec -i -e "MYSQL_PWD=$PASSWORD" "$NAME" \
  mysql -uroot crecisol_c05_migration <<'SQL'
INSERT INTO c05_identity_account VALUES
(UNHEX('00000000000000000000000000000001'),REPEAT(UNHEX('01'),32),X'01',
 UNHEX('000000000000000000000000'),UNHEX('00000000000000000000000000000000'),1,
 UTC_TIMESTAMP(6), '$argon2id$v=19$m=65536,t=3,p=1$test$test',1,1,
 UTC_TIMESTAMP(6),UTC_TIMESTAMP(6));
INSERT INTO c05_identity_membership
(tenant_id,membership_id,account_id,membership_state,role_code,created_at_utc)
VALUES (UNHEX('00000000000000000000000000000002'),
 UNHEX('00000000000000000000000000000003'),
 UNHEX('00000000000000000000000000000001'),0,'PENDING',UTC_TIMESTAMP(6));
INSERT INTO c05_identity_audit
(tenant_id,audit_id,action_code,outcome_code,context_hmac,created_at_utc)
VALUES (UNHEX('00000000000000000000000000000002'),
 UNHEX('00000000000000000000000000000008'),'REGISTER',1,
 REPEAT(UNHEX('03'),32),UTC_TIMESTAMP(6));
SQL

[[ "$(root_sql "SELECT CONCAT(profile_kind,':',membership_state,':',granted_at_utc IS NULL,':',expires_at_utc IS NULL) FROM c05_identity_membership WHERE membership_id=UNHEX('00000000000000000000000000000003')")" == '3:0:1:1' ]]
root_sql "UPDATE c05_identity_membership SET membership_state=1,approval_revision=2,approved_at_utc=UTC_TIMESTAMP(6) WHERE membership_id=UNHEX('00000000000000000000000000000003')"
[[ "$(root_sql "SELECT CONCAT(membership_state,':',granted_at_utc IS NOT NULL,':',expires_at_utc>granted_at_utc,':',changed_at_utc>=approved_at_utc) FROM c05_identity_membership WHERE membership_id=UNHEX('00000000000000000000000000000003')")" == '1:1:1:1' ]]
root_sql "UPDATE c05_identity_membership SET membership_state=2,approval_revision=3 WHERE membership_id=UNHEX('00000000000000000000000000000003')"
[[ "$(root_sql "SELECT CONCAT(membership_state,':',granted_at_utc IS NOT NULL,':',expires_at_utc>granted_at_utc) FROM c05_identity_membership WHERE membership_id=UNHEX('00000000000000000000000000000003')")" == '2:1:1' ]]

if docker exec -i -e "MYSQL_PWD=$PASSWORD" "$NAME" \
    mysql -uroot crecisol_c05_migration 2>/dev/null <<'SQL'
INSERT INTO c05_identity_session VALUES
(UNHEX('00000000000000000000000000000004'),
 UNHEX('00000000000000000000000000000005'),
 UNHEX('00000000000000000000000000000001'),
 UNHEX('00000000000000000000000000000003'),REPEAT(UNHEX('02'),32),
 UNHEX('00000000000000000000000000000006'),
 UNHEX('00000000000000000000000000000007'),UTC_TIMESTAMP(6),1,
 UTC_TIMESTAMP(6),UTC_TIMESTAMP(6) + INTERVAL 1 HOUR,NULL,NULL);
SQL
then
  echo 'FAIL cross-tenant membership/session link unexpectedly accepted' >&2
  exit 1
fi

# Exercise the live completion procedures: hard per-peer budget and an
# atomic mailbox-proof -> PENDING account + PENDING membership transition.
docker exec -i -e "MYSQL_PWD=$PASSWORD" "$NAME" \
  mysql -uroot crecisol_c05_migration <<'SQL'
INSERT INTO c04_tenant_catalog VALUES
(UNHEX('00000000000000000000000000000009'),'registration-test','Registration test',3,2,1,UTC_TIMESTAMP(6));
INSERT INTO c05_identity_registration_intent
  (tenant_id,intent_id,mailbox_hmac,peer_hmac,mailbox_ciphertext,mailbox_nonce,
   mailbox_tag,key_version,state,created_at_utc,expires_at_utc)
VALUES (UNHEX('00000000000000000000000000000009'),
        UNHEX('00000000000000000000000000000091'),REPEAT(UNHEX('11'),32),
        REPEAT(UNHEX('22'),32),REPEAT(UNHEX('55'),24),
        REPEAT(UNHEX('66'),12),REPEAT(UNHEX('77'),16),1,0,
        UTC_TIMESTAMP(6),UTC_TIMESTAMP(6)+INTERVAL 30 MINUTE);
INSERT INTO c05_identity_mailbox_proof
  (tenant_id,proof_id,mailbox_hmac,proof_token_hmac,mailbox_ciphertext,
   mailbox_nonce,mailbox_tag,key_version,proof_state,created_at_utc,
   expires_at_utc,consumed_at_utc,failed_attempts)
VALUES (UNHEX('00000000000000000000000000000009'),
        UNHEX('00000000000000000000000000000091'),REPEAT(UNHEX('11'),32),
        REPEAT(UNHEX('33'),32),REPEAT(UNHEX('55'),24),
        REPEAT(UNHEX('66'),12),REPEAT(UNHEX('77'),16),1,0,
        UTC_TIMESTAMP(6),UTC_TIMESTAMP(6)+INTERVAL 10 MINUTE,NULL,0);
INSERT INTO c05_identity_mailbox_outbox
  (tenant_id,proof_id,payload_ciphertext,payload_nonce,payload_tag,key_version,
   delivery_state,attempts,created_at_utc,expires_at_utc)
VALUES (UNHEX('00000000000000000000000000000009'),
        UNHEX('00000000000000000000000000000091'),REPEAT(UNHEX('88'),32),
        REPEAT(UNHEX('99'),12),REPEAT(UNHEX('aa'),16),1,0,0,
        UTC_TIMESTAMP(6),UTC_TIMESTAMP(6)+INTERVAL 10 MINUTE);
SQL
window_start="$(root_sql "SELECT DATE_FORMAT(UTC_TIMESTAMP(0),'%Y-%m-%d %H:%i:00')")"
for attempt in 1 2 3 4 5; do
  result="$(root_sql "CALL c05_operational_reserve_registration_completion(UNHEX('00000000000000000000000000000009'),REPEAT(UNHEX('bb'),32),'$window_start')")"
  [[ "$result" == ADMITTED ]]
done
result="$(root_sql "CALL c05_operational_reserve_registration_completion(UNHEX('00000000000000000000000000000009'),REPEAT(UNHEX('bb'),32),'$window_start')")"
[[ "$result" == LIMITED ]]

result="$(root_sql "CALL c05_operational_complete_pending_registration(
  UNHEX('00000000000000000000000000000009'),
  UNHEX('00000000000000000000000000000091'),REPEAT(UNHEX('33'),32),
  REPEAT(UNHEX('11'),32),REPEAT(UNHEX('44'),32),
  '\$argon2id\$v=19\$m=19456,t=2,p=1\$YWJjZGVmZ2hpamts\$YWJjZGVmZ2hpamtsYWJjZGVmZ2hpamts',
  UNHEX('00000000000000000000000000000092'),
  UNHEX('00000000000000000000000000000093'),UTC_TIMESTAMP(6))")"
[[ "$result" == PENDING ]]
state="$(root_sql "SELECT CONCAT(a.account_state,':',m.membership_state,':',p.proof_state,':',o.delivery_state)
  FROM c05_identity_account a
  JOIN c05_identity_membership m ON m.account_id=a.account_id
  JOIN c05_identity_mailbox_proof p
    ON p.proof_id=UNHEX('00000000000000000000000000000091')
  JOIN c05_identity_mailbox_outbox o ON o.proof_id=p.proof_id
 WHERE a.account_id=UNHEX('00000000000000000000000000000092')")"
[[ "$state" == '0:0:1:3' ]]
[[ "$(root_sql "SELECT COUNT(*) FROM c05_identity_audit WHERE action_code='REGISTRATION_PENDING'")" == 1 ]]

# Claim/lease delivery is compare-and-set; no direct table read is needed by
# the worker role. Failed SMTP attempts return to the queue and success closes
# the one-use outbox record.
docker exec -i -e "MYSQL_PWD=$PASSWORD" "$NAME" \
  mysql -uroot crecisol_c05_migration <<'SQL'
INSERT INTO c05_identity_registration_intent
  (tenant_id,intent_id,mailbox_hmac,peer_hmac,mailbox_ciphertext,mailbox_nonce,
   mailbox_tag,key_version,state,created_at_utc,expires_at_utc)
VALUES (UNHEX('00000000000000000000000000000009'),
        UNHEX('00000000000000000000000000000094'),REPEAT(UNHEX('14'),32),
        REPEAT(UNHEX('24'),32),REPEAT(UNHEX('54'),24),
        REPEAT(UNHEX('64'),12),REPEAT(UNHEX('74'),16),1,0,
        UTC_TIMESTAMP(6),UTC_TIMESTAMP(6)+INTERVAL 30 MINUTE);
INSERT INTO c05_identity_mailbox_proof
  (tenant_id,proof_id,mailbox_hmac,proof_token_hmac,mailbox_ciphertext,
   mailbox_nonce,mailbox_tag,key_version,proof_state,created_at_utc,
   expires_at_utc,consumed_at_utc,failed_attempts)
VALUES (UNHEX('00000000000000000000000000000009'),
        UNHEX('00000000000000000000000000000094'),REPEAT(UNHEX('14'),32),
        REPEAT(UNHEX('34'),32),REPEAT(UNHEX('54'),24),
        REPEAT(UNHEX('64'),12),REPEAT(UNHEX('74'),16),1,0,
        UTC_TIMESTAMP(6),UTC_TIMESTAMP(6)+INTERVAL 10 MINUTE,NULL,0);
INSERT INTO c05_identity_mailbox_outbox
  (tenant_id,proof_id,payload_ciphertext,payload_nonce,payload_tag,key_version,
   delivery_state,attempts,created_at_utc,expires_at_utc)
VALUES (UNHEX('00000000000000000000000000000009'),
        UNHEX('00000000000000000000000000000094'),REPEAT(UNHEX('84'),32),
        REPEAT(UNHEX('94'),12),REPEAT(UNHEX('a4'),16),1,0,0,
        UTC_TIMESTAMP(6),UTC_TIMESTAMP(6)+INTERVAL 10 MINUTE);
INSERT INTO c05_identity_registration_intent
  (tenant_id,intent_id,mailbox_hmac,peer_hmac,mailbox_ciphertext,mailbox_nonce,
   mailbox_tag,key_version,state,created_at_utc,expires_at_utc)
VALUES (UNHEX('00000000000000000000000000000009'),
        UNHEX('00000000000000000000000000000095'),REPEAT(UNHEX('15'),32),
        REPEAT(UNHEX('25'),32),REPEAT(UNHEX('55'),24),
        REPEAT(UNHEX('65'),12),REPEAT(UNHEX('75'),16),1,0,
        UTC_TIMESTAMP(6),UTC_TIMESTAMP(6)+INTERVAL 30 MINUTE),
       (UNHEX('00000000000000000000000000000009'),
        UNHEX('00000000000000000000000000000096'),REPEAT(UNHEX('16'),32),
        REPEAT(UNHEX('26'),32),REPEAT(UNHEX('56'),24),
        REPEAT(UNHEX('66'),12),REPEAT(UNHEX('76'),16),1,0,
        UTC_TIMESTAMP(6),UTC_TIMESTAMP(6)+INTERVAL 30 MINUTE);
INSERT INTO c05_identity_mailbox_proof
  (tenant_id,proof_id,mailbox_hmac,proof_token_hmac,mailbox_ciphertext,
   mailbox_nonce,mailbox_tag,key_version,proof_state,created_at_utc,
   expires_at_utc,consumed_at_utc,failed_attempts)
VALUES (UNHEX('00000000000000000000000000000009'),
        UNHEX('00000000000000000000000000000095'),REPEAT(UNHEX('15'),32),
        REPEAT(UNHEX('35'),32),REPEAT(UNHEX('55'),24),
        REPEAT(UNHEX('65'),12),REPEAT(UNHEX('75'),16),1,0,
        UTC_TIMESTAMP(6),UTC_TIMESTAMP(6)+INTERVAL 10 MINUTE,NULL,0),
       (UNHEX('00000000000000000000000000000009'),
        UNHEX('00000000000000000000000000000096'),REPEAT(UNHEX('16'),32),
        REPEAT(UNHEX('36'),32),REPEAT(UNHEX('56'),24),
        REPEAT(UNHEX('66'),12),REPEAT(UNHEX('76'),16),1,0,
        UTC_TIMESTAMP(6),UTC_TIMESTAMP(6)+INTERVAL 10 MINUTE,NULL,0);
INSERT INTO c05_identity_mailbox_outbox
  (tenant_id,proof_id,payload_ciphertext,payload_nonce,payload_tag,key_version,
   delivery_state,attempts,created_at_utc,expires_at_utc)
VALUES (UNHEX('00000000000000000000000000000009'),
        UNHEX('00000000000000000000000000000095'),REPEAT(UNHEX('85'),32),
        REPEAT(UNHEX('95'),12),REPEAT(UNHEX('a5'),16),1,0,0,
        UTC_TIMESTAMP(6),UTC_TIMESTAMP(6)+INTERVAL 10 MINUTE),
       (UNHEX('00000000000000000000000000000009'),
        UNHEX('00000000000000000000000000000096'),REPEAT(UNHEX('86'),32),
        REPEAT(UNHEX('96'),12),REPEAT(UNHEX('a6'),16),1,0,0,
        UTC_TIMESTAMP(6),UTC_TIMESTAMP(6)+INTERVAL 10 MINUTE);
SQL
docker exec -e "MYSQL_PWD=$PASSWORD" "$NAME" mysql -uroot crecisol_c05_migration \
  -e "CALL c05_operational_claim_mailbox_proof_outbox(UNHEX(REPEAT('dd',16)))" >/dev/null
[[ "$(root_sql "SELECT CONCAT(delivery_state,':',attempts,':',HEX(lease_owner)) FROM c05_identity_mailbox_outbox WHERE proof_id=UNHEX('00000000000000000000000000000094')")" == '1:1:DDDDDDDDDDDDDDDDDDDDDDDDDDDDDDDD' ]]
[[ "$(root_sql "CALL c05_operational_complete_mailbox_proof_delivery(UNHEX('00000000000000000000000000000009'),UNHEX('00000000000000000000000000000094'),UNHEX(REPEAT('dd',16)),FALSE)")" == RETRY ]]
[[ "$(root_sql "SELECT CONCAT(delivery_state,':',attempts,':',lease_owner IS NULL) FROM c05_identity_mailbox_outbox WHERE proof_id=UNHEX('00000000000000000000000000000094')")" == '0:1:1' ]]
docker exec -e "MYSQL_PWD=$PASSWORD" "$NAME" mysql -uroot crecisol_c05_migration \
  -e "CALL c05_operational_claim_mailbox_proof_outbox(UNHEX(REPEAT('ee',16)))" >/dev/null
[[ "$(root_sql "CALL c05_operational_complete_mailbox_proof_delivery(UNHEX('00000000000000000000000000000009'),UNHEX('00000000000000000000000000000094'),UNHEX(REPEAT('ee',16)),TRUE)")" == DELIVERED ]]
[[ "$(root_sql "SELECT CONCAT(delivery_state,':',delivered_at_utc IS NOT NULL,':',lease_owner IS NULL) FROM c05_identity_mailbox_outbox WHERE proof_id=UNHEX('00000000000000000000000000000094')")" == '2:1:1' ]]

# A crashed sender is reclaimed after its two-minute lease. The third claim
# that expires without completion permanently revokes the proof.
docker exec -e "MYSQL_PWD=$PASSWORD" "$NAME" mysql -uroot crecisol_c05_migration \
  -e "CALL c05_operational_claim_mailbox_proof_outbox(UNHEX(REPEAT('ff',16)))" >/dev/null
root_sql "UPDATE c05_identity_mailbox_outbox SET leased_until_utc=UTC_TIMESTAMP(6)-INTERVAL 1 SECOND WHERE proof_id=UNHEX('00000000000000000000000000000095')"
docker exec -e "MYSQL_PWD=$PASSWORD" "$NAME" mysql -uroot crecisol_c05_migration \
  -e "CALL c05_operational_claim_mailbox_proof_outbox(UNHEX(REPEAT('a1',16)))" >/dev/null
[[ "$(root_sql "SELECT CONCAT(delivery_state,':',attempts,':',HEX(lease_owner)) FROM c05_identity_mailbox_outbox WHERE proof_id=UNHEX('00000000000000000000000000000095')")" == '1:2:A1A1A1A1A1A1A1A1A1A1A1A1A1A1A1A1' ]]
[[ "$(root_sql "CALL c05_operational_complete_mailbox_proof_delivery(UNHEX('00000000000000000000000000000009'),UNHEX('00000000000000000000000000000095'),UNHEX(REPEAT('a1',16)),TRUE)")" == DELIVERED ]]

docker exec -e "MYSQL_PWD=$PASSWORD" "$NAME" mysql -uroot crecisol_c05_migration \
  -e "CALL c05_operational_claim_mailbox_proof_outbox(UNHEX(REPEAT('a2',16)))" >/dev/null
root_sql "UPDATE c05_identity_mailbox_outbox SET leased_until_utc=UTC_TIMESTAMP(6)-INTERVAL 1 SECOND WHERE proof_id=UNHEX('00000000000000000000000000000096')"
docker exec -e "MYSQL_PWD=$PASSWORD" "$NAME" mysql -uroot crecisol_c05_migration \
  -e "CALL c05_operational_claim_mailbox_proof_outbox(UNHEX(REPEAT('a3',16)))" >/dev/null
root_sql "UPDATE c05_identity_mailbox_outbox SET leased_until_utc=UTC_TIMESTAMP(6)-INTERVAL 1 SECOND WHERE proof_id=UNHEX('00000000000000000000000000000096')"
docker exec -e "MYSQL_PWD=$PASSWORD" "$NAME" mysql -uroot crecisol_c05_migration \
  -e "CALL c05_operational_claim_mailbox_proof_outbox(UNHEX(REPEAT('a4',16)))" >/dev/null
root_sql "UPDATE c05_identity_mailbox_outbox SET leased_until_utc=UTC_TIMESTAMP(6)-INTERVAL 1 SECOND WHERE proof_id=UNHEX('00000000000000000000000000000096')"
docker exec -e "MYSQL_PWD=$PASSWORD" "$NAME" mysql -uroot crecisol_c05_migration \
  -e "CALL c05_operational_claim_mailbox_proof_outbox(UNHEX(REPEAT('a5',16)))" >/dev/null
[[ "$(root_sql "SELECT CONCAT(o.delivery_state,':',o.attempts,':',p.proof_state,':',o.lease_owner IS NULL) FROM c05_identity_mailbox_outbox o JOIN c05_identity_mailbox_proof p USING(tenant_id,proof_id) WHERE o.proof_id=UNHEX('00000000000000000000000000000096')")" == '3:3:3:1' ]]

if docker exec -i -e "MYSQL_PWD=$PASSWORD" "$NAME" \
    mysql -uroot crecisol_c05_migration 2>/dev/null <<'SQL'
UPDATE c05_identity_audit SET action_code='tampered';
SQL
then
  echo 'FAIL audit update unexpectedly accepted' >&2
  exit 1
fi

docker exec -i -e "MYSQL_PWD=$PASSWORD" "$NAME" \
  mysql -uroot crecisol_c05_migration \
  < "$ROOT/database/migrations/005_c05_operational_login_directory.down.sql"
docker exec -i -e "MYSQL_PWD=$PASSWORD" "$NAME" \
  mysql -uroot crecisol_c05_migration \
  < "$ROOT/database/migrations/004_c05_local_proof_delivery.down.sql"
docker exec -i -e "MYSQL_PWD=$PASSWORD" "$NAME" \
  mysql -uroot crecisol_c05_migration \
  < "$ROOT/database/migrations/003_c05_pending_account_completion.down.sql"
docker exec -i -e "MYSQL_PWD=$PASSWORD" "$NAME" \
  mysql -uroot crecisol_c05_migration \
  < "$ROOT/database/migrations/002_c05_registration_proof_outbox.down.sql"
docker exec -i -e "MYSQL_PWD=$PASSWORD" "$NAME" \
  mysql -uroot crecisol_c05_migration \
  < "$ROOT/database/migrations/001_c05_identity_operational.down.sql"
remaining="$(docker exec -e "MYSQL_PWD=$PASSWORD" "$NAME" \
  mysql -uroot -Nse 'SHOW TABLES' crecisol_c05_migration)"
[[ -z "$remaining" ]]

echo 'PASS C05 operational schema MySQL 8.4: forward, constraints, append-only audit, rollback'



