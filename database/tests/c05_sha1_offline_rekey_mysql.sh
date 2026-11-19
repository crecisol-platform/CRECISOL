#!/usr/bin/env bash
# End-to-end test of the restricted offline rekey against a disposable copy of
# the owner's C05 container NAMES on an isolated GitHub Actions runner.
# Never run this against a real local persistent C05 installation.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
NAME='crecisol-c05-mysql-persistent'
VOL='crecisol-c05-mysql-data'
IMAGE='mysql:8.4@sha256:a81d043930e51745522bd5c430961cf8286f0a23f0a9a2a6e0108f7ea71b0902'
DIR="$(mktemp -d)"
PASS="$(openssl rand -hex 32)"
TOKEN="$(openssl rand -hex 32)"
KEY="$(openssl rand -hex 32)"
# The destructor owns *only* resources created after the absence check.
# A pre-existing owner volume/container is NEVER removed, even on error.
OWNED=0
cleanup() {
 if [ "$OWNED" = '1' ]; then
   docker rm -f "$NAME" >/dev/null 2>&1 || true
   docker volume rm "$VOL" >/dev/null 2>&1 || true
 fi
 rm -rf "$DIR"
}
trap cleanup EXIT
if docker ps -a --format '{{.Names}}' | grep -Fxq "$NAME" ||
   docker volume ls --format '{{.Name}}' | grep -Fxq "$VOL"; then
  echo 'FAIL: disposable test refused to reuse existing C05 container or volume' >&2
  exit 1
fi
umask 077
printf '%s\n' "$PASS" > "$DIR/mysql-root.password"
docker volume create "$VOL" >/dev/null
OWNED=1
docker run -d --name "$NAME" -p 127.0.0.1:3310:3306 \
 -v "$VOL:/var/lib/mysql" -e "MYSQL_ROOT_PASSWORD=$PASS" \
 -e MYSQL_DATABASE=crecisol_c05 "$IMAGE" >/dev/null
db() { docker exec -i -e "MYSQL_PWD=$PASS" "$NAME" mysql -uroot -N -B -D crecisol_c05 -e "$1"; }
apply() { docker exec -i -e "MYSQL_PWD=$PASS" "$NAME" mysql -uroot -D crecisol_c05 < "$1"; }
ready=0
for _ in $(seq 1 120); do
 if db 'SELECT 1' >/dev/null 2>&1; then ready=1; break; fi
 sleep 1
done
test "$ready" -eq 1
names=(
 001_c05_identity_operational 002_c05_registration_proof_outbox
 003_c05_pending_account_completion 004_c05_local_proof_delivery
 005_c05_operational_login_directory 006_c05_operational_identity_flow
 007_c05_passkeys 008_c05_terms_invitations 009_c05_pairing
 010_c05_security_notices 011_c05_platform_recovery 012_c05_scoped_profile
 013_c05_abuse_signals 014_c05_operator_recovery 015_c05_session_devices
 016_c05_existing_identity_join
)
for item in "${names[@]}"; do apply "$ROOT/database/migrations/$item.sql"; done
export C05_TEST_SECRET_DIR="$DIR"
export C05_TEST_TOKEN_KEY="$TOKEN"
export C05_TEST_ENCRYPTION_KEY="$KEY"
node --input-type=module <<'JS'
import {createHmac} from 'node:crypto';
import {writeFileSync} from 'node:fs';
import path from 'node:path';
const dir=process.env.C05_TEST_SECRET_DIR;
const token=process.env.C05_TEST_TOKEN_KEY;
const key=process.env.C05_TEST_ENCRYPTION_KEY;
writeFileSync(path.join(dir,'runtime.env'),
  'CRECISOL_C05_TOKEN_HMAC_KEY='+token+'\n'+
  'CRECISOL_C05_FACTOR_ENCRYPTION_KEY='+key+'\n',{mode:0o600});
