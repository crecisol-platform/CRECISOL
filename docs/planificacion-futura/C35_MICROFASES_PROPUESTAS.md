# C35 · Topología definitiva de plataformas y despliegues — propuestas de microfases

**BORRADOR DE PLANIFICACIÓN, NO IMPLEMENTADO NI CERTIFICADO.** Deriva de títulos de `docs/PLAN_37_CAPITULOS.md` y políticas transversales CRECISOL. Antes de activar el capítulo, cotejar el texto completo del Documento Maestro Único identificado por `docs/MASTER_REFERENCE.md` y anexos, extraer requisitos por ID y revisar el plan por PR. Prohibido tratar este archivo como maestro alternativo.

**Entrada:** C34 certificado, acta y aceptación expresa. **Riesgo rector:** Diseño de topología; acciones productivas explícitamente bloqueadas.

## 8 microfases propuestas

| Microfase | Entregable | Evidencia positiva exigida | Test negativo / NO GO |
|---|---|---|---|
| C35-01 | Mapa final de componentes y dominios | Fronteras privadas/públicas documentadas | No confundir CRECISOL con SOLQARYN |
| C35-02 | Resolución de ambientes y secretos | Esquemas de config sin valores | No secretos en repositorio |
| C35-03 | Redes, proxy y certificados diseñados | Pruebas locales de hosts y TLS simulados | No afirmar TLS real |
| C35-04 | Estrategia de DB shared/dedicated | Isolación, backups y failover por tenant | No traslado cruzado |
| C35-05 | Plan de contenedores y orquestación | Build local y límites de recursos | No deployment en VPS |
| C35-06 | Rendimiento y capacidad simulados | Sizing desde benchmarks y cuotas | No presentar estimaciones como producción |
| C35-07 | Recuperación, rollback y cutover | Runbooks de incidente y dry-run local | No modificación DNS/Hostinger |
| C35-08 | Auditorías integrales y acta C35 | A/B/C y decisión explícita sobre pendientes | No certificar despliegues no autorizados |

## Puerta de microfase y capítulo

Implementar una microfase por vez, con especificación individual creada **solo al habilitarla** en `docs/c35/C35_XX_ESPECIFICACION.md`. Antes de escribir: repositorio, dev, skill, maestro, matriz, dependencias y riesgos. Evidencia de compilación real, pruebas positivas y negativas, auditorías A (requisitos), B (seguridad/tenancy), C (arquitectura/performance/UX), PR, diff, secreto y SHA, CI final y CI posterior, acta, reversión y cero P0/P1. Una falla o prueba requerida no ejecutada bloquea el avance. No VPS, pagos reales ni modificaciones SOLQARYN.

Aunque se cierren 8/8, la certificación integral C35 requiere 3 auditorías de capítulo y aceptación expresa antes de habilitar C36. Los bloqueos de integración de terceros se conservan hasta autorización futura y pruebas reales, sin fingir certificación. Estado: PENDIENTE.
