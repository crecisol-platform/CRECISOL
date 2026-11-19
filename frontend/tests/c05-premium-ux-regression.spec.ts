import { expect, test } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

type AngularDebug = { ng: { getComponent: (element: Element) => any } };

async function mockAccountRestore(page: import('@playwright/test').Page): Promise<void> {
  await page.route('**/api/v1/identity/browser/restore', route => route.fulfill({
    status: 200,
    contentType: 'application/json',
    headers: { 'cache-control': 'no-store' },
    body: JSON.stringify({
      csrfToken: 'a'.repeat(64),
      expiresAtUtc: new Date(Date.now() + 29 * 60_000).toISOString(),
      idleMinutes: 30
    })
  }));
}

test('C05 UX | secure modal scroll stays contained at 320px, focus returns on close', async ({ page }, testInfo) => {
  await page.setViewportSize({ width: 320, height: 640 });
  await page.goto('/cuenta?vista=operadores');
  const opener = page.getByRole('button', { name: 'Recuperar acceso de operador de plataforma', exact: true });
  await opener.click();
  const modal = page.getByRole('dialog', { name: 'Recuperación segura de operador' });
  await expect(modal).toBeVisible();
  await expect(page.locator('body')).toHaveCSS('overflow', 'hidden');
  expect(await modal.evaluate(dialog => {
    const root = dialog.getBoundingClientRect();
    const scrollRegion = dialog.querySelector('.cs-modal__content')!;
    const content = scrollRegion.getBoundingClientRect();
    const header = dialog.querySelector('.cs-modal__header')!.getBoundingClientRect();
    const footer = dialog.querySelector('.cs-modal__footer')!.getBoundingClientRect();
    return root.left >= -1 && root.right <= innerWidth + 1 &&
      root.top >= -1 && root.bottom <= innerHeight + 1 &&
      content.top >= header.bottom - 1 && content.bottom <= footer.top + 1 &&
      scrollRegion.scrollHeight > scrollRegion.clientHeight;
  })).toBe(true);
  const a11y = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa']).analyze();
  expect(a11y.violations.filter(v => ['serious', 'critical'].includes(v.impact ?? ''))).toEqual([]);
  await page.screenshot({ path: testInfo.outputPath('c05-premium-modal-mobile.png') });
  await page.keyboard.press('Escape');
  await expect(opener).toBeFocused();
  await expect(page.locator('body')).not.toHaveCSS('overflow', 'hidden');
});

test('C05 UX | account security history and sessions hide raw technical IDs', async ({ page }, testInfo) => {
  await mockAccountRestore(page);
  await page.goto('/cuenta?vista=perfil');
  await expect(page.getByRole('navigation', { name: 'Mi cuenta' })).toBeVisible();
  await page.evaluate(() => {
    const portal = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-identity-portal')!);
    portal.identity.profile.set({ displayName: 'Perfil de prueba', language: 'es-HN', timeZone: 'America/Tegucigalpa' });
    portal.identity.securityEvents.set([
      { action: 'profile/read', outcome: 1, atUtc: new Date().toISOString() },
      { action: 'login', outcome: 0, atUtc: new Date().toISOString() }
    ]);
  });
  const timeline = page.getByRole('region', { name: 'Historial de seguridad' });
  await expect(timeline.getByText('Consulta de perfil')).toBeVisible();
  await expect(timeline.getByText('Inicio de sesión')).toBeVisible();
  await expect(timeline).not.toContainText('profile/read');
  await expect(timeline).not.toContainText('resultado 1');
  await page.screenshot({ path: testInfo.outputPath('c05-premium-activity-desktop.png') });

  await page.getByRole('link', { name: 'Resumen', exact: true }).click();
  await page.evaluate(() => {
    const portal = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-identity-portal')!);
    portal.identity.sessions.set([{
      id: '11111111-2222-4333-8444-555555555555',
      device: 'Chromium',
      factor: 'TOTP',
      lastSeenAtUtc: new Date().toISOString(),
      expiresAtUtc: new Date(Date.now() + 29 * 60_000).toISOString()
    }]);
  });
  const devices = page.getByRole('region', { name: 'Sesiones de cuenta' });
  await expect(devices.getByText('Chromium')).toBeVisible();
  await expect(devices).toContainText('Terminación 555555');
  await expect(devices).not.toContainText('11111111-2222-4333-8444-555555555555');
  await devices.getByRole('button', { name: 'Revocar esta sesión' }).click();
  await expect(page.getByRole('dialog', { name: 'Revocar sesión', exact: true })).toBeVisible();
});

test('C05 UX | passkey revocation selects a listed key and never requires typing its UUID', async ({ page }) => {
  await mockAccountRestore(page);
  await page.goto('/cuenta?vista=llaves');
  await expect(page.getByRole('navigation', { name: 'Mi cuenta' })).toBeVisible();
  await page.evaluate(() => {
    const portal = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-identity-portal')!);
    portal.identity.passkeys.set([{ id: '33333333-4444-4555-8666-777777777777', createdAtUtc: new Date().toISOString() }]);
  });
  await expect(page.getByRole('region', { name: 'Llaves de acceso' }).getByText('Terminación 777777')).toBeVisible();
  await page.getByRole('button', { name: 'Revocar llave de acceso', exact: true }).click();
  const dialog = page.getByRole('dialog', { name: 'Revocar llave de acceso', exact: true });
  const select = dialog.getByLabel('Llave de acceso a revocar');
  await expect(select).toBeVisible();
  await select.selectOption('33333333-4444-4555-8666-777777777777');
  await expect(select).toHaveValue('33333333-4444-4555-8666-777777777777');
  await expect(dialog.locator('input[name="factorId"]')).toHaveCount(0);
});

test('C05 UX | a newly opened dialog does not inherit another operation receipt', async ({ page }) => {
  await page.goto('/cuenta?vista=operadores');
  await expect(page.locator('cs-platform-identity-panel')).toHaveCount(1);
  await page.evaluate(() => {
    const panel = (window as unknown as AngularDebug).ng.getComponent(document.querySelector('cs-platform-identity-panel')!);
    panel.message.set('Resultado anterior de otra operación');
  });
  await page.getByRole('button', { name: 'Recuperar acceso de operador de plataforma', exact: true }).click();
  const modal = page.getByRole('dialog', { name: 'Recuperación segura de operador' });
  await expect(modal).toBeVisible();
  await expect(modal).not.toContainText('Resultado anterior de otra operación');
});
