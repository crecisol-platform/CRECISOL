# CRECISOL — C03-06 | Módulo vertical Branding e inyección explícita

**Estado:** IMPLEMENTACIÓN TÉCNICA INICIADA, todavía **NO PROBADA NI CERTIFICADA**; certificación previa **C03-05 CERTIFICADA (5/10)**, C03-06 en desarrollo sobre rama de funcionalidad, C03-07 NO INICIADA, **C03 NO CERTIFICADO; C04 BLOQUEADO**.

## Autoridad / evidencia antes de escribir

- Repositorio **exclusivo** `solqaryn-platform/CRECISOL`, base `dev` `d8f1247c537fbb313be2026eeb8ca321e10be714` confirmada, rama `feature/c03-06-branding-module-di`. Documento Maestro Único Word SHA-256 `e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e`, Git blob `214bf9c3d9763be23a77b7e0c464359584edde46`; PDF misma lectura Git blob `455847ae0cc3bf02b9b9c4c3435476d093918ea2`. Sin tocar ambos.
- Plan secuencial: `docs/planificacion-futura/C03_MICROFASES_PROPUESTAS.md`: C03-06 **módulos verticales y DI**, registro explícito probado sin Service Locator ni dependencias fantasma. Texto canónico C03 P01–P07, T01-R01–R09: 3.1 árbol modular, 3.2 dependencias y límites, T01-R03 Application, T01-R04 Infrastructure, T01-R05 API. Las ocho puertas normativas `MAE-C03-01..08` no equivalen a diez microfases técnicas.
- C03-05 certificada por [PR #58](https://github.com/solqaryn-platform/CRECISOL/pull/58) y 11/11 CI POSTMERGE; remediación Codex [PR #59](https://github.com/solqaryn-platform/CRECISOL/pull/59) y 19/19 POSTMERGE sobre `d8f1247c537fbb313be2026eeb8ca321e10be714`. No abrir C03-07/C04, ni atribuir cobertura privada inexistente.

## Decisión técnica acotada

1. Crear **un único módulo vertical real** `PublicBrandingModule` bajo `backend/src/Crecisol.Api/Modules/Branding/`, correspondiente al corte público PLATFORM efectivamente implementado. Los futuros módulos Identity, Tenants, Catalog, Orders, Payments, Scheduling, Memberships, Media, Analytics y Cases permanecen sólo planeados. No crear implementaciones fantasma, registros vacíos, escaneo/reflexión de assemblies ni dependencias circulares.
2. `Program.cs` obtiene `CRECISOL_C01_READONLY_DSN` **solo de entorno servidor** y llama `builder.Services.AddPublicBrandingModule(serverDsn)`. Contratos registrados de forma explícita: `IPublishedPlatformBrandReader` **singleton** mediante `MySqlPublishedPlatformBrandReader`, y `ReadPublishedPlatformBrand` **scoped**; falla en duplicados en lugar de aceptar overrides. Sin `IServiceProvider`, `GetService`, `ActivatorUtilities`, factory con resolutor dinámico o Service Locator **en código de producción**.
3. No introducir la lectura interna C03-04 `MySqlPublishedPlatformBrandRevisionReader`, `IPublishedPlatformBrandRevisionReader` ni `AssessPlatformBrandPublicationProposal` en la DI pública; no añadir ni una ruta. Mantener GET `/api/v1/public/branding/platform`, diez campos del DTO, no-store/nosniff, Host allowlist, forwarded-host rechazado, errores públicos 200/400/404/503.
4. `backend/tests/Crecisol.Api.ModuleTests/` con pruebas ejecutables .NET 10: lifetimes, scope, sustitución, doble registro, ausencia de servicios privados, null DSN / fail-closed. Probar CI real MySQL8.4 efímero SELECT-only, C03-05 JSON/HTTP y regresiones C01–C03/Chromium.

## Análisis de impacto/riesgo y exclusiones

| Componente | Cambio autorizado | Prohibición |
| --- | --- | --- |
| API | Nueva extensión explícita y una línea en composición root | Ningún endpoint privado, claims, DSN desde HTTP, registro de revisión interna |
| Application/Domain | Ningún cambio de fuente ni de grafo | Nada de Service Locator, ORM, acceso a Infrastructure o HTTP |
| Infrastructure | Reusar adaptador existente sin editarlo | Ningún permiso adicional, mutación/DDL ni nueva fuente de DSN |
| Frontend Angular22 | Sin cambios productivos | No afirmar panel administrativo ni autenticación privada |
| Database MySQL8.4 | Solo pruebas efímeras existentes | Sin migraciones/tablas/datos/producción |
| QA/CI/Docs | Auditorías A/B/C, pruebas negativas de DI, regresiones y acta tras pruebas | Sin simular pruebas C o CI POSTMERGE |

**Riesgos:** P0 si otra DI permite acceso a puertos privados, si se altera la única ruta/DTO o si inputs del navegador eligen origen de datos; P1 si duplicado permite override o se cambia el lifetime causando conexiones compartidas ilegales. Prevenir con allowlist, pruebas negativas, revisión diff y runners reales.

## Puertas obligatorias (NO certificados anticipados)

- **A:** Word SHA original, matriz modular, previas C01/C02/C03-01..05, fuente/camino, compatibilidad API y no falsificación de módulos futuros.
- **B:** mutaciones adversariales que prueben Service Locator, reflection, read port override, módulo duplicado, reader de revisión interno y HTTP privado; pruebas .NET con scopes y denegación de DSN.
- **C:** compilar .NET 10 Release warnings-as-errors, casos .NET nuevos y regresiones, MySQL8.4 real aislado, prueba HTTP/DTO real y Angular22/Chromium; comprobar 4 referencias ProjectReference exactas, arquitectura acíclica y ninguna ruta extra.
- CI PREMERGE/POSTMERGE **sobre SHA exacto**. PR de acta independiente sólo después de cerrar técnica y verificar ambos conjuntos de CI. No certificar C03 ni abrir C04. El propósito es un monolito modular explícito, no microservicios.

## Rollback

Revert por PR a `dev` de registro API + suite DI y artefactos QA/CI; sin estado productivo, migrations, servicios o secretos. `main` y `solqaryn/Solqaryn` inmutables.
