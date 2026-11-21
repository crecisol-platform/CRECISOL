/** C05 UX F03 — navigation + information architecture visual QA.
 * Strictly synthetic API fixtures. Neither these tests nor the UI tabs
 * constitute real backend role/tenant authorization.
 */
import { expect, test, type Page, type TestInfo } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import { writeFile } from 'node:fs/promises';

type Realm = 'guest' | 'account' | 'operator';
const sizes = [
  { width: 320, height: 720 },
  { width: 390, height: 844 },
  { width: 768, height: 1024 },
  { width: 1024, height: 768 },
  { width: 1440, height: 900 }
] as const;

async function mockRealm(page: Page, realm: Realm): Promise<void> {
  // Default fail-closed for all unexpected API requests.
  await page.route('**/api/v1/**', route => route.fulfill({
    status: 503, contentType: 'application/json', body: '{}'
  }));
  await page.route('**/api/v1/public/branding/platform', route => route.fulfill({
    status: 200, contentType: 'application/json',
    body: JSON.stringify({
      revision: 1, name: 'CRECISOL', slogan: 'Tu negocio, a tu manera.',
      descriptor: 'Comercio · Servicios · Membresías', primaryColor: '#0d468b',
      accentColor: '#1878c8', logoUrl: '/assets/crecisol-oficial.png',
      websiteUrl: null, publishedAt: '2026-11-19T00:00:00Z',
      presentation: {
        faviconUrl: '/assets/crecisol-emblem.png',
        typographyToken: 'system', iconSetToken: 'outline',
        seoTitle: 'CRECISOL', seoDescription: 'Datos ficticios de QA',
        copyrightNotice: 'Prueba local ficticia'
      }
    })
  }));
  const session = () => ({
    csrfToken: 'a'.repeat(64),
    expiresAtUtc: new Date(Date.now() + 30 * 60_000).toISOString(),
    idleMinutes: 30
  });
  await page.route('**/api/v1/identity/browser/restore', route => route.fulfill({
    status: realm === 'account' ? 200 : 401,
    contentType: 'application/json',
    body: realm === 'account' ? JSON.stringify(session()) : '{}'
  }));
  await page.route('**/api/v1/platform-identity/restore', route => route.fulfill({
    status: realm === 'operator' ? 200 : 401,
    contentType: 'application/json',
    body: realm === 'operator' ? JSON.stringify(session()) : '{}'
  }));
  await page.route('**/api/v1/platform-identity/session-policy', route => route.fulfill({
    status: 200, contentType: 'application/json',
    body: JSON.stringify({ idleMinutes: 30, revision: 1, canManage: false })
  }));
}

async function noHorizontalOverflow(page: Page): Promise<void> {
  const m = await page.evaluate(() => ({
    document: document.documentElement.scrollWidth,
    viewport: document.documentElement.clientWidth
  }));
  expect(m.document, 'F03 must not scroll the document horizontally').toBeLessThanOrEqual(m.viewport + 2);
}

async function capture(page: Page, info: TestInfo, id: string, realm: Realm): Promise<void> {
  const png = info.outputPath(id + '.png');
  const data = await page.evaluate(() => ({
    pathname: location.pathname, search: location.search,
    overflow: Math.max(0, document.documentElement.scrollWidth - document.documentElement.clientWidth),
    heading: document.querySelector('h1')?.textContent?.trim() ?? null,
    navs: [...document.querySelectorAll('nav[aria-label]')].map(el => el.getAttribute('aria-label'))
  }));
  await page.screenshot({ path: png, fullPage: true, animations: 'disabled' });
  await writeFile(info.outputPath(id + '.json'), JSON.stringify({
    id, realm, synthetic: true, runnerSha: process.env.GITHUB_SHA ?? 'local',
    viewport: page.viewportSize(), png: id + '.png', ...data
  }, null, 2));
}

async function auditNavigation(page: Page, selector: string): Promise<void> {
  const results = await new AxeBuilder({ page }).include(selector)
    .withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa']).analyze();
  expect(results.violations).toEqual([]);
}

