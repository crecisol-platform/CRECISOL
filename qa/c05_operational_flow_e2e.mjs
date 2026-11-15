// Real local product composition: Chromium/Angular -> Kestrel -> MySQL 8.4.
// All credentials/proofs are generated in memory; SMTP receipts are private
// child-process pipes. No route mocking, external mail or real users.
import { spawn, spawnSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { createHash, createHmac, randomBytes } from 'node:crypto';
import { createInterface } from 'node:readline';
import { request as httpRequest } from 'node:http';
import assert from 'node:assert/strict';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const require = createRequire(path.join(root, 'frontend/package.json'));
const { chromium } = require('@playwright/test');
const AxeBuilder = require('@axe-core/playwright').default;
const name = `crecisol-c05-flow-${process.pid}`;
const secret = () => randomBytes(32).toString('hex');
const rootPassword = secret(), appPassword = secret();
const keys = Object.fromEntries(['MAILBOX_HMAC', 'PEER_HMAC', 'MAILBOX_ENCRYPTION',
  'PROOF_OUTBOX_ENCRYPTION', 'LOGIN_LOOKUP_HMAC', 'TOKEN_HMAC', 'FACTOR_ENCRYPTION']
  .map(k => [`CRECISOL_C05_${k}_KEY`, secret()]));
const children = [];
const mails = [];
const confidential = [rootPassword, appPassword, ...Object.values(keys)];
let apiLogs = '', uiLogs = '', browser, assertions = 0;
const A = '00000000000000000000000000000001';
const B = '00000000000000000000000000000002';
const branch = '10000000-0000-4000-8000-000000000001';
const otherBranch = '10000000-0000-4000-8000-000000000002';
const resource = '20000000-0000-4000-8000-000000000001';
const password = 'Local-C05-identity-strong-1!';
const newPassword = 'Local-C05-recovered-strong-2!';
confidential.push(password, newPassword);
const delay = ms => new Promise(resolve => setTimeout(resolve, ms));
function run(command, args, options = {}) {
  const result = spawnSync(command, args, { cwd: root, encoding: 'utf8', windowsHide: true, ...options });
  if (result.status !== 0) throw new Error(`Command failed: ${command} ${args[0]} (${result.status}) ${result.stderr?.slice(0, 300)}`);
  return result.stdout.trim();
}
function child(command, args, env = process.env, cwd = root) {
  const p = spawn(command, args, { cwd, env, windowsHide: true, stdio: ['ignore', 'pipe', 'pipe'] });
  children.push(p);
  return p;
}
function sql(text, app = false) {
  return run('docker', ['exec', '-i', '-e', `MYSQL_PWD=${app ? appPassword : rootPassword}`, name,
    'mysql', app ? '-uc05_flow' : '-uroot', '-N', '-B', 'crecisol_c05'], { input: text });
}
function check(condition, label) {
  assert.ok(condition, label); assertions++; console.log(`PASS ${label}`);
}
async function until(fn, label, timeout = 60000) {
  const end = Date.now() + timeout;
  while (Date.now() < end) { const value = await fn(); if (value) return value; await delay(250); }
  throw new Error(`Timed out: ${label}`);
}
async function api(operation, fields = {}, token, host = 'tenant-a.example.test', extra = {}) {
  const body = JSON.stringify(fields);
  return await new Promise((resolve, reject) => {
    const req = httpRequest({ hostname: '127.0.0.1', port: 5187, path: `/api/v1/identity/${operation}`, method: 'POST',
      headers: { Host: host, 'Content-Type': 'application/json', 'Content-Length': Buffer.byteLength(body),
        ...(token ? { Authorization: `Bearer ${token}` } : {}), ...extra } }, response => {
      let text = ''; response.setEncoding('utf8'); response.on('data', chunk => text += chunk);
      response.on('end', () => { let data; try { data = JSON.parse(text); } catch { data = null; } resolve({ status: response.statusCode, data }); });
    });
    req.on('error', reject); req.end(body);
  });
}
async function mailFor(mailbox) {
  return await until(() => { const index = mails.findIndex(m => m.mailbox === mailbox); return index < 0 ? null : mails.splice(index, 1)[0].token; }, 'local SMTP receipt');
}
const loginHmac = mailbox => createHmac('sha256', Buffer.from(keys.CRECISOL_C05_LOGIN_LOOKUP_HMAC_KEY, 'hex')).update(mailbox.toLowerCase()).digest('hex');
const accountId = mailbox => sql(`SELECT HEX(account_id) FROM c05_identity_account WHERE login_lookup_hmac=UNHEX('${loginHmac(mailbox)}')`);
const membershipId = mailbox => sql(`SELECT BIN_TO_UUID(membership_id,0) FROM c05_identity_membership WHERE account_id=UNHEX('${accountId(mailbox)}') AND tenant_id=UNHEX('${A}')`);
function totp(seed, previous = false) {
  const alphabet = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567';
  let bits = 0, value = 0; const out = [];
  for (const c of seed) { value = (value << 5) | alphabet.indexOf(c); bits += 5; if (bits >= 8) { out.push((value >>> (bits - 8)) & 255); bits -= 8; } }
  const counter = Buffer.alloc(8); counter.writeBigInt64BE(BigInt(Math.floor(Date.now() / 30000) - (previous ? 1 : 0)));
  const hash = createHmac('sha256', Buffer.from(out)).update(counter).digest();
  const offset = hash.at(-1) & 15;
  return String((hash.readUInt32BE(offset) & 0x7fffffff) % 1000000).padStart(6, '0');
}
async function nextCode(seed) {
  // No clock injection into the product. Use real RFC6238 time and wait only
  // when a fresh code is needed. Previous step is valid for initial enrollment.
  const code = totp(seed);
  if (used.get(seed) === code) await delay(30500 - Date.now() % 30000);
  const fresh = totp(seed); used.set(seed, fresh); confidential.push(fresh); return fresh;
}
const used = new Map();
async function register(mailbox, page) {
  check((await api('registration-intents', { mailbox })).status === 202, 'neutral registration receipt');
  const token = await mailFor(mailbox); confidential.push(token);
  const proof = sql(`SELECT BIN_TO_UUID(proof_id,0) FROM c05_identity_mailbox_proof WHERE tenant_id=UNHEX('${A}') AND proof_state=0 ORDER BY created_at_utc DESC LIMIT 1`);
  if (page) {
    await page.goto('http://127.0.0.1:4200/cuenta');
    await page.getByLabel('Identificador de confirmación', { exact: true }).fill(proof);
    await page.getByLabel('Código de confirmación', { exact: true }).fill(token);
    await page.getByLabel('Correo de la solicitud').fill(mailbox);
    await page.getByLabel('Crear contraseña').fill(password);
    const response = page.waitForResponse(r => r.url().endsWith('/registration-completions'));
    await page.getByRole('button', { name: 'Crear cuenta pendiente', exact: true }).click();
    check((await response).status() === 202, 'Angular -> registration completion API');
  } else {
    check((await api('registration-completions', { proofId: proof, code: token, mailbox, password })).status === 202, 'registration proof completion');
  }
  check(Boolean(accountId(mailbox)), 'Argon2id account persisted');
  check(sql(`SELECT CONCAT(account_state,':',membership_state) FROM c05_identity_account JOIN c05_identity_membership USING(account_id) WHERE account_id=UNHEX('${accountId(mailbox)}')`) === '0:0', 'registration never grants membership');
}
async function enrollLogin(mailbox) {
  const login = await api('login', { mailbox, password }); check(login.status === 200, 'contextual password login');
  confidential.push(login.data.challenge);
  const enrollment = await api('mfa/enroll', { challenge: login.data.challenge }); check(enrollment.status === 200, 'MFA enrollment uses password-bound challenge');
  const seed = enrollment.data.secret; confidential.push(seed);
  const code = totp(seed, true); confidential.push(code);
  const verified = await api('mfa/verify', { challenge: login.data.challenge, code });
  check(verified.status === 200, 'real TOTP verification issues session');
  confidential.push(verified.data.sessionToken);
  confidential.push(...verified.data.recoveryCodes);
  return { seed, token: verified.data.sessionToken, challenge: login.data.challenge, backups: [...verified.data.recoveryCodes] };
}
async function browserSession(page) {
  // Observe the bearer actually used by Angular, and the DOM codes shown once.
  // Chromium can evict a response body before CDP Network.getResponseBody;
  // request observation avoids that race without intercepting/mocking traffic.
  const request = page.waitForRequest(r => r.url().endsWith('/sessions') && r.method() === 'POST');
  const response = page.waitForResponse(r => r.url().endsWith('/sessions'));
  await page.getByRole('button', { name: 'Consultar sesiones', exact: true }).click();
  const token = (await request).headers()['authorization'].slice(7);
  check((await response).status() === 200, 'Angular validates its actual bearer against the persistent session endpoint');
  confidential.push(token); return token;
}

try {
  console.log('C05 isolated operational flow: starting');
  run('docker', ['run', '--rm', '-d', '--name', name, '-p', '127.0.0.1::3306', '-e', `MYSQL_ROOT_PASSWORD=${rootPassword}`,
    '-e', 'MYSQL_DATABASE=crecisol_c05', 'mysql:8.4@sha256:a81d043930e51745522bd5c430961cf8286f0a23f0a9a2a6e0108f7ea71b0902']);
  await until(() => {
    const p = spawnSync('docker', ['exec', '-e', `MYSQL_PWD=${rootPassword}`, name, 'mysql', '-uroot', '-Nse', 'SELECT 1'], { encoding: 'utf8', windowsHide: true });
    return p.status === 0 && run('docker', ['logs', name]).includes('MySQL init process done');
  }, 'MySQL 8.4 readiness', 120000);
  const port = run('docker', ['port', name, '3306/tcp']).split(':').at(-1);
  for (const migration of ['001_c05_identity_operational', '002_c05_registration_proof_outbox', '003_c05_pending_account_completion',
    '004_c05_local_proof_delivery', '005_c05_operational_login_directory', '006_c05_operational_identity_flow'])
    sql(readFileSync(path.join(root, 'database/migrations', migration + '.sql'), 'utf8'));
  check(sql('SELECT VERSION()').startsWith('8.4.'), 'six forward migrations on real MySQL 8.4');
  sql(`INSERT INTO c04_tenant_catalog VALUES
    (UNHEX('${A}'),'tenant-a','Tenant A',3,2,1,UTC_TIMESTAMP(6)),
    (UNHEX('${B}'),'tenant-b','Tenant B',3,2,1,UTC_TIMESTAMP(6));
    INSERT INTO c04_verified_tenant_host VALUES
    ('tenant-a.example.test',UNHEX('${A}'),2,2,1,UTC_TIMESTAMP(6),UTC_TIMESTAMP(6),UTC_TIMESTAMP(6)+INTERVAL 1 DAY),
    ('tenant-b.example.test',UNHEX('${B}'),2,2,1,UTC_TIMESTAMP(6),UTC_TIMESTAMP(6),UTC_TIMESTAMP(6)+INTERVAL 1 DAY);
    CREATE USER 'c05_flow'@'%' IDENTIFIED BY '${appPassword}';`);
  sql(readFileSync(path.join(root, 'database/operations/c05_local_runtime_role.sql'), 'utf8'));
  sql("GRANT 'c05_identity_local_runtime' TO 'c05_flow'@'%'; SET DEFAULT ROLE 'c05_identity_local_runtime' TO 'c05_flow'@'%';");
  const smtp = child('dotnet', ['backend/tests/Crecisol.LoopbackSmtpReceiver/bin/Release/net10.0/Crecisol.LoopbackSmtpReceiver.dll']);
  let smtpPort;
  createInterface({ input: smtp.stdout }).on('line', line => {
    if (line.startsWith('READY ')) smtpPort = line.slice(6).trim();
    const match = /^MAIL (\S+) ([A-F0-9]{64})$/.exec(line.trim());
    if (match) mails.push({ mailbox: match[1], token: match[2] });
  });
  await until(() => smtpPort, 'independent SMTP receiver');
  const env = { ...process.env, ...keys, CRECISOL_C01_ALLOWED_HOSTS: '127.0.0.1;tenant-a.example.test;tenant-b.example.test',
    CRECISOL_C05_REGISTRATION_ENABLED: 'true', CRECISOL_C05_LOCAL_ACCOUNT_COMPLETION_ENABLED: 'true', CRECISOL_C05_LOCAL_IDENTITY_ENABLED: 'true',
    CRECISOL_C05_KEY_VERSION: '1', CRECISOL_C05_LOCAL_SMTP_PORT: smtpPort,
    CRECISOL_C05_IDENTITY_DSN: `Server=127.0.0.1;Port=${port};Database=crecisol_c05;User ID=c05_flow;Password=${appPassword};SslMode=None;` };
  const startApi = () => {
    const p = child('dotnet', ['backend/src/Crecisol.Api/bin/Release/net10.0/Crecisol.Api.dll', '--urls', 'http://127.0.0.1:5187'], env);
    p.stdout.on('data', b => apiLogs += b); p.stderr.on('data', b => apiLogs += b);
    return p;
  };
  let apiProcess = startApi();
  await until(async () => { try { return (await api('login', {})).status === 400; } catch { return false; } }, 'Kestrel readiness');
  const ui = child(process.execPath, [path.join(root, 'frontend/node_modules/@angular/cli/bin/ng.js'), 'serve', '--host', '127.0.0.1', '--port', '4200',
    '--proxy-config', '../qa/c05_operational_identity_proxy.json'], { ...process.env, NG_CLI_ANALYTICS: 'false' }, path.join(root, 'frontend'));
  // Angular workspace must be the child cwd, without a shell/interpolated command.
  ui.stdout.on('data', b => uiLogs += b); ui.stderr.on('data', b => uiLogs += b);
  // ng requires workspace cwd: the runner below passes --project only if launched
  // from frontend. See child helper's explicit cwd override.
  await until(async () => { try { return (await fetch('http://127.0.0.1:4200/cuenta')).ok; } catch { return false; } }, 'Angular readiness', 180000);
  browser = await chromium.launch({ headless: true });
  const browserContext = await browser.newContext();
  const page = await browserContext.newPage();
  await register('approver@example.test', page);
  check((await api('login', { mailbox: 'approver@example.test', password })).status === 401, 'pending account cannot log in');
  // Privileged setup is offline, explicit, audited and one-time. It creates no
  // session or factor; subsequent approval is done by a real HTTP MFA session.
  const bootstrap = readFileSync(path.join(root, 'database/operations/c05_bootstrap_local_authority.sql'), 'utf8');
  apiProcess.kill();
  await until(() => apiProcess.exitCode !== null || apiProcess.signalCode !== null, 'offline bootstrap API stop');
  sql(`SET @bootstrap_account=UNHEX('${accountId('approver@example.test')}'); SET @bootstrap_tenant=UNHEX('${A}');\n${bootstrap}`);
  apiProcess = startApi();
  await until(async () => { try { return (await api('login', {})).status === 400; } catch { return false; } }, 'Kestrel restart');
  const approver = await enrollLogin('approver@example.test');
  await register('member@example.test', page);
  const memberId = membershipId('member@example.test');
  await page.getByRole('region', { name: 'Iniciar sesión', exact: true }).getByLabel('Correo electrónico', { exact: true }).fill('approver@example.test');
  await page.getByLabel('Contraseña', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Continuar', exact: true }).click();
  await page.getByLabel('Código de tu aplicación de autenticación').fill(approver.backups.pop());
  await page.getByRole('button', { name: 'Verificar', exact: true }).click();
  await page.getByText('Revisar membresía con permiso administrativo', { exact: true }).click();
  await page.getByLabel('Membresía pendiente', { exact: true }).fill(memberId);
  await page.getByLabel('Motivo', { exact: true }).fill('Synthetic local approval');
  await page.getByLabel('Código MFA del aprobador', { exact: true }).fill(approver.backups.pop());
  const approvalResponse = page.waitForResponse(r => r.url().endsWith('/memberships/decide'));
  await page.getByRole('button', { name: 'Registrar decisión', exact: true }).click();
  check((await approvalResponse).status() === 200, 'Angular real MFA approver activates pending membership through API');
  const selfDecision = await api('memberships/decide', { membershipId: membershipId('approver@example.test'), decision: 'approve', reason: 'Self denied', code: await nextCode(approver.seed) }, approver.token);
  check(selfDecision.status === 401, 'self approval denied');
  await register('approval.race@example.test');
  const approvalRaceCode = approver.backups.pop();
  const approvalRace = await Promise.all(Array.from({ length: 12 }, () => api('memberships/decide', {
    membershipId: membershipId('approval.race@example.test'), decision: 'approve', reason: 'Concurrent isolated approval', code: approvalRaceCode
  }, approver.token)));
  check(approvalRace.filter(r => r.status === 200).length === 1, 'twelve concurrent approvals have exactly one audited winner');
  // Angular login and enrollment traverse the real same-origin proxy.
  await page.goto('http://127.0.0.1:4200/cuenta');
  await page.getByRole('region', { name: 'Iniciar sesión', exact: true }).getByLabel('Correo electrónico', { exact: true }).fill('member@example.test');
  await page.getByLabel('Contraseña', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Continuar', exact: true }).click();
  await page.getByRole('button', { name: 'Configurar segundo factor' }).click();
  const seed = await page.getByLabel('Clave de configuración MFA').innerText(); confidential.push(seed);
  await page.getByLabel('Código de tu aplicación de autenticación').fill(totp(seed, true));
  const verified = page.waitForResponse(r => r.url().endsWith('/mfa/verify'));
  await page.getByRole('button', { name: 'Verificar', exact: true }).click();
  check((await verified).status() === 200, 'Angular MFA request accepted by the real backend');
  await page.getByRole('heading', { name: 'Sesión verificada' }).waitFor();
  const backupCodes = await page.getByLabel('Códigos de respaldo MFA').locator('code').allTextContents(); confidential.push(...backupCodes);
  check(backupCodes.length === 10, 'MFA enrollment returns ten one-use backup codes once');
  let token = await browserSession(page);
  check(Boolean(token), 'Angular password -> enrollment -> TOTP -> authenticated session');
  const axe = await new AxeBuilder({ page }).analyze();
  check(axe.violations.filter(v => ['critical', 'serious'].includes(v.impact)).length === 0, 'authenticated Angular accessibility has no serious/critical axe findings');
  check(await page.evaluate(() => localStorage.length === 0 && sessionStorage.length === 0), 'no bearer stored in browser storage');
  const actor = accountId('member@example.test');
  sql(`INSERT INTO c05_identity_branch VALUES(UNHEX('${A}'),UUID_TO_BIN('${branch}',0),TRUE),(UNHEX('${A}'),UUID_TO_BIN('${otherBranch}',0),TRUE);
    INSERT INTO c05_identity_private_resource VALUES(UNHEX('${A}'),UUID_TO_BIN('${branch}',0),UUID_TO_BIN('${resource}',0),UNHEX('${actor}'),1,'LOCAL-PRIVATE-REFERENCE');
    INSERT INTO c05_identity_resource_grant VALUES(UNHEX('${A}'),UUID_TO_BIN(UUID(),0),UNHEX('${actor}'),UUID_TO_BIN('${branch}',0),UUID_TO_BIN('${resource}',0),1,'RESOURCE_READ',UNHEX('${accountId('approver@example.test')}'),UTC_TIMESTAMP(6),UTC_TIMESTAMP(6)+INTERVAL 1 HOUR,NULL,1);`);
  await page.getByLabel('Sucursal del recurso').fill(branch); await page.getByLabel('Identificador del recurso').fill(resource);
  await page.getByRole('button', { name: 'Consultar recurso autorizado' }).click();
  await page.getByLabel('Referencia privada').waitFor();
  assert.equal(await page.getByLabel('Referencia privada').innerText(), 'LOCAL-PRIVATE-REFERENCE');
  check(true, 'Angular private resource authorized by tenant, branch and owner');
  check((await api('private/read', { branchId: branch, resourceId: resource }, token, 'tenant-b.example.test')).status === 401, 'tenant B cannot use session A');
  check((await api('private/read', { branchId: otherBranch, resourceId: resource }, token)).status === 401, 'cross-branch IDOR denied');
  check((await api('sessions', {}, token, 'evil.example.test')).status === 400, 'unallowlisted Host denied');
  for (const header of ['X-Forwarded-Host', 'X-Forwarded-For', 'Forwarded', 'X-Tenant-Id', 'X-Actor-Id', 'X-Role'])
    check([400, 401].includes((await api('sessions', {}, token, 'tenant-a.example.test', { [header]: 'forged' })).status), `${header} cannot grant context`);
  check((await api('sessions', {}, token, 'tenant-a.example.test', { Origin: 'https://attacker.invalid' })).status === 400, 'cross-origin CSRF request denied');
  check((await api('sessions', {}, token, 'tenant-a.example.test', { Cookie: 'session=forged' })).status === 400, 'ambient cookie/fixation denied');
  check((await api('sessions', {}, secret())).status === 401, 'forged bearer denied');
  check((await api('login', { mailbox: 'member@example.test', password, tenantId: B })).status === 400, 'browser tenant selector in JSON denied');
  sql(`UPDATE c05_identity_branch SET active=0 WHERE branch_id=UUID_TO_BIN('${branch}',0)`);
  check((await api('private/read', { branchId: branch, resourceId: resource }, token)).status === 401, 'inactive branch denied');
  sql(`UPDATE c05_identity_branch SET active=1 WHERE branch_id=UUID_TO_BIN('${branch}',0);
    UPDATE c05_identity_private_resource SET owner_account_id=UNHEX('${accountId('approver@example.test')}')`);
  check((await api('private/read', { branchId: branch, resourceId: resource }, token)).status === 401, 'owner-scoped grant cannot read another owner');
  sql(`UPDATE c05_identity_private_resource SET owner_account_id=UNHEX('${actor}');
    UPDATE c05_identity_resource_grant SET revoked_at_utc=UTC_TIMESTAMP(6) WHERE permission_code='RESOURCE_READ';`);
  const denied = await Promise.all(Array.from({ length: 12 }, () => api('private/read', { branchId: branch, resourceId: resource }, token)));
  check(denied.every(r => r.status === 401), '12 concurrent private reads deny after grant revocation');
  sql(`UPDATE c05_identity_account SET account_state=2 WHERE account_id=UNHEX('${actor}')`);
  check((await api('sessions', {}, token)).status === 401, 'suspended account invalidates existing session');
  sql(`UPDATE c05_identity_account SET account_state=1 WHERE account_id=UNHEX('${actor}')`);
  const rotations = await Promise.all(Array.from({ length: 8 }, () => api('sessions/rotate', {}, token)));
  check(rotations.filter(r => r.status === 200).length === 1, '8 concurrent rotations have one winner');
  const oldToken = token; token = rotations.find(r => r.status === 200).data.sessionToken; confidential.push(token);
  check((await api('sessions', {}, oldToken)).status === 401, 'rotated token cannot be replayed');
  check((await api('sessions', {}, token)).status === 200, 'rotation winner remains usable');
  // Re-login/MFA challenge negative cases use current live factor.
  const login = await api('login', { mailbox: 'member@example.test', password });
  check(login.status === 200, 'persistent login after MFA enrollment');
  check((await api('mfa/verify', { challenge: login.data.challenge, code: '000000' })).status === 401, 'invalid MFA denied');
  const mfaCode = await nextCode(seed);
  const mfaRace = await Promise.all(Array.from({ length: 6 }, () => api('mfa/verify', { challenge: login.data.challenge, code: mfaCode })));
  check(mfaRace.filter(r => r.status === 200).length === 1, 'concurrent TOTP/challenge consumption has one winner');
  check((await api('mfa/verify', { challenge: login.data.challenge, code: mfaCode })).status === 401, 'MFA replay denied');
  await page.getByRole('button', { name: 'Consultar sesiones' }).click();
  await page.getByRole('heading', { name: 'Iniciar sesión', exact: true }).waitFor();
  check(true, 'Angular clears revoked session on server validation');
  await page.getByLabel('Correo para recuperación').fill('member@example.test');
  const recoveryReceipt = page.waitForResponse(r => r.url().endsWith('/recovery/request'));
  await page.getByRole('button', { name: 'Solicitar recuperación', exact: true }).click();
  check((await recoveryReceipt).status() === 202, 'Angular neutral recovery request -> real API');
  const recovery = await mailFor('member@example.test'); confidential.push(recovery);
  check((await api('recovery/request', { mailbox: 'unknown@example.test' })).status === 202, 'unknown recovery returns same receipt');
  check((await api('recovery/complete', { token: recovery, code: '000000', newPassword })).status === 401, 'mailbox recovery cannot bypass MFA');
  const resetCode = backupCodes.pop();
  await page.getByLabel('Token de recuperación').fill(recovery);
  await page.getByLabel('Código MFA para recuperación').fill(resetCode);
  await page.getByLabel('Contraseña recuperada').fill(newPassword);
  const resetResponse = page.waitForResponse(r => r.url().endsWith('/recovery/complete'));
  await page.getByRole('button', { name: 'Restablecer contraseña', exact: true }).click();
  check((await resetResponse).status() === 200, 'Angular recovery proof + one-use MFA backup changes persistent password');
  check((await api('recovery/complete', { token: recovery, code: resetCode, newPassword })).status === 401, 'recovery token one-use replay denied');
  check((await api('sessions', {}, token)).status === 401, 'recovery revokes all previous sessions');
  check((await api('login', { mailbox: 'member@example.test', password })).status === 401, 'old password denied after recovery');
  check((await api('login', { mailbox: 'member@example.test', password: newPassword })).status === 200, 'new password works after recovery');
  async function uiLogin(mailbox, currentPassword) {
    const factorBucket = createHmac('sha256', Buffer.from(keys.CRECISOL_C05_TOKEN_HMAC_KEY, 'hex'))
      .update('C05/quota:mfa:' + accountId(mailbox)).digest('hex');
    const currentAttempts = Number(sql(`SELECT COALESCE(MAX(attempts),0) FROM c05_identity_attempt_quota WHERE tenant_id=UNHEX('${A}') AND bucket_hmac=UNHEX('${factorBucket}') AND window_utc=FLOOR(UNIX_TIMESTAMP()/60)`));
    if (currentAttempts >= 8) {
      console.log('INFO waiting for real MFA quota window after adversarial cohort');
      await delay(60500 - Date.now() % 60000);
    }
    await page.getByRole('region', { name: 'Iniciar sesión', exact: true }).getByLabel('Correo electrónico', { exact: true }).fill(mailbox);
    await page.getByLabel('Contraseña', { exact: true }).fill(currentPassword);
    await page.getByRole('button', { name: 'Continuar', exact: true }).click();
    await page.getByLabel('Código de tu aplicación de autenticación').fill(backupCodes.pop());
    const response = page.waitForResponse(r => r.url().endsWith('/mfa/verify'));
    await page.getByRole('button', { name: 'Verificar', exact: true }).click();
    const result = await response; check(result.status() === 200, 'Angular persistent login with one-use backup factor');
    await page.getByRole('heading', { name: 'Sesión verificada' }).waitFor();
    return await browserSession(page);
  }
  token = await uiLogin('member@example.test', newPassword);
  check((await api('mfa/verify', { challenge: (await api('login', { mailbox: 'member@example.test', password: newPassword })).data.challenge, code: resetCode })).status === 401, 'consumed MFA backup cannot be reused for login');
  await page.getByText('Cambiar correo', { exact: true }).click();
  await page.getByLabel('Nuevo correo', { exact: true }).fill('member.changed@example.test');
  await page.getByLabel('Código MFA para solicitar correo').fill(backupCodes.pop());
  const mailboxRequest = page.waitForResponse(r => r.url().endsWith('/mailbox/request'));
  await page.getByRole('button', { name: 'Solicitar cambio de correo', exact: true }).click();
  check((await mailboxRequest).status() === 200, 'Angular mailbox change requires authenticated fresh second factor');
  const mailboxProof = await mailFor('member.changed@example.test'); confidential.push(mailboxProof);
  await page.getByLabel('Token del nuevo correo').fill(mailboxProof);
  await page.getByLabel('Código MFA para confirmar correo').fill(backupCodes.pop());
  const mailboxConfirmation = page.waitForResponse(r => r.url().endsWith('/mailbox/complete'));
  await page.getByRole('button', { name: 'Confirmar cambio de correo', exact: true }).click();
  check((await mailboxConfirmation).status() === 200, 'Angular verifies new mailbox and revokes sessions');
  check((await api('sessions', {}, token)).status === 401, 'mailbox change revokes old session');
  check((await api('login', { mailbox: 'member@example.test', password: newPassword })).status === 401, 'old mailbox no longer authenticates');
  token = await uiLogin('member.changed@example.test', newPassword);
  await page.getByText('Cambiar contraseña', { exact: true }).click();
  await page.getByLabel('Contraseña actual', { exact: true }).fill(newPassword);
  await page.getByLabel('Nueva contraseña', { exact: true }).fill(password);
  await page.getByLabel('Código MFA para contraseña', { exact: true }).fill(backupCodes.pop());
  const changedPassword = page.waitForResponse(r => r.url().endsWith('/password/change'));
  await page.getByRole('button', { name: 'Actualizar contraseña', exact: true }).click();
  check((await changedPassword).status() === 200, 'Angular sensitive password change verifies current password and MFA');
  check((await api('sessions', {}, token)).status === 401, 'password change revokes prior session');
  token = await uiLogin('member.changed@example.test', password);
  const rotatedUi = page.waitForResponse(r => r.url().endsWith('/sessions/rotate'));
  await page.getByRole('button', { name: 'Rotar sesión', exact: true }).click();
  const rotationResponse = await rotatedUi;
  check(rotationResponse.status() === 200, 'Angular rotates persistent session');
  const logoutUi = page.waitForResponse(r => r.url().endsWith('/logout'));
  const logoutRequest = page.waitForRequest(r => r.url().endsWith('/logout'));
  await page.getByRole('button', { name: 'Cerrar sesión', exact: true }).click();
  const logoutToken = (await logoutRequest).headers()['authorization'].slice(7); confidential.push(logoutToken);
  check((await logoutUi).status() === 200, 'Angular logout revokes server session');
  check((await api('sessions', {}, logoutToken)).status === 401, 'logout session replay denied');
  token = await uiLogin('member.changed@example.test', password);
  await page.getByText('Reemplazar segundo factor', { exact: true }).click();
  await page.getByLabel('Contraseña para reemplazar MFA').fill(password);
  await page.getByLabel('Código actual o respaldo MFA').fill(backupCodes.pop());
  const factorReplacement = page.waitForResponse(r => r.url().endsWith('/factor/replace'));
  await page.getByRole('button', { name: 'Reemplazar factor MFA', exact: true }).click();
  check((await factorReplacement).status() === 200, 'Angular factor replacement requires password plus a current one-use factor');
  check((await api('sessions', {}, token)).status === 401, 'factor replacement revokes existing sessions');
  await page.getByRole('button', { name: 'Configurar segundo factor' }).click();
  const replacedSeed = await page.getByLabel('Clave de configuración MFA').innerText(); confidential.push(replacedSeed);
  await page.getByLabel('Código de tu aplicación de autenticación').fill(totp(replacedSeed, true));
  const reverified = page.waitForResponse(r => r.url().endsWith('/mfa/verify'));
  await page.getByRole('button', { name: 'Verificar', exact: true }).click();
  const replacedReceipt = await reverified;
  check(replacedReceipt.status() === 200, 'replacement TOTP enrollment issues a fresh MFA-bound session');
  await page.getByRole('heading', { name: 'Sesión verificada' }).waitFor();
  const replacementData = { sessionToken: await browserSession(page), recoveryCodes: await page.getByLabel('Códigos de respaldo MFA').locator('code').allTextContents() };
  confidential.push(...replacementData.recoveryCodes);
  check(sql(`SELECT COUNT(*) FROM c05_identity_mfa_factor WHERE account_id=UNHEX('${actor}') AND factor_state=1`) === '1', 'replacement leaves exactly one active TOTP factor');
  check(sql(`SELECT COUNT(*) FROM c05_identity_mfa_backup WHERE account_id=UNHEX('${actor}') AND consumed_at_utc IS NULL`) === '10', 'old backup codes invalidated when factor is replaced');
  // Expired and cross-tenant proofs/challenges are exercised dynamically.
  const expiredLogin = await api('login', { mailbox: 'member.changed@example.test', password });
  check(expiredLogin.status === 200, 'login prepares an expirable challenge');
  sql(`UPDATE c05_identity_challenge SET expires_at_utc=UTC_TIMESTAMP(6)-INTERVAL 1 SECOND WHERE account_id=UNHEX('${actor}') AND consumed_at_utc IS NULL`);
  check((await api('mfa/verify', { challenge: expiredLogin.data.challenge, code: replacementData.recoveryCodes[0] })).status === 401, 'expired MFA challenge denied');
  check((await api('mfa/verify', { challenge: expiredLogin.data.challenge, code: replacementData.recoveryCodes[0] }, undefined, 'tenant-b.example.test')).status === 401, 'challenge bound to tenant A cannot authorize B');
  check((await api('recovery/request', { mailbox: 'member.changed@example.test' })).status === 202, 'prepare expirable recovery proof');
  const expiredRecovery = await mailFor('member.changed@example.test'); confidential.push(expiredRecovery);
  check((await api('recovery/complete', { token: expiredRecovery, code: replacementData.recoveryCodes[1], newPassword }, undefined, 'tenant-b.example.test')).status === 401, 'recovery proof cannot cross tenants');
  sql(`UPDATE c05_identity_recovery SET created_at_utc=UTC_TIMESTAMP(6)-INTERVAL 20 MINUTE,expires_at_utc=UTC_TIMESTAMP(6)-INTERVAL 1 SECOND WHERE account_id=UNHEX('${actor}') AND consumed_at_utc IS NULL`);
  check((await api('recovery/complete', { token: expiredRecovery, code: replacementData.recoveryCodes[1], newPassword })).status === 401, 'expired recovery proof denied');
  sql(`UPDATE c05_identity_mfa_factor SET factor_state=2,revoked_at_utc=UTC_TIMESTAMP(6) WHERE account_id=UNHEX('${actor}') AND factor_state=1`);
  check((await api('sessions', {}, replacementData.sessionToken)).status === 401, 'factor revocation immediately denies an unexpired session');
  check((await api('login', { mailbox: 'member.changed@example.test', password })).status === 401, 'revoked MFA cannot downgrade account to password-only enrollment');
  // Least privileges are asserted dynamically, not by searching GRANT text.
  for (const statement of ["UPDATE c05_identity_resource_grant SET permission_code='ADMIN'", 'DELETE FROM c05_identity_audit',
    "UPDATE c05_identity_audit SET action_code='FORGED'", 'CREATE TABLE unauthorized(id INT)']) {
    let refused = false; try { sql(statement, true); } catch { refused = true; }
    check(refused, 'SQL API role denies unauthorized mutation');
  }
  // Database failure is fail-closed. Revoking SELECT does not produce a fallback session.
  sql("REVOKE SELECT ON crecisol_c05.c05_identity_account FROM 'c05_identity_local_runtime';");
  check((await api('login', { mailbox: 'approver@example.test', password })).status === 401, 'database permission failure denies login');
  sql("GRANT SELECT ON crecisol_c05.c05_identity_account TO 'c05_identity_local_runtime';");
  check((await api('login', { mailbox: 'approver@example.test', password })).status === 200, 'restored database permission restores eligible login');
  // Keep the concurrent cohort in one actual server minute, without changing
  // the clock or clearing quotas to manufacture an admission result.
  if (Date.now() % 60000 > 50000) await delay(60500 - Date.now() % 60000);
  const logins = await Promise.all(Array.from({ length: 12 }, () => api('login', { mailbox: 'approver@example.test', password })));
  check(logins.filter(r => r.status === 200).length <= 8 && logins.some(r => r.status === 401), 'concurrent login quota admits at most eight password challenges per account-minute');
  check(!confidential.filter(s => s.length >= 12).some(s => apiLogs.includes(s)), 'API logs contain no password, seed, token or key');
  check(!apiLogs.includes('member@example.test') && !apiLogs.includes('LOCAL-PRIVATE-REFERENCE'), 'API logs contain no mailbox or private resource');
  await browser.close(); browser = undefined;
  for (const p of children.reverse()) { p.kill(); }
  await delay(1500);
  // Reversible migrations are exercised on the actual populated schema.
  for (const migration of ['006_c05_operational_identity_flow', '005_c05_operational_login_directory', '004_c05_local_proof_delivery',
    '003_c05_pending_account_completion', '002_c05_registration_proof_outbox', '001_c05_identity_operational'])
    sql(readFileSync(path.join(root, 'database/migrations', migration + '.down.sql'), 'utf8'));
  check(sql('SHOW TABLES') === '', 'six reverse migrations leave no schema tables');
  console.log(`PASS C05 operational integrated assertions=${assertions}; A/B/C evidence is limited to executed cases; no production certification`);
} catch (error) {
  // Assertions identify the failed requirement without disclosing request bodies.
  let message = error.message;
  for (const value of confidential.filter(v => v.length >= 6).sort((a, b) => b.length - a.length))
    message = message.replaceAll(value, '[redacted]');
  console.error(`FAIL ${message}`);
  console.error(`State: assertions=${assertions}; API alive=${children[1]?.exitCode === null}; Angular tail=${uiLogs.slice(-400).replace(/\x1b\[[0-9;]*m/g, '')}`);
  process.exitCode = 1;
} finally {
  if (browser) await browser.close();
  for (const p of children) { if (p.exitCode === null) p.kill(); }
  spawnSync('docker', ['rm', '-f', name], { windowsHide: true, stdio: 'ignore' });
}
