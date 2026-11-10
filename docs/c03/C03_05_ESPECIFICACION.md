# CRECISOL — C03-05 | Contratos API/DTO, validación y errores (especificación inicial)

**Estado:** CERRADA TÉCNICAMENTE tras PR #57 fusionado a dev, 3/3 CI PREMERGE SUCCESS, A/B/C y pruebas .NET10/MySQL8.4 reales; CI propia del acta pendiente; **NO CERTIFICADA**. Capítulo C03 NO CERTIFICADO; C04 BLOQUEADO. C01/C02 son los únicos capítulos certificados (2/37).

## Puerta de entrada, evidencia verificable y autoridad
- Repositorio autorizado exclusivamente `solqaryn-platform/CRECISOL`; rama base `dev` SHA `6cb6cb79792efdddeab5fef0f0596a6532ed3afc`; rama de trabajo `feature/c03-05-api-dto-contracts`.
- C03-04 formalizada mediante PR #56, squash en `dev` con el SHA anterior; **10/10 ejecuciones de workflows POSTMERGE `push/completed/success`**, incluida repetición satisfactoria del workflow C03-01. C03-04 CERTIFICADA (4/10). PR técnico #55 y acta #56 son evidencias separadas.
- Fuente: `docs/CRECISOL_DOCUMENTO_MAESTRO_UNICO.docx` (Git blob `214bf9c3d9763be23a77b7e0c464359584edde46`; SHA-256 `e45c2017e62bbbaa6853298efd5de5d785230005203d8791f791760f6f2c622e`), `docs/MASTER_REFERENCE.md`, mapa C03 y propuesta `docs/planificacion-futura/C03_MICROFASES_PROPUESTAS.md`. No se modifica el maestro. Las ocho puertas normativas MAE-C03 no son equivalentes a diez microfases técnicas.
- Alcance de fase establecido por el plan: **convenciones API y DTO**, validación de entradas tipadas y tratamiento seguro de errores, sin serializar entidades internas ni secretos.

## Contrato propuesto (NO implementado)
1. Conservar única ruta pública existente `GET /api/v1/public/branding/platform`; no introducir rutas administrativas, privadas, de tenant, publicación o escritura. No registrar `MySqlPublishedPlatformBrandRevisionReader` ni crear puentes DI a lectura interna.
2. Respuestas de API mediante tipos DTO de transporte explícitos y allowlist; mantener propiedad/semántica pública compatible con Angular y los contratos C01, sin filtrar objetos Domain/Infrastructure por serialización automática.
3. Validación estricta de método, ruta, query, host, cabeceras de contexto, formatos y tamaños relevantes, manteniendo `400` para peticiones inválidas, `404` para ausencia de publicación y `503` genérico para fallos internos. Para códigos de error adicionales, exigir esquema y pruebas; no añadir diagnósticos, conexiones, stack traces ni PII.
4. Mantener cancelación, `no-store`, `nosniff`, denegación de forwarded host y host allowlist. Evitar que el cliente escoja tenant, DSN o permisos. No confiar en valores administrables que contengan HTML/JS/SQL ejecutable.
5. Versionado explícito en `/api/v1` sin asumir que los DTO futuros, autenticación, OpenAPI privado o rutas de administración ya están listos.

## Impacto y revisión previa al código
| Área | Impacto permitido | Exclusiones |
| --- | --- | --- |
| `backend/src/Crecisol.Api/` | DTO explícito para salida pública, mapeo seguro y validación HTTP | Nada de autorización privada, DI de C03-04, DSN cliente |
| `backend/tests/` | Casos positivos y negativos de HTTP/serialización | No afirmar BOLA/IDOR/MFA privada |
| `qa/`, `.github/workflows/` | A/B/C y CI reproducible .NET10, regresión C01–C03 | Sin credenciales productivas |
| `frontend/` | Sin cambios salvo compatibilidad demostrada necesaria | Sin pantallas, login o guard privados |
| `database/` | Sin DDL, migraciones ni datos persistentes nuevos | Sin esquema tenant ni conexiones productivas |
| `docs/` | Especificación, matriz y acta solo después de evidencia | Sin modificar Word/PDF canónicos |

