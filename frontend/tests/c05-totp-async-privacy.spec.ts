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


test('B/D | two tenant enroll requests cannot publish an older QR out of order', async ({ page }) => {
  await page.route('**/api/v1/identity/browser/restore', route => route.fulfill({ status: 401, body: '{}' }));
  await page.route('**/api/v1/identity/login', route => route.fulfill({
    status: 200, contentType: 'application/json',
    body: JSON.stringify({ stepUpRequired: true, challenge: 'synthetic-c05-dual', enrollmentRequired: true })
  }));
  const newerSeed = 'GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ';
  const newerUri = uri.replace(SEED, newerSeed);
  let calls = 0, startFirst!: () => void, releaseFirst!: () => void;
  const firstSeen = new Promise<void>(resolve => { startFirst = resolve; });
  const firstHeld = new Promise<void>(resolve => { releaseFirst = resolve; });
  await page.route('**/api/v1/identity/mfa/enroll', async route => {
    if (++calls === 1) {
      startFirst(); await firstHeld;
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ secret: SEED, uri }) });
    } else {
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ secret: newerSeed, uri: newerUri }) });
    }
  });
  await page.goto('/cuenta');
  await expect.poll(() => page.evaluate(() => {
    const portal = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-identity-portal')!);
    return portal.identity.restoring();
  })).toBe(false);
  await page.evaluate(async () => {
    const portal = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-identity-portal')!);
    await portal.identity.beginLogin('synthetic@example.test', 'test-only-password');
    (window as unknown as { c05Tasks?: Promise<void>[] }).c05Tasks = [portal.identity.enrollMfa()];
  });
  await firstSeen;
  await page.evaluate(() => {
    const portal = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-identity-portal')!);
    (window as unknown as { c05Tasks?: Promise<void>[] }).c05Tasks!.push(portal.identity.enrollMfa());
  });
  await expect.poll(() => page.evaluate(() => {
    const portal = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-identity-portal')!);
    return portal.identity.enrollmentSecret();
  })).toBe(newerSeed);
  releaseFirst();
  await page.evaluate(async () => { await Promise.all((window as unknown as { c05Tasks?: Promise<void>[] }).c05Tasks!); });
  expect(await page.evaluate(() => {
    const portal = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-identity-portal')!);
    return { secret: portal.identity.enrollmentSecret(), hasQr: portal.identity.enrollmentQr().startsWith('data:image/png;base64,') };
  })).toEqual({ secret: newerSeed, hasQr: true });
});

test('B | tenant restoration cannot reauthenticate after local clear', async ({ page }) => {
  let start!: () => void, resume!: () => void;
  const seen = new Promise<void>(resolve => { start = resolve; });
  const held = new Promise<void>(resolve => { resume = resolve; });
  await page.route('**/api/v1/identity/browser/restore', async route => {
    start(); await held;
    await route.fulfill({ status: 200, contentType: 'application/json',
      body: JSON.stringify({ csrfToken: 'a'.repeat(64), expiresAtUtc: '2099-01-01T00:00:00Z' }) });
  });
  await page.goto('/cuenta');
  await seen;
  await page.evaluate(() => {
    const portal = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-identity-portal')!);
    portal.identity.clear();
  });
  resume();
  await expect.poll(() => page.evaluate(() => {
    const portal = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-identity-portal')!);
    return portal.identity.restoring();
  })).toBe(false);
  expect(await page.evaluate(() => {
    const portal = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-identity-portal')!);
    return portal.identity.state();
  })).toBe('anonymous');
});

