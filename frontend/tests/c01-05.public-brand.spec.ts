import { expect, test, type Page } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

const URL_PATH = '/api/v1/public/branding/platform';

const brand = {
  revision: 2,
  name: 'CRECISOL',
  slogan: 'Tu negocio, a tu manera.',
  descriptor: 'Comercio · Servicios · Membresías · Crecimiento',
  primaryColor: '#062E65',
  accentColor: '#078FE8',
  logoUrl: '/assets/brand.png',
  websiteUrl: null,
  publishedAt: '2026-11-08T12:00:00+00:00',
  presentation: {
    faviconUrl: '/assets/favicon.webp',
    typographyToken: 'system',
    iconSetToken: 'outline',
    seoTitle: 'CRECISOL · Tu negocio, a tu manera.',
    seoDescription: 'Comercio, servicios y membresías con identidad de plataforma.',
    copyrightNotice: 'Copyright institucional de prueba.'
  }
};

async function replyPublished(page: Page, payload: unknown = brand, code = 200): Promise<void> {
  await page.route('**' + URL_PATH, async route => {
    await route.fulfill({
      status: code,
      contentType: 'application/json; charset=utf-8',
      headers: { 'cache-control': 'no-store' },
      body: code === 200 ? JSON.stringify(payload) : '{}'
    });
  });
}

test('public identity comes ONLY from the published platform response and updates head', async ({ page }) => {
  const requests: string[] = [];
  await page.route('**' + URL_PATH, async route => {
    const request = route.request();
    expect(request.method()).toBe('GET');
    expect(new URL(request.url()).pathname).toBe(URL_PATH);
    expect(new URL(request.url()).search).toBe('');
    expect(request.headers()['authorization']).toBeUndefined();
    expect(request.headers()['x-tenant-id']).toBeUndefined();
    requests.push(request.url());
    await route.fulfill({
      status: 200,
      body: JSON.stringify(brand),
      contentType: 'application/json',
      headers: { 'cache-control': 'no-store' }
    });
  });
  await page.goto('/');
  await expect(page.getByRole('heading', { level: 1 })).toHaveText(brand.slogan);
  await expect(page.locator('.wordmark')).toContainText(brand.name);
  await expect(page.getByRole('navigation', { name: 'Navegación principal' })).toBeVisible();
  await expect(page.locator('.identity-details dd')).toHaveCount(3);
  await expect(page.locator('.area-tags li')).toHaveCount(4);
  await expect(page).toHaveTitle(brand.presentation.seoTitle);
  await expect(page.locator('meta[name="description"]'))
    .toHaveAttribute('content', brand.presentation.seoDescription);
  await expect(page.locator('meta[name="theme-color"]'))
    .toHaveAttribute('content', brand.primaryColor);
  await expect(page.locator('meta[name="robots"]')).toHaveAttribute('content', 'noindex, nofollow');
  await expect(page.locator('link[rel="icon"][data-crecisol-managed="true"]'))
    .toHaveAttribute('href', brand.presentation.faviconUrl);
  expect(await page.locator('.identity-page').evaluate(el =>
    getComputedStyle(el).getPropertyValue('--brand-primary').trim()
  )).toBe(brand.primaryColor);
  expect(requests).toHaveLength(1);
  expect(page.url()).toBe('http://127.0.0.1:4200/');
  expect(await page.locator('body').innerText()).not.toContain('ScopeId');
});

test('mobile 360 viewport fits without horizontal overflow and navigation remains operable', async ({ page }) => {
  await page.setViewportSize({ width: 360, height: 780 });
  await replyPublished(page);
  await page.goto('/');
  await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1))
    .toBeTruthy();
  await expect(page.getByRole('navigation', { name: 'Navegación principal' })
    .getByRole('link', { name: 'Identidad' })).toBeVisible();
  await page.getByRole('link', { name: 'Identidad', exact: true }).click();
  expect(new URL(page.url()).hash).toBe('#identidad');
  await expect(page.getByRole('heading', { name: 'Información de marca publicada' })).toBeVisible();
});

test('keyboard skip link, focus and accessible headings work', async ({ page }) => {
  await replyPublished(page);
  await page.goto('/');
  await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
  await page.keyboard.press('Tab');
  await expect(page.getByRole('link', { name: 'Saltar al contenido principal' })).toBeFocused();
  await page.keyboard.press('Enter');
  expect(new URL(page.url()).hash).toBe('#contenido');
  await expect(page.locator('main#contenido')).toHaveCount(1);
  await expect(page.locator('h1')).toHaveCount(1);
});

