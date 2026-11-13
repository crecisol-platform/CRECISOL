# CRECISOL — C05-02 · Integración aislada de outbox cifrado (NO certificación)

**Fecha:** 2026-11-13. **Estado:** código propuesto para revisión y pruebas; C05-02 funcional **NO CERTIFICADA**; C05-03 bloqueada. **Repositorio único:** `crecisol-platform/CRECISOL`, ID 1450414592; base `dev` SHA `a49fe605eb25916ca554c0d6a7f69e30648ce75b`. Rama feature/c05-02-protected-delivery-outbox-20261113. Sin cambios en `main`, SOLQARYN, Word/PDF, DNS, Cloudflare, producción ni cobros.

## Fuentes

- Documento Maestro Único Word, SHA-256 declarado/verificado previamente `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c`; PDF `0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77`. Fuente C05 SHA-256 `bf7036849d6f1aad36a3a111b62121947aa12fae8c2a0551e63b0cc788d4a0aa`, según `docs/MASTER_REFERENCE.md` y `docs/c05/C05_02_ESPECIFICACION.md`. No se modifican sus bytes en este PR.
- PR #119: recepción pública neutral en Kestrel/MySQL de prueba, sin DI o endpoint productivo.
- PR #120: prueba independiente de posesión de canal, CAS MySQL y buzón **solo en memoria**; seis ejecuciones POSTMERGE SUCCESS en SHA `a49fe605eb25916ca554c0d6a7f69e30648ce75b`.
- PR #121 permanece borrador documental con matriz E01–E12. Este PR técnico no declara haber superado esa matriz completa.

## Qué implementa este cambio

1. Tabla InnoDB `c05_02_delivery_outbox` por (tenant,intent) con digest, nonce, tag, ciphertext de 32 bytes, versión de clave, estado, lease, tres intentos máximos, vencimiento y FK compuesta al proof. **No contiene correo en claro ni bearer en claro**.
2. Procedimiento `c05_02_outbox_issue` con `SQL SECURITY DEFINER`: valida tenant ACTIVE, intención almacenada y expiración, bloquea tenant→intent, y persiste *challenge PENDING + outbox AEAD* en una sola transacción. Un segundo issue devuelve DENIED, también tras revocación. El rol técnico solo tiene EXECUTE.
3. `c05_02_outbox_claim`: lock y lease de 120 segundos sobre tenant+intent con reto aún vigente; solo devuelve ciphertext protegido, nunca mailbox ni capacidad en claro. Máximo tres leases; agotamiento revoca el proof. Crash/reintento conserva el **mismo desafío** y puede reenviarlo; no hay garantía de exactamente un mensaje si el proceso cae después del envío físico.
4. `c05_02_outbox_mark_sent` y `c05_02_outbox_mark_failed`: cierre con nonce exacto del lease o revocación atómica de outbox/proof todavía pendientes, sin conceder cuenta, membresía, rol, JWT ni sesión.
5. `MySqlDisposableProofDeliveryOutbox` en Infrastructure crea código CSPRNG del Domain; deriva clave HMAC separada por propósito, cifra AES-256-GCM con AAD exacta tenant UUID + intent UUID + digest + versión. Borra buffers en finally, compara digest protegido a tiempo constante tras descifrar y usa el puerto de entrega **local** existente, que obtiene el destino del intent AES-GCM previamente guardado. Solo admite DB `c05_02_disposable` en loopback, sin registros de DI o rutas productivas.

**Límites:** No hay proveedor SMTP/HTTP real, buzón de destinatario externo, autenticación de invitante, identidad/membresía persistente shared/dedicated, grant, password, MFA, JWT ni sesión. Si se observa fallo o timeout ambiguo del canal, se revoca el desafío pendiente; si el proceso termina súbitamente, se permite recuperar un lease expirado y reenviar el mismo código hasta el máximo. Una suspensión que ocurre después de claim y antes de la entrega aún exige una barrera de revalidación atómica para producción. La contención antienumeración temporal real, la defensa multi-IP, rotación de claves operacional, eliminación por retención y webhook autenticado continúan pendientes. **No conectar en API ni usar en producción.**

## Pruebas propuestas / evidencia requerida

- .NET10 Release `--warnaserror`, API y contratos previos; MySQL 8.4 real en Docker fijado por digest, bind a 127.0.0.1, credenciales aleatorias desechadas.
- Dos tenants, seis intents cifrados, 16 emisores concurrentes con una sola persistencia, 16 trabajadores con una sola reserva; desafío verificado una vez y replay denegado.
- Negativos: tenant ajeno, host no verificado (verificado en PR #120, NO re-probado en esta suite), DSN remoto, SELECT/UPDATE directo denegado al rol worker, manipulación AES-GCM, caída del transporte, suspensión, recuperación de lease tras crash, límite de tres intentos.
- QA A: procedencia, propósito y alcance. QA B: mutaciones adversarias más las aserciones .NET/MySQL. QA C: SHA/run exactos y alcance de diff; comprobar regresiones.
- Registrar números reales de aserciones, workflow/run, SHA y fallos/correcciones solo **después** de ejecutar; nunca inferir SUCCESS por crear estos archivos.

**Rollback:** revertir únicamente el commit de esta integración en la rama de desarrollo, previa revisión de dependencias. Docker, usuarios, secretos y datos de prueba desaparecen en cleanup. El Documento Maestro, `main` y servicios de producción permanecen intactos.

**Dictamen actual:** C05-02 no se certifica mediante esta integración. C05-03 sigue bloqueada. El PR puede fusionarse únicamente para su alcance técnico aislado después de A/B/C, pruebas reales y revisión de alcance, sin asumir capacidad funcional completa.
