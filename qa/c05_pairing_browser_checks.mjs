import { createRequire } from 'node:module';
const require = createRequire(new URL('../frontend/package.json', import.meta.url));
const jsQR = require('jsqr');

export async function verifyPairing({ browser, api, check, confidential, mailbox, password, backups, uiPort, sql }) {
  const requester = await browser.newContext(); const target = await requester.newPage();
  await target.goto(`http://127.0.0.1:${uiPort}/cuenta`);
  await target.getByText('Entrar vinculando un navegador', { exact: true }).click();
  const started = target.waitForResponse(r => r.url().endsWith('/pairings/start'));
  await target.getByRole('button', { name: 'Crear solicitud de vinculación', exact: true }).click();
  check((await started).status() === 200, 'uncached Angular starts a real cryptographic browser-pairing request');
  const token = await target.getByLabel('Solicitud de vinculación', { exact: true }).innerText();
  const fingerprint = await target.getByLabel('Huella de vinculación', { exact: true }).innerText();
  confidential.push(token);
  const pixels = await target.getByRole('img', { name: /QR de solicitud/ }).evaluate(async img => {
    await img.decode(); const canvas = document.createElement('canvas'); canvas.width = img.width; canvas.height = img.height;
    const context = canvas.getContext('2d'); context.drawImage(img, 0, 0);
    return { width: canvas.width, height: canvas.height, data: [...context.getImageData(0, 0, canvas.width, canvas.height).data] };
  });
  const qr = jsQR(new Uint8ClampedArray(pixels.data), pixels.width, pixels.height);
  check(qr?.data === `http://127.0.0.1:${uiPort}/cuenta#pair=${token}.${fingerprint}`,
    'rendered QR pixels decode to the exact short-lived request and public-key fingerprint, without a session secret');
  check((await api('pairings/complete', { token, signature: '00'.repeat(64) })).status === 401,
    'a QR request alone cannot create a session before approval');
  const approverContext = await browser.newContext(); const page = await approverContext.newPage();
  await page.goto(qr.data);
  await page.getByRole('region', { name: 'Iniciar sesión', exact: true }).getByLabel('Correo electrónico', { exact: true }).fill(mailbox);
  await page.getByLabel('Contraseña', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Continuar', exact: true }).click();
  await page.getByLabel('Código de tu aplicación de autenticación').fill(backups.pop());
  await page.getByRole('button', { name: 'Verificar', exact: true }).click();
  await page.getByRole('heading', { name: 'Estado de la sesión', exact: true }).waitFor();
  await page.locator('details.identity-summary-extras > summary').click();
  await page.getByRole('button', { name: 'Aprobar otro navegador', exact: true }).click();
  check(await page.getByLabel('Solicitud del navegador nuevo', { exact: true }).inputValue() === token &&
    await page.getByLabel('Huella del navegador nuevo', { exact: true }).inputValue() === fingerprint,
    'QR deep link only prefills the explicit approval form after authenticated login');
  await page.getByLabel('Código MFA para aprobar navegador', { exact: true }).fill(backups.pop());
  await page.getByLabel('Comparé la huella y autorizo este navegador', { exact: true }).check();
  const approval = page.waitForResponse(r => r.url().endsWith('/pairings/approve'));
  await page.getByRole('button', { name: 'Aprobar vinculación', exact: true }).click();
  check((await approval).status() === 200, 'authenticated second browser explicitly approves the QR request with fresh MFA');
  check((await api('pairings/complete', { token, signature: '00'.repeat(64) })).status === 401,
    'approved QR still cannot create a session without the initiating browser private-key signature');
  check((await api('pairings/complete', { token, signature: '00'.repeat(64) }, undefined, 'tenant-b.example.test')).status === 401,
    'pairing approval cannot authorize another tenant');
  const completed = target.waitForResponse(r => r.url().endsWith('/pairings/complete'));
  await target.getByRole('button', { name: 'Completar vinculación aprobada', exact: true }).click();
  check((await completed).status() === 200, 'initiating browser proves P-256 private-key possession and receives an HttpOnly session');
  await target.getByRole('heading', { name: 'Estado de la sesión', exact: true }).waitFor();
  const pairedToken = (await requester.cookies()).find(c => c.name === '__Host-crecisol-c05').value; confidential.push(pairedToken);
  check((await api('sessions', {}, pairedToken)).status === 200, 'paired session is persisted and revocable in the same tenant');
  check((await api('pairings/complete', { token, signature: '00'.repeat(64) })).status === 401,
    'consumed pairing cannot create a second session');
  // The successful browser approval intentionally leaves its institutional dialog
  // visible for review. Dismiss it through the user's keyboard path before
  // navigating to the separate session-revocation action (no forced clicks).
  await page.keyboard.press('Escape');
  await page.getByRole('dialog', { name: 'Aprobar otro navegador' }).waitFor({ state: 'hidden' });
  await page.getByRole('button', { name: 'Revocar todas mis sesiones', exact: true }).click();
  await page.getByLabel('Código MFA para cerrar todas las sesiones').fill(backups.pop());
  const revoked = page.waitForResponse(r => r.url().endsWith('/sessions/revoke-all'));
  await page.getByRole('button', { name: 'Cerrar todas mis sesiones', exact: true }).click();
  check((await revoked).status() === 200 && (await api('sessions', {}, pairedToken)).status === 401,
    'global own-session revocation from Angular invalidates the independently paired browser');
  const login = await api('login', { mailbox, password });
  const restored = await api('mfa/verify', { challenge: login.data.challenge, code: backups.pop() });
  check(restored.status === 200, 'owner can authenticate again after global session revocation');
  confidential.push(restored.data.sessionToken);
  await requester.close(); await approverContext.close();
  return restored.data.sessionToken;
}
