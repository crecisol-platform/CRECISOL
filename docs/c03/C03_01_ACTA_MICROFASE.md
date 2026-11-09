# CRECISOL — Acta formal C03-01 · Mapa de módulos y dependencias

**ESTADO DE DECISIÓN: C03-01 CERRADA TÉCNICAMENTE Y APROBADA EXPRESAMENTE PARA CERTIFICACIÓN DE MICROFASE (1/10); formalización condicionada al SUCCESS del PR de esta acta y su CI POSTMERGE. C03 NO CERTIFICADO; C04 BLOQUEADO.**

**Fecha de aceptación humana:** 9 de noviembre de 2026. **Solicitud literal del propietario tras las evidencias técnicas:** «CERTIFICA POR FAVOR LA MICROFASE 1 DEL BLOQUE 3». La solicitud autoriza específicamente el **cierre de C03-01**; no constituye aceptación anticipada de **C03-02..10**, del capítulo 03 integral, de C04, de despliegues, VPS, producción ni de funcionalidades privadas inexistentes. Se conserva el principio de un capítulo por vez.

## 1. Autoridad, secuencia y procedencia

- **Repositorio permitido:** `solqaryn-platform/CRECISOL`. Base `dev` del trabajo de C03-01: `342085ad323c9f24da5b2a197c41574376ac30f0`, correspondiente a [certificación formal C02 PR #48](https://github.com/solqaryn-platform/CRECISOL/pull/48), con 12/12 CI POSTMERGE SUCCESS. C01+C02 siguen siendo **2/37 capítulos certificados**.
- **Trabajo técnico de C03-01:** [PR #49](https://github.com/solqaryn-platform/CRECISOL/pull/49), commit de HEAD PREMERGE `f5c4ff80607ddc935f5ac80e1d60215863f44ada`; **fusionado solo a `dev`** en SHA exacto **`7217fb1e720f16c71d5a654f605eca96d81deb58`**. [CI PREMERGE integral #42690517491](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42690517491) **SUCCESS**. Esta acta documenta resultados ya obtenidos; no altera retroactivamente el código técnico.
- **Documento Maestro Único inmutable:** `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx`, Git blob `214bf9c3d9763be23a77b7e0c464359584edde46`, SHA-256 `e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e`. PDF de consulta blob `455847ae0cc3bf02b9b9c4c3435476d093918ea2`. Extracción original [#42687641345](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42687641345) SUCCESS; límites exactos `03. Arquitectura técnica limpia y dependencias` → `04. Multitenancy, dominios y aprovisionamiento`.
- **DIF-C03-01:** ocho puertas normativas de proceso del Word `MAE-C03-01..08` no equivalen a diez microentregables técnicos `C03-01..10`. C03-01 abarca inventario/trazabilidad/grafo; las nueve microfases siguientes siguen pendientes.

## 2. Evidencia real de CI POSTMERGE del trabajo técnico #49

Consultadas directamente las ejecuciones de GitHub Actions filtradas por el commit **`7217fb1e720f16c71d5a654f605eca96d81deb58`**, con `event=push`, `status=completed`, `conclusion=success` en **7/7**; **ningún fallo** en ese conjunto de ejecuciones.

| Ejecución real del SHA #49 | Conclusión |
| --- | --- |
| [C03-01 fuente/grafo/A-B-C y regresiones #42691620500](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42691620500) | **SUCCESS** |
| [Foundation #42691620886](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42691620886) | **SUCCESS** |
| [C02-01 fuente y auditorías #42691620794](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42691620794) | **SUCCESS** |
| [C01-08 integral #42691620719](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42691620719) | **SUCCESS** |
| [C01-07 stack de seguridad pública #42691621001](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42691621001) | **SUCCESS** |
| [C02-08 recorridos/denegación privada #42691620768](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42691620768) | **SUCCESS** |
| [C02-09 integral/certificación previa #42691620826](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42691620826) | **SUCCESS** |

**No** son CI de este nuevo PR documental de formalización: se comprobarán separadamente en PREMERGE y POSTMERGE. No se contaron `skipped` como `SUCCESS`.

## 3. Auditorías A, B y C reales y alcance del resultado

**Auditoría A — PASS C03-01 (trazabilidad/funcional):** Word real con hash validado y límite C03/C04, **siete unidades originales**, **tabla original de nueve filas**, dos tablas anexas de tres filas, matriz de **16 identificadores P01..P07 y T01-R01..09**; tres mutaciones de cobertura ausente, duplicada y colapsada rechazadas. [Especificación de fuente](C03_01_ESPECIFICACION.md) y [mapa JSON](C03_01_MODULOS_DEPENDENCIAS.json).

**Auditoría B — PASS C03-01 (seguridad documental/arquitectura):** **10/10 mutaciones adversariales rechazadas**: ciclos C#, SDK/ORM en Domain, MySQL directo en Application, escritura a tablas ajenas, DSN controlado por navegador, ruta privada habilitada, falso worker implementado, Cases fusionado con Orders/Scheduling/Billing, certificación prematura de C04 y módulos futuros etiquetados como actuales. No existe prueba runtime de auth BOLA/IDOR/MFA sobre módulos privados no implementados: **NO APLICA A ESTA MICROFASE**; esa falta no se hace pasar por protección global auditada.

**Auditoría C — PASS C03-01 (arquitectura/compilación/calidad):** dependencias físicas acíclicas `Crecisol.Api → Crecisol.Application + Crecisol.Infrastructure → Crecisol.Application → Crecisol.Domain`, Domain puro, frontend Angular22 con únicamente rutas públicas allowlist, harness sintético fuera del bundle productivo, compilación .NET10 Release, TypeScript/Angular22 con Node24.15.0, pruebas de contratos Domain (**51 aserciones**) y Application (**19 aserciones**), regresiones **25 Chromium C01 + 58 Chromium C02 = 83**, integración pública descartable **MySQL8.4/.NET10/API/Angular** y batería HTTP de alcance público. `npm audit --omit=dev --audit-level=high`: **0 vulnerabilidades detectadas en dependencias productivas** en esa ejecución. Sin benchmark productivo ni evaluación manual WCAG 2.2 AA declarada.

**Alcance de implementación:** solo mapeo de los cuatro proyectos .NET realmente existentes, frontend público, API pública de marca y datos publicados de C01. Los módulos futuros Identity, Tenants, Catalog, Orders, Payments, Scheduling, Memberships, Media, Analytics y Cases son **PLANNED**, no código nuevo. Arquitectura modular, port/adapters, tenant-scoped authorization, shared/dedicated, workers/outbox, panel admin integral/parametrización total y pagos permanecen **pendientes de sus capítulos/microfases**. Ningún entorno productivo ha sido validado, configurado o desplegado.

## 4. Control de cambios, riesgos y reversión

El PR técnico #49 incluyó **13 archivos exclusivamente en documentación, QA y CI**, con **61 enlaces relativos válidos**, sin patrones de credenciales detectados en los archivos afectados y con Word/PDF intactos. **No se modificaron `frontend/src/`, `backend/src/`, `database/`, API, migraciones, `main`, SOLQARYN ni producción.** No se conocen P0/P1 abiertos dentro del **alcance de inventario y pruebas públicas** efectivamente auditado; no implica cero vulnerabilidades del SaaS futuro. Riesgo residual: módulos privados ausentes, políticas reales de tenant/branch/recurso no implementadas, benchmarks y WCAG manual futura, decisiones ADR abiertas y ramas GitHub no protegidas.

**Reversión:** PR de revert documental/QA/CI sobre `dev`, conservando la procedencia del #49 y las CI ya ejecutadas. Ninguna migración, dato productivo o credencial requiere rollback. La rama `main` no se altera.

## 5. Decisión de microfase y gate final

**DECISIÓN DE MICROFASE: C03-01 CERRADA TÉCNICAMENTE (1/10)**, pendiente únicamente de formalización CI del acta; con la petición literal del propietario del **9 de noviembre de 2026** y las auditorías/trazas comprobadas, se aprueba **CERTIFICAR ÚNICAMENTE C03-01, CERRADA TÉCNICAMENTE (1/10)**. El estado documental vigente es **C03-01 ACEPTADA / FORMALIZACIÓN EN CURSO** mientras este PR documental y toda su CI POSTMERGE no estén `SUCCESS`. Tras comprobarlos, **C03-01 CERRADA TÉCNICAMENTE Y CERTIFICADA (1/10)**, C03-02 **HABILITABLE PARA PLANIFICAR EN TAREA POSTERIOR, NO INICIADA AQUÍ**, capítulo **C03 NO CERTIFICADO**, **C04 BLOQUEADO**, **2/37** capítulos certificados (solo C01 y C02).

**Puertas de control de esta acta:** verificar HEAD exacto del PR documental, archivo Maestro inmutable, triple auditoría real y regresiones en CI, prueba negativa de manipulación del estado/aceptación, diff y secretos, fusión exclusivamente a `dev`, y **todas las CI POSTMERGE SUCCESS** asociadas al SHA resultante. Registrar en el PR el SHA final y los links de CI; ningún éxito de la ejecución técnica #49 se atribuye falsamente a este PR.
