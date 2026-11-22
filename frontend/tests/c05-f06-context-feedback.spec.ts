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


/** F02 parity gate on real Angular DOM, not CSS text: each F06 feedback
 * scene must render exactly the same semantic colors as the F02 tokens.
 * Computed colors are normalized by the browser, including custom variables.
 */
async function assertF02VisualParity(page: Page, selected: Scene): Promise<void> {
  if (selected === 'field-error') {
    const invalid = page.locator('cs-identity-guest input[name="mailbox"][aria-invalid="true"]');
    await expect(invalid).toHaveCount(1);
    const colors = await invalid.evaluate(node => {
      const style = getComputedStyle(node);
      const probe = document.createElement('span');
      probe.style.color = 'var(--cs-color-danger)';
      node.appendChild(probe);
      const expected = getComputedStyle(probe).color;
      probe.remove();
      return { border: style.borderTopColor, expected };
    });
    expect(colors.border, 'invalid native field uses F02 danger token').toBe(colors.expected);
    return;
  }

  const kind = selected === 'account-success' ? 'success'
    : selected === 'operator-error' || selected === 'account-error' ? 'error' : 'pending';
  const intent = kind === 'success' ? 'success' : kind === 'error' ? 'danger' : 'info';
  const box = page.locator('cs-operation-feedback .cs-operation-feedback[data-feedback-kind="' + kind + '"]');
  await expect(box).toHaveCount(1);
  const values = await box.evaluate((node, intent) => {
    const st = getComputedStyle(node);
    const color = (token: string) => {
      const probe = document.createElement('span');
      probe.style.color = 'var(--cs-color-' + token + ')';
      node.appendChild(probe);
      const result = getComputedStyle(probe).color;
      probe.remove();
      return result;
    };
    return {
      border: st.borderLeftColor,
      background: st.backgroundColor,
      expectedBorder: color(intent),
      expectedBackground: color(intent + '-bg')
    };
  }, intent);
  expect(values.border, kind + ': feedback accent matches F02 token').toBe(values.expectedBorder);
  expect(values.background, kind + ': feedback surface matches F02 token').toBe(values.expectedBackground);

  if (selected === 'modal-pending') {
    const dialog = page.locator('dialog.cs-modal:modal');
    await expect(dialog).toHaveCount(1);
    const backgrounds = await dialog.evaluate(node => {
      const st = getComputedStyle(node);
      const probe = document.createElement('span');
      probe.style.color = 'var(--cs-color-surface)';
      node.appendChild(probe);
      const expected = getComputedStyle(probe).color;
      probe.remove();
      return { background: st.backgroundColor, expected };
    });
    expect(backgrounds.background, 'secure modal uses F02 institutional surface').toBe(backgrounds.expected);
  }
  if (selected === 'operator-error') {
    const quiet = page.locator('cs-platform-operator-cases .operator-card--quiet').first();
    await expect(quiet).toBeVisible();
    const colors = await quiet.evaluate(node => {
      const probe = document.createElement('span');
      probe.style.color = 'var(--cs-color-surface-soft)';
      node.appendChild(probe);
      const expected = getComputedStyle(probe).color;
      probe.remove();
      return { background: getComputedStyle(node).backgroundColor, expected };
    });
    expect(colors.background, 'platform operator card uses shared F02 surface').toBe(colors.expected);
  }
}

for (const viewport of viewports) for (const selected of scenes) {
  test('F06 ' + selected + ' at ' + viewport.width + 'px', async ({ page }, info: TestInfo) => {
    await page.setViewportSize(viewport);
    await scene(page, selected);
    await noHorizontalClip(page);
    await assertF02VisualParity(page, selected);
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
  // Unacknowledged freshly issued backups remain available for the owner to save.
  await expect(page.getByText('F06-SYNTHETIC-BACKUP')).toBeVisible();
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


/** F06 premium buyer acceptance: one independent artifact per full interface
 * on all five sizes. Real permissions remain the job of the integration suite.
 */
type PremiumSurface =
  'guest-login' | 'guest-register' | 'guest-recovery' |
  'account-resumen' | 'account-perfil' | 'account-llaves' |
  'account-seguridad' | 'account-historial' | 'account-administracion' |
  'operator-casos' | 'operator-sesiones' | 'operator-perfil' |
  'operator-monitor' | 'operator-seguridad';
const premiumSurfaces: readonly PremiumSurface[] = [
  'guest-login', 'guest-register', 'guest-recovery',
  'account-resumen', 'account-perfil', 'account-llaves',
  'account-seguridad', 'account-historial', 'account-administracion',
  'operator-casos', 'operator-sesiones', 'operator-perfil',
  'operator-monitor', 'operator-seguridad'
];
const operatorLabels: Readonly<Record<string, string>> = {
  casos: 'Casos', sesiones: 'Sesiones', perfil: 'Perfil',
  monitor: 'Monitor', seguridad: 'Seguridad'
};
async function premiumScene(page: Page, item: PremiumSurface): Promise<void> {
  const [realm, section] = item.split('-');
  if (realm === 'guest') {
    await syntheticApi(page, 'anonymous');
    const guestRoutes: Readonly<Record<string, string>> = {
      login: 'ingresar', register: 'registro', recovery: 'recuperar'
    };
    await page.goto('/cuenta?vista=' + guestRoutes[section]);
    await expect(page.locator('cs-identity-portal')).toBeVisible();
    return;
  }
  if (realm === 'account') {
    await account(page, section);
    return;
  }
  await operator(page);
  if (section !== 'casos') {
    const nav = page.getByRole('navigation', { name: 'Áreas de la consola de identidad' });
    await nav.getByRole('button', { name: operatorLabels[section], exact: true }).click();
  }
  const selectors: Readonly<Record<string, string>> = {
    casos: 'cs-platform-operator-cases', sesiones: 'cs-platform-operator-sessions',
    perfil: 'cs-platform-operator-profile', seguridad: 'cs-platform-operator-security',
    monitor: 'cs-platform-operator-monitor'
  };
  await expect(page.locator(selectors[section])).toBeVisible();
}

for (const viewport of viewports) for (const item of premiumSurfaces) {
  test('F06 PREMIUM interface ' + item + ' at ' + viewport.width + 'px',
    async ({ page }, info: TestInfo) => {
      await page.setViewportSize(viewport);
      await premiumScene(page, item);
      const width = await page.evaluate(() => document.documentElement.scrollWidth
        - document.documentElement.clientWidth);
      expect(width, 'A premium interface must never scroll horizontally').toBeLessThanOrEqual(2);
      if (item === 'guest-login') {
        await expect(page.getByRole('heading', { name: 'Iniciar sesión' })).toBeVisible();
        await expect(page.getByLabel('Correo electrónico')).toBeVisible();
        await expect(page.getByRole('button', { name: 'Entrar con llave de acceso' })).toBeVisible();
        await expect(page.locator('a[href*="localhost:4200"]')).toHaveCount(0);
        if (viewport.width <= 390) {
          const y = await page.getByLabel('Correo electrónico').evaluate(node => node.getBoundingClientRect().top);
          expect(y, 'The primary login field must be reachable in the first mobile viewport')
            .toBeLessThan(viewport.height);
        }
      }
      if (item === 'operator-casos') {
        await expect(page.getByRole('heading', { name: 'Sin expediente seleccionado' })).toBeVisible();
        const details = page.locator('details.operator-scope-details');
        await expect(details).not.toHaveAttribute('open', '');
        await expect(page.getByRole('button', { name: 'Consultar caso de identidad' })).toBeVisible();
        if (viewport.width <= 390)
          await expect(page.locator('.operator-nav-hint')).toBeVisible();
      }
      if (item.startsWith('operator-')) {
        const disclosure = page.locator('details.operator-scope-disclosure');
        await expect(disclosure).not.toHaveAttribute('open', '');
        const nav = page.getByRole('navigation', { name: 'Áreas de la consola de identidad' });
        await expect(nav).toBeVisible();
        if (viewport.width <= 390)
          await expect(page.locator('.operator-nav-hint')).toBeVisible();
      }
      if (viewport.width === 390 || viewport.width === 1440) {
        const scope = item.startsWith('operator-')
          ? 'cs-platform-identity-panel' : 'cs-identity-portal';
        const audit = await new AxeBuilder({ page }).include(scope)
          .withTags(['wcag2a','wcag2aa','wcag21aa','wcag22aa']).analyze();
        expect(audit.violations).toEqual([]);
      }
      const name = 'premium-' + item + '-' + viewport.width;
      await page.screenshot({ path: info.outputPath(name + '.png'),
        fullPage: true, animations: 'disabled' });
      await writeFile(info.outputPath(name + '.json'), JSON.stringify({
        name, surface: item, viewport, synthetic: true,
        runnerSha: process.env.GITHUB_SHA ?? 'local',
        decision: 'pending-human-visual-review'
      }, null, 2));
    });
}

test('F06 PREMIUM operator privacy disclosure remains keyboard accessible without hiding the action', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 720 });
  await operator(page);
  const hint = page.locator('details.operator-scope-details');
  await expect(hint).not.toHaveAttribute('open', '');
  await hint.locator('summary').focus();
  await page.keyboard.press('Enter');
  await expect(hint).toHaveAttribute('open', '');
  await expect(hint).toContainText('Toda autorización y revisión se valida en el servidor');
  await expect(page.getByRole('button', { name: 'Consultar caso de identidad' })).toBeVisible();
});

