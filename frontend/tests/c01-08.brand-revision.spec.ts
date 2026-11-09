/** C01-08: regression of identity publication on the actual Angular page.
 * These tests validate versioned read-model consumption, NOT an admin upload UI.
 */
import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';
import AxeBuilder from '@axe-core/playwright';
import { expect, test, type Page } from '@playwright/test';

const endpoint = '/api/v1/public/branding/platform';
const initial = {
  revision: 10,
  name: 'CRECISOL',
  slogan: 'Tu negocio, a tu manera.',
  descriptor: 'Comercio · Servicios · Membresías · Crecimiento',
  primaryColor: '#062E65',
  accentColor: '#078FE8',
  logoUrl: '/assets/crecisol-oficial.png',
  websiteUrl: null,
  publishedAt: '2026-11-09T06:00:00+00:00',
  presentation: {
    faviconUrl: '/assets/crecisol-emblem.png',
    typographyToken: 'system', iconSetToken: 'outline',
    seoTitle: 'CRECISOL · Tu negocio, a tu manera.',
    seoDescription: 'Versión inicial de prueba.',
    copyrightNotice: 'Aviso original de prueba.'
  }
};
const changed = {
  ...initial, revision: 11, name: 'MARCA ACTUALIZADA',
  slogan: 'Una identidad nueva y autorizada.',
  descriptor: 'Servicios · Comercio',
  primaryColor: '#124578', accentColor: '#2379A8',
  logoUrl: '/assets/logo-actualizado.png',
  publishedAt: '2026-11-09T07:00:00+00:00',
  presentation: {
    ...initial.presentation, faviconUrl: '/assets/icono-actualizado.png',
    seoTitle: 'MARCA ACTUALIZADA · Publicación',
    seoDescription: 'Descripción publicada nueva.',
    copyrightNotice: 'Aviso publicado nuevo.'
  }
};

async function serveSnapshot(page: Page, getCurrent: () => unknown) {
  await page.route('**' + endpoint, route => route.fulfill({
    status: 200, contentType: 'application/json',
    headers: { 'cache-control': 'no-store' },
    body: JSON.stringify(getCurrent())
  }));
}

test('a newly fetched published revision changes header, hero, favicon, SEO and footer together', async ({ page }) => {
  let publication: typeof initial = initial;
  let requests = 0;
  await page.route('**' + endpoint, route => {
    requests++;
    return route.fulfill({
      status: 200, contentType: 'application/json',
      headers: { 'cache-control': 'no-store' },
      body: JSON.stringify(publication)
    });
  });
  const originalBytes = readFileSync('public/assets/crecisol-oficial.png');
  expect(createHash('sha256').update(originalBytes).digest('hex'))
    .toBe('9e83d1d0a891565cf80a21a7e90d7afcaa24c9bb63e7d2a589790127dadd549f');
  await page.route('**/assets/logo-actualizado.png', route => route.fulfill({
    status: 200, contentType: 'image/png', body: originalBytes
  }));
  await page.route('**/assets/icono-actualizado.png', route => route.fulfill({
    status: 200, contentType: 'image/png',
    body: readFileSync('public/assets/crecisol-emblem.png')
  }));
  await page.goto('/');
  await expect(page.locator('.wordmark-name')).toHaveText('CRECISOL');
  await expect(page.locator('.header-brand-icon'))
    .toHaveAttribute('src', '/assets/crecisol-emblem.png');
  publication = changed;
  await page.reload();
  await expect(page.locator('.wordmark-name')).toHaveText('MARCA ACTUALIZADA');
  await expect(page.locator('.header-brand-icon'))
    .toHaveAttribute('src', changed.presentation.faviconUrl);
  await expect(page.locator('.brand-art-logo')).toHaveAttribute('src', changed.logoUrl);
  await expect(page.locator('.brand-art--logo-ready')).toHaveCount(1);
  await expect(page.getByRole('heading', { level: 1 })).toHaveText(changed.slogan);
  await expect(page.locator('.footer-brand')).toHaveText(changed.name);
  await expect(page.locator('.footer-notice')).toHaveText(changed.presentation.copyrightNotice);
  await expect(page.locator('meta[name="description"]'))
    .toHaveAttribute('content', changed.presentation.seoDescription);
  await expect(page.locator('link[rel="icon"][data-crecisol-managed="true"]'))
    .toHaveAttribute('href', changed.presentation.faviconUrl);
  await expect(page.locator('link[rel="icon"][data-crecisol-default]')).toHaveCount(0);
  await expect(page).toHaveTitle(changed.presentation.seoTitle);
  expect(requests).toBe(2);
  expect(await page.evaluate(() => [localStorage.length, sessionStorage.length])).toEqual([0,0]);
  // The fixture replaces the URL while retaining known test bytes. It does NOT
  // demonstrate admin upload, transformation pipeline, push, or live cross-session propagation.
});

test('an invalid branded icon URL rejects the full public snapshot, not only its icon', async ({ page }) => {
  await serveSnapshot(page, () => ({
    ...changed, presentation: {
      ...changed.presentation, faviconUrl: 'https://untrusted.invalid/icon.png'
    }
  }));
  let external = 0;
  page.on('request', req => { if (req.url().includes('untrusted.invalid')) external++; });
  await page.goto('/');
  await expect(page.getByRole('alert')).toContainText('no está disponible');
  await expect(page.locator('.header-brand-icon')).toHaveCount(0);
  await expect(page.locator('link[data-crecisol-managed]')).toHaveCount(0);
  await expect(page.locator('link[data-crecisol-default]')).toHaveCount(0);
  expect(external).toBe(0);
});

test('missing optional favicon uses the published logo in header but no stale browser icon', async ({ page }) => {
  await serveSnapshot(page, () => ({ ...initial, presentation: null }));
  await page.goto('/');
  await expect(page.locator('.header-brand-icon')).toHaveAttribute('src', initial.logoUrl);
  await expect(page.locator('link[rel="icon"]')).toHaveCount(0);
  await expect(page.locator('.wordmark-name')).toHaveText(initial.name);
});

test('a missing published header image keeps the current public name, not the former brand', async ({ page }) => {
  await serveSnapshot(page, () => changed);
  await page.route('**/assets/icono-actualizado.png', route => route.fulfill({ status: 404, body: '' }));
  await page.goto('/');
  await expect(page.locator('.header-brand-icon')).toHaveCount(0);
  await expect(page.locator('.wordmark-line')).toBeVisible();
  await expect(page.locator('.wordmark-name')).toHaveText(changed.name);
  await expect(page.locator('link[data-crecisol-default]')).toHaveCount(0);
  const result = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa']).analyze();
  expect(result.violations).toEqual([]);
});
