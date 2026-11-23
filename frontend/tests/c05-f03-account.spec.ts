/** CRECISOL C05 F03 — six real-account UI surfaces against local, synthetic API.
 * All identities, devices, dates and keys in this test are fabricated.
 * This cannot certify live multitenant authorization or a subjective 9/10 score.
 */
import { test, expect, type Page, type TestInfo } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import { writeFile } from 'node:fs/promises';

type SyntheticDebug = Window & {
  ng?: { getComponent: (element: Element) => any };
};
type AccountView = 'resumen' | 'perfil' | 'llaves' | 'seguridad' | 'historial' | 'administracion';
const views: readonly AccountView[] = [
  'resumen', 'perfil', 'llaves', 'seguridad', 'historial', 'administracion'
];
const sizes = [
  { width: 320, height: 720 }, { width: 390, height: 844 },
  { width: 768, height: 1024 }, { width: 1024, height: 768 },
  { width: 1440, height: 900 }
] as const;

async function syntheticAccount(page: Page, authenticated = true): Promise<void> {
  // Fail closed on unexpected API routes. No real identity action is performed.
  await page.route('**/api/v1/**', route => route.fulfill({
    status: 503, contentType: 'application/json', body: '{}'
  }));
  await page.route('**/api/v1/public/branding/platform', route => route.fulfill({
    status: 200, contentType: 'application/json',
    body: JSON.stringify({
      revision: 1, name: 'CRECISOL', slogan: 'Tu negocio, a tu manera.',
      descriptor: 'Comercio · Servicios · Membresías',
      primaryColor: '#0d468b', accentColor: '#1878c8',
      logoUrl: '/assets/crecisol-oficial.png', websiteUrl: null,
      publishedAt: '2026-11-19T00:00:00Z',
      presentation: {
        faviconUrl: '/assets/crecisol-emblem.png',
        typographyToken: 'system', iconSetToken: 'outline',
        seoTitle: 'CRECISOL', seoDescription: 'Datos sintéticos de QA',
        copyrightNotice: 'Solo pruebas aisladas'
      }
    })
  }));
  await page.route('**/api/v1/identity/browser/restore', route => route.fulfill({
    status: authenticated ? 200 : 401,
    contentType: 'application/json',
    body: authenticated ? JSON.stringify({
      csrfToken: 'a'.repeat(64),
      expiresAtUtc: new Date(Date.now() + 30 * 60_000).toISOString(),
      idleMinutes: 30
    }) : '{}'
  }));
  await page.route('**/api/v1/platform-identity/restore', route => route.fulfill({
    status: 401, contentType: 'application/json', body: '{}'
  }));
}

async function seed(page: Page): Promise<void> {
  await page.evaluate(() => {
    const root = document.querySelector('cs-identity-portal');
    const owner = root && (window as SyntheticDebug).ng?.getComponent(root);
    if (!owner) throw new Error('C05 synthetic Angular debug owner unavailable');
    owner.identity.profile.set({
      displayName: 'Perfil ficticio de prueba',
      language: 'es-HN', timeZone: 'America/Tegucigalpa'
    });
    owner.identity.sessions.set([{
      id: '11111111-2222-4333-8444-555555555555',
      device: 'Chromium de pruebas', factor: 'TOTP',
      // Match the real server's sliding 30-minute policy: a verified
      // synthetic session must not appear to have expired in screenshots.
      lastSeenAtUtc: new Date(Date.now() - 2 * 60_000).toISOString(),
      expiresAtUtc: new Date(Date.now() + 28 * 60_000).toISOString()
    }]);
    owner.identity.passkeys.set([{
      id: '33333333-4444-4555-8666-777777777777',
      createdAtUtc: '2026-11-15T12:00:00Z'
    }]);
    owner.identity.pendingMemberships.set([{
      id: '55555555-6666-4777-8888-999999999999',
      requestedAtUtc: '2026-11-19T11:00:00Z',
      mailboxHint: 'so•••@example.test'
    }]);
    owner.identity.pendingLoaded.set(true);
    owner.identity.pendingNextCursor.set(null);
    owner.identity.securityEvents.set([
      { action: 'profile/read', outcome: 1, atUtc: '2026-11-19T15:00:00Z' },
      { action: 'mfa/verify', outcome: 1, atUtc: '2026-11-18T15:00:00Z' },
      { action: 'login', outcome: 0, atUtc: '2026-11-17T15:00:00Z' },
      { action: 'opaque/internal/tenant-id', outcome: 9, atUtc: '2026-11-16T15:00:00Z' }
    ]);
  });
}

