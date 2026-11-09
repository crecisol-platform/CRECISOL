/** C01-06 final: true Angular app with both owner-approved images, not a mock image. */
import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import AxeBuilder from '@axe-core/playwright';
import { expect, test, type Page } from '@playwright/test';

const official = '/assets/crecisol-oficial.png';
const emblem = '/assets/crecisol-emblem.png';
const api = '/api/v1/public/branding/platform';
const expectedOfficial = '9e83d1d0a891565cf80a21a7e90d7afcaa24c9bb63e7d2a589790127dadd549f';
const expectedEmblem = 'ca789b73ee888cff86cf7849c27482546ce248f806676c5b9d34131e5302fd39';
const published = {
  revision: 5, name: 'CRECISOL', slogan: 'Tu negocio, a tu manera.',
  descriptor: 'Comercio · Servicios · Membresías · Crecimiento',
  primaryColor: '#062E65', accentColor: '#078FE8', logoUrl: official,
  websiteUrl: null, publishedAt: '2026-11-09T06:00:00+00:00',
  presentation: {
    faviconUrl: emblem, typographyToken: 'system', iconSetToken: 'outline',
    seoTitle: 'CRECISOL · Tu negocio, a tu manera.',
    seoDescription: 'Identidad pública de CRECISOL.',
    copyrightNotice: null
  }
};

async function ready(page: Page, replacement?: unknown): Promise<void> {
  await page.route('**' + api, route => route.fulfill({
    status: 200, contentType: 'application/json; charset=utf-8',
    headers: { 'Cache-Control': 'no-store' },
    body: JSON.stringify(replacement ?? published)
  }));
}

test('independent build assets preserve the approved full-image SHA and emblem pixels', () => {
  const hash = (file: string) => createHash('sha256').update(
    readFileSync('public/assets/' + file)).digest('hex');
  expect(hash('crecisol-oficial.png')).toBe(expectedOfficial);
  expect(hash('crecisol-emblem.png')).toBe(expectedEmblem);
});

for (const viewport of [
  { title: 'desktop-1440', width: 1440, height: 900 },
  { title: 'tablet-768', width: 768, height: 1024 },
  { title: 'mobile-360', width: 360, height: 780 },
  { title: 'compact-320', width: 320, height: 720 }
]) {
  test('official emblem visible at top and full logo present in hero on ' + viewport.title, async ({ page }) => {
    await page.setViewportSize({ width: viewport.width, height: viewport.height });
    await ready(page);
    await page.goto('/');
    const top = page.locator('.site-header .wordmark');
    await expect(top).toBeVisible();
    await expect(top).toContainText('CRECISOL');
    await expect(top).toHaveAttribute('aria-label', 'CRECISOL, ir al inicio');
    const symbol = top.locator('.header-brand-icon');
    await expect(symbol).toBeVisible();
    await expect(symbol).toHaveAttribute('src', emblem);
    expect(await symbol.evaluate(img => (img as HTMLImageElement).naturalWidth)).toBe(558);
    expect(await symbol.evaluate(img => (img as HTMLImageElement).naturalHeight)).toBe(537);
    await expect(top.locator('.wordmark-line')).toHaveCount(0);
    const hero = page.locator('.brand-art-logo');
    await expect(page.locator('.brand-art--logo-ready')).toHaveCount(1);
    await expect(hero).toBeVisible();
    await expect(hero).toHaveAttribute('src', official);
    const image = await hero.evaluate(img => {
      const node = img as HTMLImageElement;
      return { width: node.naturalWidth, height: node.naturalHeight,
        fit: getComputedStyle(node).objectFit };
    });
    expect(image).toEqual({ width: 1298, height: 859, fit: 'contain' });
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1)).toBe(true);
    await expect(page.getByRole('navigation', { name: 'Navegación principal' })).toBeVisible();
    const audit = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa']).analyze();
    expect(audit.violations).toEqual([]);
    if (viewport.title === 'desktop-1440' || viewport.title === 'mobile-360') {
      await page.screenshot({ path: 'test-results/C01-06-OFFICIAL-' + viewport.title + '.png', fullPage: true });
    }
  });
}

test('header image HTTP 404 returns text mark safely, without broken icon', async ({ page }) => {
  await ready(page);
  await page.route('**' + emblem, route => route.fulfill({ status: 404, body: '' }));
  await page.goto('/');
  await expect(page.getByRole('heading', { level: 1 })).toHaveText(published.slogan);
  await expect(page.locator('.header-brand-icon')).toHaveCount(0);
  await expect(page.locator('.wordmark-line')).toBeVisible();
  await expect(page.locator('.wordmark-name')).toHaveText('CRECISOL');
});

test('hero image HTTP 404 falls back without stale or fake brand', async ({ page }) => {
  await ready(page);
  await page.route('**' + official, route => route.fulfill({ status: 404, body: '' }));
  await page.goto('/');
  await expect(page.getByRole('heading', { level: 1 })).toHaveText(published.slogan);
  await expect(page.locator('.brand-art-logo')).toHaveCount(0);
  await expect(page.locator('.brand-art--logo-ready')).toHaveCount(0);
  await expect(page.locator('.art-tile-front')).toBeVisible();
  await expect(page.locator('.header-brand-icon')).toBeVisible();
});

test('API rejects remote, script, and tenant logo inputs without outbound asset request', async ({ page }) => {
  const outbound: string[] = [];
  page.on('request', req => { if (req.url().includes('evil.invalid')) outbound.push(req.url()); });
  for (const url of ['https://evil.invalid/x.png', 'javascript:alert(1)', '/assets/../tenant.png']) {
    await page.unrouteAll();
    await ready(page, { ...published, logoUrl: url });
    await page.goto('/');
    await expect(page.getByRole('alert')).toContainText('no está disponible');
    await expect(page.locator('.header-brand-icon')).toHaveCount(0);
    await expect(page.locator('.brand-art-logo')).toHaveCount(0);
  }
  expect(outbound).toHaveLength(0);
});

test('publisher-owned favicon and SEO remain platform scoped', async ({ page }) => {
  await ready(page);
  await page.goto('/');
  await expect(page).toHaveTitle(published.presentation.seoTitle);
  await expect(page.locator('link[rel="icon"][data-crecisol-managed="true"]'))
    .toHaveAttribute('href', emblem);
  await expect(page.locator('link[rel="icon"][data-crecisol-default="true"]')).toHaveCount(0);
  await expect(page.locator('meta[name="robots"]')).toHaveAttribute('content', 'noindex, nofollow');
  expect(await page.evaluate(() => [localStorage.length,sessionStorage.length])).toEqual([0,0]);
});
