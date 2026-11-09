import { expect, test, type Page } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

/** C02-04 real browser NEGATIVE tests for intentionally unimplemented private URLs.
 * This is NOT a test of a real private RBAC backend, MFA or cross-tenant IDOR.
 */
const endpoint = '/api/v1/public/branding/platform';
const brand = {
  revision: 4,
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

const proposedPrivate = [
  '/consola/resumen', '/consola/configuracion', '/consola/soporte-excepcional',
  '/negocio/identidad', '/negocio/equipo', '/negocio/operaciones',
  '/trabajo/pendientes', '/trabajo/seguimiento', '/trabajo/perfil',
  '/mi-cuenta/explorar', '/mi-cuenta/solicitudes', '/mi-cuenta/historial'
] as const;

async function servePlatform(page: Page): Promise<string[]> {
  const calls: string[] = [];
  await page.route('**' + endpoint, async route => {
    const req = route.request();
    expect(req.method()).toBe('GET');
    expect(req.headers()['authorization']).toBeUndefined();
    expect(req.headers()['x-tenant-id']).toBeUndefined();
    expect(new URL(req.url()).search).toBe('');
    calls.push(req.url());
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      headers: { 'cache-control': 'no-store' },
      body: JSON.stringify(brand)
    });
  });
  return calls;
}

test('all 12 design-only private paths remain not-found without a branding or private call', async ({ page }) => {
  const requests = await servePlatform(page);
  const destinations: string[] = [];
  page.on('request', req => {
    if (new URL(req.url()).pathname.startsWith('/api/')) destinations.push(req.url());
  });
  for (const path of proposedPrivate) {
    await page.goto(path);
    await expect(page.getByRole('heading', { name: 'Página no disponible', level: 1 }))
      .toBeVisible();
    await expect(page.getByRole('link', { name: 'Volver al inicio' })).toBeVisible();
    await expect(page.locator('form, input, select, iframe, .public-card, .identity-details'))
      .toHaveCount(0);
    await expect(page.locator('body')).not.toContainText('PRIVATE_TENANT_B');
  }
  expect(requests).toHaveLength(0);
  expect(destinations).toHaveLength(0);
});

test('forged tenant, branch, role, path fragment and hostname in URL do not unlock a private shell', async ({ page }) => {
  const requests = await servePlatform(page);
  const cases = [
    '/negocio/equipo?tenant_id=OTHER&rol=superadmin&branch=BRANCH_B',
    '/consola/soporte-excepcional?mfa=1&reason=forged',
    '/trabajo/seguimiento?resource_id=OTHER&claim=employee',
    '/mi-cuenta/historial?consumer=OTHER#PRIVATE_TENANT_B',
    '/login?empresa=OTHER',
    '/admin',
    '/panel/tenant-unknown'
  ];
  for (const path of cases) {
    await page.goto(path);
    await expect(page.getByRole('heading', { level: 1, name: 'Página no disponible' }))
      .toBeVisible();
    await expect(page.locator('.public-page, .identity-page, form')).toHaveCount(0);
  }
  expect(requests).toHaveLength(0);
});

test('current platform pages remain public-only and do not advertise future private routes', async ({ page }) => {
  const requests = await servePlatform(page);
  await page.goto('/');
  await expect(page.getByRole('heading', { level: 1 })).toHaveText(brand.slogan);
  await expect(page.getByRole('link', { name: 'Explorar experiencias previstas' }))
    .toBeVisible();
  await page.goto('/experiencias');
  await expect(page.getByRole('heading', { name: 'Experiencias previstas', level: 1 }))
    .toBeVisible();
  await expect(page.locator('.public-card')).toHaveCount(6);
  await expect(page.locator('a[href^="/consola"], a[href^="/negocio"], a[href^="/trabajo"], a[href^="/mi-cuenta"], a[href^="/login"]'))
    .toHaveCount(0);
  await expect(page.locator('form, input, select')).toHaveCount(0);
  await expect(page.locator('meta[name="robots"]')).toHaveAttribute('content', 'noindex, nofollow');
  expect(requests).toHaveLength(2);
});

test('the public 404 boundary is keyboard-accessible, fits 320px and passes automated WCAG', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 720 });
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await page.goto('/negocio/operaciones?tenant_id=PRIVATE_TENANT_B');
  await expect(page.getByRole('heading', { name: 'Página no disponible' })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1))
    .toBe(true);
  await page.keyboard.press('Tab');
  await expect(page.getByRole('link', { name: 'Volver al inicio' })).toBeFocused();
  const result = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa']).analyze();
  expect(result.violations).toEqual([]);
});