async function visit(page: Page, view: AccountView): Promise<void> {
  await page.goto('/cuenta?vista=' + view);
  const nav = page.getByRole('navigation', { name: 'Mi cuenta' });
  await expect(nav).toBeVisible();
  await seed(page);
  await expect(nav.getByRole('link', { name: view === 'administracion'
    ? 'Aprobaciones' : view === 'llaves'
      ? 'Llaves' : view === 'historial'
        ? 'Historial' : view === 'perfil'
          ? 'Mi perfil' : view === 'seguridad'
            ? 'Seguridad' : 'Resumen', exact: true
  })).toHaveAttribute('aria-current', 'page');
}

async function noHorizontalOverflow(page: Page): Promise<void> {
  const metrics = await page.evaluate(() => ({
    scroll: document.documentElement.scrollWidth,
    width: document.documentElement.clientWidth
  }));
  expect(metrics.scroll, 'account must not overflow page').toBeLessThanOrEqual(metrics.width + 2);
}

async function capture(page: Page, info: TestInfo, id: string, view: AccountView): Promise<void> {
  const snap = await page.evaluate(() => ({
    pathname: location.pathname, query: location.search,
    heading: document.querySelector('.identity-workspace > h2')?.textContent?.trim() ?? '',
    documentOverflow: Math.max(0, document.documentElement.scrollWidth - document.documentElement.clientWidth),
    activeTab: document.querySelector('.account-nav--workspace [aria-current="page"]')?.textContent?.trim() ?? ''
  }));
  const png = info.outputPath(id + '.png');
  await page.screenshot({ path: png, fullPage: true, animations: 'disabled' });
  await writeFile(info.outputPath(id + '.json'), JSON.stringify({
    id, view, synthetic: true, runnerSha: process.env.GITHUB_SHA ?? 'local',
    viewport: page.viewportSize(), png: id + '.png', ...snap
  }, null, 2));
}

for (const size of sizes) {
  for (const view of views) {
    test('F03 ACCOUNT ' + view + ' at ' + size.width + 'px', async ({ page }, info) => {
      await page.setViewportSize(size);
      await syntheticAccount(page);
      await visit(page, view);
      await expect(page.locator('.identity-workspace > h2')).toBeVisible();
      await expect(page.getByRole('navigation', { name: 'Ruta de navegación' }))
        .toContainText(view === 'resumen' ? 'Resumen' : view === 'perfil'
          ? 'Mi perfil' : view === 'llaves' ? 'Llaves de acceso'
            : view === 'seguridad' ? 'Seguridad' : view === 'historial'
              ? 'Historial' : 'Aprobaciones');
      await noHorizontalOverflow(page);
      if (view === 'resumen') {
        const device = page.getByRole('region', { name: 'Sesiones de cuenta' });
        await expect(device).toContainText('Chromium de pruebas');
        await expect(device).toContainText('Terminación 555555');
        await expect(page.getByText('30 minutos', { exact: true })).toBeVisible();
        await expect(device).not.toContainText('11111111-2222-4333-8444-555555555555');
      }
      if (view === 'perfil') {
        await expect(page.getByRole('region', { name: 'Perfil de mi empresa' }))
          .toContainText('Perfil ficticio de prueba');
        await expect(page.getByRole('button', { name: 'Eliminar foto de perfil' })).toHaveCount(0);
        await expect(page.getByRole('button', { name: 'Añadir foto de perfil' })).toBeVisible();
        await expect(page.getByText('Español (Honduras)', { exact: true })).toBeVisible();
      }
      if (view === 'llaves') {
        const keyPanel = page.getByRole('region', { name: 'Llaves de acceso', exact: true });
        await expect(keyPanel).toContainText('Terminación 777777');
        await expect(keyPanel).not.toContainText('33333333-4444-4555-8666-777777777777');
      }
      if (view === 'historial') {
        const timeline = page.getByRole('region', { name: 'Historial de seguridad', exact: true });
        await expect(timeline).toContainText('Consulta de perfil');
        await expect(timeline).toContainText('Verificación de segundo factor');
        await expect(timeline).not.toContainText('profile/read');
        await expect(timeline).not.toContainText('opaque/internal/tenant-id');
      }
      if (view === 'administracion') {
        const queue = page.getByRole('region', { name: 'Solicitudes de membresía pendientes' });
        await expect(queue).toContainText('so•••@example.test');
        await expect(queue).not.toContainText('55555555-6666-4777-8888-999999999999');
        await expect(page.getByText('Solo el servidor puede autorizar la consulta y las decisiones.', { exact: false })).toBeVisible();
      }
      await capture(page, info, 'account-' + view + '-' + size.width, view);
    });
  }
}

