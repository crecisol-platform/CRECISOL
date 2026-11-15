# CRECISOL — integración operativa C05, 15-11-2026

## Dictamen y alcance

**INTEGRACIÓN LOCAL IMPLEMENTADA Y VERIFICADA: 106 comprobaciones E2E reales PASS. Auditorías A/B/C aprobadas exclusivamente en el alcance local descrito. C05 integral NO CERTIFICADO; C06 BLOQUEADO.** La integración Git de este dictamen se autoriza únicamente con CI PREMERGE propia satisfactoria y verificación POSTMERGE de su SHA correspondiente, exclusivamente en `dev`. Hasta completar esas puertas, el PR #151 permanece en borrador. Las once microfases originales conservan literalmente su certificación de contratos y fixtures; no se vuelven a abrir ni se reinterpretan como producción.

**DICTAMEN DE CERTIFICACIÓN TÉCNICA LOCAL:** se certifica la integración operativa local solicitada de registro/verificación, aprobación, login, MFA y respaldos, sesiones, recuperación/cambios sensibles y lectura privada, con Angular y MySQL, exclusivamente en las condiciones y pruebas descritas. **Condición de eficacia:** PREMERGE del acta y POSTMERGE de #151 satisfactorios en sus gates funcionales, más integración y POSTMERGE satisfactorios de la corrección de compatibilidad #153. La evidencia y los SHA/run definitivos se registran en los PR. Antes de completar esta condición no se presume certificación eficaz. El dictamen no certifica C05 integral/productivo, SuperAdmin global, WebAuthn de navegador ni los alcances excluidos; no habilita C06 ni cambia el total de 4/37 capítulos completos.

Esta entrega conecta código de `backend/src/Crecisol.Api`, Angular y las migraciones operativas, en lugar de registrar adaptadores `MySqlDisposable*`. La composición exige activación explícita, MySQL loopback `crecisol_c05`, claves independientes de 256 bits, buzones reservados `@example.test`, Host directo verificado y socket loopback. No se han habilitado clientes, SMTP externo, KMS, TLS productivo, WAF, Cloudflare, cobros, VPS, `main` ni SOLQARYN.

## Fuente y trazabilidad

