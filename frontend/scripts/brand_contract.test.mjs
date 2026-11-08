import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';

const source = JSON.parse(readFileSync(
  new URL('../../backend/src/Crecisol.Api/Configuration/platform-brand.published.json', import.meta.url), 'utf8'
));
test('C01 platform seed is published and scoped', () => {
  assert.equal(source.scope, 'Platform');
  assert.equal(source.scopeId, '00000000-0000-0000-0000-000000000000');
  assert.equal(source.status, 'Published');
  assert.ok(Number.isSafeInteger(source.revision) && source.revision > 0);
});
test('C01 identity contains safe styling and the approved name', () => {
  assert.equal(source.name, 'CRECISOL');
  assert.equal(source.slogan, 'Tu negocio, a tu manera.');
  assert.match(source.primaryColor, /^#[0-9a-fA-F]{6}$/);
  assert.match(source.accentColor, /^#[0-9a-fA-F]{6}$/);
  assert.match(source.logoUrl, /^\/assets\/[a-zA-Z0-9-]+\.png$/);
});
test('No unverified domain or legal claims', () => {
  assert.equal(source.websiteUrl, null);
});