test('F03 ACCOUNT negative: untrusted URL cannot expose six account views', async ({ page }) => {
  await syntheticAccount(page, false);
  await page.goto('/cuenta?vista=historial');
  await expect(page.getByRole('navigation', { name: 'Mi cuenta' })).toHaveCount(0);
  await expect(page.getByRole('navigation', { name: 'Opciones de identidad' })).toBeVisible();
});

test('F03 ACCOUNT passkeys: confirmation chooses listed key not typed UUID', async ({ page }) => {
  await syntheticAccount(page); await visit(page, 'llaves');
  await page.getByRole('button', { name: 'Revocar esta llave', exact: true }).click();
  const dialog = page.getByRole('dialog', { name: 'Revocar llave de acceso' });
  await expect(dialog).toBeVisible();
  await expect(dialog.locator('input[name="factorId"]')).toHaveCount(0);
  const select = dialog.getByLabel('Llave de acceso a revocar');
  await expect(select).toHaveValue('33333333-4444-4555-8666-777777777777');
  await expect(select).toHaveValue('33333333-4444-4555-8666-777777777777');
  await page.keyboard.press('Escape');
  await expect(dialog).not.toBeVisible();
});

test('F03 ACCOUNT approvals: blank decision, reason and MFA required, never automatic', async ({ page }) => {
  await syntheticAccount(page); await visit(page, 'administracion');
  let decisions = 0;
  page.on('request', request => {
    if (request.url().includes('/memberships/decide')) decisions++;
  });
  await page.getByRole('button', { name: 'Revisar solicitud', exact: true }).click();
  const dialog = page.getByRole('dialog', { name: 'Revisar membresía con permiso administrativo' });
  const selected = dialog.locator('select[name="membershipId"]');
  await expect(selected).toHaveValue('55555555-6666-4777-8888-999999999999');
  const decision = dialog.getByLabel('Decisión', { exact: true });
  await expect(decision).toHaveValue('');
  await expect(dialog.locator('input[name="membershipId"]')).toHaveCount(0);
  await expect(dialog.locator('input[name="reason"]')).toHaveAttribute('minlength', '8');
  await expect(dialog.locator('input[name="code"]')).toHaveAttribute('required', '');
  await expect(dialog.locator('input[name="confirmed"]')).toHaveAttribute('required', '');
  await dialog.getByRole('button', { name: 'Registrar decisión' }).click();
  expect(decisions).toBe(0);
});

test('F03 ACCOUNT history: readable chronology, no unknown backend codes', async ({ page }) => {
  await syntheticAccount(page); await visit(page, 'historial');
  const timeline = page.getByRole('region', { name: 'Historial de seguridad', exact: true });
  await expect(timeline.locator('.identity-activity-item')).toHaveCount(4);
  await expect(timeline).toContainText('Otra actividad de seguridad');
  await expect(timeline).not.toContainText('opaque/internal/tenant-id');
  await expect(timeline).not.toContainText('resultado 1');
});

