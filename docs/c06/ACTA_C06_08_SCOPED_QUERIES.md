# CRECISOL — Acta final C06-08: aislamiento de consultas, listados y paginación

**Fecha:** 25-11-2026, Honduras. **Dictamen: C06-08 CERTIFICADA EN SU ALCANCE TÉCNICO, 9.5/10. SÍ aceptaría/compraría esta microfase delimitada.** Cierre institucional eficaz al integrar esta acta con A/B/C aprobadas. C06 8/11 microfases; capítulos completos 5/37; C06 global NO CERTIFICADO. C06-09 habilitada solamente para su desarrollo secuencial tras el cierre, sujeto a no aparecer una regresión esencial. C07 bloqueado.

## 1. Autoridad, alcance y criterio de cierre

Repositorio original ID 1450414592, ubicación vigente `crecisol-platform/CRECISOL`. AGENTS.md, Skill canónica, docs/MASTER_REFERENCE.md y registro vigente. Maestro Word/PDF inalterado: auditoría A comprobó SHA-256 y blob, 1,876 elementos originales del cuerpo. Word SHA-256 ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c. C06-07 ratificada 9.5/10; 14/14 POST técnicos y 6/6 POST institucionales aprobados antes de abrir esta microfase. [Especificación técnica y antecedentes](C06_08_ESPECIFICACION.md). El estado inicial NO CERTIFICADA de la especificación queda como historial de la candidata; el presente acta y el registro más reciente gobiernan el cierre. No se modifica el maestro ni se reescriben certificados anteriores.

El objetivo se limita a las consultas operativas existentes de identidad: aislar las filas recuperadas, límites, orden, búsquedas ya previstas y totales derivados del conjunto autorizado; no habilitar nuevos endpoints, permisos, recursos futuros, buscadores generales ni exportaciones. Autorización real del backend antes de cualquier consulta privada. No confiar en selectores enviados por navegador. C06-01 a C06-07 conservados y revalidados en las regresiones aplicables.

## 2. Entregables funcionales y arquitectura

| Superficie | Control implementado y verificado |
|---|---|
| Buzón memberships/pending | SQL parametrizado con tenant exacto, membresía/identidad/sesión revalidadas y grant tenant-wide único vigente bajo bloqueo compartido; cuenta verificada, estado pendiente y exclusión del actor propio. Permiso de sucursal no habilita inventario de empresa. Orden (created_at_utc, membership_id), 12 filas por página + una para determinar continuación. No offset ni total global. |
| Continuación | HMAC-SHA256, propósito versionado, vigencia 600 segundos, comparación de firma en tiempo constante, contexto formado desde host verificado, tenant, actor, membresía, sesión, grant y revisiones confiables. Cursor de posición visible, firmado pero no cifrado; no contiene PII ni otorga autoridad. Revalidación en cada página. Rechazo de cursor legado, manipulado, expirado, de otra sesión/tenant y revisiones obsoletas. |
| Sesiones tenant | Tenant, actor y membresía exacta, revisiones, factor activo, expiración e inactividad. Orden estable por última actividad e ID. Máximo contractual 4096, SQL 4097 para detectar exceso y denegar en vez de truncar silenciosamente. |
| Sesiones plataforma | Operador autenticado y revisión propios, mismas cotas contractuales. Inventarios de distintos operadores no se mezclan. |
| browser/restore | No recupera inventario; valida sesión y devuelve el recibo de transporte existente, sin modificar el DTO público ni el flujo de Angular. |
| security/events | Tenant y actor propio, o sujeto propio cuando no existe actor; máximo 50, orden por fecha e ID. Pruebas mismo actor en dos tenants y otro actor del mismo tenant. |
| passkeys | Cuenta autenticada global según diseño de identidad C05, máximo ocho, detección de exceso, orden estable; sin exponer otros usuarios. |
| platform/abuse-summary / platform/cases/find | Agregados restringidos al ámbito de plataforma y cuatro clases permitidas; búsqueda exacta de caso ya existente bajo reautenticación y motivo, no se añade búsqueda libre. |
| Recuperación asistida | Consultas de revisores y partes acotadas al caso/tenant o caso de plataforma; límite 3 para conservar el invariante de exactamente dos sin lectura ilimitada. |

