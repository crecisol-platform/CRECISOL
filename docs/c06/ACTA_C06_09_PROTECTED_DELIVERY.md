# CRECISOL — Acta final C06-09: descargas, exportaciones y bitácoras protegidas

**Fecha:** 25-11-2026, Honduras. **Dictamen: C06-09 CERTIFICADA EN SU ALCANCE TÉCNICO, 9.5/10. SÍ aceptaría/compraría esta microfase delimitada.** Cierre institucional eficaz al integrar esta acta con A/B/C aprobadas. C06 9/11 microfases; capítulos completos 5/37; C06 global NO CERTIFICADO. C06-10 habilitada para desarrollo secuencial después del cierre y si no existe regresión esencial posterior conocida. C06-11 pendiente; C07 bloqueado.

## 1. Autoridad y alcance

Repositorio ID 1450414592, ubicación actual `crecisol-platform/CRECISOL`, verificada desde la referencia original. AGENTS.md, Skill canónica, Maestro Word/PDF y registro vigente. Auditoría A verificó SHA-256 y blob de Maestro y 1,876 elementos originales. Word SHA-256 ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c. C06-08 ratificada en 9.5/10, 17/17 POST técnicos y 6/6 POST institucionales comprobados antes de abrir C06-09. [Especificación e historial de la candidata](C06_09_ESPECIFICACION.md). El encabezado NO CERTIFICADA de dicha especificación es histórico, no se altera el maestro ni certificados anteriores.

Protección de la entrega privada existente: autorización por tenant, identidad, propietario, acción y recurso, validación del contenido, minimización de eventos, separación de ámbito operador/tenant, caché y diagnóstico sin datos privados. Las acciones normativas media.private.read, media.private.export y finance.report.export permanecen no registradas y no grantables; no se anticipa activación de C21/C32 ni módulos de almacenamiento o exportación.

## 2. Entregables y arquitectura

| Superficie | Control implementado |
|---|---|
| identity/profile/photo | POST propio, tenant/cuenta autorizados en transacción; raster PNG RGBA 64x64 revalidado y reconstruido antes de entrega. Sin URL ni metadatos de contenedor. |
| platform/profile/photo | POST del operador autenticado, ámbito separado; otro operador y bearer tenant denegados. |
| Raster | Contrato compartido Application C06ProtectedContent, usado por Infrastructure y API, sin dependencia nueva. Tres chunks IHDR/IDAT/IEND, CRC, límite 24,576 bytes y descompresión acotada a 16,448 bytes, filtro cero; reencodificación sin reenviar bytes comprimidos originales. Entrada malformada se rechaza o, para bytes anexados al flujo zlib, se reconstruye sin esos bytes. |
| identity/private/read | ABAC C06-04 obligatorio, misma fila bloqueada, referencia ASCII alfanumérica más guion y guion bajo, máximo 120 caracteres. No URL, ruta ni capacidad de acceso. |
| platform/breakglass/read | Conserva política excepcional C06-06, vigencia, MFA y auditoría; misma validación de referencia, contenido inválido denegado. |
| identity/security/events | Consulta C06-08 limitada al actor/tenant, máximo 50; acción de catálogo o etiqueta neutral, resultado 0/1, fecha; sin texto libre arbitrario, HMAC ni identificadores internos. No se modifica el registro original. |
| platform/abuse-summary | Agregado de plataforma existente; no permite acceso al registro individual. |
| API ingress | no-store, private, same-origin en identity, platform-identity, private, media, downloads, exports y logs, incluso 400/404/405. Las rutas futuras siguen sin registrarse, incluso ante bearer real o firma manipulada. |
| ILogger | Proveedor único de API que clasifica fuente, severidad y EventId numérico; nunca renderiza formatter, mensaje, scope, excepción, categoría libre ni datos de petición. Resultado HTTP numérico conserva observabilidad básica; evidencia SQL durable separada. |