test('F03 ACCOUNT profile: stored defaults appear but cancel does not save', async ({ page }) => {
  await syntheticAccount(page); await visit(page, 'perfil');
  const open = page.getByRole('button', { name: 'Editar perfil de mi empresa' });
  await open.click();
  const dialog = page.getByRole('dialog', { name: 'Editar perfil de mi empresa' });
  await expect(dialog.getByLabel('Nombre visible en esta empresa')).toHaveValue('Perfil ficticio de prueba');
  await dialog.getByLabel('Nombre visible en esta empresa').fill('Descartado por QA');
  await page.keyboard.press('Escape');
  await open.click();
  await expect(dialog.getByLabel('Nombre visible en esta empresa')).toHaveValue('Perfil ficticio de prueba');
});

for (const view of views) {
  test('F03 ACCOUNT WCAG AA ' + view, async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await syntheticAccount(page); await visit(page, view);
    const result = await new AxeBuilder({ page }).include('#account-content')
      .withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa']).analyze();
    expect(result.violations).toEqual([]);
  });
}

test('F03 ACCOUNT zoom 200%, forced-colors, reduced-motion and six-tab mobile scroll', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 720 });
  await page.emulateMedia({ forcedColors: 'active', reducedMotion: 'reduce' });
  await syntheticAccount(page); await visit(page, 'historial');
  await page.addStyleTag({ content: ':root { font-size: 200% !important; }' });
  await noHorizontalOverflow(page);
  await expect(page.getByRole('navigation', { name: 'Mi cuenta' })).toBeVisible();
  await expect(page.getByRole('region', { name: 'Historial de seguridad', exact: true })).toBeVisible();
});

test('F03 ACCOUNT modal: a previous operation receipt cannot appear in a different dialog',
  async ({ page }) => {
    await syntheticAccount(page); await visit(page, 'seguridad');
    await page.evaluate(() => {
      const owner = (window as SyntheticDebug).ng!.getComponent(document.querySelector('cs-identity-portal')!);
      owner.message.set('Mensaje de la acción anterior: no pertenece a este diálogo');
    });
    await page.getByRole('button', { name: 'Cambiar contraseña', exact: true }).click();
    const dialog = page.getByRole('dialog', { name: 'Cambiar contraseña', exact: true });
    await expect(dialog).toBeVisible();
    await expect(dialog).not.toContainText('Mensaje de la acción anterior: no pertenece a este diálogo');
    await page.keyboard.press('Escape');
  });

test('F03 ACCOUNT long secure modal: heading, footer, focus and scroll contained on 320px',
  async ({ page }) => {
    await page.setViewportSize({ width: 320, height: 720 });
    await syntheticAccount(page); await visit(page, 'seguridad');
    const opener = page.getByRole('button', { name: 'Recuperar MFA con una llave de acceso' });
    await opener.click();
    const dialog = page.getByRole('dialog', { name: 'Recuperación del segundo factor con llave' });
    await expect(dialog).toBeVisible();
    await expect(dialog.getByRole('heading', { name: 'Recuperación del segundo factor con llave' }))
      .toBeFocused();
    await expect(page.locator('body')).toHaveCSS('overflow', 'hidden');
    const fit = await dialog.evaluate(element => {
      const rect = element.getBoundingClientRect();
      const header = element.querySelector('.cs-modal__header')!.getBoundingClientRect();
      const body = element.querySelector('.cs-modal__content')!.getBoundingClientRect();
      const footer = element.querySelector('.cs-modal__footer')!.getBoundingClientRect();
      return rect.left >= -1 && rect.right <= innerWidth + 1 &&
        rect.top >= -1 && rect.bottom <= innerHeight + 1 &&
        body.top >= header.bottom - 1 && body.bottom <= footer.top + 1;
    });
    expect(fit).toBe(true);
    await page.keyboard.press('Escape');
    await expect(dialog).not.toBeVisible();
    await expect(opener).toBeFocused();
    await expect(page.locator('body')).not.toHaveCSS('overflow', 'hidden');
  });

