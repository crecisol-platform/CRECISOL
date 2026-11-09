import { expect, test } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

/** Real Angular 22 components under build:pattern-harness. Fixture-only,
 * no production route, backend, user identity, tenant data or payment.
 */
test('the isolated preview renders only synthetic patterns and never calls an API', async ({ page }) => {
  const requests: string[] = [];
  page.on('request', request => {
    if (new URL(request.url()).pathname.startsWith('/api/')) requests.push(request.url());
  });
  await page.goto('/');
  await expect(page.getByRole('heading', { level: 1, name: 'Patrones de formularios y tablas' }))
    .toBeVisible();
  await expect(page.getByRole('textbox', { name: /Referencia sintética/ })).toBeVisible();
  await expect(page.getByRole('table', { name: 'Tabla sintética de lectura' })).toBeVisible();
  await expect(page.getByText('no forma parte de la aplicación pública', { exact: false }))
    .toBeVisible();
  expect(requests).toHaveLength(0);
});

test('local validation requires content and never claims a server save', async ({ page }) => {
  await page.goto('/');
  const input = page.getByRole('textbox', { name: /Referencia sintética/ });
  await page.getByRole('button', { name: 'Revisar formato local' }).click();
  await expect(input).toHaveAttribute('aria-invalid', 'true');
  await expect(page.getByRole('alert')).toContainText('Completa este campo');
  await input.fill('ABC-123');
  await expect(input).toHaveAttribute('aria-invalid', 'false');
  await page.getByRole('button', { name: 'Revisar formato local' }).click();
  await expect(page.getByRole('status').filter({ hasText: 'Formato del ejemplo válido' }))
    .toBeVisible();
  await expect(page.getByText('Ningún dato fue enviado ni guardado.')).toBeVisible();
  expect(await page.evaluate(() => [localStorage.length, sessionStorage.length])).toEqual([0, 0]);
});

test('HTML injection and disallowed identifiers are rejected and cannot execute markup', async ({ page }) => {
  await page.goto('/');
  const input = page.getByRole('textbox', { name: /Referencia sintética/ });
  await input.fill('<img>');
  await page.getByRole('button', { name: 'Revisar formato local' }).click();
  await expect(input).toHaveAttribute('aria-invalid', 'true');
  await expect(page.getByRole('alert')).toContainText('caracteres no admitidos');
  await expect(page.locator('cs-draft-form-pattern img, cs-draft-form-pattern svg, cs-draft-form-pattern iframe, cs-draft-form-pattern script')).toHaveCount(0);
  await input.fill('ABC+999');
  await page.getByRole('button', { name: 'Revisar formato local' }).click();
  await expect(page.getByRole('alert')).toContainText('Usa únicamente letras');
});

test('discard is never silent: cancel, Escape and explicit confirmation preserve or reset the draft', async ({ page }) => {
  await page.goto('/');
  const input = page.getByRole('textbox', { name: /Referencia sintética/ });
  await input.fill('UNSAVED-01');
  await page.getByRole('button', { name: 'Descartar borrador' }).click();
  const dialog = page.getByRole('dialog', { name: '¿Descartar el borrador?' });
  await expect(dialog).toBeVisible();
  await dialog.getByRole('button', { name: 'Conservar cambios' }).click();
  await expect(input).toHaveValue('UNSAVED-01');
  await page.getByRole('button', { name: 'Descartar borrador' }).click();
  await page.keyboard.press('Escape');
  await expect(dialog).not.toBeVisible();
  await expect(input).toHaveValue('UNSAVED-01');
  await page.getByRole('button', { name: 'Descartar borrador' }).click();
  await dialog.getByRole('button', { name: 'Sí, descartar' }).click();
  await expect(input).toHaveValue('');
  await expect(page.getByRole('button', { name: 'Descartar borrador' })).toBeDisabled();
});

