# C33 · Diccionario relacional MySQL normalizado — propuestas de microfases

**BORRADOR DE PLANIFICACIÓN, NO IMPLEMENTADO NI CERTIFICADO.** Deriva de títulos de `docs/PLAN_37_CAPITULOS.md` y políticas transversales CRECISOL. Antes de activar el capítulo, cotejar el texto completo del Documento Maestro Único identificado por `docs/MASTER_REFERENCE.md` y anexos, extraer requisitos por ID y revisar el plan por PR. Prohibido tratar este archivo como maestro alternativo.

**Entrada:** C32 certificado, acta y aceptación expresa. **Riesgo rector:** Esquema completo MySQL 8.4 LTS con tenants y migraciones seguras.

## 11 microfases propuestas

| Microfase | Entregable | Evidencia positiva exigida | Test negativo / NO GO |
|---|---|---|---|
| C33-01 | Inventario de entidades y ownership | Diccionario con actor/scope/retención | No tablas privadas sin tenant |
| C33-02 | Tipos, precisión y campos temporales | UTC y DECIMAL probados | No float para importes |
| C33-03 | Claves primarias y compuestas | FK tenant-aware e integridad real | No FK cruzadas |
| C33-04 | Índices y unicidad por ámbito | EXPLAIN, índices por tenant/sucursal | No UNIQUE global accidental |
| C33-05 | Relaciones de expediente e historial | Eventos append-only y trazabilidad | No UPDATE de decisiones históricas |
| C33-06 | Tablas de identidad y permisos | Roles, pertenencia, sesiones, MFA | No compartir credenciales entre tenants |
| C33-07 | Tablas de productos, pedidos y recursos | Normalización y constraints | No huérfanos |
| C33-08 | Almacenamiento shared y dedicated | Ensayo de topologías y router server | No DSN elegida por cliente |
| C33-09 | Migraciones y rollback | Upgrade/downgrade en DB desechables | No migración destructiva irreversible |
| C33-10 | Carga y planes de consulta | Benchmarks de integridad y performance | No full scans críticos no justificados |
| C33-11 | Auditorías integrales y acta C33 | A/B/C y aceptación de esquema | No habilitar C34 con datos inconsistentes |

## Puerta de microfase y capítulo

Implementar una microfase por vez, con especificación individual creada **solo al habilitarla** en `docs/c33/C33_XX_ESPECIFICACION.md`. Antes de escribir: repositorio, dev, skill, maestro, matriz, dependencias y riesgos. Evidencia de compilación real, pruebas positivas y negativas, auditorías A (requisitos), B (seguridad/tenancy), C (arquitectura/performance/UX), PR, diff, secreto y SHA, CI final y CI posterior, acta, reversión y cero P0/P1. Una falla o prueba requerida no ejecutada bloquea el avance. No VPS, pagos reales ni modificaciones SOLQARYN.

Aunque se cierren 11/11, la certificación integral C33 requiere 3 auditorías de capítulo y aceptación expresa antes de habilitar C34. Los bloqueos de integración de terceros se conservan hasta autorización futura y pruebas reales, sin fingir certificación. Estado: PENDIENTE.\n
## Ampliación comercial COM-001..COM-014 — planificación condicionada (10-11-2026)

**Estado: propuesta documental / no implementada / no certificada.** La ampliación deriva del expediente `docs/ANALISIS_IMPACTO_MODELO_COMERCIAL_2026-11-10.md` y de la edición comercial preparada del único Documento Maestro; **esa edición DOCX/PDF todavía debe incorporarse y verificarse con su SHA en GitHub antes de aprobar el plan como normativo**. Se conserva el número de microfases, la puerta de entrada secuencial y toda evidencia histórica. No se habilita código de C33.

- **Requisitos relacionados:** COM-001..COM-014 (impacto de datos).
- **Microfases técnicas a revisar:** C33-01, C33-02, C33-03, C33-04, C33-08, C33-09, C33-10 y C33-11 (no equivalen a puertas normativas `MAE-C33-xx`).
- **Evidencia positiva futura exigida:** Modelar entidades normalizadas para dirección, plan, oferta, precio/versión, contrato/aceptación, suscripción, ciclo, factura, pago/conciliación, campaña/ubicación y auditoría de plataforma.
- **Negativos obligatorios:** Sin JSON libre que sustituya relaciones; DECIMAL y UTC; no SQL de tenant al catálogo global; FK e índices por ámbito; pagos no se asocian a empresa distinta; historia inmutable de contrato y precio.
- **Pruebas propuestas:** COM-T01, COM-T04, COM-T07, COM-T08, COM-T16 y COM-T17; `DEFINIDAS / NO EJECUTADAS`.
- **Gate:** revisar contradicciones y versión exacta del Word, políticas comerciales y jurídicas pendientes; pruebas reales + auditorías A/B/C + acta humana de C33 cuando el capítulo esté habilitado, sin cobros ni despliegues productivos en esta etapa.
