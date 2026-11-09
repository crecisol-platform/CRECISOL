/** C02-05: five reusable PRESENTATION states. No authentication or backend effect.
 * Empty and confirmation require future authorized source data; never infer
 * success from a client-side event or a stored snapshot.
 */
export const INTERACTION_KINDS = [
  'loading', 'empty', 'error', 'offline', 'confirmation'
] as const;

export type InteractionKind = typeof INTERACTION_KINDS[number];

export interface InteractionView {
  readonly kind: InteractionKind;
  readonly eyebrow: string;
  readonly title: string;
  readonly description: string;
  readonly retryLabel?: string;
}

// Institutional, noncommercial public-identity fallback copy only. Tenant
// content and custom UI copy will be versioned/validated by backend chapters.
const PUBLIC_LOADING: InteractionView = Object.freeze({
  kind: 'loading',
  eyebrow: 'Identidad pública',
  title: 'Preparando información de plataforma',
  description: 'Consultando la publicación vigente.'
});

const PUBLIC_ERROR: InteractionView = Object.freeze({
  kind: 'error',
  eyebrow: 'Identidad pública',
  title: 'La información pública no está disponible',
  description: 'No se mostrarán datos de demostración ni de negocios privados.',
  retryLabel: 'Intentar nuevamente'
});

const PUBLIC_OFFLINE: InteractionView = Object.freeze({
  kind: 'offline',
  eyebrow: 'Identidad pública',
  title: 'Sin conexión para actualizar la información',
  description: 'No se mostrará información almacenada de otra sesión o empresa. Comprueba tu conexión e inténtalo de nuevo.',
  retryLabel: 'Intentar nuevamente'
});

/** A browser connectivity hint is NOT a security decision.
 * Only a validated platform-public GET can reach the ready state.
 */
export function publicBrandFallback(
  load: 'loading' | 'unavailable', browserOnline: boolean
): InteractionView {
  if (load === 'loading') return PUBLIC_LOADING;
  return browserOnline ? PUBLIC_ERROR : PUBLIC_OFFLINE;
}
