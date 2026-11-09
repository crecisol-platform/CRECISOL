/**
 * C01-06: actual Angular logo slot. Unapproved Word bytes are intercepted
 * only in Playwright; they are NOT committed into the Angular application.
 */
import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import AxeBuilder from '@axe-core/playwright';
import { expect, test, type Page } from '@playwright/test';

const candidatePath = process.env['C01_06_CANDIDATE'];
const sha256 = '9e83d1d0a891565cf80a21a7e90d7afcaa24c9bb63e7d2a589790127dadd549f';
const logoPath = '/assets/c01-06-review.png';
const apiPath = '/api/v1/public/branding/platform';
const brand = {
  revision: 4, name: 'CRECISOL', slogan: 'Tu negocio, a tu manera.',
  descriptor: 'Comercio · Servicios · Membresías · Crecimiento',
  primaryColor: '#062E65', accentColor: '#078FE8', logoUrl: logoPath,
  websiteUrl: null, publishedAt: '2026-11-08T12:00:00+00:00',
  presentation: null
};

test.describe('C01-06 Angular preview (candidate NOT APPROVED)', () => {
  test.skip(!candidatePath, 'Only CI may inject the canonical unapproved candidate');

  async function serveBrand(page: Page, logoUrl = logoPath): Promise<void> {
    await page.route('**' + apiPath, route => route.fulfill({
      status: 200, contentType: 'application/json',
      headers: { 'Cache-Control': 'no-store' },
      body: JSON.stringify({ ...brand, logoUrl })
    }));
  }

  async function servePrivateCandidate(page: Page): Promise<void> {
    const bytes = readFileSync(candidatePath!);
    expect(createHash('sha256').update(bytes).digest('hex')).toBe(sha256);
    await page.route('**' + logoPath, route => route.fulfill({
      status: 200, contentType: 'image/png',
      headers: { 'Cache-Control': 'no-store', 'X-Content-Type-Options': 'nosniff' },
      body: bytes
    }));
  }

  for (const viewport of [
    { name: 'desktop', width: 1440, height: 900 },
    { name: 'mobile', width: 360, height: 780 }
  ]) {
    test('published local logo is intact and accessible on ' + viewport.name, async ({ page }) => {
      await page.setViewportSize({ width: viewport.width, height: viewport.height });
      await serveBrand(page);
      await servePrivateCandidate(page);
      await page.goto('/');
      await expect(page.getByRole('heading', { level: 1 })).toHaveText(brand.slogan);
      const img = page.locator('.brand-art-logo');
      await expect(page.locator('.brand-art--logo-ready')).toHaveCount(1);
      await expect(img).toBeVisible();
      await expect(img).toHaveAttribute('src', logoPath);
      await expect(img).toHaveAttribute('alt', 'Logotipo CRECISOL: Tu negocio, a tu manera.');
      const info = await img.evaluate(element => {
        const x = element as HTMLImageElement;
        return { w: x.naturalWidth, h: x.naturalHeight,
          fit: getComputedStyle(x).objectFit, width: x.getBoundingClientRect().width };
      });
      expect(info.w).toBe(1298);
      expect(info.h).toBe(859);
      expect(info.fit).toBe('contain');
      expect(info.width).toBeLessThan(viewport.width);
      expect(await page.evaluate(() =>
        document.documentElement.scrollWidth <= window.innerWidth + 1)).toBe(true);
      const axe = await new AxeBuilder({ page })
        .withTags(['wcag2a','wcag2aa','wcag21aa','wcag22aa']).analyze();
      expect(axe.violations).toEqual([]);
    });
  }

  test('missing file restores the original illustration with no broken image', async ({ page }) => {
    await serveBrand(page);
    await page.route('**' + logoPath, route => route.fulfill({ status: 404, body: '' }));
    await page.goto('/');
    await expect(page.getByRole('heading', { level: 1 })).toHaveText(brand.slogan);
    await expect(page.locator('.brand-art-logo')).toHaveCount(0);
    await expect(page.locator('.art-tile-front')).toBeVisible();
    await expect(page.locator('.brand-art--logo-ready')).toHaveCount(0);
  });

  test('bad remote, traversal or data logo URL is rejected without fetching it', async ({ page }) => {
    const outbound: string[] = [];
    page.on('request', req => { if (req.url().includes('evil.invalid')) outbound.push(req.url()); });
    for (const bad of ['https://evil.invalid/logo.png', '/assets/../tenant.png', 'data:image/png;base64,AA']) {
      await page.unrouteAll();
      await serveBrand(page, bad);
      await page.goto('/');
      await expect(page.getByRole('alert')).toContainText('no está disponible');
      await expect(page.locator('.brand-art-logo')).toHaveCount(0);
      expect(outbound).toHaveLength(0);
    }
  });
});
