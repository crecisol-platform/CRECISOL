import { expect, test, type Page } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

/** C02-08: six journeys are contracted, but only E01/E02 have real public UI.
 * Route fixtures stub the public platform GET; the Angular pages are real.
 * No claims about live private login, authorization, tenant DB or payments.
 */
const endpoint = '/api/v1/public/branding/platform';
const brand = {
  revision: 8, name: 'CRECISOL',
  slogan: 'Tu negocio, a tu manera.',
  descriptor: 'Comercio · Servicios · Membresías · Crecimiento',
  primaryColor: '#062E65', accentColor: '#078FE8',
  logoUrl: '/assets/crecisol-oficial.png', websiteUrl: null,
  publishedAt: '2026-11-09T07:00:00+00:00',
  presentation: {
    faviconUrl: '/assets/crecisol-emblem.png',
    typographyToken: 'system', iconSetToken: 'outline',
    seoTitle: 'CRECISOL · Tu negocio, a tu manera.',
    seoDescription: 'Identidad de plataforma publicada.',
    copyrightNotice: 'Marca institucional publicada.'
  }
};

async function publicFixture(page: Page, response: unknown = brand): Promise<{ calls: string[], unauthorized: string[] }> {
  const calls: string[] = [];
  const unauthorized: string[] = [];
  page.on('request', request => {
    const url = new URL(request.url());
    if (url.pathname.startsWith('/api/') && url.pathname !== endpoint) unauthorized.push(url.pathname);
    if (request.method() !== 'GET' && url.pathname.startsWith('/api/')) unauthorized.push(request.method());
  });
  await page.route('**' + endpoint, async route => {
    const request = route.request();
    const url = new URL(request.url());
    expect(request.method()).toBe('GET');
    expect(url.pathname).toBe(endpoint);
    expect(url.search).toBe('');
    expect(request.headers()['x-tenant-id']).toBeUndefined();
    expect(request.headers()['authorization']).toBeUndefined();
    calls.push(url.toString());
    await route.fulfill({
      status: 200, contentType: 'application/json',
      headers: { 'cache-control': 'no-store' }, body: JSON.stringify(response)
    });
  });
  return { calls, unauthorized };
}

async function verifyNoOverflow(page: Page): Promise<void> {
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1))
    .toBe(true);
}

test('J01 visitor traverses home, experiences, demo information, and J02 onboarding information', async ({ page }) => {
  const { calls, unauthorized } = await publicFixture(page);
  await page.goto('/');
  await expect(page.getByRole('heading', { level: 1 })).toHaveText(brand.slogan);
  await page.getByRole('link', { name: 'Explorar experiencias previstas' }).click();
  await expect(page.getByRole('heading', { name: 'Experiencias previstas', level: 1 })).toBeVisible();
  await expect(page.locator('.public-card')).toHaveCount(6);
  const nav = page.getByRole('navigation', { name: 'Navegación pública' });
  await nav.getByRole('link', { name: 'Demostraciones' }).click();
  await expect(page.getByRole('heading', { name: 'Demostraciones previstas', level: 1 })).toBeVisible();
  await expect(page.locator('.public-card')).toHaveCount(3);
  await nav.getByRole('link', { name: 'Alta de empresas' }).click();
  await expect(page.getByRole('heading', { name: 'Alta de empresas: información', level: 1 })).toBeVisible();
  await expect(page.locator('form, input, select, [type="submit"]')).toHaveCount(0);
  await expect(page.getByText('No hay formulario operativo', { exact: false })).toBeVisible();
  await nav.getByRole('link', { name: 'Inicio' }).click();
  await expect(page.getByRole('heading', { level: 1 })).toHaveText(brand.slogan);
  expect(calls.length).toBeGreaterThanOrEqual(2);
  expect(unauthorized).toEqual([]);
});

