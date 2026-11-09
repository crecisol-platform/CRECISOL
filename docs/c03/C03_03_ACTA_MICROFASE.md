# CRECISOL — Acta de aceptación y certificación específica C03-03 · Puertos y casos de uso Application

**ESTADO DE DECISIÓN: C03-03 CERRADA TÉCNICAMENTE Y ACEPTADA EXPRESAMENTE PARA CERTIFICACIÓN (3/10); FORMALIZACIÓN EN CURSO, CONDICIONADA A CI PREMERGE/POSTMERGE DEL ACTA. C03 NO CERTIFICADO; C04 BLOQUEADO; C03-04 NO INICIADA.**

**Fecha de autorización expresa y específica del propietario: 9 de noviembre de 2026.** Una vez presentado el resultado del PR técnico #53, se indicó textualmente que «El siguiente paso es formalizar el acta de C03-03 y su aprobación específica, igual que hicimos con C03-02». El propietario respondió: **«Listo entonces adelante con el siguiente paso por favor no te detengas hasta certificar Vale gracias amigo»**. Esta aceptación **posterior a la integración técnica** autoriza cerrar y certificar **solo C03-03 en el alcance ya probado**, no autoriza C03 completo, C03-04, C04, autenticación privada, despliegues ni gastos.

## 1. Autoridad, cadena de custodia y puerta previa

- Único repositorio autorizado: `solqaryn-platform/CRECISOL`. Rama de integración: `dev`. No modificar `main`, `solqaryn/Solqaryn`, VPS, DNS ni producción.
- Documento Maestro Único Word `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx`, **Git blob `214bf9c3d9763be23a77b7e0c464359584edde46`**, **SHA-256 `e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e`**, PDF lectura Git blob `455847ae0cc3bf02b9b9c4c3435476d093918ea2`; no se modifican.
- Matriz C03 contrastada con Word: siete unidades originales, tabla original de nueve filas, **T01-R03 Application (casos de uso pequeños, validación/autorización y puertos sin MySQL directo)**; **P03/P04** árboles/límites; **DIF-C03-01** separa ocho puertas normativas `MAE-C03-01..08` de diez microfases técnicas `C03-01..10`.
- **C01 y C02 son los únicos capítulos certificados (2/37)**. **C03-01 CERTIFICADA** por PR #50, commit `8e4f25073a3f11150e4323ecb66380647f0cdc0b`, 7/7 CI POSTMERGE SUCCESS. **C03-02 CERTIFICADA** por [PR documental #52](https://github.com/solqaryn-platform/CRECISOL/pull/52), merge SHA `edb254f57f6005a84ee97588e1ec7dd659ded3d3`, **8/8 CI POSTMERGE SUCCESS**; [acta previa](C03_02_ACTA_MICROFASE.md). La aceptación de C03-02 nunca aprobó C03-03 por anticipado.

## 2. Entregable técnico y evidencias comprobadas de PR #53

