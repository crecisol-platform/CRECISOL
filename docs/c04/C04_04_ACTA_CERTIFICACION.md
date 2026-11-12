# CRECISOL — C04-04 · Acta institucional: resolución de hosts verificados (eficacia condicionada)

**Fecha:** 12-11-2026, Honduras. **Repositorio autorizado:** `crecisol-platform/CRECISOL`, GitHub ID original `1450414592`. **Rama receptora:** `dev`. **Microfase:** C04-04 (4/12). **Estado de este documento al emitirlo:** ACTA PROPUESTA; NO CERTIFICA HASTA COMPLETAR CI POSTMERGE TÉCNICA, CI DEL ACTA Y PUBLICAR EVIDENCIAS EXACTAS.

## 1. Autoridad única y secuencia

El Documento Maestro Único continúa siendo `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx` (SHA-256 `ea5afc89e74915a9b18b9753ddb832f4be8e5d00f5e66b0f008e02e03476444c`); PDF de lectura (SHA-256 `0dfd3217439760540390707ea8196d6252464a501a7fe3e6eafdc49eda39da77`). Texto C04 de 72 párrafos, SHA-256 `95be2bb0210e108317e67bd08fb9b2899d33554e684bb2d9ae3c8eced6995058` verificado con `qa/c04_01_normative_inventory.py`. Las ocho puertas `MAE-C04` del maestro no sustituyen las doce microfases técnicas.

