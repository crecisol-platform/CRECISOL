/**
 * C05 presentation-only formatting. The server's tenant, role and resource
 * authorization is authoritative; these labels must never drive access rules.
 * Do not render raw audit payloads, session IDs or authentication secrets.
 */
const actionNames: Readonly<Record<string, string>> = {
  login: 'Inicio de sesión',
  'mfa/verify': 'Verificación de segundo factor',
  'mfa/enroll': 'Configuración de autenticador',
  'profile/read': 'Consulta de perfil',
  'profile/save': 'Actualización de perfil',
  'profile/photo': 'Consulta de foto',
  'profile/photo-upload': 'Actualización de foto',
  'profile/photo-remove': 'Eliminación de foto',
  'password/change': 'Cambio de contraseña',
  'sessions': 'Consulta de sesiones',
  'sessions/list': 'Consulta de sesiones',
  'sessions/rotate': 'Renovación de sesión',
  'sessions/revoke': 'Cierre de sesión',
  'sessions/revoke-all': 'Cierre de sesiones',
  'passkeys/register': 'Registro de llave de acceso',
  'passkeys/revoke': 'Revocación de llave de acceso',
  'passkeys/register-options': 'Preparación de llave de acceso',
  'terms/accept': 'Aceptación de condiciones',
  'memberships/decide': 'Revisión de membresía',
  REGISTRATION_PENDING: 'Solicitud de registro'
};

export function identityDateLabel(value: string, timeZone?: string | null): string {
  const date = new Date(value);
  if (!Number.isFinite(date.getTime())) return 'Fecha no disponible';
  const options: Intl.DateTimeFormatOptions = {
    day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit'
  };
  // A validated profile zone takes precedence over the browser's locale zone.
  // Invalid or unavailable profile settings must not break account security.
  try {
    return new Intl.DateTimeFormat('es-HN', timeZone ? { ...options, timeZone } : options).format(date);
  } catch {
    return new Intl.DateTimeFormat('es-HN', options).format(date);
  }
}

export function identityEventLabel(action: string): string {
  // Unknown backend event codes are not displayed verbatim to end users.
  return actionNames[action] ?? 'Otra actividad de seguridad';
}

export function identityOutcomeLabel(outcome: number): string {
  if (outcome === 1) return 'Completada';
  if (outcome === 0) return 'No completada';
  return 'Estado no disponible';
}

export function identityReferenceLabel(reference: string): string {
  const suffix = reference.replace(/[^a-zA-Z0-9]/g, '').slice(-6).toUpperCase();
  return suffix ? `Terminación ${suffix}` : 'Referencia no disponible';
}
