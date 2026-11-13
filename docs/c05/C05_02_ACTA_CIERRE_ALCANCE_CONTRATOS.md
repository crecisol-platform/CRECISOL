# CRECISOL — C05-02 · Acta de ratificación por alcance verificable (condicionada)

**Fecha:** 2026-11-13. **Repositorio exclusivo:** `crecisol-platform/CRECISOL`, ID GitHub `1450414592`. **Base comprobada:** `dev` en `a26bdbd6a50ef18d1e019f9530980377317cd734` (PR #129). **Rama documental:** `feature/c05-02-certificacion-acotada-20261113`. **Estado de esta acta antes de su CI, fusión y comentario institucional:** PROPUESTA, **NO EFICAZ**.

## 1. Alcance normativo y resolución de la dependencia circular

Autoridad: Documento Maestro Único en `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx`, `docs/MASTER_REFERENCE.md`, `docs/PLAN_37_CAPITULOS.md` y `docs/c05/C05_02_ESPECIFICACION.md`. Huella Word histórica verificada: `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c`; PDF lectura: `0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77`; texto C05 extraído [CI #43364292640](https://github.com/crecisol-platform/CRECISOL/actions/runs/43364292640): `bf7036849d6f1aad36a3a111b62121947aa12fae8c2a0551e63b0cc788d4a0aa`. No modificar los maestros ni reinterpretar sus ocho pasos normativos como las once microfases del plan.

El plan técnico vigente define **C05-02 «Registro e invitaciones seguras»** con evidencia positiva **«Flujo tipado, expiración y pruebas de abuso»** y negativa **«No enumeración de correos»**. La misma planificación reserva expresamente **hash/política de credenciales (C05-03), login (C05-04), sesiones (C05-05), cambios sensibles/verificación de correo de usuario (C05-07), MFA (C05-08) y control integral de rate limit y trazas (C05-10)**.

**Hallazgo de gobernanza:** las actas técnicas individuales de C05-02 exigieron para el cierre del contrato una cuenta humana, autenticación productiva, proveedor externo y creación definitiva de membresías. Esas dependencias, para un cierre **de contratos/ensayos aislados**, son ajenas o posteriores al alcance delimitado, como ocurrió con C05-01 certificada en contratos internos y fixtures sintéticos mediante [dictamen PR #114](https://github.com/crecisol-platform/CRECISOL/pull/114#issuecomment-6844940005). Exigirlas a C05-02 bloquearía circularmente C05-03/04/05. Esta acta **no elimina** requisitos de seguridad: establece el instante y el alcance correctos de su verificación. Tampoco autoriza activar API ni un flujo de registro real.

## 2. Matriz de resultados técnicos de C05-02 ya fusionados

Cada integración siguiente tuvo PREMERGE, POSTMERGE, auditorías A/B/C y pruebas reales específicas en su ámbito. No se convierten ensayos locales en identidad humana ni se trasladan sus resultados a un SHA distinto sin regresión.

| PR técnico | Evidencia POSTMERGE verificada en el SHA de su fusión | Alcance probado |
| --- | --- | --- |
| [#115](https://github.com/crecisol-platform/CRECISOL/pull/115) | [#43372184968](https://github.com/crecisol-platform/CRECISOL/actions/runs/43372184968) | Preparación normativa y negativa T01–T20 |
| [#116](https://github.com/crecisol-platform/CRECISOL/pull/116) | [#43377586097](https://github.com/crecisol-platform/CRECISOL/actions/runs/43377586097) | Token CSPRNG/huella, estado, expiración, consumo único y contratos Domain/Application |
| [#118](https://github.com/crecisol-platform/CRECISOL/pull/118) | [#43381681826](https://github.com/crecisol-platform/CRECISOL/actions/runs/43381681826) | MySQL8.4 local, CAS/tenancy/competencia y SQL mínimo privilegio |
| [#119](https://github.com/crecisol-platform/CRECISOL/pull/119) | [#43386127775](https://github.com/crecisol-platform/CRECISOL/actions/runs/43386127775) | Kestrel HTTP real de test, recepción neutral y MySQL8.4, 40 solicitudes, AES-GCM/cuota, dos tenants |
| [#120](https://github.com/crecisol-platform/CRECISOL/pull/120) | [#43426225047](https://github.com/crecisol-platform/CRECISOL/actions/runs/43426225047) | Desafío de posesión de buzón y confirmación MySQL local de un uso |
| [#122](https://github.com/crecisol-platform/CRECISOL/pull/122) | [#43446245494](https://github.com/crecisol-platform/CRECISOL/actions/runs/43446245494) | Outbox durable cifrado y lease/reintento aislados |
| [#123](https://github.com/crecisol-platform/CRECISOL/pull/123) | [#43451338317](https://github.com/crecisol-platform/CRECISOL/actions/runs/43451338317) | Recepción SMTP independiente **solo TCP loopback de ensayo** |
| [#124](https://github.com/crecisol-platform/CRECISOL/pull/124) | [#43456758070](https://github.com/crecisol-platform/CRECISOL/actions/runs/43456758070) | Alcance del invitante, cuenta/sesión metadata sintética, permiso/sucursal/recurso en SQL atómico |
| [#125](https://github.com/crecisol-platform/CRECISOL/pull/125) | [#43461600130](https://github.com/crecisol-platform/CRECISOL/actions/runs/43461600130) | Revalidación del invitante en el punto de consumo |
| [#126](https://github.com/crecisol-platform/CRECISOL/pull/126) | [#43470734839](https://github.com/crecisol-platform/CRECISOL/actions/runs/43470734839) | Aserción HMAC sintética y revocación de sesión |
| [#127](https://github.com/crecisol-platform/CRECISOL/pull/127) | [#43477170014](https://github.com/crecisol-platform/CRECISOL/actions/runs/43477170014) | Conjunción invitación+buzón+sesión en un solo consumo |
| [#128](https://github.com/crecisol-platform/CRECISOL/pull/128) | [#43481465989](https://github.com/crecisol-platform/CRECISOL/actions/runs/43481465989) | Verificación criptográfica en backend antes del CAS MySQL; 75 aserciones y 16 competidores |
| [#129](https://github.com/crecisol-platform/CRECISOL/pull/129) | [#43485806970](https://github.com/crecisol-platform/CRECISOL/actions/runs/43485806970) | Disposición lógica de intentos de registro caducados, 69 mutaciones, 102 aserciones .NET10, 2 tenants y cuotas; [C03 integral #43485807006](https://github.com/crecisol-platform/CRECISOL/actions/runs/43485807006) SUCCESS |

Estos resultados fueron comprobados mediante consulta directa a GitHub Actions (cada ejecución POSTMERGE referida tiene `event=push` y `conclusion=success`). Fallos históricos anteriores permanecen en GitHub y no se cuentan como PASS. La presente ratificación requiere **nueva regresión conjunta** en un SHA actual antes de ser eficaz.

## 3. Matriz de aceptación de contratos — positiva y adversaria

| Código | Prueba exigible en C05-02 hoy | Evidencia | Límite |
| --- | --- | --- | --- |
| B01/T01 | HTTP de ensayo responde 202/código neutro a direcciones sintéticas conocidas y desconocidas; sin directorio real | #119, regresión conjunta | No acredita tiempos con cuentas reales |
| B02–B05/T02–T05 | Aleatoriedad, token opaco/huella, TTL, revocación, concurrencia 16 consumidores un ganador | #116, #118, #128 | No concede sesión |
| B06–B08/T06–T08 | Contexto servidor, alcance sucursal/recurso, prohibición de Owner/SuperAdmin | #119, #124, #125 | La sesión del fixture NO es login criptográfico |
| B09–B11/T09–T11 | Destinatario/contexto exacto, prueba independiente de buzón local, conjunción y unicidad MySQL | #120, #123, #127, #128 | No acredita control de bandeja externa real |
| B12/T12–T14 | Revocación/estado de tenant, cambios de invitante, versión de términos y consentimiento | #125, #127 | Consentimiento administrativo productivo pendiente |
| B13–B16/T15–T19 | Sin JWT/cookie/membresía, logs redactados, outbox/entrega/reintento, cuota parcial, privacidad | #119, #122, #123, #129 | Rate limit distribuido y retención total productiva posteriores |
| B17–B18/T20 | Sin adelantar C05-03..05 ni C06; no usar CI documental como prueba de login real | Este expediente y auditoría C | Solo permite preparar C05-03 tras certificar alcance explícito |

## 4. Auditorías A/B/C de ratificación y puerta de eficacia

- **A normativa/funcional:** maestro verificado, requisito de C05-02 contra plan, 13 PR anteriores y registros GitHub, alcance, cuenta/membresía/credencial ajenas a la activación real. Prueba .NET10 de invitaciones y HTTP neutro de ensayo.
- **B seguridad:** token y canal vinculados a tenant, HMAC en backend, revalidación de permisos, expiración y carrera SQL, integridad de fila, no enumeración del HTTP de test, 2 tenants, límites de PII, mutaciones negativas del auditor de esta acta y prueba MySQL8.4 real.
- **C arquitectura/regresión:** sin API privada/DI/login, `Domain`/`Application`/`Infrastructure` separados, SHA y RUN ID iguales para A/B, .NET10, neutral HTTP/MySQL y mailbox bound/MySQL y retención, diff documental auditado, Foundation y C03 integral sobre HEAD final y fusión, sin despliegue.

**Condiciones para CERTIFICAR C05-02 en ámbito acotado:** (1) PREMERGE del acta y regresión A/B/C completas, (2) cero fallos nuevos/P0-P1 en **contratos/adaptadores descartables**, (3) fusión exclusivamente a `dev` usando HEAD fijo, (4) POSTMERGE en SHA definitivo con CI aplicable `completed/success`, (5) comentario institucional permanente con ejecuciones y SHA exactos. `skipped` NO cuenta como `success`. Antes de esos cinco pasos el estado es **NO CERTIFICADA**.

Solo una vez cumplidos: **C05-02 CERTIFICADA EXCLUSIVAMENTE EN CONTRATOS INTERNOS Y ENSAYOS LOCALES SINTÉTICOS (2/11 microfases de C05)** y **C05-03 habilitada únicamente para preparación/desarrollo y pruebas de su propio alcance**; **C05 como capítulo NO CERTIFICADO y C06 BLOQUEADO**. La certificación funcional/productiva extremo a extremo permanece NO EMITIDA y exige pruebas propias cuando las identidades, credenciales, sesiones, proveedor externo y membership sean reales, incluida auditoría integral C05-11.

## 5. Riesgos pendientes *fuera* del ámbito y cierre técnico

**Se mantienen expresamente NO PROBADOS / NO AUTORIZADOS en producción:** autenticación verificable de personas y operadores, origen de credenciales, JWT/cookies, MFA/step-up, entrega a buzón externo, identidad shared/dedicated persistente, grants de membresía, integración API/Angular real y retención física/backups, antienumeración temporal con directorio real y límites distribuidos. Son P0/P1 para una **activación real**, no deben quedar invisibles ni considerarse mitigados. Ninguna entrada de ensayo se conecta a rutas públicas productivas.

**Rollback:** revertir solo el squash futuro de esta ratificación documental en `dev`, previa revisión de dependencias, y restituir estado anterior. Nunca tocar `main`, Documento Maestro Word/PDF, SOLQARYN, VPS/DNS, Cloudflare, producción ni cobros.
