# C05 — Acta de certificación integral de identidad operativa local

**Dictamen autónomo de 15-11-2026 (Honduras): C05 CERTIFICADO integralmente en desarrollo local aislado. Capítulos certificados: 5/37. C06 permanece BLOQUEADO para su dictamen independiente de preparación e identidades confiables, conforme a la issue #150. Producción NO HABILITADA.**

## Autoridad, código y alcance

Autorización del propietario: completar C05 con pruebas y auditorías reales, sin producción ni SOLQARYN; mantener una vista previa local. Se aplica la decisión de autonomía de 11-11-2026: no se exige una nueva firma humana ni GitHub Actions como gate prospectivo. Las actas históricas permanecen intactas.

Fuente funcional: Documento Maestro Único, elementos del cuerpo 558–588, tabla de trece filas y secuencias 5.1/5.2/5.3; integración transversal de perfiles y aislamiento contextual. `qa/master_source_guard.py` verifica Word/PDF, blobs y prefijo histórico de 1.876 elementos.

- Word SHA-256: `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c`.
- PDF SHA-256: `0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77`.
- Base de integración: `9aea43a7b516778390e7620aeaaa22e28a463052`, exclusivamente `dev` de `crecisol-platform/CRECISOL`.
- Fuente local de la ejecución final: `52f8321dc3dfbb9986b5f4c0392bf7e4daf5b8b9`; suplemento de pruebas de bitácora `7d30693`, sin cambios en backend/frontend/database.
- Head técnico remoto con árbol idéntico: `fba41381c28e7bdd5d6891fa111fb27be9a16da2`; árbol `b714212703a8163c48bf227059ad384d63f2a58c`. Integrado en `dev` por [PR #154](https://github.com/crecisol-platform/CRECISOL/pull/154), SHA `f3673f65c4f641b0afb509464aa41a3aad5b1bd7`, cuyo árbol coincide exactamente.
- Herramientas comprobadas: Node 24.15.0, .NET SDK 10.0.404, Angular 22.2.4, MySQL 8.4.13, Chromium de Playwright 1.56.1.

El alcance es software operativo en composición local aislada y datos ficticios. Todos los endpoints permanecen desactivados por defecto y requieren opt-in explícito, conexión MySQL de loopback y hosts reservados `*.example.test`. No se habilitan producción, proveedores externos, infraestructura comercial, `main`, DNS, VPS ni SOLQARYN.

## Auditoría A — requisito, implementación y aceptación

**Resultado: APROBADA** en el alcance operativo aislado; recorridos y negativas aplicables ejecutados.

| Requisito del maestro | Implementación | Evidencia ejecutable exigida |
| --- | --- | --- |
| Login contextual sin selector | Host verificado y catálogo activo; directorio de lectura 005; `MySqlOperationalIdentityFlow` | Login elegible; host/forwarding/tenant falsificados; A no usa B |
| Registro e invitación | Pruebas de correo, términos versionados, invitación ligada a tenant y correo; 008/016 | Recepción SMTP independiente; prueba única; carreras; términos incorrectos; una identidad no hereda membresías |
| Contraseñas | Argon2id; 15–128 caracteres; pantalla local íntegra de 10.001 hashes públicos comprometidos | Persistencia Argon2id, negativas de política y secretos fuera de logs |
| WebAuthn | Fido2 4.3.3; credenciales residentes, UV, RP/origen/challenge/firma/counter; migración 007 | Chromium CTAP2 real; firma válida de control; mutaciones firmadas; revocación y replay |
| MFA administrativo | TOTP SHA-256 y respaldos de un uso; step-up; realm cerrado PLATFORM_IDENTITY | Password solo no concede sesión; repetición, caducidad y cambios sensibles |
| Recuperación por correo | Token aleatorio hasheado, propósito/contexto/revisión, outbox cifrado y aviso posterior | Respuesta neutra, caducidad, uso único, segundo factor, revocación, denuncia |
| Respaldos | Diez códigos aleatorios, hashes, entrega única y regeneración al cambiar MFA | Consumo único concurrente; los anteriores dejan de servir |
| Pérdida de MFA | Passkey alternativa directa y correo previo; casos asistidos y reenrolamiento obligatorio | Sesión QR no suplanta UV reciente; contraseña nueva; MFA obligatorio; sin sesión automática |
| Pérdida total/global | Casos de soporte separados; dos operadores distintos; evidencia mínima hasheada; enfriamiento de 24 h; dos partes de entrega | Autorrevisión/duplicación denegadas; veto; revisores vigentes; ventana temprana rechazada; reemplazo y revocación |
| QR | P-256 no exportable en navegador, prueba de posesión, challenge breve, confirmación explícita y MFA | Decodificación del QR real; dos contextos Chromium; firma incorrecta/sin aprobación/cross-tenant denegados |
| Preguntas de seguridad | Ningún endpoint ni campo autorizado para respuestas personales | Contrato JSON cerrado y recuperación sin segundo factor rechazada |
| Sesiones y dispositivos | Cookies host-only Secure/HttpOnly/Strict; CSRF; inventario propio; rotación/revocación; expiración e inactividad en servidor y UI | Cookie inaccesible a JS; restore validado; replay; carreras; pertenencia/revisión/factor vigentes; límites conservados |
| Prevención de abuso | Cuotas persistentes por cuenta/peer/tenant/ruta; señal agregada sin PII; admisión acotada | Intentos concurrentes, persistencia de negativas, 32 solicitudes, señal de abuso y monitor autorizado |
| SSO futuro | No se incorpora un IdP ni se aceptan claims externos | Diferido expresamente por la fila «SSO futuro» del maestro; ninguna inferencia de certificación OIDC/SAML |
| Perfiles y fotografía | Propiedad contextual, cifrado/AAD, preferencias acotadas, RGBA 64×64 recodificado a PNG canónico; perfil de operador separado | Nombre sin ejecución HTML; imagen sin metadatos; AAD A/B; owner-selector y acceso horizontal denegados |

Las aprobaciones de membresía conceden únicamente el rol básico fijo. El aprovisionamiento inicial es una operación offline explícita; no existe registro público de operadores globales ni una contraseña administrativa de fábrica. La matriz fina de acciones de negocio de C06 no se da por implementada por esta integración.

## Auditoría B — límites y hallazgos corregidos

**Resultado: APROBADA**; hallazgos P0/P1 aplicables corregidos y probados, sin excepciones abiertas.

Revisión de las rutas de API, transacciones, migraciones y rol SQL; pruebas adversariales del mismo producto real. Los actores de auditoría solo se atribuyen después de verificar prueba/sesión; las consultas anteriores son sujetos, no autores autenticados. Las bitácoras y revisiones de soporte tienen protección de inmutabilidad en SQL y permisos mínimos.

Hallazgos corregidos antes del cierre: separación del pool de cuotas y admisión acotada; preservación del factor anterior hasta confirmar su reemplazo; imposibilidad de degradar MFA revocado a contraseña; método de sesión explícito para distinguir QR de WebAuthn; consumo atómico de prueba y membresía pendiente al solicitar otro negocio; atribución correcta del actor; expiración de evidencias visibles y conservación del vencimiento absoluto al rotar.

La API no recibe tenant/actor/rol/DSN desde el navegador. El transporte browser exige origen local exacto, cabecera propia y CSRF ligado a cookie y realm; no habilita CORS. El transporte bearer no acepta cookies ambientales. Secretos, tokens, semillas, cuerpos privados y SQL no entran en los logs del producto.

El rol API no puede crear operadores, elevar grants, hacer DDL ni borrar/alterar bitácoras. La operación definer 016 solo consume la prueba contextual y participa en la transacción exterior: no contiene un COMMIT independiente. Los cambios globales de autenticadores invalidan sesiones en todos los contextos de esa identidad, conservando relaciones y perfiles.

## Auditoría C — arquitectura, ejecución y experiencia

**Resultado: APROBADA** para arquitectura, compilaciones, concurrencia, migraciones, regresiones y UX automatizada efectivamente probadas.

Application define contratos/puertos; Infrastructure implementa MySQL y transacciones; CredentialSecurity implementa criptografía; API valida el transporte y traduce resultados. Angular usa servicios distintos para tenant y operadores, cookies HttpOnly y CSRF en memoria. No almacena bearer/semillas en localStorage o sessionStorage. Las claves privadas WebAuthn permanecen en el autenticador; la clave QR permanece no exportable en WebCrypto.

Compilación estricta de API y aprovisionador: cero errores y advertencias. Tipos Angular: PASS. Las pruebas E2E usan Angular real, Kestrel real, MySQL 8.4 real y un receptor SMTP .NET independiente. CDP emula el dispositivo CTAP2, sin simular `navigator.credentials` ni la verificación del servidor. Las pruebas públicas históricas sí conservan sus fixtures de marca; no se confunden con integración de identidad.

Los positivos de enfriamiento avanzan únicamente `ready_at` en el fixture privilegiado después de comprobar que la fecha real exige al menos 86.300 segundos y que el intento temprano se rechaza. No se afirma haber esperado 24 horas. Las cuotas y TOTP usan tiempo real; no se borran cuotas ni se inyecta reloj para obtener aceptación.

## Ejecuciones, incidencias y dictamen

Resultado final: **GO para C05 en desarrollo local aislado; cero P0/P1 abiertos en el alcance revisado**. Se acepta técnicamente bajo la delegación expresa del propietario y las auditorías A/B/C anteriores. No se concede acceso a usuarios reales ni se autorizan capítulos posteriores por inferencia.

| Ejecución real | Resultado | Evidencia versionada |
| --- | --- | --- |
| Chromium → Angular sin caché → Kestrel → MySQL/SMTP, Windows | 289 verificaciones PASS; dieciséis migraciones aplicadas y revertidas sobre esquema poblado; exit 0 | [integral_windows.json](evidence/2026_11_15/integral_windows.json) |
| Mismo flujo integral, Ubuntu/GitHub, head técnico exacto | 289 PASS; siete comprobaciones activas PREMERGE SUCCESS | [run 43804408096](https://github.com/crecisol-platform/CRECISOL/actions/runs/43804408096) |
| Bitácora propia, otro tenant, metadatos de plataforma y scope falsificado | Cinco verificaciones nuevas PASS; modo audit completo 16 PASS y reversión 016→001; exit 0 | [tenant_audit_boundaries.json](evidence/2026_11_15/tenant_audit_boundaries.json) |
| Contratos históricos ejecutables .NET | 25 proyectos PASS; exit 0 | [contracts_windows.json](evidence/2026_11_15/contracts_windows.json) |
| Regresiones históricas MySQL/HTTP | 25 suites PASS; exit 0 | [mysql_windows.json](evidence/2026_11_15/mysql_windows.json) |
| Páginas públicas Chromium | 65 PASS con aserciones originales y puerto 4200; exit 0 | [public_chromium_windows.json](evidence/2026_11_15/public_chromium_windows.json) |
| Build final | API/SMTP/aprovisionador: cero errores/advertencias .NET; tipos Angular y build de producción PASS | [build_windows.json](evidence/2026_11_15/build_windows.json) |
| Dependencias | NuGet incluyendo transitivas y npm de producción: cero vulnerabilidades reportadas en esos análisis | [nuget_audit.json](evidence/2026_11_15/nuget_audit.json), [npm_audit.json](evidence/2026_11_15/npm_audit.json) |
| Fuentes y guardas hostiles | Maestro intacto; 82 mutaciones SMTP, 99 de Argon2id y siete negativas del scope integral rechazadas | Evidencias master/smtp/password/scope del manifiesto |
| QR corregido | Respuesta real 200 e imagen decodificable con y sin caché; antes fallaba la imagen pese al 200 | Evidencias qr_cold_before/after y qr_warm_after |

El build Angular emite dos avisos de optimización por CommonJS (`qrcode`/`dijkstrajs`), sin errores ni incumplimiento de presupuesto. No se confunden con advertencias .NET. La accesibilidad se comprueba automáticamente en las páginas probadas; no se declara una auditoría manual universal WCAG.

El [manifiesto de evidencias](evidence/2026_11_15/evidence_manifest.json) conserva SHA-256 del log original y del JSON publicado. Solo se eliminan secuencias ANSI y normalizan finales de línea; se conservan las líneas y los límites declarados por los tests. El log SQL tiene una segunda línea estática antigua de su generador: se preserva y explica en el manifiesto; su primera línea identifica la ejecución real `b67d922`. Backend/database no cambiaron después de esa ejecución. Las pruebas públicas se ejecutaron desde frontend con sus rutas de imágenes y puerto originales.

El [inventario de archivos](evidence/2026_11_15/file_inventory.json) identifica los cambios técnicos y documentales. Frontend: rutas lazy y servicios de identidad/operadores, vistas y utilidades de foto/sesión. Backend: módulo API, contratos Application, criptografía y adaptadores MySQL operativos. Database: migraciones 007–016 adicionales, reversión y rol mínimo; se verificó la cadena completa 001–016, incluido el directorio de lectura 005. Las familias HTTP son `/api/v1/identity/*` y `/api/v1/platform-identity/*`: registro, login/MFA, passkeys, sesiones/logout, recuperación/cambios sensibles, invitaciones/membresías, pairing, perfiles, soporte y monitor. No hay bootstrap público.

La verificación técnica POSTMERGE de `f3673f65c4f641b0afb509464aa41a3aad5b1bd7` también terminó SUCCESS: trabajos `operational` y `regressions` del [run 43805719804](https://github.com/crecisol-platform/CRECISOL/actions/runs/43805719804). Las [comprobaciones PREMERGE](evidence/2026_11_15/ci_premerge.json) y los [trabajos POSTMERGE](evidence/2026_11_15/ci_postmerge.json) conservan sus estados reales; los checks SKIPPED no se cuentan como pruebas ejecutadas. La decisión autónoma sigue vigente: esta CI observada no crea un gate nuevo para futuras microfases.

Las ejecuciones anteriores de 203 verificaciones integradas y 35 de operadores son evidencia parcial de versiones anteriores y no se usan para completar artificialmente el resultado final.

Se preservan los fallos y diagnósticos locales: algoritmo de firma Ed25519 en el arnés; navegación antes de completar logout; cuotas reales consumidas; bloqueo de DLL por un proceso antiguo; presión de memoria al ejecutar varias suites; transporte HTTP del arnés. La reproducción independiente de doce aprobaciones con sockets separados produjo exactamente un 200 y once 401. Estos cambios del arnés mantienen las aserciones de seguridad y la verificación real del backend.

## Reversión y uso local

La reversión del fixture aplica 016→001 sobre la base aislada, después de detener API/SMTP. Nunca se aplican estos scripts a datos reales sin un plan específico y respaldo. La reversión del código consiste en retirar el opt-in y revertir el cambio técnico de `dev`; no modifica `main`. Ante una regresión P0/P1 se emite una suspensión prospectiva conservando este expediente y las actas históricas.

La vista previa autorizada utiliza 4200/5187/8025 en loopback. Las pruebas usan 4201/5188 para preservarla. Los accesos se generan en `.cache/c05-preview/access.md`, ignorado por Git y restringido al propietario local. El buzón requiere Basic Auth y contiene exclusivamente mensajes de cuentas ficticias. Reiniciar la vista previa reemplaza sus datos y accesos de prueba.

Limitaciones que no deben confundirse con producción: RP localhost; correo solo loopback y `@example.test`; claves efímeras locales; corpus finito de contraseñas; operador humano ficticio/evidencia de identidad del fixture. No se acredita entrega externa, KMS, TLS de un dominio real, proveedores WebAuthn físicos ni verificación de identidad de personas reales. No se ejecuta infraestructura de producción para solventar estos límites.

## Aislamiento transversal y siguiente capítulo

| Criterio del maestro | Aplicación y evidencia |
| --- | --- |
| TEN-001/002 | Lectura privada implementada mediante POST: token A bajo host B denegado; actor/sucursal/propietario/grant vigentes verificados. No se certifican GET/PATCH/DELETE de negocio no implementados. |
| TEN-008 | Historial propio permitido; historial B con sesión A denegado por API real. Exportación de bitácoras no forma parte de las rutas C05. |
| TEN-009 | Selector de tenant en cuerpo rechazado antes de SQL y bitácora B sin modificación; rol API no puede borrar/reescribir auditorías. |
| TEN-010 | Sesión tenant no puede consultar metadatos del monitor de plataforma; sesiones y tablas de operador están en otro realm. |
| TEN-021 | Demos exclusivamente ficticias; el operador de identidad no obtiene acceso a perfiles o recursos privados tenant por pertenecer a plataforma. |
| TEN-032 | No aplica a conexión DEDICATED operativa: esa modalidad no está aprobada ni activada en esta composición. Se conservan contratos C04; el DSN local es único, no procede del cliente y el fallo de permiso SQL se prueba fail-closed sin fallback. |

COM-T08/COM-T09/COM-T10 permanecen **NO EJECUTADAS** y fuera de esta certificación: no se activan sus operaciones comerciales. SSO es futuro según la tabla original. La issue #150 se remedia en su objetivo explícito de integración aislada; no se interpreta como habilitación de identidades productivas. C06 requiere su propio dictamen y permanece bloqueado. La certificación no depende de un nuevo runner, una nueva firma humana ni de declarar PASS un run pendiente.
