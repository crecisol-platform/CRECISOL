#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
IMAGE='mysql:8.4@sha256:a81d043930e51745522bd5c430961cf8286f0a23f0a9a2a6e0108f7ea71b0902'
NAME="crecisol-c05-operational-http-${RANDOM}-$$"
ROOT_PASSWORD="$(openssl rand -hex 24)"
APP_PASSWORD="$(openssl rand -hex 24)"
MAILBOX_KEY="$(openssl rand -hex 32)"
PEER_KEY="$(openssl rand -hex 32)"
ENCRYPTION_KEY="$(openssl rand -hex 32)"
API_PID=''
cleanup() {
  if [[ -n "$API_PID" ]]; then kill "$API_PID" >/dev/null 2>&1 || true; wait "$API_PID" 2>/dev/null || true; fi
  docker rm -f "$NAME" >/dev/null 2>&1 || true
}
trap cleanup EXIT

docker run --rm -d --name "$NAME" -p '127.0.0.1::3306' \
  -e "MYSQL_ROOT_PASSWORD=$ROOT_PASSWORD" -e MYSQL_DATABASE=crecisol_c05 "$IMAGE" >/dev/null
port="$(docker port "$NAME" '3306/tcp' | sed 's/.*://')"
root_sql() { docker exec -e "MYSQL_PWD=$ROOT_PASSWORD" "$NAME" mysql -uroot -D crecisol_c05 -N -B -e "$1"; }
same() {
  local label="$1" expected="$2" actual="$3"
  [[ "$actual" == "$expected" ]] || { echo "FAIL $label expected=$expected actual=$actual" >&2; exit 1; }
}
ready=0
for _ in $(seq 1 120); do
  if docker logs "$NAME" 2>&1 | grep -Fq 'MySQL init process done. Ready for start up.' &&
     root_sql 'SELECT 1' >/dev/null 2>&1; then ready=1; break; fi
  sleep 1
done
[[ "$ready" == 1 ]] || { docker logs "$NAME"; exit 1; }
docker exec -i -e "MYSQL_PWD=$ROOT_PASSWORD" "$NAME" mysql -uroot -D crecisol_c05 \
  < "$ROOT/database/migrations/001_c05_identity_operational.sql"

root_sql "INSERT INTO c04_tenant_catalog VALUES
 (UNHEX('00000000000000000000000000000001'),'tenant-a','Tenant A',3,2,1,UTC_TIMESTAMP(6)),
 (UNHEX('00000000000000000000000000000002'),'tenant-b','Tenant B',3,2,1,UTC_TIMESTAMP(6)),
 (UNHEX('00000000000000000000000000000003'),'tenant-c','Tenant C',2,2,1,UTC_TIMESTAMP(6)),
 (UNHEX('00000000000000000000000000000004'),'tenant-d','Tenant D',3,2,1,UTC_TIMESTAMP(6));
INSERT INTO c04_verified_tenant_host VALUES
 ('tenant-a.example.test',UNHEX('00000000000000000000000000000001'),2,2,1,UTC_TIMESTAMP(6),UTC_TIMESTAMP(6),UTC_TIMESTAMP(6)+INTERVAL 1 DAY),
 ('tenant-b.example.test',UNHEX('00000000000000000000000000000002'),2,2,1,UTC_TIMESTAMP(6),UTC_TIMESTAMP(6),UTC_TIMESTAMP(6)+INTERVAL 1 DAY),
 ('tenant-c.example.test',UNHEX('00000000000000000000000000000003'),2,1,1,UTC_TIMESTAMP(6),NULL,NULL),
 ('tenant-d.example.test',UNHEX('00000000000000000000000000000004'),2,2,1,UTC_TIMESTAMP(6),UTC_TIMESTAMP(6),UTC_TIMESTAMP(6)+INTERVAL 1 DAY);
CREATE USER 'c05_app'@'%' IDENTIFIED BY '$APP_PASSWORD';
GRANT SELECT ON crecisol_c05.c04_tenant_catalog TO 'c05_app'@'%';
GRANT SELECT ON crecisol_c05.c04_verified_tenant_host TO 'c05_app'@'%';
GRANT SELECT,INSERT,UPDATE ON crecisol_c05.c05_identity_registration_quota TO 'c05_app'@'%';
GRANT INSERT ON crecisol_c05.c05_identity_registration_intent TO 'c05_app'@'%';"