- Documento Maestro Word SHA-256 `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c`; PDF `0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77`. `qa/master_source_guard.py --self-test` pasó, incluyendo integridad del prefijo histórico y mutaciones negativas.
- Repositorio ID `1450414592`, `crecisol-platform/CRECISOL`; rama `feature/c05-identity-integration-gate-20261114`; PR [#151](https://github.com/crecisol-platform/CRECISOL/pull/151), expediente [#150](https://github.com/crecisol-platform/CRECISOL/issues/150).
- HEAD remoto inicial comprobado: `8aca317cd437ba04bee1ec95213cf4d7fa742c61`. Se conservaron los cambios locales existentes y la integración previa de C04 desde `dev`.
- Código operativo publicado en `0f93631acad42bb4e6468376496122a6a8c8490e`; corrección prospectiva del auditor y del runner en `5befbbd2cbe551577acc57676b70f5661d383b08`. El árbol Git publicado se comparó exactamente con el árbol local antes de mover la rama, sin force-push.

## Arquitectura y archivos principales

| Capa | Implementación |
| --- | --- |
| Application | `OperationalIdentityContracts.cs`: puerto de flujo, puerto criptográfico y recibos. Se conserva la canonización de login existente. |
| CredentialSecurity | `OperationalIdentityCrypto.cs`: reutiliza Argon2id y el verificador RFC 6238 SHA256 existentes. |
| Infrastructure | `MySqlOperationalIdentityFlow.cs`: transiciones transaccionales en un único esquema, SQL parametrizado, bloqueo de cuenta/membresía/factor/sesión y lectura fresca de grants. El directorio `MySqlOperationalLoginDirectory` anterior permanece compatible; las operaciones que emiten sesiones leen el mismo directorio SQL dentro de la transacción, sin confiar en un snapshot externo. |
| API | `OperationalIdentityModule.cs` y composición explícita en `Program.cs`: JSON de forma exacta y acotada, sin selectores de tenant, claims del navegador, cookies ni forwarding confiado. |
| Angular | `identity-api.service.ts` y `identity-portal.component.ts`: ruta lazy `/cuenta`, Signals, formularios con etiquetas, carga/error, expiración, bearer solo en memoria y cierre de credenciales tras cambios sensibles. |
| MySQL | Migración `006_c05_operational_identity_flow.sql` y su reversa, sobre `001..005`; rol versionado `database/operations/c05_local_runtime_role.sql`. |
| QA | `qa/c05_operational_flow_e2e.mjs`: Chromium real sin mocks de rutas, Kestrel real, MySQL 8.4 por digest y receptor SMTP independiente por pipe privado. Workflow `c05-operational-flow.yml`, token de solo lectura y Ubuntu 24.04 explícito. |

La extensión de `qa/c03_01_source.py` verifica el grafo actual permitiendo exactamente API → CredentialSecurity → Application. Conserva el inventario documental de cuatro proyectos de C03 y sigue rechazando SQL/HTTP/ORM en Domain/Application y referencias arbitrarias. La extensión de `qa/c04_04_hosts_audits.py` permite únicamente el catálogo C05 revisado, sin abrir una excepción general a nuevos adaptadores de host.

## Endpoints y recorridos disponibles

Todos son `POST /api/v1/identity/…`, únicamente al habilitar la composición local:

| Rutas | Comportamiento |
| --- | --- |
| `registration-intents`, `registration-completions` | Recibo neutral, prueba de buzón por outbox y creación de cuenta/membresía pendientes. No conceden acceso. |
| `login` | Host verificado, lookup HMAC, cuota persistida antes de Argon2id, cuenta y membresía vigentes; devuelve desafío, nunca sesión por contraseña sola. |
| `mfa/enroll`, `mfa/verify` | Semilla AES-GCM, prueba TOTP real, desafío de cinco minutos, límite de intentos y contador monotónico. Diez códigos de respaldo HMAC de un solo uso se muestran una sola vez al enrolar. |
| `sessions`, `sessions/rotate`, `sessions/revoke`, `logout` | Inventario propio por tenant, rotación con un vencedor concurrente, revocación y cierre servidor. Límite absoluto de treinta minutos e inactividad de quince; rotar no amplía el límite absoluto. |
| `recovery/request`, `recovery/complete` | Respuesta neutral, outbox cifrado, token de diez minutos ligado a propósito/tenant/revisión, MFA o código de respaldo obligatorio, nuevo Argon2id y revocación global de sesiones/desafíos/pruebas anteriores. Nunca inicia sesión automáticamente. |
| `password/change` | Contraseña actual más factor fresco, política de contraseña y revocación de sesiones. |
| `mailbox/request`, `mailbox/complete` | Sesión y factor fresco; prueba enviada al nuevo buzón, ligada a esa sesión, revisión y tenant; confirmación cambia lookup y revoca sesiones. |
| `factor/replace` | Contraseña y factor actual o respaldo, desafío de reemplazo acotado. Conserva el factor anterior hasta probar la nueva semilla, luego revoca factor/códigos/sesiones anteriores de forma atómica. La revocación de MFA nunca degrada una cuenta a enrolamiento por contraseña sola. |
| `memberships/decide` | Actor derivado del bearer, MFA fresco y grant backend vigente. Prohíbe autoaprobación; el perfil concedido es fijo `MEMBER`, sin rol elegido por el formulario. |
| `private/read` | Verifica sesión, MFA, revisiones, tenant, sucursal activa, recurso, propietario y grant de acción en la transacción. El identificador del recurso es selector, no permiso. |

Los formularios Angular consumen esas rutas, incluida aprobación, recuperación, cambio de correo/contraseña y reemplazo MFA. No hay permisos derivados de la visibilidad de un botón ni secretos en localStorage/sessionStorage.

## Persistencia y aprovisionamiento inicial

La migración 006 añade desafíos, cuotas HMAC, códigos de respaldo, sucursales/recursos privados y outbox cifrado; amplía el ledger de recuperación existente en vez de duplicarlo. Las sesiones conservan las revisiones de cuenta y membresía. Un índice único impide dos factores TOTP activos simultáneos. La auditoría se inserta en la misma transacción y permanece append-only.

El rol SQL no crea cuentas ni grants por acceso directo, no tiene DDL ni DELETE de trazas, lee el catálogo/grants y actualiza únicamente las columnas necesarias de cuenta/membresía. Las altas verificadas existentes siguen pasando por el procedimiento de registro con privilegios del definidor. El rol no recibe la operación privilegiada de bootstrap.

`database/operations/c05_bootstrap_local_authority.sql` se ejecuta fuera de HTTP, con la API detenida y una identidad SQL privilegiada. Exige IDs explícitos de una cuenta ya verificada y pendiente, ausencia de autoridad previa y tenant activo. Crea una autoridad tenant-local de ocho horas y su auditoría; no crea contraseña, sesión ni factor. La persona elegida aún debe demostrar contraseña y enrolar/verificar MFA por la API. No es un SuperAdmin global ni un bootstrap público.

## Auditorías A/B/C y evidencia real

**A — funcional y trazabilidad: PASS en el alcance local.** Se ejecutó el circuito conectado de registro, SMTP loopback, confirmación desde Angular, cuenta pendiente, aprobación MFA desde Angular, login, segundo factor, sesión, recurso privado, recuperación y cambios sensibles. La ejecución final amplió las verificaciones de 89 a **106 comprobaciones**, incluyendo aprobación concurrente, rechazo de autoridad de sucursal, rol SQL versionado, revocación explícita propia y rechazo de identificadores de sesión ajenos. Evidencia local íntegra y sin secretos en [C05_OPERATIONAL_LOCAL_2026_11_15.txt](evidence/C05_OPERATIONAL_LOCAL_2026_11_15.txt), código de referencia `b2450eb207b903534c4fd51ea888c12137fc4e44`. No se suman los intentos fallidos como aprobados.

**B — seguridad:** se probaron tenant A/B, sucursal/propietario, Host/Forwarded/XFF/claims falsos, cookies y origen cruzado, bearer inventado, cuota concurrente, MFA inválido/replay/expirado, recuperación replay/expirada/cruzada y sin bypass MFA, cuenta suspendida, revocación de factor/grant, carreras de rotación/aprobación y ausencia de secretos/PII en logs. Se corrigió una degradación potencial tras revocar MFA: una contraseña no puede reabrir enrolamiento si ya existió un factor revocado. Se mantiene separación de operaciones tenant y plataforma.

**Resultado B: PASS para las superficies locales descritas, sin P0/P1 abiertos encontrados en ese alcance.** No es un dictamen sobre las superficies excluidas del último apartado.

La revisión final añadió la condición `branch_id=tenant_id` al grant `MEMBERSHIP_APPROVE`: esa combinación representa autoridad tenant-wide explícita, provisionada fuera de HTTP. Un grant limitado a otra sucursal no puede convertirse en aprobación global de membresías. Se añadió una prueba negativa dinámica antes de la aprobación positiva de Angular.

**C — arquitectura, QA y UX:** .NET 10 Release `-warnaserror` pasó sin advertencias; tipos y build de Angular 22/Node 24 pasaron. Las 25 suites de contratos C01..C05 pasaron; `SharedTenancy` se repitió sin el argumento `contracts` que su ejecutable no acepta. Regresión histórica de recuperación: 91 aserciones .NET/HTTP/MySQL, más sus verificaciones de esquema, PASS. Axe no detectó hallazgos serious/critical en el portal autenticado. Las seis migraciones se aplicaron y revirtieron sobre el esquema poblado, sin tablas restantes.

**Resultado C: PASS para compilación, arquitectura, persistencia reversible, QA y accesibilidad automática del alcance probado.** La accesibilidad manual integral y el rendimiento productivo no fueron certificados.

Se preservan los fallos históricos: selector ambiguo de Playwright, contexto requerido por axe, espera de la cuota real tras ataques, ejecutable histórico no compilado, lectura intermitente del cuerpo de respuesta de Chromium, auditor de dependencias anterior a C05 y runner Ubuntu 26 no soportado por Playwright 1.56.1. Cada corrección debe comprobarse en una nueva ejecución; ninguno de esos fallos se etiqueta retroactivamente PASS.

La CI de `0f93631…` falló al instalar Chromium en `ubuntu-latest` (Ubuntu 26.04) y en auditores históricos no actualizados; no acredita el cierre. Las CI de `5befbbd…`, `b407dce…` y `057383e…` mantienen sus resultados propios: ningún PASS anterior sustituye el gate del SHA final. El código de referencia final `b2450eb207b903534c4fd51ea888c12137fc4e44` ya completó 106 verificaciones locales. Su [CI operativa PREMERGE #43738208988](https://github.com/crecisol-platform/CRECISOL/actions/runs/43738208988) y la [CI de rama #43738201827](https://github.com/crecisol-platform/CRECISOL/actions/runs/43738201827) están en curso/cola al redactar esta acta. Esta acta no presume esos resultados: su integración requiere terminar la CI de su propio commit y registrar SHA/run finales en el PR. Antes de la fusión no existe POSTMERGE.

Estado Git al redactarse: `dev` `0b7c23740b71ae7da1ee659a67a8969ef06d1b94`; `main` `b794181148d2c5e36c1c88127f19a8a9082acff5`, intacta. La eventual integración segura del código local no declara cinco capítulos productivos ni habilita C06.

Regresión adicional corregida: el registro de resultados C05-10 podía sufrir deadlock 1213 por gap locks entre intentos diferentes. `READ COMMITTED` conserva el bloqueo de la traza por intento y la PK única, evitando ese gap sin aumentar admisiones ni ocultar errores. La repetición local pasó **211 comprobaciones** y **seis cohortes de 16**, siempre **5 admitidos / 11 limitados / 0 no disponibles**; auditor B mantuvo **174 mutaciones rechazadas**. El fallo previo 4/11/1 se preserva. Las auditorías institucionales C05-08/C05-10 de diff exacto siguen intactas y solo corresponden a sus PR originales; sus A/B, compilaciones y todas las pruebas funcionales/SQL siguen siendo obligatorias en las integraciones actuales.

La regresión pública local completó 64/65 al primer intento; una prueba axe agotó 45 segundos bajo carga. Su repetición aislada, con un solo worker y 120 segundos de presupuesto, pasó sin alterar aserciones ni código del producto. Resultado: los 65 casos distintos disponen de ejecución satisfactoria, preservando el timeout original. La CI también ejecuta esos mismos 65 casos sin usar esa repetición como sustituto de su resultado propio.

**Corrección documental/técnica del POSTMERGE:** PR #151 pasó 14/14 PREMERGE y se integró exclusivamente en `dev`, SHA `f7144dcdff088cd8fbb3f0cd71d5abf57813ee73`, con árbol idéntico al validado. El POSTMERGE activó validadores adicionales de C02/C03 que todavía exigían exactamente cinco rutas y una certificación histórica C05-02 de diff cerrado. La corrección prospectiva conserva el sitemap/actas de C02, admite únicamente la ruta lazy `/cuenta` con composición C05 explícita y mantiene wildcard/denegación de todas las demás rutas. En C05-02 siguen ejecutándose A/B, compilación, pruebas HTTP/MySQL y evidencias same-run; el auditor institucional de diff exacto solo corresponde a su PR original. No se reescribe ningún FAIL ni se modifica el comportamiento del producto para superar esos validadores.

## Reproducción, riesgos y rollback

Compilar API y `backend/tests/Crecisol.LoopbackSmtpReceiver` en Release; instalar dependencias de `frontend`, generar assets y disponer de Chromium con Node 24. Ejecutar desde la raíz `node qa/c05_operational_flow_e2e.mjs`. El arnés crea únicamente un contenedor descartable, claves y usuarios sintéticos efímeros; al terminar detiene sus procesos y elimina ese contenedor. Los tokens SMTP se reciben por un pipe en memoria, sin endpoints de prueba que los expongan.

Para rollback: deshabilitar `CRECISOL_C05_LOCAL_IDENTITY_ENABLED`, detener la API y exportar evidencia de auditoría antes de revertir 006 → 001 en el entorno descartable. Las reversas eliminan estado de identidad local y no son una operación sobre datos de clientes. Revertir el commit de integración en la rama feature tras revisar dependencias. No modificar `main` ni las actas históricas.

No se certifican SMTP externo, KMS/rotación multiserver, TLS/WAF, soporte humano de pérdida simultánea de todos los factores, WebAuthn de navegador, SuperAdmin global, COM-T08/09/10 ni uso productivo. Estos límites no impiden probar los endpoints locales solicitados; sí impiden presentar esta entrega como certificación productiva o habilitar C06 automáticamente. La certificación integral exige completar la matriz funcional del Maestro y el dictamen institucional correspondiente.