test('F06 PREMIUM errors offer safe next steps without exposing backend details', async ({ page }) => {
  await account(page, 'seguridad');
  await setAccountStatus(page, 'error', 'seguridad');
  const notice = page.locator('cs-operation-feedback [data-feedback-kind="error"]');
  await expect(notice).toContainText('Revisa los datos y vuelve a intentarlo');
  await expect(notice).not.toContainText('SQL');
  await expect(notice).not.toContainText('stack');
});


test('F06 PREMIUM unsupported WebAuthn is safely unavailable without technical localhost links', async ({ page }) => {
  await page.addInitScript(() => {
    Object.defineProperty(window, 'PublicKeyCredential', { value: undefined, configurable: true });
  });
  await syntheticApi(page, 'anonymous');
  await page.goto('/cuenta?vista=ingresar');
  await expect(page.getByRole('button', { name: 'Entrar con llave de acceso' })).toBeDisabled();
  await expect(page.locator('.login-compatibility-note')).toContainText('navegador compatible');
  await expect(page.locator('a[href*="localhost"]')).toHaveCount(0);
});

test('F06 PREMIUM protected-modal error never suggests unrelated account recovery', async ({ page }) => {
  await account(page, 'seguridad');
  await page.getByRole('button', { name: 'Cambiar contraseña', exact: true }).click();
  const dialog = page.getByRole('dialog', { name: 'Cambiar contraseña' });
  await expect(dialog).toBeVisible();
  await page.evaluate(() => {
    const host = document.querySelector('dialog.cs-modal:modal')?.closest('cs-institutional-modal');
    const component = host && (window as DebugWindow).ng?.getComponent(host);
    if (!component) throw Error('F06 modal fixture unavailable');
    component.localError.set('No se pudo completar esta solicitud.');
  });
  const notice = dialog.locator('cs-operation-feedback [data-feedback-kind="error"]');
  await expect(notice).toContainText('Comprueba los datos y el estado de esta solicitud');
  await expect(notice).not.toContainText('recuperación de cuenta');
});


test('F06 PREMIUM operator monitor reflows at 320px and 200% text without horizontal scroll', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 720 });
  await page.emulateMedia({ forcedColors: 'active', reducedMotion: 'reduce' });
  await operator(page);
  await page.getByRole('navigation', { name: 'Áreas de la consola de identidad' })
    .getByRole('button', { name: 'Monitor', exact: true }).click();
  await page.addStyleTag({ content: ':root { font-size: 200% !important; }' });
  await expect.poll(() => page.evaluate(() =>
    document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(2);
  await expect(page.getByRole('heading', { name: 'Monitor de identidad' })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Consultar señales agregadas' })).toBeVisible();
});


test('F06 PREMIUM empty business profile uses full width rather than a cramped fake avatar', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 720 });
  await account(page, 'perfil');
  const layout = page.locator('.identity-profile-body');
  await expect(layout.locator('cs-ui-state')).toBeVisible();
  await expect(layout.locator('.identity-profile-avatar')).toHaveCount(0);
  const metrics = await layout.evaluate(element => ({
    body: element.getBoundingClientRect().width,
    state: element.querySelector('cs-ui-state')?.getBoundingClientRect().width ?? 0
  }));
  expect(metrics.state).toBeGreaterThan(190);
  expect(metrics.body - metrics.state).toBeLessThan(4);
});

test('F06 PREMIUM operator mobile summary is compact and retains visible security context', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 720 });
  await operator(page);
  const context = page.locator('.operator-context');
  await expect(context).toBeVisible();
  await expect(context).toContainText('Consulta y revisión independiente de identidad.');
  const box = await context.boundingBox();
  expect(box?.height ?? 999).toBeLessThan(105);
  await expect(page.getByRole('heading', { name: 'Casos de recuperación y revisión' })).toBeVisible();
  await expect(page.locator('details.operator-scope-disclosure')).toBeVisible();
  await expect(page.getByRole('navigation', { name: 'Áreas de la consola de identidad' })).toBeVisible();
  // The initial operator view has no status/field message. Assert page geometry
  // directly instead of waiting for a feedback element that must not exist.
  await expect.poll(() => page.evaluate(() =>
    document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(2);
});


test('F06 premium recovery keeps signed-out options accessible without a four-row mobile menu', async ({ page }) => {
  await page.setViewportSize({width:320,height:720});
  await syntheticApi(page, 'anonymous');
  await page.goto('/cuenta?vista=recuperar');
  await expect(page.locator('cs-identity-portal')).toBeVisible();
  await expect(page.getByRole('heading', { name:'Recuperar contraseña' })).toBeVisible();
  await expect(page.getByRole('button', {name:'Solicitar recuperación'})).toBeVisible();
  const secondary=page.locator('details.account-more');
  await expect(secondary).not.toHaveAttribute('open');
  await secondary.locator('summary').focus();
  await page.keyboard.press('Enter');
  await expect(secondary).toHaveAttribute('open', '');
  const columns=await page.locator('.account-subnav').evaluate(el => getComputedStyle(el).gridTemplateColumns.split(' ').length);
  expect(columns).toBe(2);
  const btn=page.getByRole('button',{name:'Solicitar recuperación'});
  const y=await btn.evaluate(el=>el.getBoundingClientRect().bottom);
  expect(y, 'the recovery request must be reachable in the first mobile viewport').toBeLessThan(730);
});

test('F06 premium approval queue leads; security details remain accessible by keyboard', async ({ page }) => {
  await page.setViewportSize({width:320,height:720});
  await account(page, 'administracion');
  const queue=page.locator('.identity-approval-queue');
  await expect(queue.getByRole('button',{name:'Consultar solicitudes'})).toBeVisible();
  const rail=page.locator('details.identity-approval-context');
  await expect(rail).not.toHaveAttribute('open','');
  const taskFirst = await queue.evaluate(element => {
    const rail = document.querySelector('details.identity-approval-context');
    return !!rail && !!(element.compareDocumentPosition(rail) & Node.DOCUMENT_POSITION_FOLLOWING);
  });
  expect(taskFirst, 'approval queue must precede the optional explanation').toBe(true);
  await rail.locator('summary').focus();
  await page.keyboard.press('Enter');
  await expect(rail).toHaveAttribute('open','');
  await expect(rail).toContainText('Ningún expediente se acepta automáticamente');
});

