// CRECISOL C05 local persistent supervisor.
//
// This tool is intentionally local-only. It creates an isolated MySQL 8.4
// container/volume, applies the product migrations once with checksums, keeps
// runtime keys outside the repository, and starts the existing API/frontend
// composition. It does not add authentication routes or production behavior.
import { spawn, spawnSync, execFileSync } from 'node:child_process';
import { createServer, request as httpRequest } from 'node:http';
import { connect } from 'node:net';
import { createHash, createHmac, randomBytes, timingSafeEqual } from 'node:crypto';
import { gzipSync, gunzipSync } from 'node:zlib';
import { mkdirSync, existsSync, readFileSync, writeFileSync, chmodSync, openSync, statSync, lstatSync } from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { assertC05ContainerIdentity, assertSafeMigrationLedger } from './c05_sha1_persistent_guards.mjs';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const args = new Set(process.argv.slice(2));
const mode = process.argv.slice(2).find(value => !value.startsWith('--')) ?? 'status';
const allowedModes = new Set(['init', 'provision-tenants', 'approve-member', 'smoke', 'start', 'status', 'backup', 'verify-backup', 'upgrade-session-policy', 'stop']);
if (args.has('--help') || !allowedModes.has(mode)) {
  console.log('Usage: node qa/c05_local_persistent.mjs <init|provision-tenants|approve-member|smoke|start|status|backup|verify-backup|upgrade-session-policy|stop>');
  process.exit(args.has('--help') ? 0 : 2);
}

const container = 'crecisol-c05-mysql-persistent';
const volume = 'crecisol-c05-mysql-data';
const image = 'mysql:8.4@sha256:a81d043930e51745522bd5c430961cf8286f0a23f0a9a2a6e0108f7ea71b0902';
const database = 'crecisol_c05';
const mysqlPort = 3310;
const apiPort = 5187;
const uiPort = 4201;
const mailboxPort = 8025;
const smtpSender = 'no-reply@crecisol.example.test';
const secretDir = process.env.CRECISOL_C05_PERSISTENT_SECRET_DIR || path.join(os.homedir(), '.crecisol', 'c05-local');
const rootPasswordFile = path.join(secretDir, 'mysql-root.password');
const runtimeEnvFile = path.join(secretDir, 'runtime.env');
const accessFile = path.join(secretDir, 'access.md');
const tenantAccountsFile = path.join(secretDir, 'tenant-accounts.json');
const tenantAccountsMarkdownFile = path.join(secretDir, 'tenant-accounts.md');
const rekeyPendingFile = path.join(secretDir, 'access.md.sha1-pending');
const mailboxPasswordFile = path.join(secretDir, 'mailbox.password');
const proxyFile = path.join(secretDir, 'proxy.json');
const backupDir = path.join(secretDir, 'backups');
const tenants = {
  a: '00000000000000000000000000000001',
  b: '00000000000000000000000000000002'
};
const migrationNames = [
  '001_c05_identity_operational', '002_c05_registration_proof_outbox',
  '003_c05_pending_account_completion', '004_c05_local_proof_delivery',
  '005_c05_operational_login_directory', '006_c05_operational_identity_flow',
  '007_c05_passkeys', '008_c05_terms_invitations', '009_c05_pairing',
  '010_c05_security_notices', '011_c05_platform_recovery',
  '012_c05_scoped_profile', '013_c05_abuse_signals',
  '014_c05_operator_recovery', '015_c05_session_devices',
  '016_c05_existing_identity_join',
  '017_c05_totp_sha1_exclusive', '018_c05_session_idle_policy'
];
const operatorMailboxes = [
  'identity.one@example.test', 'identity.two@example.test', 'identity.three@example.test'
];
const keyNames = [
  'MAILBOX_HMAC', 'PEER_HMAC', 'MAILBOX_ENCRYPTION', 'PROOF_OUTBOX_ENCRYPTION',
  'LOGIN_LOOKUP_HMAC', 'TOKEN_HMAC', 'FACTOR_ENCRYPTION'
];
const children = [];
let smtpChild;
let mailboxServer;
const receipts = [];

function secureWrite(file, content) {
  mkdirSync(path.dirname(file), { recursive: true, mode: 0o700 });
  const dir = lstatSync(path.dirname(file));
  if (!dir.isDirectory() || dir.isSymbolicLink() ||
      (process.platform !== 'win32' && (dir.mode & 0o077) !== 0))
    throw new Error('Unsafe private C05 directory permissions or symlink.');
  const existing = lstatSync(file, { throwIfNoEntry: false });
  if (existing && (!existing.isFile() || existing.isSymbolicLink()))
    throw new Error('Unsafe private C05 file target; refusing overwrite.');
  writeFileSync(file, content, { mode: 0o600 });
  if (process.platform !== 'win32') chmodSync(file, 0o600);
  else { try { chmodSync(file, 0o600); } catch { /* Windows ACL requires owner-side verification. */ } }
  const saved = lstatSync(file);
  if (!saved.isFile() || saved.isSymbolicLink() ||
      (process.platform !== 'win32' && (saved.mode & 0o077) !== 0))
    throw new Error('Private C05 file permissions cannot be assured.');
}

function randomHex(bytes = 32) { return randomBytes(bytes).toString('hex'); }

function sleep(ms) { return new Promise(resolve => setTimeout(resolve, ms)); }

async function waitUntil(fn, label, timeout = 60000) {
  const deadline = Date.now() + timeout;
  while (Date.now() < deadline) {
    if (await fn()) return;
    await sleep(250);
  }
  throw new Error(`Timed out waiting for ${label}.`);
}

function run(command, commandArgs, options = {}) {
  const result = spawnSync(command, commandArgs, {
    cwd: root, encoding: 'utf8', windowsHide: true, maxBuffer: 16 * 1024 * 1024, ...options
  });
  if (result.status !== 0) {
    throw new Error(`${command} ${commandArgs[0] ?? ''} failed with status ${result.status}`);
  }
  return (result.stdout ?? '').trim();
}

function hasContainer() {
  return run('docker', ['ps', '-a', '--filter', `name=^/${container}$`, '--format', '{{.Names}}']) === container;
}

function containerRunning() {
  return run('docker', ['ps', '--filter', `name=^/${container}$`, '--format', '{{.Names}}']) === container;
}

function loadEnvFile() {
  if (!existsSync(runtimeEnvFile)) return {};
  const result = {};
  for (const line of readFileSync(runtimeEnvFile, 'utf8').split(/\r?\n/)) {
    if (!line || line.startsWith('#')) continue;
    const index = line.indexOf('=');
    if (index > 0) result[line.slice(0, index)] = line.slice(index + 1);
  }
  return result;
}

function saveRuntimeEnv(env) {
  const ordered = [
    'CRECISOL_C01_ALLOWED_HOSTS', 'CRECISOL_C01_READONLY_DSN',
    'CRECISOL_C05_REGISTRATION_ENABLED', 'CRECISOL_C05_LOCAL_ACCOUNT_COMPLETION_ENABLED',
    'CRECISOL_C05_LOCAL_IDENTITY_ENABLED', 'CRECISOL_C05_KEY_VERSION',
    'CRECISOL_C05_IDENTITY_DSN', 'CRECISOL_C05_LOCAL_SMTP_PORT',
    ...keyNames.map(name => `CRECISOL_C05_${name}_KEY`)
  ];
  const lines = ordered.filter(key => env[key] !== undefined && env[key] !== '')
    .map(key => `${key}=${env[key]}`);
  secureWrite(runtimeEnvFile, `${lines.join('\n')}\n`);
}