Angular 22/Node 24 no cambia diseño ni contratos. .NET 10 conserva Application/Infrastructure/API y Domain de autorización; sin dependencias nuevas. MySQL 8.4 conserva el esquema, registros, grants y migraciones originales. Los recorridos reales validan fotos y referencia existentes, credenciales cruzadas, regeneración de foto, eliminación y estados privados. Regresión UX anterior SUCCESS; nueva UI N/A. No se afirma nueva evaluación visual individual.

## 3. Persistencia, seguridad y reversibilidad

MySQL real comprueba que el runtime autenticado puede realizar su lectura/escritura de auditoría tenant autorizada, pero recibe seis ERROR 1142: update/delete de auditoría tenant, SELECT de auditoría plataforma, update/delete de auditoría plataforma, DROP TABLE de auditoría tenant. ERROR 1452 impide perfil sin membresía del tenant y ERROR 3819 estado de foto inválido. Ocho operaciones rechazadas con código exacto; ninguna se confunde con error de conexión. Grantees, evidencia original y filas permanecen intactos. No hay migración nueva ni cambio de grants.

Rollback: revertir PR #214 en dev, coordinado con retiro de la ratificación si se reabre una regresión. No DDL, migración de datos, operación productiva ni desactivación de autorización. La lectura de fotos no incorpora cifrado de almacenamiento; photo_png conserva su esquema. No se acredita protección frente a un administrador DB comprometido. El filtrado de diagnóstico cubre el canal ILogger de la API, no otros procesos, volcados, respaldos o infraestructura. Los códigos numéricos reducen detalle libre de diagnóstico deliberadamente.

## 4. Historial y correcciones

- PRE 45696887241: A/C/D aprobadas; B falló al comparar Base64 con JSON textual, que puede representar caracteres mediante escapes. Se corrigió la comparación del campo JSON decodificado; se añadió prueba de compresión y corrección del ID hexadecimal del operador. No se atribuye PASS a la ejecución fallida.
- PRE 45697214588: C06-09 A/B/C/D/E 5/5 SUCCESS, 15/16 workflows aplicables. C06-04 #45697214149 tuvo E2E SUCCESS, pero A/B/C fallaron al buscar literalmente la antigua instrucción de retorno. Se actualizó su auditor estático para exigir denegación ABAC, extracción posterior y única devolución condicionada a validación de referencia, en ese orden. Cinco mutaciones adicionales comprueban el rechazo de cambios inseguros. No se alteró el producto entre esta revisión y la definitiva.
- PRE 45716942933 y regresión 45716943110: cinco auditorías C06-09 y cuatro C06-04 SUCCESS; las quince regresiones y el workflow de C06-09, 16/16 aplicables, SUCCESS. Se preservan los fallos anteriores como historial.

## 5. Alcance y SHA

