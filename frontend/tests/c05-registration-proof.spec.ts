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

  await page.goto('/cuenta');
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

  await page.goto('/cuenta');
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

  await page.goto('/cuenta');
  await page.getByLabel('Identificador de confirmación').fill(id!);
  await page.getByLabel('Código de confirmación').fill(proofCode!);
  await page.getByLabel('Correo de la solicitud').fill(mailbox);
  await page.getByLabel('Crear contraseña').fill(password);
  await page.getByRole('button', { name: 'Crear cuenta pendiente' }).click();
  await expect(page.getByRole('status').last()).toContainText(
    'pendiente de aprobación'
  );
});
