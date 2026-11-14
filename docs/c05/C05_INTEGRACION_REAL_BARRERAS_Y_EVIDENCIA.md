# CRECISOL — Capítulo 05: integración real, evidencias y barreras no resueltas

**Fecha:** 14-11-2026. **Repositorio:** crecisol-platform/CRECISOL (ID 1450414592). **Base de integración:** dev 8b9e1d5919dda192990c34fb096b235cb94bfec8. **PR de implementación:** #151. **Expediente de bloqueo:** #150.

## Diagnóstico contrastado con el código

1. backend/src/Crecisol.Api/Program.cs solo publica GET /api/v1/public/branding/platform. La identidad, la autorización privada y el aprovisionamiento de cuentas reales NO están registrados allí.
2. C05-01..10 contienen contratos y algunos adaptadores de MySQL descartable, pruebas HTTP aisladas, Argon2id, MFA y sesiones; **no** existe un circuito único confiable que conecte todas las transiciones y permita a una persona real iniciar sesión de cliente.
3. El PR #149 certificó 11/11 microfases solamente en contratos, adaptadores y fixtures sintéticos; su NO-GO productivo se conserva íntegro.
4. El registro mantiene el local-part del buzón con sus mayúsculas, mientras el login lo convierte a minúsculas. Antes de enlazar identidad persistente, resolver colisiones, alias, unicidad y semántica de propiedad del buzón bajo política backend comprobable.

## Implementación de conexión neutral verificada por diseño

- Cortes 00/01: filtro HTTP del Host directo, POST JSON con tamaño acotado, rechazo de autoridad inyectada, duplicados de JSON y payloads desconocidos.
- Corte 02: NeutralRegistrationIntake encadena las barreras con el puerto de registro neutral, SIN endpoint habilitado en API del producto.
- Nuevo runner MySQL: únicamente en backend/tests/Crecisol.NeutralRegistration.IntegrationTests/Program.cs se publica ruta de ensayo local que lleva HTTP → preflight → parser → RequestNeutralRegistrationIntent → MySqlDisposableNeutralRegistrationRecorder → procedimiento almacenado de MySQL8.4 desechable. Se examina persistencia de tenant B sin modificar A y 9 casos de entradas hostiles, además de las pruebas históricas de cuota y cifrado.
- Nuevo workflow: .github/workflows/c05-identity-neutral-mysql.yml compila .NET10 y ejecuta pruebas MySQL8.4 por digest con A/B/C ACOTADAS al ingreso neutral. Solo un run completado y exitoso sobre el SHA final valida este incremento. **No prueba el resto de autenticación.**

## Bloqueos obligatorios antes de certificar C05 en alcance operacional

| ID | Control faltante | Criterio exigido | Estado |
| --- | --- | --- | --- |
| C05-P0-01 | Entrega y prueba de buzón en el mismo proceso | Antienumeración, prueba firmada, consumo atómico, correo real solo cuando se autorice | PENDIENTE |
| C05-P0-02 | Directorio y credenciales persistentes | Canonización única, alta segura, Argon2id, duplicados denegados, aislamiento de realms | PENDIENTE |
| C05-P0-03 | Login contextual de API | Host verificado, membresía fresca, rate limit ANTES del hash y sin selector de tenant | PENDIENTE |
| C05-P0-04 | Sesión revocable de API | MFA/step-up, rotación/revocación CAS y concurrencia negativa real | PENDIENTE |
| C05-P0-05 | Recuperación/cambio sensible | Prueba un solo uso, reautenticación, no bypass MFA, revocación | PENDIENTE |
| C05-P0-06 | Autorización HTTP multitenant | Backend deny-by-default, tenant+sucursal+recurso+actor y BOLA/IDOR A/B | PENDIENTE |
| C05-P0-07 | Persistencia integrada | Transacciones, migraciones reversibles, permisos mínimos, trazas y rollback | PENDIENTE |
| C05-P0-08 | E2E y auditorías A/B/C completas | .NET10, MySQL8.4, Angular22, Chromium y mismo SHA / runs, sin P0/P1 aplicables | PENDIENTE |

## Alcance seguro de la fase actual

No registrar rutas de identidad en la API del producto, ni emitir JWT, cookies, grants, sesiones o MFA desde un fixture. No introducir datos productivos, SMTP, Cloudflare, VPS, KMS externo, WAF, cargos o despliegue. Documentar la evidencia por PR, commit y run; un resultado skipped/cancelled no es aprobación. La CI de este corte es una **prueba de integración de registro neutral, no certificación de autenticación**.

**Dictamen vigente:** C05-11 permanece cerrada en su alcance sintético. La integración de identidad real y la habilitación de C06 están BLOQUEADAS. El PR #151 continúa como borrador y debe permanecer sin fusionar hasta resolver y auditar las dependencias P0.
