// Real Chromium WebAuthn ceremonies and real API/MySQL. CDP emulates a CTAP2
// authenticator; navigator.credentials and server verification remain intact.
import { createHash, createPrivateKey, generateKeyPairSync, sign } from 'node:crypto';
export async function verifyPasskeys({ browser, api, check, confidential, password, mailbox, backups, delay, uiPort, mailFor, totp }) {
  // The preceding adversarial cohorts may have exhausted the real minute
  // quotas. Wait for a new window, without injecting time or deleting ledgers.
  await delay(60500 - Date.now() % 60000);
  const context = await browser.newContext();
  const page = await context.newPage();
  const cdp = await context.newCDPSession(page);
  await cdp.send('WebAuthn.enable');
  const { authenticatorId } = await cdp.send('WebAuthn.addVirtualAuthenticator', { options: {
    protocol: 'ctap2', ctap2Version: 'ctap2_1', transport: 'internal',
    hasResidentKey: true, hasUserVerification: true, isUserVerified: true, automaticPresenceSimulation: true
  } });
  await page.goto(`http://localhost:${uiPort}/cuenta`);
  await page.getByRole('region', { name: 'Iniciar sesión', exact: true }).getByLabel('Correo electrónico', { exact: true }).fill(mailbox);
  await page.getByLabel('Contraseña', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Continuar', exact: true }).click();
  await page.getByLabel('Código de tu aplicación de autenticación').fill(backups.pop());
  await page.getByRole('button', { name: 'Verificar', exact: true }).click();
  await page.getByRole('heading', { name: 'Estado de la sesión', exact: true }).waitFor();
  await page.getByRole('link', { name: 'Llaves', exact: true }).click();
  await page.getByRole('button', { name: 'Registrar nueva llave de acceso', exact: true }).click();
  await page.getByLabel('Código MFA para registrar llave').fill(backups.pop());
  const registered = page.waitForResponse(r => r.url().endsWith('/passkeys/register'));
  await page.getByRole('button', { name: 'Registrar llave de acceso', exact: true }).click();
  check((await registered).status() === 200, 'Angular navigator.credentials.create -> verified resident passkey stored in MySQL');
  const keys = await cdp.send('WebAuthn.getCredentials', { authenticatorId });
  check(keys.credentials.length === 1, 'real browser authenticator contains the registered credential');
  const emulatedPrivateKey = createPrivateKey({ key: Buffer.from(keys.credentials[0].privateKey, 'base64'), format: 'der', type: 'pkcs8' });
  confidential.push(keys.credentials[0].privateKey);
  function resign(response) {
    const message = Buffer.concat([Buffer.from(response.response.authenticatorData, 'base64url'),
      createHash('sha256').update(Buffer.from(response.response.clientDataJSON, 'base64url')).digest()]);
    // CTAP2 can negotiate Ed25519 as well as ES256. Node requires a null
    // digest for EdDSA; the negative proof must itself remain a valid signature.
    const algorithm = emulatedPrivateKey.asymmetricKeyType === 'ed25519' ? null : 'sha256';
    response.response.signature = sign(algorithm, message, emulatedPrivateKey).toString('base64url');
    return JSON.stringify(response);
  }
  await page.getByRole('button', { name: 'Cerrar sesión', exact: true }).click();
  await page.getByRole('heading', { name: 'Iniciar sesión', exact: true }).waitFor();
  const login = page.waitForResponse(r => r.url().endsWith('/passkeys/login'));
  await page.getByRole('button', { name: 'Entrar con llave de acceso', exact: true }).click();
  check((await login).status() === 200, 'Angular discoverable passkey login verifies user presence, verification and signature without password');
  await page.getByRole('heading', { name: 'Estado de la sesión', exact: true }).waitFor();
  // The previous passkey setup leaves the client on its dedicated keys view.
  // A session inventory belongs to the separate account summary screen.
  await page.getByRole('link', { name: 'Resumen', exact: true }).click();
  const sessionRequest = page.waitForRequest(r => r.url().endsWith('/sessions'));
  await page.getByRole('button', { name: 'Actualizar dispositivos', exact: true }).click();
  await sessionRequest;
  const token = (await context.cookies()).find(c => c.name === '__Host-crecisol-c05')?.value; confidential.push(token);
  check((await api('sessions', {}, token, 'tenant-b.example.test')).status === 401, 'passkey authentication cannot inherit membership in another tenant');
  const concurrent = await Promise.all(Array.from({ length: 32 }, () => api('sessions', {}, token)));
  console.log('INFO 32-request status counts ' + JSON.stringify(concurrent.reduce((counts, r) => ({ ...counts, [r.status]: (counts[r.status] || 0) + 1 }), {})));
  check(concurrent.every(r => r.status === 200), '32 simultaneous operational requests complete without nested quota-pool starvation');
  async function assertion() {
    const options = await api('passkeys/options');
    if (options.status !== 200) console.log('INFO passkey options denied status=' + options.status);
    check(options.status === 200 && !options.data.publicKey.allowCredentials?.length, 'passwordless options do not enumerate account credential identifiers');
    const response = await page.evaluate(async options => {
      const credential = await navigator.credentials.get({ publicKey: PublicKeyCredential.parseRequestOptionsFromJSON(options) });
      return credential.toJSON();
    }, options.data.publicKey);
    return { challenge: options.data.challenge, response: JSON.stringify(response) };
  }
  const first = await assertion();
  const winners = await Promise.all(Array.from({ length: 6 }, () => api('passkeys/login', first)));
  check(winners.filter(r => r.status === 200).length === 1, 'six concurrent signed assertions have exactly one challenge-consumption winner');
  for (const r of winners) if (r.data?.sessionToken) confidential.push(r.data.sessionToken);
  check((await api('passkeys/login', first)).status === 401, 'signed WebAuthn response cannot be replayed');
  await delay(60500 - Date.now() % 60000);
  const signingControl = await assertion();
  const signedControl = await api('passkeys/login', { ...signingControl, response: resign(JSON.parse(signingControl.response)) });
  check(signedControl.status === 200, 'independent Node signature accepted for the negotiated authenticator algorithm before applying negative mutations');
  confidential.push(signedControl.data.sessionToken);
  const altered = await assertion();
  const corrupt = JSON.parse(altered.response);
  corrupt.response.signature = Buffer.alloc(64, 1).toString('base64url');
  check((await api('passkeys/login', { ...altered, response: JSON.stringify(corrupt) })).status === 401, 'invalid WebAuthn signature denied');
  check((await api('passkeys/login', altered)).status === 401, 'failed signature consumes its challenge instead of permitting retries');
  const origin = await assertion();
  const foreign = JSON.parse(origin.response);
  const client = JSON.parse(Buffer.from(foreign.response.clientDataJSON, 'base64url'));
  client.origin = 'https://attacker.invalid';
  foreign.response.clientDataJSON = Buffer.from(JSON.stringify(client)).toString('base64url');
  check((await api('passkeys/login', { ...origin, response: resign(foreign) })).status === 401, 'foreign WebAuthn client origin denied even with a valid authenticator signature');
  const expiring = await assertion();
  const wrongHandle = JSON.parse(expiring.response);
  wrongHandle.response.userHandle = Buffer.alloc(16, 9).toString('base64url');
  check((await api('passkeys/login', { ...expiring, response: JSON.stringify(wrongHandle) })).status === 401, 'credential user handle must match its account');
  const crossTenant = await assertion();
  check((await api('passkeys/login', crossTenant, undefined, 'tenant-b.example.test')).status === 401, 'tenant A WebAuthn challenge denied under host B');
  const wrongRp = await assertion(); const wrongRpResponse = JSON.parse(wrongRp.response);
  const authData = Buffer.from(wrongRpResponse.response.authenticatorData, 'base64url');
  createHash('sha256').update('attacker.invalid').digest().copy(authData, 0);
  wrongRpResponse.response.authenticatorData = authData.toString('base64url');
  check((await api('passkeys/login', { ...wrongRp, response: resign(wrongRpResponse) })).status === 401,
    'wrong RP ID hash is rejected even when signed by the registered authenticator key');
  const wrongChallenge = await assertion(); const challengeResponse = JSON.parse(wrongChallenge.response);
  const clientData = JSON.parse(Buffer.from(challengeResponse.response.clientDataJSON, 'base64url'));
  clientData.challenge = Buffer.alloc(32, 7).toString('base64url');
  challengeResponse.response.clientDataJSON = Buffer.from(JSON.stringify(clientData)).toString('base64url');
  check((await api('passkeys/login', { ...wrongChallenge, response: resign(challengeResponse) })).status === 401,
    'a valid signature cannot substitute a different server challenge');
  const rollback = await assertion(); const rollbackResponse = JSON.parse(rollback.response);
  const rollbackData = Buffer.from(rollbackResponse.response.authenticatorData, 'base64url'); rollbackData.writeUInt32BE(1, 33);
  rollbackResponse.response.authenticatorData = rollbackData.toString('base64url');
  check((await api('passkeys/login', { ...rollback, response: resign(rollbackResponse) })).status === 401,
    'signed authenticator counter rollback cannot create a session');
  check((await api('passkeys/register-options', { code: backups.pop() })).status === 401, 'anonymous browser cannot enroll a credential');
  const pairingKey = generateKeyPairSync('ec', { namedCurve: 'prime256v1' });
  const p1363 = message => sign('sha256', Buffer.from(message), { key: pairingKey.privateKey, dsaEncoding: 'ieee-p1363' }).toString('hex');
  const pair = await api('pairings/start', { publicKey: pairingKey.publicKey.export({ type: 'spki', format: 'der' }).toString('hex'), signature: p1363('CRECISOL:C05:PAIR:START') });
  check(pair.status === 200, 'separate test browser proves a real P-256 key before pairing with a passkey-authenticated device');
  check((await api('pairings/approve', { token: pair.data.token, fingerprint: pair.data.fingerprint, confirmed: 'true', code: backups.pop() }, token)).status === 200,
    'passkey-authenticated device can approve an independent browser using fresh MFA');
  const paired = await api('pairings/complete', { token: pair.data.token, signature: p1363(`CRECISOL:C05:PAIR:${pair.data.token}:${pair.data.nonce}`) });
  check(paired.status === 200, 'paired browser receives its own delegated session'); confidential.push(paired.data.sessionToken);
  check((await api('sessions', {}, paired.data.sessionToken)).data.sessions[0].factor === 'PAIRING',
    'session ledger records pairing instead of falsely claiming a new WebAuthn verification');
  await api('recovery/request', { mailbox });
  const recoveryProof = await mailFor(mailbox); confidential.push(recoveryProof);
  check((await api('factor/recover-passkey', { token: recoveryProof, newPassword: password }, paired.data.sessionToken)).status === 401,
    'delegated pairing from a passkey device cannot impersonate fresh direct passkey user verification');
  const inventory = await api('passkeys', {}, token);
  check(inventory.status === 200 && inventory.data.passkeys.length === 1, 'own passkey inventory exposes only revocable factor references');
  // Account quota for assertions is intentionally eight/minute. Revocation is
  // independent step-up and immediately invalidates all existing sessions.
  check((await api('passkeys/revoke', { factorId: inventory.data.passkeys[0].id, code: backups.pop() }, token)).status === 200,
    'fresh MFA revokes the exact owned passkey');
  check((await api('sessions', {}, token)).status === 401, 'revoking a passkey revokes its unexpired session');
  await delay(60500 - Date.now() % 60000);
  const revoked = await assertion();
  check((await api('passkeys/login', revoked)).status === 401, 'revoked credential cannot issue a new session');
  const passwordLogin = await api('login', { mailbox, password });
  const restored = await api('mfa/verify', { challenge: passwordLogin.data.challenge, code: backups.pop() });
  check(restored.status === 200, 'independent TOTP survives passkey revocation');
  confidential.push(restored.data.sessionToken);
  await cdp.send('WebAuthn.removeCredential', { authenticatorId, credentialId: keys.credentials[0].credentialId });
  await page.reload();
  await page.getByRole('heading', { name: 'Iniciar sesión', exact: true }).waitFor();
  await page.getByRole('region', { name: 'Iniciar sesión', exact: true }).getByLabel('Correo electrónico', { exact: true }).fill(mailbox);
  await page.getByLabel('Contraseña', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Continuar', exact: true }).click();
  await page.getByLabel('Código de tu aplicación de autenticación').fill(backups.pop());
  await page.getByRole('button', { name: 'Verificar', exact: true }).click();
  await page.getByRole('heading', { name: 'Estado de la sesión', exact: true }).waitFor();
  await page.getByRole('link', { name: 'Llaves', exact: true }).click();
  await page.getByRole('button', { name: 'Registrar nueva llave de acceso', exact: true }).click();
  await page.getByLabel('Código MFA para registrar llave').fill(backups.pop());
  const secondRegistration = page.waitForResponse(r => r.url().endsWith('/passkeys/register'));
  await page.getByRole('button', { name: 'Registrar llave de acceso', exact: true }).click();
  check((await secondRegistration).status() === 200, 'owner registers an independent alternative passkey for MFA-loss recovery');
  const loggedOut = page.waitForResponse(r => r.url().endsWith('/logout'));
  await page.getByRole('button', { name: 'Cerrar sesión', exact: true }).click(); await loggedOut;
  await page.getByRole('button', { name: 'Entrar con llave de acceso', exact: true }).click();
  await page.getByRole('heading', { name: 'Estado de la sesión', exact: true }).waitFor();
  await page.getByRole('link', { name: 'Seguridad', exact: true }).click();
  await page.getByRole('button', { name: 'Recuperar MFA con una llave de acceso', exact: true }).click();
  await page.getByLabel('Correo verificado para recuperar MFA').fill(mailbox);
  const recoveryRequested = page.waitForResponse(r => r.url().endsWith('/recovery/request'));
  await page.getByRole('button', { name: 'Pedir token para recuperar MFA', exact: true }).click(); await recoveryRequested;
  const alternativeProof = await mailFor(mailbox); confidential.push(alternativeProof);
  const recoveredPassword = 'Passkey-recovered-local-strong-6!'; confidential.push(recoveredPassword);
  await page.getByText('Ya tengo el token de recuperación', { exact: true }).click();
  await page.getByLabel('Token de correo para recuperar MFA').fill(alternativeProof);
  await page.getByLabel('Nueva contraseña tras recuperar MFA').fill(recoveredPassword);
  const recovered = page.waitForResponse(r => r.url().endsWith('/factor/recover-passkey'));
  await page.getByRole('button', { name: 'Recuperar autenticadores con llave', exact: true }).click();
  check((await recovered).status() === 200, 'fresh direct WebAuthn verification and verified mailbox recover lost MFA without old password or backup');
  const reLogin = await api('login', { mailbox, password: recoveredPassword });
  check(reLogin.status === 200 && reLogin.data.enrollmentRequired, 'passkey recovery grants only mandatory reenrollment and no automatic session');
  const reEnrollment = await api('mfa/enroll', { challenge: reLogin.data.challenge }); confidential.push(reEnrollment.data.secret);
  const reVerified = await api('mfa/verify', { challenge: reLogin.data.challenge, code: totp(reEnrollment.data.secret, true) });
  check(reVerified.status === 200 && reVerified.data.recoveryCodes.length === 10, 'recovered owner proves a new real TOTP factor and receives new backup codes');
  confidential.push(reVerified.data.sessionToken, ...reVerified.data.recoveryCodes);
  backups.splice(0, backups.length, ...reVerified.data.recoveryCodes);
  await context.close();
  return { token: reVerified.data.sessionToken, password: recoveredPassword };
}