test('F03 ACCOUNT desktop: navigation is a true vertical sidebar at 1440px',
  async ({ page }) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await syntheticAccount(page); await visit(page, 'perfil');
    const layout = await page.evaluate(() => {
      const nav = document.querySelector<HTMLElement>('.account-nav--workspace')!;
      const body = document.querySelector<HTMLElement>('.account-workspace-body')!;
      const rail = document.querySelector<HTMLElement>('.account-workspace-rail')!;
      const first = nav.querySelector('a')!;
      const next = first.nextElementSibling!;
      return {
        railRight: rail.getBoundingClientRect().right,
        bodyLeft: body.getBoundingClientRect().left,
        firstY: first.getBoundingClientRect().y,
        nextY: next.getBoundingClientRect().y,
        navDirection: getComputedStyle(nav).flexDirection
      };
    });
    expect(layout.navDirection).toBe('column');
    expect(layout.railRight).toBeLessThan(layout.bodyLeft);
    expect(layout.nextY).toBeGreaterThan(layout.firstY);
    await noHorizontalOverflow(page);
  });

test('F03 ACCOUNT mobile: six tabs stay keyboard-reachable in an inner horizontal rail',
  async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await syntheticAccount(page); await visit(page, 'historial');
    const nav = page.getByRole('navigation', { name: 'Mi cuenta' });
    await expect(nav.getByRole('link')).toHaveCount(6);
    const before = await nav.evaluate(element => ({
      width: element.clientWidth,
      scroll: element.scrollWidth,
      orientation: getComputedStyle(element).flexDirection
    }));
    expect(before.orientation).toBe('row');
    expect(before.scroll).toBeGreaterThan(before.width);
    const last = nav.getByRole('link', { name: 'Aprobaciones' });
    await last.focus();
    await expect(last).toBeFocused();
    await page.keyboard.press('Enter');
    await expect(page).toHaveURL(/vista=administracion/);
    await noHorizontalOverflow(page);
  });

test('F03 approval inbox: authorized server inventory replaces hand-typed UUID', async ({ page }) => {
  await syntheticAccount(page); await visit(page, 'administracion');
  await page.evaluate(() => {
    const owner = (window as SyntheticDebug).ng!.getComponent(document.querySelector('cs-identity-portal')!);
    owner.identity.pendingMemberships.set([]);
    owner.identity.pendingLoaded.set(false);
  });
  let fetched = 0;
  await page.route('**/api/v1/identity/memberships/pending', async route => {
    fetched++;
    const request = route.request().postDataJSON() as { cursor: string };
    expect(request.cursor).toBe('start');
    await route.fulfill({
      status: 200, contentType: 'application/json',
      body: JSON.stringify({ requests: [{
        id: '55555555-6666-4777-8888-999999999999',
        requestedAtUtc: '2026-11-19T11:00:00Z', mailboxHint: 'so•••@example.test'
      }], nextCursor: null })
    });
  });
  const queue = page.getByRole('region', { name: 'Solicitudes de membresía pendientes' });
  await expect(queue).toContainText('Bandeja de aprobación protegida');
  await queue.getByRole('button', { name: 'Consultar solicitudes' }).click();
  await expect(queue).toContainText('so•••@example.test');
  await expect(queue).not.toContainText('55555555-6666-4777-8888-999999999999');
  expect(fetched).toBe(1);
});

test('F03 approval inbox: unauthorized response reveals no pending applicant and retains session',
  async ({ page }) => {
    await syntheticAccount(page); await visit(page, 'administracion');
    await page.evaluate(() => {
      const owner = (window as SyntheticDebug).ng!.getComponent(document.querySelector('cs-identity-portal')!);
      owner.identity.pendingMemberships.set([]);
      owner.identity.pendingLoaded.set(false);
    });
    await page.route('**/api/v1/identity/memberships/pending', route => route.fulfill({
      status: 401, contentType: 'application/json', body: '{}'
    }));
    await page.getByRole('button', { name: 'Consultar solicitudes' }).click();
    await expect(page.getByRole('navigation', { name: 'Mi cuenta' })).toBeVisible();
    await expect(page.getByRole('region', { name: 'Solicitudes de membresía pendientes' }))
      .toContainText('Consulta administrativa no autorizada');
    await expect(page.getByRole('region', { name: 'Solicitudes de membresía pendientes' }))
      .not.toContainText('so•••@example.test');
    await expect(page.getByRole('region', { name: 'Solicitudes de membresía pendientes' })
      .getByRole('button', { name: 'Revisar solicitud' })).toHaveCount(0);
  });

