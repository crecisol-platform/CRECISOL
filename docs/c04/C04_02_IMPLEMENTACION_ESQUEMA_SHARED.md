# CRECISOL — C04-02 · Implementación acotada del modelo SHARED y lectura interna

**Fecha:** 12-11-2026. **Repositorio vigente:** `crecisol-platform/CRECISOL`, ID `1450414592`. **Base de trabajo:** `dev` `6ef9f5ea3c807bfb5f57e8c2851ef9140e267335`. **PR:** #86, borrador, NO fusionado. **Estado:** IMPLEMENTACIÓN EN PRUEBAS, **NO CERTIFICADO**. **C04-03 BLOQUEADA**.

## 1. Autoridad, límites y dependencias

El único Documento Maestro es `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx`, SHA-256 `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c` (PDF `0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77`); inventario C04 de **72 párrafos** SHA-256 `95be2bb0210e108317e67bd08fb9b2899d33554e684bb2d9ae3c8eced6995058`. `MAE-C04-01..08` son puertas de proceso, no equivalen a las microfases técnicas `C04-01..12`.

C04-01 fue certificada **solo en alcance de contratos Domain/Application**, sin catálogo real. PR #85 integró la **preparación** C04-02 (fixture MySQL 8.4 aislado, no migración). Esta propuesta es distinta: DDL aplicativo y adaptador de lectura sin activación, autorización productiva ni API.

## 2. Alcance técnico y arquitectura

| Archivo nuevo | Responsabilidad | Límite |
| --- | --- | --- |
| `database/c04_02_shared_schema.sql` | Tablas `c04_tenants`, `c04_branches`, `c04_resources`, `c04_resource_events`; PK/FK compuestas, `UNIQUE` scoped, `RESTRICT`, triggers que impiden reasignar ownership hacia otros padres VÁLIDOS y bloquean UPDATE/DELETE de eventos | DDL aplicable solo a base nueva desechable, no migration `up` de bases existentes |
| `backend/src/Crecisol.Application/Tenancy/ReadSharedTenantResource.cs` | Puerto que resuelve un ámbito autorizado **antes** de leer; actor sin permiso = denegación; valida resultado contra tenant, branch e ID | No acepta tenant ni branch de request; no registra resolvedor productivo |
| `backend/src/Crecisol.Infrastructure/Tenancy/MySqlRfc4122Guid.cs` | Conversión UUID↔`BINARY(16)` con orden RFC 4122 y rechazo de GUID cero/longitud incorrecta | Sin normalización implícita de orden de bytes |
| `backend/src/Crecisol.Infrastructure/Tenancy/MySqlSharedTenantResourceReader.cs` | `SELECT` parametrizado por `tenant_id`, `branch_id`, `resource_id`; salida validada; sin fallback ni SQL dinámico | DSN suministrado solo por backend confiable; nunca desde navegador |
| `backend/tests/Crecisol.SharedTenancy.ContractsTests/` | Pruebas reales .NET 10 de GUID, scope, denegaciones, lector malicioso y MySQL A↔B con política de prueba | La política simulada NO demuestra pertenencia de un actor real |
| `database/tests/c04_02_backend_reader_smoke.sh` | MySQL 8.4 efímero con puerto **solo loopback** requerido por .NET host, cuenta de solo lectura y rechazos SQL | Runner CI desechable; jamás accesible externamente |
| `qa/c04_02_implementation_audits.py` y workflow propio | A/B/C, mutaciones, pruebas unitarias/integración y evidencia SHA/run | Ningún PASS sustituye la autorización productiva C04-05/06 |

**Frontend:** ningún cambio. **API:** ningún endpoint nuevo, ningún servicio en DI. **Domain:** C04-01 intacta. **Application/Infrastructure/Database:** separados, sin EF Core nuevo ni dependencia circular. Las tablas históricas de C01/C03 no se alteran, renombrarán ni migran por inferencia.

