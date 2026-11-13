# CRECISOL — C05-03 · Hash y política de credenciales (contrato técnico)

**Fecha:** 2026-11-13. **Estado inicial:** desarrollo de C05-03, **NO CERTIFICADA** hasta evidencias propias; C05-04 BLOQUEADA. **Repositorio exclusivo:** `crecisol-platform/CRECISOL` (ID 1450414592), rama `feature/c05-03-argon2id-password-policy-20261113`, base `dev` `98cf4315110dcd9cfb9fe7e9eb920ecf19f0253f`. Ningún despliegue, email real, credenciales reales, VPS, Cloudflare, SOLQARYN, cobros, main ni Word/PDF.

## 1. Autoridad y alcance

Documento Maestro Único `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx` según `docs/MASTER_REFERENCE.md`: SHA-256 Word `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c`, PDF `0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77`. Capítulo 05 extraído en [#43364292640](https://github.com/crecisol-platform/CRECISOL/actions/runs/43364292640), SHA `bf7036849d6f1aad36a3a111b62121947aa12fae8c2a0551e63b0cc788d4a0aa`.

El plan técnico `docs/planificacion-futura/C05_MICROFASES_PROPUESTAS.md` define **C05-03 Hash y política de credenciales**; prueba positiva: **hash resistente y secretos fuera del repositorio**; negativa: **sin claves en claro**. [C05-02 certificada solo en contratos y fixtures](https://github.com/crecisol-platform/CRECISOL/pull/130#issuecomment-6862363822); habilita C05-03, no habilita autenticación productiva. El propio Maestro recomienda Argon2id, admite gestores de contraseñas, política de longitud razonable y bloqueo de claves comprometidas.

## 2. Diseño de componentes

| Capa | Decisión para este incremento | Lo que NO implementa |
| --- | --- | --- |
| `Application/Identity` | `PasswordHashingPolicy` tipada, límites mínimos no rebajables, puerto obligatorio `ICompromisedPasswordScreen`, resultado de bloqueo/conocida/no disponible | No cuenta, sesión, autenticación, token ni tenant grant |
| `Infrastructure/Identity` | Adaptador interno `Argon2idPasswordHasher`, sal aleatoria CSPRNG de 16 bytes, salida opaca de 32 bytes en formato PHC versionado `$argon2id$v=19$`; parseo estricto y parámetros acotados antes de derivar | Sin registrar en DI ni exponer endpoint HTTP; sin conectarse a MySQL productivo |
| `Tests` | Contratos reales .NET 10: aleatoriedad, verificaciones, cambio de política/rehash, validación de límites, Unicode, falsos hashes, denegación por blacklist no disponible, redacción, concurrencia limitada | No probar login, usuarios ni bases productivas |
| `QA/CI` | Auditorías A/B/C, mutaciones adversarias y .NET 10 Release con advertencias como errores, regresión de identidad C05-01 y C05-02, Foundation y C03 cuando se activen | No crear permisos privilegiados de GitHub, secrets, deployments ni workflows de producción |

**Pisos de seguridad (no configurables a la baja):** mínimo 15 caracteres Unicode, aceptar por defecto hasta 128, tope permitido 256; contraseña codificada UTF-8 de 1024 bytes como máximo; rechazar controles y UTF-16 inválido. Política versionada con min/max y parámetros Argon2id; m = 19 456 KiB mínimo, t = 2 mínimo, paralelismo 1; techo de seguridad para verificación de verificador almacenado (64 MiB, 4 iteraciones) para evitar ataques de agotamiento de memoria. Usar sal independiente para cada emisión, hash de 32 bytes y comparación criptográfica constante, sin hashes rápidos SHA256/bcrypt propio ni truncamiento silencioso. **Los mínimos son salvaguardias, no precios, paquetes o parámetros comerciales**; el administrativo futuro puede elevarlos dentro de límites validados. El límite de longitud es número de runas Unicode, no bytes ni normalización silenciosa; se permiten espacios y caracteres Unicode válidos.

**Bloqueo de contraseñas comprometidas:** el servicio de emisión exige un `ICompromisedPasswordScreen` servidor fiable que devuelve Allowed/Compromised/Unavailable. `Unavailable`, ausencia o excepción **fallan cerrados**; la provisión real de un corpus/servicio protegido y versionado pertenece a la futura composición autorizada, nunca a una petición del navegador. La suite usa un fake local, **no acredita comprobación real contra una fuente actualizada**.

**Protección de secretos:** entrada del candidato en memoria temporal solo; no persistir texto claro, reversible encryption, huella rápida ni pepper hardcoded; memoria auxiliar limpiada en `finally`. El envoltorio de almacenamiento `PasswordVerifierEnvelope` redacta `ToString()` y permite exportación solamente al almacén interno futuro. Parsing PHC estricto con límites antes de cualquier KDF; hash inválido responde no-match sin errores de detalle. Sin normalizar, recortar o registrar contraseñas. Sin auth de actor, sesión, MFA, tokens, cookies, usuario real o pertenencia.

Referencia de parámetros de seguridad de carácter informativo (no Skill instalada): OWASP Password Storage Cheat Sheet, fuente primaria `https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html`; algoritmo .NET desde NuGet original `Konscious.Security.Cryptography.Argon2` versión fija `1.3.1`, `https://www.nuget.org/packages/Konscious.Security.Cryptography.Argon2/1.3.1`. La compatibilidad .NET 10 debe demostrarse en CI, no inferirse del catálogo.

## 3. Matriz de riesgos negativos

- P0: aceptar contraseña por debajo del mínimo, excesiva, Unicode inválido o control; rechazar sin emitir hash.
- P0: scanner comprometido/no disponible/lanza excepción; rechazar sin emitir hash ni usar bypass.
- P0: hash en claro, en error/log/ToString, o sal constante; denegar y demostrar redacción y aleatoriedad.
- P0: hash de otro usuario o verificador manipulado debe fallar, aunque reemplace m/t/p/sal/digest, también en igualdad de prefijos.
- P0: parámetros almacenados extremos o downgrade de Argon2id deben fallar antes de derivación, con límite de longitud PHC.
- P1: rehash al elevar parámetros sin romper verificación anterior; revisiones de política sin conceder sesión, rol o tenant.
- P1: tener password verificado no prueba identidad de actor, tenant, membresía, permiso ni rol; C05-04/C05-05 deben recomprobarlo.
- P1: falta de cribado efectivo de contraseñas comprometidas en integración real impide activar inscripción productiva; documentar no probado.

## 4. Puertas de auditoría y estado

**A — requisitos:** maestro y C05-02, trazabilidad a código/test y verificación real del hash y parámetros. **B — seguridad:** casos negativos de política, parser PHC, aleatoriedad, scanner y ausencia de llaves en claro. **C — arquitectura:** `Application` independiente, `Infrastructure` incluye KDF y no se registra en API, compilaciones en .NET10 y regresiones, diff autorizado y evidencia SHA/run exacta.

**NO-GO:** failure/pending de .NET10 o A/B/C, errores en parser, secretos hardcoded, dependencia insegura, bypass de scanner, cambios en main/SOLQARYN, o falsedad de alcance. Hasta que cierre ese gate: **C05-03 NO CERTIFICADA; C05-04 BLOQUEADA**. Una certificación eventual será exclusivamente **adaptador y política de hashing con ensayo local**, sin registro/login productivos.

**Rollback:** revertir solo PR/commit de este incremento sobre `dev` tras revisar referencias; sin tablas, migraciones, API, usuarios, credenciales reales ni producción que revertir.
