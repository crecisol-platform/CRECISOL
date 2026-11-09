# C36 · Matriz adicional de pruebas transversales — propuestas de microfases

**BORRADOR DE PLANIFICACIÓN, NO IMPLEMENTADO NI CERTIFICADO.** Deriva de títulos de `docs/PLAN_37_CAPITULOS.md` y políticas transversales CRECISOL. Antes de activar el capítulo, cotejar el texto completo del Documento Maestro Único identificado por `docs/MASTER_REFERENCE.md` y anexos, extraer requisitos por ID y revisar el plan por PR. Prohibido tratar este archivo como maestro alternativo.

**Entrada:** C35 certificado, acta y aceptación expresa. **Riesgo rector:** Pruebas negativas integrales de arquitectura/seguridad.

## 10 microfases propuestas

| Microfase | Entregable | Evidencia positiva exigida | Test negativo / NO GO |
|---|---|---|---|
| C36-01 | Extraer la matriz original completa | Cada caso del Word con ID y alcance | No omitir controles desconocidos |
| C36-02 | Matriz de actores y contextos | Tenant A/B, branch, recurso, perfiles | No fiarse de selector UI |
| C36-03 | Abuso de identidad y autorización | JWT/cookie, MFA, BOLA/IDOR, replay | No acceso horizontal |
| C36-04 | Caché, jobs y eventos | Aislamiento de keys y outbox idempotente | No contaminación o doble ejecución |
| C36-05 | Archivos, previews y respaldos | URLs, ownership, escaneo, restore | No fuga cross-tenant |
| C36-06 | Concurrencia y estados | Cotización, inventario, pagos sintéticos | No doble cierre/cargo |
| C36-07 | Rendimiento, resiliencia y fallos | Carga, timeouts, reinicio y rollback | No marcar PASS con timeout |
| C36-08 | UI, accesibilidad y seguridad browser | WCAG automatizado más revisión manual delimitada | No inventar prueba humana |
| C36-09 | Mapa de evidencias y defectos | Reporte por requisito, SHA, CI, estado | No ocultar deuda P0/P1 |
| C36-10 | Auditorías integrales y acta C36 | A/B/C y aceptación formal | No habilitar C37 con matriz incompleta |

## Puerta de microfase y capítulo

Implementar una microfase por vez, con especificación individual creada **solo al habilitarla** en `docs/c36/C36_XX_ESPECIFICACION.md`. Antes de escribir: repositorio, dev, skill, maestro, matriz, dependencias y riesgos. Evidencia de compilación real, pruebas positivas y negativas, auditorías A (requisitos), B (seguridad/tenancy), C (arquitectura/performance/UX), PR, diff, secreto y SHA, CI final y CI posterior, acta, reversión y cero P0/P1. Una falla o prueba requerida no ejecutada bloquea el avance. No VPS, pagos reales ni modificaciones SOLQARYN.

Aunque se cierren 10/10, la certificación integral C36 requiere 3 auditorías de capítulo y aceptación expresa antes de habilitar C37. Los bloqueos de integración de terceros se conservan hasta autorización futura y pruebas reales, sin fingir certificación. Estado: PENDIENTE.
