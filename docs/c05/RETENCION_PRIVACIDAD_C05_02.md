# CRECISOL — C05-02: tratamiento de datos de registro vencidos (ensayo aislado)

**Fecha:** 2026-11-13. **Repositorio exclusivo:** `crecisol-platform/CRECISOL` (ID 1450414592). **Base:** `dev` en `673536be4f79b9d8fb64ad17d806c16ee3cdcd43` después de PR #128. **Microfase:** C05-02 únicamente; **NO CERTIFICADA FUNCIONALMENTE; C05-03 BLOQUEADA**. Sin cambios en main, Word/PDF, API productiva, frontend, SOLQARYN, VPS, DNS, Cloudflare, pagos o despliegues.

## Autoridad y hallazgo

El Documento Maestro Único de CRECISOL y `docs/MASTER_REFERENCE.md` gobiernan la privacidad, el aislamiento por tenant, el registro no enumerador y las invitaciones de un solo uso. Word SHA-256 histórico `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c`; PDF de lectura `0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77`. Las huellas se comprobarán mediante el guard existente; no afirmar recálculo hasta ejecutar la CI.

La ingesta neutral de PR #119 protegía el correo mediante AES-GCM y las huellas mediante HMAC por tenant. En el fixture MySQL no existía un proceso verificable de disposición tras `expires_at_utc`: caducidad lógica NO equivale a eliminación ni anonimización. Una retención indefinida de sobres cifrados y cuotas constituye riesgo P1 de privacidad; no se debe presentar como resuelto en producción.

## Cambio acotado y modelo de riesgo

- `database/tests/fixtures/c05_02_privacy_retention_fixture.sql`: extensión **solo descartable** de MySQL 8.4, cargada después de los fixtures existentes, sin migración productiva. `c05_02_privacy_dispositions` registra una sola disposición por PK tenant+intent con FK al registro; rutina `c05_02_dispose_expired_registration_pii` **SQL SECURITY DEFINER** toma límite 1–100, reloj UTC de SQL y una transacción. Marca solo intents caducados, sobrescribe nonce, tag, ciphertext, huella HMAC del buzón y huella HMAC del peer con valores aleatorios, preservando PK/FK e invariantes de uso único. La respuesta de mantenimiento es siempre ACK, sin correo, tenant, conteos ni secretos.
- Limpia ventanas antiguas de cuota con lote limitado y conserva la actual y la anterior. Los plazos de 30 minutos/15 minutos y 100 filas son **parámetros de ensayo** de infraestructura, NO políticas administrables de producción.
- `database/tests/c05_02_privacy_retention_mysql.sh`: usa imagen MySQL 8.4 fijada por digest, Docker temporal y contraseñas aleatorias. Comprueba dos tenants (uno suspendido), lotes sucesivos, replay idempotente, rechazo de tamaños inválidos, datos vigentes inalterados, HMAC/ciphertext obsoletos sustituidos, limpieza de ventanas de cuota, integridad de FK y rol de mantenimiento con permiso **EXECUTE** solamente, no SELECT/UPDATE directos.
- `qa/c05_02_privacy_retention_audits.py` y `.github/workflows/c05-02-privacy-retention.yml`: A/B/C, mutaciones estáticas, compilación/regresión .NET10 y prueba MySQL 8.4 con la misma huella SHA/ejecución. Ningún endpoint, worker o programador productivo se activa.

**Límite de seguridad explícito:** esta es **disposición lógica a nivel de filas**, NO borrado físico garantizado de páginas InnoDB, binlogs, backups, snapshots, réplicas ni claves históricas. Las huellas de destinatario en otras tablas de pruebas requieren su propio ciclo de vida. Se necesitan políticas configurables de retención, custodia y rotación de llaves, eliminación en backups, legal hold, métricas de operaciones y verificación shared/dedicated antes de producción. Ningún adaptador de esta entrega debe registrarse en DI o ejecutarse contra bases reales.

## A/B/C y puerta NO-GO

- **A — normativa/funcional:** Documento Maestro, C05-01 y C05-02, cambio restringido a test/QA/docs/workflow, expiración UTC, no sesiones ni membresías, pruebas que no destruyan registros vigentes.
- **B — seguridad:** ataques a aislamiento multitenant, lotes nulos/extremos, repetición, FK y mínimos privilegios SQL, 45+ mutaciones de guardias textuales rechazadas; se exige **ejecución real MySQL** porque una mutación textual no certifica retención.
- **C — arquitectura/evidencia:** rama exclusiva, diff exacto de cinco archivos, auditorías y MySQL 8.4 más .NET10 en el mismo SHA/run, cero cambios productivos y sin despliegues.

**Estado al redactar:** PR y CI por ejecutar/verificar. Si alguna auditoría, SQL real, regresión o procedencia de código falla, no fusionar ni certificar el incremento. Los P0/P1 restantes de identidad del invitante/destinatario, prueba de buzón externo, consentimiento/membresía real, no enumeración de cuentas en producción y protección contra abuso distribuido siguen abiertos. **C05-02 NO CERTIFICADA FUNCIONALMENTE y C05-03 BLOQUEADA.**

**Rollback:** revertir exclusivamente el squash futuro de este conjunto de cinco archivos en `dev` tras revisar dependencias. Como no hay migración productiva ni API activada, no hay estado real que revertir; Docker destruye los ensayos y las claves de prueba.
