/** C02-03: public, platform-only information architecture.
 * These are structural labels, NOT tenant settings or commercial offers.
 * No private routes, forms, real demo data or login affordances.
 */
export type PublicInfoId = 'experiencias' | 'demostraciones' | 'alta-empresas';

export interface PublicInfoSection {
  readonly id: string;
  readonly title: string;
  readonly description: string;
  readonly status: 'Identidad publicada' | 'En preparación';
}

export interface PublicInfoPage {
  readonly path: string;
  readonly title: string;
  readonly kicker: string;
  readonly introduction: string;
  readonly note: string;
  readonly sections: readonly PublicInfoSection[];
}

export const PUBLIC_INFO_NAV: readonly { readonly path: string; readonly label: string }[] = [
  { path: '/experiencias', label: 'Experiencias' },
  { path: '/demostraciones', label: 'Demostraciones' },
  { path: '/alta-empresas', label: 'Alta de empresas' }
];

export const PUBLIC_INFO_PAGES: Readonly<Record<PublicInfoId, PublicInfoPage>> = {
  experiencias: {
    path: '/experiencias',
    title: 'Experiencias previstas',
    kicker: 'Arquitectura de producto',
    introduction: 'Seis experiencias comparten una plataforma. Esta guía describe su alcance previsto, no concede permisos ni habilita operaciones privadas.',
    note: 'Las experiencias administrativas y de consumidores necesitan identidad, dominio verificado y autorización en backend antes de estar operativas.',
    sections: [
      { id: 'sitio-saas', title: 'Sitio público de la plataforma',
        description: 'La identidad institucional publicada está disponible en Inicio. Las ofertas comerciales y la contratación operativa se desarrollarán en sus capítulos.', status: 'Identidad publicada' },
      { id: 'registro', title: 'Registro y contratación',
        description: 'La información sobre el futuro alta de empresas está separada de los servicios de registro y cobro, aún no habilitados.', status: 'En preparación' },
      { id: 'consola', title: 'Consola SuperAdmin',
        description: 'Experiencia institucional privada, sujeta a autorización backend y acceso excepcional auditado a datos sensibles.', status: 'En preparación' },
      { id: 'negocio', title: 'Panel del negocio',
        description: 'Administración futura de marca y operaciones, restringida a una empresa y a sus permisos efectivos.', status: 'En preparación' },
      { id: 'empleado', title: 'Puesto de empleado',
        description: 'Tareas previstas según sucursal, acción y asignación; sin acceso a finanzas ni expedientes ajenos por defecto.', status: 'En preparación' },
      { id: 'consumidor', title: 'Sitio y portal del consumidor',
        description: 'Información pública de cada negocio solo mediante dominio verificado. Historial y comprobantes privados exigen identidad y autorización.', status: 'En preparación' }
    ]
  },
  demostraciones: {
    path: '/demostraciones',
    title: 'Demostraciones previstas',
    kicker: 'Vista informativa',
    introduction: 'El centro de demostraciones es una capacidad planificada, no una cuenta, una consola privilegiada ni una tienda activa.',
    note: 'Todavía no hay simuladores interactivos. No se piden contraseñas, medios de pago ni datos de personas reales.',
    sections: [
      { id: 'datos', title: 'Datos exclusivamente sintéticos',
        description: 'Los recorridos de demostración usarán escenarios ficticios separados de empresas, clientes y sesiones reales.', status: 'En preparación' },
      { id: 'sectores', title: 'Recorridos por actividad y rol',
        description: 'Los ejemplos futuros distinguirán comercio, servicios y membresías sin crear aplicaciones independientes por sector.', status: 'En preparación' },
      { id: 'dispositivos', title: 'Escritorio, tableta y móvil',
        description: 'Los recorridos deberán ser accesibles por teclado, lector de pantalla y movimiento reducido.', status: 'En preparación' }
    ]
  },
  'alta-empresas': {
    path: '/alta-empresas',
    title: 'Alta de empresas: información',
    kicker: 'Proceso previsto',
    introduction: 'El registro empresarial y la contratación forman una experiencia planificada. Esta página no crea cuentas, contratos ni empresas.',
    note: 'No hay formulario operativo, selector de empresas en inicio de sesión ni cobros habilitados en esta etapa.',
    sections: [
      { id: 'requisitos', title: 'Conocer el proceso',
        description: 'Se documentarán requisitos, condiciones, consentimiento y pasos de verificación antes de solicitar un alta real.', status: 'En preparación' },
      { id: 'verificacion', title: 'Verificación y pertenencia',
        description: 'La activación futura estará controlada por backend. Ningún parámetro del navegador otorga pertenencia o permisos.', status: 'En preparación' },
      { id: 'contratacion', title: 'Contratos y pagos',
        description: 'El flujo futuro deberá mostrar términos versionados y autorizaciones explícitas. No se realizan cargos aquí.', status: 'En preparación' }
    ]
  }
};
