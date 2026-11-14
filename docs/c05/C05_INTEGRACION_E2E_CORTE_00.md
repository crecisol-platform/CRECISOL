# CRECISOL — C05 integración de identidad: corte 00, composición protegida

**Fecha:** 2026-11-14. **Expediente:** #150. **Base exacta:** `dev` `8b9e1d5919dda192990c34fb096b235cb94bfec8`. **Estado:** análisis verificable y preparación; implementación E2E **NO EJECUTADA**; C06 **BLOQUEADO**.

## Fuentes y alcance

Documento Maestro Único y `docs/MASTER_REFERENCE.md`; `docs/PLAN_37_CAPITULOS.md`; `docs/ESTADO_VIGENTE_CAPITULOS.md`; `docs/c05/C05_11_CERTIFICACION_INSTITUCIONAL.md`; `docs/c05/C05_11_MATRIZ_RIESGOS.md`; `AGENTS.md`; `.agents/skills/crecisol-project-governance/SKILL.md`. El cierre #149 demuestra coherencia de pruebas **sintéticas**, no autenticación privada integrada ni identidades utilizables por clientes. No activar funcionalidades C06 ni producción.

## Inventario observado por lectura del árbol de `dev`

- **Raíz API:** `backend/src/Crecisol.Api/Program.cs` registra solamente `GET /api/v1/public/branding/platform`. Tiene allowlist explícita de Host, rechazo de `Forwarded`/`X-Forwarded-Host`, cabeceras `no-store` y excepción neutral. **No** contiene composition root de autenticación privada, middleware de identidad tenant ni emisor de cookies/tokens de clientes. Conservar fail-closed.
- **Dominio:** `backend/src/Crecisol.Domain/Identity/` contiene modelos de cuenta, invitación, prueba de buzón, perfiles privados y limitación previa a autenticación.
- **Aplicación:** `backend/src/Crecisol.Application/Identity/` contiene registro neutro, invitaciones, verificación de buzón, login contextual preparatorio, sesiones, recuperación, cambio sensible de buzón, riesgo MFA, perfiles y limitación de intentos.
- **Criptografía:** `backend/src/Crecisol.CredentialSecurity/` contiene verificador candidato Argon2id, protector de token opaco y verificador WebAuthn ES256 sintético.
- **Infraestructura:** `backend/src/Crecisol.Infrastructure/Identity/` contiene adaptadores MySQL **Disposable**, cifrado y salidas SMTP **LoopbackOnly**. Su nomenclatura y pruebas no constituyen integración productiva.
- **Pruebas existentes:** se observan proyectos `Crecisol.NeutralRegistration.IntegrationTests`, `Crecisol.ContextualLogin.ContractsTests`, `Crecisol.SessionRevocation.MySqlTests`, `Crecisol.PasswordRecovery.MySqlTests`, `Crecisol.MfaRisk.MySqlTests`, `Crecisol.PrivateProfiles.MySqlTests`, `Crecisol.RateLimitAccess.MySqlTests` y pruebas ofensivas C04.
- **Dependencia histórica abierta:** PR #121 sigue en borrador; no atribuirle entrega de correo real.

## Corte 01 — primer incremento de código permitido, con feature flag de compilación / wiring apagado

**Objetivo:** introducir un composition root de identidad **solo para host loopback y MySQL descartable**, independiente del módulo público, manteniendo por defecto **cero endpoints privados registrados**. Escribir adaptadores finos y validar de extremo a extremo **antes** de permitir cualquier `MapPost` de identidad en `Crecisol.Api`.

1. **Contract-first:** inventariar firmas exactas de cada puerto Identity y TenantContext; definir modelo de respuesta neutra de registro/verificación, comandos, errores sin enumeración, transacción de un uso y estrategia de identidad/correo. Nunca derivar tenant/DSN/roles de Host sin verificación, claims o parámetro del solicitante.
2. **Composición aislada:** conectar servidor de ensayo únicamente en `127.0.0.1`, rechazando cualquier host/proxy no confiable, y sin rutas registradas por defecto en `Program.cs`. Prohibir payloads y secretos en logs; claves efímeras solo durante pruebas.
3. **Ruta completa de registro:** neutralidad observable, outbox cifrado/restringido, prueba de posesión del buzón, desafío de un uso con expiración y consumo atómico, cuenta/realm y membresía bajo políticas independientes. No emitir sesión ni grant desde invitación sola.
4. **Credencial y acceso:** política Argon2id PHC + dummy, comparación resistente, pertenencia fresca y host verificado, MFA obligatorio conforme a riesgo; rate limit previo al hash; respuesta indistinguible cuando denegada.
5. **Sesiones y recuperación:** tokens opacos, rotación/revocación de un uso mediante CAS, recovery con MFA/step-up coherente, cambio de correo de doble confirmación y revocación contextual.
6. **Permiso:** deny-by-default para identidad, tenant verificado, membresía, rama/sucursal, rol+acción y ownership; SuperAdmin excepcional con step-up, motivo, tiempo y auditoría. No crear aún funcionalidades privadas de C06.
7. **Persistencia:** esquema/migraciones reversibles en `database/`, tenant FK e índices, permisos SQL mínimos, fallos cerrados y limpieza comprobada. La selección de DB dedicada solo puede resolverse desde catálogo protegido backend.

