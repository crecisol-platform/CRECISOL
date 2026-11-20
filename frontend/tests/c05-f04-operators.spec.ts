/** CRECISOL C05 UX F04 — five isolated operator surfaces, synthetic visuals.
 * Backend authorization is independently tested by real Angular→.NET→MySQL E2E.
 * No real users, tenant credentials, IPs, or permission grants occur here. */
import { test, expect, type Page, type TestInfo } from '@playwright/test';
import AxeBuilder from '@axe-core/playwright';
import { writeFile } from 'node:fs/promises';

type DebugWindow = Window & { ng?: { getComponent: (node: Element) => any } };
type Section = 'casos' | 'sesiones' | 'perfil' | 'seguridad' | 'monitor';
const sections: readonly Section[] = ['casos', 'sesiones', 'perfil', 'seguridad', 'monitor'];
const sizes = [
  { width: 320, height: 720 }, { width: 390, height: 844 },
  { width: 768, height: 1024 }, { width: 1024, height: 768 },
  { width: 1440, height: 900 }
] as const;
const sampleSession = {
  id: '11111111-2222-4333-8444-555555555555', device: 'Chromium',
  lastSeenAtUtc: new Date(Date.now() - 2 * 60_000).toISOString(),
  expiresAtUtc: new Date(Date.now() + 28 * 60_000).toISOString()
};

async function fixture(page: Page, realm: 'operator' | 'business' | 'anonymous' = 'operator'): Promise<void> {
  // Explicit fail-closed default: unexpected routes never return private data.
  await page.route('**/api/v1/**', r => r.fulfill({
    status: 503, contentType: 'application/json', body: '{}'
  }));
  await page.route('**/api/v1/public/branding/platform', r => r.fulfill({
    status: 200, contentType: 'application/json',
    body: JSON.stringify({
      revision: 1, name: 'CRECISOL', slogan: 'Tu negocio, a tu manera.',
      descriptor: 'Comercio · Servicios · Membresías',
      primaryColor: '#0d468b', accentColor: '#1878c8',
      logoUrl: '/assets/crecisol-oficial.png', websiteUrl: null,
      publishedAt: '2026-11-19T00:00:00Z',
      presentation: { faviconUrl: '/assets/crecisol-emblem.png', typographyToken: 'system',
        iconSetToken: 'outline', seoTitle: 'CRECISOL', seoDescription: 'QA ficticio',
        copyrightNotice: 'Solo pruebas' }
    })
  }));
  const payload = () => JSON.stringify({
    csrfToken: 'a'.repeat(64), expiresAtUtc: new Date(Date.now() + 1_800_000).toISOString(), idleMinutes: 30
  });
  await page.route('**/api/v1/identity/browser/restore', r => r.fulfill({
    status: realm === 'business' ? 200 : 401,
    contentType: 'application/json', body: realm === 'business' ? payload() : '{}'
  }));
  await page.route('**/api/v1/platform-identity/restore', r => r.fulfill({
    status: realm === 'operator' ? 200 : 401,
    contentType: 'application/json', body: realm === 'operator' ? payload() : '{}'
  }));
  await page.route('**/api/v1/platform-identity/session-policy', r => r.fulfill({
    status: 200, contentType: 'application/json',
    body: JSON.stringify({ idleMinutes: 30, revision: 3, canManage: false })
  }));
}