test('F03 account history filtering: completed vs review, no technical codes', async ({ page }) => {
  await syntheticAccount(page); await visit(page, 'historial');
  const filter = page.getByLabel('Mostrar eventos');
  const timeline = page.getByRole('region', { name: 'Historial de seguridad', exact: true });
  await expect(timeline.locator('.identity-activity-item')).toHaveCount(4);
  await filter.selectOption('success');
  await expect(timeline.locator('.identity-activity-item')).toHaveCount(2);
  await expect(timeline).not.toContainText('profile/read');
  await filter.selectOption('attention');
  await expect(timeline.locator('.identity-activity-item')).toHaveCount(2);
  await filter.selectOption('all');
  await expect(timeline.locator('.identity-activity-item')).toHaveCount(4);
});

test('F03 approvals privacy: authorization revocation scrubs already-loaded applicant hints',
  async ({ page }) => {
    await syntheticAccount(page); await visit(page, 'administracion');
    const queue = page.getByRole('region', { name: 'Solicitudes de membresía pendientes' });
    await expect(queue).toContainText('so•••@example.test');
    await page.route('**/api/v1/identity/memberships/pending', route => route.fulfill({
      status: 401, contentType: 'application/json', body: '{}'
    }));
    await queue.getByRole('button', { name: 'Actualizar solicitudes' }).click();
    await expect(queue).toContainText('Consulta administrativa no autorizada');
    await expect(queue).not.toContainText('so•••@example.test');
    await expect(queue.locator('.identity-pending-card')).toHaveCount(0);
    await expect(page.getByRole('navigation', { name: 'Mi cuenta' })).toBeVisible();
  });


test('F03 approvals privacy: stale allowed response cannot restore data after a newer permission denial',
  async ({ page }) => {
    await syntheticAccount(page); await visit(page, 'administracion');
    await page.evaluate(() => {
      const owner = (window as SyntheticDebug).ng!.getComponent(document.querySelector('cs-identity-portal')!);
      owner.identity.pendingMemberships.set([]);
      owner.identity.pendingLoaded.set(false);
    });
    let releaseFirst!: () => void;
    let arrivedFirst!: () => void;
    const firstHeld = new Promise<void>(resolve => { releaseFirst = resolve; });
    const firstArrived = new Promise<void>(resolve => { arrivedFirst = resolve; });
    let sequence = 0;
    await page.route('**/api/v1/identity/memberships/pending', async route => {
      sequence++;
      if (sequence === 1) {
        arrivedFirst();
        await firstHeld;
        await route.fulfill({ status: 200, contentType: 'application/json',
          body: JSON.stringify({ requests: [{
            id: '55555555-6666-4777-8888-999999999999',
            requestedAtUtc: '2026-11-19T11:00:00Z', mailboxHint: 'so•••@example.test'
          }], nextCursor: null }) });
      } else {
        await route.fulfill({ status: 401, contentType: 'application/json', body: '{}' });
      }
    });
    // These two requests are intentionally concurrent; UI buttons normally
    // serialize clicks, but background calls and permission changes may race.
    await page.evaluate(() => {
      const owner = (window as SyntheticDebug).ng!.getComponent(document.querySelector('cs-identity-portal')!);
      void owner.identity.loadPendingMemberships().catch(() => undefined);
    });
    await firstArrived;
    await page.evaluate(() => {
      const owner = (window as SyntheticDebug).ng!.getComponent(document.querySelector('cs-identity-portal')!);
      void owner.identity.loadPendingMemberships().catch(() => undefined);
    });
    const queue = page.getByRole('region', { name: 'Solicitudes de membresía pendientes' });
    await expect(queue).toContainText('Consulta administrativa no autorizada');
    const lateSuccess = page.waitForResponse(r =>
      r.url().endsWith('/memberships/pending') && r.status() === 200);
    releaseFirst();
    await lateSuccess;
    // Wait until the 200 response has reached Angular before asserting that
    // an older authorized result cannot repopulate applicant data.
    await page.waitForTimeout(100);
    await expect(queue).toContainText('Consulta administrativa no autorizada');
    await expect(queue).not.toContainText('so•••@example.test');
    await expect(queue.locator('.identity-pending-card')).toHaveCount(0);
    expect(sequence).toBe(2);
  });


