import test from 'node:test';
import assert from 'node:assert/strict';
import { assertSafeMigrationLedger, assertC05ContainerIdentity } from './c05_sha1_persistent_guards.mjs';

const digest = 'a'.repeat(64);
const complete = () => new Map(Array.from({ length: 18 }, (_, i) => [i + 1, digest]));
const guard = (tables, entries) => () => assertSafeMigrationLedger(tables, entries, 18);

test('a pristine schema with no migration ledger can initialize SHA1', () => {
  assert.equal(guard(0, new Map())(), 'fresh');
});
test('a complete and contiguous C05-018 ledger can restart', () => {
  assert.equal(guard(35, complete())(), 'complete');
});
for (const [name, tables, entries] of [
  ['untracked existing tables', 3, new Map()],
  ['an empty legacy ledger', 1, new Map()],
  ['legacy 001..016', 25, new Map([...complete()].slice(0, 16))],
  ['legacy 001..017', 35, new Map([...complete()].slice(0, 17))],
  ['partial 001..015', 25, new Map([...complete()].slice(0, 15))],
  ['interrupted 017 without a ledger commit', 35, new Map([...complete()].slice(0, 16))],
  ['interrupted 018 without a ledger commit', 39, new Map([...complete()].slice(0, 17))],
  ['full-sized ledger with a missing version', 39, new Map([...complete()].filter(([n]) => n !== 16).concat([[19, digest]]))],
  ['unrecognized extra version', 39, new Map([...complete(), [19, digest]])],
  ['tampered checksum', 39, new Map([...complete()].map(([n, h]) => [n, n === 18 ? 'BAD' : h]))],
  ['inconsistent metadata', 0, complete()]
]) {
  test('fail closed: ' + name, () => assert.throws(guard(tables, entries), /refusing/i));
}
test('invalid ledger metadata always fails closed', () => {
  assert.throws(() => assertSafeMigrationLedger(-1, new Map(), 18), /refusing/i);
  assert.throws(() => assertSafeMigrationLedger(NaN, new Map(), 18), /refusing/i);
});
const volume = 'crecisol-c05-mysql-data';
const keyFile = '/tmp/c05-private/mysql-root.password';
const image = 'mysql:8.4@sha256:' + 'a'.repeat(64);
const mounts = () => [
  { Type: 'volume', Name: volume, Destination: '/var/lib/mysql' },
  { Type: 'bind', Source: keyFile, Destination: '/run/secrets/mysql-root-password', RW: false }
];
const ports = () => ({ '3306/tcp': [{ HostIp: '127.0.0.1', HostPort: '3310' }] });
const valid = (m = mounts(), p = ports(), i = image) =>
  assertC05ContainerIdentity(m, p, i, volume, keyFile, image);
test('valid isolated Docker instance can be used', () => {
  assert.doesNotThrow(() => valid());
});
test('reject C01/C02 or unexpected mounted data', () => {
  const m = mounts(); m[0].Name = 'crecisol-local-mysql-data-20261113';
  assert.throws(() => valid(m), /refusing/i);
});
test('reject writable or different root password bind', () => {
  const m = mounts(); m[1].RW = true;
  assert.throws(() => valid(m), /refusing/i);
  m[1].RW = false; m[1].Source = '/elsewhere/key';
  assert.throws(() => valid(m), /refusing/i);
});
test('reject non-loopback MySQL exposure', () => {
  const p = ports(); p['3306/tcp'][0].HostIp = '0.0.0.0';
  assert.throws(() => valid(mounts(), p), /refusing/i);
});
test('reject wrong port, image, or missing inspect fields', () => {
  const p = ports(); p['3306/tcp'][0].HostPort = '3306';
  assert.throws(() => valid(mounts(), p), /refusing/i);
  assert.throws(() => valid(mounts(), ports(), 'mysql:latest'), /refusing/i);
  assert.throws(() => valid(null, null), /refusing/i);
});
