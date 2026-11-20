/** CRECISOL UX F06 — semantic feedback, scoped async receipts, per-field errors and privacy.
 * Only controlled synthetic Angular responses; authorization is verified separately via E2E .NET/MySQL.
 */
import { test, expect, type Page, type TestInfo } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import { writeFile } from 'node:fs/promises';

type DebugWindow = Window & { ng?: { getComponent: (node: Element) => any } };
type Realm = 'anonymous' | 'business' | 'operator';
type Scene = 'account-pending' | 'account-success' | 'account-error' |
  'operator-error' | 'field-error' | 'modal-pending';
const scenes: readonly Scene[] = [
  'account-pending', 'account-success', 'account-error',
  'operator-error', 'field-error', 'modal-pending'
];
const viewports = [
  { width: 320, height: 720 }, { width: 390, height: 844 },
  { width: 768, height: 1024 }, { width: 1024, height: 768 },
  { width: 1440, height: 900 }
] as const;

async function syntheticApi(page: Page, realm: Realm): Promise<void> {
  await page.route('**/api/v1/**', route => route.fulfill({
    status: 503, contentType: 'application/json', body: '{}'
  }));
  await page.route('**/api/v1/public/branding/platform', route => route.fulfill({
    status: 200, contentType: 'application/json', body: JSON.stringify({
      revision: 1, name: 'CRECISOL', slogan: 'Tu negocio, a tu manera.',
      descriptor: 'Comercio · Servicios · Membresías', primaryColor: '#0d468b',
      accentColor: '#1878c8', logoUrl: '/assets/crecisol-oficial.png',
      websiteUrl: null, publishedAt: '2026-11-19T00:00:00Z',
      presentation: { faviconUrl: '/assets/crecisol-emblem.png',
        typographyToken: 'system', iconSetToken: 'outline',
        seoTitle: 'CRECISOL', seoDescription: 'Prueba sintética',
        copyrightNotice: 'Sin datos reales' }
    })
  }));
  const receipt = () => JSON.stringify({ csrfToken: 'a'.repeat(64),
    expiresAtUtc: new Date(Date.now() + 30 * 60_000).toISOString(), idleMinutes: 30 });
  await page.route('**/api/v1/identity/browser/restore', route => route.fulfill({
    status: realm === 'business' ? 200 : 401, contentType: 'application/json',
    body: realm === 'business' ? receipt() : '{}'
  }));
  await page.route('**/api/v1/platform-identity/restore', route => route.fulfill({
    status: realm === 'operator' ? 200 : 401, contentType: 'application/json',
    body: realm === 'operator' ? receipt() : '{}'
  }));
  await page.route('**/api/v1/platform-identity/session-policy', route => route.fulfill({
    status: 200, contentType: 'application/json',
    body: JSON.stringify({ idleMinutes: 30, revision: 3, canManage: false })
  }));
}

async function account(page: Page, view: string, realm: Realm = 'business'): Promise<void> {
  await syntheticApi(page, realm);
  await page.goto('/cuenta?vista=' + view);
  if (realm === 'business')
    await expect(page.getByRole('navigation', { name: 'Mi cuenta' })).toBeVisible();
  if (realm === 'anonymous')
    await expect(page.getByRole('button', { name: 'Continuar', exact: true })).toBeEnabled();
}

async function operator(page: Page): Promise<void> {
  await syntheticApi(page, 'operator');
  await page.goto('/cuenta?vista=operadores');
  await expect(page.getByRole('navigation', { name: 'Áreas de la consola de identidad' })).toBeVisible();
}

async function setAccountStatus(page: Page, kind: 'pending'|'success'|'error', scope: string): Promise<void> {
  await page.evaluate(({ kind, scope }) => {
    const portal = document.querySelector('cs-identity-portal');
    const owner = portal && (window as DebugWindow).ng?.getComponent(portal);
    if (!owner) throw Error('F06 fixture: account owner unavailable');
    owner.feedbackScope.set(scope);
    owner.activity.set('Verificando los permisos de esta operación…');
    owner.busy.set(kind === 'pending');
    owner.message.set(kind === 'success' ? 'El perfil autorizado quedó actualizado.' : '');
    owner.error.set(kind === 'error' ? 'No se completó la operación. Revisa tus datos.' : '');
  }, { kind, scope });
}

