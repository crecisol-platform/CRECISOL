# CRECISOL — C04-02 · Acta de preparación del modelo relacional shared (CONDICIONADA)

**Fecha:** 12 de noviembre de 2026, Honduras. **Repositorio:** `crecisol-platform/CRECISOL` (ID 1450414592). **Base prevista:** `dev` SHA `9cd099cc38549f6c6dc321c0e200b36dc9308075`. **Destino:** `dev`. **Estado de este borrador:** NO EFICAZ hasta completar sus propias CI PREMERGE/POSTMERGE.

**Alcance autorizado:** solo **PREPARACIÓN C04-02**, sin implementación de persistencia real, migraciones, APIs, usuarios, conexión a tenants o despliegues. Ninguna prueba de un fixture puede certificar las consultas autorizadas del producto.

## 1. Puertas de entrada y fuente normativa

El [acta de C04-01](https://github.com/crecisol-platform/CRECISOL/pull/84#issuecomment-6827626750) acredita exclusivamente contratos internos Domain/Application (1/12). GitHub verificó 7/7 workflows SUCCESS en su SHA `9cd099cc38549f6c6dc321c0e200b36dc9308075`. No se arrastra ninguna falla de CI vigente, sin reescribir el run histórico fallido del PR #83.

Word canónico SHA-256 `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c`, PDF `0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77`; capítulo C04 con huella de texto `95be2bb0210e108317e67bd08fb9b2899d33554e684bb2d9ae3c8eced6995058`, 72 párrafos. Plan técnico C04-02: modelo relacional shared tenant-aware, FK e índices compuestos probados en MySQL 8.4 y rechazo de relaciones cruzadas A↔B.

## 2. Entregables y auditorías a comprobar

- [Especificación del modelo y riesgos](C04_02_PREPARACION_MODELO_SHARED.md): cuatro relaciones propuestas, claves tenant/sucursal, amenazas SH-01..16, dependencias ADR-002/004 y exclusión de autorización de sesión.
- `database/tests/fixtures/c04_02_shared_integrity_fixture.sql`: **cuatro tablas desechables** llamadas `c04_02_fixture_*`, definidas solo en `database/tests/fixtures/` para MySQL 8.4. No son tablas reales/migraciones del SaaS.
- `database/tests/c04_02_shared_integrity_smoke.sh`: contenedor aislado `--network none`, imagen MySQL fija por digest, arranque final verificado (sin recaer en la carrera histórica), escenarios positivos/negativos SQL, lector de privilegios mínimos; falla si un ataque funciona o si la infraestructura falla.
- `qa/c04_02_preparation_audits.py`: **A** trazabilidad Word/FK/amenazas; **B** mutaciones adversariales de SQL, runner y documentación; **C** resultados MySQL reales y pruebas A/B vinculadas a un `GITHUB_SHA/GITHUB_RUN_ID` común.
- `.github/workflows/c04-02-preparation.yml`: seguridad de token `contents: read`, checkout sin persistencia, sin rutas de escritura o secretos de producción.

**Advertencia de seguridad:** MySQL con FKs compuestas impide ciertas relaciones de datos cruzadas, pero **NO impide un SELECT sin filtro ni autoriza a un usuario**. Debe existir autenticación, membresía, acción, sucursal y recurso revisados por backend en sus capítulos; sin ello C04-02 no puede considerarse implementada. Tampoco se afirma RLS, pruebas BOLA/IDOR del API, importación de datos existentes, aislamiento de cachés/archivos o compatibilidad EF Core hasta desarrollar esas superficies.

## 3. Puertas de eficacia, sin aceptar fallas

1. **PREMERGE** del HEAD final: auditorías A/B/C con SQL real, pruebas C04-01/C03 disparadas, cero FAIL/ERROR y revisión del diff. Los checks `skipped` solo se admiten si sus condiciones son ajenas al alcance y se documentan, nunca se cuentan como SUCCESS.
2. **Fusión controlada** exclusiva a `dev` con SHA exacto de integración, sin cambiar `main`, Word/PDF, SOLQARYN, frontend, backend, rutas productivas, DNS, Cloudflare, VPS ni servicios pagados.
3. **POSTMERGE** del SHA nuevo: mismos gates obligatorios y regresiones en `completed/success`. Registrar run IDs, SHA y fallas reparadas en un comentario permanente del PR.
4. **Aprobación de auditoría de riesgos:** no existe P0/P1 activo en el alcance de **preparación**; P0 de acceso a tenant ajeno sigue bloqueando el paso a persistencia/producción hasta que el backend lo mitigue y pruebe.

**Solo después** podrá declararse: **C04-02 PREPARACIÓN VALIDADA E INTEGRADA**. No implica **C04-02 CERTIFICADA** ni habilita C04-03. Total de capítulos institucionalmente certificados permanece **3/37**; C04 completo no certificado.

## 4. Reversibilidad y exactitud histórica

Ante falla, mantener PR bloqueado o revertir exclusivamente su merge preparatorio por SHA; preservar las evidencias anteriores y conservar la diferencia entre pruebas reales de MySQL y seguridad de aplicación todavía no ejecutada. C04-01 y el Documento Maestro Único no se alteran. Esta acta carece de firma criptográfica o notarial y no suplanta la aprobación basada en CI posterior.