async function visit(page: Page, section: Section): Promise<void> {
  await page.goto('/cuenta?vista=operadores');
  const nav = page.getByRole('navigation', { name: 'Áreas de la consola de identidad' });
  await expect(nav).toBeVisible();
  await page.evaluate(() => {
    const panel = document.querySelector('cs-platform-identity-panel');
    const owner = panel && (window as DebugWindow).ng?.getComponent(panel);
    if (!owner) throw new Error('F04 synthetic operator component unavailable');
    owner.platform.case.set({
      caseId: '11111111-2222-4333-8444-555555555555',
      readyAtUtc: new Date(Date.now() + 3_600_000).toISOString(), blocked: false, consumed: false
    });
    owner.platform.caseLoaded.set(true);
    owner.platform.sessions.set([{
      id: '11111111-2222-4333-8444-555555555555', device: 'Chromium',
      lastSeenAtUtc: new Date(Date.now() - 2 * 60_000).toISOString(),
      expiresAtUtc: new Date(Date.now() + 28 * 60_000).toISOString()
    }]);
    owner.platform.sessionsLoaded.set(true);
    owner.platform.profile.set({
      displayName: 'Operador ficticio institucional',
      language: 'es-HN', timeZone: 'America/Tegucigalpa'
    });
    owner.platform.profileLoaded.set(true);
    owner.platform.abuse.set([
      { kind: 'UNKNOWN_INTERNAL_PRIVATE_CODE', count: 12 },
      { kind: 'LOGIN_RATE_LIMIT', count: 4 },
      { kind: 'OTHER_INTERNAL_CODE', count: 1 }
    ]);
    owner.platform.abuseLoaded.set(true);
    owner.platform.sessionPolicy.set({ idleMinutes: 30, revision: 3, canManage: false });
  });
  if (section !== 'casos') await nav.getByRole('button', { name: sectionLabel(section), exact: true }).click();
  await expect(nav.getByRole('button', { name: sectionLabel(section), exact: true }))
    .toHaveAttribute('aria-current', 'page');
}
function sectionLabel(section: Section): string {
  return ({ casos: 'Casos', sesiones: 'Sesiones', perfil: 'Perfil',
    seguridad: 'Seguridad', monitor: 'Monitor' } as Record<Section, string>)[section];
}
async function noOverflow(page: Page): Promise<void> {
  const m = await page.evaluate(() => ({
    scroll: document.documentElement.scrollWidth, client: document.documentElement.clientWidth
  }));
  expect(m.scroll).toBeLessThanOrEqual(m.client + 2);
}

for (const size of sizes) {
  for (const section of sections) {
    test('F04 ' + section + ' at ' + size.width + 'px', async ({ page }, info: TestInfo) => {
      await page.setViewportSize(size);
      await fixture(page);
      await visit(page, section);
      const panel = page.locator('cs-platform-identity-panel');
      await expect(panel.locator('.operator-section-head h2')).toBeVisible();
      await noOverflow(page);
      await expect(page.getByRole('navigation', { name: 'Mi cuenta' })).toHaveCount(0);
      await expect(panel).not.toContainText(sampleSession.id);
      await expect(panel).not.toContainText('UNKNOWN_INTERNAL_PRIVATE_CODE');
      if (section === 'casos') {
        await expect(panel.getByRole('region', { name: 'Estado del expediente consultado' })).toContainText('En periodo de seguridad');
        await expect(panel).toContainText('Terminación 555555');
      }
      if (section === 'sesiones') {
        await expect(panel.getByRole('region', { name: 'Sesiones de operador' })).toContainText('Navegador Chromium');
        await expect(panel).toContainText('Terminación 555555');
      }
      if (section === 'perfil') {
        await expect(panel).toContainText('Operador ficticio institucional');
        await expect(panel).toContainText('Español (Honduras)');
        await expect(panel.getByRole('heading', { level: 3, name: 'Operador ficticio institucional' })).toBeVisible();
      }
      if (section === 'seguridad') await expect(panel).toContainText('30 minutos sin actividad');
      if (section === 'monitor') {
        await expect(panel).toContainText('Otras señales de protección');
        await expect(panel).toContainText('Atención alta');
      }
      const axe = await new AxeBuilder({ page }).include('cs-platform-identity-panel')
        .withTags(['wcag2a', 'wcag2aa', 'wcag21aa', 'wcag22aa']).analyze();
      expect(axe.violations).toEqual([]);
      const name = 'operator-' + section + '-' + size.width;
      const screenshot = info.outputPath(name + '.png');
      await page.screenshot({ path: screenshot, fullPage: true, animations: 'disabled' });
      const details = await page.evaluate(() => ({
        path: location.pathname, search: location.search,
        privateAccountNav: !!document.querySelector('nav[aria-label="Mi cuenta"]'),
        overflow: Math.max(0, document.documentElement.scrollWidth - document.documentElement.clientWidth),
        activeSection: document.querySelector('.operator-navigation [aria-current="page"]')?.textContent?.trim() ?? ''
      }));
      await writeFile(info.outputPath(name + '.json'), JSON.stringify({
        name, section, synthetic: true, runnerSha: process.env.GITHUB_SHA ?? 'local',
        viewport: page.viewportSize(), png: name + '.png', ...details
      }, null, 2));
    });
  }
}