function loadOrCreateSecret(file) {
  if (existsSync(file)) return readFileSync(file, 'utf8').trim();
  const value = randomHex(32);
  secureWrite(file, `${value}\n`);
  return value;
}

function mysqlSql(sql, user = 'root', password = readFileSync(rootPasswordFile, 'utf8').trim()) {
  return run('docker', [
    'exec', '-i', '-e', 'MYSQL_PWD', container, 'mysql', `-u${user}`, '-N', '-B', database
  ], { input: sql, env: { ...process.env, MYSQL_PWD: password } });
}

function sha256(file) {
  return createHash('sha256').update(readFileSync(file)).digest('hex');
}

function waitForMysql() {
  const deadline = Date.now() + 120000;
  while (Date.now() < deadline) {
    try {
      if (mysqlSql('SELECT 1;') === '1') return;
    } catch { /* MySQL is still initializing. */ }
    Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, 1000);
  }
  throw new Error('MySQL C05 did not become ready within 120 seconds.');
}

function ensureRuntime() {
  mkdirSync(secretDir, { recursive: true, mode: 0o700 });
  for (const file of backupSecretFiles) assertProtectedFile(file, false);
  const existing = loadEnvFile();
  const rootPassword = existsSync(rootPasswordFile)
    ? readFileSync(rootPasswordFile, 'utf8').trim() : randomHex(32);
  if (!existsSync(rootPasswordFile)) secureWrite(rootPasswordFile, `${rootPassword}\n`);
  const appPassword = existing.CRECISOL_C05_IDENTITY_DSN?.match(/Password=([^;]+)/)?.[1] || randomHex(32);
  const env = {
    ...existing,
    CRECISOL_C01_ALLOWED_HOSTS: existing.CRECISOL_C01_ALLOWED_HOSTS || '127.0.0.1;tenant-a.example.test;tenant-b.example.test;platform-admin.example.test',
    ...(process.env.CRECISOL_C01_READONLY_DSN ? { CRECISOL_C01_READONLY_DSN: process.env.CRECISOL_C01_READONLY_DSN } : {}),
    CRECISOL_C05_REGISTRATION_ENABLED: 'true',
    CRECISOL_C05_LOCAL_ACCOUNT_COMPLETION_ENABLED: 'true',
    CRECISOL_C05_LOCAL_IDENTITY_ENABLED: 'true',
    CRECISOL_C05_KEY_VERSION: '1',
    CRECISOL_C05_IDENTITY_DSN: `Server=127.0.0.1;Port=${mysqlPort};Database=${database};User ID=c05_local_runtime;Password=${appPassword};SslMode=None;Pooling=true;`,
    CRECISOL_C05_LOCAL_SMTP_PORT: String(mailboxPort),
  };
  for (const name of keyNames) env[`CRECISOL_C05_${name}_KEY`] ||= randomHex(32);
  saveRuntimeEnv(env);
  const mailboxPassword = loadOrCreateSecret(mailboxPasswordFile);
  return { env, rootPassword, appPassword, mailboxPassword };
}

function assertExistingC05Container() {
  const mounts = JSON.parse(run('docker', ['inspect', '--format', '{{json .Mounts}}', container]));
  // HostConfig persists across docker stop; NetworkSettings.Ports becomes null when stopped.
  const ports = JSON.parse(run('docker', ['inspect', '--format', '{{json .HostConfig.PortBindings}}', container]));
  const configuredImage = run('docker', ['inspect', '--format', '{{.Config.Image}}', container]);
  assertC05ContainerIdentity(mounts, ports, configuredImage, volume, rootPasswordFile, image);
}

function ensureContainer(rootPassword) {
  if (!hasContainer()) {
    run('docker', ['volume', 'create', volume]);
    run('docker', [
      'run', '-d', '--name', container, '--restart', 'unless-stopped',
      '-p', `127.0.0.1:${mysqlPort}:3306`,
      '--mount', `source=${volume},destination=/var/lib/mysql`,
      '--mount', `type=bind,source=${rootPasswordFile},destination=/run/secrets/mysql-root-password,readonly`,
      '-e', 'MYSQL_ROOT_PASSWORD_FILE=/run/secrets/mysql-root-password',
      '-e', `MYSQL_DATABASE=${database}`, image
    ]);
  } else {
    // Never start or query a preexisting name that points to another volume.
    assertExistingC05Container();
    if (!containerRunning()) run('docker', ['start', container]);
  }
  assertExistingC05Container();
  waitForMysql();
}

function applyMigrations() {
  // Inspect the schema BEFORE writing a ledger; a reused or interrupted DB
  // without one must never be interpreted as a fresh installation.
  const priorTableCount = Number(mysqlSql('SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE();'));
  const priorLedgerCount = Number(mysqlSql("SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE() AND table_name='c05_schema_migrations';"));
  if (!Number.isSafeInteger(priorTableCount) || !Number.isSafeInteger(priorLedgerCount) ||
      priorLedgerCount > 1 || (priorTableCount > 0 && priorLedgerCount !== 1))
    throw new Error('Existing C05 schema without a valid migration ledger; refusing any writes.');
  mysqlSql(`CREATE TABLE IF NOT EXISTS c05_schema_migrations (
    version INT UNSIGNED NOT NULL PRIMARY KEY,
    migration_name VARCHAR(160) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    sha256 CHAR(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    applied_at_utc DATETIME(6) NOT NULL
  ) ENGINE=InnoDB;`);
  const applied = new Map(mysqlSql('SELECT version,sha256 FROM c05_schema_migrations ORDER BY version;')
    .split(/\r?\n/).filter(Boolean).map(row => { const [version, hash] = row.split('\t'); return [Number(version), hash]; }));
  // Existing persistent SHA256 factors must NEVER be retired automatically by
  // 'init' or 'start': the operator offline rekey and verified backup must be
  // performed first in a separate explicit maintenance window.
  // Only a truly empty database may initialize the 001-017 chain here.
  // A partially migrated existing database (including 001-015 or interrupted
  // 017 without a ledger record) must NEVER reach the destructive 017 script.
  // A fully migrated database is checked against every recorded checksum below.
  assertSafeMigrationLedger(priorTableCount, applied, migrationNames.length);
  for (let index = 0; index < migrationNames.length; index++) {
    const version = index + 1;
    const name = migrationNames[index];
    const file = path.join(root, 'database', 'migrations', `${name}.sql`);
    const hash = sha256(file);
    if (applied.has(version)) {
      if (applied.get(version) !== hash) throw new Error(`Migration checksum mismatch for ${name}.`);
      continue;
    }
    mysqlSql(readFileSync(file, 'utf8'));
    mysqlSql(`INSERT INTO c05_schema_migrations(version,migration_name,sha256,applied_at_utc)
      VALUES(${version},'${name}','${hash}',UTC_TIMESTAMP(6));`);
  }
}