## 3. Política de seguridad obligatoria

1. Un GUID, host, slug, cookie o cabecera nunca es permiso. `IAuthorizedSharedReadScopeResolver` devolverá un ámbito solamente tras verificar identidad, membresía, acción, sucursal y, para datos privados, propiedad del recurso. Su implementación REAL permanece pendiente de C04-05/06; ningún endpoint la usa hoy.
2. El caso de uso no acepta parámetros tenant/sucursal del cliente y deniega antes de consultar si no hay ámbito autorizado. Aunque un adaptador malicioso devuelva otro tenant, branch o recurso, la respuesta se rechaza.
3. La SQL filtra expresamente los tres IDs en `WHERE` y utiliza parámetros binarios. Se verifica contra dos tenants con identificadores de sucursal/recurso repetidos. La PK/FK NO impide por sí sola un UPDATE hacia un tenant/sucursal destino VÁLIDO; por eso el esquema tiene cinco triggers de identidad inmutable/bitácora append-only, probados con ataques SQLSTATE 45000 (ERROR 1644). Las FK tampoco evitan un `SELECT` sin predicado: queda documentado, sin atribuir row-level security a MySQL.
4. La prueba del lector usa credenciales creadas solo en CI, `GRANT SELECT` sobre `c04_resources` exclusivamente. No puede actualizar, borrar, eliminar tablas ni enumerar el catálogo. Se destruye el contenedor al finalizar, con acceso a puerto aleatorio vinculado a `127.0.0.1`.
5. No se añaden permisos SuperAdmin, escritura de datos, modelos de negocio, router dedicated, archivos/Cloudflare, cachés, colas, backups o migraciones sobre tablas C01/C03. Esas superficies permanecen separadamente bloqueadas.

## 4. Tres auditorías, evidencia y puerta de salida

- **A / normativa:** Word/PDF y SHA exacto; 72 párrafos y contenido `FK compuestas con tenant`; correspondencia C04-02 con la planificación; diff solo en su alcance; reversibilidad.
- **B / seguridad:** mutaciones adversariales individuales del DDL, triggers, contrato, SQL y runner; pruebas A→B/B→A hacia destinos inexistentes **y existentes**, branch, evento append-only; autorizador ausente/error/cancelación; permisos SQL mínimos y prohibición de rutas privadas no autorizadas.
- **C / ingeniería:** .NET 10 compilado realmente, MySqlConnector existente 2.6.2 sin paquetes nuevos, pruebas de RFC4122 y MySQL 8.4 en runner; evidencias A/B/.NET/MySQL mismo SHA/run; índices, regresiones C03/C04-01 y CI PREMERGE/POSTMERGE al integrar.

**NO_GO:** si falla algo obligatorio, no hay PR listo, integración, certificación ni paso a C04-03. Los resultados históricos fallidos permanecen en GitHub, corregidos solo mediante nuevas ejecuciones y SHAs.

**Deuda explícita fuera de C04-02:** C04-05/06 implementarán autenticación efectiva, membresía y autorización real; C04-03/09 dedicado y migraciones seguras; C04-10/11 cachés, trabajos y BOLA/IDOR HTTP. Un test con resolvedor falso jamás certifica esas capas. Los bloqueos deben figurar en acta para impedir una exposición prematura.

## 5. Reversión y restricciones de ejecución

Eliminar/revertir exclusivamente los nuevos commits del PR #86 si no superan las auditorías. El DDL **nunca se ha aplicado sobre una base persistente de CRECISOL**; bases de CI se destruyen. C01/C02/C03, `dev`, `main`, maestros Word/PDF, SOLQARYN, VPS, DNS, Cloudflare y producción permanecen sin alteraciones por esta implementación.

**Dictamen actual:** PR de implementación en revisión, **C04-02 NO CERTIFICADO**, **C04-03 BLOQUEADA**, capítulos certificados **3/37**.