test('F06 premium case lookup leads without exposing unauthorized data or concealing the limits', async ({page})=>{
  await page.setViewportSize({width:320,height:720});
  await operator(page);
  const lookup=page.locator('section[aria-label="Consulta puntual de caso"]');
  const notice=page.locator('details.operator-scope-details');
  const result=page.locator('section[aria-label="Estado del expediente consultado"]');
  await expect(lookup.getByRole('button',{name:'Consultar caso de identidad'})).toBeVisible();
  await expect(result.getByRole('heading',{name:'Sin expediente seleccionado'})).toBeVisible();
  await expect(result.getByRole('heading',{name:'Estado y prioridad del caso'})).toHaveCount(0);
  const taskFirst = await lookup.evaluate(element => {
    const privacy = document.querySelector('details.operator-scope-details');
    return !!privacy && !!(element.compareDocumentPosition(privacy) & Node.DOCUMENT_POSITION_FOLLOWING);
  });
  expect(taskFirst, 'protected case lookup must precede optional policy details').toBe(true);
  await notice.locator('summary').focus();
  await page.keyboard.press('Enter');
  await expect(notice).toHaveAttribute('open','');
  await expect(notice).toContainText('Toda autorización y revisión se valida en el servidor');
});

test('F06 premium account security keeps MFA confirmation behind a protected dialog', async({page})=>{
  await page.setViewportSize({width:390,height:844});
  await account(page,'seguridad');
  const disclosure=page.locator('details.identity-security-intro');
  await expect(disclosure).not.toHaveAttribute('open','');
  await expect(disclosure.locator('summary')).toBeVisible();
  const action=page.getByRole('button',{name:'Cambiar contraseña',exact:true});
  await expect(action).toBeVisible();
  await action.click();
  const modal=page.getByRole('dialog',{name:'Cambiar contraseña'});
  await expect(modal).toBeVisible();
  await expect(modal.locator('input[type=password]')).toHaveCount(2);
});


// Buyer-quality guard: compact empty feedback must not impersonate loaded
// private account data, nor consume another full mobile viewport.
for (const view of ['resumen', 'perfil', 'llaves', 'historial', 'administracion'] as const) {
  test('F06 premium compact empty account state '+view+' at 320px', async ({ page }) => {
    await page.setViewportSize({width:320,height:720});
    await account(page, view);
    const empty=page.locator('cs-identity-workspace cs-ui-state .cs-state[data-kind="empty"]').first();
    await expect(empty).toBeVisible();
    await expect(empty.locator('h3')).toBeVisible();
    await expect(empty.locator('p')).toBeVisible();
    const box=await empty.boundingBox();
    expect(box?.height??9999,'empty state should be compact without clipping its explanation').toBeLessThan(218);
    const hasOverflow=await empty.evaluate(el=>el.scrollWidth>el.clientWidth+2);
    expect(hasOverflow).toBe(false);
    await expect.poll(()=>page.evaluate(()=>document.documentElement.scrollWidth-document.documentElement.clientWidth)).toBeLessThanOrEqual(2);
  });
}
test('F06 premium empty state scales to 200 percent without scrollbars or hidden text', async({page})=>{
  await page.setViewportSize({width:320,height:720});
  await account(page, 'llaves');
  await page.addStyleTag({content:':root { font-size: 200% !important; }'});
  const state=page.locator('cs-identity-workspace cs-ui-state .cs-state[data-kind="empty"]').first();
  await expect(state).toBeVisible();
  await expect(state.getByRole('heading',{name:'Ninguna llave visible'})).toBeVisible();
  await expect.poll(()=>page.evaluate(()=>document.documentElement.scrollWidth-document.documentElement.clientWidth)).toBeLessThanOrEqual(2);
});

 
// Premium-readiness checks: mobile text must stay legible, with a compact
// administrative hierarchy and keyboard-accessible five-area navigation.
test('F06 premium operator headings and context remain legible at 320px', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 720 });
  await operator(page);
  const heading = page.locator('.page-heading--operator');
  await expect(heading.getByRole('heading', { name: 'Identidad de plataforma' })).toBeVisible();
  const box = await heading.boundingBox();
  expect(box?.height ?? 999, 'platform heading should not consume a mobile viewport').toBeLessThan(145);
  const computed = await page.evaluate(() => {
    const font = (selector: string) => {
      const element = document.querySelector(selector);
      return element ? Number.parseFloat(getComputedStyle(element).fontSize) : 0;
    };
    return {
      context: font('.operator-context__section'),
      help: font('.operator-nav-hint'),
      intro: font('.operator-workspace > .operator-section-head p'),
      titleCount: document.querySelectorAll('.operator-workspace > .operator-section-head .operator-kicker:not([hidden])').length
    };
  });
  expect(computed.context).toBeGreaterThanOrEqual(14);
  expect(computed.help).toBeGreaterThanOrEqual(14);
  expect(computed.intro).toBeGreaterThanOrEqual(14);
  const kicker = page.locator('.operator-workspace > .operator-section-head .operator-kicker');
  if (await kicker.count()) await expect(kicker).toBeHidden();
  await expect.poll(() => page.evaluate(() =>
    document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(2);
});

test('F06 premium operator navigation has five keyboard-reachable touch targets', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 720 });
  await operator(page);
  const navigation = page.getByRole('navigation', { name: 'Áreas de la consola de identidad' });
  const buttons = navigation.getByRole('button');
  await expect(buttons).toHaveCount(5);
  for (const button of await buttons.all()) {
    const height = await button.evaluate(el => el.getBoundingClientRect().height);
    expect(height, 'navigation touch targets must measure at least 44px').toBeGreaterThanOrEqual(44);
  }
  await buttons.last().focus();
  await expect(buttons.last()).toBeFocused();
  await expect(page.locator('.operator-nav-hint')).toContainText('Tab');
  await expect.poll(() => page.evaluate(() =>
    document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(2);
});

// Buyer review: nested empty-state cards and tiny mobile navigation.
for (const view of ['perfil', 'llaves', 'historial', 'administracion'] as const) {
  test('F06 premium flat nested empty panel '+view+' keeps accessible content', async ({ page }) => {
    await page.setViewportSize({ width: 320, height: 720 });
    await account(page, view);
    const state = page.locator('cs-identity-workspace cs-ui-state .cs-state[data-kind="empty"]').first();
    await expect(state).toBeVisible();
    await expect(state.locator('h3')).toBeVisible();
    await expect(state.locator('p')).toBeVisible();
    const result = await state.evaluate(el => {
      const style = getComputedStyle(el);
      return { border: style.borderTopWidth, bg: style.backgroundColor };
    });
    expect(result.border).toBe('0px');
    expect(result.bg).toBe('rgba(0, 0, 0, 0)');
    await expect.poll(() => page.evaluate(() =>
      document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(2);
  });
}

test('F06 premium mobile account navigation explains all six areas readably', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 720 });
  await account(page, 'resumen');
  const hint = page.locator('.account-nav-mobile-help');
  await expect(hint).toBeVisible();
  await expect(hint).toContainText('seis áreas');
  const size = await hint.evaluate(el => Number.parseFloat(getComputedStyle(el).fontSize));
  expect(size).toBeGreaterThanOrEqual(14);
});

// Buyer acceptance: the main operator action must be reachable without
// a first-screen scroll at 320x720; decorative headings do not block a task.
test('F06 buyer mobile case lookup action fits in first viewport', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 720 });
  await operator(page);
  const lookup = page.locator('section[aria-label="Consulta puntual de caso"]');
  await expect(lookup).toBeVisible();
  await expect(lookup.getByRole('heading')).toHaveCount(0);
  await expect(lookup.getByRole('textbox', { name: 'Referencia del caso' })).toBeVisible();
  const action = lookup.getByRole('button', { name: 'Consultar caso de identidad' });
  await expect(action).toBeVisible();
  const bottom = await action.evaluate(el => el.getBoundingClientRect().bottom);
  expect(bottom, 'case query must be visible without scrolling a 320x720 phone').toBeLessThanOrEqual(720);
  const disclosure = page.locator('details.operator-scope-disclosure');
  await disclosure.locator('summary').focus();
  await page.keyboard.press('Enter');
  await expect(disclosure).toHaveAttribute('open', '');
  await expect(disclosure).toContainText('Ninguna pestaña concede autorización por sí misma');
  await expect.poll(() => page.evaluate(() =>
    document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(2);
});