function seedCatalogAndRuntimeRole(appPassword) {
  const terms = 'Entorno ficticio de supervisión QA CRECISOL. Solo datos sintéticos; no constituye un contrato productivo.';
  const termsHash = createHash('sha256').update(terms).digest('hex');
  mysqlSql(`INSERT IGNORE INTO c04_tenant_catalog VALUES
    (UNHEX('${tenants.a}'),'tenant-a','Tenant A',3,2,1,UTC_TIMESTAMP(6)),
    (UNHEX('${tenants.b}'),'tenant-b','Tenant B',3,2,1,UTC_TIMESTAMP(6));
    INSERT IGNORE INTO c04_verified_tenant_host VALUES
    ('tenant-a.example.test',UNHEX('${tenants.a}'),2,2,1,UTC_TIMESTAMP(6),UTC_TIMESTAMP(6),UTC_TIMESTAMP(6)+INTERVAL 365 DAY),
    ('tenant-b.example.test',UNHEX('${tenants.b}'),2,2,1,UTC_TIMESTAMP(6),UTC_TIMESTAMP(6),UTC_TIMESTAMP(6)+INTERVAL 365 DAY);
    INSERT IGNORE INTO c05_identity_terms(tenant_id,terms_version,content_text,content_hash,published_at_utc,active)
    VALUES
    (UNHEX('${tenants.a}'),'local-v1','${terms}',UNHEX('${termsHash}'),UTC_TIMESTAMP(6),TRUE),
    (UNHEX('${tenants.b}'),'local-v1','${terms}',UNHEX('${termsHash}'),UTC_TIMESTAMP(6),TRUE);`);
  mysqlSql(`CREATE USER IF NOT EXISTS 'c05_local_runtime'@'%' IDENTIFIED BY '${appPassword}';`);
  mysqlSql(readFileSync(path.join(root, 'database', 'operations', 'c05_local_runtime_role.sql'), 'utf8'));
  mysqlSql("GRANT 'c05_identity_local_runtime' TO 'c05_local_runtime'@'%'; SET DEFAULT ROLE 'c05_identity_local_runtime' TO 'c05_local_runtime'@'%';");
}

function provisionOperators(env) {
  const count = Number(mysqlSql('SELECT COUNT(*) FROM c05_platform_operator;') || 0);
  if (count === operatorMailboxes.length) return [];
  if (count !== 0) throw new Error('Partial platform-operator state found; refusing to duplicate or repair it automatically.');
  const access = [];
  for (const mailbox of operatorMailboxes) {
    const password = `Crecisol-Local-${randomHex(18)}!`;
    const bundleText = run('dotnet', [
      'backend/tools/Crecisol.LocalIdentityProvisioner/bin/Release/net10.0/Crecisol.LocalIdentityProvisioner.dll',
      '--isolated-test-bundle'
    ], { input: JSON.stringify({ Mailbox: mailbox, Password: password }), env: { ...process.env, ...env } });
    const bundle = JSON.parse(bundleText);
    mysqlSql(`INSERT INTO c05_platform_operator(operator_id,login_hmac,conflict_login_hmac,password_phc,protected_seed,active,security_revision,last_totp_step,protected_contact,can_manage_session_policy)
      VALUES(UNHEX('${bundle.id}'),UNHEX('${bundle.hash}'),UNHEX('${bundle.conflict}'),'${bundle.phc}',UNHEX('${bundle.envelope}'),TRUE,1,NULL,UNHEX('${bundle.contact}'),${mailbox === operatorMailboxes[0] ? 'TRUE' : 'FALSE'});`);
    for (const hash of bundle.backupHashes) mysqlSql(`INSERT INTO c05_platform_mfa_backup VALUES(UNHEX('${bundle.id}'),UNHEX('${hash}'),NULL);`);
    access.push({ mailbox, password, seed: bundle.seed, backups: bundle.backups });
  }
  return access;
}

function writeAccess(operatorAccess, mailboxPassword) {
  if (existsSync(accessFile)) return;
  const operatorText = operatorAccess.map(operator => [
    `Correo: ${operator.mailbox}`,
    `Contraseña: ${operator.password}`,
    `Semilla TOTP: ${operator.seed}`,
    `Códigos de respaldo de un uso: ${operator.backups.join(' / ')}`
  ].join('\n')).join('\n\n');
  secureWrite(accessFile, `# CRECISOL C05 — acceso local privado\n\n` +
    `No copiar, versionar ni compartir este archivo. Todo es sintético y solo local.\n\n` +
    `## Operadores de identidad de plataforma\n\n${operatorText}\n\n` +
    `## Identidades tenant\n\n` +
    `Aprobador empresarial: approver@example.test\n` +
    `Usuario empresarial: member@example.test\n` +
    `Segundo tenant de aislamiento: tenant-b.example.test\n\n` +
    `## Buzón SMTP loopback\n\n` +
    `URL: http://127.0.0.1:${mailboxPort}/\n` +
    `Usuario: preview\nContraseña: ${mailboxPassword}\n\n` +
    `## URLs\n\n` +
    `Frontend C05: http://127.0.0.1:${uiPort}/cuenta\n` +
    `API C05: http://127.0.0.1:${apiPort}\n` +
    `MySQL C05: 127.0.0.1:${mysqlPort}/${database}\n`);
}

function loadTenantAccounts() {
  if (!existsSync(tenantAccountsFile)) return {};
  return JSON.parse(readFileSync(tenantAccountsFile, 'utf8'));
}

function saveTenantAccounts(accounts) {
  secureWrite(tenantAccountsFile, `${JSON.stringify(accounts, null, 2)}\n`);
}

function digestForMailbox(value, keyName, tenantHex = '') {
  const key = Buffer.from(currentEnv[keyName], 'hex');
  const input = tenantHex ? Buffer.concat([Buffer.from(tenantHex, 'hex'), Buffer.from(value)]) : Buffer.from(value);
  return createHmac('sha256', key).update(input).digest('hex');
}

let currentEnv = {};

function scopedCall(scope, operation, fields = {}, token, host) {
  const body = JSON.stringify(fields);
  return new Promise((resolve, reject) => {
    const req = httpRequest({ hostname: '127.0.0.1', port: apiPort,
      path: `/api/v1/${scope === 'platform' ? 'platform-identity' : 'identity'}/${operation}`, method: 'POST',
      headers: { Host: host, 'Content-Type': 'application/json', 'Content-Length': Buffer.byteLength(body),
        ...(token ? { Authorization: `Bearer ${token}` } : {}) } }, response => {
      let text = '';
      response.setEncoding('utf8'); response.on('data', chunk => text += chunk);
      response.on('end', () => { let data = null; try { data = JSON.parse(text); } catch { /* empty response */ } resolve({ status: response.statusCode, data }); });
    });
    req.setTimeout(30000, () => req.destroy(new Error('C05 local HTTP timeout')));
    req.on('error', reject); req.end(body);
  });
}

function apiCall(operation, fields = {}, token, host = 'tenant-a.example.test') {
  return scopedCall('identity', operation, fields, token, host);
}

function platformCall(operation, fields = {}, token) {
  return scopedCall('platform', operation, fields, token, 'platform-admin.example.test');
}

async function waitForReceipt(mailbox) {
  let receipt;
  await waitUntil(() => {
    const index = receipts.findIndex(item => item.mailbox === mailbox && item.kind === 'MAIL');
    if (index < 0) return false;
    receipt = receipts.splice(index, 1)[0]; return true;
  }, `SMTP receipt for ${mailbox}`);
  return receipt.token;
}

function totp(seed) {
  const alphabet = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567';
  let bits = 0, value = 0, bytes = [];
  for (const character of seed) { value = (value << 5) | alphabet.indexOf(character); bits += 5; if (bits >= 8) { bytes.push((value >>> (bits - 8)) & 255); bits -= 8; } }
  const counter = Buffer.alloc(8); counter.writeBigInt64BE(BigInt(Math.floor(Date.now() / 30000)));
  if (!/^[A-Z2-7]{32}$/.test(seed) || bytes.length !== 20)
    throw new Error('Old TOTP seed invalidated; local SHA1 re-enrollment is required.');
  const hmac = createHmac('sha1', Buffer.from(bytes)).update(counter).digest();
  const offset = hmac.at(-1) & 15;
  return String((hmac.readUInt32BE(offset) & 0x7fffffff) % 1000000).padStart(6, '0');
}

