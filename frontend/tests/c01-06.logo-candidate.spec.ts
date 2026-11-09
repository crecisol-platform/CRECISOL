/**
 * C01-06: isolated, temporary browser review for UNAPPROVED Word media.
 * The PNG is supplied by CI from /tmp; it is never installed in Angular.
 */
import { createHash } from 'node:crypto';
import { mkdirSync, readFileSync } from 'node:fs';
import AxeBuilder from '@axe-core/playwright';
import { test, expect } from '@playwright/test';

const candidatePath = process.env['C01_06_CANDIDATE'];
const expectedSha = '9e83d1d0a891565cf80a21a7e90d7afcaa24c9bb63e7d2a589790127dadd549f';
const originalDimensions = { width: 1298, height: 859 };
const reviewPath = '/__c01_06_review';
const imagePath = '/__c01_06_candidate.png';

const reviewHtml = String.raw`<!doctype html>
<html lang="es">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Revisión aislada — candidato CRECISOL NO APROBADO</title>
<style>
* { box-sizing: border-box; }
body { margin: 0; color: #162b45; background: #f1f5fa; font: 16px/1.55 system-ui, sans-serif; }
main { width: min(100% - 32px, 1024px); margin: 28px auto; padding: clamp(16px, 4vw, 36px); background: white; border-radius: 14px; }
h1 { margin: 0 0 12px; font-size: clamp(1.6rem, 4vw, 2.2rem); }
.warning { padding: 12px 16px; background: #fff4d6; color: #584008; border-left: 4px solid #a16b00; font-weight: 750; }
figure { margin: 20px 0; padding: 14px; border: 1px solid #b5c7db; border-radius: 10px; }
img { display: block; width: 100%; height: auto; object-fit: contain; }
figcaption { margin-top: 12px; font-size: .875rem; }
p { overflow-wrap: anywhere; }
</style>
</head>
<body>
<main>
<h1>Revisión aislada del candidato</h1>
<p class="warning" role="status">NO APROBADO · No es el logotipo instalado en el sitio.</p>
<figure>
<img id="candidate" src="/__c01_06_candidate.png" width="1298" height="859" alt="Imagen candidata extraída del Documento Maestro CRECISOL, pendiente de cotejo y aprobación humana">
<figcaption>Archivo original extraído, sin recorte, filtros, redibujado ni sustitución.</figcaption>
</figure>
<p>Si la imagen no carga, no debe considerarse aprobada. La vista solo sirve para cotejo gráfico y pruebas aisladas.</p>
</main>
<script>document.documentElement.dataset.unapprovedScriptRan = 'yes';</script>
</body>
</html>`;

const csp = "default-src 'none'; img-src 'self'; style-src 'unsafe-inline'; script-src 'none'; connect-src 'none'; object-src 'none'; frame-src 'none'; base-uri 'none'; form-action 'none'";
const previewHeaders = { 'Cache-Control': 'no-store', 'Content-Security-Policy': csp, 'X-Content-Type-Options': 'nosniff' };

test.describe('C01-06 isolated candidate, not application branding', () => {
  test.skip(!candidatePath, 'C01-06 isolated CI supplies C01_06_CANDIDATE; never read a public asset');

  test.beforeEach(async ({ page }) => {
    const data = readFileSync(candidatePath!);
    expect(createHash('sha256').update(data).digest('hex')).toBe(expectedSha);
    await page.route('**' + reviewPath, route => route.fulfill({
      status: 200, contentType: 'text/html; charset=utf-8', headers: previewHeaders, body: reviewHtml
    }));
    await page.route('**' + imagePath, route => route.fulfill({
      status: 200, contentType: 'image/png', headers: {
        'Cache-Control': 'no-store',
        'X-Content-Type-Options': 'nosniff'
      }, body: data
    }));
  });

  for (const viewport of [{ name: 'desktop', width: 1440, height: 900 },
                          { name: 'mobile', width: 360, height: 780 }]) {
    test('unapproved image fits ' + viewport.name + ' and WCAG automated scan', async ({ page }) => {
      await page.setViewportSize({ width: viewport.width, height: viewport.height });
      const response = await page.goto(reviewPath);
      expect(response?.status()).toBe(200);
      expect(response?.headers()['cache-control']).toBe('no-store');
      await expect(page.getByRole('status')).toContainText('NO APROBADO');
      const image = page.locator('#candidate');
      await expect(image).toBeVisible();
      await expect.poll(() => image.evaluate(img => (img as HTMLImageElement).naturalWidth))
        .toBe(originalDimensions.width);
      expect(await image.evaluate(img => (img as HTMLImageElement).naturalHeight))
        .toBe(originalDimensions.height);
      const bounds = await image.boundingBox();
      expect(bounds).not.toBeNull();
      expect(bounds!.width).toBeLessThanOrEqual(viewport.width);
      expect(bounds!.width / bounds!.height).toBeCloseTo(
        originalDimensions.width / originalDimensions.height, 2);
      const horizontalOverflow = await page.evaluate(() =>
        document.documentElement.scrollWidth > window.innerWidth);
      expect(horizontalOverflow).toBe(false);
      const audit = await new AxeBuilder({ page }).analyze();
      expect(audit.violations).toEqual([]);
      mkdirSync('test-results', { recursive: true });
      await page.screenshot({
        path: 'test-results/c01-06-review-NOT-APPROVED-' + viewport.name + '.png',
        fullPage: true
      });
    });
  }

  test('strict CSP blocks untrusted inline script and preserves same-origin resources', async ({ page }) => {
    const response = await page.goto(reviewPath);
    expect(response?.headers()['content-security-policy']).toContain("script-src 'none'");
    expect(await page.evaluate(() => document.documentElement.dataset['unapprovedScriptRan']))
      .toBeUndefined();
    await expect(page.locator('#candidate')).toBeVisible();
    await expect(page.locator('[data-tenant-id]')).toHaveCount(0);
    await expect(page.getByRole('status')).toContainText('NO APROBADO');
  });

  test('unavailable image cannot silently count as visual approval', async ({ page }) => {
    await page.route('**' + imagePath, route => route.fulfill({ status: 404, body: '' }));
    await page.goto(reviewPath);
    await expect.poll(() => page.locator('#candidate').evaluate(img =>
      (img as HTMLImageElement).complete)).toBe(true);
    expect(await page.locator('#candidate').evaluate(img =>
      (img as HTMLImageElement).naturalWidth)).toBe(0);
    await expect(page.getByRole('status')).toContainText('NO APROBADO');
    await expect(page.getByText('Si la imagen no carga')).toBeVisible();
  });
});
