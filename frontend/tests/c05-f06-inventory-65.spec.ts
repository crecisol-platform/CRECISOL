/**
 * C05 / UX F06: 65-surface CURRENT-HEAD visual inventory, NOT A CERTIFICATION.
 * Adapted from F01's original read-only evidence baseline, now tied to this SHA.
 * Source baseline: PR #174; this copy does not modify historical F01.
 * Everything shown here is synthetic and must be labeled as such.
 * No production accounts, real MFA QR, backup codes or personal data are used.
 */
import { test, expect, type Page, type TestInfo } from '@playwright/test';
import { writeFile } from 'node:fs/promises';
import { createHash } from 'node:crypto';

const TARGET_SHA = process.env.GITHUB_SHA ?? 'local';
const sizes = [
  { name: 'mobile-320', width: 320, height: 720 },
  { name: 'mobile-390', width: 390, height: 844 },
  { name: 'tablet-768', width: 768, height: 1024 },
  { name: 'laptop-1024', width: 1024, height: 768 },
  { name: 'desktop-1440', width: 1440, height: 900 }
] as const;

type DebugWindow = Window & {
  ng?: { getComponent(element: Element): any };
};

// Preserve a distinctive cryptographic suffix: a long title and its
// '-scrolled-footer' image must NEVER overwrite each other when slugged.
const slug = (value: string): string => {
  const prefix = value.normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '').toLowerCase()
    .replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '').slice(0, 58);
  const suffix = createHash('sha256').update(value, 'utf8').digest('hex').slice(0, 12);
  return prefix + '-' + suffix;
};


/** F01 source-of-truth identifiers: a missing surface is a failed gate, never skipped. */
const modalInventory: Record<string, string> = {
  "Editar perfil de mi empresa": "C05-F01-024",
  "Actualizar foto de perfil": "C05-F01-025",
  "Eliminar foto de perfil": "C05-F01-026",
  "Registrar nueva llave de acceso": "C05-F01-027",
  "Revocar llave de acceso": "C05-F01-028",
  "Cambiar contraseña": "C05-F01-029",
  "Reemplazar segundo factor": "C05-F01-030",
  "Recuperación del segundo factor con llave": "C05-F01-031",
  "Cambiar correo": "C05-F01-032",
  "Revisar membresía con permiso administrativo": "C05-F01-033",
  "Invitar a una cuenta": "C05-F01-034",
  "Revocar todas mis sesiones": "C05-F01-035",
  "Aprobar otro navegador": "C05-F01-036",
  "Revocar sesión": "C05-F01-037",
  "Consulta segura de recurso": "C05-F01-038",
  "Recuperación segura de operador": "C05-F01-039",
  "Localizar una solicitud de identidad pendiente": "C05-F01-040",
  "Revisar identidad del titular": "C05-F01-041",
  "Emitir parte de recuperación": "C05-F01-042",
  "Revocar sesión de operador": "C05-F01-043",
  "Cerrar todas mis sesiones de plataforma": "C05-F01-044",
  "Editar perfil de operador": "C05-F01-045",
  "Actualizar foto de operador": "C05-F01-046",
  "Cambiar contraseña de operador": "C05-F01-047",
  "Cambiar correo de operador": "C05-F01-048",
  "Reemplazar MFA de operador": "C05-F01-049",
  "Vencimiento de sesiones por inactividad": "C05-F01-050",
};
const pageInventory: Record<string, string> = {
  'ingresar': 'C05-F01-001', 'registro': 'C05-F01-002',
  'confirmar': 'C05-F01-003', 'recuperar': 'C05-F01-004',
  'invitacion': 'C05-F01-005', 'acceso-existente': 'C05-F01-006',
  'resumen': 'C05-F01-009', 'perfil': 'C05-F01-010',
  'llaves': 'C05-F01-011', 'seguridad': 'C05-F01-012',
  'administracion': 'C05-F01-013',
  // F03 Historial is independent; F01-057 is reserved for EMPTY history only.
  'Casos': 'C05-F01-019', 'Sesiones': 'C05-F01-020',
  'Perfil': 'C05-F01-021', 'Seguridad': 'C05-F01-022',
  'Monitor': 'C05-F01-023'
};
function surfaceIdsFor(id: string, modalTitle: string): readonly string[] {
  const ids = new Set<string>();
  const add = (value?: string) => { if (value) ids.add(value); };
  if (modalTitle) add(modalInventory[modalTitle]);
  for (const [view, inventoryId] of Object.entries(pageInventory)) {
    if (id.startsWith('public-' + view + '-') ||
        id.startsWith('account-' + view + '-') ||
        id.startsWith('operator-' + view + '-')) {
      add(inventoryId);
    }
  }
  if (id.startsWith('account-mfa-challenge')) add('C05-F01-007');
  if (id.startsWith('account-mfa-enrollment')) add('C05-F01-008');
  if (id.startsWith('account-terms-current')) add('C05-F01-014');
  if (id.startsWith('public-pairing-')) add('C05-F01-015');
  if (id.startsWith('public-passkey-entry')) add('C05-F01-016');
  if (id.startsWith('operator-login-')) add('C05-F01-017');
  if (id.startsWith('operator-mfa-challenge')) add('C05-F01-018');
  if (id.startsWith('operator-recovery-dialog')) add('C05-F01-039');
  if (id.startsWith('account-loading')) add('C05-F01-051');
  if (id.startsWith('account-error')) add('C05-F01-052');
  if (id.startsWith('account-success')) add('C05-F01-053');
  if (id.startsWith('account-session-cleared')) add('C05-F01-054');
  if (id.startsWith('account-resumen-empty')) add('C05-F01-055');
  if (id.startsWith('account-llaves-empty')) add('C05-F01-056');
  if (id.startsWith('account-perfil-empty') ||
      id.startsWith('account-historial-empty')) add('C05-F01-057');
  if (id.startsWith('account-backup-codes')) add('C05-F01-058');
  if (id.startsWith('operator-restoring')) add('C05-F01-059');
  if (id.startsWith('operator-error')) add('C05-F01-060');
  if (id.startsWith('operator-session-expired')) add('C05-F01-061');
  if (id.startsWith('operator-cases-empty')) add('C05-F01-062');
  if (id.startsWith('operator-monitor-empty')) add('C05-F01-063');
  if (id.startsWith('operator-factor-enrollment')) add('C05-F01-064');
  if (id.startsWith('operator-backup-codes')) add('C05-F01-065');
  return [...ids].sort();
}