// C05 SHA1 post-cutover gate: do NOT start an API with a partially
// re-keyed operator realm. 'status', 'backup' and 'verify-backup' stay usable.
function requireSha1OperatorsReady() {
  if (mysqlSql('SELECT status FROM c05_totp_sha1_policy WHERE policy_id=1;') !== '1')
    throw new Error('C05-017 SHA1 policy must be active before opening the local API.');
  const counts = mysqlSql(`SELECT COUNT(*),SUM(active=1 AND recovery_pending=0
    AND LENGTH(protected_seed)=48) FROM c05_platform_operator;`);
  if (counts !== '3\t3')
    throw new Error('Three synthetic platform operators must complete authorized offline SHA1 rekey before any API start.');
  if (!existsSync(accessFile)) throw new Error('Private operator access document missing; refusing to start API.');
  const privateText = readFileSync(accessFile, 'utf8').replaceAll('\r\n', '\n');
  for (const mailbox of operatorMailboxes) {
    const blocks = privateText.split(`Correo: ${mailbox}\n`);
    if (blocks.length !== 2 || !/^Semilla TOTP: [A-Z2-7]{32}$/m.test(blocks[1].split('\n\n')[0]))
      throw new Error('Offline SHA1 operator key record missing or legacy; refusing to start API.');
  }
}

async function startProvisionApi(runtime) {
  requireSha1OperatorsReady();
  const smtpPort = await startSmtpReceiver();
  const apiEnv = { ...process.env, ...runtime.env, ASPNETCORE_URLS: `http://127.0.0.1:${apiPort}`, CRECISOL_C05_LOCAL_SMTP_PORT: String(smtpPort) };
  const api = spawnChild('dotnet', ['backend/src/Crecisol.Api/bin/Release/net10.0/Crecisol.Api.dll', '--urls', `http://127.0.0.1:${apiPort}`], apiEnv, 'api.log');
  await waitUntil(async () => {
    try { return (await apiCall('login', {})).status === 400; }
    catch { return false; }
  }, 'C05 API readiness');
  return api;
}

async function stopChildren() {
  for (const child of children) if (!child.killed) child.kill('SIGTERM');
  await sleep(500);
}

async function registerTenantAccount(mailbox, password, tenantHex, host, accounts) {
  const existingId = mysqlSql(`SELECT HEX(account_id) FROM c05_identity_account WHERE login_lookup_hmac=UNHEX('${digestForMailbox(mailbox, 'CRECISOL_C05_LOGIN_LOOKUP_HMAC_KEY')}') LIMIT 1;`);
  if (existingId) {
    if (!accounts[mailbox]) throw new Error(`Existing account ${mailbox} has no protected local credential record.`);
    return accounts[mailbox];
  }
  const intent = await apiCall('registration-intents', { mailbox }, undefined, host);
  if (intent.status !== 202) throw new Error(`Registration intent declined for ${mailbox}.`);
  const code = await waitForReceipt(mailbox);
  const proofId = mysqlSql(`SELECT BIN_TO_UUID(proof_id,0) FROM c05_identity_mailbox_proof WHERE tenant_id=UNHEX('${tenantHex}') AND mailbox_hmac=UNHEX('${digestForMailbox(mailbox, 'CRECISOL_C05_MAILBOX_HMAC_KEY', tenantHex)}') AND proof_state=0 ORDER BY created_at_utc DESC LIMIT 1;`);
  if (!proofId) throw new Error(`No pending mailbox proof found for ${mailbox}.`);
  const completion = await apiCall('registration-completions', { proofId, code, mailbox, password }, undefined, host);
  if (completion.status !== 202) throw new Error(`Registration completion declined for ${mailbox}.`);
  return { mailbox, password, tenant: tenantHex };
}

async function enrollTenantAccount(account, host) {
  if (account.seed) return account;
  const login = await apiCall('login', { mailbox: account.mailbox, password: account.password }, undefined, host);
  if (login.status !== 200) throw new Error(`Pending account login failed for ${account.mailbox}.`);
  const enrollment = await apiCall('mfa/enroll', { challenge: login.data.challenge }, undefined, host);
  if (enrollment.status !== 200) throw new Error(`MFA enrollment failed for ${account.mailbox}.`);
  const seed = enrollment.data.secret;
  const verified = await apiCall('mfa/verify', { challenge: login.data.challenge, code: totp(seed) }, undefined, host);
  if (verified.status !== 200) throw new Error(`MFA verification failed for ${account.mailbox}.`);
  return { ...account, seed, backups: verified.data.recoveryCodes, sessionToken: verified.data.sessionToken };
}

async function provisionTenants() {
  let apiBusy = false;
  try { run('bash', ['-lc', `ss -ltn | grep -q ':${apiPort} '`]); apiBusy = true; } catch { /* API is stopped, as required for offline bootstrap. */ }
  if (apiBusy) throw new Error('Stop the persistent C05 supervisor before provisioning tenant accounts.');
  init();
  currentEnv = loadEnvFile();
  const runtime = ensureRuntime();
  const accounts = loadTenantAccounts();
  const approverPassword = accounts['approver@example.test']?.password || `Crecisol-Tenant-${randomHex(18)}!`;
  const memberPassword = accounts['member@example.test']?.password || `Crecisol-Tenant-${randomHex(18)}!`;
  const memberBPassword = accounts['member-b@example.test']?.password || `Crecisol-Tenant-${randomHex(18)}!`;
  const api = await startProvisionApi(runtime);
  try {
    accounts['approver@example.test'] = await registerTenantAccount('approver@example.test', approverPassword, tenants.a, 'tenant-a.example.test', accounts);
    const state = mysqlSql(`SELECT account_state FROM c05_identity_account WHERE login_lookup_hmac=UNHEX('${digestForMailbox('approver@example.test', 'CRECISOL_C05_LOGIN_LOOKUP_HMAC_KEY')}') LIMIT 1;`);
    if (state === '0') {
      await stopChildren();
      const accountId = mysqlSql(`SELECT HEX(account_id) FROM c05_identity_account WHERE login_lookup_hmac=UNHEX('${digestForMailbox('approver@example.test', 'CRECISOL_C05_LOGIN_LOOKUP_HMAC_KEY')}') LIMIT 1;`);
      mysqlSql(`SET @bootstrap_account=UNHEX('${accountId}'); SET @bootstrap_tenant=UNHEX('${tenants.a}');\n${readFileSync(path.join(root, 'database', 'operations', 'c05_bootstrap_local_authority.sql'), 'utf8')}`);
      children.length = 0;
      await startProvisionApi(runtime);
    }
    accounts['approver@example.test'] = await enrollTenantAccount(accounts['approver@example.test'], 'tenant-a.example.test');
    const terms = await apiCall('terms', {}, accounts['approver@example.test'].sessionToken, 'tenant-a.example.test');
    if (terms.status === 200) await apiCall('terms/accept', { version: terms.data.version, hash: terms.data.hash, accepted: 'true' }, accounts['approver@example.test'].sessionToken, 'tenant-a.example.test');
    accounts['member@example.test'] = await registerTenantAccount('member@example.test', memberPassword, tenants.a, 'tenant-a.example.test', accounts);
    accounts['member-b@example.test'] = await registerTenantAccount('member-b@example.test', memberBPassword, tenants.b, 'tenant-b.example.test', accounts);
    saveTenantAccounts(accounts);
    secureWrite(path.join(secretDir, 'tenant-accounts.md'), `# C05 cuentas tenant locales\n\nAprobador: approver@example.test\nContraseña: ${accounts['approver@example.test'].password}\n\nUsuario tenant A: member@example.test\nContraseña: ${accounts['member@example.test'].password}\n\nUsuario pendiente tenant B: member-b@example.test\nContraseña: ${accounts['member-b@example.test'].password}\n\nLas cuentas son ficticias. Los secretos no pertenecen al repositorio.\n`);
    console.log('C05 tenant identities prepared: approver active, member A pending, member B pending.');
  } finally {
    await stopChildren();
    if (api && !api.killed) api.kill('SIGTERM');
  }
}

