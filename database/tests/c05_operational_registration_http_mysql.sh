#!/usr/bin/env bash
set -euo pipefail
trap 'status=$?; echo "FAIL C05 operational registration HTTP test at line $LINENO (exit $status)" >&2; exit "$status"' ERR

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
IMAGE='mysql:8.4@sha256:a81d043930e51745522bd5c430961cf8286f0a23f0a9a2a6e0108f7ea71b0902'
NAME="crecisol-c05-operational-http-${RANDOM}-$$"
ROOT_PASSWORD="$(openssl rand -hex 24)"
APP_PASSWORD="$(openssl rand -hex 24)"
MAILBOX_KEY="$(openssl rand -hex 32)"
PEER_KEY="$(openssl rand -hex 32)"
ENCRYPTION_KEY="$(openssl rand -hex 32)"
OUTBOX_KEY="$(openssl rand -hex 32)"
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
app_sql() { docker exec -e "MYSQL_PWD=$APP_PASSWORD" "$NAME" mysql -h127.0.0.1 -P3306 -uc05_app -D crecisol_c05 -N -B -e "$1"; }
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
docker exec -i -e "MYSQL_PWD=$ROOT_PASSWORD" "$NAME" mysql -uroot -D crecisol_c05 \
  < "$ROOT/database/migrations/002_c05_registration_proof_outbox.sql"

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
GRANT INSERT ON crecisol_c05.c05_identity_registration_intent TO 'c05_app'@'%';
GRANT EXECUTE ON PROCEDURE crecisol_c05.c05_operational_stage_mailbox_proof TO 'c05_app'@'%';
GRANT EXECUTE ON PROCEDURE crecisol_c05.c05_operational_consume_mailbox_proof TO 'c05_app'@'%';"

dsn="Server=127.0.0.1;Port=$port;Database=crecisol_c05;User ID=c05_app;Password=$APP_PASSWORD;SslMode=None;"
cd "$ROOT"
dotnet build backend/src/Crecisol.Api/Crecisol.Api.csproj -c Release -warnaserror >/dev/null
api_port=5187
CRECISOL_C01_ALLOWED_HOSTS='127.0.0.1;tenant-a.example.test;tenant-b.example.test;tenant-c.example.test;tenant-d.example.test' \
CRECISOL_C05_REGISTRATION_ENABLED=true CRECISOL_C05_IDENTITY_DSN="$dsn" \
CRECISOL_C05_KEY_VERSION=1 CRECISOL_C05_MAILBOX_HMAC_KEY="$MAILBOX_KEY" \
CRECISOL_C05_PEER_HMAC_KEY="$PEER_KEY" CRECISOL_C05_MAILBOX_ENCRYPTION_KEY="$ENCRYPTION_KEY" \
CRECISOL_C05_PROOF_OUTBOX_ENCRYPTION_KEY="$OUTBOX_KEY" \
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
same 'operational proof/outbox atomically persisted' 2 "$(root_sql 'SELECT COUNT(*) FROM c05_identity_mailbox_proof p JOIN c05_identity_mailbox_outbox o USING (tenant_id,proof_id) WHERE p.proof_state=0 AND o.delivery_state=0 AND OCTET_LENGTH(o.payload_ciphertext)=32')"
if app_sql 'SELECT COUNT(*) FROM c05_identity_mailbox_outbox' >/dev/null 2>&1; then
  echo 'FAIL API DB role can read the mailbox proof outbox' >&2; exit 1
fi
if app_sql "INSERT INTO c05_identity_mailbox_outbox (tenant_id,proof_id,payload_ciphertext,payload_nonce,payload_tag,key_version,delivery_state,attempts,created_at_utc,expires_at_utc) VALUES (UNHEX(REPEAT('11',16)),UNHEX(REPEAT('22',16)),REPEAT(UNHEX('33'),32),REPEAT(UNHEX('44'),12),REPEAT(UNHEX('55'),16),1,0,0,UTC_TIMESTAMP(6),UTC_TIMESTAMP(6)+INTERVAL 1 MINUTE)" >/dev/null 2>&1; then
  echo 'FAIL API DB role can insert directly into the mailbox proof outbox' >&2; exit 1
fi

# Controlled local test sink: retrieve only the encrypted outbox row from the
# disposable root connection, decrypt in process memory, and submit as the
# mailbox owner. The product API never returns this code to its caller.
proof_payload="$(root_sql "SELECT CONCAT(HEX(o.tenant_id),'|',HEX(o.proof_id),'|',HEX(p.proof_token_hmac),'|',HEX(o.payload_nonce),'|',HEX(o.payload_tag),'|',HEX(o.payload_ciphertext),'|',o.key_version) FROM c05_identity_mailbox_outbox o JOIN c05_identity_mailbox_proof p USING (tenant_id,proof_id) WHERE o.tenant_id=UNHEX('00000000000000000000000000000001') LIMIT 1" \
  | OUTBOX_KEY="$OUTBOX_KEY" node -e '