test('B | operator confirmation cancelled during HTTP does not reveal backup codes', async ({ page }) => {
  await page.route('**/api/v1/platform-identity/restore', route => route.fulfill({ status: 401, body: '{}' }));
  let start!: () => void, resume!: () => void;
  const seen = new Promise<void>(resolve => { start = resolve; });
  const held = new Promise<void>(resolve => { resume = resolve; });
  await page.route('**/api/v1/platform-identity/factor/confirm', async route => {
    start(); await held;
    await route.fulfill({ status: 200, contentType: 'application/json',
      body: JSON.stringify({ recoveryCodes: ['synthetic-backup-not-real'] }) });
  });
  await page.goto('/cuenta?vista=operadores');
  await expect.poll(() => page.evaluate(() => {
    const panel = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-platform-identity-panel')!);
    return panel.platform.restoring();
  })).toBe(false);
  await page.evaluate(() => {
    const panel = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-platform-identity-panel')!);
    panel.platform.factorChallenge = 'synthetic-factor';
    (window as unknown as { c05Task?: Promise<void> }).c05Task = panel.platform.confirmFactor('123456');
  });
  await seen;
  await page.evaluate(() => {
    const panel = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-platform-identity-panel')!);
    panel.platform.clear();
  });
  resume();
  await page.evaluate(async () => { await (window as unknown as { c05Task?: Promise<void> }).c05Task; });
  expect(await page.evaluate(() => {
    const panel = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-platform-identity-panel')!);
    return panel.platform.recoveryCodes();
  })).toEqual([]);
});

test('B | recovery enrollment cancelled during HTTP does not reveal operator backup codes', async ({ page }) => {
  await page.route('**/api/v1/platform-identity/restore', route => route.fulfill({ status: 401, body: '{}' }));
  let start!: () => void, resume!: () => void;
  const seen = new Promise<void>(resolve => { start = resolve; });
  const held = new Promise<void>(resolve => { resume = resolve; });
  await page.route('**/api/v1/platform-identity/operator-recovery/enroll', async route => {
    start(); await held;
    await route.fulfill({ status: 200, contentType: 'application/json',
      body: JSON.stringify({ recoveryCodes: ['synthetic-backup-not-real'] }) });
  });
  await page.goto('/cuenta?vista=operadores');
  await expect.poll(() => page.evaluate(() => {
    const panel = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-platform-identity-panel')!);
    return panel.platform.restoring();
  })).toBe(false);
  await page.evaluate(() => {
    const panel = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-platform-identity-panel')!);
    panel.platform.recoveryCase = 'synthetic-case'; panel.platform.recoveryChallenge = 'synthetic-token';
    (window as unknown as { c05Task?: Promise<void> }).c05Task = panel.platform.confirmRecovery('123456');
  });
  await seen;
  await page.evaluate(() => {
    const panel = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-platform-identity-panel')!);
    panel.platform.discardRecoverySetup();
  });
  resume();
  await page.evaluate(async () => { await (window as unknown as { c05Task?: Promise<void> }).c05Task; });
  expect(await page.evaluate(() => {
    const panel = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-platform-identity-panel')!);
    return panel.platform.recoveryCodes();
  })).toEqual([]);
});

test('B | late operator recovery share is never restored after logout', async ({ page }) => {
  await page.route('**/api/v1/platform-identity/restore', route => route.fulfill({ status: 401, body: '{}' }));
  let start!: () => void, resume!: () => void;
  const seen = new Promise<void>(resolve => { start = resolve; });
  const held = new Promise<void>(resolve => { resume = resolve; });
  await page.route('**/api/v1/platform-identity/release-share', async route => {
    start(); await held;
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ share: 'synthetic-share' }) });
  });
  await page.goto('/cuenta?vista=operadores');
  await expect.poll(() => page.evaluate(() => {
    const panel = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-platform-identity-panel')!);
    return panel.platform.restoring();
  })).toBe(false);
  await page.evaluate(() => {
    const panel = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-platform-identity-panel')!);
    panel.platform.state.set('authenticated');
    (window as unknown as { c05Task?: Promise<void> }).c05Task = panel.platform.releaseShare('synthetic-case', '123456');
  });
  await seen;
  await page.evaluate(() => {
    const panel = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-platform-identity-panel')!);
    panel.platform.clear();
  });
  resume();
  await page.evaluate(async () => { await (window as unknown as { c05Task?: Promise<void> }).c05Task; });
  expect(await page.evaluate(() => {
    const panel = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-platform-identity-panel')!);
    return panel.platform.handoffShare();
  })).toBe('');
});