test('F04 platform realm never borrows a business session or its administrative navigation', async ({ page }) => {
  await fixture(page, 'business');
  await page.goto('/cuenta?vista=operadores');
  await expect(page.getByRole('button', { name: 'Acceder a identidad de plataforma' })).toBeVisible();
  await expect(page.getByRole('navigation', { name: 'Áreas de la consola de identidad' })).toHaveCount(0);
  await expect(page.getByRole('navigation', { name: 'Mi cuenta' })).toHaveCount(0);
});
test('F04 guest cannot see global operator data', async ({ page }) => {
  await fixture(page, 'anonymous');
  await page.goto('/cuenta?vista=operadores');
  await expect(page.getByRole('navigation', { name: 'Áreas de la consola de identidad' })).toHaveCount(0);
  await expect(page.getByRole('region', { name: 'Estado del expediente consultado' })).toHaveCount(0);
});

test('F04 case review requires conscious decision and a distinct confirmation', async ({ page }) => {
  await fixture(page); await visit(page, 'casos');
  await page.getByRole('button', { name: 'Revisar identidad del titular', exact: true }).click();
  const dialog = page.getByRole('dialog', { name: 'Revisar identidad del titular' });
  const choice = dialog.getByLabel('Decisión de identidad');
  await expect(choice).toHaveValue('');
  expect(await choice.evaluate((el: HTMLSelectElement) => el.validity.valueMissing)).toBe(true);
  await expect(dialog.getByLabel('Confirmo la decisión administrativa y que mi revisión quedará registrada de forma permanente')).not.toBeChecked();
  await page.keyboard.press('Escape');
  await expect(dialog).not.toBeVisible();
});

test('F04 operator session revocation binds only the selected shortened reference', async ({ page }) => {
  await fixture(page); await visit(page, 'sesiones');
  const region = page.getByRole('region', { name: 'Sesiones de operador' });
  await region.getByRole('button', { name: 'Revocar sesión de operador Terminación 555555' }).click();
  const dialog = page.getByRole('dialog', { name: 'Revocar sesión de operador' });
  await expect(dialog).toContainText('Terminación 555555');
  await expect(dialog).not.toContainText(sampleSession.id);
  await page.keyboard.press('Escape');
  await expect(dialog).not.toBeVisible();
});

test('F04 operator read denial clears previously displayed devices without leaking tenant data', async ({ page }) => {
  await fixture(page); await visit(page, 'sesiones');
  await expect(page.getByRole('region', { name: 'Sesiones de operador' })).toContainText('Navegador Chromium');
  await page.route('**/api/v1/platform-identity/sessions', r => r.fulfill({
    status: 403, contentType: 'application/json', body: '{}'
  }));
  await page.getByRole('button', { name: 'Consultar sesiones de operador', exact: true }).click();
  await expect(page.getByRole('region', { name: 'Sesiones de operador' })).toContainText('Sesiones aún no consultadas');
  await expect(page.getByRole('region', { name: 'Sesiones de operador' })).not.toContainText('Navegador Chromium');
  await expect(page.getByRole('navigation', { name: 'Áreas de la consola de identidad' })).toBeVisible();
});

