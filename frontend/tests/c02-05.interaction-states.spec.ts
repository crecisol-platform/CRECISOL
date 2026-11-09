import { expect, test, type Page } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

/** C02-05: public-identity-only state tests with synthetic same-origin responses.
 * No real tenant, private login, authorized writes or financial confirmations.
 */
const endpoint = '/api/v1/public/branding/platform';
const brand = {
  revision: 5,
  name: 'CRECISOL',
  slogan: 'Tu negocio, a tu manera.',
  descriptor: 'Comercio · Servicios · Membresías · Crecimiento',
  primaryColor: '#062E65',
  accentColor: '#078FE8',
  logoUrl: '/assets/crecisol-oficial.png',
  websiteUrl: null,
  publishedAt: '2026-11-09T07:00:00+00:00',
  presentation: {
    faviconUrl: '/assets/crecisol-emblem.png',
    typographyToken: 'system',
    iconSetToken: 'outline',
    seoTitle: 'CRECISOL · Tu negocio, a tu manera.',
    seoDescription: 'Identidad pública publicada.',
    copyrightNotice: 'Aviso institucional publicado.'
  }
};

async function validPlatform(page: Page): Promise<string[]> {
  const calls: string[] = [];
  await page.route('**' + endpoint, async route => {
    const req = route.request();
    expect(req.method()).toBe('GET');
    const url = new URL(req.url());
    expect(url.pathname).toBe(endpoint);
    expect(url.search).toBe('');
    expect(req.headers()['authorization']).toBeUndefined();
    expect(req.headers()['x-tenant-id']).toBeUndefined();
    calls.push(req.url());
    await route.fulfill({ status: 200, contentType: 'application/json',
      headers: { 'cache-control': 'no-store' }, body: JSON.stringify(brand) });
  });
  return calls;
}

test('loading is announced and hides public data until a fresh validated response', async ({ page }) => {
  let release!: () => void;
  const delayed = new Promise<void>(resolve => { release = resolve; });
  const calls: string[] = [];
  await page.route('**' + endpoint, async route => {
    calls.push(route.request().url());
    await delayed;
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(brand) });
  });
  await page.goto('/experiencias');
  const loading = page.getByRole('status');
  await expect(loading).toContainText('Preparando información de plataforma');
  await expect(loading).toHaveAttribute('aria-busy', 'true');
  await expect(page.locator('[data-interaction-state="loading"]')).toHaveCount(1);
  await expect(page.locator('.public-page, .public-card, .public-wordmark')).toHaveCount(0);
  release();
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Experiencias previstas');
  await expect(page.locator('[data-interaction-state]')).toHaveCount(0);
  expect(calls).toHaveLength(1);
});

test('HTTP error fails closed, exposes safe retry and recovers only after fresh GET', async ({ page }) => {
  let valid = false;
  let calls = 0;
  await page.route('**' + endpoint, route => {
    calls++;
    return route.fulfill({
      status: valid ? 200 : 503,
      contentType: 'application/json',
      body: valid ? JSON.stringify(brand) : JSON.stringify({ debug: 'PRIVATE_TENANT_B' })
    });
  });
  await page.goto('/alta-empresas');
  await expect(page.getByRole('alert')).toContainText('La información pública no está disponible');
  await expect(page.locator('[data-interaction-state="error"]')).toHaveCount(1);
  await expect(page.locator('.public-card, .public-page, form')).toHaveCount(0);
  await expect(page.locator('body')).not.toContainText('PRIVATE_TENANT_B');
  await expect(page.locator('meta[name="theme-color"], link[data-crecisol-managed]')).toHaveCount(0);
  valid = true;
  await page.getByRole('button', { name: 'Intentar nuevamente' }).click();
  await expect(page.getByRole('heading', { name: 'Alta de empresas: información', level: 1 })).toBeVisible();
  await expect(page.locator('.public-card')).toHaveCount(3);
  expect(calls).toBe(2);
  expect(await page.evaluate(() => [localStorage.length, sessionStorage.length])).toEqual([0, 0]);
});

test('offline/online browser hints change guidance only; no stale content or silent retry', async ({ page }) => {
  let calls = 0;
  await page.route('**' + endpoint, route => {
    calls++;
    return route.fulfill({ status: 503, contentType: 'application/json', body: '{}' });
  });
  await page.goto('/demostraciones');
  await expect(page.locator('[data-interaction-state="error"]')).toBeVisible();
  // Simulate ONLY the browser network hint, not a real authenticated response.
  await page.evaluate(() => {
    Object.defineProperty(navigator, 'onLine', { configurable: true, value: false });
    window.dispatchEvent(new Event('offline'));
  });
  await expect(page.locator('[data-interaction-state="offline"]')).toBeVisible();
  await expect(page.getByRole('alert')).toContainText('Sin conexión para actualizar la información');
  await expect(page.locator('.public-page, form')).toHaveCount(0);
  await page.evaluate(() => {
    Object.defineProperty(navigator, 'onLine', { configurable: true, value: true });
    window.dispatchEvent(new Event('online'));
  });
  await expect(page.locator('[data-interaction-state="error"]')).toBeVisible();
  await expect(page.locator('.public-page')).toHaveCount(0);
  expect(calls).toBe(1); // Online hint alone never replays a privileged or public request.
});