Angular 22/Node 24 conserva código, diseño institucional, contratos y acciones anteriores. .NET 10 conserva separación API/Application/Domain/Infrastructure; cursor y consultas en Infrastructure, adaptación de transporte en API; no nuevos paquetes ni cambios de criptografía de credenciales. MySQL 8.4 conserva esquema lógico, roles y grants; añade exclusivamente cinco índices compuestos. Las pantallas afectadas se ejercitaron en Chromium real: primera página, carga siguiente, actualización, revocación de autorización de continuación, limpieza de filas y reinicio seguro. Se verificaron 8 aserciones Angular específicas de C06-08. No se afirma rediseño o nueva evaluación visual individual; UI nueva N/A, regresión funcional y workflow UX anterior SUCCESS.

## 3. Persistencia, reversibilidad y seguridad

Migración 023 (forward/down): pending por tenant/estado/fecha/ID, sesiones tenant por tenant/cuenta/membresía/revocación/actividad/ID, sesiones plataforma por revisión/operador/revocación/actividad/ID, eventos por tenant/sujeto/actor/fecha/ID, señales por fecha/tipo. La posición revisión/operador preserva el índice implícito de la FK de operador; ambos son predicados de igualdad. Pruebas D sobre migraciones 001–022 previas y dos ciclos de 023; comparación completa de metadatos de índices originales y FKs, filas y grants; E repite integridad antes/después de su recorrido HTTP/Chromium. MySQL real demostró SQL ERROR 1142 al intentar DROP INDEX desde runtime autenticado, con SELECT exitoso antes/después. No se deshabilitan FKs, no se cambia ningún grant, no se modifican migraciones anteriores. EXPLAIN en fixture comprueba posibilidad de uso del índice; no es medición de SLA ni volumen de producción.

Rollback técnico: retirar 023 mediante su down en entorno aislado y revertir PR #212 coordinadamente, sin desactivar autorización ni privacidad; preservar registros y metadatos previos. La reversión de producto requiere reabrir C06-08 y bloquear C06-09 hasta revalidación. No operar datos productivos.

## 4. Historial de defectos y resolución

- PRE 45641579789: A/B/C aprobadas; D falló en arranque por nombre de contenedor inválido; la regresión de MySQL de C06-02 también detectó una condición de arranque. Se corrigen sufijo PID y disponibilidad TCP del MySQL definitivo. No se considera PASS la ejecución fallida.
- PRE 45642143005: A/B/C y las doce regresiones independientes SUCCESS. E aprobó las aserciones de aislamiento y 540 comprobaciones HTTP, pero falló al retirar ix_c06_08_operator_sessions con ERROR 1553 (FK); D no acreditó correctamente denegación DDL. Se ajustó índice y test: prueba autenticada positiva y denegación por privilegios, nunca error de conexión; comparación íntegra de metadatos antes/después. No se certifica esta ejecución.
- PRE 45660058699: A/B/C/D/E 5/5 SUCCESS, 13 workflows aplicables SUCCESS, cinco omitidos por condición. El nuevo árbol corrige ambos defectos; no atribuye resultados de esta revisión a los SHA anteriores.

## 5. Alcance exacto y trazabilidad

