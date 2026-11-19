/**
 * C05 / UX F01: SCREENSHOT BASELINE, NOT A CERTIFICATION.
 * Run exclusively against the pre-redesign base f61a79d242288c686a88a7c3dd711f2be9a48c18.
 * Everything shown here is synthetic and must be labeled as such.
 * No production accounts, real MFA QR, backup codes or personal data are used.
 */
import { test, expect, type Page, type TestInfo } from '@playwright/test';
import { writeFile } from 'node:fs/promises';

const BASELINE = 'f61a79d242288c686a88a7c3dd711f2be9a48c18';
const sizes = [
  { name: 'mobile-320', width: 320, height: 720 },
  { name: 'mobile-390', width: 390, height: 844 },
  { name: 'tablet-768', width: 768, height: 1024 },
  { name: 'desktop-1440', width: 1440, height: 900 }
] as const;

type DebugWindow = Window & {
  ng?: { getComponent(element: Element): any };
};

const slug = (value: string): string => value.normalize('NFD')
  .replace(/[\u0300-\u036f]/g, '').toLowerCase()
  .replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '').slice(0, 75);

async function capture(page: Page, testInfo: TestInfo, id: string): Promise<void> {
  const name = slug(id);
  const path = testInfo.outputPath(name + '.png');
  const measurements = await page.evaluate(() => ({
    route: location.pathname + location.search,
    bodyScrollX: Math.max(0, document.documentElement.scrollWidth - document.documentElement.clientWidth),
    visibleHeadings: [...document.querySelectorAll('h1,h2,h3')]
      .filter(x => x.getBoundingClientRect().height > 0)
      .map(x => x.textContent?.trim()).filter(Boolean).slice(0, 20),
    dialogsOpen: document.querySelectorAll('dialog:modal').length,
    dialog: (() => {
      const dialog = document.querySelector('dialog.cs-modal:modal');
      if (!dialog) return null;
      const rect = dialog.getBoundingClientRect();
      const footer = dialog.querySelector('.cs-modal__footer')?.getBoundingClientRect();
      return {
        scrollTop: dialog.scrollTop, scrollHeight: dialog.scrollHeight,
        clientHeight: dialog.clientHeight,
        horizontalOverflow: Math.max(0, dialog.scrollWidth - dialog.clientWidth),
        inViewport: rect.left >= -1 && rect.top >= -1 &&
          rect.right <= innerWidth + 1 && rect.bottom <= innerHeight + 1,
        footerInViewport: Boolean(footer && footer.top >= rect.top && footer.bottom <= rect.bottom)
      };
    })()
  }));
  await page.screenshot({ path, fullPage: measurements.dialogsOpen === 0, animations: 'disabled' });
  await writeFile(testInfo.outputPath(name + '.json'), JSON.stringify({
    id, sourceSha: BASELINE, runnerSha: process.env.GITHUB_SHA || 'local',
    synthetic: true, ...measurements,
    viewport: page.viewportSize(), screenshot: name + '.png',
    captureTimeUtc: new Date().toISOString()
  }, null, 2));
}

