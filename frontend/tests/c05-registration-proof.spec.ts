import { expect, test } from '@playwright/test';

const proofId = '11111111-1111-4111-8111-111111111111';
const code = 'a'.repeat(64);
const endpoint = '/api/v1/identity/registration-completions';
const mailbox = 'angular-e2e@example.test';
const password = 'Crecisol-local-test-strong-1!';

test('account portal submits only bounded mailbox proof data and keeps a neutral receipt', async ({ page }) => {
  const requests: Array<{ body: unknown; authorization?: string; tenant?: string }> = [];
  await page.route(`**${endpoint}`, async route => {
    requests.push({
      body: route.request().postDataJSON(),
      authorization: route.request().headers()['authorization'],
      tenant: route.request().headers()['x-tenant-id']
    });
    await route.fulfill({
      status: 202,
      contentType: 'application/json',
      headers: { 'cache-control': 'no-store' },
      body: JSON.stringify({ accepted: true })
    });
  });

  await page.goto('/cuenta?vista=confirmar');
  await page.getByLabel('Identificador de confirmación').fill(proofId);
  await page.getByLabel('Código de confirmación').fill(code);
  await page.getByLabel('Correo de la solicitud').fill(mailbox);
  await page.getByLabel('Crear contraseña').fill(password);
  await page.getByRole('button', { name: 'Crear cuenta pendiente' }).click();

  await expect.poll(() => requests.length).toBe(1);
  expect(requests[0]).toEqual({ body: { proofId, code, mailbox, password }, authorization: undefined, tenant: undefined });
  await expect(page.getByRole('status').last()).toContainText(
    'pendiente de aprobación'
  );
});

test('account portal rejects malformed mailbox proof locally', async ({ page }) => {
  let submitted = false;
  await page.route(`**${endpoint}`, async route => {
    submitted = true;
    await route.fulfill({ status: 202, contentType: 'application/json', body: '{"accepted":true}' });
  });

  await page.goto('/cuenta?vista=confirmar');
  await page.getByLabel('Identificador de confirmación').fill('not-a-guid');
  await page.getByLabel('Código de confirmación').fill('short');
  await page.getByLabel('Correo de la solicitud').fill(mailbox);
  await page.getByLabel('Crear contraseña').fill(password);
  await page.getByRole('button', { name: 'Crear cuenta pendiente' }).click();
  await expect.poll(() => submitted).toBe(false);
});

test('Angular proxy completes a real registration against the disposable API and MySQL', async ({ page }) => {
  test.skip(
    !process.env.C05_PROOF_ID || !process.env.C05_PROOF_CODE,
    'Requires the isolated MySQL/API harness to inject a proof from its encrypted local outbox.'
  );
  const id = process.env.C05_PROOF_ID;
  const proofCode = process.env.C05_PROOF_CODE;
  expect(id).toMatch(/^[0-9a-f-]{36}$/i);
  expect(proofCode).toMatch(/^[0-9a-f]{64}$/i);

  await page.goto('/cuenta?vista=confirmar');
  await page.getByLabel('Identificador de confirmación').fill(id!);
  await page.getByLabel('Código de confirmación').fill(proofCode!);
  await page.getByLabel('Correo de la solicitud').fill(mailbox);
  await page.getByLabel('Crear contraseña').fill(password);
  await page.getByRole('button', { name: 'Crear cuenta pendiente' }).click();
  await expect(page.getByRole('status').last()).toContainText(
    'pendiente de aprobación'
  );
});

test('identity flows expose individual navigable views', async ({ page }) => {
  await page.goto('/cuenta');
  await expect(page.getByRole('heading', { name: 'Iniciar sesión', exact: true })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Confirmar correo', exact: true })).toHaveCount(0);
  await page.getByRole('link', { name: 'Solicitar cuenta', exact: true }).click();
  await expect(page).toHaveURL(/vista=registro/);
  await expect(page.getByRole('heading', { name: 'Solicitar una cuenta', exact: true })).toBeVisible();
  await expect(page.getByRole('heading', { name: 'Iniciar sesión', exact: true })).toHaveCount(0);
  // F02/F07: secondary identity routes are intentionally collapsed by
  // default; navigating between account tasks closes the disclosure again.
  // Exercise the accessible user flow instead of clicking a hidden link.
  const secondary = page.locator('details.account-more');
  const summary = secondary.locator('summary');
  const confirm = secondary.getByRole('link', { name: 'Confirmar correo', exact: true });
  const recover = secondary.getByRole('link', { name: 'Recuperar acceso', exact: true });
  await expect(summary).toBeVisible();
  await expect(confirm).not.toBeVisible();
  await summary.click();
  await expect(confirm).toBeVisible();
  await confirm.click();
  await expect(page).toHaveURL(/vista=confirmar/);
  await expect(page.getByLabel('Identificador de confirmación')).toBeVisible();
  await expect(recover).not.toBeVisible();
  await summary.click();
  await expect(recover).toBeVisible();
  await recover.click();
  await expect(page).toHaveURL(/vista=recuperar/);
  await expect(page.getByLabel('Correo para recuperación')).toBeVisible();
  await expect(page.getByLabel('Identificador de confirmación')).toHaveCount(0);
  await page.getByRole('link', { name: 'Acceso de personal autorizado' }).click();
  await expect(page).toHaveURL(/vista=operadores/);
  await expect(page.getByLabel('Correo del operador de identidad')).toBeVisible();
  await expect(page.getByLabel('Correo para recuperación')).toHaveCount(0);
});
