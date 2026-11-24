# CRECISOL C06-02 — Modelo de roles y capacidades · especificación y estado candidato

**ADENDA FUNCIONAL DE C06-02 (24-11-2026, PRE/POST TÉCNICOS APROBADOS):** la primera especificación era deliberadamente preparatoria. La matriz canónica [C06_02_MATRIZ_ROLES_CAPACIDADES.json](C06_02_MATRIZ_ROLES_CAPACIDADES.json) y su [explicación funcional](C06_02_MATRIZ_ROLES_CAPACIDADES.md) ahora diferencian **10 perfiles** (plataforma, propietario, administrador, encargado, staff, lector, financiero, personalizado), **108/108 acciones**, **12/12 contratos internos**. [PR #197](https://github.com/crecisol-platform/CRECISOL/pull/197) integrado a `dev` SHA `68f9e3d638dff77ef95870b9a15cb1b18825c671`, PREMERGE A/B/C #45488601928 y POSTMERGE #45488939368 3/3 SUCCESS cada uno; **31 mutaciones negativas, 40 contratos .NET, 23 ataques MySQL y 20 adaptador SQL**. Adenda sujeta a CI propia del acta. **No se activa ningún grant, endpoint ni autorización ABAC.**

**ESTADO VIGENTE (24-11-2026): CÓDIGO IMPLEMENTADO, A/B/C PRE Y POSTMERGE APROBADAS; PR #194 FUSIONADO EN DEV SHA `5022328e499f1e3f039a3d308e2b1d754231215c`. CERTIFICACIÓN TÉCNICA SOLO EN MODELO INTERNO CONDICIONADA A GATES PROPIOS DEL ACTA.** El material de preparación que sigue al informe actual es historial: su estado "NO IMPLEMENTADO" describe únicamente el momento inicial del PR #194. No es la situación de esta candidata.

**Alcance exacto:** roles y propuestas de capacidades/adscripción por empresa y sucursal, verificables en Domain/Application/Infrastructure y MySQL 8.4 aislado, **sin concesión efectiva de derechos**. Se mantienen las 28 acciones futuras de C06-01 sin posibilidad de grant, y los 80 registros de inventario sin políticas C06 conectadas. C06-03/C06-04 conservan sus propias puertas de identidad y ABAC, respectivamente. No hay ninguna nueva pantalla, operación HTTP ni concesión de rol en sesiones C05.

## Informe de la candidata antes de integrar (historial preservado)

1. **Identidad:** repositorio original ID `1450414592`, ahora `crecisol-platform/CRECISOL`; base inmutable `dev` `b2e436221bca684eb43d4ce72e7044080cfd6601`. PR #194 `feature/c06-02-modelo-roles-preparacion-20261124` abierto en borrador. Ni `dev` ni `main` modificados por C06-02.
2. **Modelos y puertos:** `backend/src/Crecisol.Domain/Authorization/C06PreparedRoleSnapshot.cs`, `backend/src/Crecisol.Application/Authorization/InspectPreparedTenantRoles.cs`, `backend/src/Crecisol.Infrastructure/Authorization/MySqlPreparedRoleReader.cs`. Validación en servidor de tenant, actor, sucursal, acción y tiempo; rol/revocación; consulta SQL con JOIN por tenant y parámetros; solo devuelve candidatos no autorizantes. `AuthorizesPrivateResource=false` por contrato.
3. **MySQL:** migración aditiva `database/migrations/019_c06_prepared_roles.sql`, reversión `.down.sql`. Cinco tablas nuevas: catálogo de capacidades, roles tenant, propuestas de capacidades, propuestas de roles por membresía/sucursal y bitácora. FK compuestas, versión, revocación, bloqueo de grants, auditoría append-only, sin seed/product grants. Solo aplicada en CI sobre base desechable de MySQL 8.4, después de migraciones C05.
4. **Pruebas .NET:** `backend/tests/Crecisol.PreparedRoles.ContractsTests/`: **37 verificaciones positivas y negativas PASS**, incluyendo aislamiento de tenants/actores/sucursales, códigos no asignables, revocación, entradas de tiempo inválidas, duplicados, denegación y cancelación.
5. **Pruebas MySQL reales:** `backend/tests/Crecisol.PreparedRoles.MySqlTests/` y `database/tests/c06_02_prepared_roles_mysql.sh`: **21 ataques negativos rechazados**, **10 verificaciones del adaptador antes** y **10 después** de revocar, persistencia/versionado, privilegios sin activar y reversión aislada que preserva C05. **Diez mutaciones adversariales estructurales** fueron rechazadas adicionalmente por auditoría estática. No extrapolar estas cifras a BOLA/IDOR operativo ni a producción.
6. **Auditoría A — trazabilidad:** validación del DOCX maestro SHA-256 `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c`, del catálogo C06-01 y del diff acotado.
7. **Auditoría B — seguridad:** contratos .NET, MySQL cross-tenant, realms y aislamiento, ventanas temporales, revocación, integridad, auditoría y rollback.
8. **Auditoría C — arquitectura/compatibilidad:** capas Domain/Application/Infrastructure independientes de API, `.NET 10` con compilación sin errores ni warnings y Angular 22 compilado; frontend no cambiado. Advertencias anteriores no bloqueantes Angular: presupuestos CSS en cuatro componentes y paquetes CommonJS (`qrcode`, `dijkstrajs`); quedan fuera del cambio de C06-02. **Nota UX 9.5/10 no aplicable a interfaces porque esta microfase no crea ninguna**; no atribuir valoración visual inexistente.
9. **Evidencia verificable sobre HEAD técnico `54aee9d25a8ca7c40a9b57aa370b4785ab321884`:** [workflow propio A/B/C 3/3 SUCCESS](https://github.com/crecisol-platform/CRECISOL/actions/runs/45467607993). [Inventario C06-01 SUCCESS](https://github.com/crecisol-platform/CRECISOL/actions/runs/45467606577). [Fundamentos SUCCESS](https://github.com/crecisol-platform/CRECISOL/actions/runs/45467620845). El SHA resultante de futuras ediciones documentales requiere cotejo CI propio.
10. **Incidencia de gobierno — NO-GO de integración:** [auditor histórico C05 SHA1](https://github.com/crecisol-platform/CRECISOL/actions/runs/45467624528) falla en verificación de `qa/c05_sha1_exclusive_audits.py C`, porque su allowlist histórica compara el diff completo desde el commit del corte C05 y rechaza toda nueva ruta `backend/` o `database/` de C06. Sus pruebas A/B terminaron SUCCESS, pero la C de ámbito falla por definición. No equivale por sí misma a regresión de TOTP real; **no alterar, deshabilitar ni debilitar el candado** sin una decisión de alcance independiente y pruebas. El PR permanece draft y la CI roja se registra sin reinterpretarla como éxito.
11. **Regresiones ampliadas:** C05 operacional Angular/API/MySQL y C03-10 integral deben verificarse en el SHA exacto antes del cierre. C06-02 no certifica un motor RBAC/ABAC ni pruebas de escalada C06-10.
12. **Rollback:** revertir el PR antes de fusión no afecta datos. Si se ejecutase la migración C06 solo en un ambiente desechable, aplicar `.down.sql` con copia previa y verificar que permanezcan C05 y C04. Prohibida toda operación sobre datos reales de propietario, producción, `main` o SOLQARYN.

**Nota posterior:** la incompatibilidad histórica de C05 se corrigió por PR #195, sus gates A/B/C PRE/POST pasaron, y los de C06-02 PRE/POST pasaron sobre los SHAs registrados en `C06_02_ACTA_CIERRE.md`. La institucionalización sigue condicionada a CI propia del acta. Ningún permiso HTTP C06 se ha activado.

---

## Preparación inicial del PR #194 (historial preservado, anterior a la implementación)

**Estado:** PREPARACIÓN DOCUMENTAL; NO IMPLEMENTADO; NO PROBADO; NO CERTIFICADO.  
**Fecha:** 2026-11-24. **Repositorio autorizado:** `crecisol-platform/CRECISOL` (ID `1450414592`).  
**Base verificada:** `dev` SHA `b2e436221bca684eb43d4ce72e7044080cfd6601`.  
**Dependencia certificada:** C06-01, PR #192 y acta PR #193, solamente inventario y QA.

## 1. Autoridad y delimitación
Fuentes de preparación: Documento Maestro Único aprobado según `docs/MASTER_REFERENCE.md`, C06 y sus controles TEN; `docs/ESTADO_VIGENTE_CAPITULOS.md`; `docs/c06/C06_01_CATALOGO_ACCIONES.json`; `docs/c06/C06_01_ACTA_CIERRE.md`; `docs/planificacion-futura/C06_MICROFASES_PROPUESTAS.md`; Skill propia y `AGENTS.md` en `dev`.

El maestro C06 fue contrastado y su huella registrada en el acta C06-01; **esta preparación no constituye una nueva lectura independiente del binario Word ni revalida su SHA-256**. La implementación deberá repetir la verificación automatizada sobre el Word vigente. La numeración de ocho pasos normativos del Word no sustituye las 11 microfases técnicas.

**Objetivo exclusivo:** especificar un modelo mínimo de roles, capacidades y asignaciones contextualizadas por empresa y sucursal, sin activar grants ni modificar C05. C06-03 resolverá pertenencia/actor confiable; C06-04 evaluará ABAC de recurso/propiedad; ambas conservan sus gates independientes.

## 2. Invariantes no negociables
1. **Deny-by-default:** una acción no publicada y aprobada para concesión no puede ser asignada. Las 28 `normative_actions` de C06-01 permanecen `grantable=false` y `http_registered=false`. Una ruta pública o con prueba preautenticada no constituye un permiso de negocio.
2. **Reinos separados:** identidad PLATFORM no se convierte en identidad TENANT; una cuenta, rol o sesión de operador no hereda acceso empresarial. SuperAdmin y soporte excepcional se gobiernan posteriormente por C06-06, con MFA, motivo, plazo y bitácora; nunca acceso permanente implícito.
3. **Asignación acotada:** (actor autenticado, tenant resuelto por servidor, membresía activa, rol activo, capacidad aprobada, sucursal permitida y vigencia), sin confiar en `tenant_id`, claims autorreferidos, host manipulado, encabezados, URLs ni controles Angular.
4. **Sin escalada por delegación:** el otorgante solo puede delegar un subconjunto de sus capacidades efectivas y dentro del mismo tenant/sucursales autorizados; ningún rol tenant puede administrar políticas PLATFORM. Las reglas finales de delegación se probarán en C06 posteriores.
5. **Revocación segura:** desactivación de membresía/rol/asignación invalida su uso en la siguiente evaluación del backend; sesiones o cachés no deben mantener acceso efectivo. El mecanismo e invalidación reales requieren implementación y pruebas.
6. **Separación de funciones:** permisos financieros, privacidad, exportaciones, aprobación, reembolso y borrado lógico requieren capacidades explícitas; rol general o etiqueta UI no concede operaciones sensibles.
7. **Resguardos tenant:** clave tenant en lecturas, escrituras, auditoría y eventual persistencia; sucursal y dueño del recurso se verifican por el backend. Shared/dedicated DB no cambia las reglas.
8. **Versionado e historial:** toda asignación o revocación futura debe identificar actor, aprobador cuando corresponda, alcance, motivo, versión y tiempo; sin registrar secretos, datos personales innecesarios o tokens.

## 3. Entidades contractuales propuestas (no implementadas)
| Concepto | Campos mínimos normativos | Restricción |
| --- | --- | --- |
| CapabilityDefinition | action_id, realm, module, status, grantable, sensitivity | Derivada del catálogo C06-01; futuras no concedibles |
| RoleDefinition | role_id, realm, tenant_scope nullable únicamente PLATFORM, name, status, version | Sin roles universales TENANT que atraviesen empresas |
| RoleCapability | role_id, action_id, effect, version | Solo `ALLOW` explícito habilitado; prohibido `*` o wildcards |
| MembershipRoleAssignment | assignment_id, tenant_id, membership_id, role_id, branch_scope, status, valid_from, valid_until, version | FK y validación de tenant coincidente, vigencia y pertenencia activa |
| AuditDecision | request_id, actor_id, tenant_id, action_id, target_type/id, decision, policy_version, timestamp | Acceso mínimo, retención configurada y sin PII innecesaria |

Los identificadores son **diseño preliminar**, no aseveraciones de tablas/campos existentes. No crear migraciones sin comprobar el esquema C04/C05 y su compatibilidad real.

## 4. Roles candidatos, sin poderes habilitados
- **PLATFORM_OPERATOR:** operaciones PLATFORM expresamente permitidas; sin lectura privada de tenants.
- **TENANT_OWNER:** administración empresarial acotada a membresía/tenant verificado; no asume automáticamente privilegios financieros sensibles ni PLATFORM.
- **TENANT_ADMIN:** subconjunto delegable dentro del mismo tenant y sucursales.
- **TENANT_STAFF:** acciones operativas explícitas por sucursal; sin asignación/delegación por defecto.
- **TENANT_VIEWER:** lectura mínima explícita y filtrada por recurso; no exportación implícita.

Estos son **arquetipos para probar**, no roles persistidos, permisos predeterminados ni migración automática. La distinción de consumidor/titular de datos se estudiará en políticas C06-03/04/07, sin asumir que sea empleado de un tenant.

## 5. Matriz preliminar de decisión
| Actor/condición | Decisión exigida |
| --- | --- |
| Sin sesión o prueba válida para endpoint protegido | DENY |
| Sesión TENANT usada en operación PLATFORM | DENY |
| Sesión PLATFORM usada contra datos privados de TENANT | DENY, salvo mecanismo excepcional futuro C06-06 |
| Rol de tenant A presentado para tenant B | DENY, sin revelar existencia del recurso |
| Membresía suspendida/revocada | DENY aun con JWT, UI o caché antiguos |
| Rol correcto pero sucursal ajena | DENY |
| Capacidad inexistente, no aprobada o `grantable=false` | DENY |
| Usuario con capacidad pero recurso propiedad ajena | DENY por defecto; política C06-04 debe demostrar excepción legítima |
| Usuario con capacidad efectiva y todas las condiciones verificadas | Posible ALLOW del backend únicamente tras política final y verificación del recurso |

## 6. Pruebas a construir y ejecutar antes de certificar
**Positivas:** asignación tenant/sucursal válida; consulta de capacidades efectivas del mismo actor; rol con subconjunto explícito; revocación efectiva y versionada; denegación de rutas no contempladas por catálogo.

**Negativas prioritarias:** duplicar capacidad; acción futura concedible por mutación; realm confundido; rol cruzado entre tenants; sucursal ajena; membresía inactiva; propietario del recurso ajeno; tenant/actor falsificados por request; revocación con caché/sesión antigua; delegación mayor al techo; permisos financieros heredados; soporte sin MFA/motivo; enumeración de existencia; wildcard de permisos.

**Pruebas de persistencia (si se implementan):** claves foráneas compuestas por tenant, rollback transaccional, concurrencia/versionado, SHARED y contrato DEDICATED, integridad de bitácora, reversión de migración, fallos seguros ante indisponibilidad.

**Auditoría A:** trazabilidad individual de action_id/realm y roles contra Word/C06-01/API real.  
**Auditoría B:** aislamiento multitenant, escalada, BOLA/IDOR, revocación, confidencialidad y default deny.  
**Auditoría C:** arquitectura limpia Domain/Application/Infrastructure/API, índices/rendimiento, regresiones C05, frontend coherente y accesible si hay UI.

**Ejecución en esta preparación:** NINGUNA de las pruebas arriba descritas; NO se asignan PASS, CI ni puntuación.

## 7. Impacto, puerta de implementación y rollback
- **Ahora:** únicamente nuevo documento `docs/c06/C06_02_ESPECIFICACION.md` en rama aislada. Sin cambios funcionales en `frontend/`, `backend/`, `database/`, QA, CI, `dev` o `main`.
- **Futuro backend:** verificar modelos C05 de membresía, sesiones y roles existentes antes de diseñar el evaluador; autorización en Application/API con contratos sin dependencia de frameworks en Domain. Mantener autenticación C05 operativa.
- **Futuro database:** solo cambios tenant-safe tras revisión real de esquema y migraciones MySQL 8.4.
- **Futuro frontend:** UI de capacidades solo informativa; ocultar un botón nunca sustituye protección HTTP.
- **NO-GO:** ausencia de contraste nuevo del Word, esquema incompatible, pruebas no ejecutadas, P0/P1, claim o frontend como origen de autoridad, cambio de permisos por defecto, falta de rollback o ruptura de C05.
- **Reversión preparatoria:** cerrar el PR o revertir el único commit documental; no afecta sesiones, datos ni grants.

**Fin de la instantánea de preparación histórica:** sustituida por el dictamen acotado del acta C06-02 y su puerta documental. C06-03 solo se habilitará para preparación tras la certificación propia del acta; C07 continúa bloqueado.
