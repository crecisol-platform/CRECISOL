/** CRECISOL C05 UX F05 — isolated synthetic scenarios: never asserts backend authorizations.
 * Actual Angular→API .NET10→MySQL8.4 security regression is a distinct CI job. */
import { test, expect, type Page, type TestInfo } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import { writeFile } from 'node:fs/promises';
import QRCode from 'qrcode';

type AngularDebug = Window & { ng?: { getComponent: (node: Element) => any } };
type Realm = 'operator' | 'business' | 'anonymous';
type Scenario =
  | 'recuperacion-operador-solicitud' | 'recuperacion-operador-doble-control'
  | 'mfa-operador-verificacion' | 'mfa-operador-vinculacion'
  | 'passkey-registro' | 'passkey-revocacion'
  | 'cambio-contrasena' | 'aprobacion-membresia' | 'invitacion'
  | 'revocacion-sesion' | 'renovacion-sesion';

const scenarios: readonly Scenario[] = [
  'recuperacion-operador-solicitud', 'recuperacion-operador-doble-control',
  'mfa-operador-verificacion', 'mfa-operador-vinculacion',
  'passkey-registro', 'passkey-revocacion',
  'cambio-contrasena', 'aprobacion-membresia', 'invitacion',
  'revocacion-sesion', 'renovacion-sesion'
];
const viewports = [
  { width: 320, height: 720 }, { width: 390, height: 844 },
  { width: 768, height: 1024 }, { width: 1024, height: 768 },
  { width: 1440, height: 900 }
] as const;
const id = '33333333-4444-4555-8666-777777777777';

async function syntheticApi(page: Page, realm: Realm): Promise<void> {
  await page.route('**/api/v1/**', r => r.fulfill({
    status: 503, contentType: 'application/json', body: '{}'
  }));
  await page.route('**/api/v1/public/branding/platform', r => r.fulfill({
    status: 200, contentType: 'application/json', body: JSON.stringify({
      revision: 1, name: 'CRECISOL', slogan: 'Tu negocio, a tu manera.',
      descriptor: 'Comercio · Servicios · Membresías', primaryColor: '#0d468b',
      accentColor: '#1878c8', logoUrl: '/assets/crecisol-oficial.png',
      websiteUrl: null, publishedAt: '2026-11-19T00:00:00Z',
      presentation: { faviconUrl: '/assets/crecisol-emblem.png',
        typographyToken: 'system', iconSetToken: 'outline',
        seoTitle: 'CRECISOL', seoDescription: 'Pruebas sintéticas',
        copyrightNotice: 'Datos ficticios' }
    })
  }));
  const receipt = () => JSON.stringify({
    csrfToken: 'a'.repeat(64),
    expiresAtUtc: new Date(Date.now() + 30 * 60_000).toISOString(),
    idleMinutes: 30
  });
  await page.route('**/api/v1/identity/browser/restore', r => r.fulfill({
    status: realm === 'business' ? 200 : 401, contentType: 'application/json',
    body: realm === 'business' ? receipt() : '{}'
  }));
  await page.route('**/api/v1/platform-identity/restore', r => r.fulfill({
    status: realm === 'operator' ? 200 : 401, contentType: 'application/json',
    body: realm === 'operator' ? receipt() : '{}'
  }));
  await page.route('**/api/v1/platform-identity/session-policy', r => r.fulfill({
    status: 200, contentType: 'application/json',
    body: JSON.stringify({ idleMinutes: 30, revision: 3, canManage: false })
  }));
}

async function seedAccount(page: Page): Promise<void> {
  await page.evaluate(() => {
    const portal = document.querySelector('cs-identity-portal');
    const owner = portal && (window as AngularDebug).ng?.getComponent(portal);
    if (!owner) throw Error('F05 business QA owner unavailable');
    owner.identity.profile.set({
      displayName: 'Cuenta ficticia de auditoría', language: 'es-HN',
      timeZone: 'America/Tegucigalpa'
    });
    owner.identity.passkeys.set([{ id: '33333333-4444-4555-8666-777777777777',
      createdAtUtc: '2026-11-15T12:00:00Z' }]);
    owner.identity.pendingMemberships.set([{
      id: '55555555-6666-4777-8888-999999999999',
      requestedAtUtc: '2026-11-19T11:00:00Z',
      mailboxHint: 'so•••@example.test'
    }]);
    owner.identity.pendingLoaded.set(true);
  });
}

