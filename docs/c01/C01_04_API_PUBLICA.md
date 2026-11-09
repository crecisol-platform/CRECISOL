# C01-04 · Caso de uso y API pública de marca publicada

**Estado:** **C01-04 CERRADA TÉCNICAMENTE EN DEV**, con 3 auditorías A/B/C, MySQL 8.4.13 real y PR #18 fusionado SHA `9addbedd3c33c77c9be15fe25a391f9167a05ef5`. Capítulo C01 sigue **NO CERTIFICADO (0/37)**. Fuente: `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx` aprobado, huella en `docs/MASTER_REFERENCE.md`, matriz C01-01, contratos C01-02 y esquema probado C01-03.

## Alcance aprobado y trazabilidad

| Fuente | Obligación y puerta observable |
| --- | --- |
| P04, P06, T01-R04, T02-R07, T03-R02 | Separar marca global/tenant; nunca obtener datos privados/tenant por URL, host o identificador del cliente. |
| P07, T01-R03 | Tema, iconos, textos y SEO son datos tipados; rechazo de markup, CSS/JS arbitrarios, rutas remotas o SVG activo. |
| P14, P25 | Nombre/lema/descriptor/SEO/favicon/copyright se leen de la revisión publicada, no de literales esparcidos por la API. |
| P24, T03-R04 | Host se limita por configuración **local explícita**; no afirmar propiedad del dominio ni usar Host para resolver permisos. |

## Implementación de C01-04

- `Crecisol.Application`: `IPublishedPlatformBrandReader` define un único puerto, sin argumentos scope, tenant o DSN; `ReadPublishedPlatformBrand` exige `BrandSnapshot.RequirePublicPlatform()` y produce DTO `PublicPlatformBrand` sin ScopeId, estado interno ni tokens de edición.
- `Crecisol.Infrastructure`: MySqlConnector 2.6.2 referenciado exclusivamente en el adaptador, fijado a `brand_publications` y `brand_revisions`. SQL fijo selecciona únicamente `scope_kind = 1`, GUID vacío y `publication_status = 2`, con FK compuestas ya probadas en C01-03. `DATETIME(6)` se interpreta como UTC y el dominio revalida cada campo leído antes de exponerlo. Conexión de **solo lectura** recibida desde variable del servidor, nunca del cliente.
- `Crecisol.Api`: **solo** `GET /api/v1/public/branding/platform`, anónimo y sin rutas privadas. No hay endpoints tenant, draft, preview, admin, cambios, autenticación, upload ni publicación. Ningún negocio obtiene privilegios por Host. La lista `CRECISOL_C01_ALLOWED_HOSTS` es obligatoria y explícita; la falta de configuración impide iniciar. `CRECISOL_C01_READONLY_DSN` se proporciona únicamente en el proceso servidor; si falta, la API responde 503 y **no** utiliza una copia de demo como respaldo.

## Contrato HTTP cerrado

| Resultado | Comportamiento |
| --- | --- |
| 200 | Únicamente DTO de identidad PLATFORM Published ya validado. `Cache-Control: no-store`; JSON sin cookies, credenciales ni identificadores tenant. |
| 400 | Host no permitido, Forwarded/X-Forwarded-Host no confiable, tenant/Authorization header o query no admitida. |
| 404 | No hay puntero publicado PLATFORM o ruta inexistente (admin/preview/tenant). No sustituir por marca ajena. |
| 405 | Métodos POST/PUT/PATCH/DELETE/OPTIONS en el endpoint GET. |
| 503 | Falta DSN, error de conexión o fila inválida desde almacenamiento. Respuesta sin errores SQL ni datos sensibles. |

No se habilita CORS ni cookies; las vistas Angular de C01-05 deberán decidir un consumo **same-origin** seguro. El Host permitido en pruebas es **127.0.0.1 únicamente**, no `crecisol.com` ni un dominio empresarial sin verificación. Queda prohibido usar este servicio como autorización de un tenant.

## Pruebas y auditorías requeridas

