# CRECISOL — C04-12 · Acta integral A/B/C CONDICIONADA

**Nota institucional posterior de precedencia:** este documento conserva el estado histórico de preparación y prueba C04-12; no constituye el estado actual después de las puertas originales. La [ratificación institucional propuesta](C04_12_CERTIFICACION_INSTITUCIONAL.md) registra la auditoría del capítulo y sus SHA/run IDs verificados, además de las pruebas aún pendientes de aprobación. Su eficacia se limita al momento en que su PR tenga CI PREMERGE y POSTMERGE todas SUCCESS, fusión solo en dev y comentario institucional permanente. Las afirmaciones «pendiente» o «no certificado» del cuerpo siguiente corresponden a su fecha histórica y **no anulan una ratificación futura efectivamente aprobada**. Hasta esa eficacia, C04 completo y C05 siguen bloqueados.


**Estado inicial:** C04-12 **EN AUDITORÍA / NO CERTIFICADA**. **El capítulo C04 NO está certificado** hasta completar todas las puertas. **Fecha:** 12-11-2026 (Honduras). **Repo:** `crecisol-platform/CRECISOL` (ID 1450414592). **Rama:** `dev`, base de entrada `90c63108f5d9b38dd57399395b88af215e08b6ec`.

## 1. Autoridad, precedencia y requisitos exactos

El único Documento Maestro es `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx`, con huella SHA-256 aprobada `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c`, PDF asociado SHA-256 `0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77`. Gobierno: `docs/MASTER_REFERENCE.md`, `docs/PLAN_37_CAPITULOS.md`, `docs/planificacion-futura/C04_MICROFASES_PROPUESTAS.md`, `docs/SEGURIDAD_MULTITENANT.md`, .agents/skills/crecisol-project-governance/SKILL.md y AGENTS.md.

