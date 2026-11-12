# CRECISOL — C04-08 · Especificación preparatoria de suspensión, reactivación y borrado lógico

**Fecha:** 2026-11-12. **Estado:** PREPARACIÓN, NO IMPLEMENTADA, NO PROBADA, NO CERTIFICADA.
**Repositorio:** `crecisol-platform/CRECISOL` (GitHub ID 1450414592); base `dev`; rama `feature/c04-08-preparacion-ciclo-vida-20261112`.
**Alcance:** contratos internos sintéticos Domain/Application y pruebas desechables. Prohibidas rutas privadas, adaptadores productivos, nuevas migraciones productivas, producción, cobros, DNS/Cloudflare, cambios en main, Word/PDF maestro o SOLQARYN.

## 1. Puerta de entrada y autoridad

- C04-07 certificada **solo en alcance sintético** por PR #96/#97 y comentario institucional permanente de PR #97; SHA final institucional `1d4d37a1850f5dcb338729582d6f2a38e273a742`, con 20/20 workflows aplicables SUCCESS según acta de cierre. Comprobar antes de cualquier integración técnica que `dev` no haya retrocedido ni introducido P0/P1.
- Fuente funcional: `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx` (SHA-256 registrado `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c`), `docs/MASTER_REFERENCE.md`; capítulo normativo C04, 72 párrafos y huella textual consignada en acta C04-07. Matriz de doce microfases técnicas en `docs/planificacion-futura/C04_MICROFASES_PROPUESTAS.md`: positivo «Transiciones seguras y auditables», negativo «Suspensión no deja acceso residual».
- La certificación de C04-07 **no** prueba autenticación ni revocación de sesiones reales. La preparación C04-08 no puede afirmar esas capacidades.

## 2. Máquina de estados preliminar (sujeta a cotejo final con Word)

Mantener separados el estado de aprovisionamiento `Draft` y el estado de habilitación de acceso; NO activar tenants desde la creación sintética.
- `Draft`: sin acceso privado; eventual activación exige decisión administrativa autenticada y autorizada *en fases habilitadas*, no por esta preparación.
- `Active`: estado conceptual que permitiría acceso únicamente tras las demás verificaciones de identidad, membresía, rol, sucursal, recurso y vigencia; el estado `Active` **nunca es permiso por sí solo**.
- `Suspended`: denegación inmediata en toda nueva evaluación de autorización y procesos internos; retener información sin borrarla. Bloquear escritura, lecturas privadas, trabajos en cola, exportaciones, previsualizaciones, medios, caché y sesiones al integrar esas superficies.
- `Deleted` lógico: negar acceso y aprovisionamiento ordinario; preservar evidencia, integridad referencial, auditoría y retención legal aplicable. Ninguna operación física de borrado/destrucción incluida.
- `Reactivated` no es estado persistente independiente: evento auditado de `Suspended -> Active` solo con autorización confiable, revisión coincidente, motivos y comprobaciones vigentes; no por pago/HTTP/host o token aislado.

**Transiciones a validar:** `Active -> Suspended`, `Suspended -> Active` y `Active|Suspended -> Deleted` lógico. Transiciones desde `Draft`, recuperación de `Deleted`, repetición con mismo ID o cambios de negocio requieren decisión normativa explícita; en tanto, **DENY por defecto**. No admitir `Deleted -> Active` por la operación de reactivación.

## 3. Contrato mínimo propuesto

Caso de uso interno por comando de administración confiable con `TenantId` interno, `ActorId`, acción tipada, `ExpectedRevision`, `OperationId` idempotente, `ReasonCode` validado, motivo acotado y `UtcNow` inyectado. Ninguno de esos campos es una credencial ni concede autorización. No incluir DSN, URL de conexión, secretos, ni estado impuesto por cliente.

El adaptador futuro deberá realizar **una sola operación transaccional** con lectura bloqueante, comprobación de tenant/actor/permiso/revisión y estado de origen, CAS de revisión, actualización de estado, invalidación o epoch de autorización y registro auditado inmutable; efectos posteriores mediante outbox idempotente y política fail-closed. Precomprobaciones en memoria no sustituyen esas condiciones en la misma transacción. Reintentos exactos devuelven resultado previo; operaciones con igual ID pero diferente intención se rechazan.