async function capture(page: Page, testInfo: TestInfo, id: string): Promise<void> {
  const name = slug(id);
  const path = testInfo.outputPath(name + '.png');
  const measurements = await page.evaluate(() => ({
    route: location.pathname + location.search,
    bodyScrollX: Math.max(0, document.documentElement.scrollWidth - document.documentElement.clientWidth),
    visibleHeadings: [...document.querySelectorAll('h1,h2,h3')]
      .filter(x => x.getBoundingClientRect().height > 0)
      .map(x => x.textContent?.trim()).filter(Boolean).slice(0, 20),
    dialogsOpen: document.querySelectorAll('dialog:modal').length,
    feedbackKinds: [...document.querySelectorAll<HTMLElement>('.cs-operation-feedback[data-feedback-kind]')]
      .map(node => node.getAttribute('data-feedback-kind')).filter(Boolean),
    dialog: (() => {
      const dialog = document.querySelector('dialog.cs-modal:modal');
      if (!dialog) return null;
      const rect = dialog.getBoundingClientRect();
      const footer = dialog.querySelector('.cs-modal__footer')?.getBoundingClientRect();
      const body = dialog.querySelector('.cs-modal__content');
      return {
        scrollTop: body?.scrollTop ?? 0, scrollHeight: body?.scrollHeight ?? 0,
        clientHeight: body?.clientHeight ?? 0,
        horizontalOverflow: body ? Math.max(0, body.scrollWidth - body.clientWidth) : 99,
        inViewport: rect.left >= -1 && rect.top >= -1 &&
          rect.right <= innerWidth + 1 && rect.bottom <= innerHeight + 1,
        footerInViewport: Boolean(footer && footer.top >= rect.top && footer.bottom <= rect.bottom)
      };
    })()
  }));

  expect(measurements.bodyScrollX, 'no horizontal overflow in '+id).toBeLessThanOrEqual(2);
  if (measurements.dialog) {
    expect(measurements.dialog.inViewport, 'native dialog must fit viewport: '+id).toBe(true);
    expect(measurements.dialog.horizontalOverflow, 'modal must not cut horizontally: '+id).toBeLessThanOrEqual(2);
  }
  const modalTitle = measurements.dialogsOpen
    ? ((await page.locator('dialog.cs-modal:modal h2').first().textContent()) ?? '').trim()
    : '';
  const inventoryIds = surfaceIdsFor(id, modalTitle);
  // Surface IDs cannot be established from filenames alone. A rapid operator
  // tab change can otherwise photograph another tab while reporting PASS.
  const operatorHeadingByTab: Record<string, string> = {
    Casos: 'Casos de recuperación y revisión',
    Sesiones: 'Sesiones de plataforma',
    Perfil: 'Mi perfil de operador',
    Seguridad: 'Seguridad de plataforma',
    Monitor: 'Monitor de identidad'
  };
  const operatorTab = id.match(/^operator-(Casos|Sesiones|Perfil|Seguridad|Monitor)-(mobile-320|mobile-390|tablet-768|laptop-1024|desktop-1440)$/)?.[1];
  if (operatorTab) {
    expect(measurements.visibleHeadings, 'wrong operator tab before screenshot: ' + id)
      .toContain(operatorHeadingByTab[operatorTab]);
  }
  await page.screenshot({ path, fullPage: measurements.dialogsOpen === 0, animations: 'disabled' });
  if (operatorTab) {
    const afterHeadings = await page.locator('section.operator-workspace:visible > header.operator-section-head h2')
      .allTextContents();
    expect(afterHeadings.map(s => s.trim()),
      'operator tab changed during screenshot capture: ' + id)
      .toEqual([operatorHeadingByTab[operatorTab]]);
  }
  await writeFile(testInfo.outputPath(name + '.json'), JSON.stringify({
    id, inventoryIds, modalTitle, sourceSha: TARGET_SHA, runnerSha: TARGET_SHA,
    synthetic: true, ...measurements,
    viewport: page.viewportSize(), screenshot: name + '.png',
    captureTimeUtc: new Date().toISOString()
  }, null, 2));
}

