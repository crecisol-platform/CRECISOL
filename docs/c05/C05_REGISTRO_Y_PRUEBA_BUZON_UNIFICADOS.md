# C05 — Prueba unificada de registro neutral y posesión de buzón (aislada)

**Fecha:** 2026-11-14. **Alcance:** solo ensayo loopback ASP.NET Core 10 con MySQL 8.4 descartable, sin endpoints privados en `Crecisol.Api/Program.cs`. PR de trabajo #151, expediente #150.

## Camino comprobable en un mismo proceso

`POST /__test_only/c05-integration/registration-intent` valida transporte y JSON antes de invocar el caso de uso y el procedimiento MySQL de ingesta neutral. A partir del identificador *obtenido exclusivamente por el lector de fixture protegido* (no se devuelve en HTTP anónimo), la misma aplicación de ensayo ejecuta `POST /__test_only/c05-integration/proof-request` y `POST /__test_only/c05-integration/proof-confirm`, usando `VerifyNeutralRegistrationMailbox`, `MySqlDisposableEmailPossessionAdapter` y un buzón simulado que no puede entregar fuera del proceso. El comprobante del cliente es siempre neutral; no se crea ninguna cuenta, rol, membresía, sesión ni privilegio.

La cadena se prueba con un solo contenedor MySQL descartable y tres fixtures SQL ya existentes: tenants/invitaciones, ingesta y proof CAS. La CI específica ejecuta el script `database/tests/c05_identity_neutral_proof_http_mysql.sh` y exige evidencia sobre el mismo SHA de GitHub y el mismo run.

**Seguridad comprobada mediante casos ejecutables:** host B no accede a tenant A; el token de B usado en A no verifica; desafío incorrecto o reutilizado no cambia el estado; prueba auténtica cambia PENDING a VERIFIED exactamente una vez; JSON duplicado, selector de tenant, encabezados actor/Forwarded, host no verificado y campo mailbox introducido en la confirmación se deniegan antes de la llamada protegida. El correo, token y claves no se escriben en los logs.

**Importante:** este nuevo flujo prueba **posesión de buzón sintética por canal loopback**, no el proveedor SMTP real. Tampoco prueba alta de identidad+Argon2id, MFA, login contextual, emisión y revocación de sesión, rate limit compartido con login, autorización por tenant/sucursal/recurso o frontend Angular privado. A/B/C del capítulo completo siguen pendientes; C06 permanece BLOQUEADO. No atribuir `success` al nuevo corte antes de leer la CI final. `dev`, `main`, producción, Documento Maestro Único y SOLQARYN no se alteran.