const crypto = require("node:crypto");
let text = ""; process.stdin.setEncoding("utf8");
process.stdin.on("data", chunk => text += chunk);
process.stdin.on("end", () => {
  const [tenant, proof, digest, nonce, tag, ciphertext, versionText] = text.trim().split("|");
  const proofId = proof.toLowerCase();
  const key = Buffer.from(process.env.OUTBOX_KEY, "hex");
  const aad = Buffer.concat([
    Buffer.from("CRECISOL/C05/registration-proof-outbox/v1\0", "ascii"),
    Buffer.from(tenant, "hex"), Buffer.from(proofId, "hex"),
    (() => { const b=Buffer.alloc(4); b.writeInt32BE(Number(versionText)); return b; })(),
    Buffer.from(digest, "hex")
  ]);
  const decipher = crypto.createDecipheriv("aes-256-gcm", key, Buffer.from(nonce, "hex"));
  decipher.setAAD(aad); decipher.setAuthTag(Buffer.from(tag, "hex"));
  const token = Buffer.concat([decipher.update(Buffer.from(ciphertext, "hex")), decipher.final()]);
  const canonical = `${proofId.slice(0,8)}-${proofId.slice(8,12)}-${proofId.slice(12,16)}-${proofId.slice(16,20)}-${proofId.slice(20)}`;
  process.stdout.write(`${canonical}|${token.toString("hex")}`);
});')"
IFS='|' read -r proof_id proof_code <<< "$proof_payload"
[[ "$proof_id" =~ ^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$ && "$proof_code" =~ ^[0-9a-f]{64}$ ]]
wrong_proof="$(curl -sS -i -X POST "http://127.0.0.1:$api_port/api/v1/identity/registration-proofs/confirm" \
  -H 'Host: tenant-a.example.test' -H 'Content-Type: application/json' \
  --data-binary "{\"proofId\":\"$proof_id\",\"code\":\"$(printf '%064d' 0)\"}")"
[[ "${wrong_proof%%$'\r\n'*}" == *'202'* ]]
same 'wrong mailbox proof attempts tracked without enumeration' 1 "$(root_sql "SELECT failed_attempts FROM c05_identity_mailbox_proof WHERE tenant_id=UNHEX('00000000000000000000000000000001') AND proof_id=UNHEX(REPLACE('$proof_id','-',''))")"
curl -sS -o /dev/null -X POST "http://127.0.0.1:$api_port/api/v1/identity/registration-proofs/confirm" \
  -H 'Host: tenant-b.example.test' -H 'Content-Type: application/json' \
  --data-binary "{\"proofId\":\"$proof_id\",\"code\":\"$proof_code\"}"
same 'cross-tenant mailbox proof cannot consume tenant A proof' 0 "$(root_sql "SELECT proof_state FROM c05_identity_mailbox_proof WHERE tenant_id=UNHEX('00000000000000000000000000000001') AND proof_id=UNHEX(REPLACE('$proof_id','-',''))")"
valid_proof="$(curl -sS -i -X POST "http://127.0.0.1:$api_port/api/v1/identity/registration-proofs/confirm" \
  -H 'Host: tenant-a.example.test' -H 'Content-Type: application/json' \
  --data-binary "{\"proofId\":\"$proof_id\",\"code\":\"$proof_code\"}")"
[[ "${valid_proof%%$'\r\n'*}" == *'202'* ]]
same 'valid mailbox proof consumed once' 1 "$(root_sql "SELECT proof_state FROM c05_identity_mailbox_proof WHERE tenant_id=UNHEX('00000000000000000000000000000001') AND proof_id=UNHEX(REPLACE('$proof_id','-',''))")"
same 'successful proof revokes pending outbox' 3 "$(root_sql "SELECT delivery_state FROM c05_identity_mailbox_outbox WHERE tenant_id=UNHEX('00000000000000000000000000000001') AND proof_id=UNHEX(REPLACE('$proof_id','-',''))")"
curl -sS -o /dev/null -X POST "http://127.0.0.1:$api_port/api/v1/identity/registration-proofs/confirm" \
  -H 'Host: tenant-a.example.test' -H 'Content-Type: application/json' \
  --data-binary "{\"proofId\":\"$proof_id\",\"code\":\"$proof_code\"}"
same 'replay does not change consumed mailbox proof' 1 "$(root_sql "SELECT proof_state FROM c05_identity_mailbox_proof WHERE tenant_id=UNHEX('00000000000000000000000000000001') AND proof_id=UNHEX(REPLACE('$proof_id','-',''))")"

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
same 'quota-intents equal admitted reservations across time windows' \
  "$(root_sql "SELECT COALESCE(SUM(admitted_count),0) FROM c05_identity_registration_quota WHERE tenant_id=UNHEX('00000000000000000000000000000001')")" \
  "$(root_sql "SELECT COUNT(*) FROM c05_identity_registration_intent WHERE tenant_id=UNHEX('00000000000000000000000000000001')")"

intent_count_before_forward="$(root_sql 'SELECT COUNT(*) FROM c05_identity_registration_intent')"
forbidden="$(curl -sS -i -X POST "http://127.0.0.1:$api_port/api/v1/identity/registration-intents" \
  -H 'Host: tenant-a.example.test' -H 'X-Forwarded-Host: tenant-b.example.test' \
  -H 'Content-Type: application/json' --data-binary '{"mailbox":"other@example.test"}')"
[[ "${forbidden%%$'\r\n'*}" == *'400'* ]]
same 'forged forwarding header writes no intent' \
  "$intent_count_before_forward" \
  "$(root_sql 'SELECT COUNT(*) FROM c05_identity_registration_intent')"

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

echo 'PASS C05 product API registration/proof .NET10 HTTP + MySQL8.4: verified host, A/B, pending-host denial, neutral receipts, local test-sink decrypt, wrong proof, cross-tenant denial, one-time consume/replay, encrypted outbox, direct DB read/write denied, atomic quota, forged forwarding denial'