- **Compilación real** ASP.NET Core 10 y dependencias, compilación y 51 pruebas Domain anteriores y nueva consola Application.
- **Integración real** con MySQL 8.4 en contenedor descartable, aislado mediante `--network none`, socket UNIX local, usuario creado durante el test con solo dos `GRANT SELECT`. Rechazo de UPDATE por permisos. Datos **sintéticos**: borrador de plataforma, revisión de plataforma publicada y marca tenant diferenciada.
- **Seguridad HTTP real:** evitar fuga tenant en GET, falsificación Host/Forwarded/X-Tenant-ID, query injection, Authorization, CORS, cookies, rutas privadas y verbos de escritura. Responder 404 al retirar publicación; responder 503 al invalidar un registro y al quitar configuración. Restablecer condiciones en base desechable.
- **Auditoría A:** trazabilidad Word y esquema/puertos/DTO/casos positivos.
- **Auditoría B:** auth privado ausente por diseño, denegación por defecto, inputs/tenant/SQL/Host, errores sin fuga, no secrets y test negativo real.
- **Auditoría C:** arquitectura limpia, proveedor/versiones, UTC, no frontend, reversibilidad, rendimiento de consulta indexada y alcance del PR.

El workflow read-only `.github/workflows/c01-04-check.yml` ejecutará tres trabajos separados sobre un PR `feature/c01-04-*`. No se declara PASS sin leer jobs, logs y SHA del último commit.

## Riesgos no resueltos y límites

No existen pertenencia de actor, auth/RBAC/ABAC, tenant verified host, shared/dedicated router, límite de sucursal, expedientes, auditoría privada, escritura versionada, compare-and-swap, rollback operativo ni gestión de secretos de producción. La consulta global pública **no certifica aislamiento horizontal de datos privados**, que no se exponen en C01-04. Cualquier ruta privada nueva sería P0/NO GO hasta implementar control del servidor y pruebas BOLA/IDOR. La base es efímera, no se conecta ni despliega en Hostinger, VPS, Cloudflare, DNS ni dominio real.

## Impacto y reversión

- `frontend/`: sin cambios (C01-05).
- `backend/`: proyectos Application, Infrastructure y API públicos mínimos; Domain inalterado, sin servicios privados, sin guardar credenciales.
- `database/`: esquema C01-03 inalterado; solo test de integración para DB desechable; no migraciones productivas.
- `qa/`, `.github/`, `docs/`: pruebas y auditorías específicas.

**Reversión:** revertir PR C01-04. El test elimina el contenedor y socket temporales; no hay registros persistidos ni ambiente operativo que restaurar. La fase siguiente será **C01-05**, solo tras cierre individual C01-04 y sin certificar el capítulo: 0/37.

## Primera evidencia de validación

[CI #42556858527](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42556858527) — 3/3 trabajos SUCCESS: API .NET 10 Release sin errores/advertencias; Domain 51/51, Application 19/19, MySQL real 8.4.13 con SELECT-only, 37/37 solicitudes HTTP positivas/negativas y auditorías A/B/C PASS. [Acta C01-04](C01_04_ACTA_MICROFASE.md). La integración se condiciona a CI del último commit y revisión manual del PR #18; no es certificación del capítulo.

## Cierre validado en GitHub

[CI final C01-04 #42557022367](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42557022367) SUCCESS: 3/3 trabajos (auditorías A/B/C, build net10, Domain 51, Application 19, 37 HTTP, MySQL real). [Fundación del mismo SHA #42557022442](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42557022442) SUCCESS; [PR #18](https://github.com/solqaryn-platform/CRECISOL/pull/18) merged, SHA `9addbedd3c33c77c9be15fe25a391f9167a05ef5`, [post-merge #42557167001](https://github.com/solqaryn-platform/CRECISOL/actions/runs/42557167001) SUCCESS. [Acta de cierre](C01_04_ACTA_MICROFASE.md). **4/8 microfases cerradas, 0/37 capítulos certificados**; C01-05 es la siguiente fase.
