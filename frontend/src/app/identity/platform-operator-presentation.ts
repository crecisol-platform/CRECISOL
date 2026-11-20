import { identityDateLabel, identityReferenceLabel, identityTimeZoneLabel } from './identity-presentation';

/** C05 F04: presentation-only interpretation; never a backend authorization decision. */
export function operatorCaseView(item: {
  caseId: string; readyAtUtc: string | null; blocked: boolean; consumed: boolean;
} | null): { status: string; priority: string; caption: string; reference: string; ready: string } | null {
  if (!item) return null;
  const reference = identityReferenceLabel(item.caseId);
  const ready = item.readyAtUtc ? identityDateLabel(item.readyAtUtc) : 'Pendiente de doble revisión';
  if (item.blocked) return { status: 'Bloqueado', priority: 'Atención alta', reference, ready,
    caption: 'La revisión o denuncia de seguridad impide continuar. Solo el backend determina el cierre.' };
  if (item.consumed) return { status: 'Finalizado', priority: 'Archivado', reference, ready,
    caption: 'El proceso ya fue utilizado. No puede volver a emitirse una parte anterior.' };
  if (!item.readyAtUtc) return { status: 'En verificación', priority: 'Revisión pendiente', reference, ready,
    caption: 'La recuperación exige dos operadores independientes y una espera mínima de 24 horas.' };
  const at = Date.parse(item.readyAtUtc);
  return { status: Number.isFinite(at) && at <= Date.now() ? 'Plazo transcurrido' : 'En periodo de seguridad',
    priority: 'Control de seguridad', reference, ready,
    caption: 'El plazo es solo informativo: la operación requiere autorización, evidencia vigente y MFA.' };
}

export function operatorDeviceLabel(raw: string): string {
  const names: Readonly<Record<string, string>> = {
    chromium: 'Navegador Chromium', chrome: 'Navegador Chrome',
    firefox: 'Navegador Firefox', safari: 'Navegador Safari', edge: 'Navegador Edge'
  };
  // Restrict to coarse browser family; never display raw user agent/IP/fingerprint.
  return names[String(raw).trim().toLowerCase()] ?? 'Dispositivo de plataforma';
}

export function operatorLanguageLabel(value: string): string {
  return ({ 'es-HN': 'Español (Honduras)', es: 'Español', en: 'English' } as Readonly<Record<string, string>>)[value]
    ?? 'Idioma configurado';
}
export { identityDateLabel, identityReferenceLabel, identityTimeZoneLabel };

export type OperatorSignalPriority = 'alta' | 'revision' | 'informativa';
export interface OperatorAbuseView {
  id: string; label: string; count: number; priority: OperatorSignalPriority;
}

/** Numeric counts drive only an indicative UI attention band. Unknown signal
 * codes are never rendered verbatim and never grant privileges. */
export function operatorAbuseViews(input: readonly { kind: string; count: number }[]): OperatorAbuseView[] {
  const names: Readonly<Record<string, string>> = {
    LOGIN_RATE_LIMIT: 'Intentos de acceso limitados',
    MFA_RATE_LIMIT: 'Verificaciones de segundo factor limitadas',
    AUTH_BURST: 'Actividad de autenticación elevada',
    LOGIN_BURST: 'Intentos de acceso reiterados',
    RECOVERY_BURST: 'Solicitudes de recuperación reiteradas'
  };
  const known = new Map<string, number>();
  let other = 0;
  for (const entry of input.slice(0, 50)) {
    const value = Number(entry.count);
    const count = Number.isSafeInteger(value) && value >= 0 ? value : 0;
    if (Object.prototype.hasOwnProperty.call(names, entry.kind)) {
      known.set(entry.kind, (known.get(entry.kind) ?? 0) + count);
    } else {
      other += count;
    }
  }
  const band = (count: number): OperatorSignalPriority =>
    count >= 10 ? 'alta' : count >= 3 ? 'revision' : 'informativa';
  const visible: OperatorAbuseView[] = [...known].map(([kind, count], index) =>
    ({ id: String(index), label: names[kind], count, priority: band(count) }));
  if (other > 0) visible.push({
    id: 'other', label: 'Otras señales de protección', count: other, priority: band(other)
  });
  return visible.sort((a, b) => b.count - a.count);
}
