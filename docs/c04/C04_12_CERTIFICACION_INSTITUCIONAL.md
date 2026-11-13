# CRECISOL — C04-12 · Ratificación institucional del capítulo 04 (alcance interno/sintético)

**Fecha de preparación:** 12 de noviembre de 2026, Honduras. **Repositorio autorizado:** `crecisol-platform/CRECISOL` (ID 1450414592). **Integración única:** `dev`. **Estado del documento hasta sus últimas puertas:** ratificación **CONDICIONADA, NO EFICAZ**. No declarar capítulo C04 certificado mientras no estén concluidas y verificadas la CI PREMERGE propia, fusión en `dev`, CI POSTMERGE propia y comentario institucional permanente sobre el SHA final. La firma/autorización operativa del propietario para continuar no sustituye pruebas pendientes ni crea capacidad productiva.

## 1. Alcance y autoridad canónica

- **Documento Maestro Único inmutable:** `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx`, Git blob SHA-1 `fe7aaab60b603f1844e18666e2f99c0ea97e86e9`, SHA-256 **`ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c`**; PDF de lectura Git blob SHA-1 `93c8412b2450e85a882b622e7a012477c61c2359`, SHA-256 **`0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77`**. Los SHA-256/Word XML se comprobaron realmente en el gate integral C04-12; no se editan las fuentes.
- `docs/MASTER_REFERENCE.md`, `docs/PLAN_37_CAPITULOS.md`, `docs/planificacion-futura/C04_MICROFASES_PROPUESTAS.md`, `docs/SEGURIDAD_MULTITENANT.md`, `.agents/skills/crecisol-project-governance/SKILL.md` y `AGENTS.md` siguen gobernando la microfase.
- C04-12 es «Auditorías integrales y acta C04»; positivo: «A/B/C, matriz de fallos P0 y aceptación»; negativo absoluto: «No habilitar C05 con fugas abiertas». La aprobación de once microfases previas NO implica automáticamente aprobar el capítulo.
- **Puerta precedente C04-11:** [dictamen institucional PR #108, comentario 6842233364](https://github.com/crecisol-platform/CRECISOL/pull/108#issuecomment-6842233364), **22/22 CI** en seis puertas, `dev` `90c63108f5d9b38dd57399395b88af215e08b6ec`, solo pruebas ofensivas en contratos/fixtures sintéticos. Los registros de 14/14 de ese capítulo son HISTÓRICOS, no anulan la certificación posterior de 22/22.

## 2. Las doce microfases y barreras probadas

| Microfase | Evidencia concreta dentro del ámbito permitido | Prohibición/negativa bloqueante |
| --- | --- | --- |
| C04-01 | Catálogo tipado `Domain/Application`, estados, denominación no sensible | Host/slug no crea tenant; secretos/DSN no expuestos |
| C04-02 | `Shared` SQL MySQL 8.4, FK y UNIQUE compuestos, lector backend con permisos limitados | No cruza identidad ni lectura/escritura entre A y B |
| C04-03 | `Dedicated`: puertos/slot tipados y elección únicamente del servidor | URL/header/cookie/claims no deciden DSN |
| C04-04 | Host verificado con parser/allowlist/estado temporal, prueba de adapter HTTP contextual | No Forwarded arbitrario, host expirado/ambiguo |
| C04-05 | Identidad contextual `TenantId` + sesión/actor/membresía del lector de confianza | tenant_id/claim del cliente nunca es permiso |
| C04-06 | Scope de sucursal, recurso, actor, owner, acción/grant/revisión | No BOLA/IDOR ni herencia de otra sucursal |
| C04-07 | Provisión idempotente, replay exacto, rollback ante falla y 12-way InnoDB | Un negocio no crea dos tenants ni replay falsificado |
| C04-08 | Lifecycle con suspensión/reactivación/borrado lógico/epoch/retención | Sin acceso residual ni pérdida de datos prematura |
| C04-09 | Migraciones `Shared/Dedicated` en DB desechable, fencing y `mysqldump/restore` | No actuar sobre tenant/slot/backup ajeno |
| C04-10 | HMAC de claves/artefactos: caché, jobs, referencias de medios, exportaciones, 12-way job | Sin cruce A/B ni clave como autorización |
| C04-11 | Pruebas ofensivas BOLA/IDOR, Host/claims/DSN falsificados, SQL `SELECT/UPDATE/DELETE/DDL` | 0 modificaciones de filas del tenant B en el fixture |
| C04-12 | Pruebas independientes integrales A/B/C, Word/PDF SHA, `.NET10`, Angular22/Chromium, **nueve fixtures MySQL8.4** | No certificar C04 ni preparar C05 con P0/P1 del ámbito probado o gates pendientes |

## 3. Tres auditorías finales y matriz de hallazgos P0/P1

**A — Normativa/funcional:** validación exacta SHA-256 y DOCX XML del maestro, doce filas C04, once actas técnicas/históricas, catorce proyectos de contrato básicos exigidos (20 proyectos compilados en CI), once auditorías A históricas reejecutadas y pruebas de contratos con resultados reales. Se exige consistencia de alcance, trazabilidad y aceptación según gobierno.

**B — Seguridad transversal:** catálogo/host/sesión/actor/branch/resource, permisos por backend, Shared/Dedicated, aprovisionamiento, lifecycle, migración y recuperación, cache/job/media/export y ofensiva A/B, carreras/rollback y pruebas negativas reales; auditorías B históricas más guardas y mutaciones estáticas (no sustituir pentest por texto). Las quince barreras `R04-01..R04-15` se documentan en `docs/c04/C04_12_MATRIZ_RIESGOS.md`; su columna `PENDIENTE DE CI` refleja la preparación histórica. **Cualquier fuga o P0/P1 en contratos/fixtures realmente ensayados impide el cierre.**

**C — Clean Architecture, rendimiento, calidad y regresiones:** `Domain/Application/Infrastructure/API` sin alteraciones de producto, compilación `.NET10 Release -warnaserror`, **Node24/Angular22**, TypeScript, npm audit, tests de assets, Playwright/Chromium públicos y accesibilidad automatizada, nueve suites de MySQL8.4 más C03 PLATFORM y API pública, control de redes/puertos de Docker aislado/loopback. Comprueba A/B/.NET/MySQL/frontend/Chromium **del mismo SHA/run** y prohíbe desplegar, crear DSN de clientes o conexiones productivas.

**Riesgos residuales excluidos y NO certificados:** autenticación JWT/MFA/passkeys privada, autorización de datos verdaderos y acceso privado excepcional SuperAdmin, router/DSN físico de clientes, colas/workers de tenant, Redis/CDN y caché real, Cloudflare R2/Stream, firmas de media, descarga/exportación de clientes, backups reales, DNS/domains comerciales, altas/cobros, COM-001..014 y sus COM-T06/T07/T09/T17 **no ejecutadas**. La futura integración/penetración real de C05/C06/C07/C08+ continúa OBLIGATORIA. Decir «C04 certificado» sin calificar su ámbito como **contratos y fixtures sintéticos** sería materialmente incorrecto.

## 4. Evidencia verificable en GitHub y cuatro puertas originales

**PR técnico [#109](https://github.com/crecisol-platform/CRECISOL/pull/109)**, HEAD `8d5cff2c04da59e2f1494d7ab2f45700ca6c32ef`, diff exclusivamente `.github/workflows/c04-12-integral.yml` y `qa/c04_12_integral_audits.py`, ningún cambio productivo. 

**Puerta técnica PREMERGE: 2/2 completed/success** — [integral C04-12 — run 43359338930](https://github.com/crecisol-platform/CRECISOL/actions/runs/43359338930) y [Foundation — run 43359338915](https://github.com/crecisol-platform/CRECISOL/actions/runs/43359338915). Verificó Word/PDF, 11 suites A/B, 20 compilaciones y tests `.NET10`, Node24/Angular22/Chromium, nueve MySQL8.4 y A/B/C. Fallo histórico run [43359223379](https://github.com/crecisol-platform/CRECISOL/actions/runs/43359223379) por aserción de texto de estado (`22/22` vs `14/14` histórico) quedó preservado y corregido en nuevo SHA; no fue una fuga; **el run FAILURE histórico NO se contabiliza como PASS**.

**Puerta técnica POSTMERGE: 2/2 completed/success** — `dev` squash `6ec93e516c32ce3aa23c641eb6f36423ec21049c`; [integral — run 43360186955](https://github.com/crecisol-platform/CRECISOL/actions/runs/43360186955) y [Foundation — run 43360186941](https://github.com/crecisol-platform/CRECISOL/actions/runs/43360186941). Repetición completa y misma cadena QA sin fallos; `main` quedó intacta.

**PR documental [#110](https://github.com/crecisol-platform/CRECISOL/pull/110)**, HEAD `3bc95b9cb0a00461e29e4f1d74a408a0181ede07`, cuatro archivos: `C04_12_ESPECIFICACION.md`, `C04_12_MATRIZ_RIESGOS.md`, `C04_12_ACTA_CONDICIONADA.md`, `ESTADO_VIGENTE_CAPITULOS.md`.

**Puerta documental PREMERGE: 4/4 completed/success** — [C04-12 integral 43360506955](https://github.com/crecisol-platform/CRECISOL/actions/runs/43360506955), [C04-02 MySQL 43360506895](https://github.com/crecisol-platform/CRECISOL/actions/runs/43360506895), [C03 institucional 43360506801](https://github.com/crecisol-platform/CRECISOL/actions/runs/43360506801), [Foundation 43360506783](https://github.com/crecisol-platform/CRECISOL/actions/runs/43360506783). Job condicional C01-02 `skipped` se excluye como no aplicable, **no suma SUCCESS**.

**Puerta documental POSTMERGE:** PR #110 fusionado exclusivamente en `dev`, squash **`88fcfc62cbe059f85c00e88afccbbf03879b6cf7`**. **Ejecuciones iniciadas pero NO PRESUPUESTAS SUCCESS al redactar**: C04-12 integral [43361264045](https://github.com/crecisol-platform/CRECISOL/actions/runs/43361264045); C04-02 MySQL [43361263928](https://github.com/crecisol-platform/CRECISOL/actions/runs/43361263928); C03 institucional [43361263922](https://github.com/crecisol-platform/CRECISOL/actions/runs/43361263922), Foundation por confirmar. Deben concluir todas en `completed/success`; no declarar **12/12** ni C04 certificado sin ese requisito y la siguiente ratificación.

## 5. Ratificación adicional propia y dictamen de efecto limitado

Este documento se incorpora mediante PR de **ratificación institucional separada** sobre el squash documental. La ratificación **no es eficaz** sin:
1. comprobar documental POSTMERGE de #110 en su SHA final (4/4 aplicables con 0 errores);
2. CI PREMERGE de ratificación sobre el HEAD exacto, 100% completada y SUCCESS, A/B/C y regresiones reales del capítulo;
3. fusión de ratificación **solo a `dev`** tras revisión exacta del diff, sin conflicto y sin tocar `main`;
4. CI POSTMERGE de ratificación sobre SHA final `dev`, 100% `completed/success`;
5. comentario institucional permanente en su PR con TODOS los SHAs y los IDs de las seis puertas, matriz y límites; volver a cotejar `dev` y `main`.

**Estado hasta completar TODOS:** C04-12 NO CERTIFICADA; C04 capítulo NO CERTIFICADO (11/12); C05 BLOQUEADO; capítulos completos 3/37. **Solo después, y únicamente en alcance de arquitectura y contratos internos/fixtures sintéticos:** **C04-12 CERTIFICADA, C04 12/12 microfases certificadas, capítulo 04 CERTIFICADO exclusivamente en sus límites documentados, C01–C04 = 4/37 capítulos completos; C05 habilitado SOLAMENTE PARA PREPARACIÓN**. No produce una plataforma lista para producción, y no habilita cobros, dominio de clientes ni VPS.

Reversión por SHA de PR #109 `6ec93e516c32ce3aa23c641eb6f36423ec21049c`, PR #110 `88fcfc62cbe059f85c00e88afccbbf03879b6cf7` y eventual ratificación, únicamente en `dev`, sujeto a análisis de dependencias. Nunca editar `solqaryn/Solqaryn`, `main`, Documento Maestro, Cloudflare, VPS o producción.