test('F06 buyer mobile recovery prioritizes a safe request over the decorative aside', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 720 });
  await syntheticApi(page, 'anonymous');
  await page.goto('/cuenta?vista=recuperar');
  await expect(page.locator('.account-layout--recovery .account-aside')).toBeHidden();
  const main = page.getByRole('button', { name: 'Solicitar recuperación' });
  await expect(main).toBeVisible();
  const bottom = await main.evaluate(el => el.getBoundingClientRect().bottom);
  expect(bottom, 'primary recovery task should lead in the first phone viewport').toBeLessThan(600);
  const secondary = page.locator('details.account-more');
  await expect(secondary).not.toHaveAttribute('open');
  await secondary.locator('summary').focus();
  await page.keyboard.press('Enter');
  await expect(secondary).toHaveAttribute('open', '');
  await expect(secondary.getByRole('link', { name: 'Recuperar acceso' })).toHaveAttribute('aria-current', 'page');
});

test('F06 buyer operator security preserves 30-minute policy and protected actions', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 720 });
  await operator(page);
  const nav = page.getByRole('navigation', { name: 'Áreas de la consola de identidad' });
  await nav.getByRole('button', { name: 'Seguridad', exact: true }).click();
  const policy = page.getByRole('region', { name: 'Política vigente de inactividad' });
  await expect(policy).toContainText('30 minutos sin actividad');
  await expect(policy.locator('.operator-tag')).toBeHidden();
  await expect(page.getByRole('button', { name: 'Cambiar contraseña de operador', exact: true })).toBeVisible();
  await expect.poll(() => page.evaluate(() =>
    document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(2);
});

// No duplicate policy box before the real operator work. The disclosure
// remains keyboard reachable after the workspace and before sign-out.
test('F06 buyer operator realm disclosure follows the protected work and is accessible', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 720 });
  await operator(page);
  const work = page.locator('.operator-content');
  const disclosure = page.locator('details.operator-scope-disclosure');
  const order = await work.evaluate(element => {
    const details = document.querySelector('details.operator-scope-disclosure');
    const signout = document.querySelector('.operator-exit');
    return !!details && !!signout &&
      !!(element.compareDocumentPosition(details) & Node.DOCUMENT_POSITION_FOLLOWING) &&
      !!(details.compareDocumentPosition(signout) & Node.DOCUMENT_POSITION_FOLLOWING);
  });
  expect(order, 'privacy details must stay after tasks and before sign out').toBe(true);
  await disclosure.locator('summary').focus();
  await page.keyboard.press('Enter');
  await expect(disclosure).toHaveAttribute('open', '');
  await expect(disclosure).toContainText('Ninguna pestaña concede autorización por sí misma');
});

test('F06 buyer public login keeps compact accessible H1 and main action above the fold', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 720 });
  await syntheticApi(page, 'anonymous');
  await page.goto('/cuenta?vista=ingresar');
  const aside = page.locator('.account-layout--guest .account-aside');
  await expect(aside).toBeVisible();
  await expect(aside.getByRole('heading', { level: 1, name: 'Tu cuenta, a tu manera.' })).toBeVisible();
  const height = await aside.evaluate(el => el.getBoundingClientRect().height);
  expect(height, 'institutional heading must be compact on a 320px viewport').toBeLessThanOrEqual(70);
  const submit = page.getByRole('button', { name: 'Continuar', exact: true });
  await expect(submit).toBeVisible();
  expect(await submit.evaluate(el => el.getBoundingClientRect().bottom))
    .toBeLessThanOrEqual(720);
  await expect.poll(() => page.evaluate(() =>
    document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(2);
});

test('F06 buyer passive security-policy read does not invent a success notification', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 720 });
  await operator(page);
  await page.getByRole('navigation', { name: 'Áreas de la consola de identidad' })
    .getByRole('button', { name: 'Seguridad', exact: true }).click();
  await expect(page.getByRole('region', { name: 'Política vigente de inactividad' }))
    .toContainText('30 minutos sin actividad');
  await expect(page.getByText('La solicitud de plataforma terminó. Comprueba el resultado autorizado.'))
    .toHaveCount(0);
});

test('F06 buyer invitation enrollment choice is legible and never exposes the transport sentinel', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 720 });
  await syntheticApi(page, 'anonymous');
  await page.goto('/cuenta?vista=invitacion');
  const option = page.getByRole('checkbox', { name: 'Todavía no tengo configurado el segundo factor' });
  await expect(option).toBeVisible();
  await expect(page.getByLabel('Código de tu autenticador o de respaldo')).toBeVisible();
  await expect(page.getByText('escribe enroll')).toHaveCount(0);
  await option.check();
  await expect(page.locator('cs-identity-guest input[type=hidden][name=code]')).toHaveValue('enroll');
  await expect(page.getByLabel('Código de tu autenticador o de respaldo')).toHaveCount(0);
  await expect(page.getByText(/Esta opción no concede acceso sin verificación/)).toBeVisible();
  await option.uncheck();
  await expect(page.locator('cs-identity-guest input[type=hidden][name=code]')).toHaveCount(0);
  await expect(page.getByLabel('Código de tu autenticador o de respaldo')).toBeVisible();
  await expect.poll(() => page.evaluate(() =>
    document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(2);
});

test('F06 buyer account photo picker is localized, validates client size and clears after cancel', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 720 });
  await syntheticApi(page, 'business');
  await page.goto('/cuenta?vista=perfil');
  await page.getByRole('button', { name: 'Añadir foto de perfil' }).click();
  const dialog = page.getByRole('dialog', { name: 'Actualizar foto de perfil' });
  const input = dialog.getByLabel(/Foto de perfil \(PNG, JPEG o WebP, hasta 2 MB\)/);
  const save = dialog.getByRole('button', { name: 'Guardar mi foto de perfil' });
  await expect(save).toBeDisabled();
  await expect(input).toHaveAttribute('type', 'file');
  await expect(dialog.getByText('Seleccionar imagen del dispositivo')).toBeVisible();
  await expect(dialog.getByText('Ninguna imagen seleccionada')).toBeVisible();
  await input.setInputFiles({ name: 'imagen.svg', mimeType: 'image/svg+xml', buffer: Buffer.from('<svg/>') });
  await expect(dialog.getByRole('alert')).toContainText('PNG, JPEG o WebP');
  await expect(save).toBeDisabled();
  await input.setInputFiles({ name: 'ejemplo.png', mimeType: 'image/png',
    buffer: Buffer.from([137,80,78,71,13,10,26,10]) });
  await expect(dialog.getByText('ejemplo.png')).toBeVisible();
  await expect(dialog.getByRole('alert')).toHaveCount(0);
  await expect(save).toBeEnabled();
  const a11y = await new AxeBuilder({ page }).include('dialog.cs-modal:modal')
    .withTags(['wcag2a','wcag2aa','wcag21aa','wcag22aa']).analyze();
  expect(a11y.violations).toEqual([]);
  await dialog.getByRole('button', { name: 'Cerrar ventana' }).click();
  await page.getByRole('button', { name: 'Añadir foto de perfil' }).click();
  await expect(page.getByRole('dialog', { name: 'Actualizar foto de perfil' })
    .getByText('Ninguna imagen seleccionada')).toBeVisible();
});