PR [#212](https://github.com/crecisol-platform/CRECISOL/pull/212), base `ff5de0670d5a09e31b32c3b6aff7e78c5449fee8`, head `6640cd914608d9907633a60c573dd78cdaf3f105`, merge virtual PRE `9d7db9b7152b45ca0c81fd99205c929df831fb7f`, merge real en dev `990ae64da906ec07f16c87bdbd1b476cfd16b257`. Árbol idéntico `b81b7940b14561b612005b163d0f33ad3aa38763` y mismos padres del merge verificados en GitHub; 28 archivos exactos según qa/c06_08_query_scope.py, cinco commits, cero divergencia de base. Diff revisado: sin cambios en frontend, maestro, catálogo/roles, dependencias, grants ni migraciones previas. Los auditores predecesores mantienen sus controles y admiten solo el alcance exacto de este sucesor. Alcance registrado:

- `.github/workflows/c06-08-scoped-queries.yml`
- `backend/src/Crecisol.Api/Modules/Identity/OperationalIdentityModule.cs`
- `backend/src/Crecisol.Infrastructure/Identity/C06PendingCursor.cs`
- `backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.Assisted.cs`
- `backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.Authorized.cs`
- `backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.OperatorRecovery.cs`
- `backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.Passkeys.cs`
- `backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.PendingApprovals.cs`
- `backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.Platform.cs`
- `backend/tests/Crecisol.C06Query.ContractsTests/Crecisol.C06Query.ContractsTests.csproj`
- `backend/tests/Crecisol.C06Query.ContractsTests/Program.cs`
- `database/migrations/023_c06_scoped_query_indexes.down.sql`
- `database/migrations/023_c06_scoped_query_indexes.sql`
- `database/tests/c06_02_prepared_roles_mysql.sh`
- `database/tests/c06_08_scoped_query_mysql.sh`
- `docs/c06/C06_08_ESPECIFICACION.md`
- `qa/c05_operational_flow_e2e.mjs`
- `qa/c05_sha1_exclusive_audits.py`
- `qa/c06_02_role_model_audit.py`
- `qa/c06_03_context_audits.py`
- `qa/c06_04_abac_audits.py`
- `qa/c06_05_stepup_audits.py`
- `qa/c06_06_exceptional_scope.py`
- `qa/c06_07_privacy_audits.py`
- `qa/c06_07_privacy_scope.py`
- `qa/c06_08_query_audits.py`
- `qa/c06_08_query_checks.mjs`
- `qa/c06_08_query_scope.py`

## 6. Evidencia real PRE por SHA

[Workflow definitivo 45660058699](https://github.com/crecisol-platform/CRECISOL/actions/runs/45660058699). Los cinco logs confirman checkout del mismo merge virtual `9d7db9b7152b45ca0c81fd99205c929df831fb7f`.

| Auditoría | Job | Evidencia |
|---|---|---|
| A — requisitos y trazabilidad | 139367175244 | SUCCESS. Maestro Word/PDF y blob intactos, 1,876 elementos, catálogo y diff exacto. |
| B — seguridad y contratos | 139367174667 | SUCCESS. 33 comprobaciones del cursor firmado, negativos de integridad/alcance/caducidad, 255 comprobaciones de privacidad para 79 operaciones y WebAuthn real. |
| C — arquitectura y compilación | 139367174098 | SUCCESS. .NET 10 sin errores ni advertencias, Angular 22/Node 24 compilado; 16/16 pruebas del oráculo/relay de privacidad. |
| D — MySQL 8.4 e integridad | 139367174984 | SUCCESS. 5 índices exactos, conteos aislados, desempate binario, privilegios intactos y ERROR 1142 real; doble reversión sin residuos, índices/FK/filas previos preservados. |
| E — integración completa | 139367174750 | SUCCESS. 474 comprobaciones integradas; 540 comprobaciones HTTP de privacidad, 70 formas exitosas distintas, 149 respuestas con cabecera browser, 148 observadas en relay de navegador, dos enrolamientos MFA. 8 aserciones Angular C06-08; rollback exitoso preserva todos los índices y FKs originales. Logs sin credenciales, correo ni recurso privado en los casos ejecutados. |

13 workflows PRE aplicables SUCCESS:

- [CRECISOL foundation check — 45660058692](https://github.com/crecisol-platform/CRECISOL/actions/runs/45660058692).
- [CRECISOL C05 SHA1 only cutover security A-B-C MySQL — 45660058611](https://github.com/crecisol-platform/CRECISOL/actions/runs/45660058611).
- [CRECISOL C06-03 trusted actor tenant membership A-B-C — 45660058901](https://github.com/crecisol-platform/CRECISOL/actions/runs/45660058901).
- [CRECISOL C06-04 ABAC ownership and same-branch IDOR A-B-C-E2E — 45660058595](https://github.com/crecisol-platform/CRECISOL/actions/runs/45660058595).
- [CRECISOL C06-05 sensitive operation step-up A-B-C-E2E — 45660058724](https://github.com/crecisol-platform/CRECISOL/actions/runs/45660058724).
- [C05 operational identity Angular API MySQL — 45660058868](https://github.com/crecisol-platform/CRECISOL/actions/runs/45660058868).
- [CRECISOL C06-08 scoped query isolation five audits A-B-C-D-E — 45660058699](https://github.com/crecisol-platform/CRECISOL/actions/runs/45660058699).
- [CRECISOL C06-06 exceptional access five audits A-B-C-D-E — 45660058641](https://github.com/crecisol-platform/CRECISOL/actions/runs/45660058641).
- [CRECISOL C05 institutional modals A-B-C UX security — 45660058663](https://github.com/crecisol-platform/CRECISOL/actions/runs/45660058663).
- [CRECISOL C06-02 prepared roles and scoped capabilities A-B-C — 45660058866](https://github.com/crecisol-platform/CRECISOL/actions/runs/45660058866).
- [CRECISOL C06-01 inventory and canonical permission boundaries — 45660058798](https://github.com/crecisol-platform/CRECISOL/actions/runs/45660058798).
- [CRECISOL C03-10 integral A-B-C chapter gate — 45660058614](https://github.com/crecisol-platform/CRECISOL/actions/runs/45660058614).
- [CRECISOL C06-07 privacy minimization five audits A-B-C-D-E — 45660058835](https://github.com/crecisol-platform/CRECISOL/actions/runs/45660058835).

Cinco omitidos por condición: 45660058739, 45660058672, 45660058818, 45660058645, 45660058718; no son pruebas aprobadas. Los conteos de contratos, HTTP, E2E y regresiones se superponen y no se suman como pruebas únicas.

## 7. Valoración y límites de aceptación

**9.5/10 técnico; SÍ aceptaría/compraría esta microfase en el alcance local auditado.** Fundamento: trazabilidad exacta, autorización previa, resultados reales de aislamiento horizontal, límites deterministas, rechazo de filtros/cursor ajenos, UI funcional sin regresión, arquitectura y reversibilidad demostradas. Sin P0/P1 abierto identificado en la evidencia ejecutada. La nota es una valoración técnica del asistente, no una medición estadística, una garantía universal ni una nueva puntuación visual del sistema completo.

Límites explícitos: el buzón usa una vista viva (no snapshot multiversión); el corte temporal de cada consulta excluye altas posteriores al reloj confiable de esa consulta, sin prometer instantánea inmutable entre páginas. Cursor firmado, no cifrado. Inventarios de sesión conservan su máximo de 4096 sin paginación nueva; el exceso se deniega y no se presenta como inventario completo. La búsqueda y el conteo se limitan a los contratos operativos existentes; no se certifican filtros genéricos ni nuevos módulos, ni C06-09 (medios/exportaciones/bitácoras), ni la matriz ofensiva integral C06-10, ni la certificación global C06-11. La escala y latencia productivas, pruebas de carga con datos reales y evaluación visual independiente quedan fuera de esta microfase, no se simulan. No producción, VPS, cobros, cambios SOLQARYN ni main. main se conserva en `b794181148d2c5e36c1c88127f19a8a9082acff5`; GitHub informa que no está protegida; no se altera configuración.

POST automático del merge técnico observado al redactar: 17 workflows registrados, 2 SUCCESS, 0 FAILURE, 15 sin conclusión y 0 con otra conclusión. C06-08 POST #45670671071; no se atribuye PASS a ejecuciones pendientes. El cierre técnico se apoya en el PRE real del árbol integrado idéntico y la decisión de autonomía del propietario; antes de abrir código C06-09, comprobar que no haya regresión esencial posterior conocida. Si aparece, detener y corregir.

**C06 8/11; capítulos completos 5/37; C06 global NO CERTIFICADO; C06-09 habilitada tras integrar esta acta, C06-10/C06-11 pendientes, C07 bloqueado.**
