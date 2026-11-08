# Arquitectura de desarrollo inicial — CRECISOL

Estado: DISEÑADA, NO IMPLEMENTADA. Fuente: Documento Maestro Único identificado en docs/MASTER_REFERENCE.md.

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
CI/QA solo valida aspectos respaldados por herramientas reales, sin despliegues ni cobros productivos. Seguridad P0 es una puerta transversal. Detalles en docs/SEGURIDAD_MULTITENANT.md y .agents/skills/crecisol-project-governance/references/.