test('F06 buyer operator photo picker rejects oversized file with safe Spanish feedback', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 720 });
  await operator(page);
  await page.getByRole('navigation', { name: 'Áreas de la consola de identidad' })
    .getByRole('button', { name: 'Perfil', exact: true }).click();
  await page.getByRole('button', { name: 'Actualizar foto de operador' }).click();
  const dialog = page.getByRole('dialog', { name: 'Actualizar foto de operador' });
  const input = dialog.getByLabel(/Foto del operador \(PNG, JPEG o WebP, hasta 2 MB\)/);
  const save = dialog.getByRole('button', { name: 'Guardar foto de operador' });
  await expect(save).toBeDisabled();
  await expect(dialog.getByText('Seleccionar imagen del dispositivo')).toBeVisible();
  await input.setInputFiles({ name: 'grande.webp', mimeType: 'image/webp',
    buffer: Buffer.alloc(2 * 1024 * 1024 + 1) });
  await expect(dialog.getByRole('alert')).toContainText('supera 2 MB');
  await expect(save).toBeDisabled();
  await input.setInputFiles({ name: 'perfil.webp', mimeType: 'image/webp',
    buffer: Buffer.from([82,73,70,70]) });
  await expect(dialog.getByText('perfil.webp')).toBeVisible();
  await expect(save).toBeEnabled();
  const a11y = await new AxeBuilder({ page }).include('dialog.cs-modal:modal')
    .withTags(['wcag2a','wcag2aa','wcag21aa','wcag22aa']).analyze();
  expect(a11y.violations).toEqual([]);
});

test('F06 buyer terms read does not impersonate a completed account mutation', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 720 });
  await syntheticApi(page, 'anonymous');
  await page.route('**/api/v1/identity/terms', route => route.fulfill({
    status: 200, contentType: 'application/json',
    body: JSON.stringify({ version: 'qa-f06', hash: 'b'.repeat(64),
      text: 'Condiciones de prueba sin datos personales.' })
  }));
  await page.goto('/cuenta?vista=ingresar');
  const terms = page.locator('details.account-terms');
  await terms.locator('summary').click();
  await terms.getByRole('button', { name: 'Leer condiciones vigentes' }).click();
  await expect(terms).toContainText('Condiciones de prueba sin datos personales.');
  await expect(page.getByText('La solicitud finalizó. Comprueba el estado autorizado antes de continuar.'))
    .toHaveCount(0);
  await expect(page.getByText('Operación completada')).toHaveCount(0);
});

test('F06 buyer public invitation and existing identity load terms in context before enabling submit', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 720 });
  await syntheticApi(page, 'anonymous');
  await page.route('**/api/v1/identity/terms', route => route.fulfill({
    status: 200, contentType: 'application/json',
    body: JSON.stringify({ version: 'qa-terms-2026', hash: 'b'.repeat(64),
      text: 'Contrato ficticio de evaluación. El acceso depende de la autorización real.' })
  }));
  for (const [view, submitText] of [
    ['invitacion', 'Aceptar invitación de cuenta'],
    ['acceso-existente', 'Enviar solicitud de relación pendiente']
  ] as const) {
    await page.goto('/cuenta?vista=' + view);
    const submit = page.getByRole('button', { name: submitText, exact: true });
    await expect(submit).toBeDisabled();
    const guest = page.locator('cs-identity-guest');
    await guest.getByRole('button', { name: 'Cargar condiciones vigentes' }).click();
    const details = guest.locator('details.cs-guest-terms');
    await expect(details).toBeVisible();
    await details.locator('summary').click();
    await expect(details).toContainText('Contrato ficticio de evaluación.');
    await expect(submit).toBeEnabled();
    await expect(page.getByText('La solicitud finalizó. Comprueba el estado autorizado antes de continuar.'))
      .toHaveCount(0);
  }
});

test('F06 buyer newly issued operator backup codes appear before console tasks and can be hidden', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 720 });
  await operator(page);
  await page.evaluate(() => {
    const host = document.querySelector('cs-platform-identity-panel');
    const owner = host && (window as DebugWindow).ng?.getComponent(host);
    if (!owner) throw Error('operator fixture missing');
    owner.platform.recoveryCodes.set(['A'.repeat(32), 'B'.repeat(32)]);
  });
  const backup = page.locator('cs-platform-identity-panel aside[aria-label="Códigos de respaldo del operador"]');
  await expect(backup).toBeVisible();
  const geometry = await backup.evaluate(el => {
    const work = document.querySelector('.operator-content');
    return {
      beforeWork: !!work && !!(el.compareDocumentPosition(work) & Node.DOCUMENT_POSITION_FOLLOWING),
      top: el.getBoundingClientRect().top
    };
  });
  expect(geometry.beforeWork, 'one-use codes must precede the operator console').toBe(true);
  expect(geometry.top, 'operator must see one-use code panel in first phone viewport').toBeLessThan(720);
  await backup.getByRole('button', { name: 'Ocultar respaldos de operador' }).click();
  await expect(backup).toHaveCount(0);
});

test('F06 buyer success-feedback contrast stays WCAG-compliant during entry motion', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 720 });
  await account(page, 'resumen');
  await setAccountStatus(page, 'success', 'resumen');
  const feedback = page.locator('cs-operation-feedback .cs-operation-feedback[data-feedback-kind="success"]');
  await expect(feedback).toBeVisible();
  // A fade-from-transparent animation temporarily rendered an inaccessible
  // contrast ratio during a real successful terms-acceptance transaction.
  await expect(feedback).toHaveCSS('opacity', '1');
  for (const width of [320, 390, 1440]) {
    await page.setViewportSize({ width, height: width < 500 ? 720 : 900 });
    const violations = await new AxeBuilder({ page }).include('cs-operation-feedback')
      .withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa']).analyze();
    expect(violations.violations.filter(v => v.id === 'color-contrast')).toEqual([]);
  }
});

test('F06 buyer invitation first field prioritization at 320 pixels', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 720 });
  await syntheticApi(page, 'anonymous');
  for (const [view, label] of [
    ['invitacion', 'Correo invitado'],
    ['acceso-existente', 'Correo de tu cuenta']
  ] as const) {
    await page.goto('/cuenta?vista=' + view);
    const field = page.getByRole('textbox', { name: label, exact: true });
    await expect(field).toBeVisible();
    const top = await field.evaluate(el => el.getBoundingClientRect().top);
    expect(top, view + ' primary account field must not be delayed by duplicate guidance')
      .toBeLessThanOrEqual(550);
    await expect.poll(() => page.evaluate(() =>
      document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(2);
  }
});

test('F06 buyer session dashboard prioritizes operations over duplicate information', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 720 });
  await account(page, 'resumen');
  const action = page.getByRole('button', { name: 'Actualizar dispositivos', exact: true });
  await expect(action).toBeVisible();
  const geometry = await action.evaluate(el => {
    const card = document.querySelector('#identity-session-card-heading');
    return { buttonBottom: el.getBoundingClientRect().bottom,
      beforePolicy: !!card && !!(el.compareDocumentPosition(card) & Node.DOCUMENT_POSITION_FOLLOWING) };
  });
  expect(geometry.buttonBottom, 'device action must appear in the initial 320x720 viewport')
    .toBeLessThanOrEqual(720);
  expect(geometry.beforePolicy, 'actions must be placed before supporting policy copy').toBe(true);
  await expect(page.getByText('30 minutos', { exact: true }).first()).toBeVisible();
});

test('F06 buyer invitation and existing-account management stay progressively discoverable', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 720 });
  await syntheticApi(page, 'anonymous');
  for (const view of ['invitacion', 'acceso-existente']) {
    await page.goto('/cuenta?vista=' + view);
    const secondary = page.locator('details.account-more');
    await expect(secondary).not.toHaveAttribute('open');
    await secondary.locator('summary').focus();
    await page.keyboard.press('Enter');
    await expect(secondary).toHaveAttribute('open', '');
    await expect(secondary.getByRole('navigation', { name: 'Otras opciones de cuenta' }))
      .toBeVisible();
    await expect.poll(() => page.evaluate(() =>
      document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(2);
  }
});


