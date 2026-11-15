#!/usr/bin/env bash
set -euo pipefail
trap 'status=$?; echo "FAIL C05 operational registration HTTP test at line $LINENO (exit $status)" >&2; if [[ -f /tmp/crecisol-c05-operational-api.log ]]; then tail -n 30 /tmp/crecisol-c05-operational-api.log >&2; fi; exit "$status"' ERR

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
IMAGE='mysql:8.4@sha256:a81d043930e51745522bd5c430961cf8286f0a23f0a9a2a6e0108f7ea71b0902'
NAME="crecisol-c05-operational-http-${RANDOM}-$$"
ROOT_PASSWORD="$(openssl rand -hex 24)"
APP_PASSWORD="$(openssl rand -hex 24)"
MAILBOX_KEY="$(openssl rand -hex 32)"
PEER_KEY="$(openssl rand -hex 32)"
ENCRYPTION_KEY="$(openssl rand -hex 32)"
OUTBOX_KEY="$(openssl rand -hex 32)"
LOGIN_KEY="$(openssl rand -hex 32)"
API_PID=''
SMTP_PID=''
cleanup() {
  if [[ -n "$API_PID" ]]; then kill "$API_PID" >/dev/null 2>&1 || true; wait "$API_PID" 2>/dev/null || true; fi
  if [[ -n "$SMTP_PID" ]]; then kill "$SMTP_PID" >/dev/null 2>&1 || true; wait "$SMTP_PID" 2>/dev/null || true; fi
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
docker exec -i -e "MYSQL_PWD=$ROOT_PASSWORD" "$NAME" mysql -uroot -D crecisol_c05 \
  < "$ROOT/database/migrations/003_c05_pending_account_completion.sql"
docker exec -i -e "MYSQL_PWD=$ROOT_PASSWORD" "$NAME" mysql -uroot -D crecisol_c05 \
  < "$ROOT/database/migrations/004_c05_local_proof_delivery.sql"

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
root_sql "GRANT EXECUTE ON PROCEDURE crecisol_c05.c05_operational_reserve_registration_completion TO 'c05_app'@'%'; GRANT EXECUTE ON PROCEDURE crecisol_c05.c05_operational_complete_pending_registration TO 'c05_app'@'%';"
root_sql "GRANT EXECUTE ON PROCEDURE crecisol_c05.c05_operational_claim_mailbox_proof_outbox TO 'c05_app'@'%'; GRANT EXECUTE ON PROCEDURE crecisol_c05.c05_operational_complete_mailbox_proof_delivery TO 'c05_app'@'%';"

dsn="Server=127.0.0.1;Port=$port;Database=crecisol_c05;User ID=c05_app;Password=$APP_PASSWORD;SslMode=None;"
cd "$ROOT"
dotnet build backend/src/Crecisol.Api/Crecisol.Api.csproj -c Release -warnaserror >/dev/null
dotnet build backend/tests/Crecisol.LoopbackSmtpReceiver/Crecisol.LoopbackSmtpReceiver.csproj -c Release -warnaserror >/dev/null
api_port=5187
CRECISOL_C01_ALLOWED_HOSTS='127.0.0.1;tenant-a.example.test;tenant-b.example.test;tenant-c.example.test;tenant-d.example.test' \
CRECISOL_C05_REGISTRATION_ENABLED=true CRECISOL_C05_IDENTITY_DSN="$dsn" \
CRECISOL_C05_KEY_VERSION=1 CRECISOL_C05_MAILBOX_HMAC_KEY="$MAILBOX_KEY" \
CRECISOL_C05_PEER_HMAC_KEY="$PEER_KEY" CRECISOL_C05_MAILBOX_ENCRYPTION_KEY="$ENCRYPTION_KEY" \
CRECISOL_C05_PROOF_OUTBOX_ENCRYPTION_KEY="$OUTBOX_KEY" \
CRECISOL_C05_LOGIN_LOOKUP_HMAC_KEY="$LOGIN_KEY" CRECISOL_C05_LOCAL_ACCOUNT_COMPLETION_ENABLED=false \
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

locked_proof_hex="$(root_sql "SELECT HEX(proof_id) FROM c05_identity_mailbox_outbox WHERE tenant_id=UNHEX('00000000000000000000000000000002') LIMIT 1")"
locked_proof_id="$(printf '%s' "$locked_proof_hex" | node -e 'let s="";process.stdin.on("data",x=>s+=x).on("end",()=>{s=s.trim().toLowerCase();process.stdout.write(`${s.slice(0,8)}-${s.slice(8,12)}-${s.slice(12,16)}-${s.slice(16,20)}-${s.slice(20)}`)})')"
for _ in $(seq 1 5); do
  curl -sS -o /dev/null -X POST "http://127.0.0.1:$api_port/api/v1/identity/registration-proofs/confirm" \
    -H 'Host: tenant-b.example.test' -H 'Content-Type: application/json' \
    --data-binary "{\"proofId\":\"$locked_proof_id\",\"code\":\"$(printf '%064d' 0)\"}"
done
same 'fifth invalid code revokes proof' 2 "$(root_sql "SELECT proof_state FROM c05_identity_mailbox_proof WHERE tenant_id=UNHEX('00000000000000000000000000000002') AND proof_id=UNHEX('$locked_proof_hex')")"
same 'fifth invalid code counter is exact' 5 "$(root_sql "SELECT failed_attempts FROM c05_identity_mailbox_proof WHERE tenant_id=UNHEX('00000000000000000000000000000002') AND proof_id=UNHEX('$locked_proof_hex')")"
same 'fifth invalid code revokes outbox' 3 "$(root_sql "SELECT delivery_state FROM c05_identity_mailbox_outbox WHERE tenant_id=UNHEX('00000000000000000000000000000002') AND proof_id=UNHEX('$locked_proof_hex')")"

curl -sS -o /dev/null -X POST "http://127.0.0.1:$api_port/api/v1/identity/registration-intents" \
  -H 'Host: tenant-b.example.test' -H 'Content-Type: application/json' \
  --data-binary '{"mailbox":"expired-proof@example.test"}'
expired_proof_hex="$(root_sql "SELECT HEX(proof_id) FROM c05_identity_mailbox_proof WHERE tenant_id=UNHEX('00000000000000000000000000000002') AND proof_id<>UNHEX('$locked_proof_hex') LIMIT 1")"
root_sql "UPDATE c05_identity_mailbox_proof SET created_at_utc=UTC_TIMESTAMP(6)-INTERVAL 11 MINUTE,expires_at_utc=UTC_TIMESTAMP(6)-INTERVAL 1 SECOND WHERE tenant_id=UNHEX('00000000000000000000000000000002') AND proof_id=UNHEX('$expired_proof_hex'); UPDATE c05_identity_mailbox_outbox SET created_at_utc=UTC_TIMESTAMP(6)-INTERVAL 11 MINUTE,expires_at_utc=UTC_TIMESTAMP(6)-INTERVAL 1 SECOND WHERE tenant_id=UNHEX('00000000000000000000000000000002') AND proof_id=UNHEX('$expired_proof_hex')"
expired_proof_id="$(printf '%s' "$expired_proof_hex" | node -e 'let s="";process.stdin.on("data",x=>s+=x).on("end",()=>{s=s.trim().toLowerCase();process.stdout.write(`${s.slice(0,8)}-${s.slice(8,12)}-${s.slice(12,16)}-${s.slice(16,20)}-${s.slice(20)}`)})')"
curl -sS -o /dev/null -X POST "http://127.0.0.1:$api_port/api/v1/identity/registration-proofs/confirm" \
  -H 'Host: tenant-b.example.test' -H 'Content-Type: application/json' \
  --data-binary "{\"proofId\":\"$expired_proof_id\",\"code\":\"$(printf '%064d' 0)\"}"
same 'expired code cannot be confirmed' 3 "$(root_sql "SELECT proof_state FROM c05_identity_mailbox_proof WHERE tenant_id=UNHEX('00000000000000000000000000000002') AND proof_id=UNHEX('$expired_proof_hex')")"
same 'expired code outbox revoked' 3 "$(root_sql "SELECT delivery_state FROM c05_identity_mailbox_outbox WHERE tenant_id=UNHEX('00000000000000000000000000000002') AND proof_id=UNHEX('$expired_proof_hex')")"

intent_count_before_pending_host="$(root_sql 'SELECT COUNT(*) FROM c05_identity_registration_intent')"
pending_response="$(curl -sS -i -X POST "http://127.0.0.1:$api_port/api/v1/identity/registration-intents" \
  -H 'Host: tenant-c.example.test' -H 'Content-Type: application/json' \
  --data-binary '{"mailbox":"person.two@example.test"}')"