PR [#214](https://github.com/crecisol-platform/CRECISOL/pull/214), base `da67bfe38940edd25842b2f963090e3d1406ce45`, head `af865e81a1ff8ace8fbbae3f824fe89a11a71477`, merge virtual PRE `4670574540f8008e74efcd73c6105d952d8dc15c`, merge real en dev `01e27372689fa3494727eebc30bd0b71db56d174`. Árbol exacto `85b4badbd881abb755f363d764b1c2af8dcb4ff9` verificado por GitHub, mismos padres. Seis commits, 30 archivos exactos, cero divergencia de base; no modifica frontend, dependencias de producto, catálogo, roles, permisos, migraciones, Maestro o main. Los auditores predecesores conservan sus invariantes y admiten el alcance exacto de este sucesor. Archivos:

- `.github/workflows/c06-09-protected-delivery.yml`
- `backend/src/Crecisol.Api/Configuration/C06SafeRuntimeLoggerProvider.cs`
- `backend/src/Crecisol.Api/Contracts/C06IdentityResponseProjection.cs`
- `backend/src/Crecisol.Api/Program.cs`
- `backend/src/Crecisol.Application/Identity/C06ProtectedContent.cs`
- `backend/src/Crecisol.Infrastructure/Identity/CanonicalProfilePng.cs`
- `backend/src/Crecisol.Infrastructure/Identity/MySqlOperationalIdentityFlow.Profile.cs`
- `backend/src/Crecisol.Infrastructure/Tenancy/C06CaseAbacProjection.cs`
- `backend/src/Crecisol.Infrastructure/Tenancy/C06ExceptionalAccess.cs`
- `backend/tests/Crecisol.C06Delivery.ContractsTests/Crecisol.C06Delivery.ContractsTests.csproj`
- `backend/tests/Crecisol.C06Delivery.ContractsTests/Program.cs`
- `backend/tests/Crecisol.C06Privacy.ContractsTests/Program.cs`
- `database/tests/c06_09_delivery_mysql.sh`
- `docs/c06/C06_09_ESPECIFICACION.md`
- `qa/c05_operational_flow_e2e.mjs`
- `qa/c05_operator_recovery_checks.mjs`
- `qa/c05_profile_browser_checks.mjs`
- `qa/c05_sha1_exclusive_audits.py`
- `qa/c06_02_role_model_audit.py`
- `qa/c06_03_context_audits.py`
- `qa/c06_04_abac_audits.py`
- `qa/c06_05_stepup_audits.py`
- `qa/c06_06_exceptional_scope.py`
- `qa/c06_07_privacy_audits.py`
- `qa/c06_07_privacy_scope.py`
- `qa/c06_08_query_checks.mjs`
- `qa/c06_08_query_scope.py`
- `qa/c06_09_delivery_audits.py`
- `qa/c06_09_delivery_checks.mjs`
- `qa/c06_09_delivery_scope.py`

## 6. Evidencia PRE definitiva

[Workflow C06-09 #45716942933](https://github.com/crecisol-platform/CRECISOL/actions/runs/45716942933). Logs de cinco jobs confirman checkout del merge virtual `4670574540f8008e74efcd73c6105d952d8dc15c`.

| Auditoría | Job | Resultado |
|---|---|---|
| A — requisitos y trazabilidad | 139565303593 | SUCCESS; Maestro Word/PDF y blob, 1,876 elementos, catálogo y diff exacto. |
| B — seguridad | 139565303414 | SUCCESS; 80 comprobaciones de entrega, 33 comprobaciones de cursor y 255 comprobaciones de privacidad de 79 operaciones, WebAuthn real. |
| C — arquitectura | 139565303803 | SUCCESS; .NET sin errores ni advertencias, compilación Angular 22/Node 24 y 16/16 del oráculo/relay. |
| D — persistencia | 139565303021 | SUCCESS; MySQL 8.4, ocho operaciones indebidas rechazadas (1142, 1452, 3819), evidencia y grants intactos. |
| E — integración | 139565303370 | SUCCESS; 528 comprobaciones integradas, 54 verificaciones específicas C06-09, 574 comprobaciones HTTP de privacidad, 73 formas exitosas, 149 respuestas con cabecera browser, 148 observadas en relay de navegador, dos enrolamientos MFA; regresiones, rollback anterior y canarios de logs aprobados. |

[Regresión C06-04 #45716943110](https://github.com/crecisol-platform/CRECISOL/actions/runs/45716943110): A 139565304234, B 139565304224, C 139565304201 y E 139565303712, cuatro SUCCESS. B registra 66 mutaciones estructurales y ejecuta .NET/MySQL reales, E registra 351 comprobaciones integradas, incluyendo rechazo de acceso IDOR entre sucursales, actores y tenants y rollback.

16 workflows PRE aplicables SUCCESS:

- [CRECISOL foundation check — 45716943047](https://github.com/crecisol-platform/CRECISOL/actions/runs/45716943047).
- [CRECISOL C06-01 inventory and canonical permission boundaries — 45716943029](https://github.com/crecisol-platform/CRECISOL/actions/runs/45716943029).
- [CRECISOL C06-03 trusted actor tenant membership A-B-C — 45716942805](https://github.com/crecisol-platform/CRECISOL/actions/runs/45716942805).
- [CRECISOL C04-02 SHARED application A-B-C dotnet10 MySQL8.4 — 45716942792](https://github.com/crecisol-platform/CRECISOL/actions/runs/45716942792).
- [CRECISOL C06-02 prepared roles and scoped capabilities A-B-C — 45716943262](https://github.com/crecisol-platform/CRECISOL/actions/runs/45716943262).
- [CRECISOL C05 SHA1 only cutover security A-B-C MySQL — 45716943125](https://github.com/crecisol-platform/CRECISOL/actions/runs/45716943125).
- [CRECISOL C05 institutional modals A-B-C UX security — 45716942890](https://github.com/crecisol-platform/CRECISOL/actions/runs/45716942890).
- [CRECISOL C03-08 local typed configuration A-B-C — 45716943333](https://github.com/crecisol-platform/CRECISOL/actions/runs/45716943333).
- [CRECISOL C03-10 integral A-B-C chapter gate — 45716942788](https://github.com/crecisol-platform/CRECISOL/actions/runs/45716942788).
- [CRECISOL C06-04 ABAC ownership and same-branch IDOR A-B-C-E2E — 45716943110](https://github.com/crecisol-platform/CRECISOL/actions/runs/45716943110).
- [C05 operational identity Angular API MySQL — 45716942790](https://github.com/crecisol-platform/CRECISOL/actions/runs/45716942790).
- [CRECISOL C06-05 sensitive operation step-up A-B-C-E2E — 45716942806](https://github.com/crecisol-platform/CRECISOL/actions/runs/45716942806).
- [CRECISOL C06-07 privacy minimization five audits A-B-C-D-E — 45716943311](https://github.com/crecisol-platform/CRECISOL/actions/runs/45716943311).
- [CRECISOL C06-06 exceptional access five audits A-B-C-D-E — 45716943230](https://github.com/crecisol-platform/CRECISOL/actions/runs/45716943230).
- [CRECISOL C06-08 scoped query isolation five audits A-B-C-D-E — 45716943082](https://github.com/crecisol-platform/CRECISOL/actions/runs/45716943082).
- [CRECISOL C06-09 protected delivery and audit five audits A-B-C-D-E — 45716942933](https://github.com/crecisol-platform/CRECISOL/actions/runs/45716942933).

Seis omitidos por condición: 45716942799, 45716943210, 45716943070, 45716942800, 45716943157, 45716942862. No son aprobaciones. Los conteos de contratos, E2E y regresiones se superponen, no se suman como pruebas únicas.

## 7. Dictamen y condiciones

**9.5/10 técnico; SÍ aceptaría/compraría la microfase en su alcance auditado.** Fundamento: entrega revalidada por ámbito, reconstrucción de medios, referencias restringidas, bitácoras minimizadas, diagnóstico cerrado, evidencia SQL preservada, compilaciones y pruebas reales de regresión. Sin P0/P1 abierto identificado en la evidencia ejecutada. La calificación es juicio técnico, no garantía universal, medición estadística, evaluación visual nueva ni certificación de producción.

No existe todavía un servicio de exportación, almacenamiento externo o URL firmada funcional; las pruebas comprueban que no se activan por rutas o credenciales manipuladas. Cuando se implementen esos módulos, exigir autenticación del destinatario y revalidación por tenant, membresía, acción, sucursal, recurso y revisión al entregar contenido, caducidad y alcance vinculados, no credenciales en URL ni logs. No se certifica esa capacidad futura. No se acredita cifrado at-rest, seguridad de infraestructura ni pruebas de carga productivas. No cambios de frontend, main, SOLQARYN, VPS, producción o cobros. main conserva `b794181148d2c5e36c1c88127f19a8a9082acff5`; GitHub informa que no está protegida, no se cambió configuración.

POST técnico observado al redactar: 21 workflows registrados, 3 SUCCESS, 0 FAILURE, 18 sin conclusión, 0 con otra conclusión. C06-09 POST #45720845103. No se atribuye PASS a repetición pendiente. La aceptación se fundamenta en PRE real del árbol exacto integrado y la decisión de autonomía del propietario. Antes de iniciar código C06-10, comprobar que no haya regresión esencial posterior conocida; si aparece, detener y corregir.

**C06 9/11; capítulos completos 5/37; C06 global NO CERTIFICADO; C06-10 habilitada tras integrar esta acta, C06-11 pendiente, C07 bloqueado.**
