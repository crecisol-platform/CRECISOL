import { expect, test, type Page } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

/** C02-07: real public Angular routes and synthetic platform publication only.
 * No real tenant, registration, account, payment or authorization inference.
 */
const endpoint = '/api/v1/public/branding/platform';
const brand = {
  revision: 7,
  name: 'CRECISOL',
  slogan: 'Tu negocio, a tu manera.',
  descriptor: 'Comercio · Servicios · Membresías · Crecimiento',
  primaryColor: '#062E65', accentColor: '#078FE8',
  logoUrl: '/assets/crecisol-oficial.png', websiteUrl: null,
  publishedAt: '2026-11-09T07:00:00+00:00',
  presentation: {
    faviconUrl: '/assets/crecisol-emblem.png',
    typographyToken: 'system', iconSetToken: 'outline',
    seoTitle: 'CRECISOL · Tu negocio, a tu manera.',
    seoDescription: 'Identidad pública publicada.',
    copyrightNotice: 'Identidad institucional publicada.'
  }
};

async function mockPublished(page: Page, payload: unknown = brand): Promise<string[]> {
  const calls: string[] = [];
  await page.route('**' + endpoint, async route => {
    const req = route.request();
    expect(req.method()).toBe('GET');
    expect(new URL(req.url()).search).toBe('');
    expect(req.headers()['authorization']).toBeUndefined();
    expect(req.headers()['x-tenant-id']).toBeUndefined();
    calls.push(req.url());
    await route.fulfill({ status: 200, contentType: 'application/json',
      headers: { 'cache-control': 'no-store' }, body: JSON.stringify(payload) });
  });
  return calls;
}

async function noDocumentOverflow(page: Page): Promise<void> {
  const size = await page.evaluate(() => ({
    content: document.documentElement.scrollWidth, viewport: innerWidth
  }));
  expect(size.content, 'Only a dedicated table region may scroll horizontally')
    .toBeLessThanOrEqual(size.viewport + 1);
}

async function touchTargets(page: Page, selector: string): Promise<void> {
  for (const link of await page.locator(selector).all()) {
    await expect(link).toBeVisible();
    const box = await link.boundingBox();
    expect(box, 'Visible control must have geometry').not.toBeNull();
    expect(box!.width, '44 CSS pixel touch width').toBeGreaterThanOrEqual(44);
    expect(box!.height, '44 CSS pixel touch height').toBeGreaterThanOrEqual(44);
  }
}

async function audit(page: Page): Promise<void> {
  const a11y = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa']).analyze();
  expect(a11y.violations).toEqual([]);
}

test('home exposes all navigation and touch targets at 320, 360, 768 and 1280px', async ({ page }) => {
  const calls = await mockPublished(page);
  for (const width of [320, 360, 768, 1280]) {
    await page.setViewportSize({ width, height: 800 });
    await page.goto('/');
    await expect(page.getByRole('heading', { level: 1 })).toHaveText(brand.slogan);
    await expect(page.getByRole('navigation', { name: 'Navegación principal' })
      .getByRole('link')).toHaveCount(3);
    await noDocumentOverflow(page);
    await touchTargets(page, '.primary-nav a, .primary-action, .site-footer .back-to-top');
  }
  expect(calls).toHaveLength(4);
});

test('all three public information pages preserve cards and visible mobile/tablet/desktop links', async ({ page }) => {
  const calls = await mockPublished(page);
  for (const width of [320, 768, 1280]) {
    await page.setViewportSize({ width, height: 780 });
    for (const [path, count] of [['/experiencias', 6], ['/demostraciones', 3],
      ['/alta-empresas', 3]] as const) {
      await page.goto(path);
      await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
      await expect(page.locator('.public-card')).toHaveCount(count);
      await expect(page.getByRole('navigation', { name: 'Navegación pública' })
        .getByRole('link')).toHaveCount(4);
      await touchTargets(page, '.public-nav a, .public-note a');
      await noDocumentOverflow(page);
      await expect(page.locator('form, input, select, iframe')).toHaveCount(0);
    }
  }
  expect(calls).toHaveLength(9);
});