function operatorCredentials(mailbox) {
  const text = readFileSync(accessFile, 'utf8');
  const block = text.split('Correo: ').find(value => value.startsWith(mailbox));
  if (!block) throw new Error(`Missing local operator credentials for ${mailbox}.`);
  const password = /^Contraseña: (.+)$/m.exec(block)?.[1];
  const seed = /^Semilla TOTP: (.+)$/m.exec(block)?.[1];
  if (!password || !seed || !/^[A-Z2-7]{32}$/.test(seed))
    throw new Error('Offline SHA1 operator re-enrollment is required; do not reuse an old seed.');
  return { mailbox, password, seed };
}

async function smoke() {
  currentEnv = loadEnvFile();
  const operator = operatorCredentials('identity.one@example.test');
  await sleep(30500 - (Date.now() % 30000));
  const platformLogin = await platformCall('login', { mailbox: operator.mailbox, password: operator.password });
  if (platformLogin.status !== 200) throw new Error('Platform operator password login failed.');
  const platformMfa = await platformCall('mfa', { challenge: platformLogin.data.challenge, code: totp(operator.seed) });
  if (platformMfa.status !== 200) throw new Error('Platform operator MFA failed.');
  const platformSession = await platformCall('session', {}, platformMfa.data.sessionToken);
  if (platformSession.status !== 200) throw new Error('Platform session validation failed.');
  const tenants = loadTenantAccounts();
  const approver = tenants['approver@example.test'];
  if (!approver?.seed) throw new Error('Persistent approver credentials are incomplete.');
  await sleep(30500 - (Date.now() % 30000));
  const login = await apiCall('login', { mailbox: approver.mailbox, password: approver.password });
  if (login.status !== 200) throw new Error('Tenant approver password login failed.');
  const mfa = await apiCall('mfa/verify', { challenge: login.data.challenge, code: totp(approver.seed) });
  if (mfa.status !== 200) throw new Error('Tenant approver MFA failed.');
  const session = await apiCall('sessions', {}, mfa.data.sessionToken);
  if (session.status !== 200) throw new Error('Tenant session validation failed.');
  const crossTenant = await apiCall('private/read', { branchId: '10000000-0000-4000-8000-000000000001', resourceId: '20000000-0000-4000-8000-000000000001' }, mfa.data.sessionToken, 'tenant-b.example.test');
  if (crossTenant.status === 200) throw new Error('Cross-tenant private read was unexpectedly allowed.');
  console.log('PASS platform operator password + MFA + session');
  console.log('PASS tenant approver password + MFA + session');
  console.log(`PASS cross-tenant private read denied with HTTP ${crossTenant.status}`);
}

async function approveMember() {
  currentEnv = loadEnvFile();
  const accounts = loadTenantAccounts();
  const approver = accounts['approver@example.test'];
  const member = accounts['member@example.test'];
  if (!approver?.password || !approver?.seed || !member) throw new Error('Tenant account credentials are incomplete.');
  const membershipId = mysqlSql(`SELECT BIN_TO_UUID(m.membership_id,0) FROM c05_identity_membership m JOIN c05_identity_account a USING(account_id) WHERE a.login_lookup_hmac=UNHEX('${digestForMailbox(member.mailbox, 'CRECISOL_C05_LOGIN_LOOKUP_HMAC_KEY')}') AND m.tenant_id=UNHEX('${tenants.a}') LIMIT 1;`);
  const state = mysqlSql(`SELECT membership_state FROM c05_identity_membership WHERE membership_id=UUID_TO_BIN('${membershipId}',0);`);
  if (state === '1') { console.log('PASS member@example.test already active'); return; }
  await sleep(30500 - (Date.now() % 30000));
  const login = await apiCall('login', { mailbox: approver.mailbox, password: approver.password });
  if (login.status !== 200) throw new Error('Approver login failed.');
  const mfa = await apiCall('mfa/verify', { challenge: login.data.challenge, code: totp(approver.seed) });
  if (mfa.status !== 200) throw new Error('Approver MFA failed.');
  const terms = await apiCall('terms', {}, mfa.data.sessionToken);
  if (terms.status !== 200) throw new Error('Approver terms read failed.');
  const accepted = await apiCall('terms/accept', { version: terms.data.version, hash: terms.data.hash, accepted: 'true' }, mfa.data.sessionToken);
  if (accepted.status !== 200) throw new Error('Approver terms acceptance failed.');
  await sleep(30500 - (Date.now() % 30000));
  const approvalCode = totp(approver.seed);
  const decision = await apiCall('memberships/decide', { membershipId, decision: 'approve', reason: 'Persistent local QA supervision', code: approvalCode }, mfa.data.sessionToken);
  if (decision.status !== 200) throw new Error(`Membership approval failed with HTTP ${decision.status}.`);
  saveTenantAccounts(accounts);
  console.log('PASS member@example.test approved by tenant approver MFA');
}

function init() {
  const runtime = ensureRuntime();
  ensureContainer(runtime.rootPassword);
  applyMigrations();
  seedCatalogAndRuntimeRole(runtime.appPassword);
  const operators = provisionOperators(runtime.env);
  writeAccess(operators, runtime.mailboxPassword);
  console.log(`C05 persistent environment ready: ${database}, migrations=${migrationNames.length}, operators=${operatorMailboxes.length}`);
  console.log(`Secrets and access guide: ${accessFile}`);
}

function spawnChild(command, commandArgs, env, logName) {
  const logPath = path.join(secretDir, logName);
  const log = requireFsAppend(logPath);
  const child = spawn(command, commandArgs, { cwd: root, env, detached: false, stdio: ['ignore', log, log] });
  children.push(child);
  return child;
}

function requireFsAppend(file) {
  mkdirSync(path.dirname(file), { recursive: true, mode: 0o700 });
  const fd = openSync(file, 'a', 0o600);
  try { chmodSync(file, 0o600); } catch { /* Best effort on Windows-backed filesystems. */ }
  return fd;
}