async function setOperatorError(page: Page): Promise<void> {
  await page.evaluate(() => {
    const panel = document.querySelector('cs-platform-identity-panel');
    const owner = panel && (window as DebugWindow).ng?.getComponent(panel);
    if (!owner) throw Error('F06 fixture: operator owner unavailable');
    owner.feedbackScope.set('casos');
    owner.busy.set(false);
    owner.error.set('No se completó la consulta de plataforma.');
  });
}

async function openBusyModal(page: Page): Promise<void> {
  await page.getByRole('button', { name: 'Cambiar contraseña', exact: true }).click();
  await expect(page.getByRole('dialog', { name: 'Cambiar contraseña' })).toBeVisible();
  await page.evaluate(() => {
    const portal = document.querySelector('cs-identity-portal');
    const owner = portal && (window as DebugWindow).ng?.getComponent(portal);
    // F05 intentionally removes the native title attribute once a modal opens.
    const modalHost = document.querySelector('dialog.cs-modal:modal')?.closest('cs-institutional-modal');
    const modal = modalHost && (window as DebugWindow).ng?.getComponent(modalHost);
    if (!owner || !modal) throw Error('F06 fixture: modal unavailable');
    modal.onDialogSubmit();
    owner.busy.set(true);
  });
  await expect(page.locator('dialog.cs-modal cs-operation-feedback [data-feedback-kind="pending"]')).toBeVisible();
}

async function scene(page: Page, selected: Scene): Promise<void> {
  if (selected === 'operator-error') {
    await operator(page); await setOperatorError(page); return;
  }
  if (selected === 'field-error') {
    await account(page, 'ingresar', 'anonymous');
    await page.getByRole('button', { name: 'Continuar', exact: true }).click();
    await expect(page.locator('.cs-field-feedback').first()).toBeVisible();
    return;
  }
  await account(page, 'seguridad');
  if (selected === 'modal-pending') { await openBusyModal(page); return; }
  await setAccountStatus(page,
    selected === 'account-pending' ? 'pending' : selected === 'account-error' ? 'error' : 'success',
    'seguridad');
  await expect(page.locator('cs-operation-feedback')).toHaveCount(1);
}