[[ "${pending_response%%$'\r\n'*}" == *'202'* ]]
same 'unverified tenant creates no intent' "$intent_count_before_pending_host" "$(root_sql 'SELECT COUNT(*) FROM c05_identity_registration_intent')"

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

# Actual browser -> Angular same-origin proxy -> Kestrel -> disposable MySQL.
# A separate synthetic SMTP process receives the code over loopback TCP.
kill "$API_PID" >/dev/null 2>&1 || true
wait "$API_PID" 2>/dev/null || true
API_PID=''
coproc SMTP_RECEIVER { dotnet "$ROOT/backend/tests/Crecisol.LoopbackSmtpReceiver/bin/Release/net10.0/Crecisol.LoopbackSmtpReceiver.dll"; }
SMTP_PID=$SMTP_RECEIVER_PID
read -r -t 15 smtp_ready <&"${SMTP_RECEIVER[0]}"
[[ "$smtp_ready" =~ ^READY\ ([0-9]{4,5})$ ]]
smtp_port="${BASH_REMATCH[1]}"
CRECISOL_C01_ALLOWED_HOSTS='127.0.0.1;tenant-a.example.test;tenant-b.example.test;tenant-c.example.test;tenant-d.example.test' \
CRECISOL_C05_REGISTRATION_ENABLED=true CRECISOL_C05_IDENTITY_DSN="$dsn" \
CRECISOL_C05_KEY_VERSION=1 CRECISOL_C05_MAILBOX_HMAC_KEY="$MAILBOX_KEY" \
CRECISOL_C05_PEER_HMAC_KEY="$PEER_KEY" CRECISOL_C05_MAILBOX_ENCRYPTION_KEY="$ENCRYPTION_KEY" \
CRECISOL_C05_PROOF_OUTBOX_ENCRYPTION_KEY="$OUTBOX_KEY" \
CRECISOL_C05_LOGIN_LOOKUP_HMAC_KEY="$LOGIN_KEY" CRECISOL_C05_LOCAL_SMTP_PORT="$smtp_port" \
CRECISOL_C05_LOCAL_ACCOUNT_COMPLETION_ENABLED=true \
dotnet run --no-build -c Release --project backend/src/Crecisol.Api/Crecisol.Api.csproj \
  --urls "http://127.0.0.1:$api_port" >> /tmp/crecisol-c05-operational-api.log 2>&1 &