- [PR técnico #53](https://github.com/solqaryn-platform/CRECISOL/pull/53), HEAD PREMERGE `4944348018bb3a1434e09eb7fdc802e269290b1b`; **fusionado mediante squash solo en `dev`** con SHA **`c3bb9e4e314297c502b644ba1306a16364e7af2e`**.
- Diff: **seis archivos nuevos**, sin alterar contratos existentes: caso de uso `AssessPlatformBrandPublicationProposal` y puerto `IPublishedPlatformBrandRevisionReader`, pruebas nuevas, especificación, auditor A/B/C y workflow. El puerto se consume **solo para evaluación interna read-only**, sin adaptador, DI, endpoint ni operación de publicación.
- [CI PREMERGE C03-03 #42713903148](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42713903148) y [Foundation #42713903003](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42713903003) terminaron `SUCCESS` sobre el HEAD técnico. Los workflows anteriores que quedaron `skipped` por filtro de rama **no se contaron como PASS**.
- **CI técnica POSTMERGE** sobre SHA `c3bb9e4e314297c502b644ba1306a16364e7af2e`: **3/3 workflows** con `event=push`, `status=completed` y `conclusion=success`, sin fallos ni skipped:

| Workflow | Evidencia real |
| --- | --- |
| [C03-03 Application A/B/C #42714098896](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42714098896) | **SUCCESS** |
| [Foundation #42714098908](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42714098908) | **SUCCESS** |
| [Regresión integral C03-01/C01/C02 #42714098950](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42714098950) | **SUCCESS** |

- **Auditoría A — PASS**: SHA real del Word, siete unidades, nueve filas y T01-R03 Application; 2/10 microfases anteriores certificadas. **Auditoría B — PASS**: Application depende solo de Domain, sin HTTP/ORM/MySQL/DI, sin ruta privada, grafo acíclico y **7/7 mutaciones adversariales** rechazadas. **Auditoría C — PASS**: compilación real **.NET 10 Release, 0 errores y 0 advertencias**; **37/37** aserciones de C03-03 Application, **19/19** Application C01, **35/35** Domain C03-02 y **51/51** Domain C01. Regresión integral del código público: **83 Chromium** y MySQL 8.4/.NET 10/Angular 22 desechables con 37 casos HTTP; sin BOLA/IDOR/MFA privados.
- [Especificación](C03_03_ESPECIFICACION.md), [mapa previo](C03_01_ESPECIFICACION.md) y [plan](../C03_PREPARACION.md) conservan la dirección `Domain <- Application <- Infrastructure <- API`.

## 3. Límites, riesgos y reversión

**No se certifican** escritura/publicación real, concurrencia atómica, autorización de usuarios, `TenantContext` verificado, BOLA/IDOR/MFA, membresía, propiedad privada de recursos, routing de tenant shared/dedicated, consola administrativa, media/archivos, workers/outbox, pagos, panel editable completo, WCAG manual, rendimiento de producción, VPS, datos reales ni producción. El preflight de Application no concede permisos; revisar una versión NO sustituye autorización ni transacción. No se considera P0/P1 conocido en los contratos acotados probados; no equivale a una auditoría global de módulos inexistentes.

**Impacto**: `frontend/` sin cambios, `database/` sin cambios ni migraciones, `backend/src/Crecisol.Domain`, `Crecisol.Infrastructure` y `Crecisol.Api` intactos; solo nuevo código `Crecisol.Application` y pruebas internas. No se introdujeron secretos, PSP ni conexiones productivas. **Rollback**: revert mediante PR contra `dev` del squash técnico `c3bb9e4e314297c502b644ba1306a16364e7af2e` y, si aplica, revert del PR documental; no hay datos que migrar ni instalaciones que revertir.

## 4. Decisión de certificación y compuertas del acta

Con tu autorización posterior **«Listo entonces adelante con el siguiente paso por favor no te detengas hasta certificar Vale gracias amigo»**, se aprueba **CERTIFICAR ÚNICAMENTE C03-03 EN SU ALCANCE APPLICATION INTERNO**, sin abrir otra microfase.

**Formalización en curso:** mientras se tramita el PR documental, el estado real es **C03-03 CERRADA TÉCNICAMENTE, ACEPTADA PARA CERTIFICACIÓN, CONDICIONADA A CI PREMERGE y CI POSTMERGE DEL ACTA**; no se declara certificada antes de verificar ambas CI sobre los SHA precisos y todas las regresiones obligatorias. La CI del PR técnico #53 no sustituye la CI de certificación.

El control `qa/c03_03_closure_gate.py` debe comprobar evidencia real A/B/C y compilación, autorización específica, SHA técnico, Word, 37/19/35/51 pruebas, 7/7 mutaciones Domain/Application, 3/3 workflows técnicos y rechazo de **12/12 alteraciones adversariales del acta**. Las compuertas C03-01/C03-02 y C02 deben preservar sus actas históricas y bloquear saltos al capítulo o a C03-04/C04. La aceptación definitiva se registra mediante comentario de cierre del PR con los SHA y las URL PREMERGE/POSTMERGE del acta; el archivo no inventa SHA futuro.

**Solo tras CI final correcta: C03-01, C03-02 y C03-03 CERTIFICADAS (3/10), C03 NO CERTIFICADO; C03-04 HABILITABLE PARA PLANIFICAR, NO INICIADA; C04 BLOQUEADO; 2/37 capítulos certificados (C01 y C02).**
