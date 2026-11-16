import { expect, test } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

for (const viewport of [{ width: 390, height: 844 }, { width: 1366, height: 768 }]) {
  test(`C05 account experience is accessible and has no horizontal overflow at ${viewport.width}px`, async ({ page }) => {
    await page.setViewportSize(viewport);
    await page.goto('/cuenta');
    const login = page.getByRole('region', { name: 'Iniciar sesión', exact: true });
    await expect(login.getByLabel('Correo electrónico', { exact: true })).toBeVisible();
    await expect(login.getByLabel('Contraseña', { exact: true })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Confirmar correo', exact: true })).toHaveCount(0);
    await expect(page.getByLabel('Correo del operador de identidad')).toHaveCount(0);
    const overflow = await page.evaluate(() => Math.max(
      0, document.documentElement.scrollWidth - document.documentElement.clientWidth));
    expect(overflow).toBeLessThanOrEqual(2);
    const results = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa']).analyze();
    expect(results.violations.filter(v => v.impact === 'critical' || v.impact === 'serious')).toEqual([]);
    await page.getByRole('link', { name: 'Recuperar acceso', exact: true }).click();
    await expect(page.getByLabel('Correo para recuperación')).toBeVisible();
    await expect(page.getByLabel('Token de recuperación')).not.toBeVisible();
    await page.getByText('Ya tengo mi código de recuperación', { exact: true }).click();
    await expect(page.getByLabel('Token de recuperación')).toBeVisible();
    await expect(page.getByLabel('Contraseña', { exact: true })).toHaveCount(0);
  });
}

test('C05 account header uses only the validated published platform brand', async ({ page }) => {
  const headers: Array<{ auth?: string; tenant?: string }> = [];
  await page.route('**/api/v1/public/branding/platform', async route => {
    const req = route.request();
    headers.push({ auth: req.headers()['authorization'], tenant: req.headers()['x-tenant-id'] });
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      headers: { 'cache-control': 'no-store' },
      body: JSON.stringify({
        revision: 2,
        name: 'CRECISOL',
        slogan: 'Tu negocio, a tu manera.',
        descriptor: 'Comercio · Servicios · Membresías · Crecimiento',
        primaryColor: '#062E65',
        accentColor: '#078FE8',
        logoUrl: '/assets/crecisol-oficial.png',
        websiteUrl: null,
        publishedAt: '2026-11-08T12:00:00+00:00',
        presentation: {
          faviconUrl: '/assets/crecisol-emblem.png',
          typographyToken: 'system', iconSetToken: 'outline',
          seoTitle: 'CRECISOL · Tu negocio, a tu manera.',
          seoDescription: 'Comercio, servicios y membresías con identidad de plataforma.',
          copyrightNotice: 'Copyright institucional de prueba.'
        }
      })
    });
  });
  await page.goto('/cuenta');
  const header = page.getByRole('link', { name: 'Volver al inicio de CRECISOL' });
  await expect(header).toContainText('CRECISOL');
  await expect(header.locator('img.brand-mark')).toHaveAttribute('src', '/assets/crecisol-oficial.png');
  expect(headers).toEqual([{ auth: undefined, tenant: undefined }]);
});

test('C05 focused operator realm contains no consumer account navigation', async ({ page }) => {
  await page.goto('/cuenta?vista=operadores');
  await expect(page.getByRole('heading', { name: 'Identidad de plataforma', exact: true })).toBeVisible();
  await expect(page.getByLabel('Correo del operador de identidad')).toBeVisible();
  await expect(page.locator('.account-aside')).toHaveCount(0);
  await expect(page.getByRole('navigation', { name: 'Opciones de identidad' })).toHaveCount(0);
  await expect(page.getByLabel('Correo para recuperación')).toHaveCount(0);
  const width = await page.evaluate(() =>
    document.documentElement.scrollWidth - document.documentElement.clientWidth);
  expect(width).toBeLessThanOrEqual(2);
});

test('C05 public entry presents two primary choices and secondary recovery actions', async ({ page }) => {
  await page.goto('/cuenta');
  const primary = page.getByRole('navigation', { name: 'Opciones de identidad' });
  await expect(primary.getByRole('link')).toHaveCount(2);
  await expect(primary.getByRole('link', { name: 'Iniciar sesión', exact: true })).toBeVisible();
  await expect(primary.getByRole('link', { name: 'Solicitar cuenta', exact: true })).toBeVisible();
  const secondary = page.getByRole('navigation', { name: 'Otras opciones de cuenta' });
  await expect(secondary.getByRole('link', { name: 'Recuperar acceso', exact: true })).toBeVisible();
  await expect(secondary.getByRole('link', { name: 'Confirmar correo', exact: true })).toBeVisible();
});
