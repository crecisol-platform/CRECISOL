# Arquitectura de desarrollo inicial — CRECISOL

Estado: **DISEÑADA; corte público C01/C02 implementado, probado y certificado en su alcance; arquitectura de módulos privados NO IMPLEMENTADA**. C03-01 inventaría dependencias físicas del stack existente, sin convertir planes de módulos o Workers en software activo. Fuente: Documento Maestro Único identificado en docs/MASTER_REFERENCE.md y [C03-01](c03/C03_01_ESPECIFICACION.md).

## Límites físicos
frontend/ (Angular, design system, editor, experiencias públicas y privadas)
backend/ (API .NET, módulos verticales, application/domain/infra/workers)
database/ (MySQL, SQL/migraciones/normalización/fixtures)

No crear procesos o microservicios distribuidos sin necesidad demostrada. Los controladores solo delegan en casos de uso. Domain es puro, Application coordina, Infrastructure implementa puertos; workers asíncronos usan outbox idempotente.

## Identidad y tenancy
La misma plataforma sirve a tenants segregados lógicamente en un MySQL compartido y opcionalmente tenants con DB propia. Router del lado servidor, mapeo seguro de ubicación, conexiones separadas y privilegios mínimos. Identidad con membresía tenant-contextual sin selector de empresa en login normal. SuperAdmin usa consola central con acceso excepcional registrado a información privada.

## Experiencia parametrizable
Identidad/SEO, dominio autorizado, íconos, textos, footer/copyright separado, tema, formularios, listas y módulos por tenant; nunca repetir literales. Versionar y previsualizar cambios antes y después de guardar y antes de descargar. Bloques allowlist sin código ejecutable.

## Calidad y lanzamiento
CI/QA solo valida aspectos respaldados por herramientas reales, sin despliegues ni cobros productivos. Seguridad P0 es una puerta transversal. Detalles en docs/SEGURIDAD_MULTITENANT.md y .agents/skills/crecisol-project-governance/references/.\n\n## Contratos comerciales SaaS — diseño futuro condicionado a edición canónica

**Fuente de análisis no normativo:** `docs/ANALISIS_IMPACTO_MODELO_COMERCIAL_2026-11-10.md` (`COM-001..COM-014`). Aprobado conceptualmente por el propietario, pero el DOCX/PDF revisado aún no está integrado en GitHub; no equivale a código.

- **Dirección:** ruta contextual, subdominio y dominio verificado son vías de publicación/contexto; no deciden permisos, base de datos ni capacidades.
- **Oferta SaaS:** una combinación versionada de modalidad de dirección, plan/entitlements, plazo contractual de 3/6/12/24/36 meses y periodicidad de cobro. Los cambios de catálogo no alteran versiones de contrato ya aceptadas.
- **Plano de control:** CRECISOL administra precios, contratos, suscripciones, facturas, cobros/conciliación, estados de empresas y campañas institucionales. El plano de tenant administra operaciones, membresías y cobros de sus propios consumidores, sin mezclar registros financieros.
- **UI:** consola SuperAdmin y autoservicio de propietario con autorización backend separada; precios/publicaciones/avisos/campañas configurables sin SQL/JS arbitrarios.
- **Eventos:** transiciones idempotentes, reconciliación de pagos antes de activar servicios, suspensión reversible y cancelación/no renovación con trazabilidad.
- **Contratos jurídicos:** renovación, gracia, penalidad, refund, datos retenidos y espacios publicitarios quedan sujetos a legalidad verificable y consentimiento expreso; no implementar cargas reales todavía.

Implementación solamente cuando el capítulo correspondiente esté habilitado (principalmente C04/C08/C19/C20/C23/C26/C31/C33/C34/C36). C03-09 sigue pendiente.
