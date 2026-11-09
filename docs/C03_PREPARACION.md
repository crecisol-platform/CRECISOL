# CRECISOL — Capítulo 03 · Arquitectura técnica limpia y dependencias

**ESTADO VIGENTE: C03-01 CERRADA TÉCNICAMENTE Y ACEPTADA EXPRESAMENTE PARA CERTIFICACIÓN (1/10 microfases); formalización documental condicionada a CI PREMERGE y POSTMERGE del acta. C03 NO CERTIFICADO; C04 BLOQUEADO.** El capítulo anterior C02 está certificado por aceptación humana expresa el 9 de noviembre de 2026; C01+C02 = **2/37 capítulos certificados**. No se inicia C03-02 ni se habilita C04.

## Puerta de entrada verificada

- `solqaryn-platform/CRECISOL`, `dev` SHA `342085ad323c9f24da5b2a197c41574376ac30f0`, [PR #48 certificado C02](https://github.com/solqaryn-platform/CRECISOL/pull/48). [CI integral C02-09 #42685252655](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42685252655), **12/12 workflows POSTMERGE SUCCESS** de SHA exacto, auditorías A/B/C y `APROBADO` del propietario registrados. La certificación de C02 tiene alcance público/sintético limitado; no autoriza backend privado ni despliegues.
- Documento Maestro Único SHA-256 `e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e`; Git blob `214bf9c3d9763be23a77b7e0c464359584edde46`; no modificar Word/PDF.
- La fuente real de capítulo 03 se extrajo de GitHub Actions [#42687641345](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42687641345) **SUCCESS**: siete unidades originales de texto, una tabla original de nueve filas, dos tablas anexas de tres filas, tres subapartados y requisitos G.7. Finaliza al comenzar el capítulo 04. Los ocho pasos normativos `MAE-C03-01..08` no equivalen a las diez microfases técnicas propuestas `C03-01..10` (**DIF-C03-01**).

## Secuencia C03 — un entregable por vez

| Microfase | Entrega técnica | Estado vigente |
| --- | --- | --- |
| C03-01 | Inventario y mapa de módulos, grafo acíclico .NET/Angular y negativos | **CERRADA TÉCNICAMENTE (1/10) CON ACEPTACIÓN HUMANA EXPRESA**, sujeta a CI PREMERGE y POSTMERGE del PR documental de cierre. [Acta y evidencia 7/7 CI](c03/C03_01_ACTA_MICROFASE.md), [especificación](c03/C03_01_ESPECIFICACION.md), [grafo máquina](c03/C03_01_MODULOS_DEPENDENCIAS.json); [PR técnico #49](https://github.com/solqaryn-platform/CRECISOL/pull/49) fusionado a `dev` SHA `7217fb1e720f16c71d5a654f605eca96d81deb58` |
| C03-02 | Contratos Domain puros | **HABILITABLE TRAS CI FINAL C03-01, PERO NO INICIADA**; necesita rama, especificación y tres auditorías propias |
| C03-03 | Puertos y casos de uso Application | **PENDIENTE** |
| C03-04 | Adaptadores Infrastructure | **PENDIENTE** |
| C03-05 | Contratos API/DTO, validación y errores | **PENDIENTE** |
| C03-06 | Módulos verticales/DI, sin Service Locator | **PENDIENTE** |
| C03-07 | Workers/outbox/eventos versionados e idempotencia | **PENDIENTE** |
| C03-08 | Configuración segura y entornos locales | **PENDIENTE** |
| C03-09 | Compatibilidad y benchmark basal reproducible | **PENDIENTE** |
| C03-10 | Auditorías integrales A/B/C, acta y aceptación humana C03 | **PENDIENTE**: no certificar ni abrir C04 antes de aprobación expresa |

Este calendario técnico deriva de la [propuesta C03](planificacion-futura/C03_MICROFASES_PROPUESTAS.md) ya contrastada con el Word y queda subordinado a su texto; no reescribe las ocho puertas normativas ni certifica fases futuras.

## Alcance de la primera microfase y arquitectura real

- **Backend físico actual:** `Crecisol.Domain` puro; `Crecisol.Application` → Domain; `Crecisol.Infrastructure` → Application (adaptador MySqlConnector); `Crecisol.Api` → Application e Infrastructure. Comprobar con `ProjectReference` reales, ciclos, paquetes y compilación .NET 10, sin crear nuevos módulos de negocio.
- **Frontend actual:** una aplicación pública Angular 22 con rutas allowlist y laboratorio `pattern-harness` aislado, no las dos apps sugeridas por el Maestro. No añadir login, editor, shells privados ni guards como autorización.
- **Base de datos actual:** tabla/lectura pública de marca C01 en MySQL 8.4. Las ubicaciones de datos shared/dedicated, control de propiedad tenant y operaciones privadas siguen pendientes. No crear migraciones, DSL de seguridad ni conexiones productivas.
- **Plan futuros módulos:** Identity, Tenants, Catalog, Orders, Payments, Scheduling, Memberships, Media, Analytics y Cases separado para G.7; workers/outbox, DTO/OpenAPI y adaptadores externos solo como contratos futuros.
- **P0 transversal:** aislamiento por tenant/sucursal/recurso en backend, login normal sin selector de empresa, SuperAdmin excepcional con MFA/motivo/expiración/auditoría, no saltar TenantContext, no usar input cliente para DSN, no ejecutar configuración no confiable. Los módulos privados todavía no permiten pruebas runtime BOLA/IDOR y no se simula certificación de ellas.

## Puerta de salida C03-01

Auditoría **A**: fuente Word real y matriz 16 IDs; **B**: diez mutaciones adversariales y denegación de avances/cambios ilegales; **C**: grafo .NET exacto, Angular público, .NET10/Angular22 y MySQL8.4/API solo de publicación pública mediante CI real. Revisión del diff, secretos, enlaces, impacto en permisos/PII, rollback por revert, CI PREMERGE y POSTMERGE del SHA exacto. El [acta C03-01](c03/C03_01_ACTA_MICROFASE.md) registra aceptación humana literal «CERTIFICA POR FAVOR LA MICROFASE 1 DEL BLOQUE 3», evidencia 7/7 CI POSTMERGE del PR técnico y decisión **C03-01 CERRADA TÉCNICAMENTE (1/10)**. Su formalización definitiva se subordina a **todas** las CI PREMERGE/POSTMERGE del PR documental de cierre, sin confundirlas con el PR técnico. **Capítulo C03 NO CERTIFICADO, C04 BLOQUEADO**.

**Ambiente y costes:** exclusivamente documentación, QA, CI y código de prueba; sin modificar `frontend/src/`, `backend/src/`, `database/` ni `main`, SOLQARYN, VPS, DNS, hosting, pagos o producción. No activar automatizaciones ni servicios externos.
