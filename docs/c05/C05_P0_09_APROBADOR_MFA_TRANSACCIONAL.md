# CRECISOL — C05-P0 · Aprobación de membresías ligada a MFA real de ensayo

**Corte:** continuación del PR #151, 14/15-11-2026. **Alcance:** exclusivamente código, MySQL 8.4 descartable, .NET 10, pruebas y auditorías. **No habilitar C06 ni producción.**

## Debilidad anterior y solución

La activación inicial del harness se basaba en una autoridad y una orden sembradas por configuración privilegiada; el SQL comprobaba revisiones pero **no probaba una sesión MFA de un aprobador existente**. Ese flujo histórico se conserva como ensayo separado, nunca como prueba de aprobador real.

Se añade un recorrido adicional para un *segundo candidato verificado* cuya aprobación exige **un aprobador diferente, cuenta activa, contraseña Argon2id previamente comprobada, TOTP HMAC real, sesión MFA vinculada y vigente, Host verificado del backend y grant específico `memberships.approve`**. El grant se siembra únicamente por el administrador de la base descartable para demostrar el gate; no equivale a un operador humano ni a un RBAC productivo.

## Arquitectura y operación de ensayo

- `ActivateMembershipWithMfaApprover` (Application) recibe bearer opaco, host verificado y candidato. Solo el protector del servidor obtiene digest HMAC; no recibe ID del aprobador, códigos TOTP ni flags falsificables.
- `MySqlDisposableMfaMembershipApprovalStore` (Infrastructure) exige conexión local `c05_02_disposable`, ejecuta solo procedimiento protegido y borra su copia del digest.
- `c05_02_activate_by_mfa_approver` (database/tests) usa una transacción InnoDB cruzando `c05_02_disposable` y `c05_05_disposable` **en la misma instancia descartable**. Obtiene actor desde token digest, comprueba vinculación de desafío, factor vigente, sesión no revocada, cuenta/credencial/tenant activos, revisiones, buzón original del candidato, prueba vigente, aprobación distinta del propio actor, host, grant `memberships.approve` y expiraciones. Solo entonces inserta orden consumida, membresía y cambio de candidato en la misma transacción; rechazos hacen ROLLBACK sin emitir sesión.
- Usuario SQL `c05_02_mfa_approval_worker`: exclusivamente EXECUTE del procedimiento, sin SELECT/UPDATE directo de credenciales, tokens, grants ni membresías.

## Auditoría exigida para cierre del corte

A funcional: caso legítimo con una sesión Argon2id/TOTP previamente probada, un solo ganador entre 12 activaciones concurrentes, orden/autoría verificables y retorno a estado neutro del fixture.

B seguridad: denegación sin grant, grant suspendido o revocado, tenant extranjero, self-approval, token faltante o falso, usuario SQL sin SELECT; el procedimiento es la única frontera de escritura.

C arquitectura: módulos Application/Infrastructure/DB separados, no registro en Program.cs ni recursos reales; no introducir migración productiva, datos personales ni SMTP. Compilar .NET 10 estricta, ejecutar MySQL8.4 y regresiones C03/C04 del SHA final.

**Bloqueos restantes:** conceder permisos y aprovisionar aprobadores reales con MFA en una identidad persistente de producto, unificar recuperación/sesiones y desarrollar endpoints privados + Angular autenticado. **El corte no certifica C05 operativo.** Conservar #151 DRAFT y `dev`/`main` sin cambios hasta completar P0 y repetir A/B/C integrales. No tocar SOLQARYN, VPS, Cloudflare ni cobros.
