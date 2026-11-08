# CRECISOL

**Tu negocio, a tu manera.** — SaaS multitenant para comercio, servicios y membresías.

Repositorio privado e independiente. El Documento Maestro Único institucional se conserva en `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx` con su exportación PDF. La trazabilidad de versión y las huellas verificadas constan en `docs/MASTER_REFERENCE.md`.

## Estado inicial del desarrollo — 8 de noviembre de 2026

- **37 capítulos documentados, 0 capítulos certificados en código.** La fase 1 (capítulo 01: identidad, visión, alcance y principios) está en desarrollo, sin certificación.
- `main`: integración estable de documentación y cambios revisados; no presume software ya certificado.
- `dev`: integración de desarrollo; tareas nuevas en `feature/cNN-*` y PR.
- El repositorio contiene arquitectura, normas, estructura y verificaciones preliminares; **todavía no existe un SaaS operativo, infraestructura desplegada ni cobros**.
- En este repositorio privado la protección automática de ramas no está activa por las condiciones del plan GitHub. Seguimos con PR, revisión manual y CI disponible; ver `docs/REPOSITORY_GATES.md`.

## Estructura

- `frontend/` — Angular 22 y Node.js 24 para herramientas, sujetos a pruebas reales.
- `backend/` — ASP.NET Core 10, arquitectura limpia y monolito modular.
- `database/` — MySQL 8.4 LTS, esquema relacional con protección por tenant.
- `docs/` — fuente maestra, diseño y secuencia de 37 capítulos.
- `qa/` — verificaciones reproducibles y pruebas con evidencia.
- `.agents/skills/crecisol-project-governance/` — Skill local de gobierno exclusiva de CRECISOL.

## Reglas de contribución

Revisar `AGENTS.md`, la Skill local y el maestro canónico antes de escribir. Un capítulo a la vez, con pruebas positivas/negativas y triple auditoría A/B/C antes de certificar. Aislamiento tenant/sucursal y autorización server-side obligatorios.

No copiar credenciales, fuentes, configuraciones ni recursos de `solqaryn/Solqaryn`. Solo consulta autorizada; ningún cambio en SOLQARYN. No desplegar ni activar servicios externos durante la fase local.

**Comprobación inicial:** `python3 qa/validate_foundation.py` verifica estructura y alcance, **no compila ni certifica** Angular, .NET o MySQL.

## Referencias visuales históricas

La [biblioteca de diseño curada](docs/referencias-visuales/README.md) documenta cómo nació la idea y los prototipos aprobados como **inspiración visual**, no como código ni como documentación normativa. Se mantendrá el logo oficial azul y se construirá el frontend de CRECISOL desde cero.
