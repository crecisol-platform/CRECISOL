# CRECISOL — C05-02 · Especificación preparatoria de registro e invitaciones seguras

**Estado:** PREPARACIÓN DOCUMENTAL EN CURSO, **C05-02 NO IMPLEMENTADA / NO CERTIFICADA**. **Repositorio:** `crecisol-platform/CRECISOL` (ID 1450414592); rama `feature/c05-02-preparacion-registro-invitaciones-20261112` desde `dev` `39b0a322e6b8f0f8eb55db8668a959c16521f132`, sin cambiar producción ni `main`. C05-03..11 y C06 BLOQUEADOS; C05 como capítulo NO CERTIFICADO.

## 0. Fuente normativa y puerta secuencial

**Autoridad primaria verificable:** `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx` SHA-256 `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c`; PDF derivado SHA-256 `0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77`. [Extracción real del capítulo 05 #43364292640](https://github.com/crecisol-platform/CRECISOL/actions/runs/43364292640): SHA-256 textual `bf7036849d6f1aad36a3a111b62121947aa12fae8c2a0551e63b0cc788d4a0aa`, 93 líneas aplanadas (1807:1900), 9 009 caracteres. El Word contiene ocho pasos normativos de proceso, no confundir con el **plan funcional de once microfases**.

**Puerta habilitante, ya verificada:** C05-01 certificada **SOLO EN CONTRATOS INTERNOS Y FIXTURES SINTÉTICOS** tras [dictamen institucional PR #114, comentario 6844940005](https://github.com/crecisol-platform/CRECISOL/pull/114#issuecomment-6844940005); PR técnico #113 HEAD `beab4638e960e746871d48143c4822a7dafdb549` y dev técnico `d4a62cad50c66923dec2c871d844b3b578085896`; PR documental #114 dev final `39b0a322e6b8f0f8eb55db8668a959c16521f132`, **12/12 CI aplicables SUCCESS**, 100 aserciones C05-01 .NET10 y auditorías A/B/C. Los errores intermedios se mantienen históricos, no se llaman PASS.

**C05-02 del plan técnico:** «Registro e invitaciones seguras»; evidencia positiva futura «Flujo tipado, expiración y pruebas de abuso»; negativa **«No enumeración de correos»**. En el Word, el registro/invitación son contextuales, la invitación debe ser de uso único, el canal de correo debe verificarse y las condiciones legales vigentes deben aceptarse cuando corresponda. El correo o dominio válido no confiere membresía, firma, rol ni propiedad de tenant.

## 1. Flujo propuesto, sin habilitar operaciones todavía

1. **Contexto seguro:** la URL/host validada por el backend puede sugerir la empresa; no concede acceso. El registro normal NO muestra selector libre de empresas. El código nunca confía en `X-Tenant-Id`, query, cookie, `tenant_id`, slug o claim aportado por cliente para autorizar vínculo.
2. **Inicio de registro genérico:** registrar una intención sin revelar si existe el correo/identidad; presentar la misma respuesta pública a solicitudes equivalentes. Proteger timing, trazas, eventos, rate limit y excepciones; no filtrar estado de usuarios o invitaciones por reenvío de emails. La protección contra abuso real se implementará/medirá en la fase correspondiente sin declarar éxito aquí.
3. **Invitación emitida por operador autorizado:** requiere sesión y permiso verificados por backend, tenant y sucursal (si corresponde) y acción de política del servidor, no campos del formulario. No permitir autoinvitarse como Owner o SuperAdmin. Prevenir generación duplicada y carreras.
4. **Capacidad de invitación:** token aleatorio CSPRNG no derivable y destinado a un solo propósito, **solo hash protegido en almacenamiento**, vencimiento corto versionado por política de seguridad, un destinatario/contexto exacto, una sola consumición atómica, revocación y trazabilidad mínima sin PII. Nunca incluir token en logs, eventos, enlaces compartidos, preview, métricas o DTO posteriores.
5. **Canal de correo:** comprobar la posesión del canal mediante prueba independiente; ni el conocimiento de dirección, ni el clic de invitación por sí solo acredita credenciales de sesión, conformidad legal o facultad de operar. Cambio de email exige procedimiento reforzado de C05-07, no suplantación por vinculación automática.
6. **Aceptación controlada:** confirmar que el sujeto correcto y la invitación vigente corresponden a la misma empresa, perfil y alcance; revalidar el estado de la empresa, invitante, cuenta, membresía y revisiones. Solo el backend puede cambiar estados. Persistencia real debe usar transacción, índice único por ámbito y compare-and-swap o comando condicional a una única fila.
7. **Términos y consentimiento:** conservar versión exacta de términos vigentes por platform/tenant, marca UTC, vínculo de sujeto y ámbito; sin aceptación no activar funciones que exijan esa relación. La parametrización se hace en administrativo en fases de integración y no se codifican condiciones comerciales.
8. **Invitación aceptada ≠ sesión:** respuesta de un flujo no crea JWT, cookie, contraseña, sesión productiva ni permiso sobre otros tenants. Las políticas de contraseña/hash corresponden a C05-03; login a C05-04; sesiones a C05-05; verificación/cambios a C05-07; MFA/step-up a C05-08; antifraude/rate limit a C05-10.
9. **Separación plataforma/tenant:** una invitación empresarial jamás crea operador de plataforma, ni acceso a datos privados o rol global, aunque el sujeto ya posea cuenta global y pertenencias en otras empresas.
10. **Reintento y fallo:** fallo de reloj, duplicado, ambigüedad, reuso, cambio de receptor/tenant, error de backend o concurrencia rechaza sin emitir segundo grant; no intentar recuperación con preguntas de seguridad.

Esta preparación **no implementa endpoints HTTP, email real, almacenamiento de tokens, hashing real, adaptadores, migraciones ni alta de cuentas**; las transiciones son requisitos para pruebas posteriores, NO EJECUTADAS.

## 2. Invariantes de seguridad de diseño (B01–B18)

Cada Bxx es futura aserción positiva/negativa, **no prueba ejecutada de autenticación real**.

- **B01 Registro no enumera cuentas:** respuesta y observables públicos neutrales para correos conocidos/desconocidos.
- **B02 Invitación no equivale a autorización:** token y conocimiento de correo no confieren sesión ni acceso.
- **B03 Invitación de un solo uso:** consumo atómico y persistente, replay denegado incluso concurrente.
- **B04 Invitación expira y es revocable:** fecha UTC obligatoria, rechazo en límite y ante revocación.
- **B05 Token CSPRNG y hash protegido:** nunca guardar token en claro ni incluirlo en logs o DTO.
- **B06 Tenant derivado del servidor:** ni host ni slug ni X-Tenant-Id ni claim dan concesiones.
- **B07 Actor invitante autorizado:** servidor revalida membresía, acción, sucursal y recurso si aplica.
- **B08 No escalada a Owner/SuperAdmin:** rol/perfil solicitados por navegador se ignoran o rechazan.
- **B09 Destinatario exacto y canal verificado:** email verificado no prueba pertenencia automática.
- **B10 Una identidad no hereda vínculos:** invitación para B no reutiliza membresía de A.
- **B11 Duplicados y carreras fail-closed:** cero o varias coincidencias o estado cambiado rechazan.
- **B12 Condiciones legales versionadas:** aceptación ligada a sujeto, versión y ámbito correcto.
- **B13 No activar sesión ni contraseña:** registro no emite credencial/JWT/sesión antes de C05-03..05.
- **B14 Sin preguntas de seguridad:** datos biográficos no sustituyen prueba criptográfica.
- **B15 No PII/secretos en trazas:** correo, token, URL sensible y datos de tenant ajeno se redactan.
- **B16 Suspensión/revocación de tenant prevalece:** no consumir ni emitir vínculos si actor/tenant no vigentes.
- **B17 C05-03..11 y C06 bloqueadas:** preparación no autoriza avanzar de fase ni producción.
- **B18 NO IMPLEMENTADA NI CERTIFICADA:** tres auditorías documentales no sustituyen pruebas runtime.

## 3. Matriz negativa T01–T20 (DISEÑADAS / NO EJECUTADAS EN RUNTIME)

| ID | Riesgo | Condición de rechazo exigida |
| --- | --- | --- |
| T01 | P0: dirección existente vs inexistente | No enumeración por respuesta/tiempo/traza |
| T02 | P0: token de invitación reutilizado | Un solo ganador atómico |
| T03 | P0: dos consumos concurrentes del mismo token | Segundo consumo sin grant |
| T04 | P0: token caducado o revocado | Rechazo incluso en borde UTC |
| T05 | P0: token plano persistido o logueado | Hash solamente, sin exposición |
| T06 | P0: host/slug/tenant_id de B como actor A | No heredar vínculo de A hacia B |
| T07 | P0: actor no autorizado invita | No emitir invitación |
| T08 | P0: autoconcesión Owner/SuperAdmin | Perfil y rol solo autorizados en backend |
| T09 | P0: destinatario B consume invitación de A | Actor/contexto exacto, rechazo |
| T10 | P0: email verificado concede membresía | Email es prueba de canal, no de tenant |
| T11 | P0: cuenta global duplicada provoca grant doble | Duplicado o ambigüedad = denegación |
| T12 | P0: tenant suspendido a mitad del flujo | Revalidación, no activar vínculo |
| T13 | P0: invitante pierde permiso a mitad del flujo | Revocar/denegar antes de consumo |
| T14 | P0: términos obsoletos o de otra empresa | Aceptación exacta o denegación |
| T15 | P0: invitación genera sesión o JWT | Sin activar autenticación antes de C05-04/05 |
| T16 | P0: replay por distinto canal/dispositivo | Propósito, destino y nonce validados |
| T17 | P1: error externo filtra correo/tenant/secretos | Trazas neutrales y eventos redactados |
| T18 | P1: tormenta de invitaciones provoca abuso | Cuotas/rate limit backend, no DoS de víctimas |
| T19 | P1: falla almacenamiento/envío parcial | Idempotencia y no grant en inconsistencia |
| T20 | P0: CI documental habilita C05-03 | Gate secuencial y acta comprobados |

## 4. Impacto por arquitectura y dependencias

| Componente | Diseño autorizado para C05-02 futuro | Prohibición actual |
| --- | --- | --- |
| Domain .NET 10 | Tipos opacos para estado/nonce/expiración/receptor/tenant y versión | No material secreto o rol implícito |
| Application | Puertos futuros de emisión/aceptación y política de autorización backend | No confiar en id/email/tenant de usuario |
| Infrastructure | Hash+consumo atómico en MySQL8.4 compartido, claves tenant compuestas; eventual email sender | Sin nuevas tablas, migraciones, workers o tokens ahora |
| API ASP.NET Core 10 | Registro público neutral, invitación autenticada con antifraude y CSRF aplicables | Sin endpoints privados ahora |
| Angular 22 | Mensajes indistinguibles, acceso inclusivo y errores neutrales | Sin selector de empresa ni pantallas de auth operativas |
| Seguridad | A/B aislamiento, no enumeración, concurrencia, host malicioso y revocación real en implementación | Test documental NO demuestra seguridad end-to-end |
| Modelo comercial | Consentimiento parametrizado/versionado conforme COM-006/010/012 y Word vigente | Sin cobros, compra de servicios ni aceptación legal falsa |

**Punto abierto sin bypass:** la futura implementación C05-02 tendrá que resolver una invitación con token de un solo uso sin activar sesiones antes de C05-03/04/05; cualquier contrato interno es evidencia de modelo y no acceso real. La normalización del email, definición de tasas y contrato de notificaciones deben aprobarse y probarse con protección de PII. No introducir reglas fijas de precios, paquetes, dominios o términos: parametrizables desde panel administrativo cuando exista.

## 5. Auditorías preparatorias, puertas y reversibilidad

- **A – normativa/trazabilidad:** Word/PDF SHA exacto, fuente C05, C05-01 cierre con comment 6844940005 y runs exactos, orden 11 microfases, fase C05-02 y alcance registral.
- **B – seguridad documental:** verificar B01–B18 y T01–T20 completos; mutar 18 invariantes y rechazar su eliminación; comprobar ausencia de API privada nueva, secretos y workflows con escritura privilegiada.
- **C – arquitectura y evidencia:** validar runner GitHub real, SHA/run exactos de A+B, PR HEAD contra base dev por ambos padres o push solo dev, diff limitado a documentación/QA/workflow, sin tocar frontend/backend/database/Word/PDF ni SOLQARYN.
- **NO GO:** P0/P1 documental, fuente Word no verificable, mutación aceptada, prueba real afirmada sin ejecución, CI de HEAD final fallida, diff fuera de ámbito o credenciales/PII.
- **CI PREMERGE y POSTMERGE:** si cumplen, solo declarar **C05-02 PREPARACIÓN DOCUMENTAL AUDITADA**, nunca IMPLEMENTADA/CERTIFICADA; la implementación requiere código por ramas, pruebas positivas y negativas reales, .NET/Angular/MySQL cuando aplique y nueva acta A/B/C.
- **Rollback:** revertir exclusivamente la integración preparatoria del futuro PR en dev tras análisis de dependencias, sin alterar C05-01, main, Word/PDF, VPS, DNS, Cloudflare, SOLQARYN o cobros.

**Estado prospectivo:** C05-01 certificada en modelo interno, C05-02 solamente EN PREPARACIÓN, 1/11 microfases C05 efectivas, C05 NO CERTIFICADO y C06 BLOQUEADO.