test('F03 mobile deep links keep the active account tab visible without page overflow',
  async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await syntheticAccount(page);
    for (const view of ['seguridad', 'historial', 'administracion'] as const) {
      await visit(page, view);
      const rail = page.getByRole('navigation', { name: 'Mi cuenta' });
      await expect.poll(() => rail.evaluate(element => {
        const active = element.querySelector<HTMLElement>('a[aria-current="page"]');
        if (!active) return false;
        const outer = element.getBoundingClientRect();
        const inner = active.getBoundingClientRect();
        return inner.left >= outer.left - 2 && inner.right <= outer.right + 2;
      })).toBe(true);
      if (view === 'administracion')
        expect(await rail.evaluate(element => element.scrollLeft)).toBeGreaterThan(0);
      await noHorizontalOverflow(page);
    }
  });

/* F03 premium gate: the 9.5 buyer review must not be purchased by hiding
 * navigation, reducing touch targets or dropping protected account actions. */
for (const width of [320, 768] as const) {
  for (const view of views) {
    test('F03 PREMIUM six-view geometry ' + view + ' at ' + width + 'px',
      async ({ page }) => {
        await page.setViewportSize({ width, height: 900 });
        await syntheticAccount(page); await visit(page, view);
        const workspace = page.locator('cs-identity-workspace .identity-workspace');
        await expect(workspace).toHaveAttribute('data-view', view);
        await expect(workspace.locator('h2').first()).toBeVisible();
        const nav = page.getByRole('navigation', { name: 'Mi cuenta' });
        await expect(nav.getByRole('link')).toHaveCount(6);
        const targetSizes = await nav.getByRole('link').evaluateAll(elements =>
          elements.map(element => element.getBoundingClientRect().height));
        expect(Math.min(...targetSizes), 'six account links require 44px minimum targets')
          .toBeGreaterThanOrEqual(43.5);
        await noHorizontalOverflow(page);
        if (width === 320) {
          await expect(page.locator('#account-nav-mobile-help')).toBeVisible();
          await expect(page.locator('#account-nav-mobile-help')).toContainText('Seis áreas');
          await expect(nav).toHaveCSS('overflow-x', 'auto');
        }
        await expect(page.getByRole('button', { name: 'Cerrar sesión', exact: true }))
          .toBeVisible();
        if (view === 'administracion') {
          const queue = page.getByRole('region', { name: 'Solicitudes de membresía pendientes' });
          await expect(queue).toContainText('so•••@example.test');
          await expect(queue).not.toContainText('55555555-6666-4777-8888-999999999999');
        }
        if (view === 'llaves') {
          await expect(page.getByRole('button', { name: 'Revocar esta llave', exact: true }))
            .toBeVisible();
        }
        if (view === 'seguridad') {
          await expect(page.getByRole('button', { name: 'Cambiar contraseña', exact: true }))
            .toBeVisible();
          await expect(page.getByRole('button', { name: 'Reemplazar segundo factor' }))
            .toBeVisible();
        }
      });
  }
}

test('F03 PREMIUM membership review preserves MFA, blank decision and mandatory confirmation',
  async ({ page }) => {
    await page.setViewportSize({ width: 320, height: 720 });
    await syntheticAccount(page); await visit(page, 'administracion');
    await page.getByRole('button', { name: 'Revisar solicitud', exact: true }).click();
    const dialog = page.getByRole('dialog', { name: 'Revisar membresía con permiso administrativo' });
    await expect(dialog).toBeVisible();
    await expect(dialog.locator('select[name="decision"]')).toHaveValue('');
    for (const field of ['membershipId', 'decision', 'reason', 'code']) {
      await expect(dialog.locator('[name="' + field + '"]')).toHaveAttribute('required', '');
    }
    await expect(dialog.locator('input[name="confirmed"]')).toHaveAttribute('required', '');
    await expect(dialog.locator('input[name="confirmed"]')).not.toBeChecked();
    await expect(dialog.getByRole('note', { name: 'Advertencia de seguridad' }))
      .toContainText('La decisión es permanente');
    await page.keyboard.press('Escape');
    await expect(dialog).not.toBeVisible();
});