**C04-03 entrada certificada exclusivamente en contratos internos SHARED/DEDICATED:** [acta #89](https://github.com/crecisol-platform/CRECISOL/pull/89#issuecomment-6831740515), `dev` SHA `537e520222bc54ef6ab512768a078154c543f679`, 4/4 CI documental final SUCCESS. Capítulos institucionales completos C01/C02/C03 = **3/37**; C04 no certificado. Norma específica C04-04: `Resolución de hosts verificados`, prueba positiva `Allowlist y ciclo de validación de dominio`, negativa `No confiar en Forwarded no verificado` (plan original).

## 2. Código realmente integrado: no es autorización privada

[PR técnico #90](https://github.com/crecisol-platform/CRECISOL/pull/90), HEAD técnico PREMERGE `0f3df507e59b80bbf4a67896a49642a1214e05e5`, fusionado exclusivamente en `dev` SHA `c3fd9bfabe0bbcd71022176876d131dad47c8d52`. Diff de **8 archivos** dentro de C04-04, sin alteraciones a `Program.cs`, base productiva ni frontend:

- **Domain** `backend/src/Crecisol.Domain/Tenancy/VerifiedTenantHost.cs`: normalización estricta ASCII de DNS, sin puerto, URL, IP, wildcard, Unicode U-label ni segmentos ambiguos; vínculo inmutable tenant/host con modo Subdomain/CustomDomain, estado PendingVerification/Verified/Suspended/Revoked, revisión positiva y ventana UTC de verificación y caducidad. `PathSlug` no se resuelve por host.
- **Application** `backend/src/Crecisol.Application/Tenancy/ResolveVerifiedTenantHost.cs`: consulta exacta al catálogo protegido hipotético, exige **una única asociación**, coincidencia de host, estado Verified y ventana vigente con reloj backend. Devuelve **solo pista interna de contexto**; no usuario autorizado, no sesión, no base de datos, no DSN, sin fallback a otro tenant.
- **API — validador NO conectado** `backend/src/Crecisol.Api/Configuration/StrictVerifiedHostTransport.cs`: valida solo cabecera directa Host y rechaza `Forwarded`, `X-Forwarded-*`, reescrituras y duplicados. `Program.cs` y su allowlist previa quedan intactos; el validador no registra middleware, ruta ni DI de tenant.
- **Pruebas** `backend/tests/Crecisol.VerifiedHosts.ContractsTests/`: .NET10 real y `DefaultHttpContext` de ASP.NET, con catálogo y reloj simulados explícitamente. **No hay DNS público ni confirmación de propiedad de dominio real**.
- **QA y CI**: `qa/c04_04_hosts_audits.py`, `.github/workflows/c04-04-verified-hosts.yml`, y `docs/c04/C04_04_ESPECIFICACION.md`, con auditorías A/B/C y evidencias SHA/run. No se añadieron secretos, providers, paquetes ni entornos.

## 3. Auditorías A/B/C y PREMERGE realmente verificados

**SHA técnico PREMERGE:** `0f3df507e59b80bbf4a67896a49642a1214e05e5`. **6/6 workflows aplicables SUCCESS**, 5 checks condicionales `skipped` NO contabilizados:

| Control | Run real | Dictamen |
| --- | --- | --- |
| C04-04 A/B/C y .NET10 host attacks | [43287373915](https://github.com/crecisol-platform/CRECISOL/actions/runs/43287373915) | SUCCESS |
| C04-01 contratos tenant | [43287373382](https://github.com/crecisol-platform/CRECISOL/actions/runs/43287373382) | SUCCESS |
| C04-02 SHARED MySQL8.4 | [43287373646](https://github.com/crecisol-platform/CRECISOL/actions/runs/43287373646) | SUCCESS |
| C03-08 configuración tipada | [43287373515](https://github.com/crecisol-platform/CRECISOL/actions/runs/43287373515) | SUCCESS |
| C03-10 integral Angular/.NET/Chromium/MySQL | [43287373593](https://github.com/crecisol-platform/CRECISOL/actions/runs/43287373593) | SUCCESS |
| Foundation | [43287374088](https://github.com/crecisol-platform/CRECISOL/actions/runs/43287374088) | SUCCESS |

**Auditoría A / trazabilidad:** Word/PDF, 72 párrafos C04, plan original, CanonicalTenantHost, estados de verificación, host exacto y separación Domain/Application/HTTP.  
**Auditoría B / seguridad:** **65 mutaciones de guard rechazadas**; pruebas reales .NET10 **128 aserciones positivas/negativas** de canonicalización, puertos/URLs/Unicode/IPv4/IPv6, Host duplicado, `Forwarded` y `X-Forwarded-*`, registros A/B duplicados, host desviado, estados Pending/Suspended/Revoked, expiración, publicación futura, UTC no válida, fallos y cancelación. Cero autorización por Host; el catálogo real y la membresía siguen pendientes.  
**Auditoría C / arquitectura:** build `.NET10 Release -warnaserror` sin errores ni warnings, A/B/.NET en mismo SHA/run, puertos limpios, el validador API aislado sin alterar `Program.cs`; regresiones .NET C04-01, C04-02, C04-03 y C03-05 también superadas en el workflow; regresiones externas C03-10/C04-02 en CI propia.

**Hallazgo histórico preservado:** [run fallido inicial 43287107366](https://github.com/crecisol-platform/CRECISOL/actions/runs/43287107366) señaló que el test esperaba permitir un dominio *antes* de que el catálogo publicara el estado Verified. El código rechazó correctamente. Se ajustó la prueba sin debilitar seguridad y se añadió rechazo explícito previo a publicación. El nuevo SHA aprobó todas las puertas. El run histórico conserva su conclusión `failure` y NO cuenta en el balance del último SHA.

## 4. Puerta POSTMERGE técnica — NO presumir conclusión

SHA integrado `c3fd9bfabe0bbcd71022176876d131dad47c8d52`. Los workflows de código relevantes incluyen C04-04, C04-02, C04-01, C03-02/03/05/06/08/10 y Foundation según sus filtros de ruta. Deben verificarse **TODOS los aplicables** en `completed/success` de ese SHA exacto. Sus run IDs definitivos serán publicados en el comentario institucional del PR del acta. No afirmar `skipped` como SUCCESS ni inferir verde a partir de la CI PREMERGE.

## 5. P0/P1 no autorizados y reversión

Esta entrega **NO verifica DNS real**, propiedad de dominio, desafío TXT/CNAME/HTTP, SNI/TLS, política de proxy de IP confiable, coexistencia global de dominios bajo unicidad transaccional, renovación de lease, caché de host, callback de suspensión ni host takeover. Sin adaptador real de catálogo, no existe enrutamiento efectivo de tenants; el validador ASP.NET no está conectado a la API.

El host verificado por sí solo **no demuestra pertenencia ni permiso de un usuario**. C04-05/06 deben implementar identidad, membresía, autorización por rol/acción, branch/owner y validación del contexto contra el host, y C04-07/11 deben probar validación DNS/provisión y ataques reales cross-tenant. Sin estas condiciones P0, está prohibida la exposición de rutas privadas. Dentro del alcance C04-04 **interno NO EXPUESTO**, no se identifican P0/P1 activos luego de las pruebas; los riesgos futuros se mantienen bloqueados.

Reversión: revertir exclusivamente el squash `c3fd9bfabe0bbcd71022176876d131dad47c8d52` en `dev` si surgiera incidencia, con CI de regresión. No existen datos de producción que revertir. `main`, maestro Word/PDF, SOLQARYN, VPS, Cloudflare, DNS, pagos, frontend y bases productivas no se modificaron.

## 6. Condiciones de eficacia institucional

La presente acta será eficaz solamente tras: (a) **todos los workflows técnicos POSTMERGE aplicables SUCCESS** del SHA `c3fd9b...`; (b) CI PREMERGE propia del PR documental en último HEAD, sin fallos; (c) fusión exclusiva a `dev`; (d) CI POSTMERGE del SHA documental completo con todos los controles aplicables en SUCCESS; (e) comentario institucional permanente que registre SHAs, IDs y 0 P0/P1 del alcance no expuesto.

**Solo entonces:** `C04-04 CERTIFICADA EN SU ALCANCE INTERNO DE CONTRATOS DE HOST VERIFICADO Y DEFENSA FORWARDED (4/12)`; `C04-05 HABILITADA SOLO PARA PREPARACIÓN`; capítulo `C04 NO CERTIFICADO` y capítulos completos certificados `3/37`. No confundir este dictamen con verificación de propiedad DNS real o sistema de autenticación terminado.

Mientras falte una puerta: **C04-04 PENDIENTE, C04-05 BLOQUEADA**. Un documento no sustituye pruebas ejecutadas ni una CI previa equivale a CI posterior. Firma operacional basada en evidencias de GitHub; no firma criptográfica ni certificación de producción.