test('E01 and E02 information paths show only available content and planned boundaries', async ({ page }) => {
  await publicFixture(page);
  const cases = [
    ['/experiencias', 'Experiencias previstas', 6],
    ['/demostraciones', 'Demostraciones previstas', 3],
    ['/alta-empresas', 'Alta de empresas: información', 3]
  ] as const;
  for (const [path, title, cards] of cases) {
    await page.goto(path);
    await expect(page.getByRole('heading', { level: 1, name: title })).toBeVisible();
    await expect(page.locator('.public-card')).toHaveCount(cards);
    await expect(page.getByRole('note')).toContainText('En preparación');
    await expect(page.locator('form, input, iframe, .private-shell')).toHaveCount(0);
    await expect(page.getByRole('main')).toHaveCount(1);
  }
  await expect(page.locator('body')).toContainText('No hay formulario operativo');
  expect(await page.evaluate(() => [localStorage.length, sessionStorage.length])).toEqual([0, 0]);
});

test('public journey remains keyboard-operable on mobile with meaningful focus and current nav', async ({ page }) => {
  await publicFixture(page);
  await page.setViewportSize({ width: 320, height: 740 });
  await page.goto('/experiencias');
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Experiencias previstas');
  await page.keyboard.press('Tab');
  await expect(page.getByRole('link', { name: 'Saltar al contenido principal' })).toBeFocused();
  await page.keyboard.press('Enter');
  await expect(page.locator('main#contenido')).toBeFocused();
  await expect(page.getByRole('navigation', { name: 'Navegación pública' })
    .getByRole('link', { name: 'Experiencias' })).toHaveAttribute('aria-current', 'page');
  await verifyNoOverflow(page);
});

test('public state loading has no old content or privileged data before fresh release', async ({ page }) => {
  let release!: () => void;
  const pending = new Promise<void>(resolve => { release = resolve; });
  await page.route('**' + endpoint, async route => {
    await pending;
    await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(brand) });
  });
  await page.goto('/experiencias');
  await expect(page.locator('[data-interaction-state="loading"]')).toBeVisible();
  await expect(page.locator('.public-card, .public-page, form')).toHaveCount(0);
  release();
  await expect(page.getByRole('heading', { name: 'Experiencias previstas', level: 1 })).toBeVisible();
  await expect(page.locator('.public-card')).toHaveCount(6);
});

test('failed onboarding journey recovers on explicit retry only after a fresh valid GET', async ({ page }) => {
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
  await expect(page.locator('[data-interaction-state="error"]')).toBeVisible();
  await expect(page.locator('.public-card, form')).toHaveCount(0);
  await expect(page.locator('body')).not.toContainText('PRIVATE_TENANT_B');
  valid = true;
  await page.getByRole('button', { name: 'Intentar nuevamente' }).click();
  await expect(page.getByRole('heading', { name: 'Alta de empresas: información', level: 1 })).toBeVisible();
  expect(calls).toBe(2);
  await expect(page.locator('form, input, [type="submit"]')).toHaveCount(0);
});

test('offline and online are advice only, without silent retry or private success', async ({ page }) => {
  let calls = 0;
  await page.route('**' + endpoint, route => {
    calls++;
    return route.fulfill({ status: 503, contentType: 'application/json', body: '{}' });
  });
  await page.goto('/demostraciones');
  await expect(page.locator('[data-interaction-state="error"]')).toBeVisible();
  await page.evaluate(() => {
    Object.defineProperty(navigator, 'onLine', { configurable: true, value: false });
    window.dispatchEvent(new Event('offline'));
  });
  await expect(page.locator('[data-interaction-state="offline"]')).toBeVisible();
  await page.evaluate(() => {
    Object.defineProperty(navigator, 'onLine', { configurable: true, value: true });
    window.dispatchEvent(new Event('online'));
  });
  await expect(page.locator('[data-interaction-state="error"]')).toBeVisible();
  expect(calls).toBe(1);
  await expect(page.locator('.public-page, form')).toHaveCount(0);
});

