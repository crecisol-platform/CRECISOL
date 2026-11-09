# CRECISOL — Capítulo 03 · Arquitectura técnica limpia y dependencias

**ESTADO VIGENTE: 2/37 capítulos CERTIFICADOS (C01+C02); C03-01 CERTIFICADA y C03-02 CERRADA TÉCNICAMENTE CON ACEPTACIÓN HUMANA ESPECÍFICA (2/10 microfases), formalización C03-02 condicionada a CI PREMERGE/POSTMERGE del acta. C03 NO CERTIFICADO; C04 BLOQUEADO.** La certificación previa C03-01 y los tres workflows exitosos del PR técnico #51 permiten formalizar C03-02 sin habilitar C03-03 automáticamente. C01+C02 siguen siendo los únicos **2/37 capítulos** certificados.

## Puerta de entrada verificada

- `solqaryn-platform/CRECISOL`, `dev` SHA `342085ad323c9f24da5b2a197c41574376ac30f0`, [PR #48 certificado C02](https://github.com/solqaryn-platform/CRECISOL/pull/48). [CI integral C02-09 #42685252655](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42685252655), **12/12 workflows POSTMERGE SUCCESS** de SHA exacto, auditorías A/B/C y `APROBADO` del propietario registrados. La certificación de C02 tiene alcance público/sintético limitado; no autoriza backend privado ni despliegues.
- Documento Maestro Único SHA-256 `e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e`; Git blob `214bf9c3d9763be23a77b7e0c464359584edde46`; no modificar Word/PDF.
- La fuente real de capítulo 03 se extrajo de GitHub Actions [#42687641345](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42687641345) **SUCCESS**: siete unidades originales de texto, una tabla original de nueve filas, dos tablas anexas de tres filas, tres subapartados y requisitos G.7. Finaliza al comenzar el capítulo 04. Los ocho pasos normativos `MAE-C03-01..08` no equivalen a las diez microfases técnicas propuestas `C03-01..10` (**DIF-C03-01**).

## Secuencia C03 — un entregable por vez

| Microfase | Entrega técnica | Estado vigente |
| --- | --- | --- |
| C03-01 | Inventario y mapa de módulos, grafo acíclico .NET/Angular y negativos | **CERTIFICADA (1/10)**: acta [C03-01](c03/C03_01_ACTA_MICROFASE.md), PR #50 a `dev` SHA `8e4f25073a3f11150e4323ecb66380647f0cdc0b`, 7/7 CI POSTMERGE SUCCESS; alcance previo histórico preservado |
| C03-02 | Contratos Domain puros | **CERRADA TÉCNICAMENTE, ACEPTADA PARA CERTIFICACIÓN (2/10)**, formalización CI del acta pendiente: [acta](c03/C03_02_ACTA_MICROFASE.md), [especificación](c03/C03_02_ESPECIFICACION.md), PR [#51](https://github.com/solqaryn-platform/CRECISOL/pull/51) SHA `adc975c7bcf4b6a772cc160a5b377d26af2b5fa6` 3/3 POSTMERGE SUCCESS; A/B/C y contratos reales |
| C03-03 | Puertos y casos de uso Application | **HABILITABLE PARA PLANIFICAR TRAS CI FINAL C03-02; NO INICIADA** |
| C03-04 | Adaptadores Infrastructure | **PENDIENTE** |
| C03-05 | Contratos API/DTO, validación y errores | **PENDIENTE** |
| C03-06 | Módulos verticales/DI, sin Service Locator | **PENDIENTE** |
| C03-07 | Workers/outbox/eventos versionados e idempotencia | **PENDIENTE** |
| C03-08 | Configuración segura y entornos locales | **PENDIENTE** |
| C03-09 | Compatibilidad y benchmark basal reproducible | **PENDIENTE** |
| C03-10 | Auditorías integrales A/B/C, acta y aceptación humana C03 | **PENDIENTE**: no certificar ni abrir C04 antes de aprobación expresa |

Este calendario técnico deriva de la [propuesta C03](planificacion-futura/C03_MICROFASES_PROPUESTAS.md) ya contrastada con el Word y queda subordinado a su texto; no reescribe las ocho puertas normativas ni certifica fases futuras.

## Arquitectura real y límites de C03-01/C03-02

- **Backend físico actual:** `Crecisol.Domain` puro; `Crecisol.Application` → Domain; `Crecisol.Infrastructure` → Application (adaptador MySqlConnector); `Crecisol.Api` → Application e Infrastructure. Comprobar con `ProjectReference` reales, ciclos, paquetes y compilación .NET 10, sin crear nuevos módulos de negocio.
- **Frontend actual:** una aplicación pública Angular 22 con rutas allowlist y laboratorio `pattern-harness` aislado, no las dos apps sugeridas por el Maestro. No añadir login, editor, shells privados ni guards como autorización.
- **Base de datos actual:** tabla/lectura pública de marca C01 en MySQL 8.4. Las ubicaciones de datos shared/dedicated, control de propiedad tenant y operaciones privadas siguen pendientes. No crear migraciones, DSL de seguridad ni conexiones productivas.
- **Plan futuros módulos:** Identity, Tenants, Catalog, Orders, Payments, Scheduling, Memberships, Media, Analytics y Cases separado para G.7; workers/outbox, DTO/OpenAPI y adaptadores externos solo como contratos futuros.
- **P0 transversal:** aislamiento por tenant/sucursal/recurso en backend, login normal sin selector de empresa, SuperAdmin excepcional con MFA/motivo/expiración/auditoría, no saltar TenantContext, no usar input cliente para DSN, no ejecutar configuración no confiable. Los módulos privados todavía no permiten pruebas runtime BOLA/IDOR y no se simula certificación de ellas.

## Puerta de salida C03-01

Auditoría **A**: fuente Word real y matriz 16 IDs; **B**: diez mutaciones adversariales y denegación de avances/cambios ilegales; **C**: grafo .NET exacto, Angular público, .NET10/Angular22 y MySQL8.4/API solo de publicación pública mediante CI real. Revisión del diff, secretos, enlaces, impacto en permisos/PII, rollback por revert, CI PREMERGE y POSTMERGE del SHA exacto. El [acta C03-01](c03/C03_01_ACTA_MICROFASE.md) registra aceptación humana literal «CERTIFICA POR FAVOR LA MICROFASE 1 DEL BLOQUE 3», evidencia 7/7 CI POSTMERGE del PR técnico y decisión **C03-01 CERRADA TÉCNICAMENTE (1/10)**. Su formalización definitiva se subordina a **todas** las CI PREMERGE/POSTMERGE del PR documental de cierre, sin confundirlas con el PR técnico. **Capítulo C03 NO CERTIFICADO, C04 BLOQUEADO**.

**Ambiente y costes:** exclusivamente documentación, QA, CI y código de prueba; sin modificar `frontend/src/`, `backend/src/`, `database/` ni `main`, SOLQARYN, VPS, DNS, hosting, pagos o producción. No activar automatizaciones ni servicios externos.


## Puerta de salida C03-02 (formalización específica)

El [PR técnico #51](https://github.com/solqaryn-platform/CRECISOL/pull/51) se fusionó solo a `dev` SHA `adc975c7bcf4b6a772cc160a5b377d26af2b5fa6`; GitHub verificó **3/3 CI POSTMERGE SUCCESS**: [Domain A/B/C #42701725556](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42701725556), [Foundation #42701724980](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42701724980), [C03-01 integral #42701725339](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42701725339). .NET10 Release 0 errores, 35 nuevos casos Domain, 51 C01 Domain, 19 Application, 5 mutaciones adversariales rechazadas; 83 Chromium y MySQL8.4/API/Angular públicos en regresión. El propietario autorizó específicamente formalizar C03-02 en respuesta a la solicitud explícita de acta/certificación: «adelante con ello por favor» (9 noviembre 2026). [Acta C03-02](c03/C03_02_ACTA_MICROFASE.md). **Esta autorización no extiende la cobertura a autenticación privada, tenant routing dedicado, C03-03, C04, producción, pagos ni VPS**. Al verificar CI PREMERGE y POSTMERGE del PR documental, **C03-02 CERTIFICADA (2/10)**; antes de ese gate sigue **CERRADA TÉCNICAMENTE / FORMALIZACIÓN EN CURSO**. C03 no certificado.
