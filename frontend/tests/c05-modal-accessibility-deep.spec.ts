import { expect, test } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
// Independent D: real Chromium reflow, keyboard and accessibility (beyond A/B/C).
for (const viewport of [{ width: 320, height: 740 }, { width: 768, height: 900 }, { width: 1440, height: 900 }]) {
  test(`D | privacy and reflow at ${viewport.width}px`, async ({ page }) => {
    await page.setViewportSize(viewport);
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await page.goto('/cuenta?vista=operadores');
    const opener = page.getByRole('button', { name: 'Recuperar acceso de operador de plataforma', exact: true });
    const dialog = page.getByRole('dialog', { name: 'Recuperación segura de operador', exact: true });
    await opener.click();
    await expect(dialog).toBeVisible();
    await expect(page.locator('dialog.cs-modal:modal')).toHaveCount(1);
    await expect(page.locator('body')).toHaveCSS('overflow', 'hidden');
    expect(await page.evaluate(() => {
      const el = document.querySelector('dialog.cs-modal:modal');
      if (!el) return false;
      const b = el.getBoundingClientRect();
      return b.left >= -1 && b.top >= -1 && b.right <= innerWidth + 1 && b.bottom <= innerHeight + 1;
    })).toBe(true);
    expect(await page.evaluate(() => document.documentElement.scrollWidth -
      document.documentElement.clientWidth)).toBeLessThanOrEqual(2);
    await dialog.getByLabel('Correo del operador sin acceso', { exact: true }).fill('fictional-audit@example.test');
    await page.keyboard.press('Tab');
    expect(await page.evaluate(() => Boolean(document.activeElement?.closest('dialog.cs-modal')))).toBe(true);
    const axe = await new AxeBuilder({ page }).withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa']).analyze();
    expect(axe.violations.filter(v => ['critical', 'serious'].includes(v.impact ?? ''))).toEqual([]);
    await page.keyboard.press('Escape');
    await expect(dialog).toHaveCount(0);
    await expect(opener).toBeFocused();
    await opener.click();
    await expect(dialog.getByLabel('Correo del operador sin acceso', { exact: true })).toHaveValue('');
    await dialog.getByRole('button', { name: 'Cerrar ventana', exact: true }).click();
    await expect(opener).toBeFocused();
  });
}
test('D | forced-colors and reduced-motion modal remains operable', async ({ page }) => {
  await page.emulateMedia({ forcedColors: 'active', reducedMotion: 'reduce' });
  await page.goto('/cuenta?vista=operadores');
  const opener = page.getByRole('button', { name: 'Recuperar acceso de operador de plataforma', exact: true });
  await opener.click();
  const dialog = page.getByRole('dialog', { name: 'Recuperación segura de operador', exact: true });
  await expect(dialog.getByRole('button', { name: 'Cerrar ventana', exact: true })).toBeVisible();
  await page.keyboard.press('Escape');
  await expect(dialog).toHaveCount(0);
  await expect(opener).toBeFocused();
});