dsn="Server=127.0.0.1;Port=$port;Database=crecisol_c05;User ID=c05_app;Password=$APP_PASSWORD;SslMode=None;"
cd "$ROOT"
dotnet build backend/src/Crecisol.Api/Crecisol.Api.csproj -c Release -warnaserror >/dev/null
api_port=5187
CRECISOL_C01_ALLOWED_HOSTS='127.0.0.1;tenant-a.example.test;tenant-b.example.test;tenant-c.example.test;tenant-d.example.test' \
CRECISOL_C05_REGISTRATION_ENABLED=true CRECISOL_C05_IDENTITY_DSN="$dsn" \
CRECISOL_C05_KEY_VERSION=1 CRECISOL_C05_MAILBOX_HMAC_KEY="$MAILBOX_KEY" \
CRECISOL_C05_PEER_HMAC_KEY="$PEER_KEY" CRECISOL_C05_MAILBOX_ENCRYPTION_KEY="$ENCRYPTION_KEY" \
dotnet run --no-build -c Release --project backend/src/Crecisol.Api/Crecisol.Api.csproj \
  --urls "http://127.0.0.1:$api_port" > /tmp/crecisol-c05-operational-api.log 2>&1 &
API_PID=$!
for _ in $(seq 1 60); do
  curl -sS -o /dev/null http://127.0.0.1:"$api_port"/no-route -H 'Host: tenant-a.example.test' 2>/dev/null && break
  sleep 1
done
response_a="$(curl -sS -i -X POST "http://127.0.0.1:$api_port/api/v1/identity/registration-intents" \
  -H 'Host: tenant-a.example.test' -H 'Content-Type: application/json' \
  --data-binary '{"mailbox":"person.one@example.test"}')"
response_b="$(curl -sS -i -X POST "http://127.0.0.1:$api_port/api/v1/identity/registration-intents" \
  -H 'Host: tenant-b.example.test' -H 'Content-Type: application/json' \
  --data-binary '{"mailbox":"person.one@example.test"}')"
[[ "${response_a%%$'\r\n'*}" == *'202'* && "${response_b%%$'\r\n'*}" == *'202'* ]]
same 'two initial tenant-scoped intents' 2 "$(root_sql 'SELECT COUNT(*) FROM c05_identity_registration_intent')"
same 'intent isolation A/B' 2 "$(root_sql 'SELECT COUNT(DISTINCT tenant_id) FROM c05_identity_registration_intent')"
same 'ciphertext persisted' 2 "$(root_sql 'SELECT COUNT(*) FROM c05_identity_registration_intent WHERE OCTET_LENGTH(mailbox_ciphertext)>0 AND mailbox_hmac<>mailbox_ciphertext')"

pending_response="$(curl -sS -i -X POST "http://127.0.0.1:$api_port/api/v1/identity/registration-intents" \
  -H 'Host: tenant-c.example.test' -H 'Content-Type: application/json' \
  --data-binary '{"mailbox":"person.two@example.test"}')"
[[ "${pending_response%%$'\r\n'*}" == *'202'* ]]
same 'unverified tenant creates no intent' 2 "$(root_sql 'SELECT COUNT(*) FROM c05_identity_registration_intent')"

for _ in $(seq 1 10); do
  curl -sS -o /dev/null -X POST "http://127.0.0.1:$api_port/api/v1/identity/registration-intents" \
    -H 'Host: tenant-a.example.test' -H 'Content-Type: application/json' \
    --data-binary '{"mailbox":"person.two@example.test"}'
done
same 'atomic quota cap' 8 "$(root_sql 'SELECT MAX(admitted_count) FROM c05_identity_registration_quota')"
same 'quota-limited intent count' 9 "$(root_sql 'SELECT COUNT(*) FROM c05_identity_registration_intent')"

forbidden="$(curl -sS -i -X POST "http://127.0.0.1:$api_port/api/v1/identity/registration-intents" \
  -H 'Host: tenant-a.example.test' -H 'X-Forwarded-Host: tenant-b.example.test' \
  -H 'Content-Type: application/json' --data-binary '{"mailbox":"other@example.test"}')"
[[ "${forbidden%%$'\r\n'*}" == *'400'* ]]
same 'forged forwarding header writes no intent' 9 "$(root_sql 'SELECT COUNT(*) FROM c05_identity_registration_intent')"

pids=()
for i in $(seq 1 24); do
  curl -sS -o /dev/null -X POST "http://127.0.0.1:$api_port/api/v1/identity/registration-intents" \
    -H 'Host: tenant-d.example.test' -H 'Content-Type: application/json' \
    --data-binary "{\"mailbox\":\"parallel-$i@example.test\"}" &
  pids+=("$!")
done
for pid in "${pids[@]}"; do wait "$pid"; done
same 'concurrent peer quota admits exactly eight' 8 "$(root_sql "SELECT admitted_count FROM c05_identity_registration_quota WHERE tenant_id=UNHEX('00000000000000000000000000000004')")"
same 'concurrent peer writes exactly eight intents' 8 "$(root_sql "SELECT COUNT(*) FROM c05_identity_registration_intent WHERE tenant_id=UNHEX('00000000000000000000000000000004')")"

echo 'PASS C05 product API registration-intent .NET10 HTTP + MySQL8.4: verified host, A/B, pending-host denial, neutral receipt, encrypted mailbox, atomic quota, forged forwarding denial'