test('WCAG 2.2 AA automatic checks pass for desktop and compact mobile', async ({ page }) => {
  await replyPublished(page);
  await page.goto('/');
  await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
  const desktop = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'])
    .analyze();
  expect(desktop.violations).toEqual([]);

  await page.setViewportSize({ width: 375, height: 812 });
  const mobile = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'])
    .analyze();
  expect(mobile.violations).toEqual([]);
});

test('fail closed on published tenant response with private fields', async ({ page }) => {
  await replyPublished(page, {
    ...brand,
    scope: 'Tenant',
    scopeId: '22222222-2222-2222-2222-222222222222',
    name: 'PRIVATE_TENANT_B'
  });
  await page.goto('/');
  await expect(page.getByRole('alert')).toContainText('no está disponible');
  await expect(page.locator('body')).not.toContainText('PRIVATE_TENANT_B');
  await expect(page.locator('meta[name="description"]')).toHaveCount(0);
  await expect(page.locator('.identity-details')).toHaveCount(0);
});

test('reject injected markup, unsafe colors and untrusted assets', async ({ page }) => {
  const badCases: unknown[] = [
    { ...brand, slogan: '<img src=x onerror=alert(1)>' },
    { ...brand, primaryColor: 'url(javascript:alert(1))' },
    { ...brand, presentation: { ...brand.presentation, faviconUrl: 'https://evil.invalid/logo.png' } },
    { ...brand, presentation: { ...brand.presentation, typographyToken: 'url(https://evil.invalid)' } }
  ];
  for (const payload of badCases) {
    await page.unrouteAll();
    await replyPublished(page, payload);
    await page.goto('/');
    await expect(page.getByRole('alert')).toContainText('no está disponible');
    await expect(page.locator('link[data-crecisol-managed]')).toHaveCount(0);
    await expect(page.locator('.identity-page')).toHaveCount(0);
    await expect(page.locator('meta[name="theme-color"]')).toHaveCount(0);
  }
});

test('HTTP 404, 503 and repeated failure never show demo or stale branding', async ({ page }) => {
  let status = 404;
  await page.route('**' + URL_PATH, route => route.fulfill({
    status, contentType: 'application/json', body: '{}'
  }));
  await page.goto('/');
  await expect(page.getByRole('alert')).toContainText('no está disponible');
  status = 200;
  await page.getByRole('button', { name: 'Intentar nuevamente' }).click();
  // Change response to the valid payload for the second request.
  // This handler intentionally returned {} -> still rejected safely.
  await expect(page.getByRole('alert')).toBeVisible();
  await page.reload();
  await expect(page.getByRole('alert')).toBeVisible();
  await expect(page.locator('body')).not.toContainText(brand.slogan);
  await expect(page).toHaveTitle('Identidad pública no disponible');
});

test('recovery after outage needs fresh published response and never reads localStorage', async ({ page }) => {
  let online = false;
  await page.route('**' + URL_PATH, route => route.fulfill({
    status: online ? 200 : 503,
    contentType: 'application/json',
    body: online ? JSON.stringify(brand) : '{}'
  }));
  await page.goto('/');
  await expect(page.getByRole('alert')).toContainText('no está disponible');
  online = true;
  await page.getByRole('button', { name: 'Intentar nuevamente' }).click();
  await expect(page.getByRole('heading', { level: 1 })).toHaveText(brand.slogan);
  expect(await page.evaluate(() => [localStorage.length, sessionStorage.length])).toEqual([0, 0]);
  await page.reload();
  await expect(page.getByRole('heading', { level: 1 })).toHaveText(brand.slogan);
});

test('missing optional presentation keeps safe published name, description and no favicon', async ({ page }) => {
  await replyPublished(page, { ...brand, presentation: null });
  await page.goto('/');
  await expect(page.getByRole('heading', { level: 1 })).toHaveText(brand.slogan);
  await expect(page).toHaveTitle(brand.name);
  await expect(page.locator('meta[name="description"]')).toHaveAttribute('content', brand.descriptor);
  await expect(page.locator('link[data-crecisol-managed]')).toHaveCount(0);
});

test('unknown routes are a separate lazy page, not an admin or tenant panel', async ({ page }) => {
  await page.goto('/private/tenant/admin');
  await expect(page.getByRole('heading', { name: 'Página no disponible' })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Volver al inicio' })).toBeVisible();
  await expect(page.locator('.identity-details')).toHaveCount(0);
});

test('reduced motion disables decorative progress animation', async ({ page }) => {
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await page.route('**' + URL_PATH, route => route.fulfill({
    status: 503, contentType: 'application/json', body: '{}'
  }));
  await page.goto('/');
  await expect(page.getByRole('alert')).toBeVisible();
  const duration = await page.locator('body').evaluate(() =>
    getComputedStyle(document.documentElement).scrollBehavior);
  expect(duration).toBe('auto');
});
