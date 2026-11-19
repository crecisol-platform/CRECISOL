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
    dialogsOpen: document.querySelectorAll('dialog:modal').length
  }));
  await page.screenshot({ path, fullPage: true, animations: 'disabled' });
  await writeFile(testInfo.outputPath(name + '.json'), JSON.stringify({
    id, sourceSha: BASELINE, synthetic: true, ...measurements,
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
    await capture(page, testInfo, prefix + '-modal-' + String(i + 1).padStart(2, '0') + '-' + label);
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
    await page.goto('/cuenta?vista=operadores');
    await expect(page.getByRole('heading', { name: 'Identidad de plataforma' })).toBeVisible();
    await capture(page, testInfo, 'operator-login-' + size.name);
    await page.getByRole('button', { name: 'Recuperar acceso de operador de plataforma' }).click();
    await capture(page, testInfo, 'operator-recovery-dialog-' + size.name);
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
      await captureModals(page, testInfo, 'account-' + view + '-' + size.name);
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
}