function startSmtpReceiver() {
  return new Promise((resolve, reject) => {
    const receiver = spawn('dotnet', ['backend/tests/Crecisol.LoopbackSmtpReceiver/bin/Release/net10.0/Crecisol.LoopbackSmtpReceiver.dll'], {
      cwd: root, env: process.env, stdio: ['ignore', 'pipe', 'ignore']
    });
    smtpChild = receiver; children.push(receiver);
    let buffer = '';
    let resolved = false;
    const timer = setTimeout(() => {
      if (!resolved) { resolved = true; reject(new Error('Loopback SMTP receiver did not report a port.')); }
    }, 15000);
    receiver.once('error', error => { if (!resolved) { resolved = true; clearTimeout(timer); reject(error); } });
    receiver.once('exit', code => { if (!resolved) { resolved = true; clearTimeout(timer); reject(new Error(`SMTP receiver exited with ${code}.`)); } });
    receiver.stdout.on('data', chunk => {
      buffer += chunk.toString('utf8');
      const lines = buffer.split(/\r?\n/); buffer = lines.pop() ?? '';
      for (const line of lines) {
        const ready = /^READY (\d+)$/.exec(line.trim());
        if (ready && !resolved) { resolved = true; clearTimeout(timer); resolve(Number(ready[1])); }
        const mail = /^(MAIL|NOTICE|CAPABILITY) (\S+) ([A-F0-9]{64})(?: (\S+))?$/.exec(line.trim());
        if (mail) receipts.push({ kind: mail[1], mailbox: mail[2], token: mail[3], purpose: mail[4] ?? '' });
      }
    });
  });
}

function startMailbox(password) {
  const auth = `Basic ${Buffer.from(`preview:${password}`).toString('base64')}`;
  mailboxServer = createServer((request, response) => {
    response.setHeader('Cache-Control', 'no-store');
    response.setHeader('X-Content-Type-Options', 'nosniff');
    if (request.headers.authorization !== auth) {
      response.writeHead(401, { 'WWW-Authenticate': 'Basic realm="CRECISOL local mailbox"' });
      response.end(); return;
    }
    if (request.method !== 'GET' || request.url !== '/') { response.writeHead(404); response.end(); return; }
    const rows = receipts.map(item => `<tr><td>${item.kind}</td><td>${item.mailbox}</td><td><code>${item.token}</code></td><td>${item.purpose}</td></tr>`).join('');
    response.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8' });
    response.end(`<!doctype html><meta charset="utf-8"><title>CRECISOL buzón local</title><h1>Buzón SMTP local</h1><p>Solo datos sintéticos; los mensajes viven en memoria.</p><p><a href="http://127.0.0.1:${uiPort}/cuenta">Abrir C05</a></p><table border="1" cellpadding="6"><tr><th>Tipo</th><th>Destinatario</th><th>Código/token</th><th>Propósito</th></tr>${rows}</table>`);
  });
  mailboxServer.listen(mailboxPort, '127.0.0.1');
}

async function start() {
  init();
  requireSha1OperatorsReady();
  const runtime = ensureRuntime();
  const smtpPort = await startSmtpReceiver();
  startMailbox(runtime.mailboxPassword);
  const apiEnv = { ...process.env, ...runtime.env, ASPNETCORE_URLS: `http://127.0.0.1:${apiPort}`, CRECISOL_C05_LOCAL_SMTP_PORT: String(smtpPort) };
  const api = spawnChild('dotnet', ['backend/src/Crecisol.Api/bin/Release/net10.0/Crecisol.Api.dll', '--urls', `http://127.0.0.1:${apiPort}`], apiEnv, 'api.log');
  const proxy = {
    '/api/v1/platform-identity': { target: `http://127.0.0.1:${apiPort}`, secure: false, changeOrigin: true, headers: { Host: 'platform-admin.example.test' } },
    '/api': { target: `http://127.0.0.1:${apiPort}`, secure: false, changeOrigin: true, headers: { Host: 'tenant-a.example.test' } }
  };
  secureWrite(proxyFile, JSON.stringify(proxy, null, 2));
  const ui = spawnChild(process.execPath, ['frontend/node_modules/@angular/cli/bin/ng.js', 'serve', '--host', '127.0.0.1', '--port', String(uiPort), '--proxy-config', proxyFile], { ...process.env, NG_CLI_ANALYTICS: 'false' }, 'frontend.log');
  console.log(`C05 persistent services starting: frontend=http://127.0.0.1:${uiPort}/cuenta backend=http://127.0.0.1:${apiPort} mailbox=http://127.0.0.1:${mailboxPort}/`);
  console.log(`SMTP receiver port=${smtpPort}; Ctrl+C stops API/frontend/SMTP but preserves the C05 Docker volume.`);
  await new Promise(resolve => {
    const stop = () => {
      for (const child of children) if (!child.killed) child.kill('SIGTERM');
      if (mailboxServer) mailboxServer.close();
      resolve();
    };
    process.once('SIGINT', stop); process.once('SIGTERM', stop);
    api.once('exit', code => { if (code && code !== 0) console.error(`C05 API exited with ${code}; see ${path.join(secretDir, 'api.log')}`); });
    ui.once('exit', code => { if (code && code !== 0) console.error(`C05 frontend exited with ${code}; see ${path.join(secretDir, 'frontend.log')}`); });
  });
}

function status() {
  const db = hasContainer() && containerRunning();
  if (db) assertExistingC05Container(); // Never query a same-name container backed by another volume.
  let migrations = 0, operators = 0, tenantsCount = 0, accounts = 0, activeMemberships = 0, pendingMemberships = 0;
  if (db) {
    try {
      migrations = Number(mysqlSql('SELECT COUNT(*) FROM c05_schema_migrations;'));
      operators = Number(mysqlSql('SELECT COUNT(*) FROM c05_platform_operator;'));
      tenantsCount = Number(mysqlSql('SELECT COUNT(*) FROM c04_tenant_catalog;'));
      accounts = Number(mysqlSql('SELECT COUNT(*) FROM c05_identity_account;'));
      activeMemberships = Number(mysqlSql('SELECT COUNT(*) FROM c05_identity_membership WHERE membership_state=1;'));
      pendingMemberships = Number(mysqlSql('SELECT COUNT(*) FROM c05_identity_membership WHERE membership_state=0;'));
    } catch { /* report unavailable below */ }
  }
  console.log(JSON.stringify({ container, volume, database, mysqlPort, dockerRunning: db, migrations, operators, tenants: tenantsCount, accounts, activeMemberships, pendingMemberships, secretDir }, null, 2));
}


const c05SnapshotCounts = [
  ['operators', 'c05_platform_operator'],
  ['accounts', 'c05_identity_account'],
  ['identityFactors', 'c05_identity_mfa_factor'],
  ['platformBackups', 'c05_platform_mfa_backup'],
  ['identitySessions', 'c05_identity_session'],
  ['platformSessions', 'c05_platform_session'],
  ['memberships', 'c05_identity_membership'],
  ['tenants', 'c04_tenant_catalog']
];
const backupSecretFiles = [
  rootPasswordFile, runtimeEnvFile, accessFile, mailboxPasswordFile,
  tenantAccountsFile, tenantAccountsMarkdownFile
];
// Never trust the contents of a link or a private file readable by other
// local users, even if its SHA-256 matches a previously stored fingerprint.
function assertProtectedFile(file, required = true) {
  const info = lstatSync(file, { throwIfNoEntry: false });
  if (!info) {
    if (required) throw new Error('Missing required C05 private file.');
    return false;
  }
  if (!info.isFile() || info.isSymbolicLink() ||
      (process.platform !== 'win32' && (info.mode & 0o077) !== 0))
    throw new Error('Unsafe private C05 file permissions or symbolic link.');
  return true;
}
function assertNoUnresolvedRekey() {
  // Pending credentials are neither a verified factor nor a recoverable SQL snapshot.
  if (existsSync(rekeyPendingFile))
    throw new Error('Unresolved SHA1 offline operator rekey; reconcile protected pending credentials before backup/restore.');
}
function sha256Bytes(value) { return createHash('sha256').update(value).digest('hex'); }
// Authenticated manifests prevent coherent archive+manifest substitution when
// a backup directory is writable but the separately held C05 key is not.
function backupManifestMac(manifest) {
  const keyHex = loadEnvFile().CRECISOL_C05_TOKEN_HMAC_KEY || '';
  if (!/^[0-9a-fA-F]{64}$/.test(keyHex))
    throw new Error('Protected C05 manifest authentication key is unavailable.');
  const { manifestMacSha256, ...data } = manifest;
  return createHmac('sha256', Buffer.from(keyHex, 'hex'))
    .update('C05/backup-manifest/v2\0').update(JSON.stringify(data)).digest('hex');
}
function authenticatedManifest(manifest) {
  if (manifest.format !== 'C05-local-backup-v2' ||
      !/^[a-f0-9]{64}$/.test(manifest.manifestMacSha256 || ''))
    return false;
  const expected = Buffer.from(backupManifestMac(manifest), 'hex');
  const actual = Buffer.from(manifest.manifestMacSha256, 'hex');
  return timingSafeEqual(expected, actual);
}