## Puertas de prueba obligatorias pendientes
- **A, funcional y trazabilidad:** contrastar con Maestro, esquema previo y 37 capítulos; compatibilidad con los consumidores Angular y 37 escenarios HTTP previos; pruebas de éxito/ausencia/error.
- **B, seguridad:** pruebas negativas de Host/X-Forwarded-Host/Forwarded/query/Authorization/X-Tenant-Id, falla segura de DSN y cancelación; fuzz de entradas y comprobación de que la serialización no incluye secretos ni entidades internas.
- **C, arquitectura/rendimiento/UX:** .NET 10 Release con warnings-as-errors; Domain y Application sin referencias API/Infrastructure nuevas; pruebas existentes C01–C03, integración MySQL 8.4 pública y Chromium Angular 22; comprobar tamaño de payload y comportamiento uniforme de errores.
- Validar CI PREMERGE en HEAD exacto, fusionar exclusivamente a `dev` si todo pasa y confirmar CI POSTMERGE en el nuevo SHA. **No fabricar acta de certificación sin ejecución y autorización posterior específica.**

## Riesgos, límites y reversión
- P0: un contrato DTO aparentemente público puede filtrar campos de infraestructura o cambiar el esquema consumido por Angular; usar allowlist y pruebas de regresión de contrato.
- P0: nuevos esquemas de error pueden filtrar excepciones o datos privados; fallar de forma genérica y no loggear secretos.
- P1: cambios en semántica HTTP podrían romper consumidores C01/C02; preservar compatibilidad verificable.
- La creación de este documento **no prueba ni certifica** C03-05. Revertir la rama/PR sin migraciones ni estado de producción. Sin cambios en `main`, SOLQARYN, VPS, DNS, cobros o despliegue.

## Implementación sometida a CI (no certificación anticipada)

- API: `PublishedPlatformBrandDto` y `PublishedBrandPresentationDto` son contratos de transporte explícitos, con los mismos 10/6 nombres JSON que el parser Angular vigente. Se conserva la ruta pública GET y se devuelve `Results.Json(PublishedPlatformBrandDto.FromApplication(brand))`, sin pasar entidades Domain, secretos ni revisiones privadas al serializador.
- `PublicBrandRequestValidator` rechaza método distinto de GET, query, Authorization, X-Tenant-Id, cuerpo/Content-Length, chunked transfer y encabezados de sobrescritura; se conservan también Host allowlist y rechazo de Forwarded/X-Forwarded-Host en el pipeline.
- Pruebas: `Crecisol.Api.ContractsTests` (.NET10, JSON y casos positivos/negativos), `qa/c03_05_http_smoke.py` (23 solicitudes reales baseline más ausencia, inválido, DSN no configurado), y auditorías A/B/C con 12 mutaciones rechazadas. Estas son **pruebas planificadas, no declaradas PASS** hasta ver sus runs sobre el commit exacto.
- CI: `.github/workflows/c03-05-api-dto.yml` con .NET10 Release, regresiones C01-C03, MySQL8.4 efímero y SQL SELECT-only, HTTP real, auditorías independientes. El workflow integral C03-01 se habilita para regresión en la rama C03-05.
- Impacto efectivo: se modifica exclusivamente API pública, pruebas, QA, script de prueba MySQL sin DDL y CI; sin nuevos endpoints, cambios Domain/Application/Infrastructure productivos, nuevas tablas, frontend, migraciones ni despliegue.
- **NO GO** si falta un resultado `completed/success` en PREMERGE o POSTMERGE, o si hay algún P0/P1. El acta formal se redactará exclusivamente con resultados observados y aprobación humana, no con suposiciones.

## Evidencia técnica previa a certificación documental

PR #57 fusionado solo a `dev` SHA `9d52ed9539b72611cc15bd71433ce7d7707dd13f` tras 3/3 CI PREMERGE SUCCESS sobre `3e444169b7beeacc6c891318588a582c893aa516`: C03-05 #42742553465, integral #42742553132, Foundation #42742553083. Nuevos 57 tests, 154 regresiones, 12/12 mutaciones, 27 HTTP reales, 83 Chromium, 37 HTTP históricos, MySQL8.4 local `--network none` SQL SELECT-only; .NET10 Release 0 errores/advertencias. POSTMERGE técnico del SHA integrado: #42743329418, #42743329202, #42743329425; estos IDs se verifican por separado como `push/completed/success`. [Acta C03-05](C03_05_ACTA_MICROFASE.md) y CI documental son obligatorias para certificar **5/10**. C03-06 NO INICIADA, C03 NO CERTIFICADO, C04 BLOQUEADO, sin producción ni SOLQARYN.
