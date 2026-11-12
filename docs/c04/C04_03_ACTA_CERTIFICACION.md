# CRECISOL — C04-03 · Acta institucional de contratos de almacenamiento dedicado (eficacia condicionada)

**Fecha:** 12 de noviembre de 2026 (Honduras). **Proyecto único:** `crecisol-platform/CRECISOL`, GitHub repo ID `1450414592`. **Destino:** `dev`. **Microfase:** C04-03 de 12. **Estado al crear el documento:** ACTA PROPUESTA, **NO EFICAZ HASTA COMPLETAR CI TÉCNICA Y DOCUMENTAL POSTMERGE**.

## 1. Fuente canónica y secuencia obligatoria

El Documento Maestro Único vigente `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx` conserva SHA-256 `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c` y PDF de lectura `0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77`. Capítulo C04: **72 párrafos canónicos**, SHA de texto `95be2bb0210e108317e67bd08fb9b2899d33554e684bb2d9ae3c8eced6995058`, verificados en CI. Las ocho puertas `MAE-C04` del Word no sustituyen las doce microfases de implementación.

**Precedente de entrada:** C04-02 certificada estrictamente para modelo SHARED y lector interno no expuesto en [acta de PR #87](https://github.com/crecisol-platform/CRECISOL/pull/87#issuecomment-6831090024), `dev` SHA `e2b0a921449f4a5a762a7af4c29f3c96a36b6b37`, **3/3 CI documental POSTMERGE SUCCESS**. Capítulos completos certificados hasta aquí: C01/C02/C03, **3/37**; C04 como capítulo permanece NO CERTIFICADO.

**Norma de C04-03:** `docs/planificacion-futura/C04_MICROFASES_PROPUESTAS.md`, “Contratos de almacenamiento dedicado” con puertos de ubicación tipados decididos por servidor y prueba negativa “No DSN desde URL, cookie o cabecera”. C04-04 solo será habilitada tras la presente certificación.

## 2. Entregables implementados y límite real

[PR técnico #88](https://github.com/crecisol-platform/CRECISOL/pull/88), HEAD PREMERGE `1d9a71bc1684d722a43783e34f4b25fe4d39d21c`; integración exclusiva `dev` SHA **`8bc133a898a6677dd7e2cac8a8c0a70e41bcb800`**. Diff de 7 archivos, sin alteración a infraestructura externa:

- `backend/src/Crecisol.Domain/Tenancy/TenantStoragePlacement.cs`: `TenantPlacementMode.Shared/Dedicated`; `DedicatedStorageSlotId` opaco no vacío; snapshot inmutable que obliga a SHARED sin slot, DEDICATED con slot, revisión positiva, UTC y enum válido. Ningún host/DSN/nombre de DB/secreto.
- `backend/src/Crecisol.Application/Tenancy/ResolveCurrentTenantStorage.cs`: puertos `ICurrentTenantPlacementAuthorization` y `ITrustedTenantPlacementReader`, caso de uso `ResolveCurrentTenantStorage`. Obtiene identidad autorizada del **backend** sin aceptar IDs de navegador; autoriza antes de leer, rechaza ausencia, error, cancelación o respuesta de tenant ajeno, sin fallback a shared.
- `backend/tests/Crecisol.TenantStorage.ContractsTests/`: suite positiva y negativa .NET 10 con falsos proveedores **identificados expresamente**, no confundidos con membresía real.
- `docs/c04/C04_03_ESPECIFICACION.md`: alcance, riesgos y controles de aceptación; `qa/c04_03_contracts_audits.py` y `.github/workflows/c04-03-storage-contracts.yml`: tres auditorías + mutaciones y pruebas reales con evidencia del mismo SHA/run.

**No implementado por C04-03:** resolver de identidad/membresía real, router físico a base dedicada, infraestructura de catálogos, DSN/secretos/pools, provisión, conexiones, migraciones, APIs privadas, exposición HTTP, cachés/medios/export/colas/backups, alojamiento dedicado. No existen rutas ni DI nuevas. `frontend/`, `database/`, `Infrastructure/`, `Api/`, `main`, Word/PDF, producción y SOLQARYN intactos. **Este cierre NO certifica acceso privado end-to-end ni almacenamiento dedicado operativo.**

## 3. Auditorías independientes y pruebas reales — PREMERGE

Las cinco ejecuciones aplicables corresponden al HEAD `1d9a71bc1684d722a43783e34f4b25fe4d39d21c`. Todas finalizaron **SUCCESS**:

| Control PREMERGE | Run real | Resultado |
| --- | --- | --- |
| C04-03 A/B/C + .NET 10 | [43273338846](https://github.com/crecisol-platform/CRECISOL/actions/runs/43273338846) | SUCCESS |
| C04-02 shared MySQL 8.4 | [43273339126](https://github.com/crecisol-platform/CRECISOL/actions/runs/43273339126) | SUCCESS |
| C04-01 contratos tenant | [43273338610](https://github.com/crecisol-platform/CRECISOL/actions/runs/43273338610) | SUCCESS |
| C03-10 integral Angular/.NET/MySQL/Chromium | [43273338914](https://github.com/crecisol-platform/CRECISOL/actions/runs/43273338914) | SUCCESS |
| Foundation | [43273338794](https://github.com/crecisol-platform/CRECISOL/actions/runs/43273338794) | SUCCESS |

Otros **tres workflows** omitidos por condiciones de path no se contaron como PASS.

**Auditoría A (funcional/trazabilidad):** Word canónico y sus hashes, 72 párrafos C04, requisitos de ubicación tipada, no DSN desde inputs externos, separación Domain/Application y matriz de dependencias.

**Auditoría B (seguridad/P0):** **46 mutaciones adversariales** de límites de confianza rechazadas. Suite C04-03 .NET10 con **42 aserciones reales** positivas/negativas: SHARED/DEDICATED, tenant A/B, slot, modo, revisión, UTC, error, denegación antes de consulta, catálogo B por A, cancelación y excepción, ausencia de fallback, firmas de API de Application sin input de navegador. Falsos proveedores no demuestran autorización de actor real: queda fuera del alcance.

**Auditoría C (arquitectura/calidad):** builds Release .NET10 con warnings como errores, suites de C04-01 **88 aserciones** y C04-02 **38 aserciones**; auditorías A/B y prueba .NET atadas a SHA/run común. C03-10 ejecutó .NET, Angular 22/Node24, Chromium y MySQL 8.4 como regresión independiente. Sin paquetes, servicios ni dependencias circulares nuevas.

## 4. CI posterior del PR #88 — condición de eficacia

**SHA integrado:** `8bc133a898a6677dd7e2cac8a8c0a70e41bcb800`. Siete workflows aplicables iniciados para dicho SHA. **No presumir su resultado hasta verificarlo**:

| Control POSTMERGE | Run |
| --- | --- |
| C04-03 contratos A/B/C .NET10 | [43274451891](https://github.com/crecisol-platform/CRECISOL/actions/runs/43274451891) |
| C04-02 shared MySQL8.4 | [43274451995](https://github.com/crecisol-platform/CRECISOL/actions/runs/43274451995) |
| C04-01 contratos | [43274452000](https://github.com/crecisol-platform/CRECISOL/actions/runs/43274452000) |
| C03-02 Domain | [43274451980](https://github.com/crecisol-platform/CRECISOL/actions/runs/43274451980) |
| C03-03 Application | [43274451877](https://github.com/crecisol-platform/CRECISOL/actions/runs/43274451877) |
| C03-10 integral | [43274451832](https://github.com/crecisol-platform/CRECISOL/actions/runs/43274451832) |
| Foundation | [43274451833](https://github.com/crecisol-platform/CRECISOL/actions/runs/43274451833) |

**Condición:** 7/7 `completed/success` en SHA exacto, sin fallos vigentes ni pendientes de aplicación. Registrar los identificadores y las conclusiones verificadas en comentario permanente del PR.

## 5. Riesgos, prohibiciones y plan de reversión

**P0 pendiente para futuras fases, no superficie pública habilitada:** el resolvedor `ICurrentTenantPlacementAuthorization` y el catálogo tipado no tienen implementaciones reales ni registros DI, por lo que este PR **no permite una conexión dedicada ni elegirla desde un request**. Antes de cualquier routing real se exige identidad, membresía, rol, acción, sucursal, recurso y host verificado por backend (C04-04/05/06), exclusividad de slot por tenant, control de secretos/leases y migraciones/routing por servidor (C04-07/09), además de pruebas BOLA/IDOR (C04-11). El ID opaco **no es credencial ni permiso**.

No se identificaron P0/P1 abiertos **en el alcance interno no conectado** probado; los riesgos de superficies futuras permanecen bloqueados y no se dan por aprobados. No se hizo benchmark de DSN reales ni prueba de failover: no hay router/DSN que probar.

**Reversión:** revertir solo squash commit `8bc133a898a6677dd7e2cac8a8c0a70e41bcb800` si surge incidencia. No existe migración ni dato productivo modificado. Nunca alterar SOLQARYN, Word/PDF, `main`, VPS, DNS, Cloudflare o servicios de cobros.

## 6. Eficacia institucional de esta acta (estrictamente condicionada)

La petición del propietario de continuar con calidad estricta autoriza la aceptación técnica **solo si** se verifican: (1) **7/7 POSTMERGE SUCCESS** del SHA técnico; (2) la propia CI PREMERGE del PR documental sobre último HEAD en `completed/success`; (3) fusión documental exclusiva a `dev`; (4) CI POSTMERGE propia sobre el nuevo SHA `dev`, `completed/success`; (5) registro de todos los run IDs en un comentario final.

**Solo entonces:** **C04-03 CERTIFICADA EN ALCANCE EXCLUSIVO DE CONTRATOS INTERNOS DE ALMACENAMIENTO SHARED/DEDICATED (3/12)** y **C04-04 HABILITADA SOLO PARA PREPARACIÓN**. **C04 COMPLETO NO CERTIFICADO** y **3/37 capítulos certificados**. No se declara el router operativo, provisioning, auth real ni infraestructura dedicada certificada.

Si falta un gate: **C04-03 PENDIENTE Y C04-04 BLOQUEADA**. No se reescriben fallos históricos ni se atribuye ejecución de pruebas que no existan. Firma operacional por evidencia GitHub, sin firma criptográfica ni validación de producción.
