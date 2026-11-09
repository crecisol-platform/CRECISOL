# C01-03 — esquema relacional mínimo para identidad versionada

**Estado:** PROPUESTA EN REVISIÓN, NO CERTIFICADA. Rama `feature/c01-03-mysql-brand` desde `dev` SHA `01d75456c6f16dcf543f9ad5736463e4f5622f61`. La fuente es el Documento Maestro Único verificado en `docs/MASTER_REFERENCE.md`, la matriz C01-01 y los contratos C01-02.

## Propósito y trazabilidad
- `brand_scopes` materializa `BrandScope` (Platform=1 con Guid.Empty / Tenant=2 con Guid no vacío) sin inferir permiso de acceso.
- `brand_revisions` representa `BrandSnapshot`, estados Draft=1 y Published=2 y presentación opcional; las revisiones son claves compuestas por ámbito.
- `brand_publications` apunta mediante FK compuesta a una revisión **Published** del mismo ámbito. Nada del navegador selecciona una base dedicada o un tenant.
- Requisitos C01-01: P04/P06, P07, P14/P25/P31, T01-R03/R04, T02-R01/R02/R03, T03-R01/R02. Contratos: `docs/c01/C01_02_CONTRATOS.md`.

## Alcance
Solo DDL de diseño, sin conexión a MySQL Hostinger, sin migración ejecutada, datos reales, seed institucional, APIs, autenticación, carga de logos, preview o publicación ejecutable. Separación `frontend/`, `backend/`, `database/` intacta. El contrato .NET usa `Guid` y `DateTimeOffset`; el futuro adaptador debe establecer explícitamente orden de bytes UUID, UTC y validación de longitudes antes de persistir. `DATETIME(6)` no transporta zona horaria: tratarlo únicamente como UTC en el adaptador. Los `CHECK` de SQL son defensa adicional, no reemplazan `BrandValidation`, ni la autorización en backend.

**Pendiente C01-04+:** integridad referencial con el catálogo tenant autorizado cuando exista; revalidar que scope tenant corresponde al actor/host, validación estricta de URL y activos, política de inmutabilidad (sin UPDATE/DELETE en revisiones), publicación transaccional de forma compare-and-swap, auditoría y restauración, acceso dedicado/shared mediante router del servidor. No conceder permisos anónimos a tablas tenant o draft.

## Pruebas y puertas
Comprobar en MySQL **8.4 LTS efímero y aislado**: DDL, insertar identidad Platform sintética con Draft/Published, publicar solo revisión Published del mismo scope, negar ámbitos inválidos, revisiones 0, estado inválido, Draft apuntado, referencias cruzadas y color inválido; probar rollback/reversión de DDL en base descartable. Los casos deben ejecutar comandos reales con logs y código de salida.

- Auditoría A: trazabilidad de tabla/campo/FK con C01-02 y matriz maestra.
- Auditoría B: negación cross-scope, no credenciales/DSN, no acceso privado ni exposición pública.
- Auditoría C: índice compuesto, normalización, integridad, compatibilidad real MySQL 8.4 y reversibilidad.
- **NO GO para merge/cierre** mientras no existan ejecución real y tres auditorías con evidencia. `C01` sigue 0/37 certificados.

## Reversión
Para un esquema **de prueba** vacío, eliminar en este orden: `brand_publications`, `brand_revisions`, `brand_scopes`. No se ha migrado ninguna base real. La configuración futura se suministrará por variables protegidas y resolución server-side, nunca desde frontend ni repositorio.
