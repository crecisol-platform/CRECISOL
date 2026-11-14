# CRECISOL C05-10 — Acta institucional CONDICIONADA: rate limit y trazas privadas

**Redacción:** 14-11-2026. **Repositorio:** `crecisol-platform/CRECISOL` (ID GitHub `1450414592`). **Rama de ratificación:** `feature/c05-10-rate-limit-institutional-20261114`, creada desde `dev` `1b0a3ec6a62ef38582b0ef514f53cb62965b15cd`. **AL REDACTAR: ACTA NO EFICAZ; C05-10 NO CERTIFICADA; C05-11 BLOQUEADA; C05 9/11.** Las leyendas históricas iniciales no deben sustituirse por un PASS anticipado: la eficacia depende de todas las puertas siguientes y de un dictamen permanente posterior a su ejecución.

## 1. Autoridad y requisito del Documento Maestro

Fuentes normativas: **Documento Maestro Único** `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx`, su registro `docs/MASTER_REFERENCE.md`, `docs/PLAN_37_CAPITULOS.md`, derivación `docs/planificacion-futura/C05_MICROFASES_PROPUESTAS.md`, gobernanza `.agents/skills/crecisol-project-governance/SKILL.md` y `AGENTS.md`. Huella SHA256 Word `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c`, PDF de consulta `0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77`, verificados previamente en runners institucionales. **Requisito exacto:** «C05-10 | Rate limit y trazas de acceso | Pruebas contra brute-force y filtrado de logs | No registrar tokens o contraseñas». Se mantiene el bloqueo a C05-11 hasta esta ratificación.

