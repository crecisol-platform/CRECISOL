# C05 — Segunda auditoría de funcionalidad, arquitectura y experiencia de identidad

**Fecha:** 16-11-2026. **Repositorio:** `crecisol-platform/CRECISOL`. **Baseline obligatorio:** `dev` SHA `74cdf1268c2c6f3710f4426260965035dc90f85c`; `main` SHA `b794181148d2c5e36c1c88127f19a8a9082acff5`. **Nuevo alcance:** corrección editorial y progresividad de interfaz, sin cambios en backend/database. **Estado de este incremento: EN VALIDACIÓN; NO CERTIFICAR antes de los gates reales.**

## Fuente y alcance

Leídos `AGENTS.md`, Skill local `.agents/skills/crecisol-project-governance/SKILL.md`, `docs/MASTER_REFERENCE.md`, `docs/PLAN_37_CAPITULOS.md`, `docs/ESTADO_VIGENTE_CAPITULOS.md`, acta integral `docs/c05/C05_INTEGRAL_OPERATIVO_2026_11_15.md`, acta del PR #155 y código de los módulos Angular, transporte ASP.NET Core, persistencia MySQL y tests E2E. No modificar Documento Maestro, capítulo C06, SOLQARYN, producción ni datos reales.

Baseline C05 del [PR #155](https://github.com/crecisol-platform/CRECISOL/pull/155): [#43875650515](https://github.com/crecisol-platform/CRECISOL/actions/runs/43875650515) trabajo `operational` y `regressions` en SUCCESS, 294 aserciones integradas, [#43875650561](https://github.com/crecisol-platform/CRECISOL/actions/runs/43875650561) Foundation SUCCESS, [#43875650518](https://github.com/crecisol-platform/CRECISOL/actions/runs/43875650518) C03-04 SUCCESS, [#43875650493](https://github.com/crecisol-platform/CRECISOL/actions/runs/43875650493) C03-10 SUCCESS. Todos sobre el mismo SHA de `dev`.

## Hallazgos concretos de la segunda revisión

| ID | Capa | Evidencia en código | Riesgo / clasificación | Acción |
| --- | --- | --- | --- | --- |
| UX-01 | Angular /cuenta | Cuatro pestañas principales y enlaces adicionales repetían la navegación de identidad; `.account-aside` oscuro con altura mínima de 680 px | P2, jerarquía ruidosa y tarea principal postergada | Reducir navegación principal a iniciar sesión / solicitar cuenta; agrupar tareas excepcionales bajo navegación secundaria; usar superficies editoriales sobrias compatibles con la paleta C01/C02 |
| UX-02 | Registro/recuperación | Dos formularios de recuperación visibles al mismo tiempo, sin exigir un paso progresivo | P2, sobrecarga cognitiva, confusión entre solicitar token y usarlo | Mantener solicitud simple; revelar formulario de token y MFA al activar «Ya tengo mi código de recuperación», con prueba real de ambas transiciones |
| UX-03 | Vista de operador | `platform-identity-panel.component.html` contiene 18 formularios, varios de soporte institucional, MFA y revisiones; la plantilla histórica es de ~15 KB | P2, superficie muy densa; requiere extracción a submódulos sin alterar el realm | Pendiente: flujo visual de operadores en PR dedicado con actualización íntegra de pruebas adversariales de operadores. Un intento inicial de ocultar casos fue revertido porque el entorno bloqueó modificar el arnés sensible; **NO** se entrega una modificación con tests rotos |
| UX-04 | Cuenta autenticada | `identity-workspace.component.html` ~14 KB/15 formularios, aunque distribuidos por vista `vista` | P2, deuda de mantenibilidad y progresividad | Pendiente extracción de perfil, claves, seguridad, aprobaciones y sesiones a componentes/rutas de presentación reutilizables con tests E2E |
| UX-05 | Accesibilidad | El enlace de salto apuntaba a `#account-content` sin destino con foco programático; el panel mantenía un mínimo de altura artificial | P2 | Añadir `tabindex=-1` al destino, compactar vista única y seguir probando teclado/axe |
| BE-01 | API / aislamiento | `OperationalIdentityModule.cs` verifica Host/Origin/CSRF, usa `example.test` y origen loopback en integración; `MySqlOperationalIdentityFlow.cs` rechaza DSN externos | Límite de certificación, **NO** defecto de ambiente local | Mantener fail-closed, no activar producción ni reconfigurar Host/DSN para superar tests |
| BE-02 | Mantenibilidad | `MySqlOperationalIdentityFlow` refactorizado a parciales credenciales, autorizado, recuperación, sin alterar la SQL histórica | Riesgo controlado en baseline certificado | Conservar, no refactorizar criptografía y SQL por una tarea de interfaz |
| DB-01 | Persistencia | 16 migraciones `001–016` y 16 scripts de reversión; FK, restricciones y rutas por tenant en tabla; rollback probado en entorno descartable | Sin evidencia de incidencia nueva en esta revisión | No aplicar migraciones ni modificar registros del usuario; toda futura migración exige script nuevo y auditoría de integridad/rollback |
| QA-01 | Evidencia | 294 comprobaciones automatizadas cubren integración local, Chromium y varias negativas, pero ninguna prueba automática certifica una inspección visual subjetiva exhaustiva | P2 de criterio de aceptación | Revisión visual manual en 390, 768, 1366 y 1920 px con teclado, lectores de pantalla y marca oficial antes de calificar «UX comercial final» |

Clasificación: P2 identifica deuda de experiencia/mantenibilidad y no implica vulnerabilidad explotable observada. **No hay base para afirmar perfección absoluta ni garantía de producción.** No se declaró que todos los 880 blobs del repositorio recibieron revisión manual línea por línea.

## Cambios seguros de este corte
1. `frontend/src/app/pages/identity-portal.component.html/css`: jerarquía editorial de C01/C02, panel de información más liviano solo cuando se necesita; identidad autenticada y operadores usan un panel centrado sin bloque de marketing innecesario.
2. Navegación primaria de dos elecciones y vías secundarias de confirmación, recuperación, invitación y vínculo. Mantener el selector `vista` como vista visual, nunca tenant. Preservar rutas/API y permisos.
3. `frontend/src/app/pages/identity-recovery.component.html`: paso explícito para código de recuperación/MFA, conservando sus datos y validación. Sin persistencia de secretos en URL ni storage.
4. `qa/c05_operational_flow_e2e.mjs`, `frontend/tests/c05-identity-responsive.spec.ts`: probar la transición y que los campos sensibles no aparecen hasta su apertura.

**No se aceptan cambios en `backend/`, `database/`, `main` ni en las pruebas negativas de operadores en esta reparación editorial.** Las pruebas del PR #155 no se atribuyen al SHA nuevo.

## Puertas A/B/C específicas pendientes de ejecutar
- **A Funcional:** Angular registro/login/confirmar/recovery/MFA/operadores, estado challenge y logout, enlaces `vista`, y pruebas de entorno real.
- **B Seguridad:** negativa XSS/CSRF/IDOR/BOLA, Host verificado, aislamiento de tenant, cambio de sesión, revocación, backup MFA; verificar que el diff no introduce rutas ni privilegios.
- **C Arquitectura/UX:** .NET 10 compilación; Angular 22 build/types; MySQL 8.4 + rollback; Playwright Chromium responsive 390/1366, axe, test de navegación 2 niveles y ausencia de campos inaccesibles; revisión visual humana para calidad editorial.

**Regla de cierre:** ningún GO/merge con tests aplicables fallidos o sin ejecutar, sin análisis de impacto y sin A/B/C verificables. C05 conserva la certificación local histórica pero el incremento editorial nuevo sigue pendiente de su propia evidencia. C06 no inicia; COM-T08/09/10 y producción no se ejecutan.