La caché, tokens/sesiones y workers futuros deben validar el epoch/revisión actual; sin control de revocación efectiva no habilitar acceso ni declarar la suspensión productiva. No confiar en contenido de token previamente emitido para sortear estado suspendido.

## 4. Matriz positiva y negativa obligatoria

| Caso | Resultado esperado |
| --- | --- |
| Suspender Active con actor autorizado, revisión vigente | Estado Suspended, revision +1, evento auditado único, acceso lógico DENY |
| Repetir idéntica operación | Replay inmutable sin segunda transición ni segundo evento |
| Reactivar Suspended con autorización y prerequisitos vigentes | Active con nueva revisión y evento de reactivación |
| Borrado lógico Active o Suspended | Deleted, datos retenidos y referencia existente; acceso DENY |
| Token/sesión previamente emitidos tras suspensión | DENY por estado/epoch vigente; nunca aprovechar autorización vieja |
| Worker/cola/export/cache/preview después de suspensión | DENY/pausa y aislamiento; sin exposición residual |
| Actor, tenant, host o claims falsificados | DENY sin modificación ni filtración |
| Reactivación de Deleted o Draft | DENY |
| Cambio de estado con revisión obsoleta | CONFLICT/DENY; cero efectos secundarios |
| Dos cambios concurrentes con misma revisión | Uno solo confirma, el otro conflicto; auditoría exacta |
| OperationId duplicada con intención diferente | DENY por colisión semántica |
| Fallo previo a commit y pérdida de respuesta postcommit | Rollback íntegro/replay exacto sin doble evento |
| Tenant A intenta afectar tenant B | DENY, sin diferencias de información observables |
| Falta de auditoría o invalidación de epoch | Rollback/NO_GO de activación |

## 5. Implementación y pruebas exigibles antes del cierre

1. Revisar tipos actuales `TenantLifecycleState`, `TenantProvisioningIdempotency`, interfaces Domain/Application y contratos de autorización C04-05/06; evitar duplicar enums o redefinir `Draft`.
2. Abrir PR técnico aislado de la preparación: contratos y casos de uso internos, sin wiring API/DI ni migraciones productivas.
3. Pruebas .NET 10 Release `-warnaserror` para transiciones, concurrencia, idempotencia, fallos, autorización negativa, aislamiento tenant, reversión; registrar conteos **realmente ejecutados**.
4. Fixture MySQL 8.4/InnoDB desechable, fuera de producción, con transacciones, CAS, integridad auditada, carreras y rollback; registrar positivos y negativos **reales**, no simulados.
5. Auditoría **A** funcional/normativa: trazabilidad contra el Word, regresión C04-07 y alcance C04-08.
6. Auditoría **B** seguridad: denegación residual, cross-tenant, sesiones stale, trabajos, caché, colisiones, reactivación y bypass de revisión.
7. Auditoría **C** arquitectura/rendimiento/reversibilidad: Domain sin frameworks, Application por puertos, sin API ni secretos, SQL acotado y sin lock inversion/deadlocks sin tratamiento.
8. Ejecutar CI aplicable PREMERGE de SHA final; corregir todos los fallos con nuevo SHA; merge solamente a `dev` tras PASS; CI POSTMERGE, acta documental independiente, su PRE/POSTMERGE y dictamen con SHA/run IDs. Los `skipped` condicionales no se cuentan como PASS.

## 6. Riesgos, gate y reversión

**P0:** acceso residual a datos privados; reactivación ilegítima; efectos cross-tenant; invalidación de sesión/caché no transaccional. **P1:** doble transición/auditoría; borrado lógico irreversible por accidente; divergencia de revisión; carreras sin política de retry. Tratarlos con denegación por defecto, CAS, auditoría y pruebas negativas.

**NO_GO** mientras falten evidencia real de pruebas y auditorías A/B/C, exista P0/P1 abierto, SHA no coincidente, workflows obligatorios pendientes o el ensayo se interprete como producción. La fase queda únicamente PREPARADA. Rollback: revertir este documento/PR de preparación; no se cambia base de datos ni datos productivos.

**Siguiente fase:** C04-09 BLOQUEADA hasta certificación efectiva de C04-08. Capítulo C04 NO CERTIFICADO (7/12 microfases certificadas al entrar); capítulos completos C01–C03 = 3/37.
