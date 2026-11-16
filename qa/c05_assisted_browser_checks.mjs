import { createHash } from 'node:crypto';

export async function verifyAssisted({ browser, api, platform, check, confidential, operators, password, enrollLogin,
  nextCode, totp, mailFor, noticeFor, sql, A, accountId, uiPort, reserveTestWindow }) {
  await reserveTestWindow(50);
  const mailbox = 'invited@example.test';
  const original = await enrollLogin(mailbox);
  check((await platform('session', {}, original.token)).status === 401,
    'tenant identity cannot become an operator of global identity recovery');
  check((await api('platform/login', { mailbox: operators[0].mailbox, password: operators[0].password })).status === 401,
    'platform authentication is denied under a tenant Host');
  const subject = await browser.newContext(); const page = await subject.newPage();
  await page.goto(`http://127.0.0.1:${uiPort}/cuenta?vista=recuperar`);
  await page.getByText('Perdí todos mis métodos de acceso', { exact: true }).click();
  await page.getByLabel('Correo de la cuenta que necesita asistencia').fill(mailbox);
  await page.getByLabel('Mis métodos automáticos seguros no están disponibles', { exact: true }).check();
  const requested = page.waitForResponse(r => r.url().endsWith('/assisted/request'));
  await page.getByRole('button', { name: 'Solicitar recuperación asistida', exact: true }).click();
  check((await requested).status() === 202, 'Angular requests an isolated assisted recovery case with neutral receipt');
  const caseId = await page.getByLabel('Referencia de recuperación asistida', { exact: true }).innerText();
  check(sql(`SELECT COUNT(*) FROM c05_identity_audit WHERE action_code='assisted/request' AND subject_account_id=UNHEX('${accountId(mailbox)}') AND actor_account_id IS NULL`) === '1',
    'anonymous recovery request is attributed to a subject, never falsely to an authenticated actor');
  const unknown = await api('assisted/request', { mailbox: 'nonexistent.assisted@example.test', automaticMethodsUnavailable: 'true' });
  check(unknown.status === 202 && typeof unknown.data.reference === 'string' && unknown.data.reference !== caseId,
    'unknown and known assisted requests both return uncorrelated references without account enumeration');
  const contexts = [], pages = [], tokens = [];
  for (const operator of operators) {
    const context = await browser.newContext(); const operatorPage = await context.newPage(); contexts.push(context); pages.push(operatorPage);
    await operatorPage.goto(`http://127.0.0.1:${uiPort}/cuenta?vista=operadores`);
    await operatorPage.getByText('Personal autorizado de identidad CRECISOL', { exact: true }).click();
    await operatorPage.getByLabel('Correo del operador de identidad').fill(operator.mailbox);
    await operatorPage.getByLabel('Contraseña del operador', { exact: true }).fill(operator.password);
    await operatorPage.getByRole('button', { name: 'Acceder a identidad de plataforma', exact: true }).click();
    await operatorPage.getByLabel('Código MFA del operador', { exact: true }).fill(totp(operator.seed, true));
    const verified = operatorPage.waitForResponse(r => r.url().endsWith('/platform-identity/mfa'));
    await operatorPage.getByRole('button', { name: 'Verificar operador', exact: true }).click();
    check((await verified).status() === 200, 'independently provisioned platform operator proves password and real TOTP');
    const cookie = (await context.cookies()).find(c => c.name === '__Host-crecisol-platform-c05');
    check(cookie?.secure && cookie.httpOnly && cookie.sameSite === 'Strict', 'platform realm has a separate Secure HttpOnly SameSite cookie');
    tokens.push(cookie.value); confidential.push(cookie.value);
    check((await api('sessions', {}, cookie.value)).status === 401, 'platform session cannot act as a tenant session or read tenant data');
  }
  async function review(index) {
    const operatorPage = pages[index], operator = operators[index];
    await operatorPage.getByRole('button', { name: 'Revisar identidad del titular', exact: true }).click();
    await operatorPage.getByLabel('Caso a revisar', { exact: true }).fill(caseId);
    await operatorPage.getByLabel('SHA-256 del expediente de verificación').fill(createHash('sha256').update(`FICTIONAL independent identity evidence ${index}`).digest('hex'));
    await operatorPage.getByLabel('Motivo de la revisión de identidad').fill('Independent fictional verification, no real personal documents');
    await operatorPage.getByLabel('Código MFA fresco para revisar').fill(await nextCode(operator.seed));
    await operatorPage.getByLabel('Verifiqué la identidad de forma independiente; no usé preguntas personales ni solo un correo nuevo', { exact: true }).check();
    const reviewed = operatorPage.waitForResponse(r => r.url().endsWith('/platform-identity/review'));
    await operatorPage.getByRole('button', { name: 'Registrar revisión de identidad', exact: true }).click();
    check((await reviewed).status() === 200, 'platform reviewer commits a fresh-MFA identity attestation and evidence digest');
    // The server response can arrive before Angular clears busy().
    // Escape is correctly blocked while busy; use the enabled close control
    // and assert the modal is no longer in the native top layer before moving on.
    await operatorPage.getByRole('dialog', { name: 'Revisar identidad del titular', exact: true })
      .getByRole('button', { name: 'Cerrar ventana', exact: true }).click();
    await operatorPage.locator('dialog.cs-modal:modal').waitFor({ state: 'hidden' });
  }
  await review(0);
  check(sql(`SELECT COUNT(*) FROM c05_identity_assisted_review WHERE case_id=UUID_TO_BIN('${caseId}',0) AND approved=1`) === '1', 'one verified operator is insufficient for assisted recovery');
  check((await platform('release-share', { caseId, code: await nextCode(operators[1].seed) }, tokens[1])).status === 401,
    'unreviewed second operator cannot release a handoff share before double control');
  await review(1);
  const recoveryToken = await mailFor(mailbox); confidential.push(recoveryToken);
  const completionPassword = 'Recovered-assisted-local-strong-3!'; confidential.push(completionPassword);
  check(Number(sql(`SELECT TIMESTAMPDIFF(SECOND,UTC_TIMESTAMP(6),ready_at_utc) FROM c05_identity_assisted_case WHERE case_id=UUID_TO_BIN('${caseId}',0)`)) > 86300,
    'actual runtime sets a full twenty-four-hour cooling window after the second independent review');
  check((await api('assisted/complete', { caseId, token: recoveryToken, newPassword: completionPassword })).status === 401,
    'valid delivered recovery capability is unusable before the security delay');
  check((await platform('review', { caseId, decision: 'approve', identityVerified: 'true', evidenceHash: 'ab'.repeat(32), reason: 'Repeated reviewer cannot replace double control', code: await nextCode(operators[0].seed) }, tokens[0])).status === 401,
    'one operator cannot approve twice or modify an existing immutable review');
  // Boundary fixture: exercise both branches of the production clock predicate
  // without pretending that twenty-four hours actually elapsed during this test.
  console.log('INFO privileged fixture advances only the case readiness boundary; wall clock and 24-hour product policy are unchanged');
  sql(`UPDATE c05_identity_assisted_case SET ready_at_utc=UTC_TIMESTAMP(6)-INTERVAL 1 SECOND WHERE case_id=UUID_TO_BIN('${caseId}',0)`);
  sql(`UPDATE c05_platform_operator SET active=0 WHERE operator_id=UNHEX('${operators[1].id}')`);
  check((await api('assisted/complete', { caseId, token: recoveryToken, newPassword: completionPassword })).status === 401,
    'suspending either reviewer invalidates recovery even after a valid double approval');
  sql(`UPDATE c05_platform_operator SET active=1 WHERE operator_id=UNHEX('${operators[1].id}')`);
  const shares = [];
  for (let index = 0; index < operators.length; index++) {
    const operatorPage = pages[index];
    await operatorPage.getByRole('button', { name: 'Emitir parte de recuperación', exact: true }).click();
    await operatorPage.getByLabel('Caso para entrega segura', { exact: true }).fill(caseId);
    await operatorPage.getByLabel('Código MFA para emitir mi parte').fill(await nextCode(operators[index].seed));
    const released = operatorPage.waitForResponse(r => r.url().endsWith('/platform-identity/release-share'));
    await operatorPage.getByRole('button', { name: 'Emitir mi parte de recuperación', exact: true }).click();
    check((await released).status() === 200, 'each independently authenticated reviewer releases only their own expiring handoff share');
    const shareDialog = operatorPage.getByRole('dialog', { name: 'Emitir parte de recuperación', exact: true });
    shares.push(await shareDialog.getByLabel('Parte de recuperación del operador', { exact: true }).innerText());
    confidential.push(shares[index]);
    await shareDialog.getByRole('button', { name: 'Cerrar ventana', exact: true }).click();
    await operatorPage.locator('dialog.cs-modal:modal').waitFor({ state: 'hidden' });
    await operatorPage.getByLabel('Parte de recuperación del operador').waitFor({ state: 'hidden' });
  }
  check(shares[0] !== shares[1], 'two-operator handoff shares are independent random capabilities');
  await reserveTestWindow(20);
  check((await api('assisted/handoff-complete', { caseId, shareA: shares[0], shareB: shares[0], newPassword: completionPassword })).status === 401,
    'duplicating one operator share never replaces the second independent share');
  await page.getByLabel('Referencia para entrega por dos operadores').fill(caseId);
  await page.getByLabel('Parte del primer operador').fill(shares[0]);
  await page.getByLabel('Parte del segundo operador').fill(shares[1]);
  await page.getByLabel('Contraseña para recuperación por dos operadores').fill(completionPassword);
  const completed = page.waitForResponse(r => r.url().endsWith('/assisted/handoff-complete'));
  await page.getByRole('button', { name: 'Completar recuperación con ambas partes', exact: true }).click();
  check((await completed).status() === 200, 'Angular completes loss-of-email recovery with both separately delivered operator shares');
  check((await api('assisted/handoff-complete', { caseId, shareA: shares[0], shareB: shares[1], newPassword: completionPassword })).status === 401,
    'completed assisted case and its shares cannot be replayed');
  check((await api('sessions', {}, original.token)).status === 401, 'assisted recovery revokes all sessions and authenticators of the previous identity revision');
  check(!(await subject.cookies()).some(c => c.name === '__Host-crecisol-c05'), 'assisted recovery does not automatically log the subject in');
  const reLogin = await api('login', { mailbox, password: completionPassword });
  check(reLogin.status === 200 && reLogin.data.enrollmentRequired === true, 'new password only authorizes mandatory MFA reenrollment, not a session');
  const invalid = await api('mfa/verify', { challenge: reLogin.data.challenge, code: '000000' });
  check(invalid.status === 401, 'old or missing MFA cannot authenticate during assisted reenrollment');
  check(sql(`SELECT consumed_at_utc IS NULL FROM c05_identity_mfa_reset_authorization WHERE account_id=UNHEX('${accountId(mailbox)}')`) === '1',
    'failed MFA proof does not consume the authorized reenrollment window');
  const enrollment = await api('mfa/enroll', { challenge: reLogin.data.challenge });
  confidential.push(enrollment.data.secret);
  const session = await api('mfa/verify', { challenge: reLogin.data.challenge, code: totp(enrollment.data.secret, true) });
  check(session.status === 200 && session.data.recoveryCodes.length === 10, 'fresh real TOTP enrollment replaces all lost methods and emits new one-use backup codes');
  confidential.push(session.data.sessionToken, ...session.data.recoveryCodes);
  check(sql(`SELECT consumed_at_utc IS NOT NULL FROM c05_identity_mfa_reset_authorization WHERE account_id=UNHEX('${accountId(mailbox)}')`) === '1', 'successful reenrollment consumes its narrowly scoped authorization');
  const vetoCase = await api('assisted/request', { mailbox, automaticMethodsUnavailable: 'true' });
  const notice = await noticeFor(mailbox, 'ASSISTED_REQUESTED');
  check((await api('incidents/report', { token: notice })).status === 202 && sql(`SELECT blocked FROM c05_identity_assisted_case WHERE case_id=UUID_TO_BIN('${vetoCase.data.reference}',0)`) === '1',
    'owner fraud report from a preexisting channel blocks an active assisted case');
  for (const statement of ["UPDATE c05_platform_operator SET active=1", 'DELETE FROM c05_platform_identity_audit',
    "UPDATE c05_identity_assisted_review SET approved=1"]) {
    let denied = false; try { sql(statement, true); } catch { denied = true; }
    check(denied, 'API database role cannot provision operators or rewrite platform audit and identity reviews');
  }
  await subject.close(); for (const context of contexts) await context.close();
  return { mailbox, password: completionPassword, seed: enrollment.data.secret, backups: session.data.recoveryCodes };
}
