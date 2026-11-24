# CRECISOL C06-03 — Identidad, pertenencias y contexto autorizado

**Estado:** PREPARACIÓN DE CAMBIO — NO IMPLEMENTADA / NO PROBADA / NO CERTIFICADA. Los resultados de CI modificarán expresamente este estado, nunca por anticipación. Fecha 24-11-2026.

## Fuente y puerta
Repositorio `crecisol-platform/CRECISOL` (ID `1450414592`), rama base `dev` SHA `e5fe2830ab8284238389d7a8d5cc6d4add1a0b99`; Documento Maestro Único C06 (SHA-256 aprobado `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c`, a comprobar nuevamente por CI), `docs/MASTER_REFERENCE.md`, `docs/ESTADO_VIGENTE_CAPITULOS.md` y gobernanza propia. Dependencia C06-01/02 certificadas solo en sus alcances: 80 HTTP inventariadas, 28 acciones futuras no concedibles y roles/propuestas sin grants.

**Objetivo exacto:** confirmar en **el servidor**, antes de realizar operaciones autenticadas C05, que sesión, cuenta, factor MFA, tenant seleccionado **solo por host canónico verificado del backend**, y pertenencia vigente corresponden al **mismo actor y organización**. El navegador jamás podrá seleccionar actor, tenant, membresía, política de sesión, conexión MySQL o rol por enviar campos, headers, cookies distintas al token opaco o claims inventados.

## Límites y dependencias verificadas
La ruta C05 existente ya resuelve host desde `c04_verified_tenant_host` + `c04_tenant_catalog` y valida el token opaco usando HMAC, `c05_identity_session`, `c05_identity_account`, `c05_identity_membership`, factor MFA y política de inactividad configurable (actualmente 30 min; rango 5–120) en **una transacción MySQL**. No crear un motor paralelo de login, JWT ni un segundo selector de empresa. No tocar el módulo PLATFORM, el esquema C05, el catálogo de acciones C06-01 ni la matriz C06-02.

C04-05/`ResolveCurrentTenantIdentityContext` es un **contrato interno anterior no operativo** que carece de adaptadores reales. C06-03 utilizará un control real **dentro de la misma transacción operativa C05**, con evidencia tipada Domain derivada únicamente de las filas bloqueadas de la base, no un adaptador ficticio construido con claims. No expondrá esa evidencia a HTTP: **no es permiso para recurso** y C06-04 todavía debe aplicar ABAC/propiedad.

## Implementación prevista (puerta a verificar antes de integrar)
- Domain: `C06OperationalMembershipContext` y `C06LockedIdentityEvidence` de solo lectura, que rechacen incongruencias entre host, sesión, cuenta, membresía y MFA; fechas y revisiones inválidas; expiración/inactividad; tenant/actor/membresía ajenos.
- Infrastructure: método C06 que reconstruya la evidencia **desde filas C05 validadas en la transacción** y exija su integridad antes de cualquier operación autenticada (sin propagar secretos, correo ni credenciales). C05 mantiene su verificación anterior.
- Backend/API: endurecimiento de la consulta C04 de host para impedir alias/tipos de dirección incoherentes y cambios de catálogo; se usan exclusivamente Host directo filtrado, política server-side y token opaco. No se registra nueva ruta HTTP ni selector de tenant.
- Database: sin migración nueva. Se reutilizan PK/FK, índice de token y registros tenant-scoped C04/C05. No hay acceso a datos reales ni recursos de usuario.
- Frontend: sin modificaciones; no es una protección dependiente de botones.

## Pruebas de aceptación obligatorias
**Positivas:** sesión MFA vigente con membresía activa en tenant A, misma membresía/revisión y host; cuenta con más de una membresía en empresas distintas que no permite heredar la de B; continuidad del mismo flujo C05 sin cambiar HTTP ni MFA.

**Negativas:** actor de formulario o header falso; token ajeno, no presentado, inválido o revocado; sesión caducada/inactiva o revisión desactualizada; cuenta suspendida o correo no verificado; membresía pendiente, rechazada, suspendida, vencida o revocada; sesión de A presentada en host B; cambio de tenant por Host/Forwarded/X-Tenant/URL/claim; sesión y membresía con distintos account_id/tenant_id/membership_id; factor MFA ajeno o revocado; host expirado, suspendido o con `address_mode` distinto; operación simultánea de revocación; fecha de servidor y policy no válidas. No distinguir en el HTTP la existencia de una cuenta de otro tenant.

**Auditoría A:** Maestro, estado, trazabilidad con C06-01/02 y equivalencia de rutas. **B:** mutaciones negativas contra aislamiento, sesión y membresía, ninguna escalada; no datos sensibles en respuestas/logs. **C:** Domain sin framework, Infrastructure SQL en transacción, compilación .NET10/Angular22, E2E C05 real, ausencia de regresiones y rollback. Registrar SHA y resultados positivos/negativos por ejecución, no afirmar PRE/POST antes de obtenerlos.

**Criterio comercial de aceptación ≥9.5/10:** si no pasan todas las comprobaciones críticas de identidad, actor, tenant, sesión, revocación y host, o queda P0/P1, **NO COMPRAR / NO CERTIFICAR**. Sin pantallas nuevas: se evalúa calidad técnica, funcional y de seguridad; no inventar una nota de UX visual.

## Impacto y reversión
Cambios previstos en `backend/src/Crecisol.Domain/Tenancy/`, extensión aislada `backend/src/Crecisol.Infrastructure/Tenancy/`, las dos funciones operativas C05 necesarias para enlazar al guard, proyecto de pruebas `backend/tests/Crecisol.C06Membership.ContractsTests/`, auditor de C06-03, workflow exclusivo y este documento. Revertir PR antes del merge no cambia datos. Posterior: revertir commit con revisión de dependencias; **no hay migración ni operación destructiva**. Sin cambios en `main`, Word/PDF, SOLQARYN, producción, VPS ni cobros.
