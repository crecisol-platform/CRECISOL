# CRECISOL — C04-05 · Especificación de contexto tenant por identidad

**Fecha:** 12-11-2026, Honduras. **Repositorio:** `crecisol-platform/CRECISOL` (ID GitHub `1450414592`). **Base verificada:** `dev` SHA `344998d8bcd4b006e71bd85d2ed391456e879a95`. **Rama:** `feature/c04-05-contexto-tenant-identidad-20261112`. **Estado:** PREPARADA para implementación interna. **C04-05 NO CERTIFICADA; C04-06 BLOQUEADA**.

## 1. Autoridad, secuencia y prueba de entrada

Documento Maestro Único: `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx` SHA-256 `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c`; PDF de lectura SHA-256 `0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77`. Capítulo C04 canónico: 72 párrafos, SHA de texto `95be2bb0210e108317e67bd08fb9b2899d33554e684bb2d9ae3c8eced6995058`; verificar en CI. Las ocho puertas normativas MAE-C04 no equivalen a las 12 microfases técnicas.

La microfase anterior C04-04 está **certificada EXCLUSIVAMENTE en la resolución INTERNA de hosts**, no como DNS ownership real ni autenticación privada, según [PR #91 y dictamen](https://github.com/crecisol-platform/CRECISOL/pull/91#issuecomment-6833642526). SHA final de `dev` `344998d8bcd4b006e71bd85d2ed391456e879a95`, 4/4 CI del acta POSTMERGE SUCCESS. Capítulos completos 3/37, C04 como capítulo NO certificado.

Plan autorizado `docs/planificacion-futura/C04_MICROFASES_PROPUESTAS.md`: **C04-05 Contexto tenant por identidad**; positiva **Contexto desde membresía/host autorizado**; negativa **No usar tenant_id como permiso**.

## 2. Alcance estrictamente aislado y frontera de confianza

En esta microfase implementar **tipos Domain y caso de uso Application internos con puertos**; sin autenticación productiva, Infrastructure, middleware, rutas API, DI, migraciones, acceso a información privada ni conexión de DB real. El propósito es probar la combinación de *evidencia de sesión autenticada del backend* + *host internamente verificado C04-04* + *membresía válida del mismo actor y tenant* + *tenant activo del catálogo C04-01*. Ninguno de esos identificadores, aisladamente, es permiso de leer/escribir un recurso.

**Cadenas de confianza previstas:**
1. `ITrustedCurrentActorSessionReader`: futuro adaptador backend que compruebe sesión/token REAL, firma, emisor, audiencia, revocación y estado de cuenta. Debe entregar **solo** `TrustedActorSessionSnapshot` validado; ningún GUID, cookie, claim u objeto creado desde el navegador es confiable por defecto. C04-05 implementa contratos/orden, **no** ese adaptador real.
2. `ITrustedCurrentDirectHostReader`: futuro adaptador backend del transporte C04-04, que valide **solo Host directo** (sin `Forwarded`/rewrite), y entregue `CanonicalTenantHost` sin confiar en él como permiso. C04-05 NO se conecta a `Program.cs`.
3. `ResolveVerifiedTenantHost` (C04-04): resuelve ese host únicamente si el catálogo confiable retorna **un vínculo vigente y único**. El resultado es una pista, NO una identidad.
4. `ITrustedActorTenantMembershipReader`: recibe `TenantActorId` procedente de identidad confiable y `TenantId` procedente del host verificado; devuelve **todos** los vínculos exactos y su estado, sin seleccionar primero, enumerar otros tenants o aceptar `tenant_id` del request.
5. `ITrustedTenantCatalogReader` (C04-01): debe retornar **exactamente** ese tenant en estado Active y con modalidad de dirección consistente con el host, o denegar.
6. El caso de uso `ResolveCurrentTenantIdentityContext.ExecuteAsync(CancellationToken)` no admite actor/tenant/host/claim como parámetro de entrada. Devuelve **evidencia interna tipada** con revisiones de contexto y sin rol/branch/recurso/DSN. *No constituye decisión de autorización para un recurso*.

**Flujo fail closed:** denegar sesión inexistente, estado suspendido/revocado o expirado; host ausente/ambiguo/falso; membresía ausente, duplicada, suspendida/revocada, de actor/tenant incorrecto o caducada; tenant inactivo, devuelto para otra empresa o modalidad distinta; error de cualquier puerto; cancelación; tiempo UTC inválido; cambio de host entre chequeos. Sin fallback a primera membresía, empresa por defecto, estado shared, SuperAdmin implícito, ni header `X-Tenant-Id`. Sin escoger automáticamente una empresa por la primera membresía en inicio de sesión normal.

**Revalidación:** reloj backend comprobado al inicio/final, segunda comprobación del host para detectar reasignación intermedia; no implica atomicidad de catálogo real. Los adaptadores productivos aún deben aportar controles transaccionales, expiración y revocación coherentes en C04-06/07/11.

## 3. Matriz positiva, negativa y de ambigüedad

Positivos: actor A sesión activa + host empresa A + membresía A activa + tenant A Active; actor B en empresa B; mismo actor con membresías A y B elige **solo host verificado** y membresía de ese tenant (nunca primer tenant de la lista); revisión positiva/UTC; sesión y membresía válidas justo en inicio de vigencia.

Negativos P0: `tenant_id`/slug/host/claim inventado por request no llega al caso de uso; GUID actor/tenant vacío; sesión ausente, suspendida, revocada, futura, caducada, repetida o inválida; host ausente, suplantado, duplicado, expirado, re-asignado entre consultas; miembro de A en host B; actor diferente en vínculo; tenant diferente en vínculo; catálogo devuelve otro tenant, tenant suspendido/inactivo, modalidad de dirección equivocada; listado nulo/múltiple aunque sea del mismo tenant; error/cancelación antes/durante/después de cada lookup; reloj no UTC; contexto de salida no debe contener string/DSN/permiso/rol/branch.

Negativos P1: host caduca mientras se consulta membresía; sesión o membresía caduca antes de devolver contexto; ningún contexto debe almacenarse globalmente ni persistirse en un singleton; ausencia de infraestructura deja la ruta de la API inalterada.

Regresiones: C04-01/02/03/04, .NET10 Domain/Application/Api, MySQL 8.4 shared, Chromium/Angular 22 Node24, C03-10 y Foundation. Las pruebas de la microfase usan **dobles explícitos**, nunca se interpretan como autenticación de credenciales reales ni BOLA HTTP.

## 4. Auditorías y condiciones de cierre

**A — trazabilidad funcional:** Word/PDF y huellas, 72 párrafos, fila C04-05 del plan, código interno sin endpoints, contrato de prueba negativo.  
**B — seguridad y aislamiento:** matriz A/B de actor/empresa/host, estados/expiración, ambigüedad, cancelación y excepciones; mutaciones de guards; no obtener tenant del navegador; no permitir uso del host como permiso.  
**C — arquitectura/calidad:** builds .NET10 `Release -warnaserror`, tests ejecutados en runner real, A/B/.NET con mismo SHA/run; dependencia Domain→Application, sin nuevos providers/deploys; regresiones Angular/MySQL vía workflows ya existentes.

**NO_GO:** cualquier P0/P1 en el alcance interno, workflow obligatorio en `failure/cancelled/pending`, commit cambiado sin re-ejecutar, prueba sin ejecutar o hallazgo sin resolver. Solo luego de PREMERGE green y revisión de diff se podrá fusionar PR técnico en `dev`; confirmar POSTMERGE de su SHA. Preparar acta documental separada y comprobar su PREMERGE/POSTMERGE; publicar SHA/run IDs **reales**, nunca certificar antes.

**Alcance certificable al cumplir:** únicamente *contratos y resolución interna de contexto por sesión de backend simulado, host verificado internamente y membresía tipada* (5/12), no identidad productiva, login, MFA, autorización para operaciones, sucursal/recurso, BOLA/IDOR real, persistencia de membresía o acceso al almacenamiento. **C04-06** se habilitará **solo para preparación** tras C04-05 eficaz; capítulo C04 completo seguirá NO certificado (3/37).

No alterar `main`, Documento Maestro Word/PDF, `frontend/`, `database/`, `Infrastructure/`, API pública, Cloudflare, DNS/VPS, cobros o `solqaryn/Solqaryn`. Reversión exclusivamente del squash técnico C04-05 y sus documentos, sin datos productivos que migrar.
