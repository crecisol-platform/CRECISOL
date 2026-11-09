import { expect, test, type Page } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

/** C02-03 public IA E2E. Fixtures mock ONLY the published platform read-model.
 * These checks are browser behavior tests, not authentication or tenant-domain tests.
 */
const endpoint = '/api/v1/public/branding/platform';
const brand = {
  revision: 3,
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

async function published(page: Page, payload: unknown = brand): Promise<string[]> {
  const requests: string[] = [];
  await page.route('**' + endpoint, async route => {
    const req = route.request();
    const url = new URL(req.url());
    expect(req.method()).toBe('GET');
    expect(url.pathname).toBe(endpoint);
    expect(url.search).toBe('');
    expect(req.headers()['authorization']).toBeUndefined();
    expect(req.headers()['x-tenant-id']).toBeUndefined();
    requests.push(req.url());
    await route.fulfill({
      status: 200,
      contentType: 'application/json; charset=utf-8',
      headers: { 'cache-control': 'no-store' },
      body: JSON.stringify(payload)
    });
  });
  return requests;
}

test('published home offers a real link to the experiences sitemap without changing C01 identity', async ({ page }) => {
  await published(page);
  await page.goto('/');
  await expect(page.getByRole('heading', { level: 1 })).toHaveText(brand.slogan);
  await page.getByRole('link', { name: 'Explorar experiencias previstas' }).click();
  await expect(page).toHaveURL(/\/experiencias$/);
  await expect(page.getByRole('heading', { name: 'Experiencias previstas', level: 1 })).toBeVisible();
  await expect(page.locator('.public-card')).toHaveCount(6);
  await expect(page.getByRole('navigation', { name: 'Navegación pública' })).toBeVisible();
});

test('all three allowlisted public deep links are accessible without privileged controls', async ({ page }) => {
  const requests = await published(page);
  const pages = [
    { path: '/experiencias', title: 'Experiencias previstas', count: 6 },
    { path: '/demostraciones', title: 'Demostraciones previstas', count: 3 },
    { path: '/alta-empresas', title: 'Alta de empresas: información', count: 3 }
  ];
  for (const expected of pages) {
    await page.goto(expected.path);
    await expect(page.getByRole('heading', { level: 1, name: expected.title })).toBeVisible();
    await expect(page.locator('.public-card')).toHaveCount(expected.count);
    await expect(page.getByRole('navigation', { name: 'Navegación pública' })
      .getByRole('link', { name: expected.path === '/experiencias' ? 'Experiencias' :
        expected.path === '/demostraciones' ? 'Demostraciones' : 'Alta de empresas' }))
      .toHaveAttribute('aria-current', 'page');
    await expect(page.locator('form, input, select, iframe')).toHaveCount(0);
    await expect(page.locator('meta[name="robots"]')).toHaveAttribute('content', 'noindex, nofollow');
    await expect(page.locator('meta[name="description"]'))
      .toHaveAttribute('content', brand.presentation.seoDescription);
    await expect(page.locator('link[rel="icon"][data-crecisol-managed="true"]'))
      .toHaveAttribute('href', brand.presentation.faviconUrl);
    await expect(page).toHaveTitle(expected.title + ' · CRECISOL');
  }
  expect(requests.length).toBe(3);
  expect(await page.evaluate(() => [localStorage.length, sessionStorage.length])).toEqual([0, 0]);
});

test('public navigation updates the mapped content instead of reusing another route state', async ({ page }) => {
  await published(page);
  await page.goto('/experiencias');
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Experiencias previstas');
  await page.getByRole('navigation', { name: 'Navegación pública' })
    .getByRole('link', { name: 'Demostraciones' }).click();
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Demostraciones previstas');
  await expect(page.locator('.public-card')).toHaveCount(3);
  await page.getByRole('navigation', { name: 'Navegación pública' })
    .getByRole('link', { name: 'Alta de empresas' }).click();
  await expect(page.getByRole('heading', { level: 1 }))
    .toHaveText('Alta de empresas: información');
  await expect(page.locator('form, input, select')).toHaveCount(0);
  await expect(page.locator('body')).toContainText('No hay formulario operativo');
});

test('unknown and private-like paths remain the isolated not-found page with no platform brand fetch', async ({ page }) => {
  let calls = 0;
  await page.route('**' + endpoint, route => {
    calls++;
    return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(brand) });
  });
  for (const path of [
    '/admin', '/superadmin', '/panel/propietario', '/empleado/tareas',
    '/cliente/expedientes', '/negocio/otro-tenant', '/login?empresa=otro',
    '/experiencias/privado', '/altas/real'
  ]) {
    await page.goto(path);
    await expect(page.getByRole('heading', { name: 'Página no disponible' })).toBeVisible();
    await expect(page.locator('.public-card, .identity-details, form')).toHaveCount(0);
    await expect(page.getByRole('link', { name: 'Volver al inicio' })).toBeVisible();
  }
  expect(calls).toBe(0);
});

