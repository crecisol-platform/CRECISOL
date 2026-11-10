# CRECISOL — C03-08 | Constancia de certificación documental (8/10)

**Estado del PR de esta constancia:** propuesta de **C03-08 CERTIFICADA (8/10)**, condicionada a verificar las CI **PREMERGE y POSTMERGE del propio PR de certificación** y su SHA exacto. No se toma una prueba `skipped` como `success`. La certificación formal se consolida únicamente después de esas comprobaciones, con aceptación expresa del propietario ya recibida. **C03-09 NO INICIADA; C03 NO CERTIFICADO; C04 BLOQUEADO; 2/37 capítulos certificados (C01 y C02).**

## 1. Autoridad, aceptación y alcance inmutable

- Repositorio exclusivo `solqaryn-platform/CRECISOL`; rama `dev` y rama de PR `feature/c03-08-certificacion-final`. Prohibidas escrituras a `solqaryn/Solqaryn`, `main`, producción, VPS, DNS, cobros, servicios externos y despliegues.
- Fuente de autoridad: `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx`, Git blob `214bf9c3d9763be23a77b7e0c464359584edde46`, SHA-256 **`e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e`**. El PDF es copia de lectura y ambos archivos permanecen intactos. Ocho puertas normativas MAE no sustituyen las diez microfases técnicas.
- Autorización humana específica del **10 de noviembre de 2026**, literal parcial: **«cierra la fase número 8 con su acta y todo»**, y orden expresa de realizar **las tres auditorías en cada fase**, resolver hallazgos antes de certificar. La orden precede a las comprobaciones finales de este PR; no se presenta como posterior a ellas ni autoriza C03-09, capítulo C03 completo, C04 ni producción.
- Antecedente: **C03-07 CERTIFICADA (7/10)** con PR #63, PR #64 y remediación #68, [constancia histórica](C03_07_CERTIFICACION_POSTMERGE.md). La [acta técnica propia de C03-08](C03_08_ACTA_MICROFASE.md) permanece inmutable como registro de cierre técnico anterior a la certificación.

## 2. PR técnico #70 — fuente exacta