test('F06 buyer mandatory invitation and membership flows stay visible, direct and privacy-safe at 320px', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 720 });
  await syntheticApi(page, 'anonymous');
  const cases = [
    { view: 'invitacion', heading: 'Aceptar una invitación', first: 'Correo invitado',
      fields: ['mailbox', 'password', 'code', 'token', 'accepted'] },
    { view: 'acceso-existente', heading: 'Solicitar acceso con mi cuenta', first: 'Correo de tu cuenta',
      fields: ['mailbox', 'password', 'code', 'proofId', 'proofCode', 'accepted'] }
  ] as const;
  for (const scenario of cases) {
    await page.goto('/cuenta?vista=' + scenario.view);
    const flow = page.locator('cs-identity-guest section.cs-guest-process');
    await expect(flow).toBeVisible();
    await expect(flow.getByRole('heading', { name: scenario.heading, level: 2 })).toBeVisible();
    await expect(page.locator('cs-identity-guest > details[open]')).toHaveCount(0);
    await expect(page.getByRole('navigation', { name: 'Opciones de identidad' })).toHaveCount(0);
    const returnLink = page.getByRole('link', { name: 'Volver a iniciar sesión', exact: true });
    await expect(returnLink).toBeVisible();
    const form = flow.locator('form[csFieldFeedback]');
    await expect(form).toHaveCount(1);
    for (const field of scenario.fields) {
      await expect(form.locator('[name="' + field + '"]')).toHaveCount(1);
    }
    await expect(form.locator('input[name="accepted"]')).toHaveAttribute('required', '');
    const first = form.getByRole('textbox', { name: scenario.first, exact: true });
    const firstTop = await first.evaluate(el => el.getBoundingClientRect().top);
    expect(firstTop, scenario.view + ' first verified field must lead the phone flow')
      .toBeLessThanOrEqual(480);
    await expect.poll(() => page.evaluate(() =>
      document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(2);
    await returnLink.click();
    await expect(page.getByRole('heading', { name: 'Iniciar sesión', level: 2 })).toBeVisible();
  }
});


test('F06 buyer recovery wizard shows its first operator input above the protected footer at 320px', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 720 });
  await syntheticApi(page, 'anonymous');
  await page.goto('/cuenta?vista=operadores');
  await page.getByRole('button', { name: 'Recuperar acceso de operador de plataforma' }).click();
  const dialog = page.getByRole('dialog', { name: 'Recuperación segura de operador' });
  await expect(dialog).toBeVisible();
  await expect(dialog.locator('.cs-modal__description')).toContainText('dos revisores independientes');
  await expect(dialog.locator('.cs-modal__description')).toContainText('24 horas');
  await expect(dialog.locator('.cs-modal__description')).toContainText('Una aprobación de negocio no sustituye esta revisión.');
  const input = dialog.getByLabel('Correo del operador sin acceso', { exact: true });
  await expect(input).toBeVisible();
  const geometry = await dialog.evaluate(element => {
    const field = element.querySelector<HTMLInputElement>('#operator-recovery-mail');
    const footer = element.querySelector<HTMLElement>('.cs-modal__footer');
    if (!field || !footer) throw new Error('Recovery wizard input or protected footer is missing');
    return { fieldBottom: field.getBoundingClientRect().bottom, footerTop: footer.getBoundingClientRect().top };
  });
  expect(geometry.fieldBottom, 'first recovery field must not be hidden behind the modal footer')
    .toBeLessThanOrEqual(geometry.footerTop);
  await input.focus();
  await expect(input).toBeFocused();
  await expect.poll(() => page.evaluate(() =>
    document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(2);
});


test('F06 buyer protected wizard step list stays compact and fully readable on 320px', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 720 });
  await syntheticApi(page, 'anonymous');
  await page.goto('/cuenta?vista=operadores');
  await page.getByRole('button', { name: 'Recuperar acceso de operador de plataforma' }).click();
  const dialog = page.getByRole('dialog', { name: 'Recuperación segura de operador' });
  const stepper = dialog.getByRole('navigation', { name: 'Etapas del proceso' });
  await expect(stepper).toBeVisible();
  for (const label of ['Solicitar revisión', 'Aportar dos revisiones', 'Activar nuevo MFA']) {
    await expect(stepper.getByText(label, { exact: true })).toBeVisible();
  }
  const metrics = await stepper.evaluate(element => {
    const box = element.getBoundingClientRect();
    const steps = [...element.querySelectorAll('li')].map(li => li.getBoundingClientRect());
    return { height: box.height, horizontalOverflow: element.scrollWidth - element.clientWidth,
      ordered: steps.every((item, index) => index === 0 || item.left >= steps[index - 1].left) };
  });
  expect(metrics.height, 'three step labels should not consume the whole first viewport')
    .toBeLessThanOrEqual(125);
  expect(metrics.horizontalOverflow, 'step list must not clip security or process text')
    .toBeLessThanOrEqual(2);
  expect(metrics.ordered, 'visual step order must follow the DOM').toBe(true);
  await expect.poll(() => page.evaluate(() =>
    document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(2);
});


test('F06 ended sessions show safe notices in their own realm and manual logout clears them', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 720 });
  await syntheticApi(page, 'business');
  await page.goto('/cuenta?vista=resumen');
  await expect(page.getByRole('navigation', { name: 'Mi cuenta' })).toBeVisible();
  await page.evaluate(() => {
    const el = document.querySelector('cs-identity-portal')!;
    const owner = (window as DebugWindow).ng!.getComponent(el);
    owner.feedbackScope.set('resumen');
    owner.message.set('Recibo anterior al cierre de sesión.');
    owner.identity.clear();
    owner.identity.sessionEndedNotice.set(true); // Synthetic server expiry: no credential reuse.
  });
  await expect(page.locator('cs-operation-feedback [data-feedback-kind="info"]'))
    .toContainText('Tu sesión dejó de estar disponible.');
  await expect(page.getByText('Recibo anterior al cierre de sesión.')).toHaveCount(0);
  await expect(page.getByRole('heading', { name: 'Iniciar sesión', exact: true })).toBeVisible();
  await expect(page.getByRole('navigation', { name: 'Mi cuenta' })).toHaveCount(0);
  await page.evaluate(() => {
    const el = document.querySelector('cs-identity-portal')!;
    const owner = (window as DebugWindow).ng!.getComponent(el);
    owner.identity.clear(); // A deliberate logout cannot masquerade as expiry.
  });
  await expect(page.getByText('Tu sesión dejó de estar disponible.')).toHaveCount(0);

  await syntheticApi(page, 'operator');
  await page.goto('/cuenta?vista=operadores');
  const operatorHost = page.locator('cs-platform-identity-panel');
  await expect(operatorHost.getByRole('navigation', { name: 'Áreas de la consola de identidad' }))
    .toBeVisible();
  await page.evaluate(() => {
    const el = document.querySelector('cs-platform-identity-panel')!;
    const owner = (window as DebugWindow).ng!.getComponent(el);
    owner.feedbackScope.set('casos');
    owner.message.set('Recibo anterior del operador.');
    owner.platform.clear();
    owner.platform.sessionEndedNotice.set(true); // Synthetic server expiry in platform only.
  });
  await expect(operatorHost.locator('cs-operation-feedback [data-feedback-kind="info"]'))
    .toContainText('La sesión de operador terminó o fue revocada.');
  await expect(page.getByText('Recibo anterior del operador.')).toHaveCount(0);
  await expect(operatorHost.getByRole('button', { name: 'Acceder a identidad de plataforma' }))
    .toBeVisible();
  await expect(operatorHost.getByRole('navigation', { name: 'Áreas de la consola de identidad' }))
    .toHaveCount(0);
  await expect(page.getByText('Tu sesión dejó de estar disponible.')).toHaveCount(0);
  await page.evaluate(() => {
    const el = document.querySelector('cs-platform-identity-panel')!;
    (window as DebugWindow).ng!.getComponent(el).platform.clear();
  });
  await expect(page.getByText('La sesión de operador terminó o fue revocada.')).toHaveCount(0);
});