test('B | a late pairing-start response cannot recreate a cleared private key or QR', async ({ page }) => {
  await page.route('**/api/v1/identity/browser/restore', route => route.fulfill({ status: 401, body: '{}' }));
  let start!: () => void, resume!: () => void;
  const seen = new Promise<void>(resolve => { start = resolve; });
  const held = new Promise<void>(resolve => { resume = resolve; });
  await page.route('**/api/v1/identity/pairings/start', async route => {
    start(); await held;
    await route.fulfill({ status: 200, contentType: 'application/json',
      body: JSON.stringify({ token: 'synthetic-pair', nonce: 'synthetic-nonce', fingerprint: 'synthetic-fingerprint' }) });
  });
  await page.goto('/cuenta');
  await expect.poll(() => page.evaluate(() => {
    const portal = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-identity-portal')!);
    return portal.identity.restoring();
  })).toBe(false);
  await page.evaluate(() => {
    const portal = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-identity-portal')!);
    (window as unknown as { c05Task?: Promise<void> }).c05Task = portal.identity.startPairing();
  });
  await seen;
  await page.evaluate(() => {
    const portal = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-identity-portal')!);
    portal.identity.clear();
  });
  resume();
  await page.evaluate(async () => { await (window as unknown as { c05Task?: Promise<void> }).c05Task; });
  expect(await page.evaluate(() => {
    const portal = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-identity-portal')!);
    return { pairing: portal.identity.pairing(), privateKeyPresent: portal.identity.pairingKey !== null };
  })).toEqual({ pairing: null, privateKeyPresent: false });
});

test('B | late pairing completion cannot reauthenticate a closed tenant session', async ({ page }) => {
  await page.route('**/api/v1/identity/browser/restore', route => route.fulfill({ status: 401, body: '{}' }));
  let start!: () => void, resume!: () => void;
  const seen = new Promise<void>(resolve => { start = resolve; });
  const held = new Promise<void>(resolve => { resume = resolve; });
  await page.route('**/api/v1/identity/pairings/complete', async route => {
    start(); await held;
    await route.fulfill({ status: 200, contentType: 'application/json',
      body: JSON.stringify({ csrfToken: 'c'.repeat(64), expiresAtUtc: '2099-01-01T00:00:00Z' }) });
  });
  await page.goto('/cuenta');
  await expect.poll(() => page.evaluate(() => {
    const portal = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-identity-portal')!);
    return portal.identity.restoring();
  })).toBe(false);
  await page.evaluate(async () => {
    const portal = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-identity-portal')!);
    const key = await crypto.subtle.generateKey({ name: 'ECDSA', namedCurve: 'P-256' }, false, ['sign', 'verify']);
    portal.identity.pairingKey = key.privateKey;
    portal.identity.pairingNonce = 'synthetic-nonce';
    portal.identity.pairing.set({ token: 'synthetic-pair', fingerprint: 'synthetic-fingerprint', qr: 'synthetic-qr' });
    (window as unknown as { c05Task?: Promise<void> }).c05Task = portal.identity.completePairing();
  });
  await seen;
  await page.evaluate(() => {
    const portal = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-identity-portal')!);
    portal.identity.clear();
  });
  resume();
  await page.evaluate(async () => { await (window as unknown as { c05Task?: Promise<void> }).c05Task; });
  expect(await page.evaluate(() => {
    const portal = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-identity-portal')!);
    return { state: portal.identity.state(), pairing: portal.identity.pairing() };
  })).toEqual({ state: 'anonymous', pairing: null });
});

