import { expect, test } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';

for (const viewport of [{ width: 390, height: 844 }, { width: 1366, height: 768 }]) {
  test(`C05 institutional modal: focus, Escape, privacy and WCAG at ${viewport.width}px`, async ({ page }) => {
    await page.setViewportSize(viewport);
    await page.goto('/cuenta?vista=operadores');
    const opener = page.getByRole('button', { name: 'Recuperar acceso de operador de plataforma', exact: true });
    const modal = page.getByRole('dialog', { name: 'Recuperación segura de operador', exact: true });
    await expect(opener).toBeVisible();
    await expect(modal).toHaveCount(0); // Closed modal must not enter the accessible tree.
    await expect(page.getByLabel('Correo del operador sin acceso')).not.toBeVisible();
    await opener.click();
    await expect(modal).toBeVisible();
    await expect(page.locator('dialog.cs-modal:modal')).toHaveCount(1);
    await expect(modal.getByLabel('Correo del operador sin acceso')).toBeVisible();
    // Native modal makes the background inert and traps keyboard navigation.
    await expect(page.getByLabel('Correo del operador de identidad')).not.toBeFocused();
    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa']).analyze();
    expect(results.violations.filter(v => ['critical', 'serious'].includes(v.impact ?? ''))).toEqual([]);
    const outsideWidth = await page.evaluate(() => Math.max(0,
      document.documentElement.scrollWidth - document.documentElement.clientWidth));
    expect(outsideWidth).toBeLessThanOrEqual(2);
    await page.keyboard.press('Escape');
    await expect(modal).toHaveCount(0);
    await expect(opener).toBeFocused(); // Restore the keyboard return point.
    await opener.click();
    await expect(modal).toBeVisible();
    await modal.getByRole('button', { name: 'Cerrar ventana', exact: true }).click();
    await expect(modal).toHaveCount(0);
    await expect(opener).toBeFocused();
  });
}