async function seedOperator(page: Page): Promise<void> {
  await page.evaluate(() => {
    const panel = document.querySelector('cs-platform-identity-panel');
    const owner = panel && (window as AngularDebug).ng?.getComponent(panel);
    if (!owner) throw Error('F05 platform QA owner unavailable');
    owner.platform.sessions.set([{
      id: '11111111-2222-4333-8444-555555555555', device: 'Chromium',
      lastSeenAtUtc: new Date(Date.now() - 2 * 60_000).toISOString(),
      expiresAtUtc: new Date(Date.now() + 28 * 60_000).toISOString()
    }]);
    owner.platform.sessionsLoaded.set(true);
  });
}

function expectedKind(s: Scenario): string {
  if (s.startsWith('recuperacion-') || s.startsWith('mfa-operador')) return 'wizard';
  if (['passkey-revocacion','aprobacion-membresia','revocacion-sesion'].includes(s)) return 'destructive';
  if (s === 'renovacion-sesion') return 'simple';
  return 'form';
}

function expectedTitle(s: Scenario): string {
  const labels: Record<Scenario,string> = {
    'recuperacion-operador-solicitud': 'Recuperación segura de operador',
    'recuperacion-operador-doble-control': 'Recuperación segura de operador',
    'mfa-operador-verificacion': 'Reemplazar MFA de operador',
    'mfa-operador-vinculacion': 'Reemplazar MFA de operador',
    'passkey-registro': 'Registrar nueva llave de acceso',
    'passkey-revocacion': 'Revocar llave de acceso',
    'cambio-contrasena': 'Cambiar contraseña',
    'aprobacion-membresia': 'Revisar membresía con permiso administrativo',
    invitacion: 'Invitar a una cuenta',
    'revocacion-sesion': 'Revocar sesión de operador',
    'renovacion-sesion': 'Renovar la sesión de plataforma'
  };
  return labels[s];
}

async function openScenario(page: Page, scenario: Scenario): Promise<void> {
  if (scenario.startsWith('recuperacion-')) {
    await page.goto('/cuenta?vista=operadores');
    const opener = page.getByRole('button', { name: 'Recuperar acceso de operador de plataforma', exact: true });
    await expect(opener).toBeVisible();
    await opener.click();
    if (scenario === 'recuperacion-operador-doble-control')
      await page.getByRole('button', { name: 'Ya tengo las dos partes verificadas' }).click();
    return;
  }
  if (scenario.startsWith('mfa-operador') || scenario.endsWith('sesion')) {
    await page.goto('/cuenta?vista=operadores');
    const nav = page.getByRole('navigation', { name: 'Áreas de la consola de identidad' });
    await expect(nav).toBeVisible();
    await seedOperator(page);
    if (scenario.startsWith('mfa-operador')) {
      await nav.getByRole('button', { name: 'Seguridad', exact: true }).click();
      if (scenario === 'mfa-operador-vinculacion') {
        const qr = await QRCode.toDataURL('otpauth://totp/CRECISOL-QA:ficticio?secret=JBSWY3DPEHPK3PXP&issuer=CRECISOL-QA&algorithm=SHA1', { width: 256, margin: 2 });
        await page.evaluate((encoded) => {
          const panel = document.querySelector('cs-platform-identity-panel');
          const owner = panel && (window as AngularDebug).ng?.getComponent(panel);
          if (!owner) throw Error('F05 factor QA owner unavailable');
          owner.platform.factorSecret.set('JBSWY3DPEHPK3PXP');
          owner.platform.factorQr.set(encoded);
        }, qr);
      }
      await page.getByRole('button', { name: 'Reemplazar MFA de operador', exact: true }).click();
    } else {
      await nav.getByRole('button', { name: 'Sesiones', exact: true }).click();
      if (scenario === 'revocacion-sesion')
        await page.getByRole('button', { name: 'Revocar sesión de operador Terminación 555555' }).click();
      else
        await page.getByRole('button', { name: 'Rotar sesión de operador', exact: true }).click();
    }
    return;
  }
  const view =
    scenario.startsWith('passkey') ? 'llaves' :
    ['aprobacion-membresia','invitacion'].includes(scenario) ? 'administracion' : 'seguridad';
  await page.goto('/cuenta?vista=' + view);
  await expect(page.getByRole('navigation', { name: 'Mi cuenta' })).toBeVisible();
  await seedAccount(page);
  if (scenario === 'passkey-registro')
    await page.getByRole('button', { name: 'Registrar nueva llave de acceso', exact: true }).click();
  else if (scenario === 'passkey-revocacion')
    await page.getByRole('button', { name: 'Revocar esta llave', exact: true }).click();
  else if (scenario === 'cambio-contrasena')
    await page.getByRole('button', { name: 'Cambiar contraseña', exact: true }).click();
  else if (scenario === 'aprobacion-membresia')
    await page.getByRole('button', { name: 'Revisar solicitud', exact: true }).click();
  else if (scenario === 'invitacion')
    await page.getByRole('button', { name: 'Invitar a una cuenta', exact: true }).click();
}