async function setup(page: Page, realm: 'anonymous' | 'account' | 'operator'): Promise<void> {
  await page.route('**/api/v1/public/branding/platform', route => route.fulfill({
    status: 200, contentType: 'application/json',
    body: JSON.stringify({
      revision: 1, name: 'CRECISOL', slogan: 'Tu negocio, a tu manera.',
      descriptor: 'Comercio · Servicios · Membresías', primaryColor: '#062E65',
      accentColor: '#078FE8', logoUrl: '/assets/crecisol-oficial.png',
      websiteUrl: null, publishedAt: '2026-11-19T00:00:00Z',
      presentation: { faviconUrl: '/assets/crecisol-emblem.png',
        typographyToken: 'system', iconSetToken: 'outline',
        seoTitle: 'CRECISOL', seoDescription: 'Entorno ficticio de pruebas',
        copyrightNotice: 'Entorno ficticio' }
    })
  }));
  const session = () => ({
    csrfToken: 'a'.repeat(64),
    expiresAtUtc: new Date(Date.now() + 30 * 60_000).toISOString(),
    idleMinutes: 30
  });
  await page.route('**/api/v1/identity/browser/restore', route => route.fulfill({
    status: realm === 'account' ? 200 : 401, contentType: 'application/json',
    body: realm === 'account' ? JSON.stringify(session()) : '{}'
  }));
  await page.route('**/api/v1/platform-identity/restore', route => route.fulfill({
    status: realm === 'operator' ? 200 : 401, contentType: 'application/json',
    body: realm === 'operator' ? JSON.stringify(session()) : '{}'
  }));
  await page.route('**/api/v1/platform-identity/session-policy', route => route.fulfill({
    status: 200, contentType: 'application/json',
    body: JSON.stringify({ idleMinutes: 30, revision: 1, canManage: true })
  }));
  await page.route('**/api/v1/identity/terms', route => route.fulfill({
    status: 200, contentType: 'application/json',
    body: JSON.stringify({
      version: 'qa-f01', hash: 'b'.repeat(64),
      text: 'Condiciones de entorno sintético. No constituyen contrato productivo.'
    })
  }));
}

async function seedAccount(page: Page): Promise<void> {
  await page.evaluate(() => {
    const el = document.querySelector('cs-identity-portal');
    const owner = el && (window as DebugWindow).ng?.getComponent(el);
    if (!owner) throw new Error('Angular debug component unavailable for synthetic F06 inventory snapshots');
    owner.identity.profile.set({
      displayName: 'Persona ficticia', language: 'es-HN', timeZone: 'America/Tegucigalpa'
    });
    owner.identity.sessions.set([{
      id: '11111111-2222-4333-8444-555555555555', device: 'Chromium',
      factor: 'TOTP', lastSeenAtUtc: new Date().toISOString(),
      expiresAtUtc: new Date(Date.now() + 30 * 60_000).toISOString()
    }]);
    owner.identity.securityEvents.set([
      { action: 'login', outcome: 1, atUtc: new Date().toISOString() },
      { action: 'profile/read', outcome: 1, atUtc: new Date().toISOString() }
    ]);
    owner.identity.passkeys.set([{
      id: '33333333-4444-4555-8666-777777777777',
      createdAtUtc: new Date().toISOString()
    }]);
    // A guarded decision dialog is only available after a server-scoped list.
    // This is synthetic UI state and never bypasses backend authorization.
    owner.identity.pendingMemberships.set([{
      id: '55555555-6666-4777-8888-999999999999',
      requestedAtUtc: '2026-11-19T11:00:00Z',
      mailboxHint: 'so•••@example.test'
    }]);
    owner.identity.pendingLoaded.set(true);
    owner.identity.pendingNextCursor.set(null);
  });
}

async function seedOperator(page: Page): Promise<void> {
  await page.evaluate(() => {
    const el = document.querySelector('cs-platform-identity-panel');
    const owner = el && (window as DebugWindow).ng?.getComponent(el);
    if (!owner) throw new Error('Angular debug operator unavailable for synthetic F06 inventory snapshots');
    owner.platform.profile.set({
      displayName: 'Operador ficticio', language: 'es-HN', timeZone: 'America/Tegucigalpa'
    });
    owner.platform.sessions.set([{
      id: 'aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee', device: 'Chromium',
      lastSeenAtUtc: new Date().toISOString(),
      expiresAtUtc: new Date(Date.now() + 30 * 60_000).toISOString()
    }]);
    owner.platform.sessionsLoaded.set(true);
    owner.platform.abuse.set([
      { kind: 'Intentos de acceso rechazados', count: 3 },
      { kind: 'Revisión de factor', count: 1 }
    ]);
    owner.platform.sessionPolicy.set({ idleMinutes: 30, revision: 1, canManage: true });
  });
}

