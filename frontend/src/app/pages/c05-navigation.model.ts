/** C05 / UX F03 — Pure, allowlisted presentation routing.
 * This is NOT a router guard or backend authorization decision.
 * Unknown or state-incompatible deep links resolve to a safe visible view
 * without modifying credentials, the URL, or the server-side realm.
 */
export const guestViews = [
  'ingresar', 'registro', 'confirmar', 'recuperar', 'invitacion', 'acceso-existente'
] as const;
export const workspaceViews = [
  'resumen', 'perfil', 'llaves', 'seguridad', 'historial', 'administracion'
] as const;

export type GuestView = typeof guestViews[number];
export type WorkspaceView = typeof workspaceViews[number];
export type OperatorSection = 'casos' | 'sesiones' | 'perfil' | 'seguridad' | 'monitor';

export const guestLabels: Readonly<Record<GuestView, string>> = {
  ingresar: 'Iniciar sesión',
  registro: 'Solicitar cuenta',
  confirmar: 'Confirmar correo',
  recuperar: 'Recuperar acceso',
  invitacion: 'Aceptar invitación',
  'acceso-existente': 'Ya tengo cuenta'
};

export const workspaceLabels: Readonly<Record<WorkspaceView, string>> = {
  resumen: 'Resumen',
  perfil: 'Mi perfil',
  llaves: 'Llaves de acceso',
  seguridad: 'Seguridad',
  historial: 'Historial',
  administracion: 'Aprobaciones'
};

export const operatorSections: ReadonlyArray<{ id: OperatorSection; label: string; description: string }> = [
  { id: 'casos', label: 'Casos', description: 'Consulta y revisión independiente de identidad.' },
  { id: 'sesiones', label: 'Sesiones', description: 'Dispositivos y revocaciones de la plataforma.' },
  { id: 'perfil', label: 'Perfil', description: 'Datos personales del operador autorizado.' },
  { id: 'seguridad', label: 'Seguridad', description: 'Factor MFA y política de inactividad.' },
  { id: 'monitor', label: 'Monitor', description: 'Señales agregadas de seguridad.' }
] as const;

export function guestViewFor(raw: string): GuestView {
  return (guestViews as readonly string[]).includes(raw) ? raw as GuestView : 'ingresar';
}

export function workspaceViewFor(raw: string): WorkspaceView {
  return (workspaceViews as readonly string[]).includes(raw) ? raw as WorkspaceView : 'resumen';
}

export function isSecondaryGuestView(raw: string): boolean {
  return raw === 'confirmar' || raw === 'recuperar' ||
    raw === 'invitacion' || raw === 'acceso-existente';
}

export function operatorSectionFor(raw: string): OperatorSection | null {
  return operatorSections.find(section => section.id === raw)?.id ?? null;
}