test('F06 authentic restoring status is visible and never mistaken for a completed action', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 720 });
  await syntheticApi(page, 'business');
  await page.goto('/cuenta?vista=resumen');
  // The real initial restore is asynchronous: wait for its finally block before
  // injecting a second synthetic restoring state, so an old response cannot
  // overwrite the fixture and produce a false failure.
  await expect(page.getByRole('navigation', { name: 'Mi cuenta' })).toBeVisible();
  await expect.poll(() => page.evaluate(() => {
    const el = document.querySelector('cs-identity-portal');
    const owner = el && (window as DebugWindow).ng?.getComponent(el);
    if (!owner) throw new Error('Account component unavailable during restore gate');
    return owner.identity.restoring();
  })).toBe(false);
  await page.evaluate(() => {
    const el = document.querySelector('cs-identity-portal')!;
    const owner = (window as DebugWindow).ng!.getComponent(el);
    owner.feedbackScope.set('resumen');
    owner.message.set('Recibo caducado mientras se valida la sesión.');
    owner.identity.restoring.set(true);
  });
  const pending = page.locator('cs-operation-feedback [data-feedback-kind="pending"]');
  await expect(pending).toContainText('Comprobando si tu sesión sigue disponible');
  await expect(pending).toHaveAttribute('aria-busy', 'true');
  await expect(page.getByText('Operación completada')).toHaveCount(0);
  await expect(page.getByText('Recibo caducado mientras se valida la sesión.')).toHaveCount(0);
  await page.evaluate(() => {
    const el = document.querySelector('cs-identity-portal')!;
    (window as DebugWindow).ng!.getComponent(el).identity.restoring.set(false);
  });
  await expect(pending).toHaveCount(0);
});


test('F06 buyer confirm-email screen keeps secondary routes discoverable but initially collapsed', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 720 });
  await syntheticApi(page, 'anonymous');
  await page.goto('/cuenta?vista=confirmar');
  const main = page.getByRole('button', { name: 'Crear cuenta pendiente', exact: true });
  await expect(main).toBeVisible();
  const primary = page.locator('cs-identity-guest form[csFieldFeedback]');
  for (const name of ['proofId', 'proofCode', 'completionMailbox', 'completionPassword']) {
    await expect(primary.locator('[name="' + name + '"]')).toHaveCount(1);
  }
  const secondary = page.locator('details.account-more');
  await expect(secondary).not.toHaveAttribute('open');
  await secondary.locator('summary').focus();
  await page.keyboard.press('Enter');
  await expect(secondary).toHaveAttribute('open', '');
  const links = secondary.getByRole('navigation', { name: 'Otras opciones de cuenta' });
  await expect(links).toBeVisible();
  await expect(links.getByRole('link', { name: 'Confirmar correo' })).toHaveAttribute('aria-current', 'page');
  await expect.poll(() => page.evaluate(() =>
    document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(2);
});

test('F06 buyer: MFA challenge and QR enrollment have one clear heading and no discarded security context', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 720 });
  await syntheticApi(page, 'anonymous');
  await page.goto('/cuenta?vista=ingresar');
  await expect(page.getByRole('heading', { name: 'Iniciar sesión', exact: true })).toBeVisible();
  // Never inject a second state while the service's initial restore is unsettled.
  await expect.poll(() => page.evaluate(() => {
    const host = document.querySelector('cs-identity-portal')!;
    return (window as DebugWindow).ng!.getComponent(host).identity.restoring();
  })).toBe(false);
  await page.evaluate(() => {
    const host = document.querySelector('cs-identity-portal')!;
    const owner = (window as DebugWindow).ng!.getComponent(host);
    owner.identity.stateValue.set('challenge');
  });
  await expect(page.getByRole('heading', { name: 'Verificación en dos pasos', level: 1 })).toHaveCount(1);
  await expect(page.getByRole('heading', { name: 'Introduce tu código', level: 2 })).toHaveCount(1);
  await expect(page.getByLabel('Código de tu aplicación de autenticación')).toBeVisible();
  await page.evaluate(() => {
    const host = document.querySelector('cs-identity-portal')!;
    (window as DebugWindow).ng!.getComponent(host).identity.enrollmentRequired.set(true);
  });
  await expect(page.getByRole('heading', { name: 'Configura tu autenticador', level: 2 })).toHaveCount(1);
  await expect(page.getByRole('heading', { name: 'Introduce tu código' })).toHaveCount(0);
  await expect(page.getByText('La configuración se realiza una sola vez.')).toBeVisible();
  await expect.poll(() => page.evaluate(() =>
    document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(2);
});

test('F06 buyer: platform login and operator challenge avoid duplicate headings without concealing MFA', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 720 });
  await syntheticApi(page, 'anonymous');
  await page.goto('/cuenta?vista=operadores');
  await expect(page.getByRole('heading', { name: 'Identidad de plataforma', level: 1 })).toHaveCount(1);
  await expect(page.locator('cs-platform-identity-panel > section.operator-panel > h2')).toHaveCount(0);
  const action = page.getByRole('button', { name: 'Acceder a identidad de plataforma' });
  await expect(action).toBeVisible();
  expect(await action.evaluate(node => node.getBoundingClientRect().bottom)).toBeLessThanOrEqual(720);
  await expect.poll(() => page.evaluate(() => {
    const host = document.querySelector('cs-platform-identity-panel')!;
    return (window as DebugWindow).ng!.getComponent(host).platform.restoring();
  })).toBe(false);
  await page.evaluate(() => {
    const host = document.querySelector('cs-platform-identity-panel')!;
    const owner = (window as DebugWindow).ng!.getComponent(host);
    owner.platform.state.set('challenge');
  });
  await expect(page.getByLabel('Código MFA del operador')).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Identidad de plataforma', level: 1 })).toHaveCount(1);
  await expect(page.locator('cs-platform-identity-panel > section.operator-panel > h2')).toHaveCount(0);
  await expect.poll(() => page.evaluate(() =>
    document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(2);
});

test('F06 recovery secondary navigation stays folded while all real recovery methods remain discoverable', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 720 });
  await syntheticApi(page, 'anonymous');
  await page.goto('/cuenta?vista=recuperar');
  const summary = page.locator('details.account-more summary');
  const secondary = page.locator('details.account-more');
  await expect(secondary).not.toHaveAttribute('open');
  await expect(summary).toBeVisible();
  for (const method of [
    'Ya tengo mi código de recuperación',
    'Reportar un cambio que no reconozco',
    'Perdí todos mis métodos de acceso'
  ]) {
    await expect(page.locator('cs-identity-recovery summary').filter({ hasText: method })).toBeVisible();
  }
  await summary.focus();
  await page.keyboard.press('Enter');
  await expect(secondary).toHaveAttribute('open', '');
  await expect(secondary.getByRole('navigation', { name: 'Otras opciones de cuenta' })).toBeVisible();
  await expect.poll(() => page.evaluate(() =>
    document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(2);
});

test('F06 secondary routes reset when navigating between public identity tasks without reload', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 720 });
  await syntheticApi(page, 'anonymous');
  await page.goto('/cuenta?vista=recuperar');
  const options = page.locator('details.account-more');
  await expect(options).not.toHaveAttribute('open');
  await options.locator('summary').focus();
  await page.keyboard.press('Enter');
  await expect(options).toHaveAttribute('open', '');
  await options.getByRole('link', { name: 'Confirmar correo' }).click();
  await expect(page).toHaveURL(/vista=confirmar/);
  await expect(page.getByRole('heading', { name: 'Confirmar correo' })).toBeVisible();
  await expect(options).not.toHaveAttribute('open');
  await options.locator('summary').focus();
  await page.keyboard.press('Enter');
  await expect(options).toHaveAttribute('open', '');
  await options.getByRole('link', { name: 'Recuperar acceso' }).click();
  await expect(page).toHaveURL(/vista=recuperar/);
  await expect(options).not.toHaveAttribute('open');
  await expect(page.getByRole('button', { name: 'Solicitar recuperación' })).toBeVisible();
  await expect.poll(() => page.evaluate(() =>
    document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(2);
});