async function captureDialogBottom(page: Page, testInfo: TestInfo, id: string): Promise<void> {
  const body = page.locator('dialog.cs-modal:modal .cs-modal__content');
  if (await body.evaluate(element => element.scrollHeight > element.clientHeight + 6)) {
    await body.evaluate(element => {
      element.scrollTop = element.scrollHeight;
      element.dispatchEvent(new Event('scroll'));
    });
    await expect.poll(() => body.evaluate(element => element.scrollTop)).toBeGreaterThan(0);
    await capture(page, testInfo, id + '-scrolled-footer');
    await body.evaluate(element => { element.scrollTop = 0; });
  }
}

async function captureModals(page: Page, testInfo: TestInfo, prefix: string): Promise<void> {
  const triggers = page.locator('button.institutional-task__trigger:visible');
  const count = await triggers.count();
  for (let i = 0; i < count; i++) {
    const trigger = triggers.nth(i);
    if (await trigger.isDisabled()) continue;
    const label = (await trigger.innerText()).trim() || String(i + 1);
    await trigger.click();
    const dialog = page.locator('dialog.cs-modal:modal');
    await expect(dialog).toHaveCount(1);
    const id = prefix + '-modal-' + String(i + 1).padStart(2, '0') + '-' + label;
    await capture(page, testInfo, id);
    const fileInput = dialog.locator('input[type=file]');
    if (await fileInput.count() === 1) {
      // Synthetic image fixture verifies local selection UI without server upload.
      await fileInput.setInputFiles({ name: 'foto-sintetica.png', mimeType: 'image/png',
        buffer: Buffer.from([137,80,78,71,13,10,26,10]) });
      await expect(dialog.getByText('foto-sintetica.png')).toBeVisible();
      await capture(page, testInfo, id + '-synthetic-photo-selected');
    }
    await captureDialogBottom(page, testInfo, id);
    await dialog.locator('button.cs-modal__cancel').click();
    await expect(dialog).toHaveCount(0);
  }
}