test('F04 monitor filters only aggregate bands and never prints unrecognized backend codes', async ({ page }) => {
  await fixture(page); await visit(page, 'monitor');
  const monitor = page.getByRole('region', { name: 'Monitor de seguridad de plataforma' });
  await monitor.getByLabel('Filtrar atención').selectOption('revision');
  await expect(monitor.locator('.operator-list > li')).toHaveCount(1);
  await expect(monitor).toContainText('Intentos de acceso limitados');
  await expect(monitor).not.toContainText('UNKNOWN_INTERNAL_PRIVATE_CODE');
  await monitor.getByLabel('Filtrar atención').selectOption('all');
  await expect(monitor.locator('.operator-list > li')).toHaveCount(2);
});


test('F04 200 percent text, forced colors and reduced motion preserve operator navigation', async ({ page }) => {
  await page.setViewportSize({ width: 320, height: 720 });
  await page.emulateMedia({ forcedColors: 'active', reducedMotion: 'reduce' });
  await fixture(page); await visit(page, 'seguridad');
  await page.addStyleTag({ content: ':root { font-size: 200% !important; }' });
  await noOverflow(page);
  const nav = page.getByRole('navigation', { name: 'Áreas de la consola de identidad' });
  await nav.getByRole('button', { name: 'Monitor', exact: true }).focus();
  await page.keyboard.press('Enter');
  await expect(nav.getByRole('button', { name: 'Monitor', exact: true })).toHaveAttribute('aria-current', 'page');
  await noOverflow(page);
});


test('F04 all operator task actions retain stable accessible names and connected descriptions', async ({ page }) => {
  await fixture(page);
  const taskMatrix = [
    { section: 'perfil' as const, labels: [
      ['Editar perfil de operador', 'operator-profile-edit-hint'],
      ['Actualizar foto de operador', 'operator-profile-photo-hint']
    ] },
    { section: 'seguridad' as const, labels: [
      ['Cambiar contraseña de operador', 'operator-password-hint'],
      ['Cambiar correo de operador', 'operator-mailbox-hint'],
      ['Reemplazar MFA de operador', 'operator-mfa-hint']
    ] },
    { section: 'sesiones' as const, labels: [
      ['Rotar sesión de operador', 'operator-rotate-hint'],
      ['Cerrar todas mis sesiones de plataforma', 'operator-revoke-all-hint']
    ] }
  ];
  for (const group of taskMatrix) {
    await visit(page, group.section);
    for (const [label, hint] of group.labels) {
      const control = page.getByRole('button', { name: label, exact: true });
      await expect(control).toBeVisible();
      await expect(control).toHaveAttribute('aria-describedby', hint);
      await expect(page.locator('#' + hint)).toBeVisible();
      expect((await page.locator('#' + hint).innerText()).trim().length).toBeGreaterThan(25);
    }
  }
});


test('F04 platform policy denial erases stale privileged settings', async ({ page }) => {
  await fixture(page); await visit(page, 'seguridad');
  const policy = page.getByRole('region', { name: 'Política vigente de inactividad' });
  await expect(policy).toContainText('30 minutos sin actividad');
  await page.route('**/api/v1/platform-identity/session-policy', route => route.fulfill({
    status: 403, contentType: 'application/json', body: '{}'
  }));
  const nav = page.getByRole('navigation', { name: 'Áreas de la consola de identidad' });
  // Selecting the already active tab is intentionally a no-op. Re-enter
  // Seguridad from another section to issue a real server policy refresh.
  await nav.getByRole('button', { name: 'Perfil', exact: true }).click();
  const denied = page.waitForResponse(response => response.url().endsWith('/platform-identity/session-policy') &&
    response.status() === 403);
  await nav.getByRole('button', { name: 'Seguridad', exact: true }).click();
  await denied;
  await expect(policy).not.toContainText('30 minutos sin actividad');
  await expect(policy).toContainText('Consulta Seguridad para recuperar el valor vigente de la API');
  await expect(nav.getByRole('button', { name: 'Seguridad', exact: true })).toHaveAttribute('aria-current', 'page');
});