Precedente C05-09: [PR institucional #144, dictamen definitivo](https://github.com/crecisol-platform/CRECISOL/pull/144#issuecomment-6884289441), 13/13 CI obligatorias SUCCESS y cierre sintético de perfiles/fotos seguras (9/11); `dev` `6b7e0d39785bcad00cc059954eaef510f10d84fa`. C05-09 NO incorporó login real, fotos reales, Cloudflare ni rol SuperAdmin; C05-10 no interpreta esos componentes como productivos.

## 2. Alcance técnico efectivamente desarrollado

[PR técnico #145](https://github.com/crecisol-platform/CRECISOL/pull/145), HEAD `9fc11e1c779c465975856a40ccb7a84bf7653fe0`, diff **EXACTAMENTE 11 archivos**, squash exclusivo a `dev` `1b0a3ec6a62ef38582b0ef514f53cb62965b15cd`. Sin cambios en `main`, en Word/PDF Maestro ni en `solqaryn/Solqaryn`.

1. **Domain:** `backend/src/Crecisol.Domain/Identity/PreAuthRateLimitModels.cs` define propósitos `PasswordLogin`, `PasswordRecovery`, `MfaChallenge`, digest HMAC-SHA256 tipado y redactado, veredictos no autorizantes de admisión/limitación/fallo y resultado privado. No contiene sesión, bearer, cuenta productiva ni rol.
2. **Application:** `ThrottledContextualPasswordAttempt` requiere fuente `HttpContext.Connection.RemoteIpAddress` de un lector backend, host de tenant verificado por `ResolveVerifiedTenantHost`, digests HMAC y **admisión transaccional en SQL ANTES de C05-04 Argon2id**. La verificación de la contraseña usa el caso C05-04 genuino, dummy PHC para usuario desconocido, no selector del navegador. Devuelve 202 vacío tanto para credencial válida como inexistente/inválida, 429 vacío cuando agota cuota y 503 genérico si falla red confiable, host, política, MySQL, persistencia o trazabilidad. El resultado Ready no genera sesión/JWT; no crea permisos.
3. **Infrastructure HMAC:** `SyntheticPreAuthDigestProtector` recibe clave de 256 bits aleatoria SOLO en runtime del ensayo, calcula `HMACSHA256.HashData` con separación por tenant, propósito, `SOURCE/SUBJECT`, correo canónico `CanonicalLoginHandle` e IP real del socket. IPv4 mapped normalization e IPv6 /64 evitan evasión trivial; clave, buffers de correo e IP se limpian en memoria. Nunca se registra IP/correo en claro, token, contraseña, cookie ni encabezado de autenticación.
4. **Infrastructure MySQL:** `MySqlDisposablePreAuthRateLimitLedger` se restringe a `127.0.0.1` y esquema `c05_10_disposable` de MySQL 8.4; usuario de runner con permiso solo `EXECUTE` en dos stored procedures, no SELECT/UPDATE/DELETE tablas. Comprueba revisiones de host y de política. Usa `MySqlConnector@2.6.2` y cero de buffers transitorios. No se utiliza Redis, WAF ni servicio externo.
5. **Fixture MySQL:** `database/tests/fixtures/c05_10_disposable_rate_limits.sql` contiene `c05_10_policies` (por tenant+acción, límites de sujeto/origen, ventana/tiempo de espera, enabled y revision), `c05_10_buckets` (PK compuesta tenant+propósito+dimensión+digest), `c05_10_access_traces` (ID aleatorio, dos HMAC, ADMITTED/LIMITED, version de política, UTC MySQL) y `c05_10_access_outcomes` (VERIFIED/REJECTED de un único intento ya admitido); triggers prohíben cambiar/borrar las dos bitácoras. Admisión `c05_10_admit_once` bloquea el origen antes que el sujeto (`SOURCE`→`SUBJECT`), usa `FOR SHARE` para política y `FOR UPDATE` para buckets, `ON DUPLICATE KEY UPDATE` para crear/serializar sin carrera, transacción atómica y enfriamiento; no crea una traza por cada paquete que llega durante bloqueo. `c05_10_complete_once` solo acepta un resultado legítimo por intento y rechaza cruce entre tenants, propósito o digest. Todos los TTL son de MySQL, no del navegador. **Políticas RECOVERY/MFA en este fixture son contratos de datos, NO integración real con servicios de recuperación o MFA.**
6. **Ensayo real:** `backend/tests/Crecisol.RateLimitAccess.MySqlTests` arranca ASP.NET Core .NET10 en **HTTP `127.0.0.1:0`**, simula tenant A/B con C04 verificado, C05-04 Argon2id real y dummy real; las rutas `/__c05_10_test__/password-login` existen SOLO en el ejecutable de pruebas, no en `backend/src/Crecisol.Api/Program.cs` (única ruta de branding público). `database/tests/c05_10_rate_limit_http_mysql.sh` arranca MySQL8.4 por digest pinneado, credenciales temporales aleatorias, instalación local y borrado final. `qa/c05_10_rate_limit_audits.py` verifica 11 archivos exactos y A/B/C ligadas al commit.
7. **Resultados de ataques en ensayo:** 16 solicitudes HTTP simultáneas contra mismo buzón desconocido → **5 admitidas / 11 limitadas / 0 unavailable**; cambio repetido de correo desde misma fuente bloqueado; falsificación de X-Forwarded-For/X-Tenant-Id/Authorization no cambia el presupuesto; B funciona aunque A esté limitado; mismo login válido/inválido/inexistente recibe 202 vacío; falla de host, IP, revisión, política, SQL no permite saltar Argon2id; 16 finalizaciones del mismo intento → 1 única y 15 denegadas; trigger de auditoría niega UPDATE/DELETE, token/IP/password no se escriben en tablas.

## 3. Prueba técnica PREMERGE: tres puertas

| CI | SHA probado y veredicto |
| --- | --- |
| [#43643373571 — C05-10](https://github.com/crecisol-platform/CRECISOL/actions/runs/43643373571) | `completed/success` SHA `9fc11e1c779c465975856a40ccb7a84bf7653fe0`: Auditorías A/B/C, **163 mutaciones estáticas rechazadas**, compilación .NET10 warnings-as-errors, **120 comprobaciones reales HTTP/MySQL 8.4**, race 5/11/0, regresiones C05-01..09, C por SHA/run `f87a38d6b6cad95d29a3d77818cd54b2e4cff11e`, run 43643373571. |
| [#43643373529 — Foundation](https://github.com/crecisol-platform/CRECISOL/actions/runs/43643373529) | `completed/success` en el mismo HEAD. |
| [#43643373711 — C03 integral](https://github.com/crecisol-platform/CRECISOL/actions/runs/43643373711) | `completed/success` en el mismo HEAD: .NET10, Angular22, Node24, Chromium, MySQL8.4, portabilidad e historia documental. |

Los logs de #43643373571 también confirman `PASS CRECISOL C05-09 REAL HTTP/.NET10/MySQL8.4: 221 checks`, `PASS CRECISOL C05-08 REAL HTTP/.NET10/MySQL8.4: 149 checks` y regresiones C05-05..07; son pruebas de datos sintéticos, no de clientes reales. La aserción exacta de carrera se mantuvo igual desde el primer diseño.

## 4. Fallos históricos de desarrollo preservados y remediados

- [#43642793271](https://github.com/crecisol-platform/CRECISOL/actions/runs/43642793271): guarda de no despliegue de auditoría A detectó una palabra prohibida en texto informativo del workflow; se corrigió solo la redacción, **sin eliminar ni relajar la guarda**.
- [#43642793209](https://github.com/crecisol-platform/CRECISOL/actions/runs/43642793209): compilación falló al faltar namespace `Crecisol.Domain.Tenancy` del tipo `VerifiedTenantHostHint`; se corrigió import y posición de parámetro `p_host_revision`.
- [#43642979726](https://github.com/crecisol-platform/CRECISOL/actions/runs/43642979726): primer ensayo real de carrera 16 falló; se preservó la aserción 5/11, se añadieron conteos agregados SIN PII y se sustituyó el `INSERT IGNORE` de buckets por un UPSERT que bloquea de forma determinista en InnoDB. La ejecución nueva #43643373571 demostró 5/11/0 y superó los 120 checks. Estos fallos intermedios siguen siendo históricos, **no** se reinterpretan como success.

## 5. POSTMERGE técnico: exigencia antes de eficacia

Squash del PR #145 a `dev`: `1b0a3ec6a62ef38582b0ef514f53cb62965b15cd`. Se iniciaron cinco workflows propios (sin inferir que alguno ya esté finalizado):
- [#43643788104](https://github.com/crecisol-platform/CRECISOL/actions/runs/43643788104): C05-10 A/B/C + HTTP/MySQL real.
- [#43643788149](https://github.com/crecisol-platform/CRECISOL/actions/runs/43643788149): Foundation.
- [#43643788151](https://github.com/crecisol-platform/CRECISOL/actions/runs/43643788151): C03-03 Application.
- [#43643788122](https://github.com/crecisol-platform/CRECISOL/actions/runs/43643788122): C03-04 Infrastructure/MySQL.
- [#43643788137](https://github.com/crecisol-platform/CRECISOL/actions/runs/43643788137): C03-10 integral.

**NO VERIFICADOS TODOS AL REDACTAR.** Deben terminar **5/5 `completed/success`** para alcanzar gate de cierre técnico. No contar workflows skipped de ramas anteriores ni PRs distintos como aprobación.

## 6. Matriz de auditorías A/B/C y límites productivos

**A — Trazabilidad y negocio:** Word canónico e huellas, secuencia C05-09→C05-10, tres dimensiones de políticas (tenant, propósito, fuente/sujeto), ausencia de secretos en bitácora y no inicio de C05-11 mientras haya fallos. Plan 11 microfases intacto.

**B — seguridad ofensiva y PII:** Doble HMAC por ámbito, solo IP socket backend, HTTPS/reverse proxy no simulado ni supuestamente habilitado; no actor/tenant/IP/tokens desde browser, no enumeración de correo mediante HTTP; política ausente/disabled/host revisado deniega; carrera 5/11, persistencia SQL, cooldown, fuente/email rotativos, causa SQL caída bloquea, evidencias A/B HMAC aisladas, append-only, no lectura de bitácora por cuenta de prueba, no borrado o actualización sin autorización; no P0/P1 **vigentes en el alcance sintético ensayado**.

**C — arquitectura, pruebas y desempeño:** Domain/Application/Infrastructure separados, únicamente fixtures en database/tests, servidor de ensayo solo 127.0.0.1, no cambio en API productiva, parámetros versionados/host-catalog, inmutabilidad de logs, eliminación de fallo de concurrencia real, pruebas C05-01..09/Angular22/.NET10/MySQL8.4, diff/SHA exactos. **No se acredita benchmark de gran escala o mitigación de botnet; la medida probada es el CAS de 16 solicitudes en un fixture.**

**NO-GO PRODUCTIVO:** no está conectada la limitación con rutas de login/recuperación/MFA productivas, panel administrativo real, colector de auditorías durable, KMS para HMAC estable compartida entre procesos, proxy de confianza, WAF/DDoS, Redis, límites globales distribuidos, TTL/retención legal y eliminación física, analítica de acceso, métricas reales, trazas en backend privado, acceso SuperAdmin ni protección total frente a botnets. Las políticas de ensayo para MFA/recuperación no implican que esos servicios estén integrados. Por ello **C05 completo no puede certificarse** antes de C05-11 y una integración/productización autorizada. No alterar Cloudflare R2, VPS, DNS, producción, cuentas, secretos reales ni cobros.

## 7. Condiciones obligatorias para hacer eficaz el acta

1. Técnica PREMERGE del PR #145: **3/3 CI SUCCESS** SHA HEAD `9fc11e1c779c465975856a40ccb7a84bf7653fe0`.
2. Técnica POSTMERGE del PR #145: **5/5 CI SUCCESS** SHA `1b0a3ec6a62ef38582b0ef514f53cb62965b15cd`.
3. PR documental SEPARADO con diff **EXACTAMENTE tres archivos**: esta acta, `qa/c05_10_closure_gate.py`, `.github/workflows/c05-10-certification.yml`. Realizar auditorías institucionales A/B/C con decenas de mutaciones de guardas y compilación/pruebas genuinas .NET10/MySQL8.4/HTTP 16 + regresiones C05-01..09.
4. PREMERGE documental **2/2 CI SUCCESS** en HEAD/base correctos y sin fallo vigente; fusionar solamente en `dev` con SHA esperado.
5. POSTMERGE documental **2/2 CI SUCCESS** en nuevo SHA de `dev`, comprobar auditoría C, diff de tres archivos, ancestro técnico, y estabilidad de `main`.
6. Publicar **dictamen institucional definitivo** en PR documental con SHA exacto, 12/12 gates obligatorios (3+5+2+2), A/B/C, conteos reales, fallos históricos y NO-GO productivo. Hasta ese momento este texto es **CONDICIONADO, NO EFICAZ**.

**SI Y SOLO SI se verifican las seis puertas**: C05-10 **CERTIFICADA EXCLUSIVAMENTE EN EL NÚCLEO DE ENSAYO ANTIABUSO SQL/HMAC/HTTP Y TRAZAS SIN CONTRASEÑAS/TOKENS**, C05 10/11 en sus alcances; C05-11 habilitada SOLO PARA DESARROLLO/INTEGRACIÓN, C05 como capítulo NO CERTIFICADO, C06 BLOQUEADO. No se concede autorización de producción.

**Rollback**: si no se completa el cierre, mantener PR documental en borrador; después de fusionado, revertir solo el squash documental y, tras verificar dependencias, el squash técnico `1b0a3ec6a62ef38582b0ef514f53cb62965b15cd` en `dev`; conservar C05-01..09 y fallos históricos. `main`, Word/PDF, `solqaryn/Solqaryn` y servicios externos intocados.
