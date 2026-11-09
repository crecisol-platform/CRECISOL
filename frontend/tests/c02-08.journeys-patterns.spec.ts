import { expect, test } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

/** C02-08: isolated real Angular test component journeys only.
 * Never linked into src/main.ts, a public route or a backend transaction.
 */

test('synthetic journey has no real API requests, PII or commercial confirmation', async ({ page }) => {
  const calls: string[] = [];
  page.on('request', request => {
    if (new URL(request.url()).pathname.startsWith('/api/')) calls.push(request.url());
  });
  await page.goto('/');
  await expect(page.getByRole('heading', { level: 1, name: 'Patrones de formularios y tablas' })).toBeVisible();
  await expect(page.getByText('no forma parte de la aplicación pública', { exact: false })).toBeVisible();
  await expect(page.getByRole('table', { name: 'Tabla sintética de lectura' })).toBeVisible();
  const input = page.getByRole('textbox', { name: /Referencia sintética/ });
  await input.fill('JOURNEY-08');
  await page.getByRole('button', { name: 'Revisar formato local' }).click();
  await expect(page.getByText('Ningún dato fue enviado ni guardado.')).toBeVisible();
  expect(calls).toEqual([]);
  expect(await page.evaluate(() => [localStorage.length, sessionStorage.length])).toEqual([0, 0]);
});

test('draft journey cancel, Escape and explicit discard preserve correct synthetic context', async ({ page }) => {
  await page.goto('/');
  const input = page.getByRole('textbox', { name: /Referencia sintética/ });
  await input.fill('NO-SAVE');
  await page.getByTestId('switch-context').click();
  const dialog = page.getByRole('dialog', { name: '¿Cambiar el ejemplo?' });
  await expect(dialog).toBeVisible();
  await dialog.getByRole('button', { name: 'Conservar borrador' }).click();
  await expect(input).toHaveValue('NO-SAVE');
  await page.getByTestId('switch-context').click();
  await page.keyboard.press('Escape');
  await expect(dialog).not.toBeVisible();
  await expect(input).toHaveValue('NO-SAVE');
  await page.getByTestId('switch-context').click();
  await dialog.getByRole('button', { name: 'Descartar y cambiar' }).click();
  await expect(input).toHaveValue('');
  await expect(page.locator('tbody')).not.toContainText('A10');
  await expect(page.locator('tbody')).toContainText('B1');
});

test('table journey filters and sorts only fixture rows, then clears on context switch', async ({ page }) => {
  await page.goto('/');
  const filter = page.getByRole('searchbox', { name: 'Filtrar filas de ejemplo' });
  await filter.fill('Registro 2');
  await expect(page.locator('tbody tr')).toHaveCount(1);
  await expect(page.locator('tbody tr').first()).toContainText('A2');
  await filter.fill('');
  await page.getByRole('button', { name: 'Ordenar por Código' }).click();
  await expect(page.getByRole('columnheader', { name: /Código/ })).toHaveAttribute('aria-sort', 'ascending');
  await page.getByTestId('switch-context').click();
  await expect(filter).toHaveValue('');
  await expect(page.locator('tbody tr')).toHaveCount(2);
  await expect(page.locator('tbody')).toContainText('B1');
  await expect(page.locator('tbody')).not.toContainText('A2');
  expect(await page.evaluate(() => [localStorage.length, sessionStorage.length])).toEqual([0, 0]);
});

test('unsafe synthetic schema and HTML payload never execute or retain old private-looking text', async ({ page }) => {
  await page.goto('/');
  const input = page.getByRole('textbox', { name: /Referencia sintética/ });
  await input.fill('<img>');
  await page.getByRole('button', { name: 'Revisar formato local' }).click();
  await expect(input).toHaveAttribute('aria-invalid', 'true');
  await expect(page.locator('cs-draft-form-pattern img, cs-draft-form-pattern iframe, cs-draft-form-pattern script'))
    .toHaveCount(0);
  await input.fill('');
  await page.getByTestId('invalid-schema').click();
  await expect(page.getByRole('alert')).toContainText('configuración inválida');
  await expect(page.getByRole('textbox', { name: /Referencia sintética/ })).toHaveCount(0);
  await page.getByTestId('invalid-schema').click();
  await expect(page.getByRole('textbox', { name: /Referencia sintética/ })).toHaveValue('');
  await expect(page.locator('tbody svg, tbody iframe, tbody script')).toHaveCount(0);
});

test('synthetic mobile journey keeps focus and localized table scroll under axe automated checks', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 720 });
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await page.goto('/');
  await expect(page.getByRole('heading', { level: 1, name: 'Patrones de formularios y tablas' })).toBeVisible();
  const input = page.getByRole('textbox', { name: /Referencia sintética/ });
  await input.fill('TEMPORAL');
  await page.getByRole('button', { name: 'Descartar borrador' }).click();
  const dialog = page.getByRole('dialog', { name: '¿Descartar el borrador?' });
  await expect(dialog).toBeVisible();
  await expect(dialog.getByRole('button', { name: 'Conservar cambios' })).toBeFocused();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1)).toBe(true);
  let check = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa']).analyze();
  expect(check.violations).toEqual([]);
  await dialog.getByRole('button', { name: 'Conservar cambios' }).click();
  await page.setViewportSize({ width: 1280, height: 800 });
  await expect(page.getByRole('table', { name: 'Tabla sintética de lectura' })).toBeVisible();
  check = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa']).analyze();
  expect(check.violations).toEqual([]);
});
