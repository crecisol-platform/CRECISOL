// CRECISOL C05-only offline SHA1 re-key of THREE synthetic platform operators.
// No public endpoint, no tenant grants, no user seed logging. Execution is
// explicit only AFTER manual C05-017 backup/restore and API shutdown.
// Existing password, operator ID and tenant records remain unchanged.
import { spawnSync } from 'node:child_process';
import { createHmac } from 'node:crypto';
import { existsSync, readFileSync, writeFileSync, renameSync, chmodSync, statSync } from 'node:fs';
import { connect } from 'node:net';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const dir = process.env.CRECISOL_C05_PERSISTENT_SECRET_DIR ||
  path.join(os.homedir(), '.crecisol', 'c05-local');
const container = 'crecisol-c05-mysql-persistent';
const accounts = ['identity.one@example.test', 'identity.two@example.test', 'identity.three@example.test'];
const envFile = path.join(dir, 'runtime.env');
const passFile = path.join(dir, 'mysql-root.password');
const accessFile = path.join(dir, 'access.md');
const pendingFile = path.join(dir, 'access.md.sha1-pending');

function execute(command, args, options = {}) {
  const r = spawnSync(command, args, {
    cwd:root,encoding:'utf8',windowsHide:true,maxBuffer:1024*1024,
    ...options
  });
  if (r.status !== 0) throw new Error(command+' returned nonzero status');
  return (r.stdout || '').trim();
}
function db(sql, password) {
  return execute('docker', [
    'exec','-i','-e','MYSQL_PWD',container,'mysql','-uroot','-N','-B','crecisol_c05'
  ], {input:sql,env:{...process.env,MYSQL_PWD:password}});
}
function envRead(file) {
  const env = {};
  for(const line of readFileSync(file,'utf8').split(/\r?\n/)) {
    if(!line || line.startsWith('#'))continue;
    const i=line.indexOf('=');
    if(i<1)throw new Error('Malformed private runtime environment');
    env[line.slice(0,i)]=line.slice(i+1);
  }
  return env;
}
function listening(port) {
  return new Promise(resolve=>{
    const socket=connect({host:'127.0.0.1',port},()=>{
      socket.destroy();resolve(true);
    });
    socket.setTimeout(750,()=>{socket.destroy();resolve(false)});
    socket.on('error',()=>resolve(false));
  });
}
function requireCheck(ok,reason) { if(!ok)throw new Error(reason); }
async function main(){
  requireCheck(process.argv.length===3 && process.argv[2]==='--rekey-after-verified-017',
    'Explicit offline rekey mode required');
  requireCheck(process.env.CRECISOL_C05_LOCAL_SHA1_REKEY_APPROVED==='YES',
    'Explicit local QA rekey approval required');
  for(const file of [envFile,passFile,accessFile])
    requireCheck(existsSync(file),'Private local C05 prerequisite missing');
  requireCheck(!existsSync(pendingFile),'Previous protected pending rekey material must be reconciled');
  requireCheck(!await listening(5187),'Stop the C05 API before offline operator rekey');
  const volumes=JSON.parse(execute('docker',['inspect','--format','{{json .Mounts}}',container]));
  requireCheck(volumes.some(x=>x.Type==='volume'&&
    x.Name==='crecisol-c05-mysql-data'&&x.Destination==='/var/lib/mysql'),
    'Refusing non-C05 persistent volume');
  const bindings=JSON.parse(execute('docker',['inspect','--format',
    '{{json .NetworkSettings.Ports}}',container]));
  requireCheck(bindings['3306/tcp']?.some(x=>
    x.HostIp==='127.0.0.1'&&x.HostPort==='3310'),
    'Refusing MySQL not bound to expected loopback port');
  const runtime=envRead(envFile);
  const key=runtime.CRECISOL_C05_TOKEN_HMAC_KEY;
  requireCheck(/^[0-9a-fA-F]{64}$/.test(key||''),
    'Missing C05 protected token key');
  const password=readFileSync(passFile,'utf8').trim();
  requireCheck(/^[0-9a-fA-F]{64}$/.test(password),'Invalid private C05 MySQL root credential');
  requireCheck(db("SELECT COUNT(*) FROM c05_schema_migrations WHERE version=17",password)==='1',
    'C05-017 not recorded in migration ledger');
  requireCheck(db("SELECT status FROM c05_totp_sha1_policy WHERE policy_id=1",password)==='1',
    'C05-017 SHA1 policy not committed');
  const original=readFileSync(accessFile,'utf8');
  const bundles=[];
  let nextAccess=original;
  execute('dotnet',['build','backend/tools/Crecisol.LocalIdentityProvisioner/Crecisol.LocalIdentityProvisioner.csproj',
    '-c','Release','--nologo','-warnaserror']);
  for(const mailbox of accounts){
    const hash=createHmac('sha256',Buffer.from(key,'hex'))
      .update('C05/platform-login:'+mailbox).digest('hex');
    const account=db("SELECT CONCAT(HEX(operator_id),'\\t',recovery_pending,'\\t',"+
      "LENGTH(protected_seed),'\\t',active) FROM c05_platform_operator "+
      "WHERE login_hmac=UNHEX('"+hash+"')",password);
    const parts=account.split(/\t/);
    requireCheck(parts.length===4 && /^[0-9A-F]{32}$/.test(parts[0]) &&
      parts[1]==='1' && parts[2]==='0' && parts[3]==='1',
      'Operator must be previously invalidated, active and uniquely identifiable');
    const material=execute('dotnet',['run','--no-build','-c','Release',
      '--project','backend/tools/Crecisol.LocalIdentityProvisioner/Crecisol.LocalIdentityProvisioner.csproj',
      '--','--isolated-existing-factor-bundle'],
      {input:JSON.stringify({Mailbox:mailbox,OperatorId:parts[0]}),
       env:{...process.env,...runtime}});
    const item=JSON.parse(material);
    requireCheck(item.id===parts[0]&&item.hash===hash.toUpperCase()&&
      /^[A-Z2-7]{32}$/.test(item.seed)&&
      /^[A-F0-9]{96}$/.test(item.envelope)&&
      item.backupHashes.length===10&&item.backups.length===10,
      'Offline encrypted bundle invalid');
    bundles.push(item);
    const escaped=mailbox.replaceAll('.','\\.');
    const re=new RegExp('(Correo: '+escaped+'\\r?\\nContraseña: [^\\r\\n]+\\r?\\nSemilla TOTP: )[A-Z2-7]+(\\r?\\nCódigos de respaldo de un uso: )[^\\r\\n]+');
    requireCheck(re.test(nextAccess),'Private access document has changed layout');
    nextAccess=nextAccess.replace(re,(_,a,b)=>a+item.seed+b+item.backups.join(' / '));
  }
  // Write future access information BEFORE changing database, so a failure
  // after commit cannot strand operators without newly generated MFA codes.
  writeFileSync(pendingFile,nextAccess,{mode:0o600,flag:'wx'});
  chmodSync(pendingFile,0o600);
  requireCheck((statSync(pendingFile).mode & 0o077)===0,
    'Unprotected temporary access file');
  const lines=['CREATE TEMPORARY TABLE c05_rekey_guard(v INT NOT NULL CHECK(v=1));',
    'START TRANSACTION;'];
  for(const item of bundles){
    lines.push("UPDATE c05_platform_operator SET protected_seed=UNHEX('"+
      item.envelope+"'),last_totp_step=NULL,recovery_pending=0,"+
      "security_revision=security_revision+1 WHERE operator_id=UNHEX('"+
      item.id+"') AND login_hmac=UNHEX('"+item.hash+
      "') AND recovery_pending=1 AND LENGTH(protected_seed)=0;");
    lines.push('INSERT INTO c05_rekey_guard VALUES (ROW_COUNT());');
    for(const h of item.backupHashes)
      lines.push("INSERT INTO c05_platform_mfa_backup VALUES(UNHEX('"+
        item.id+"'),UNHEX('"+h+"'),NULL);");
  }
  lines.push('COMMIT;');
  db(lines.join('\n'),password);
  requireCheck(db("SELECT COUNT(*) FROM c05_platform_operator WHERE "+
    "recovery_pending=0 AND LENGTH(protected_seed)=48",password)==='3',
    'Operator rekey transaction not fully committed');
  renameSync(pendingFile,accessFile);
  console.log('PASS C05 offline-only rekey: three synthetic operators; existing passwords/IDs unchanged');
  console.log('Open access.md locally to scan/enter each new SHA1 factor; never share it.');
}
main().catch(()=>{console.error('C05 local operator rekey halted; no secrets displayed. Check private pending file before retry.');process.exitCode=1;});