async function setup(page: Page, realm: 'anonymous' | 'account' | 'operator'): Promise<void> {
  await page.route('**/api/v1/public/branding/platform', route => route.fulfill({
    status: 200, contentType: 'application/json',
    body: JSON.stringify({
      revision: 1, name: 'CRECISOL', slogan: 'Tu negocio, a tu manera.',
      descriptor: 'Comercio · Servicios · Membresías', primaryColor: '#062E65',
      accentColor: '#078FE8', logoUrl: '/assets/crecisol-oficial.png',
      websiteUrl: null, publishedAt: '2026-11-19T00:00:00Z',
      presentation: { faviconUrl: '/assets/crecisol-emblem.png',
        typographyToken: 'system', iconSetToken: 'outline',
        seoTitle: 'CRECISOL', seoDescription: 'Entorno ficticio de pruebas',
        copyrightNotice: 'Entorno ficticio' }
    })
  }));
  const session = () => ({
    csrfToken: 'a'.repeat(64),
    expiresAtUtc: new Date(Date.now() + 30 * 60_000).toISOString(),
    idleMinutes: 30
  });
  await page.route('**/api/v1/identity/browser/restore', route => route.fulfill({
    status: realm === 'account' ? 200 : 401, contentType: 'application/json',
    body: realm === 'account' ? JSON.stringify(session()) : '{}'
  }));
  await page.route('**/api/v1/platform-identity/restore', route => route.fulfill({
    status: realm === 'operator' ? 200 : 401, contentType: 'application/json',
    body: realm === 'operator' ? JSON.stringify(session()) : '{}'
  }));
  await page.route('**/api/v1/platform-identity/session-policy', route => route.fulfill({
    status: 200, contentType: 'application/json',
    body: JSON.stringify({ idleMinutes: 30, revision: 1, canManage: true })
  }));
  await page.route('**/api/v1/identity/terms', route => route.fulfill({
    status: 200, contentType: 'application/json',
    body: JSON.stringify({
      version: 'qa-f01', hash: 'b'.repeat(64),
      text: 'Condiciones de entorno sintético. No constituyen contrato productivo.'
    })
  }));
}

async function seedAccount(page: Page): Promise<void> {
  await page.evaluate(() => {
    const el = document.querySelector('cs-identity-portal');
    const owner = el && (window as DebugWindow).ng?.getComponent(el);
    if (!owner) throw new Error('Angular debug component unavailable for synthetic F01 snapshots');
    owner.identity.profile.set({
      displayName: 'Persona ficticia', language: 'es-HN', timeZone: 'America/Tegucigalpa'
    });
    owner.identity.sessions.set([{
      id: '11111111-2222-4333-8444-555555555555', device: 'Chromium',
      factor: 'TOTP', lastSeenAtUtc: new Date().toISOString(),
      expiresAtUtc: new Date(Date.now() + 30 * 60_000).toISOString()
    }]);
    owner.identity.securityEvents.set([
      { action: 'login', outcome: 1, atUtc: new Date().toISOString() },
      { action: 'profile/read', outcome: 1, atUtc: new Date().toISOString() }
    ]);
    owner.identity.passkeys.set([{
      id: '33333333-4444-4555-8666-777777777777',
      createdAtUtc: new Date().toISOString()
    }]);
  });
}

async function seedOperator(page: Page): Promise<void> {
  await page.evaluate(() => {
    const el = document.querySelector('cs-platform-identity-panel');
    const owner = el && (window as DebugWindow).ng?.getComponent(el);
    if (!owner) throw new Error('Angular debug operator unavailable for synthetic F01 snapshots');
    owner.platform.profile.set({
      displayName: 'Operador ficticio', language: 'es-HN', timeZone: 'America/Tegucigalpa'
    });
    owner.platform.sessions.set([{
      id: 'aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee', device: 'Chromium',
      lastSeenAtUtc: new Date().toISOString(),
      expiresAtUtc: new Date(Date.now() + 30 * 60_000).toISOString()
    }]);
    owner.platform.abuse.set([
      { kind: 'Intentos de acceso rechazados', count: 3 },
      { kind: 'Revisión de factor', count: 1 }
    ]);
    owner.platform.sessionPolicy.set({ idleMinutes: 30, revision: 1, canManage: true });
  });
}

async function captureDialogBottom(page: Page, testInfo: TestInfo, id: string): Promise<void> {
  const dialog = page.locator('dialog.cs-modal:modal');
  if (await dialog.evaluate(element => element.scrollHeight > element.clientHeight + 6)) {
    await dialog.evaluate(element => { element.scrollTop = element.scrollHeight; });
    await capture(page, testInfo, id + '-scrolled-footer');
  }
}