test('F06 buyer: desktop MFA and platform login retain focused reading widths', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await syntheticApi(page, 'anonymous');
  await page.goto('/cuenta?vista=ingresar');
  await expect(page.getByRole('heading', { name: 'Iniciar sesión', exact: true })).toBeVisible();
  await expect.poll(() => page.evaluate(() => {
    const host = document.querySelector('cs-identity-portal')!;
    return (window as DebugWindow).ng!.getComponent(host).identity.restoring();
  })).toBe(false);
  await page.evaluate(() => {
    const host = document.querySelector('cs-identity-portal')!;
    (window as DebugWindow).ng!.getComponent(host).identity.stateValue.set('challenge');
  });
  const intro = page.locator('.account-workspace-intro--challenge');
  const otp = page.locator('cs-identity-mfa');
  await expect(intro).toHaveCSS('display', 'block');
  await expect(page.getByRole('heading', { name: 'Verificación en dos pasos', level: 1 })).toBeVisible();
  const [introBox, otpBox] = await Promise.all([intro.boundingBox(), otp.boundingBox()]);
  expect(introBox, 'MFA heading must remain in a readable column').not.toBeNull();
  expect(otpBox, 'MFA form must remain in a readable column').not.toBeNull();
  expect(introBox!.width).toBeLessThanOrEqual(700);
  expect(otpBox!.width).toBeLessThanOrEqual(700);
  expect(Math.abs(introBox!.x - otpBox!.x)).toBeLessThanOrEqual(2);
  await page.goto('/cuenta?vista=operadores');
  const button = page.getByRole('button', { name: 'Recuperar acceso de operador de plataforma' });
  const form = page.locator('cs-platform-identity-panel > section.operator-panel > form');
  await expect(button).toBeVisible();
  const [buttonBox, formBox] = await Promise.all([button.boundingBox(), form.boundingBox()]);
  expect(buttonBox).not.toBeNull();
  expect(formBox).not.toBeNull();
  expect(buttonBox!.width).toBeLessThanOrEqual(650);
  expect(Math.abs(buttonBox!.x - formBox!.x)).toBeLessThanOrEqual(2);
  await expect.poll(() => page.evaluate(() =>
    document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(2);
});

test('F06 premium editorial: confirmation and recovery prioritize their own forms over unrelated tabs', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 720 });
  await syntheticApi(page, 'anonymous');
  for (const task of [
    { view: 'confirmar', heading: 'Confirmar correo', action: 'Crear cuenta pendiente' },
    { view: 'recuperar', heading: 'Recuperar contraseña', action: 'Solicitar recuperación' }
  ]) {
    await page.goto('/cuenta?vista=' + task.view);
    await expect(page.getByRole('heading', { name: task.heading })).toBeVisible();
    await expect(page.getByRole('navigation', { name: 'Opciones de identidad' })).toHaveCount(0);
    await expect(page.locator('.account-guest-return')).toBeVisible();
    await expect(page.getByRole('button', { name: task.action })).toBeVisible();
    await expect(page.locator('details.account-more')).not.toHaveAttribute('open');
    await expect.poll(() => page.evaluate(() =>
      document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(2);
  }
  await page.locator('.account-guest-return').click();
  await expect(page).toHaveURL(/vista=ingresar/);
  await expect(page.getByRole('navigation', { name: 'Opciones de identidad' })).toBeVisible();
});


test('F06 buyer contextual public headings avoid misleading guest marketing', async ({ page }) => {
  await syntheticApi(page, 'anonymous');
  const journeys = [
    { view: 'confirmar', title: 'Confirma tu correo.',
      message: 'La aprobación del negocio sigue siendo necesaria.', task: 'Confirmar correo' },
    { view: 'recuperar', title: 'Recupera tu acceso.',
      message: 'no abre sesión por sí sola.', task: 'Recuperar contraseña' },
    { view: 'invitacion', title: 'Tu invitación.',
      message: 'segundo factor y aceptación', task: 'Aceptar una invitación' },
    { view: 'acceso-existente', title: 'Tu cuenta, más negocios.',
      message: 'sin transferir permisos ni datos privados', task: 'Solicitar acceso con mi cuenta' }
  ] as const;
  for (const width of [320, 1440]) {
    await page.setViewportSize({ width, height: width === 320 ? 720 : 900 });
    for (const journey of journeys) {
      await page.goto('/cuenta?vista=' + journey.view);
      const aside = page.locator('.account-layout--guest .account-aside');
      if (journey.view === 'recuperar' && width === 320) {
        // F06 intentionally removes this decorative hero on narrow recovery
        // screens so the actual secured recovery form is the first task.
        await expect(aside).toBeHidden();
        await expect(aside).toContainText(journey.message);
      } else {
        await expect(aside.getByRole('heading', { level: 1, name: journey.title })).toBeVisible();
        await expect(aside).toContainText(journey.message);
      }
      await expect(page.getByRole('heading', { name: journey.task })).toBeVisible();
      await expect(page.getByRole('navigation', { name: 'Opciones de identidad' })).toHaveCount(0);
      await expect.poll(() => page.evaluate(() =>
        document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(2);
    }
  }
  await page.goto('/cuenta?vista=ingresar');
  await expect(page.getByRole('heading', { level: 1, name: 'Tu cuenta, a tu manera.' })).toBeVisible();
});


test('F06 buyer staged proofs preserve every mandatory security field', async ({ page }) => {
  await syntheticApi(page, 'anonymous');
  const tasks = [
    { view: 'confirmar', stages: ['1. Verificación del correo', '2. Crear la cuenta'],
      inputs: ['proofId', 'proofCode', 'completionMailbox', 'completionPassword'],
      submit: 'Crear cuenta pendiente' },
    { view: 'invitacion', stages: ['1. Identidad y segundo factor', '2. Invitación y condiciones'],
      inputs: ['mailbox', 'password', 'code', 'token', 'accepted'],
      submit: 'Aceptar invitación de cuenta' },
    { view: 'acceso-existente', stages: ['1. Identidad y segundo factor', '2. Confirmación del correo'],
      inputs: ['mailbox', 'password', 'code', 'proofId', 'proofCode', 'accepted'],
      submit: 'Enviar solicitud de relación pendiente' }
  ] as const;
  for (const width of [320, 390, 768, 1440]) {
    await page.setViewportSize({ width, height: width === 320 ? 720 : 900 });
    for (const task of tasks) {
      await page.goto('/cuenta?vista=' + task.view);
      const guest = page.locator('cs-identity-guest');
      const groups = guest.getByRole('group');
      await expect(groups).toHaveCount(2);
      for (const stage of task.stages) {
        await expect(guest.getByRole('group', { name: stage })).toBeVisible();
      }
      for (const name of task.inputs) {
        const control = guest.locator('form [name="' + name + '"]');
        await expect(control).toHaveCount(1);
        await expect(control).toHaveAttribute('required', '');
      }
      await expect(guest.getByRole('button', { name: task.submit })).toBeVisible();
      if (task.view === 'acceso-existente') {
        await expect(guest.locator('input[name="code"]')).toHaveAttribute(
          'pattern', '([0-9]{6}|[0-9a-fA-F]{32})');
        await expect(guest.locator('input[name="proofCode"]')).toHaveAttribute('maxlength', '64');
      }
      await expect.poll(() => page.evaluate(() =>
        document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(2);
    }
  }
});
