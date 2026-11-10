# CRECISOL — Acta de aceptación y certificación específica C03-04 · Adaptador Infrastructure reemplazable

**ESTADO DE DECISIÓN: C03-04 CERRADA TÉCNICAMENTE Y ACEPTADA EXPRESAMENTE PARA CERTIFICACIÓN (4/10); FORMALIZACIÓN EN CURSO, CONDICIONADA A CI PREMERGE Y POSTMERGE DEL ACTA. C03 NO CERTIFICADO; C04 BLOQUEADO; C03-05 NO INICIADA.**

**Fecha de autorización específica posterior: 9 de noviembre de 2026.** Una vez comunicada la integración técnica y las **3/3 CI POSTMERGE SUCCESS** del [PR técnico #55](https://github.com/solqaryn-platform/CRECISOL/pull/55), se explicó «El siguiente paso es formalizar el acta y certificar específicamente C03-04, con tu aprobación posterior a estas pruebas y la CI propia del acta». El propietario respondió literalmente: **«Adelante por favor con ello»**. Esta aceptación posterior es **solo para C03-04**, no para C03 completo, C03-05, C04, autenticación privada, publicación efectiva, producción ni servicios externos. Se distingue de la autorización anterior «Adelante con ello por favor», que habilitaba iniciar el trabajo técnico, pero **no** certificarlo.

## 1. Procedencia y secuencia autorizada

- Único repositorio: `solqaryn-platform/CRECISOL`, rama de integración `dev`; sin escritura en `main`, `solqaryn/Solqaryn`, VPS o producción.
- Documento Maestro Único Word `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx`: **SHA-256 `e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e`** y blob `214bf9c3d9763be23a77b7e0c464359584edde46`; copia de lectura PDF blob `455847ae0cc3bf02b9b9c4c3435476d093918ea2`; no se modifican.
- Fuente C03 original verificada en CI: siete unidades textuales originales, una tabla de nueve filas (cabecera y ocho capacidades), **T01-R04 Infrastructure** (adaptadores MySQL, correo, pagos, medios, social y reloj, reemplazables), **T01-R03 Application** (puertos sin MySQL directo), P03/P04 límites y árbol. **DIF-C03-01**: `MAE-C03-01..08` son ocho puertas de proceso; `C03-01..10` diez microfases técnicas. Esta acta solo trata la cuarta.
- Cadena previa: C03-01 CERTIFICADA [PR #50](https://github.com/solqaryn-platform/CRECISOL/pull/50); C03-02 CERTIFICADA [PR #52](https://github.com/solqaryn-platform/CRECISOL/pull/52); **C03-03 CERTIFICADA (3/10)** mediante [PR documental #54](https://github.com/solqaryn-platform/CRECISOL/pull/54), `dev` SHA `1481dfbf75b3b47c8d5bc9d7bea56406e470f215`, **9/9 CI POSTMERGE SUCCESS**. Las actas previas se mantienen intactas. Los únicos capítulos certificados son **C01 y C02 (2/37)**.

## 2. Implementación técnica y evidencias reales del PR #55

- [PR técnico #55](https://github.com/solqaryn-platform/CRECISOL/pull/55), HEAD PREMERGE **`0bed4e82fee2459ef971140ff0d2de8a0eeb296a`**, fusionado por squash exclusivamente en `dev` SHA definitivo **`d529f85f0bc8710940d195fceaea06bf6c5e674d`**. El SHA de `refs/heads/dev` coincidió con el merge.
- Diff de siete archivos **nuevos**, sin alterar código existente: `backend/src/Crecisol.Infrastructure/Branding/MySqlPublishedPlatformBrandRevisionReader.cs`; proyecto y programa `backend/tests/Crecisol.Infrastructure.ContractsTests/`; `database/tests/c03_04_revision_reader_smoke.sh`; [especificación](C03_04_ESPECIFICACION.md); `qa/c03_04_infrastructure_audits.py`; workflow `.github/workflows/c03-04-infrastructure.yml`. No hay migraciones, tablas, secrets ni nuevas rutas.
- El adaptador implementa `IPublishedPlatformBrandRevisionReader` de Application: **consulta SQL constante SELECT-only de revisión publicada PLATFORM** con scope fijo, join de publicación/revisión y validación de status. No registra DI/API ni autoriza operaciones. El puntero publicado nunca se sustituye por borrador, tenant o caché.

**Gate técnico PREMERGE** sobre el HEAD anterior: [C03-04 A/B/C/MySQL #42722310989](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42722310989) — **SUCCESS** y [Foundation #42722310053](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42722310053) — **SUCCESS**. Dos workflows filtrados de C01-04/C03-01 resultaron `skipped`; no se contaron como PASS.

**Gate técnico POSTMERGE** sobre SHA `d529f85f0bc8710940d195fceaea06bf6c5e674d`: **3/3 workflows** con `event=push`, `status=completed`, `conclusion=success`, sin fallos ni skips:

| Workflow técnico | Evidencia |
| --- | --- |
| [C03-04 A/B/C + MySQL 8.4 #42722550261](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42722550261) | **SUCCESS** |
| [C03-01 regresión integral #42722550413](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42722550413) | **SUCCESS** |
| [Foundation #42722550228](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42722550228) | **SUCCESS** |

**Auditoría A — PASS:** Word real, SHA y T01-R04, 7 unidades/9 filas, precedencia C03-03 9/9. **Auditoría B — PASS:** Infrastructure→Application→Domain, SQL SELECT-only PLATFORM, sin HTTP/DSN de navegador, sin DI/API privada; **10/10 mutaciones adversariales rechazadas**. **Auditoría C — PASS:** compilación .NET 10 Release **0 warnings, 0 errors**, **12 contratos ejecutables** del adaptador, MySQL 8.4 real desechable `--network none` + socket UNIX, usuario de lectura con `SELECT` restringido y `UPDATE` denegado, `SELECT` revocado y error seguro, puntero de versión cambiante, borradores/tenant excluidos, publicación ausente sin fallback. Regresiones: **37/37 C03-03 Application, 19/19 C01 Application, 35/35 C03-02 Domain, 51/51 C01 Domain** (142 aserciones) y **83 Chromium** + integración pública MySQL/API/Angular con **37 casos HTTP**. Ninguna prueba de entorno desechable se interpreta como operación productiva.

## 3. Fronteras P0, impacto y reversión

**No se certifican** publicación o escritura real, transacción/CAS de versión, permisos sobre datos privados, `TenantContext` verificado, BOLA/IDOR/MFA, router shared/dedicated, medios/archivos de tenants, consola administrativa, workers/outbox, pagos, WCAG manual, benchmark productivo ni aislamiento de múltiples tenants con autenticación. El adaptador solo lee la revisión de PLATFORM y no se conecta a ninguna API nueva. La cadena de conexión se entrega desde código de confianza; el test usa `CRECISOL_C03_04_TEST_DSN` sintético y efímero, nunca desde Host/claims/browser. El `SELECT` por sí mismo **no es control de autorización**.

**Impacto real:** `frontend/`, `backend/src/Crecisol.Domain`, `backend/src/Crecisol.Application`, `backend/src/Crecisol.Api`, tablas y DDL **sin modificaciones**. Bajo `database/` únicamente un test local, no un cambio de esquema. `main`, SOLQARYN, VPS, DNS, cobros y producción intactos. **Rollback:** revert del squash técnico `d529f85f0bc8710940d195fceaea06bf6c5e674d` mediante nuevo PR a `dev`, y revert del PR documental si fuese necesario; sin migración, secretos o datos reales que restaurar.

## 4. Aceptación y puerta formal de certificación

Se aprueba **CERTIFICAR ÚNICAMENTE C03-04 EN SU ALCANCE INFRASTRUCTURE DE SOLO LECTURA**, con autorización humana literal posterior **«Adelante por favor con ello»** (9 de noviembre de 2026). La aprobación **no** extiende el capítulo ni habilita implementar C03-05.

El archivo se crea durante la **FORMALIZACIÓN EN CURSO**; aún no prueba que las futuras CI del acta hayan pasado. El gate `qa/c03_04_closure_gate.py` comprueba A/B/C sobre ejecución real, Word y checksum, autorización, commits y runs técnicos, actas anteriores, estado 4/10 acotado y **12/12 alteraciones adversariales de certificación rechazadas**. Las CI `PREMERGE y POSTMERGE` del propio PR de certificación, sobre HEAD y SHA de integración exactos, deberán verificarse en GitHub; `skipped` no cuenta como PASS. Ningún gate puede sustituir esa verificación externa.

**Solo tras completar ambas CI:** C03-01, C03-02, C03-03 y **C03-04 CERTIFICADAS (4/10)**; **C03 NO CERTIFICADO**; **C03-05 HABILITABLE SOLO PARA PLANIFICACIÓN, NO INICIADA**; **C04 BLOQUEADO**; **2/37 capítulos certificados**. Registrar el resultado final y los enlaces de run en el comentario oficial del PR documental, sin inventar SHA futuro.