async function geometry(page: Page): Promise<{ width: number; height: number; dialogHeight: number; overflow: number }> {
  return page.evaluate(() => {
    const dlg = document.querySelector('dialog.cs-modal:modal') as HTMLDialogElement | null;
    const scroller = dlg?.querySelector('.cs-modal__content') as HTMLElement | null;
    if (!dlg || !scroller) throw Error('Missing active native dialog');
    const r = dlg.getBoundingClientRect();
    const v = window.visualViewport;
    const width = v?.width ?? innerWidth, height = v?.height ?? innerHeight;
    if (r.left < -2 || r.top < -2 || r.right > width + 2 || r.bottom > height + 2)
      throw Error('Modal cut off by viewport: ' + JSON.stringify({ r: { left:r.left, right:r.right,top:r.top,bottom:r.bottom },width,height }));
    if (scroller.scrollWidth > scroller.clientWidth + 2)
      throw Error('Modal content overflows horizontally');
    if (document.documentElement.scrollWidth > document.documentElement.clientWidth + 2)
      throw Error('Page overflow under modal');
    return { width, height, dialogHeight: r.height,
      overflow: Math.max(0, scroller.scrollWidth - scroller.clientWidth) };
  });
}

for (const viewport of viewports) for (const scenario of scenarios) {
  test('F05 ' + scenario + ' at ' + viewport.width + 'px', async ({ page }, info: TestInfo) => {
    await page.setViewportSize(viewport);
    const realm: Realm = scenario.startsWith('recuperacion-') ? 'anonymous' :
      scenario.startsWith('mfa-operador') || scenario.endsWith('sesion') ? 'operator' : 'business';
    await syntheticApi(page, realm);
    await openScenario(page, scenario);
    const title = expectedTitle(scenario);
    const dlg = page.getByRole('dialog', { name: title, exact: true });
    await expect(dlg).toBeVisible();
    await expect(dlg).toHaveAttribute('data-kind', expectedKind(scenario));
    await expect(page.locator('dialog.cs-modal:modal')).toHaveCount(1);
    const box = await geometry(page);
    // Premium mobile and desktop touch/keyboard targets: do not solve clipping
    // by hiding the native close controls or shrinking their hit areas.
    for (const selector of ['.cs-modal__dismiss', '.cs-modal__cancel']) {
      const target = dlg.locator(selector);
      await expect(target).toBeVisible();
      const rect = await target.boundingBox();
      expect(rect?.width ?? 0, selector + ' width').toBeGreaterThanOrEqual(43.75);
      expect(rect?.height ?? 0, selector + ' height').toBeGreaterThanOrEqual(43.75);
    }
    if (expectedKind(scenario) === 'wizard') {
      const announcement = dlg.locator('.cs-modal__step-announcement');
      await expect(announcement).toHaveAttribute('aria-live', 'polite');
      await expect(announcement).toContainText('Paso ');
      const current = dlg.locator('.cs-modal__step--current');
      await expect(current).toHaveCount(1);
      await expect(current).toHaveAttribute('aria-current', 'step');
      if (scenario.endsWith('doble-control') || scenario.endsWith('vinculacion'))
        await expect(dlg.locator('.cs-modal__step--complete')).toHaveCount(1);
      if (scenario === 'recuperacion-operador-doble-control') {
        await expect(dlg.locator('.cs-modal__stepper')).toBeInViewport({ ratio: 0.9 });
        await expect.poll(() => dlg.locator('.cs-modal__content')
          .evaluate((element: Element) => element.scrollTop)).toBe(0);
      }
    }
    if (expectedKind(scenario) === 'destructive') {
      const risk = dlg.getByRole('note', { name: 'Advertencia de seguridad' });
      await expect(risk).toBeVisible();
      await expect(risk.locator('span')).not.toBeEmpty();
    }
    if (scenario === 'passkey-revocacion')
      await expect(dlg.getByLabel('Llave de acceso a revocar').locator('option:checked'))
        .toHaveText('Terminación 777777');
    if (scenario === 'aprobacion-membresia') {
      await expect(dlg.getByLabel('Solicitud pendiente seleccionada').locator('option:checked'))
        .toContainText('so•••@example.test');
      await expect(dlg.getByText('Expediente seleccionado · Terminación 999999'))
        .toBeVisible();
    }
    const a11y = await new AxeBuilder({ page }).include('dialog.cs-modal:modal')
      .withTags(['wcag2a','wcag2aa','wcag21aa','wcag22aa']).analyze();
    expect(a11y.violations).toEqual([]);
    const key = 'modal-' + scenario + '-' + viewport.width;
    const target = info.outputPath(key + '.png');
    await page.screenshot({ path: target, animations: 'disabled' });
    await writeFile(info.outputPath(key + '.json'), JSON.stringify({
      key, scenario, kind: expectedKind(scenario), title, synthetic: true,
      realm, runnerSha: process.env.GITHUB_SHA ?? 'local',
      viewport, metrics: box, png: key + '.png'
    }, null, 2));
    await page.keyboard.press('Escape');
    await expect(dlg).toHaveCount(0);
    await expect(page.locator('body')).not.toHaveCSS('overflow', 'hidden');
  });
}

