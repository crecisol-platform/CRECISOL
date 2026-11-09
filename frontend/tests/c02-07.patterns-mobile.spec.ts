import { expect, test, type Page } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

/** C02-07: isolated real Angular form/table components, synthetic fixtures only. */
async function noDocumentOverflow(page: Page): Promise<void> {
  const size = await page.evaluate(() => ({
    content: document.documentElement.scrollWidth, viewport: innerWidth
  }));
  expect(size.content, 'Scrollable table must not force whole-document horizontal scrolling')
    .toBeLessThanOrEqual(size.viewport + 1);
}

async function checkControls(page: Page, selector: string): Promise<void> {
  const controls = page.locator(selector);
  expect(await controls.count(), 'At least one relevant control must exist').toBeGreaterThan(0);
  for (const item of await controls.all()) {
    await expect(item).toBeVisible();
    const box = await item.boundingBox();
    expect(box).not.toBeNull();
    expect(box!.width).toBeGreaterThanOrEqual(44);
    expect(box!.height).toBeGreaterThanOrEqual(44);
  }
}

async function audit(page: Page): Promise<void> {
  const result = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa']).analyze();
  expect(result.violations).toEqual([]);
}

test('synthetic form and table reflow at 320, 360, 768 and 1280 CSS pixels', async ({ page }) => {
  for (const width of [320, 360, 768, 1280]) {
    await page.setViewportSize({ width, height: 740 });
    await page.goto('/');
    await expect(page.getByRole('heading', { level: 1, name: 'Patrones de formularios y tablas' })).toBeVisible();
    await expect(page.getByRole('table', { name: 'Tabla sintética de lectura' })).toBeVisible();
    await noDocumentOverflow(page);
    await checkControls(page, '.testing-controls button, cs-draft-form-pattern form button:not([disabled]), cs-table-pattern th button');
    if (width === 320) {
      const scroll = await page.locator('.table-scroll').evaluate(el => ({
        total: el.scrollWidth, visible: el.clientWidth
      }));
      expect(scroll.total).toBeGreaterThan(scroll.visible);
      await page.locator('.table-scroll').evaluate(el => { el.scrollLeft = el.scrollWidth; });
      await expect(page.getByRole('columnheader', { name: /Estado/ })).toBeVisible();
      await noDocumentOverflow(page);
    }
  }
});

test('200 percent text on 320px retains local validation and safe modal controls', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 740 });
  await page.goto('/');
  await page.addStyleTag({ content: 'html { font-size: 200% !important; }' });
  await noDocumentOverflow(page);
  const textbox = page.getByRole('textbox', { name: /Referencia sintética/ });
  await expect(textbox).toBeVisible();
  await textbox.fill('BORRADOR-7');
  await page.getByRole('button', { name: 'Descartar borrador' }).click();
  const dialog = page.getByRole('dialog', { name: '¿Descartar el borrador?' });
  await expect(dialog).toBeVisible();
  await checkControls(page, 'cs-draft-form-pattern dialog button');
  await noDocumentOverflow(page);
  await dialog.getByRole('button', { name: 'Conservar cambios' }).click();
  await expect(textbox).toHaveValue('BORRADOR-7');
});

test('synthetic scope changes require keyboard-operable modal before discarding unsaved text', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 760 });
  await page.goto('/');
  const input = page.getByRole('textbox', { name: /Referencia sintética/ });
  await input.fill('ONLY-A');
  await page.getByTestId('switch-context').click();
  const dialog = page.getByRole('dialog', { name: '¿Cambiar el ejemplo?' });
  await expect(dialog).toBeVisible();
  await checkControls(page, 'cs-pattern-harness > main > dialog button');
  await expect(dialog.getByRole('button', { name: 'Conservar borrador' })).toBeFocused();
  await page.keyboard.press('Escape');
  await expect(dialog).not.toBeVisible();
  await expect(input).toHaveValue('ONLY-A');
  await page.getByTestId('switch-context').click();
  await dialog.getByRole('button', { name: 'Descartar y cambiar' }).click();
  await expect(input).toHaveValue('');
  await expect(page.locator('tbody')).not.toContainText('A10');
  await expect(page.locator('tbody')).toContainText('B1');
  await noDocumentOverflow(page);
});

test('screen reader semantics and native dialogs pass automated mobile WCAG 2.2 AA', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 720 });
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await page.goto('/');
  await audit(page);
  const input = page.getByRole('textbox', { name: /Referencia sintética/ });
  await input.fill('SINTETICO');
  await page.getByRole('button', { name: 'Descartar borrador' }).click();
  await expect(page.getByRole('dialog', { name: '¿Descartar el borrador?' })).toBeVisible();
  await audit(page);
  await page.keyboard.press('Escape');
  await expect(input).toHaveValue('SINTETICO');
  await noDocumentOverflow(page);
});

test('forced colors preserve focus, table sort and no backend operation', async ({ page }) => {
  const calls: string[] = [];
  page.on('request', request => {
    if (new URL(request.url()).pathname.startsWith('/api/')) calls.push(request.url());
  });
  await page.setViewportSize({ width: 320, height: 740 });
  await page.emulateMedia({ forcedColors: 'active', reducedMotion: 'reduce' });
  await page.goto('/');
  await page.keyboard.press('Tab');
  await expect(page.getByTestId('switch-context')).toBeFocused();
  await page.getByRole('button', { name: 'Ordenar por Código' }).click();
  await expect(page.getByRole('columnheader', { name: /Código/ }))
    .toHaveAttribute('aria-sort', 'ascending');
  await noDocumentOverflow(page);
  expect(calls).toHaveLength(0);
});
