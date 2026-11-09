# C01-03 — esquema relacional mínimo para identidad versionada

**Estado:** EN VALIDACIÓN, NO CERTIFICADA. El PR #15 se fusionó a `dev` en `c476f937ee49661b4e971a9bab2c70b24bfa89d1` antes de la prueba MySQL y la auditoría triple; esto NO cerró la microfase. Corrección en rama `feature/c01-03-validacion-integridad` desde ese SHA. La fuente es el Documento Maestro Único verificado en `docs/MASTER_REFERENCE.md`, la matriz C01-01 y los contratos C01-02.

## Propósito y trazabilidad
- `brand_scopes` materializa `BrandScope` (Platform=1 con Guid.Empty / Tenant=2 con Guid no vacío) sin inferir permiso de acceso.
- `brand_revisions` representa `BrandSnapshot`, estados Draft=1 y Published=2 y presentación opcional; las revisiones son claves compuestas por ámbito.
- `brand_publications` apunta mediante FK compuesta a una revisión **Published** del mismo ámbito. Nada del navegador selecciona una base dedicada o un tenant.
- Requisitos C01-01: P04/P06, P07, P14/P25/P31, T01-R03/R04, T02-R01/R02/R03, T03-R01/R02. Contratos: `docs/c01/C01_02_CONTRATOS.md`.

## Alcance
Solo DDL de diseño, sin conexión a MySQL Hostinger, sin migración ejecutada, datos reales, seed institucional, APIs, autenticación, carga de logos, preview o publicación ejecutable. Separación `frontend/`, `backend/`, `database/` intacta. El contrato .NET usa `Guid` y `DateTimeOffset`; el futuro adaptador debe establecer explícitamente orden de bytes UUID, UTC y validación de longitudes antes de persistir. `DATETIME(6)` no transporta zona horaria: tratarlo únicamente como UTC en el adaptador. Los `CHECK` de SQL son defensa adicional, no reemplazan `BrandValidation`, ni la autorización en backend.

**Pendiente C01-04+:** integridad referencial con el catálogo tenant autorizado cuando exista; revalidar que scope tenant corresponde al actor/host, validación estricta de URL y activos, política de inmutabilidad (sin UPDATE/DELETE en revisiones), publicación transaccional de forma compare-and-swap, auditoría y restauración, acceso dedicado/shared mediante router del servidor. No conceder permisos anónimos a tablas tenant o draft.

## Ajustes derivados de la revisión del modelo

Se alinearon los límites de almacenamiento con el Domain net10: URL origen 255, título SEO 120, descripción SEO 260 y copyright 220 caracteres; la revisión se limita a long.MaxValue positivo. Se añadieron CHECKs de activos locales PNG/WebP, tokens tipados y presentación completa/opcional. Los identificadores siguen siendo BINARY(16) y el adaptador deberá convertir GUID explícitamente. No se configura ningún proveedor .NET/MySQL, host o conexión. La compatibilidad de un proveedor .NET real con la persistencia queda **pendiente de selección y ensayo en C01-04**, no se simula.

**Límite de seguridad del esquema:** el DDL no implementa permisos de filas ni un rol de base por tenant. La FK niega referencias inconsistentes *entre ámbitos*, pero una consulta ejecutada por un actor con privilegios de lectura sin filtros puede ver filas de todos los tenants. El backend debe resolver host/identidad/membresía y aplicar scope/acción; no hay lectura pública de tenants. Además, se requiere política **inmutable** para revisiones, autorización por operación, auditoría y concurrencia al escribir/publicar; estas capacidades son NO GO para exponer rutas antes de C01-04+.

## Pruebas y puertas
Comprobar en MySQL **8.4 LTS efímero y aislado**: DDL, insertar identidad Platform sintética con Draft/Published, publicar solo revisión Published del mismo scope, negar ámbitos inválidos, revisiones 0, estado inválido, Draft apuntado, referencias cruzadas y color inválido; probar rollback/reversión de DDL en base descartable. Los casos deben ejecutar comandos reales con logs y código de salida. Script: `database/tests/c01_03_mysql_smoke.sh` (9 aserciones positivas y 22 casos negativos explícitos). Tres auditores por separado en `qa/c01_03_three_audits.py`, además de regresión de contratos Domain .NET 10. Workflow específico `.github/workflows/c01-03-check.yml` para PRs `feature/c01-03-*`, sin secretos, red MySQL ni despliegues. Las pruebas y las auditorías no se consideran PASS hasta verificar las ejecuciones GitHub Actions.

- Auditoría A: trazabilidad de tabla/campo/FK con C01-02 y matriz maestra.
- Auditoría B: negación cross-scope, no credenciales/DSN, no acceso privado ni exposición pública.
- Auditoría C: índice compuesto, normalización, integridad, compatibilidad real MySQL 8.4 y reversibilidad.
- **NO GO para merge/cierre** mientras no existan ejecución real y tres auditorías con evidencia. `C01` sigue 0/37 certificados.

## Reversión
Para un esquema **de prueba** vacío, eliminar en este orden: `brand_publications`, `brand_revisions`, `brand_scopes`. No se ha migrado ninguna base real. La configuración futura se suministrará por variables protegidas y resolución server-side, nunca desde frontend ni repositorio.