API_PID=$!
for _ in $(seq 1 60); do
  if curl -sS -o /dev/null "http://127.0.0.1:$api_port/no-route" -H 'Host: tenant-a.example.test' 2>/dev/null; then break; fi
  sleep 1
done
root_sql "DELETE FROM c05_identity_registration_quota WHERE tenant_id=UNHEX('00000000000000000000000000000001')"
curl -sS -o /dev/null -X POST "http://127.0.0.1:$api_port/api/v1/identity/registration-intents" \
  -H 'Host: tenant-a.example.test' -H 'Content-Type: application/json' \
  --data-binary '{"mailbox":"angular-e2e@example.test"}'
angular_mailbox_digest="$(MAILBOX_KEY="$MAILBOX_KEY" node -e 'const c=require("node:crypto");const t=Buffer.from("00000000000000000000000000000001","hex");const m=Buffer.from("angular-e2e@example.test","utf8");process.stdout.write(c.createHmac("sha256",Buffer.from(process.env.MAILBOX_KEY,"hex")).update(Buffer.concat([t,m])).digest("hex"))')"
angular_proof_id="$(root_sql "SELECT BIN_TO_UUID(p.proof_id,0) FROM c05_identity_mailbox_proof p JOIN c05_identity_registration_intent i ON i.tenant_id=p.tenant_id AND i.intent_id=p.proof_id WHERE p.tenant_id=UNHEX('00000000000000000000000000000001') AND p.mailbox_hmac=UNHEX('$angular_mailbox_digest') AND p.proof_state=0 AND i.state=0 ORDER BY p.created_at_utc DESC LIMIT 1")"
[[ "$angular_proof_id" =~ ^[0-9a-f-]{36}$ ]]
angular_proof_digest="$(root_sql "SELECT HEX(proof_token_hmac) FROM c05_identity_mailbox_proof WHERE tenant_id=UNHEX('00000000000000000000000000000001') AND proof_id=UUID_TO_BIN('$angular_proof_id',0)")"
angular_proof_code=''
for _ in $(seq 1 90); do
  if IFS=' ' read -r -t 1 marker recipient candidate <&"${SMTP_RECEIVER[0]}"; then
    if [[ "$marker" == MAIL && "$recipient" == angular-e2e@example.test && "$candidate" =~ ^[A-F0-9]{64}$ ]]; then
      angular_proof_code="${candidate,,}"
      break
    fi
  fi
