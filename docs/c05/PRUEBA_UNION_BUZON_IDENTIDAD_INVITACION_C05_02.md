# CRECISOL — C05-02: unión de prueba de buzón y aserción firmada

**Fecha:** 2026-11-13. **Repositorio:** crecisol-platform/CRECISOL, ID 1450414592. **Base dev:** 4616484e0a238f7accdeda803b5341d080d81c12 (PR #126). **Rama:** feature/c05-02-mailbox-proof-claim-gate-20261113.

**Estado:** ENSAYO TÉCNICO, NO CERTIFICACIÓN FUNCIONAL; C05-03 BLOQUEADA. Main, SOLQARYN, Word/PDF y producción sin modificaciones.

**Documento Maestro Único:** docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx y docs/MASTER_REFERENCE.md. Hash histórico Word ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c, PDF 0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77, extracción C05 bf7036849d6f1aad36a3a111b62121947aa12fae8c2a0551e63b0cc788d4a0aa. No afirmar que estas huellas fueron recomputadas en esta intervención.

## Hallazgo

La entrega SMTP independiente SOLO local y la firma HMAC-SHA256 con revocación atómica de PR #126 estaban probadas por separado. Falta una decisión de consumo que exija ambas evidencias para el MISMO tenant, destinatario, invitación, buzón e instante. Un correo o token por sí solo NO constituye identidad autenticada.

## Nuevo ensayo MySQL 8.4

- c05_02_mailbox_claim_channels: el ADMIN del fixture sintetiza una asociación (tenant,recipient,mailbox_digest), ACTIVE/REVOKED. No es un directorio real de identidad.
- c05_02_mailbox_claim_bindings: exactamente una asociación por tenant e invitación entre recipient, intent, sesión firmada, hash del mailbox y SHA256 de los 125 bytes de la aserción; unique tenant+intent y FKs tenant-scoped. READY/USED/REVOKED.
- c05_02_claim_mailbox_bound_once: ÚNICO UPDATE multitabla InnoDB cambia invitación PENDING->CLAIMED y binding READY->USED bajo todas las condiciones actuales: issuer original con cuenta/sesión/vínculo/scope/revisions, branch/recurso/owner/action; tenant activo, destinatario exacto, términos, consentimiento, canal vigente, sesión de destinatario activa y firmada, prueba de mailbox VERIFIED y no expirada, digest exacto del token y digest de la aserción. Denegación uniforme si no actualiza exactamente dos filas, sin grants.
- Integración de pruebas .NET 10 con MySqlDisposableEmailPossessionAdapter: emite y verifica un challenge de laboratorio sin enviarlo a internet; firma sintética de test, dos tenants, cambios adversarios posteriores, 16 reclamos concurrentes y SQL worker exclusivamente EXECUTE.
- El SHA256 de la aserción almacenado obliga a poseer los bytes exactos; MySQL NO verifica por sí mismo la firma HMAC. Al integrar, un backend fiable deberá ejecutar también el verificador HMAC de PR #126 y nunca admitir llamadas directas al procedimiento desde cliente.

## Bloqueos P0/P1 intactos

Falta autenticación efectiva de personas/operadores, vinculación comprobada de recepción externa de correo a sujeto autenticado, MFA/step-up cuando corresponda, revocación real de sesión, antienumeración E2E, protección contra abuso distribuido, alta transaccional de cuentas/membresías con permisos por tenant y router shared/dedicated, consentimiento legal versionado real, rotación/retención de claves, notificación y recuperación segura. Las filas de sesiones, canal e intent son METADATOS SINTÉTICOS del ensayo, no sesiones JWT ni correo real. La ruta histórica de claim sin prueba de buzón no puede servir como vía privada productiva.

## Puertas A/B/C

A: fuente normativa, compatibilidad C04/C05-02, diff y ausencia de API/DI. B: negativos en MySQL8.4 reales, SQL mínimo privilegio, firmas/ints/digests alterados, tenants cruzados, revocación, consentimiento, 16 reclamaciones simultáneas, mutaciones estáticas. C: prueba misma ejecución SHA/run, .NET10 Release -warnaserror, regresiones Foundation y C03 integral, rollback y límites. Antes de CI marcar NO EJECUTADO, jamás SUCCESS.

**No certificar C05-02 funcional ni avanzar C05-03 mientras exista un P0/P1.** Sin producción, despliegues, pagos, DNS, VPS, main ni SOLQARYN. Reversión: revertir solo el squash de este PR tras revisar dependencias, conservar toda evidencia histórica.
