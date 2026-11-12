# CRECISOL — C04-01 · Acta de cierre técnico de contratos internos (condicionada)

**Fecha:** 12 de noviembre de 2026, Honduras.  
**Repositorio:** `crecisol-platform/CRECISOL` (ID GitHub `1450414592`); destino de integración `dev`.  
**Microfase:** C04-01 — catálogo confiable de tenants. **Alcance estrictamente limitado:** contratos internos tipados Domain/Application y pruebas, sin almacenamiento, alta ni exposición HTTP.  
**Estado del acta antes de la fusión:** AUDITORÍAS DE CONTRATOS APROBADAS, FORMALIZACIÓN PENDIENTE de CI PREMERGE/POSTMERGE del PR #83. **No declarar C04-01 CERTIFICADA por redactar esta acta**.

## 1. Autoridad y alcance de la implementación

**Puerta C03 satisfecha:** [PR #81](https://github.com/crecisol-platform/CRECISOL/pull/81#issuecomment-6812682477), C03 CERTIFICADO 10/10, `3/37` capítulos; [PR #82](https://github.com/crecisol-platform/CRECISOL/pull/82#issuecomment-6824560520) dejó preparación C04-01 auditada y 2/2 POSTMERGE SUCCESS.

**Documento Maestro Único:** Word SHA-256 `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c`, PDF `0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77`; C04 extraído de Word con SHA-256 de texto `95be2bb0210e108317e67bd08fb9b2899d33554e684bb2d9ae3c8eced6995058`, 72 párrafos indexados `1735..1806`. [Matriz completa](C04_01_IMPLEMENTACION_CONTRATOS.md) separa **8 puertas del Word** de **12 microfases técnicas**; no se modifica el maestro.

**Entregado:** `TenantId` tipado, `TenantCatalogSnapshot` inmutable, nombres válidos y normalización NFC, estados `Draft/PendingVerification/Active/Suspended/Deactivated`, modalidades `PathSlug/Subdomain/CustomDomain`, versión y UTC, ToString redactado. Application lee solo mediante `ITrustedTenantCatalogReader` y exige ANTES `IPlatformCatalogReadAuthorization`; rechaza identidades cambiadas o respuestas cruzadas y proyecta exactamente cinco campos internos. Ningún puerto escribe, crea, publica o resuelve host/DSN.

**NO entregado:** implementación productiva del puerto de autorización/membresía, almacenamiento de catálogo, DB MySQL, modelo shared/dedicated, validación DNS, login privado, DI de servicio, endpoints administrativos, provisión atómica, transición de suspensión, cache/archivos de tenants y pruebas de aislamiento cross-tenant reales. Estos riesgos son bloqueantes para sus respectivas microfases, no aprobaciones implícitas. **C04 como capítulo no está implementado ni certificado.**

## 2. Auditoría A — trazabilidad y función de contratos

[Run PREMERGE de contratos #43240599882](https://github.com/crecisol-platform/CRECISOL/actions/runs/43240599882), commit `02e857389e8aea9f236b1cb619fd1a678a37126c`: `PASS C04-01 A` con Word íntegro, 72 párrafos C04 y siete invariantes normativas extraídas, correspondencia 12 vs 8 puertas, fuente canónica e identidad de C03. Se verificaron código Domain/Application, DTO interno y proyecto de pruebas. No hay cambios en Word/PDF ni SOLQARYN.

## 3. Auditoría B — seguridad y negativas ejecutadas

Mismo run: `PASS C04-01 B` para **19 mutaciones de límites de confianza rechazadas**, sin API privada ni DDL tenant; prueba real de `ReadInternalTenantCatalog` deniega antes de leer para actor no autorizado, ID forjado, reader que devuelve otro tenant, cancellation en tres momentos, fallos de permisos y almacenamiento, formatos de datos y propiedades de DTO. **El ejecutor de prueba usa dobles simulados** para los puertos sin implementación productiva, por lo que no se afirma BOLA/IDOR de HTTP ni separación real entre dos bases/usuarios. Exponer nuevas rutas sin identidad real sigue prohibido (P0).

## 4. Auditoría C — arquitectura, calidad y regresiones

El mismo run compiló `Crecisol.Api` y la suite `Crecisol.TenantCatalog.ContractsTests` en **Release .NET 10 con 0 warnings, 0 errors**, luego ejecutó **88 aserciones C04-01**, 35 Domain C03-02, 37 Application C03-03 y 57 contratos C03-05 API, todos PASS. El gate `PASS C04-01 C` vinculó fuentes A/B y dotnet al mismo `GITHUB_SHA/GITHUB_RUN_ID`, comprobando proyecto limpio sin dependencias nuevas. La primera ejecución #43240358422 falló por `CS8602` en el **test**, corregido en la siguiente ejecución; se conserva como historial, no se transforma en SUCCESS.

**Rendimiento/UX:** no hay interfaz ni I/O de catálogo; benchmarking p95, a11y y MySQL 8.4 **NO APLICAN a los contratos puros** y se deben probar en las microfases que activen cada superficie. La suite integral C03-10 del PR #83 es regresión independiente obligatoria si GitHub la programa.

## 5. Condiciones de eficacia (puertas del PR #83)

1. Revisar diff contra `dev`, permisos, secretos y riesgos, y esperar que **todos los workflows PREMERGE obligatorios del último HEAD** terminen `SUCCESS` (los checks `skipped` por condición no cuentan como aprobaciones).
2. Fusionar solo a `dev`, con SHA de integración exacto y maestros binarios, `main`, SOLQARYN y producción intactos.
3. Obtener CI POSTMERGE sobre **el SHA integrado**: gate C04-01 A/B/C .NET10, Foundation, preparación y regresiones aplicables en `SUCCESS`. Registrar run IDs y salida en comentario del PR después de verificarlos.
4. No se identifican P0/P1 **dentro de la superficie interna implementada** y todos los negativos aplicables están ejecutados; los riesgos de capacidades futuras se mantienen documentados.

**Solo cumplidas las cuatro:** `C04-01 CERTIFICADA EN SU ALCANCE DE CONTRATOS INTERNOS` (1/12 microfases técnicas) y `C04-02 HABILITADA SOLO PARA PREPARACIÓN`; C04 sigue **NO CERTIFICADO** y total de capítulos continúa **3/37**. Mientras alguna condición falte: **C04-01 PENDIENTE DE CERTIFICACIÓN; C04-02 BLOQUEADA**. Ningún resultado autoriza desplegar, conectar DSN, publicar tenants ni saltar C04-02..12.

## 6. Rollback y límites transversales

Revertir por SHA **solo** el merge de código/documentación C04-01 si aparece falla de seguridad o CI; no revertir C03, Word/PDF, `main` ni tocar `solqaryn/Solqaryn`. El repositorio está público, pero no hay infraestructura real ni datos productivos; mantener variables y tokens fuera de archivos y logs.

**Responsable de la aceptación:** propietario autorizó expresamente continuar C04-01 con prioridad a calidad; aceptación de integración condicionada a evidencias reales y auditorías. No se presume firma criptográfica ni validación en producción.