async function captureModals(page: Page, testInfo: TestInfo, prefix: string): Promise<void> {
  const triggers = page.locator('button.institutional-task__trigger:visible');
  const count = await triggers.count();
  for (let i = 0; i < count; i++) {
    const trigger = triggers.nth(i);
    if (await trigger.isDisabled()) continue;
    const label = (await trigger.innerText()).trim() || String(i + 1);
    await trigger.click();
    const dialog = page.locator('dialog.cs-modal:modal');
    await expect(dialog).toHaveCount(1);
    const id = prefix + '-modal-' + String(i + 1).padStart(2, '0') + '-' + label;
    await capture(page, testInfo, id);
    await captureDialogBottom(page, testInfo, id);
    await dialog.locator('button.cs-modal__cancel').click();
    await expect(dialog).toHaveCount(0);
  }
}

for (const size of sizes) {
  test('F01 | public journeys ' + size.name, async ({ page }, testInfo) => {
    await page.setViewportSize({ width: size.width, height: size.height });
    await setup(page, 'anonymous');
    const views = ['ingresar', 'registro', 'confirmar', 'recuperar', 'invitacion', 'acceso-existente'];
    for (const view of views) {
      await page.goto('/cuenta?vista=' + view);
      await expect(page.locator('#account-content')).toBeVisible();
      await capture(page, testInfo, 'public-' + view + '-' + size.name);
    }
    await page.goto('/cuenta?vista=ingresar');
    await page.getByText('Entrar vinculando un navegador', { exact: true }).click();
    await capture(page, testInfo, 'public-pairing-disclosure-synthetic-' + size.name);
    const { default: pairingQr } = await import('qrcode');
    const fakePairingQr = await pairingQr.toDataURL('https://qa.example.test/c05/f01/pairing-NOT-VALID');
    await page.evaluate((qrData: string) => {
      const el = document.querySelector('cs-identity-portal')!;
      const owner = (window as DebugWindow).ng!.getComponent(el);
      owner.identity.pairing.set({
        token: 'AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA',
        fingerprint: 'AAAAAAAAAAAA', qr: qrData
      });
    }, fakePairingQr);
    await capture(page, testInfo, 'public-pairing-qr-FAKE-ONLY-' + size.name);

    await page.goto('/cuenta?vista=operadores');
    await expect(page.getByRole('heading', { name: 'Identidad de plataforma' })).toBeVisible();
    await capture(page, testInfo, 'operator-login-' + size.name);
    await page.getByRole('button', { name: 'Recuperar acceso de operador de plataforma' }).click();
    await capture(page, testInfo, 'operator-recovery-dialog-' + size.name);
    await captureDialogBottom(page, testInfo, 'operator-recovery-dialog-' + size.name);
  });

  test('F01 | authenticated business surfaces and 15 modal families ' + size.name,
    async ({ page }, testInfo) => {
    await page.setViewportSize({ width: size.width, height: size.height });
    await setup(page, 'account');
    for (const view of ['resumen', 'perfil', 'llaves', 'seguridad', 'administracion']) {
      await page.goto('/cuenta?vista=' + view);
      await expect(page.getByRole('navigation', { name: 'Mi cuenta' })).toBeVisible();
      await capture(page, testInfo, 'account-' + view + '-empty-' + size.name);
      await seedAccount(page);
      await capture(page, testInfo, 'account-' + view + '-populated-' + size.name);
      if (view === 'resumen') {
        await page.evaluate(() => {
          const el = document.querySelector('cs-identity-portal')!;
          const owner = (window as DebugWindow).ng!.getComponent(el);
          owner.identity.recoveryCodes.set(['AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA']);
        });
        await capture(page, testInfo, 'account-backup-codes-FAKE-ONLY-' + size.name);
        await page.evaluate(() => {
          const el = document.querySelector('cs-identity-portal')!;
          (window as DebugWindow).ng!.getComponent(el).identity.recoveryCodes.set([]);
        });
      }
      await captureModals(page, testInfo, 'account-' + view + '-' + size.name);
      if (view === 'seguridad') {
        await page.getByRole('button', { name: 'Cambiar contraseña', exact: true }).click();
        const dialog = page.getByRole('dialog', { name: 'Cambiar contraseña' });
        await page.evaluate(() => {
          const el = document.querySelector('cs-identity-portal')!;
          const owner = (window as DebugWindow).ng!.getComponent(el);
          owner.error.set('Solicitud rechazada. Revisa los datos sintéticos.');
        });
        await capture(page, testInfo, 'account-modal-error-synthetic-' + size.name);
        await page.evaluate(() => {
          const el = document.querySelector('cs-identity-portal')!;
          const owner = (window as DebugWindow).ng!.getComponent(el);
          owner.error.set('');
          owner.message.set('Solicitud de prueba procesada.');
        });
        await capture(page, testInfo, 'account-modal-feedback-synthetic-' + size.name);
        await dialog.getByRole('button', { name: 'Cerrar', exact: true }).click();
        await expect(dialog).toHaveCount(0);
      }
      if (view === 'resumen') {
        const revoke = page.getByRole('button', { name: /Revocar sesión / }).first();
        if (await revoke.count()) {
          await revoke.click();
          await capture(page, testInfo, 'account-revoke-single-session-' + size.name);
          await page.locator('dialog.cs-modal:modal button.cs-modal__cancel').click();
        }
      }
    }
  });

  test('F01 | platform console and 12 modal families ' + size.name,
    async ({ page }, testInfo) => {
    await page.setViewportSize({ width: size.width, height: size.height });
    await setup(page, 'operator');
    await page.goto('/cuenta?vista=operadores');
    await expect(page.getByRole('navigation', { name: 'Áreas de la consola de identidad' })).toBeVisible();
    await seedOperator(page);
    for (const tab of ['Casos', 'Sesiones', 'Perfil', 'Seguridad', 'Monitor']) {
      await page.getByRole('navigation', { name: 'Áreas de la consola de identidad' })
        .getByRole('button', { name: tab, exact: true }).click();
      await capture(page, testInfo, 'operator-' + tab + '-' + size.name);
      await captureModals(page, testInfo, 'operator-' + tab + '-' + size.name);
      if (tab === 'Sesiones') {
        const revoke = page.getByRole('button', { name: /Revocar sesión de operador / }).first();
        if (await revoke.count()) {
          await revoke.click();
          await capture(page, testInfo, 'operator-revoke-single-session-' + size.name);
          await page.locator('dialog.cs-modal:modal button.cs-modal__cancel').click();
        }
      }
    }
  });

  test('F01 | empty, loading, error and expiry simulation ' + size.name,
    async ({ page }, testInfo) => {
    await page.setViewportSize({ width: size.width, height: size.height });
    await setup(page, 'account');
    await page.goto('/cuenta?vista=resumen');
    await expect(page.getByRole('navigation', { name: 'Mi cuenta' })).toBeVisible();
    await page.evaluate(() => {
      const el = document.querySelector('cs-identity-portal')!;
      const owner = (window as DebugWindow).ng!.getComponent(el);
      owner.identity.restoring.set(true);
    });
    await capture(page, testInfo, 'account-loading-synthetic-' + size.name);
    await page.evaluate(() => {
      const el = document.querySelector('cs-identity-portal')!;
      const owner = (window as DebugWindow).ng!.getComponent(el);
      owner.identity.restoring.set(false);
      owner.error.set('No se pudo completar la solicitud. Inténtalo nuevamente.');
    });
    await capture(page, testInfo, 'account-error-synthetic-' + size.name);
    await page.evaluate(() => {
      const el = document.querySelector('cs-identity-portal')!;
      const owner = (window as DebugWindow).ng!.getComponent(el);
      owner.error.set('');
      owner.message.set('Solicitud recibida en el entorno ficticio.');
    });
    await capture(page, testInfo, 'account-success-synthetic-' + size.name);
    await page.evaluate(() => {
      const el = document.querySelector('cs-identity-portal')!;
      const owner = (window as DebugWindow).ng!.getComponent(el);
      owner.message.set('');
      owner.identity.clear();
    });
    await capture(page, testInfo, 'account-session-cleared-synthetic-' + size.name);
  });
  test('F01 | challenge and TOTP QR with synthetic enrollment only ' + size.name,
    async ({ page }, testInfo) => {
    await page.setViewportSize({ width: size.width, height: size.height });
    await setup(page, 'anonymous');
    await page.goto('/cuenta');
    await expect(page.getByRole('heading', { name: 'Iniciar sesión' })).toBeVisible();
    await page.evaluate(() => {
      const el = document.querySelector('cs-identity-portal')!;
      const owner = (window as DebugWindow).ng?.getComponent(el);
      if (!owner) throw new Error('Angular account debug component unavailable');
      owner.identity.stateValue.set('challenge');
    });
    await expect(page.getByRole('heading', { name: 'Verificación en dos pasos' })).toBeVisible();
    await capture(page, testInfo, 'account-mfa-challenge-synthetic-' + size.name);
    await page.evaluate(() => {
      const el = document.querySelector('cs-identity-portal')!;
      const owner = (window as DebugWindow).ng!.getComponent(el);
      owner.identity.enrollmentRequired.set(true);
    });
    await expect(page.getByRole('button', { name: 'Configurar segundo factor' })).toBeVisible();
    await capture(page, testInfo, 'account-mfa-enrollment-required-synthetic-' + size.name);
    const { default: QRCode } = await import('qrcode');
    const qr = await QRCode.toDataURL(
      'otpauth://totp/CRECISOL:qa-f01%40example.test?secret=AAAAAAAAAAAAAAAA' +
      '&issuer=CRECISOL&algorithm=SHA1&digits=6&period=30'
    );
    await page.evaluate((qrData: string) => {
      const el = document.querySelector('cs-identity-portal')!;
      const owner = (window as DebugWindow).ng!.getComponent(el);
      owner.identity.enrollmentSecret.set('AAAAAAAAAAAAAAAA');
      owner.identity.enrollmentQr.set(qrData);
    }, qr);
    await expect(page.getByRole('img', { name: /Código QR privado/ })).toBeVisible();
    await capture(page, testInfo, 'account-mfa-enrollment-qr-SYNTHETIC-ONLY-' + size.name);
    await page.getByText('No puedo escanear: mostrar clave manual').click();
    await capture(page, testInfo, 'account-mfa-enrollment-manual-fake-seed-' + size.name);

    await page.goto('/cuenta?vista=operadores');
    await page.evaluate(() => {
      const el = document.querySelector('cs-platform-identity-panel')!;
      const owner = (window as DebugWindow).ng!.getComponent(el);
      owner.platform.state.set('challenge');
    });
    await expect(page.getByLabel('Código MFA del operador')).toBeVisible();
    await capture(page, testInfo, 'operator-mfa-challenge-synthetic-' + size.name);
  });

  test('F01 | operator denial, error, empty and expired visual states ' + size.name,
    async ({ page }, testInfo) => {
    await page.setViewportSize({ width: size.width, height: size.height });
    await setup(page, 'operator');
    await page.goto('/cuenta?vista=operadores');
    const nav = page.getByRole('navigation', { name: 'Áreas de la consola de identidad' });
    await expect(nav).toBeVisible();
    await capture(page, testInfo, 'operator-cases-empty-synthetic-' + size.name);
    await page.evaluate(() => {
      const el = document.querySelector('cs-platform-identity-panel')!;
      (window as DebugWindow).ng!.getComponent(el).platform.restoring.set(true);
    });
    await capture(page, testInfo, 'operator-restoring-synthetic-' + size.name);
    await page.evaluate(() => {
      const el = document.querySelector('cs-platform-identity-panel')!;
      (window as DebugWindow).ng!.getComponent(el).platform.restoring.set(false);
    });
    await nav.getByRole('button', { name: 'Sesiones' }).click();
    await capture(page, testInfo, 'operator-sessions-empty-synthetic-' + size.name);
    await nav.getByRole('button', { name: 'Monitor' }).click();
    await capture(page, testInfo, 'operator-monitor-empty-synthetic-' + size.name);
    await nav.getByRole('button', { name: 'Seguridad' }).click();
    await expect(page.getByRole('button', { name: /Caducidad por inactividad/ })).toBeVisible();
    await page.evaluate(() => {
      const el = document.querySelector('cs-platform-identity-panel')!;
      (window as DebugWindow).ng!.getComponent(el).platform.recoveryCodes.set(['BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB']);
    });
    await capture(page, testInfo, 'operator-backup-codes-FAKE-ONLY-' + size.name);
    await page.evaluate(() => {
      const el = document.querySelector('cs-platform-identity-panel')!;
      (window as DebugWindow).ng!.getComponent(el).platform.recoveryCodes.set([]);
    });
    await page.getByRole('button', { name: 'Reemplazar MFA de operador', exact: true }).click();
    const operatorDialog = page.getByRole('dialog', { name: 'Reemplazar MFA de operador' });
    await expect(operatorDialog).toBeVisible();
    const { default: operatorQr } = await import('qrcode');
    const operatorFakeQr = await operatorQr.toDataURL(
      'otpauth://totp/CRECISOL:operator-f01%40example.test?secret=BBBBBBBBBBBBBBBB' +
      '&issuer=CRECISOL&algorithm=SHA1&digits=6&period=30'
    );
    await page.evaluate((fakeQr: string) => {
      const el = document.querySelector('cs-platform-identity-panel')!;
      const owner = (window as DebugWindow).ng!.getComponent(el);
      owner.platform.factorSecret.set('BBBBBBBBBBBBBBBB');
      owner.platform.factorQr.set(fakeQr);
    }, operatorFakeQr);
    await expect(operatorDialog.getByRole('img', { name: /QR privado para vincular/ })).toBeVisible();
    await capture(page, testInfo, 'operator-factor-enrollment-qr-FAKE-ONLY-' + size.name);
    await captureDialogBottom(page, testInfo, 'operator-factor-enrollment-qr-FAKE-ONLY-' + size.name);
    await operatorDialog.getByText('No puedo escanear: mostrar clave manual').click();
    await capture(page, testInfo, 'operator-factor-enrollment-manual-FAKE-ONLY-' + size.name);
    await operatorDialog.getByRole('button', { name: 'Cerrar', exact: true }).click();
    await expect(operatorDialog).toHaveCount(0);
    await page.evaluate(() => {
      const el = document.querySelector('cs-platform-identity-panel')!;
      const owner = (window as DebugWindow).ng!.getComponent(el);
      owner.platform.sessionPolicy.set({ idleMinutes: 30, revision: 1, canManage: false });
    });
    await capture(page, testInfo, 'operator-policy-readonly-synthetic-' + size.name);
    await page.evaluate(() => {
      const el = document.querySelector('cs-platform-identity-panel')!;
      const owner = (window as DebugWindow).ng!.getComponent(el);
      owner.error.set('No se pudo completar la solicitud. Revisa los datos.');
    });
    await capture(page, testInfo, 'operator-error-synthetic-' + size.name);
    await page.evaluate(() => {
      const el = document.querySelector('cs-platform-identity-panel')!;
      const owner = (window as DebugWindow).ng!.getComponent(el);
      owner.error.set('');
      owner.platform.clear();
    });
    await capture(page, testInfo, 'operator-session-expired-simulated-' + size.name);
  });

}
