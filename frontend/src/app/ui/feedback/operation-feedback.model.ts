/** C05 UX F06 — messages are presentation only, never a receipt of authorization.
 * Messages must originate from an acknowledged API result or a safe local failure.
 * Never forward server payloads, tokens or exception strings to this view.
 */
export type OperationFeedbackKind = 'pending' | 'success' | 'error' | 'info';
export interface OperationFeedbackView {
  readonly kind: OperationFeedbackKind;
  readonly text: string;
}

/** Scope is a route/section key, NOT a tenant or security decision. */
export function contextualFeedback(
  origin: string, current: string, busy: boolean, message: string,
  error: string, action: string
): OperationFeedbackView | null {
  if (!origin || origin !== current) return null;
  if (busy) return { kind: 'pending', text: action || 'Verificando la solicitud…' };
  if (error) return { kind: 'error', text: error };
  return message ? { kind: 'success', text: message } : null;
}

/** Static institutional copy; unknown commands must not be echoed. */
const activities: Readonly<Record<string, string>> = {
  login: 'Verificando tus credenciales…',
  register: 'Registrando la solicitud de cuenta…',
  confirmation: 'Comprobando el correo y las condiciones…',
  verify: 'Verificando tu segundo factor…',
  logout: 'Cerrando la sesión…',
  enroll: 'Preparando la vinculación segura del autenticador…',
  'passkey-login': 'Comprobando tu llave de acceso…',
  'passkey-list': 'Consultando tus llaves registradas…',
  'pending-memberships': 'Consultando solicitudes autorizadas…',
  'terms-read': 'Consultando las condiciones vigentes…',
  'pair-start': 'Preparando una vinculación entre navegadores…',
  'pair-complete': 'Verificando la aprobación de vinculación…',
  'security-events': 'Consultando tu actividad de seguridad…',
  'profile-read': 'Actualizando la información del perfil…',
  'profile-photo': 'Guardando la foto verificada…',
  'profile-photo-remove': 'Eliminando la foto seleccionada…',
  'sessions-list': 'Consultando las sesiones autorizadas…',
  'sessions-rotate': 'Renovando esta sesión…',
  'sessions-revoke': 'Revocando la sesión seleccionada…',
  'membership-request': 'Registrando tu solicitud empresarial…',
  profile: 'Guardando las preferencias del perfil…',
  'assisted-request': 'Enviando la solicitud de recuperación asistida…',
  'assisted-complete': 'Verificando el canal de recuperación…',
  'assisted-handoff': 'Validando las dos partes independientes…',
  'passkey-recovery': 'Verificando la recuperación con llave de acceso…',
  incident: 'Registrando el reporte de seguridad…',
  'sessions-all': 'Revocando las sesiones verificadas…',
  'pairing-approve': 'Verificando la aprobación del navegador…',
  terms: 'Registrando la aceptación de condiciones…',
  'invitation-issue': 'Emitiendo la invitación autorizada…',
  'invitation-accept': 'Verificando la aceptación de invitación…',
  'passkey-add': 'Registrando la nueva llave de acceso…',
  'passkey-revoke': 'Revocando la llave seleccionada…',
  private: 'Comprobando los permisos de consulta…',
  password: 'Actualizando la contraseña protegida…',
  factor: 'Preparando el reemplazo del autenticador…',
  'factor-confirm': 'Verificando el nuevo autenticador…',
  'mailbox-request': 'Solicitando la verificación de correo…',
  'mailbox-complete': 'Confirmando el nuevo correo…',
  membership: 'Validando la decisión administrativa…',
  'recovery-request': 'Solicitando instrucciones de recuperación…',
  'recovery-complete': 'Verificando el restablecimiento de contraseña…',
  mfa: 'Verificando el MFA de operador…',
  case: 'Consultando el expediente autorizado…',
  'find-case': 'Buscando la referencia autorizada…',
  review: 'Registrando la revisión independiente…',
  share: 'Preparando la parte temporal de recuperación…',
  'session-policy': 'Actualizando la política de inactividad…',
  'revoke-all': 'Revocando las sesiones de plataforma…',
  'operator-recovery-request': 'Solicitando revisión de identidad del operador…',
  'operator-recovery-complete': 'Verificando las dos revisiones del operador…',
  'operator-recovery-enroll': 'Validando el nuevo MFA del operador…',
  'operator-incident': 'Registrando el incidente del operador…',
  'load-abuse': 'Consultando las señales de protección…'
};
export function activityLabel(command: string): string {
  return activities[command] ?? 'Procesando la operación protegida…';
}