test('skip links transfer keyboard focus to a real main region', async ({ page }) => {
  await mockPublished(page);
  for (const path of ['/', '/experiencias']) {
    await page.setViewportSize({ width: 320, height: 720 });
    await page.goto(path);
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
    await page.keyboard.press('Tab');
    await expect(page.getByRole('link', { name: 'Saltar al contenido principal' })).toBeFocused();
    await page.keyboard.press('Enter');
    await expect(page).toHaveURL(/#contenido$/);
    await expect(page.locator('main#contenido')).toBeFocused();
  }
});

test('200 percent font resizing keeps public text and navigation readable without overflow', async ({ page }) => {
  await mockPublished(page);
  await page.setViewportSize({ width: 320, height: 760 });
  for (const path of ['/', '/experiencias', '/alta-empresas']) {
    await page.goto(path);
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
    await page.addStyleTag({ content: 'html { font-size: 200% !important; }' });
    await noDocumentOverflow(page);
    await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
    if (path === '/') {
      await expect(page.getByRole('link', { name: 'Conocer la propuesta' })).toBeVisible();
      await expect(page.locator('.primary-nav a')).toHaveCount(3);
    } else {
      await expect(page.locator('.public-nav a')).toHaveCount(4);
    }
  }
});

test('published long brand tokens reflow at 320px without tenant-oriented navigation', async ({ page }) => {
  const long = {
    ...brand,
    name: 'PLATAFORMACRECISOL'.repeat(5),
    slogan: 'UNAEXPERIENCIAPUBLICAPARAMUCHOSNEGOCIOS'.repeat(3),
    descriptor: 'ÁMBITOSUNIVERSALESPUBLICADOS'.repeat(6),
    presentation: { ...brand.presentation, typographyToken: 'editorial' }
  };
  expect(long.name.length).toBeLessThanOrEqual(100);
  expect(long.slogan.length).toBeLessThanOrEqual(160);
  expect(long.descriptor.length).toBeLessThanOrEqual(180);
  const calls = await mockPublished(page, long);
  await page.setViewportSize({ width: 320, height: 780 });
  await page.goto('/');
  await expect(page.getByRole('heading', { level: 1 })).toHaveText(long.slogan);
  await expect(page.locator('.wordmark-name')).toContainText(long.name);
  await noDocumentOverflow(page);
  await page.goto('/experiencias?tenant_id=OTHER&scope=Tenant');
  await expect(page.locator('.public-wordmark')).toContainText(long.name);
  await noDocumentOverflow(page);
  await expect(page.locator('body')).not.toContainText('PRIVATE_TENANT_B');
  expect(calls).toHaveLength(2);
});

test('reduced motion error page has keyboard-operable retry and no privileged success', async ({ page }) => {
  await page.route('**' + endpoint, route => route.fulfill({
    status: 503, contentType: 'application/json', body: '{}'
  }));
  await page.setViewportSize({ width: 320, height: 720 });
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await page.goto('/');
  await expect(page.getByRole('alert')).toContainText('no está disponible');
  await noDocumentOverflow(page);
  await touchTargets(page, '.retry-button');
  await page.keyboard.press('Tab');
  await expect(page.getByRole('button', { name: /Intentar nuevamente/ })).toBeFocused();
  await page.keyboard.press('Enter');
  await expect(page.getByRole('alert')).toBeVisible();
  await audit(page);
});

test('mobile platform-info failure clears stale brand and retains accessible retry', async ({ page }) => {
  let status = 200;
  await page.route('**' + endpoint, route => route.fulfill({
    status, contentType: 'application/json',
    body: status === 200 ? JSON.stringify(brand) : JSON.stringify({ internal: 'PRIVATE_TENANT_B' })
  }));
  await page.setViewportSize({ width: 320, height: 720 });
  await page.goto('/experiencias');
  await expect(page.locator('.public-card')).toHaveCount(6);
  status = 503;
  await page.goto('/demostraciones');
  await expect(page.locator('[data-interaction-state="error"]')).toBeVisible();
  await expect(page.locator('.public-page, .public-card')).toHaveCount(0);
  await expect(page.locator('body')).not.toContainText('PRIVATE_TENANT_B');
  await expect(page.locator('meta[name="theme-color"], link[data-crecisol-managed]')).toHaveCount(0);
  await touchTargets(page, '.interaction-retry');
  await noDocumentOverflow(page);
  await audit(page);
});

test('public routes and 404 pass real WCAG 2.2 AA automated checks', async ({ page }) => {
  await mockPublished(page);
  for (const [path, width] of [
    ['/', 320], ['/experiencias', 320], ['/demostraciones', 768],
    ['/alta-empresas', 1280], ['/unknown-private-area', 320]
  ] as const) {
    await page.setViewportSize({ width, height: 760 });
    await page.goto(path);
    await expect(page.getByRole('main')).toHaveCount(1);
    await expect(page.getByRole('heading', { level: 1 })).toHaveCount(1);
    await noDocumentOverflow(page);
    await audit(page);
  }
});

test('forced colors retain visible keyboard focus and all public links on mobile', async ({ page }) => {
  await mockPublished(page);
  await page.setViewportSize({ width: 320, height: 700 });
  await page.emulateMedia({ forcedColors: 'active', reducedMotion: 'reduce' });
  await page.goto('/experiencias');
  await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
  await page.keyboard.press('Tab');
  await expect(page.getByRole('link', { name: 'Saltar al contenido principal' })).toBeFocused();
  const outline = await page.evaluate(() =>
    getComputedStyle(document.activeElement as HTMLElement).outlineStyle);
  expect(outline).not.toBe('none');
  await noDocumentOverflow(page);
  await touchTargets(page, '.public-nav a');
});

test('private-shaped routes stay denied at mobile size without platform GET or hidden controls', async ({ page }) => {
  let calls = 0;
  await page.route('**' + endpoint, route => {
    calls++;
    return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(brand) });
  });
  await page.setViewportSize({ width: 320, height: 700 });
  await page.goto('/negocio/otros-tenant?tenant=OTHER');
  await expect(page.getByRole('heading', { name: 'Página no disponible' })).toBeVisible();
  await expect(page.getByRole('link', { name: 'Volver al inicio' })).toBeVisible();
  await expect(page.locator('form, .public-page, .identity-page')).toHaveCount(0);
  await touchTargets(page, '.back-link');
  await noDocumentOverflow(page);
  expect(calls).toBe(0);
  await audit(page);
});
