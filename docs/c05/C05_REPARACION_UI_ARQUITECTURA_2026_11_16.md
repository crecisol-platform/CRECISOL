# C05 — Reparación de mantenibilidad, experiencia y arquitectura (16-11-2026)

**Tipo:** hallazgos de mantenimiento y UX post-certificación local, **NO** reapertura ni revocación automática de las once microfases históricas. Este documento corresponde al PR #155 en **DRAFT**; **no certifica** la nueva revisión. No modifica el Documento Maestro Único.

## Autoridad y corte

- Repositorio autorizado ID 1450414592: `crecisol-platform/CRECISOL`, rama base `dev` en `403dd2088b7d83b6be27811a7649340aeffc5c79` (PR #154 ya fusionado). `main` permanecía en `b794181148d2c5e36c1c88127f19a8a9082acff5`.
- `AGENTS.md`, `.agents/skills/crecisol-project-governance/SKILL.md`, `docs/MASTER_REFERENCE.md`, el índice de 37 capítulos y el acta `docs/c05/C05_INTEGRAL_OPERATIVO_2026_11_15.md` consultados.
- Según el acta, C05 fue certificado **en código y desarrollo local aislado** con 294 comprobaciones, no en producción; las nuevas modificaciones requieren pruebas propias. C06 sigue bloqueado. COM-T08/09/10 NO EJECUTADAS.
- Las capturas aportadas por el propietario muestran una interfaz pública desproporcionadamente larga, con formularios de acciones distintas apilados. Esto no se resuelve únicamente ampliando botones.

## Inventario y conclusiones verificadas

| Capa | Situación observada en `dev` | Tratamiento de este PR |
| --- | --- | --- |
| Angular público | `identity-portal.component.ts`, ~36.8 KB / 474 líneas con registro, login, verificación, recuperación, invitaciones, sesión, perfil, MFA y permisos | Separar shell/plantillas/componentes y navegación por vistas, sin rutas privadas nuevas; mejorar layout y responsive |
| Angular operadores | `platform-identity-panel.component.ts`, ~20 KB mezclado visualmente al final de la página de clientes | Separar plantilla, mantener realm independiente y sección visual explícita sin publicar funciones privilegiadas nuevas |
| Angular API | `identity-api.service.ts`, ~17.7 KB / 378 líneas; `platform-identity-api.service.ts`, ~10 KB; protección mediante cookies HttpOnly, CSRF y estados | Conservar contratos y transportes por ahora; futuras divisiones exigen matriz de efectos/seguridad y regresiones |
| Infraestructura .NET | `MySqlOperationalIdentityFlow.cs` ~48.2 KB / 691 líneas, ya usa otras clases `partial` | Extraer mecánicamente métodos login/MFA, sesión autorizada y recuperación a archivos parciales; no cambiar SQL ni algoritmos |
| Transporte API | `OperationalIdentityModule.cs` ~15 KB; catálogo explícito de acciones y protección deny-by-default | Preservar sin cambiar la semántica de admisión / transporte |
| MySQL 8.4 | Migraciones operativas 001–016 más 16 down scripts; FKs, checks, claves por tenant; tablas PLATFORM separadas | **Sin cambios de esquema**: no reescribir migraciones aplicadas por limpieza cosmética; cualquier cambio futuro debe ser nueva migración probada |
| QA | `qa/c05_operational_flow_e2e.mjs` ~58 KB y suites auxiliares ya modularizadas; E2E histórico dependía de formularios simultáneamente visibles | Ajustar navegación entre etapas y probar autenticación y abuso real con Angular/Kestrel/MySQL; conservar ataques y negativas |

## Principios de cambio

1. Mantener `/cuenta` como la ruta lazy allowlist existente por compatibilidad con C02/C03 y validadores. La vista pública se escoge por `vista`, **nunca** la empresa/tenant.
2. El acceso visual a operadores no es autorización: el servidor comprueba el realm, Host y permisos siempre.
3. El token, CSRF, MFA, claves WebAuthn y capacidades de recuperación no se guardan en almacenamiento persistente del navegador ni se incorporan a query params.
4. Sin SQL nuevo, sin tocar datos del propietario, sin `main`, SOLQARYN, despliegue, servicios externos ni cobros.
5. No eliminar ni debilitar pruebas adversariales (XSS, IDOR, concurrencia, revocación, CSRF y MFA). Los E2E deben navegar de la pantalla de registro a login, no asumir que los formularios están todos montados simultáneamente.
6. El diseño C05 exige legibilidad, jerarquía, contraste, móvil, teclado y estados. El constructor universal visual del C09 permanece fuera de alcance.

## Puertas y estado de auditorías

- **A / Funcional:** pendiente de nueva CI `qa/c05_operational_flow_e2e.mjs` íntegra en SHA final y pruebas de navegación individual; las 294 aserciones previas **no** pueden atribuirse a este PR.
- **B / Seguridad:** pendientes los mismos recorridos reales MySQL/API, regresiones históricas, XSS/tenant y observación de roles. Ningún control backend o migración fue rebajado intencionadamente.
- **C / Arquitectura y UX:** extracción declarativa, build Angular 22 y .NET 10, prueba móvil+desktop, axe, ausencia de scroll horizontal, tests C01–C04 y revisión manual de interfaz por el propietario.
- **NO-GO:** no fusionar PR #155 si quedan fallos P0/P1, tests relevantes omitidos, formularios inaccesibles, permisos debilitados o evidencias no ejecutadas. Mantener DRAFT y documentar cualquier fallo inicial sin reescribir historia.
- **Rollback:** descartar rama/PR #155 sin migraciones ni datos modificados. Si se fusiona tras aprobación, revertir sus commits de interfaz y refactor sin modificar SQL; rerun QA previa.

**Resultado actual:** reparación implementada en rama de trabajo, pruebas del nuevo SHA pendientes de aprobación. No declarar certificación adicional.
