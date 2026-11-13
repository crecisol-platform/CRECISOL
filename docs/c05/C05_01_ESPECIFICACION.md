# CRECISOL — C05-01 · Especificación preparatoria del modelo de cuentas y credenciales

**Estado:** C05-01 **PREPARACIÓN DOCUMENTAL; NO IMPLEMENTADA Y NO CERTIFICADA**. Sin permiso para C05-02..11 ni C06. **Repositorio:** `crecisol-platform/CRECISOL`, ID `1450414592`; rama feature desde `dev` `479cce19a27a1c947df615a01284f2de60f1168f`.
**Precedente obligatorio:** C04 certificado **únicamente como arquitectura/contratos internos y fixtures sintéticos**, PR #111 [dictamen final](https://github.com/crecisol-platform/CRECISOL/pull/111#issuecomment-6843638186); no se presume autorización privada productiva.

## 0. Matriz de procedencia canónica verificada

La autoridad funcional es `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx`, SHA-256 `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c`; su copia PDF de lectura SHA-256 `0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77` (ver `docs/MASTER_REFERENCE.md`). **No editar estas fuentes.**

**Extracción del capítulo 05 desde Word real:** [workflow #43364292640](https://github.com/crecisol-platform/CRECISOL/actions/runs/43364292640) `completed/success`, 93 líneas del documento aplanado, `1807:1900`, 9 009 caracteres; SHA-256 del texto `bf7036849d6f1aad36a3a111b62121947aa12fae8c2a0551e63b0cc788d4a0aa`. El propio Word señala **16 párrafos sustantivos y una tabla original de 13 filas**; las 93 líneas aplanadas incluyen celdas y anexos, no representan 93 párrafos normativos.

| Ancla fuente real de C05 | Implicación con C05-01 | Microfase responsable futura |
| --- | --- | --- |
| Identidad y login contextual sin selector de empresa | Modelo separa identidad de pertenencia; el host no equivale a permiso | C05-01, C05-04 |
| Registro contextual, invitación única, correo verificado y términos vigentes | Distinguir prueba de canal y prueba de pertenencia | C05-02, C05-07 |
| Política de contraseñas, verificador Argon2id preferente y bloqueo de claves comprometidas | Modelar credencial independiente del perfil/tenant | C05-03 |
| Passkeys WebAuthn, RP ID/challenge/origen/firma validados en servidor | Identificadores de autenticador y dominios no se derivan del cliente | C05-08 |
| MFA y step-up de SuperAdmin/administradores | Identidad de operador separada de rol y acceso excepcional | C05-08, C06/C07 |
| Recuperación por correo, tokens hash de uso único, sin enumeración | La capacidad de recuperar no concede automáticamente sesión o tenant | C05-06 |
| Códigos de respaldo, recuperación de MFA y doble control | Separar recuperación de secreto de recuperación de privilegios | C05-08 |
| QR sólo como challenge seguro, nunca bearer de sesión | No conceder autoridad por capturar una imagen | C05-08 y fases de acceso físico |
| Preguntas de seguridad prohibidas | Ningún dato biográfico restablece una cuenta | C05-06, C05-08 |
| Sesiones/dispositivos, cookies seguras, CSRF, rotación y revocación | Diseñar sujeto + sesión/tenant como ejes independientes | C05-05 |
| Rate limits por cuenta/IP/tenant/ruta y anomalías | La protección no debe convertirse en enumeración o DoS | C05-10 |
| SSO OIDC/SAML futuro con issuer, audience, nonce y roles propios | Claims externos no crean pertenencia ni privilegios | C05-11 / ADR posterior |
| TEN-001/002/008/009/010/021/032 cuando exista superficie | Plan de negativas obligatorio, nunca marcar PASS por texto | C05-11 y capítulos de integración |
| Contrato G.7: principal autenticado, pertenencia contextual, sesión y MFA según riesgo | Ninguna autorización de cliente sin identidad y revisión actual | C05-04..11, C06 |

El Word incluye ocho pasos de **proceso** (`C05-01 Extraer` a `C05-08 Acta`). El plan de ejecución funcional `docs/planificacion-futura/C05_MICROFASES_PROPUESTAS.md` contiene **11 microfases propuestas** (`C05-01 Modelo de cuentas y credenciales` hasta `C05-11 Auditorías y acta`). Estos dos esquemas no son equivalentes ni habilitan implementación automática.

## 1. Objetivo funcional acotado

Diseñar la frontera entre una identidad personal, sus verificadores de autenticación, la vinculación explícita y revocable a cada empresa, la operación de plataforma y los perfiles de consumidor. La futura autorización de operación privada requerirá identidad autenticada por servidor, tenant y host verificados, vínculo activo del actor, branch/resource/acción y estado/caducidad actuales.

**Entidades conceptuales propuestas, NO modelos persistidos ni contratos ejecutables en C05-01 preparación:**

1. **Principal / Subject:** ID opaco estable de persona, estado y revisión; ninguna autorización por el mero ID. Identidad global no equivale a perfil ni a superadministración.
2. **Credential / Authenticator:** vínculo al principal, tipo, estado, método de verificación, revisión y material exclusivamente protegido (hash, clave pública, referencia a secreto); no incluir contraseña, TOTP seed ni recovery token en DTO.
3. **TenantMembership:** clave/relación explícita `(tenant_id, principal_id, membership_id)`, estado/revisión/expiración, rol o concesión por política backend; vínculo de A no autoriza a B, ni de la sucursal A a la B.
4. **PlatformOperator:** ámbito separado del tenant; privilegios globales no se heredan de propietario/empleado/consumidor; acceso a datos privados requiere futura excepción MFA + causa + vencimiento + auditoría.
5. **ProfileScope:** datos personales limitados y consentimiento por contexto de plataforma/empresa/consumidor; una foto se almacenará en Cloudflare en fases posteriores, con referencia y autorización tenant-scoped, nunca en BLOB público.
6. **SecurityState:** historial mínimo, epochs de revocación, sesiones/dispositivos y eventos de seguridad como contratos a concretar en C05-05/10. No se implementa su verificador en esta preparación.

**Decisión abierta deliberada:** reglas de normalización de correo, duplicados, vinculación voluntaria de identidades y tratamiento de colisiones quedan sujetas a diseño y pruebas de C05-01 **implementación** y C05-02. El correo verificado **no prueba** pertenencia, autorización, propiedad de un tenant ni consentimiento para vincular cuentas de negocios distintos.

## 2. Invariantes fail-closed de diseño

Cada frase Bxx siguiente es requisito de contrato documental y deberá convertirse después en aserción positiva/negativa real, no una declaración de pruebas ya ejecutadas.

- **B01 Solo servidor valida credenciales:** ningún identificador, cookie arbitraria, JWT sin validar, claim o estructura construida por frontend crea un principal autenticado.
- **B02 Una identidad en dos negocios no hereda permisos:** la misma persona debe recibir vínculos y concesiones independientes por empresa, sucursal, recurso y acción.
- **B03 El correo no autoriza:** correo verificado y coincidencia de email son datos de identidad/canal, no grant de otra empresa ni deduplicación automática de permisos.
- **B04 Host verificado no es permiso:** el hostname comprobado solo da contexto; nunca sustituye membresía vigente y autorización backend.
- **B05 Claim tenant_id no concede tenant:** `X-Tenant-Id`, URL, slug, query, cookie o claims aportados por cliente jamás deciden tenant o DSN.
- **B06 SuperAdmin realm separado y MFA:** operador global no recibe visibilidad privada automática, y requiere step-up, motivo, plazo y auditoría para excepción autorizada.
- **B07 Sin selector de empresa en login normal:** la resolución contextual es automática; en ambigüedad se presenta contexto seguro o se deniega.
- **B08 Pertenencia activa, inequívoca y revocable:** cuenta, vínculo tenant, rama, acción, recurso y sesión tienen validación independiente con caducidad y revisión.
- **B09 Denegar en ambigüedad:** cero o múltiples vínculos/hosts incompatibles, cache obsoleta, cuenta suspendida o resultado contradictorio implican rechazo.
- **B10 Recuperación: token de un solo uso:** futura recuperación con token aleatorio/hash, expiración corta, finalidad específica, no login automático y revocación posterior.
- **B11 Preguntas de seguridad prohibidas:** respuestas personales nunca autentican, restablecen factor ni sustituyen prueba criptográfica.
- **B12 Sesiones revocadas no se reutilizan:** credenciales y sesiones se versionan; reset, pérdida de dispositivo y suspensión deben invalidar accesos pendientes.
- **B13 C05-02 a C05-11 bloqueadas:** la presente preparación no habilita implementación adelantada, migración ni código de otras microfases.
- **B14 NO EJECUTADAS:** pruebas de autenticación HTTP, sesiones, recovery real, MySQL de identidad y MFA no se han corrido dentro de este PR de preparación.
- **B15 NO IMPLEMENTADA Y NO CERTIFICADA:** una especificación o CI de documentos no acredita seguridad de un servicio privado inexistente.
- **B16 No incluir secretos ni PII en logs:** ni hashes sensibles, códigos, credenciales, tokens, correo completo o IDs de otra empresa en errores, traces o artefactos públicos.

## 3. Matriz de amenaza negativa C05-01 (diseño; runtime pendiente)

Las filas T01–T24 son obligaciones de prueba futura. **Estado de todas: DISEÑADAS / NO EJECUTADAS EN RUNTIME**. En esta preparación solo se prueban la existencia de las barreras y resistencia a eliminación de texto, nunca exploits reales.

| ID | Vector P0/P1 | Rechazo o invariante obligatorio |
| --- | --- | --- |
| T01 | P0: misma identidad/email en tenant A y B | Sin membresía explícita en B no hay acceso a B |
| T02 | P0: perfil consumidor de A como operador de B | Perfiles, roles y scopes separados |
| T03 | P0: header `X-Tenant-Id` apunta a B | Input del cliente no constituye contexto confiable |
| T04 | P0: host o slug de B usado por actor A | Host no sustituye membresía ni prueba de identidad |
| T05 | P0: actor_id/subject_id forjado | Resolver principal solo con prueba verificada por backend |
| T06 | P0: claim role=SuperAdmin o realm falsificado | Separación de realm, MFA y autorización excepcional |
| T07 | P0: acceso cruzado entre sucursales de un tenant | Branch/recurso/acción validados por backend |
| T08 | P0: membresía revocada o tenant suspendido | Denegación inmediata conforme epoch/revisión |
| T09 | P0: sesión revocada o token rotado | No admitir replay de sesión/refresh token |
| T10 | P0: reset de A usado para B | Recovery ligado a sujeto/finalidad y scope apropiado |
| T11 | P0: token de recuperación reutilizado/expirado | Hash, uso único, vencimiento y revocación |
| T12 | P0: pregunta personal sustituye MFA | Prohibición total de preguntas de seguridad |
| T13 | P0: invitación ajena/reutilizada | Invitación vinculada, uso único, anti-enumeración |
| T14 | P0: passkey de dominio/origen no válido | Verificar RP ID, challenge, origen, firma y contador según caso |
| T15 | P0: cambio de email recientemente usado para recuperar MFA | Procedimiento reforzado, notificación y enfriamiento |
| T16 | P0: actor global lee datos privados sin paso excepcional | MFA/step-up, motivo, caducidad y registro independiente |
| T17 | P0: clave persistida en claro o secreto en DTO/log | Solo verificador resistente y secretos protegidos |
| T18 | P0: URL de foto/export/medio de B obtenida por A | Medio tenant-scoped; ruta/UUID no es permiso |
| T19 | P0: DSN Dedicated de B o query Shared sin tenant | Router y filtros exclusivamente backend |
| T20 | P1: correo inexistente revela registro por error o tiempo | Respuestas neutrales y mitigación de enumeración |
| T21 | P1: ataques repetidos por cuenta/IP/tenant/ruta | Rate limit/riesgo sin DoS a víctimas |
| T22 | P1: expiración, revisión o grant ambiguos | Rechazar sin fallback o selección primera |
| T23 | P1: cambio de dispositivo o sesión sin auditoría | Estado/evento de seguridad mínimo y notificación |
| T24 | P0: C05/06 habilitado por CI documental sin pruebas | Gate secuencial: código y pruebas reales primero |

## 4. Impacto técnico y compatibilidad futura

- **Domain (.NET 10):** tipos puros e identidades opacas; evitar que `TenantActorId` de C04-05 sea interpretado como verificador. Integración futura únicamente por contratos tipados.
- **Application:** futuro caso de uso de identidad confía en un adaptador de autenticación validada, nunca se invoca desde un DTO de cliente para asignar roles. C04-06 debe seguir revalidando pertenencia y acción.
- **Infrastructure/MySQL 8.4:** diseñar integridad referencial y unicidad por ámbito; separación de perfiles, credenciales, membership y servicios de autenticación; ningún SQL ni conexión real se escribe aquí. Proveedor EF Core sigue sujeto a ADR-002.
- **API/Frontend:** login sin selector, sesiones seguras, anti-CSRF, rate limiting, errores neutrales, accesibilidad; requerirá flujos reales y tests Playwright/Chromium en fases correspondientes. La API actual expone solo el branding público, no autenticación privada.
- **COM-006/010/012:** aceptación de contratos SaaS ligada a representante verificado, sin activar pagos; C05-02/04/05/08/11 deberán cotejar el impacto comercial, no ejecutar `COM-T08/09/10` durante este PR.
- **Cloudflare:** referencias seguras futuras para fotos y adjuntos; sin cargas, URLs firmadas ni servicios externos en esta preparación.

## 5. Puertas de revisión y reversión

**Auditoría A preparatoria:** verificar Word/PDF por SHA y texto C05 por hash, tabla y cobertura de 11 microfases, separación de nomenclaturas, precedente C04 exacto y enlaces de fuente. **Auditoría B preparatoria:** exigir B01–B16, T01–T24, someter barreras documentales a mutaciones y verificar ausencia de nueva API privada y secretos; no afirmar pentest. **Auditoría C preparatoria:** runner real read-only, identidad SHA/run en mismo proceso, evidencia A/B, arquitectura sin cambios, fuente de versión y límites no ejecutados.

**CI de preparación** se autoriza para documentos y verificaciones estáticas; su éxito solo cierra **la preparación auditada**, no C05-01. Para C05-01 implementada y certificada serán imprescindibles código seguro, pruebas positivas y negativas reales, compilación .NET, suite MySQL de identidad cuando aplique, regresiones Angular/Chromium, auditorías A/B/C y cierre por SHA. Hasta ese momento **C05-02 BLOQUEADA**.

**Rollback:** revertir únicamente los commits del PR preparatorio después de verificar dependencias, sin modificar `main`, Word/PDF, `frontend/`, `backend/`, `database/`, SOLQARYN, producción o recursos externos.