test('malicious tenant-scoped publication is rejected; metadata and old content stay cleared', async ({ page }) => {
  let injected = false;
  await page.route('**' + endpoint, route => route.fulfill({
    status: 200, contentType: 'application/json',
    body: JSON.stringify(injected ? { ...brand, scope: 'Tenant', tenantId: 'OTHER', name: 'PRIVATE_TENANT_B' } : brand)
  }));
  await page.goto('/experiencias');
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Experiencias previstas');
  await expect(page.locator('link[data-crecisol-managed]')).toHaveCount(1);
  injected = true;
  await page.goto('/demostraciones');
  await expect(page.locator('[data-interaction-state="error"]')).toHaveCount(1);
  await expect(page.locator('.public-page, .public-wordmark, .public-card')).toHaveCount(0);
  await expect(page.locator('body')).not.toContainText('PRIVATE_TENANT_B');
  await expect(page.locator('meta[name="description"], meta[name="theme-color"], link[data-crecisol-managed]'))
    .toHaveCount(0);
  expect(await page.evaluate(() => [localStorage.length, sessionStorage.length])).toEqual([0, 0]);
});

test('retry is single-flight and never shows a success acknowledgement while pending', async ({ page }) => {
  let calls = 0;
  let release!: () => void;
  const delayed = new Promise<void>(resolve => { release = resolve; });
  await page.route('**' + endpoint, async route => {
    calls++;
    if (calls === 1) {
      await route.fulfill({ status: 503, contentType: 'application/json', body: '{}' });
      return;
    }
    await delayed;
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(brand) });
  });
  await page.goto('/experiencias');
  await expect(page.locator('[data-interaction-state="error"]')).toBeVisible();
  await page.evaluate(() => {
    const button = document.querySelector<HTMLButtonElement>('button.interaction-retry');
    if (!button) throw new Error('Missing retry');
    button.click();
    button.click();
  });
  await expect(page.locator('[data-interaction-state="loading"]')).toBeVisible();
  await expect(page.locator('[data-interaction-state="confirmation"], [data-interaction-state="empty"]'))
    .toHaveCount(0);
  release();
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Experiencias previstas');
  expect(calls).toBe(2);
});

test('mobile failure and offline alerts pass automated WCAG checks with keyboard-accessible retry', async ({ page }) => {
  await page.route('**' + endpoint, route => route.fulfill({ status: 503,
    contentType: 'application/json', body: '{}' }));
  await page.setViewportSize({ width: 320, height: 720 });
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await page.goto('/alta-empresas');
  await expect(page.locator('[data-interaction-state="error"]')).toBeVisible();
  await page.keyboard.press('Tab');
  await expect(page.getByRole('button', { name: 'Intentar nuevamente' })).toBeFocused();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1)).toBe(true);
  let audit = await new AxeBuilder({ page }).withTags(['wcag2a','wcag2aa','wcag21aa','wcag22aa']).analyze();
  expect(audit.violations).toEqual([]);
  await page.evaluate(() => {
    Object.defineProperty(navigator, 'onLine', { configurable: true, value: false });
    window.dispatchEvent(new Event('offline'));
  });
  await expect(page.locator('[data-interaction-state="offline"]')).toBeVisible();
  audit = await new AxeBuilder({ page }).withTags(['wcag2a','wcag2aa','wcag21aa','wcag22aa']).analyze();
  expect(audit.violations).toEqual([]);
});

test('unknown and proposed private paths never render any interaction state or make branding GET', async ({ page }) => {
  let calls = 0;
  await page.route('**' + endpoint, route => {
    calls++;
    return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(brand) });
  });
  for (const path of [
    '/consola/resumen', '/negocio/equipo?tenant_id=OTHER',
    '/trabajo/seguimiento?resource_id=OTHER',
    '/mi-cuenta/historial?consumer=OTHER', '/login?empresa=OTHER'
  ]) {
    await page.goto(path);
    await expect(page.getByRole('heading', { name: 'Página no disponible', level: 1 })).toBeVisible();
    await expect(page.locator('cs-interaction-state, .public-page, form')).toHaveCount(0);
  }
  expect(calls).toBe(0);
});
