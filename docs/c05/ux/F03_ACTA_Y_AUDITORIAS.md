# CRECISOL · C05 UX · F03 — Acta de cierre técnico delimitado

**Resolución: F03 CERRADA TÉCNICAMENTE EN SU ALCANCE DE NAVEGACIÓN Y ARQUITECTURA DE INFORMACIÓN.**  
**Calificación premium final ≥9/10 de las 65 interfaces: NO CERTIFICADA, reservada para la última fase.**  
**Fecha:** 19-11-2026 (America/Tegucigalpa) / 20-11-2026 UTC.

## Identidad, dependencias y límites

- Repositorio autorizado: `crecisol-platform/CRECISOL` (ID 1450414592). No se toca `solqaryn/Solqaryn`.
- Rama: `feature/c05-ux-f03-arquitectura-navegacion-20261119`, PR [#176](https://github.com/crecisol-platform/CRECISOL/pull/176) **DRAFT, sin fusionar**.
- Base F02: `0361934286282ddb1b6ba94797125859d37464d7`, PR #175 técnicamente cerrada.
- C05 fuente: `f61a79d242288c686a88a7c3dd711f2be9a48c18`, PR #172 de TOTP-SHA1 y sesión por inactividad configurable. No se cambian sus contratos.
- Prueba integral **SUCCESS** del último SHA de código: `71db3b7e49561a0f71fb014d966805b3673f6a88`, [run #44648016787](https://github.com/crecisol-platform/CRECISOL/actions/runs/44648016787). El contenido documental de esta acta y del registro de hallazgos provoca un nuevo HEAD: su CI deberá ser verificada por separado antes de afirmar SUCCESS de ese SHA.
- Alcance del trabajo: componentes de portal y operador en `frontend/src/app/pages/`, rutas de presentación, CSS acotado, QA, workflow y documentación F03. **Ningún cambio** a `frontend/src/app/app.routes.ts`, servicios o endpoints API, TOTP SHA1, WebAuthn, 30 minutos inactividad configurable, `backend/`, `database/`, VPS, producción, C01/C02, `main`, `dev` o PR #173.

## Matriz de resultados A/B/C

| Auditoría | Prueba / criterio | Evidencia del SHA 71db3b7e... | Veredicto |
| --- | --- | --- | --- |
| **A — Funcionalidad** | Rutas permitidas, comportamiento por estado, componentes, navegación de operador, `loadSessionPolicy()` preservado | `qa/c05_ux_f03_navigation_audits.py A` en CI | PASS |
| **A — Compilación** | TypeScript/Angular 22 aplicación y harness institucional F02 | Dos compilaciones SUCCESS | PASS |
| **A — E2E** | Cuenta invitada, autenticada y operador en cinco resoluciones, enlaces y regresión de teclado | **18/18** Playwright F03 | PASS |
| **A — Regresión previa** | F02 componentes y modales institucionales C05 | **11/11** F02 y **2/2** modales C05 | PASS |
| **B — Separación** | Invitado no muestra áreas privadas, operador sin login no muestra su consola; allowlists y datos sintéticos | `qa/c05_ux_f03_navigation_audits.py B`, pruebas negativas Chromium | PASS en alcance UI |
| **B — Privacidad** | Sin datos personales, QR real, envío backend real, persistencia o token privado en URL de operador | Inspección del diff y del manifest sintético | PASS en alcance UI |
| **C — Arquitectura/UX** | Tokens F02, foco/skip-link, `aria-current`, breadcrumbs, zoom 200%, forced-colors, reduced-motion, axe WCAG | `qa/c05_ux_f03_navigation_audits.py C` y 18 Chromium | PASS |
| **C — Capturas** | 320,390,768,1024,1440, estados guest/recovery/account profile/account security/operator cases/operator security | `qa/c05_ux_f03_evidence_audits.py`: **30 PNG/30 JSON**, manifest y SHA coincidente | PASS |

## Artefactos y verificación de integridad

- GitHub Actions [run #44648016787](https://github.com/crecisol-platform/CRECISOL/actions/runs/44648016787): **COMPLETED / SUCCESS** en el SHA de código `71db3b7e...`.
- Artefacto **ID 14588439356**, `c05-f03-navigation-71db3b7e49561a0f71fb014d966805b3673f6a88`. ZIP descargado e inspeccionado fuera del runner.
- ZIP: **66 archivos: 35 PNG** (30 de F03 + 5 del harness F02), **31 JSON** (30 individuales y un manifiesto F03).
- El manifiesto declara `runnerSha=71db3b7e...`, `screenshots=30`, `perViewport={320:6,390:6,768:6,1024:6,1440:6}`, `synthetic=true`, `backendAuthorizationVerified=false`, `finalAestheticNineOfTenCertified=false`. Cada imagen tiene hash SHA-256 registrado.
- SHA-256 del ZIP descargado e inspeccionado: `e6940c94374c3f26503294721421f4f36bcd2ab2ae8a3b58a177fcea7c604dcb`.
- La revisión visual independiente de las capturas finales 320,390,1440 confirmó navegación legible, separación de ámbitos y eliminación de textos redundantes de operador; el documento no presentó desbordamiento horizontal en las pruebas. Los detalles de captura son sintéticos y no demuestran calidad 9/10 de todas las interfaces.

## Defectos reales corregidos y regresiones

**F03-UX-001 — RESUELTO (P1):** primer run [#44647268135](https://github.com/crecisol-platform/CRECISOL/actions/runs/44647268135) mostró **17/18 PASS** con un fallo real del skip-link: `href="#account-content"` con el `<base href="/">` hacía navegar a la portada pública `/#account-content`. Se implementó `focusContent(event)` para cancelar navegación y enfocar el contenido de `/cuenta` sin cambiar `?vista`; test de teclado ahora exige foco real y URL preservada. Run correctivo 18/18 PASS.

**F03-UX-002 — RESUELTO (P2):** la consola operadora repetía encabezado, explicación y sección en el estado autenticado. Se eliminó la introducción duplicada para operadores ya verificados, conservando formularios y controles.

**Sin regresiones funcionales activas detectadas en el alcance probado F03**. No se ocultan runs fallidos/cancelados históricos, que permanecen auditables.

## Criterios explícitos de no sobrecertificación

- Esta fase **NO** rediseña ni califica las 65 pantallas/modales/estados completos. El objetivo ≥9/10 **se mide por pantalla al terminar la última fase**, no por media de esta fase ni por CI verde.
- Las pruebas de navegación son de frontend **sintético**: no ejecutan controles de autorización real en .NET/MySQL, IDOR entre tenants, MFA físico, expiración real de 30 minutos, OAuth/WebAuthn de hardware ni recuperación 24 horas. No atribuirles ninguna certificación de esos contratos.
- El usuario solicitó no avanzar de fase hasta certificar el alcance vigente. **F04 NO INICIADA**. PR #176 DRAFT sin fusionar por dependencia con PR #175 y #172.
- Si surgiera un fallo vigente en CI del SHA final con este documento, el cierre se reabre y no se declara certificado hasta corregirlo. No activar producción, compras, pagos ni servicios externos.

**Veredicto condicionado al gate del HEAD final:** F03 técnicamente completada y probada. Cierre institucional documentado, pendiente únicamente de la repetición de CI sobre el nuevo commit del acta; dejar constancia final en conversación del PR tras comprobarlo sin crear otro commit.
