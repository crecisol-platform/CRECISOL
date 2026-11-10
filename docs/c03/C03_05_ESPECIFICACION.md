# CRECISOL — C03-05 | Contratos API/DTO, validación y errores (especificación inicial)

**Estado:** INICIADA — especificación y análisis de impacto; NO implementada, NO probada, NO certificada. Capítulo C03 NO CERTIFICADO; C04 BLOQUEADO. C01/C02 son los únicos capítulos certificados (2/37).

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
