import { expect, test, type Page } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

async function noDocumentOverflow(page: Page): Promise<void> {
  const m = await page.evaluate(() => ({
    page: document.documentElement.scrollWidth,
    viewport: document.documentElement.clientWidth
  }));
  expect(m.page, 'The document must not scroll horizontally').toBeLessThanOrEqual(m.viewport + 2);
}

async function assertA11y(page: Page): Promise<void> {
  const results = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa']).analyze();
  expect(results.violations).toEqual([]);
}

for (const viewport of [
  { width: 320, height: 720 }, { width: 390, height: 844 },
  { width: 768, height: 1024 }, { width: 1024, height: 768 },
  { width: 1440, height: 900 }
]) {
  test('F02 | all primitives reflow and preserve semantics at ' + viewport.width + 'px',
    async ({ page }, testInfo) => {
      await page.setViewportSize(viewport);
      await page.goto('/');
      await expect(page.getByRole('heading', { name: 'Sistema institucional de interfaces' })).toBeVisible();
      await expect(page.getByRole('navigation', { name: 'Áreas de ejemplo' })).toBeVisible();
      await expect(page.getByRole('table', { name: 'Dispositivos ficticios' })).toBeVisible();
      await expect(page.getByRole('textbox', { name: /Correo de prueba/ })).toBeVisible();
      await noDocumentOverflow(page);
      await assertA11y(page);
      await page.screenshot({
        path: testInfo.outputPath('f02-institutional-' + viewport.width + '.png'),
        fullPage: true, animations: 'disabled'
      });
    });
}

test('F02 | navigation, action intents and local field validation are keyboard accessible',
  async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto('/');
    const nav = page.getByRole('navigation', { name: 'Áreas de ejemplo' });
    await nav.getByRole('button', { name: 'Seguridad', exact: true }).click();
    await expect(nav.getByRole('button', { name: 'Seguridad', exact: true }))
      .toHaveAttribute('aria-current', 'page');
    const field = page.getByRole('textbox', { name: /Correo de prueba/ });
    await expect(field).toHaveAttribute('aria-describedby', 'demo-email-hint');
    await page.getByRole('button', { name: 'Revisar formato' }).click();
    await expect(page.getByRole('alert', { name: '' }).filter({ hasText: /Revisa el formato/ })).toBeVisible();
    await expect(field).toHaveAttribute('aria-invalid', 'true');
    await field.fill('usuario@example.test');
    await page.getByRole('button', { name: 'Revisar formato' }).click();
    await expect(field).not.toHaveAttribute('aria-invalid', 'true');
    await expect(page.getByTestId('action-feedback'))
      .toContainText('No se envió ninguna solicitud');
    await noDocumentOverflow(page);
  });

test('F02 | authorized row scope switch hides stale rows and escapes markup',
  async ({ page }) => {
    await page.goto('/');
    const table = page.getByRole('table', { name: 'Dispositivos ficticios' });
    await expect(table.getByText('Chromium · ejemplo')).toBeVisible();
    await expect(table.getByText('<img src=x onerror=alert(1)>')).toBeVisible();
    await expect(table.locator('img')).toHaveCount(0);
    await page.getByTestId('change-scope').getByRole('button').click();
    await expect(table).toHaveCount(0);
    await expect(page.getByText('Los datos de este ámbito aún no están disponibles.')).toBeVisible();
    await page.getByTestId('reload-scope').getByRole('button').click();
    await expect(page.getByRole('table', { name: 'Dispositivos ficticios' })).toBeVisible();
  });

test('F02 | fixed-button contrast is unaffected by invalid published colors',
  async ({ page }) => {
    await page.goto('/');
    const root = page.getByTestId('institutional-theme');
    const primary = page.getByRole('button', { name: 'Acción principal', exact: true });
    const before = await primary.evaluate(el => getComputedStyle(el).backgroundColor);
    await page.getByTestId('theme-humanist').getByRole('button').click();
    await expect(root).toHaveAttribute('data-cs-font', 'humanist');
    await expect(root).toHaveAttribute('data-cs-icon', 'solid');
    await page.getByTestId('theme-invalid').getByRole('button').click();
    await expect(root).toHaveAttribute('data-cs-font', 'system');
    const primaryToken = await root.evaluate(el =>
      getComputedStyle(el).getPropertyValue('--cs-brand-primary').trim());
    const accentToken = await root.evaluate(el =>
      getComputedStyle(el).getPropertyValue('--cs-brand-accent').trim());
    expect(primaryToken).toBe('#0d468b');
    expect(accentToken).toBe('#1878c8');
    expect(await primary.evaluate(el => getComputedStyle(el).backgroundColor)).toBe(before);
    expect(await page.evaluate(() => performance.getEntriesByType('resource')
      .filter(r => r.name.includes('invalid.example')).length)).toBe(0);
  });

test('F02 | busy and disabled actions cannot be activated; status semantics are explicit',
  async ({ page }) => {
    await page.goto('/');
    await expect(page.getByRole('button', { name: 'Acción no disponible' })).toBeDisabled();
    await expect(page.getByRole('button', { name: /Procesando/ })).toBeDisabled();
    await expect(page.getByRole('button', { name: /Procesando/ })).toHaveAttribute('aria-busy', 'true');
    await page.getByRole('button', { name: 'Acción secundaria' }).click();
    await expect(page.getByTestId('action-feedback')).toContainText('Ningún dato fue modificado');
    await page.getByRole('button', { name: 'Cargando', exact: true }).click();
    const pending = page.getByRole('status').filter({ hasText: 'Consultando' });
    await expect(pending).toHaveAttribute('aria-busy', 'true');
    await page.getByRole('button', { name: 'Error', exact: true }).click();
    await expect(page.getByRole('alert').filter({ hasText: 'No se pudo consultar' })).toBeVisible();
  });

test('F02 | 200% text scaling at 320px stays usable',
  async ({ page }) => {
    await page.setViewportSize({ width: 320, height: 720 });
    await page.goto('/');
    await page.addStyleTag({ content: ':root { font-size: 200% !important; }' });
    await noDocumentOverflow(page);
    const nav = page.getByRole('navigation', { name: 'Áreas de ejemplo' });
    await nav.getByRole('button', { name: 'Llaves', exact: true }).click();
    await expect(nav.getByRole('button', { name: 'Llaves', exact: true }))
      .toHaveAttribute('aria-current', 'page');
    await assertA11y(page);
  });

test('F02 | reduced motion and forced colors preserve real controls and no API activity',
  async ({ page }) => {
    const apiCalls: string[] = [];
    page.on('request', r => {
      const url = new URL(r.url());
      if (url.pathname.startsWith('/api/') || url.hostname !== '127.0.0.1') apiCalls.push(r.url());
    });
    await page.setViewportSize({ width: 320, height: 740 });
    await page.emulateMedia({ forcedColors: 'active', reducedMotion: 'reduce' });
    await page.goto('/');
    await page.getByRole('button', { name: 'Error', exact: true }).click();
    await noDocumentOverflow(page);
    await assertA11y(page);
    expect(apiCalls).toEqual([]);
  });