test('B | a late operator case read cannot reveal case metadata after local logout', async ({ page }) => {
  await page.route('**/api/v1/platform-identity/restore', route => route.fulfill({ status: 401, body: '{}' }));
  let start!: () => void, resume!: () => void;
  const seen = new Promise<void>(resolve => { start = resolve; });
  const held = new Promise<void>(resolve => { resume = resolve; });
  await page.route('**/api/v1/platform-identity/case', async route => {
    start(); await held;
    await route.fulfill({ status: 200, contentType: 'application/json',
      body: JSON.stringify({ caseId: 'synthetic-case', readyAtUtc: null, blocked: false, consumed: false }) });
  });
  await page.goto('/cuenta?vista=operadores');
  await expect.poll(() => page.evaluate(() => {
    const panel = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-platform-identity-panel')!);
    return panel.platform.restoring();
  })).toBe(false);
  await page.evaluate(() => {
    const panel = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-platform-identity-panel')!);
    panel.platform.state.set('authenticated');
    (window as unknown as { c05Task?: Promise<void> }).c05Task = panel.platform.lookup('synthetic-case');
  });
  await seen;
  await page.evaluate(() => {
    const panel = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-platform-identity-panel')!);
    panel.platform.clear();
  });
  resume();
  await page.evaluate(async () => { await (window as unknown as { c05Task?: Promise<void> }).c05Task; });
  expect(await page.evaluate(() => {
    const panel = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-platform-identity-panel')!);
    return panel.platform.case();
  })).toBe(null);
});


test('B | assisted recovery reference must be removed on session teardown', async ({ page }) => {
  await page.route('**/api/v1/identity/browser/restore', route => route.fulfill({ status: 401, body: '{}' }));
  await page.goto('/cuenta');
  await expect.poll(() => page.evaluate(() => {
    const portal = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-identity-portal')!);
    return portal.identity.restoring();
  })).toBe(false);
  expect(await page.evaluate(() => {
    const portal = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-identity-portal')!);
    portal.identity.assistedReference.set('synthetic-private-case-reference');
    portal.identity.clear();
    return portal.identity.assistedReference();
  })).toBe('');
});


test('B | late tenant session 401 cannot clear a newer authenticated session', async ({ page }) => {
  await page.route('**/api/v1/identity/browser/restore', route => route.fulfill({ status: 401, body: '{}' }));
  let received!: () => void, release!: () => void;
  const requestSeen = new Promise<void>(resolve => { received = resolve; });
  const held = new Promise<void>(resolve => { release = resolve; });
  await page.route('**/api/v1/identity/sessions', async route => {
    received(); await held;
    await route.fulfill({ status: 401, contentType: 'application/json', body: '{}' });
  });
  await page.goto('/cuenta');
  await expect.poll(() => page.evaluate(() => {
    const portal = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-identity-portal')!);
    return portal.identity.restoring();
  })).toBe(false);
  await page.evaluate(() => {
    const api = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-identity-portal')!).identity;
    api.stateValue.set('authenticated');
    api.csrfValue = 'a'.repeat(64);
    (window as unknown as { c05Task?: Promise<void> }).c05Task = api.refreshSessions().catch(() => {});
  });
  await requestSeen;
  await page.evaluate(() => {
    const api = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-identity-portal')!).identity;
    api.clear();  // Old request belongs to a retired session.
    api.csrfValue = 'b'.repeat(64);
    api.stateValue.set('authenticated');  // A different, newly established synthetic session.
  });
  release();
  await page.evaluate(async () => { await (window as unknown as { c05Task?: Promise<void> }).c05Task; });
  expect(await page.evaluate(() => {
    const api = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-identity-portal')!).identity;
    return { state: api.state(), csrf: api.csrfValue };
  })).toEqual({ state: 'authenticated', csrf: 'b'.repeat(64) });
});