test('F05 native dialog traps keyboard and restores its exact launcher', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await syntheticApi(page, 'business');
  await openScenario(page, 'cambio-contrasena');
  const dlg = page.getByRole('dialog', { name: 'Cambiar contraseña', exact: true });
  await expect(dlg.getByRole('heading', { name: 'Cambiar contraseña' })).toBeFocused();
  await page.keyboard.press('Tab');
  expect(await page.evaluate(() => Boolean(document.activeElement?.closest('dialog.cs-modal')))).toBe(true);
  await dlg.getByLabel('Contraseña actual').fill('EXAMPLE-ONLY-NOT-A-REAL-CREDENTIAL');
  await page.keyboard.press('Escape');
  await expect(dlg).toHaveCount(0);
  await expect(page.getByRole('button', { name: 'Cambiar contraseña', exact: true })).toBeFocused();
  await page.getByRole('button', { name: 'Cambiar contraseña', exact: true }).click();
  await expect(dlg.getByLabel('Contraseña actual')).toHaveValue('');
});

test('F05 only one privileged native dialog may exist at a time', async ({ page }) => {
  await syntheticApi(page, 'business');
  await openScenario(page, 'cambio-contrasena');
  await page.evaluate(() => {
    const host = [...document.querySelectorAll('cs-institutional-modal')].find(x => x.getAttribute('title') === 'Cambiar correo');
    const dialog = host && (window as AngularDebug).ng?.getComponent(host);
    if (!dialog) throw Error('F05 nested-dialog fixture unavailable');
    dialog.open();
  });
  await expect(page.locator('dialog.cs-modal:modal')).toHaveCount(1);
  await expect(page.getByRole('dialog', { name: 'Cambiar correo' })).toHaveCount(0);
});

test('F05 busy transaction cannot be dismissed by Escape or close button', async ({ page }) => {
  await syntheticApi(page, 'business');
  await openScenario(page, 'cambio-contrasena');
  const dlg = page.getByRole('dialog', { name: 'Cambiar contraseña', exact: true });
  await page.evaluate(() => {
    const node = document.querySelector('cs-identity-portal');
    const owner = node && (window as AngularDebug).ng?.getComponent(node);
    if (!owner) throw Error('Busy-test owner unavailable');
    owner.busy.set(true);
  });
  await expect(dlg.getByRole('button', { name: 'Cerrar ventana' })).toBeDisabled();
  await page.keyboard.press('Escape');
  await expect(dlg).toBeVisible();
  await page.evaluate(() => {
    const node = document.querySelector('cs-identity-portal');
    const owner = node && (window as AngularDebug).ng?.getComponent(node);
    owner.busy.set(false);
  });
  // Wait for Angular's UI to reflect the newly unblocked transaction before
  // testing native cancel again; a synchronous signal is not an instant DOM flush.
  await expect(dlg.getByRole('button', { name: 'Cerrar ventana' })).toBeEnabled();
  await page.keyboard.press('Escape');
  await expect(dlg).toHaveCount(0);
});