test('tenant, branch, role and source query strings never authorize public journeys', async ({ page }) => {
  const { calls, unauthorized } = await publicFixture(page);
  await page.goto('/experiencias?tenant_id=OTHER&role=superadmin&branch=PRIVATE&db=other');
  await expect(page.getByRole('heading', { name: 'Experiencias previstas', level: 1 })).toBeVisible();
  await expect(page.locator('.public-wordmark')).toContainText('CRECISOL');
  await expect(page.locator('body')).not.toContainText('PRIVATE_TENANT_B');
  expect(calls).toHaveLength(1);
  expect(unauthorized).toEqual([]);
});

test('private scoped published payload fails closed and clears previous metadata', async ({ page }) => {
  let tampered = false;
  await page.route('**' + endpoint, route => route.fulfill({
    status: 200, contentType: 'application/json',
    body: JSON.stringify(tampered ?
      { ...brand, scope: 'Tenant', tenantId: 'OTHER', name: 'PRIVATE_TENANT_B' } : brand)
  }));
  await page.goto('/experiencias');
  await expect(page.locator('.public-card')).toHaveCount(6);
  tampered = true;
  await page.goto('/demostraciones');
  await expect(page.locator('[data-interaction-state="error"]')).toBeVisible();
  await expect(page.locator('.public-page, .public-card, form')).toHaveCount(0);
  await expect(page.locator('body')).not.toContainText('PRIVATE_TENANT_B');
  await expect(page.locator('meta[name="description"], meta[name="theme-color"], link[data-crecisol-managed]'))
    .toHaveCount(0);
});

test('all 12 E03-E06 proposed private paths are denied without any branding or private API', async ({ page }) => {
  let brandCalls = 0;
  const privateCalls: string[] = [];
  page.on('request', request => {
    if (new URL(request.url()).pathname.startsWith('/api/')) privateCalls.push(request.url());
  });
  await page.route('**' + endpoint, route => {
    brandCalls++;
    return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(brand) });
  });
  for (const path of [
    '/consola/resumen', '/consola/configuracion', '/consola/soporte-excepcional',
    '/negocio/identidad', '/negocio/equipo', '/negocio/operaciones',
    '/trabajo/pendientes', '/trabajo/seguimiento', '/trabajo/perfil',
    '/mi-cuenta/explorar', '/mi-cuenta/solicitudes', '/mi-cuenta/historial'
  ]) {
    await page.goto(path + '?tenant_id=OTHER&role=admin&branch=UNTRUSTED');
    await expect(page.getByRole('heading', { level: 1, name: 'Página no disponible' })).toBeVisible();
    await expect(page.locator('cs-public-information, cs-pattern-harness, form, input')).toHaveCount(0);
  }
  expect(brandCalls).toBe(0);
  expect(privateCalls).toEqual([]);
});

test('unknown deep links and login company selector never create enrollment or privileged flows', async ({ page }) => {
  let brandCalls = 0;
  await page.route('**' + endpoint, route => {
    brandCalls++;
    return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(brand) });
  });
  for (const path of ['/login?empresa=OTHER', '/alta-empresas/registrar',
    '/demostraciones/real', '/experiencias/privado']) {
    await page.goto(path);
    await expect(page.getByRole('heading', { level: 1, name: 'Página no disponible' })).toBeVisible();
    await expect(page.locator('form, input, iframe, .public-card')).toHaveCount(0);
  }
  expect(brandCalls).toBe(0);
});

test('real public and denied journeys pass automated mobile accessibility without false certification', async ({ page }) => {
  await publicFixture(page);
  await page.setViewportSize({ width: 320, height: 760 });
  await page.emulateMedia({ reducedMotion: 'reduce' });
  for (const path of ['/experiencias', '/alta-empresas', '/consola/resumen']) {
    await page.goto(path);
    await expect(page.getByRole('main')).toHaveCount(1);
    await expect(page.getByRole('heading', { level: 1 })).toHaveCount(1);
    await verifyNoOverflow(page);
    const axe = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa']).analyze();
    expect(axe.violations).toEqual([]);
  }
});
