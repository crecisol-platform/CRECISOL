import { expect, test } from '@playwright/test';

// Test-only credential. No user/tenant/operator secrets are embedded in the suite.
const SEED = 'JBSWY3DPEHPK3PXPJBSWY3DPEHPK3PXP';
const uri = 'otpauth://totp/CRECISOL%3Acuenta-01234567?secret=' + SEED +
  '&issuer=CRECISOL&algorithm=SHA1&digits=6&period=30';

type AngularDebug = { ng: { getComponent: (element: Element) => any } };
test('A/B/D | a late tenant SHA1 enrollment response cannot resurrect a cleared QR', async ({ page }) => {
  await page.route('**/api/v1/identity/browser/restore', route => route.fulfill({ status: 401, body: '{}' }));
  await page.route('**/api/v1/identity/login', route => route.fulfill({
    status: 200, contentType: 'application/json',
    body: JSON.stringify({ stepUpRequired: true, challenge: 'synthetic-c05-challenge', enrollmentRequired: true })
  }));
  let started!: () => void;
  let resume!: () => void;
  const seen = new Promise<void>(resolve => { started = resolve; });
  const held = new Promise<void>(resolve => { resume = resolve; });
  await page.route('**/api/v1/identity/mfa/enroll', async route => {
    started(); await held;
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ secret: SEED, uri }) });
  });
  await page.goto('/cuenta');
  await expect.poll(() => page.evaluate(() => {
    const portal = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-identity-portal')!);
    return portal.identity.restoring();
  })).toBe(false);
  await page.evaluate(async () => {
    const el = document.querySelector('cs-identity-portal');
    const portal = (window as unknown as AngularDebug).ng.getComponent(el!);
    await portal.identity.beginLogin('synthetic@example.test', 'test-only-password');
    (window as unknown as { c05MfaTask?: Promise<void> }).c05MfaTask = portal.identity.enrollMfa();
  });
  await seen;
  await page.evaluate(() => {
    const portal = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-identity-portal')!);
    portal.identity.clear(); // Simulates challenge timeout/forced logout during HTTP.
  });
  resume();
  await page.evaluate(async () => { await (window as unknown as { c05MfaTask?: Promise<void> }).c05MfaTask; });
  expect(await page.evaluate(() => {
    const portal = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-identity-portal')!);
    return { state: portal.identity.state(), seed: portal.identity.enrollmentSecret(), qr: portal.identity.enrollmentQr() };
  })).toEqual({ state: 'anonymous', seed: '', qr: '' });
});

test('B/D | closed recovery modal never restores a late operator MFA QR', async ({ page }) => {
  await page.route('**/api/v1/platform-identity/restore', route => route.fulfill({ status: 401, body: '{}' }));
  let started!: () => void;
  let resume!: () => void;
  const seen = new Promise<void>(resolve => { started = resolve; });
  const held = new Promise<void>(resolve => { resume = resolve; });
  await page.route('**/api/v1/platform-identity/operator-recovery/complete', async route => {
    started(); await held;
    await route.fulfill({ status: 200, contentType: 'application/json',
      body: JSON.stringify({ challenge: 'synthetic-recovery', secret: SEED, uri }) });
  });
  await page.goto('/cuenta?vista=operadores');
  await expect.poll(() => page.evaluate(() => {
    const panel = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-platform-identity-panel')!);
    return panel.platform.restoring();
  })).toBe(false);
  await page.evaluate(() => {
    const panel = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-platform-identity-panel')!);
    (window as unknown as { c05MfaTask?: Promise<void> }).c05MfaTask =
      panel.platform.completeRecovery('synthetic-case', 'A', 'B', 'synthetic-password');
  });
  await seen;
  await page.evaluate(() => {
    const panel = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-platform-identity-panel')!);
    panel.platform.discardRecoverySetup();
  });
  resume();
  await page.evaluate(async () => { await (window as unknown as { c05MfaTask?: Promise<void> }).c05MfaTask; });
  expect(await page.evaluate(() => {
    const panel = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-platform-identity-panel')!);
    return { seed: panel.platform.operatorRecoverySeed(), qr: panel.platform.operatorRecoveryQr() };
  })).toEqual({ seed: '', qr: '' });
});

test('B/D | factor replacement cannot reveal a QR after logout', async ({ page }) => {
  await page.route('**/api/v1/platform-identity/restore', route => route.fulfill({ status: 401, body: '{}' }));
  let started!: () => void;
  let resume!: () => void;
  const seen = new Promise<void>(resolve => { started = resolve; });
  const held = new Promise<void>(resolve => { resume = resolve; });
  await page.route('**/api/v1/platform-identity/factor/replace', async route => {
    started(); await held;
    await route.fulfill({ status: 200, contentType: 'application/json',
      body: JSON.stringify({ challenge: 'synthetic-factor', secret: SEED, uri }) });
  });
  await page.goto('/cuenta?vista=operadores');
  await expect.poll(() => page.evaluate(() => {
    const panel = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-platform-identity-panel')!);
    return panel.platform.restoring();
  })).toBe(false);
  await page.evaluate(() => {
    const panel = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-platform-identity-panel')!);
    panel.platform.state.set('authenticated'); // Synthetic state only; API still mocked.
    (window as unknown as { c05MfaTask?: Promise<void> }).c05MfaTask =
      panel.platform.replaceFactor('synthetic-password', '123456');
  });
  await seen;
  await page.evaluate(() => {
    const panel = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-platform-identity-panel')!);
    panel.platform.clear(); // Revocation/privacy teardown before the response.
  });
  resume();
  await page.evaluate(async () => { await (window as unknown as { c05MfaTask?: Promise<void> }).c05MfaTask; });
  expect(await page.evaluate(() => {
    const panel = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-platform-identity-panel')!);
    return { seed: panel.platform.factorSecret(), qr: panel.platform.factorQr() };
  })).toEqual({ seed: '', qr: '' });
});