test('F05 recovery step 2 is not usable without two parts and new password', async ({ page }) => {
  await syntheticApi(page, 'anonymous');
  await openScenario(page, 'recuperacion-operador-doble-control');
  const dlg = page.getByRole('dialog', { name: 'Recuperación segura de operador' });
  await expect(dlg.getByLabel('Parte del primer revisor de operador')).toBeVisible();
  const submit = dlg.getByRole('button', { name: 'Iniciar reenrolamiento de operador' });
  expect(await submit.evaluate((button: HTMLButtonElement) =>
    (button.closest('form') as HTMLFormElement).checkValidity())).toBe(false);
  await expect(dlg.getByLabel('No tengo disponibles mis métodos seguros de operador')).toHaveCount(0);
  await dlg.getByRole('button', { name: 'Volver a solicitar revisión' }).click();
  await expect(dlg.locator('.cs-modal__step--current')).toContainText('Solicitar revisión');
});

test('F05 zoom 200 percent, landscape, reduced motion and forced colors keep secure dialog usable', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 430 });
  await page.emulateMedia({ forcedColors: 'active', reducedMotion: 'reduce' });
  await syntheticApi(page, 'operator');
  await openScenario(page, 'mfa-operador-verificacion');
  await page.addStyleTag({ content: ':root { font-size: 200% !important; }' });
  await geometry(page);
  const dlg = page.getByRole('dialog', { name: 'Reemplazar MFA de operador' });
  await expect(dlg.getByLabel('Contraseña para reemplazar MFA de operador')).toBeVisible();
  await dlg.getByRole('button', { name: 'Cerrar ventana' }).click();
  await expect(dlg).toHaveCount(0);
});

test('F05 does not stack over a native session or security dialog', async ({ page }) => {
  await syntheticApi(page, 'business');
  await openScenario(page, 'cambio-contrasena');
  const dlg = page.getByRole('dialog', { name: 'Cambiar contraseña', exact: true });
  await page.keyboard.press('Escape');
  await expect(dlg).toHaveCount(0);
  await page.evaluate(() => {
    const other = document.createElement('dialog');
    other.id = 'f05-existing-native-dialog';
    other.setAttribute('aria-label', 'Sesión vencida');
    document.body.appendChild(other);
    other.showModal();
    const host = [...document.querySelectorAll('cs-institutional-modal')]
      .find(node => node.querySelector('h2')?.textContent?.trim() === 'Cambiar contraseña');
    const modal = host && (window as AngularDebug).ng?.getComponent(host);
    if (!modal) throw Error('F05 component fixture unavailable');
    modal.open();
  });
  await expect(page.locator('dialog:modal')).toHaveCount(1);
  await expect(dlg).toHaveCount(0);
  await page.evaluate(() => {
    const other = document.querySelector<HTMLDialogElement>('#f05-existing-native-dialog');
    other?.close();
    other?.remove();
  });
});

test('F05 secure mobile wizard reveals continuation and clears it at the bottom', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 720 });
  await syntheticApi(page, 'anonymous');
  await openScenario(page, 'recuperacion-operador-doble-control');
  const dialog = page.getByRole('dialog', { name: 'Recuperación segura de operador', exact: true });
  await expect(dialog).toBeVisible();
  const content = dialog.locator('.cs-modal__content');
  await expect.poll(() => content.evaluate(el => el.scrollHeight - el.clientHeight)).toBeGreaterThan(12);
  const cue = dialog.locator('.cs-modal__scroll-cue');
  await expect(cue).toBeVisible();
  await expect(cue).toContainText('Desplaza el contenido');
  const footer = dialog.locator('.cs-modal__footer');
  const footerHeight = await footer.evaluate(el => el.getBoundingClientRect().height);
  expect(footerHeight, 'mobile secure footer cannot crowd out the form').toBeLessThanOrEqual(130);
  await expect(cue).toHaveAttribute('aria-label', 'Desplaza el contenido para continuar');
  await content.evaluate(el => { el.scrollTop = el.scrollHeight; el.dispatchEvent(new Event('scroll')); });
  await expect(cue).toHaveCount(0);
  await dialog.getByRole('button', { name: 'Cerrar ventana' }).click();
  await expect(dialog).toHaveCount(0);
  await expect(page.locator('body')).not.toHaveCSS('overflow', 'hidden');
});
