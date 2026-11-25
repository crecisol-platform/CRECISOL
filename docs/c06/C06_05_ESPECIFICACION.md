# CRECISOL — C06-05 Reautenticación para operaciones sensibles

**CANDIDATA EN DESARROLLO; NO CERTIFICADA.** Base dev 9bcd3fca482ef49df12b602e3641b1fc08a1994f; repositorio original ID 1450414592, ahora crecisol-platform/CRECISOL. C06-04 certificada en su alcance, 4/11. No abrir C06-06 antes de aceptación propia de C06-05.

## Autoridad y alcance
Documento Maestro único, blob Word fe7aaab60b603f1844e18666e2f99c0ea97e86e9, SHA-256 registrado ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c. Comparado el blob del árbol base con el aprobado y los 70 extractos auditados del capítulo C06 ([C06-01 A, run 45459310029](https://github.com/crecisol-platform/CRECISOL/actions/runs/45459310029)): actor/tenant/acción/recurso/contexto, denegación por defecto, separación y auditoría sin secretos, revocación. El nuevo gate A vuelve a leer y validar el Word original, no solo este texto. Plan de 11 microfases: C06-05 step-up y finalidad justificada, sin bypass por API. El registro vigente prevalece sobre las leyendas históricas de AGENTS/SKILL. No alterar Word/PDF, main, SOLQARYN, producción, VPS, datos reales, cobros.

## Decisión de diseño e impacto
Reutilizar C05 TOTP SHA1 y recuperación de un uso y la sesión/membresía/actor comprobados en C06-03. No crear token step-up reutilizable, TTL artificial, nuevo endpoint, grant, tabla o variante criptográfica. Para acciones de múltiples pasos (alta de passkey, confirmación de factor, cambios por correo), conservar desafíos ligados a sesión/actor/revisión, caducidad y consumo C05. La verificación de forma de entrada nunca equivale a autenticación; las pruebas reales de criptografía, cuotas, bloqueo SQL, revisiones y autorizaciones específicas permanecen obligatorias. Los códigos TOTP no se convierten en firmas criptográficas de la intención; se consumen en la transacción de la operación efectiva. No usar la sesión ni un booleano del navegador como prueba.

### Operaciones sensibles reales (catálogo C06-01)
| Realm | Operación de servidor | Finalidad |
| --- | --- | --- |
| TENANT | `passkeys/register-options` | Propósito fijo de la acción autenticada |
| TENANT | `passkeys/revoke` | Propósito fijo de la acción autenticada |
| TENANT | `invitations/issue` | Propósito fijo de la acción autenticada |
| TENANT | `sessions/revoke-all` | Propósito fijo de la acción autenticada |
| TENANT | `pairings/approve` | Propósito fijo de la acción autenticada |
| TENANT | `factor/replace` | Propósito fijo de la acción autenticada |
| TENANT | `password/change` | Propósito fijo de la acción autenticada |
| TENANT | `mailbox/request` | Propósito fijo de la acción autenticada |
| TENANT | `mailbox/complete` | Propósito fijo de la acción autenticada |
| TENANT | `memberships/decide` | Motivo humano existente 8–240 caracteres |
| PLATFORM | `platform/sessions/revoke-all` | Propósito fijo de la acción autenticada |
| PLATFORM | `platform/cases/find` | Motivo humano existente 12–512 caracteres |
| PLATFORM | `platform/password/change` | Propósito fijo de la acción autenticada |
| PLATFORM | `platform/factor/replace` | Propósito fijo de la acción autenticada |
| PLATFORM | `platform/mailbox/request` | Propósito fijo de la acción autenticada |
| PLATFORM | `platform/mailbox/complete` | Propósito fijo de la acción autenticada |
| PLATFORM | `platform/review` | Motivo humano existente 12–512 caracteres |
| PLATFORM | `platform/release-share` | Propósito fijo de la acción autenticada |
| PLATFORM | `platform/session-policy/update` | Propósito fijo de la acción autenticada |

Total: 10 TENANT + 9 PLATFORM = 19 operaciones de prueba fresca. No activar las 28 acciones normativas futuras. No reclamar nuevas funcionalidades de C06-06, privacidad, finanzas ni administración de permisos. Consultas de solo sesión y recuperación por desafíos no son grants de step-up. Los motivos existentes se comprueban en el servicio, también contra entradas internas inválidas; se rechaza motivo sin letras/dígitos o con controles de línea. No se afirma que su contenido sea verdadero. Las acciones que ya no tienen un campo motivo conservan el contrato existente; su finalidad es la acción seleccionada por el servidor, no un texto impuesto en Angular.

### Archivos y límites
- Domain: C06SensitiveOperationPolicy.cs, lista cerrada y forma de prueba/motivo, sin SQL ni HTTP.
- Infrastructure: Authorized.cs, Platform.cs; guardas previas a mutaciones/dispatch; autenticación y consumo C05 intactos; HMAC de finalidad de operador en la bitácora existente, sin código ni razón en claro; resultado real conserva su semántica. El motivo de la aprobación TENANT sigue registrado como HMAC en el ledger de aprobación. No modificar ABAC C06-04.
- Pruebas de contrato .NET; E2E aislado opt-in --c06-05 junto con --c06-04, negativas de sesión sin prueba, campo inventado, razón inválida con prueba fresca, prueba inválida, estado inalterado y replay entre operaciones, éxito real de aprobación desde Angular. Pruebas de C05 y C06-03/04 no se suprimen.
- A/B/C/D CI read-only y scope exacto; ampliar únicamente la lista de rutas de diff de auditores anteriores C06-02/C06-03/C06-04 para las rutas de esta fase (mantener todas sus guardas, compilaciones, negativas y reglas DB/API). 
- Frontend y DB: sin cambios. Nota visual N/A para el diff, no certificar nueva interfaz. Política de inactividad administrable y MFA de C05 intactos.

## Puertas de aceptación
A: verificar maestro, inventario de las 19 acciones existentes y diff por SHA. B: contratos .NET, separación realm, input y causa, pruebas negativas HTTP y consumo real de C05. C: arquitectura, .NET10 y Angular22 con Node24; regresiones C06-03/04 y C05. D: Chromium/Kestrel/MySQL8.4 desechable, E2E real y rollback heredado. Revisar resultados y logs sobre HEAD y SHA integrado; no evaluar 9.5/10 ni comprar antes de PASS y revisión de alcance, sin P0/P1. Revisión estática no sustituye runtime. 
Rollback: revertir el PR técnico sin migración; los datos, credenciales, MFA, sesión y bitácora C05 existentes conservan sus contratos. Evaluar por separado cualquier regresión antes de fusionar. 
Riesgo residual: no se cambia el protocolo OTP por una firma de operación ni se afirma que la justificación sea verdadera. C06-06 definirá el acceso excepcional temporal; cualquier acción futura requiere catálogo, autorización y gate propio.
