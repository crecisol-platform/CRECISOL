# CRECISOL · C04-02 — Implementación propuesta del esquema SHARED

**Fecha:** 12-11-2026. **Repositorio:** `crecisol-platform/CRECISOL` (GitHub ID `1450414592`). **Base:** `dev` `6ef9f5ea3c807bfb5f57e8c2851ef9140e267335`. **Estado:** BORRADOR TÉCNICO; NO CERTIFICADO.

## Alcance deliberado
Se añade `database/c04_02_shared_schema.sql` como **esquema aplicativo propuesto**, separado del fixture desechable `database/tests/fixtures/c04_02_shared_integrity_fixture.sql`. Define tenant, sucursal, recurso y eventos mediante PK/FK compuestas. Las relaciones `ON UPDATE/DELETE RESTRICT` evitan la reasignación cruzada o el borrado accidental del historial dependiente. Unicidad de `external_key` por tenant+sucursal. No se aplica automáticamente en ninguna base, no contiene datos ni DSN, no crea endpoints ni permisos. Esta entrega NO habilita persistencia privada hasta que existan controles backend probados.

## Controles obligatorios pendientes antes de integrar
1. Demostrar en MySQL 8.4 real la ejecución del **DDL aplicativo** (no solo el fixture), la inspección de `information_schema`, casos positivos y ataques A↔B (insert/update/delete/duplicados/ID cero/errores SQL).
2. Demostrar conversión de `Guid` en orden RFC4122 a `BINARY(16)` mediante vectores de ida/vuelta y rechazo de ids inválidos. Revisar ADR-002/provider antes de incorporar adaptador.
3. Demostrar autorización backend de identidad, membresía, tenant, sucursal, rol/acción y propiedad, y predicados aplicados a **cada consulta privada**. Un SELECT sin filtro sigue leyendo A+B aunque las FK sean perfectas.
4. Auditar A: coherencia con Word SHA-256 `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c`, especificación C04 y compatibilidad con C01/C03. Auditar B: mutaciones de FK, consultas BOLA/IDOR y permisos SQL. Auditar C: esquema, índices, performance, compatibilidad .NET 10/MySQL 8.4 y regresiones.
5. CI PREMERGE y POSTMERGE del SHA exacto; registrar ejecuciones reales y cero fallos vigentes. Mientras falte uno de estos requisitos, dejar el PR en DRAFT y C04-03 BLOQUEADA.

## Impacto y reversión
`frontend/`: ninguno. `backend/`: ninguno. `database/`: nuevo DDL no aplicado. API, contratos, credenciales, migraciones productivas y Word/PDF intactos. Reversión de la propuesta: eliminar el archivo en la rama (ningún rollback de datos necesario porque no se aplica). En caso de un ensayo desechable, usar una base nueva vacía y eliminarla por completo tras la prueba. No ejecutar sobre bases existentes. SOLQARYN, `main` y producción excluidos.

**Dictamen:** preparación MySQL del PR #85 se preserva; esquema real propuesto para revisión, **C04-02 no certificada**.
