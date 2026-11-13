# CRECISOL — C05-02 · Verificación HMAC previa al claim de buzón en el backend

**Fecha:** 2026-11-13. **Repositorio único:** `crecisol-platform/CRECISOL`, rama de integración `dev`, base `9384d10a7bf319889f3906f7e97fd8afcf3d770f` (PR #127). **Alcance:** código, pruebas, auditorías y documentación. Sin cambios en `main`, Word/PDF Maestro, SOLQARYN, producción, DNS, Cloudflare, VPS ni cobros.

## Autoridad y brecha

La fuente normativa es el Documento Maestro Único (véase `docs/MASTER_REFERENCE.md`) y la especificación `docs/c05/C05_02_ESPECIFICACION.md`: el registro neutral no concede acceso; la invitación es de un uso y debe vincularse al destinatario exacto, con verificación de canal, revocación, autorización backend y consentimiento versionado. Las fases posteriores C05-03/04/05 implementarán credenciales, login y sesiones de identidad; esta prueba NO las habilita.

PR #127 demostró 75 aserciones reales y 16 consumidores concurrentes, pero su ejecutable de prueba componía manualmente la verificación HMAC y la llamada MySQL. Esa composición no puede ser la única frontera de seguridad: si una integración futura omitiese la comparación HMAC y llamase solo al procedimiento SQL, el propio SQL compara el SHA-256 de bytes de aserción guardados, **no valida su firma**.

## Corrección técnica propuesta

Se incorpora `MySqlDisposableMailboxBoundRecipientClaimLedger` como un adaptador **solo para ensayos descartables**, en `Infrastructure/Identity`:

- Recibe `TenantId` verificado del servidor, `InvitationId`, `trustedMailboxIntentId` de una asociación backend protegida, capacidad aleatoria de invitación y aserción de 125 bytes. No recibe actor/receptor/tenant de campos de autorización del navegador.
- Verifica HMAC-SHA256 de propósito separado y comparación constante, versión de clave, tenant exacto, vigencia 30–300 segundos, timestamps extremos y destinatario no vacío **antes de conectarse a SQL**.
- Usa únicamente MySQL loopback, esquema `c05_02_disposable`, sesión sin `local_infile`, procedimiento `c05_02_claim_mailbox_bound_once` y parámetros tipados. El SQL exige, en un UPDATE/CAS, buzón `VERIFIED` vigente y vinculable, consentimiento, invitante autorizado con sucursal/acción/recurso/owner, revocación de sesión y paso conjunto de invitación a `CLAIMED` y vínculo a `USED`.
- Borra copias temporales del token digerido, aserción y clave de verificación cuando corresponde. Ninguna clave aparece en código, base de datos ni logs.
- La batería `Crecisol.MailboxBound.MySqlTests` ahora llama al **adaptador de backend real**, no a un verificador HMAC ad hoc dentro de la prueba. Conserva ataques de integridad y tenant cruzado, revocaciones de buzón/sesión/permisos, SQL EXECUTE-only, doble consumo y carreras de 16 solicitantes.

**Riesgo explícito:** `trustedMailboxIntentId` actualmente solo puede originarse del fixture privilegiado. No hay todavía una identidad humana autenticada, directorio privado productivo, proveedor real de sesión, transporte externo de correo ni API de enrolamiento. Jamás exponer este método como ruta pública ni conceder EXECUTE del procedimiento a un usuario externo. El nuevo código es una barrera reutilizable, **NO un registro funcional completo**.

## Puertas y certificación

A: trazabilidad normativa y scope de `dev`, sin rutas privadas nuevas. B: validación de fuente, firma, revocaciones MySQL de dos tenants, SQL de mínimo privilegio y pruebas adversarias reales. C: compilación .NET10 strict, auditoría A/B/C de mismo SHA/run, Foundation y regresión integral C03 sobre el commit final. No contar `skipped` como PASS ni pruebas previas como evidencia del nuevo SHA.

**C05-02 NO CERTIFICADA FUNCIONALMENTE; C05-03 BLOQUEADA.** Para certificar toda C05-02 faltan, como mínimo: ruta de registro/control de uso extremo a extremo, prueba de canal externo o sustituto no productivo aprobado contractualmente, identidad efectiva y revocable del invitante/destinatario con permisos privados, alta transaccional de membresía según el capítulo correspondiente, medidas contra abuso y no enumeración E2E, rotación/retención PII y cierre de auditorías. Aceptación del propietario no convierte pruebas sintéticas en identidades humanas.

**Rollback:** revertir solo el squash de este incremento en `dev` tras análisis de dependencias; preservar el historial de PR #127 y sus CI. Ningún despliegue automático.
