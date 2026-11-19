import { expect, test } from '@playwright/test';
import {
  identityDateLabel, identityEventLabel, identityOutcomeLabel, identityReferenceLabel
} from '../src/app/identity/identity-presentation';

test('C05 privacy | unknown internal events stay human-readable and do not leak raw codes', () => {
  expect(identityEventLabel('profile/read')).toBe('Consulta de perfil');
  expect(identityEventLabel('UNRECOGNIZED/SECRET/internal-id')).toBe('Otra actividad de seguridad');
  expect(identityOutcomeLabel(1)).toBe('Completada');
  expect(identityOutcomeLabel(0)).toBe('No completada');
  expect(identityOutcomeLabel(9)).toBe('Estado no disponible');
  const id = '11111111-2222-4333-8444-555555555555';
  expect(identityReferenceLabel(id)).toBe('Terminación 555555');
  expect(identityReferenceLabel(id)).not.toContain(id);
});

test('C05 dates | respect profile time zone, reject invalid dates and survive invalid zone', () => {
  const sample = '2026-11-19T23:30:00.000Z';
  const local = identityDateLabel(sample, 'America/Tegucigalpa');
  const european = identityDateLabel(sample, 'Europe/Berlin');
  expect(local).not.toBe(european);
  expect(local).toContain('2026');
  expect(identityDateLabel('invalid', 'America/Tegucigalpa')).toBe('Fecha no disponible');
  expect(identityDateLabel(sample, 'Invalid/Zone')).toContain('2026');
});
