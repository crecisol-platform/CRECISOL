# C37 · Referencias normativas y técnicas — propuestas de microfases

**BORRADOR DE PLANIFICACIÓN, NO IMPLEMENTADO NI CERTIFICADO.** Deriva de títulos de `docs/PLAN_37_CAPITULOS.md` y políticas transversales CRECISOL. Antes de activar el capítulo, cotejar el texto completo del Documento Maestro Único identificado por `docs/MASTER_REFERENCE.md` y anexos, extraer requisitos por ID y revisar el plan por PR. Prohibido tratar este archivo como maestro alternativo.

**Entrada:** C36 certificado, acta y aceptación expresa. **Riesgo rector:** Versiones y fuentes originales verificadas; no sustituir maestro.

## 8 microfases propuestas

| Microfase | Entregable | Evidencia positiva exigida | Test negativo / NO GO |
|---|---|---|---|
| C37-01 | Inventario de referencias del maestro | Fuentes, fecha, autoridad y uso registrados | No inventar enlaces ni versiones |
| C37-02 | Verificación de tecnologías soportadas | Versiones Angular/.NET/MySQL/Node con evidencia | No declarar compatibilidad sin ejecución |
| C37-03 | Referencias de seguridad y privacidad | Fuentes oficiales y contexto regional | No afirmar equivalencia normativa automática |
| C37-04 | Referencias de accesibilidad y UX | WCAG y criterios automatizados/manuales separados | No declarar cumplimiento integral sin pruebas |
| C37-05 | Referencias de APIs y protocolos | Normas y compatibilidad con pruebas | No usar borradores como estándar definitivo |
| C37-06 | Registro de referencias externas Skills | Verificar origen, SHA y ruta exacta si aplica | No instalar/copiar/ejecutar sin permiso |
| C37-07 | Evaluación de obsolescencia y licencias | Matriz de riesgos, alternativas y ADR | No introducir paquetes no aprobados |
| C37-08 | Auditorías finales y acta C37 | A/B/C, aceptación del capítulo y cierre programático | No declarar 37/37 sin actas y evidencia acumulada |

## Puerta de microfase y capítulo

Implementar una microfase por vez, con especificación individual creada **solo al habilitarla** en `docs/c37/C37_XX_ESPECIFICACION.md`. Antes de escribir: repositorio, dev, skill, maestro, matriz, dependencias y riesgos. Evidencia de compilación real, pruebas positivas y negativas, auditorías A (requisitos), B (seguridad/tenancy), C (arquitectura/performance/UX), PR, diff, secreto y SHA, CI final y CI posterior, acta, reversión y cero P0/P1. Una falla o prueba requerida no ejecutada bloquea el avance. No VPS, pagos reales ni modificaciones SOLQARYN.

Aunque se cierren 8/8, la certificación integral C37 requiere 3 auditorías de capítulo y aceptación expresa antes de cierre del programa de 37 capítulos, condicionado a todas las actas y aceptación final. Los bloqueos de integración de terceros se conservan hasta autorización futura y pruebas reales, sin fingir certificación. Estado: PENDIENTE.\n
## Ampliación comercial COM-001..COM-014 — dependencia transversal (10-11-2026)

**Solo planificación; NO IMPLEMENTADO / NO CERTIFICADO.** Requisitos derivados de `docs/ANALISIS_IMPACTO_MODELO_COMERCIAL_2026-11-10.md` y de una actualización del Word maestro **todavía pendiente de incorporación y comprobación de huellas en GitHub**; esta nota no sustituye el Word ni habilita C37.

- **COM:** COM-006, COM-007, COM-008, COM-011, COM-014.
- **Microfases a revisar sin renumerar:** C37-01, C37-03, C37-04, C37-05, C37-07 y C37-08.
- **Aceptación futura:** Verificar fuentes oficiales vigentes sobre contratación SaaS, cancelación electrónica, renovación, información de precios, privacidad, publicidad y comprobantes; registrar citas exactas al adoptar reglas.
- **Negativos:** No trasladar automáticamente prohibiciones sectoriales de telecomunicaciones a SaaS B2B ni afirmar validez de penalidades sin revisión jurídica.
- **Pruebas definidas y NO ejecutadas:** COM-T11, COM-T12 y COM-T17.
- **Gate:** Word aprobado y verificable, capítulo precedente certificado, A/B/C, CI y acta propia. Sin datos productivos, despliegues ni pagos reales.
