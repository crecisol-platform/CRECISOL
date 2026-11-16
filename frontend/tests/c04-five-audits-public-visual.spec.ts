/** C01-C04 independent visual evidence. Public C01/C02 only; no production
 * UI or tenant API is inferred from these screenshots. */
import { expect, test } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

const published = {
  revision: 7,
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
    copyrightNotice: 'Aviso institucional de prueba.'
  }
};

const pages = [
  { path: '/', name: 'inicio', title: published.slogan },
  { path: '/experiencias', name: 'experiencias', title: 'Experiencias previstas' },
  { path: '/demostraciones', name: 'demostraciones', title: 'Demostraciones previstas' },
  { path: '/alta-empresas', name: 'alta-empresas', title: 'Alta de empresas: información' }
] as const;
const viewports = [
  { name: 'desktop', width: 1440, height: 900 },
  { name: 'mobile', width: 375, height: 812 }
] as const;

for (const viewport of viewports) {
  for (const expected of pages) {
    test(`visual audit C01/C02 ${expected.name} ${viewport.name}`, async ({ page }) => {
      await page.setViewportSize({ width: viewport.width, height: viewport.height });
      await page.emulateMedia({ reducedMotion: 'reduce' });
      await page.route('**/api/v1/public/branding/platform', route => route.fulfill({
        status: 200,
        contentType: 'application/json; charset=utf-8',
        headers: { 'cache-control': 'no-store' },
        body: JSON.stringify(published)
      }));
      await page.goto(expected.path);
      await expect(page.getByRole('heading', { level: 1 })).toHaveText(expected.title);
      await expect(page.locator('main')).toBeVisible();
      await expect(page.locator('main h1')).toHaveCount(1);
      expect(await page.evaluate(() =>
        document.documentElement.scrollWidth <= window.innerWidth + 1
      ), 'Document must not overflow viewport').toBe(true);
      const a11y = await new AxeBuilder({ page })
        .withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa'])
        .analyze();
      expect(a11y.violations, 'Automated WCAG 2.2 AA audit').toEqual([]);
      await page.screenshot({
        path: `../.c04-visual-proof/${expected.name}-${viewport.name}.png`,
        fullPage: true, animations: 'disabled'
      });
    });
  }
}
