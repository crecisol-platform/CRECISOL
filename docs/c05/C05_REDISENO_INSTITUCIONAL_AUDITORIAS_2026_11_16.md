# CRECISOL — C05 · Acta suplementaria del rediseño institucional de identidad

**Fecha:** 16-11-2026. **Estado inicial del acta:** CONDICIONADO A CI POSTMERGE. **Capítulos:** se preserva el estado vigente C01–C05 5/37, exclusivamente en sus alcances documentados; **C06 BLOQUEADO**.

## 1. Autoridad, origen y alcance

- Repositorio único: `crecisol-platform/CRECISOL` (ID 1450414592). Rama de integración `dev`, sin intervención de `main`, SOLQARYN, VPS, producción, proveedores externos o cobros.
- Referencias vigentes: `AGENTS.md`, `.agents/skills/crecisol-project-governance/SKILL.md`, `docs/MASTER_REFERENCE.md`, `docs/PLAN_37_CAPITULOS.md`, `docs/ESTADO_VIGENTE_CAPITULOS.md` y `docs/c05/C05_INTEGRAL_OPERATIVO_2026_11_15.md`. El Documento Maestro Único Word/PDF permanece inalterado. Su certificación histórica C05 de desarrollo local aislado no se revoca ni se extiende a producción.
- PR técnico [#166](https://github.com/crecisol-platform/CRECISOL/pull/166): origen `feature/c05-institutional-modals-20261116` HEAD `5cd97df5d3d120622e95c1a8e6aae70e35010e82`, base `dev` previa `94c1c0a1531b8f2453461f7814abd17b55a9607f`.
- Integración exclusiva a `dev` mediante squash `3d132ace38f07cf73aede518037f2bca6f70e80d`. Diff limitado a **25 archivos** (`frontend/`, `qa/`, `.github/workflows/`); **cero cambios en `backend/`, `database/`, migraciones, configuración de producción, Documento Maestro o `main`**.

## 2. Cambios funcionales y diseño

- Consola de identidad de operadores en **Casos, Sesiones, Perfil, Seguridad y Monitor**, sin agrupar los dieciocho formularios en una sola pantalla.
- Cuenta autenticada mantiene vistas principales planas y navega a las tareas delicadas con **modales institucionales** reutilizables. Inventario original comprobado: **18 formularios de operador y 15 de cuenta**; mismos nombres de campos y contratos de servicios.
- `InstitutionalModalComponent` standalone Angular 22 con `<dialog>.showModal()`, top layer, bloqueo de fondo, cierre y retorno de foco, estado ocupado sin Escape prematuro, limpieza de formularios, adaptación responsive, alto contraste y reducción de movimiento.
- Parte de recuperación restringida al diálogo activo y eliminada al cerrarlo. Confirmación de cierre de todas las sesiones diferenciada de su disparador para teclado y tecnologías de asistencia; no se incorporan permisos frontend que reemplacen autorización backend.
- No hay cambios en la semántica de autorización por identidad, tenant verificado, acción, sesión, MFA, recuperación ni persistencia. El frontend por sí solo nunca concede acceso.

## 3. Cinco auditorías y evidencia PREMERGE del SHA final

| Puerta | Resultado real | Evidencia |
| --- | --- | --- |
| **A — funcional** | PASS: preservación 18+15, vistas por tareas y contratos de campo | [CI A/B/C/D 43980822409](https://github.com/crecisol-platform/CRECISOL/actions/runs/43980822409) |
| **B — seguridad** | PASS: modalidades de foco, cancelación durante busy, limpieza de partes secretas y seis mutaciones negativas del componente; conserva regresiones de backend | [CI A/B/C/D 43980822409](https://github.com/crecisol-platform/CRECISOL/actions/runs/43980822409) |
| **C — arquitectura y UX automática** | PASS: Angular 22, tipos, build, Chromium, responsive, axe y control de cambios | [CI A/B/C/D 43980822409](https://github.com/crecisol-platform/CRECISOL/actions/runs/43980822409) |
| **D — accesibilidad ampliada** | PASS: cuatro pruebas reales Chromium a 320/768/1440 px, foco/teclado, Escape, formularios limpios, forced-colors y reduced-motion | [CI A/B/C/D 43980822409](https://github.com/crecisol-platform/CRECISOL/actions/runs/43980822409) |
| **E — integración y regresiones** | PASS: dos trabajos SUCCESS, Angular → API .NET 10 → MySQL 8.4 aislado, **296 aserciones operativas**, regresiones históricas C01–C05, aplicación y reversión de **16 migraciones** hasta esquema vacío | [CI E 43980822541](https://github.com/crecisol-platform/CRECISOL/actions/runs/43980822541) |

Comprobaciones complementarias: [C03 regresión integral 43980822674](https://github.com/crecisol-platform/CRECISOL/actions/runs/43980822674) SUCCESS y [Foundation 43980822496](https://github.com/crecisol-platform/CRECISOL/actions/runs/43980822496) SUCCESS. Los trabajos condicionales marcados `skipped` NO se computan como ejecutados.

**Trazabilidad de fallos:** ejecuciones anteriores de E fallaron por navegación con un modal activo y por un localizador Playwright con nombre duplicado; se conservaron como fallos históricos. Las transiciones quedaron corregidas y repetidas sobre el HEAD final, con E 2/2 SUCCESS. Nunca atribuir esos FAIL históricos a un PASS ni eliminar negativas para lograr el verde.

## 4. Riesgos, límites y reversión

- **No se detectaron P0/P1 vigentes en el alcance ejecutado.** Esta frase no afirma ausencia universal de defectos ni seguridad de producción.
- La prueba automática de accesibilidad, CSS y Chromium **NO sustituye una revisión visual humana de todas las pantallas con capturas actuales, un lector de pantalla real ni validación subjetiva pixel a pixel**. La equivalencia estética comercial final queda expresamente fuera del dictamen técnico hasta contar con esa evidencia. No confundir WCAG automatizado con auditoría manual de accesibilidad completa.
- Fuera de alcance: producción, servicios de correo externos, COM-T08/09/10, SuperAdmin productivo, Cloudflare, VPS, compradores, pagos, C06 y SOLQARYN.
- Reversión técnica: `git revert` del squash `3d132ace38f07cf73aede518037f2bca6f70e80d` en una rama controlada y nueva validación antes de integrar; no hay migración nueva ni cambio de datos que revertir en este incremento. `main` no se toca.

## 5. Formalización documental y puerta de cierre

El PR #121 (documentación preparatoria antigua) y el PR #156 (propuesta UX sustituida) se **cerraron sin fusionar**, conservando su historial. El PR #166 se fusionó a `dev` tras cinco auditorías PREMERGE aprobadas y revisión de alcance.

**Este acta solo podrá declararse CERTIFICADA TÉCNICAMENTE EN ALCANCE LOCAL AISLADO** después de comprobar las ejecuciones POSTMERGE aplicables del squash exacto, actualizar este apartado con sus resultados y fusionar el PR documental únicamente a `dev`. Hasta entonces: **ACTA CONDICIONADA / NO CERTIFICADA COMO SUPLEMENTO UX**.