for (const size of sizes) {
  test('F03 guest: deep links and progressive management at ' + size.width + 'px',
    async ({ page }, info) => {
      await page.setViewportSize(size);
      await mockRealm(page, 'guest');
      await page.goto('/cuenta?vista=ingresar');
      await expect(page.getByRole('heading', { level: 1, name: 'Tu cuenta, a tu manera.' })).toBeVisible();
      const primary = page.getByRole('navigation', { name: 'Opciones de identidad' });
      await expect(primary.getByRole('link', { name: 'Iniciar sesión' }))
        .toHaveAttribute('aria-current', 'page');
      await expect(page.locator('.account-more')).not.toHaveAttribute('open');
      await expect(page.getByRole('navigation', { name: 'Ruta de navegación' }))
        .toContainText('Iniciar sesión');
      await noHorizontalOverflow(page);
      await auditNavigation(page, '.account-nav');
      await capture(page, info, 'guest-' + size.width, 'guest');

      await primary.getByRole('link', { name: 'Solicitar cuenta' }).click();
      await expect(page).toHaveURL(/vista=registro/);
      await expect(primary.getByRole('link', { name: 'Solicitar cuenta' }))
        .toHaveAttribute('aria-current', 'page');
      await page.goto('/cuenta?vista=recuperar');
      const disclosure = page.locator('details.account-more');
      await expect(disclosure).not.toHaveAttribute('open');
      await disclosure.locator('summary').focus();
      await page.keyboard.press('Enter');
      await expect(disclosure).toHaveAttribute('open', '');
      const secondary = page.getByRole('navigation', { name: 'Otras opciones de cuenta' });
      await expect(secondary.getByRole('link', { name: 'Recuperar acceso' }))
        .toHaveAttribute('aria-current', 'page');
      await noHorizontalOverflow(page);
      await auditNavigation(page, '.account-more');
      await capture(page, info, 'guest-recovery-' + size.width, 'guest');

      // Unauthenticated deep links cannot masquerade as privileged sections.
      await page.goto('/cuenta?vista=seguridad');
      await expect(primary.getByRole('link', { name: 'Iniciar sesión' }))
        .toHaveAttribute('aria-current', 'page');
      await expect(page.getByRole('navigation', { name: 'Mi cuenta' })).toHaveCount(0);
      await expect(page.getByRole('navigation', { name: 'Ruta de navegación' }))
        .toContainText('Iniciar sesión');
    });

  test('F03 account: responsive links and state-safe deep link at ' + size.width + 'px',
    async ({ page }, info) => {
      await page.setViewportSize(size);
      await mockRealm(page, 'account');
      await page.goto('/cuenta?vista=perfil');
      const nav = page.getByRole('navigation', { name: 'Mi cuenta' });
      await expect(nav).toBeVisible();
      await expect(page.getByRole('heading', { level: 1, name: 'Mi perfil' })).toBeVisible();
      await expect(nav.getByRole('link', { name: 'Mi perfil' }))
        .toHaveAttribute('aria-current', 'page');
      await expect(page.getByRole('navigation', { name: 'Ruta de navegación' }))
        .toContainText('Mi perfil');
      await auditNavigation(page, '.account-nav--workspace');
      await noHorizontalOverflow(page);
      await capture(page, info, 'account-profile-' + size.width, 'account');
      await nav.getByRole('link', { name: 'Seguridad' }).click();
      await expect(page).toHaveURL(/vista=seguridad/);
      await expect(nav.getByRole('link', { name: 'Seguridad' }))
        .toHaveAttribute('aria-current', 'page');
      await expect(page.getByText('Sección actual: Seguridad')).toHaveCount(1);
      await noHorizontalOverflow(page);
      await capture(page, info, 'account-security-' + size.width, 'account');
      // Return to unknown account-incompatible view, keeping URL but not inventing a section.
      await page.goto('/cuenta?vista=recuperar');
      await expect(nav.getByRole('link', { name: 'Resumen' }))
        .toHaveAttribute('aria-current', 'page');
    });

  test('F03 platform: separate navigation and policy contract at ' + size.width + 'px',
    async ({ page }, info) => {
      await page.setViewportSize(size);
      await mockRealm(page, 'operator');
      let policyCalls = 0;
      page.on('request', request => {
        if (request.url().includes('/api/v1/platform-identity/session-policy')) policyCalls++;
      });
      await page.goto('/cuenta?vista=operadores');
      const nav = page.getByRole('navigation', { name: 'Áreas de la consola de identidad' });
      await expect(nav).toBeVisible();
      await expect(page.getByRole('heading', { name: 'Identidad de plataforma', level: 1 })).toBeVisible();
      await expect(nav.getByRole('button', { name: 'Casos' }))
        .toHaveAttribute('aria-current', 'page');
      await expect(page.getByRole('navigation', { name: 'Mi cuenta' })).toHaveCount(0);
      await noHorizontalOverflow(page);
      await capture(page, info, 'operator-cases-' + size.width, 'operator');
      await nav.getByRole('button', { name: 'Seguridad' }).click();
      await expect(nav.getByRole('button', { name: 'Seguridad' }))
        .toHaveAttribute('aria-current', 'page');
      await expect(page.getByText('Factor MFA y política de inactividad.')).toBeVisible();
      await expect.poll(() => policyCalls).toBeGreaterThan(0);
      await noHorizontalOverflow(page);
      await auditNavigation(page, '.operator-navigation');
      await capture(page, info, 'operator-security-' + size.width, 'operator');
      await nav.getByRole('button', { name: 'Sesiones' }).click();
      await expect(nav.getByRole('button', { name: 'Sesiones' }))
        .toHaveAttribute('aria-current', 'page');
      await expect(page).toHaveURL(/vista=operadores/); // no private IDs in URL
    });
}