for (const size of sizes) {
  test('F06 COMPLETE-65 | public journeys ' + size.name, async ({ page }, testInfo) => {
    await page.setViewportSize({ width: size.width, height: size.height });
    await setup(page, 'anonymous');
    const views = ['ingresar', 'registro', 'confirmar', 'recuperar', 'invitacion', 'acceso-existente'];
    for (const view of views) {
      await page.goto('/cuenta?vista=' + view);
      await expect(page.locator('#account-content')).toBeVisible();
      const secondary = page.locator('details.account-more');
      await expect(secondary, 'secondary routes must not crowd the task in ' + view)
        .not.toHaveAttribute('open');
      await expect(secondary.locator('summary')).toBeVisible();
      if (view === 'ingresar' || view === 'registro') {
        await expect(page.getByRole('navigation', { name: 'Opciones de identidad' })).toBeVisible();
        await expect(page.locator('.account-guest-return')).toHaveCount(0);
      } else {
        // A protected or focused task must not show unrelated login/register tabs.
        await expect(page.getByRole('navigation', { name: 'Opciones de identidad' })).toHaveCount(0);
        await expect(page.locator('.account-guest-return')).toBeVisible();
        await expect(page.locator('.account-guest-return')).toHaveAttribute('href', /vista=ingresar/);
      }
      await capture(page, testInfo, 'public-' + view + '-' + size.name);
      if (view === 'invitacion' || view === 'acceso-existente') {
        const readTerms = page.locator('cs-identity-guest')
          .getByRole('button', { name: 'Cargar condiciones vigentes' });
        await expect(readTerms).toBeVisible();
        await readTerms.click();
        await expect(page.locator('cs-identity-guest details.cs-guest-terms')).toBeVisible();
        await capture(page, testInfo, 'public-' + view + '-terms-loaded-' + size.name);
      }
      if (view === 'invitacion') {
        const enrollment = page.getByRole('checkbox', { name: 'Todavía no tengo configurado el segundo factor' });
        await enrollment.check();
        await expect(page.locator('cs-identity-guest input[type=hidden][name=code]')).toHaveValue('enroll');
        await capture(page, testInfo, 'public-invitation-mfa-setup-choice-' + size.name);
        await enrollment.uncheck();
      }
    }
    await page.goto('/cuenta?vista=ingresar');
    const terms = page.locator('details.account-terms');
    await terms.locator('summary').click();
    await expect(terms).toHaveAttribute('open','');
    await capture(page, testInfo, 'account-terms-current-'+size.name);
    await page.getByRole('button',{name:'Leer condiciones vigentes'}).click();
    await expect(terms).toContainText('Condiciones de entorno sintético');
    await capture(page, testInfo, 'account-terms-current-loaded-'+size.name);
    await page.getByRole('button',{name:'Entrar con llave de acceso'}).first().waitFor({state:'visible'});
    await capture(page, testInfo, 'public-passkey-entry-'+size.name);
    await page.goto('/cuenta?vista=ingresar');
    await page.getByText('Entrar vinculando un navegador', { exact: true }).click();
    await capture(page, testInfo, 'public-pairing-disclosure-synthetic-' + size.name);
    const { default: pairingQr } = await import('qrcode');
    const fakePairingQr = await pairingQr.toDataURL('https://qa.example.test/c05/f01/pairing-NOT-VALID');
    await page.evaluate((qrData: string) => {
      const el = document.querySelector('cs-identity-portal')!;
      const owner = (window as DebugWindow).ng!.getComponent(el);
      owner.identity.pairing.set({
        token: 'AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA',
        fingerprint: 'AAAAAAAAAAAA', qr: qrData
      });
    }, fakePairingQr);
    await capture(page, testInfo, 'public-pairing-qr-FAKE-ONLY-' + size.name);

    await page.goto('/cuenta?vista=operadores');
    await expect(page.getByRole('heading', { name: 'Identidad de plataforma', level: 1 })).toBeVisible();
    await expect(page.locator('cs-platform-identity-panel > section.operator-panel > h2')).toHaveCount(0);
    await capture(page, testInfo, 'operator-login-' + size.name);
    await page.getByRole('button', { name: 'Recuperar acceso de operador de plataforma' }).click();
    await capture(page, testInfo, 'operator-recovery-dialog-' + size.name);
    await captureDialogBottom(page, testInfo, 'operator-recovery-dialog-' + size.name);
  });

  test('F06 COMPLETE-65 | authenticated business surfaces and 15 modal families ' + size.name,
    async ({ page }, testInfo) => {
    await page.setViewportSize({ width: size.width, height: size.height });
    await setup(page, 'account');
    for (const view of ['resumen', 'perfil', 'llaves', 'seguridad', 'historial', 'administracion']) {
      await page.goto('/cuenta?vista=' + view);
      await expect(page.getByRole('navigation', { name: 'Mi cuenta' })).toBeVisible();
      await capture(page, testInfo, 'account-' + view + '-empty-' + size.name);
      await seedAccount(page);
      if (view === 'perfil') {
        // 1x1 PNG is a synthetic fixture, never a real customer portrait.
        await page.evaluate(() => {
          const owner=(window as DebugWindow).ng!.getComponent(document.querySelector('cs-identity-portal')!);
          owner.identity.photo.set('data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+nx4kAAAAASUVORK5CYII=');
        });
      }
      await capture(page, testInfo, 'account-' + view + '-populated-' + size.name);
      if (view === 'resumen') {
        await page.evaluate(() => {
          const el = document.querySelector('cs-identity-portal')!;
          const owner = (window as DebugWindow).ng!.getComponent(el);
          owner.identity.recoveryCodes.set(['AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA']);
        });
        const accountCode = page.locator('cs-identity-workspace .cs-one-time-codes__value');
        await expect(accountCode).toHaveCount(1);
        await expect(accountCode).toHaveText('A'.repeat(32));
        await expect(accountCode).toHaveAttribute('tabindex', '0');
        await expect(accountCode).toHaveCSS('white-space', 'nowrap');
        await expect.poll(() => page.evaluate(() =>
          document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(2);
        await capture(page, testInfo, 'account-backup-codes-FAKE-ONLY-' + size.name);
        await page.evaluate(() => {
          const el = document.querySelector('cs-identity-portal')!;
          (window as DebugWindow).ng!.getComponent(el).identity.recoveryCodes.set([]);
        });
        await expect(page.locator('cs-identity-workspace .cs-one-time-codes__value')).toHaveCount(0);
      }
      if (view === 'resumen') {
        const extras = page.locator('details.identity-summary-extras');
        await extras.locator('summary').click();
        await expect(extras).toHaveAttribute('open','');
        await capture(page,testInfo,'account-resumen-session-more-open-'+size.name);
      }
      await captureModals(page, testInfo, 'account-' + view + '-' + size.name);
      if (view === 'llaves') {
        const key=page.getByRole('button',{name:'Revocar esta llave',exact:true}).first();
        await expect(key).toBeVisible();
        await key.click();
        const dialog=page.getByRole('dialog',{name:'Revocar llave de acceso'});
        await expect(dialog).toBeVisible();
        await capture(page,testInfo,'account-llaves-revoke-protected-'+size.name);
        await dialog.locator('button.cs-modal__cancel').click();
      }
      if (view === 'administracion') {
        const review=page.getByRole('button',{name:'Revisar solicitud',exact:true}).first();
        await expect(review).toBeVisible();
        await review.click();
        const dialog=page.getByRole('dialog',{name:'Revisar membresía con permiso administrativo'});
        await expect(dialog).toBeVisible();
        await capture(page,testInfo,'account-administracion-review-protected-'+size.name);
        await dialog.locator('button.cs-modal__cancel').click();
      }
      if (view === 'seguridad') {
        await page.getByRole('button', { name: 'Cambiar contraseña', exact: true }).click();
        const dialog = page.getByRole('dialog', { name: 'Cambiar contraseña' });
        await page.evaluate(() => {
          const el = document.querySelector('cs-identity-portal')!;
          const owner = (window as DebugWindow).ng!.getComponent(el);
          owner.error.set('Solicitud rechazada. Revisa los datos sintéticos.');
        });
        await capture(page, testInfo, 'account-modal-error-synthetic-' + size.name);
        await page.evaluate(() => {
          const el = document.querySelector('cs-identity-portal')!;
          const owner = (window as DebugWindow).ng!.getComponent(el);
          owner.error.set('');
          owner.message.set('Solicitud de prueba procesada.');
        });
        await capture(page, testInfo, 'account-modal-feedback-synthetic-' + size.name);
        await dialog.getByRole('button', { name: 'Cerrar', exact: true }).click();
        await expect(dialog).toHaveCount(0);
      }
      if (view === 'resumen') {
        const revoke = page.getByRole('button', { name: 'Revocar esta sesión', exact: true }).first();
        if (await revoke.count()) {
          await revoke.click();
          await capture(page, testInfo, 'account-revoke-single-session-' + size.name);
          await page.locator('dialog.cs-modal:modal button.cs-modal__cancel').click();
        }
      }
    }
  });

  test('F06 COMPLETE-65 | platform console and 12 modal families ' + size.name,
    async ({ page }, testInfo) => {
    await page.setViewportSize({ width: size.width, height: size.height });
    await setup(page, 'operator');
    await page.goto('/cuenta?vista=operadores');
    await expect(page.getByRole('navigation', { name: 'Áreas de la consola de identidad' })).toBeVisible();
    await seedOperator(page);
    const tabHeadings: Record<string, string> = {
      Casos: 'Casos de recuperación y revisión',
      Sesiones: 'Sesiones de plataforma',
      Perfil: 'Mi perfil de operador',
      Seguridad: 'Seguridad de plataforma',
      Monitor: 'Monitor de identidad'
    };
    for (const tab of ['Casos', 'Sesiones', 'Perfil', 'Seguridad', 'Monitor']) {
      await page.getByRole('navigation', { name: 'Áreas de la consola de identidad' })
        .getByRole('button', { name: tab, exact: true }).click();
      await expect(page.locator('section.operator-workspace:visible > header.operator-section-head h2'),
        'the selected operator tab must render before recording ' + tab)
        .toHaveText(tabHeadings[tab]);
      await capture(page, testInfo, 'operator-' + tab + '-' + size.name);
      await captureModals(page, testInfo, 'operator-' + tab + '-' + size.name);
      if (tab === 'Sesiones') {
        await expect(page.getByRole('button', { name: /Revocar sesión de operador / }).first())
          .toBeVisible();
      }
      if (tab === 'Sesiones') {
        const revoke = page.getByRole('button', { name: /Revocar sesión de operador / }).first();
        if (await revoke.count()) {
          await revoke.click();
          await capture(page, testInfo, 'operator-revoke-single-session-' + size.name);
          await page.locator('dialog.cs-modal:modal button.cs-modal__cancel').click();
        }
      }
    }
  });

  test('F06 COMPLETE-65 | empty, loading, error and expiry simulation ' + size.name,
    async ({ page }, testInfo) => {
    await page.setViewportSize({ width: size.width, height: size.height });
    await setup(page, 'account');
    await page.goto('/cuenta?vista=resumen');
    await expect(page.getByRole('navigation', { name: 'Mi cuenta' })).toBeVisible();
    await page.evaluate(() => {
      const el = document.querySelector('cs-identity-portal')!;
      const owner = (window as DebugWindow).ng!.getComponent(el);
      owner.identity.restoring.set(true);
    });
    await expect(page.locator('cs-operation-feedback [data-feedback-kind="pending"]')).toBeVisible();
    await capture(page, testInfo, 'account-loading-synthetic-' + size.name);
    await page.evaluate(() => {
      const el = document.querySelector('cs-identity-portal')!;
      const owner = (window as DebugWindow).ng!.getComponent(el);
      owner.identity.restoring.set(false);
      owner.feedbackScope.set('resumen');
      owner.message.set('');
      owner.error.set('No se pudo completar la solicitud. Inténtalo nuevamente.');
    });
    await expect(page.locator('cs-operation-feedback [data-feedback-kind="error"]'))
      .toContainText('No se pudo completar la solicitud.');
    await capture(page, testInfo, 'account-error-synthetic-' + size.name);
    await page.evaluate(() => {
      const el = document.querySelector('cs-identity-portal')!;
      const owner = (window as DebugWindow).ng!.getComponent(el);
      owner.error.set('');
      owner.message.set('Solicitud recibida en el entorno ficticio.');
    });
    await expect(page.locator('cs-operation-feedback [data-feedback-kind="success"]'))
      .toContainText('Solicitud recibida en el entorno ficticio.');
    await capture(page, testInfo, 'account-success-synthetic-' + size.name);
    await page.evaluate(() => {
      const el = document.querySelector('cs-identity-portal')!;
      const owner = (window as DebugWindow).ng!.getComponent(el);
      owner.message.set('');
      owner.feedbackScope.set('');
      owner.identity.clear();
      // Synthetic timer/revocation event; actual server enforcement is tested separately.
      owner.identity.sessionEndedNotice.set(true);
    });
    await expect(page.locator('cs-operation-feedback [data-feedback-kind="info"]'))
      .toContainText('Tu sesión dejó de estar disponible.');
    await capture(page, testInfo, 'account-session-cleared-synthetic-' + size.name);
  });
  test('F06 COMPLETE-65 | challenge and TOTP QR with synthetic enrollment only ' + size.name,
    async ({ page }, testInfo) => {
    await page.setViewportSize({ width: size.width, height: size.height });
    await setup(page, 'anonymous');
    await page.goto('/cuenta');
    await expect(page.getByRole('heading', { name: 'Iniciar sesión' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Continuar', exact: true })).toBeEnabled();
    await page.evaluate(() => {
      const el = document.querySelector('cs-identity-portal')!;
      const owner = (window as DebugWindow).ng?.getComponent(el);
      if (!owner) throw new Error('Angular account debug component unavailable');
      owner.identity.stateValue.set('challenge');
    });
    await expect(page.getByRole('heading', { name: 'Verificación en dos pasos', level: 1 })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Introduce tu código', level: 2 })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Verificación en dos pasos' })).toHaveCount(1);
    await capture(page, testInfo, 'account-mfa-challenge-synthetic-' + size.name);
    await page.evaluate(() => {
      const el = document.querySelector('cs-identity-portal')!;
      const owner = (window as DebugWindow).ng!.getComponent(el);
      owner.identity.enrollmentRequired.set(true);
    });
    await expect(page.getByRole('heading', { name: 'Configura tu autenticador', level: 2 })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Introduce tu código' })).toHaveCount(0);
    await expect(page.getByRole('button', { name: 'Configurar segundo factor' })).toBeVisible();
    await capture(page, testInfo, 'account-mfa-enrollment-required-synthetic-' + size.name);
    const { default: QRCode } = await import('qrcode');
    const qr = await QRCode.toDataURL(
      'otpauth://totp/CRECISOL:qa-f06%40example.test?secret=AAAAAAAAAAAAAAAA' +
      '&issuer=CRECISOL&algorithm=SHA1&digits=6&period=30'
    );
    await page.evaluate((qrData: string) => {
      const el = document.querySelector('cs-identity-portal')!;
      const owner = (window as DebugWindow).ng!.getComponent(el);
      owner.identity.enrollmentSecret.set('AAAAAAAAAAAAAAAA');
      owner.identity.enrollmentQr.set(qrData);
    }, qr);
    await expect(page.getByRole('img', { name: /Código QR privado/ })).toBeVisible();
    await capture(page, testInfo, 'account-mfa-enrollment-qr-SYNTHETIC-ONLY-' + size.name);
    await page.getByText('No puedo escanear: mostrar clave manual').click();
    await capture(page, testInfo, 'account-mfa-enrollment-manual-fake-seed-' + size.name);

    await page.goto('/cuenta?vista=operadores');
    await expect(page.getByRole('button', { name: 'Acceder a identidad de plataforma' })).toBeEnabled();
    await page.evaluate(() => {
      const el = document.querySelector('cs-platform-identity-panel')!;
      const owner = (window as DebugWindow).ng!.getComponent(el);
      owner.platform.state.set('challenge');
    });
    await expect(page.getByLabel('Código MFA del operador')).toBeVisible();
    await capture(page, testInfo, 'operator-mfa-challenge-synthetic-' + size.name);
  });

  test('F06 COMPLETE-65 | operator denial, error, empty and expired visual states ' + size.name,
    async ({ page }, testInfo) => {
    await page.setViewportSize({ width: size.width, height: size.height });
    await setup(page, 'operator');
    await page.goto('/cuenta?vista=operadores');
    const nav = page.getByRole('navigation', { name: 'Áreas de la consola de identidad' });
    await expect(nav).toBeVisible();
    await capture(page, testInfo, 'operator-cases-empty-synthetic-' + size.name);
    await page.evaluate(() => {
      const el = document.querySelector('cs-platform-identity-panel')!;
      (window as DebugWindow).ng!.getComponent(el).platform.restoring.set(true);
    });
    await expect(page.locator('cs-platform-identity-panel cs-operation-feedback [data-feedback-kind="pending"]'))
      .toContainText('Verificando tu sesión de operador');
    await capture(page, testInfo, 'operator-restoring-synthetic-' + size.name);
    await page.evaluate(() => {
      const el = document.querySelector('cs-platform-identity-panel')!;
      (window as DebugWindow).ng!.getComponent(el).platform.restoring.set(false);
    });
    await expect(page.locator('cs-platform-identity-panel cs-operation-feedback [data-feedback-kind="pending"]')).toHaveCount(0);
    await nav.getByRole('button', { name: 'Sesiones' }).click();
    await capture(page, testInfo, 'operator-sessions-empty-synthetic-' + size.name);
    await nav.getByRole('button', { name: 'Monitor' }).click();
    await capture(page, testInfo, 'operator-monitor-empty-synthetic-' + size.name);
    await nav.getByRole('button', { name: 'Seguridad' }).click();
    await expect(page.getByRole('button', { name: /Caducidad por inactividad/ })).toBeVisible();
    await page.evaluate(() => {
      const el = document.querySelector('cs-platform-identity-panel')!;
      (window as DebugWindow).ng!.getComponent(el).platform.recoveryCodes.set(['BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB']);
    });
    const operatorCode = page.locator('cs-platform-identity-panel .cs-one-time-codes__value');
    await expect(operatorCode).toHaveCount(1);
    await expect(operatorCode).toHaveText('B'.repeat(32));
    await expect(operatorCode).toHaveAttribute('tabindex', '0');
    await expect(operatorCode).toHaveCSS('white-space', 'nowrap');
    await expect.poll(() => page.evaluate(() =>
      document.documentElement.scrollWidth - document.documentElement.clientWidth)).toBeLessThanOrEqual(2);
    await capture(page, testInfo, 'operator-backup-codes-FAKE-ONLY-' + size.name);
    await page.evaluate(() => {
      const el = document.querySelector('cs-platform-identity-panel')!;
      (window as DebugWindow).ng!.getComponent(el).platform.recoveryCodes.set([]);
    });
    await expect(page.locator('cs-platform-identity-panel .cs-one-time-codes__value')).toHaveCount(0);
    await page.getByRole('button', { name: 'Reemplazar MFA de operador', exact: true }).click();
    const operatorDialog = page.getByRole('dialog', { name: 'Reemplazar MFA de operador' });
    await expect(operatorDialog).toBeVisible();
    const { default: operatorQr } = await import('qrcode');
    const operatorFakeQr = await operatorQr.toDataURL(
      'otpauth://totp/CRECISOL:operator-f06%40example.test?secret=BBBBBBBBBBBBBBBB' +
      '&issuer=CRECISOL&algorithm=SHA1&digits=6&period=30'
    );
    await page.evaluate((fakeQr: string) => {
      const el = document.querySelector('cs-platform-identity-panel')!;
      const owner = (window as DebugWindow).ng!.getComponent(el);
      owner.platform.factorSecret.set('BBBBBBBBBBBBBBBB');
      owner.platform.factorQr.set(fakeQr);
    }, operatorFakeQr);
    await expect(operatorDialog.getByRole('img', { name: /QR privado para vincular/ })).toBeVisible();
    await capture(page, testInfo, 'operator-factor-enrollment-qr-FAKE-ONLY-' + size.name);
    await captureDialogBottom(page, testInfo, 'operator-factor-enrollment-qr-FAKE-ONLY-' + size.name);
    await operatorDialog.getByText('No puedo escanear: mostrar clave manual').click();
    await capture(page, testInfo, 'operator-factor-enrollment-manual-FAKE-ONLY-' + size.name);
    await operatorDialog.getByRole('button', { name: 'Cerrar', exact: true }).click();
    await expect(operatorDialog).toHaveCount(0);
    await page.getByRole('button', { name: 'Caducidad por inactividad de sesiones' }).click();
    const policyDialog = page.getByRole('dialog', { name: 'Vencimiento de sesiones por inactividad' });
    await expect(policyDialog).toBeVisible();
    await page.evaluate(() => {
      const el = document.querySelector('cs-platform-identity-panel')!;
      const owner = (window as DebugWindow).ng!.getComponent(el);
      owner.platform.sessionPolicy.set({ idleMinutes: 30, revision: 1, canManage: false });
    });
    await expect(policyDialog.getByText(/Consulta habilitada.*administrador de seguridad/)).toBeVisible();
    await capture(page, testInfo, 'operator-policy-readonly-synthetic-' + size.name);
    await policyDialog.getByRole('button', { name: 'Cerrar', exact: true }).click();
    await page.evaluate(() => {
      const el = document.querySelector('cs-platform-identity-panel')!;
      const owner = (window as DebugWindow).ng!.getComponent(el);
      owner.feedbackScope.set('seguridad');
      owner.error.set('No se pudo completar la solicitud. Revisa los datos.');
    });
    await expect(page.locator('cs-platform-identity-panel cs-operation-feedback [data-feedback-kind="error"]'))
      .toContainText('No se pudo completar la solicitud.');
    await capture(page, testInfo, 'operator-error-synthetic-' + size.name);
    await page.evaluate(() => {
      const el = document.querySelector('cs-platform-identity-panel')!;
      const owner = (window as DebugWindow).ng!.getComponent(el);
      owner.error.set('');
      owner.feedbackScope.set('');
      owner.platform.clear();
      // Synthetic expiry callback only; server rejects the actual expired cookie in integration.
      owner.platform.sessionEndedNotice.set(true);
    });
    await expect(page.locator('cs-platform-identity-panel cs-operation-feedback [data-feedback-kind="info"]'))
      .toContainText('La sesión de operador terminó o fue revocada.');
    await capture(page, testInfo, 'operator-session-expired-simulated-' + size.name);
  });

}