## Puertas de prueba, obligatorias antes de integrar código

**Auditoría A — funcional/trazabilidad:** flujo registro→posesión buzón→credencial→login→MFA→sesión→revocación/recovery en un mismo proceso de ensayo; demostrar que ninguna invitación crea membresía o sesión por sí sola. Documentar correspondencia de requerimiento del Maestro, puertos, endpoints y test.

**Auditoría B — seguridad multitenant P0:** negativas de host y XFF falsos, mezcla de realm y tenant A/B, IDOR/BOLA, sesiones y token replay concurrentes, verificación y recovery caducados, MFA replay, timing/antienumeración, CSRF en caso de cookies, acceso de sucursales, auditoría sin PII/contraseñas/tokens, SQL mínimo privilegio, fallos de DB y KMS fail-closed. Probar pertenencia revocada entre login y acción.

**Auditoría C — arquitectura/compatibilidad:** .NET 10 Release `-warnaserror`, MySQL 8.4 desechable, Node 24/Angular 22/Chromium cuando proceda, responsabilidad Domain/Application/Infrastructure/API, migraciones + rollback, carga y concurrencia, ausencia de sockets externos, accesibilidad del flujo Angular en una fase posterior y trazas por SHA.

**Evidencia actual:** solo inspección documental y de código de GitHub. **NO** se ejecutaron compilaciones ni pruebas C05-E2E de este corte; las tres auditorías de esta integración siguen **PENDIENTES**, no PASS. Los CI de #148/#149 pertenecen exclusivamente al núcleo sintético anterior.

## Límites de fusión y reversión

La presente especificación puede revisarse en PR borrador. **NO FUSIONAR** una implementación privada hasta que A/B/C, pruebas reales completas y 0 P0/P1 estén documentadas sobre el mismo SHA. No exponer el producto a usuarios reales ni afirmar que un servidor loopback cumple KMS, TLS, SMTP externo, WAF, Cloudflare, privacidad legal o COM-T08/09/10. Rollback de cada incremento: revertir solo su commit de feature o squash sobre `dev` tras analizar dependencias, sin tocar `main` ni ningún recurso de SOLQARYN.

## Avance de código del corte 00 (rama feature, sin fusión)

Se añadió `backend/src/Crecisol.Api/Contracts/NeutralRegistrationTransportPreflight.cs` como **preflight de transporte exclusivamente** para un futuro registro neutral. Rechaza host ausente/ambiguo, campos Forwarded/X-Forwarded-*, método distinto de POST, query, tipo no JSON, cuerpo sin tamaño positivo o mayor de 4096 bytes, autorización/cookies, identificadores tenant/actor/rol, transfer encoding y métodos reescritos. Reutiliza la validación `StrictVerifiedHostTransport.TryReadDirectHost`; **no** verifica pertenencia a tenant, SMTP, sesión, MFA ni autorización.

Se ampliaron las aserciones ejecutables de `backend/tests/Crecisol.Api.ContractsTests/Program.cs` con 23 casos positivos/negativos de este preflight. **Estas aserciones no equivalen a pruebas E2E** y su resultado todavía requiere ejecución en CI sobre el SHA final de la rama. Ningún endpoint se registra en `Program.cs`; no se activa transporte de producción ni se hacen migraciones. La exposición de identidad sigue en NO-GO.

**Auditorías A/B/C de integración:** PENDIENTES. **Pruebas C05 de este corte:** implementadas, NO DECLARADAS PASS hasta verificar salida real de .NET 10. Los resultados previos de #149 no certifican este código. El PR debe seguir en borrador y sin fusión hasta cumplir los gates aplicables.