let sql=[],access='# CRECISOL synthetic offline SHA256 pre-cutover account data\n\n';
for(let i=1;i<=3;i++){
 const mailbox='identity.'+['one','two','three'][i-1]+'@example.test';
 const id=String(i).repeat(32);
 const login=createHmac('sha256',Buffer.from(token,'hex')).update('C05/platform-login:'+mailbox).digest('hex');
 const conflict=String(i+3).repeat(64);
 const oldSeed='A'.repeat(52);
 const oldEnvelope=String(i+5).repeat(120);
 sql.push("INSERT INTO c05_platform_operator(operator_id,login_hmac,conflict_login_hmac,"+
  "password_phc,protected_seed,active,security_revision,last_totp_step,protected_contact) "+
  "VALUES(UNHEX('"+id+"'),UNHEX('"+login+"'),UNHEX('"+conflict+"'),"+
  "'synthetic-no-login',UNHEX('"+oldEnvelope+"'),TRUE,1,123,UNHEX('ABCD'));");
 access+='Correo: '+mailbox+'\nContraseña: Synthetic-Local-Test-'+i+'!\n'+
    'Semilla TOTP: '+oldSeed+'\nCódigos de respaldo de un uso: synthetic-old\n\n';
}
writeFileSync(path.join(dir,'fixture.sql'),sql.join('\n'),{mode:0o600});
writeFileSync(path.join(dir,'access.md'),access,{mode:0o600});
JS
apply "$DIR/fixture.sql"
apply "$ROOT/database/migrations/017_c05_totp_sha1_exclusive.sql"
db "CREATE TABLE c05_schema_migrations (version INT UNSIGNED NOT NULL PRIMARY KEY,\
migration_name VARCHAR(160) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,\
sha256 CHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,\
applied_at_utc DATETIME(6) NOT NULL) ENGINE=InnoDB;"
db "INSERT INTO c05_schema_migrations VALUES(17,'017_c05_totp_sha1_exclusive',\
'$(sha256sum "$ROOT/database/migrations/017_c05_totp_sha1_exclusive.sql" | cut -d' ' -f1)',UTC_TIMESTAMP(6));"
test "$(db "SELECT COUNT(*) FROM c05_platform_operator WHERE recovery_pending=1 AND LENGTH(protected_seed)=0")" = 3
export CRECISOL_C05_PERSISTENT_SECRET_DIR="$DIR"
export CRECISOL_C05_LOCAL_SHA1_REKEY_APPROVED=YES
node "$ROOT/qa/c05_sha1_local_operator_rekey.mjs" --rekey-after-verified-017
test "$(db "SELECT COUNT(*) FROM c05_platform_operator WHERE recovery_pending=0 AND LENGTH(protected_seed)=48 AND security_revision=3")" = 3
test "$(db "SELECT COUNT(*) FROM c05_platform_mfa_backup WHERE consumed_at_utc IS NULL")" = 30
test ! -e "$DIR/access.md.sha1-pending"
node --input-type=module <<'JS'
import {readFileSync,statSync} from 'node:fs';
import path from 'node:path';
const f=path.join(process.env.C05_TEST_SECRET_DIR,'access.md');
const t=readFileSync(f,'utf8');
if ((statSync(f).mode & 0o077)!==0 ||
    (t.match(/Semilla TOTP: [A-Z2-7]{32}(?=\r?\n)/g)||[]).length!==3 ||
    (t.match(/Contraseña: Synthetic-Local-Test-/g)||[]).length!==3 ||
    (t.match(/Códigos de respaldo de un uso: ([A-F0-9]{32} \/ ){9}[A-F0-9]{32}/g)||[]).length!==3)
 throw new Error('Protected operator rekey material or original password persistence failed');