async function noHorizontalClip(page: Page): Promise<void> {
  await expect.poll(() => page.evaluate(() =>
    document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(2);
  await page.locator('cs-operation-feedback, .cs-field-feedback').first().evaluate(element => {
    const rect = element.getBoundingClientRect();
    const width = window.visualViewport?.width ?? innerWidth;
    if (rect.left < -2 || rect.right > width + 2) throw Error('F06 feedback cut off at viewport');
  });
}

for (const viewport of viewports) for (const selected of scenes) {
  test('F06 ' + selected + ' at ' + viewport.width + 'px', async ({ page }, info: TestInfo) => {
    await page.setViewportSize(viewport);
    await scene(page, selected);
    await noHorizontalClip(page);
    if (selected !== 'field-error') {
      const tone = selected === 'account-pending' || selected === 'modal-pending' ? 'pending'
        : selected === 'account-success' ? 'success' : 'error';
      const rendered = page.locator('cs-operation-feedback [data-feedback-kind="' + tone + '"]');
      await expect(rendered).toHaveCount(1);
      await expect(rendered).toHaveAttribute('role', tone === 'error' ? 'alert' : 'status');
      await expect(rendered).not.toContainText('undefined');
    } else {
      await expect(page.locator('input[name="mailbox"]')).toHaveAttribute('aria-invalid', 'true');
      await expect(page.locator('.cs-field-feedback').first()).toContainText('obligatorio');
    }
    const inspected = selected === 'field-error' ? 'form[csFieldFeedback]' :
      selected === 'modal-pending' ? 'dialog.cs-modal:modal' : 'cs-operation-feedback';
    const results = await new AxeBuilder({ page }).include(inspected)
      .withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa']).analyze();
    expect(results.violations).toEqual([]);
    const filename = 'feedback-' + selected + '-' + viewport.width;
    await page.screenshot({ path: info.outputPath(filename + '.png'),
      fullPage: selected !== 'modal-pending', animations: 'disabled' });
    await writeFile(info.outputPath(filename + '.json'), JSON.stringify({
      filename, scene: selected, viewport, runnerSha: process.env.GITHUB_SHA ?? 'local',
      synthetic: true, violations: results.violations.length, png: filename + '.png'
    }, null, 2));
  });
}

test('F06 required inputs show safe inline errors and remove ARIA markers after correction', async ({ page }) => {
  await account(page, 'ingresar', 'anonymous');
  await page.getByRole('button', { name: 'Continuar', exact: true }).click();
  const mailbox = page.getByLabel('Correo electrónico');
  await expect(mailbox).toHaveAttribute('aria-invalid', 'true');
  const describedBy = await mailbox.getAttribute('aria-describedby');
  expect(describedBy).toMatch(/^cs-field-feedback-/);
  await expect(page.locator('#' + describedBy)).toContainText('obligatorio');
  await mailbox.fill('persona@example.test');
  await expect(mailbox).not.toHaveAttribute('aria-invalid', 'true');
  await expect(page.locator('#' + describedBy)).toHaveCount(0);
});

test('F06 invalid submission is blocked by local constraints without native tooltip', async ({ page }) => {
  await account(page, 'ingresar', 'anonymous');
  const form = page.locator('cs-identity-guest form[csFieldFeedback]').first();
  await expect(form).toHaveAttribute('novalidate', '');
  let sent = 0;
  await page.route('**/api/v1/identity/login', route => {
    sent++;
    return route.fulfill({ status: 503, contentType: 'application/json', body: '{}' });
  });
  await page.getByRole('button', { name: 'Continuar', exact: true }).click();
  await expect(page.locator('.cs-field-feedback')).toHaveCount(2);
  expect(sent).toBe(0);
  await expect(page.getByLabel('Correo electrónico')).toBeFocused();
});

test('F06 failed login leaves mailbox editable but scrubs password without echoing it', async ({ page }) => {
  await account(page, 'ingresar', 'anonymous');
  await page.getByLabel('Correo electrónico').fill('persona@example.test');
  await page.getByLabel('Contraseña', { exact: true }).fill('F06-FAKE-SECRET-DO-NOT-PERSIST');
  await page.getByRole('button', { name: 'Continuar', exact: true }).click();
  await expect(page.locator('cs-operation-feedback [data-feedback-kind="error"]')).toBeVisible();
  await expect(page.getByLabel('Correo electrónico')).toHaveValue('persona@example.test');
  await expect(page.getByLabel('Contraseña', { exact: true })).toHaveValue('');
  await expect(page.locator('cs-operation-feedback')).not.toContainText('F06-FAKE-SECRET');
});

test('F06 failed operator login scrubs its credentials in the separate realm', async ({ page }) => {
  await syntheticApi(page, 'anonymous');
  await page.goto('/cuenta?vista=operadores');
  await expect(page.getByRole('button', { name: 'Acceder a identidad de plataforma' })).toBeEnabled();
  await page.getByLabel('Correo del operador de identidad').fill('operador@example.test');
  await page.getByLabel('Contraseña del operador').fill('F06-SYNTHETIC-NO-CREDENTIAL');
  await page.getByRole('button', { name: 'Acceder a identidad de plataforma' }).click();
  await expect(page.locator('cs-platform-identity-panel cs-operation-feedback [data-feedback-kind="error"]')).toBeVisible();
  await expect(page.getByLabel('Correo del operador de identidad')).toHaveValue('operador@example.test');
  await expect(page.getByLabel('Contraseña del operador')).toHaveValue('');
});

test('F06 navigation rejects obsolete feedback yet preserves backup-code secrets until saved', async ({ page }) => {
  await account(page, 'resumen');
  await setAccountStatus(page, 'success', 'resumen');
  await page.evaluate(() => {
    const portal = document.querySelector('cs-identity-portal');
    const owner = portal && (window as DebugWindow).ng?.getComponent(portal);
    owner.identity.recoveryCodes.set(['F06-SYNTHETIC-BACKUP']);
  });
  await expect(page.getByText('F06-SYNTHETIC-BACKUP')).toBeVisible();
  await page.getByRole('link', { name: 'Mi perfil', exact: true }).click();
  await expect(page.locator('cs-operation-feedback')).toHaveCount(0);
  await expect(page.getByText('F06-SYNTHETIC-BACKUP')).toHaveCount(0);
  await page.getByRole('link', { name: 'Resumen', exact: true }).click();
  // Disclosure was just issued; navigating to its designated summary cannot discard it.
  await expect(page.getByText('F06-SYNTHETIC-BACKUP')).toBeVisible();
  await page.getByRole('button', { name: 'Ya guardé mis códigos' }).click();
  await expect(page.getByText('F06-SYNTHETIC-BACKUP')).toHaveCount(0);
  await page.getByRole('link', { name: 'Mi perfil', exact: true }).click();
  await page.getByRole('link', { name: 'Resumen', exact: true }).click();
  await expect(page.getByText('F06-SYNTHETIC-BACKUP')).toHaveCount(0);
});

test('F06 late response from a departed section does not resurrect a success notice', async ({ page }) => {
  await account(page, 'perfil');
  let delivered = 0;
  await page.route('**/api/v1/identity/profile/read', async route => {
    delivered++;
    await new Promise(resolve => setTimeout(resolve, 600));
    await route.fulfill({ status: 200, contentType: 'application/json',
      body: JSON.stringify({ displayName: 'Persona ficticia', language: 'es-HN',
        timeZone: 'America/Tegucigalpa', hasPhoto: false }) });
  });
  await page.getByRole('button', { name: 'Actualizar datos' }).click();
  await expect.poll(() => delivered).toBe(1);
  await expect(page.locator('cs-operation-feedback [data-feedback-kind="pending"]')).toBeVisible();
  await page.getByRole('link', { name: 'Historial', exact: true }).click();
  await expect(page.locator('cs-operation-feedback')).toHaveCount(0);
  await expect.poll(() => page.evaluate(() => {
    const el = document.querySelector('cs-identity-portal');
    const owner = el && (window as DebugWindow).ng?.getComponent(el);
    return owner?.busy() ?? true;
  })).toBe(false);
  await expect(page.locator('cs-operation-feedback')).toHaveCount(0);
});

test('F06 no premature modal receipt and no closing of a pending protected transaction', async ({ page }) => {
  await account(page, 'seguridad');
  await openBusyModal(page);
  const dialog = page.getByRole('dialog', { name: 'Cambiar contraseña' });
  await expect(dialog.getByRole('button', { name: 'Cerrar ventana' })).toBeDisabled();
  await page.keyboard.press('Escape');
  await expect(dialog).toBeVisible();
  await page.evaluate(() => {
    const portal = document.querySelector('cs-identity-portal');
    const owner = portal && (window as DebugWindow).ng?.getComponent(portal);
    owner.message.set('Solicitud registrada por el servidor.');
    owner.busy.set(false);
  });
  await expect(dialog.locator('cs-operation-feedback [data-feedback-kind="success"]')).toBeVisible();
  await expect(dialog.locator('cs-operation-feedback [data-feedback-kind="pending"]')).toHaveCount(0);
});

test('F06 reduced motion, high-contrast and 200-percent text retain context and focus', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 430 });
  await page.emulateMedia({ reducedMotion: 'reduce', forcedColors: 'active' });
  await account(page, 'seguridad');
  await setAccountStatus(page, 'error', 'seguridad');
  await page.addStyleTag({ content: ':root { font-size: 200% !important; }' });
  await expect(page.locator('cs-operation-feedback [data-feedback-kind="error"]')).toBeVisible();
  await noHorizontalClip(page);
  const animation = await page.locator('cs-operation-feedback section').evaluate(el =>
    getComputedStyle(el).animationName);
  expect(animation).toBe('none');
});
