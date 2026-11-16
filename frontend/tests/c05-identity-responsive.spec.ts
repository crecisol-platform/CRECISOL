import { expect, test } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

for (const viewport of [{ width: 390, height: 844 }, { width: 1366, height: 768 }]) {
  test(`C05 account experience is accessible and has no horizontal overflow at ${viewport.width}px`, async ({ page }) => {
    await page.setViewportSize(viewport);
    await page.goto('/cuenta');
    const login = page.getByRole('region', { name: 'Iniciar sesión', exact: true });
    await expect(login.getByLabel('Correo electrónico', { exact: true })).toBeVisible();
    await expect(login.getByLabel('Contraseña', { exact: true })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Confirmar correo', exact: true })).toHaveCount(0);
    await expect(page.getByLabel('Correo del operador de identidad')).toHaveCount(0);
    const overflow = await page.evaluate(() => Math.max(
      0, document.documentElement.scrollWidth - document.documentElement.clientWidth));
    expect(overflow).toBeLessThanOrEqual(2);
    const results = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa']).analyze();
    expect(results.violations.filter(v => v.impact === 'critical' || v.impact === 'serious')).toEqual([]);
    await page.getByRole('link', { name: 'Recuperar acceso', exact: true }).click();
    await expect(page.getByLabel('Correo para recuperación')).toBeVisible();
    await expect(page.getByLabel('Contraseña', { exact: true })).toHaveCount(0);
  });
}
