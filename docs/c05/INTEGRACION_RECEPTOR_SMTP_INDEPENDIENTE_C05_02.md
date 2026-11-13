# CRECISOL — C05-02: receptor SMTP independiente, SOLO ENSAYO LOCAL

Fecha: 2026-11-13. Estado: nueva integración técnica en evaluación. **C05-02 NO CERTIFICADA funcionalmente; C05-03 bloqueada.** Repositorio autorizado crecisol-platform/CRECISOL (ID 1450414592), base dev SHA 407d233b3c490d0c2c4e8e2c8611e63b058be558.

## Procedencia y contexto

- Documento Maestro Único: Word SHA-256 ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c; PDF SHA-256 0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77; texto de C05 SHA-256 bf7036849d6f1aad36a3a111b62121947aa12fae8c2a0551e63b0cc788d4a0aa. Huellas históricas documentadas en docs/MASTER_REFERENCE.md. Estos bytes NO se modifican.
- PR #119: ingesta HTTP neutral/MySQL; PR #120: prueba criptográfica independiente de correo; PR #122: outbox AEAD transaccional y leases, 4/4 POSTMERGE SUCCESS sobre 407d233b3c490d0c2c4e8e2c8611e63b058be558.
- Matriz de requisitos E01–E12: docs/c05/C05_02_PUERTA_ENTREGA_PRIVACIDAD_20261113.md, PR #121 aún borrador. El incremento profundiza E01, E03, E06, E09, E11 y E12; NO acredita el cierre del resto.

## Qué se propone implementar

1. **Envío SMTP solamente loopback**: LoopbackOnlySmtpProofSink implementa ITrustedLocalProofMailboxSink; el destino se descifra y valida a partir del intent AES-GCM retenido en MySQL por el adaptador existente, no del navegador. Exige desafío de 32 bytes, destinatario canonical @example.test y puerto no privilegiado. Conexión al literal 127.0.0.1, sin MX/DNS, TLS externo, credenciales productivas ni DI API. El uso de SmtpClient es exclusivamente para ensayo desechable, no un diseño productivo.
2. **Receptor en otro proceso**: Crecisol.LoopbackSmtpReceiver .NET10 escucha TCP en 127.0.0.1 y puerto efímero, ejecuta comandos SMTP limitados, acepta solo remitente no-reply@crecisol.example.test y destinatarios @example.test. No conserva mailboxes ni secretos en disco.
3. **Confirmación posterior**: test padre recibe código por tubería stdout privada del proceso receptor (nunca se imprime en CI) y lo presenta al adaptador de verificación MySQL existente. Validar primero token incorrecto, tenant cruzado, consumo único y denegación de replay. La tubería es SOLO un transporte de prueba, no una API de verificación para clientes.
4. **Negativas obligatorias**: puerto SMTP 25, destino externo, CRLF injection, tenant ajeno, challenge incorrecto, replay, fallo por receptor terminado, revocación y cero tablas/columnas de grants o PII en claro. Dos tenants en MySQL8.4 desechable y un servicio receptor de proceso independiente.
5. **Sin conexión productiva**: ninguna tabla, ruta o DI de producción modificada, ningún envío fuera de loopback. Script con usuario SQL restringido, claves aleatorias, Docker y limpieza garantizada al cerrar la ejecución.

## Alcance que NO demuestra

SMTP 250 de un receptor sintético acredita únicamente aceptación por el proceso local. No demuestra entrega a inbox real, identidad de usuario, verificación de un canal humano, invitación autorizada, pertenencia, JWT, contraseña o sesión. El código debe seguir aislado y NO HABILITAR registros productivos.

Persisten riesgos P0/P1: proveedor e inbox reales bajo ensayo autorizado; identidad y permisos del invitante en backend (tenant/rol/acción/sucursal/recurso); persistencia shared/dedicated; consentimiento legal; límites anti-enumeración temporal E2E y abuso multi-IP; rotación de claves, recuperación de outbox, retención PII y carrera suspensión/lease/envío. Nada de esto se considera resuelto por SMTP sintético.

## Tres auditorías obligatorias

- **A: funcional/trazabilidad** del Word/PDF/C05, PR #119/#120/#122, entrada cifrada y SMTP recibido por proceso separado.
- **B: ofensiva** con mutaciones de guardas más pruebas .NET10/MySQL8.4 reales de dos tenants, CRLF, puertos externos, token incorrecto/replay y fallos del canal. No equiparar mutación de texto con una prueba runtime.
- **C: arquitectura/evidencia** de diff, trazabilidad SHA/run exacto, CI, regresiones, no producción, no main y no SOLQARYN. Mantener errores históricos, corregir antes de integrar.

**Estado al redactar:** implementación propuesta; auditorías y pruebas nuevas todavía NO EJECUTADAS. Al cerrar, registrar solo resultados verificables. **C05-02 NO CERTIFICADA; C05-03 bloqueada.**

Rollback: revertir exclusivamente los archivos de esta integración tras revisar dependencias. Docker de ensayo, procesos, secretos y bases desechables se eliminan; main, Word/PDF, SOLQARYN, VPS, Cloudflare, DNS y cobros permanecen sin tocar.