test('B | late operator session 401 cannot clear a newer authenticated session', async ({ page }) => {
  await page.route('**/api/v1/platform-identity/restore', route => route.fulfill({ status: 401, body: '{}' }));
  let received!: () => void, release!: () => void;
  const requestSeen = new Promise<void>(resolve => { received = resolve; });
  const held = new Promise<void>(resolve => { release = resolve; });
  await page.route('**/api/v1/platform-identity/sessions', async route => {
    received(); await held;
    await route.fulfill({ status: 401, contentType: 'application/json', body: '{}' });
  });
  await page.goto('/cuenta?vista=operadores');
  await expect.poll(() => page.evaluate(() => {
    const panel = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-platform-identity-panel')!);
    return panel.platform.restoring();
  })).toBe(false);
  await page.evaluate(() => {
    const api = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-platform-identity-panel')!).platform;
    api.state.set('authenticated');
    api.csrf = 'a'.repeat(64);
    (window as unknown as { c05Task?: Promise<void> }).c05Task = api.loadSessions().catch(() => {});
  });
  await requestSeen;
  await page.evaluate(() => {
    const api = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-platform-identity-panel')!).platform;
    api.clear();
    api.csrf = 'b'.repeat(64);
    api.state.set('authenticated');
  });
  release();
  await page.evaluate(async () => { await (window as unknown as { c05Task?: Promise<void> }).c05Task; });
  expect(await page.evaluate(() => {
    const api = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-platform-identity-panel')!).platform;
    return { state: api.state(), csrf: api.csrf };
  })).toEqual({ state: 'authenticated', csrf: 'b'.repeat(64) });
});

test('B | overlapping pairing attempts cannot publish a stale QR with a newer signing key', async ({ page }) => {
  await page.route('**/api/v1/identity/browser/restore', route => route.fulfill({ status: 401, body: '{}' }));
  let received!: () => void, release!: () => void;
  const requestSeen = new Promise<void>(resolve => { received = resolve; });
  const held = new Promise<void>(resolve => { release = resolve; });
  let call = 0;
  await page.route('**/api/v1/identity/pairings/start', async route => {
    if (++call === 1) {
      received(); await held;
      await route.fulfill({ status: 200, contentType: 'application/json',
        body: JSON.stringify({ token: 'old-pair', nonce: 'old-nonce', fingerprint: 'old-fingerprint' }) });
    } else {
      await route.fulfill({ status: 200, contentType: 'application/json',
        body: JSON.stringify({ token: 'new-pair', nonce: 'new-nonce', fingerprint: 'new-fingerprint' }) });
    }
  });
  await page.goto('/cuenta');
  await expect.poll(() => page.evaluate(() => {
    const portal = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-identity-portal')!);
    return portal.identity.restoring();
  })).toBe(false);
  await page.evaluate(() => {
    const api = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-identity-portal')!).identity;
    (window as unknown as { c05Tasks?: Promise<void>[] }).c05Tasks = [api.startPairing()];
  });
  await requestSeen;
  await page.evaluate(() => {
    const api = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-identity-portal')!).identity;
    (window as unknown as { c05Tasks?: Promise<void>[] }).c05Tasks!.push(api.startPairing());
  });
  await expect.poll(() => page.evaluate(() => {
    const api = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-identity-portal')!).identity;
    return api.pairing()?.token ?? '';
  })).toBe('new-pair');
  release();
  await page.evaluate(async () => { await Promise.all((window as unknown as { c05Tasks?: Promise<void>[] }).c05Tasks!); });
  expect(await page.evaluate(() => {
    const api = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-identity-portal')!).identity;
    return { token: api.pairing()?.token ?? '', hasSigningKey: api.pairingKey !== null,
      qrIsPng: api.pairing()?.qr.startsWith('data:image/png;base64,') ?? false };
  })).toEqual({ token: 'new-pair', hasSigningKey: true, qrIsPng: true });
});


