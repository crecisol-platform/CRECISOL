# C05 UX F01 — Inventario de mensajes, permisos y transiciones (SHA f61a79d242...)

## Transiciones que dibujan pantallas

| Ámbito | Estado de entrada | Evento usuario | Servicio Angular | Contrato de salida / estado UI |
| --- | --- | --- | --- | --- |
| Cuenta | anonymous | correo + contraseña | `IdentityPortalComponent.login` → `IdentityApiService.beginLogin` | `identity/login` → challenge; no acceso privado por contraseña sola |
| Cuenta | anonymous | llave registrada | `passkeyLogin` → `loginPasskey` | `identity/passkeys/options`, `passkeys/login` → authenticated si permitido |
| Cuenta | anonymous | registro | `register` → `requestRegistration` | `registration-intents`, recepción neutra |
| Cuenta | anonymous | confirmación | `confirmRegistration` → `completeRegistration` | `registration-completions`, cuenta pendiente, sin membresía |
| Cuenta | anonymous | recuperación | `securityAction(...)` | `recovery/request`, `recovery/complete`, `assisted/*`, `incidents/report` |
| Cuenta | challenge | código MFA/backup | `verify` → `verifyMfa` | `mfa/verify` → authenticated, sesiones/códigos condicionados |
| Cuenta | challenge | crear autenticador | `enroll` → `enrollMfa` | `mfa/enroll`; QR temporal sin exportación persistente |
| Cuenta | authenticated | pestañas | `IdentityWorkspaceComponent` | perfil, sesiones, llaves, seguridad, aprobaciones |
| Cuenta | authenticated | acción sensible | `IdentityPortalComponent.securityAction` | `password/*`, `factor/*`, `mailbox/*`, `passkeys/*`, `memberships/*`, `invitations/*` |
| Cuenta | authenticated | caducidad, cierre, revocación | `IdentityApiService.clear` | vuelve a anonymous, revoca datos en memoria |
| Plataforma | anonymous | login operador | `PlatformIdentityPanelComponent.action(login)` | `platform-identity/login` → challenge |
| Plataforma | challenge | MFA operador | `action(mfa)` | `platform-identity/mfa` → authenticated |
| Plataforma | authenticated | áreas Casos, Sesiones, Perfil, Seguridad, Monitor | `panel.set`/ `openSecurity` | componentes `cs-platform-operator-*` aislados de cuenta |
| Plataforma | authenticated | política de inactividad | `action(session-policy)` | `platform-identity/session-policy/update`; `minutes`, `expectedRevision`, MFA, `canManage` |
| Plataforma | cualquier estado | incidente, recuperación, cierre | `PlatformIdentityApiService` | contratos `platform-identity/*` sin heredar permisos de tenant |

## Mensajes exactos relevantes en código base

Fuente `frontend/src/app/pages/identity-portal.component.ts`, líneas 48–90, 158–170:
- `Ingresa el código de verificación.` (login inicia challenge, no autenticación completada).
- `Solicitud recibida. Esto todavía no crea una cuenta ni concede acceso.` (intención de registro).
- `Solicitud recibida. La cuenta queda pendiente de aprobación y todavía no concede acceso.` (completado de cuenta).
- `Verificación completada.` (MFA).
- `La solicitud de cierre de sesión terminó.` (cierre).
- `Si la cuenta cumple los requisitos, recibirás instrucciones en el correo verificado.` (respuesta neutra de recuperación).
- `Solicitud procesada. Los cambios de credenciales cierran las sesiones anteriores.` (mensaje genérico del resto de operaciones).

Fuente `frontend/src/app/pages/platform-identity-panel.component.ts`, línea 45:
- `Solicitud procesada por el ámbito de identidad de plataforma.` (confirmación común tras el envío de distintos formularios de operador).

Fuente `frontend/src/app/identity/identity-api.service.ts`, líneas 471–476:
- `El servicio de identidad no está disponible. Inténtalo de nuevo.` (HTTP status 0).
- `No se pudo completar la solicitud. Revisa los datos e inténtalo de nuevo.` (fallos genéricos restantes).
- El código de error interno, correo, recurso, sesión o consulta SQL no se debe incorporar a avisos de usuario.

El modal compartido en `frontend/src/app/ui/institutional-modal` presenta `feedback` (`role=status`) y `error` (`role=alert`) y cierra/restablece el formulario según su ciclo. La prueba debe verificar que un resultado previo no aparezca en otra operación ni una expiración se presente como éxito. **La línea base puede contener estos defectos; se inventarían aquí, NO se corrigen en F01.**

## Permisos y política de prueba

Los nombres de pestaña y `[disabled]` son *controles de presentación*, no una decisión backend. La revisión de membresía requiere un aprobador autorizado con MFA; la política global requiere `canManage`, revisión de versión y MFA, y la revisión de identidad requiere operadores de plataforma independientes. La seguridad debe validarse en `MySqlOperationalIdentityFlow.*`, la capa de aplicación y HTTP/MySQL (no presumir resultado desde el componente Angular).

Los endpoints de `OperationalIdentityModule` están restringidos en la composición local a loopback, host `.example.test`, JSON válido, origen permitido, separación de cookies de ámbito y denegación segura. La lectura privada `GET /api/v1/private/cases/{resourceId}` opera con autorización de recurso y no debe confundirse con la operación interna `POST /api/v1/identity/private/read`.

## Flujo crítico de sesión y tiempo

El valor inicial visible es **30 minutos de inactividad**, administrable por operador autorizado (rango UI actual 5–120). `sessions/keepalive` solamente se solicita ante actividad humana real con limitación; el backend comprueba sesión en cada operación. En F01 documentar *qué ve el usuario* a 29:xx, 30:00, después de 401 y tras nuevo login. Un screenshot de un mock `clear()` se etiqueta `expired-simulated` y **no certifica el cálculo real de expiración**.

## Estados por registrar como evidencia diferente

- Inicial/restauración (loading).
- Formulario válido, inválido nativo, error 4xx/5xx o servicio no disponible.
- Recepción aceptada pero no autenticada ni aprobada.
- MFA challenge/QR/backup, expiración o secreto limpiado.
- Sesiones sin datos y pobladas, revocación individual/todas.
- Llaves sin datos/pobladas, WebAuthn compatible/no compatible.
- Perfil sin foto/foto, historial vacío/abundante, aceptación condiciones pendiente/registrada.
- Permiso denegado tenant, sucursal, recurso y acceso plataforma↔empresa.
- Modal: apertura, Escape, click de cierre, scroll, retorno de foco, estado ocupado y mensaje previo obsoleto.

**Estado del catálogo:** rutas, servicios y textos identificados por lectura de fuentes; comportamiento y seguridad de cada estado pendiente de pruebas reales y capturas por SHA.
