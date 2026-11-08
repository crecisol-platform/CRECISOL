# CRECISOL
**Tu negocio, a tu manera.** · SaaS multitenant para comercio, servicios y membresías.

Repositorio privado independiente de SOLQARYN. Solo contiene fundamentos de desarrollo, **no un SaaS implementado**.

## Estado inicial (2026-11-08)
- main conserva el commit inicial; trabajo de configuración en dev.
- Capítulos del Documento Maestro Único: 37. Certificados en código: 0.
- Controles de seguridad pendientes de habilitación en GitHub: revisar docs/REPOSITORY_GATES.md.
- Entorno: programación y pruebas locales; no existe despliegue de CRECISOL.

## Estructura
- frontend/ — Angular 22, Node 24 para tooling, dos experiencias públicas/administrativas cuando se construyan.
- backend/ — ASP.NET Core 10, Clean Architecture / monolito modular.
- database/ — MySQL 8.4 LTS, datos relacionales y tenancy compartida o dedicada.
- docs/ — referencia al único maestro, plan y decisiones.
- qa/ — verificaciones reproducibles sin credenciales.
- .agents/skills/crecisol-project-governance/ — única Skill local propia.

## Disciplina
Un capítulo por vez; tres auditorías y evidencia positiva/negativa antes de certificar. Aislamiento estricto de tenants, control del SuperAdmin auditado, parametrización completa de datos comerciales y seguridad server-side obligatoria.

Leer AGENTS.md, la Skill local y docs/MASTER_REFERENCE.md antes de editar. No copiar código, recursos ni configuraciones de solqaryn/Solqaryn.

## Comprobación local inicial
Ejecutar: python3 qa/validate_foundation.py

**Documentación maestra:** el único archivo oficial de planificación es CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx (y su exportación PDF). Ver docs/MASTER_REFERENCE.md para estado, hash y ubicación prevista; la copia binaria todavía NO está subida a este repositorio.