test('B/D | C05 30-minute server-bound activity renewal needs real user input and server response', async ({ page }) => {
  const expiry = new Date(Date.now() + 30 * 60_000).toISOString();
  let keepalives = 0, deny = false, logouts = 0;
  await page.route('**/api/v1/identity/browser/restore', route => route.fulfill({
    status: 200, contentType: 'application/json',
    body: JSON.stringify({ csrfToken: 'a'.repeat(64), expiresAtUtc: expiry, idleMinutes: 30 })
  }));
  await page.route('**/api/v1/identity/sessions/keepalive', route => {
    keepalives += 1;
    return route.fulfill(deny ? { status: 401, contentType: 'application/json', body: '{}' } : {
      status: 200, contentType: 'application/json',
      headers: { 'X-Crecisol-Session-Expires-At': new Date(Date.now() + 30 * 60_000).toISOString(),
        'X-Crecisol-Idle-Minutes': '30' },
      body: JSON.stringify({ idleMinutes: 30 })
    });
  });
  await page.route('**/api/v1/identity/logout', route => { logouts += 1; return route.fulfill({ status: 200, body: '{}' }); });
  await page.goto('/cuenta');
  await expect.poll(() => page.evaluate(() => {
    const portal = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-identity-portal')!);
    return portal.identity.restoring();
  })).toBe(false);
  expect(await page.evaluate(() => {
    const api = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-identity-portal')!).identity;
    return { state: api.state(), minutes: api.idleMinutes() };
  })).toEqual({ state: 'authenticated', minutes: 30 });
  // A synthetic, untrusted event must not generate a heartbeat.
  await page.evaluate(() => {
    document.dispatchEvent(new Event('pointerdown', { bubbles: true }));
  });
  expect(keepalives).toBe(0);
  await page.evaluate(() => {
    const api = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-identity-portal')!).identity;
    api.activityLastSent = Date.now() - 61_000;
  });
  await page.locator('body').click({ position: { x: 4, y: 4 } });
  await expect.poll(() => keepalives).toBe(1);
  // Even while authenticated, local timeout must NOT revoke another active
  // tab's shared cookie; only the server can reject that credential.
  await page.evaluate(() => {
    const api = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-identity-portal')!).identity;
    api.sessionWindow.fire();
  });
  expect(logouts).toBe(0);
  expect(await page.evaluate(() => {
    const api = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-identity-portal')!).identity;
    return api.state();
  })).toBe('anonymous');
  // Restore tests do not grant network authority after a simulated server
  // rejection. A fresh synthetic UI session is explicitly test-only.
  await page.evaluate(() => {
    const api = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-identity-portal')!).identity;
    api.stateValue.set('authenticated');
    api.csrfValue = 'a'.repeat(64);
    api.activityLastSent = Date.now() - 61_000;
  });
  deny = true;
  await page.locator('body').click({ position: { x: 4, y: 4 } });
  await expect.poll(() => keepalives).toBe(2);
  await expect.poll(() => page.evaluate(() => {
    const api = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-identity-portal')!).identity;
    return api.state();
  })).toBe('anonymous');
});

test('A/D | operator policy modal is read-only without grant and requires MFA when editable', async ({ page }) => {
  await page.route('**/api/v1/platform-identity/restore', route => route.fulfill({ status: 401, body: '{}' }));
  await page.goto('/cuenta?vista=operadores');
  await expect.poll(() => page.evaluate(() => {
    const panel = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-platform-identity-panel')!);
    return panel.platform.restoring();
  })).toBe(false);
  await page.evaluate(() => {
    const panel = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-platform-identity-panel')!);
    panel.platform.state.set('authenticated');
    panel.platform.sessionPolicy.set({ idleMinutes: 30, revision: 1, canManage: false });
    panel.panel.set('seguridad');
  });
  await page.getByRole('button', { name: 'Caducidad por inactividad de sesiones' }).click();
  const modal = page.getByRole('dialog', { name: 'Vencimiento de sesiones por inactividad' });
  await expect(modal).toBeVisible();
  await expect(modal.getByText('30 minutos sin actividad')).toBeVisible();
  await expect(modal.getByLabel('Minutos de inactividad permitidos (5 a 120)')).toHaveCount(0);
  await page.evaluate(() => {
    const panel = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-platform-identity-panel')!);
    panel.platform.sessionPolicy.set({ idleMinutes: 30, revision: 1, canManage: true });
  });
  await expect(modal.getByLabel('Minutos de inactividad permitidos (5 a 120)')).toBeVisible();
  await expect(modal.getByLabel('Código MFA del administrador para guardar el cambio')).toBeVisible();
  await expect(modal.getByRole('button', { name: 'Guardar tiempo de inactividad' })).toBeVisible();
});
