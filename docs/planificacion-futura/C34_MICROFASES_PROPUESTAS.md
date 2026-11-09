# C34 · Formularios completos y tablas de gestión — propuestas de microfases

**BORRADOR DE PLANIFICACIÓN, NO IMPLEMENTADO NI CERTIFICADO.** Deriva de títulos de `docs/PLAN_37_CAPITULOS.md` y políticas transversales CRECISOL. Antes de activar el capítulo, cotejar el texto completo del Documento Maestro Único identificado por `docs/MASTER_REFERENCE.md` y anexos, extraer requisitos por ID y revisar el plan por PR. Prohibido tratar este archivo como maestro alternativo.

**Entrada:** C33 certificado, acta y aceptación expresa. **Riesgo rector:** UX CRUD basada en contratos seguros para todos los módulos.

## 9 microfases propuestas

| Microfase | Entregable | Evidencia positiva exigida | Test negativo / NO GO |
|---|---|---|---|
| C34-01 | Inventario de formularios por rol | Cobertura de campos obligatorios y permisos | No UI que oculte permiso sin backend |
| C34-02 | Componentes reutilizables de validación | Schemas tipados y mensajes accesibles | No código libre desde campos |
| C34-03 | Formularios con borrador/dirty state | Deshacer, confirmación y recuperación | No pérdida silenciosa de datos |
| C34-04 | Tablas con orden y paginación | Filtros y paginación servidor tenant-aware | No enumeración de recursos ajenos |
| C34-05 | Acciones masivas autorizadas | Selección, revisión y ejecución idempotente | No cruzar sucursales |
| C34-06 | Modales, errores y confirmaciones | Teclado, aria y focus management | No cierre sin decisión explícita |
| C34-07 | Previews y adjuntos en formularios | Referencias firmadas y validación de tipo | No insertar HTML activo |
| C34-08 | Responsive y pruebas de navegadores | Casos Chromium/teclado/axe | No funcionalidad exclusiva desktop |
| C34-09 | Auditorías integrales y acta C34 | A/B/C, regresión y aceptación | No iniciar C35 con UX/seguridad P0/P1 |

## Puerta de microfase y capítulo

Implementar una microfase por vez, con especificación individual creada **solo al habilitarla** en `docs/c34/C34_XX_ESPECIFICACION.md`. Antes de escribir: repositorio, dev, skill, maestro, matriz, dependencias y riesgos. Evidencia de compilación real, pruebas positivas y negativas, auditorías A (requisitos), B (seguridad/tenancy), C (arquitectura/performance/UX), PR, diff, secreto y SHA, CI final y CI posterior, acta, reversión y cero P0/P1. Una falla o prueba requerida no ejecutada bloquea el avance. No VPS, pagos reales ni modificaciones SOLQARYN.

Aunque se cierren 9/9, la certificación integral C34 requiere 3 auditorías de capítulo y aceptación expresa antes de habilitar C35. Los bloqueos de integración de terceros se conservan hasta autorización futura y pruebas reales, sin fingir certificación. Estado: PENDIENTE.
