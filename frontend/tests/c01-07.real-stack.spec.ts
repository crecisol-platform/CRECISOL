/**
 * C01-07: NO page.route(), no mocked API, no tenant selector.
 * Real MySQL 8.4 -> .NET 10 API -> Angular 22 dev proxy -> Chromium.
 * Shell harness controls ONLY disposable synthetic database publications.
 */
import { expect, test, type Page } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

const path = '/api/v1/public/branding/platform';
async function loadActual(page: Page) {
  const [response] = await Promise.all([
    page.waitForResponse(r => new URL(r.url()).pathname === path),
    page.goto('/')
  ]);
  expect(response.url()).toBe('http://127.0.0.1:4200' + path);
  expect(response.headers()['cache-control']).toContain('no-store');
  expect(response.headers()['access-control-allow-origin']).toBeUndefined();
  expect(response.headers()['set-cookie']).toBeUndefined();
  return response;
}

test('published: real platform publication appears without tenant or draft data', async ({ page }) => {
  const result = await loadActual(page);
  expect(result.status()).toBe(200);
  const body = await result.json();
  expect(body.revision).toBe(2);
  expect(body.name).toBe('CRECISOL');
  expect(body.primaryColor).toBe('#062E65');
  expect(body.logoUrl).toBe('/assets/crecisol-oficial.png');
  expect(body.scope).toBeUndefined();
  expect(body.tenantId).toBeUndefined();
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Tu negocio, a tu manera.');
  await expect(page.locator('.brand-art--logo-ready')).toHaveCount(1);
  await expect(page.locator('.header-brand-icon')).toBeVisible();
  await expect(page.locator('.identity-details dd')).toHaveCount(3);
  await expect(page).toHaveTitle('CRECISOL · Tu negocio, a tu manera.');
  await expect(page.locator('meta[name="robots"]')).toHaveAttribute('content', 'noindex, nofollow');
  expect(await page.locator('body').innerText()).not.toContain('PRIVATE_TENANT_B');
  expect(await page.locator('body').innerText()).not.toContain('UNPUBLISHED_DRAFT');
  expect(await page.evaluate(() => [localStorage.length, sessionStorage.length])).toEqual([0, 0]);
});

test('published: mobile 320px has no horizontal overflow and satisfies automated WCAG AA', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 720 });
  expect((await loadActual(page)).status()).toBe(200);
  await expect(page.locator('.brand-art--logo-ready')).toHaveCount(1);
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1)).toBe(true);
  const result = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa']).analyze();
  expect(result.violations).toEqual([]);
});

test('absent: deletion of platform publication never falls back to a published tenant', async ({ page }) => {
  const response = await loadActual(page);
  expect(response.status()).toBe(404);
  await expect(page.getByRole('alert')).toContainText('no está disponible');
  await expect(page).toHaveTitle('Identidad pública no disponible');
  expect(await page.locator('body').innerText()).not.toContain('PRIVATE_TENANT_B');
  await expect(page.locator('.identity-page')).toHaveCount(0);
});

test('invalid: XSS in stored brand is denied by server and no image executes', async ({ page }) => {
  const response = await loadActual(page);
  expect(response.status()).toBe(503);
  await expect(page.getByRole('alert')).toContainText('no está disponible');
  await expect(page.locator('img[src="x"]')).toHaveCount(0);
  expect(await page.locator('body').innerText()).not.toContain('onerror');
  expect(await page.locator('.identity-page')).toHaveCount(0);
});

test('updated: publication revision switch returns fresh metadata, not stale cached brand', async ({ page }) => {
  const response = await loadActual(page);
  expect(response.status()).toBe(200);
  const body = await response.json();
  expect(body.revision).toBe(3);
  await expect(page).toHaveTitle('CRECISOL revisión 3');
  await expect(page.locator('meta[name="description"]'))
    .toHaveAttribute('content', 'Identidad revisada sin cache antiguo.');
  await expect(page.locator('.version-stamp')).toContainText('Versión 3');
});
