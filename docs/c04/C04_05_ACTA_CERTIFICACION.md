# CRECISOL — C04-05 · Acta institucional de contexto tenant por identidad (eficacia condicionada)

**Fecha:** 12-11-2026, Honduras. **Repositorio:** `crecisol-platform/CRECISOL` (GitHub ID `1450414592`). **Rama receptora:** `dev`. **Microfase:** C04-05 (5 de 12). **Estado al redactar:** ACTA PROPUESTA, NO EFICAZ sin CI POSTMERGE técnica y CI PRE/POSTMERGE propia verificadas y dictamen final publicado. No habilitar C04-06 antes de completar todas las puertas.

## 1. Autoridad y certificación de entrada

Documento Maestro Único `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx` SHA-256 `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c`; PDF lectura SHA-256 `0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77`. Texto original C04: 72 párrafos, SHA-256 `95be2bb0210e108317e67bd08fb9b2899d33554e684bb2d9ae3c8eced6995058`, verificador `qa/c04_01_normative_inventory.py`. Ocho puertas normativas MAE-C04 distintas de 12 microfases técnicas.

**Puerta C04-04 acreditada:** [acta institucional #91](https://github.com/crecisol-platform/CRECISOL/pull/91#issuecomment-6833642526), `dev` SHA `344998d8bcd4b006e71bd85d2ed391456e879a95`, CI documental POSTMERGE 4/4 SUCCESS; certificado SOLO validador de hosts y contexto interno NO EXPUESTO, no verificación DNS real. Capítulos completos C01/C02/C03: **3/37**. Capítulo C04 completo: NO CERTIFICADO.

**Norma C04-05:** `docs/planificacion-futura/C04_MICROFASES_PROPUESTAS.md`, “Contexto tenant por identidad”, positiva “Contexto desde membresía/host autorizado”, negativa “No usar tenant_id como permiso”.

## 2. Implementación técnica real y límites

[PR técnico #92](https://github.com/crecisol-platform/CRECISOL/pull/92), HEAD PREMERGE `4b3b82a4884e27b5ab09eb6ebc03bb6e6a3d308f`, integrado exclusivamente a `dev` SHA **`6fa782311756a12fb5f847b68bde3f7a35b3dd12`**. Diff revisado **7 archivos nuevos C04-05**, sin modificaciones a `Program.cs`, Infrastructure, frontend, database, otros capítulos ni al Word/PDF:

- `backend/src/Crecisol.Domain/Tenancy/TenantIdentityContext.cs`: actor GUID opaco; sesión tipada Active/Suspended/Revoked con revisión y timestamps UTC; membresía exacta actor/tenant Pending/Active/Suspended/Revoked y ventana; evidencia interna con revisiones de sesión/membresía/host/tenant y diagnósticos redactados. Identificadores/claims NO son credenciales ni permisos.
- `backend/src/Crecisol.Application/Tenancy/ResolveCurrentTenantIdentityContext.cs`: usa puertos de **futura** sesión backend validada, host directo seguro y catálogo de membresías; reutiliza `ResolveVerifiedTenantHost` de C04-04 y el lector `ITrustedTenantCatalogReader` de C04-01. Comprueba en secuencia sesión activa, host vinculado único/verificado, **una sola membresía exacta de ese actor y tenant**, tenant Active con modo de dirección consistente, segundo lookup de host para detectar rebind/revisión y sesión/membresía vigentes al finalizar. Sin fallback a primer tenant, GUID, Host, slug, header, claim, DSN ni SuperAdmin implícito; `ExecuteAsync` solo acepta `CancellationToken`.
- `backend/tests/Crecisol.TenantIdentity.ContractsTests/`: suite de pruebas .NET 10 con proveedores **fake explícitos**, casos A/B y negativos de revocación, ausencia/duplicados, miembro de empresa/actor ajeno, host re-asignado, expiración, error y cancelación. Nunca confundir fakes con autenticación o HTTP privada realmente comprobados.
- `qa/c04_05_identity_audits.py` y `.github/workflows/c04-05-tenant-identity.yml`: 3 auditorías con mutaciones de trust boundary y evidencia SHA/run, permisos read-only, sin despliegues. `docs/c04/C04_05_ESPECIFICACION.md`: alcance normativo y riesgos.

**Ausencias deliberadas NO certificadas:** validación criptográfica real de JWT/sesión, revocación transaccional, persistencia de membresías y exclusividad bajo carreras, login con MFA, autorización HTTP y roles/acciones/sucursal/recurso/owner, provisioning, DNS ownership y proxies/TLS, DSN/router dedicado, BOLA/IDOR de endpoints privados, p95 de infra ni producción. `TenantIdentityContextEvidence` es evidencia **no autorizante** para un futuro control C04-06, no un grant/bearer token ni autorización para acceder a datos.

## 3. Tres auditorías y pruebas reales — PREMERGE

**HEAD técnico** `4b3b82311...` [SHA exacto: `4b3b82a4884e27b5ab09eb6ebc03bb6e6a3d308f`]. **5/5 workflows aplicables SUCCESS**; tres `skipped` condicionales nunca contados como PASS.

| Gate PREMERGE | Run real | Resultado |
| --- | --- | --- |
| C04-05 A/B/C, .NET10 pruebas de identidad/tenant | [43291605011](https://github.com/crecisol-platform/CRECISOL/actions/runs/43291605011) | SUCCESS |
| C04-01 contratos | [43291604749](https://github.com/crecisol-platform/CRECISOL/actions/runs/43291604749) | SUCCESS |
| C04-02 SHARED + MySQL8.4 | [43291605166](https://github.com/crecisol-platform/CRECISOL/actions/runs/43291605166) | SUCCESS |
| C03-10 integral Angular22/Node24/.NET10/Chromium/MySQL8.4 | [43291605010](https://github.com/crecisol-platform/CRECISOL/actions/runs/43291605010) | SUCCESS |
| Foundation | [43291604904](https://github.com/crecisol-platform/CRECISOL/actions/runs/43291604904) | SUCCESS |

**A funcional y trazabilidad:** Documento Maestro canónico 72 párrafos C04, referencias SHA Word/PDF, requisitos positivos/negativos C04-05, Domain/Application separados, sin nuevas rutas privadas o conexiones.

**B seguridad/P0:** **89 mutaciones adversariales** rechazadas; **110 aserciones positivas/negativas reales .NET10**. Pruebas de un actor con membresías A y B resuelto solo por host verificado; actor de A en host B sin membresía, membresía de otro actor o tenant, lista ausente o duplicada, sesión/membresía suspendida/revocada/caducada/futura, empresa desactivada, modos de dirección discordantes, cambio de host/revisión durante consultas, sesión/membresía que expira durante operación, cancelación y fallos. El actor solo proviene de puerto backend; no hay parámetro `tenant_id`.

**C arquitectura y calidad:** builds reales `.NET10 Release -warnaserror` sin errores; pruebas y A/B/C ligadas al mismo SHA/run; regresiones de C04-01 88 aserciones, C04-02 38, C04-03 42, C04-04 128 y API C03-05 57, más MySQL8.4 y C03-10 por workflows independientes. Ningún adaptador que genere sesiones reales ni registro en `Program.cs`.

**Fallo histórico preservado:** [run inicial #43291446384](https://github.com/crecisol-platform/CRECISOL/actions/runs/43291446384) falló al comparar la frase documental `no autenticación productiva` con la equivalente realmente escrita `sin autenticación productiva`. El nuevo commit alineó el guard sin suprimir condiciones ni test de seguridad; su nueva CI [#43291605011](https://github.com/crecisol-platform/CRECISOL/actions/runs/43291605011) terminó SUCCESS. El run histórico conserva `failure`, no se cuenta como PASS.

## 4. Puerta técnica POSTMERGE — NO presumir conclusión

SHA integrado técnico `6fa782311756a12fb5f847b68bde3f7a35b3dd12`: todos los workflows aplicables DEBEN finalizar en `completed/success`; no basta con la CI del HEAD anterior. Al redactar, fueron iniciadas las siguientes ejecuciones:

| Gate POSTMERGE técnico | Run |
| --- | --- |
| C04-05 identidad y A/B/C | [43292457276](https://github.com/crecisol-platform/CRECISOL/actions/runs/43292457276) |
| C04-02 MySQL8.4 | [43292457185](https://github.com/crecisol-platform/CRECISOL/actions/runs/43292457185) |
| C04-01 contratos | [43292457055](https://github.com/crecisol-platform/CRECISOL/actions/runs/43292457055) |
| C03-02 Domain | [43292457188](https://github.com/crecisol-platform/CRECISOL/actions/runs/43292457188) |
| C03-03 Application | [43292457031](https://github.com/crecisol-platform/CRECISOL/actions/runs/43292457031) |
| C03-10 integral | [43292457161](https://github.com/crecisol-platform/CRECISOL/actions/runs/43292457161) |
| Foundation | [43292457189](https://github.com/crecisol-platform/CRECISOL/actions/runs/43292457189) |

No reetiquetar resultado o asumir que estos runs ya aprobaron. Sus conclusiones efectivas se registrarán en comentario institucional tras verificarlas en GitHub.

## 5. Riesgos diferidos y reversión

**Puertas P0 futuras EXPLICITAMENTE ABIERTAS**, no expuestas por este PR: adaptador de autenticación con validación real de sesión/claims/firma/revocación, membresía persistente y grants versionados bajo revocación concurrente, control de roles/acciones, scopes de sucursal/owner/recurso, autorización de cada API privada, verificación DNS/proxy/host real y pruebas BOLA/IDOR HTTP y SQL para cross-tenant. El host y un GUID de actor/tenant **nunca son permiso**. Sin estos gates no se habilitarán rutas privadas ni acceso a storage. Los resultados de esta CI únicamente cubren la lógica interna con dobles y no implican ausencia de riesgos en producción o endpoints no implementados.

No se identificaron P0/P1 sin resolver EN EL ALCANCE INTERNO NO CONECTADO probado; no se certifica la seguridad end-to-end. Reversión: revertir solo el squash técnico `6fa782311756a12fb5f847b68bde3f7a35b3dd12`, reejecutar regresiones; no hay datos ni esquemas productivos modificados. No alterar `main`, Word/PDF único, SOLQARYN, Cloudflare, DNS, VPS, pagos o servicios externos.

## 6. Eficacia institucional estricta

El propietario autorizó avanzar solo con cero fallos vigentes y tres auditorías. Esta acta será eficaz ÚNICAMENTE cuando: **(1)** todos los workflows POSTMERGE técnicos del SHA `6fa782...` terminen SUCCESS; **(2)** su PR documental propio apruebe CI PREMERGE sobre último HEAD; **(3)** se fusione solo en `dev` con SHA esperado; **(4)** todos los workflows documentales POSTMERGE sobre el nuevo SHA final de dev terminen SUCCESS; **(5)** un comentario permanente registre IDs, SHAs y límites de seguridad con una última verificación. Ningún test `skipped` equivale a PASS; ningún histórico `failure` se reescribe.

**Solo después:** C04-05 CERTIFICADA EN ALCANCE EXCLUSIVO DE CONTEXTO INTERNO TIPADO ACTOR-HOST-MEMBRESÍA-TENANT (5/12); C04-06 HABILITADA SOLO PARA PREPARACIÓN; capítulo C04 NO CERTIFICADO, capítulos institucionales C01-C03 3/37. **Antes:** C04-05 PENDIENTE y C04-06 BLOQUEADA. Firma operacional sustentada por evidencias GitHub; no firma criptográfica ni certificación de infraestructura productiva.