test('table filtering and keyboard-accessible sorting never change underlying rows', async ({ page }) => {
  await page.goto('/');
  const rows = page.locator('tbody tr');
  await expect(rows).toHaveCount(3);
  await expect(rows.first().locator('td').first()).toHaveText('A10');
  await page.getByRole('button', { name: 'Ordenar por Código' }).click();
  await expect(page.getByRole('columnheader', { name: /Código/ })).toHaveAttribute('aria-sort', 'ascending');
  await expect(rows.first().locator('td').first()).toHaveText('A2');
  await page.getByRole('button', { name: 'Ordenar por Código' }).click();
  await expect(page.getByRole('columnheader', { name: /Código/ })).toHaveAttribute('aria-sort', 'descending');
  await expect(rows.first().locator('td').first()).toHaveText('A10');
  const filter = page.getByRole('searchbox', { name: 'Filtrar filas de ejemplo' });
  await filter.fill('SIN-FILAS');
  await expect(page.getByRole('status', { name: '' }).filter({ hasText: 'Ninguna fila coincide' }))
    .toBeVisible();
  await expect(page.getByText('Mostrando 0 filas de ejemplo.')).toBeVisible();
  await filter.fill('Registro 2');
  await expect(rows).toHaveCount(1);
  await expect(rows.first().locator('td').first()).toHaveText('A2');
});

test('scope changes clear the old draft, filter, sort and rows instead of leaking a previous context', async ({ page }) => {
  await page.goto('/');
  const input = page.getByRole('textbox', { name: /Referencia sintética/ });
  const filter = page.getByRole('searchbox', { name: 'Filtrar filas de ejemplo' });
  await input.fill('STALE-A');
  await filter.fill('A10');
  await expect(page.locator('tbody tr')).toHaveCount(1);
  await page.getByTestId('switch-context').click();
  const change = page.getByRole('dialog', { name: '¿Cambiar el ejemplo?' });
  await expect(change).toBeVisible();
  await change.getByRole('button', { name: 'Conservar borrador' }).click();
  await expect(input).toHaveValue('STALE-A');
  await page.getByTestId('switch-context').click();
  await change.getByRole('button', { name: 'Descartar y cambiar' }).click();
  await expect(input).toHaveValue('');
  await expect(filter).toHaveValue('');
  await expect(page.locator('tbody tr')).toHaveCount(2);
  await expect(page.locator('tbody')).not.toContainText('A10');
  await expect(page.locator('tbody')).toContainText('B1');
  await page.getByTestId('invalid-schema').click();
  await expect(page.getByRole('alert')).toContainText('configuración inválida');
  await expect(page.getByRole('textbox', { name: /Referencia sintética/ })).toHaveCount(0);
  await page.getByTestId('invalid-schema').click();
  await expect(page.getByRole('textbox', { name: /Referencia sintética/ })).toHaveValue('');
});

test('synthetic table content is text-interpolated, not rendered as executable SVG or HTML', async ({ page }) => {
  await page.goto('/');
  await expect(page.locator('tbody')).toContainText('<svg onload=alert(1)>');
  await expect(page.locator('tbody svg, tbody img, tbody iframe, tbody script')).toHaveCount(0);
  await expect(page.locator('tbody td')).toHaveCount(9);
});

test('320px layout, native dialog, table scroll and keyboard pass automated WCAG checks', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 700 });
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await page.goto('/');
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1))
    .toBe(true);
  await page.keyboard.press('Tab');
  await expect(page.getByTestId('switch-context')).toBeFocused();
  const input = page.getByRole('textbox', { name: /Referencia sintética/ });
  await input.fill('SINTETICO');
  await page.getByRole('button', { name: 'Descartar borrador' }).click();
  const dialog = page.getByRole('dialog', { name: '¿Descartar el borrador?' });
  await expect(dialog).toBeVisible();
  await expect(dialog.getByRole('button', { name: 'Conservar cambios' })).toBeFocused();
  let axe = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa']).analyze();
  expect(axe.violations).toEqual([]);
  await dialog.getByRole('button', { name: 'Conservar cambios' }).click();
  axe = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa']).analyze();
  expect(axe.violations).toEqual([]);
});