C04-11 tiene [dictamen definitivo](https://github.com/crecisol-platform/CRECISOL/pull/108#issuecomment-6842233364): **22/22 workflows obligatorios SUCCESS**, última SHA `90c63108f5d9b38dd57399395b88af215e08b6ec`, únicamente pruebas ofensivas sintéticas. La fila C04-12 exige «Auditorías integrales y acta C04», evidencia «A/B/C, matriz de fallos P0 y aceptación» y negativa «No habilitar C05 con fugas abiertas». La matriz propuesta `C04_12_MATRIZ_RIESGOS.md` contiene quince barreras R04-01..15 a validar, sin darles PASS anticipadamente.

## 2. Cobertura de las once microfases y alcance estrictamente verificable

| Fase | Acta y control del capítulo | Estado de evidencia |
| --- | --- | --- |
| C04-01 | [Catálogo confiable](C04_01_ACTA_CONTRATOS.md), nombres sin DSN y Host sin alta arbitraria | Certificación previa en alcance de contratos |
| C04-02 | [Modelo shared](C04_02_ACTA_CERTIFICACION.md), FK compuestas MySQL 8.4 y permisos DB | Certificación previa en ámbito sintético |
| C04-03 | [Placement dedicado](C04_03_ACTA_CERTIFICACION.md), slot confiable y no DSN usuario | Certificación previa de puertos |
| C04-04 | [Hosts verificados](C04_04_ACTA_CERTIFICACION.md), allowlist, identidad de dominio | Certificación previa de contratos/adaptador de ensayo |
| C04-05 | [Identidad contextual](C04_05_ACTA_CERTIFICACION.md), sesión/membresía reales del backend en contratos | Certificación previa sintética |
| C04-06 | [Scopes branch/resource](C04_06_ACTA_CERTIFICACION.md), acción/grant/owner | Certificación previa sintética |
| C04-07 | [Provisión idempotente](C04_07_ACTA_CERTIFICACION.md), carreras/replay/rollback | Certificación previa de contratos y MySQL desechable |
| C04-08 | [Lifecycle](C04_08_ACTA_CERTIFICACION.md), suspensión/reactivación/epoch | Certificación previa sintética |
| C04-09 | [Migraciones](C04_09_ACTA_CERTIFICACION.md), Shared/Dedicated/fencing/dump restore | Certificación previa de compatibilidad aislada |
| C04-10 | [Artefactos privados](C04_10_CERTIFICACION_INSTITUCIONAL.md), HMAC/cache/job/media/export y mutaciones | Certificación institucional solo interna/sintética |
| C04-11 | [Ofensiva](C04_11_CERTIFICACION_INSTITUCIONAL.md), BOLA/IDOR/host/claims/DSN | Certificación institucional ofensiva únicamente sintética |
| C04-12 | A/B/C transversal y acta final de C04 | **PENDIENTE DE VERIFICACIÓN COMPLETA** |

No se reabren ni se editan actas históricas para convertir estados fallidos o `skipped` en PASS. Solo los nuevos runs sobre el SHA final determinan si el capítulo puede cerrarse.

## 3. Tres auditorías integrales obligatorias

**A — Funcional y trazabilidad:** contrastar Word/PDF exactos SHA-256, plan C04 y fila de microfase, once actas y contratos/puertos, regresión .NET10 Release warnaserror, evidencia de operación positiva y negativa de los once ámbitos. **No equivalen contratos a APIs privadas operativas.**

**B — Aislamiento y privacidad:** amenazas R04-01..15 de `C04_12_MATRIZ_RIESGOS.md`; claves, IDOR/BOLA, hosts/claims/DSN falsificados, propietarios/grants/revisiones, epoch tras suspensión, provisión y trabajos concurrentes, claves media/export opacas y revocadas, migración/backup/restore sin cruce; ataques .NET y nueve fixtures MySQL 8.4 aislados. Cualquier lectura/escritura cross-tenant encontrada P0 es NO-GO.

**C — Arquitectura, rendimiento/UX y dependencias:** Domain/Application/Infrastructure/API separados, .NET10/Angular22/Node24/Chromium y C01-C03/C04 en regresión, ejecución MySQL sintética sin credenciales reales, integridad de material de identidad de marca, ausencia de despliegue/secretos/alcance productivo, instrucciones de rollback y evidencia del mismo SHA/run. Criterio absoluto: `completed/success` para TODAS las obligaciones; `skipped` nunca cuenta como PASS.

## 4. Evidencia por completar (no presumir resultados)

- **PR técnico:** [#109](https://github.com/crecisol-platform/CRECISOL/pull/109) DRAFT; desarrolla únicamente `qa/c04_12_integral_audits.py` y `.github/workflows/c04-12-integral.yml`. La CI debe revisar once suites .NET, nueve fixtures MySQL8.4, Angular22/Chromium, matriz R04 y A/B/C. **El SHA y todos los run IDs finales deben añadirse después de comprobarlos.**
- **PR documental:** [#110](https://github.com/crecisol-platform/CRECISOL/pull/110) DRAFT; contiene especificación, matriz de riesgos y este acta, sin cambiar el Documento Maestro. **Sin CI PREMERGE/POSTMERGE no será eficaz.**
- **Trazabilidad de errores:** los fallos previos a la corrección no se borran ni reinterpretan. Cada remediación deberá comprobarse con nuevo commit y nueva ejecución.
- **PR institucional posterior:** solo se abrirá cuando las cuatro puertas anteriores estén completas, con aceptación de alcance y estado nuevo de C04; precisará su propia CI PREMERGE/POSTMERGE y comentario permanente antes de habilitar C05. No declarar `4/37` antes de la ratificación eficaz.

## 4.1 Evidencia PREMERGE técnica completa y POSTMERGE técnico iniciado

El PR técnico [#109](https://github.com/crecisol-platform/CRECISOL/pull/109) cerró HEAD `8d5cff2c04da59e2f1494d7ab2f45700ca6c32ef` con **2/2 workflows aplicables completed/success**: [C04-12 integral A/B/C .NET10/Angular22/Chromium/MySQL8.4 — 43359338930](https://github.com/crecisol-platform/CRECISOL/actions/runs/43359338930) y [Foundation — 43359338915](https://github.com/crecisol-platform/CRECISOL/actions/runs/43359338915). El fallo histórico [43359223379](https://github.com/crecisol-platform/CRECISOL/actions/runs/43359223379) detectó una aserción de texto de estado (`22/22` frente al historial escrito `14/14`), NO una fuga; se corrigió en nuevo SHA sin reescribir el historial. El gate final verificó también Word/PDF SHA-256 exactos, plan C04 12/12, once suites de auditoría, veinte proyectos .NET10 Release, Angular22/Chromium y nueve runners MySQL8.4 reales en entorno sintético. Los resultados no certifican operaciones productivas.

Tras revisar diff exacto (solamente `qa/c04_12_integral_audits.py` y `.github/workflows/c04-12-integral.yml`, sin cambios en frontend/backend/product database), GitHub fusionó #109 por squash **solo a `dev`**, SHA **`6ec93e516c32ce3aa23c641eb6f36423ec21049c`**; `main` no se tocó.

**POSTMERGE técnico iniciado, todavía NO considerado PASS al registrar:** [C04-12 integral — 43360186955](https://github.com/crecisol-platform/CRECISOL/actions/runs/43360186955) y [Foundation — 43360186941](https://github.com/crecisol-platform/CRECISOL/actions/runs/43360186941). Exigir `completed/success` de ambos en este mismo SHA antes de fusionar documentación. El acta permanece **CONDICIONADA / NO EFICAZ**. No se presume ninguna futura certificación ni habilitación de C05.

## 5. Límites y condiciones de NO-GO

**Prohibido extrapolar** la certificación a login/MFA/JWT, permisos productivos de SuperAdmin, DSN router físico, Cloudflare, R2/Stream, Redis, workers tenant, exportadores, backups productivos, activaciones de pagos ni DNS. La integración real de C05/C06/C07 y posteriores deberá validar estos sistemas cuando existan y repetir ofensiva. Las pruebas COM-T06/T07/T09/T17 siguen **DEFINIDAS / NO EJECUTADAS**.

**ANTES del dictamen:** C04-12 NO CERTIFICADA, C04 COMPLETO NO CERTIFICADO, C05 BLOQUEADO, 3/37 capítulos completos. **DESPUÉS SOLO SI PRUEBAS + 3 AUDITORÍAS + ACTA + ACEPTACIÓN + CI Y RATIFICACIÓN PROPIAS QUEDAN 100% VERDE:** capítulo C04 CERTIFICADO **SOLAMENTE** para arquitectura, contratos internos y fixtures sintéticos; C04-01..12 12/12 dentro de sus alcances; C05 habilitado SOLO PARA PREPARACIÓN; capítulos completos C01-C04 4/37. Si falta algo: NO-GO.

No cambiar `main`, Word/PDF canónicos, producción, VPS, DNS, Cloudflare, cobros ni `solqaryn/Solqaryn`; reversión por PR/squash exactos solo tras análisis del diff, sin migración de datos del SaaS.