done
[[ "$angular_proof_code" =~ ^[0-9a-f]{64}$ ]]
angular_digest_check="$(TENANT_HEX=00000000000000000000000000000001 PROOF_ID="$angular_proof_id" PROOF_CODE="$angular_proof_code" node -e 'const c=require("node:crypto");const id=process.env.PROOF_ID.replaceAll("-","");const raw=Buffer.concat([Buffer.from("CRECISOL/C05-02/mailbox-possession/v1/","ascii"),Buffer.from(process.env.TENANT_HEX,"hex"),Buffer.from(id,"hex"),Buffer.from(process.env.PROOF_CODE,"hex")]);process.stdout.write(c.createHash("sha256").update(raw).digest("hex"))')"
same 'loopback SMTP token matches the protected proof ledger' "${angular_proof_digest,,}" "${angular_digest_check,,}"
same 'Angular journey received a fresh unused proof' '0:0' "$(root_sql "SELECT CONCAT(p.proof_state,':',i.state) FROM c05_identity_mailbox_proof p JOIN c05_identity_registration_intent i ON i.tenant_id=p.tenant_id AND i.intent_id=p.proof_id WHERE p.tenant_id=UNHEX('00000000000000000000000000000001') AND p.proof_id=UNHEX(REPLACE('$angular_proof_id','-',''))")"
same 'SMTP worker completed outbox delivery' 2 "$(root_sql "SELECT delivery_state FROM c05_identity_mailbox_outbox WHERE tenant_id=UNHEX('00000000000000000000000000000001') AND proof_id=UNHEX(REPLACE('$angular_proof_id','-',''))")"
legacy_confirmation="$(curl -sS -i -X POST "http://127.0.0.1:$api_port/api/v1/identity/registration-proofs/confirm" \
  -H 'Host: tenant-a.example.test' -H 'Content-Type: application/json' \
  --data-binary "{\"proofId\":\"$angular_proof_id\",\"code\":\"$angular_proof_code\"}")"
[[ "${legacy_confirmation%%$'\r\n'*}" == *'404'* ]]
same 'proof-only endpoint disabled while pending account completion is enabled' '0:0' \
  "$(root_sql "SELECT CONCAT(p.proof_state,':',i.state) FROM c05_identity_mailbox_proof p JOIN c05_identity_registration_intent i ON i.tenant_id=p.tenant_id AND i.intent_id=p.proof_id WHERE p.tenant_id=UNHEX('00000000000000000000000000000001') AND p.proof_id=UNHEX(REPLACE('$angular_proof_id','-',''))")"
invalid_completion="$(curl -sS -i -X POST "http://127.0.0.1:$api_port/api/v1/identity/registration-completions" \
  -H 'Host: tenant-a.example.test' -H 'Content-Type: application/json' \
  --data-binary "{\"proofId\":\"$angular_proof_id\",\"code\":\"$(printf '%064d' 0)\",\"mailbox\":\"angular-e2e@example.test\",\"password\":\"Crecisol-local-test-strong-1!\"}")"
[[ "${invalid_completion%%$'\r\n'*}" == *'202'* ]]
same 'wrong completion code increments lockout without consuming proof' '0:1' \
  "$(root_sql "SELECT CONCAT(proof_state,':',failed_attempts) FROM c05_identity_mailbox_proof WHERE tenant_id=UNHEX('00000000000000000000000000000001') AND proof_id=UNHEX(REPLACE('$angular_proof_id','-',''))")"
