# CRECISOL C06-02 — Especificación preparatoria del modelo de roles y capacidades

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

**Dictamen actual:** PREPARACIÓN ABIERTA. La implementación de C06-02 requiere su propio contrato, diseño revisado y pruebas con evidencia por SHA; este texto no certifica C06-02 ni habilita C06-03/C07.