console.log('PASS C05 offline SHA1 operator rekey: three secret bundles protected, accounts/passwords retained');
JS
# Independently prove that rekeyed database envelopes decrypt to the exact
# private SHA1 seed of each operator, and that ten distinct backup hashes match.
# This catches AAD/key/operator mix-ups which mere row-count checks miss.
db "SELECT HEX(operator_id),HEX(protected_seed),HEX(login_hmac) FROM c05_platform_operator ORDER BY operator_id" > "$DIR/operator-envelopes.tsv"
db "SELECT HEX(operator_id),HEX(token_hmac) FROM c05_platform_mfa_backup WHERE consumed_at_utc IS NULL" > "$DIR/operator-backups.tsv"
test "$(db "SELECT COUNT(*) FROM c05_platform_operator WHERE password_phc='synthetic-no-login'")" = 3
node --input-type=module <<'JS'
import {createHmac,createDecipheriv} from 'node:crypto';
import {readFileSync} from 'node:fs';
import path from 'node:path';
const dir=process.env.C05_TEST_SECRET_DIR;
const source=readFileSync(path.join(dir,'access.md'),'utf8');
const key=Buffer.from(process.env.C05_TEST_ENCRYPTION_KEY,'hex');
const token=Buffer.from(process.env.C05_TEST_TOKEN_KEY,'hex');
const raw=readFileSync(path.join(dir,'operator-envelopes.tsv'),'utf8').trim();
const rows=raw.split(/\r?\n/).map(line=>line.split('\t'));
const backupRows=readFileSync(path.join(dir,'operator-backups.tsv'),'utf8')
  .trim().split(/\r?\n/).map(line=>line.split('\t'));
if(rows.length!==3||backupRows.length!==30||key.length!==32||token.length!==32)
  throw new Error('Unexpected offline rekey fixture state');
const abc='ABCDEFGHIJKLMNOPQRSTUVWXYZ234567';
function base32(bytes){
  let value=0,bits=0,output='';
  for(const byte of bytes){
    value=(value<<8)|byte;bits+=8;
    while(bits>=5){output+=abc[(value>>(bits-5))&31];bits-=5;}
  }
  if(bits>0)output+=abc[(value<<(5-bits))&31];
  return output;
}
for(let i=1;i<=3;i++){
  const id=String(i).repeat(32).toUpperCase();
  const mailbox='identity.'+['one','two','three'][i-1]+'@example.test';
  const row=rows.find(v=>v[0]===id);
  if(!row||row.length!==3)throw new Error('Missing original operator ID');
  const envelope=Buffer.from(row[1],'hex');
  if(envelope.length!==48)throw new Error('Invalid AES-GCM factor envelope');
  const decipher=createDecipheriv('aes-256-gcm',key,envelope.subarray(0,12),{authTagLength:16});
  decipher.setAAD(Buffer.from('C05/operational/v1/platform-factor:'+id));
  decipher.setAuthTag(envelope.subarray(12,28));
  const seed=Buffer.concat([decipher.update(envelope.subarray(28)),decipher.final()]);
  if(seed.length!==20)throw new Error('Not an exclusive SHA1 seed');
  const block=source.split('Correo: '+mailbox+'\n')[1];
  const recorded=/^Semilla TOTP: ([A-Z2-7]{32})$/m.exec(block||'')?.[1];
  if(!recorded||base32(seed)!==recorded)
    throw new Error('Offline operator encrypted factor does not match private enrolled seed');
  const expectedLogin=createHmac('sha256',token)
    .update('C05/platform-login:'+mailbox).digest('hex').toUpperCase();
  if(row[2]!==expectedLogin)throw new Error('Operator digest mismatched original account');
  const serialized=/^Códigos de respaldo de un uso: (.+)$/m.exec(block||'')?.[1]||'';
  const codes=serialized.split(' / ');
  if(codes.length!==10||new Set(codes).size!==10)
    throw new Error('Invalid fresh backup code set');
  const expected=new Set(codes.map(code=>createHmac('sha256',token)
    .update('C05/platform-backup:'+id+':'+code).digest('hex').toUpperCase()));
  const actual=backupRows.filter(v=>v[0]===id).map(v=>v[1]);
  if(actual.length!==10||new Set(actual).size!==10||actual.some(x=>!expected.has(x)))
    throw new Error('Stored offline operator backup digest does not match private codes');
  seed.fill(0);
}
console.log('PASS offline SHA1 crypto: 3 independent AES-GCM envelope decryptions and 30 backup digests');
JS
if node "$ROOT/qa/c05_sha1_local_operator_rekey.mjs" --rekey-after-verified-017 >/dev/null 2>&1; then
 echo 'FAIL offline rekey replay was unexpectedly allowed' >&2
 exit 1
fi
test "$(db "SELECT COUNT(*) FROM c05_platform_mfa_backup WHERE consumed_at_utc IS NULL")" = 30
echo 'PASS C05 SHA1 rekey on MySQL8.4: three operators, thirty unique backups, replay denied'