test('F03 keyboard: skip link and focus preserve native navigation', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await mockRealm(page, 'guest');
  await page.goto('/cuenta?vista=ingresar');
  await page.keyboard.press('Tab');
  await expect(page.getByRole('link', { name: 'Ir al contenido principal' })).toBeFocused();
  await page.keyboard.press('Enter');
  await expect(page.locator('#account-content')).toBeFocused();
  await expect(page).toHaveURL(/\/cuenta\?vista=ingresar$/);
  await page.getByRole('navigation', { name: 'Opciones de identidad' })
    .getByRole('link', { name: 'Solicitar cuenta' }).focus();
  await page.keyboard.press('Enter');
  await expect(page).toHaveURL(/vista=registro/);
});

test('F03 200% text at 320px, reduced motion and forced colors stay navigable',
  async ({ page }) => {
    await page.setViewportSize({ width: 320, height: 720 });
    await page.emulateMedia({ forcedColors: 'active', reducedMotion: 'reduce' });
    await mockRealm(page, 'guest');
    await page.goto('/cuenta?vista=ingresar');
    await page.addStyleTag({ content: ':root { font-size: 200% !important; }' });
    await noHorizontalOverflow(page);
    await page.locator('.account-more summary').click();
    await expect(page.getByRole('navigation', { name: 'Otras opciones de cuenta' }))
      .toBeVisible();
    await auditNavigation(page, '.account-more');
  });

test('F03 platform login remains separate when no operator authorization exists',
  async ({ page }) => {
    await mockRealm(page, 'guest');
    await page.goto('/cuenta?vista=operadores');
    await expect(page.getByRole('button', { name: 'Acceder a identidad de plataforma' })).toBeVisible();
    await expect(page.getByRole('navigation', { name: 'Áreas de la consola de identidad' }))
      .toHaveCount(0);
    await expect(page.getByRole('navigation', { name: 'Mi cuenta' })).toHaveCount(0);
    await noHorizontalOverflow(page);
  });
