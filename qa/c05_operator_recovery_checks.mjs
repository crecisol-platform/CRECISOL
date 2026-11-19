import { createHash } from 'node:crypto';

export async function verifyOperatorRecovery({ browser, platform, api, check, confidential, operators, nextCode, totp,
  sql, noticeFor, mailFor, uiPort, reservePlatformWindow }) {
  await reservePlatformWindow(35);
  const target = operators[0];
  const oldLogin = await platform('login', { mailbox: target.mailbox, password: target.password });
  const oldSession = await platform('mfa', { challenge: oldLogin.data.challenge, code: target.backups.pop() });
  check(oldSession.status === 200, 'platform one-use backup can authenticate a provisioned operator');
  const oldToken = oldSession.data.sessionToken; confidential.push(oldToken);
  const pendingLogin = await platform('login', { mailbox: target.mailbox, password: target.password });
  check((await platform('mfa', { challenge: pendingLogin.data.challenge, code: operators[0].backups.at(-1) + '00' })).status === 400,
    'malformed platform recovery factor cannot enter the cryptographic verifier');
  const subject = await browser.newContext(), page = await subject.newPage();
  await page.goto(`http://127.0.0.1:${uiPort}/cuenta?vista=operadores`);
  await page.getByText('Personal autorizado de identidad CRECISOL', { exact: true }).click();
  await page.getByText('Recuperar acceso de operador de plataforma', { exact: true }).click();
  await page.getByLabel('Correo del operador sin acceso', { exact: true }).fill(target.mailbox);
  await page.getByLabel('No tengo disponibles mis métodos seguros de operador', { exact: true }).check();
  const requested = page.waitForResponse(r => r.url().endsWith('/operator-recovery/request'));
  await page.getByRole('button', { name: 'Solicitar recuperación de operador', exact: true }).click();
  check((await requested).status() === 202, 'Angular requests recovery exclusively inside the independent platform realm');
  const caseId = await page.getByLabel('Referencia de recuperación de operador', { exact: true }).innerText();
  check(sql(`SELECT COUNT(*) FROM c05_platform_identity_audit WHERE action_code='platform/operator-recovery/request' AND case_id=UUID_TO_BIN('${caseId}',0) AND operator_id IS NULL`) === '1',
    'public operator recovery requests cannot forge an authenticated operator in the immutable audit');
  const evidenceHash = createHash('sha256').update('FICTIONAL institutional identity evidence').digest('hex');
  check((await platform('review', { caseId, decision: 'approve', identityVerified: 'true', evidenceHash,
    reason: 'Target cannot approve their own institutional identity', code: target.backups.pop() }, oldToken)).status === 401,
    'platform operator cannot review their own global recovery case');
  const contexts = [], reviewers = [], tokens = [];
  for (const operator of operators.slice(1)) {
    const context = await browser.newContext(), p = await context.newPage(); contexts.push(context); reviewers.push(p);
    await p.goto(`http://127.0.0.1:${uiPort}/cuenta?vista=operadores`);
    await p.getByText('Personal autorizado de identidad CRECISOL', { exact: true }).click();
    await p.getByLabel('Correo del operador de identidad').fill(operator.mailbox);
    await p.getByLabel('Contraseña del operador', { exact: true }).fill(operator.password);
    await p.getByRole('button', { name: 'Acceder a identidad de plataforma', exact: true }).click();
    await p.getByLabel('Código MFA del operador', { exact: true }).fill(operator.backups.pop());
    const response = p.waitForResponse(r => r.url().endsWith('/platform-identity/mfa'));
    await p.getByRole('button', { name: 'Verificar operador', exact: true }).click();
    check((await response).status() === 200, 'independent institutional reviewer authenticates with a one-use platform backup');
    const token = (await context.cookies()).find(c => c.name === '__Host-crecisol-platform-c05').value; tokens.push(token); confidential.push(token);
    if (operator.id === operators[1].id) {
      const found = await platform('cases/find', { mailbox: target.mailbox, realm: 'PLATFORM_OPERATOR',
        reason: 'Verified fictional owner lost their neutral request reference', code: operator.backups.pop() }, token);
      check(found.status === 200 && found.data.caseId === caseId,
        'fresh-MFA platform support can locate an existing anonymous request without exposing identity or business lists');
      check(sql(`SELECT COUNT(*) FROM c05_platform_identity_audit WHERE action_code='platform/cases/find' AND case_id=UUID_TO_BIN('${caseId}',0) AND purpose_hmac IS NOT NULL`) === '1',
        'support lookup purpose is stored as an immutable digest rather than personal text');
    }
    await p.getByRole('button', { name: 'Revisar identidad del titular', exact: true }).click();
    await p.getByLabel('Caso a revisar', { exact: true }).fill(caseId);
    await p.getByLabel('SHA-256 del expediente de verificación').fill(evidenceHash);
    await p.getByLabel('Motivo de la revisión de identidad').fill('Independent institutional verification of fictional operator');
    await p.getByLabel('Código MFA fresco para revisar').fill(operator.backups.pop());
    await p.getByLabel('Verifiqué la identidad de forma independiente; no usé preguntas personales ni solo un correo nuevo', { exact: true }).check();
    const reviewed = p.waitForResponse(r => r.url().endsWith('/platform-identity/review'));
    await p.getByRole('button', { name: 'Registrar revisión de identidad', exact: true }).click();
    check((await reviewed).status() === 200, 'distinct platform reviewer approves an operator case after independent identity attestation');
    await p.getByRole('dialog', { name: 'Revisar identidad del titular', exact: true })
      .getByRole('button', { name: 'Cerrar ventana', exact: true }).click();
    await p.locator('dialog.cs-modal:modal').waitFor({ state: 'hidden' });
  }
  const profilePage = reviewers[0];
  await profilePage.getByRole('button', { name: 'Perfil', exact: true }).click();
  await profilePage.getByRole('button', { name: 'Editar perfil de operador', exact: true }).click();
  await profilePage.getByLabel('Nombre visible del operador', { exact: true }).fill('Fictional platform profile');
  const saved = profilePage.waitForResponse(r => r.url().endsWith('/platform-identity/profile/save'));
  await profilePage.getByRole('button', { name: 'Guardar perfil de operador', exact: true }).click();
  check((await saved).status() === 200, 'operator profile persists in its independent platform table and encryption realm');
  await profilePage.getByRole('dialog', { name: 'Editar perfil de operador', exact: true })
    .getByRole('button', { name: 'Cerrar ventana', exact: true }).click();
  await profilePage.locator('dialog.cs-modal:modal').waitFor({ state: 'hidden' });
  check((await platform('profile/read', {}, tokens[1])).data.displayName === '', 'one operator cannot inherit another operator profile');
  check((await api('profile/read', {}, tokens[0])).status === 401, 'platform profile credential cannot read any tenant profile');
  check(Number(sql(`SELECT TIMESTAMPDIFF(SECOND,UTC_TIMESTAMP(6),ready_at_utc) FROM c05_platform_recovery_case WHERE case_id=UUID_TO_BIN('${caseId}',0)`)) > 86300,
    'operator recovery enforces the same full twenty-four-hour cooling policy');
  check((await platform('release-share', { caseId, code: operators[1].backups.pop() }, tokens[0])).status === 401,
    'even a reviewed institutional operator cannot release a share before the cooling deadline');
  console.log('INFO privileged operator-case boundary fixture; no wall-clock or product cooling policy override');
  sql(`UPDATE c05_platform_recovery_case SET ready_at_utc=UTC_TIMESTAMP(6)-INTERVAL 1 SECOND WHERE case_id=UUID_TO_BIN('${caseId}',0)`);
  const shares = [];
  for (let index = 0; index < reviewers.length; index++) {
    const p = reviewers[index], operator = operators[index + 1];
    await p.locator('dialog.cs-modal:modal').waitFor({ state: 'hidden' });
    await p.getByRole('button', { name: 'Casos', exact: true }).click();
    await p.getByRole('button', { name: 'Emitir parte de recuperación', exact: true }).click();
    await p.getByLabel('Caso para entrega segura', { exact: true }).fill(caseId);
    await p.getByLabel('Código MFA para emitir mi parte').fill(operator.backups.pop());
    const release = p.waitForResponse(r => r.url().endsWith('/platform-identity/release-share'));
    await p.getByRole('button', { name: 'Emitir mi parte de recuperación', exact: true }).click();
    check((await release).status() === 200, 'reviewer releases only their own operator-recovery share after the security deadline');
    const shareDialog = p.getByRole('dialog', { name: 'Emitir parte de recuperación', exact: true });
    shares.push(await shareDialog.getByLabel('Parte de recuperación del operador').innerText()); confidential.push(shares[index]);
    await shareDialog.getByRole('button', { name: 'Cerrar ventana', exact: true }).click();
    await p.locator('dialog.cs-modal:modal').waitFor({ state: 'hidden' });
    await p.getByLabel('Parte de recuperación del operador').waitFor({ state: 'hidden' });
    check(true, 'reviewer secret appears only inside its modal and clears on dismissal');
  }
  const newPassword = 'Fictional-recovered-platform-strong-4!'; confidential.push(newPassword);
  await reservePlatformWindow(25);
  check((await platform('operator-recovery/complete', { caseId, shareA: shares[0], shareB: shares[0], newPassword })).status === 401,
    'duplicating an institutional reviewer share cannot recover an operator');
  await page.getByLabel('Caso aprobado de operador').fill(caseId);
  await page.getByLabel('Parte del primer revisor de operador').fill(shares[0]);
  await page.getByLabel('Parte del segundo revisor de operador').fill(shares[1]);
  await page.getByLabel('Nueva contraseña del operador recuperado').fill(newPassword);
  const staged = page.waitForResponse(r => r.url().endsWith('/operator-recovery/complete'));
  await page.getByRole('button', { name: 'Iniciar reenrolamiento de operador', exact: true }).click();
  check((await staged).status() === 200, 'two independent operator shares authorize only mandatory new MFA enrollment');
  await page.getByText('No puedo escanear: mostrar clave manual', { exact: true }).click();
  const seed = await page.getByLabel('Semilla MFA del operador recuperado', { exact: true }).innerText(); confidential.push(seed);
  check((await platform('session', {}, oldToken)).status === 401, 'staging operator recovery revokes every preexisting platform session');
  check((await platform('login', { mailbox: target.mailbox, password: target.password })).status === 401,
    'pending mandatory reenrollment blocks the old password and factor from creating fresh sessions');
  await page.getByLabel('Código del nuevo MFA de operador', { exact: true }).fill(totp(seed, true));
  const enrolled = page.waitForResponse(r => r.url().endsWith('/operator-recovery/enroll'));
  await page.getByRole('button', { name: 'Confirmar nuevo MFA de operador', exact: true }).click();
  check((await enrolled).status() === 200, 'new real TOTP proof atomically replaces operator password and old authenticators');
  await page.getByLabel('Códigos de respaldo del operador').waitFor();
  const backups = await page.getByLabel('Códigos de respaldo del operador').locator('code').allTextContents(); confidential.push(...backups);
  check(backups.length === 10, 'operator recovery emits ten independent new one-use backup codes once');
  check(!(await subject.cookies()).some(c => c.name === '__Host-crecisol-platform-c05'), 'operator recovery never logs the operator in automatically');
  await page.keyboard.press('Escape'); // Cerrar recuperación institucional ya verificada.
  async function uiLogin(mailbox, password, code) {
    if (await page.locator('dialog.cs-modal[open]').count()) await page.keyboard.press('Escape');
    await page.getByLabel('Correo del operador de identidad').fill(mailbox);
    await page.getByLabel('Contraseña del operador', { exact: true }).fill(password);
    await page.getByRole('button', { name: 'Acceder a identidad de plataforma', exact: true }).click();
    await page.getByLabel('Código MFA del operador', { exact: true }).fill(code);
    const response = page.waitForResponse(r => r.url().endsWith('/platform-identity/mfa'));
    await page.getByRole('button', { name: 'Verificar operador', exact: true }).click();
    check((await response).status() === 200, 'recovered operator authenticates through the normal password-plus-MFA browser flow');
  }
  await uiLogin(target.mailbox, newPassword, backups.pop());
  await page.getByRole('button', { name: 'Sesiones', exact: true }).click();
  const listed = page.waitForResponse(r => r.url().endsWith('/platform-identity/sessions'));
  await page.getByRole('button', { name: 'Consultar sesiones de operador', exact: true }).click();
  check((await listed).status() === 200, 'operator can inspect their own live browser/device sessions');
  const beforeRotation = (await subject.cookies()).find(c => c.name === '__Host-crecisol-platform-c05').value;
  const rotated = page.waitForResponse(r => r.url().endsWith('/platform-identity/sessions/rotate'));
  await page.getByRole('button', { name: 'Rotar sesión de operador', exact: true }).click();
  check((await rotated).status() === 200, 'Angular rotates the independent platform session cookie and CSRF binding');
  const afterRotation = (await subject.cookies()).find(c => c.name === '__Host-crecisol-platform-c05').value; confidential.push(afterRotation);
  check((await platform('session', {}, beforeRotation)).status === 401 && (await platform('session', {}, afterRotation)).status === 200,
    'rotated platform cookie cannot be replayed and its replacement stays valid');
  check(sql(`SELECT CONCAT(COUNT(DISTINCT expires_at_utc),':',COUNT(DISTINCT mfa_at_utc)) FROM c05_platform_session WHERE operator_id=UNHEX('${target.id}') AND security_revision=(SELECT security_revision FROM c05_platform_operator WHERE operator_id=UNHEX('${target.id}'))`) === '1:1',
    'platform rotation preserves both the absolute deadline and original MFA time');
  const ownSession = (await platform('sessions', {}, afterRotation)).data.sessions[0];
  check(ownSession.device === 'Chromium', 'session device label is a coarse browser family rather than a raw fingerprint or IP');
  check((await platform('sessions/revoke', { sessionId: ownSession.id }, tokens[1])).status === 401,
    'another platform operator cannot revoke the subject operator session by identifier');
  await page.getByRole('button', { name: 'Cerrar todas mis sesiones de plataforma', exact: true }).click();
  await page.getByLabel('Código MFA para cerrar sesiones de plataforma').fill(backups.pop());
  const allRevoked = page.waitForResponse(r => r.url().endsWith('/platform-identity/sessions/revoke-all'));
  await page.getByRole('dialog', { name: 'Cerrar todas mis sesiones de plataforma', exact: true })
    .getByRole('button', { name: 'Confirmar cierre de todas mis sesiones de plataforma', exact: true }).click();
  check((await allRevoked).status() === 200 && (await platform('session', {}, afterRotation)).status === 401,
    'fresh platform MFA revokes all own platform sessions without changing tenant identities');
  await uiLogin(target.mailbox, newPassword, backups.pop());
  await page.getByRole('button', { name: 'Seguridad', exact: true }).click();
  await page.getByRole('button', { name: 'Reemplazar MFA de operador', exact: true }).click();
  await page.getByLabel('Contraseña para reemplazar MFA de operador').fill(newPassword);
  await page.getByLabel('Código MFA o respaldo para reemplazar factor de operador').fill(backups.pop());
  const replace = page.waitForResponse(r => r.url().endsWith('/platform-identity/factor/replace'));
  await page.getByRole('button', { name: 'Preparar nuevo factor de operador', exact: true }).click();
  check((await replace).status() === 200, 'operator can recover or replace MFA automatically with password and a fresh one-use backup');
  await page.getByText('No puedo escanear: mostrar clave manual', { exact: true }).click();
  const newSeed = await page.getByLabel('Semilla del factor de operador a reemplazar').innerText(); confidential.push(newSeed);
  await page.getByLabel('Código del factor nuevo de operador').fill(totp(newSeed, true));
  const confirmed = page.waitForResponse(r => r.url().endsWith('/platform-identity/factor/confirm'));
  await page.getByRole('button', { name: 'Activar nuevo factor de operador', exact: true }).click();
  check((await confirmed).status() === 200, 'new operator factor is activated only after actual TOTP proof, with old sessions revoked');
  await page.waitForFunction(oldCode => ![...document.querySelectorAll('cs-platform-identity-panel aside code')].some(n => n.textContent === oldCode), backups[0]);
  await page.getByLabel('Códigos de respaldo del operador').locator('code').nth(9).waitFor();
  const finalBackups = await page.getByLabel('Códigos de respaldo del operador').locator('code').allTextContents(); confidential.push(...finalBackups);
  await reservePlatformWindow(30);
  await uiLogin(target.mailbox, newPassword, finalBackups.pop());
  await page.getByRole('button', { name: 'Seguridad', exact: true }).click();
  await page.getByRole('button', { name: 'Cambiar contraseña de operador', exact: true }).click();
  const finalPassword = 'Fictional-platform-password-change-5!'; confidential.push(finalPassword);
  await page.getByLabel('Contraseña actual del operador').fill(newPassword);
  await page.getByLabel('Nueva contraseña de operador', { exact: true }).fill(finalPassword);
  await page.getByLabel('Código MFA para cambiar contraseña de operador').fill(finalBackups.pop());
  const changed = page.waitForResponse(r => r.url().endsWith('/platform-identity/password/change'));
  await page.getByRole('button', { name: 'Cambiar contraseña de plataforma', exact: true }).click();
  check((await changed).status() === 200, 'operator password change requires current password and fresh independent factor');
  await uiLogin(target.mailbox, finalPassword, finalBackups.pop());
  await page.getByRole('button', { name: 'Seguridad', exact: true }).click();
  await page.getByRole('button', { name: 'Cambiar correo de operador', exact: true }).click();
  const changedMailbox = 'identity.changed@example.test';
  await page.getByLabel('Nuevo correo del operador', { exact: true }).fill(changedMailbox);
  await page.getByLabel('Código MFA para solicitar correo de operador').fill(finalBackups.pop());
  const requestedMailbox = page.waitForResponse(r => r.url().endsWith('/platform-identity/mailbox/request'));
  await page.getByRole('button', { name: 'Solicitar nuevo correo de operador', exact: true }).click();
  check((await requestedMailbox).status() === 200, 'operator mailbox change requests an independently delivered proof for the new address');
  const token = await mailFor(changedMailbox); confidential.push(token);
  await page.getByLabel('Token del nuevo correo de operador').fill(token);
  await page.getByLabel('Código MFA para confirmar correo de operador').fill(finalBackups.pop());
  const confirmedMailbox = page.waitForResponse(r => r.url().endsWith('/platform-identity/mailbox/complete'));
  await page.getByRole('button', { name: 'Confirmar correo de operador', exact: true }).click();
  check((await confirmedMailbox).status() === 200, 'operator mailbox proof and fresh MFA commit atomically and revoke old sessions');
  await reservePlatformWindow(35);
  check((await platform('login', { mailbox: target.mailbox, password: finalPassword })).status === 401, 'old operator mailbox cannot authenticate after verified replacement');
  check((await platform('login', { mailbox: changedMailbox, password: finalPassword })).status === 200, 'verified new operator mailbox is the only active login handle');
  await platform('session', {}, tokens[1]);
  await subject.close(); for (const context of contexts) await context.close();
  return tokens[1];
}