test('forged tenant query or browser selected company cannot change the published platform request', async ({ page }) => {
  const calls = await published(page);
  await page.goto('/experiencias?tenant_id=OTHER&scope=Tenant&branch=PRIVATE');
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Experiencias previstas');
  await expect(page.locator('body')).not.toContainText('PRIVATE_TENANT_B');
  await expect(page.locator('.public-wordmark')).toContainText('CRECISOL');
  expect(calls.length).toBe(1);
});

test('unexpected private fields in published payload fail closed and never render tenant name', async ({ page }) => {
  await published(page, { ...brand, scope: 'Tenant', tenantId: 'OTHER', name: 'PRIVATE_TENANT_B' });
  await page.goto('/experiencias');
  await expect(page.getByRole('alert')).toContainText('no está disponible');
  await expect(page.locator('.public-card, .public-page')).toHaveCount(0);
  await expect(page.locator('body')).not.toContainText('PRIVATE_TENANT_B');
  await expect(page.locator('meta[name="theme-color"]')).toHaveCount(0);
  await expect(page.locator('link[data-crecisol-managed]')).toHaveCount(0);
});

test('unavailable publication remains private-data-free and retries only with a fresh valid snapshot', async ({ page }) => {
  let online = false;
  await page.route('**' + endpoint, route => route.fulfill({
    status: online ? 200 : 503,
    contentType: 'application/json',
    body: online ? JSON.stringify(brand) : '{}'
  }));
  await page.goto('/alta-empresas');
  await expect(page.getByRole('alert')).toContainText('no está disponible');
  await expect(page.locator('.public-card, form')).toHaveCount(0);
  online = true;
  await page.getByRole('button', { name: 'Intentar nuevamente' }).click();
  await expect(page.getByRole('heading', { name: 'Alta de empresas: información', level: 1 })).toBeVisible();
  expect(await page.evaluate(() => [localStorage.length, sessionStorage.length])).toEqual([0, 0]);
});

test('320px mobile and keyboard access pass WCAG 2.2 AA automated audit', async ({ page }) => {
  await published(page);
  await page.setViewportSize({ width: 320, height: 720 });
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await page.goto('/experiencias');
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Experiencias previstas');
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1)).toBe(true);
  await page.keyboard.press('Tab');
  await expect(page.getByRole('link', { name: 'Saltar al contenido principal' })).toBeFocused();
  await page.keyboard.press('Enter');
  // Angular's router can commit fragment navigation after the key event returns.
  await expect(page).toHaveURL(/#contenido$/);
  await expect(page.locator('main#contenido')).toHaveCount(1);
  const mobile = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa']).analyze();
  expect(mobile.violations).toEqual([]);
  await page.setViewportSize({ width: 1280, height: 800 });
  await page.goto('/demostraciones');
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Demostraciones previstas');
  const desktop = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa']).analyze();
  expect(desktop.violations).toEqual([]);
});