function databaseSnapshot(containerName, rootPassword) {
  const sql = [
    "SELECT CONCAT('migration:',version,':',sha256) FROM c05_schema_migrations ORDER BY version",
    ...c05SnapshotCounts.map(([name, table]) =>
      "SELECT CONCAT('" + name + ":',COUNT(*)) FROM " + table)
  ].join(';\n') + ';';
  const output = run('docker', [
    'exec', '-i', '-e', 'MYSQL_PWD', containerName,
    'mysql', '-uroot', '-N', '-B', database
  ], { input: sql, env: { ...process.env, MYSQL_PWD: rootPassword } });
  const rows = output.split(/\r?\n/).filter(Boolean);
  const migrations = rows.filter(line => line.startsWith('migration:'));
  if (![16, 17, 18].includes(migrations.length) ||
      rows.length !== migrations.length + c05SnapshotCounts.length ||
      !migrations.every((line, i) =>
        line.startsWith('migration:' + (i + 1) + ':') &&
        /^[a-f0-9]{64}$/i.test(line.split(':')[2] || '')) ||
      !c05SnapshotCounts.every(([name], i) =>
        new RegExp('^' + name + ':[0-9]+$').test(rows[migrations.length + i]))) {
    throw new Error('Invalid C05 backup snapshot or incomplete migration ledger.');
  }
  return { migrations: migrations.length, digest: sha256Bytes(rows.join('\n')) };
}

function backup() {
  assertNoUnresolvedRekey();
  // Never create or mutate a database or encryption key during a backup.
  for (const required of [rootPasswordFile, runtimeEnvFile, accessFile])
    assertProtectedFile(required);
  for (const file of backupSecretFiles) assertProtectedFile(file, false);
  if (!hasContainer() || !containerRunning()) throw new Error('C05 database must already be running to back up.');
  assertExistingC05Container();
  const password = readFileSync(rootPasswordFile, 'utf8').trim();
  const snapshot = databaseSnapshot(container, password);
  const dump = run('docker', [
    'exec', '-e', 'MYSQL_PWD', container,
    'mysqldump', '-uroot', '--single-transaction', '--quick',
    '--routines', '--triggers', '--hex-blob', database
  ], { env: { ...process.env, MYSQL_PWD: password }, maxBuffer: 64 * 1024 * 1024 });
  if (!dump.includes('CREATE TABLE')) throw new Error('Empty or invalid C05 SQL dump; refusing backup.');
  const archived = gzipSync(Buffer.from(dump + '\n', 'utf8'));
  mkdirSync(backupDir, { recursive: true, mode: 0o700 });
  const filename = 'crecisol-c05-' + new Date().toISOString().replace(/[:.]/g, '-') +
    '-' + randomHex(4) + '.sql.gz';
  const target = path.join(backupDir, filename);
  const secrets = {};
  for (const file of backupSecretFiles) {
    if (existsSync(file)) secrets[path.basename(file)] = sha256Bytes(readFileSync(file));
  }
  const manifest = {
    format: 'C05-local-backup-v2', database, migrations: snapshot.migrations,
    snapshotSha256: snapshot.digest, archiveSha256: sha256Bytes(archived), secrets
  };
  manifest.manifestMacSha256 = backupManifestMac(manifest);
  secureWrite(target, archived);
  secureWrite(target + '.manifest.json', JSON.stringify(manifest, null, 2) + '\n');
  secureWrite(path.join(backupDir, '.latest'), filename + '\n');
  console.log('C05 backup and private-key fingerprint manifest created outside repository: ' + target);
  console.log('IMPORTANT: separately archive the protected key/access files; the manifest does not contain or copy secrets.');
}

function verifyBackup() {
  assertNoUnresolvedRekey();
  const latestFile = path.join(backupDir, '.latest');
  assertProtectedFile(latestFile);
  for (const file of backupSecretFiles) assertProtectedFile(file, false);
  const filename = readFileSync(latestFile, 'utf8').trim();
  if (!/^crecisol-c05-[A-Za-z0-9TZ-]+\.sql\.gz$/.test(filename))
    throw new Error('Unsafe C05 backup filename; refusing restore.');
  const target = path.join(backupDir, filename);
  assertProtectedFile(target);
  assertProtectedFile(target + '.manifest.json');
  const archive = readFileSync(target);
  const manifest = JSON.parse(readFileSync(target + '.manifest.json', 'utf8'));
  if (!['C05-local-backup-v1', 'C05-local-backup-v2'].includes(manifest?.format) ||
      manifest.database !== database ||
      ![16, 17, 18].includes(manifest.migrations) ||
      !/^[a-f0-9]{64}$/.test(manifest.snapshotSha256 || '') ||
      sha256Bytes(archive) !== manifest.archiveSha256 ||
      !manifest.secrets || typeof manifest.secrets !== 'object' ||
      ![rootPasswordFile, runtimeEnvFile, accessFile].every(file =>
        manifest.secrets[path.basename(file)] === sha256Bytes(readFileSync(file))) ||
      // Also reject newly appearing protected files absent from the manifest.
      !backupSecretFiles.filter(file => existsSync(file)).every(file =>
        manifest.secrets[path.basename(file)] === sha256Bytes(readFileSync(file)))) {
    throw new Error('C05 archive or external encryption/access key fingerprints mismatch; refusing restore.');
  }
  if (manifest.format === 'C05-local-backup-v2' && !authenticatedManifest(manifest))
    throw new Error('C05 authenticated backup manifest was altered; refusing restore.');
  if (manifest.format === 'C05-local-backup-v1')
    console.warn('WARNING: legacy unsigned C05 manifest; restore verification only, not approved for automatic offline upgrade.');
  for (const [name, digest] of Object.entries(manifest.secrets)) {
    if (!backupSecretFiles.some(file => path.basename(file) === name) ||
        !/^[a-f0-9]{64}$/.test(digest) ||
        !backupSecretFiles.some(file => path.basename(file) === name &&
          existsSync(file) && sha256Bytes(readFileSync(file)) === digest)) {
      throw new Error('C05 protected secret file changed/missing; refusing restore.');
    }
  }
  const unzipped = gunzipSync(archive);
  const temp = 'crecisol-c05-restore-' + randomHex(8);
  const tempPassword = randomHex(32);
  let ownedContainerId = null;
  try {
    // No port or persistent volume. Delete ONLY the container actually created by this call.
    ownedContainerId = run('docker', [
      'run', '-d', '--name', temp, '--network', 'none',
      '-e', 'MYSQL_ROOT_PASSWORD', '-e', 'MYSQL_DATABASE=' + database, image
    ], { env: { ...process.env, MYSQL_ROOT_PASSWORD: tempPassword } });
    const deadline = Date.now() + 120000;
    let ready = false;
    while (Date.now() < deadline) {
      try {
        const answer = run('docker', [
          'exec', '-e', 'MYSQL_PWD', ownedContainerId,
          'mysql', '-uroot', '-Nse', 'SELECT 1'
        ], { env: { ...process.env, MYSQL_PWD: tempPassword } });
        if (answer === '1') { ready = true; break; }
      } catch { /* MySQL starts inside a temporary network-isolated container. */ }
      Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, 1000);
    }
    if (!ready) throw new Error('Temporary C05 restore MySQL did not become ready.');
    run('docker', [
      'exec', '-i', '-e', 'MYSQL_PWD', ownedContainerId,
      'mysql', '-uroot', database
    ], { input: unzipped, env: { ...process.env, MYSQL_PWD: tempPassword },
         maxBuffer: 64 * 1024 * 1024 });
    const restored = databaseSnapshot(ownedContainerId, tempPassword);
    if (restored.migrations !== manifest.migrations ||
        restored.digest !== manifest.snapshotSha256)
      throw new Error('Restored C05 database differs in ledger/account/factor/session/tenant counts.');
    console.log('PASS C05 isolated SQL restore: migrations=' + restored.migrations +
      '; ledger and entity counts match; external private key fingerprints match.');
    console.log('This verifies present keys; an independently protected key copy and owner device MFA test are still required.');
    return manifest.format;
  } finally {
    if (ownedContainerId) {
      try { run('docker', ['rm', '-f', ownedContainerId]); }
      catch { /* no other container is targeted */ }
    }
  }
}


