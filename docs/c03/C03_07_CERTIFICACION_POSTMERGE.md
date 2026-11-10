# CRECISOL — C03-07 | Constancia final de certificación con remediación verificada

**ESTADO VIGENTE FINAL VERIFICADO — 10 de noviembre de 2026:** **C03-07 CERTIFICADA (7/10)** después de la remediación [PR #68](https://github.com/solqaryn-platform/CRECISOL/pull/68), squash a `dev` **`cebf18888bd57eb9695aaafb6ecc76a82f8eba8b`**, **11/11 CI POSTMERGE SUCCESS**; incluye C03-07 A/B/C, MySQL8.4, C03-02..05, Windows/Ubuntu y regresiones C01/C03. El PR #65 integró la propuesta de estado, pero su primer POSTMERGE registró seis regresiones documentales, por lo que **no** fundamenta por sí solo la certificación. La corrección #68 es la puerta final verificada. **C03-08 NO INICIADA; C03 NO CERTIFICADO; C04 BLOQUEADO; 2/37 capítulos certificados (C01 y C02).**

## Autoridad y aceptación

- Repositorio exclusivo `solqaryn-platform/CRECISOL`. Fuente: Documento Maestro Único `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx`, SHA-256 **`e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e`**, Git blob `214bf9c3d9763be23a77b7e0c464359584edde46`. T01-R06 Workers del capítulo 03. Word y PDF intactos.
- Aceptación expresa del propietario ANTES de las pruebas, 10 de noviembre de 2026: **«OK, ya adelante, continúa, por favor, hasta certificarla.»** y posterior continuidad **«OK, continúa hasta que deje certificada la microfase 7, por favor.»** Se circunscribe exclusivamente a C03-07.
- Precedencia: C03-01 a C03-06 CERTIFICADAS (6/10), PR documental #61, última constancia sobre SHA `a1923d117febc5fd4e5a544b558c0f85ad517b44` [C03-06](C03_06_CERTIFICACION_POSTMERGE.md). C03-05 certificada previamente en `12fb19b0c35038c297939e17b27770c4fd63f77e`.

## 1. PR técnico #63: PREMERGE / POSTMERGE exactos

**HEAD PREMERGE `f7efc6d2b62db916bb429303887bcae3d6b0a4fd`**:
- [#42815359099](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42815359099) C03-07 A/B/C .NET10 MySQL8.4 — **completed/success**.
- [#42815359163](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42815359163) Foundation — **completed/success**.
- Otras ejecuciones condicionales `skipped` no se sumaron.

PR #63 fusionado mediante squash a `dev` SHA exacto **`a256885afa5ee2f639bd4ed6f7d8595b709e6409`**. **8/8 CI POSTMERGE SUCCESS** con evento `push`, SHA idéntico y `status=completed/conclusion=success`:

| Run ID | CI |
| --- | --- |
| [42815698115](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42815698115) | C03-07 A/B/C .NET10, MySQL8.4, inyección negativa |
| [42815698155](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42815698155) | C03-01 integral + regresiones |
| [42815698446](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42815698446) | C03-02 Domain |
| [42815698259](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42815698259) | C03-03 Application |
| [42815698005](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42815698005) | C03-04 Infrastructure/MySQL |
| [42815698160](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42815698160) | C03-05 API/DTO |
| [42815697923](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42815697923) | Preflight local A/B + Windows |
| [42815698031](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42815698031) | Foundation |

## 2. PR documental #64: PREMERGE / POSTMERGE exactos

**HEAD PREMERGE `8605a94c66de294c23cbcff3eff7580d7a47b57d`**:
- [#42817055092](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42817055092) C03-07 .NET10, MySQL8.4, auditorías A/B/C y gate de acta 15/15 — **completed/success**.
- [#42817055080](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42817055080) Foundation — **completed/success**.
- `skipped` condicionales no son éxito ni se contabilizan.

PR #64 fusionado mediante squash a `dev` SHA exacto **`b1e019afabfb44f05076024b8d34cde74a340acc`**. **5/5 CI POSTMERGE SUCCESS**:
- [#42817551338](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42817551338) C03-07 A/B/C, MySQL8.4 y 15 pruebas de acta.
- [#42817551072](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42817551072) C03-01 integral.
- [#42817551041](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42817551041) C03-05 API/DTO.
- [#42817551186](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42817551186) C03 local A/B Windows.
- [#42817551091](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42817551091) Foundation.

## 3. PR de estado #65 y remediación #68 — cierre real, sin ocultar fallos

**PR #65:** PREMERGE HEAD `0c3a97530de5d4092cc750ba2d0ddd1c8e81d79b`, [C03-07 #42818829648](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42818829648) y [Foundation #42818829320](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42818829320), **2/2 SUCCESS**. Fusión a `dev` SHA `8e842136d6203a408271150bdccb701e6ff8fb3b`. Su primera CI POSTMERGE **NO fue certificable**: seis ejecuciones fallaron en validaciones documentales (C03-02 `42819215000`, C03-03 `42819215334`, C03-04 `42819215112`, C03-05 `42819215346`, C03-06 `42819215250`, portabilidad `42819214844`). La compilación y la base MySQL8.4 de C03-07 habían pasado; no se ignoraron estos fallos ni se declararon verdes.

**PR de remediación #68:** cambios exclusivamente en `AGENTS.md`, `docs/C01_PREPARACION.md`, `backend/README.md` y `.github/workflows/c03-07-outbox.yml`, conservando el código técnico, Word/PDF y las actas históricas. Restituyó los marcadores C03-02/03/04, 4/10 histórico, formato C01 requerido y añadió `qa/c03_local_preflight.py` + `qa/test_c03_portability.py` como controles preventivos después de los contratos runtime.

**PREMERGE HEAD #68 `4322771269b13ad41939a121a47cc17eb6638958`**: [A/B/C + .NET10 + MySQL8.4 y preflight #42819963900](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42819963900) y [Foundation #42819963578](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42819963578), ambos `status=completed/conclusion=success`.

**POSTMERGE #68:** `dev` SHA exacto **`cebf18888bd57eb9695aaafb6ecc76a82f8eba8b`**, 11/11 workflows `event=push/status=completed/conclusion=success`:

| Workflow | GitHub Actions run |
| --- | --- |
| C03 local static A/B, Windows/Ubuntu | [42820273168](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42820273168) |
| Foundation | [42820272959](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42820272959) |
| C03-02 Domain | [42820272880](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42820272880) |
| C03-03 Application | [42820273077](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42820273077) |
| C01-06 identidad de marca | [42820273151](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42820273151) |
| C03-04 Infrastructure/MySQL8.4 | [42820273176](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42820273176) |
| C03-05 API/DTO | [42820273087](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42820273087) |
| C03-07 A/B/C, .NET10, MySQL8.4, acta | [42820272906](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42820272906) |
| C01-08 capítulo integral | [42820273036](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42820273036) |
| C01-07 seguridad full-stack | [42820273109](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42820273109) |
| C03-01 arquitectura integral y Chromium | [42820272895](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42820272895) |

Los seis fallos históricos de #65 se consideran **remediados y reemplazados por CI nueva sobre un SHA distinto**; no se reetiquetan como SUCCESS. El PR duplicado #67 se cerró **sin merge** para no alterar el mismo alcance por segunda vez. Las ejecuciones condicionales `skipped` se excluyen de cualquier recuento de éxito.

## 4. Alcance probado, auditorías A/B/C y negativos

- **Auditoría A (PASS):** Word aprobado y T01-R06; infraestructura de workers sin transgredir Clean Architecture ni reabrir pasos futuros.
- **Auditoría B (PASS):** **16/16 mutaciones adversariales de código** rechazadas; rollback/lease/ACK cruzado protegido, SQL con scope PLATFORM y FK compuesta, denegación de versiones no admitidas y de SQL DELETE/DDL. El gate de acta rechazó **15/15 mutaciones documentales** y mantuvo las **16/16 mutaciones históricas C03-06**.
- **Auditoría C (PASS):** .NET10 Release `TreatWarningsAsErrors`, **26/26 aserciones** Domain/Application; **40/40 aserciones MySQL8.4 real efímero** para atomicidad agregado/outbox, rollback, 8 claims concurrentes, fencing, recovery, replay, backoff, 3 intentos y dead-letter. Regresiones C01/C02/C03-01..06 y HTTP público ejecutadas en CI referida.
- **Límites explícitos:** únicamente evento interno `platform.foundation-check.v1` con payload fijo y recibo técnico idempotente. **No se certifican BOLA/IDOR/MFA privados**, tenancy autorizada, ruteo de DSN dedicado, workers productivos, efectos comerciales, entrega exactly-once, tráfico externo, pagos ni producción. API pública sin ruta adicional; `frontend/`, Word/PDF, `main` y SOLQARYN intactos. No se realizó deploy, VPS, DNS ni gasto de servicios.

## 5. Cierre definitivo y rollback

**C03-07 CERTIFICADA (7/10)** dentro del corte técnico PLATFORM limitado, por autorización humana expresa, PR #63 de código, PR #64 de acta, PR #65 de estado y **remediación final #68 con 11/11 CI POSTMERGE SUCCESS en SHA exacto**. El fallo inicial de #65 está explicado en §3 y no invalida la CI corregida de #68. Esta constancia actualiza el texto condicional de su versión previa, sin reescribir los registros originales. La **formalización de esta actualización de constancia** conserva su propia CI de PR y POSTMERGE como control posterior, sin reinterpretar los 11 checks de #68.

**Rollback:** revert por PR del commit de estado, del acta `b1e019afabfb44f05076024b8d34cde74a340acc` y/o código `a256885afa5ee2f639bd4ed6f7d8595b709e6409`, de forma secuencial. No hay datos productivos, migraciones aplicadas fuera de CI, cuentas externas o despliegues por revertir.

**Siguiente microfase:** C03-08 NO INICIADA y no autorizada por esta constancia. **C03 NO CERTIFICADO y C04 BLOQUEADO** hasta completar C03-08..10 y auditoría integral del capítulo.
