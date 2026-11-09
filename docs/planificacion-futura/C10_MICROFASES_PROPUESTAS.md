# C10 · Catálogo universal y taxonomías administrables — microfases propuestas

> **BORRADOR DE PLANIFICACIÓN; NO HABILITADO PARA DESARROLLO NI CERTIFICACIÓN.** Derivado de `docs/PLAN_37_CAPITULOS.md` y reglas transversales; antes de activar el capítulo cotejar su sección completa y anexos con el Word canónico e integridad de `docs/MASTER_REFERENCE.md`, completar su matriz y ajustar por PR. No reemplaza el Documento Maestro.

**Entrada:** C09 certificado y aceptado. **Riesgo rector:** Modelo extensible de productos/servicios con scopes y validación.

## 9 microfases técnicas, una por vez

| Código | Entregable mínimo | Evidencia positiva requerida | Negativa obligatoria |
|---|---|---|---|
| C10-01 | Modelo universal de ofertas | Tipos producto, servicio y acceso trazables | No esquemas separados por rubro |
| C10-02 | Taxonomía versionada de categorías | Jerarquías y slugs únicos por tenant | No colisión cross-tenant |
| C10-03 | Atributos y variantes tipadas | Reglas de tipos, rangos y opciones testeadas | No ejecución de expresiones arbitrarias |
| C10-04 | Catálogo de precios y monedas | Precios decimales y vigencias verificables | No truncamiento ni monedas mezcladas |
| C10-05 | Disponibilidad y visibilidad pública | Campos publicados separados de borradores | No filtrar costes o notas internas |
| C10-06 | Búsqueda, filtros y paginación | Consultas indexadas y tenant-aware | No enumerar productos de otro negocio |
| C10-07 | Interfaces de gestión de catálogo | CRUD autorizado y formularios accesibles | No edición desde URL ajena |
| C10-08 | Importación y exportación local | Validación de lotes, límites y recuperación | No CSV formula injection |
| C10-09 | Auditorías integrales y acta C10 | A/B/C, regresión y aceptación | No iniciar C11 sin aislamiento probado |

## Puertas por microfase y capítulo

Cada C10-XX requiere dependencia anterior cerrada, requisitos trazados, impacto `frontend/`/`backend/`/`database/`, branch específica, tests positivos y negativos reales, auditorías **A trazabilidad / B seguridad-tenancy / C arquitectura-rendimiento-UX**, acta, revisión del diff y secretos, CI PASS del último SHA, PR a `dev`, merge seguro, CI posterior y rollback. Guardar la especificación individual **solo al activar** como `docs/c10/C10_XX_ESPECIFICACION.md`. Si una puerta no pasa, **BLOQUEADO**; no falsificar PASS ni saltar dependencias. No producción ni SOLQARYN.

Incluso con 9/9 microfases cerradas, C10 necesita **auditoría integral triple, regresión, ausencia de P0/P1, acta y aceptación expresa** antes de habilitar C11. Este plan no demuestra implementación.