function apiIsListening() {
  return new Promise(resolve => {
    const socket = connect({ host: '127.0.0.1', port: apiPort }, () => {
      socket.destroy(); resolve(true);
    });
    socket.setTimeout(750, () => { socket.destroy(); resolve(false); });
    socket.on('error', () => resolve(false));
  });
}

/** Explicit offline 017->018 upgrade. No implicit migration of existing volumes.
 * The owner's private SQL and encryption-key recovery must first be validated.
 */
async function upgradeSessionPolicy() {
  if (process.env.CRECISOL_C05_LOCAL_IDLE_POLICY_UPGRADE_APPROVED !== 'YES' ||
      process.env.CRECISOL_C05_LOCAL_POLICY_ADMIN_APPROVED !== 'YES')
    throw new Error('Explicit separate local idle-policy upgrade and security-admin approvals required.');
  if (await apiIsListening()) throw new Error('Stop C05 API before offline session-policy upgrade.');
  if (!hasContainer() || !containerRunning())
    throw new Error('Existing isolated C05 database must be running; will not create one.');
  assertExistingC05Container();
  assertNoUnresolvedRekey();
  requireSha1OperatorsReady();
  // Verifies SQL recovery in an ephemeral container plus external file hashes.
  // The manifest does not contain independent copies of the keys themselves.
  if (verifyBackup() !== 'C05-local-backup-v2')
    throw new Error('Offline upgrade requires a fresh authenticated C05-v2 SQL backup; legacy manifest is insufficient.');
  const expected = migrationNames.length - 1;
  const history = mysqlSql('SELECT version,sha256 FROM c05_schema_migrations ORDER BY version')
    .split(/\r?\n/).filter(Boolean);
  if (history.length !== expected || history.some((line, index) => {
    const [version, stored] = line.split('\t');
    const migrationFile = path.join(root, 'database', 'migrations', migrationNames[index] + '.sql');
    return Number(version) !== index + 1 || stored !== sha256(migrationFile);
  })) throw new Error('Historic C05 ledger does not match 001-017; refusing offline upgrade.');
  if (mysqlSql('SELECT status FROM c05_totp_sha1_policy WHERE policy_id=1') !== '1')
    throw new Error('Only completed TOTP-SHA1 installations may be upgraded.');
  const migration = path.join(root, 'database/migrations/018_c05_session_idle_policy.sql');
  const exists = mysqlSql("SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE() AND table_name='c05_session_idle_policy'");
  const column = mysqlSql("SELECT COUNT(*) FROM information_schema.columns WHERE table_schema=DATABASE() AND table_name='c05_platform_operator' AND column_name='can_manage_session_policy'");
  if (exists !== '0' || column !== '0')
    throw new Error('Partially applied session-policy migration: restore verified backup before retrying.');
  const key = loadEnvFile().CRECISOL_C05_TOKEN_HMAC_KEY || '';
  if (!/^[0-9a-f]{64}$/i.test(key)) throw new Error('Protected token key unavailable.');
  const adminHash = createHmac('sha256', Buffer.from(key, 'hex'))
    .update('C05/platform-login:' + operatorMailboxes[0]).digest('hex');
  const admin = mysqlSql("SELECT COUNT(*) FROM c05_platform_operator WHERE login_hmac=UNHEX('" +
    adminHash + "') AND active=1 AND recovery_pending=0 AND LENGTH(protected_seed)=48");
  if (admin !== '1') throw new Error('Explicit synthetic administrator identity not verified.');
  // Historical sessions are revoked to prevent a fixed 5/15-minute credential
  // being carried into the new sliding 30-minute policy.
  mysqlSql(readFileSync(migration, 'utf8'));
  // New table privileges are not inherited by the existing runtime role.
  mysqlSql(readFileSync(path.join(root, 'database/operations/c05_local_runtime_role.sql'), 'utf8'));
  mysqlSql("INSERT INTO c05_schema_migrations(version,migration_name,sha256,applied_at_utc)" +
    " VALUES(18,'018_c05_session_idle_policy','" + sha256(migration) + "',UTC_TIMESTAMP(6))");
  const changed = mysqlSql("UPDATE c05_platform_operator SET can_manage_session_policy=1 WHERE login_hmac=UNHEX('" +
    adminHash + "') AND active=1 AND recovery_pending=0; SELECT ROW_COUNT()");
  if (changed !== '1' ||
      mysqlSql('SELECT COUNT(*) FROM c05_platform_operator WHERE can_manage_session_policy=1') !== '1')
    throw new Error('Administrator privilege upgrade incomplete; do not restart API.');
  if (mysqlSql('SELECT idle_minutes FROM c05_session_idle_policy WHERE policy_id=1') !== '30')
    throw new Error('Initial 30-minute idle policy missing.');
  console.log('PASS isolated C05-018: 30-minute idle default, one approved synthetic administrator; old sessions revoked.');
  console.log('Owner-held external key recovery and real authenticators remain separate gates.');
}

function stop() {
  if (hasContainer() && containerRunning()) {
    assertExistingC05Container();
    run('docker', ['stop', container]);
  }
  console.log(`C05 MySQL stopped; volume ${volume} preserved.`);
}

try {
  if (mode === 'init') init();
  else if (mode === 'provision-tenants') await provisionTenants();
  else if (mode === 'approve-member') await approveMember();
  else if (mode === 'smoke') await smoke();
  else if (mode === 'start') await start();
  else if (mode === 'status') status();
  else if (mode === 'backup') backup();
  else if (mode === 'verify-backup') verifyBackup();
  else if (mode === 'upgrade-session-policy') await upgradeSessionPolicy();
  else if (mode === 'stop') stop();
} catch (error) {
  console.error(`C05 persistent operation failed: ${error.message}`);
  process.exitCode = 1;
}