cross_tenant_completion="$(curl -sS -i -X POST "http://127.0.0.1:$api_port/api/v1/identity/registration-completions" \
  -H 'Host: tenant-b.example.test' -H 'Content-Type: application/json' \
  --data-binary "{\"proofId\":\"$angular_proof_id\",\"code\":\"$angular_proof_code\",\"mailbox\":\"angular-e2e@example.test\",\"password\":\"Crecisol-local-test-strong-1!\"}")"
[[ "${cross_tenant_completion%%$'\r\n'*}" == *'202'* ]]
same 'cross-tenant completion cannot consume another tenant proof' '0:1' \
  "$(root_sql "SELECT CONCAT(proof_state,':',failed_attempts) FROM c05_identity_mailbox_proof WHERE tenant_id=UNHEX('00000000000000000000000000000001') AND proof_id=UNHEX(REPLACE('$angular_proof_id','-',''))")"
forged_completion="$(curl -sS -i -X POST "http://127.0.0.1:$api_port/api/v1/identity/registration-completions" \
  -H 'Host: tenant-a.example.test' -H 'X-Forwarded-Host: tenant-b.example.test' \
  -H 'Content-Type: application/json' \
  --data-binary "{\"proofId\":\"$angular_proof_id\",\"code\":\"$angular_proof_code\",\"mailbox\":\"angular-e2e@example.test\",\"password\":\"Crecisol-local-test-strong-1!\"}")"
[[ "${forged_completion%%$'\r\n'*}" == *'400'* ]]
same 'forged forwarding header cannot consume completion proof' '0:1' \
  "$(root_sql "SELECT CONCAT(proof_state,':',failed_attempts) FROM c05_identity_mailbox_proof WHERE tenant_id=UNHEX('00000000000000000000000000000001') AND proof_id=UNHEX(REPLACE('$angular_proof_id','-',''))")"
(cd "$ROOT/frontend" && C05_PROOF_ID="$angular_proof_id" C05_PROOF_CODE="$angular_proof_code" \
  npx playwright test --config=playwright.c05-operational.config.ts \
    tests/c05-registration-proof.spec.ts --grep 'real registration against the disposable API and MySQL')
angular_final_state="$(root_sql "SELECT CONCAT(p.proof_state,'|',i.state,'|',p.failed_attempts,'|',HEX(p.mailbox_hmac),'|',HEX(p.proof_token_hmac)) FROM c05_identity_mailbox_proof p JOIN c05_identity_registration_intent i ON i.tenant_id=p.tenant_id AND i.intent_id=p.proof_id WHERE p.tenant_id=UNHEX('00000000000000000000000000000001') AND p.proof_id=UNHEX(REPLACE('$angular_proof_id','-',''))")"
if [[ "${angular_final_state%%|*}" != 1 ]]; then
  echo "FAIL Angular completion internal database state=$angular_final_state" >&2
  tail -n 20 /tmp/crecisol-c05-operational-api.log >&2
  exit 1
fi
same 'completed registration creates exactly one unverified-state account' 1 "$(root_sql "SELECT COUNT(*) FROM c05_identity_account WHERE account_state=0 AND mailbox_verified_at_utc IS NOT NULL AND password_phc LIKE '\$argon2id\$v=19\$%'")"
same 'completed registration has no active membership or granted role' '0:0:PENDING' "$(root_sql "SELECT CONCAT(SUM(m.membership_state=1),':',SUM(m.role_code<>'PENDING'),':',MAX(m.role_code)) FROM c05_identity_membership m JOIN c05_identity_account a USING(account_id) WHERE a.account_state=0 AND a.mailbox_verified_at_utc IS NOT NULL")"
same 'completed registration issues no session' 0 "$(root_sql 'SELECT COUNT(*) FROM c05_identity_session')"

echo 'PASS C05 pending account path: Angular browser/proxy -> .NET10 API -> MySQL8.4; verified host, tenant isolation, synthetic mailbox proof, pending account/membership, no session, replay/lockout/expiry, least DB privilege and atomic quota'


