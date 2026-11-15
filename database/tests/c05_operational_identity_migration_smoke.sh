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

docker exec -i -e "MYSQL_PWD=$PASSWORD" "$NAME" \
  mysql -uroot crecisol_c05_migration \
  < "$ROOT/database/migrations/001_c05_identity_operational.sql"

actual="$(docker exec -e "MYSQL_PWD=$PASSWORD" "$NAME" \
  mysql -uroot -Nse 'SHOW TABLES' crecisol_c05_migration | sort)"
expected="$(printf '%s\n' c05_identity_account c05_identity_audit \
  c05_identity_membership c05_identity_mfa_factor c05_identity_session | sort)"
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

if docker exec -i -e "MYSQL_PWD=$PASSWORD" "$NAME" \
    mysql -uroot crecisol_c05_migration 2>/dev/null <<'SQL'
UPDATE c05_identity_audit SET action_code='tampered';
SQL
then
  echo 'FAIL audit update unexpectedly accepted' >&2
  exit 1
fi

echo 'PASS C05 operational schema MySQL 8.4: tables, constraints, append-only audit'

