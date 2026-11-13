# CRECISOL — C05-07 · Verificación de correo y cambios sensibles

**Estado inicial:** EN DESARROLLO, NO CERTIFICADA; **C05-08 BLOQUEADA**. Fecha: 2026-11-13. Repositorio exclusivo `crecisol-platform/CRECISOL` (ID 1450414592). Base `dev` `4a97dbd29b3acc93e9c20ae09e0e26945af98ea0`, rama `feature/c05-07-mailbox-sensitive-changes-20261113`. Solamente código, documentación y pruebas desechables. Sin cambios en `main`, SOLQARYN, API productiva, Word/PDF, frontend, VPS, DNS, Cloudflare ni cobros.

## 1. Fuente, precedente y requisito exacto

Autoridad: Documento Maestro Único `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx`, registro `docs/MASTER_REFERENCE.md`, Word SHA256 `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c`, PDF de lectura `0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77`; capítulo 05 extraído en [CI #43364292640](https://github.com/crecisol-platform/CRECISOL/actions/runs/43364292640). Plan derivado `docs/planificacion-futura/C05_MICROFASES_PROPUESTAS.md`:

**C05-07 | Verificación de correo y cambios sensibles | Reautenticación y confirmaciones auditable | No cambio de correo sin comprobación.**

La microfase precedente [C05-06](https://github.com/crecisol-platform/CRECISOL/pull/138#issuecomment-6865602159) está certificada **solo en contratos y recuperación HTTP/MySQL 8.4 sintética**, `dev` `4a97dbd29b3acc93e9c20ae09e0e26945af98ea0`. No supone un directorio real, SMTP, sesión de producción ni políticas MFA ya habilitadas.

## 2. Arquitectura y límites de confianza

| Archivo/capa | Entrega y protección | No autoriza |
| --- | --- | --- |
| `backend/src/Crecisol.Application/Identity/SensitiveMailboxVerification.cs` | Token tipado `MailboxApprovalDigest` de 32 bytes; `IssuedMailboxApproval` delivery-only; `VerifyAndChangeSensitiveMailbox` sin referencias a HTTP ni MySQL. Verifica buzón registrado no verificado. Para el cambio sensible invoca obligatoriamente `ITrustedImmediatePasswordReauthentication.VerifyFreshAsync` **dentro del mismo caso de uso** con la contraseña actual, nunca acepta como parámetro una evidencia C05-04 previamente cacheada. Solo acepta un resultado `ContextualLoginPreparationStatus.Ready` obtenido de Argon2id real, con tenant y revisión de `VerifiedTenantHostHint` coincidentes. Dos aprobaciones independientes y ninguna autorización resultante. | Registro público, selector de empresa, perfiles, roles, MFA, sesión o endpoint productivo. |
| `backend/src/Crecisol.CredentialSecurity/MailboxApprovalTokenProtector.cs` | Token opaco **256-bit CSPRNG**, forma `cv7_`+base64url canónica, huella HMAC-SHA256 de una clave **distinta** a los tokens de sesión C05-05 y reset C05-06. Borra buffers, `Dispose` limpia clave, `ToString` censurado. El digest de email mantiene el HMAC de mailbox C05-06 mediante `ITrustedPasswordRecoveryProtector`; nunca texto claro en DB. | Token en URL/GET, claims de login, cookie/JWT, importación de claves desde cliente o secret manager productivo. |
| `backend/src/Crecisol.Infrastructure/Identity/MySqlDisposableSensitiveMailboxLedger.cs` | `MySqlConnector@2.6.2`, solo base efímera `c05_05_disposable` sobre `127.0.0.1`. Ejecuta procedimientos `SQL SECURITY DEFINER`, cuenta de ensayo solamente `EXECUTE`, sin lectura/escritura directa de PII. | Migración real, ruta de tenant sugerida por navegador, grant privado, cambios en API/DI productiva. |
| `database/tests/fixtures/c05_07_disposable_mailbox.sql` | Tres tablas InnoDB: verificación registrada, cambio de correo con dos secretos independientes, auditoría append-only sin dirección/contraseña/token/huella. Dos triggers de veto de UPDATE/DELETE de auditoría; cuatro procedimientos de transacción real `FOR UPDATE`, `UTC_TIMESTAMP(6)`, estados y revisiones. | Persistencia de cuentas reales, outbox SMTP, directorio global con unicidad transaccional entre tenants, SIEM irreversible de producción. |
| `Crecisol.MailboxApproval.MySqlTests` y `database/tests/c05_07_mailbox_http_mysql.sh` | ASP.NET 10 escuchando **solo en 127.0.0.1**, MySQL 8.4 desechable, dos hosts/tenants A/B, cuatro endpoints `/__c05_07_test__/` en ejecutable de **pruebas**, envíos en memoria a buzones sintéticos, 16 solicitudes HTTP concurrentes. | HTTP externo, HTTPS productivo, SMTP, pruebas reales de propiedad de una bandeja de correo ajena. |

### Verificación de buzón previamente registrado

El servidor recibe la dirección canónica y un host verificado; el SQL busca únicamente un buzón **ya registrado**, del tenant exacto, todavía no verificado, con identidad/credencial/membresía/host activos y realm `TENANT`. `POST /__c05_07_test__/verify/request` siempre responde **202 sin contenido ni token**, igual si el correo está ausente, ya verificado o pertenece a realm PLATFORM. Solo la coincidencia activa entrega un secreto de ensayo al *sink* de memoria. La solicitud nueva revoca la anterior. La confirmación `POST /__c05_07_test__/verify/confirm` comprueba HMAC de token, tenant, revisión del buzón, vínculo exacto, revisión del sujeto y 10 minutos UTC. Si es válida, marca el buzón verificado e invalida sesiones/recovery anteriores en transacción; tokens repetidos, expirados, reenviados o de otro tenant denegados. No crea cuenta ni sesión.

### Cambio sensible de correo, doble aprobación

La petición exige **reautenticación con la contraseña actual** por `PrepareContextualPasswordLogin` C05-04 en el mismo request HTTP, resuelve el host exclusivamente por backend y exige que el correo anterior coincida con el **buzón registrado y verificado en SQL**. `c05_07_begin_change` compara actor, link, cuenta/credencial/membresía/catálogo/host/revisiones actuales y realm `TENANT` dentro de transacción. No admite reemplazo igual ni dirección registrada a otro actor; la comprobación de disponibilidad global es conservadora, pero **la unicidad transaccional global entre varios tenants está fuera del alcance del fixture**. El nuevo desafío vence a los **5 minutos UTC**, revoca solicitudes previas del actor y genera **dos HMAC diferentes**: uno destinado exclusivamente al buzón anterior y otro al nuevo. Ambos correos son sintéticos, entregados en memoria.

`c05_07_confirm_change` bloquea el desafío y revalida todas las revisiones; marca la primera aprobación sin actualizar el correo. Solo cuando la **segunda aprobación independiente** se recibe a tiempo y sigue vigente el buzón antiguo, cambia el digest de correo mediante CAS. En **la misma transacción** revoca todas las sesiones previas del actor global de A/B, incrementa `subject_revision` de cada tenant, revoca desafíos de recuperación pendientes y registra un evento `MAILBOX_CHANGED` sin secretos, emails ni digests. No afecta sesiones ajenas. Los replays son denegados (incluido 16-way HTTP CAS exactamente un ganador). Una membresía suspendida/reactivada, un buzón desverificado/reverificado o una revisión alterada invalida el desafío viejo permanentemente.

**Conservación del aislamiento:** la modificación es solamente de la fila del buzón `(tenant_id,actor_id)` en el tenant autorizado. Se revocan sesiones globales de ese actor, pero nunca se modifican buzones de otros tenants, ni sesiones de otros actores. La gestión productiva del handle global y deduplicación universal requerirá un directorio de identidades definitivo, pendiente.

### Trazabilidad de seguridad y sin PII

Tabla `c05_07_security_audit` guarda únicamente tenant_id, actor_id, operation_id, tipo y timestamp UTC; eventos: `VERIFY_ISSUED`, `MAILBOX_VERIFIED`, `CHANGE_REQUESTED`, `PREVIOUS_CONFIRMED`, `REPLACEMENT_CONFIRMED`, `MAILBOX_CHANGED`. Triggers rechazan UPDATE/DELETE aun a la cuenta privilegiada de test. La cuenta runner no tiene SELECT sobre el historial. **No implica bitácora productiva WORM, SIEM ni protección contra administración raíz**.

## 3. Matriz adversaria y pruebas reales

| Positivo obligatorio | Negativo de cierre |
| --- | --- |
| Verificación de buzón registrado no verificado y marcado SQL | Email desconocido/ya verificado/PLATFORM: 202 neutral sin entrega. Token incorrecto, reemitido, caducado, B en A, replay: sin verificación |
| Argon2id del C05-04 reautentica de nuevo con host A validado por backend | Contraseña falsa, cuenta desconocida, host no verificado, headers Forwarded/X-Tenant-Id, tenant B con token A: no cambio |
| Token de 256 bits, HMAC separado, mailbox digest C05-06 | Claves cero, diferente clave, token del reset usado para confirmar email, token por URL: denegado |
| Dos confirmaciones old/new, vence a los 5 min, CAS atomicidad | Primera aprobación por sí sola no cambia correo, replay de cualquier aprobación no puede aplicar ni resucitar |
| Aislamiento A/B y revocación del actor global tras cambio | Correo global de otro actor no se usurpa, buzón de B no se modifica, sesión del tercero sigue vigente |
| Auditoría inmutable sin correo, token, hash o contraseña | SELECT/UPDATE/DELETE del runner prohibidos; UPDATE/DELETE incluso con fixture privilegiada rechazados |
| MySQL8.4 real/HTTP .NET10; 16 redenciones competidoras | Exactamente uno confirma la segunda aprobación y 15 la reciben denegada |
| Regresiones C05-01..06, C03, Foundation, A/B/C | Sin tests runtime reales, sin SHA/run y sin diff autorizado no hay certificación |

La simulación realiza nuevas pruebas después de suspender/restaurar membresía, desverificar/reverificar mailbox y expirar tokens en SQL, demostrando que un enlace viejo no vuelve a funcionar. Se verifica que `c05_07_changes.state='APPLIED'` implica ambos timestamps de confirmación y un único evento de cambio.

## 4. Riesgos P0/P1 excluidos del prototipo (NO-GO productivo)

**La API real sigue con el único endpoint público de branding** en `backend/src/Crecisol.Api/Program.cs`. Este trabajo NO conecta rutas HTTP privadas ni entrega SMTP, no envía links a personas, no persiste dirección real ni contraseñas nuevas, no provisiona cuentas ni autoriza membresías. Antes de habilitar un cambio real faltan comprobación real de propiedad del buzón, outbox duradero cifrado y atómico con transacción, validación documental de identidad si aplica, identidad global/alias unificado entre tenants con unicidad fuerte, reautenticación por sesión fresca y política MFA/step-up C05-08, controles antifraude/rate-limit C05-10, neutralidad temporal/anti-enumeración de latencias, gestor y rotación de secretos, HTTPS/CSRF/CORS según transporte, bitácoras y avisos reales al email anterior, tareas de recuperación de incidentes y pruebas manuales de accesibilidad.

**Ningún flujo de C05-07 anula MFA, crea nuevas sesiones, concede roles, modifica credenciales de acceso, habilita producción o afirma seguridad productiva.** No integrar en VPS ni activar Cloudflare/DNS/pagos.

## 5. Auditorías y puerta de cierre

**A funcional:** Word y C05-06 precedente, doble confirmación, reautenticación real, vencimiento, auditoría, revocación. **B seguridad:** token HMAC, enumeración, A/B, host verified, credenciales/roles, FK, SQL locks y carreras, audit append-only, mutaciones negativas; cero P0/P1 en ámbito sintético. **C arquitectura/rendimiento/QA:** diff de diez archivos del incremento, .NET10 strict, MySQL8.4 real, Angular22/Chromium y regresiones previas C05-01..06, provenance SHA/run, rollback y permisos CI read-only.

**C05-07 NO CERTIFICADA; C05-08 BLOQUEADA** hasta recompilación real, pruebas ejecutadas, A/B/C, 3/3 PREMERGE técnicas, 5/5 POSTMERGE técnicas, acta independiente PRE/POSTMERGE y dictamen permanente. Eventual certificación sería **solo el comportamiento del fixture HTTP/MySQL local y sintético (7/11 C05)**; no producto terminado ni capítulo 05.

**Rollback:** revertir exclusivamente en `dev` los commits/PR de C05-07, con análisis de dependencias. Preservar certificaciones C05-01..06. No tocar `main`, SOLQARYN ni Documento Maestro Word/PDF.
