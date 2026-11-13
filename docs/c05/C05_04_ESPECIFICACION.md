# CRECISOL — C05-04 · Login contextual sin selector de empresa

**Fecha:** 2026-11-13. **Repositorio autorizado:** `crecisol-platform/CRECISOL` (ID 1450414592). **Rama:** `feature/c05-04-contextual-password-login-20261113`, base `dev` en `70d68817269536b82e9e86bde0e7f448a5a48b6e`. **Estado al iniciar: NO CERTIFICADA; C05-05 BLOQUEADA.** Ningún despliegue, compras, cobros, modificación de main/SOLQARYN, Word/PDF, API privada, worker ni infraestructura productiva.

## 1. Autoridad, dependencia y alcance

Documento Maestro Único `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx`, referencia `docs/MASTER_REFERENCE.md` (Word SHA-256 `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c`, PDF `0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77`). Capítulo 05 previamente extraído en run [#43364292640](https://github.com/crecisol-platform/CRECISOL/actions/runs/43364292640). Secuencia: C05-03 [certificada en contratos criptográficos y ensayos locales](https://github.com/crecisol-platform/CRECISOL/pull/132#issuecomment-6863110105), solo después de [PR #132](https://github.com/crecisol-platform/CRECISOL/pull/132) y su CI, commit `70d68817269536b82e9e86bde0e7f448a5a48b6e`.

`docs/planificacion-futura/C05_MICROFASES_PROPUESTAS.md` asigna a **C05-04 «Login contextual»** la evidencia positiva **«Vinculación de pertenencia sin selector de empresa»** y negativa **«No confundir sesión de tenant A y B»**. C05-05 implementará sesiones y revocación: esta microfase debe validar contexto **antes de sesión**, sin emitir ni simular JWT/cookies, grants, tenant storage, branch/resource authorization o tokens. La API productiva continúa con branding público únicamente.

## 2. Contrato para construcción sin sesión

**Application** `Identity/PrepareContextualPasswordLogin.cs`: caso de uso interno que acepta **solo identificador global canónico + candidato de contraseña + cancelación**, nunca tenant, slug, Host, rol, branch, recurso, DSN o token elegidos por navegador. `CanonicalLoginHandle` de ensayo valida identificadores email ASCII, de longitud <=254 y casefold determinista, sin aceptar dominios/Unicode ambiguos; la política final de email internacionalizado queda por verificar separadamente. El directorio backend entrega 0/1/N registros para detectar duplicados, ligados al actor, a la credencial `PasswordVerifier` activa, revisiones e identidad vigentes. El puerto de Argon2id valida realmente el PHC C05-03; para usuario inexistente se usa verificador señuelo del backend y respuesta genérica, sin afirmar equivalencia temporal de respuesta E2E ni haber medido ataques de enumeración.

**Contexto confiable** proviene exclusivamente de `ITrustedLoginEntryReader` backend, que distingue entrada central de un host verificado; no de un campo `tenant_id`, cookies o parámetros. Con host, `ResolveVerifiedTenantHost` de C04-04 verifica titularidad y se exige vínculo EXACTO con ese tenant; otros vínculos no se heredan. Desde entrada central, se acepta **una sola membresía activa**; 0 = denegar, 2+ = contexto ambiguo (sin elegir primero ni enumerar empresas). Este último resultado es **no autorizante**: C05-05 deberá exigir un mecanismo posterior de selección contextual verificada, nunca confiar en un GUID del navegador.

**Revalidación antirrace**: leer el directorio de credenciales después de Argon2id; reconsultar todas las membresías, entrada backend, vínculo host verificado y estado/revisión del tenant al cierre; fecha UTC del servidor monótona, vencimientos exclusivos y denegación ante cambio, suspensión, rebind, duplicados o errores. Los IDs y revisiones solo se devuelven en `PreparedContextualLoginEvidence` interno, explícitamente NO es autorización de recursos ni comprobante criptográfico transferible. El resultado `Ready` no crea sesión. `AccountTenantProfileKind.Owner/Employee/Consumer` nunca otorga permisos. Plataforma/SuperAdmin usan realm separado, MFA/step-up en fases futuras; esta operación solo trabaja con realm de cuentas tenant.

**Crypto adapter** `Crecisol.CredentialSecurity/Argon2idLoginCandidateVerifier.cs`: adapta el hash PHC real de C05-03 al puerto de Application sin nuevas dependencias ni modificar `Crecisol.Infrastructure` (C03-04 exige MySqlConnector pin único). No registrar en DI productiva, no añadir endpoint ni persistir PII. Los secretos se reciben como `ReadOnlyMemory<char>` efímero del llamador; no se registran; futuras rutas deben borrar buffers de entrada.

## 3. Matriz de amenazas y pruebas

| Control obligatorio | Positiva | Adversaria / NO-GO |
| --- | --- | --- |
| P01 Identidad previa a contexto | Contraseña Argon2id real coincide con credencial activa asociada a actor | Candidato erróneo, cuenta inexistente/suspendida, credencial revocada, duplicados, PHC manipulado |
| P02 Entrada sin selector | Host A verificado y vínculo A; host B y vínculo B | Host ajeno, rebind, host no verificado, tenant mal asociado, header/slug/tenant no forman parte de argumentos |
| P03 Entrada central | Una membresía activa resuelta automáticamente | 2 activos => Ambiguous sin listar tenant; 0 activos/revocados => Denied |
| P04 Aislamiento transversal | Actor global con A+B no confunde perfiles/tenants bajo host A | Mismatched actor, enlace con otro tenant, cambio de revisión/estado y duplicado concurrente |
| P05 Estado y tiempo | UTC, revisiones estables, cuenta/credencial/tenant activos | Hora futura, reloj regresivo, caducidad, catálogo suspendido/alterado entre lecturas |
| P06 Sin sesión/grant | Objeto no portador, `ToString` redactado | No JWT, cookie, credencial de sesión, rol, branch/resource, API privada, DSN ni conexión productiva |
| P07 Privacidad y límites | Resultado genérico Denied, verificador señuelo Argon2id para cuenta desconocida | No revelar existencia/tenant por errores; límites anti-enumeración temporales y rate limit distribuido siguen para C05-10 |

## 4. Auditorías y gate

**A — trazabilidad** al Documento Maestro, C05-03 certificado y contrato C05-04. **B — seguridad y multitenancy:** tests .NET10 Argon2id reales con dos tenants y mutaciones adversarias (host, actor, membresía, revocación, duplicación, PHC, reloj); no equivaler test sintético a protección distribuida. **C — arquitectura y regresión:** Application solo Domain; adapter solo Application; ninguna DI/API/SQL productiva; `Crecisol.Infrastructure` conserva contrato histórico, Foundation/C03 integral y regresiones C05-01/02/03; mismo SHA/run.

No abrir C05-05 sin **A/B/C reales, suites positivas y negativas, CI PREMERGE/POSTMERGE en HEAD final, acta institucional y 0 P0/P1 en alcance**. Esta microfase solo puede certificarse para **contratos y flujo de contexto pre-sesión probado en memoria sintética**. C05 como capítulo y login real E2E seguirán pendientes hasta completar fases posteriores.

**Rollback:** revertir únicamente la integración de C05-04 en `dev` previa evaluación de dependencias; no existe estado real de sesiones, cuentas, tablas ni despliegues que revertir.