- [PR #70](https://github.com/solqaryn-platform/CRECISOL/pull/70), HEAD PREMERGE `97ac8431ee4b1d3ab263dc54205b2bd22f07600a`.
- **2/2 CI PREMERGE SUCCESS:** [C03-08 A/B/C #42823974509](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42823974509); [Foundation #42823974454](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42823974454).
- Squash únicamente a `dev` SHA **`221afb6cc515fc79a3b6ca6e010af11f2e20b0a2`**. **6/6 CI POSTMERGE SUCCESS** con evento `push`, `completed/success` y SHA idéntico: [#42824458247 C03-08](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42824458247), [#42824458457 C03-01 integral](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42824458457), [#42824458283 C03-05 API](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42824458283), [#42824458016 C03-06 DI](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42824458016), [#42824458308 portabilidad](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42824458308) y [#42824458319 Foundation](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42824458319).
- Entrega: contrato tipado `LocalRuntimeSettings` para perfiles `Disabled` (predeterminado), `Local` y `Ci`; composición previa al arranque exclusivamente mediante variables del proceso, validación explícita de host loopback, no red externa, lote acotado, socket desechable. No crea rutas ni roles ni worker activo; base MySQL8.4 efímera y pública de prueba.

## 3. PR de acta y auditoría #71 — fuente exacta

- [PR #71](https://github.com/solqaryn-platform/CRECISOL/pull/71), HEAD PREMERGE `bd57aab5d8d76dfcd0f53f2d121a4d5df50c0122`.
- **2/2 CI PREMERGE SUCCESS:** [C03-08 A/B/C/acta #42825656795](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42825656795) y [Foundation #42825656755](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42825656755); otras ejecuciones condicionales `skipped` no se sumaron.
- Squash a `dev` SHA exacto **`b333e58378b6a5ea0438f1ee6b11de85d9ce04df`**. **5/5 CI POSTMERGE SUCCESS** `push/completed/success`, verificadas directamente contra ese SHA:
  1. [#42826620681 — C03-08 A/B/C + acta](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42826620681).
  2. [#42826620758 — C03-01 integral, Angular22/Chromium, API/MySQL8.4](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42826620758).
  3. [#42826620733 — portabilidad Windows/Linux](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42826620733).
  4. [#42826620877 — Foundation](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42826620877).
  5. [#42826621425 — API/DTO MySQL8.4](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42826621425).

## 4. Tres auditorías independientes A/B/C — comprobadas antes del cierre

**Auditoría A — trazabilidad (PASS):** Word real, huella exacta, siete unidades originales y nueve filas del capítulo C03, 37 capítulos y precedencia 7/10; T01, arquitectura y límites del corte de configuración local. Especificación, código, CI y acta enlazados; ninguna modificación del Word ni de las actas históricas.

**Auditoría B — seguridad (PASS para este corte):** **18/18 mutaciones adversariales** de configuración rechazadas: activación por defecto, opción huérfana, perfil producción/remoto, host duplicado/host manipulado, bypass de red externa, número/lote fuera de rango, socket no desechable, inyección y path traversal. **15/15 mutaciones adversariales del acta** rechazadas, incluida falsificación de SHA, CI, aprobación o avance; los 14 controles negativos adicionales de esta constancia requieren su CI propia. Acceso a red externa denegado, ninguna credencial versionada, HTTP privado no introducido, DDL productivo inexistente.

**Auditoría C — arquitectura, compatibilidad, calidad y rendimiento basal (PASS para la microfase, no benchmark C03-09):** .NET10 Release warnings-as-errors, **72 aserciones** tipadas ejecutables, MySQL8.4 efímero `--network none` y socket Unix con usuario SELECT-only, API pública HTTP real, regresión outbox C03-07 (26 contratos + 40 MySQL), C03-06 DI, C01/C02/C03, Angular22/Node24 y Chromium integral. Sin nuevos endpoints, migraciones ni cambios UI. El benchmark basal reproducible es C03-09 y **no** se declara realizado.

## 5. Guardas de continuidad y limitaciones explícitas

- El estado vigente propuesto es **C03-08 CERTIFICADA (8/10)**; la siguiente microfase permanece **C03-09 NO INICIADA**. El capítulo **C03 NO CERTIFICADO**, el capítulo **C04 BLOQUEADO**, y el total continúa en **2/37 capítulos certificados (C01 y C02)**.
- No se certifican **BOLA/IDOR/MFA** privados, aislamiento/autorización real de tenant o sucursal, router DSN shared/dedicated, SuperAdmin, facturación, trabajadores productivos, persistencia privada, pagos ni producción. El contrato tipado es operativo/local y jamás un selector de permisos, tenant, DSN o entorno productivo.
- Los validadores antiguos no se eliminan: se adaptan para aceptar únicamente el 8/10 con evidencias del PR #70 y #71, preservar 7/10 y sus SHAs, y rechazar 9/10, C03 certificado o desbloqueo de C04. La CI nueva ejecuta nuevamente A/B/C, 15 mutaciones de acta, 14 mutaciones adicionales del certificado y regresiones Windows/Linux, .NET10, MySQL8.4 y Angular22/Chromium.

## 6. Gate de este PR y decisión de certificación

**PREMERGE del PR documental de certificación:** verificar sus workflows aplicables en estado `completed/success` sobre el HEAD exacto, incluidos A/B/C, .NET10, MySQL8.4, portabilidad e integral. **POSTMERGE:** fusionar únicamente a `dev` y comprobar **todos** los workflows activados sobre el SHA de integración, sin `failure`, `cancelled` o `in_progress`. Los `skipped` no se contabilizan como `success`. Sin ambas evidencias y las pruebas adversariales, la inscripción de 8/10 sigue **CONDICIONADA**, nunca una afirmación de certificación final.

**Rollback:** revert del PR documental de estado y, si se revierte el alcance técnico, de los squashes `b333e58378b6a5ea0438f1ee6b11de85d9ce04df` (acta) y `221afb6cc515fc79a3b6ca6e010af11f2e20b0a2` (código), por PR independiente en `dev`. No existen datos productivos, producción, `main` ni servicios externos que revertir.
